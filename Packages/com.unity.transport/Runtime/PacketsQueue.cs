using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine.Assertions;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Networking.Transport.Utilities.LowLevel.Unsafe;

namespace Unity.Networking.Transport
{
    /// <summary>A queue of packets with an internal pool of preallocated packet buffers.</summary>
    public unsafe struct PacketsQueue : IDisposable
    {
        private NativeList<int> m_Queue;
        private UnsafeAtomicFreeList m_FreeList;
        [NativeDisableParallelForRestriction] private NativeArray<PacketBuffer> m_Buffers;
        [NativeDisableUnsafePtrRestriction] private IntPtr m_PayloadsPtr;
        [NativeDisableUnsafePtrRestriction] private IntPtr m_EndpointsPtr;
        private int m_Capacity;
        private int m_MetadataSize;
        private int m_PayloadSize;
        private int m_EndpointSize;
        private int m_DefaultDataOffset;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [NativeDisableUnsafePtrRestriction] private IntPtr m_BuffersUsage;
#endif

        internal int BuffersInUse => m_FreeList.InUse;
        internal int BuffersAvailable => m_Capacity - m_FreeList.InUse;
        internal int PayloadCapacity => m_PayloadSize;
        internal int EndpointCapacity => m_EndpointSize;

        /// <summary>Total capacity of the queue.</summary>
        /// <value>Capacity in number of packets.</value>
        public int Capacity => m_Capacity;

        /// <summary>Number of packets currently in the queue.</summary>
        /// <value>Count in number of packets.</value>
        public int Count => m_Queue.Length;

        /// <summary>Whether the queue has been created or not.</summary>
        /// <value>True if created, false otherwise.</value>
        public bool IsCreated => m_Buffers.IsCreated;

        internal ref PacketMetadata GetMetadataRef(int bufferIndex) => ref *(PacketMetadata*)(m_Buffers[bufferIndex].Metadata);
        internal ref NetworkEndpoint GetEndpointRef(int bufferIndex) => ref *(NetworkEndpoint*)(m_Buffers[bufferIndex].Endpoint);

        internal PacketBuffer GetPacketBuffer(int bufferIndex) => m_Buffers[bufferIndex];

        private static void Initialize(out PacketsQueue packetsQueue, int metadataSize, int payloadSize, int endpointSize, int capacity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (metadataSize != UnsafeUtility.SizeOf<PacketMetadata>())
            {
                throw new ArgumentException($"Metadata size ({metadataSize}) must be {UnsafeUtility.SizeOf<PacketMetadata>()}");
            }
#endif
            packetsQueue.m_Capacity = capacity;
            packetsQueue.m_MetadataSize = metadataSize;
            packetsQueue.m_PayloadSize = payloadSize;
            packetsQueue.m_EndpointSize = endpointSize;
            packetsQueue.m_DefaultDataOffset = 0;
            packetsQueue.m_PayloadsPtr = IntPtr.Zero;
            packetsQueue.m_EndpointsPtr = IntPtr.Zero;
            packetsQueue.m_FreeList = new UnsafeAtomicFreeList(capacity, Allocator.Persistent);
            packetsQueue.m_Buffers = new NativeArray<PacketBuffer>(capacity, Allocator.Persistent);
            packetsQueue.m_Queue = new NativeList<int>(capacity, Allocator.Persistent);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var buffersUsageSize = (int)math.ceil((float)packetsQueue.m_Capacity / UnsafeUtility.SizeOf<ulong>());
            packetsQueue.m_BuffersUsage = new IntPtr(UnsafeUtility.Malloc(buffersUsageSize, UnsafeUtility.AlignOf<ulong>(), Allocator.Persistent));
            UnsafeUtility.MemClear((void*)packetsQueue.m_BuffersUsage, buffersUsageSize);
#endif
        }

