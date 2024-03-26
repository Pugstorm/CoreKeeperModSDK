using Unity.Collections;
using Unity.Entities;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode.LowLevel
{
    /// <summary>
    /// Helper struct that can be used in your spawn classification systems (and classification
    /// jobs) to create <see cref="SnapshotDataBufferComponentLookup"/> instances.
    /// In order to use the helper, the system that create/hold the instance, must be created
    /// after the <see cref="GhostCollectionSystem"/> and after the <see cref="GhostReceiveSystem"/>, because
    /// of the need to retrieve the <see cref="SpawnedGhostEntityMap"/> and the <see cref="SnapshotDataLookupCache"/>
    /// data.
    /// </summary>
    public struct SnapshotDataLookupHelper
    {
        [ReadOnly] private BufferLookup<GhostCollectionPrefabSerializer> m_GhostCollectionPrefabSerializerLookup;
        [ReadOnly] private BufferLookup<GhostCollectionComponentIndex> m_GhostCollectionComponentIndexLookup;
        [ReadOnly] private BufferLookup<GhostCollectionComponentType> m_GhostCollectionComponentTypeLookup;
        [ReadOnly] private BufferLookup<GhostComponentSerializer.State> m_GhostCollectionSerializersLookup;
        [ReadOnly] internal NativeParallelHashMap<SpawnedGhost, Entity>.ReadOnly m_ghostMap;
        internal NativeHashMap<SnapshotLookupCacheKey, SnapshotDataLookupCache.SerializerIndexAndOffset> m_SnapshotDataLookupCache;
        internal Entity m_GhostCollectionEntity;
        /// <summary>
        /// Default constructor, collect and initialize all the internal <see cref="BufferFromEntity{T}"/> handles
        /// and collect the necessary data structures.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="ghostCollectionEntity">The entity that hold the GhostCollection component</param>
        /// <param name="spawnMapEntity">The entity that hold the SpawnedGhostEntityMap component</param>
        public SnapshotDataLookupHelper(ref SystemState state,
            Entity ghostCollectionEntity, Entity spawnMapEntity)
        {
            m_GhostCollectionPrefabSerializerLookup = state.GetBufferLookup<GhostCollectionPrefabSerializer>(true);
            m_GhostCollectionComponentIndexLookup = state.GetBufferLookup<GhostCollectionComponentIndex>(true);
            m_GhostCollectionComponentTypeLookup = state.GetBufferLookup<GhostCollectionComponentType>(true);
            m_GhostCollectionSerializersLookup = state.GetBufferLookup<GhostComponentSerializer.State>(true);
            //This will add the right dependencies to the system that hold the helper class. The lookup is not hold
            //because it is not strictly necessary in this case.
            var ghostMap = state.GetComponentLookup<SpawnedGhostEntityMap>(true);
            var lookupCache = state.GetComponentLookup<SnapshotDataLookupCache>();
            m_ghostMap = ghostMap[spawnMapEntity].Value;
            m_SnapshotDataLookupCache = lookupCache[ghostCollectionEntity].ComponentDataOffsets;
            m_GhostCollectionEntity = ghostCollectionEntity;
        }

        /// <summary>
        /// Call this method in your system OnUpdate to refresh all the internal <see cref="BufferFromEntity{T}"/> handles.
        /// </summary>
        /// <param name="state"></param>
        public void Update(ref SystemState state)
        {
            m_GhostCollectionPrefabSerializerLookup.Update(ref state);
            m_GhostCollectionComponentIndexLookup.Update(ref state);
            m_GhostCollectionComponentTypeLookup.Update(ref state);
            m_GhostCollectionSerializersLookup.Update(ref state);
        }

        /// <summary>
        /// Create a new <see cref="SnapshotDataBufferComponentLookup"/> instance that can be used on the main thread or in job.
        /// This method introduce a sync point, because internally retrieve all the necessary <see cref="DynamicBuffer{T}"/>.
        /// </summary>
        /// <remarks>
        /// The method requires that the <see cref="Update"/> method has been called and that all the internal handles
        /// has been updated.
        /// </remarks>
        /// <returns>A valid <see cref="SnapshotDataBufferComponentLookup"/> instance</returns>
        public SnapshotDataBufferComponentLookup CreateSnapshotBufferLookup()
        {
            return new SnapshotDataBufferComponentLookup(
                m_GhostCollectionPrefabSerializerLookup[m_GhostCollectionEntity],
                m_GhostCollectionComponentIndexLookup[m_GhostCollectionEntity],
                m_GhostCollectionComponentTypeLookup[m_GhostCollectionEntity],
                m_GhostCollectionSerializersLookup[m_GhostCollectionEntity],
                m_SnapshotDataLookupCache,
                m_ghostMap);
        }
    }
}
