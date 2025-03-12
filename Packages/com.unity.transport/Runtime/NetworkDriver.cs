using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport.Protocols;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Networking.Transport.Error;
using Unity.Networking.Transport.Logging;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// The <c>NetworkDriver</c> is the main API with which users interact with the Unity Transport
    /// package. It can be thought of as a socket with extra features. Refer to the manual for
    /// examples of how to use this API.
    /// </summary>
    public struct NetworkDriver : IDisposable
    {
        /// <summary>Create a <see cref="Concurrent"/> copy of the <c>NetworkDriver</c>.</summary>
        /// <returns>A <see cref="Concurrent"/> instance for the driver.</returns>
        public Concurrent ToConcurrent()
        {
            if (!IsCreated)
                return default;

            return new Concurrent
            {
                m_EventQueue = m_EventQueue.ToConcurrent(),
                m_ConnectionList = m_NetworkStack.Connections,
                m_PipelineProcessor = m_PipelineProcessor.ToConcurrent(),
                m_DefaultHeaderFlags = m_DefaultHeaderFlags,
                m_DriverSender = m_DriverSender.ToConcurrent(),
                m_DriverReceiver = m_DriverReceiver,
                m_PacketPadding = m_NetworkStack.PacketPadding,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_ThreadIndex = 0,
                m_PendingBeginSend = m_PendingBeginSend
#endif
            };
        }

        private Concurrent ToConcurrentSendOnly()
        {
            return new Concurrent
            {
                m_EventQueue = default,
                m_ConnectionList = m_NetworkStack.Connections,
                m_PipelineProcessor = m_PipelineProcessor.ToConcurrent(),
                m_DefaultHeaderFlags = m_DefaultHeaderFlags,
                m_DriverSender = m_DriverSender.ToConcurrent(),
                m_DriverReceiver = m_DriverReceiver,
                m_PacketPadding = m_NetworkStack.PacketPadding,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_ThreadIndex = 0,
                m_PendingBeginSend = m_PendingBeginSend
#endif
            };
        }

        /// <summary>
        /// Structure that can be used to access a <c>NetworkDriver</c> from multiple jobs. Only a
        /// subset of operations are supported because not all operations are safe to perform
        /// concurrently. Must be obtained with the <see cref="ToConcurrent"/> method.
        /// </summary>
        public struct Concurrent
        {
            /// <inheritdoc cref="NetworkDriver.PopEventForConnection(NetworkConnection, out DataStreamReader)"/>
            public NetworkEvent.Type PopEventForConnection(NetworkConnection connection, out DataStreamReader reader)
            {
                return PopEventForConnection(connection, out reader, out var _);
            }

            /// <inheritdoc cref="NetworkDriver.PopEventForConnection(NetworkConnection, out DataStreamReader, out NetworkPipeline)"/>
            public NetworkEvent.Type PopEventForConnection(NetworkConnection connection, out DataStreamReader reader, out NetworkPipeline pipe)
            {
                pipe = default;

                reader = default;
                if (m_ConnectionList.ConnectionAt(connection.InternalId) != connection.ConnectionId)
                    return (int)NetworkEvent.Type.Empty;

                var type = m_EventQueue.PopEventForConnection(connection.InternalId, out var offset, out var size, out var pipelineId);
                pipe = new NetworkPipeline { Id = pipelineId };

                if (size > 0)
                    reader = new DataStreamReader(m_DriverReceiver.GetDataStreamSubArray(offset, size));

                return type;
            }

            /// <inheritdoc cref="NetworkDriver.MaxHeaderSize"/>
            public int MaxHeaderSize(NetworkPipeline pipe)
            {
                var headerSize = m_PipelineProcessor.m_MaxPacketHeaderSize;
                if (pipe.Id > 0)
                {
                    // All headers plus one byte for pipeline id
                    headerSize += m_PipelineProcessor.SendHeaderCapacity(pipe) + 1;
                }

                return headerSize;
            }

            internal struct PendingSend
            {
                public NetworkPipeline Pipeline;
                public NetworkConnection Connection;
                public NetworkInterfaceSendHandle SendHandle;
                public int headerSize;
            }

            /// <inheritdoc cref="NetworkDriver.BeginSend(NetworkConnection, out DataStreamWriter, int)"/>
            public unsafe int BeginSend(NetworkConnection connection, out DataStreamWriter writer, int requiredPayloadSize = 0)
            {
                return BeginSend(NetworkPipeline.Null, connection, out writer, requiredPayloadSize);
            }

            /// <inheritdoc cref="NetworkDriver.BeginSend(NetworkPipeline, NetworkConnection, out DataStreamWriter, int)"/>
            public unsafe int BeginSend(NetworkPipeline pipe, NetworkConnection connection, out DataStreamWriter writer, int requiredPayloadSize = 0)
            {
                writer = default;

                if (connection.InternalId < 0 || connection.InternalId >= m_ConnectionList.Count)
                    return (int)Error.StatusCode.NetworkIdMismatch;

                var c = m_ConnectionList.ConnectionAt(connection.InternalId);
                if (c.Version != connection.Version)
                    return (int)Error.StatusCode.NetworkVersionMismatch;

                if (m_ConnectionList.GetConnectionState(c) != NetworkConnection.State.Connected)
                    return (int)Error.StatusCode.NetworkStateMismatch;

                var maxMessageSize = m_DriverSender.m_SendQueue.PayloadCapacity;

                var pipelineHeader = (pipe.Id > 0) ? m_PipelineProcessor.SendHeaderCapacity(pipe) + 1 : 0;
                var pipelinePayloadCapacity = m_PipelineProcessor.PayloadCapacity(pipe);

                // If the pipeline doesn't have an explicity payload capacity, then use whatever
                // will fit inside the MTU (considering protocol and pipeline overhead). If there is
                // an explicity pipeline payload capacity we use that directly. Right now only
                // fragmented pipelines have an explicity capacity, and we want users to be able to
                // rely on this configured value.
                var payloadCapacity = pipelinePayloadCapacity == 0
                    ? maxMessageSize - m_PacketPadding - pipelineHeader
                    : pipelinePayloadCapacity;

                // Total capacity is the full size of the buffer we'll allocate. Without an explicit
                // pipeline payload capacity, this is the MTU. Otherwise it's the pipeline payload
                // capacity plus whatever overhead we need to transmit the packet.
                var totalCapacity = pipelinePayloadCapacity == 0
                    ? maxMessageSize
                    : pipelinePayloadCapacity + m_PacketPadding + pipelineHeader;

                // Check if we can accomodate the user's required payload size.
                if (payloadCapacity < requiredPayloadSize)
                {
                    return (int)Error.StatusCode.NetworkPacketOverflow;
                }

                // Allocate less memory if user doesn't require our full capacity.
                if (requiredPayloadSize > 0 && payloadCapacity > requiredPayloadSize)
                {
                    var extraCapacity = payloadCapacity - requiredPayloadSize;
                    payloadCapacity -= extraCapacity;
                    totalCapacity -= extraCapacity;
                }

                var sendHandle = default(NetworkInterfaceSendHandle);
                if (totalCapacity > maxMessageSize)
                {
                    sendHandle.data = (IntPtr)UnsafeUtility.Malloc(totalCapacity, 8, Allocator.Temp);
                    sendHandle.capacity = totalCapacity;
                    sendHandle.id = 0;
                    sendHandle.size = 0;
                    sendHandle.flags = SendHandleFlags.AllocatedByDriver;
                }
                else
                {
                    var result = 0;
                    if ((result = m_DriverSender.BeginSend(out sendHandle, (uint)totalCapacity)) != 0)
                    {
                        return result;
                    }
                }

                if (sendHandle.capacity < totalCapacity)
                    return (int)Error.StatusCode.NetworkPacketOverflow;

                var slice = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>((byte*)sendHandle.data + m_PacketPadding + pipelineHeader, payloadCapacity, Allocator.Invalid);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var safety = AtomicSafetyHandle.GetTempMemoryHandle();
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref slice, safety);
#endif
                writer = new DataStreamWriter(slice);
                writer.m_SendHandleData = (IntPtr)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<PendingSend>(), UnsafeUtility.AlignOf<PendingSend>(), Allocator.Temp);
                *(PendingSend*)writer.m_SendHandleData = new PendingSend
                {
                    Pipeline = pipe,
                    Connection = connection,
                    SendHandle = sendHandle,
                    headerSize = m_PacketPadding,
                };
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_PendingBeginSend[m_ThreadIndex * JobsUtility.CacheLineSize / 4] = m_PendingBeginSend[m_ThreadIndex * JobsUtility.CacheLineSize / 4] + 1;
#endif
                return (int)Error.StatusCode.Success;
            }

            /// <inheritdoc cref="NetworkDriver.EndSend"/>
            public unsafe int EndSend(DataStreamWriter writer)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // Just here to trigger a safety check on the writer
                if (writer.Capacity == 0)
                    throw new InvalidOperationException("EndSend without matching BeginSend");
