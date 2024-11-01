using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests
{
    partial class SystemSafetyTests_DependenciesAndJobDebugger : ECSTestsFixture
    {
        struct ReadWriteJob : IJobChunk
        {
            public ComponentTypeHandle<EcsTestData> Blah;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
            }
        }

        #region SystemBase

        [UpdateBefore(typeof(CorrectSystem))]
        partial class MisbehavingSystem : SystemBase
        {
            protected override void OnCreate()
            {
                base.OnCreate();
                GetComponentTypeHandle<EcsTestData>(false);
            }

            protected override void OnUpdate()
            {
                //@TODO: EntityManager.GetComponentTypeHandle vs Scheduling dependencies
                var job = new ReadWriteJob {Blah = GetComponentTypeHandle<EcsTestData>(false)};
                job.Schedule(GetEntityQuery(typeof(EcsTestData)), default);
            }
        }

        partial class CorrectSystem : SystemBase
        {
            protected override void OnCreate()
            {
                base.OnCreate();
                GetComponentTypeHandle<EcsTestData>(false);
            }

            protected override void OnUpdate()
            {
                var job = new ReadWriteJob {Blah = GetComponentTypeHandle<EcsTestData>(false)};
                Dependency = job.Schedule(GetEntityQuery(typeof(EcsTestData)), Dependency);
            }
        }

        [UpdateBefore(typeof(CorrectSystem))]
        partial class NestedBrokenSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                World.GetOrCreateSystem<MisbehavingSystem>().Update(World.Unmanaged);
            }
        }

        #endregion

        #region ISystem

        [UpdateBefore(typeof(CorrectISystem))]
        [BurstCompile]
        partial struct MisbehavingISystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.GetComponentTypeHandle<EcsTestData>(false);
            }

            [BurstCompile]

            public void OnUpdate(ref SystemState state)
            {
                var job = new ReadWriteJob {Blah = state.GetComponentTypeHandle<EcsTestData>(false)};
                job.Schedule(state.GetEntityQuery(ComponentType.ReadWrite<EcsTestData>()), default);
            }
        }

        [BurstCompile]
        partial struct CorrectISystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.GetComponentTypeHandle<EcsTestData>(false);
            }
            [BurstCompile]

            public void OnUpdate(ref SystemState state)
            {
                var job = new ReadWriteJob {Blah = state.GetComponentTypeHandle<EcsTestData>(false)};
                state.Dependency = job.Schedule(state.GetEntityQuery(ComponentType.ReadWrite<EcsTestData>()), state.Dependency);
            }
        }

        [UpdateBefore(typeof(CorrectISystem))]
        partial struct NestedBrokenISystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.GetComponentTypeHandle<EcsTestData>(false);
            }
            public void OnUpdate(ref SystemState state)
            {
                state.WorldUnmanaged.GetExistingUnmanagedSystem<MisbehavingISystem>().Update(state.WorldUnmanaged);
            }
        }

        #endregion

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void MissedDependencyMakesActionableErrorMessage([Values]bool iSystem)
        {
            var arch = World.EntityManager.CreateArchetype(typeof(EcsTestData));
            World.EntityManager.CreateEntity(arch, 5000);
            string systemName = "Unity.Entities.Tests.SystemSafetyTests_DependenciesAndJobDebugger+MisbehavingSystem";

            if (iSystem)
            {
                systemName = "Unity.Entities.Tests.SystemSafetyTests_DependenciesAndJobDebugger+MisbehavingISystem";
                var sys1 = World.GetOrCreateSystem<MisbehavingISystem>();
                var sys2 = World.GetOrCreateSystem<CorrectISystem>();

                sys1.Update(World.Unmanaged);
                Assert.Throws<InvalidOperationException>(()=> { sys2.Update(World.Unmanaged); });
            }
            else
            {
                var sys1 = World.GetOrCreateSystem<MisbehavingSystem>();
                var sys2 = World.GetOrCreateSystem<CorrectSystem>();

                sys1.Update(World.Unmanaged);
                Assert.Throws<InvalidOperationException>(()=> { sys2.Update(World.Unmanaged); });
            }
            LogAssert.Expect(LogType.Error,
                $"The system {systemName} writes Unity.Entities.Tests.EcsTestData" +
                " via SystemSafetyTests_DependenciesAndJobDebugger:ReadWriteJob but that type was not assigned to the Dependency property. To ensure correct" +
                " behavior of other systems, the job or a dependency must be assigned to the Dependency property before " +
                "returning from the OnUpdate method.");
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void MissedDependencyFromNestedUpdateMakesActionableErrorMessage([Values]bool iSystem)
        {
            var arch = World.EntityManager.CreateArchetype(typeof(EcsTestData));
            World.EntityManager.CreateEntity(arch, 5000);

            string systemName = "Unity.Entities.Tests.SystemSafetyTests_DependenciesAndJobDebugger+MisbehavingSystem";
            if (iSystem)
            {
                systemName = "Unity.Entities.Tests.SystemSafetyTests_DependenciesAndJobDebugger+MisbehavingISystem";
                World.GetOrCreateSystem<MisbehavingISystem>();
                var sys1 = World.GetOrCreateSystem<NestedBrokenISystem>();
                var sys2 = World.GetOrCreateSystem<CorrectISystem>();

                sys1.Update(World.Unmanaged);
                Assert.Throws<InvalidOperationException>(()=> { sys2.Update(World.Unmanaged); });
            }
            else
            {
                var sys1 = World.GetOrCreateSystem<NestedBrokenSystem>();
                var sys2 = World.GetOrCreateSystem<CorrectSystem>();

                sys1.Update(World.Unmanaged);
                Assert.Throws<InvalidOperationException>(() => { sys2.Update(World.Unmanaged); });
            }

            LogAssert.Expect(LogType.Error,
                $"The system {systemName} writes Unity.Entities.Tests.EcsTestData" +
                " via SystemSafetyTests_DependenciesAndJobDebugger:ReadWriteJob but that type was not assigned to the Dependency property. To ensure correct" +
                " behavior of other systems, the job or a dependency must be assigned to the Dependency property before " +
                "returning from the OnUpdate method.");
            World.Update();
        }

        public partial class ForEachReproSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var lookup = GetComponentLookup<EcsTestData>();

                Entities
                    .WithName("RotationSpeedSystem_ForEach")
                    .ForEach((Entity entity, ref EcsTestData rotation, in EcsTestData2 rotationSpeed) =>
                    {
                        var value = lookup[entity];
                    })
                    .ScheduleParallel();
            }
        }

        [BurstCompile]

        public partial struct IJobEntityReproISystem : ISystem
        {
            public ComponentLookup<EcsTestData> lookup;

            public void OnCreate(ref SystemState state)
            {
                lookup = state.GetComponentLookup<EcsTestData>();
            }
            partial struct TestJob : IJobEntity
            {
                public ComponentLookup<EcsTestData> lookup;

                public void Execute(Entity entity, ref EcsTestData rotation, in EcsTestData2 rotationSpeed)
                {
                    var value = lookup[entity];
                }
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                lookup.Update(ref state);

                var testJob = new TestJob() { lookup = lookup };
                testJob.ScheduleParallel();
            }
        }

        [Ignore("DOTS-6905 Needs re-evaluated after we solve the NullReferenceException issues")]
        [Test]
        public void NoExtraMessageFromForEachSystemRepro([Values]bool iSystem)
        {
            var arch = World.EntityManager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            World.EntityManager.CreateEntity(arch, 5000);
            var g = World.GetOrCreateSystemManaged<SimulationSystemGroup>();
            if (iSystem)
                g.AddSystemToUpdateList(World.GetOrCreateSystem<IJobEntityReproISystem>());
            else
                g.AddSystemToUpdateList(World.GetOrCreateSystem<ForEachReproSystem>());

            World.Update();

            var regex = new System.Text.RegularExpressions.Regex(
                    "^InvalidOperationException: .*(?:_Job)?\\.JobData\\.lookup is not declared \\[ReadOnly\\] in a IJobParallelFor"+
                    " job\\. The container does not support parallel writing\\. Please use a more suitable container type\\.$");

            LogAssert.Expect(LogType.Exception, regex);
        }
    }
}
