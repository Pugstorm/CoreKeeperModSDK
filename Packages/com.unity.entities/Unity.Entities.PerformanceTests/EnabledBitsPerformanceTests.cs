using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Tests;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    public sealed partial class EnabledBitsPerformanceTests : EntityPerformanceTestFixture
    {
        struct TestData0 : IComponentData, IEnableableComponent {public int Value;}
        struct TestData1 : IComponentData, IEnableableComponent {public int Value;}
        struct TestData2 : IComponentData, IEnableableComponent {public int Value;}
        struct TestData3 : IComponentData, IEnableableComponent {public int Value;}
        struct TestData4 : IComponentData, IEnableableComponent {public int Value;}
        struct TestData5 : IComponentData, IEnableableComponent {public int Value;}
        struct TestData6 : IComponentData, IEnableableComponent {public int Value;}
        struct TestData7 : IComponentData, IEnableableComponent {public int Value;}

        [Test, Performance]
        public unsafe void GetEnabledMask([Values(0, 1, 2)] int allTypeCount, [Values(0, 1, 2)] int noneTypeCount,
            [Values(0, 1, 2)] int anyTypeCount, [Values(0, 1, 2)] int disabledTypeCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(TestData0), typeof(TestData1), typeof(TestData2),
                typeof(TestData3), typeof(TestData4), typeof(TestData5), typeof(TestData6), typeof(TestData7));
            int chunkCount = 1000;
            m_Manager.CreateEntity(archetype, chunkCount * archetype.ChunkCapacity);

            var possibleTypes = new ComponentType[]{typeof(TestData0), typeof(TestData1), typeof(TestData2),
                typeof(TestData3), typeof(TestData4), typeof(TestData5), typeof(TestData6), typeof(TestData7)};
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp);
            int queryTypeCount = 0;

            var allTypes = new FixedList32Bytes<ComponentType>();
            for (int i = 0; i < allTypeCount; ++i)
            {
                allTypes.Add(possibleTypes[queryTypeCount++]);
            }
            queryBuilder.WithAll(ref allTypes);

            var noneTypes = new FixedList32Bytes<ComponentType>();
            for (int i = 0; i < noneTypeCount; ++i)
            {
                noneTypes.Add(possibleTypes[queryTypeCount++]);
            }
            queryBuilder.WithNone(ref noneTypes);

            var anyTypes = new FixedList32Bytes<ComponentType>();
            for (int i = 0; i < anyTypeCount; ++i)
            {
                anyTypes.Add(possibleTypes[queryTypeCount++]);
            }
            queryBuilder.WithAny(ref anyTypes);

            var disabledTypes = new FixedList32Bytes<ComponentType>();
            for (int i = 0; i < disabledTypeCount; ++i)
            {
                disabledTypes.Add(possibleTypes[queryTypeCount++]);
            }
            queryBuilder.WithDisabled(ref disabledTypes);

            using var query = m_Manager.CreateEntityQuery(queryBuilder);

            var chunkCache = query.__impl->GetMatchingChunkCache();
            Assert.AreEqual(chunkCount, chunkCache.Length);
            var chunkList = chunkCache.MatchingChunks->Ptr;
            var matchingArchetype = query._GetImpl()->_QueryData->MatchingArchetypes.Ptr[0];
            v128 mask = default;
            Measure.Method(
                    () =>
                    {
                        for (int i = 0; i < chunkCount; ++i)
                        {
                            ChunkIterationUtility.GetEnabledMask(chunkList[i], matchingArchetype, out mask);
                        }
                    })
                .WarmupCount(1)
                .MeasurementCount(1000)
                .SampleGroup(new SampleGroup($"GetEnabledMask SIMD (Chunks={chunkCount} All={allTypeCount} Any={anyTypeCount} None={noneTypeCount} Disabled={disabledTypeCount})", SampleUnit.Microsecond))
                .Run();
            Measure.Method(
                    () =>
                    {
                        for (int i = 0; i < chunkCount; ++i)
                        {
                            ChunkIterationUtility.GetEnabledMaskNoBurstForTests(chunkList[i], matchingArchetype, out mask);
                        }
                    })
                .WarmupCount(1)
                .MeasurementCount(1000)
                .SampleGroup(new SampleGroup($"GetEnabledMask NoBurst (Chunks={chunkCount} All={allTypeCount} Any={anyTypeCount} None={noneTypeCount} Disabled={disabledTypeCount})", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void ShiftRight128_Performance()
        {
            int callsPerSample = 128*100;

            v128 v = default;
            v128 accumulate = default;
            Measure.Method(
                    () =>
                    {
                        for (int i = 0; i < callsPerSample; ++i)
                        {
                            EnabledBitUtility.ShiftRightBurstForTests(ref v, out v128 vOut, i % 128 );
                            accumulate.ULong0 |= vOut.ULong0;
                            accumulate.ULong1 |= vOut.ULong1;
                        }
                    })
                .SetUp(() =>
                {
                    v.ULong0 += 1;
                    v.ULong1 += 1;
                })
                .CleanUp(() => {
                    Assert.AreNotEqual(0, accumulate.ULong0);
                    Assert.AreNotEqual(0, accumulate.ULong1);
                })
                .WarmupCount(1)
                .MeasurementCount(1000)
                .SampleGroup(new SampleGroup($"ShiftRight_Burst_{callsPerSample}x", SampleUnit.Microsecond))
                .Run();
        }

        [BurstCompile(CompileSynchronously = true)]
        partial class DisableAndProcessSystem : SystemBase
        {
            [BurstCompile(CompileSynchronously = true)]
            partial struct DisablerJob : IJobEntity
            {
                [NativeDisableParallelForRestriction] // necessary to allow parallel writes, even safe ones (only to the current entity).
                public ComponentLookup<EcsTestDataEnableable> Lookup;
                void Execute(Entity e)
                {
                    if ((Lookup[e].value % 2) == 0)
                        Lookup.SetComponentEnabled(e, false);
                }
            }

            [BurstCompile(CompileSynchronously = true)]
            partial struct ProcessJob : IJobEntity
            {
                void Execute(ref EcsTestDataEnableable data)
                {
                    data.value = -data.value;
                }
            }

            private ComponentLookup<EcsTestDataEnableable> _lookup;
            private EntityQuery _query;
            protected override void OnCreate()
            {
                _lookup = GetComponentLookup<EcsTestDataEnableable>(false);
                _query = GetEntityQuery(typeof(EcsTestDataEnableable));
            }

            protected override void OnUpdate()
            {
                _lookup.Update(this);
                var jobDisable = new DisablerJob { Lookup = _lookup };
                Dependency = jobDisable.ScheduleParallelByRef(_query, Dependency);
                var jobProcess = new ProcessJob();
                jobProcess.ScheduleParallelByRef(_query, Dependency).Complete();
                Dependency = default;
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        partial class RemoveTagAndProcess : SystemBase
        {
            [BurstCompile(CompileSynchronously = true)]
            partial struct RemoveTagJob : IJobEntity
            {
                public EntityCommandBuffer.ParallelWriter Commands;
                void Execute(Entity e, in EcsTestDataEnableable data)
                {
                    if ((data.value % 2) == 0)
                        // not sure what to pass for a sort key here, but data.value is at least deterministic!
                        Commands.RemoveComponent<EcsTestTag>(data.value, e);
                }
            }

            [BurstCompile(CompileSynchronously = true)]
            partial struct ProcessJob : IJobEntity
            {
                void Execute(Entity e, ref EcsTestDataEnableable data)
                {
                    data.value = -data.value;
                }
            }

            private EntityQuery _queryRemove;
            private EntityQuery _queryProcess;
            protected override void OnCreate()
            {
                _queryRemove = GetEntityQuery(typeof(EcsTestDataEnableable));
                _queryProcess = GetEntityQuery(typeof(EcsTestDataEnableable), typeof(EcsTestTag));
            }
            protected override void OnUpdate()
            {
                using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                var jobRemove = new RemoveTagJob { Commands = ecb.AsParallelWriter() };
                jobRemove.ScheduleParallelByRef(_queryRemove, Dependency).Complete();

                ecb.Playback(EntityManager);

                var jobProcess = new ProcessJob();
                jobProcess.ScheduleParallelByRef(_queryProcess, Dependency).Complete();

                Dependency = default;
            }
        }

        [Test, Performance]
        public unsafe void EnabledBits_Integration_Performance()
        {
            // Create a bunch of entities
            // Run a parallel job over them that disables half of them
            // Run a job that targets the enabled half
            using var entities = CollectionHelper.CreateNativeArray<Entity>(10000, World.UpdateAllocator.ToAllocator);

            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable), typeof(EcsTestTag));

            var sys1 = World.CreateSystemManaged<DisableAndProcessSystem>();
            Measure.Method(
                    () =>
                    {
                        sys1.Update();
                    })
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype1, entities);
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        m_Manager.SetComponentData(entities[i], new EcsTestDataEnableable(i));
                    }
                })
                .CleanUp(() =>
                {
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        bool expectedEnabled = ((i % 2) == 0) ? false : true;
                        Assert.AreEqual(expectedEnabled, m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entities[i]), $"Entity {i} unexpected enabled bit value");
                        int expectedValue = ((i % 2) == 0) ? i : -i;
                        Assert.AreEqual(expectedValue, m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value, $"Entity {i} unexpected final component value");
                    }
                    m_Manager.DestroyEntity(entities);
                })
                .WarmupCount(1)
                .MeasurementCount(10)
                .SampleGroup("DisableAndProcess")
                .Run();

            var sys2 = World.CreateSystemManaged<RemoveTagAndProcess>();
            Measure.Method(
                    () =>
                    {
                        sys2.Update();
                    })
                .SetUp(() =>
                {
                    m_Manager.CreateEntity(archetype2, entities);
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        m_Manager.SetComponentData(entities[i], new EcsTestDataEnableable(i));
                    }
                })
                .CleanUp(() =>
                {
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        bool expectedHasTag = ((i % 2) == 0) ? false : true;
                        Assert.AreEqual(expectedHasTag, m_Manager.HasComponent<EcsTestTag>(entities[i]), $"Entity {i} unexpected has tag");
                        int expectedValue = ((i % 2) == 0) ? i : -i;
                        Assert.AreEqual(expectedValue, m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value, $"Entity {i} unexpected final component value");
                    }
                    m_Manager.DestroyEntity(entities);
                })
                .WarmupCount(1)
                .MeasurementCount(10)
                .SampleGroup("RemoveTagAndProcess")
                .Run();
        }
    }
}
