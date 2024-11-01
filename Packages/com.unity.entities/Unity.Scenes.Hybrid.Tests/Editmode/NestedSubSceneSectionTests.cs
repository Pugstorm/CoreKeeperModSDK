using NUnit.Framework;
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.TestTools;
#endif
using Unity.Entities;
using Unity.Entities.Tests;
using Unity.Scenes.Editor.Tests;

namespace Unity.Scenes.Hybrid.Tests
{
    partial class CountPendingLoads : SystemBase
    {
        int m_TickCount = 0;
        int m_NumPendingLoads;
        public int NumPendingLoads => m_NumPendingLoads;
        protected override void OnUpdate()
        {
            int tickCount = ++m_TickCount;
            int numPendingLoads = 0;

            // root scene
            Entities
                .WithNone<DisableSceneResolveAndLoad, SceneSectionStreamingSystem.StreamingState, ResolvedSceneHash>()
                .ForEach((Entity entity, in RequestSceneLoaded requestSceneLoad) =>
            {
                if ((requestSceneLoad.LoadFlags & SceneLoadFlags.BlockOnStreamIn) == SceneLoadFlags.BlockOnStreamIn)
                {
                    numPendingLoads++;
                }
            }).Run();

            m_NumPendingLoads = numPendingLoads;
        }
    }

    class NestedSubSceneSectionConversion : NestedSubSceneSectionTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_Settings.Setup(false);
            base.SetUpOnce();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            base.TearDownOnce();
            m_Settings.TearDown();
        }
    }

    class NestedSubSceneSectionBaking : NestedSubSceneSectionTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_Settings.Setup(true);
            base.SetUpOnce();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            base.TearDownOnce();
            m_Settings.TearDown();
        }
    }

    abstract class NestedSubSceneSectionTests : SubSceneTestFixture
    {
        protected TestLiveConversionSettings m_Settings;

        public NestedSubSceneSectionTests()
        {
            PlayModeScenePath = "Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/TestSceneWithSubScene/NestedSubSceneSectionTestScene.unity";
        }

        // Only works in Editor for now until we can support SubScene building with new build settings in a test
        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public void LoadSceneAsyncBlocking_LoadsAllSections()
        {
            using (var world = TestWorldSetup.CreateEntityWorld("World", false))
            {
                EntityManager manager = world.EntityManager;

                CountPendingLoads countPendingLoads = world.GetOrCreateSystemManaged<CountPendingLoads>();

                var resolveParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                };

                SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, resolveParams);

                int sanityCheck = 30;
                do
                {
                    world.Update();
                    countPendingLoads.Update();
                    if (--sanityCheck == 0)
                        break;
                } while (countPendingLoads.NumPendingLoads>0);

                Assert.IsFalse(sanityCheck == 0, $"We timed out trying to load nested subscenes {sanityCheck}.");

                EntitiesAssert.Contains(manager,
                    EntityMatch.Partial(new SubSceneSectionTestData(1776)),
                    EntityMatch.Partial(new SubSceneSectionTestData(1789))
                );
            }
        }

        // Only works in Editor for now until we can support SubScene building with new build settings in a test
        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public void LoadAndUnloadSceneAsyncBlocking_LoadsAllSections()
        {
            using (var world = TestWorldSetup.CreateEntityWorld("World", false))
            {
                EntityManager manager = world.EntityManager;
                CountPendingLoads countPendingLoads = world.GetOrCreateSystemManaged<CountPendingLoads>();

                var resolveParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                };

                Entity sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, resolveParams);

                int sanityCheck = 30;
                do
                {
                    world.Update();
                    countPendingLoads.Update();
                    if (--sanityCheck == 0)
                        break;
                } while (countPendingLoads.NumPendingLoads>0);

                Assert.IsFalse(sanityCheck == 0, $"We timed out trying to load nested subscenes.");

                EntitiesAssert.Contains(manager,
                    EntityMatch.Partial(new SubSceneSectionTestData(1776)),
                    EntityMatch.Partial(new SubSceneSectionTestData(1789))
                );

                SceneSystem.UnloadScene(world.Unmanaged, sceneEntity);
            }
        }

        // Only works in Editor for now until we can support SubScene building with new build settings in a test
        [Test, Explicit]
        [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public void LoadAndUnloadSceneAsync_LoadsAllSections()
        {
            using (var world = TestWorldSetup.CreateEntityWorld("World", false))
            {
                EntityManager manager = world.EntityManager;
                CountPendingLoads countPendingLoads = world.GetOrCreateSystemManaged<CountPendingLoads>();

                var resolveParams = new SceneSystem.LoadParameters
                {
                };

                Entity sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, resolveParams);

                int sanityCheck = 300;
                do
                {
                    world.Update();
                } while (sanityCheck-- > 0);

                EntitiesAssert.Contains(manager,
                    EntityMatch.Partial(new SubSceneSectionTestData(1776)),
                    EntityMatch.Partial(new SubSceneSectionTestData(1789))
                );

                SceneSystem.UnloadScene(world.Unmanaged, sceneEntity);
            }
        }
    }
}
