using System;
using Unity.Entities.Tests;
using Unity.PerformanceTesting;
using Unity.Collections;
using NUnit.Framework;
using Unity.Burst;
using Unity.Jobs;

namespace Unity.Entities.PerformanceTests
{
    public partial class EntitiesTestsFixture : ECSTestsFixture
    {
        public enum ScheduleType
        {
            Run,
            Schedule,
            ScheduleParallel
        }

        protected partial class LambdaJobsTestComponentSystem : SystemBase
        {
            protected override void OnUpdate()
            {
            }

            public void WriteToOneComponentLambda(ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((Entity entity, ref EcsTestFloatData d1) =>
                        {
                            d1.Value++;
                        }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((Entity entity, ref EcsTestFloatData d1) =>
                        {
                            d1.Value++;
                        }).Schedule();
                        CompleteDependency();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((Entity entity, ref EcsTestFloatData d1) =>
                        {
                            d1.Value++;
                        }).ScheduleParallel();
                        CompleteDependency();
                        break;
                }
            }

            public void UseEntityQueryIndex(ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((Entity entity, int entityInQueryIndex, ref EcsTestFloatData d1) =>
                        {
                            d1.Value++;
                        }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((Entity entity, int entityInQueryIndex, ref EcsTestFloatData d1) =>
                        {
                            d1.Value++;
                        }).Schedule();
                        CompleteDependency();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((Entity entity, int entityInQueryIndex, ref EcsTestFloatData d1) =>
                        {
                            d1.Value++;
                        }).ScheduleParallel();
                        CompleteDependency();
                        break;
                }
            }

            public void ReadOneWriteOneComponentLambda(ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((Entity entity, ref EcsTestFloatData d1, in EcsTestFloatData2 d2) =>
                        {
                            d1.Value = d2.Value0 * 2;
                        }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((Entity entity, ref EcsTestFloatData d1, in EcsTestFloatData2 d2) =>
                        {
                            d1.Value = d2.Value0 * 2;
                        }).Schedule();
                        CompleteDependency();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((Entity entity, ref EcsTestFloatData d1, in EcsTestFloatData2 d2) =>
                        {
                            d1.Value = d2.Value0 * 2;
                        }).ScheduleParallel();
                        CompleteDependency();
                        break;
                }
            }

            public void WriteThreeComponentLambda(ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.ForEach((Entity entity, ref EcsTestFloatData d1, ref EcsTestFloatData2 d2, ref EcsTestFloatData3 d3) =>
                        {
                            d1.Value++;
                            d2.Value0++;
                            d3.Value0++;
                        }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.ForEach((Entity entity, ref EcsTestFloatData d1, ref EcsTestFloatData2 d2, ref EcsTestFloatData3 d3) =>
                        {
                            d1.Value++;
                            d2.Value0++;
                            d3.Value0++;
                        }).Schedule();
                        CompleteDependency();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.ForEach((Entity entity, ref EcsTestFloatData d1, ref EcsTestFloatData2 d2, ref EcsTestFloatData3 d3) =>
                        {
                            d1.Value++;
                            d2.Value0++;
                            d3.Value0++;
                        }).ScheduleParallel();
                        CompleteDependency();
                        break;
                }
            }

            public void SimpleLambda()
            {
                Entities.ForEach((Entity entity, ref EcsTestFloatData d1, ref EcsTestFloatData2 d2, ref EcsTestFloatData3 d3) =>
                {
                    d1.Value = d2.Value0 + d3.Value0;
                }).Run();
            }

            public unsafe void SimpleLambdaWithPointerCapture()
            {
                byte* innerRawPtr = (byte*)IntPtr.Zero;
                Entities
                    .WithNativeDisableUnsafePtrRestriction(innerRawPtr)
                    .ForEach((Entity entity, ref EcsTestFloatData d1, ref EcsTestFloatData2 d2, ref EcsTestFloatData3 d3) =>
                    {
                        if (innerRawPtr == null)
                            d1.Value = d2.Value0 + d3.Value0;
                    }).Run();
            }