        /// <summary>
        /// Created a pool and allocated the buffers
        /// </summary>
        /// <param name="capacity">The ammount of packets available</param>
        /// <param name="payloadSize">Maximum size of packet payloads</param>
        internal PacketsQueue(int capacity, int payloadSize = NetworkParameterConstants.AbsoluteMaxMessageSize)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (capacity <= 0)
                throw new ArgumentException($"{nameof(capacity)} must be > 0");
#endif
            var metadataSize = UnsafeUtility.SizeOf<PacketMetadata>();
            var endpointSize = UnsafeUtility.SizeOf<NetworkEndpoint>();

            // PacketMetadata is prepended to the payload
            var payloadAndMetadataSize = payloadSize + metadataSize;

            Initialize(out this,
                metadataSize,
                payloadSize,
                endpointSize,
                capacity);

            payloadAndMetadataSize = AddPaddingToAlign(payloadAndMetadataSize, JobsUtility.CacheLineSize);
            endpointSize = AddPaddingToAlign(endpointSize, JobsUtility.CacheLineSize);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.IsTrue(payloadAndMetadataSize >= m_MetadataSize + m_PayloadSize);
            Assert.IsTrue(endpointSize >= m_EndpointSize);
#endif

            m_PayloadsPtr = new IntPtr(UnsafeUtility.Malloc(capacity * payloadAndMetadataSize, JobsUtility.CacheLineSize, Allocator.Persistent));
            m_EndpointsPtr = new IntPtr(UnsafeUtility.Malloc(capacity * endpointSize, JobsUtility.CacheLineSize, Allocator.Persistent));

            new FillBufferPointers
            {
                Buffers = m_Buffers,
                Payloads = m_PayloadsPtr,
                Endpoints = m_EndpointsPtr,
                PayloadAndMetadataSize = payloadAndMetadataSize,
                EndpointSize = endpointSize,
            }.Run();
        }

        /// <summary>
        /// Creates a new pool using preallocated memory
        /// </summary>
        /// <param name="metadataSize">The size in bytes of a Metadata buffer</param>
        /// <param name="payloadSize">The size in bytes of a Payload buffer</param>
        /// <param name="endpointSize">The size in bytes of a Endpoint buffer</param>
        /// <param name="capacity">The ammount of packets available</param>
        /// <param name="buffers">An array containing the preallocated PacketBuffers to use</param>
        internal PacketsQueue(int metadataSize, int payloadSize, int endpointSize, int capacity, NativeArray<PacketBuffer> buffers)
        {
            Initialize(out this,
                metadataSize,
                payloadSize,
                endpointSize,
                capacity);

            buffers.CopyTo(m_Buffers);
        }

        private static int AddPaddingToAlign(int size, int alignment)
        {
            return alignment * (int)math.ceil((float)size / (float)alignment);
        }

        [BurstCompile]
        private struct FillBufferPointers : IJob
        {
            public NativeArray<PacketBuffer> Buffers;
            [NativeDisableUnsafePtrRestriction] public IntPtr Payloads;
            [NativeDisableUnsafePtrRestriction] public IntPtr Endpoints;
            public int PayloadAndMetadataSize;
            public int EndpointSize;

            public void Execute()
            {
                var payloadsPtr = (byte*)Payloads;
                var endpointsPtr = (byte*)Endpoints;
                var metadataSize = UnsafeUtility.SizeOf<PacketMetadata>();

                for (int i = 0; i < Buffers.Length; i++)
                {
                    Buffers[i] = new PacketBuffer
                    {
                        Metadata = new IntPtr(payloadsPtr + i * PayloadAndMetadataSize),
                        Payload = new IntPtr(payloadsPtr + i * PayloadAndMetadataSize + metadataSize),
                        Endpoint = new IntPtr(endpointsPtr + i * EndpointSize),
                    };
                }
            }
        }

