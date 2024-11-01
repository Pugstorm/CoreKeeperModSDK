using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Jobs;
using System.Reflection;
using System.Linq;

namespace Unity.Entities.Tests
{
    public partial class WorldTests : ECSTestsCommonBase
    {
        World m_PreviousWorld;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
        }

        [TearDown]
        public override void TearDown()
        {
            World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
            base.TearDown();
        }

        [Test]
        public void ActiveWorldResets()
        {
            var copy = CopyWorlds(alsoClear: true);
            try
            {
                var worldA = new World("WorldA");
                var worldB = new World("WorldB");

                World.DefaultGameObjectInjectionWorld = worldB;

                Assert.AreEqual(worldB, World.DefaultGameObjectInjectionWorld);
                Assert.That(World.All[0], Is.EqualTo(worldA));
                Assert.That(World.All[1], Is.EqualTo(worldB));

                worldB.Dispose();

                Assert.That(worldB.IsCreated, Is.False);
                Assert.That(worldA.IsCreated, Is.True);
                Assert.That(World.DefaultGameObjectInjectionWorld, Is.Null);

                worldA.Dispose();

                Assert.That(World.All.Count, Is.EqualTo(0));
            }
            finally
            {
                ResetWorlds(copy);
            }
        }




        //@TODO: Test for adding a manager from one world to another.

        [Test]
        public unsafe void WorldNoOverlappingChunkSequenceNumbers()
        {
            using(var worldA = new World("WorldA"))
            using(var worldB = new World("WorldB"))
            {
                World.DefaultGameObjectInjectionWorld = worldB;

                worldA.EntityManager.CreateEntity();
                worldB.EntityManager.CreateEntity();

                var worldAChunks = worldA.EntityManager.GetAllChunks();
                var worldBChunks = worldB.EntityManager.GetAllChunks();

                for (int i = 0; i < worldAChunks.Length; i++)
                {
                    var chunkA = worldAChunks[i].m_Chunk;
                    for (int j = 0; j < worldBChunks.Length; j++)
                    {
                        var chunkB = worldBChunks[i].m_Chunk;
                        var sequenceNumberDiff = chunkA.SequenceNumber - chunkB.SequenceNumber;

                        // Any chunk sequence numbers in different worlds should be separated by at least 32 bits
                        Assert.IsTrue(sequenceNumberDiff > 1 << 32);
                    }
                }

                worldAChunks.Dispose();
                worldBChunks.Dispose();
            }
        }

        [Test]
        public unsafe void WorldChunkSequenceNumbersNotReused()
        {
            using (var worldA = new World("WorldA"))
            {
                ulong lastChunkSequenceNumber = 0;
                {
                    var entity = worldA.EntityManager.CreateEntity();
                    var chunk = worldA.EntityManager.GetChunk(entity);
                    lastChunkSequenceNumber = chunk.m_Chunk.SequenceNumber;

                    worldA.EntityManager.DestroyEntity(entity);
                }

                for (int i = 0; i < 1000; i++)
                {
                    var entity = worldA.EntityManager.CreateEntity();
                    var chunk = worldA.EntityManager.GetChunk(entity);
                    var chunkSequenceNumber = chunk.m_Chunk.SequenceNumber;

                    // Sequence numbers should be increasing and should not be reused when chunk is re-used (after zero count)
                    Assert.IsTrue(chunkSequenceNumber > lastChunkSequenceNumber);
                    lastChunkSequenceNumber = chunkSequenceNumber;

                    worldA.EntityManager.DestroyEntity(entity);
                }
            }
        }

        [UpdateInGroup(typeof(SimulationSystemGroup))]
        public partial class UpdateCountSystem : SystemBase
        {
            public double lastUpdateTime;
            public float lastUpdateDeltaTime;
            public int updateCount;

            protected override void OnUpdate()
            {
                lastUpdateTime = SystemAPI.Time.ElapsedTime;
                lastUpdateDeltaTime = SystemAPI.Time.DeltaTime;
                updateCount++;
            }
        }

