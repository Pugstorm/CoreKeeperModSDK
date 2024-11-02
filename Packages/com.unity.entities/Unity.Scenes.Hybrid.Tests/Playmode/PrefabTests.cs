using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes.Hybrid.Tests.Playmode
{
    public class PrefabTests : SubSceneTestFixture
    {
        public PrefabTests()
        {
            // In the editor we can produce an entity file from a prefab directly
            PlayModeScenePath = "Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/TestSceneWithSubScene/TestPrefab.prefab";
            // In a player build we need a scene to reference the prefab with EntityPrefabReference to produce an entity file for the prefab
            BuildScenePath = "Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/TestSceneWithSubScene/TestSceneWithPrefabReferenced.unity";
            // This GUID needs to be hardcoded because the PrefabTests instance running the test is different than the one from the Prebuild/PostBuild steps. And in a player build we can't have access to Editor API to retrieve the GUID from the asset path
            BuildSceneGUID = new Hash128("7cd006e3eb08a624a8fc5c1b782924eb");
        }

        [OneTimeSetUp]
        public void OnetimeSetup()
        {
            base.SetUpOnce();
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            base.TearDownOnce();
        }

#if !UNITY_EDITOR
        private Entity FindPrefabTestEntity(EntityManager entityManager, int prefabIndex)
        {
            using var prefabQuery = entityManager.CreateEntityQuery(typeof(PrefabTestComponent));
            using var prefabTestEntities = prefabQuery.ToEntityArray(Collections.Allocator.Temp);
            using var prefabTestComponents = prefabQuery.ToComponentDataArray<PrefabTestComponent>(Collections.Allocator.Temp);

            // TestSceneWithPrefabReferenced has 2 prefab references.
            Assert.AreEqual(2, prefabTestEntities.Length);
            for (int i = 0; i < prefabTestEntities.Length; ++i)
            {
                if (prefabTestComponents[i].PrefabIndex == prefabIndex)
                    return prefabTestEntities[i];
            }
            return Entity.Null;
        }
#endif

        [UnityTest]
        //No need to run this test in a standalone player. Loading a prefab as a scene is already tested in the next test CanLoadPrefabWithWeakAssetReference
        [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public IEnumerator CanLoadPrefabAsScene()
        {
            using (var world = CreateEntityWorld("World"))
            {
                var em = world.EntityManager;
                var sceneSectionStreamingSystem = world.GetExistingSystemManaged<SceneSectionStreamingSystem>();
                var loadParams = new SceneSystem.LoadParameters { Flags = SceneLoadFlags.BlockOnImport };

                Assert.IsTrue(PlayModeSceneGUID.IsValid);
                var prefabSceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, PlayModeSceneGUID, loadParams);
                world.Update();
                while (!sceneSectionStreamingSystem.AllStreamsComplete)
                {
                    world.Update();
                    yield return null;
                }

                Assert.AreNotEqual(Entity.Null, prefabSceneEntity);
                Assert.IsTrue(SceneSystem.IsSceneLoaded(world.Unmanaged, prefabSceneEntity));

                var ecsTestDataQuery = em.CreateEntityQuery(typeof(SubSceneSectionTestData));
                var ecsTestDataInPrefabQuery = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] {ComponentType.ReadOnly<SubSceneSectionTestData>()},
                    Options = EntityQueryOptions.IncludePrefab
                });
                Assert.AreEqual(0, ecsTestDataQuery.CalculateEntityCount());
                Assert.AreEqual(2, ecsTestDataInPrefabQuery.CalculateEntityCount());
                Assert.IsTrue(em.HasComponent<PrefabRoot>(prefabSceneEntity));
                var prefabRoot = em.GetComponentData<PrefabRoot>(prefabSceneEntity).Root;
                Assert.AreNotEqual(prefabRoot, Entity.Null);
                Assert.IsTrue(em.HasComponent<LinkedEntityGroup>(prefabRoot));
                var instance = em.Instantiate(prefabRoot);
                Assert.AreEqual(2, ecsTestDataQuery.CalculateEntityCount());
                Assert.AreEqual(4, ecsTestDataInPrefabQuery.CalculateEntityCount());
            }
        }

        [UnityTest]
        public IEnumerator CanLoadModelPrefab()
        {
            using var world = CreateEntityWorld("World");
            var em = world.EntityManager;
            var sceneSectionStreamingSystem = world.GetExistingSystemManaged<SceneSectionStreamingSystem>();
            var loadParams = new SceneSystem.LoadParameters { Flags = SceneLoadFlags.BlockOnImport };

#if UNITY_EDITOR
            var modelPrefabGUID = SetupTestScene("Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/TestSceneWithSubScene/TestModel.fbx");
            Assert.IsTrue(modelPrefabGUID.IsValid);
            var prefabReference = new EntityPrefabReference(modelPrefabGUID);
#else
            //Player build test: Retrieve the prefabReference from the scene referencing it.
            Assert.IsTrue(BuildSceneGUID.IsValid);
            SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, loadParams);
            world.Update();
            while (!sceneSectionStreamingSystem.AllStreamsComplete)
            {
                world.Update();
                yield return null;
            }
            var prefabTestEntity = FindPrefabTestEntity(em, 1);
            Assert.AreNotEqual(prefabTestEntity, Entity.Null);
            EntityPrefabReference prefabReference = em.GetComponentData<PrefabTestComponent>(prefabTestEntity).PrefabReference;
            Assert.IsTrue(prefabReference.AssetGUID.IsValid);