#endif
                PendingSend* pendingSendPtr = (PendingSend*)writer.m_SendHandleData;
                if (pendingSendPtr == null || pendingSendPtr->Connection == default)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    throw new InvalidOperationException("EndSend without matching BeginSend");
#else
                    return (int)Error.StatusCode.NetworkSendHandleInvalid;
#endif
                }

                if (m_ConnectionList.ConnectionAt(pendingSendPtr->Connection.InternalId).Version != pendingSendPtr->Connection.Version)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    throw new InvalidOperationException("Connection closed between begin and end send");
#else
                    return (int)Error.StatusCode.NetworkVersionMismatch;
#endif
                }

                if (writer.HasFailedWrites)
                {
                    AbortSend(writer);
                    // DataStreamWriter can only have failed writes if we overflow its capacity.
                    return (int)Error.StatusCode.NetworkPacketOverflow;
                }

                PendingSend pendingSend = *(PendingSend*)writer.m_SendHandleData;
                pendingSendPtr->Connection = default;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_PendingBeginSend[m_ThreadIndex * JobsUtility.CacheLineSize / 4] = m_PendingBeginSend[m_ThreadIndex * JobsUtility.CacheLineSize / 4] - 1;
#endif

                pendingSend.SendHandle.size = pendingSend.headerSize + writer.Length;
                int retval = 0;
                if (pendingSend.Pipeline.Id > 0)
                {
                    pendingSend.SendHandle.size += m_PipelineProcessor.SendHeaderCapacity(pendingSend.Pipeline) + 1;
                    var oldHeaderFlags = m_DefaultHeaderFlags;
                    m_DefaultHeaderFlags = UdpCHeader.HeaderFlags.HasPipeline;
                    retval = m_PipelineProcessor.Send(this, pendingSend.Pipeline, pendingSend.Connection, pendingSend.SendHandle, pendingSend.headerSize);
                    m_DefaultHeaderFlags = oldHeaderFlags;
                }
                else
                    // TODO: Is there a better way we could set the hasPipeline value correctly?
                    // this case is when the message is sent from the pipeline directly, "without a pipeline" so the hasPipeline flag is set in m_DefaultHeaderFlags
                    // allowing us to capture it here
                    retval = CompleteSend(pendingSend.Connection, pendingSend.SendHandle, (m_DefaultHeaderFlags & UdpCHeader.HeaderFlags.HasPipeline) != 0);
                if (retval <= 0)
                    return retval;
                return writer.Length;
            }

            /// <inheritdoc cref="NetworkDriver.AbortSend"/>
            public unsafe void AbortSend(DataStreamWriter writer)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // Just here to trigger a safety check on the writer
                if (writer.Capacity == 0)
                    throw new InvalidOperationException("EndSend without matching BeginSend");
#endif
                PendingSend* pendingSendPtr = (PendingSend*)writer.m_SendHandleData;
                if (pendingSendPtr == null || pendingSendPtr->Connection == default)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    throw new InvalidOperationException("AbortSend without matching BeginSend");
#else
                    DebugLog.LogError("AbortSend without matching BeginSend");
                    return;
#endif
                }
                PendingSend pendingSend = *(PendingSend*)writer.m_SendHandleData;
                pendingSendPtr->Connection = default;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_PendingBeginSend[m_ThreadIndex * JobsUtility.CacheLineSize / 4] = m_PendingBeginSend[m_ThreadIndex * JobsUtility.CacheLineSize / 4] - 1;
