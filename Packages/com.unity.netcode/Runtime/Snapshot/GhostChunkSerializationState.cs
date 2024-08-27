#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.NetCode.LowLevel.Unsafe
{
    unsafe struct GhostChunkSerializationState
    {
        public ulong sequenceNumber;
        public int ghostType;
        public int baseImportance;

        // the entity and data arrays are 2d arrays (chunk capacity * max snapshots)
        // Find baseline by finding the largest tick not at writeIndex which has been acked by the other end
        // Pass in entity, data [writeIndex] as current and entity, data [baseline] as baseline
        // If entity[baseline] is incorrect there is no delta compression
        private byte* snapshotData;
        private int allocatedChunkCapacity;
        private int allocatedDataSize;

        [StructLayout(LayoutKind.Sequential)]
        public struct MetaData
        {
            public NetworkTick lastUpdate;
            public int startIndex;
            public int snapshotWriteIndex;
            public uint orderChangeVersion;
            public NetworkTick firstZeroChangeTick;
            public uint firstZeroChangeVersion;
            public int allIrrelevant;
            public NetworkTick lastValidTick;
        }

        // The memory layout of the snapshot data is (all items are rounded up to an even 16 bytes)
        // MetaData properties which change frequently
        // uint[GhostSystemConstants.SnapshotHistorySize] snapshot index, the tick for each history position
        // uint[GhostSystemConstants.SnapshotHistorySize+31 / 32] snapshot is acked, one bit per history position
        // begin array[GhostSystemConstants.SnapshotHistorySize], the following are interleaved, one of these per history position
        //     Entity[capacity], the entity which this history position is for, if it does not match current entity the snapshot cannot be used
        //     byte[capacity * snapshotSize], the raw snapshot data for each entity in the chunk
        // end array
        // When a ghost archetype contains buffers, the snapshotData associated with the buffer will contains the following pair:
        //   uint bufferLen the length of the buffer
        //   uint bufferContentOffset offset from the beginning of the dynamic history slot wher buffers elements and the masks are stored (see info below)

        // This must match the MetaData struct
        const int MetaDataSizeInInts = 8;
        // 4 is size of uint in bytes, the chunk size is in bytes
        const int DataPerChunkSize = (4 * (MetaDataSizeInInts + GhostSystemConstants.SnapshotHistorySize + ((GhostSystemConstants.SnapshotHistorySize+31)>>5)) + 15) & (~15);

        // Buffers, due to their dynamic nature, require another history container.
        // The buffers contents are stored in a dynamic array that can grow to accomodate more data as needed. Also, the snapshot dynamic storage
        // can handle different kind of DynamicBuffer element type. Each serialized buffer contents size (len * ComponentSnapshotSize) is aligned to 16 bytes
        // The memory layout is:
        // begin array[GhostSystemConstants.SnapshotHistorySize]
        //     uint dynamicDataSize[capacity]  total buffers data used by each entity in the chunk, aligned to 16 bytes
        //     beginArray[current buffers in the chunk]
        //         uints[Len*ChangeBitMaskUintSize] the elements changemask, aligned to 16 bytes
        //         byte[Len*ComponentSnapshotSize] all the raw elements snapshot data
        //     end
        // end
        private byte* snapshotDynamicData;
        private int snapshotDynamicCapacity;


        public void AllocateSnapshotData(int serializerDataSize, int chunkCapacity)
        {
            allocatedChunkCapacity = chunkCapacity;
            allocatedDataSize = serializerDataSize;
            snapshotData = (byte*) UnsafeUtility.Malloc(
                CalculateSize(serializerDataSize, chunkCapacity), 16, Allocator.Persistent);

            // Just clear snapshot index
            UnsafeUtility.MemClear(snapshotData, DataPerChunkSize);
            snapshotDynamicData = null;
            snapshotDynamicCapacity = 0;
        }

        public void FreeSnapshotData()
        {
            UnsafeUtility.Free(snapshotData, Allocator.Persistent);
            if(snapshotDynamicData != null)
                UnsafeUtility.Free(snapshotDynamicData, Allocator.Persistent);
            snapshotData = null;
            snapshotDynamicData = null;
            snapshotDynamicCapacity = 0;
        }

        public bool IsSameSizeAndCapacity(int size, int capacity)
        {
            return size == allocatedDataSize && capacity == allocatedChunkCapacity;
        }

        public bool GetAllIrrelevant()
        {
            return ((MetaData*)snapshotData)->allIrrelevant != 0;
        }
        public void SetAllIrrelevant(bool irrelevant)
        {
            ((MetaData*)snapshotData)->allIrrelevant = (irrelevant?1:0);
        }
        public NetworkTick GetLastUpdate()
        {
            return ((MetaData*)snapshotData)->lastUpdate;
        }
        public int GetStartIndex()
        {
            return ((MetaData*)snapshotData)->startIndex;
        }
        public void SetLastUpdate(NetworkTick tick)
        {
            ((MetaData*)snapshotData)->lastUpdate = tick;
            ((MetaData*)snapshotData)->startIndex = 0;
        }
        public void SetStartIndex(int index)
        {
            ((MetaData*)snapshotData)->startIndex = index;
        }
        public int GetSnapshotWriteIndex()
        {
            return ((MetaData*)snapshotData)->snapshotWriteIndex;
        }
        public void SetSnapshotWriteIndex(int index)
        {
            ((MetaData*)snapshotData)->snapshotWriteIndex = index;
            // Mark this new thing we are trying to send as not acked
            ClearAckFlag(index);
        }

        public uint GetOrderChangeVersion()
        {
            return ((MetaData*)snapshotData)->orderChangeVersion;
        }
        public void SetOrderChangeVersion(uint version)
        {
            ((MetaData*)snapshotData)->orderChangeVersion = version;
        }
        public NetworkTick GetFirstZeroChangeTick()
        {
            return ((MetaData*)snapshotData)->firstZeroChangeTick;
        }
        public uint GetFirstZeroChangeVersion()
        {
            return ((MetaData*)snapshotData)->firstZeroChangeVersion;
        }
        public void SetFirstZeroChange(NetworkTick tick, uint version)
        {
            ((MetaData*)snapshotData)->firstZeroChangeTick = tick;
            ((MetaData*)snapshotData)->firstZeroChangeVersion = version;
        }
        public NetworkTick GetLastValidTick()
        {
            return ((MetaData*)snapshotData)->lastValidTick;
        }
        public void SetLastValidTick(NetworkTick tick)
        {
            ((MetaData*)snapshotData)->lastValidTick = tick;
        }
        public bool HasAckFlag(int pos)
        {
            var idx = GhostSystemConstants.SnapshotHistorySize + (pos>>5);
            uint bit = 1u<<(pos&31);
            return (GetSnapshotIndex()[idx] & bit) != 0;
        }
        public void SetAckFlag(int pos)
        {
            var idx = GhostSystemConstants.SnapshotHistorySize + (pos>>5);
            uint bit = 1u<<(pos&31);
            GetSnapshotIndex()[idx] |= bit;
        }
        public void ClearAckFlag(int pos)
        {
            var idx = GhostSystemConstants.SnapshotHistorySize + (pos>>5);
            uint bit = 1u<<(pos&31);
            GetSnapshotIndex()[idx] &= (~bit);
        }


        private static int CalculateSize(int serializerDataSize, int chunkCapacity)
        {
            int entitySize = (UnsafeUtility.SizeOf<Entity>() * chunkCapacity + 15) & (~15);
            int dataSize = (serializerDataSize * chunkCapacity + 15) & (~15);
            return DataPerChunkSize + GhostSystemConstants.SnapshotHistorySize * (entitySize + dataSize);
        }

        public uint* GetSnapshotIndex()
        {
            // The +MetaDataSizeInInts is the change versions and tick
            return ((uint*) snapshotData) + MetaDataSizeInInts;
        }

        public Entity* GetEntity(int serializerDataSize, int chunkCapacity, int historyPosition)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (historyPosition < 0 || historyPosition >= GhostSystemConstants.SnapshotHistorySize)
                throw new IndexOutOfRangeException("Reading invalid history position");
            if (serializerDataSize != allocatedDataSize || chunkCapacity != allocatedChunkCapacity)
                throw new IndexOutOfRangeException("Chunk capacity or data size changed");
#endif
            int entitySize = (UnsafeUtility.SizeOf<Entity>() * chunkCapacity + 15) & (~15);
            int dataSize = (serializerDataSize * chunkCapacity + 15) & (~15);
            return (Entity*) (snapshotData + DataPerChunkSize + historyPosition * (entitySize + dataSize));
        }

        public byte* GetData(int serializerDataSize, int chunkCapacity, int historyPosition)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (historyPosition < 0 || historyPosition >= GhostSystemConstants.SnapshotHistorySize)
                throw new IndexOutOfRangeException("Reading invalid history position");
            if (serializerDataSize != allocatedDataSize || chunkCapacity != allocatedChunkCapacity)
                throw new IndexOutOfRangeException("Chunk capacity or data size changed");
#endif
            int entitySize = (UnsafeUtility.SizeOf<Entity>() * chunkCapacity + 15) & (~15);
            int dataSize = (serializerDataSize * chunkCapacity + 15) & (~15);
            return (snapshotData + DataPerChunkSize + entitySize + historyPosition * (entitySize + dataSize));
        }

        /// <summary>
        /// Return the pointer to the dynamic data snapshot storage for the given history position or null if the storage
        /// is not present or not initialized
        /// </summary>
        /// <param name="historyPosition"></param>
        /// <param name="capacity"></param>
        /// <param name="chunkCapacity"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public byte* GetDynamicDataPtr(int historyPosition, int chunkCapacity, out int capacity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (historyPosition < 0 || historyPosition >= GhostSystemConstants.SnapshotHistorySize)
                throw new IndexOutOfRangeException("Reading invalid history position");
