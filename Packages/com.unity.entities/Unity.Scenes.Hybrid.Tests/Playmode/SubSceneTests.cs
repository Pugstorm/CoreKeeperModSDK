using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Burst;
#if UNITY_EDITOR
using Unity.Scenes.Editor.Tests;
#endif
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Scenes.Hybrid.Tests.Playmode
{
    public class SubSceneTests : SubSceneTestFixture
    {
#if UNITY_EDITOR
        private TestLiveConversionSettings m_Settings;
#endif

        public SubSceneTests()
        {
            PlayModeScenePath = "Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/TestSceneWithSubScene/Subscene/TestSubScene.unity";
            BuildScenePath = "Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/TestSceneWithSubScene/TestScene.unity";
            BuildSceneGUID = new Unity.Entities.Hash128("785a8fb7f3d8213b9b65da9d2c45c22b");
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
#if UNITY_EDITOR
            m_Settings.Setup(true);
#endif
            base.SetUpOnce();
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            base.TearDownOnce();
#if UNITY_EDITOR
            m_Settings.TearDown();
#endif
        }

#if false
        [UnityTest]
        public IEnumerator LoadMultipleSubscenes_Async_WithAssetBundles()
        {
            using (var worldA = CreateEntityWorld("World A"))
            using (var worldB = CreateEntityWorld("World B"))
            {

#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);

                var worldAScene = SceneSystem.LoadSceneAsync(worldA.Unmanaged, PlayModeSceneGUID);
                var worldBScene = SceneSystem.LoadSceneAsync(worldB.Unmanaged, PlayModeSceneGUID);
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);
                var initialContentFilesNumber = Loading.ContentLoadInterface.GetContentFiles(Unity.Entities.Content.RuntimeContentManager.Namespace).Length;
                var worldAScene = SceneSystem.LoadSceneAsync(worldA.Unmanaged, BuildSceneGUID);
                var worldBScene = SceneSystem.LoadSceneAsync(worldB.Unmanaged, BuildSceneGUID);
#endif

                Assert.IsFalse(SceneSystem.IsSceneLoaded(worldA.Unmanaged, worldAScene));
                Assert.IsFalse(SceneSystem.IsSceneLoaded(worldB.Unmanaged, worldBScene));

                while (!SceneSystem.IsSceneLoaded(worldA.Unmanaged, worldAScene) ||
                       !SceneSystem.IsSceneLoaded(worldB.Unmanaged, worldBScene))
                {
                    worldA.Update();
                    worldB.Update();
                    yield return null;
                }

                var worldAEntities = worldA.EntityManager.GetAllEntities(worldA.UpdateAllocator.ToAllocator);
                var worldBEntities = worldB.EntityManager.GetAllEntities(worldB.UpdateAllocator.ToAllocator);
                using (worldAEntities)
                using (worldBEntities)
                {
                    Assert.AreEqual(worldAEntities.Length, worldBEntities.Length);
                }

                var worldAQuery = worldA.EntityManager.CreateEntityQuery(typeof(SharedWithMaterial));
                var worldBQuery = worldB.EntityManager.CreateEntityQuery(typeof(SharedWithMaterial));
                Assert.AreEqual(worldAQuery.CalculateEntityCount(), worldBQuery.CalculateEntityCount());
                Assert.AreEqual(1, worldAQuery.CalculateEntityCount());

                // Get Material on RenderMesh
                var sharedEntitiesA = worldAQuery.ToEntityArray(worldA.UpdateAllocator.ToAllocator);
                var sharedEntitiesB = worldBQuery.ToEntityArray(worldB.UpdateAllocator.ToAllocator);

                SharedWithMaterial sharedA;
                SharedWithMaterial sharedB;
                using (sharedEntitiesA)
                using (sharedEntitiesB)
                {
                    sharedA = worldA.EntityManager.GetSharedComponentManaged<SharedWithMaterial>(sharedEntitiesA[0]);
                    sharedB = worldB.EntityManager.GetSharedComponentManaged<SharedWithMaterial>(sharedEntitiesB[0]);
                }

                Assert.AreSame(sharedA.material, sharedB.material);
                Assert.IsTrue(sharedA.material != null, "sharedA.material != null");
#if !UNITY_EDITOR
                var contentFilesNumberAfterLoadingScene = Loading.ContentLoadInterface.GetContentFiles(Unity.Entities.Content.RuntimeContentManager.Namespace).Length;
#endif

                SceneSystem.UnloadScene(worldA.Unmanaged, worldAScene);
                SceneSystem.UnloadScene(worldB.Unmanaged, worldBScene);

                worldA.Update();
                worldB.Update();
#if !UNITY_EDITOR
                var contentFilesNumberAfterUnLoadingScene = contentFilesNumberAfterLoadingScene - initialContentFilesNumber;
                Assert.AreEqual(2, contentFilesNumberAfterUnLoadingScene);
#endif
            }
        }
#endif

#if false
        [UnityTest]
        public IEnumerator LoadMultipleSubscenes_Blocking_WithAssetBundles()
        {
            using (var worldA = CreateEntityWorld("World A"))
            using (var worldB = CreateEntityWorld("World B"))
            {
                var sceneSectionStreamingSystemA = worldA.GetExistingSystemManaged<SceneSectionStreamingSystem>();
                var sceneSectionStreamingSystemB = worldA.GetExistingSystemManaged<SceneSectionStreamingSystem>();

                var loadParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                };

#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);

                var worldAScene = SceneSystem.LoadSceneAsync(worldA.Unmanaged, PlayModeSceneGUID, loadParams);
                var worldBScene = SceneSystem.LoadSceneAsync(worldB.Unmanaged, PlayModeSceneGUID, loadParams);
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);
                var initialContentFilesNumber = Loading.ContentLoadInterface.GetContentFiles(Unity.Entities.Content.RuntimeContentManager.Namespace).Length;
                var worldAScene = SceneSystem.LoadSceneAsync(worldA.Unmanaged, BuildSceneGUID, loadParams);
                var worldBScene = SceneSystem.LoadSceneAsync(worldB.Unmanaged, BuildSceneGUID, loadParams);
#endif

                Assert.IsFalse(SceneSystem.IsSceneLoaded(worldA.Unmanaged, worldAScene));
                Assert.IsFalse(SceneSystem.IsSceneLoaded(worldB.Unmanaged, worldBScene));

                worldA.Update();
                while (!sceneSectionStreamingSystemA.AllStreamsComplete)
                {
                    worldA.Update();
                    yield return null;
                }
                worldB.Update();
                while (!sceneSectionStreamingSystemB.AllStreamsComplete)
                {
                    worldB.Update();
                    yield return null;
                }

                Assert.IsTrue(SceneSystem.IsSceneLoaded(worldA.Unmanaged, worldAScene));
                Assert.IsTrue(SceneSystem.IsSceneLoaded(worldB.Unmanaged, worldBScene));

                var worldAEntities = worldA.EntityManager.GetAllEntities(worldA.UpdateAllocator.ToAllocator);
                var worldBEntities = worldB.EntityManager.GetAllEntities(worldB.UpdateAllocator.ToAllocator);
                using (worldAEntities)
                using (worldBEntities)
                {
                    Assert.AreEqual(worldAEntities.Length, worldBEntities.Length);
                }

                var worldAQuery = worldA.EntityManager.CreateEntityQuery(typeof(SharedWithMaterial));
                var worldBQuery = worldB.EntityManager.CreateEntityQuery(typeof(SharedWithMaterial));
                Assert.AreEqual(worldAQuery.CalculateEntityCount(), worldBQuery.CalculateEntityCount());
                Assert.AreEqual(1, worldAQuery.CalculateEntityCount());

                // Get Material on RenderMesh
                var sharedEntitiesA = worldAQuery.ToEntityArray(worldA.UpdateAllocator.ToAllocator);
                var sharedEntitiesB = worldBQuery.ToEntityArray(worldB.UpdateAllocator.ToAllocator);

                SharedWithMaterial sharedA;
                SharedWithMaterial sharedB;
                using (sharedEntitiesA)
                using (sharedEntitiesB)
                {
                    sharedA = worldA.EntityManager.GetSharedComponentManaged<SharedWithMaterial>(sharedEntitiesA[0]);
                    sharedB = worldB.EntityManager.GetSharedComponentManaged<SharedWithMaterial>(sharedEntitiesB[0]);
                }

                Assert.AreSame(sharedA.material, sharedB.material);
                Assert.IsTrue(sharedA.material != null, "sharedA.material != null");

#if !UNITY_EDITOR
                var contentFilesNumberAfterLoadingScene = Loading.ContentLoadInterface.GetContentFiles(Unity.Entities.Content.RuntimeContentManager.Namespace).Length;
#endif

                SceneSystem.UnloadScene(worldA.Unmanaged, worldAScene);
                worldA.Update();

                SceneSystem.UnloadScene(worldB.Unmanaged, worldBScene);
                worldB.Update();

#if !UNITY_EDITOR
                var contentFilesNumberAfterUnLoadingScene = contentFilesNumberAfterLoadingScene - initialContentFilesNumber;
                Assert.AreEqual(2, contentFilesNumberAfterUnLoadingScene);
#endif
            }
        }