#endif
                AbortSend(pendingSend.SendHandle);
            }

            internal unsafe int CompleteSend(NetworkConnection sendConnection, NetworkInterfaceSendHandle sendHandle, bool hasPipeline)
            {
                if (0 != (sendHandle.flags & SendHandleFlags.AllocatedByDriver))
                {
                    var ret = 0;
                    var originalHandle = sendHandle;
                    var maxMessageSize = m_DriverSender.m_SendQueue.PayloadCapacity;
                    if ((ret = m_DriverSender.BeginSend(out sendHandle, (uint)math.max(maxMessageSize, originalHandle.size))) != 0)
                    {
                        return ret;
                    }
                    UnsafeUtility.MemCpy((void*)sendHandle.data, (void*)originalHandle.data, originalHandle.size);
                    sendHandle.size = originalHandle.size;
                }

                var endpoint = m_ConnectionList.GetConnectionEndpoint(sendConnection.ConnectionId);
                sendHandle.size -= m_PacketPadding;
                var result = m_DriverSender.EndSend(ref endpoint, ref sendHandle, m_PacketPadding, sendConnection.ConnectionId);

                // TODO: We temporarily add always a pipeline id (even if it's 0) when using new Layers
                if (!hasPipeline)
                {
                    var packetProcessor = m_DriverSender.m_SendQueue.GetPacketProcessor(sendHandle.id);
                    packetProcessor.PrependToPayload((byte)0);
                }
                return result;
            }

            internal void AbortSend(NetworkInterfaceSendHandle sendHandle)
            {
                if (0 == (sendHandle.flags & SendHandleFlags.AllocatedByDriver))
                {
                    m_DriverSender.AbortSend(ref sendHandle);
                }
            }

            /// <inheritdoc cref="NetworkDriver.GetConnectionState"/>
            public NetworkConnection.State GetConnectionState(NetworkConnection id)
            {
                if (id.InternalId < 0 || id.InternalId >= m_ConnectionList.Count)
                    return NetworkConnection.State.Disconnected;
                var connection = m_ConnectionList.ConnectionAt(id.InternalId);
                if (connection.Version != id.Version)
                    return NetworkConnection.State.Disconnected;

                var state = m_ConnectionList.GetConnectionState(connection);
                return state == NetworkConnection.State.Disconnecting ? NetworkConnection.State.Disconnected : state;
            }

            internal NetworkEventQueue.Concurrent m_EventQueue;

            [ReadOnly] internal ConnectionList m_ConnectionList;
            internal NetworkPipelineProcessor.Concurrent m_PipelineProcessor;
            internal UdpCHeader.HeaderFlags m_DefaultHeaderFlags;
            internal NetworkDriverSender.Concurrent m_DriverSender;
            [ReadOnly] internal NetworkDriverReceiver m_DriverReceiver;
            internal int m_PacketPadding;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            [NativeSetThreadIndex] internal int m_ThreadIndex;
            [NativeDisableParallelForRestriction] internal NativeArray<int> m_PendingBeginSend;
#endif
        }

        // internal variables :::::::::::::::::::::::::::::::::::::::::::::::::

        internal NetworkStack m_NetworkStack;

        NetworkDriverSender m_DriverSender;
        NetworkDriverReceiver m_DriverReceiver;

        internal NetworkDriverReceiver Receiver => m_DriverReceiver;
        internal NetworkEventQueue EventQueue => m_EventQueue;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        NativeArray<int> m_PendingBeginSend;
#endif

        NetworkEventQueue m_EventQueue;

        private struct InternalState
        {
            public long LastUpdateTime;
            public long UpdateTimeAdjustment;
            public bool Bound;
            public bool Listening;
        }

        [NativeDisableContainerSafetyRestriction]
        private NativeReference<InternalState> m_InternalState;

        private NetworkPipelineProcessor m_PipelineProcessor;
        private UdpCHeader.HeaderFlags m_DefaultHeaderFlags;

        [ReadOnly] internal NetworkSettings m_NetworkSettings;

        /// <summary>Current settings used by the driver.</summary>
        /// <remarks>
        /// Current settings are read-only and can't be modified except through methods like
        /// <see cref="SimulatorStageParameterExtensions.ModifySimulatorStageParameters"/>.
        /// </remarks>
        public NetworkSettings CurrentSettings => m_NetworkSettings.AsReadOnly();

        /// <summary>
        /// Whether the driver has been bound to an endpoint with the <see cref="Bind"/> method.
        /// Binding to an endpoint is a prerequisite to listening to new connections (with the
        /// <see cref="Listen"/> method). It is also a prerequiste to making new connections, but
        /// the <see cref="Connect"/> method will automatically bind the driver to the wildcard
        /// address if it's not already bound.
        /// </summary>
        /// <value>True if the driver is bound, false otherwise.</value>
        public bool Bound
        {
            get => m_InternalState.Value.Bound;
            private set
            {
                var state = m_InternalState.Value;
                state.Bound = value;
                m_InternalState.Value = state;
            }
        }

        /// <summary>
        /// Whether the driver is listening for new connections (e.g. acting like a server). Use
        /// the <see cref="Listen"/> method to start listening for new connections.
        /// </summary>
        /// <value>True if the driver is listening, false otherwise.</value>
        public bool Listening
        {
            get => m_InternalState.Value.Listening;
            private set
            {
                var state = m_InternalState.Value;
                state.Listening = value;
                m_InternalState.Value = state;
            }
        }

        internal long LastUpdateTime => m_InternalState.Value.LastUpdateTime;

        internal int PipelineCount => m_PipelineProcessor.PipelineCount;

        /// <summary>
        /// Create a new <c>NetworkDriver</c> with custom settings.
        /// </summary>
        /// <param name="settings">Configuration for the driver.</param>
        /// <returns>Newly-constructed driver.</returns>
        public static NetworkDriver Create(NetworkSettings settings)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Create(new IPCNetworkInterface(), settings);
#else
            return Create(new UDPNetworkInterface(), settings);