#endif
            //If the chunk state has just been created the dynamic data ptr must be allocated
            //once we collected the necessary capacity
            if (snapshotDynamicData == null)
            {
                capacity = 0;
                return null;
            }
            var headerSize = GetDynamicDataHeaderSize(chunkCapacity);
            var slotStride = snapshotDynamicCapacity / GhostSystemConstants.SnapshotHistorySize;
            capacity = slotStride - headerSize;
            return snapshotDynamicData + slotStride*historyPosition;
        }

        static public int GetDynamicDataHeaderSize(int chunkCapacity)
        {
            return GhostComponentSerializer.SnapshotSizeAligned(sizeof(uint) * chunkCapacity);
        }

        public void EnsureDynamicDataCapacity(int historySlotCapacity, int chunkCapacity)
        {
            //Get the next pow2 that fit the required size
            var headerSize = GetDynamicDataHeaderSize(chunkCapacity);
            var wantedSize = GhostComponentSerializer.SnapshotSizeAligned(historySlotCapacity + headerSize);
            var newCapacity = math.ceilpow2(wantedSize * GhostSystemConstants.SnapshotHistorySize);
            if (snapshotDynamicCapacity < newCapacity)
            {
                var temp = (byte*)UnsafeUtility.Malloc(newCapacity, 16, Allocator.Persistent);
                //Copy the old content
                if (snapshotDynamicData != null)
                {
                    var slotSize = snapshotDynamicCapacity / GhostSystemConstants.SnapshotHistorySize;
                    var newSlotSize = newCapacity / GhostSystemConstants.SnapshotHistorySize;
                    var sourcePtr = snapshotDynamicData;
                    var destPtr = temp;
                    for (int i = 0; i < GhostSystemConstants.SnapshotHistorySize; ++i)
                    {
                        UnsafeUtility.MemCpy(destPtr, sourcePtr,slotSize);
                        destPtr += newSlotSize;
                        sourcePtr += slotSize;
                    }
                    UnsafeUtility.Free(snapshotDynamicData, Allocator.Persistent);
                }
                snapshotDynamicCapacity = newCapacity;
                snapshotDynamicData = temp;
            }
        }
    }

    static class ConnectionGhostStateExtensions
    {
        public static ref ConnectionStateData.GhostState GetGhostState(ref this ConnectionStateData.GhostStateList self, in GhostCleanup cleanup)
        {
            //Map the right index by unmasking the prespawn bit (if present)
            var index = (int)(cleanup.ghostId & ~PrespawnHelper.PrespawnGhostIdBase);
            var isPrespawnGhost = PrespawnHelper.IsPrespawnGhostId(cleanup.ghostId);
            var list = isPrespawnGhost ? self.PrespawnList : self.List;
            ref var state = ref list.ElementAt(index);

            //The initial state for pre-spawned ghosts must be set to relevant, otherwise clients may not received despawn messages
            //for prespawn that has been destroyed or marked as irrelevant. It is also mandatory for static optimization since
            //we never actually send information to the client until the prespawns changed state.
            if (state.SpawnTick != cleanup.spawnTick)
                state = new ConnectionStateData.GhostState
                {
                    SpawnTick = cleanup.spawnTick,
                    Flags = isPrespawnGhost ? ConnectionStateData.GhostStateFlags.IsRelevant : 0
                };

            return ref state;
        }
    }
    unsafe struct ConnectionStateData : IDisposable
    {
        [Flags]
        public enum GhostStateFlags
        {
            IsRelevant = 1,
            SentWithChanges = 2,
            CantUsePrespawnBaseline = 4
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct GhostState
        {
            public NetworkTick SpawnTick;
            public int LastIndexInChunk;
            public ArchetypeChunk LastChunk;
            public GhostStateFlags Flags;
            public NetworkTick LastDespawnSendTick;
        }
        public struct GhostStateList : IDisposable
        {
            public ref UnsafeList<GhostState> List
            {
                get { return ref m_List[0]; }
            }
            public ref UnsafeList<GhostState> PrespawnList
            {
                get { return ref m_List[1]; }
            }
            public NetworkTick AckedDespawnTick
            {
                get
                {
                    byte* ptr = (byte*)m_List;
                    ptr += 2*UnsafeUtility.SizeOf<UnsafeList<GhostState>>();
                    return new NetworkTick{SerializedData = *(uint*)ptr};
                }
                set
                {
                    byte* ptr = (byte*)m_List;
                    ptr += 2*UnsafeUtility.SizeOf<UnsafeList<GhostState>>();
                    *(uint*)ptr = value.SerializedData;
                }
            }
            public uint DespawnRepeatCount
            {
                get
                {
                    byte* ptr = (byte*)m_List;
                    ptr += 2*UnsafeUtility.SizeOf<UnsafeList<GhostState>>();
                    return ((uint*)ptr)[1];
                }
                set
                {
                    byte* ptr = (byte*)m_List;
                    ptr += 2*UnsafeUtility.SizeOf<UnsafeList<GhostState>>();
                    ((uint*)ptr)[1] = value;
                }
            }
            [NativeDisableUnsafePtrRestriction]
            // The "list" is 2 UnsafeLists, one for regular and one for pre-spawned ghosts, followed by a uint for AckedDespawnTick
            UnsafeList<GhostState>* m_List;
            Allocator m_Allocator;

            //Space that are reserved specifically for prespawn.. Can we remove it ?
            public GhostStateList(int capacity, int prespawnCapacity, Allocator allocator)
            {
                m_Allocator = allocator;
                m_List = (UnsafeList<GhostState>*)UnsafeUtility.Malloc(2*UnsafeUtility.SizeOf<UnsafeList<GhostState>>() + 2*UnsafeUtility.SizeOf<uint>(), UnsafeUtility.AlignOf<UnsafeList<GhostState>>(), allocator);
                m_List[0] = new UnsafeList<GhostState>(CalculateStateListCapacity(capacity), allocator, NativeArrayOptions.ClearMemory);
                m_List[1] = new UnsafeList<GhostState>(CalculateStateListCapacity(prespawnCapacity), allocator, NativeArrayOptions.ClearMemory);
                AckedDespawnTick = NetworkTick.Invalid;
            }
            public void Dispose()
            {
                m_List[0].Dispose();
                m_List[1].Dispose();
                UnsafeUtility.Free(m_List, m_Allocator);
            }

            public static int CalculateStateListCapacity(int capacity)
            {
                return (capacity + 1023) & (~1023);
            }
        }

        public void Dispose()
        {
            var chunkStates = SerializationState->GetValueArray(Allocator.Temp);
            for (int i = 0; i < chunkStates.Length; ++i)
                chunkStates[i].FreeSnapshotData();
            SerializationState->Dispose();
            AllocatorManager.Free(Allocator.Persistent, SerializationState);
            ClearHistory.Dispose();
            AckedPrespawnSceneMap.Dispose();
            UnsafeList<PrespawnHelper.GhostIdInterval>.Destroy(m_NewLoadedPrespawnRanges);
            GhostStateData.Dispose();
#if NETCODE_DEBUG
            NetDebugPacket.Dispose();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public ConnectionStateData Create(Entity connection)
        {
            var hashMapData = AllocatorManager.Allocate<UnsafeHashMap<ArchetypeChunk, GhostChunkSerializationState>>(Allocator.Persistent);
            *hashMapData = new UnsafeHashMap<ArchetypeChunk, GhostChunkSerializationState>(1024, Allocator.Persistent);
            return new ConnectionStateData
            {
                Entity = connection,
                SerializationState = hashMapData,
                ClearHistory = new UnsafeParallelHashMap<int, NetworkTick>(256, Allocator.Persistent),
#if NETCODE_DEBUG
                NetDebugPacket = new PacketDumpLogger(),
#endif
                GhostStateData = new GhostStateList(1024, 1024, Allocator.Persistent),
                AckedPrespawnSceneMap = new UnsafeParallelHashMap<ulong, int>(256, Allocator.Persistent),
                m_NewLoadedPrespawnRanges = UnsafeList<PrespawnHelper.GhostIdInterval>.Create(32, Allocator.Persistent),
            };
        }

        public Entity Entity;
        public UnsafeHashMap<ArchetypeChunk, GhostChunkSerializationState>* SerializationState;
        public UnsafeParallelHashMap<int, NetworkTick> ClearHistory;
#if NETCODE_DEBUG
        public PacketDumpLogger NetDebugPacket;
#endif
        public GhostStateList GhostStateData;
        public UnsafeParallelHashMap<ulong, int> AckedPrespawnSceneMap;
        public ref UnsafeList<PrespawnHelper.GhostIdInterval> NewLoadedPrespawnRanges => ref m_NewLoadedPrespawnRanges[0];
        private UnsafeList<PrespawnHelper.GhostIdInterval>* m_NewLoadedPrespawnRanges;

        public void EnsureGhostStateCapacity(int capacity, int prespawnCapacity)
        {
            if (capacity > GhostStateData.List.Length)
                GhostStateData.List.Resize(GhostStateList.CalculateStateListCapacity(capacity), NativeArrayOptions.ClearMemory);

            if(prespawnCapacity > GhostStateData.PrespawnList.Length)
                GhostStateData.PrespawnList.Resize(GhostStateList.CalculateStateListCapacity(prespawnCapacity), NativeArrayOptions.ClearMemory);
        }
    }
}