#endif

#if !UNITY_DISABLE_MANAGED_COMPONENTS // PostLoadCommandBuffer is a managed component
        private static PostLoadCommandBuffer CreateTestProcessAfterLoadDataCommandBuffer(int value)
        {
            var postLoadCommandBuffer = new PostLoadCommandBuffer();
            postLoadCommandBuffer.CommandBuffer = new EntityCommandBuffer(Allocator.Persistent, PlaybackPolicy.MultiPlayback);
            var postLoadEntity = postLoadCommandBuffer.CommandBuffer.CreateEntity();
            postLoadCommandBuffer.CommandBuffer.AddComponent(postLoadEntity, new TestProcessAfterLoadData {Value = value});
            return postLoadCommandBuffer;
        }
#if false
        [UnityTest]
        public IEnumerator LoadSubscene_With_PostLoadCommandBuffer([Values] bool loadAsync, [Values] bool addCommandBufferToSection)
        {
            var postLoadCommandBuffer = CreateTestProcessAfterLoadDataCommandBuffer(42);

            using (var world = CreateEntityWorld("World"))
            {
#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);
#endif
                if (addCommandBufferToSection)
                {
                    var resolveParams = new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.DisableAutoLoad
                    };
#if UNITY_EDITOR
                    SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, resolveParams);
#else
                    SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, resolveParams);