#endif
        }

        /// <summary>
        /// Create a new <c>NetworkDriver</c> with default settings.
        /// </summary>
        /// <returns>Newly-constructed driver.</returns>
        public static NetworkDriver Create() => Create(new NetworkSettings(Allocator.Temp));

        /// <summary>
        /// Create a new <c>NetworkDriver</c> with a custom network interface.
        /// </summary>
        /// <typeparam name="N">Type of the network interface to use.</typeparam>
        /// <param name="networkInterface">Instance of the custom network interface.</param>
        /// <returns>Newly-constructed driver.</returns>
        public static NetworkDriver Create<N>(N networkInterface) where N : unmanaged, INetworkInterface
            => Create(ref networkInterface);

        /// <inheritdoc cref="NetworkDriver.Create{N}(N)"/>
        public static NetworkDriver Create<N>(ref N networkInterface) where N : unmanaged, INetworkInterface
            => Create(ref networkInterface, new NetworkSettings(Allocator.Temp));

        /// <summary>
        /// Create a new <c>NetworkDriver</c> with a custom network interface and custom settings.
        /// </summary>
        /// <typeparam name="N">Type of the network interface to use.</typeparam>
        /// <param name="networkInterface">Instance of the custom network interface.</param>
        /// <param name="settings">Configuration for the driver.</param>
        /// <returns>Newly-constructed driver.</returns>
        public static NetworkDriver Create<N>(N networkInterface, NetworkSettings settings) where N : unmanaged, INetworkInterface
            => Create(ref networkInterface, settings);

        /// <inheritdoc cref="NetworkDriver.Create{N}(N, NetworkSettings)"/>
        public static NetworkDriver Create<N>(ref N networkInterface, NetworkSettings settings) where N : unmanaged, INetworkInterface
        {
            var driver = default(NetworkDriver);

            driver.m_NetworkSettings = new NetworkSettings(settings, Allocator.Persistent);

            var networkParams = settings.GetNetworkConfigParameters();
#if !UNITY_WEBGL || UNITY_EDITOR
            // Legacy support for baselib queue capacity parameters
            #pragma warning disable 618
            if (settings.TryGet<BaselibNetworkParameter>(out var baselibParameter))
            {
                if (networkParams.sendQueueCapacity == NetworkParameterConstants.SendQueueCapacity &&
                    networkParams.receiveQueueCapacity == NetworkParameterConstants.ReceiveQueueCapacity)
                {
                    networkParams.sendQueueCapacity = baselibParameter.sendQueueCapacity;
                    networkParams.receiveQueueCapacity = baselibParameter.receiveQueueCapacity;
                    driver.m_NetworkSettings.AddRawParameterStruct(ref networkParams);
                }
            }
            #pragma warning restore 618
#endif
            NetworkStack.InitializeForSettings(out driver.m_NetworkStack, ref networkInterface, ref settings, out var sendQueue, out var receiveQueue);

            driver.m_PipelineProcessor = new NetworkPipelineProcessor(settings, driver.m_NetworkStack.PacketPadding);

            driver.m_DriverSender = new NetworkDriverSender(sendQueue);
            driver.m_DriverReceiver = new NetworkDriverReceiver(receiveQueue);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            driver.m_PendingBeginSend = new NativeArray<int>(JobsUtility.MaxJobThreadCount * JobsUtility.CacheLineSize / 4, Allocator.Persistent);
#endif

            driver.m_DefaultHeaderFlags = 0;

            driver.m_EventQueue = new NetworkEventQueue(NetworkParameterConstants.InitialEventQueueSize);

            var time = TimerHelpers.GetCurrentTimestampMS();
            var state = new InternalState
            {
                LastUpdateTime = networkParams.fixedFrameTimeMS > 0 ? 1 : time,
                UpdateTimeAdjustment = 0,
                Bound = false,
                Listening = false,
            };

            driver.m_InternalState = new NativeReference<InternalState>(state, Allocator.Persistent);

            return driver;
        }

        /// <summary>Use <see cref="Create"/> to construct <c>NetworkDriver</c> instances.</summary>
        [Obsolete("Use NetworkDriver.Create(INetworkInterface networkInterface) instead.", true)]
        public NetworkDriver(INetworkInterface netIf)
            => throw new NotImplementedException();

        /// <summary>Use <see cref="Create"/> to construct <c>NetworkDriver</c> instances.</summary>
        [Obsolete("Use NetworkDriver.Create(INetworkInterface networkInterface, NetworkSettings settings) instead.", true)]
        public NetworkDriver(INetworkInterface netIf, NetworkSettings settings)
            => throw new NotImplementedException();

        // interface implementation :::::::::::::::::::::::::::::::::::::::::::
        public void Dispose()
        {
            if (!IsCreated)
                return;

            m_NetworkStack.Dispose();

            m_DriverSender.Dispose();
            m_DriverReceiver.Dispose();

            m_NetworkSettings.Dispose();

            m_PipelineProcessor.Dispose();

            m_EventQueue.Dispose();

            m_InternalState.Dispose();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_PendingBeginSend.Dispose();
#endif
        }

        /// <summary>Whether the driver is been correctly created.</summary>
        /// <value>True if correctly created, false otherwise.</value>
        public bool IsCreated => m_InternalState.IsCreated;

        [BurstCompile]
        struct UpdateJob : IJob
        {
            public NetworkDriver driver;

            public void Execute()
            {
                driver.InternalUpdate();
            }
        }

        [BurstCompile]
        struct ClearEventQueue : IJob
        {
            public NetworkEventQueue eventQueue;
            public NetworkDriverReceiver driverReceiver;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            public NativeArray<int> pendingSend;
            [ReadOnly] public ConnectionList connectionList;
            public long listenState;
#endif
            public void Execute()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                for (int i = 0; i < connectionList.Count; ++i)
                {
                    int conCount = eventQueue.GetCountForConnection(i);
                    if (conCount != 0 && connectionList.GetConnectionState(connectionList.ConnectionAt(i)) != NetworkConnection.State.Disconnected)
                    {
                        DebugLog.ErrorResetNotEmptyEventQueue(conCount, i, listenState);
                    }
                }
                bool didPrint = false;
                for (int i = 0; i < JobsUtility.MaxJobThreadCount; ++i)
                {
                    if (pendingSend[i * JobsUtility.CacheLineSize / 4] > 0)
                    {
                        pendingSend[i * JobsUtility.CacheLineSize / 4] = 0;
                        if (!didPrint)
                        {
                            DebugLog.LogError("Missing EndSend, calling BeginSend without calling EndSend will result in a memory leak");
                            didPrint = true;
                        }
                    }
                }
#endif
                eventQueue.Clear();
                driverReceiver.ClearStream();
            }
        }

        private void UpdateLastUpdateTime()
        {
            var state = m_InternalState.Value;
            var networkParams = m_NetworkSettings.GetNetworkConfigParameters();

            long now = networkParams.fixedFrameTimeMS > 0
                ? LastUpdateTime + networkParams.fixedFrameTimeMS
                : TimerHelpers.GetCurrentTimestampMS() - state.UpdateTimeAdjustment;

            long frameTime = now - LastUpdateTime;
            if (networkParams.maxFrameTimeMS > 0 && frameTime > networkParams.maxFrameTimeMS)
            {
                state.UpdateTimeAdjustment += frameTime - networkParams.maxFrameTimeMS;
                now = LastUpdateTime + networkParams.maxFrameTimeMS;
            }

            state.LastUpdateTime = now;
            m_InternalState.Value = state;
        }

        /// <summary>
        /// Schedule an update job. This job will process incoming packets and check timeouts
        /// (queueing up the relevant events to be consumed by <see cref="PopEvent"/> and
        /// <see cref="Accept"/>) and will send any packets queued with <see cref="EndSend"/>. This
        /// job should generally be scheduled once per tick.
        /// </summary>
        /// <param name="dependency">Job to depend on.</param>
        /// <returns>Handle to the update job.</returns>
        public JobHandle ScheduleUpdate(JobHandle dependency = default)
        {
            UpdateLastUpdateTime();

            var updateJob = new UpdateJob {driver = this};

            // Clearing the event queue and receiving/sending data only makes sense if we're bound.
            if (Bound)
            {
                var connections = m_NetworkStack.Connections;

                var clearJob = new ClearEventQueue
                {
                    eventQueue = m_EventQueue,
                    driverReceiver = m_DriverReceiver,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    pendingSend = m_PendingBeginSend,
                    connectionList = connections,
                    listenState = Listening ? 1 : 0,
#endif
                };

                var handle = clearJob.Schedule(dependency);
                handle = updateJob.Schedule(handle);
                handle = m_NetworkStack.ScheduleReceive(ref m_DriverReceiver, ref connections, ref m_EventQueue, ref m_PipelineProcessor, LastUpdateTime, handle);
                handle = m_NetworkStack.ScheduleSend(ref m_DriverSender, LastUpdateTime, handle);

                return handle;
            }
            else
            {
                return updateJob.Schedule(dependency);
            }
        }

        /// <summary>
        /// Schedule a send job. This job is basically a subset of the update job (see
        /// <see cref="ScheduleUpdate"/>) and only takes care of sending packets queued with
        /// <see cref="EndSend"/>. It should be lightweight enough to schedule multiple times per
        /// tick to improve latency if there's a significant amount of packets being sent.
        /// </summary>
        /// <param name="dependency">Job to depend on.</param>
        /// <returns>Handle to the send job.</returns>
        public JobHandle ScheduleFlushSend(JobHandle dependency = default)
        {
            return Bound ? m_NetworkStack.ScheduleSend(ref m_DriverSender, LastUpdateTime, dependency) : dependency;
        }

        void InternalUpdate()
        {
            m_PipelineProcessor.Timestamp = LastUpdateTime;

            m_PipelineProcessor.UpdateReceive(ref this, out var updateCount);

            if (updateCount > m_NetworkStack.Connections.Count * 64)
            {
                DebugLog.DriverTooManyUpdates(updateCount);
            }

            m_DefaultHeaderFlags = UdpCHeader.HeaderFlags.HasPipeline;
            m_PipelineProcessor.UpdateSend(ToConcurrentSendOnly(), out updateCount);
            if (updateCount > m_NetworkStack.Connections.Count * 64)
            {
                DebugLog.DriverTooManyUpdates(updateCount);
            }

            m_DefaultHeaderFlags = 0;

            // Drop incoming connections if not listening. If we're bound but are not listening
            // (say because the user never called Listen or because they called StopListening),
            // clients can still establish connections and these connections will be perfectly
            // valid from their point of view, except that the server will never answer anything.
            if (!Listening)
            {
                ConnectionId connectionId;
                while ((connectionId = m_NetworkStack.Connections.AcceptConnection()) != default)
                {
                    Disconnect(new NetworkConnection(connectionId));
                }
            }
        }

        /// <summary>Register a custom pipeline stage.</summary>
        /// <remarks>
        /// <para>
        /// Can only be called before a driver is bound (see <see cref="Bind" />).
        /// </para>
        /// <para>
        /// Note that the default pipeline stages (<see cref="FragmentationPipelineStage" />,
        /// <see cref="ReliableSequencedPipelineStage" />, <see cref="UnreliableSequencedPipelineStage" />,
        /// and <see cref="SimulatorPipelineStage" />) don't need to be registered. Registering a
        /// pipeline stage is only required for custom ones.
        /// </para>
        /// </remarks>
        /// <typeparam name="T">Type of the pipeline stage (must be unmanaged).</typeparam>
        /// <param name="stage">An instance of the pipeline stage.</param>
        /// <exception cref="InvalidOperationException">
        /// If the driver is not created or bound. Note that this is only thrown if safety checks
        /// are enabled (i.e. in the editor). Otherwise the pipeline is registered anyway (with
        /// likely erroneous behavior down the line).
        /// </exception>
        public void RegisterPipelineStage<T>(T stage) where T : unmanaged, INetworkPipelineStage
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsCreated)
                throw new InvalidOperationException("Driver must be constructed before registering pipeline stages.");
            if (Bound)
                throw new InvalidOperationException("Can't register a pipeline stage after the driver is bound.");