            public void StructuralChangesWithECB(EntityManager manager)
            {
                {
                    var ecb = new EntityCommandBuffer(Allocator.Temp, PlaybackPolicy.SinglePlayback);
                    Entities
                        .ForEach((Entity entity) =>
                        {
                            ecb.AddComponent<EcsTestFloatData>(entity);
                        }).Run();
                    ecb.Playback(manager);
                }
                {
                    var ecb = new EntityCommandBuffer(Allocator.Temp, PlaybackPolicy.SinglePlayback);
                    Entities
                        .ForEach((Entity entity) =>
                    {
                        ecb.RemoveComponent<EcsTestFloatData>(entity);
                    }).Run();
                    ecb.Playback(manager);
                }
            }

            public void StructuralChangesInLambda(EntityManager manager)
            {
                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity entity) =>
                    {
                        manager.AddComponent<EcsTestFloatData>(entity);
                    }).Run();
                Entities
                    .WithStructuralChanges()
                    .ForEach((Entity entity) =>
                    {
                        manager.RemoveComponent<EcsTestFloatData>(entity);
                    }).Run();
            }
        }

        protected LambdaJobsTestComponentSystem LambdaJobsTestSystem => World.GetOrCreateSystemManaged<LambdaJobsTestComponentSystem>();
    }

    [Category("Performance")]
    partial class LambdaJobsPerformanceTests : EntitiesTestsFixture
    {
        [Test, Performance]
        [Category("Performance")]
        public void LambdaJobsForEach_Performance_UseEntityInQueryIndex(
            [Values(ScheduleType.Run, ScheduleType.Schedule, ScheduleType.ScheduleParallel)] ScheduleType scheduleType, [Values(1, 1000, 100000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestFloatData));
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator))
            {
                m_Manager.CreateEntity(archetype, entities);
                Measure.Method(() => { LambdaJobsTestSystem.UseEntityQueryIndex(scheduleType); })
                    .SampleGroup(new SampleGroup("UseEntityInQueryIndex", SampleUnit.Microsecond))
                    .WarmupCount(5).MeasurementCount(100).Run();
            }
        }

        [Test, Performance]
        [Category("Performance")]
        public void LambdaJobsForEach_Performance_WriteToOneComponentLambda(
            [Values(ScheduleType.Run, ScheduleType.Schedule, ScheduleType.ScheduleParallel)] ScheduleType scheduleType, [Values(1, 1000, 100000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestFloatData));
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator))
            {
                m_Manager.CreateEntity(archetype, entities);
                Measure.Method(() => { LambdaJobsTestSystem.WriteToOneComponentLambda(scheduleType); })
                    .SampleGroup(new SampleGroup("WriteToOneComponentLambda", SampleUnit.Microsecond))
                    .WarmupCount(5).MeasurementCount(100).Run();
            }
        }

        [Test, Performance]
        [Category("Performance")]
        public void LambdaJobsForEach_Performance_ReadOneWriteOneComponentLambda(
            [Values(ScheduleType.Run, ScheduleType.Schedule, ScheduleType.ScheduleParallel)] ScheduleType scheduleType, [Values(1, 1000, 100000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestFloatData), typeof(EcsTestFloatData2));
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator))
            {
                m_Manager.CreateEntity(archetype, entities);
                Measure.Method(() => { LambdaJobsTestSystem.ReadOneWriteOneComponentLambda(scheduleType); })
                    .SampleGroup(new SampleGroup("ReadOneWriteOneComponentLambda", SampleUnit.Microsecond))
                    .WarmupCount(5).MeasurementCount(100).Run();
            }
        }

        [Test, Performance]
        [Category("Performance")]
        public void LambdaJobsForEach_Performance_WriteThreeComponentLambda(
            [Values(ScheduleType.Run, ScheduleType.Schedule, ScheduleType.ScheduleParallel)] ScheduleType scheduleType, [Values(1, 1000, 100000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestFloatData), typeof(EcsTestFloatData2), typeof(EcsTestFloatData3));
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator))
            {
                m_Manager.CreateEntity(archetype, entities);
                Measure.Method(() => { LambdaJobsTestSystem.WriteThreeComponentLambda(scheduleType); })
                    .SampleGroup(new SampleGroup("WriteThreeComponentLambda", SampleUnit.Microsecond))
                    .WarmupCount(5).MeasurementCount(100).Run();
            }
        }

        // Tests the performance of the LambdaJobs ForEach & ForEach on ReadOnly components
        // Also tests capturing a pointer (could affect bursted performance if NoAlias not applied correctly)
        [Test, Performance]
        [Category("Performance")]
        public void LambdaJobsForEach_Performance_Simple([Values(true, false)] bool withPointerCapture)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestFloatData), typeof(EcsTestFloatData2), typeof(EcsTestFloatData3));
            var entityCount = 1000000;
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator))
            {
                m_Manager.CreateEntity(archetype, entities);

                if (withPointerCapture)
                {
                    Measure.Method(() => { LambdaJobsTestSystem.SimpleLambdaWithPointerCapture(); })
                        .WarmupCount(5)
                        .MeasurementCount(100)
                        .SampleGroup(new SampleGroup("LambdaJobsForEach_Performance_WithPointerCapture", SampleUnit.Microsecond))
                        .Run();
                }
                else
                {
                    Measure.Method(() => { LambdaJobsTestSystem.SimpleLambda(); })
                        .WarmupCount(5)
                        .MeasurementCount(100)
                        .SampleGroup(new SampleGroup("LambdaJobsForEach_Performance_Simple", SampleUnit.Microsecond))
                        .Run();
                }
            }
        }

        [Test, Performance]
        [Category("Performance")]
        public void LambdaJobsForEachStructuralChanges_Performance_InLambda_vs_WithECB([Values(1, 1000, 10000)] int entityCount, [Values(true, false)] bool withECB)
        {
            var archetype = new EntityArchetype();
            archetype = m_Manager.CreateArchetype();
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator))
            {
                m_Manager.CreateEntity(archetype, entities);
                if (withECB)
                {
                    Measure.Method(() =>
                    {
                        LambdaJobsTestSystem.StructuralChangesWithECB(m_Manager);
                    })
                        .SampleGroup(new SampleGroup("StructuralChangesWithECB", SampleUnit.Microsecond))
                        .WarmupCount(1)
                        .MeasurementCount(100)
                        .Run();
                }
                else
                {
                    Measure.Method(() =>
                    {
                        LambdaJobsTestSystem.StructuralChangesInLambda(m_Manager);
                    })
                        .SampleGroup(new SampleGroup("StructuralChangesInLambda", SampleUnit.Microsecond))
                        .WarmupCount(1)
                        .MeasurementCount(100)
                        .Run();
                }
            }
        }

        protected partial class TestSystem3_EB : SystemBase
        {
            public int Count;
            protected override void OnUpdate()
            {
                int count = 0;
                Entities
                    .ForEach((Entity entity, ref EcsTestData d1, ref EcsTestData2 d2 , ref EcsTestDataEnableable3 d3) =>
                    {
                        count += d1.value + d2.value0 + d3.value0;
                    }).Run();
                Count = count;
            }
        }
        protected partial class TestSystem3_EB_RO : SystemBase
        {
            public int Count;
            protected override void OnUpdate()
            {
                int count = 0;
                Entities
                    .ForEach((Entity entity, in EcsTestData d1, in EcsTestData2 d2, in EcsTestDataEnableable3 d3) =>
                    {
                        count += d1.value + d2.value0 + d3.value0;
                    }).Run();
                Count = count;
            }
        }

        [Test, Performance]
        [Category("Performance")]
        public void ForEach_Overhead_EnabledBits([Values(0,1,2,4,8,16,32,64,128,256,512)] int disableEveryNth)
        {
            EntityArchetype archetype = m_Manager.CreateArchetype(typeof(EcsTestData),
                typeof(EcsTestData2), typeof(EcsTestDataEnableable3));

            int entityCount = 100000;
            using var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator);
            m_Manager.CreateEntity(archetype, entities);
            int expectedCount = 0;
            if (disableEveryNth > 0)
            {
                for (int i = 0; i < entityCount; ++i)
                {
                    m_Manager.SetComponentData(entities[i], new EcsTestData(i));
                    m_Manager.SetComponentData(entities[i], new EcsTestData2(i));
                    m_Manager.SetComponentData(entities[i], new EcsTestDataEnableable3(i));
                    if ((i % disableEveryNth) == 0)
                        m_Manager.SetComponentEnabled<EcsTestDataEnableable3>(entities[i], false);
                    else
                        expectedCount += 3 * i;
                }
            }

            var sys3 = World.GetOrCreateSystemManaged<TestSystem3_EB>();
            Measure.Method(() =>
                {
                    sys3.Update();
                })
                .CleanUp(() => Assert.AreEqual(expectedCount, sys3.Count))
                .WarmupCount(10)
                .MeasurementCount(500)
                .SampleGroup(new SampleGroup("RW", SampleUnit.Microsecond))
                .Run();

            var sys3RO = World.GetOrCreateSystemManaged<TestSystem3_EB_RO>();
            Measure.Method(() =>
                {
                    sys3RO.Update();
                })
                .CleanUp(() => Assert.AreEqual(expectedCount, sys3RO.Count))
                .WarmupCount(10)
                .MeasurementCount(500)
                .SampleGroup(new SampleGroup("RO", SampleUnit.Microsecond))
                .Run();
        }

        protected partial class TestSystem4_EB : SystemBase
        {
            [BurstCompile(CompileSynchronously = true)]
            partial struct TestJob : IJobEntity
            {
                public NativeReference<int> Count;
                void Execute(Entity entity, ref EcsTestData d1, ref EcsTestData2 d2, ref EcsTestDataEnableable3 d3)
                {
                    Count.Value += d1.value + d2.value0 + d3.value0;
                }
            }

            public int Count;
            protected override void OnUpdate()
            {
                using var countRef = new NativeReference<int>(World.UpdateAllocator.ToAllocator);
                var job = new TestJob { Count = countRef };
                job.Run();
                Count = countRef.Value;
            }
        }
        protected partial class TestSystem4_EB_RO : SystemBase
        {
            [BurstCompile(CompileSynchronously = true)]
            partial struct TestJob : IJobEntity
            {
                public NativeReference<int> Count;
                void Execute(Entity entity, in EcsTestData d1, in EcsTestData2 d2, in EcsTestDataEnableable3 d3)
                {
                    Count.Value += d1.value + d2.value0 + d3.value0;
                }
            }

            public int Count;
            protected override void OnUpdate()
            {
                using var countRef = new NativeReference<int>(World.UpdateAllocator.ToAllocator);
                var job = new TestJob { Count = countRef };
                job.Run();
                Count = countRef.Value;
            }
        }
        [Test, Performance]
        [Category("Performance")]
        public void IJobEntity_Overhead_EnabledBits([Values(0,1,2,4,8,16,32,64,128,256,512)] int disableEveryNth)
        {
            EntityArchetype archetype = m_Manager.CreateArchetype(typeof(EcsTestData),
                typeof(EcsTestData2), typeof(EcsTestDataEnableable3));

            int entityCount = 100000;
            using var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator);
            m_Manager.CreateEntity(archetype, entities);
            int expectedCount = 0;
            if (disableEveryNth > 0)
            {
                for (int i = 0; i < entityCount; ++i)
                {
                    m_Manager.SetComponentData(entities[i], new EcsTestData(i));
                    m_Manager.SetComponentData(entities[i], new EcsTestData2(i));
                    m_Manager.SetComponentData(entities[i], new EcsTestDataEnableable3(i));
                    if ((i % disableEveryNth) == 0)
                        m_Manager.SetComponentEnabled<EcsTestDataEnableable3>(entities[i], false);
                    else
                        expectedCount += 3 * i;
                }
            }

            var sys4 = World.GetOrCreateSystemManaged<TestSystem4_EB>();
            Measure.Method(() =>
                {
                    sys4.Update();
                })
                .CleanUp(() => Assert.AreEqual(expectedCount, sys4.Count))
                .WarmupCount(10)
                .MeasurementCount(500)
                .SampleGroup(new SampleGroup("RW", SampleUnit.Microsecond))
                .Run();

            var sys4RO = World.GetOrCreateSystemManaged<TestSystem4_EB_RO>();
            Measure.Method(() =>
                {
                    sys4RO.Update();
                })
                .CleanUp(() => Assert.AreEqual(expectedCount, sys4RO.Count))
                .WarmupCount(10)
                .MeasurementCount(500)
                .SampleGroup(new SampleGroup("RO", SampleUnit.Microsecond))
                .Run();
        }
    }
}