#endif
                    world.Update();
                    var section = world.EntityManager.CreateEntityQuery(typeof(SceneSectionData)).GetSingletonEntity();
                    world.EntityManager.AddComponentData(section, postLoadCommandBuffer);
                }

                var loadParams = new SceneSystem.LoadParameters
                {
                    Flags = loadAsync ? 0 : SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                };

#if UNITY_EDITOR
                var scene = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, loadParams);
#else
                var scene = SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, loadParams);
#endif

                if (!addCommandBufferToSection)
                    world.EntityManager.AddComponentData(scene, postLoadCommandBuffer);

                if (loadAsync)
                {
                    while (!SceneSystem.IsSceneLoaded(world.Unmanaged, scene))
                    {
                        world.Update();
                        yield return null;
                    }
                }
                else
                {
                    world.Update();
                    Assert.IsTrue(SceneSystem.IsSceneLoaded(world.Unmanaged, scene));
                }

                var ecsTestDataQuery = world.EntityManager.CreateEntityQuery(typeof(TestProcessAfterLoadData));
                Assert.AreEqual(1, ecsTestDataQuery.CalculateEntityCount());
                Assert.AreEqual(43, ecsTestDataQuery.GetSingleton<TestProcessAfterLoadData>().Value);
            }

            // Check that command buffer has been Disposed
            Assert.IsFalse(postLoadCommandBuffer.CommandBuffer.IsCreated);
        }
#endif

#if false
        [Test]
        public void Load_MultipleInstancesOfSameSubScene_By_Instantiating_ResolvedScene()
        {
            var postLoadCommandBuffer1 = CreateTestProcessAfterLoadDataCommandBuffer(42);
            var postLoadCommandBuffer2 = CreateTestProcessAfterLoadDataCommandBuffer(7);

            using (var world = CreateEntityWorld("World"))
            {
#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);
                var resolvedScene = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, new SceneSystem.LoadParameters {AutoLoad = false, Flags = SceneLoadFlags.BlockOnImport});
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);
                var resolvedScene = SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, new SceneSystem.LoadParameters {AutoLoad = false, Flags = SceneLoadFlags.BlockOnImport});
