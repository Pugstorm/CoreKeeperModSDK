using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Networking.Transport.Utilities;

namespace Unity.NetCode
{
    /// <summary>
    /// Component present only for ghosts spawned by the client, tracking the latest <see cref="SnapshotDataBuffer"/>
    /// history slot used to store the incoming ghost snapshots from the server.
    /// </summary>
    public struct SnapshotData : IComponentData
    {
        /// <summary>
        /// Internal use only.
        /// </summary>
        public struct DataAtTick
        {
            /// <summary>
            /// Pointer to the snapshot data for which the tick is less than, or equals to, the target tick.
            /// </summary>
            public System.IntPtr SnapshotBefore;
            /// <summary>
            /// Pointer to the snapshot data for which the tick is newer than the target tick.
            /// </summary>
            public System.IntPtr SnapshotAfter;
            /// <summary>
            /// The current fraction used to interpolate/extrapolated the component field for interpolated ghosts.
            /// </summary>
            public float InterpolationFactor;
            /// <summary>
            /// The target server tick we are currently updating or deserializing.
            /// </summary>
            public NetworkTick Tick;
            /// <summary>
            /// The history slot index that contains the ghost snapshot (that is <b>older</b> than the target <see cref="Tick"/>).
            /// </summary>
            public int BeforeIdx;
            /// <summary>
            /// The history slot index that contains the ghost snapshot (that is <b>newer</b> than the target <see cref="Tick"/>).
            /// </summary>
            public int AfterIdx;
            /// <summary>
            /// The required values of the <see cref="GhostComponentAttribute.OwnerSendType"/> property in order for a component to be sent.
            /// The mask depends on the presence and value of the <see cref="GhostOwner"/> component:
            /// <list type="bullet">
            /// <li><see cref="SendToOwnerType.All"/>if the <see cref="GhostOwner"/> is not present on the entity</li>
            /// <li><see cref="SendToOwnerType.SendToOwner"/>if the value of the <see cref="GhostOwner"/> is equals to the <see cref="NetworkId"/> of the client.</li>
            /// <li><see cref="SendToOwnerType.SendToNonOwner"/>if the value of the <see cref="GhostOwner"/> is different than the <see cref="NetworkId"/> of the client.</li>
            /// </list>
            /// </summary>
            public SendToOwnerType RequiredOwnerSendMask;
            /// <summary>
            /// The network id of the client owning the ghost. 0 if the ghost does not have a <see cref="NetCode.GhostOwner"/>.
            /// </summary>
            public int GhostOwner;
        }
        /// <summary>
        /// The size (in bytes) of the ghost snapshots. It is constant after the ghost entity is spawned, and corresponds to the
        /// <see cref="GhostCollectionPrefabSerializer.SnapshotSize"/>.
        /// </summary>
        public int SnapshotSize;
        /// <summary>
        /// The history slot used to store the last received data from the server. It is always less than <see cref="GhostSystemConstants.SnapshotHistorySize"/>.
        /// </summary>
        public int LatestIndex;

