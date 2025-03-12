using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Networking.Transport.Logging;
using BurstRuntime = Unity.Burst.BurstRuntime;
using static Unity.Networking.Transport.NetworkPipelineStage;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// Buffer passed to the <c>Send</c> method of a pipeline stage. This type is only useful if
    /// implementing a custom <see cref="INetworkPipelineStage"/>.
    /// </summary>
    public unsafe struct InboundSendBuffer
    {
        public byte* buffer;
        public byte* bufferWithHeaders;
        public int bufferLength;
        public int bufferWithHeadersLength;
        public int headerPadding;

        public void SetBufferFromBufferWithHeaders()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (bufferWithHeadersLength < headerPadding)
                throw new IndexOutOfRangeException("Buffer is too small to fit headers");
#endif
            buffer = bufferWithHeaders + headerPadding;
            bufferLength = bufferWithHeadersLength - headerPadding;
        }
    }

    /// <summary>
    /// Buffer passed to the <c>Receive</c> method of a pipeline stage. This type is only useful if
    /// implementing a custom <see cref="INetworkPipelineStage"/>.
    /// </summary>
    public unsafe struct InboundRecvBuffer
    {
        public byte* buffer;
        public int bufferLength;

        public InboundRecvBuffer Slice(int offset)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (bufferLength < offset)
                throw new ArgumentOutOfRangeException("Buffer does not contain enough data");
#endif
            InboundRecvBuffer slice;
            slice.buffer = buffer + offset;
            slice.bufferLength = bufferLength - offset;
            return slice;
        }
    }

    /// <summary>
    /// Current context of a pipeline stage instance. This type is only useful if implementing a
    /// custom <see cref="INetworkPipelineStage"/>, where it will get passed to the send and receive
    /// methods of the pipeline.
    /// </summary>
    public unsafe struct NetworkPipelineContext
    {
        public byte* staticInstanceBuffer;
        public byte* internalSharedProcessBuffer;
        public byte* internalProcessBuffer;
        public DataStreamWriter header;
        public long timestamp;
        public int staticInstanceBufferLength;
        public int internalSharedProcessBufferLength;
        public int internalProcessBufferLength;
        public int accumulatedHeaderCapacity;
    }

    /// <summary>Interface that custom pipeline stages must implement.</summary>
    public unsafe interface INetworkPipelineStage
    {
        /// <summary>
        /// Initialize the static storage for the pipeline from the settings. More importantly, this
        /// method is responsible for providing the <see cref="NetworkPipelineStage"/> structure,
        /// which contains function pointers for most of the pipeline stage functionality.
        /// </summary>
        /// <param name="staticInstanceBuffer">Static storage pointer.</param>
        /// <param name="staticInstanceBufferLength">Static storage length.</param>
        /// <param name="settings">Settings provided to the driver.</param>
        /// <returns>Runtime information for a pipeline instance.</returns>
        NetworkPipelineStage StaticInitialize(byte* staticInstanceBuffer, int staticInstanceBufferLength, NetworkSettings settings);

        /// <summary>
        /// Amount of data that the pipeline stage requires in "static" storage (storage shared by
        /// all instances of the pipeline stage). For example, this is often used to store
        /// configuration parameters obtained through <see cref="NetworkSettings"/>.
        /// </summary>
        /// <value>Size in bytes.</value>
        int StaticSize { get; }
    }

    /// <summary>
    /// Concrete implementation details of a pipeline stage. Only used if implementing custom
    /// pipeline stages. Implementors of <see cref="INetworkPipelineStage"/> are required to produce
    /// this structure on static initialization. The values in this structure will then be used at
    /// runtime for each pipeline where the stage is used.
    /// </summary>
    public unsafe struct NetworkPipelineStage
    {
        public NetworkPipelineStage(TransportFunctionPointer<ReceiveDelegate> Receive,
                                    TransportFunctionPointer<SendDelegate> Send,
                                    TransportFunctionPointer<InitializeConnectionDelegate> InitializeConnection,
                                    int ReceiveCapacity,
                                    int SendCapacity,
                                    int HeaderCapacity,
                                    int SharedStateCapacity,
                                    int PayloadCapacity = 0) // 0 means any size
        {
            this.Receive = Receive;
            this.Send = Send;
            this.InitializeConnection = InitializeConnection;
            this.ReceiveCapacity = ReceiveCapacity;
            this.SendCapacity = SendCapacity;
            this.HeaderCapacity = HeaderCapacity;
            this.SharedStateCapacity = SharedStateCapacity;
            this.PayloadCapacity = PayloadCapacity;
            StaticStateStart = StaticStateCapacity = 0;
        }

        /// <summary>
        /// Requests that a pipeline stage can make in their send and receive methods.
        /// </summary>
        [Flags]
        public enum Requests
        {
            /// <summary>No request. Default value.</summary>
            None = 0,
            /// <summary>Request to run the receive/send method immediately again.</summary>
            Resume = 1,
            /// <summary>Request to run the receive method on the next update.</summary>
            Update = 2,
            /// <summary>Request to run the send method on the next update.</summary>
            SendUpdate = 4,
            /// <summary>Request to raise an error.</summary>
            Error = 8
        }

        // Be careful when changing the signature of the following delegates.
        // They are unsafely casted to function pointers with matching arguments.

        /// <summary>Type of the method used for the receive direction of the pipeline.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ReceiveDelegate(ref NetworkPipelineContext ctx, ref InboundRecvBuffer inboundBuffer, ref Requests requests, int systemHeadersSize);

        /// <summary>Type of the method used for the send direction of the pipeline.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int SendDelegate(ref NetworkPipelineContext ctx, ref InboundSendBuffer inboundBuffer, ref Requests requests, int systemHeadersSize);

        /// <summary>Type of the method used to initialize connections.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void InitializeConnectionDelegate(byte* staticInstanceBuffer, int staticInstanceBufferLength,
            byte* sendProcessBuffer, int sendProcessBufferLength, byte* recvProcessBuffer, int recvProcessBufferLength,
            byte* sharedProcessBuffer, int sharedProcessBufferLength);

        public TransportFunctionPointer<ReceiveDelegate> Receive;
        public TransportFunctionPointer<SendDelegate> Send;
        public TransportFunctionPointer<InitializeConnectionDelegate> InitializeConnection;

        public readonly int ReceiveCapacity;
        public readonly int SendCapacity;
        public readonly int HeaderCapacity;
        public readonly int SharedStateCapacity;
        public readonly int PayloadCapacity;

        internal int StaticStateStart;
        internal int StaticStateCapacity;
    }

    /// <summary>Identifier for a pipeline stage.</summary>
    public struct NetworkPipelineStageId : IEquatable<NetworkPipelineStageId>
    {
        private long m_TypeHash;

        internal static NetworkPipelineStageId Get(Type stage)
        {
            return new NetworkPipelineStageId { m_TypeHash = BurstRuntime.GetHashCode64(stage) };
        }

        /// <summary>Get the stage ID for the given stage type.</summary>
        /// <typeparam name="T">Type of the <see cref="INetworkPipelineStage"/>.</typeparam>
        /// <returns>Stage ID for the given stage type.</returns>
        public static NetworkPipelineStageId Get<T>() where T : unmanaged, INetworkPipelineStage
        {
            return new NetworkPipelineStageId { m_TypeHash = BurstRuntime.GetHashCode64<T>() };
        }

        public override int GetHashCode() => (int)m_TypeHash;

        public override bool Equals(object other) => this == (NetworkPipelineStageId)other;

        public bool Equals(NetworkPipelineStageId other) => m_TypeHash == other.m_TypeHash;

        public static bool operator==(NetworkPipelineStageId lhs, NetworkPipelineStageId rhs) =>
            lhs.m_TypeHash == rhs.m_TypeHash;

        public static bool operator!=(NetworkPipelineStageId lhs, NetworkPipelineStageId rhs) =>
            lhs.m_TypeHash != rhs.m_TypeHash;
    }

    /// <summary>
    /// Identifier for a network pipeline obtained with <see cref="NetworkDriver.CreatePipeline"/>.
    /// </summary>
    public struct NetworkPipeline : IEquatable<NetworkPipeline>
    {
        internal int Id;

        /// <summary>The default pipeline. Acts as a no-op passthrough.</summary>
        /// <value>Default pipeline used for sending.</value>
        public static NetworkPipeline Null => default;

        public static bool operator==(NetworkPipeline lhs, NetworkPipeline rhs)
        {
            return lhs.Id == rhs.Id;
        }

        public static bool operator!=(NetworkPipeline lhs, NetworkPipeline rhs)
        {
            return lhs.Id != rhs.Id;
        }

        public override bool Equals(object compare)
        {
            return this == (NetworkPipeline)compare;
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public bool Equals(NetworkPipeline connection)
        {
            return connection.Id == Id;
        }
    }

    internal struct NetworkPipelineProcessor : IDisposable
    {
        public const int Alignment = 8;
        public const int AlignmentMinusOne = Alignment - 1;

        public int PayloadCapacity(NetworkPipeline pipeline)
        {
            if (pipeline.Id > 0)
            {
                var p = m_Pipelines[pipeline.Id - 1];
                return p.payloadCapacity;
            }
            return 0;
        }

        public Concurrent ToConcurrent()
        {
            var concurrent = new Concurrent
            {
                m_StageStructs = m_StageStructs,
                m_StaticInstanceBuffer = m_StaticInstanceBuffer,
                m_Pipelines = m_Pipelines,
                m_PipelineStagesIndices = m_PipelineStagesIndices,
                m_AccumulatedHeaderCapacity = m_AccumulatedHeaderCapacity,
                m_SendStageNeedsUpdateWrite = m_SendStageNeedsUpdateRead.AsParallelWriter(),
                sizePerConnection = sizePerConnection,
                sendBuffer = m_SendBuffer,
                sharedBuffer = m_SharedBuffer,
                m_timestamp = m_timestamp,
                m_MaxPacketHeaderSize = m_MaxPacketHeaderSize,
            };
            return concurrent;
        }

        public struct Concurrent
        {
            [ReadOnly] internal NativeList<NetworkPipelineStage> m_StageStructs;
            [ReadOnly] internal NativeList<byte> m_StaticInstanceBuffer;
            [ReadOnly] internal NativeList<PipelineImpl> m_Pipelines;
            [ReadOnly] internal NativeList<int> m_PipelineStagesIndices;
            [ReadOnly] internal NativeList<int> m_AccumulatedHeaderCapacity;
            internal NativeQueue<UpdatePipeline>.ParallelWriter m_SendStageNeedsUpdateWrite;
            [ReadOnly] internal NativeArray<int> sizePerConnection;
            // TODO: not really read-only, just hacking the safety system
            [ReadOnly] internal NativeList<byte> sharedBuffer;
            [ReadOnly] internal NativeList<byte> sendBuffer;
            [ReadOnly] internal NativeArray<long> m_timestamp;
            internal int m_MaxPacketHeaderSize;

            public int SendHeaderCapacity(NetworkPipeline pipeline)
            {
                var p = m_Pipelines[pipeline.Id - 1];
                return p.headerCapacity;
            }

            public int PayloadCapacity(NetworkPipeline pipeline)
            {
                if (pipeline.Id > 0)
                {
                    var p = m_Pipelines[pipeline.Id - 1];
                    return p.payloadCapacity;
                }
                return 0;
            }

            public unsafe int Send(NetworkDriver.Concurrent driver, NetworkPipeline pipeline, NetworkConnection connection, NetworkInterfaceSendHandle sendHandle, int headerSize)
            {
                if (sendHandle.data == IntPtr.Zero)
                {
                    return (int)Error.StatusCode.NetworkSendHandleInvalid;
                }

                var connectionId = connection.InternalId;

                // TODO: not really read-only, just hacking the safety system
                NativeArray<byte> tmpBuffer = sendBuffer.AsArray();
                int* sendBufferLock = (int*)tmpBuffer.GetUnsafeReadOnlyPtr();
                sendBufferLock += connectionId * sizePerConnection[SendSizeOffset] / 4;

                if (Interlocked.CompareExchange(ref *sendBufferLock, 1, 0) != 0)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    DebugLog.LogError("The parallel network driver needs to process a single unique connection per job, processing a single connection multiple times in a parallel for is not supported.");
#endif
                    driver.AbortSend(sendHandle);
                    return (int)Error.StatusCode.NetworkDriverParallelForErr;
                }
                NativeList<UpdatePipeline> currentUpdates = new NativeList<UpdatePipeline>(128, Allocator.Temp);

                int retval = ProcessPipelineSend(driver, 0, pipeline, connection, sendHandle, headerSize, currentUpdates);

                Interlocked.Exchange(ref *sendBufferLock, 0);
                // Move the updates requested in this iteration to the concurrent queue so it can be read/parsed in update routine
                for (int i = 0; i < currentUpdates.Length; ++i)
                    m_SendStageNeedsUpdateWrite.Enqueue(currentUpdates[i]);

                return retval;
            }

            internal unsafe int ProcessPipelineSend(NetworkDriver.Concurrent driver, int startStage, NetworkPipeline pipeline, NetworkConnection connection,
                NetworkInterfaceSendHandle sendHandle, int headerSize, NativeList<UpdatePipeline> currentUpdates)
            {
                int initialHeaderSize = headerSize;
                int retval = sendHandle.size;
                NetworkPipelineContext ctx = default(NetworkPipelineContext);
                ctx.timestamp = m_timestamp[0];
                var p = m_Pipelines[pipeline.Id - 1];
                var connectionId = connection.InternalId;

                // If the call comes from update, the sendHandle is set to default.
                bool inUpdateCall = sendHandle.data == IntPtr.Zero;

                // Save the latest error returned by a pipeline send. We need to do so because we
                // can't rely on an error being bubbled up immediately once encountered, since a
                // successful resume call could overwrite it.
                int savedErrorCode = 0;

                var resumeQ = new NativeList<int>(16, Allocator.Temp);
                int resumeQStart = 0;

                var inboundBuffer = default(InboundSendBuffer);
                if (!inUpdateCall)
                {
                    inboundBuffer.bufferWithHeaders = (byte*)sendHandle.data + initialHeaderSize + 1;
                    inboundBuffer.bufferWithHeadersLength = sendHandle.size - initialHeaderSize - 1;
                    inboundBuffer.buffer = inboundBuffer.bufferWithHeaders + p.headerCapacity;
                    inboundBuffer.bufferLength = inboundBuffer.bufferWithHeadersLength - p.headerCapacity;
                }

                while (true)
                {
                    headerSize = p.headerCapacity;

                    int internalBufferOffset = p.sendBufferOffset + sizePerConnection[SendSizeOffset] * connectionId;
                    int internalSharedBufferOffset = p.sharedBufferOffset + sizePerConnection[SharedSizeOffset] * connectionId;

                    // If this is not the first stage we need to fast forward the buffer offset to the correct place
                    if (startStage > 0)
                    {
                        if (inboundBuffer.bufferWithHeadersLength > 0)
                        {
                            DebugLog.LogError("Can't start from a stage with a buffer");
                            return (int)Error.StatusCode.NetworkStateMismatch;
                        }
                        for (int i = 0; i < startStage; ++i)
                        {
                            internalBufferOffset += (m_StageStructs[m_PipelineStagesIndices[p.FirstStageIndex + i]].SendCapacity + AlignmentMinusOne) & (~AlignmentMinusOne);
                            internalSharedBufferOffset += (m_StageStructs[m_PipelineStagesIndices[p.FirstStageIndex + i]].SharedStateCapacity + AlignmentMinusOne) & (~AlignmentMinusOne);
                            headerSize -= m_StageStructs[m_PipelineStagesIndices[p.FirstStageIndex + i]].HeaderCapacity;
                        }
                    }

                    for (int i = startStage; i < p.NumStages; ++i)
                    {
                        var networkPipelineStage = m_StageStructs[m_PipelineStagesIndices[p.FirstStageIndex + i]];
                        int stageHeaderCapacity = networkPipelineStage.HeaderCapacity;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        if (stageHeaderCapacity > headerSize)
                            throw new InvalidOperationException($"Stage {i} '{networkPipelineStage}' does not contain enough header space to send the message");
#endif
                        inboundBuffer.headerPadding = headerSize;
                        headerSize -= stageHeaderCapacity;
                        if (stageHeaderCapacity > 0 && inboundBuffer.bufferWithHeadersLength > 0)
                        {
                            var headerArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(inboundBuffer.bufferWithHeaders + headerSize, stageHeaderCapacity, Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref headerArray, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
                            ctx.header = new DataStreamWriter(headerArray);
                        }
                        else
                            ctx.header = new DataStreamWriter(stageHeaderCapacity, Allocator.Temp);
                        var prevInbound = inboundBuffer;
                        NetworkPipelineStage.Requests requests = NetworkPipelineStage.Requests.None;

                        var sendResult = ProcessSendStage(i, internalBufferOffset, internalSharedBufferOffset, p, ref resumeQ, ref ctx, ref inboundBuffer, ref requests, m_MaxPacketHeaderSize);

                        if ((requests & NetworkPipelineStage.Requests.Update) != 0)
                            AddSendUpdate(connection, i, pipeline, currentUpdates);

                        if (inboundBuffer.bufferWithHeadersLength == 0)
                        {
                            if ((requests & NetworkPipelineStage.Requests.Error) != 0 && !inUpdateCall)
                            {
                                retval = sendResult;
                                savedErrorCode = sendResult;
                            }
                            break;
                        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        if (inboundBuffer.headerPadding != prevInbound.headerPadding)
                            throw new InvalidOperationException($"Changing the header padding in a pipeline is not supported. Stage {i} '{networkPipelineStage}'.");
#endif
                        if (inboundBuffer.buffer != prevInbound.buffer)
                        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            if (inboundBuffer.buffer != inboundBuffer.bufferWithHeaders + inboundBuffer.headerPadding ||
                                inboundBuffer.bufferLength + inboundBuffer.headerPadding > inboundBuffer.bufferWithHeadersLength)
                                throw new InvalidOperationException($"When creating an internal buffer in pipelines the buffer must be a subset of the buffer with headers. Stage {i} '{networkPipelineStage}'.");
#endif
                            // Copy header to new buffer so it is part of the payload
                            UnsafeUtility.MemCpy(inboundBuffer.bufferWithHeaders + headerSize, ctx.header.AsNativeArray().GetUnsafeReadOnlyPtr(), ctx.header.Length);
                        }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        else
                        {
                            if (inboundBuffer.bufferWithHeaders != prevInbound.bufferWithHeaders)
                                throw new InvalidOperationException($"Changing the send buffer with headers without changing the buffer is not supported. Stage {i} '{networkPipelineStage}'.");
                        }
#endif
                        if (ctx.header.Length < stageHeaderCapacity)
                        {
                            int wastedSpace = stageHeaderCapacity - ctx.header.Length;
                            // Remove wasted space in the header
                            UnsafeUtility.MemMove(inboundBuffer.buffer - wastedSpace, inboundBuffer.buffer, inboundBuffer.bufferLength);
                        }

                        // Update the inbound buffer for next iteration
                        inboundBuffer.buffer = inboundBuffer.bufferWithHeaders + headerSize;
                        inboundBuffer.bufferLength = ctx.header.Length + inboundBuffer.bufferLength;


                        internalBufferOffset += (ctx.internalProcessBufferLength + AlignmentMinusOne) & (~AlignmentMinusOne);
                        internalSharedBufferOffset += (ctx.internalSharedProcessBufferLength + AlignmentMinusOne) & (~AlignmentMinusOne);
                    }

                    if (inboundBuffer.bufferLength != 0)
                    {
                        if (sendHandle.data != IntPtr.Zero && inboundBuffer.bufferWithHeaders == (byte*)sendHandle.data + initialHeaderSize + 1)
                        {
                            // Actually send the data - after collapsing it again
                            if (inboundBuffer.buffer != inboundBuffer.bufferWithHeaders)
                            {
                                UnsafeUtility.MemMove(inboundBuffer.bufferWithHeaders, inboundBuffer.buffer, inboundBuffer.bufferLength);
                                inboundBuffer.buffer = inboundBuffer.bufferWithHeaders;
                            }
                            ((byte*)sendHandle.data)[initialHeaderSize] = (byte)pipeline.Id;
                            int sendSize = initialHeaderSize + 1 + inboundBuffer.bufferLength;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                            if (sendSize > sendHandle.size)
                                throw new InvalidOperationException("Pipeline increased the data in the buffer, this is not allowed");
#endif
                            sendHandle.size = sendSize;
                            if ((retval = driver.CompleteSend(connection, sendHandle, true)) < 0)
                            {
                                DebugLog.PipelineCompleteSendFailed(retval);
                            }
                            sendHandle = default;
                        }
                        else
                        {
                            // TODO: This sends the packet directly, bypassing the pipeline process. The problem is that in that way
                            // we can't set the hasPipeline flag in the headers. There is a workaround for now.
                            // Sending without pipeline, the correct pipeline will be added by the default flags when this is called

                            if (driver.BeginSend(connection, out var writer) == 0)
                            {
                                writer.WriteByte((byte)pipeline.Id);
                                writer.WriteBytesUnsafe(inboundBuffer.buffer, inboundBuffer.bufferLength);

                                if ((retval = driver.EndSend(writer)) <= 0)
                                {
                                    DebugLog.PipelineEndSendFailed(retval);
                                }
                            }
                        }
                    }

                    if (resumeQStart >= resumeQ.Length)
                    {
                        break;
                    }

                    startStage = resumeQ[resumeQStart++];

                    inboundBuffer = default;
                }
                if (sendHandle.data != IntPtr.Zero)
                    driver.AbortSend(sendHandle);

                // If we encountered an error, it takes precendence over the last returned value.
                return savedErrorCode < 0 ? savedErrorCode : retval;
            }

            private unsafe int ProcessSendStage(int startStage, int internalBufferOffset, int internalSharedBufferOffset,
                PipelineImpl p, ref NativeList<int> resumeQ, ref NetworkPipelineContext ctx, ref InboundSendBuffer inboundBuffer, ref NetworkPipelineStage.Requests requests, int systemHeaderSize)
            {
                var stageIndex = p.FirstStageIndex + startStage;
                var pipelineStage = m_StageStructs[m_PipelineStagesIndices[stageIndex]];
                ctx.accumulatedHeaderCapacity = m_AccumulatedHeaderCapacity[stageIndex];
                ctx.staticInstanceBuffer = (byte*)m_StaticInstanceBuffer.GetUnsafeReadOnlyPtr() + pipelineStage.StaticStateStart;
                ctx.staticInstanceBufferLength = pipelineStage.StaticStateCapacity;
                ctx.internalProcessBuffer = (byte*)sendBuffer.GetUnsafeReadOnlyPtr() + internalBufferOffset;
                ctx.internalProcessBufferLength = pipelineStage.SendCapacity;

                ctx.internalSharedProcessBuffer = (byte*)sharedBuffer.GetUnsafeReadOnlyPtr() + internalSharedBufferOffset;
                ctx.internalSharedProcessBufferLength = pipelineStage.SharedStateCapacity;

                requests = NetworkPipelineStage.Requests.None;
                var retval = ((delegate * unmanaged[Cdecl] < ref NetworkPipelineContext, ref InboundSendBuffer, ref Requests, int, int >)pipelineStage.Send.Ptr.Value)(ref ctx, ref inboundBuffer, ref requests, systemHeaderSize);
                if ((requests & NetworkPipelineStage.Requests.Resume) != 0)
                    resumeQ.Add(startStage);
                return retval;
            }
        }
        private NativeList<NetworkPipelineStage> m_StageStructs;
        private NativeList<NetworkPipelineStageId> m_StageIds;
        private NativeList<int> m_PipelineStagesIndices;
        private NativeList<int> m_AccumulatedHeaderCapacity;
        private NativeList<PipelineImpl> m_Pipelines;
        private NativeList<byte> m_StaticInstanceBuffer;
        private NativeList<byte> m_ReceiveBuffer;
        private NativeList<byte> m_SendBuffer;
        private NativeList<byte> m_SharedBuffer;
        private NativeList<UpdatePipeline> m_ReceiveStageNeedsUpdate;
        private NativeList<UpdatePipeline> m_SendStageNeedsUpdate;
        private NativeQueue<UpdatePipeline> m_SendStageNeedsUpdateRead;

        private NativeArray<int> sizePerConnection;

        private NativeArray<long> m_timestamp;

        private int m_MaxPacketHeaderSize;

        private const int SendSizeOffset = 0;
        private const int RecveiveSizeOffset = 1;
        private const int SharedSizeOffset = 2;

        internal int PipelineCount => m_Pipelines.Length;

        internal struct PipelineImpl
        {
            public int FirstStageIndex;
            public int NumStages;

            public int receiveBufferOffset;
            public int sendBufferOffset;
            public int sharedBufferOffset;
            public int headerCapacity;
            public int payloadCapacity;
        }

        public unsafe NetworkPipelineProcessor(NetworkSettings settings, int maxPacketHeaderSize)
        {
            m_StaticInstanceBuffer = new NativeList<byte>(0, Allocator.Persistent);
            m_StageStructs = new NativeList<NetworkPipelineStage>(8, Allocator.Persistent);
            m_StageIds = new NativeList<NetworkPipelineStageId>(8, Allocator.Persistent);
            m_PipelineStagesIndices = new NativeList<int>(16, Allocator.Persistent);
            m_AccumulatedHeaderCapacity = new NativeList<int>(16, Allocator.Persistent);
            m_Pipelines = new NativeList<PipelineImpl>(16, Allocator.Persistent);
            m_ReceiveBuffer = new NativeList<byte>(0, Allocator.Persistent);
            m_SendBuffer = new NativeList<byte>(0, Allocator.Persistent);
            m_SharedBuffer = new NativeList<byte>(0, Allocator.Persistent);
            sizePerConnection = new NativeArray<int>(3, Allocator.Persistent);
            // Store an int for the spinlock first in each connections send buffer, round up to alignment of 8
            sizePerConnection[SendSizeOffset] = Alignment;
            m_ReceiveStageNeedsUpdate = new NativeList<UpdatePipeline>(128, Allocator.Persistent);
            m_SendStageNeedsUpdate = new NativeList<UpdatePipeline>(128, Allocator.Persistent);
            m_SendStageNeedsUpdateRead = new NativeQueue<UpdatePipeline>(Allocator.Persistent);
            m_timestamp = new NativeArray<long>(1, Allocator.Persistent);
            m_MaxPacketHeaderSize = maxPacketHeaderSize;

            RegisterPipelineStage<NullPipelineStage>(new NullPipelineStage(), settings);
            RegisterPipelineStage<FragmentationPipelineStage>(new FragmentationPipelineStage(), settings);
            RegisterPipelineStage<ReliableSequencedPipelineStage>(new ReliableSequencedPipelineStage(), settings);
            RegisterPipelineStage<UnreliableSequencedPipelineStage>(new UnreliableSequencedPipelineStage(), settings);
            RegisterPipelineStage<SimulatorPipelineStage>(new SimulatorPipelineStage(), settings);
            #pragma warning disable CS0618
            RegisterPipelineStage<SimulatorPipelineStageInSend>(new SimulatorPipelineStageInSend(), settings);
            #pragma warning restore CS0618
        }

        public void Dispose()
        {
            m_PipelineStagesIndices.Dispose();
            m_AccumulatedHeaderCapacity.Dispose();
            m_ReceiveBuffer.Dispose();
            m_SendBuffer.Dispose();
            m_SharedBuffer.Dispose();
            m_Pipelines.Dispose();
            sizePerConnection.Dispose();
            m_ReceiveStageNeedsUpdate.Dispose();
            m_SendStageNeedsUpdate.Dispose();
            m_SendStageNeedsUpdateRead.Dispose();
            m_timestamp.Dispose();
            m_StageStructs.Dispose();
            m_StageIds.Dispose();
            m_StaticInstanceBuffer.Dispose();
        }

        public long Timestamp
        {
            get { return m_timestamp[0]; }
            internal set { m_timestamp[0] = value; }
        }

        public unsafe void RegisterPipelineStage<T>(T stage, NetworkSettings settings) where T : unmanaged, INetworkPipelineStage
        {
            // Resize the static buffer.
            var oldStaticBufferSize = m_StaticInstanceBuffer.Length;
            if (stage.StaticSize > 0)
            {
                var newStaticBufferSize = oldStaticBufferSize + stage.StaticSize;
                newStaticBufferSize = (newStaticBufferSize + 15) & (~15);
                m_StaticInstanceBuffer.ResizeUninitialized(newStaticBufferSize);
            }

            // Create the stage structure.
            var staticBufferOffset = oldStaticBufferSize;
            var staticBufferPtr = (byte*)m_StaticInstanceBuffer.GetUnsafePtr() + staticBufferOffset;
            var stageStruct = stage.StaticInitialize(staticBufferPtr, stage.StaticSize, settings);
            stageStruct.StaticStateStart = staticBufferOffset;
            stageStruct.StaticStateCapacity = stage.StaticSize;

            // Add the stage structure and ID to internal lists.
            m_StageStructs.Add(stageStruct);
            m_StageIds.Add(NetworkPipelineStageId.Get<T>());
        }

        public unsafe void InitializeConnection(NetworkConnection con)
        {
            var requiredReceiveSize = (con.InternalId + 1) * sizePerConnection[RecveiveSizeOffset];
            var requiredSendSize = (con.InternalId + 1) * sizePerConnection[SendSizeOffset];
            var requiredSharedSize = (con.InternalId + 1) * sizePerConnection[SharedSizeOffset];
            if (m_ReceiveBuffer.Length < requiredReceiveSize)
                m_ReceiveBuffer.ResizeUninitialized(requiredReceiveSize);
            if (m_SendBuffer.Length < requiredSendSize)
                m_SendBuffer.ResizeUninitialized(requiredSendSize);
            if (m_SharedBuffer.Length < requiredSharedSize)
                m_SharedBuffer.ResizeUninitialized(requiredSharedSize);

            UnsafeUtility.MemClear((byte*)m_ReceiveBuffer.GetUnsafePtr() + con.InternalId * sizePerConnection[RecveiveSizeOffset], sizePerConnection[RecveiveSizeOffset]);
            UnsafeUtility.MemClear((byte*)m_SendBuffer.GetUnsafePtr() + con.InternalId * sizePerConnection[SendSizeOffset], sizePerConnection[SendSizeOffset]);
            UnsafeUtility.MemClear((byte*)m_SharedBuffer.GetUnsafePtr() + con.InternalId * sizePerConnection[SharedSizeOffset], sizePerConnection[SharedSizeOffset]);

            InitializeStages(con.InternalId);
        }

        unsafe void InitializeStages(int networkId)
        {
            var connectionId = networkId;

            for (int i = 0; i < m_Pipelines.Length; i++)
            {
                var pipeline = m_Pipelines[i];

                int recvBufferOffset = pipeline.receiveBufferOffset + sizePerConnection[RecveiveSizeOffset] * connectionId;
                int sendBufferOffset = pipeline.sendBufferOffset + sizePerConnection[SendSizeOffset] * connectionId;
                int sharedBufferOffset = pipeline.sharedBufferOffset + sizePerConnection[SharedSizeOffset] * connectionId;

                for (int stage = pipeline.FirstStageIndex;
                     stage < pipeline.FirstStageIndex + pipeline.NumStages;
                     stage++)
                {
                    var pipelineStage = m_StageStructs[m_PipelineStagesIndices[stage]];
                    var sendProcessBuffer = (byte*)m_SendBuffer.GetUnsafePtr() + sendBufferOffset;
                    var sendProcessBufferLength = pipelineStage.SendCapacity;
                    var recvProcessBuffer = (byte*)m_ReceiveBuffer.GetUnsafePtr() + recvBufferOffset;
                    var recvProcessBufferLength = pipelineStage.ReceiveCapacity;
                    var sharedProcessBuffer = (byte*)m_SharedBuffer.GetUnsafePtr() + sharedBufferOffset;
                    var sharedProcessBufferLength = pipelineStage.SharedStateCapacity;

                    var staticInstanceBuffer = (byte*)m_StaticInstanceBuffer.GetUnsafePtr() + pipelineStage.StaticStateStart;
                    var staticInstanceBufferLength = pipelineStage.StaticStateCapacity;
                    ((delegate * unmanaged[Cdecl] < byte*, int, byte*, int, byte*, int, byte*, int, void >)pipelineStage.InitializeConnection.Ptr.Value)(staticInstanceBuffer, staticInstanceBufferLength,
                        sendProcessBuffer, sendProcessBufferLength, recvProcessBuffer, recvProcessBufferLength,
                        sharedProcessBuffer, sharedProcessBufferLength);

                    sendBufferOffset += (sendProcessBufferLength + AlignmentMinusOne) & (~AlignmentMinusOne);
                    recvBufferOffset += (recvProcessBufferLength + AlignmentMinusOne) & (~AlignmentMinusOne);
                    sharedBufferOffset += (sharedProcessBufferLength + AlignmentMinusOne) & (~AlignmentMinusOne);
                }
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void ValidateStages(NativeArray<NetworkPipelineStageId> stages)
        {
            var reliableIndex = -1;
            var fragmentedIndex = -1;
            for (int i = 0; i < stages.Length; i++)
            {
                if (stages[i] == NetworkPipelineStageId.Get<ReliableSequencedPipelineStage>())
                    reliableIndex = i;
                if (stages[i] == NetworkPipelineStageId.Get<FragmentationPipelineStage>())
                    fragmentedIndex = i;
            }

            // Check that fragmentation doesn't follow the reliability pipeline. This order is not
            // supported since the reliability pipeline can't handle packets larger than the MTU.
            if (reliableIndex >= 0 && fragmentedIndex >= 0 && fragmentedIndex > reliableIndex)
                throw new InvalidOperationException("Cannot create pipeline with ReliableSequenced followed by Fragmentation stage. Should reverse their order.");
        }

        public NetworkPipeline CreatePipeline(NativeArray<NetworkPipelineStageId> stages)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Pipelines.Length > 255)
                throw new InvalidOperationException("Cannot create more than 255 pipelines on a single driver");
            ValidateStages(stages);
#endif
            var receiveCap = 0;
            var sharedCap = 0;
            var sendCap = 0;
            var headerCap = 0;
            var payloadCap = 0;
            var pipeline = new PipelineImpl();
            pipeline.FirstStageIndex = m_PipelineStagesIndices.Length;
            pipeline.NumStages = stages.Length;
            for (int i = 0; i < stages.Length; i++)
            {
                var stageIndex = m_StageIds.IndexOf(stages[i]);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (stageIndex == -1)
                    throw new InvalidOperationException("Trying to create pipeline with invalid stage.");
#endif
                m_PipelineStagesIndices.Add(stageIndex);
                m_AccumulatedHeaderCapacity.Add(headerCap);    // For every stage, compute how much header space has already bee used by other stages when sending
                // Make sure all data buffers are aligned
                receiveCap += (m_StageStructs[stageIndex].ReceiveCapacity + AlignmentMinusOne) & (~AlignmentMinusOne);
                sendCap += (m_StageStructs[stageIndex].SendCapacity + AlignmentMinusOne) & (~AlignmentMinusOne);
                headerCap += m_StageStructs[stageIndex].HeaderCapacity;
                sharedCap += (m_StageStructs[stageIndex].SharedStateCapacity + AlignmentMinusOne) & (~AlignmentMinusOne);
                if (payloadCap == 0)
                {
                    payloadCap = m_StageStructs[stageIndex].PayloadCapacity; // The first non-zero stage determines the pipeline capacity
                }
            }

            pipeline.receiveBufferOffset = sizePerConnection[RecveiveSizeOffset];
            sizePerConnection[RecveiveSizeOffset] = sizePerConnection[RecveiveSizeOffset] + receiveCap;

            pipeline.sendBufferOffset = sizePerConnection[SendSizeOffset];
            sizePerConnection[SendSizeOffset] = sizePerConnection[SendSizeOffset] + sendCap;

            pipeline.sharedBufferOffset = sizePerConnection[SharedSizeOffset];
            sizePerConnection[SharedSizeOffset] = sizePerConnection[SharedSizeOffset] + sharedCap;

            pipeline.headerCapacity = headerCap;
            pipeline.payloadCapacity = payloadCap;

            m_Pipelines.Add(pipeline);
            return new NetworkPipeline {Id = m_Pipelines.Length};
        }

        public void GetPipelineBuffers(NetworkPipeline pipelineId, NetworkPipelineStageId stageId, NetworkConnection connection,
            out NativeArray<byte> readProcessingBuffer, out NativeArray<byte> writeProcessingBuffer,
            out NativeArray<byte> sharedBuffer)
        {
            if (pipelineId.Id - 1 < 0 || pipelineId.Id - 1 >= m_Pipelines.Length)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("The specified pipeline is not valid.");
#else
                writeProcessingBuffer = default;
                readProcessingBuffer = default;
                sharedBuffer = default;
                return;
#endif
            }

            var pipeline = m_Pipelines[pipelineId.Id - 1];

            int recvBufferOffset = pipeline.receiveBufferOffset + sizePerConnection[RecveiveSizeOffset] * connection.InternalId;
            int sendBufferOffset = pipeline.sendBufferOffset + sizePerConnection[SendSizeOffset] * connection.InternalId;
            int sharedBufferOffset = pipeline.sharedBufferOffset + sizePerConnection[SharedSizeOffset] * connection.InternalId;

            int stageIndexInList;
            bool stageFound = true;
            for (stageIndexInList = pipeline.FirstStageIndex;
                 stageIndexInList < pipeline.FirstStageIndex + pipeline.NumStages;
                 stageIndexInList++)
            {
                if (m_StageIds[m_PipelineStagesIndices[stageIndexInList]] == stageId)
                {
                    stageFound = true;
                    break;
                }
                sendBufferOffset += (m_StageStructs[m_PipelineStagesIndices[stageIndexInList]].SendCapacity + AlignmentMinusOne) & (~AlignmentMinusOne);
                recvBufferOffset += (m_StageStructs[m_PipelineStagesIndices[stageIndexInList]].ReceiveCapacity + AlignmentMinusOne) & (~AlignmentMinusOne);
                sharedBufferOffset += (m_StageStructs[m_PipelineStagesIndices[stageIndexInList]].SharedStateCapacity + AlignmentMinusOne) & (~AlignmentMinusOne);
            }

            if (!stageFound)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("Could not find stage ID. Make sure the type for this stage ID is added when the pipeline is created.");
#else
                writeProcessingBuffer = default;
                readProcessingBuffer = default;
                sharedBuffer = default;
                return;
#endif
            }

            writeProcessingBuffer = m_SendBuffer.AsArray().GetSubArray(sendBufferOffset, m_StageStructs[m_PipelineStagesIndices[stageIndexInList]].SendCapacity);
            readProcessingBuffer = m_ReceiveBuffer.AsArray().GetSubArray(recvBufferOffset, m_StageStructs[m_PipelineStagesIndices[stageIndexInList]].ReceiveCapacity);
            sharedBuffer = m_SharedBuffer.AsArray().GetSubArray(sharedBufferOffset, m_StageStructs[m_PipelineStagesIndices[stageIndexInList]].SharedStateCapacity);
        }

        /// <summary>
        /// Returns a writable pointer to the <see cref="INetworkParameter"/> associated with this stage.
        /// Note that Parameters are not designed to be runtime-modified, so this API is unsafe and experimental.
        /// NetCode use-case: Runtime editing of simulator params.
        /// </summary>
        internal unsafe T* GetWriteablePipelineParameter<T>(NetworkPipelineStageId stageId)
            where T : unmanaged, INetworkParameter
        {
            var stageIndex = m_StageIds.IndexOf(stageId);
            if (stageIndex == -1)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("Could not find stage ID. Make sure the type for this stage ID is added when the pipeline is created.");
#else
                return null;
#endif
            }

            var pipelineStage = m_StageStructs[stageIndex];
            var staticInstanceBuffer = (byte*)m_StaticInstanceBuffer.GetUnsafePtr() + pipelineStage.StaticStateStart;
            var staticInstanceBufferLength = pipelineStage.StaticStateCapacity;
            if (staticInstanceBufferLength != sizeof(T))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException($"GetPipelineParameterBuffer type mismatch! Expected staticInstanceBufferLength ({staticInstanceBufferLength}) to be of sizeof({typeof(T)}) ({sizeof(T)})!");
