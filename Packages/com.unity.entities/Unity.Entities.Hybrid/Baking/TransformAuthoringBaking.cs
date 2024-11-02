using System;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Baking;
using Unity.Entities.Conversion;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine.Jobs;

namespace Unity.Entities
{
    internal struct TransformAuthoringBaking
    {
        EntityManager               _EntityManager;
        EntityQuery                 _StaticQuery;
        EntityQueryMask             _StaticQueryMask;
        EntityQuery                 _AdditionalEntityParentQuery;
        internal JobHandle          _JobHandle;
        NativeParallelHashMap<int, bool>    _LocalToWorldIndices;
        IncrementalHierarchy        _SceneHierarchy;

        public TransformAuthoringBaking(EntityManager entityManager)
        {
            _EntityManager = entityManager;
            _AdditionalEntityParentQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<AdditionalEntityParent>()
                .WithAllRW<TransformAuthoring>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .Build(entityManager);
            _StaticQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Static>().Build(entityManager);
            Assert.IsFalse(_StaticQuery.HasFilter(), "The use of EntityQueryMask in this job will not respect the query's active filter settings.");
            _StaticQueryMask = _StaticQuery.GetEntityQueryMask();
            _LocalToWorldIndices = new NativeParallelHashMap<int, bool>(1024, Allocator.Persistent);
            _JobHandle = default;
            _SceneHierarchy = default;
        }

        public void Dispose()
        {
            _AdditionalEntityParentQuery.Dispose();
            _StaticQuery.Dispose();
            _LocalToWorldIndices.Dispose();
        }

        // Perform any work that doesn't rely on all the entities being created
        public JobHandle Prepare(IncrementalHierarchy hierarchy, NativeList<int> changedTransforms)
        {
            _JobHandle.Complete();

            _SceneHierarchy = hierarchy;
            _LocalToWorldIndices.Clear();

            var sceneHierarchy = hierarchy.AsReadOnly();
            _JobHandle = sceneHierarchy.CollectHierarchyInstanceIdsAndIndicesAsync(changedTransforms, _LocalToWorldIndices);

            var bakeTransformAuthoring = new BakeToTransformAuthoringListJob
            {
                Hierarchy = hierarchy.AsReadOnly(),
                ChangedIndices = _LocalToWorldIndices,
                ChangeVersion = _EntityManager.GlobalSystemVersion,
                TransformAuthorings = _SceneHierarchy.TransformAuthorings.AsArray()
            };
            _JobHandle = bakeTransformAuthoring.ScheduleReadOnly(_SceneHierarchy.TransformArray, 64, _JobHandle);
            return _JobHandle;
        }

