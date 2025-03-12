#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Baselib.LowLevel;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using ErrorState = Unity.Baselib.LowLevel.Binding.Baselib_ErrorState;
using ErrorCode = Unity.Baselib.LowLevel.Binding.Baselib_ErrorCode;
using Unity.Mathematics;
using Unity.Networking.Transport.Logging;
using UnityEngine;

namespace Unity.Networking.Transport
{
    using NetworkRequest = Binding.Baselib_RegisteredNetwork_Request;
    using CompletionResult = Binding.Baselib_RegisteredNetwork_CompletionResult;
    using RegisteredNetworkEndpoint = Binding.Baselib_RegisteredNetwork_Endpoint;
    using NetworkSocket = Binding.Baselib_RegisteredNetwork_Socket_UDP;

    /// <summary>
    /// Default interface used by <see cref="NetworkDriver"/>, which will send/receive all traffic
    /// over UDP. Not available on WebGL.
    /// </summary>
    [BurstCompile]
    public struct UDPNetworkInterface : INetworkInterface
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private class SocketList
        {
            public struct SocketId
            {
                public NetworkSocket socket;
            }
            public List<SocketId> OpenSockets = new List<SocketId>();

            ~SocketList()
            {
                foreach (var socket in OpenSockets)
                {
                    Binding.Baselib_RegisteredNetwork_Socket_UDP_Close(socket.socket);
                }
            }
        }
        private static SocketList AllSockets = new SocketList();
#endif

        // Array size to use when batching send/receive requests. We need to batch these requests
        // because the array is stack-allocated, and using the send/receive queue sizes could lead
        // to a stack overflow.
        const uint k_RequestsBatchSize = 64;

        // Safety value for the maximum number of times we can recreate a socket. Recreating a
        // socket so many times would indicate some deeper issue that we won't solve by opening
        // new sockets all the time. This also prevents logging endlessly if we get stuck in a
        // loop of recreating sockets very frequently.
        const uint k_MaxNumSocketRecreate = 1000;

        private struct PacketBufferLayout
        {
            public uint MetadataOffset;
            public uint EndpointOffset;
            public uint PayloadOffset;
        }

        internal enum SocketStatus
        {
            SocketNormal,
            SocketNeedsRecreate,
            SocketFailed,
        }

        internal struct InternalState
        {
            public NetworkSocket Socket;
            public SocketStatus SocketStatus;
            public NetworkEndpoint BindEndpoint;
            public int ReceiveQueueCapacity;
            public int SendQueueCapacity;
            public long LastUpdateTime;
            public long LastSocketRecreateTime;
            public uint NumSocketRecreate;
        }

        private PacketsQueue m_ReceiveQueue;
        private UnsafeBaselibNetworkArray m_SendBuffers;
        private UnsafeBaselibNetworkArray m_ReceiveBuffers;
        private PacketBufferLayout m_PacketBufferLayout;

        internal NativeReference<InternalState> m_InternalState;

        /// <inheritdoc/>
        public unsafe NetworkEndpoint LocalEndpoint
        {
            get
            {
                var socket = m_InternalState.Value.Socket;
                var error = default(ErrorState);
                var endpoint = default(NetworkEndpoint);

                Binding.Baselib_RegisteredNetwork_Socket_UDP_GetNetworkAddress(socket, &endpoint.BaselibAddress, &error);
                if (error.code != ErrorCode.Success)
                {
                    // Bind endpoint is better than nothing if we can't get the effective one.
                    return m_InternalState.Value.BindEndpoint;
                }

                return endpoint;
            }
        }

        /// <inheritdoc/>
        public unsafe int Initialize(ref NetworkSettings settings, ref int packetPadding)
        {
            var networkConfiguration = settings.GetNetworkConfigParameters();

            var state = new InternalState
            {
                ReceiveQueueCapacity = networkConfiguration.receiveQueueCapacity,
                SendQueueCapacity = networkConfiguration.sendQueueCapacity,
            };

            m_InternalState = new NativeReference<InternalState>(state, Allocator.Persistent);

            var bufferSize = networkConfiguration.maxMessageSize + UnsafeUtility.SizeOf<PacketMetadata>() + (int)Binding.Baselib_RegisteredNetwork_Endpoint_MaxSize;

            m_ReceiveBuffers = new UnsafeBaselibNetworkArray(state.ReceiveQueueCapacity, bufferSize);
            m_SendBuffers = new UnsafeBaselibNetworkArray(state.SendQueueCapacity, bufferSize);

            return 0;
        }