        /// <summary>
        /// The latest snapshot server tick received by the client.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns>A valid tick if the buffer is not empty, otherwise 0.</returns>
        internal unsafe NetworkTick GetLatestTick(in DynamicBuffer<SnapshotDataBuffer> buffer)
        {
            if (buffer.Length == 0)
                return NetworkTick.Invalid;
            byte* snapshotData;
            snapshotData = (byte*)buffer.GetUnsafeReadOnlyPtr() + LatestIndex * SnapshotSize;
            return new NetworkTick{SerializedData = *(uint*)snapshotData};
        }
        /// <summary>
        /// The tick of the oldest snapshot received by the client.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns>a valid tick if the buffer is not empty, 0 otherwise </returns>
        internal unsafe NetworkTick GetOldestTick(in DynamicBuffer<SnapshotDataBuffer> buffer)
        {
            if (buffer.Length == 0)
                return NetworkTick.Invalid;
            byte* snapshotData;

            // The snapshot store is a ringbuffer. Once it is full, the entry after "latest" is the oldest (i.e. the next one to be overwritten).
            // That might however be uninitialized (tick 0) so we scan forward from that until we find a valid entry.
            var oldestIndex = (LatestIndex + 1) % GhostSystemConstants.SnapshotHistorySize;
            while (oldestIndex != LatestIndex)
            {
                snapshotData = (byte*)buffer.GetUnsafeReadOnlyPtr() + oldestIndex * SnapshotSize;
                var oldestTick = new NetworkTick{SerializedData = *(uint*)snapshotData};
                if (oldestTick.IsValid)
                    return oldestTick;
                oldestIndex = (oldestIndex + 1) % GhostSystemConstants.SnapshotHistorySize;
            }

            snapshotData = (byte*)buffer.GetUnsafeReadOnlyPtr() + LatestIndex * SnapshotSize;
            return new NetworkTick{SerializedData = *(uint*)snapshotData};
        }
        /// <summary>
        /// Determine it the latest snapshot received by the server has not changes (all changemasks were 0).
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="numChangeUints"></param>
        /// <returns>True if the snapshot has no changes, false otherwise.</returns>
        internal unsafe bool WasLatestTickZeroChange(in DynamicBuffer<SnapshotDataBuffer> buffer, int numChangeUints)
        {
            if (buffer.Length == 0)
                return false;
            byte* snapshotData;
            snapshotData = (byte*)buffer.GetUnsafeReadOnlyPtr() + LatestIndex * SnapshotSize;
            uint* changeMask = (uint*)(snapshotData+4);
            uint anyChange = 0;
            for (int i = 0; i < numChangeUints; ++i)
            {
                anyChange |= changeMask[i];
            }
            return (anyChange == 0);
        }