        // Perform any work that relies on all entities having been constructed
        public void UpdateTransforms(UnsafeParallelHashMap<int, Entity> gameObjectToEntity, UnsafeParallelHashMap<Entity, TransformUsageFlagCounters> transformUsages, ref bool hasTransformUsageChanged)
        {
            using var marker = new ProfilerMarker("TransformHierarchyBaking.BakeToTransformAuthoring").Auto();

#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            var changedUsage = new NativeArray<bool>(maxThreadCount, Allocator.TempJob);

            var parentsToForceTransform = new UnsafeDependencyStream<Entity>(Allocator.TempJob);
            parentsToForceTransform.BeginWriting();

            var job = new BakeToTransformAuthoringComponentJob()
            {
                Hierarchy = _SceneHierarchy.AsReadOnly(),
                ChangedIndices = _LocalToWorldIndices,
                TransformAuthoring = _EntityManager.GetComponentLookup<TransformAuthoring>(),
                GameObjectToEntity = gameObjectToEntity,
                TransformAuthorings = _SceneHierarchy.TransformAuthorings.AsArray(),
                TransformUsages = transformUsages,
                HasTransformUsageChanged = hasTransformUsageChanged,
                ChangeVersion = _EntityManager.GlobalSystemVersion,
                ParentsToForceTransform = parentsToForceTransform,
                ChangedUsage = changedUsage
            };

            _JobHandle = job.ScheduleParallel(job.TransformAuthorings.Length, 128, _JobHandle);

            var cmd = new EntityCommandBuffer(Allocator.TempJob);
            var additionalEntityJob = new BakeAdditionalEntityTransformAuthoringJob
            {
                TransformAuthoringLookup = _EntityManager.GetComponentLookup<TransformAuthoring>(true),
                TransformAuthoringHandle = _EntityManager.GetComponentTypeHandle<TransformAuthoring>(false),
                HasStatic = _StaticQueryMask,
                AdditionalEntityParent = _EntityManager.GetComponentTypeHandle<AdditionalEntityParent>(true),
                Entities = _EntityManager.GetEntityTypeHandle(),
                Commands = cmd.AsParallelWriter(),
                Hierarchy = _SceneHierarchy.AsReadOnly(),
                GameObjectToEntity = gameObjectToEntity,
                TransformUsages = transformUsages,
                ChangeVersion = _EntityManager.GlobalSystemVersion,
                ParentsToForceTransform = parentsToForceTransform,
                ChangedUsage = changedUsage
            };
            _JobHandle = additionalEntityJob.ScheduleParallelByRef(_AdditionalEntityParentQuery, _JobHandle);

            _JobHandle = parentsToForceTransform.EndWriting(_JobHandle);

            _JobHandle.Complete();

            new ForceTransformsOnParentsJob
            {
                parentsToForceTransform = parentsToForceTransform,
                TransformAuthoring = _EntityManager.GetComponentLookup<TransformAuthoring>()
            }.Run();

            if (!hasTransformUsageChanged)
            {
                foreach (var changed in changedUsage)
                {
                    hasTransformUsageChanged |= changed;
                }
            }

            cmd.Playback(_EntityManager);
            cmd.Dispose();

            parentsToForceTransform.Dispose();
            changedUsage.Dispose();
        }

        static bool IsNone(TransformUsageFlags flags)
        {
            return flags == TransformUsageFlags.None;
        }

        static bool IsManualOverride(TransformUsageFlags flags)
        {
            return (flags & TransformUsageFlags.ManualOverride) != 0;
        }

        static bool IsWorldSpace(TransformUsageFlags flags)
        {
            return (flags & TransformUsageFlags.WorldSpace) != 0;
        }

        static bool IsDynamic(TransformUsageFlags flags)
        {
            return (flags & TransformUsageFlags.Dynamic) != 0;
        }

        static bool IsNonUniformScale(TransformUsageFlags flags)
        {
            return (flags & TransformUsageFlags.NonUniformScale) != 0;
        }

        static TransformUsageFlags GetTransformUsageFlagsFromIndex(int index, ref UnsafeParallelHashMap<int, Entity> gameObjectToEntity, ref SceneHierarchy sceneHierarchy, ref UnsafeParallelHashMap<Entity, TransformUsageFlagCounters> transformUsages, out Entity entity)
        {
            if (!gameObjectToEntity.TryGetValue(sceneHierarchy.GetInstanceIdForIndex(index), out entity))
                Debug.LogError("InternalError");
            transformUsages.TryGetValue(entity, out var parentTransformUsage);
            return parentTransformUsage.Flags;
        }

        static bool IsAnyParentDynamicOrManual(int parentIndex, ref UnsafeParallelHashMap<int, Entity> gameObjectToEntity, ref SceneHierarchy sceneHierarchy, ref UnsafeParallelHashMap<Entity, TransformUsageFlagCounters> transformUsages)
        {
            while (parentIndex != -1)
            {
                var parentTransformUsageFlags = GetTransformUsageFlagsFromIndex(parentIndex, ref gameObjectToEntity, ref sceneHierarchy, ref transformUsages, out _);
                if (IsDynamic(parentTransformUsageFlags) || IsManualOverride(parentTransformUsageFlags))
                    return true;

                // If the parent is in world space then it will be detach and their parents are irrelevant
                if (IsWorldSpace(parentTransformUsageFlags))
                    return false;

                // Access the next parent
                parentIndex = sceneHierarchy.GetParentForIndex(parentIndex);
            }
            return false;
        }

