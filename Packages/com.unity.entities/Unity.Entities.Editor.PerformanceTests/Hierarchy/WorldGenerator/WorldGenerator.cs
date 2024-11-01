using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace Unity.Entities.Editor.PerformanceTests
{
    class WorldGenerator : IDisposable
    {
        // Minimum set to be picked up by ParentSystem + GUID for tracking entities after changes
        static readonly ComponentType[] k_BasicArchetype = { typeof(EntityGuid), typeof(LocalToWorld), typeof(LocalTransform) };
        static readonly ComponentType[][] k_ArchetypeVariants =
        {
            new ComponentType[] {typeof(SegmentId)},

            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker1)},
            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker2)},
            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker3)},
            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker4)},

            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker1), typeof(ArchetypeMarker2)},
            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker1), typeof(ArchetypeMarker3)},
            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker1), typeof(ArchetypeMarker4)},
            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker2), typeof(ArchetypeMarker3)},
            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker2), typeof(ArchetypeMarker4)},
            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker3), typeof(ArchetypeMarker4)},

            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker1), typeof(ArchetypeMarker2), typeof(ArchetypeMarker3)},
            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker1), typeof(ArchetypeMarker2), typeof(ArchetypeMarker4)},
            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker1), typeof(ArchetypeMarker3), typeof(ArchetypeMarker4)},
            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker2), typeof(ArchetypeMarker3), typeof(ArchetypeMarker4)},

            new ComponentType[] {typeof(SegmentId), typeof(ArchetypeMarker1), typeof(ArchetypeMarker2), typeof(ArchetypeMarker3), typeof(ArchetypeMarker4)}
        };

        static readonly ProfilerMarker k_InitializeWorldMarker = new ProfilerMarker($"{nameof(WorldGenerator)}: Initialize World");
        static readonly ProfilerMarker k_CloneWorldMarker = new ProfilerMarker($"{nameof(WorldGenerator)}: Clone World");
        static readonly ProfilerMarker k_GenerateArchetypeMarker = new ProfilerMarker($"{nameof(WorldGenerator)}: Generate Archetype");
        static readonly ProfilerMarker k_GenerateAllEntitiesMarker = new ProfilerMarker($"{nameof(WorldGenerator)} : Generate All Entities");
        static readonly ProfilerMarker k_GenerateArchetypeBatchMarker = new ProfilerMarker("Generate Archetype Batch");
        static readonly ProfilerMarker k_ApplySegmentationMarker = new ProfilerMarker("Apply Segmentation");
        static readonly ProfilerMarker k_AppendToAllEntitiesMarker = new ProfilerMarker("Append Batch to All Entities");
        static readonly ProfilerMarker k_AssignEntityGuid = new ProfilerMarker("Assign EntityGuid to All Entities");
        static readonly ProfilerMarker k_AssignParentsMarker = new ProfilerMarker($"{nameof(WorldGenerator)} : Assign Parents");
        static readonly ProfilerMarker k_AssignParentsPrepareMarker = new ProfilerMarker("Prepare");
        static readonly ProfilerMarker k_AssignParentsCleanupMarker = new ProfilerMarker("Cleanup");
        static readonly ProfilerMarker k_ParentingJobMarker = new ProfilerMarker("Parenting Job");
        static readonly ProfilerMarker k_UpdateParentingMarker = new ProfilerMarker($"{nameof(WorldGenerator)}.{nameof(UpdateParenting)}");

        readonly EntityHierarchyScenario m_Scenario;

        World m_World;
        List<World> m_Clones = new List<World>();
        int m_ArchetypeSequenceNumber;

        // TODO: Useful?
        // Probably useful to test cloning
        public WorldGenerator(EntityHierarchyScenario scenario)
        {
            m_Scenario = scenario;
        }

        public void Dispose()
        {
            if (m_World != null && m_World.IsCreated)
                m_World.Dispose();

            foreach (var clone in m_Clones)
            {
                if (clone != null && clone.IsCreated)
                    clone.Dispose();
            }
        }

        public World Original
        {
            get
            {
                if (m_World == null || !m_World.IsCreated)
                    InitializeWorld();
                return m_World;
            }
        }

        public World Get()
        {
            if (m_World == null || !m_World.IsCreated)
                InitializeWorld();

            var clone = new World($"{m_World.Name} (Clone {m_Clones.Count.ToString()})");

            using (k_CloneWorldMarker.Auto())
            {
#pragma warning disable CS0618 // Type or member is obsolete
                clone.EntityManager.CopyAndReplaceEntitiesFrom(m_World.EntityManager);
#pragma warning restore CS0618 // Type or member is obsolete
            }

            m_Clones.Add(clone);

            return clone;
        }

        void InitializeWorld()
        {
            using (k_InitializeWorldMarker.Auto())
            {
                m_World = GenerateWorld();
            }
        }

        World GenerateWorld()
        {
            var world = new World("DefaultGroupingStrategy Setup");
            var entityManager = world.EntityManager;

            var allEntities = GenerateEntitiesWithFragmentation(entityManager, world.UpdateAllocator.ToAllocator);

            if (m_Scenario.MaximumDepth > 1)
                AssignParents(allEntities, entityManager);

            allEntities.Dispose();

            UpdateParenting(world);

            return world;
        }

        NativeArray<Entity> GenerateEntitiesWithFragmentation(EntityManager entityManager, Allocator allocator)
        {
            using (k_GenerateAllEntitiesMarker.Auto())
            {
                // Allocate all entities
                var allEntities = CollectionHelper.CreateNativeArray<Entity>(m_Scenario.TotalEntities, allocator);

                // Burst-friendly running counter, used when copying new entities to allEntities array
                var entityIndex = new NativeReference<int>(0, entityManager.World.UpdateAllocator.ToAllocator);

                // Ensure we are creating at least one archetype
                var archetypesToCreate = math.max(m_Scenario.AmountOfArchetypeVariants, 1);
                var entitiesPerArchetype = m_Scenario.TotalEntities / archetypesToCreate;
                var remainder = m_Scenario.TotalEntities - entitiesPerArchetype * archetypesToCreate;
                var requiresVariants = m_Scenario.AmountOfArchetypeVariants + m_Scenario.PercentageOfSegmentation > 0.0f;

                for (var archetypeIndex = 0; archetypeIndex < archetypesToCreate; ++archetypeIndex)
                {
                    using (k_GenerateArchetypeBatchMarker.Auto())
                    {
                        // Just add the remainder entities to the first archetype variant
                        var entitiesToAdd = archetypeIndex == 0 ? entitiesPerArchetype + remainder : entitiesPerArchetype;

                        var archetype = requiresVariants
                            ? entityManager.CreateArchetype(GetNextArchetypeVariant(archetypesToCreate, k_BasicArchetype))
                            : entityManager.CreateArchetype(k_BasicArchetype);

                        var createdEntities = entityManager.CreateEntity(archetype, entitiesToAdd, entityManager.World.UpdateAllocator.ToAllocator);

                        using (k_AppendToAllEntitiesMarker.Auto())
                        {
                            var copyJob = new CopyEntities
                            {
                                Source = createdEntities,
                                Destination = allEntities,
                                Offset = entityIndex
                            };
                            copyJob.Run();
                        }

                        createdEntities.Dispose();
                    }
                }

                if (m_Scenario.SegmentsCount > 1)
                {
                    // TODO: This is super slow
                    using (k_ApplySegmentationMarker.Auto())
                    {
                        var setFragmentIdJob = new SetFragmentId
                        {
                            Entities = allEntities,
                            TransactionManager = entityManager.BeginExclusiveEntityTransaction(),
                            SegmentsCount = m_Scenario.SegmentsCount
                        };

                        entityManager.ExclusiveEntityTransactionDependency = setFragmentIdJob.Schedule();
                        entityManager.EndExclusiveEntityTransaction();
                    }
                }

                entityIndex.Dispose();

                using (k_AssignEntityGuid.Auto())
                {
                    var buffer = new EntityCommandBuffer(entityManager.World.UpdateAllocator.ToAllocator);
                    new AssignEntityGuid
                    {
                        Entities = allEntities,
                        CommandBuffer = buffer.AsParallelWriter(),
                    }.Schedule(allEntities.Length, 128).Complete();
                    buffer.Playback(entityManager);
                    buffer.Dispose();
                }

                return allEntities;
            }
        }

        ComponentType[] GetNextArchetypeVariant(int maximumVariantsCount, params ComponentType[] componentTypes)
        {
            using (k_GenerateArchetypeMarker.Auto())
            {
                m_ArchetypeSequenceNumber %= math.clamp(maximumVariantsCount, 1, k_ArchetypeVariants.Length);
                var variantTypes = k_ArchetypeVariants[m_ArchetypeSequenceNumber++];

                var originalCount = componentTypes.Length;
                var variantComponentsCount = variantTypes.Length;
                var result = new ComponentType[originalCount + variantComponentsCount];
                Array.Copy(componentTypes, 0, result, 0, originalCount);
                Array.Copy(variantTypes, 0, result, originalCount, variantComponentsCount);

                return result;
            }
        }

        void AssignParents(NativeArray<Entity> allEntities, EntityManager entityManager)
        {
            using (k_AssignParentsMarker.Auto())
            {
                k_AssignParentsPrepareMarker.Begin();

                // Initialize depth map
                var depthMap = CollectionHelper.CreateNativeArray<UnsafeList<Entity>>(m_Scenario.MaximumDepth,
                                                                                        entityManager.World.UpdateAllocator.ToAllocator,
                                                                                        NativeArrayOptions.UninitializedMemory);

                // Pre-allocate the correct number of spots per depth
                var entityCount = allEntities.Length;
                var depthCount = depthMap.Length;
                var averageEntitiesPerDepth = entityCount / depthCount;
                var numberAtRoot = math.max((int)(averageEntitiesPerDepth * 0.10f), 1);
                var numberAtMaxDepth = m_Scenario.MaximumDepth > 2
                    ? math.max((int)(averageEntitiesPerDepth * 0.75f), 1)
                    : entityCount - numberAtRoot;
                var numberAtOtherDepths = m_Scenario.MaximumDepth > 2
                    ? (entityCount - numberAtRoot - numberAtMaxDepth) / (depthCount - 2)
                    : 0;

                // Correct for division rounding error
                var remainder = entityCount - (numberAtRoot + numberAtMaxDepth + numberAtOtherDepths * (depthCount - 2));
                numberAtMaxDepth += remainder;

                // Pre-allocate and set lengths: can't use capacity because it is set to nearest higher power of 2, starting at 8 (1 = 8, 11 = 16, etc.)
                depthMap[0] = new UnsafeList<Entity>(numberAtRoot, entityManager.World.UpdateAllocator.ToAllocator) { m_length = numberAtRoot };
                depthMap[depthCount - 1] = new UnsafeList<Entity>(numberAtMaxDepth, entityManager.World.UpdateAllocator.ToAllocator) { m_length = numberAtMaxDepth };

                for (var i = 1; i < depthCount - 1; ++i)
                {
                    depthMap[i] = new UnsafeList<Entity>(numberAtOtherDepths, entityManager.World.UpdateAllocator.ToAllocator) { m_length = numberAtOtherDepths };
                }

                var commandBuffer = new EntityCommandBuffer(entityManager.World.UpdateAllocator.ToAllocator);

                k_AssignParentsPrepareMarker.End();

                using (k_ParentingJobMarker.Auto())
                {
                    var parentingJob = new DistributeParents
                    {
                        Entities = allEntities,
                        DepthMap = depthMap,
                        RNG = new Random(m_Scenario.Seed),
                        Commands = commandBuffer
                    };

                    parentingJob.Run();
                }

                using (k_AssignParentsCleanupMarker.Auto())
                {
                    commandBuffer.Playback(entityManager);

                    commandBuffer.Dispose();

                    foreach (var parentList in depthMap)
                    {
                        parentList.Dispose();
                    }

                    depthMap.Dispose();
                }
            }
        }


        static unsafe void UpdateParenting(World world)
        {
            using (k_UpdateParentingMarker.Auto())
            {
                var parentSystem = world.GetOrCreateSystem<ParentSystem>();
                parentSystem.Update(world.Unmanaged);
                world.Unmanaged.ResolveSystemState(parentSystem)->CompleteDependencyInternal();
            }
        }

        [BurstCompile]
        struct SetFragmentId : IJob
        {
            [ReadOnly]
            public NativeArray<Entity> Entities;
            public ExclusiveEntityTransaction TransactionManager;
            public int SegmentsCount;

            public void Execute()
            {
                for (int i = 0, n = Entities.Length; i < n; ++i)
                {
                    TransactionManager.SetSharedComponent(Entities[i], new SegmentId { Value = i % SegmentsCount });
                }
            }
        }

        [BurstCompile]
        struct AssignEntityGuid : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Entity> Entities;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            public void Execute(int index)
                => CommandBuffer.SetComponent(index, Entities[index], new EntityGuid(Entities[index].Index, 0, 0, (uint)index));
        }

        [BurstCompile]
        struct CopyEntities : IJob
        {
            [ReadOnly]
            public NativeArray<Entity> Source;

            [WriteOnly]
            public NativeArray<Entity> Destination;

            public NativeReference<int> Offset;

            public void Execute()
            {
                var destinationIndex = Offset.Value;
                for (var i = 0; i < Source.Length; ++i)
                {
                    Destination[destinationIndex++] = Source[i];
                }
                Offset.Value = destinationIndex;
            }
        }

        [BurstCompile]
        struct DistributeParents : IJob
        {
            [ReadOnly]
            public NativeArray<Entity> Entities;

            // Assumes at least a Depth of 2
            // All depths are pre-allocated and pre-sized
            public NativeArray<UnsafeList<Entity>> DepthMap;

            public EntityCommandBuffer Commands;
            public Random RNG;

            public void Execute()
            {
                var depthCount = DepthMap.Length;

                // Process all entities at root
                var i = 0;
                var parentsAtRoot = DepthMap[0];
                for (;i < parentsAtRoot.Length; ++i)
                {
                    parentsAtRoot[i] = Entities[i];
                }
                DepthMap[0] = parentsAtRoot;

                // Process all non-root depths
                for (var depthIndex = 1; depthIndex < depthCount; ++depthIndex)
                {
                    var currentDepth = DepthMap[depthIndex];

                    for (var d = 0; d < currentDepth.Length; ++d)
                    {
                        var entity = Entities[i++];
                        currentDepth[d] = entity;

                        var parentIndex = RNG.NextInt(DepthMap[depthIndex-1].Length);
                        Commands.AddComponent(entity, new Parent { Value = DepthMap[depthIndex-1][parentIndex] });
                    }

                    DepthMap[depthIndex] = currentDepth;
                }
            }
        }
    }

    public struct ArchetypeMarker1 : IComponentData { }
    public struct ArchetypeMarker2 : IComponentData { }
    public struct ArchetypeMarker3 : IComponentData { }
    public struct ArchetypeMarker4 : IComponentData { }
    public struct SegmentId : ISharedComponentData
    {
        // ReSharper disable once NotAccessedField.Global
        public int Value;
    }
}
