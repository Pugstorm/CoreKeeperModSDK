using NUnit.Framework;
using Unity.Entities.Build;
using Unity.Entities.Hybrid.Tests.Baking.ExcludedAssemblyTest;
using Unity.Scenes.Editor.Tests;
using Unity.Entities.Hybrid.Tests.Baking.SeparateAssembly;

namespace Unity.Entities.Hybrid.Tests.Baking
{
    internal class BakingAssemblyTests : BakingSystemFixtureBase
    {
        private BakingSystem m_BakingSystem;
        private TestLiveConversionSettings m_Settings;

        [SetUp]
        public override void Setup()
        {
            m_Settings.Setup(true);
            base.Setup();

            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var settings = MakeDefaultSettings();
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
        public void BakingExcludeAssemblyTests_BuiltInBuildsPath()
        {
            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var manager = World.EntityManager;

            // Create the gameobject with the authoring component
            var go = CreateGameObject();
            go.AddComponent<ComponentInAssemblyAuthoring>();

            // Create the a setting with a filter and another one without a filter
            var originalFilter = EntitiesClientSettings.instance.FilterSettings;
            try
            {
                var clearFilter = new BakingSystemFilterSettings();
                var testFilter = new BakingSystemFilterSettings("Unity.Entities.Hybrid.Tests.SeparateAssembly");

                EntitiesClientSettings.instance.FilterSettings = clearFilter;

                var bakingSettingsNormal = MakeDefaultSettings();
                bakingSettingsNormal.DotsSettings = EntitiesClientSettings.instance;
                var bakingSettingsExclude = MakeDefaultSettings();
                bakingSettingsExclude.DotsSettings = EntitiesClientSettings.instance;

                // Bake the gameobject
                BakingUtility.BakeGameObjects(World, new[] {go}, bakingSettingsNormal);

                // With regular baking it is expected that the baker runs and the component is added
                var entity = m_BakingSystem.GetEntity(go);
                Assert.AreEqual(true, manager.HasComponent<ComponentInAssemblyBakerC>(entity));
                Assert.AreEqual(true, manager.HasComponent<ComponentInAssemblyBakingSystemC>(entity));

                // With the filter we expect that the baker hasn't run and the component is not in the entity
                EntitiesClientSettings.instance.FilterSettings = testFilter;
                BakingUtility.BakeGameObjects(World, new[] {go}, bakingSettingsExclude);
                entity = m_BakingSystem.GetEntity(go);
                Assert.AreEqual(false, manager.HasComponent<ComponentInAssemblyBakerC>(entity));
                Assert.AreEqual(false, manager.HasComponent<ComponentInAssemblyBakingSystemC>(entity));

                // We repeat the same two checks to make sure nothing is left that affects this
                EntitiesClientSettings.instance.FilterSettings = clearFilter;
                BakingUtility.BakeGameObjects(World, new[] {go}, bakingSettingsNormal);
                entity = m_BakingSystem.GetEntity(go);
                Assert.AreEqual(true, manager.HasComponent<ComponentInAssemblyBakerC>(entity));
                Assert.AreEqual(true, manager.HasComponent<ComponentInAssemblyBakingSystemC>(entity));

                EntitiesClientSettings.instance.FilterSettings = testFilter;
                BakingUtility.BakeGameObjects(World, new[] {go}, bakingSettingsExclude);
                entity = m_BakingSystem.GetEntity(go);
                Assert.AreEqual(false, manager.HasComponent<ComponentInAssemblyBakerC>(entity));
                Assert.AreEqual(false, manager.HasComponent<ComponentInAssemblyBakingSystemC>(entity));
            }
            finally
            {
                EntitiesClientSettings.instance.FilterSettings = originalFilter;
            }
        }

        [Test]
        public void AlwaysIncludeBakingSystemAttribute_Works()
        {
            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            var manager = World.EntityManager;

            // Create the a setting with a filter and another one without a filter
            var originalFilter = EntitiesClientSettings.instance.FilterSettings;
            try
            {
                EntitiesClientSettings.instance.FilterSettings = new BakingSystemFilterSettings("Unity.Entities.Hybrid.Tests.ExcludedAssembly");

                // Create the gameobject with the authoring component
                var go = CreateGameObject();
                go.AddComponent<ComponentInAssemblyAuthoring>();

                var bakingSettingsExclude = MakeDefaultSettings();
                bakingSettingsExclude.DotsSettings = EntitiesClientSettings.instance;

                // Bake the gameobject
                BakingUtility.BakeGameObjects(World, new[] {go}, bakingSettingsExclude);

                var entity = m_BakingSystem.GetEntity(go);
                Assert.AreEqual(true, manager.HasComponent<AlwaysIncludeBakingSystemComponent>(entity));
            }
            finally
            {
                EntitiesClientSettings.instance.FilterSettings = originalFilter;
            }
        }
    }
}