        static void CalculateGlobalTransformUsage(ref SceneHierarchy sceneHierarchy, ref UnsafeParallelHashMap<int, Entity> gameObjectToEntity, ref UnsafeParallelHashMap<Entity, TransformUsageFlagCounters> transformUsages, Entity entity, int parentIndex, int threadIndex, ref UnsafeDependencyStream<Entity> parentsToForceTransform, out Entity outParent, out RuntimeTransformComponentFlags outUsage)
        {
            transformUsages.TryGetValue(entity, out var entityTransformUsage);
            var computedUsage = entityTransformUsage.Flags;
            outParent = Entity.Null;

            // If no one needs the transform on this entity, then there is no reason to check out the parents
            if (IsNone(computedUsage))
            {
                outUsage = RuntimeTransformComponentFlags.None;
                return;
            }

            if (IsManualOverride(computedUsage))
            {
                // Check if there is any parent that is Dynamic or Manual with at least one intermediate node as None
                if (parentIndex != -1 && IsAnyParentDynamicOrManual(parentIndex, ref gameObjectToEntity, ref sceneHierarchy, ref transformUsages))
                {
                    // Access the immediate parent
                    if (!gameObjectToEntity.TryGetValue(sceneHierarchy.GetInstanceIdForIndex(parentIndex), out outParent))
                        Debug.LogError($"Expected parent entity for parent id");

                    transformUsages.TryGetValue(outParent, out var parentTransformUsage);
                    if (IsNone(parentTransformUsage.Flags))
                    {
                        // Coerce your parent into having Transform Data
                        parentsToForceTransform.Add(outParent, threadIndex);
                    }
                }

                outParent = Entity.Null;
                outUsage = RuntimeTransformComponentFlags.ManualOverride;
                return;
            }

            // We are always adding LocalToWorld from this point
            outUsage = RuntimeTransformComponentFlags.LocalToWorld;

            // NonUniformScale is independant on any other considerations
            if (IsNonUniformScale(computedUsage))
            {
                outUsage |= RuntimeTransformComponentFlags.PostTransformMatrix;
            }

            // The parent is irrelevant as it is requesting to be in world space or it hasn't got one
            if (IsWorldSpace(computedUsage) || parentIndex == -1 || !IsAnyParentDynamicOrManual(parentIndex, ref gameObjectToEntity, ref sceneHierarchy, ref transformUsages))
            {
                if (IsDynamic(computedUsage))
                {
                    outUsage |= RuntimeTransformComponentFlags.LocalTransform;
                }
            }
            else
            {
                // Access the immediate parent
                if (!gameObjectToEntity.TryGetValue(sceneHierarchy.GetInstanceIdForIndex(parentIndex), out outParent))
                    Debug.LogError($"Expected parent entity for parent id");

                transformUsages.TryGetValue(outParent, out var parentTransformUsage);
                if (IsNone(parentTransformUsage.Flags))
                {
                    // Coerce your parent into having Transform Data
                    parentsToForceTransform.Add(outParent, threadIndex);
                }

                outUsage |= RuntimeTransformComponentFlags.LocalTransform | RuntimeTransformComponentFlags.RequestParent;
            }
        }

        [BurstCompile]
        struct ForceTransformsOnParentsJob : IJob
        {
            [NativeDisableParallelForRestriction]
            public ComponentLookup<TransformAuthoring> TransformAuthoring;

            [ReadOnly]
            public UnsafeDependencyStream<Entity> parentsToForceTransform;

            public void Execute()
            {
                foreach (var entry in parentsToForceTransform)
                {
                    var currentEntity = entry;
                    while (currentEntity != Entity.Null)
                    {
                        var transformAuthoring = TransformAuthoring[currentEntity];

                        // Exit the loop as we have already processed this part of the tree
                        if (transformAuthoring.RuntimeTransformUsage != RuntimeTransformComponentFlags.None)
                            break;

                        transformAuthoring.RuntimeTransformUsage = RuntimeTransformComponentFlags.LocalToWorld |
                                                                   RuntimeTransformComponentFlags.LocalTransform |
                                                                   RuntimeTransformComponentFlags.RequestParent;

                        // We need to assign the parent
                        transformAuthoring.RuntimeParent = transformAuthoring.AuthoringParent;

                        // Copy the values back
                        TransformAuthoring[currentEntity] = transformAuthoring;

                        // Move to the next parent
                        currentEntity = transformAuthoring.RuntimeParent;
                    }
                }
            }
        }

