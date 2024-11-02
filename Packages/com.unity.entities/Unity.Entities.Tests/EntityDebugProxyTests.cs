using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Entities.Tests
{
    partial class EntityDebugProxyTests : ECSTestsFixture
    {
        public static World WorldForTest;
        static Entity CreateEntity(EntityManager em)
        {
            var entity = em.CreateEntity();
            em.SetName(entity, "Test");

            em.AddComponentData(entity, new EcsTestData(1));
            em.AddBuffer<EcsIntElement>(entity).Add(2);
            em.AddSharedComponentManaged(entity, new EcsTestSharedComp{value = 3});
            #if !UNITY_DISABLE_MANAGED_COMPONENTS
            em.AddComponentData(entity, new EcsTestManagedComponent(){ value = "boing" });
            #endif

            WorldForTest = em.World;

            return entity;
        }

        static T Get<T>(object[] array)
        {
            for (int i = 0; i != array.Length; i++)
            {
                if (array[i].GetType() == typeof(T))
                    return (T)array[i];
            }

            return default;
        }

        static void CheckEntity(Entity entity, bool partOfSystem)
        {
            #if !DOTS_DISABLE_DEBUG_NAMES
            var expectedName = $"'Test' Entity({entity.Index}:{entity.Version})";
            #else
            var expectedName = $"'' Entity({entity.Index}:{entity.Version})";
            #endif
            Assert.AreEqual(expectedName +  " Test World", EntityDebugProxy.GetDebugName(entity.Index, entity.Version));

            var proxy = new EntityDebugProxy(entity);
            var components = proxy.Components;
            #if !UNITY_DISABLE_MANAGED_COMPONENTS
            Assert.AreEqual(5, components.Length); // +1 for Simulate
            #else
            Assert.AreEqual(4, components.Length); // +1 for Simulate
            #endif

            Assert.AreEqual(default(Simulate), Get<Simulate>(proxy.Components));

            Assert.AreEqual(1, Get<EcsTestData>(proxy.Components).value);

            Assert.AreEqual(1, Get<EcsIntElement[]>(proxy.Components).Length);
            Assert.AreEqual(2, Get<EcsIntElement[]>(proxy.Components)[0].Value);

            Assert.AreEqual(3, Get<EcsTestSharedComp>(proxy.Components).value);

            #if !UNITY_DISABLE_MANAGED_COMPONENTS
            Assert.AreEqual("boing", Get<EcsTestManagedComponent>(proxy.Components).value);
            #endif

            var entityManagerDebug = new EntityManagerDebugView(proxy.World.EntityManager);

            var entities = entityManagerDebug.Entities;
            Assert.AreEqual(1 + (partOfSystem ? 1 : 0), entities.Length);
            Assert.AreEqual(expectedName, entities[0].ToString());
            Assert.AreEqual(WorldForTest, entities[0].World);

            Assert.AreEqual(1 + (partOfSystem ? 1 : 0), entityManagerDebug.ArchetypesUsed.Length);
            Assert.AreEqual(proxy.Archetype, entityManagerDebug.ArchetypesUsed[0]);
            var testAccess = entityManagerDebug.NumEntities;
        }

        [Test]
        public void TestEntityDebugProxyComponents()
        {
            var entity = CreateEntity(m_Manager);
            CheckEntity(entity, false);

            Assert.AreEqual(4, Marshal.SizeOf(typeof(EcsTestData)));
        }

        struct Job : IJob
        {
            public NativeReference<bool> Success;
            public Entity Entity;

            public void Execute()
            {
                CheckEntity(Entity, false);
                Success.Value = true;
            }
        }

        [Test]
        public void AccessDebugProxyFromJob()
        {
            var entity = CreateEntity(m_Manager);
            var job = new Job
            {
                Entity = entity,
                Success = new NativeReference<bool>(false, Allocator.Persistent)
            };
            job.Schedule().Complete();
            Assert.IsTrue(job.Success.Value);
            job.Success.Dispose();
        }

        partial class CheckDebugProxyFromEntitiesForEach : SystemBase
        {
            public NativeReference<bool> Reference = new NativeReference<bool>(false, Allocator.Persistent);

            protected override void OnDestroy()
            {
                Reference.Dispose();
            }

            protected override void OnUpdate()
            {
                var referenceValue = Reference;
                Entities.WithoutBurst().WithNativeDisableParallelForRestriction(referenceValue).ForEach((Entity entity, ref EcsTestData outputValue) =>
                {
                    CheckEntity(entity, true);
                    referenceValue.Value = true;
                }).ScheduleParallel();
            }
        }

        [Test]
        public void AccessDebugProxyFromSystem()
        {
            CreateEntity(m_Manager);
            var system = World.CreateSystemManaged<CheckDebugProxyFromEntitiesForEach>();
            system.Update();

            system.EntityManager.CompleteAllTrackedJobs();
            Assert.IsTrue(system.Reference.Value);
        }

        [Test]
        unsafe public void EntityQueryVisualizerEnableBits()
        {
            var entityDisabled = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entityDisabled, false);
            var entityEnabled = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            var enabledEnabled2 = m_Manager.CreateEntity(typeof(EcsTestData2), typeof(EcsTestDataEnableable));


            var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            var debug = new EntityQueryDebugView(query);
            var matchingEntities = debug.MatchingEntities;
            Assert.AreEqual(2, matchingEntities.Length);
            Assert.AreEqual(new Entity_(World, entityEnabled, false).ToString(), matchingEntities[0].ToString());
            Assert.AreEqual(new Entity_(World, enabledEnabled2, false).ToString(), matchingEntities[1].ToString());

            var debugView = new EntityQueryDebugView(m_Manager.UniversalQuery);
            matchingEntities = debugView.MatchingEntities;
            Assert.AreEqual(3, matchingEntities.Length);
            Assert.AreEqual(new Entity_(World, entityDisabled, false).ToString(), matchingEntities[0].ToString());
            Assert.AreEqual(new Entity_(World, entityEnabled, false).ToString(), matchingEntities[1].ToString());
            Assert.AreEqual(new Entity_(World, enabledEnabled2, false).ToString(), matchingEntities[2].ToString());

            query.Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // This code relies on Atomic Safety Handles for detecting disposal
            Assert.AreEqual(null, debug.MatchingEntities);
            Assert.AreEqual(null, debug.MatchingChunks);
#endif
        }


        partial class CheckDebugProxyWorldSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var entity = EntityManager.CreateEntity();
                Assert.AreEqual(World, EntityDebugProxy.GetWorld(entity));
            }
        }

        [Test]
        public void DisambiguateWorldBasedOnExecutingSystem()
        {
            using var world = new World("TestWorld2");

            world.GetOrCreateSystemManaged<CheckDebugProxyWorldSystem>().Update();
            World.GetOrCreateSystemManaged<CheckDebugProxyWorldSystem>().Update();
        }


        partial class DebugSystemBaseWithVariable : SystemBase
        {
            public int Value;
            protected override void OnUpdate()
            {
            }
        }

        partial struct DebugISystemWithVariable : ISystem
        {
            public int Value;
        }

        [Test]
        public void SystemDebugVisualization()
        {
            var system = World.CreateSystemManaged<DebugSystemBaseWithVariable>();
            system.Value = 5;

            var systemDebugView = new SystemDebugView(system);
            var debugObject = (DebugSystemBaseWithVariable) systemDebugView.UserData;

            Assert.AreEqual(5, debugObject.Value);
            Assert.AreEqual(typeof(DebugSystemBaseWithVariable),systemDebugView.Type);
            Assert.AreEqual(nameof(DebugSystemBaseWithVariable),systemDebugView.ToString());
            Assert.AreEqual(World,systemDebugView.SystemState.EntityManager.World);
        }

        [Test]
        public unsafe void ISystemDebugVisualization()
        {
            var system = World.CreateSystem<DebugISystemWithVariable>();
            World.Unmanaged.GetUnsafeSystemRef<DebugISystemWithVariable>(system).Value = 5;

            var systemDebugView = new SystemDebugView(system);
            var debugObject = (DebugISystemWithVariable) systemDebugView.UserData;

            Assert.AreEqual(5, debugObject.Value);
            Assert.AreEqual(typeof(DebugISystemWithVariable),systemDebugView.Type);
            Assert.AreEqual(nameof(DebugISystemWithVariable),systemDebugView.ToString());
            Assert.AreEqual(World,systemDebugView.SystemState.EntityManager.World);
        }

        [Test]
        public void DoesntIncludeMetaChunkEntities()
        {
            // Meta chunk entities can be found by viewing an entity with a chunk component
            m_Manager.CreateEntity(ComponentType.ChunkComponent<EcsTestData>());
            var debugView = new EntityManagerDebugView(m_Manager);
            Assert.AreEqual(1, debugView.Entities.Length);
        }
    }
}
