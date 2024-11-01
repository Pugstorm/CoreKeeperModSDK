using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Transforms;

namespace Unity.Entities.Editor
{
    [GenerateTestsForBurstCompatibility]
    struct HierarchyEntityChanges : IDisposable
    {
        // Entity changes
        public NativeList<Entity> CreatedEntities;
        public NativeList<Entity> DestroyedEntities;

        // Parent component changes
        public NativeList<Entity> AddedParentEntities;
        public NativeList<Entity> RemovedParentEntities;
        public NativeList<Parent> AddedParentComponents;
        public NativeList<Parent> RemovedParentComponents;

        // The scene tag component for added parent component whose value is 'Entity.Null'
        public NativeList<SceneTag> AddedParentSceneTagForNullParentComponents;

        // Scene tag without parent changes
        public NativeList<Entity> AddedSceneTagWithoutParentEntities;
        public NativeList<Entity> RemovedSceneTagWithoutParentEntities;
        public NativeList<SceneTag> AddedSceneTagWithoutParentComponents;


        public bool HasChanges()
        {
            return !(CreatedEntities.Length == 0
                     && DestroyedEntities.Length == 0
                     && AddedParentEntities.Length == 0
                     && RemovedParentEntities.Length == 0
                     && AddedParentComponents.Length == 0
                     && RemovedParentComponents.Length == 0
                     && AddedSceneTagWithoutParentEntities.Length == 0
                     && RemovedSceneTagWithoutParentEntities.Length == 0
                     && AddedSceneTagWithoutParentComponents.Length == 0);
        }

        public int GetChangeCount()
        {
            var count = 0;

            count += CreatedEntities.Length;
            count += DestroyedEntities.Length;
            count += AddedParentEntities.Length;
            count += RemovedParentEntities.Length;
            count += AddedSceneTagWithoutParentEntities.Length;
            count += RemovedSceneTagWithoutParentEntities.Length;

            return count;
        }

        public void Clear()
        {
            CreatedEntities.Clear();
            DestroyedEntities.Clear();
            AddedParentEntities.Clear();
            RemovedParentEntities.Clear();
            AddedParentComponents.Clear();
            RemovedParentComponents.Clear();
            AddedParentSceneTagForNullParentComponents.Clear();
            AddedSceneTagWithoutParentEntities.Clear();
            RemovedSceneTagWithoutParentEntities.Clear();
            AddedSceneTagWithoutParentComponents.Clear();
        }

        public HierarchyEntityChanges(Allocator allocator)
        {
            CreatedEntities = new NativeList<Entity>(allocator);
            DestroyedEntities = new NativeList<Entity>(allocator);
            AddedParentEntities = new NativeList<Entity>(allocator);
            RemovedParentEntities = new NativeList<Entity>(allocator);
            AddedParentComponents = new NativeList<Parent>(allocator);
            RemovedParentComponents = new NativeList<Parent>(allocator);
            AddedParentSceneTagForNullParentComponents = new NativeList<SceneTag>(allocator);
            AddedSceneTagWithoutParentEntities = new NativeList<Entity>(allocator);
            RemovedSceneTagWithoutParentEntities = new NativeList<Entity>(allocator);
            AddedSceneTagWithoutParentComponents = new NativeList<SceneTag>(allocator);
        }

        public void Dispose()
        {
            CreatedEntities.Dispose();
            DestroyedEntities.Dispose();
            AddedParentEntities.Dispose();
            RemovedParentEntities.Dispose();
            AddedParentComponents.Dispose();
            RemovedParentComponents.Dispose();
            AddedParentSceneTagForNullParentComponents.Dispose();
            AddedSceneTagWithoutParentEntities.Dispose();
            RemovedSceneTagWithoutParentEntities.Dispose();
            AddedSceneTagWithoutParentComponents.Dispose();
        }
    }

    /// <summary>
    /// The <see cref="HierarchyEntityChangeTracker"/> is responsible for tracking hierarchy changes over time from the underlying data model (entity or gameObject).
    /// </summary>
    class HierarchyEntityChangeTracker : IDisposable
    {
        public enum OperationModeType
        {
            /// <summary>
            /// Linear operation mode outputs all entities linearly as is without any hierarchical information.
            /// </summary>
            Linear,

            /// <summary>
            /// This mode outputs hierarchical information based on <see cref="SceneReference"/> and <see cref="Parent"/> components.
            /// </summary>
            SceneReferenceAndParentComponents,
        }

        static readonly EntityQueryDesc k_EntityQueryDesc = new EntityQueryDesc();

