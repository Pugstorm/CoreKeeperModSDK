using NUnit.Framework;
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.TestTools;
#endif
using Unity.Entities;
using Unity.Entities.Tests;

namespace Unity.Scenes.Hybrid.Tests
{
    public class SectionMetadataTests : SubSceneTestFixture
    {
        public SectionMetadataTests() : base()
        {
            PlayModeScenePath = "Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/TestSceneWithSubScene/TestSubSceneWithSectionMetadata.unity";
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            base.SetUpOnce();
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            base.TearDownOnce();
        }

        // Only works in Editor for now until we can support SubScene building with new build settings in a test
        [Ignore("Unstable on CI, DOTS-3750")]
        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public void SectionMetadata()
        {
            using (var world = TestWorldSetup.CreateEntityWorld("World", false))
            {
                var resolveParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.DisableAutoLoad
                };
                var sceneSystem = world.GetOrCreateSystem<SceneSystem>();

                var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, resolveParams);
                world.Update();
                var manager = world.EntityManager;
                var sectionEntities = manager.GetBuffer<ResolvedSectionEntity>(sceneEntity);

                Assert.AreEqual(3, sectionEntities.Length);
                Assert.IsTrue(manager.HasComponent<TestMetadata>(sectionEntities[0].SectionEntity));
                Assert.IsFalse(manager.HasComponent<TestMetadata>(sectionEntities[1].SectionEntity));
                Assert.IsTrue(manager.HasComponent<TestMetadata>(sectionEntities[2].SectionEntity));

                Assert.IsTrue(manager.HasComponent<TestMetadataTag>(sectionEntities[0].SectionEntity));
                Assert.IsFalse(manager.HasComponent<TestMetadataTag>(sectionEntities[1].SectionEntity));
                Assert.IsTrue(manager.HasComponent<TestMetadataTag>(sectionEntities[2].SectionEntity));

                // These components should not be added, instead an error is logged that meta info components can't contain entities or blob assets
                var filteredTypes = new[]
                {
                    typeof(TestMetadataWithEntity), typeof(TestMetadataWithBlobAsset), typeof(EcsTestSharedComp), typeof(EcsIntElement), typeof(EcsCleanup1),
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                    typeof(EcsTestManagedComponent)
#endif
                };

                foreach (var type in filteredTypes)
                {
                    var componentType = ComponentType.FromTypeIndex(TypeManager.GetTypeIndex(type));
                    Assert.IsFalse(manager.HasComponent(sectionEntities[0].SectionEntity, componentType));
                }

                Assert.AreEqual(0, manager.GetComponentData<TestMetadata>(sectionEntities[0].SectionEntity).SectionIndex);
                Assert.AreEqual(13, manager.GetComponentData<TestMetadata>(sectionEntities[0].SectionEntity).Value);
                Assert.AreEqual(42, manager.GetComponentData<TestMetadata>(sectionEntities[2].SectionEntity).SectionIndex);
                Assert.AreEqual(100, manager.GetComponentData<TestMetadata>(sectionEntities[2].SectionEntity).Value);

                var hash = EntityScenesPaths.GetSubSceneArtifactHash(PlayModeSceneGUID, manager.GetComponentData<SceneSystemData>(sceneSystem).BuildConfigurationGUID, true, ImportMode.Synchronous);
                Assert.IsTrue(hash.IsValid);
            }
        }
    }
}
