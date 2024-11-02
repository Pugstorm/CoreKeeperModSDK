using NUnit.Framework;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Scenes.Editor.Tests;
#endif
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEditor;

namespace Unity.Scenes.Hybrid.Tests
{
    public class SubSceneSectionTestsBaking : SubSceneSectionTests
    {
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_Settings.Setup(true);
            base.SetUpOnce();
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            base.TearDownOnce();
            m_Settings.TearDown();
        }
    }

    public abstract class SubSceneSectionTests : SubSceneTestFixture
    {
        public TestLiveConversionSettings m_Settings;
        public SubSceneSectionTests() : base()
        {
            PlayModeScenePath = "Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/TestSceneWithSubScene/SubSceneSectionTestScene.unity";
        }

        // Only works in Editor for now until we can support SubScene building with new build settings in a test
        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public void LoadSceneAsync_LoadsAllSections()
        {
            using (var world = TestWorldSetup.CreateEntityWorld("World", false))
            {
                var manager = world.EntityManager;

                var resolveParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                };

                SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, resolveParams);
                world.Update();

                EntitiesAssert.Contains(manager,
                    EntityMatch.Partial(new SubSceneSectionTestData(42)),
                    EntityMatch.Partial(new SubSceneSectionTestData(43)),
                    EntityMatch.Partial(new SubSceneSectionTestData(44))
                );
            }
        }

        // Only works in Editor for now until we can support SubScene building with new build settings in a test
        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public void LoadSceneAsync_DeleteSceneEntityUnloadsAllSections()
        {
            using (var world = TestWorldSetup.CreateEntityWorld("World", false))
            {
                var manager = world.EntityManager;

                var resolveParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                };

                var subSceneSectionTestDataQuery = manager.CreateEntityQuery(typeof(SubSceneSectionTestData));

                var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, resolveParams);
                world.Update();

                Assert.AreEqual(3, subSceneSectionTestDataQuery.CalculateEntityCount());

                manager.DestroyEntity(sceneEntity);
                world.Update();

                Assert.AreEqual(0, subSceneSectionTestDataQuery.CalculateEntityCount());
            }
        }

        // Only works in Editor for now until we can support SubScene building with new build settings in a test
        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public void CanLoadSectionsIndividually()
        {
            using (var world = TestWorldSetup.CreateEntityWorld("World", false))
            {
                var manager = world.EntityManager;

                var resolveParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn | SceneLoadFlags.DisableAutoLoad
                };

                var subSceneSectionTestDataQuery = manager.CreateEntityQuery(typeof(SubSceneSectionTestData));

                var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, resolveParams);
                world.Update();

                Assert.AreEqual(0, subSceneSectionTestDataQuery.CalculateEntityCount());

                var section0Entity = FindSectionEntity(manager, sceneEntity, 0);
                var section10Entity = FindSectionEntity(manager, sceneEntity, 10);
                var section20Entity = FindSectionEntity(manager, sceneEntity, 20);
                Assert.AreNotEqual(Entity.Null, section0Entity);
                Assert.AreNotEqual(Entity.Null, section10Entity);
                Assert.AreNotEqual(Entity.Null, section20Entity);

                Assert.IsFalse(SceneSystem.IsSectionLoaded(world.Unmanaged, section0Entity));
                Assert.IsFalse(SceneSystem.IsSectionLoaded(world.Unmanaged, section10Entity));
                Assert.IsFalse(SceneSystem.IsSectionLoaded(world.Unmanaged, section20Entity));

                manager.AddComponentData(section0Entity,
                    new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});

                world.Update();

                Assert.IsTrue(SceneSystem.IsSectionLoaded(world.Unmanaged, section0Entity));
                Assert.IsFalse(SceneSystem.IsSectionLoaded(world.Unmanaged, section10Entity));
                Assert.IsFalse(SceneSystem.IsSectionLoaded(world.Unmanaged, section20Entity));

                Assert.AreEqual(1, subSceneSectionTestDataQuery.CalculateEntityCount());
                Assert.AreEqual(42, subSceneSectionTestDataQuery.GetSingleton<SubSceneSectionTestData>().Value);

                manager.AddComponentData(section20Entity,
                    new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                world.Update();

                Assert.IsTrue(SceneSystem.IsSectionLoaded(world.Unmanaged, section0Entity));
                Assert.IsFalse(SceneSystem.IsSectionLoaded(world.Unmanaged, section10Entity));
                Assert.IsTrue(SceneSystem.IsSectionLoaded(world.Unmanaged, section20Entity));

                Assert.AreEqual(2, subSceneSectionTestDataQuery.CalculateEntityCount());
                EntitiesAssert.Contains(manager,
                    EntityMatch.Partial(new SubSceneSectionTestData(42)),
                    EntityMatch.Partial(new SubSceneSectionTestData(44)));


                manager.AddComponentData(section10Entity,
                    new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                world.Update();

                Assert.IsTrue(SceneSystem.IsSectionLoaded(world.Unmanaged, section0Entity));
                Assert.IsTrue(SceneSystem.IsSectionLoaded(world.Unmanaged, section10Entity));
                Assert.IsTrue(SceneSystem.IsSectionLoaded(world.Unmanaged, section20Entity));

                Assert.AreEqual(3, subSceneSectionTestDataQuery.CalculateEntityCount());
                EntitiesAssert.Contains(manager,
                    EntityMatch.Partial(new SubSceneSectionTestData(42)),
                    EntityMatch.Partial(new SubSceneSectionTestData(43)),
                    EntityMatch.Partial(new SubSceneSectionTestData(44)));
            }
        }

        // Only works in Editor for now until we can support SubScene building with new build settings in a test
        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public void TestGetSectionStreamingState()
        {
            using (var world = TestWorldSetup.CreateEntityWorld("World", false))
            {
                var manager = world.EntityManager;

                var resolveParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn | SceneLoadFlags.DisableAutoLoad
                };

                var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, resolveParams);
                Assert.AreEqual(SceneSystem.SceneStreamingState.Loading, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));
                world.Update();

                Assert.AreEqual(SceneSystem.SceneStreamingState.LoadedSectionEntities, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));

                var section0Entity = FindSectionEntity(manager, sceneEntity, 0);
                var section10Entity = FindSectionEntity(manager, sceneEntity, 10);
                var section20Entity = FindSectionEntity(manager, sceneEntity, 20);
                Assert.AreNotEqual(Entity.Null, section0Entity);
                Assert.AreNotEqual(Entity.Null, section10Entity);
                Assert.AreNotEqual(Entity.Null, section20Entity);

                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                manager.AddComponentData(section10Entity, new RequestSceneLoaded());
                Assert.AreEqual(SceneSystem.SectionStreamingState.LoadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));

                world.Update();

                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.LoadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                manager.RemoveComponent<RequestSceneLoaded>(section10Entity);
                Assert.AreEqual(SceneSystem.SectionStreamingState.UnloadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));

                world.Update();

                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                manager.AddComponentData(section0Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                Assert.AreEqual(SceneSystem.SectionStreamingState.LoadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));

                world.Update();

                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                manager.AddComponentData(section10Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                Assert.AreEqual(SceneSystem.SectionStreamingState.LoadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));

                world.Update();

                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                manager.AddComponentData(section20Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                Assert.AreEqual(SceneSystem.SectionStreamingState.LoadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                world.Update();

                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                manager.RemoveComponent<RequestSceneLoaded>(section10Entity);
                Assert.AreEqual(SceneSystem.SectionStreamingState.UnloadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));

                world.Update();

                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                manager.RemoveComponent<RequestSceneLoaded>(section0Entity);
                Assert.AreEqual(SceneSystem.SectionStreamingState.UnloadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));

                world.Update();

                // Section 0 unload is on hold until we unload section 20
                Assert.AreEqual(SceneSystem.SectionStreamingState.UnloadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                manager.RemoveComponent<RequestSceneLoaded>(section20Entity);
                world.Update();

                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                manager.AddComponentData(section0Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                manager.AddComponentData(section10Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                manager.AddComponentData(section20Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});

                world.Update();

                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                // Unloading section 0 and 20 while loading section 10. This will unload section 20, load section 10 and put section 0 on UnloadRequested
                manager.RemoveComponent<RequestSceneLoaded>(section10Entity);
                world.Update();

                manager.RemoveComponent<RequestSceneLoaded>(section0Entity);
                manager.RemoveComponent<RequestSceneLoaded>(section20Entity);

                manager.AddComponentData(section10Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});

                world.Update();

                Assert.AreEqual(SceneSystem.SectionStreamingState.UnloadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));
            }
        }

        // Only works in Editor for now until we can support SubScene building with new build settings in a test
        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public void TestLoadSectionsSynchronously_OutOfOrder()
        {
            using (var world = TestWorldSetup.CreateEntityWorld("World", false))
            {
                var manager = world.EntityManager;

                var resolveParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn | SceneLoadFlags.DisableAutoLoad
                };

                var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, resolveParams);
                Assert.AreEqual(SceneSystem.SceneStreamingState.Loading, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));
                world.Update();

                Assert.AreEqual(SceneSystem.SceneStreamingState.LoadedSectionEntities, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));

                var section0Entity = FindSectionEntity(manager, sceneEntity, 0);
                var section10Entity = FindSectionEntity(manager, sceneEntity, 10);
                var section20Entity = FindSectionEntity(manager, sceneEntity, 20);
                Assert.AreNotEqual(Entity.Null, section0Entity);
                Assert.AreNotEqual(Entity.Null, section10Entity);
                Assert.AreNotEqual(Entity.Null, section20Entity);

                // Check nothing is loaded initially
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                world.Update();

                // Loading section 10 sync when section 0 is not loaded. This should produce an error and change the loading to async instead. After that is will put the loading of section 1 on hold.
                manager.AddComponentData(section10Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                Assert.AreEqual(SceneSystem.SectionStreamingState.LoadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));

                // world.Update() will throw an error as a result, the section will be set to async load and wait for section 0
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Can't load section 10 synchronously because section 0 hasn't been loaded first. Loading section asynchronously instead");
                world.Update();
                Assert.AreEqual(SceneSystem.SectionStreamingState.LoadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));

                // Removing the loading on section 0 (that was on hold) to check the system resets correctly when the load is on hold.
                manager.RemoveComponent<RequestSceneLoaded>(section10Entity);

                world.Update();

                // Check the state is reset
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                // Check that the order in the chunk doesn't change the outcome on the loading
                // Load section 0, 10, 20
                manager.AddComponentData(section0Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                manager.AddComponentData(section10Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                manager.AddComponentData(section20Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});

                world.Update();
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                manager.RemoveComponent<RequestSceneLoaded>(section0Entity);
                manager.RemoveComponent<RequestSceneLoaded>(section10Entity);
                manager.RemoveComponent<RequestSceneLoaded>(section20Entity);

                world.Update();

                // Check all unloaded
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                // Load section 20, 10, 0
                manager.AddComponentData(section20Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                manager.AddComponentData(section10Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                manager.AddComponentData(section0Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});

                world.Update();
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                manager.RemoveComponent<RequestSceneLoaded>(section0Entity);
                manager.RemoveComponent<RequestSceneLoaded>(section10Entity);
                manager.RemoveComponent<RequestSceneLoaded>(section20Entity);

                world.Update();

                // Check all unloaded
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                // Load section 10, 0, 20
                manager.AddComponentData(section10Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                manager.AddComponentData(section0Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});
                manager.AddComponentData(section20Entity, new RequestSceneLoaded {LoadFlags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn});

                world.Update();
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));

                manager.RemoveComponent<RequestSceneLoaded>(section0Entity);
                manager.RemoveComponent<RequestSceneLoaded>(section10Entity);
                manager.RemoveComponent<RequestSceneLoaded>(section20Entity);

                world.Update();

                // Check all unloaded
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section10Entity));
                Assert.AreEqual(SceneSystem.SectionStreamingState.Unloaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section20Entity));
            }
        }

        static Entity FindSectionEntity(EntityManager manager, Entity sceneEntity, int sectionIndex)
        {
            var sections = manager.GetBuffer<ResolvedSectionEntity>(sceneEntity);
            for (int i = 0; i < sections.Length; ++i)
            {
                var sectionEntity = sections[i].SectionEntity;
                var sectionData = manager.GetComponentData<SceneSectionData>(sectionEntity);
                if (sectionData.SubSectionIndex == sectionIndex)
                    return sectionEntity;
            }
            return Entity.Null;
        }

        [Test]
        public unsafe void TestBUR2491()
        {
            // This test isn't strictly about section data.
            // But the layout of the struct can cause invalid Burst code to be generated.
            // See BUR-2491 for details.
            var data = new SceneSectionData();
            void* a = &data.ExternalEntitiesRefRange;
            void* b = TestBUR2491Helper.GetFieldAddress(ref data);
            Assert.AreEqual((ulong)a, (ulong)b);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    static class TestBUR2491Helper
    {
        [BurstCompile(CompileSynchronously = true)]
        public static unsafe void* GetFieldAddress(ref SceneSectionData data)
        {
            fixed (void* a = &data.ExternalEntitiesRefRange)
            {
                return a;
            }
        }
    }
}