#endif
                world.Update();

                Assert.IsTrue(world.EntityManager.HasComponent<ResolvedSectionEntity>(resolvedScene));

                var loadParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                };

                var scene1 = world.EntityManager.Instantiate(resolvedScene);
                world.EntityManager.AddComponentData(scene1, postLoadCommandBuffer1);
                SceneSystem.LoadSceneAsync(world.Unmanaged, scene1, loadParams);

                var scene2 = world.EntityManager.Instantiate(resolvedScene);
                world.EntityManager.AddComponentData(scene2, postLoadCommandBuffer2);
                SceneSystem.LoadSceneAsync(world.Unmanaged, scene2, loadParams);

                world.Update();

                var ecsTestDataQuery = world.EntityManager.CreateEntityQuery(typeof(TestProcessAfterLoadData));
                using (var ecsTestDataArray = ecsTestDataQuery.ToComponentDataArray<TestProcessAfterLoadData>(world.UpdateAllocator.ToAllocator))
                {
                    CollectionAssert.AreEquivalent(new[] {8, 43}, ecsTestDataArray.ToArray().Select(e => e.Value));
                }

                world.EntityManager.DestroyEntity(scene1);
                world.Update();
                using (var ecsTestDataArray = ecsTestDataQuery.ToComponentDataArray<TestProcessAfterLoadData>(world.UpdateAllocator.ToAllocator))
                {
                    CollectionAssert.AreEquivalent(new[] {8}, ecsTestDataArray.ToArray().Select(e => e.Value));
                }

                world.EntityManager.DestroyEntity(scene2);
                world.Update();
                using (var ecsTestDataArray = ecsTestDataQuery.ToComponentDataArray<TestProcessAfterLoadData>(world.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(0, ecsTestDataArray.Length);
                }
            }
        }
#endif

#if false
        [Test]
        public void Load_MultipleInstancesOfSameSubScene_With_NewInstance_Flag()
        {
            var postLoadCommandBuffer1 = CreateTestProcessAfterLoadDataCommandBuffer(42);
            var postLoadCommandBuffer2 = CreateTestProcessAfterLoadDataCommandBuffer(7);

            using (var world = CreateEntityWorld("World"))
            {
                var loadParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn | SceneLoadFlags.NewInstance
                };

#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);
                var scene1 = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, loadParams);
                world.EntityManager.AddComponentData(scene1, postLoadCommandBuffer1);

                var scene2 = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, loadParams);
                world.EntityManager.AddComponentData(scene2, postLoadCommandBuffer2);

                world.Update();
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);
                var scene1 = SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, loadParams);
                world.EntityManager.AddComponentData(scene1, postLoadCommandBuffer1);

                var scene2 = SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, loadParams);
                world.EntityManager.AddComponentData(scene2, postLoadCommandBuffer2);

                world.Update();
#endif
                var ecsTestDataQuery = world.EntityManager.CreateEntityQuery(typeof(TestProcessAfterLoadData));
                using (var ecsTestDataArray = ecsTestDataQuery.ToComponentDataArray<TestProcessAfterLoadData>(world.UpdateAllocator.ToAllocator))
                {
                    CollectionAssert.AreEquivalent(new[] {8, 43}, ecsTestDataArray.ToArray().Select(e => e.Value));
                }
            }
        }
#endif

        [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
        private partial class Group1 : ComponentSystemGroup {}

        [UpdateBefore(typeof(Group1))]
        [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
        private partial class Group2 : ComponentSystemGroup {}

        [UpdateInGroup(typeof(Group1))]
        [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
        private partial class System1 : SystemBase
        {
            public static int CounterRead;
            protected override void OnUpdate()
            {
                CounterRead = s_Counter++;
            }
        }

        [UpdateInGroup(typeof(Group2))]
        [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
        private partial class System2 : SystemBase
        {
            public static int CounterRead;
            protected override void OnUpdate()
            {
                CounterRead = s_Counter++;
            }
        }

        private static int s_Counter = 0;

#if false
        [Test]
        public void PostProcessAfterLoadGroup_SupportsSystemGroups()
        {
            using (var world = CreateEntityWorld("World"))
            {
                var loadParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn | SceneLoadFlags.NewInstance
                };
#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);
                SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, loadParams);
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);
                SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, loadParams);
#endif
                world.Update();
                Assert.Greater(System1.CounterRead, System2.CounterRead);
            }
        }