        public void Dispose()
        {
            if (IsCreated)
            {
                if (m_PayloadsPtr != IntPtr.Zero)
                {
                    UnsafeUtility.Free((void*)m_PayloadsPtr, Allocator.Persistent);
                    m_PayloadsPtr = IntPtr.Zero;
                }

                if (m_EndpointsPtr != IntPtr.Zero)
                {
                    UnsafeUtility.Free((void*)m_EndpointsPtr, Allocator.Persistent);
                    m_EndpointsPtr = IntPtr.Zero;
                }

                m_FreeList.Dispose();
                m_Buffers.Dispose();
                m_Queue.Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                UnsafeUtility.Free((void*)m_BuffersUsage, Allocator.Persistent);
                m_BuffersUsage = IntPtr.Zero;
#endif
            }
        }

        /// <summary>
        /// Gets the packet processor for the packet at the given index.
        /// </summary>
        /// <param name="packetIndex">Index of the packet in the queue.</param>
        /// <returns>Packet processor for the packet at the provided index.</returns>
        /// <exception cref="IndexOutOfRangeException">If the index is not valid.</exception>
        public PacketProcessor this[int packetIndex]
        {
            get
            {
                var bufferIndex = m_Queue[packetIndex];

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (bufferIndex < 0)
                    throw new IndexOutOfRangeException(string.Format("Cannot access the queued packet with index {0} because it has been released", packetIndex));
#endif

                return GetPacketProcessor(bufferIndex);
            }
        }

        internal PacketProcessor GetPacketProcessor(int bufferIndex)
        {
            return new PacketProcessor
            {
                m_Queue = this,
                m_BufferIndex = bufferIndex,
            };
        }

        /// <summary>
        /// Acquires a new packet buffer from the packets pool if there are any available.
        /// </summary>
        /// <param name="packetProcessor">Packet processor for the new packet.</param>
        /// <returns>True if a new packet was enqueued, false otherwise.</returns>
        public unsafe bool EnqueuePacket(out PacketProcessor packetProcessor)
        {
            if (TryAcquireBuffer(out var bufferIndex))
            {
                GetMetadataRef(bufferIndex) = new PacketMetadata { DataOffset = m_DefaultDataOffset };
                EnqueueAndGetProcessor(bufferIndex, out packetProcessor);
                return true;
            }

            packetProcessor = default;
            return false;
        }

        // Baselib acquires all the packets at initialization, meaning that it needs to enqueue
        // a packet skiping the acquiring step. The bufferIndex is already known.
        internal bool EnqueuePacket(int bufferIndex, out PacketProcessor packetProcessor)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsUsed(bufferIndex))
                throw new InvalidOperationException($"Trying to enqueue a packet ({bufferIndex}) but it has not been acquired");
#endif

            EnqueueAndGetProcessor(bufferIndex, out packetProcessor);
            return true;
        }

        private void EnqueueAndGetProcessor(int bufferIndex, out PacketProcessor packetProcessor)
        {
            m_Queue.Add(bufferIndex);
            packetProcessor = GetPacketProcessor(bufferIndex);
            GetMetadataRef(bufferIndex).DataCapacity = PayloadCapacity;
        }

        /// <summary>
        /// Copies all the packets from the given queue into this one. Note that no error is raised
        /// if not all packets could be copied. It is the responsibility of the caller to ensure
        /// that the queue can fit all the packets from the given queue.
        /// </summary>
        /// <param name="originQueue">Queue that contains the packets to enqueue.</param>
        public void EnqueuePackets(ref PacketsQueue originQueue)
        {
            var count = originQueue.Count;

            for (int i = 0; i < count; i++)
            {
                var packet = originQueue[i];

                if (packet.Length == 0)
                    continue;

                if (EnqueuePacket(out var packetProcessor))
                {
                    packet.CopyPayload((byte*)packetProcessor.GetUnsafePayloadPtr() + packetProcessor.Offset, packet.Length);
                    packetProcessor.SetUnsafeMetadata(packet.Length, packetProcessor.Offset);
                    packetProcessor.ConnectionRef = packet.ConnectionRef;
                    packetProcessor.EndpointRef = packet.EndpointRef;
                }
                else
                {
                    // There are no more available packet buffers so we can't continue copying.
                    return;
                }
            }
        }

        internal int DequeuePacketNoRelease(int packetIndex)
        {
            var bufferIndex = m_Queue[packetIndex];
            m_Queue[packetIndex] = -1;
            return bufferIndex;
        }

        private unsafe void DropPacket(int packetIndex)
        {
            var bufferIndex = DequeuePacketNoRelease(packetIndex);

            if (bufferIndex < 0)
                return;

            GetMetadataRef(bufferIndex) = default;
            ReleaseBuffer(bufferIndex);
        }

        /// <summary>
        /// Removes and releases all the packets in the queue.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < m_Queue.Length; i++)
            {
                DropPacket(i);
            }

            m_Queue.Clear();
        }

        internal void UnsafeResetAcquisitionState()
        {
            m_FreeList.Reset();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            for (int i = 0; i < Capacity; i++)
            {
                if (IsUsed(i))
                    MarkAsNotUsed(i);
            }
#endif
        }

        internal bool TryAcquireBuffer(out int bufferIndex)
        {
            bufferIndex = m_FreeList.Pop();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (bufferIndex >= 0)
            {
                CheckIndex(bufferIndex);
                MarkAsUsed(bufferIndex);
            }
#endif
            return bufferIndex >= 0;
        }

        internal void ReleaseBuffer(int bufferIndex)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckIndex(bufferIndex);
            MarkAsNotUsed(bufferIndex);