        static readonly EntityQueryDesc k_ParentQueryDesc = new EntityQueryDesc {All = new ComponentType[] {typeof(Parent)}};
        static readonly EntityQueryDesc k_SceneTagWithoutParentQueryDesc = new EntityQueryDesc {All = new ComponentType[] {typeof(SceneTag)}, None = new ComponentType[] {typeof(SceneReference), typeof(Parent)}};

        readonly World m_World;

        /// <summary>
        /// Change trackers handle the low level entities APIs and gather a set of potentially changed data.
        /// </summary>
        readonly EntityDiffer m_EntityChangeTracker;

        readonly ComponentDataDiffer m_ParentChangeTracker;
        readonly UnmanagedSharedComponentDataDiffer m_SceneTagWithoutParentChangeTracker;
        readonly EntityQuery m_EmptyQuery;

        OperationModeType m_OperationModeType = OperationModeType.SceneReferenceAndParentComponents;

        EntityQueryDesc m_EntityQueryDesc;

        EntityQuery m_EntityQuery;
        EntityQuery m_ParentQuery;
        EntityQuery m_SceneTagWithoutParentQuery;

#if ENTITY_STORE_V1
        NativeList<int> m_DistinctBuffer;
#else
        NativeHashMap<Entity, int> m_DistinctBuffer;
#endif

        /// <summary>
        /// Gets or sets the operation mode used by the change tracker. This determines which components and data drives the hierarchy.
        /// </summary>
        public OperationModeType OperationMode
        {
            get => m_OperationModeType;
            set
            {
                if (m_OperationModeType == value)
                    return;

                m_OperationModeType = value;
                RebuildQueryCache(m_EntityQueryDesc);
            }
        }

        /// <summary>
        /// Gets or sets the entity query used by the change tracker.
        /// </summary>
        /// <remarks>
        /// A value of 'null' indicates the 'UniversalQuery' should be used.
        /// </remarks>
        public EntityQueryDesc EntityQueryDesc
        {
            get => m_EntityQueryDesc;
            set
            {
                if (m_EntityQueryDesc == value)
                    return;

                m_EntityQueryDesc = value;
                RebuildQueryCache(value);
            }
        }

        void RebuildQueryCache(EntityQueryDesc value)
        {
            var desc = null == value ? m_World.EntityManager.UniversalQueryWithSystems.GetEntityQueryDesc() : value;

            switch (m_OperationModeType)
            {
                case OperationModeType.Linear:
                {
                    m_EntityQuery = CreateEntityQuery(desc, k_EntityQueryDesc);
                    m_ParentQuery = m_EmptyQuery;
                    m_SceneTagWithoutParentQuery = m_EmptyQuery;
                    break;
                }

                case OperationModeType.SceneReferenceAndParentComponents:
                {
                    m_EntityQuery = CreateEntityQuery(desc, k_EntityQueryDesc);
                    m_ParentQuery = CreateEntityQuery(desc, k_ParentQueryDesc);
                    m_SceneTagWithoutParentQuery = CreateEntityQuery(desc, k_SceneTagWithoutParentQueryDesc);
                    break;
                }
            }
        }

        // Component that is never added to an Entity, used to create EntityQuery that is always empty.
        private struct UnusedTag : IComponentData {}

        public unsafe HierarchyEntityChangeTracker(World world, Allocator allocator)
        {
            m_World = world;
            var ecs = world.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
            m_EntityChangeTracker = new EntityDiffer(world);
            m_ParentChangeTracker = new ComponentDataDiffer(ecs, ComponentType.ReadOnly<Parent>());
            m_SceneTagWithoutParentChangeTracker = new UnmanagedSharedComponentDataDiffer(ComponentType.ReadOnly<SceneTag>());

#if ENTITY_STORE_V1
            m_DistinctBuffer = new NativeList<int>(16, allocator);
#else
            m_DistinctBuffer = new NativeHashMap<Entity, int>(16, allocator);
#endif

            m_EmptyQuery = m_World.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[] {typeof(UnusedTag)}});