#endif

            var prefabSceneEntity = SceneSystem.LoadPrefabAsync(world.Unmanaged, prefabReference, loadParams);
            world.Update();
            while (!sceneSectionStreamingSystem.AllStreamsComplete)
            {
                world.Update();
                yield return null;
            }

            Assert.AreNotEqual(Entity.Null, prefabSceneEntity);
            Assert.IsTrue(SceneSystem.IsSceneLoaded(world.Unmanaged, prefabSceneEntity));

            Assert.IsTrue(em.HasComponent<PrefabRoot>(prefabSceneEntity));
            var prefabRoot = em.GetComponentData<PrefabRoot>(prefabSceneEntity).Root;
            Assert.AreNotEqual(prefabRoot, Entity.Null);
            Assert.IsTrue(em.HasComponent<LinkedEntityGroup>(prefabRoot));
            //Assert.IsTrue(em.HasComponent<PrefabTestMeshComponent>(prefabRoot));
        }

        [UnityTest]
        public IEnumerator CanLoadPrefabWithWeakAssetReference()
        {
            using (var world = CreateEntityWorld("World"))
            {
                var em = world.EntityManager;

                Entity requestEntity = Entity.Null;
                EntityPrefabReference prefabReference = new EntityPrefabReference();
#if UNITY_EDITOR
                //Editor Play mode test: Create an entity and en EntityPrefabReference to load the prefab from its guid
                Assert.IsTrue(PlayModeSceneGUID.IsValid);
                requestEntity = em.CreateEntity();
                prefabReference = new EntityPrefabReference(PlayModeSceneGUID);
#else
                //Player build test: Retrieve the prefabReference from the scene referencing it.
                Assert.IsTrue(BuildSceneGUID.IsValid);
                var streamingSystem = world.GetExistingSystemManaged<SceneSectionStreamingSystem>();
                var loadParams = new SceneSystem.LoadParameters {Flags = SceneLoadFlags.BlockOnImport};
                SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, loadParams);
                world.Update();
                while (!streamingSystem.AllStreamsComplete)
                {
                    world.Update();
                    yield return null;
                }
                requestEntity = FindPrefabTestEntity(em, 0);
                Assert.AreNotEqual(requestEntity, Entity.Null);
                prefabReference = em.GetComponentData<PrefabTestComponent>(requestEntity).PrefabReference;
                Assert.IsTrue(prefabReference.AssetGUID.IsValid);
#endif

                Assert.AreNotEqual(Entity.Null, requestEntity);
                Assert.IsTrue(prefabReference.Id.IsValid);
                em.AddComponentData(requestEntity, new RequestEntityPrefabLoaded {Prefab = prefabReference});
                world.Update();
                Assert.IsTrue(em.HasComponent<PrefabAssetReference>(requestEntity));
                while (!em.HasComponent<PrefabLoadResult>(requestEntity))
                {
                    world.Update();
                    yield return null;
                }

                Assert.IsTrue(em.HasComponent<PrefabAssetReference>(requestEntity));
                var ecsTestDataQuery = em.CreateEntityQuery(typeof(SubSceneSectionTestData));
                var ecsTestDataInPrefabQuery = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] {ComponentType.ReadOnly<SubSceneSectionTestData>()},
                    Options = EntityQueryOptions.IncludePrefab
                });
                Assert.AreEqual(0, ecsTestDataQuery.CalculateEntityCount());
                Assert.AreEqual(2, ecsTestDataInPrefabQuery.CalculateEntityCount());
                var prefabRoot = em.GetComponentData<PrefabLoadResult>(requestEntity).PrefabRoot;
                Assert.AreNotEqual(prefabRoot, Entity.Null);
                Assert.IsTrue(em.HasComponent<LinkedEntityGroup>(prefabRoot));
                var instance = em.Instantiate(prefabRoot);
                Assert.AreEqual(2, ecsTestDataQuery.CalculateEntityCount());
                Assert.AreEqual(4, ecsTestDataInPrefabQuery.CalculateEntityCount());

                // delete request entity and the prefab instance to release reference and unload prefab
                em.DestroyEntity(instance);
                world.Update();
                Assert.AreEqual(0, ecsTestDataQuery.CalculateEntityCount());
                Assert.AreEqual(2, ecsTestDataInPrefabQuery.CalculateEntityCount());

                em.DestroyEntity(requestEntity);
                world.Update();
                Assert.AreEqual(0, ecsTestDataQuery.CalculateEntityCount());
                Assert.AreEqual(0, ecsTestDataInPrefabQuery.CalculateEntityCount());

            }
        }

        [UnityTest]
        public IEnumerator TwoLoadRequestsLoadSamePrefab([Values] bool waitForLoadBetweenRequests)
        {
            using (var world = CreateEntityWorld("World"))
            {
                var em = world.EntityManager;

                Entity requestEntity1 = Entity.Null;
                EntityPrefabReference prefabReference = new EntityPrefabReference();
#if UNITY_EDITOR
                Assert.IsTrue(PlayModeSceneGUID.IsValid);
                requestEntity1 = em.CreateEntity();
                prefabReference = new EntityPrefabReference(PlayModeSceneGUID);
#else
                Assert.IsTrue(BuildSceneGUID.IsValid);
                var loadParams = new SceneSystem.LoadParameters {Flags = SceneLoadFlags.BlockOnImport};
                SceneSystem.LoadSceneAsync(world.Unmanaged, BuildSceneGUID, loadParams);
                world.Update();
                var streamingSystem = world.GetExistingSystemManaged<SceneSectionStreamingSystem>();
                while (!streamingSystem.AllStreamsComplete)
                {
                    world.Update();
                    yield return null;
                }
                requestEntity1 = FindPrefabTestEntity(em, 0);
                Assert.AreNotEqual(requestEntity1, Entity.Null);
                prefabReference = em.GetComponentData<PrefabTestComponent>(requestEntity1).PrefabReference;
                Assert.IsTrue(prefabReference.AssetGUID.IsValid);
#endif

                Assert.AreNotEqual(Entity.Null, requestEntity1);
                Assert.IsTrue(prefabReference.Id.IsValid);
                em.AddComponentData(requestEntity1, new RequestEntityPrefabLoaded {Prefab = prefabReference});

                world.Update();
                if (waitForLoadBetweenRequests)
                {
                    while (!em.HasComponent<PrefabLoadResult>(requestEntity1))
                    {
                        world.Update();
                        yield return null;
                    }
                }

                var requestEntity2 = em.CreateEntity();
                em.AddComponentData(requestEntity2, new RequestEntityPrefabLoaded {Prefab = prefabReference});

                Assert.IsTrue(em.HasComponent<PrefabAssetReference>(requestEntity1));
                world.Update();
                Assert.IsTrue(em.HasComponent<PrefabAssetReference>(requestEntity2));

                if (!waitForLoadBetweenRequests)
                {
                    while (!em.HasComponent<PrefabLoadResult>(requestEntity1))
                    {
                        world.Update();
                        yield return null;
                    }
                }

                Assert.AreEqual(em.GetComponentData<PrefabLoadResult>(requestEntity1).PrefabRoot,
                    em.GetComponentData<PrefabLoadResult>(requestEntity2).PrefabRoot);

                var ecsTestDataQuery = em.CreateEntityQuery(typeof(SubSceneSectionTestData));
                var ecsTestDataInPrefabQuery = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] {ComponentType.ReadOnly<SubSceneSectionTestData>()},
                    Options = EntityQueryOptions.IncludePrefab
                });
                Assert.AreEqual(0, ecsTestDataQuery.CalculateEntityCount());
                Assert.AreEqual(2, ecsTestDataInPrefabQuery.CalculateEntityCount());

                // delete request entity to release reference and unload prefab
                em.DestroyEntity(requestEntity1);
                world.Update();
                Assert.AreEqual(2, ecsTestDataInPrefabQuery.CalculateEntityCount());
                em.DestroyEntity(requestEntity2);
                world.Update();
                Assert.AreEqual(0, ecsTestDataInPrefabQuery.CalculateEntityCount());
            }
        }
	}
}