        /// <summary>
        /// Try to find the two closest received ghost snapshots for a given <paramref name="targetTick"/>,
        /// and fill the <paramref name="data"/> accordingly.
        /// </summary>
        /// <param name="targetTick"></param>
        /// <param name="predictionOwnerOffset"></param>
        /// <param name="localNetworkId"></param>
        /// <param name="targetTickFraction"></param>
        /// <param name="buffer"></param>
        /// <param name="data"></param>
        /// <param name="MaxExtrapolationTicks"></param>
        /// <returns>True if at least one snapshot has been received and if its tick is less or equal the current target tick.</returns>
        internal unsafe bool GetDataAtTick(NetworkTick targetTick, int predictionOwnerOffset,
            int localNetworkId,
            float targetTickFraction, in DynamicBuffer<SnapshotDataBuffer> buffer, out DataAtTick data, uint MaxExtrapolationTicks)
        {
            data = default;
            if (buffer.Length == 0)
                return false;
            var numBuffers = buffer.Length / SnapshotSize;
            int beforeIdx = 0;
            NetworkTick beforeTick = NetworkTick.Invalid;
            int afterIdx = 0;
            NetworkTick afterTick = NetworkTick.Invalid;
            // If last tick is fractional before should not include the tick we are targeting, it should instead be included in after
            if (targetTickFraction < 1)
                targetTick.Decrement();
            // Loop from latest available to oldest available snapshot
            int slot;
            for (slot = 0; slot < numBuffers; ++slot)
            {
                var curIndex = (LatestIndex + GhostSystemConstants.SnapshotHistorySize - slot) % GhostSystemConstants.SnapshotHistorySize;
                var snapshotData = (byte*)buffer.GetUnsafeReadOnlyPtr() + curIndex * SnapshotSize;
                var tick = new NetworkTick{SerializedData = *(uint*)snapshotData};
                if (!tick.IsValid)
                    continue;
                if (tick.IsNewerThan(targetTick))
                {
                    afterTick = tick;
                    afterIdx = curIndex;
                }
                else
                {
                    beforeTick = tick;
                    beforeIdx = curIndex;
                    break;
                }
            }

            if (!beforeTick.IsValid)
            {
                return false;
            }

            data.SnapshotBefore = (System.IntPtr)((byte*)buffer.GetUnsafeReadOnlyPtr() + beforeIdx * SnapshotSize);
            data.Tick = beforeTick;
            data.GhostOwner = predictionOwnerOffset != 0 ? *(int*) (data.SnapshotBefore + predictionOwnerOffset) : 0;
            if (predictionOwnerOffset == 0)
                data.RequiredOwnerSendMask = SendToOwnerType.All;
            else if (localNetworkId == data.GhostOwner)
                data.RequiredOwnerSendMask = SendToOwnerType.SendToOwner;
            else
                data.RequiredOwnerSendMask = SendToOwnerType.SendToNonOwner;
            if (!afterTick.IsValid)
            {
                data.BeforeIdx = beforeIdx;
                var beforeBeforeTick = NetworkTick.Invalid;
                int beforeBeforeIdx = 0;
                if (beforeTick != targetTick || targetTickFraction < 1)
                {
                    for (++slot; slot < numBuffers; ++slot)
                    {
                        var curIndex = (LatestIndex + GhostSystemConstants.SnapshotHistorySize - slot) % GhostSystemConstants.SnapshotHistorySize;
                        var snapshotData = (byte*)buffer.GetUnsafeReadOnlyPtr() + curIndex * SnapshotSize;
                        var tick = new NetworkTick{SerializedData = *(uint*)snapshotData};
                        if (!tick.IsValid)
                            continue;
                        beforeBeforeTick = tick;
                        beforeBeforeIdx = curIndex;
                        break;
                    }
                }
                if (beforeBeforeTick.IsValid)
                {
                    data.AfterIdx = beforeBeforeIdx;
                    data.SnapshotAfter = (System.IntPtr)((byte*)buffer.GetUnsafeReadOnlyPtr() + beforeBeforeIdx * SnapshotSize);

                    if (targetTick.TicksSince(beforeTick) > MaxExtrapolationTicks)
                    {
                        targetTick = beforeTick;
                        targetTick.Add(MaxExtrapolationTicks);
                    }
                    data.InterpolationFactor = (float) (targetTick.TicksSince(beforeBeforeTick)) / (float) (beforeTick.TicksSince(beforeBeforeTick));
                    if (targetTickFraction < 1)
                        data.InterpolationFactor += targetTickFraction / (float) (beforeTick.TicksSince(beforeBeforeTick));
                    data.InterpolationFactor = 1-data.InterpolationFactor;
                }
                else
                {
                    data.AfterIdx = beforeIdx;
                    data.SnapshotAfter = data.SnapshotBefore;
                    data.InterpolationFactor = 0;
                }
            }
            else
            {
                data.BeforeIdx = beforeIdx;
                data.AfterIdx = afterIdx;
                data.SnapshotAfter = (System.IntPtr)((byte*)buffer.GetUnsafeReadOnlyPtr() + afterIdx * SnapshotSize);
                data.InterpolationFactor = (float) (targetTick.TicksSince(beforeTick)) / (float) (afterTick.TicksSince(beforeTick));
                if (targetTickFraction < 1)
                    data.InterpolationFactor += targetTickFraction / (float) (afterTick.TicksSince(beforeTick));
            }

            return true;
        }
    }

    /// <summary>
    /// A data structure used to store ghosts snapshot buffers data content.
    /// Typically around 1-12kb per entity. Thus, we always allocate on the heap.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SnapshotDataBuffer : IBufferElementData
    {
        /// <summary>
        /// An element value.
        /// </summary>
        public byte Value;
    }