        [Test]
        public void WorldSimulation_FixedRateUtils_Simple()
        {
            using (var world = new World("World A"))
            {
                var sim = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
                var uc = world.GetOrCreateSystemManaged<UpdateCountSystem>();
                sim.AddSystemToUpdateList(uc);

                // Unity.Core.Hybrid.UpdateWorldTimeSystem
                var timeData = new TimeData();

                void AdvanceWorldTime(float amount)
                {
                    uc.updateCount = 0;
                    timeData = new TimeData(timeData.ElapsedTime + amount, amount);
                    world.SetTime(timeData);
                }

                sim.RateManager = new RateUtils.FixedRateSimpleManager(1.0f);

                // first frame will tick at elapsedTime=0
                AdvanceWorldTime(0.5f);
                world.Update();
                Assert.AreEqual(0.0, uc.lastUpdateTime, 0.001f);
                Assert.AreEqual(1.0, uc.lastUpdateDeltaTime, 0.001f);
                Assert.AreEqual(1, uc.updateCount);

                // Advance elapsed time to 1.6 seconds. the second tick should occur at 1.0
                AdvanceWorldTime(1.1f);
                world.Update();
                Assert.AreEqual(1.0, uc.lastUpdateTime, 0.001f);
                Assert.AreEqual(1.0, uc.lastUpdateDeltaTime, 0.001f);
                Assert.AreEqual(1, uc.updateCount);

                // Advance elapsed time to 1.7 seconds. the third tick should occur at 2.0
                AdvanceWorldTime(0.1f);
                world.Update();
                Assert.AreEqual(2.0, uc.lastUpdateTime, 0.001f);
                Assert.AreEqual(1.0, uc.lastUpdateDeltaTime, 0.001f);
                Assert.AreEqual(1, uc.updateCount);

                // Advance elapsed time to 2.7 seconds. The third tick should occur at 3.0
                AdvanceWorldTime(1.0f);
                world.Update();
                Assert.AreEqual(3.0, uc.lastUpdateTime, 0.001f);
                Assert.AreEqual(1.0, uc.lastUpdateDeltaTime, 0.001f);
                Assert.AreEqual(1, uc.updateCount);

                // Advance elapsed time to 4.9 seconds. The fourth tick should occur at 4.0
                AdvanceWorldTime(2.2f);
                world.Update();
                Assert.AreEqual(4.0, uc.lastUpdateTime, 0.001f);
                Assert.AreEqual(1.0, uc.lastUpdateDeltaTime, 0.001f);
                Assert.AreEqual(1, uc.updateCount);
            }
        }

        [Test]
        public void WorldSimulation_FixedRateUtils_CatchUp()
        {
            using (var world = new World("World A"))
            {
                var sim = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
                var uc = world.GetOrCreateSystemManaged<UpdateCountSystem>();
                sim.AddSystemToUpdateList(uc);

                // Unity.Core.Hybrid.UpdateWorldTimeSystem
                var timeData = new TimeData();

                void AdvanceWorldTime(float amount)
                {
                    uc.updateCount = 0;
                    timeData = new TimeData(timeData.ElapsedTime + amount, amount);
                    world.SetTime(timeData);
                }

                sim.RateManager = new RateUtils.FixedRateCatchUpManager(0.1f);

                // first frame will tick at elapsedTime=0
                AdvanceWorldTime(0.05f);
                world.Update();
                Assert.AreEqual(0.0, uc.lastUpdateTime, 0.001f);
                Assert.AreEqual(0.1, uc.lastUpdateDeltaTime, 0.001f);
                Assert.AreEqual(1, uc.updateCount);

                // Advance elapsed time to 0.16 seconds. the second tick should occur at 0.1
                AdvanceWorldTime(0.11f);
                world.Update();
                Assert.AreEqual(0.1, uc.lastUpdateTime, 0.001f);
                Assert.AreEqual(0.1, uc.lastUpdateDeltaTime, 0.001f);
                Assert.AreEqual(1, uc.updateCount);

                // Advance elapsed time to 0.17 seconds. No tick should occur.
                AdvanceWorldTime(0.01f);
                world.Update();
                Assert.AreEqual(0.1, uc.lastUpdateTime, 0.001f);
                Assert.AreEqual(0.1, uc.lastUpdateDeltaTime, 0.001f);
                Assert.AreEqual(0, uc.updateCount);

                // Advance elapsed time to 0.27 seconds. The third tick should occur at 0.20
                AdvanceWorldTime(0.1f);
                world.Update();
                Assert.AreEqual(0.2, uc.lastUpdateTime, 0.001f);
                Assert.AreEqual(0.1, uc.lastUpdateDeltaTime, 0.001f);
                Assert.AreEqual(1, uc.updateCount);

                // Advance elapsed time to 0.49 seconds. The fourth and fifth ticks should occur at 0.30 and 0.40.
                AdvanceWorldTime(0.22f);
                world.Update();
                Assert.AreEqual(0.40, uc.lastUpdateTime, 0.001f);
                Assert.AreEqual(0.1, uc.lastUpdateDeltaTime, 0.001f);
                Assert.AreEqual(2, uc.updateCount);
            }
        }

