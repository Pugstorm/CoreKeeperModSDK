// TODO: convert tests over to use/compare with foreach and IJobEntity
// https://jira.unity3d.com/browse/DOTS-6252
#if FALSE
using System;
using System.Linq;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Transforms;

namespace Unity.Entities.PerformanceTests
{
    [TestFixture]
    public unsafe partial class ForEachISystemPerformanceTests : ECSTestsFixture
    {
        public enum ScheduleType
        {
            Run,
            Schedule,
            ScheduleParallel
        }

        public enum SystemType
        {
            StructSystemType,
            ClassSystemType
        }

        // Test and compare the performance of Entities.ForEach in ISystem to SystemBase
        [Test, Performance]
        [Category("Performance")]
        public void ISystem_EFE_Performance([Values(10, 1000)] int jobsPerUpdate,
            [Values] ScheduleType scheduleType,
            [Values] SystemType systemType)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestFloatData), typeof(EcsTestFloatData2), typeof(EcsTestFloatData3));
            var entityCount = 1000;

            using (var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator))
            {
                m_Manager.CreateEntity(archetype, entities);

                var group = World.CreateSystem<BenchmarkSystemGroup>();
                group.SystemType = systemType;
                group.IterationCount = 1;
                group.JobsPerUpdate = jobsPerUpdate;
                group.ScheduleType = scheduleType;

                Measure.Method(
                        () =>
                        {
                            group.Update();
                        })
                    .WarmupCount(1)
                    .MeasurementCount(100)
                    .Run();
            }
        }

        public partial class ForEachBenchmarkSystemGroup : ComponentSystemGroup
        {
            public int IterationCount;
            public int LoopsPerSystemUpdate;

            public ForEachType ForEachType { get; set; }

            protected override void OnStartRunning()
            {
                base.OnStartRunning();

                for (int i = 0; i != IterationCount; i++)
                {
                    switch (ForEachType)
                    {
                        case ForEachType.Entities:
                        {
                            var res = World.CreateSystem<EntitiesForEachSystem>();
                            res.Struct.LoopsPerSystem = LoopsPerSystemUpdate;
                            AddSystemToUpdateList(res);
                            break;
                        }
                        case ForEachType.Idiomatic:
                        {
                            var res = World.GetOrCreateSystem<IdiomaticForEachSystem>();
                            res.Struct.LoopsPerSystem = LoopsPerSystemUpdate;
                            AddSystemToUpdateList(res);
                            break;
                        }
                    }
                }
            }
        }

        public partial class BenchmarkSystemGroup : ComponentSystemGroup
        {
            // Assign values to these fields post-OnCreate() based on test case settings, before the first Update()
            public int IterationCount;
            public int JobsPerUpdate;
            public SystemType SystemType;
            public ScheduleType ScheduleType;
            public ForEachType ForEachType { get; set; }

            protected override void OnStartRunning()
            {
                base.OnStartRunning();

                for (int i = 0; i != IterationCount; i++)
                {
                    switch (SystemType)
                    {
                        case SystemType.StructSystemType:
                        {
                            var res = World.CreateSystem<StructTestSystem>();
                            res.Struct.LoopsPerSystem = JobsPerUpdate;
                            res.Struct.ScheduleType = ScheduleType;
                            AddSystemToUpdateList(res);
                            break;
                        }
                        default:
                        {
                            var res = World.GetOrCreateSystem<ClassTestSystem>();
                            res.LoopsPerSystem = JobsPerUpdate;
                            res.ScheduleType = ScheduleType;
                            AddSystemToUpdateList(res);
                            break;
                        }
                    }
                }
            }
        }

        partial class ClassTestSystem : SystemBase
        {
            public int LoopsPerSystem;
            public ScheduleType ScheduleType;

            protected override void OnUpdate()
            {
                switch (ScheduleType)
                {
                    case ScheduleType.Run:
                        for (var i = 0; i < LoopsPerSystem; i++)
                        {
                            Entities.ForEach((Entity entity, ref EcsTestFloatData d1, in EcsTestFloatData2 d2, in EcsTestFloatData3 d3) => { d1.Value = d2.Value0 + d3.Value0; })
                                .Run();
                        }
                        break;
                    case ScheduleType.Schedule:
                        for (var i = 0; i < LoopsPerSystem; i++)
                        {
                            Entities.ForEach((Entity entity, ref EcsTestFloatData d1, in EcsTestFloatData2 d2, in EcsTestFloatData3 d3) => { d1.Value = d2.Value0 + d3.Value0; })
                                .Schedule();
                        }
                        break;
                    case ScheduleType.ScheduleParallel:
                        for (var i = 0; i < LoopsPerSystem; i++)
                        {
                            Entities.ForEach((Entity entity, ref EcsTestFloatData d1, in EcsTestFloatData2 d2, in EcsTestFloatData3 d3) => { d1.Value = d2.Value0 + d3.Value0; })
                                .ScheduleParallel();
                        }
                        break;
                }
                Dependency.Complete();
            }
        }

        public enum ForEachType
        {
            Idiomatic,
            Entities
        }

        [Test, Performance]
        [Category("Performance")]
        public void IdiomaticForEach_Versus_EntitiesForEach([Values(10, 1000, 10000)] int loopsPerUpdate, [Values] ForEachType forEachType)
        {
            var archetype = m_Manager.CreateArchetype(RotateAspect.RequiredComponents.Append(ComponentType.ReadWrite<SpeedModifier>()).ToArray());
            const int entityCount = 1000;

            using (var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator))
            {
                m_Manager.CreateEntity(archetype, entities);

                var group = World.CreateSystem<ForEachBenchmarkSystemGroup>();
                group.ForEachType = forEachType;
                group.IterationCount = 1;
                group.LoopsPerSystemUpdate = loopsPerUpdate;

                Measure.Method(
                        () =>
                        {
                            group.Update();
                        })
                    .WarmupCount(1)
                    .MeasurementCount(100)
                    .Run();
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        partial struct IdiomaticForEachSystem : ISystem
        {
            public int LoopsPerSystem { get; set; }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                for (int i = 0; i < LoopsPerSystem; i++)
                {
                    foreach (var (rotateAspect, speedModifierRef) in SystemAPI.Query<RotateAspect, RefRO<SpeedModifier>>())
                        rotateAspect.Rotate(state.Time.DeltaTime, speedModifierRef.ValueRO.Value);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        partial struct EntitiesForEachSystem: ISystem
        {
            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                for (int i = 0; i < LoopsPerSystem; i++)
                {
                    var deltaTime = state.Time.DeltaTime;

                    state.Entities.ForEach((RotateAspect rotateAspect, in SpeedModifier speedModifier) =>
                    {
                        rotateAspect.Rotate(time: deltaTime, speedModifier.Value);
                    }).Run();
                }
            }

            public int LoopsPerSystem { get; set; }
        }

        [BurstCompile(CompileSynchronously = true)]
        partial struct StructTestSystem : ISystem
        {
            public int LoopsPerSystem;
            public ScheduleType ScheduleType;

            [BurstDiscard]
            static void CheckRunningBurst()
            {
                throw new ArgumentException("Not running burst");
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                CheckRunningBurst();

                switch (ScheduleType)
                {
                    case ScheduleType.Run:
                        for (var i = 0; i < LoopsPerSystem; i++)
                        {
                            state.Entities.ForEach((Entity entity, ref EcsTestFloatData d1, in EcsTestFloatData2 d2, in EcsTestFloatData3 d3) => { d1.Value = d2.Value0 + d3.Value0; })
                                .Run();
                        }
                        break;
                    case ScheduleType.Schedule:
                        for (var i = 0; i < LoopsPerSystem; i++)
                        {
                            state.Entities.ForEach((Entity entity, ref EcsTestFloatData d1, in EcsTestFloatData2 d2, in EcsTestFloatData3 d3) => { d1.Value = d2.Value0 + d3.Value0; })
                                .Schedule();
                        }
                        break;
                    case ScheduleType.ScheduleParallel:
                        for (var i = 0; i < LoopsPerSystem; i++)
                        {
                            state.Entities.ForEach((Entity entity, ref EcsTestFloatData d1, in EcsTestFloatData2 d2, in EcsTestFloatData3 d3) => { d1.Value = d2.Value0 + d3.Value0; })
                                .ScheduleParallel();
                        }
                        break;
                }
                state.Dependency.Complete();
            }
        }
    }

    public struct RotationSpeed : IComponentData
    {
        public float RadiansPerSecond;
    }
    public struct SpeedModifier : IComponentData
    {
        public float Value;
    }
    public readonly partial struct RotateAspect : IAspect
    {
        readonly RefRW<Rotation> Rotation;

        public void Rotate(float time, float speedModifier) =>
            Rotation.ValueRW.Value =
                math.mul(
                    math.normalize(Rotation.ValueRO.Value),
                    quaternion.AxisAngle(math.up(), time * speedModifier));
    }
}
#endif