#else
                return null;
#endif
            }

            return (T*)staticInstanceBuffer;
        }

        internal struct UpdatePipeline
        {
            public NetworkPipeline pipeline;
            public int stage;
            public NetworkConnection connection;
        }

        internal unsafe void UpdateSend(NetworkDriver.Concurrent driver, out int updateCount)
        {
            // Clear the send lock since it cannot be kept here and can be lost if there are exceptions in send
            NativeArray<byte> tmpBuffer = m_SendBuffer.AsArray();
            int* sendBufferLock = (int*)tmpBuffer.GetUnsafePtr();
            for (int connectionOffset = 0; connectionOffset < m_SendBuffer.Length; connectionOffset += sizePerConnection[SendSizeOffset])
                sendBufferLock[connectionOffset / 4] = 0;

            NativeList<UpdatePipeline> sendUpdates = new NativeList<UpdatePipeline>(m_SendStageNeedsUpdateRead.Count + m_SendStageNeedsUpdate.Length, Allocator.Temp);

            UpdatePipeline updateItem;
            while (m_SendStageNeedsUpdateRead.TryDequeue(out updateItem))
            {
                if (driver.GetConnectionState(updateItem.connection) == NetworkConnection.State.Connected)
                    AddSendUpdate(updateItem.connection, updateItem.stage, updateItem.pipeline, sendUpdates);
            }

            for (int i = 0; i < m_SendStageNeedsUpdate.Length; i++)
            {
                updateItem = m_SendStageNeedsUpdate[i];
                if (driver.GetConnectionState(m_SendStageNeedsUpdate[i].connection) == NetworkConnection.State.Connected)
                    AddSendUpdate(updateItem.connection, updateItem.stage, updateItem.pipeline, sendUpdates);
            }

            updateCount = sendUpdates.Length;

            NativeList<UpdatePipeline> currentUpdates = new NativeList<UpdatePipeline>(128, Allocator.Temp);
            // Move the updates requested in this iteration to the concurrent queue so it can be read/parsed in update routine
            for (int i = 0; i < updateCount; ++i)
            {
                updateItem = sendUpdates[i];
                var result = ToConcurrent().ProcessPipelineSend(driver, updateItem.stage, updateItem.pipeline, updateItem.connection, default, 0, currentUpdates);
                if (result < 0)
                {
                    DebugLog.PipelineProcessSendFailed(result);
                }
            }
            for (int i = 0; i < currentUpdates.Length; ++i)
                m_SendStageNeedsUpdateRead.Enqueue(currentUpdates[i]);
        }

        private static void AddSendUpdate(NetworkConnection connection, int stageId, NetworkPipeline pipelineId, NativeList<UpdatePipeline> currentUpdates)
        {
            var newUpdate = new UpdatePipeline
            {connection = connection, stage = stageId, pipeline = pipelineId};
            bool uniqueItem = true;
            for (int j = 0; j < currentUpdates.Length; ++j)
            {
                if (currentUpdates[j].stage == newUpdate.stage &&
                    currentUpdates[j].pipeline.Id == newUpdate.pipeline.Id &&
                    currentUpdates[j].connection == newUpdate.connection)
                    uniqueItem = false;
            }
            if (uniqueItem)
                currentUpdates.Add(newUpdate);
        }

        public void UpdateReceive(ref NetworkDriver driver, out int updateCount)
        {
            NativeArray<UpdatePipeline> receiveUpdates = new NativeArray<UpdatePipeline>(m_ReceiveStageNeedsUpdate.Length, Allocator.Temp);

            // Move current update requests to a new queue
            updateCount = 0;
            for (int i = 0; i < m_ReceiveStageNeedsUpdate.Length; ++i)
            {
                if (driver.GetConnectionState(m_ReceiveStageNeedsUpdate[i].connection) == NetworkConnection.State.Connected)
                    receiveUpdates[updateCount++] = m_ReceiveStageNeedsUpdate[i];
            }
            m_ReceiveStageNeedsUpdate.Clear();

            // Process all current requested updates, new update requests will (possibly) be generated from the pipeline stages
            for (int i = 0; i < updateCount; ++i)
            {
                UpdatePipeline updateItem = receiveUpdates[i];
                var receiver = driver.Receiver;
                var eventQueue = driver.EventQueue;
                ProcessReceiveStagesFrom(ref receiver, ref eventQueue, updateItem.stage, updateItem.pipeline, updateItem.connection, default);
            }
        }

        public unsafe void Receive(byte pipelineId, ref NetworkDriverReceiver receiver, ref NetworkEventQueue eventQueue, ref NetworkConnection connection, byte* buffer, int bufferLength)
        {
            if (pipelineId == 0 || pipelineId > m_Pipelines.Length)
            {
                DebugLog.ErrorPipelineReceiveInvalid(pipelineId, m_Pipelines.Length);
                return;
            }
            var p = m_Pipelines[pipelineId - 1];
            int startStage = p.NumStages - 1;

            InboundRecvBuffer inBuffer;
            inBuffer.buffer = buffer;
            inBuffer.bufferLength = bufferLength;
            ProcessReceiveStagesFrom(ref receiver, ref eventQueue, startStage, new NetworkPipeline {Id = pipelineId}, connection, inBuffer);
        }

        private unsafe void ProcessReceiveStagesFrom(ref NetworkDriverReceiver receiver, ref NetworkEventQueue eventQueue, int startStage, NetworkPipeline pipeline,
            NetworkConnection connection, InboundRecvBuffer buffer)
        {
            var p = m_Pipelines[pipeline.Id - 1];
            var connectionId = connection.InternalId;
            var resumeQ = new NativeList<int>(16, Allocator.Temp);
            int resumeQStart = 0;

            var systemHeaderSize = m_MaxPacketHeaderSize;

            var inboundBuffer = buffer;

            var ctx = new NetworkPipelineContext
            {
                timestamp = Timestamp,
                header = default
            };

            while (true)
            {
                bool needsUpdate = false;
                bool needsSendUpdate = false;
                int internalBufferOffset = p.receiveBufferOffset + sizePerConnection[RecveiveSizeOffset] * connectionId;
                int internalSharedBufferOffset = p.sharedBufferOffset + sizePerConnection[SharedSizeOffset] * connectionId;

                // Adjust offset accounting for stages in front of the starting stage, since we're parsing the stages in reverse order
                for (int st = 0; st < startStage; ++st)
                {
                    internalBufferOffset += (m_StageStructs[m_PipelineStagesIndices[p.FirstStageIndex + st]].ReceiveCapacity + AlignmentMinusOne) & (~AlignmentMinusOne);
                    internalSharedBufferOffset += (m_StageStructs[m_PipelineStagesIndices[p.FirstStageIndex + st]].SharedStateCapacity + AlignmentMinusOne) & (~AlignmentMinusOne);
                }

                for (int i = startStage; i >= 0; --i)
                {
                    ProcessReceiveStage(i, pipeline, internalBufferOffset, internalSharedBufferOffset, ref ctx, ref inboundBuffer, ref resumeQ, ref needsUpdate, ref needsSendUpdate, systemHeaderSize);
                    if (needsUpdate)
                    {
                        var newUpdate = new UpdatePipeline
                        {connection = connection, stage = i, pipeline = pipeline};
                        bool uniqueItem = true;
                        for (int j = 0; j < m_ReceiveStageNeedsUpdate.Length; ++j)
                        {
                            if (m_ReceiveStageNeedsUpdate[j].stage == newUpdate.stage &&
                                m_ReceiveStageNeedsUpdate[j].pipeline.Id == newUpdate.pipeline.Id &&
                                m_ReceiveStageNeedsUpdate[j].connection == newUpdate.connection)
                                uniqueItem = false;
                        }
                        if (uniqueItem)
                            m_ReceiveStageNeedsUpdate.Add(newUpdate);
                    }

                    if (needsSendUpdate)
                        AddSendUpdate(connection, i, pipeline, m_SendStageNeedsUpdate);

                    if (inboundBuffer.buffer == null)
                        break;

                    // Offset needs to be adjusted for the next pipeline (the one in front of this one)
                    if (i > 0)
                    {
                        internalBufferOffset -=
                            (m_StageStructs[m_PipelineStagesIndices[p.FirstStageIndex + i - 1]].ReceiveCapacity + AlignmentMinusOne) & (~AlignmentMinusOne);
                        internalSharedBufferOffset -=
                            (m_StageStructs[m_PipelineStagesIndices[p.FirstStageIndex + i - 1]].SharedStateCapacity + AlignmentMinusOne) & (~AlignmentMinusOne);
                    }

                    needsUpdate = false;
                }

                if (inboundBuffer.buffer != null)
                    receiver.PushDataEvent(connection, pipeline.Id, inboundBuffer.buffer, inboundBuffer.bufferLength, ref eventQueue);

                if (resumeQStart >= resumeQ.Length)
                {
                    return;
                }

                startStage = resumeQ[resumeQStart++];
                inboundBuffer = default;
            }
        }

        private unsafe void ProcessReceiveStage(int stage, NetworkPipeline pipeline, int internalBufferOffset,
            int internalSharedBufferOffset, ref NetworkPipelineContext ctx, ref InboundRecvBuffer inboundBuffer,
            ref NativeList<int> resumeQ, ref bool needsUpdate, ref bool needsSendUpdate, int systemHeadersSize)
        {
            var p = m_Pipelines[pipeline.Id - 1];

            var stageId = m_PipelineStagesIndices[p.FirstStageIndex + stage];
            var pipelineStage = m_StageStructs[stageId];
            ctx.staticInstanceBuffer = (byte*)m_StaticInstanceBuffer.GetUnsafePtr() + pipelineStage.StaticStateStart;
            ctx.staticInstanceBufferLength = pipelineStage.StaticStateCapacity;
            ctx.internalProcessBuffer = (byte*)m_ReceiveBuffer.GetUnsafePtr() + internalBufferOffset;
            ctx.internalProcessBufferLength = pipelineStage.ReceiveCapacity;
            ctx.internalSharedProcessBuffer = (byte*)m_SharedBuffer.GetUnsafePtr() + internalSharedBufferOffset;
            ctx.internalSharedProcessBufferLength = pipelineStage.SharedStateCapacity;
            NetworkPipelineStage.Requests requests = NetworkPipelineStage.Requests.None;

            ((delegate * unmanaged[Cdecl] < ref NetworkPipelineContext, ref InboundRecvBuffer, ref Requests, int, void >)pipelineStage.Receive.Ptr.Value)(ref ctx, ref inboundBuffer, ref requests, systemHeadersSize);

            if ((requests & NetworkPipelineStage.Requests.Resume) != 0)
                resumeQ.Add(stage);
            needsUpdate = (requests & NetworkPipelineStage.Requests.Update) != 0;
            needsSendUpdate = (requests & NetworkPipelineStage.Requests.SendUpdate) != 0;
        }
    }
}