#endif
            m_PipelineProcessor.RegisterPipelineStage<T>(stage, m_NetworkSettings);
        }

        /// <summary>Create a new pipeline from stage types.</summary>
        /// <remarks>
        /// The order of the different stages is important, as that is the order in which the stages
        /// will process a packet when sending messages (the reverse order is used when processing
        /// received packets).
        /// </remarks>
        /// <param name="stages">Array of stages the pipeline should contain.</param>
        /// <exception cref="InvalidOperationException">
        /// If called after the driver has established connections or before it is created. Note
        /// this is only thrown if safety checks are enabled (i.e. in the editor).
        /// </exception>
        public NetworkPipeline CreatePipeline(params Type[] stages)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsCreated)
                throw new InvalidOperationException("Driver must be constructed before creating pipelines.");
            if (m_NetworkStack.Connections.Count > 0)
                throw new InvalidOperationException("Pipelines can't be created after establishing connections.");
#endif
            var stageIds = new NativeArray<NetworkPipelineStageId>(stages.Length, Allocator.Temp);
            for (int i = 0; i < stages.Length; i++)
                stageIds[i] = NetworkPipelineStageId.Get(stages[i]);
            return CreatePipeline(stageIds);
        }

        /// <summary>Create a new pipeline from stage IDs.</summary>
        /// <remarks>
        /// <para>
        /// The order of the different stages is important, as that is the order in which the stages
        /// will process a packet when sending messages (the reverse order is used when processing
        /// received packets).
        /// </para>
        /// <para>
        /// Note that this method is Burst-compatible. Note also that no reference to the native
        /// array is kept internally by the driver. It is thus safe to dispose of it immediately
        /// after calling this method (or to use a temporary allocation for the array).
        /// </para>
        /// </remarks>
        /// <param name="stages">Array of stage IDs the pipeline should contain.</param>
        /// <exception cref="InvalidOperationException">
        /// If called after the driver has established connections or before it is created. Note
        /// this is only thrown if safety checks are enabled (i.e. in the editor).
        /// </exception>
        public NetworkPipeline CreatePipeline(NativeArray<NetworkPipelineStageId> stages)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsCreated)
                throw new InvalidOperationException("Driver must be constructed before creating pipelines.");
            if (m_NetworkStack.Connections.Count > 0)
                throw new InvalidOperationException("Pipelines can't be created after establishing connections.");
#endif
            return m_PipelineProcessor.CreatePipeline(stages);
        }

        /// <summary>
        /// Bind the driver to an endpoint. This endpoint would normally be a local IP address and
        /// port which the driver will use for its communications. Binding to a wildcard address
        /// (<see cref="NetworkEndpoint.AnyIpv4"/> or <see cref="NetworkEndpoint.AnyIpv6"/>) will
        /// result in the driver using any local address for its communications, and binding to port
        /// 0 will result in the driver using an ephemeral free port chosen by the OS.
        /// </summary>
        /// <param name="endpoint">Endpoint to bind to.</param>
        /// <returns>0 on success, negative error code on error.</returns>
        /// <exception cref="InvalidOperationException">
        /// If the driver is not created properly, if it's already bound, or if there are already
        /// connections made to the driver (although that shouldn't be possible). Note that these
        /// exceptions are only thrown if safety checks are enabled (i.e. in the editor).
        /// </exception>
        public int Bind(NetworkEndpoint endpoint)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsCreated)
                throw new InvalidOperationException(
                    "Driver must be constructed with a populated or empty INetworkParameter params list");
            // question: should this really be an error?
            if (Bound)
                throw new InvalidOperationException(
                    "Bind can only be called once per NetworkDriver");
            if (m_NetworkStack.Connections.Count > 0)
                throw new InvalidOperationException(
                    "Bind cannot be called after establishing connections");