        // TODO: DOTS-5468
        [BurstCompile]
        unsafe struct BakeAdditionalEntityTransformAuthoringJob : IJobChunk
        {
            [NativeDisableContainerSafetyRestriction]
            [ReadOnly]
            public ComponentLookup<TransformAuthoring>   TransformAuthoringLookup;
            public EntityQueryMask                               HasStatic;

            public EntityTypeHandle                              Entities;
            public ComponentTypeHandle<TransformAuthoring>       TransformAuthoringHandle;
            [ReadOnly]
            public ComponentTypeHandle<AdditionalEntityParent>   AdditionalEntityParent;

            public EntityCommandBuffer.ParallelWriter            Commands;

            [ReadOnly]
            public UnsafeParallelHashMap<Entity, TransformUsageFlagCounters> TransformUsages;
            [ReadOnly]
            public SceneHierarchy                                    Hierarchy;
            [ReadOnly]
            public UnsafeParallelHashMap<int, Entity>                        GameObjectToEntity;
            public uint                                              ChangeVersion;
            public UnsafeDependencyStream<Entity>                    ParentsToForceTransform;
            [NativeDisableParallelForRestriction]
            public NativeArray<bool>                                 ChangedUsage;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                var count = chunk.Count;

                // By default only get read access to the chunk. Most of the time we don't want to dirty these chunks. Only if something actually changed
                var transformsRO = (TransformAuthoring*)chunk.GetComponentDataPtrRO(ref TransformAuthoringHandle);

                TransformAuthoring* transformsRW = null;

                var parents = chunk.GetNativeArray(ref AdditionalEntityParent);
                var entities = chunk.GetNativeArray(Entities);

                // Replicate static flag onto additional entities from primary entity
                var isStatic = HasStatic.MatchesIgnoreFilter(chunk);
                for (int i = 0; i != count; i++)
                {
                    var isParentStatic = HasStatic.MatchesIgnoreFilter(parents[i].Parent);

                    if (isParentStatic != isStatic)
                    {
                        if (isParentStatic)
                            Commands.AddComponent(unfilteredChunkIndex, entities[i], ComponentType.ReadWrite<Static>());
                        else
                            Commands.RemoveComponent(unfilteredChunkIndex, entities[i], ComponentType.ReadWrite<Static>());
                    }
                }

                // Configure TransformAuthoring onto additional entities by treating primary entities as a parent
                // and the additional entity as a transform with identity local transform relative to parent
                for (int i = 0; i != count; i++)
                {
                    var parentTransform = TransformAuthoringLookup[parents[i].Parent];

                    var parentIndex = Hierarchy.GetIndexForInstanceId(parents[i].ParentInstanceID);
                    CalculateGlobalTransformUsage(ref Hierarchy, ref GameObjectToEntity, ref TransformUsages, entities[i], parentIndex, m_ThreadIndex, ref ParentsToForceTransform, out var runtimeParent, out var runtimeTransformUsage);


                    if (parentTransform.ChangeVersion != transformsRO[i].ChangeVersion || transformsRO[i].RuntimeParent != runtimeParent || transformsRO[i].RuntimeTransformUsage != runtimeTransformUsage)
                    {
                        ChangedUsage[m_ThreadIndex] = true;

                        TransformAuthoring transform;
                        transform.AuthoringParent = parents[i].Parent;
                        transform.Position = parentTransform.Position;
                        transform.Rotation = parentTransform.Rotation;

                        transform.LocalPosition = new float3(0);
                        transform.LocalRotation = quaternion.identity;
                        transform.LocalScale = new float3(1);
                        transform.LocalToWorld = parentTransform.LocalToWorld;
                        transform.ChangeVersion = ChangeVersion;
                        transform.RuntimeParent = runtimeParent;
                        transform.RuntimeTransformUsage = runtimeTransformUsage;

                        if (transformsRW == null)
                            transformsRW = (TransformAuthoring*)chunk.GetComponentDataPtrRW(ref TransformAuthoringHandle);
                        transformsRW[i] = transform;
                    }
                }
            }
        }

