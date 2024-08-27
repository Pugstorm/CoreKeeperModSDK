using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode.LowLevel
{
    /// <summary>
    /// Helper struct that can be used to inspect the presence of components from a <see cref="SnapshotData"/> buffer
    /// and retrieve their data.
    /// The lookup can be passed ot jobs
    /// <remarks>
    /// The helper allow to only read component data. Buffers are not supported.
    /// </remarks>
    /// </summary>
    public struct SnapshotDataBufferComponentLookup
    {
        [ReadOnly]DynamicBuffer<GhostCollectionPrefabSerializer> m_ghostPrefabType;
        [ReadOnly]DynamicBuffer<GhostCollectionComponentIndex> m_ghostComponentIndices;
        [ReadOnly]DynamicBuffer<GhostCollectionComponentType> m_ghostComponentTypes;
        [ReadOnly]DynamicBuffer<GhostComponentSerializer.State> m_ghostSerializers;
        readonly NativeParallelHashMap<SpawnedGhost, Entity>.ReadOnly m_ghostMap;
        NativeHashMap<SnapshotLookupCacheKey, SnapshotDataLookupCache.SerializerIndexAndOffset> m_componentOffsetCacheRW;

        internal SnapshotDataBufferComponentLookup(
            in DynamicBuffer<GhostCollectionPrefabSerializer> ghostPrefabType,
            in DynamicBuffer<GhostCollectionComponentIndex> ghostComponentIndices,
            in DynamicBuffer<GhostCollectionComponentType> ghostComponentTypes,
            in DynamicBuffer<GhostComponentSerializer.State> ghostSerializers,
            in NativeHashMap<SnapshotLookupCacheKey, SnapshotDataLookupCache.SerializerIndexAndOffset> componentOffsetCache,
            in NativeParallelHashMap<SpawnedGhost, Entity>.ReadOnly ghostMap)
        {
           m_ghostPrefabType = ghostPrefabType;
           m_ghostComponentIndices = ghostComponentIndices;
           m_ghostComponentTypes = ghostComponentTypes;
           m_ghostSerializers = ghostSerializers;
           m_componentOffsetCacheRW = componentOffsetCache;
           m_ghostMap = ghostMap;
        }

        /// <summary>
        /// Check if the spawning ghost mode is owner predicted.
        /// </summary>
        /// <param name="ghost"></param>
        /// <returns>True if the spawning ghost is owner predicted</returns>
        public bool IsOwnerPredicted(in GhostSpawnBuffer ghost)
        {
            return m_ghostPrefabType.ElementAtRO(ghost.GhostType).OwnerPredicted != 0;
        }

        /// <summary>
        /// Check if the spawning ghost has a <see cref="GhostOwner"/>.
        /// </summary>
        /// <param name="ghost"></param>
        /// <returns>True if the spawning ghost is owner predicted</returns>
        public bool HasGhostOwner(in GhostSpawnBuffer ghost)
        {
            return m_ghostPrefabType.ElementAtRO(ghost.GhostType).PredictionOwnerOffset != 0;
        }

        /// <summary>
        /// Retrieve the network id of the player owning the ghost if the ghost archetype has a
        /// <see cref="GhostOwner"/>.
        /// </summary>
        /// <param name="ghost"></param>
        /// <param name="data"></param>
        /// <returns>the id of the player owning the ghost, if the <see cref="GhostOwner"/> is present, 0 otherwise.</returns>
        public int GetGhostOwner(in GhostSpawnBuffer ghost, in DynamicBuffer<SnapshotDataBuffer> data)
        {
            ref readonly var ghostPrefabSerializer = ref m_ghostPrefabType.ElementAtRO(ghost.GhostType);
            if (ghostPrefabSerializer.PredictionOwnerOffset != 0)
            {
                unsafe
                {
                    var dataPtr = (byte*)data.GetUnsafeReadOnlyPtr() + ghost.DataOffset;
                    return *(int*)(dataPtr + ghostPrefabSerializer.PredictionOwnerOffset);
                }
            }
            return 0;
        }

        /// <summary>
        /// Retrieve the prediction mode used as fallback if the spawning ghost has not been
        /// classified.
        /// </summary>
        /// <param name="ghost"></param>
        /// <returns>The fallback mode to use</returns>
        public GhostSpawnBuffer.Type GetFallbackPredictionMode(in GhostSpawnBuffer ghost)
        {
            return m_ghostPrefabType.ElementAtRO(ghost.GhostType).FallbackPredictionMode;
        }

        /// <summary>
        /// Check if the a component of type <typeparamref name="T"/> is present this spawning ghost.
        /// </summary>
        /// <param name="ghostTypeIndex">The index in the <see cref="GhostCollectionPrefabSerializer"/> collection</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        //This work for both IComponentData and IBufferElementData
        public bool HasComponent<T>(int ghostTypeIndex) where T: unmanaged, IComponentData
        {
            return GetComponentDataOffset(TypeManager.GetTypeIndex<T>(), ghostTypeIndex, out _) >= 0;
        }

        /// <summary>
        /// Check if the a component of type <typeparamref name="T"/> is present this spawning ghost.
        /// </summary>
        /// <param name="ghostTypeIndex">The index in the <see cref="GhostCollectionPrefabSerializer"/> collection</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        //This work for both IComponentData and IBufferElementData
        public bool HasBuffer<T>(int ghostTypeIndex) where T: unmanaged, IBufferElementData
        {
            return GetComponentDataOffset(TypeManager.GetTypeIndex<T>(), ghostTypeIndex, out _) >= 0;
        }

        /// <summary>
        /// Try to retrieve the data for a component type <typeparam name="T"></typeparam> from the the snapshot history buffer.
        /// </summary>
        /// <remarks>
        /// Buffers aren't supported.
        /// <para>
        /// Only component present on the root entity can be retrieved. Trying to get data for component in a child entity is not supported.
        /// </para>
        /// </remarks>
        /// <param name="ghostTypeIndex">The index in the <see cref="GhostCollectionPrefabSerializer"/> collection.</param>
        /// <param name="snapshotBuffer">The entity snapshot history buffer.</param>
        /// <param name="componentData">The deserialized component data.</param>
        /// <param name="slotIndex">The slot in the history buffer to use.</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if the component is present and the component data is initialized. False otherwise</returns>
        public bool TryGetComponentDataFromSnapshotHistory<T>(int ghostTypeIndex, in DynamicBuffer<SnapshotDataBuffer> snapshotBuffer,
            out T componentData, int slotIndex=0) where T : unmanaged, IComponentData
        {
            componentData = default;
            var offset = GetComponentDataOffset(TypeManager.GetTypeIndex<T>(), ghostTypeIndex, out var serializerIndex);
            if (offset < 0)
                return false;

            var snapshotSize = m_ghostPrefabType.ElementAtRO(ghostTypeIndex).SnapshotSize;
            var dataOffset = snapshotSize * slotIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (snapshotSize > 0 && dataOffset > (snapshotBuffer.Length - snapshotSize) )
                throw new System.IndexOutOfRangeException($"Cannot read component data from the snapshot buffer at index {slotIndex}. The snapshot buffer has {snapshotBuffer.Length/snapshotSize} slots.");
#endif
            CopyDataFromSnapshot(snapshotBuffer, dataOffset + offset, serializerIndex, ref componentData);
            return true;
        }

        /// <summary>
        /// Try to retrieve the data for a component type <typeparam name="T"></typeparam> from the spawning buffer.
        /// </summary>
        /// <remarks>
        /// Buffers aren't supported.
        /// <para>
        /// Only component present on the root entity can be retrieved. Trying to get data for component in a child entity is not supported.
        /// </para>
        /// </remarks>
        /// <param name="ghost"></param>
        /// <param name="snapshotData"></param>
        /// <param name="componentData"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if the component is present and the component data is initialized. False otherwise</returns>
        public bool TryGetComponentDataFromSpawnBuffer<T>(in GhostSpawnBuffer ghost,
            in DynamicBuffer<SnapshotDataBuffer> snapshotData, out T componentData) where T: unmanaged, IComponentData
        {
            componentData = default;
            var offset = GetComponentDataOffset(TypeManager.GetTypeIndex<T>(), ghost.GhostType, out var serializerIndex);
            if (offset < 0)
                return false;
            CopyDataFromSnapshot(snapshotData, ghost.DataOffset + offset, serializerIndex, ref componentData);
            return true;
        }

        private unsafe void CopyDataFromSnapshot<T>(DynamicBuffer<SnapshotDataBuffer> historyBuffer, int dataOffset,
            int serializerIndex , ref T componentData) where T : unmanaged, IComponentData
        {
            //From here retrieving the data requires the serializer for this component type and ghost
            ref readonly var serializer = ref m_ghostSerializers.ElementAtRO(serializerIndex);
            //Force copy the type, not matter what the client filter is. Worst scenario, the component
            //has the default data (as it should be).
            var deserializerState = new GhostDeserializerState
            {
                GhostMap = m_ghostMap,
                SendToOwner = SendToOwnerType.All
            };
            //TODO: we may eventually use a more specialized version of this function that does less things and specifically designed for that
            var compDataPtr = (byte*)historyBuffer.GetUnsafeReadOnlyPtr() + dataOffset;
            var dataAtTick = new SnapshotData.DataAtTick
            {
                SnapshotBefore = (System.IntPtr)compDataPtr,
                SnapshotAfter = (System.IntPtr)compDataPtr,
                RequiredOwnerSendMask = SendToOwnerType.All
            };
            m_ghostSerializers[serializerIndex].CopyFromSnapshot.Invoke(
                (System.IntPtr)UnsafeUtility.AddressOf(ref deserializerState),
                (System.IntPtr)UnsafeUtility.AddressOf(ref dataAtTick),
                0,
                0,
                (System.IntPtr)UnsafeUtility.AddressOf(ref componentData), serializer.ComponentSize,
                1);
        }

        //The offset for the component, along with its serializer that the user wants to inspect are cached by the GetComponentDataOffset.
        //There were two options when to cache this information:
        //- when we process the prefab if we know that a component type should be "inspected" (maybe an attribute?? or registered to the collection)
        //- by caching on demand the result of this function, by providing a a small cache of (ghost-type, component-type) pairs.
        //
        //In order to pre-cache that information during prefab processing we need to provide some API (registration or attribute for code gen),
        //to declare which component CAN be inspected.
        //For sake of simplicity is done here, on demand and only if necessary.
        //Why not caching this into the GhostCollectionPrefabType ?
        //In general, users need to inspect the ghost buffer to resolve and classify pre-spawned ghosts (pretty much).
        //If you have 1000 prefabs, how many of them can be "predicatively spawned"? not many probably. It is safe to assume
        //that this cache will be small in general, and not needed for the majority of the prefabs.
        //On the other end, we are also not expecting many component types need to be inspected either. Maybe 1 or 2 custom
        //component are used in a whole project to uniquely identifying a spawn.
        //But because the bound is not well known yet (assumption need data support), it is better to be a little more flexible.
        private int GetComponentDataOffset(int typeIndex, int ghostType, out int serializerIndex)
        {
            if (!m_componentOffsetCacheRW.IsCreated)
                return FindSerializerIndexAndComponentDataOffset(typeIndex, ghostType, out serializerIndex);

            var key = new SnapshotLookupCacheKey(typeIndex, ghostType);
            if (!m_componentOffsetCacheRW.TryGetValue(key, out var cachedOffset))
            {
                cachedOffset.dataOffset = FindSerializerIndexAndComponentDataOffset(typeIndex, ghostType, out cachedOffset.serializerIndex);
                m_componentOffsetCacheRW.Add(key, cachedOffset);
            }
            serializerIndex = cachedOffset.serializerIndex;
            return cachedOffset.dataOffset;
        }

        //The calculated offset comprises also the initial snapshot header (that depend on the ghost type).
        private int FindSerializerIndexAndComponentDataOffset(int typeIndex, int ghostType, out int compSerializerIndex)
        {
            var prefabType = m_ghostPrefabType.ElementAtRO(ghostType);
            var offset = GhostComponentSerializer.SnapshotHeaderSizeInBytes(prefabType);
            for (var i = 0; i < prefabType.NumComponents; ++i)
            {
                ref readonly var compIndices = ref m_ghostComponentIndices.ElementAtRO(prefabType.FirstComponent + i); ;
                var comType = m_ghostComponentTypes.ElementAtRO(compIndices.ComponentIndex).Type;
                if (comType.TypeIndex == typeIndex)
                {
                    compSerializerIndex = compIndices.SerializerIndex;
                    return offset;
                }
                var compSize =  comType.IsBuffer
                    ? GhostComponentSerializer.DynamicBufferComponentSnapshotSize
                    : m_ghostSerializers.ElementAtRO(compIndices.SerializerIndex).SnapshotSize;
                offset += GhostComponentSerializer.SnapshotSizeAligned(compSize);
            }
            //Not found
            compSerializerIndex = default;
            return -1;
        }
    }

    internal struct SnapshotLookupCacheKey : System.IEquatable<SnapshotLookupCacheKey>
    {
        public int ghostType;
        public int typeIndex;

        public SnapshotLookupCacheKey(int ghostType, int typeIndex)
        {
            this.ghostType = ghostType;
            this.typeIndex = typeIndex;
        }

        public bool Equals(SnapshotLookupCacheKey other)
        {
            return ghostType == other.ghostType && typeIndex == other.typeIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is SnapshotLookupCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)math.hash(new int2(ghostType, typeIndex));
        }
    }

    /// <summary>
    /// Add to the GhostCollection singleton a new <see cref="SnapshotDataLookupCache"/> component that is used
    /// by the <see cref="SnapshotDataBufferComponentLookup"/>.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [CreateAfter(typeof(GhostCollectionSystem))]
    [CreateBefore(typeof(GhostReceiveSystem))]
    internal partial struct SnapshotLookupCacheSystem : ISystem
    {
        /// <summary>
        /// Maps a given component and ghost type pairs to the data offset inside the snapshot.
        /// </summary>
        private NativeHashMap<SnapshotLookupCacheKey, SnapshotDataLookupCache.SerializerIndexAndOffset> m_SnapshotDataLookupCache;

        public void OnCreate(ref SystemState state)
        {
            m_SnapshotDataLookupCache = new NativeHashMap<SnapshotLookupCacheKey, SnapshotDataLookupCache.SerializerIndexAndOffset>(128, Allocator.Persistent);
            var collection = SystemAPI.GetSingletonEntity<GhostCollection>();
            state.EntityManager.SetComponentData(collection, new SnapshotDataLookupCache
            {
                ComponentDataOffsets = m_SnapshotDataLookupCache
            });
            state.Enabled = false;
        }
        public void OnDestroy(ref SystemState state)
        {
            m_SnapshotDataLookupCache.Dispose();
        }
    }

    /// <summary>
    /// Component added <see cref="GhostCollection"/> singleton entity, used internally
    /// to cache the offset of the inspected component in the snapshot buffer for the different ghost types.
    /// </summary>
    internal struct SnapshotDataLookupCache : IComponentData
    {
        public struct SerializerIndexAndOffset
        {
            public int serializerIndex;
            public int dataOffset;
        }
        internal NativeHashMap<SnapshotLookupCacheKey, SerializerIndexAndOffset> ComponentDataOffsets;
    }
}