#endif

#if false
        [Test]
        public void Load_EnableableComponentsHaveCorrectState()
        {
            using (var world = CreateEntityWorld("World"))
            {
                var loadParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn | SceneLoadFlags.NewInstance
                };
#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);
                SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, loadParams);
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);
                SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, loadParams);
#endif
                world.Update();
                using var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<SingletonTag1>()
                    .Build(world.EntityManager);
                Assert.IsTrue(query.TryGetSingletonEntity<SingletonTag1>(out Entity e));
                Assert.IsFalse(world.EntityManager.IsComponentEnabled<EnableableTag1>(e), "EnableableTag1 should be disabled");
                Assert.IsTrue(world.EntityManager.IsComponentEnabled<EnableableTag2>(e), "EnableableTag2 should be enabled");
                Assert.IsFalse(world.EntityManager.IsComponentEnabled<EnableableTag3>(e), "EnableableTag3 should be disabled");
                Assert.IsTrue(world.EntityManager.IsComponentEnabled<EnableableTag4>(e), "EnableableTag4 should be enabled");
            }
        }
#endif

#if false
        [UnityTest]
        public IEnumerator SubscenesCompleteLoading_When_ConcurrentSectionStreamCountIsSetTo0()
        {
            var postLoadCommandBuffers =
                Enumerable.Range(1, 10).Select(i => CreateTestProcessAfterLoadDataCommandBuffer(i)).ToArray();

            using (var world = CreateEntityWorld("World"))
            {
                var sceneSectionStreamingSystem = world.GetExistingSystemManaged<SceneSectionStreamingSystem>();

                var loadParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.NewInstance | SceneLoadFlags.BlockOnImport
                };

                sceneSectionStreamingSystem.MaximumWorldsMovedPerUpdate = 0;

#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);

                var scenes = postLoadCommandBuffers.Select(cb =>
                {
                    var scene = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, loadParams);
                    world.EntityManager.AddComponentData(scene, cb);
                    return scene;
                }).ToArray();
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);

                var scenes = postLoadCommandBuffers.Select(cb =>
                {
                    var scene = SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, loadParams);
                    world.EntityManager.AddComponentData(scene, cb);
                    return scene;
                }).ToArray();
#endif

                // Increase ConcurrentSectionStreamCount to 10 so all streams are started in the first update
                sceneSectionStreamingSystem.ConcurrentSectionStreamCount = 10;
                world.Update();
                world.GetExistingSystemManaged<SceneSectionStreamingSystem>().ConcurrentSectionStreamCount = 0;
                //All streams should still be in progress
                Assert.AreEqual(10, sceneSectionStreamingSystem.StreamArrayLength);

                while (!sceneSectionStreamingSystem.AllStreamsComplete)
                {
                    world.Update();
                    yield return null;
                }

                var ecsTestDataQuery = world.EntityManager.CreateEntityQuery(typeof(TestProcessAfterLoadData));
                // No scenes should have been moved yet since MaximumMoveEntitiesFromPerFrame is 0
                Assert.AreEqual(0, ecsTestDataQuery.CalculateEntityCount());

                // Move all scenes in one update
                sceneSectionStreamingSystem.MaximumWorldsMovedPerUpdate = 10;
                world.Update();

                //All streams are completed so the stream array should have been resized to ConcurrentSectionStreamCount (0)
                Assert.AreEqual(0, sceneSectionStreamingSystem.StreamArrayLength);
                using (var ecsTestDataArray = ecsTestDataQuery.ToComponentDataArray<TestProcessAfterLoadData>(world.UpdateAllocator.ToAllocator))
                {
                    CollectionAssert.AreEquivalent(Enumerable.Range(2, 10), ecsTestDataArray.ToArray().Select(e => e.Value));
                }
                sceneSectionStreamingSystem.ConcurrentSectionStreamCount = 5;
                Assert.AreEqual(5, sceneSectionStreamingSystem.StreamArrayLength);
            }
        }
#endif

#endif

    }

    public struct TestProcessAfterLoadData : IComponentData
    {
        public int Value;
    }

    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
    public partial struct IncrementEcsTestDataProcessAfterLoadSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var data in SystemAPI.Query<RefRW<TestProcessAfterLoadData>>())
            {
                data.ValueRW.Value++;
            }
        }
    }
}