            RebuildQueryCache(null);
        }

        public void Dispose()
        {
            m_EntityChangeTracker.Dispose();
            m_ParentChangeTracker.Dispose();
            m_SceneTagWithoutParentChangeTracker.Dispose();
            m_DistinctBuffer.Dispose();
        }

        public HierarchyEntityChanges GetChanges(Allocator allocator)
        {
            var changes = new HierarchyEntityChanges(allocator);
            GetChanges(changes);
            return changes;
        }

        public void GetChanges(HierarchyEntityChanges changes)
        {
            changes.Clear();

            // Component changes can be gathered in parallel.
            var entityChangesJobHandle = m_EntityChangeTracker.GetEntityQueryMatchDiffAsync(m_EntityQuery, changes.CreatedEntities, changes.DestroyedEntities);
            var parentChanges = m_ParentChangeTracker.GatherComponentChangesAsync(m_ParentQuery, Allocator.TempJob, out var parentComponentChangesJobHandle);

            JobHandle.CombineDependencies(entityChangesJobHandle, parentComponentChangesJobHandle).Complete();

            // Shared component changes must be gathered synchronously.
            var sceneTagWithoutParentChanges = m_SceneTagWithoutParentChangeTracker.GatherComponentChanges(m_World.EntityManager, m_SceneTagWithoutParentQuery, Allocator.TempJob);

            parentChanges.GetAddedComponentEntities(changes.AddedParentEntities);
            parentChanges.GetRemovedComponentEntities(changes.RemovedParentEntities);
            parentChanges.GetAddedComponentData(changes.AddedParentComponents);
            parentChanges.GetRemovedComponentData(changes.RemovedParentComponents);

            sceneTagWithoutParentChanges.GetAddedComponentEntities(changes.AddedSceneTagWithoutParentEntities);
            sceneTagWithoutParentChanges.GetRemovedComponentEntities(changes.RemovedSceneTagWithoutParentEntities);
            sceneTagWithoutParentChanges.GetAddedComponentData(changes.AddedSceneTagWithoutParentComponents);

            if (changes.HasChanges())
            {
                new DistinctJob
                {
#if ENTITY_STORE_V1
                    EntityCapacity = m_World.EntityManager.EntityCapacity,
#endif
                    Changes = changes,
                    DistinctBuffer = m_DistinctBuffer
                }.Run();

                // Include the SceneTag component for null 'Parent' references.
                changes.AddedParentSceneTagForNullParentComponents.ResizeUninitialized(changes.AddedParentEntities.Length);

                for (var i = 0; i < changes.AddedParentEntities.Length; i++)
                {
                    var entity = changes.AddedParentEntities[i];

                    if (changes.AddedParentComponents[i].Value == Entity.Null && m_World.EntityManager.HasComponent<SceneTag>(entity))
                        changes.AddedParentSceneTagForNullParentComponents[i] = m_World.EntityManager.GetSharedComponent<SceneTag>(entity);
                    else
                        changes.AddedParentSceneTagForNullParentComponents[i] = default;
                }
            }

            parentChanges.Dispose();
            sceneTagWithoutParentChanges.Dispose();
        }

        [BurstCompile]
        struct DistinctJob : IJob
        {
            public HierarchyEntityChanges Changes;

#if ENTITY_STORE_V1
            public int EntityCapacity;
            public NativeList<int> DistinctBuffer;
#else
            public NativeHashMap<Entity, int> DistinctBuffer;
#endif

            public void Execute()
            {
#if ENTITY_STORE_V1
                DistinctBuffer.ResizeUninitialized(EntityCapacity);
#endif

                if (Changes.CreatedEntities.Length > 0 && Changes.DestroyedEntities.Length > 0)
                    RemoveDuplicate(DistinctBuffer, Changes.CreatedEntities, Changes.DestroyedEntities);

                if (Changes.AddedParentEntities.Length > 0 && Changes.RemovedParentEntities.Length > 0)
#if ENTITY_STORE_V1
                    RemoveDuplicate(DistinctBuffer.AsArray(), Changes.AddedParentEntities, Changes.RemovedParentEntities, Changes.AddedParentComponents, Changes.RemovedParentComponents);
#else
                    RemoveDuplicate(DistinctBuffer, Changes.AddedParentEntities, Changes.RemovedParentEntities, Changes.AddedParentComponents, Changes.RemovedParentComponents);
#endif

                if (Changes.AddedSceneTagWithoutParentEntities.Length > 0 && Changes.RemovedSceneTagWithoutParentEntities.Length > 0)
#if ENTITY_STORE_V1
                    RemoveDuplicate(DistinctBuffer.AsArray(), Changes.AddedSceneTagWithoutParentEntities, Changes.RemovedSceneTagWithoutParentEntities, Changes.AddedSceneTagWithoutParentComponents);
#else
                    RemoveDuplicate(DistinctBuffer, Changes.AddedSceneTagWithoutParentEntities, Changes.RemovedSceneTagWithoutParentEntities, Changes.AddedSceneTagWithoutParentComponents);
#endif
            }

            static unsafe void RemoveDuplicate(
#if ENTITY_STORE_V1
                NativeList<int> index,
#else
                NativeHashMap<Entity, int> index,
#endif
                NativeList<Entity> added, NativeList<Entity> removed)
            {
#if ENTITY_STORE_V1
                UnsafeUtility.MemClear(index.GetUnsafePtr(), index.Length * sizeof(int));
#else
                index.Clear();
#endif

                var addedLength = added.Length;
                var removedLength = removed.Length;

                for (var i = 0; i < addedLength; i++)
#if ENTITY_STORE_V1
                    index[added[i].Index] = i + 1;
#else
                    index[added[i]] = i + 1;
#endif

                for (var i = 0; i < removedLength; i++)
                {
#if ENTITY_STORE_V1
                    var addIndex = index[removed[i].Index] - 1;
#else
                    // TryGetValue returns default is the value is missing.
                    index.TryGetValue(removed[i], out var addIndex);
                    addIndex -= 1;
#endif

                    if (addIndex < 0)
                        continue;

                    // An entity was recorded as added AND removed with the same index.
                    var addedEntity = added[addIndex];
                    var removedEntity = removed[i];

                    if (addedEntity.Version != removedEntity.Version)
                        continue;

                    // Swap back
                    added[addIndex] = added[addedLength - 1];
                    removed[i] = removed[removedLength - 1];

#if ENTITY_STORE_V1
                    index[added[addIndex].Index] = addIndex + 1;
#else
                    index[added[addIndex]] = addIndex + 1;
#endif

                    addedLength--;
                    removedLength--;
                    i--;
                }

                added.ResizeUninitialized(addedLength);
                removed.ResizeUninitialized(removedLength);
            }

            unsafe void RemoveDuplicate<TData>(
#if ENTITY_STORE_V1
                NativeArray<int> index,
#else
                NativeHashMap<Entity, int> index,
#endif
                NativeList<Entity> added, NativeList<Entity> removed, NativeList<TData> data) where TData : unmanaged
            {
#if ENTITY_STORE_V1
                UnsafeUtility.MemClear(index.GetUnsafePtr(), index.Length * sizeof(int));
#else
                index.Clear();
#endif

                var addedLength = added.Length;
                var removedLength = removed.Length;

                for (var i = 0; i < addedLength; i++)
#if ENTITY_STORE_V1
                    index[added[i].Index] = i + 1;
#else
                    index[added[i]] = i + 1;
#endif

                for (var i = 0; i < removedLength; i++)
                {
#if ENTITY_STORE_V1
                    var addIndex = index[removed[i].Index] - 1;
#else
                    // TryGetValue returns default is the value is missing.
                    index.TryGetValue(removed[i], out var addIndex);
                    addIndex -= 1;
#endif

                    if (addIndex < 0)
                        continue;

                    // An entity was recorded as added AND removed with the same index.
                    var addedEntity = added[addIndex];
                    var removedEntity = removed[i];

                    if (addedEntity.Version != removedEntity.Version)
                        continue;

                    // Swap back
                    added[addIndex] = added[addedLength - 1];
                    data[addIndex] = data[addedLength - 1];
                    removed[i] = removed[removedLength - 1];

#if ENTITY_STORE_V1
                    index[added[addIndex].Index] = addIndex + 1;
#else
                    index[added[addIndex]] = addIndex + 1;
#endif

                    addedLength--;
                    removedLength--;
                }

                added.ResizeUninitialized(addedLength);
                data.ResizeUninitialized(addedLength);
                removed.ResizeUninitialized(removedLength);
            }

            unsafe void RemoveDuplicate<TData>(
#if ENTITY_STORE_V1
                NativeArray<int> index,
#else
                NativeHashMap<Entity, int> index,
#endif
                NativeList<Entity> addedEntities, NativeList<Entity> removedEntities, NativeList<TData> addedData, NativeList<TData> removedData) where TData : unmanaged
            {
#if ENTITY_STORE_V1
                UnsafeUtility.MemClear(index.GetUnsafePtr(), index.Length * sizeof(int));
#else
                index.Clear();
#endif

                var addedLength = addedEntities.Length;
                var removedLength = removedEntities.Length;

                for (var i = 0; i < addedLength; i++)
#if ENTITY_STORE_V1
                    index[addedEntities[i].Index] = i + 1;
#else
                    index[addedEntities[i]] = i + 1;
#endif

                for (var i = 0; i < removedLength; i++)
                {
#if ENTITY_STORE_V1
                    var addIndex = index[removedEntities[i].Index] - 1;
#else
                    // TryGetValue returns default is the value is missing.
                    index.TryGetValue(removedEntities[i], out var addIndex);
                    addIndex -= 1;
#endif

                    if (addIndex < 0)
                        continue;

                    var a = addedData[addIndex];
                    var b = removedData[i];

                    // Only filter if the data is the same.
                    if (UnsafeUtility.MemCmp(&a, &b, sizeof(TData)) != 0)
                        continue;

                    // An entity was recorded as added AND removed with the same index.
                    var addedEntity = addedEntities[addIndex];
                    var removedEntity = removedEntities[i];

                    if (addedEntity.Version != removedEntity.Version)
                        continue;

                    // Swap back
                    addedEntities[addIndex] = addedEntities[addedLength - 1];
                    addedData[addIndex] = addedData[addedLength - 1];
                    removedEntities[i] = removedEntities[removedLength - 1];
                    removedData[i] = removedData[removedLength - 1];

#if ENTITY_STORE_V1
                    index[addedEntities[addIndex].Index] = addIndex + 1;
#else
                    index[addedEntities[addIndex]] = addIndex + 1;
#endif

                    addedLength--;
                    removedLength--;
                }

                addedEntities.ResizeUninitialized(addedLength);
                addedData.ResizeUninitialized(addedLength);
                removedEntities.ResizeUninitialized(removedLength);
            }
        }

        EntityQuery CreateEntityQuery(params EntityQueryDesc[] queriesDesc)
        {
            try
            {
                ValidateEntityQueryDesc(queriesDesc);
            }
            catch
            {
                return m_EmptyQuery;
            }

            var builder = new EntityQueryBuilder(Allocator.Temp);

            unsafe
            {
                for (var q = 0; q != queriesDesc.Length; q++)
                {
                    fixed (ComponentType* types = queriesDesc[q].All)
                            builder.WithAll(types, queriesDesc[q].All.Length);

                    fixed (ComponentType* types = queriesDesc[q].Any)
                        builder.WithAny(types, queriesDesc[q].Any.Length);

                    fixed (ComponentType* types = queriesDesc[q].None)
                        builder.WithNone(types, queriesDesc[q].None.Length);
                }

                builder.WithOptions(queriesDesc[0].Options);
            }

            return m_World.EntityManager.CreateEntityQuery(builder);
        }

        static void ValidateEntityQueryDesc(params EntityQueryDesc[] queriesDesc)
        {
            var count = 0;

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var entityQueryDesc in queriesDesc)
                count += entityQueryDesc.None.Length + entityQueryDesc.All.Length + entityQueryDesc.Any.Length;

            var componentTypeIds = new NativeArray<TypeIndex>(count, Allocator.Temp);

            var componentIndex = 0;

            foreach (var entityQueryDesc in queriesDesc)
            {
                ValidateComponentTypes(entityQueryDesc.None, ref componentTypeIds, ref componentIndex);
                ValidateComponentTypes(entityQueryDesc.All, ref componentTypeIds, ref componentIndex);
                ValidateComponentTypes(entityQueryDesc.Any, ref componentTypeIds, ref componentIndex);
            }

            // Check for duplicate, only if necessary
            if (count > 1)
            {
                // Sort the Ids to have identical value adjacent
                componentTypeIds.Sort();

                // Check for identical values
                var refId = componentTypeIds[0];

                for (var i = 1; i < componentTypeIds.Length; i++)
                {
                    var curId = componentTypeIds[i];
                    if (curId == refId)
                    {
                        var compType = TypeManager.GetType(curId);
                        throw new EntityQueryDescValidationException(
                            $"EntityQuery contains a filter with duplicate component type name {compType.Name}.  Queries can only contain a single component of a given type in a filter.");
                    }

                    refId = curId;
                }
            }

            componentTypeIds.Dispose();
        }

        static void ValidateComponentTypes(ComponentType[] componentTypes, ref NativeArray<TypeIndex> allComponentTypeIds, ref int curComponentTypeIndex)
        {
            foreach (var componentType in componentTypes)
            {
                allComponentTypeIds[curComponentTypeIndex++] = componentType.TypeIndex;

                if (componentType.AccessModeType == ComponentType.AccessMode.Exclude)
                    throw new ArgumentException("EntityQueryDesc cannot contain Exclude Component types");
            }
        }
    }
}