        [Test]
        public void DisposeAllWorlds()
        {
            var worlds = CopyWorlds(alsoClear: true);
            try
            {
                var createdWorlds = new[] { new World("a"), new World("b") };

                foreach (var world in World.All)
                {
                    Assert.That(world.IsCreated, Is.True);
                }

                World.DisposeAllWorlds();

                Assert.That(World.All.Count, Is.EqualTo(0));
                Assert.IsFalse(createdWorlds.All(w => w.IsCreated));
            }
            finally
            {
                ResetWorlds(worlds);
            }
        }

        [Test]
        public void IteratingOverBoxedNoAllocReadOnlyCollectionThrows()
        {
            var sourceList = Enumerable.Range(1, 10).ToList();
            var readOnlyCollection = new World.NoAllocReadOnlyCollection<int>(sourceList);

            var ex = Assert.Throws<NotSupportedException>(() => ((IEnumerable<int>)readOnlyCollection).GetEnumerator());
            var ex2 = Assert.Throws<NotSupportedException>(() => ((IEnumerable)readOnlyCollection).GetEnumerator());
            Assert.That(ex.Message, Is.EqualTo($"To avoid boxing, do not cast {nameof(World.NoAllocReadOnlyCollection<int>)} to IEnumerable<T>."));
            Assert.That(ex2.Message, Is.EqualTo($"To avoid boxing, do not cast {nameof(World.NoAllocReadOnlyCollection<int>)} to IEnumerable."));
        }

#if !DOTS_DISABLE_DEBUG_NAMES
        [Test]
        public void WorldTimeSingletonHasAnEntityName()
        {
            using (var world = new World("w"))
            using (var timeSingletonQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<WorldTime>(), ComponentType.ReadWrite<WorldTimeQueue>()))
            {
                Assert.That(timeSingletonQuery.IsEmptyIgnoreFilter, Is.True);

                world.SetTime(new TimeData(10, 0.1f));
                Assert.That(timeSingletonQuery.IsEmptyIgnoreFilter, Is.False);
                var timeSingleton = timeSingletonQuery.GetSingletonEntity();
                Assert.That(world.EntityManager.GetName(timeSingleton), Is.EqualTo("WorldTime"));
            }
        }

