using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Networking.Transport
{
    internal struct NetworkDriverSender : IDisposable
    {
        private PacketsQueue m_SendQueue;
        private NativeQueue<int> m_PendingSendQueue;

        // It makes more sense to have the SendQueue in the NetworkStack, however the concurrent
        // version of the NetworkDriverSender requires its pool to acquire the buffers, so for now
        // we keep it here.
        internal PacketsQueue SendQueue => m_SendQueue;

        internal NetworkDriverSender(PacketsQueue sendQueue)
        {
            m_SendQueue = sendQueue;
            m_PendingSendQueue = new NativeQueue<int>(Allocator.Persistent);
        }

        public void Dispose()
        {
            if (m_PendingSendQueue.IsCreated)
            {
                m_PendingSendQueue.Dispose();
                m_SendQueue.Dispose();
            }
        }

        internal JobHandle FlushPackets(JobHandle dependency)
        {
            return new DequeuePacketsJob
            {
                Queue = m_SendQueue,
                PendingSendQueue = m_PendingSendQueue,
            }
                .Schedule(dependency);
        }

        [BurstCompile]
        private struct DequeuePacketsJob : IJob
        {
            public PacketsQueue Queue;
            public NativeQueue<int> PendingSendQueue;

            public void Execute()
            {
                var count = PendingSendQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    Queue.EnqueuePacket(PendingSendQueue.Dequeue(), out _);
                }
            }
        }

        internal Concurrent ToConcurrent()
        {
            return new Concurrent
            {
                m_SendQueue = m_SendQueue,
                m_PendingSendQueue = m_PendingSendQueue.AsParallelWriter(),
            };
        }

        internal struct Concurrent
        {
            [ReadOnly] internal PacketsQueue m_SendQueue;
            internal NativeQueue<int>.ParallelWriter m_PendingSendQueue;

            public int BeginSend(out NetworkInterfaceSendHandle sendHandle, uint packetSize = 0)
            {
                sendHandle = default;

                if (packetSize == 0)
                    packetSize = (uint)m_SendQueue.PayloadCapacity;
                else if (packetSize > m_SendQueue.PayloadCapacity)
                    return (int)Error.StatusCode.NetworkPacketOverflow;

                if (m_SendQueue.TryAcquireBuffer(out var bufferIndex))
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    m_SendQueue.GetMetadataRef(bufferIndex) = default;
#endif

                    var buffer = m_SendQueue.GetPacketBuffer(bufferIndex);

                    sendHandle.id = bufferIndex;
                    sendHandle.data = buffer.Payload;
                    sendHandle.capacity = (int)packetSize;

                    return (int)Error.StatusCode.Success;
                }

                return (int)Error.StatusCode.NetworkSendQueueFull;
            }

            public void AbortSend(ref NetworkInterfaceSendHandle sendHandle)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (m_SendQueue.GetMetadataRef(sendHandle.id) != default)
                {
                    throw new InvalidOperationException("Trying to abort a send of an already EndSend packet.");
                }
#endif
                m_SendQueue.ReleaseBuffer(sendHandle.id);
            }

            public unsafe int EndSend(ref NetworkEndpoint destination, ref NetworkInterfaceSendHandle sendHandle, int padding = 0, ConnectionId connectionId = default)
            {
                var bufferIndex = sendHandle.id;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (!m_SendQueue.IsUsed(bufferIndex))
                {
                    throw new InvalidOperationException(string.Format("Trying to EndSend a packet with a non-acquired buffer (idx: {0}).", bufferIndex));
                }

                if (m_SendQueue.GetMetadataRef(bufferIndex) != default)
                {
                    throw new InvalidOperationException("Trying to EndSend a packet twice.");
                }
#endif

                m_SendQueue.GetMetadataRef(bufferIndex) = new PacketMetadata
                {
                    DataOffset = padding,
                    DataLength = sendHandle.size,
                    DataCapacity = sendHandle.capacity,
                    Connection = connectionId,
                };

                m_SendQueue.GetEndpointRef(bufferIndex) = destination;

                m_PendingSendQueue.Enqueue(bufferIndex);

                return sendHandle.size;
            }
        }
    }
}
