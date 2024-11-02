using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Conversion;
using Unity.Entities.Tests;
using Unity.Scenes.Editor.Tests;
using UnityEngine;

namespace Unity.Entities.Hybrid.Tests.Baking
{
    class TempBakingAuthoringTest : MonoBehaviour { public int Field; }

    [TemporaryBakingType]
    struct TempBakingComponentTest : IComponentData { public UnityObjectRef<TempBakingAuthoringTest> component; }

    class TempBakingComponentSystem : Baker<TempBakingAuthoringTest>
    {
        public override void Bake(TempBakingAuthoringTest authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new TempBakingComponentTest(){ component = authoring});
        }
    }

    internal class BakingSystemTests : BakingSystemFixtureBase
    {
        private BakingSystem m_BakingSystem;
        private GameObject m_Prefab;
        private TestLiveConversionSettings m_Settings;

        [SetUp]
        public override void Setup()
        {
            m_Settings.Setup(true);
            base.Setup();

            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            m_BakingSystem.PrepareForBaking(MakeDefaultSettings(), default);

            m_Manager = World.EntityManager;
            m_Prefab = InstantiatePrefab("Prefab");
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            m_BakingSystem = null;
            m_Settings.TearDown();
        }

        [Test]
        public void GetEntity_WithNull_Returns_NullEntity()
        {
            var e1 = m_BakingSystem.GetEntity((Component)null);
            var e2 = m_BakingSystem.GetEntity((GameObject)null);

            Assert.AreEqual(Entity.Null, e1);
            Assert.AreEqual(Entity.Null, e2);
        }

        [Test]
        public void GetEntity_WithUnregisteredGameObject_ReturnsNull()
        {
            var go = CreateGameObject();

            var e1 = m_BakingSystem.GetEntity(go);
            Assert.AreEqual(Entity.Null, e1);

            var e2 = m_BakingSystem.GetEntity(go.transform);
            Assert.AreEqual(Entity.Null, e2);
        }

        [Test]
        public void Remove_TemporaryBakingComponent_Test()
        {
            var component = m_Prefab.AddComponent<TempBakingAuthoringTest>();
            BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

            // The baking system should have stripped out the temporary component at this point
            var query = m_Manager.CreateEntityQuery(typeof(TempBakingComponentTest));
            Assert.IsTrue(query.CalculateEntityCount() == 0);
        }

        [Test]
        public void GetAllSystems_ForDefaultWorlds_Doesnt_Include_BakingSystems_Test()
        {
            var systemList = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            foreach (var system in systemList)
            {
                var flag = TypeManager.GetSystemFilterFlags(system);
                Assert.IsTrue((WorldSystemFilterFlags.BakingSystem & flag) == 0, $"The system {system} is flagged as a baking system but it can't be part of the default runtime world.");
            }

            systemList = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ProcessAfterLoad);
            foreach (var system in systemList)
            {
                var flag = TypeManager.GetSystemFilterFlags(system);
                Assert.IsTrue((WorldSystemFilterFlags.BakingSystem & flag) == 0, $"The system {system} is flagged as a baking system but it can't be part of the {WorldSystemFilterFlags.ProcessAfterLoad} world.");
            }

            systemList = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.EntitySceneOptimizations);
            foreach (var system in systemList)
            {
                var flag = TypeManager.GetSystemFilterFlags(system);
                Assert.IsTrue((WorldSystemFilterFlags.BakingSystem & flag) == 0, $"The system {system} is flagged as a baking system but it can't be part of the {WorldSystemFilterFlags.EntitySceneOptimizations} world.");
            }
        }
    }
}