#endif
            m_FreeList.Push(bufferIndex);
        }

        internal void SetDefaultDataOffset(int offset)
        {
            m_DefaultDataOffset = offset;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private void CheckIndex(int bufferIndex)
        {
            if (bufferIndex < 0 || bufferIndex >= m_Capacity)
                throw new IndexOutOfRangeException($"The buffer index {bufferIndex} is out of range [0, {m_Capacity - 1}]");
        }

        private static readonly int k_ULongSizeInBits = UnsafeUtility.SizeOf<ulong>() * 8;

        internal bool IsUsed(int bufferIndex)
        {
            var intIndex = bufferIndex / k_ULongSizeInBits;
            var bitIndex = bufferIndex % k_ULongSizeInBits;

            var usage = (ulong)Interlocked.Read(ref ((long*)m_BuffersUsage)[intIndex]);

            return (usage & (1UL << bitIndex)) > 0;
        }

        private void MarkAsUsed(int bufferIndex)
        {
            var intIndex = bufferIndex / k_ULongSizeInBits;
            var bitIndex = bufferIndex % k_ULongSizeInBits;
            do
            {
                var usage = ((ulong*)m_BuffersUsage)[intIndex];
                if ((usage & (1UL << bitIndex)) > 0)
                {
                    // This should never happen. If we hit this exception it might indicate
                    // a failure in the UnsafeAtomicFreeList implementation or bad memory
                    throw new InvalidOperationException($"Trying to acquire buffer with index {bufferIndex} but it was already marked as used");
                }
                var newUsage = usage | (1UL << bitIndex);
                if (Interlocked.CompareExchange(ref ((long*)m_BuffersUsage)[intIndex], (long)newUsage, (long)usage) == (long)usage)
                    break;
            }
            while (true);
        }

        private void MarkAsNotUsed(int bufferIndex)
        {
            var intIndex = bufferIndex / k_ULongSizeInBits;
            var bitIndex = bufferIndex % k_ULongSizeInBits;
            do
            {
                var usage = ((ulong*)m_BuffersUsage)[intIndex];
                if ((usage & (1UL << bitIndex)) == 0)
                {
                    throw new InvalidOperationException($"Trying to release a buffer with index {bufferIndex} but it was already marked as not used");
                }
                var newUsage = usage & ~(1UL << bitIndex);
                if (Interlocked.CompareExchange(ref ((long*)m_BuffersUsage)[intIndex], (long)newUsage, (long)usage) == (long)usage)
                    break;
            }
            while (true);
        }

#endif
    }
}