        [BurstCompile]
        struct BakeToTransformAuthoringListJob : IJobParallelForTransform
        {
            [ReadOnly] public SceneHierarchy           Hierarchy;
            public NativeArray<TransformAuthoring>     TransformAuthorings;

            [ReadOnly] public NativeParallelHashMap<int, bool> ChangedIndices;
            public  uint                               ChangeVersion;

            public void Execute(int index, TransformAccess transform)
            {
                if (!ChangedIndices.TryGetValue(index, out var selfChanged))
                     return;

                var parentIndex = Hierarchy.GetParentForIndex(index);
                var parentInstanceID = 0;
                if (parentIndex != -1)
                    parentInstanceID = Hierarchy.GetInstanceIdForIndex(parentIndex);

                TransformAuthoring value;
                value.Position = transform.position;
                value.Rotation = transform.rotation;

                value.LocalPosition = transform.localPosition;
                value.LocalRotation = transform.localRotation;
                value.LocalScale = transform.localScale;

                value.LocalToWorld = transform.localToWorldMatrix;
                value.RuntimeTransformUsage = default;
                value.RuntimeParent = default;
                // This is a bit of a hack, in the flattened NativeList
                value.AuthoringParent.Index = parentInstanceID;
                value.AuthoringParent.Version = 0;
                value.ChangeVersion = ChangeVersion;

                TransformAuthorings[index] = value;
            }
        }


        [BurstCompile]
        struct BakeToTransformAuthoringComponentJob : IJobFor
        {
            [ReadOnly] public SceneHierarchy Hierarchy;
            [ReadOnly] public NativeParallelHashMap<int, bool> ChangedIndices;
            [ReadOnly] public NativeArray<TransformAuthoring> TransformAuthorings;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<TransformAuthoring> TransformAuthoring;

            [ReadOnly]
            public UnsafeParallelHashMap<int, Entity> GameObjectToEntity;
            [ReadOnly]
            public UnsafeParallelHashMap<Entity, TransformUsageFlagCounters> TransformUsages;
            public uint                                              ChangeVersion;
            public bool                                              HasTransformUsageChanged;
            public UnsafeDependencyStream<Entity>                    ParentsToForceTransform;
            [NativeDisableParallelForRestriction]
            public NativeArray<bool>                                 ChangedUsage;
            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            public void Execute(int index)
            {
                // TODO: Review the performance impact of this change.
                var hasPossibleChange = HasTransformUsageChanged || !ChangedIndices.IsEmpty /*ChangedIndices.TryGetValue(index, out var selfChanged)*/;
                if (!hasPossibleChange)
                    return;

                // instanceID and primary entity we want to apply this to
                int instanceID = Hierarchy.GetInstanceIdForIndex(index);
                if (!GameObjectToEntity.TryGetValue(instanceID, out var entity))
                    return;

                // Need to transform the parent from instanceID to Entity
                TransformAuthoring value = TransformAuthorings[index];
                var parentInstanceID = value.AuthoringParent.Index;
                GameObjectToEntity.TryGetValue(parentInstanceID, out value.AuthoringParent);

                // Calculate hierarchical transform usage
                var parentIndex = Hierarchy.GetIndexForInstanceId(parentInstanceID);
                CalculateGlobalTransformUsage(ref Hierarchy, ref GameObjectToEntity, ref TransformUsages, entity, parentIndex, m_ThreadIndex, ref ParentsToForceTransform, out value.RuntimeParent, out value.RuntimeTransformUsage);

                // Apply it only if it changed, so that we don't dirty change filtering
                var oldValue = TransformAuthoring[entity];
                if (!oldValue.Equals(value))
                {
                    ChangedUsage[m_ThreadIndex] = ChangedUsage[m_ThreadIndex] || (oldValue.RuntimeParent != value.RuntimeParent) || (oldValue.RuntimeTransformUsage != value.RuntimeTransformUsage);
                    value.ChangeVersion = ChangeVersion;
                    TransformAuthoring[entity] = value;
                }
            }
        }
    }
}