    /// <summary>
    /// A data structure used to store ghosts dynamic buffers data content.
    /// BeginArray(SnapshotHistorySize]
    /// uint dataSize, (16 bytes aligned) current serialized data length for each slot. Used for delta compression
    /// EndArray
    /// BeginArray(SnapshotHistorySize]
    ///  for each buffers:
    ///     uint[maskBits] elements change bitmask
    ///     byte[numElements] serialized buffers data
    /// EndArray
    /// The buffer grow in size as necessary to accomodate new data. All slots have the same size, usually larger
    /// than the data size.
    /// The serialized element size is aligned to the 16 bytes boundary
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SnapshotDynamicDataBuffer : IBufferElementData
    {
        /// <summary>
        /// An element value.
        /// </summary>
        public byte Value;
    }

    /// <summary>
    /// Helper class for managing ghost buffers data. Internal use only.
    /// </summary>
    public unsafe struct SnapshotDynamicBuffersHelper
    {
        /// <summary>
        /// Get the size of the header at the beginning of the dynamic snapshot buffer. The size
        /// of the header is constant.
        /// </summary>
        /// <returns></returns>
        static public uint GetHeaderSize()
        {
            return (uint)GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint) * GhostSystemConstants.SnapshotHistorySize);
        }

        /// <summary>
        /// Retrieve the dynamic buffer history slot pointer
        /// </summary>
        /// <param name="dynamicDataBuffer"></param>
        /// <param name="historyPosition"></param>
        /// <param name="bufferLength"></param>
        /// <returns></returns>
        /// <exception cref="System.IndexOutOfRangeException"></exception>
        /// <exception cref="System.InvalidOperationException"></exception>
        static public byte* GetDynamicDataPtr(byte* dynamicDataBuffer, int historyPosition, int bufferLength)
        {
            var headerSize = GetHeaderSize();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            //Must be aligned to 16 bytes
            if (historyPosition < 0 || historyPosition >GhostSystemConstants.SnapshotHistorySize)
                throw new System.IndexOutOfRangeException("invalid history position");
            if(bufferLength < headerSize)
                throw new System.InvalidOperationException($"Snapshot dynamic buffer must always be at least {headerSize} bytes");
#endif
            var slotCapacity = GetDynamicDataCapacity(headerSize, bufferLength);
            return dynamicDataBuffer + headerSize + historyPosition * slotCapacity;
        }
        /// <summary>
        /// Return the currently available space (masks + buffer data) available in each slot.
        /// </summary>
        /// <param name="headerSize"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        static public uint GetDynamicDataCapacity(uint headerSize, int length)
        {
            if (length < headerSize)
                return 0;
            return (uint)(length - headerSize) / GhostSystemConstants.SnapshotHistorySize;
        }

        /// <summary>
        /// Return the history buffer capacity and the resulting size of each history buffer slot necessary to store
        /// the given dynamic data size.
        /// </summary>
        /// <param name="dynamicDataSize"></param>
        /// <param name="slotSize"></param>
        /// <returns></returns>
        static public uint CalculateBufferCapacity(uint dynamicDataSize, out uint slotSize)
        {
            var headerSize = GetHeaderSize();
            var newCapacity = headerSize + math.ceilpow2(dynamicDataSize * GhostSystemConstants.SnapshotHistorySize);
            slotSize = (newCapacity - headerSize) / GhostSystemConstants.SnapshotHistorySize;
            return newCapacity;
        }

        /// <summary>
        /// Compute the size of the bitmask for the given number of elements and mask bits. The size is aligned to 16 bytes.
        /// </summary>
        /// <param name="changeMaskBits"></param>
        /// <param name="numElements"></param>
        /// <returns></returns>
        public static int GetDynamicDataChangeMaskSize(int changeMaskBits, int numElements)
        {
            return GhostComponentSerializer.SnapshotSizeAligned(GhostComponentSerializer.ChangeMaskArraySizeInUInts(numElements * changeMaskBits)*4);
        }
    }
}