        internal void CreateQueues(int sendQueueCapacity, int receiveQueueCapacity, int payloadSize, out PacketsQueue sendQueue, out PacketsQueue receiveQueue)
        {
            var metadataSize = UnsafeUtility.SizeOf<PacketMetadata>();
            var endpointSize = UnsafeUtility.SizeOf<NetworkEndpoint>();

            // The registered network endpoint size might require some extra bytes
            endpointSize = math.max(endpointSize, (int)Binding.Baselib_RegisteredNetwork_Endpoint_MaxSize);

            m_PacketBufferLayout = new PacketBufferLayout
            {
                MetadataOffset = 0,
                EndpointOffset = (uint)metadataSize,
                PayloadOffset = (uint)(metadataSize + endpointSize),
            };

            receiveQueue = new PacketsQueue(
                metadataSize,
                payloadSize,
                endpointSize,
                receiveQueueCapacity,
                GetTempPacketBuffersArray(receiveQueueCapacity, ref m_ReceiveBuffers));

            m_ReceiveQueue = receiveQueue;

            sendQueue = new PacketsQueue(
                metadataSize,
                payloadSize,
                endpointSize,
                sendQueueCapacity,
                GetTempPacketBuffersArray(sendQueueCapacity, ref m_SendBuffers));

            if (m_SendBuffers.ElementSize < metadataSize + payloadSize + endpointSize)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException($"The required buffer size ({metadataSize + payloadSize + endpointSize}) does not fit in the allocated send buffers ({m_SendBuffers.ElementSize})");
#else
                DebugLog.ErrorCreateQueueWrongSendSize(metadataSize + payloadSize + endpointSize, m_SendBuffers.ElementSize);
#endif
            }