#endif

        public partial class ContainerOwnerSystem : SystemBase
        {
            public NativeArray<int> Container;
            protected override void OnCreate()
            {
                Container = new NativeArray<int>(1, Allocator.Persistent);
            }

            protected override void OnDestroy()
            {
                Container.Dispose();
            }

            protected override void OnUpdate() { }
        }

        public partial class ContainerUsingSystem : SystemBase
        {
            public struct ContainerJob : IJob
            {
                public NativeArray<int> Container;
                public void Execute()
                {}
            }
            protected override void OnUpdate()
            {
                var job = new ContainerJob {Container = World.GetExistingSystemManaged<ContainerOwnerSystem>().Container};
                Dependency = job.Schedule(Dependency);
            }
        }

        [Test]
        public void World_DisposeWithRunningJobs_Succeeds()
        {
            using (var w = new World("Test"))
            {
                // Ordering is important, the owner system needs to be destroyed before the user system
                var user = w.GetOrCreateSystemManaged<ContainerUsingSystem>();
                var owner = w.GetOrCreateSystemManaged<ContainerOwnerSystem>();

                owner.Update();
                user.Update();
            }
        }

        public static World[] CopyWorlds(bool alsoClear = false)
        {
            var worlds = World.s_AllWorlds.ToArray();
            if (alsoClear)
                World.s_AllWorlds.Clear();
            return worlds;
        }

        public static void ResetWorlds(params World[] world)
        {
            World.s_AllWorlds.Clear();
            foreach (var w in world)
            {
                World.s_AllWorlds.Add(w);
            }
        }

        public partial class MultiPhaseTestSystem : SystemBase
        {
            private int TotalSystemCount;
            public bool IsRunning;
            protected override void OnStartRunning()
            {
                base.OnStartRunning();
                IsRunning = true;
            }

            protected override void OnStopRunning()
            {
                base.OnStopRunning();
                // All systems should still exist
                Assert.AreEqual(TotalSystemCount, World.Systems.Count);
                // Systems should not yet be destroyed
                foreach (var system in World.Systems)
                {
                    Assert.AreEqual(system.World, World); // stand-in for "has system.OnAfterDestroyInternal been called"
                }

                IsRunning = false;
            }

            protected override void OnDestroy()
            {
                base.OnDestroy();
                // All systems should still exist
                Assert.AreEqual(TotalSystemCount, World.Systems.Count);
                // Systems should all be stopped and disabled, but not yet destroyed
                foreach (var system in World.Systems)
                {
                    Assert.IsFalse((system as MultiPhaseTestSystem)?.IsRunning ?? false);
                    Assert.AreEqual(system.World, World); // stand-in for "has system.OnAfterDestroyInternal been called"
                }
            }

            protected override void OnUpdate()
            {
                TotalSystemCount = World.Systems.Count;
            }
        }
        public partial class MultiPhaseTestSystem1 : MultiPhaseTestSystem
        {
        }
        public partial class MultiPhaseTestSystem2 : MultiPhaseTestSystem
        {
        }
        public partial class MultiPhaseTestSystem3 : MultiPhaseTestSystem
        {
        }

        [Test]
        public void World_Dispose_MultiPhaseSystemDestroy()
        {
            World world = new World("WorldX");
            var sys1 = world.CreateSystemManaged<MultiPhaseTestSystem1>();
            var sys2 = world.CreateSystemManaged<MultiPhaseTestSystem2>();
            var sys3 = world.CreateSystemManaged<MultiPhaseTestSystem3>();
            sys1.Update();
            sys2.Update();
            sys3.Update();
            world.Dispose();
            Assert.AreEqual(0, world.Systems.Count);
        }

        [DisableAutoCreation]
        public partial struct UnregisteredGenericSystem<T>:ISystem
        {
            public void OnUpdate(ref SystemState state)
            {
                throw new InvalidOperationException("I ran!");
            }
        }
        [Test]
        public void CreateSystem_WithUnregisteredSystemType_Throws()
        {
            // Creating unregistered unmanaged systems is not supported; the necessary reflection would not be Burst-compatible.
            using var world = new World("Test World");
            SystemHandle sys = default;
            Assert.Throws<ArgumentException>(() => { sys = world.CreateSystem<UnregisteredGenericSystem<int>>(); });
        }
        [Test]
        public void GetOrCreateSystem_WithUnregisteredSystemType_Throws()
        {
            // Creating unregistered unmanaged systems is not supported; the necessary reflection would not be Burst-compatible.
            using var world = new World("Test World");
            SystemHandle sys = default;
            Assert.Throws<ArgumentException>(() => { sys = world.GetOrCreateSystem<UnregisteredGenericSystem<int>>(); });
        }

        [DisableAutoCreation]
        public partial class UnregisteredGenericSystemManaged<T>:SystemBase
        {
            protected override void OnUpdate()
            {
                throw new InvalidOperationException("I ran!");
            }
        }
        [Test]
        public void AddSystemManaged_WithUnregisteredSystemType_Works()
        {
            using var world = new World("Test World");
            var sys = new UnregisteredGenericSystemManaged<int>();
            Assert.DoesNotThrow(() => world.AddSystemManaged(sys));
            Assert.That(() => sys.Update(), Throws.InvalidOperationException.With.Message.Contain("I ran!") );
        }
        [Test]
        public void CreateSystemManaged_WithUnregisteredSystemType_Works()
        {
            using var world = new World("Test World");
            UnregisteredGenericSystemManaged<int> sys = default;
            Assert.DoesNotThrow(() => { sys = world.CreateSystemManaged<UnregisteredGenericSystemManaged<int>>(); });
            Assert.That(() => sys.Update(), Throws.InvalidOperationException.With.Message.Contain("I ran!") );
        }
        [Test]
        public void GetOrCreateSystemManaged_WithUnregisteredSystemType_Works()
        {
            using var world = new World("Test World");
            UnregisteredGenericSystemManaged<int> sys = default;
            Assert.DoesNotThrow(() => { sys = world.GetOrCreateSystemManaged<UnregisteredGenericSystemManaged<int>>(); });
            Assert.That(() => sys.Update(), Throws.InvalidOperationException.With.Message.Contain("I ran!") );
        }


#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void World_Dispose_DisposesManagedComponent()
        {
            var managedWithRefCount = new EcsTestManagedCompWithRefCount();
            using(var world = new World("WorldX"))
            {
               var entity = world.EntityManager.CreateEntity();
               world.EntityManager.AddComponentObject(entity, managedWithRefCount);
               UnityEngine.Assertions.Assert.AreEqual(1, managedWithRefCount.RefCount);
            }
            UnityEngine.Assertions.Assert.AreEqual(0, managedWithRefCount.RefCount);
        }
#endif
    }
}
