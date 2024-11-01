using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Conversion;
using Unity.Entities.Tests;
using Unity.Scenes.Editor.Tests;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Entities.Hybrid.Tests.Baking
{
    [UpdateInGroup(typeof(PreBakingSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial class PreBakingTestSystem : SystemBase
    {
        public static bool executed = false;
        protected override void OnUpdate()
        {
            // BakeGameObjects removes all entities in the world, so adding a component doesn't work to check if
            // this system has run. Using a  static bool as a workaround
            executed = true;
        }
    }

    [UpdateInGroup(typeof(TransformBakingSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial class TransformBakingTestSystem : SystemBase
    {
        private EntityQuery query;

        protected override void OnCreate()
        {
            query = GetEntityQuery(typeof(BakingSystemGroupTests.TempBakingComponentTest));
        }

        protected override void OnUpdate()
        {
            // Checking that PreBaking has executed before
            Assert.AreEqual(true, PreBakingTestSystem.executed);
            // Checking that the Baker has executed before
            Assert.AreEqual(1, query.CalculateEntityCount());

            // Add component to mark that this system has executed
            EntityManager.AddComponent<BakingSystemGroupTests.TransformBakingGroupHasExecutedComponent>(query);
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial class BakingTestSystem : SystemBase
    {
        private EntityQuery query;

        protected override void OnCreate()
        {
            query = GetEntityQuery(
                typeof(BakingSystemGroupTests.TempBakingComponentTest),
                typeof(BakingSystemGroupTests.TransformBakingGroupHasExecutedComponent));
        }

        protected override void OnUpdate()
        {
            // Checking that PreBaking has executed before
            Assert.AreEqual(true, PreBakingTestSystem.executed);
            // Checking that the Baker and TransformBakingSystemGroup have executed before
            Assert.AreEqual(1, query.CalculateEntityCount());

            // Add component to mark that this system has executed
            EntityManager.AddComponent<BakingSystemGroupTests.BakingGroupHasExecutedComponent>(query);
        }
    }

    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial class PostBakingTestSystem : SystemBase
    {
        private EntityQuery query;

        protected override void OnCreate()
        {
            query = GetEntityQuery(
                typeof(BakingSystemGroupTests.TempBakingComponentTest),
                typeof(BakingSystemGroupTests.TransformBakingGroupHasExecutedComponent),
                typeof(BakingSystemGroupTests.BakingGroupHasExecutedComponent));
        }

        protected override void OnUpdate()
        {
            // Checking that PreBaking has executed before
            Assert.AreEqual(true, PreBakingTestSystem.executed);
            // Checking that the Baker, TransformBakingSystemGroup and the normal BakingGroup have executed before
            Assert.AreEqual(1, query.CalculateEntityCount());

            // Add component to mark that this system has executed
            EntityManager.AddComponent<BakingSystemGroupTests.PostBakingGroupHasExecutedComponent>(query);
        }
    }

    internal class BakingSystemGroupTests : BakingSystemFixtureBase
    {
        internal class TempBakingAuthoringTest : MonoBehaviour { }
        internal struct TempBakingComponentTest : IComponentData { }
        internal struct TransformBakingGroupHasExecutedComponent : IComponentData { }
        internal struct BakingGroupHasExecutedComponent : IComponentData { }
        internal struct PostBakingGroupHasExecutedComponent : IComponentData {  }

        class TempBakingComponentSystem : Baker<TempBakingAuthoringTest>
        {
            public override void Bake(TempBakingAuthoringTest authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<TempBakingComponentTest>(entity);
            }
        }

        private BakingSystem m_BakingSystem;
        private TestLiveConversionSettings m_Settings;

        [SetUp]
        public override void Setup()
        {
            m_Settings.Setup(true);
            base.Setup();

            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var settings = MakeDefaultSettings();
            settings.ExtraSystems.Add(typeof(PreBakingTestSystem));
            settings.ExtraSystems.Add(typeof(BakingTestSystem));
            settings.ExtraSystems.Add(typeof(PostBakingTestSystem));
            settings.ExtraSystems.Add(typeof(TransformBakingTestSystem));
            m_BakingSystem.PrepareForBaking(settings, default);

            m_Manager = World.EntityManager;
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            m_BakingSystem = null;
            m_Settings.TearDown();
        }

        [Test]
        public void BakingGroupsTest()
        {
            var go = CreateGameObject();
            go.AddComponent<TempBakingAuthoringTest>();

            // BakeGameObjects removes all entities in the world, so adding a component doesn't work to check if
            // this system has run. Using a  static bool as a workaround
            PreBakingTestSystem.executed = false;

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);

            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var entity = m_BakingSystem.GetEntity(go);
            var manager = World.EntityManager;

            // Check that prebaking has executed
            Assert.AreEqual(true, PreBakingTestSystem.executed);
            // Check that bakers have executed
            Assert.AreEqual(true, manager.HasComponent<TempBakingComponentTest>(entity));
            // Check that the Transform group has executed
            Assert.AreEqual(true, manager.HasComponent<TransformBakingGroupHasExecutedComponent>(entity));
            // Check that the normal system group has executed
            Assert.AreEqual(true, manager.HasComponent<BakingGroupHasExecutedComponent>(entity));
            // Check that the post baking system group has executed
            Assert.AreEqual(true, manager.HasComponent<PostBakingGroupHasExecutedComponent>(entity));
        }
    }
}