#endif
            var result = m_NetworkStack.Bind(ref endpoint);
            Bound = result == 0;

            return result;
        }

        /// <summary>
        /// Set the driver to Listen for incomming connections
        /// </summary>
        /// <value>Returns 0 on success.</value>
        /// <exception cref="InvalidOperationException">If the driver is not created properly</exception>
        /// <exception cref="InvalidOperationException">If listen is called more then once on the driver</exception>
        /// <exception cref="InvalidOperationException">If bind has not been called before calling Listen.</exception>
        /// <exception cref="InvalidOperationException">If called on WebGL when not using Relay.</exception>
        public int Listen()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsCreated)
                throw new InvalidOperationException(
                    "Driver must be constructed with a populated or empty INetworkParameter params list");

            if (Listening)
                throw new InvalidOperationException(
                    "Listen can only be called once per NetworkDriver");
            if (!Bound)
                throw new InvalidOperationException(
                    "Listen can only be called after a successful call to Bind");
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            var usingWebSocket = m_NetworkStack.TryGetLayer<NetworkInterfaceLayer<WebSocketNetworkInterface>>(out _);
            var usingRelay = m_NetworkStack.TryGetLayer<RelayLayer>(out _);
            if (usingWebSocket && !usingRelay)
            {
                throw new InvalidOperationException("Web browsers do not support listening for new WebSocket connections.");
            }
#endif

            if (!Bound)
                return -1;

            var ret = m_NetworkStack.Listen();
            Listening = ret == 0;
            return ret;
        }

        // Offered as a workaround for DOTS until we support Burst-compatible constructors/dispose.
        // This is not something we want users to be able to do normally. See MTT-2607.
        [Obsolete("The correct way to stop listening is disposing of the driver (and recreating a new one).")]
        internal void StopListening()
        {
            Listening = false;
        }

        /// <summary>
        /// Accept any new incoming connections. Connections must be accepted before data can be
        /// sent on them. It's also the only way to obtain the <see cref="NetworkConnection"/> value
        /// for new connections on servers.
        /// </summary>
        /// <returns>New connection if any, otherwise a default-value object.</returns>
        public NetworkConnection Accept()
        {
            if (!Listening)
                return default;

            var connectionId = m_NetworkStack.Connections.AcceptConnection();
            return new NetworkConnection(connectionId);
        }

        /// <summary>
        /// Establish a new connection to the given endpoint. Note that this only starts
        /// establishing the new connection. From there it will either succeeds (a
        /// <see cref="NetworkEvent.Type.Connect"/> event will pop on the connection) or fail (a
        /// <see cref="NetworkEvent.Type.Disconnect"/> event will pop on the connection) at a
        /// later time.
        /// </summary>
        /// <remarks>
        /// Establishing a new connection normally requires the driver to be bound (e.g. with the
        /// <see cref="Bind"/> method), but if that's not the case when calling this method, the
        /// driver will be implicitly bound to the appropriate wildcard address. This is a behavior
        /// similar to BSD sockets, where calling <c>connect</c> automatically binds the socket to
        /// an ephemeral port.
        /// </remarks>
        /// <param name="endpoint">Endpoint to connect to.</param>
        /// <returns>New connection object, or a default-valued object on failure.</returns>
        public NetworkConnection Connect(NetworkEndpoint endpoint)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsCreated)
                throw new InvalidOperationException("Driver must be constructed.");
