using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using static Unity.Entities.Editor.EntityBakingPreview;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The <see cref="HierarchySearch"/> is used to handle the shared state for searching/filtering.
    /// </summary>
    [BurstCompile]
    class HierarchySearch : IDisposable
    {
        struct EntityQueryCache : IDisposable
        {
            EntityQueryDesc m_Desc;
            internal EntityQuery m_Query;
            World m_OriginatingWorld;
            HierarchyFilter.ModifyEntityQuery[] m_EntityQueryModifiers;

            public EntityQuery EntityQuery => m_Query;

            public EntityQueryCache(EntityManager entityManager, EntityQueryDesc desc, HierarchyFilter.ModifyEntityQuery[] entityQueryModifiers)
            {
                m_Desc = desc;
                m_Query = entityManager.CreateEntityQuery(desc);
                foreach (var modifier in entityQueryModifiers)
                    modifier(ref m_Query);
                m_OriginatingWorld = entityManager.World;
                m_EntityQueryModifiers = entityQueryModifiers;
            }

            public void Dispose()
            {
                if (null != m_Desc && m_OriginatingWorld.IsCreated)
                    m_Query.Dispose();

                m_Desc = null;
                m_OriginatingWorld = null;
            }

            public bool Equals(EntityQueryDesc other, World world, HierarchyFilter.ModifyEntityQuery[] entityQueryModifiers)
            {
                return m_OriginatingWorld != null &&
                    world.SequenceNumber == m_OriginatingWorld.SequenceNumber &&
                    Enumerable.SequenceEqual(m_EntityQueryModifiers, entityQueryModifiers) &&
                    Equals(m_Desc, other);
            }
        }

        /// <summary>
        /// The underlying hierarchy.
        /// </summary>
        readonly HierarchyNameStore m_HierarchyNameStore;

        /// <summary>
        /// A persisted bitmask cache over the <see cref="EntityNameStorage"/>.
        /// </summary>
        NativeBitArray m_EntityNameStorageMask;

        /// <summary>
        /// The world this search operates on.
        /// </summary>
        World m_World;

        /// <summary>
        /// A cached version of the <see cref="EntityQuery"/> to avoid constructing it each time.
        /// </summary>
        EntityQueryCache m_EntityQueryCache;

        /// <summary>
        /// If true; unnamed nodes are skipped during name filtering.
        /// </summary>
        public bool ExcludeUnnamedNodes { get; set; }

        public HierarchySearch(HierarchyNameStore hierarchyNameStore, Allocator allocator)
        {
            m_HierarchyNameStore = hierarchyNameStore;
            m_EntityNameStorageMask = new NativeBitArray(EntityNameStorage.kMaxEntries, allocator);
        }

        public void Dispose()
        {
            m_EntityNameStorageMask.Dispose();
            m_EntityQueryCache.Dispose();
        }

        public void SetWorld(World world)
        {
            m_World = world;
            m_EntityQueryCache.Dispose();
        }

        public HierarchyFilter CreateHierarchyFilter(string searchString, ICollection<string> tokens, Allocator allocator)
        {
            return new HierarchyFilter(this, searchString, tokens, allocator);
        }

        internal void ApplyEntityIndexFilter(HierarchyNodeStore.Immutable nodes, int index, NativeBitArray mask)
        {
            if (index == -1)
                return;

            if (m_World == null)
            {
                mask.Clear();
                return;
            }

            new FilterByIndex
            {
                Index = index,
                Nodes = nodes,
                NodeMatchesMask = mask,
            }.Run();
        }

        internal void ApplyNodeKindFilter(HierarchyNodeStore.Immutable nodes, NodeKind kind, NativeBitArray mask)
        {
            new FilterByKind
            {
                Kind = kind,
                Nodes = nodes,
                NodeMatchesMask = mask,
            }.Run();
        }

        internal void ApplyEntityQueryFilter(HierarchyNodeStore.Immutable nodes, EntityQueryDesc queryDesc, HierarchyFilter.ModifyEntityQuery[] entityQueryModifiers, NativeBitArray mask, Allocator allocator)
        {
            if (null == queryDesc)
                return;

            if (m_World == null)
            {
                mask.Clear();
                return;
            }

            if (!m_EntityQueryCache.Equals(queryDesc, m_World, entityQueryModifiers))
            {
                m_EntityQueryCache.Dispose();
                m_EntityQueryCache = new EntityQueryCache(m_World.EntityManager, queryDesc, entityQueryModifiers);
            }

            if (entityQueryModifiers.Length > 0)
            {
                // In case of SharedComponent we need to apply a different filtering that starts with executing the EntityQuery itself.
                FilterByEntityQuery(ref mask, nodes, ref m_EntityQueryCache.m_Query, allocator);

                // Since EntityModifiers cannot be cached, dispose the EntityQueryCache.
                m_EntityQueryCache.Dispose();
            }
            else
            {
                // TODO(DOTS-6706): if m_EntityQueryCache.EntityQuery references enableable components, this GetEntityQueryMask() call will throw.
                var entityQueryMask = m_EntityQueryCache.EntityQuery.GetEntityQueryMask();
                FilterByEntityQuery(ref mask, nodes, ref entityQueryMask);
            }
        }

        /// <summary>
        /// Applies name filtering based on the given tokens.
        /// </summary>
        /// <param name="nodes">The source nodes being filtered.</param>
        /// <param name="tokens">The search tokens.</param>
        /// <param name="mask">The bitmask of currently included nodes.</param>
        internal void ApplyNameFilter(HierarchyNodeStore.Immutable nodes, NativeList<FixedString64Bytes> tokens, NativeBitArray mask)
        {
            unsafe
            {
                if (tokens.Length == 0)
                    return;

#if !DOTS_DISABLE_DEBUG_NAMES
                // Reset all bits to true. The job will remove any unmatched entries.
                m_EntityNameStorageMask.SetBits(0, true, m_EntityNameStorageMask.Length);

                if (m_World != null)
                {
                    new BuildEntityNameStoragePatternCacheLowerInvariant<FixedString64Bytes>
                    {
                        EntityNameStorageMask = m_EntityNameStorageMask,
                        Tokens = tokens,
                        EntityNameStorageLowerInvariant = m_HierarchyNameStore.EntityNameStorageLowerInvariant
                    }.Run();
                }
#endif

                new FilterByNameLowerInvariant<FixedString64Bytes>
                {
                    Nodes = nodes,
                    Tokens = tokens,
                    ExcludeUnnamedNodes = ExcludeUnnamedNodes,
#if !DOTS_DISABLE_DEBUG_NAMES
                    EntityNameStorageMask = m_EntityNameStorageMask,
#if ENTITY_STORE_V1
                    NameByEntity = m_World != null ? m_HierarchyNameStore.NameByEntity : null,
#else
                    NameStoreAccess = m_World != null ? m_HierarchyNameStore.NameStoreAccess : default,
#endif
#endif
                    NameByHandleLowerInvariant = m_HierarchyNameStore.NameByHandleLowerInvariant,
                    NodeMatchesMask = mask,
                }.Run();
            }
        }

        /// <summary>
        /// Applies scene filtering. This includes a scene node if ANY sub elements are already included.
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="mask"></param>
        internal void ApplyIncludeSubSceneFilter(HierarchyNodeStore.Immutable nodes, NativeBitArray mask)
        {
            new FilterIncludeSubScene
            {
                Nodes = nodes,
                NodeMatchesMask = mask
            }.Run();
        }

        internal void ApplyPrefabStageFilter(HierarchyNodeStore.Immutable nodes, NativeBitArray mask)
        {
            new FilterByPrefabStage
            {
                Nodes = nodes,
                NodeMatchesMask = mask
            }.Run();
        }

        [BurstCompile(DisableSafetyChecks = true)]
        struct FilterByPrefabStage : IJob
        {
            [ReadOnly] public HierarchyNodeStore.Immutable Nodes;
            [ReadOnly] public int Index;

            public NativeBitArray NodeMatchesMask;

            public void Execute()
            {
                for (var index = 0; index < Nodes.Count; index++)
                {
                    if (!NodeMatchesMask.IsSet(index))
                        continue;

                    if ((Nodes[index].Flags & HierarchyNodeFlags.IsPrefabStage) == 0)
                        NodeMatchesMask.Set(index, false);
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true)]
        struct FilterByIndex : IJob
        {
            [ReadOnly] public HierarchyNodeStore.Immutable Nodes;
            [ReadOnly] public int Index;

            public NativeBitArray NodeMatchesMask;

            public void Execute()
            {
                for (var index = 0; index < Nodes.Count; index++)
                {
                    if (!NodeMatchesMask.IsSet(index))
                        continue;

                    var handle = Nodes[index].Handle;

                    if (handle.Kind == NodeKind.Entity)
                    {
                        if (handle.Index != Index)
                            NodeMatchesMask.Set(index, false);
                    }
                    else if (handle.Kind != NodeKind.SubScene)
                        NodeMatchesMask.Set(index, false);
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true)]
        struct FilterByKind : IJob
        {
            [ReadOnly] public HierarchyNodeStore.Immutable Nodes;
            [ReadOnly] public NodeKind Kind;

            public NativeBitArray NodeMatchesMask;

            public void Execute()
            {
                for (var index = 0; index < Nodes.Count; index++)
                {
                    if (!NodeMatchesMask.IsSet(index))
                        continue;

                    var handle = Nodes[index].Handle;

                    if (handle.Kind != Kind)
                        NodeMatchesMask.Set(index, false);
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true)]
        static void FilterByEntityQuery(ref NativeBitArray nodeMatchesMask, in HierarchyNodeStore.Immutable nodes, ref EntityQuery query, Allocator allocator)
        {
            for (var index = 0; index < nodes.Count; index++)
            {
                nodeMatchesMask.Set(index, false);
            }

            // Apply entity query:
            var entities = query.ToEntityArray(allocator);
            for(var entityIndex = 0; entityIndex < entities.Length; ++entityIndex)
            {
                // Map the entities to their respective nodes and set the node mask:
                var entity = entities[entityIndex];
                var entityHandler = HierarchyNodeHandle.FromEntity(entity);
                var nodeIndex = nodes.IndexOf(entityHandler);
                nodeMatchesMask.Set(nodeIndex, true);
            }
            entities.Dispose();
        }

        [BurstCompile(DisableSafetyChecks = true)]
        static void FilterByEntityQuery(ref NativeBitArray nodeMatchesMask, in HierarchyNodeStore.Immutable nodes, ref EntityQueryMask queryMask)
        {
            for (var index = 0; index < nodes.Count; index++)
            {
                if (!nodeMatchesMask.IsSet(index))
                    continue;

                var handle = nodes[index].Handle;

                if (handle.Kind != NodeKind.Entity)
                {
                    nodeMatchesMask.Set(index, false);
                    continue;
                }

                // It's okay that this mask ignores filtering, since we still want to show matching entities with relevant components disabled.
                if (!queryMask.MatchesIgnoreFilter(handle.ToEntity()))
                    nodeMatchesMask.Set(index, false);
            }
        }

        /// <summary>
        /// Bursted job to compute a search pattern bitmask directly over the <see cref="EntityNameStorage"/>.
        /// </summary>
        /// <typeparam name="TPattern">A fixed string pattern type.</typeparam>
        [BurstCompile(DisableSafetyChecks = true)]
        struct BuildEntityNameStoragePatternCacheLowerInvariant<TPattern> : IJob
            where TPattern : unmanaged, IUTF8Bytes, INativeList<byte>, IEquatable<TPattern>
        {
            [ReadOnly] public NativeList<TPattern> Tokens;

            [WriteOnly] public NativeBitArray EntityNameStorageMask;

            public EntityNameStorageLowerInvariant EntityNameStorageLowerInvariant;

            public void Execute()
            {
                for (var index = 1; index < EntityNameStorage.Entries; index++)
                {
                    FixedString64Bytes name = default;
                    EntityNameStorageLowerInvariant.GetFixedString(index, ref name);

                    var match = true;

                    for (int tokenIndex = 0, count = Tokens.Length; match && tokenIndex < count; tokenIndex++)
                        match = SIMDSearch.Contains(name, Tokens[tokenIndex]);

                    if (!match)
                        EntityNameStorageMask.Set(index, false);
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true)]
        unsafe struct FilterByNameLowerInvariant<TPattern> : IJob
            where TPattern : unmanaged, IUTF8Bytes, INativeList<byte>, IEquatable<TPattern>
        {
            [ReadOnly] public HierarchyNodeStore.Immutable Nodes;
            [ReadOnly] public NativeList<TPattern> Tokens;
            [ReadOnly] public bool ExcludeUnnamedNodes;
            [ReadOnly] public bool CurrentWorldExists;

#if !DOTS_DISABLE_DEBUG_NAMES
            [ReadOnly] public NativeBitArray EntityNameStorageMask;
#if ENTITY_STORE_V1
            [NativeDisableUnsafePtrRestriction] public EntityName* NameByEntity;
#else
            [ReadOnly] public EntityNameStoreAccess NameStoreAccess;
#endif

#endif

            [ReadOnly] public NativeParallelHashMap<HierarchyNodeHandle, FixedString64Bytes> NameByHandleLowerInvariant;

            public NativeBitArray NodeMatchesMask;

            public void Execute()
            {
                for (var index = 0; index < Nodes.Count; index++)
                {
                    if (!NodeMatchesMask.IsSet(index))
                        continue;

                    var handle = Nodes[index].Handle;

                    if (handle.Kind == NodeKind.Entity)
                    {
#if !DOTS_DISABLE_DEBUG_NAMES
#if ENTITY_STORE_V1
                        if (NameByEntity[handle.Index].Index > 0)
                        {
                            // Fast path. This name already exists in the database.
                            NodeMatchesMask.Set(index, EntityNameStorageMask.IsSet(NameByEntity[handle.Index].Index));
                            continue;
                        }
#else
                        var entityName = NameStoreAccess.GetEntityNameByEntityIndex(handle.Index);
                        if (entityName.Index > 0)
                        {
                            // Fast path. This name already exists in the database.
                            NodeMatchesMask.Set(index, EntityNameStorageMask.IsSet(entityName.Index));
                            continue;
                        }
#endif

#endif

                        if (ExcludeUnnamedNodes)
                        {
                            NodeMatchesMask.Set(index, false);
                            continue;
                        }

                        // Slow path, we need to check the entity name directly
                        FixedString64Bytes name = default;
                        HierarchyNameStore.Formatting.FormatEntityLowerInvariant(handle, ref name);

                        var match = true;

                        for (int i = 0, count = Tokens.Length; match && i < count; i++)
                            match = SIMDSearch.Contains(name, Tokens[i]);

                        NodeMatchesMask.Set(index, match);
                    }
                    else
                    {
                        if (!NameByHandleLowerInvariant.TryGetValue(handle, out var name))
                        {
                            if (ExcludeUnnamedNodes)
                            {
                                NodeMatchesMask.Set(index, false);
                                continue;
                            }

                            HierarchyNameStore.Formatting.FormatHandleLowerInvariant(handle, ref name);
                        }

                        var match = true;

                        for (int i = 0, count = Tokens.Length; match && i < count; i++)
                            match = SIMDSearch.Contains(name, Tokens[i]);

                        NodeMatchesMask.Set(index, match);
                    }
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true)]
        struct FilterIncludeSubScene : IJob
        {
            [ReadOnly] public HierarchyNodeStore.Immutable Nodes;

            public NativeBitArray NodeMatchesMask;

            public void Execute()
            {
                var subSceneStartIndex = -1;
                var subSceneEndIndex = -1;

                for (var index = 0; index < Nodes.CountHierarchicalNodes; index++)
                {
                    if (Nodes[index].Handle.Kind == NodeKind.SubScene)
                    {
                        subSceneStartIndex = index;
                        subSceneEndIndex = index + Nodes[index].NextSiblingOffset;
                        continue;
                    }

                    // We only care about nodes within the sub scene.
                    if (index > subSceneEndIndex)
                    {
                        subSceneStartIndex = -1;
                        subSceneEndIndex = -1;
                        continue;
                    }

                    if (NodeMatchesMask.IsSet(index))
                    {
                        NodeMatchesMask.Set(subSceneStartIndex, true);
                        subSceneStartIndex = -1;
                        subSceneEndIndex = -1;
                    }
                }
            }
        }
    }
}
