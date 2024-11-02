using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using Unity.Entities.Tests;
using Unity.Jobs;
using Unity.Profiling;
using static Unity.Entities.PerformanceTests.PerformanceTestHelpers;

namespace Unity.Entities.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    public partial class EntityQueryIncrementalCachingPerformanceTests : EntityPerformanceTestFixture
    {
        public enum EntityQueryFilterMode
        {
            FilterDisabled,
            FilterEnabledAllPass,
            FilterEnabledNonePass,
        }

        NativeArray<EntityQuery> CreateUniqueQueries(int size)
        {
            var queries = CollectionHelper.CreateNativeArray<EntityQuery>(size, World.UpdateAllocator.ToAllocator);

            var typeCount = CollectionHelper.Log2Ceil(size);
            for (int i = 0; i < size; i++)
            {
                var typeList = new List<ComponentType>();
                for (int typeIndex = 0; typeIndex < typeCount; typeIndex++)
                {
                    if ((i & (1 << typeIndex)) != 0)
                        typeList.Add(TestTags.TagTypes[typeIndex]);
                }

                typeList.Add(typeof(EcsTestData));
                typeList.Add(typeof(EcsTestSharedComp));

                var types = typeList.ToArray();
                queries[i] = m_Manager.CreateEntityQuery(types);
            }

            return queries;
        }

        [Test, Performance]
        public void RebuildMatchingChunkCache_EmptyArchetypes([Values(100, 1000, 10000)] int archetypeCount)
        {
            using var archetypes = CreateUniqueArchetypes(m_Manager, archetypeCount, World.UpdateAllocator.ToAllocator,
                typeof(EcsTestData));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            Measure.Method(() =>
                {
                    query.ForceUpdateCache();
                })
                .SetUp(() => query.InvalidateCache())
                .MeasurementCount(100)
                .WarmupCount(1)
                .Run();
        }

        [Test, Performance]
        public void CreateDestroyEntity_Scaling([Values(10, 100)] int archetypeCount, [Values(10, 100)] int queryCount)
        {
            const int kInitialEntityCount = 5000000;
            const int kCreateDestroyEntityCount = 200000;

            using(var archetypes = CreateUniqueArchetypes(m_Manager, archetypeCount, World.UpdateAllocator.ToAllocator,typeof(EcsTestData), typeof(EcsTestSharedComp)))
            using(var queries = CreateUniqueQueries(queryCount))
            {
                for (int archetypeIndex = 0; archetypeIndex < archetypeCount; ++archetypeIndex)
                {
                    m_Manager.CreateEntity(archetypes[archetypeIndex], kInitialEntityCount / archetypeCount);
                }

                var basicArchetype = m_Manager.CreateArchetype(typeof(EcsTestData));

                var createEntities = default(NativeArray<Entity>);
                Measure.Method(() =>
                    {
                        createEntities = m_Manager.CreateEntity(basicArchetype, kCreateDestroyEntityCount,
                            World.UpdateAllocator.ToAllocator);
                    })
                    .CleanUp(() =>
                    {
                        m_Manager.DestroyEntity(createEntities);
                        createEntities.Dispose();
                    })
                    .MeasurementCount(100)
                    .WarmupCount(1)
                    .SampleGroup("CreateEntities")
                    .Run();

                var destroyEntities = default(NativeArray<Entity>);
                Measure.Method(() => { m_Manager.DestroyEntity(destroyEntities); })
                    .SetUp(() =>
                    {
                        destroyEntities = m_Manager.CreateEntity(basicArchetype, kCreateDestroyEntityCount,
                            World.UpdateAllocator.ToAllocator);
                    })
                    .CleanUp(() => { destroyEntities.Dispose(); })
                    .WarmupCount(1)
                    .MeasurementCount(100)
                    .SampleGroup("DestroyEntities")
                    .Run();
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct TestChunkJob : IJobChunk
        {
            public ComponentTypeHandle<EcsTestData> EcsTestDataRW;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var data = chunk.GetNativeArray(ref EcsTestDataRW);
                data[0] = new EcsTestData {value = 10};
            }
        }

        void IJobChunk_Performance_Scheduling(int entityCount, int archetypeCount, EntityQueryFilterMode filterMode)
        {
            using (var archetypes = CreateUniqueArchetypes(m_Manager, archetypeCount,
                World.UpdateAllocator.ToAllocator, typeof(EcsTestData), typeof(EcsTestSharedComp)))
            using (var basicQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData),
                typeof(EcsTestSharedComp)))
            {
                for (int archetypeIndex = 0; archetypeIndex < archetypeCount; ++archetypeIndex)
                {
                    m_Manager.CreateEntity(archetypes[archetypeIndex], entityCount / archetypeCount);
                }
                if (filterMode == EntityQueryFilterMode.FilterEnabledAllPass)
                    basicQuery.SetSharedComponentFilterManaged(default(EcsTestSharedComp));
                else if (filterMode == EntityQueryFilterMode.FilterEnabledNonePass)
                    basicQuery.SetSharedComponentFilterManaged(new EcsTestSharedComp(17));
                var handle = default(JobHandle);
                Measure.Method(() =>
                    {
                        handle = new TestChunkJob
                        {
                            EcsTestDataRW = m_Manager.GetComponentTypeHandle<EcsTestData>(false)
                        }.ScheduleParallel(basicQuery, handle);
                    })
                    .CleanUp(() =>
                    {
                        handle.Complete();
                    })
                    .SampleGroup(new SampleGroup("IJobChunk_Scheduling", SampleUnit.Microsecond))
                    .WarmupCount(1)
                    .MeasurementCount(100)
                    .Run();
            }
        }

        [Test, Performance]
        public void IJobChunk_Performance_Scheduling_WithFilter([Values(100, 10000, 5000000)] int entityCount,
            [Values(10, 100)] int archetypeCount)
        {
            IJobChunk_Performance_Scheduling(entityCount, archetypeCount, EntityQueryFilterMode.FilterEnabledAllPass);
        }
        [Test, Performance]
        public void IJobChunk_Performance_Scheduling_WithoutFilter([Values(100, 10000, 5000000)] int entityCount,
            [Values(10, 100)] int archetypeCount)
        {
            IJobChunk_Performance_Scheduling(entityCount, archetypeCount, EntityQueryFilterMode.FilterDisabled);
        }

        void IJobChunk_Performance_Executing(int entityCount, int archetypeCount, EntityQueryFilterMode filterMode)
        {
            using (var archetypes = CreateUniqueArchetypes(m_Manager, archetypeCount, World.UpdateAllocator.ToAllocator, typeof(EcsTestData), typeof(EcsTestSharedComp)))
            using (var basicQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp)))
            {
                for (int archetypeIndex = 0; archetypeIndex < archetypeCount; ++archetypeIndex)
                {
                    m_Manager.CreateEntity(archetypes[archetypeIndex], entityCount / archetypeCount);
                }

                if (filterMode == EntityQueryFilterMode.FilterEnabledAllPass)
                    basicQuery.SetSharedComponentFilterManaged(default(EcsTestSharedComp));
                else if (filterMode == EntityQueryFilterMode.FilterEnabledNonePass)
                    basicQuery.SetSharedComponentFilterManaged(new EcsTestSharedComp(17));

                Measure.Method(() =>
                    {
                        new TestChunkJob
                        {
                            EcsTestDataRW = m_Manager.GetComponentTypeHandle<EcsTestData>(false)
                        }.Run(basicQuery);
                    })
                    .CleanUp(() =>
                    {
                    })
                    .SampleGroup(new SampleGroup("IJobChunk_Executing", SampleUnit.Microsecond))
                    .WarmupCount(1)
                    .MeasurementCount(100)
                    .Run();
            }
        }

        [Test, Performance]
        public void IJobChunk_Performance_Executing_WithFilterPass([Values(100, 10000, 5000000)] int entityCount,
            [Values(10, 100)] int archetypeCount)
        {
            IJobChunk_Performance_Executing(entityCount, archetypeCount, EntityQueryFilterMode.FilterEnabledAllPass);
        }
        [Test, Performance]
        public void IJobChunk_Performance_Executing_WithFilterFail([Values(100, 10000, 5000000)] int entityCount,
            [Values(10, 100)] int archetypeCount)
        {
            IJobChunk_Performance_Executing(entityCount, archetypeCount, EntityQueryFilterMode.FilterEnabledNonePass);
        }
        [Test, Performance]
        public void IJobChunk_Performance_Executing_WithoutFilter([Values(100, 10000, 5000000)] int entityCount,
            [Values(10, 100)] int archetypeCount)
        {
            IJobChunk_Performance_Executing(entityCount, archetypeCount, EntityQueryFilterMode.FilterDisabled);
        }

        partial class ForEachRunWithoutJobs_TestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.ForEach((ref EcsTestData2 data2) => { data2.value1 = data2.value0; }).Run();
            }
        }

        partial class ForEachRunWithoutJobs_CleanupSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.ForEach((ref EcsTestData2 data2) =>
                {
                    Assert.AreEqual(data2.value0, data2.value1);
                    data2.value1 = -1;
                }).Run();
            }
        }

        [Test, Performance]
        public void EntitiesForEach_ManyEmptyArchetypes_Run()
        {
            int archetypeCount = 500;
            using var archetypes = CreateUniqueArchetypes(m_Manager, archetypeCount, World.UpdateAllocator.ToAllocator,
                typeof(EcsTestData2), typeof(EcsTestSharedComp));
            int entityCount = 500;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData2));
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entityCount; ++i)
            {
                m_Manager.SetComponentData(entities[i], new EcsTestData2 { value0 = i, value1 = -1 });
            }

            var testSystem = World.CreateSystemManaged<ForEachRunWithoutJobs_TestSystem>();
            var cleanupSystem = World.CreateSystemManaged<ForEachRunWithoutJobs_CleanupSystem>();
            Measure.Method(() =>
                {
                    testSystem.Update();
                })
                .CleanUp(() =>
                {
                    cleanupSystem.Update();
                })
                .SampleGroup(new SampleGroup("EntitiesForEach_ManyEmptyArchetypes_Run", SampleUnit.Microsecond))
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();
        }
    }
}