#endif

            if (!Bound)
            {
                var nep = endpoint.Family == NetworkFamily.Ipv6 ? NetworkEndpoint.AnyIpv6 : NetworkEndpoint.AnyIpv4;
                if (Bind(nep) != 0)
                    return default;
            }

            var connectionId = m_NetworkStack.Connections.StartConnecting(ref endpoint);
            var networkConnection = new NetworkConnection(connectionId);

            m_PipelineProcessor.InitializeConnection(networkConnection);
            return networkConnection;
        }

        /// <summary>
        /// Close a connection. Note that to properly notify a peer of this disconnection, it is
        /// required to schedule at least one update with <see cref="ScheduleUpdate"/> and complete
        /// it. Failing to do could leave the remote peer unaware that the connection has been
        /// closed (however it will time out on its own after a while).
        /// </summary>
        /// <param name="connection">Connection to close.</param>
        /// <returns>0 on success, a negative value on error.</returns>
        public int Disconnect(NetworkConnection connection)
        {
            var connectionState = GetConnectionState(connection);

            if (connectionState != NetworkConnection.State.Disconnected)
            {
                var c = connection.ConnectionId;
                m_NetworkStack.Connections.StartDisconnecting(ref c);
            }

            return 0;
        }

        /// <summary>
        /// Get the low-level pipeline buffers for a given pipeline stage on a given pipeline and
        /// for a given connection. Can be used to extract information from a pipeline at runtime.
        /// Note that this is a low-level API which is not recommended for general use.
        /// </summary>
        /// <param name="pipeline">Pipeline to get the buffers from.</param>
        /// <param name="stageId">Pipeline stage to get the buffers from.</param>
        /// <param name="connection">Connection for which to get the pipeline buffers.</param>
        /// <param name="readProcessingBuffer">Buffer used by the receive method of the pipeline.</param>
        /// <param name="writeProcessingBuffer">Buffer used by the send method of the pipeline.</param>
        /// <param name="sharedBuffer">Buffer used by both receive and send methods of the pipeline.</param>
        /// <exception cref="InvalidOperationException">If the connection is invalid.</exception>
        public void GetPipelineBuffers(NetworkPipeline pipeline, NetworkPipelineStageId stageId, NetworkConnection connection, out NativeArray<byte> readProcessingBuffer, out NativeArray<byte> writeProcessingBuffer, out NativeArray<byte> sharedBuffer)
        {
            if (m_NetworkStack.Connections.ConnectionAt(connection.InternalId) != connection.ConnectionId)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("Invalid connection");
#else
                DebugLog.LogError("Trying to get pipeline buffers for invalid connection.");
                readProcessingBuffer = default;
                writeProcessingBuffer = default;
                sharedBuffer = default;
                return;
#endif
            }
            m_PipelineProcessor.GetPipelineBuffers(pipeline, stageId, connection, out readProcessingBuffer, out writeProcessingBuffer, out sharedBuffer);
        }

        // Keeping the pipeline parameter there to avoid breaking DOTS.
        /// <inheritdoc cref="NetworkPipelineProcessor.GetWriteablePipelineParameter{T}"/>
        internal unsafe T* GetWriteablePipelineParameter<T>(NetworkPipeline pipeline, NetworkPipelineStageId stageId)
            where T : unmanaged, INetworkParameter
        {
            return m_PipelineProcessor.GetWriteablePipelineParameter<T>(stageId);
        }

        /// <summary>Get the current state of the given connection.</summary>
        /// <param name="connection">Connection to get the state of.</param>
        /// <returns>State of the connection.</returns>
        public NetworkConnection.State GetConnectionState(NetworkConnection connection)
        {
            var state = m_NetworkStack.Connections.GetConnectionState(connection.ConnectionId);
            return state == NetworkConnection.State.Disconnecting ? NetworkConnection.State.Disconnected : state;
        }

        /// <summary>Obsolete. Use <see cref="GetRemoteEndpoint"/> instead.</summary>
        [Obsolete("RemoteEndPoint has been renamed to GetRemoteEndpoint. (UnityUpgradable) -> GetRemoteEndpoint(*)", false)]
        public NetworkEndpoint RemoteEndPoint(NetworkConnection id)
        {
            return m_NetworkStack.Connections.GetConnectionEndpoint(id.ConnectionId);
        }

        /// <summary>
        /// Get the remote endpoint of a connection (the endpoint used to reach the remote peer on the connection).
        /// </summary>
        /// <remarks>
        /// The returned value should not be assumed to be constant for a given connection, as it is
        /// possible for remote peers to change address during the course of a session (e.g. if a
        /// mobile client changes IP address because they're hopping between cell towers).
        /// </remarks>
        /// <param name="connection">Connection to get the endpoint of.</param>
        /// <returns>The remote endpoint of the connection.</returns>
        public NetworkEndpoint GetRemoteEndpoint(NetworkConnection connection)
        {
            if (m_NetworkSettings.TryGet<RelayNetworkParameter>(out var relayParams))
                return relayParams.ServerData.Endpoint;
            else
                return m_NetworkStack.Connections.GetConnectionEndpoint(connection.ConnectionId);
        }

        /// <summary>Obsolete. Use <see cref="GetLocalEndpoint"/> instead.</summary>
        [Obsolete("LocalEndPoint has been renamed to GetLocalEndpoint. (UnityUpgradable) -> GetLocalEndpoint()", false)]
        public NetworkEndpoint LocalEndPoint()
        {
            return m_NetworkStack.GetLocalEndpoint();
        }

        /// <summary>
        /// Get the local endpoint used by the driver (the endpoint remote peers will use to reach this driver).
        /// </summary>
        /// <remarks>
        /// The returned value should not be assumed to be constant for a given connection, as it is
        /// possible for remote peers to change address during the course of a session (e.g. if a
        /// mobile client changes IP address because they're hopping between cell towers).
        /// </remarks>
        /// <returns>The local endpoint of the driver.</returns>
        public NetworkEndpoint GetLocalEndpoint()
        {
            return m_NetworkStack.GetLocalEndpoint();
        }

        /// <summary>Get the maximum size of headers when sending on the given pipeline.</summary>
        /// <remarks>Only accounts for the Unity Transport headers (no UDP or IP).</remarks>
        /// <param name="pipe">Pipeline to get the header size for.</param>
        /// <returns>The maximum size of the headers on the given pipeline.</returns>
        public int MaxHeaderSize(NetworkPipeline pipe)
        {
            return ToConcurrentSendOnly().MaxHeaderSize(pipe);
        }

        /// <summary>Begin sending data on the given connection and pipeline.</summary>
        /// <param name="pipe">Pipeline to send the data on.</param>
        /// <param name="connection">Connection to send the data to.</param>
        /// <param name="writer"><see cref="DataStreamWriter"/> the data can be written to.</param>
        /// <param name="requiredPayloadSize">
        /// Size that the returned <see cref="DataStreamWriter"/> must support. The method will
        /// return an error if that payload size is not supported by the pipeline. Defaults to 0,
        /// which means the <see cref="DataStreamWriter"/> will be as large as it can be.
        /// </param>
        /// <returns>0 on success, a negative error code on error.</returns>
        public int BeginSend(NetworkPipeline pipe, NetworkConnection connection, out DataStreamWriter writer, int requiredPayloadSize = 0)
        {
            return ToConcurrentSendOnly().BeginSend(pipe, connection, out writer, requiredPayloadSize);
        }

        /// <summary>Begin sending data on the given connection (default pipeline).</summary>
        /// <param name="pipe">Pipeline to send the data on.</param>
        /// <param name="connection">Connection to send the data to.</param>
        /// <param name="writer"><see cref="DataStreamWriter"/> the data can be written to.</param>
        /// <param name="requiredPayloadSize">
        /// Size that the returned <see cref="DataStreamWriter"/> must support. The method will
        /// return an error if that payload size is not supported by the pipeline. Defaults to 0,
        /// which means the <see cref="DataStreamWriter"/> will be as large as it can be.
        /// </param>
        /// <returns>0 on success, a negative value on error.</returns>
        public int BeginSend(NetworkConnection connection, out DataStreamWriter writer, int requiredPayloadSize = 0)
        {
            return ToConcurrentSendOnly().BeginSend(NetworkPipeline.Null, connection, out writer, requiredPayloadSize);
        }

        /// <summary>
        /// Enqueue a send operation for the data in the given <see cref="DataStreamWriter"/>,
        /// which must have been obtained by a prior call to <see cref="BeginSend"/>.
        /// </summary>
        /// <remarks>
        /// This method doesn't actually send anything on the wire. It simply enqueues the send
        /// operation, which will be performed in the next <see cref="ScheduleFlushSend"/> or
        /// <see cref="ScheduleUpdate"/> job.
        /// </remarks>
        /// <param name="writer"><see cref="DataStreamWriter"/> to send.</param>
        /// <returns>The number of bytes to be sent on success, a negative value on error.</returns>
        public int EndSend(DataStreamWriter writer)
        {
            return ToConcurrentSendOnly().EndSend(writer);
        }

        /// <summary>Aborts a send started with <see cref="BeginSend"/>.</summary>
        /// <param name="writer"><see cref="DataStreamWriter"/> to cancel.</param>
        public void AbortSend(DataStreamWriter writer)
        {
            ToConcurrentSendOnly().AbortSend(writer);
        }

        /// <summary>
        /// Pops the next event from the event queue, <see cref="NetworkEvent.Type.Empty"/> will be
        /// returned if there are no more events to pop.
        /// </summary>
        /// <remarks>
        /// The <c>reader</c> obtained from this method will contain different things for different
        /// event types. For <see cref="NetworkEvent.Type.Data"/>, it contains the actual received
        /// payload. For <see cref="NetworkEvent.Type.Disconnect"/>, it contains a single byte
        /// identifying the reason for the disconnection (see <see cref="Error.DisconnectReason"/>).
        /// For other event types, it contains nothing.
        /// </remarks>
        /// <param name="connection">Connection on which the event occured.</param>
        /// <param name="reader">
        /// <see cref="DataStreamReader"/> from which the event data (e.g. payload) can be read from.
        /// </param>
        /// <returns>The type of the event popped.</returns>
        public NetworkEvent.Type PopEvent(out NetworkConnection connection, out DataStreamReader reader)
        {
            return PopEvent(out connection, out reader, out var _);
        }

        /// <summary>
        /// Pops the next event from the event queue, <see cref="NetworkEvent.Type.Empty"/> will be
        /// returned if there are no more events to pop.
        /// </summary>
        /// <remarks>
        /// The <c>reader</c> obtained from this method will contain different things for different
        /// event types. For <see cref="NetworkEvent.Type.Data"/>, it contains the actual received
        /// payload. For <see cref="NetworkEvent.Type.Disconnect"/>, it contains a single byte
        /// identifying the reason for the disconnection (see <see cref="Error.DisconnectReason"/>).
        /// For other event types, it contains nothing.
        /// </remarks>
        /// <param name="connection">Connection on which the event occured.</param>
        /// <param name="reader">
        /// <see cref="DataStreamReader"/> from which the event data (e.g. payload) can be read from.
        /// </param>
        /// <param name="pipe">Pipeline on which the data event was received.</param>
        /// <returns>The type of the event popped.</returns>
        public NetworkEvent.Type PopEvent(out NetworkConnection connection, out DataStreamReader reader, out NetworkPipeline pipe)
        {
            reader = default;

            NetworkEvent.Type type = default;
            int id = default;
            int offset = default;
            int size = default;
            int pipelineId = default;

            while (true)
            {
                type = m_EventQueue.PopEvent(out id, out offset, out size, out pipelineId);
                var connectionId = m_NetworkStack.Connections.ConnectionAt(id);

                //This is in service of not providing any means for a server's / listening NetworkDriver's user-level code to obtain a NetworkConnection handle
                //that corresponds to an underlying Connection that lives in m_NetworkStack.Connections without having obtained it from Accept() first.
                if (id >= 0 && type == NetworkEvent.Type.Data && !m_NetworkStack.Connections.IsConnectionAccepted(ref connectionId))
                {
                    DebugLog.LogWarning("A NetworkEvent.Data event was discarded for a connection that had not been accepted yet. To avoid this, consider calling Accept() prior to PopEvent() in your project's network update loop, or only use PopEventForConnection() in conjunction with Accept().");
                    continue;
                }

                break;
            }

            pipe = new NetworkPipeline { Id = pipelineId };

            if (size >= 0)
                reader = new DataStreamReader(m_DriverReceiver.GetDataStreamSubArray(offset, size));

            connection = id < 0
                ? default
                : new NetworkConnection(m_NetworkStack.Connections.ConnectionAt(id));

            return type;
        }

        /// <summary>
        /// Pops the next event from the event queue for the given connection,
        /// <see cref="NetworkEvent.Type.Empty"/> will be returned if there are no more events.
        /// </summary>
        /// <remarks>
        /// The <c>reader</c> obtained from this method will contain different things for different
        /// event types. For <see cref="NetworkEvent.Type.Data"/>, it contains the actual received
        /// payload. For <see cref="NetworkEvent.Type.Disconnect"/>, it contains a single byte
        /// identifying the reason for the disconnection (see <see cref="Error.DisconnectReason"/>).
        /// For other event types, it contains nothing.
        /// </remarks>
        /// <param name="connection">Connection for which to pop the event.</param>
        /// <param name="reader">
        /// <see cref="DataStreamReader"/> from which the event data (e.g. payload) can be read from.
        /// </param>
        /// <returns>The type of the event popped.</returns>
        public NetworkEvent.Type PopEventForConnection(NetworkConnection connection, out DataStreamReader reader)
        {
            return PopEventForConnection(connection, out reader, out var _);
        }

        /// <summary>
        /// Pops the next event from the event queue for the given connection,
        /// <see cref="NetworkEvent.Type.Empty"/> will be returned if there are no more events.
        /// </summary>
        /// <remarks>
        /// The <c>reader</c> obtained from this method will contain different things for different
        /// event types. For <see cref="NetworkEvent.Type.Data"/>, it contains the actual received
        /// payload. For <see cref="NetworkEvent.Type.Disconnect"/>, it contains a single byte
        /// identifying the reason for the disconnection (see <see cref="Error.DisconnectReason"/>).
        /// For other event types, it contains nothing.
        /// </remarks>
        /// <param name="connection">Connection for which to pop the event.</param>
        /// <param name="reader">
        /// <see cref="DataStreamReader"/> from which the event data (e.g. payload) can be read from.
        /// </param>
        /// <param name="pipe">Pipeline on which the data event was received.</param>
        /// <returns>The type of the event popped.</returns>
        public NetworkEvent.Type PopEventForConnection(NetworkConnection connection, out DataStreamReader reader, out NetworkPipeline pipe)
        {
            reader = default;
            pipe = default;

            if (connection.InternalId < 0 || connection.InternalId >= m_NetworkStack.Connections.Count ||
                m_NetworkStack.Connections.ConnectionAt(connection.InternalId).Version != connection.Version)
                return (int)NetworkEvent.Type.Empty;
            var type = m_EventQueue.PopEventForConnection(connection.InternalId, out var offset, out var size, out var pipelineId);
            pipe = new NetworkPipeline { Id = pipelineId };

            if (size > 0)
                reader = new DataStreamReader(m_DriverReceiver.GetDataStreamSubArray(offset, size));

            return type;
        }

        /// <summary>
        /// Returns the size of the event queue for a specific connection. This is the number of
        /// events that could be popped with <see cref="PopEventForConnection"/>.
        /// </summary>
        /// <param name="connection">Connection to get the event queue size of.</param>
        /// <returns>Number of events in the connection's event queue.</returns>
        public int GetEventQueueSizeForConnection(NetworkConnection connection)
        {
            if (connection.InternalId < 0 || connection.InternalId >= m_NetworkStack.Connections.Count ||
                m_NetworkStack.Connections.ConnectionAt(connection.InternalId).Version != connection.Version)
                return 0;
            return m_EventQueue.GetCountForConnection(connection.InternalId);
        }

        /// <summary>Error code raised by the last receive job, if any.</summary>
        /// <value>Code from the <see cref="Error.StatusCode"/> enum.</value>
        public int ReceiveErrorCode
        {
            get => m_DriverReceiver.Result.ErrorCode;
        }
    }
}