            if (m_ReceiveBuffers.ElementSize < metadataSize + payloadSize + endpointSize)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException($"The required buffer size ({metadataSize + payloadSize + endpointSize}) does not fit in the allocated receive buffers ({m_ReceiveBuffers.ElementSize})");
#else
                DebugLog.ErrorCreateQueueWrongReceiveSize(metadataSize + payloadSize + endpointSize, m_ReceiveBuffers.ElementSize);
#endif
            }
        }

        private unsafe NativeArray<PacketBuffer> GetTempPacketBuffersArray(int capacity, ref UnsafeBaselibNetworkArray buffers)
        {
            var buffersList = new NativeArray<PacketBuffer>(capacity, Allocator.Temp, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < capacity; i++)
            {
                var bufferPtr = buffers.GetBufferPtr(i);
                buffersList[i] = new PacketBuffer
                {
                    Metadata = bufferPtr + (int)m_PacketBufferLayout.MetadataOffset,
                    Endpoint = bufferPtr + (int)m_PacketBufferLayout.EndpointOffset,
                    Payload  = bufferPtr + (int)m_PacketBufferLayout.PayloadOffset,
                };
            }

            return buffersList;
        }

        public unsafe void Dispose()
        {
            CloseSocket(m_InternalState.Value.Socket);

            m_SendBuffers.Dispose();
            m_ReceiveBuffers.Dispose();

            m_InternalState.Dispose();
        }

        [BurstCompile]
        struct FlushSendJob : IJob
        {
            public PacketsQueue SendQueue;
            [NativeDisableContainerSafetyRestriction]
            public NativeReference<InternalState> InternalState;
            public UnsafeBaselibNetworkArray SendBuffers;
            public PacketBufferLayout PacketBufferLayout;

            public unsafe void Execute()
            {
                ScheduleSendRequests();
                ProcessSendRequests();
                ProcessSendResults();
            }

            private unsafe void ScheduleSendRequests()
            {
                var error = default(ErrorState);
                var count = SendQueue.Count;

                var requests = new NativeList<NetworkRequest>(count, Allocator.Temp);
                var requestsPtr = (NetworkRequest*)requests.GetUnsafePtr();
                var queueIndices = new NativeList<int>(count, Allocator.Temp);

                // Prepare all send requests.
                for (int i = 0; i < count; i++)
                {
                    var packetProcessor = SendQueue[i];
                    if (packetProcessor.Length == 0)
                        continue;

                    var request = GetRequest(packetProcessor.m_BufferIndex, ref SendBuffers, ref PacketBufferLayout);

                    request.payload.offset += (uint)packetProcessor.Offset;
                    request.payload.data += packetProcessor.Offset;
                    request.payload.size = (uint)packetProcessor.Length;

                    // Convert endpoint to baselib format.
                    var endpoint = *(Binding.Baselib_NetworkAddress*)request.remoteEndpoint.slice.data;
                    Binding.Baselib_RegisteredNetwork_Endpoint_Create(&endpoint, request.remoteEndpoint.slice, &error);
                    if (error.code != ErrorCode.Success)
                    {
                        DebugLog.ErrorBaselib("Unexpected endpoint format.", error);
                        packetProcessor.Drop();
                        continue;
                    }

                    requests.Add(request);
                    queueIndices.Add(i);
                }

                // Schedule all the send requests.
                var scheduledCount = (int)Binding.Baselib_RegisteredNetwork_Socket_UDP_ScheduleSend(
                    InternalState.Value.Socket, requestsPtr, (uint)requests.Length, &error);
                if (error.code != ErrorCode.Success)
                {
                    DebugLog.ErrorBaselib("Couldn't schedule send requests.", error);
                    MarkSocketAsNeedingRecreate(ref InternalState);
                    return;
                }

                // Dequeue packets that were sent. Normally that should be all packets since we
                // ensure we never schedule more sends than what baselib can accomodate. Still, if
                // not all packets were scheduled, we can't dequeue them in this manner here because
                // these sends will never be completed and we'll never release their buffers.
                for (int i = 0; i < scheduledCount; i++)
                {
                    SendQueue.DequeuePacketNoRelease(queueIndices[i]);
                }
            }

            private unsafe void ProcessSendRequests()
            {
                var error = default(ErrorState);
                var socket = InternalState.Value.Socket;
                var count = SendQueue.Capacity;

                // Logically we'd just loop indefinitely until the status is not pending. But to
                // avoid any risk of deadlock, limit the number of calls to the queue capacity.
                for (int i = 0; i < count; i++)
                {
                    var status = Binding.Baselib_RegisteredNetwork_Socket_UDP_ProcessSend(socket, &error);
                    if (error.code != ErrorCode.Success)
                    {
                        DebugLog.ErrorBaselib("Couldn't process scheduled send request.", error);
                        MarkSocketAsNeedingRecreate(ref InternalState);
                        return;
                    }

                    if (status != Binding.Baselib_RegisteredNetwork_ProcessStatus.Pending)
                        break;
                }
            }

            private unsafe void ProcessSendResults()
            {
                var results = new NativeArray<CompletionResult>(SendQueue.Capacity, Allocator.Temp);
                var resultsPtr = (CompletionResult*)results.GetUnsafePtr();

                var error = default(ErrorState);
                var count = (int)Binding.Baselib_RegisteredNetwork_Socket_UDP_DequeueSend(
                    InternalState.Value.Socket, resultsPtr, (uint)results.Length, &error);
                if (error.code != ErrorCode.Success)
                {
                    DebugLog.ErrorBaselib("Couldn't dequeue send results.", error);
                    MarkSocketAsNeedingRecreate(ref InternalState);
                    return;
                }

                // Re-enqueue all completed sends. This way if there's any other layer below, it
                // will have an opportunity to process it (e.g. the bottom layer can release the
                // buffer when it clears the send queue).
                for (int i = 0; i < count; i++)
                {
                    var bufferIndex = results[i].requestUserdata.ToInt32() - 1;
                    SendQueue.EnqueuePacket(bufferIndex, out _);
                }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // Check for any failed sends.
                var failedCount = 0;
                for (int i = 0; i < count; i++)
                {
                    if (results[i].status == Binding.Baselib_RegisteredNetwork_CompletionStatus.Failed)
                        failedCount++;
                }

                if (failedCount > 0)
                    DebugLog.BaselibFailedToSendPackets(failedCount);
#endif
            }
        }

        [BurstCompile]
        struct ReceiveJob : IJob
        {
            public PacketsQueue ReceiveQueue;
            [NativeDisableContainerSafetyRestriction]
            public NativeReference<InternalState> InternalState;
            public UnsafeBaselibNetworkArray ReceiveBuffers;
            public OperationResult Result;
            public PacketBufferLayout PacketBufferLayout;
            public long UpdateTime;

            public unsafe void Execute()
            {
                // Update last update time of internal state.
                var state = InternalState.Value;
                state.LastUpdateTime = UpdateTime;
                InternalState.Value = state;

                var socket = InternalState.Value.Socket;

                // If we just recreated the socket, need to reset the receive queues.
                if (InternalState.Value.LastSocketRecreateTime == UpdateTime)
                    ResetReceiveQueue(ref ReceiveQueue);

                // Baselib requires receives to be scheduled before the message arrives, so we keep them always scheduled.
                // At this point the process of the received messages has already happened, users accessed them as events
                // and their buffers should be released.
                if (ScheduleAllReceives(socket, ref ReceiveQueue, ref ReceiveBuffers, ref PacketBufferLayout) != 0)
                {
                    MarkSocketAsNeedingRecreate(ref InternalState);
                    return;
                }

                var error = default(ErrorState);

                var pollCount = 0;
                var status = default(Binding.Baselib_RegisteredNetwork_ProcessStatus);
                while ((status = Binding.Baselib_RegisteredNetwork_Socket_UDP_ProcessRecv(socket, &error)) == Binding.Baselib_RegisteredNetwork_ProcessStatus.Pending
                       && pollCount++ < ReceiveQueue.Capacity) {}

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (status == Binding.Baselib_RegisteredNetwork_ProcessStatus.Pending)
                {
                    DebugLog.LogWarning("There are pending receive packets after the baselib process receive");
                }
#endif

                var results = stackalloc CompletionResult[(int)k_RequestsBatchSize];

                var dequeueAgain = true;
                var totalCount = 0;
                var failedCount = 0;

                while (dequeueAgain)
                {
                    // Pop Completed Requests off the CompletionQ
                    var count = (int)Binding.Baselib_RegisteredNetwork_Socket_UDP_DequeueRecv(socket, results, k_RequestsBatchSize, &error);
                    if (error.code != ErrorCode.Success)
                    {
                        MarkSocketAsNeedingRecreate(ref InternalState);
                        Result.ErrorCode = (int)error.code; // TODO should we really return baselib error codes to users?
                        return;
                    }

                    totalCount += count;

                    for (int i = 0; i < count; i++)
                    {
                        var bufferIndex = (int)results[i].requestUserdata - 1;

                        if (results[i].status == Binding.Baselib_RegisteredNetwork_CompletionStatus.Failed)
                        {
                            failedCount++;
                            continue;
                        }

                        var receivedBytes = (int)results[i].bytesTransferred;
                        if (receivedBytes <= 0 || receivedBytes > ReceiveQueue.PayloadCapacity)
                            continue;

                        if (!ReceiveQueue.EnqueuePacket(bufferIndex, out var packetProcessor))
                        {
                            Result.ErrorCode = (int)Error.StatusCode.NetworkReceiveQueueFull;
                            DebugLog.LogError("Could not enqueue received packet.");
                            return;
                        }

                        // TODO: These lines are not burst compatible due to baselib api.
                        // // The receivied endpoint is in RegisteredNetwork format, we need to parse it to generic.
                        // var endpointSlice = ReceiveBuffers.AtIndexAsSlice(bufferIndex);
                        // endpointSlice.offset = PacketBufferLayout.EndpointOffset;
                        // endpointSlice.data = new IntPtr((byte*)endpointSlice.data + PacketBufferLayout.EndpointOffset);
                        // if (!ConvertEndpointBufferToGeneric(endpointSlice))
                        //     receivedBytes = 0;

                        packetProcessor.SetUnsafeMetadata(receivedBytes);
                    }

                    // If we filled our batch with requests, there might be more to dequeue.
                    dequeueAgain = count == (int)k_RequestsBatchSize;
                }

                // All receive requests being marked as failed is as close as we're going to get to
                // a signal that the socket has failed with the current baselib API (at least on
                // platforms that use the basic POSIX sockets implementation under the hood). Note
                // that we can't do the same check on send requests, since there might be legit
                // scenarios where sends are failing temporarily without the socket being borked.
                if (totalCount > 0 && totalCount == failedCount)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    DebugLog.LogError("All socket receive requests were marked as failed, likely because socket itself has failed.");
#endif
                    MarkSocketAsNeedingRecreate(ref InternalState);
                }

                ConvertEndpointsToGeneric();
            }

            private void ConvertEndpointsToGeneric()
            {
                var count = ReceiveQueue.Count;
                for (int i = 0; i < ReceiveQueue.Count; i++)
                {
                    var packetProcessor = ReceiveQueue[i];
                    var request = GetRequest(packetProcessor.m_BufferIndex, ref ReceiveBuffers, ref PacketBufferLayout);
                    if (!ConvertEndpointBufferToGeneric(request.remoteEndpoint.slice))
                        packetProcessor.Drop();
                }
            }
        }

        /// <inheritdoc/>
        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dep)
        {
            // TODO: Move this inside the receive job (requires MTT-3703).
            if (m_InternalState.Value.SocketStatus == SocketStatus.SocketNeedsRecreate)
                RecreateSocket(arguments.Time, ref arguments.ReceiveQueue);

            if (m_InternalState.Value.SocketStatus == SocketStatus.SocketFailed)
            {
                arguments.ReceiveResult.ErrorCode = (int)Error.StatusCode.NetworkSocketError;
                return dep;
            }

            return new ReceiveJob
            {
                InternalState = m_InternalState,
                ReceiveQueue = arguments.ReceiveQueue,
                ReceiveBuffers = m_ReceiveBuffers,
                Result = arguments.ReceiveResult,
                PacketBufferLayout = m_PacketBufferLayout,
                UpdateTime = arguments.Time,
            }.Schedule(dep);
        }

        /// <inheritdoc/>
        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dep)
        {
            if (m_InternalState.Value.SocketStatus != SocketStatus.SocketNormal)
                return dep;

            return new FlushSendJob
            {
                InternalState = m_InternalState,
                SendQueue = arguments.SendQueue,
                SendBuffers = m_SendBuffers,
                PacketBufferLayout = m_PacketBufferLayout,
            }.Schedule(dep);
        }

        /// <inheritdoc/>
        public unsafe int Bind(NetworkEndpoint endpoint)
        {
            var state = m_InternalState.Value;

            CloseSocket(state.Socket);

            var result = CreateSocket(state.SendQueueCapacity, state.ReceiveQueueCapacity, endpoint, out var newSocket);
            if (result == 0)
            {
                state.Socket = newSocket;
                state.SocketStatus = SocketStatus.SocketNormal;
                state.BindEndpoint = endpoint;

                // Need to release acquisition status of all packet buffers since we closed their socket.
                ResetReceiveQueue(ref m_ReceiveQueue);

                result = ScheduleAllReceives(newSocket, ref m_ReceiveQueue, ref m_ReceiveBuffers, ref m_PacketBufferLayout);
            }
            else
            {
                state.Socket = default;
                state.SocketStatus = SocketStatus.SocketFailed;
            }

            m_InternalState.Value = state;

            return result;
        }

        /// <inheritdoc/>
        public int Listen()
        {
            return 0;
        }

        private static unsafe int CreateSocket(int sendQueueCapacity, int receiveQueueCapacity, NetworkEndpoint endpoint, out NetworkSocket socket)
        {
            var error = default(ErrorState);
            socket = Binding.Baselib_RegisteredNetwork_Socket_UDP_Create(
                &endpoint.BaselibAddress,
                Binding.Baselib_NetworkAddress_AddressReuse.DoNotAllow,
                checked((uint)sendQueueCapacity),
                checked((uint)receiveQueueCapacity),
                &error);

            if (error.code != ErrorCode.Success)
            {
                DebugLog.ErrorBaselibBind(error, endpoint.Port);
                return (int)Error.StatusCode.NetworkSocketError;
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AllSockets.OpenSockets.Add(new SocketList.SocketId { socket = socket });
#endif
            return 0;
        }

        private static void CloseSocket(NetworkSocket socket)
        {
            if (socket.handle != IntPtr.Zero)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AllSockets.OpenSockets.Remove(new SocketList.SocketId { socket = socket });
#endif
                Binding.Baselib_RegisteredNetwork_Socket_UDP_Close(socket);
            }
        }

        private void RecreateSocket(long updateTime, ref PacketsQueue receiveQueue)
        {
            var state = m_InternalState.Value;

            // If we already recreated the socket in the last update or if we hit the limit of
            // socket recreations, then something's wrong at the socket layer and recreating it
            // likely won't solve the issue. Just fail the socket in that scenario.
            if (state.LastSocketRecreateTime == state.LastUpdateTime || state.NumSocketRecreate >= k_MaxNumSocketRecreate)
            {
                DebugLog.LogError("Unrecoverable socket failure. An unknown condition is preventing the application from reliably creating sockets.");
                state.SocketStatus = SocketStatus.SocketFailed;
            }
            else
            {
                DebugLog.LogWarning("Socket error encountered; attempting recovery by creating a new one.");
                state.LastSocketRecreateTime = updateTime;
                state.NumSocketRecreate++;

                CloseSocket(state.Socket);
                var result = CreateSocket(state.SendQueueCapacity, state.ReceiveQueueCapacity, state.BindEndpoint, out var newSocket);
                if (result == 0)
                {
                    state.Socket = newSocket;
                    state.SocketStatus = SocketStatus.SocketNormal;
                }
            }

            m_InternalState.Value = state;
        }

        private static void MarkSocketAsNeedingRecreate(ref NativeReference<InternalState> internalState)
        {
            var state = internalState.Value;
            state.SocketStatus = SocketStatus.SocketNeedsRecreate;
            internalState.Value = state;
        }

        private static void ResetReceiveQueue(ref PacketsQueue receiveQueue)
        {
            if (receiveQueue.BuffersInUse != 0)
            {
                receiveQueue.Clear();
                receiveQueue.UnsafeResetAcquisitionState();
            }
        }

        private static unsafe bool ConvertEndpointBufferToGeneric(Binding.Baselib_RegisteredNetwork_BufferSlice endpointSlice)
        {
            var endpoint = default(NetworkEndpoint);
            var error = default(ErrorState);


            Binding.Baselib_RegisteredNetwork_Endpoint_GetNetworkAddress(
                new RegisteredNetworkEndpoint { slice = endpointSlice },
                &endpoint.BaselibAddress,
                &error
            );

            if (error.code != (int)ErrorCode.Success)
            {
                DebugLog.ErrorBaselib("Create Registered Endpoint", error);
                return false;
            }

            *(NetworkEndpoint*)endpointSlice.data = endpoint;
            return true;
        }

        private unsafe static NetworkRequest GetRequest(int bufferIndex, ref UnsafeBaselibNetworkArray buffers, ref PacketBufferLayout bufferLayout)
        {
            var bufferSlice = buffers.AtIndexAsSlice(bufferIndex);

            var request = new NetworkRequest
            {
                payload = bufferSlice,
                remoteEndpoint = new RegisteredNetworkEndpoint
                {
                    slice = bufferSlice,
                },
                requestUserdata = new IntPtr(bufferIndex + 1),
            };

            request.payload.offset = bufferLayout.PayloadOffset;
            request.payload.data = new IntPtr((byte*)request.payload.data + bufferLayout.PayloadOffset);

            request.remoteEndpoint.slice.offset = bufferLayout.EndpointOffset;
            request.remoteEndpoint.slice.data = new IntPtr((byte*)request.remoteEndpoint.slice.data + bufferLayout.EndpointOffset);
            request.remoteEndpoint.slice.size = Binding.Baselib_RegisteredNetwork_Endpoint_MaxSize;

            return request;
        }

        private static unsafe int ScheduleAllReceives(NetworkSocket socket, ref PacketsQueue receiveQueue, ref UnsafeBaselibNetworkArray receiveBuffers, ref PacketBufferLayout packetBufferLayout)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (packetBufferLayout.PayloadOffset == 0 && packetBufferLayout.MetadataOffset == 0)
                throw new ArgumentNullException("Invalid packetBufferLayour");
#endif
            var error = default(ErrorState);

            var requests = stackalloc NetworkRequest[(int)k_RequestsBatchSize];
            var count = 0;

            do
            {
                count = 0;
                while (count < k_RequestsBatchSize && receiveQueue.TryAcquireBuffer(out var bufferIndex))
                {
                    requests[count++] = GetRequest(bufferIndex, ref receiveBuffers, ref packetBufferLayout);
                }

                Binding.Baselib_RegisteredNetwork_Socket_UDP_ScheduleRecv(socket, requests, (uint)count, &error);

                if (error.code != ErrorCode.Success)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    DebugLog.ErrorBaselib("Schedule Receive", error);
#endif
                    return (int)Error.StatusCode.NetworkSocketError;
                }
            }
            while (count == k_RequestsBatchSize);

            return 0;
        }
    }
}
#endif // !UNITY_WEBGL || UNITY_EDITOR
