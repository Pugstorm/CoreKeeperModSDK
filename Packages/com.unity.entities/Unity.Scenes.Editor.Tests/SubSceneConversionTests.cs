using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Conversion;
using Unity.Entities.Hybrid.Tests;
using Unity.Entities.Tests;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unity.Scenes.Editor.Tests
{
    class SubSceneBakingTests : SubSceneConversionAndBakingTests
    {
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_Settings.Setup(true);
            base.OneTimeSetUp();
        }

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            base.OneTimeTearDown();
            m_Settings.TearDown();
        }

        [Test]
        public void ImportingASubscene_Exports_ExportedTypesFile()
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, () =>
            {
                var go = new GameObject();
                go.AddComponent<MockDataAuthoring>();
                return new List<GameObject> { go };
            });

            var buildSettings = default(Unity.Entities.Hash128);
            var hash = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.Synchronous);
            AssetDatabaseCompatibility.GetArtifactPaths(hash, out var artifactPaths);

            var exportedTypesFile = "";
            foreach (var line in artifactPaths)
            {
                if (Path.GetExtension(line).Replace(".", "") == EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesExportedTypes))
                {
                    exportedTypesFile = line;
                    break;
                }
            }
            Assert.IsFalse(string.IsNullOrEmpty(exportedTypesFile));

            var allLines = File.ReadAllLines(exportedTypesFile);
            var findType = false;
            foreach (var line in allLines)
            {
                if (line.Contains("Unity.Entities.Tests.MockData"))
                {
                    findType = true;
                    break;
                }
            }
            Assert.IsTrue(findType);
        }
    }

    abstract class SubSceneConversionAndBakingTests
    {
        protected TestLiveConversionSettings m_Settings;

        protected TestWithTempAssets m_TempAssets;
        TestWithSubScenes m_SubSceneTest;
        TestWithCustomDefaultGameObjectInjectionWorld m_World;
        private PlayerLoopSystem m_PrevPlayerLoop;
        Texture m_Texture1;
        Texture m_Texture2;

        public void OneTimeSetUp()
        {
            m_PrevPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            m_TempAssets.SetUp();
            m_SubSceneTest.Setup();
            m_World.Setup();
            DefaultWorldInitialization.DefaultLazyEditModeInitialize();
            m_Texture1 = new Texture2D(64, 64);
            AssetDatabase.CreateAsset(m_Texture1, m_TempAssets.GetNextPath("Texture1.asset"));
            m_Texture2 = new Texture2D(32, 32);
            AssetDatabase.CreateAsset(m_Texture2, m_TempAssets.GetNextPath("Texture2.asset"));
        }

        public void OneTimeTearDown()
        {
            m_World.TearDown();
            m_SubSceneTest.TearDown();
            m_TempAssets.TearDown();
            PlayerLoop.SetPlayerLoop(m_PrevPlayerLoop);
        }

        void CheckPublicRefs(EntityManager entityManager)
        {
            using (var query = entityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] {ComponentType.ReadOnly<PublicEntityRef>()},
                }
            ))
            {
                using (var entities = query.ToEntityArray(entityManager.World.UpdateAllocator.ToAllocator))
                {
                    foreach (var entity in entities)
                    {
                        var buf = entityManager.GetBuffer<PublicEntityRef>(entity);
                        for (int i = 0; i < buf.Length; ++i)
                        {
                            Assert.IsTrue(entityManager.Exists(buf[i].targetEntity),
                                $"Entity at index {i} doesn't exist: {buf[i].targetEntity}");
                        }
                    }
                }
            }
        }

        [Test]
        public void SubScene_WithDependencyOnAssetInScene_ClearCache_EndToEnd()
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, () =>
            {
                var go = new GameObject();
                var authoring = go.AddComponent<DependencyTestAuthoring>();
                var sphereHolder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                authoring.Asset = sphereHolder.GetComponent<MeshFilter>().sharedMesh;
                UnityEngine.Object.DestroyImmediate(sphereHolder);
                return new List<GameObject> { go };
            });

            var buildSettings = default(Unity.Entities.Hash128);
            var originalHash = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.Synchronous);
            Assert.IsTrue(originalHash.IsValid);

            // Clear Cache (First time this creates global dependency asset, so we will test both steps)
            EntitiesCacheUtility.UpdateEntitySceneGlobalDependency();

            var newHashCreated = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.Synchronous);
            Assert.IsTrue(newHashCreated.IsValid);
            Assert.AreNotEqual(originalHash, newHashCreated);

            // Clear Cache (This updates existing asset)
            EntitiesCacheUtility.UpdateEntitySceneGlobalDependency();

            var newHashUpdated = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.Synchronous);
            Assert.IsTrue(newHashUpdated.IsValid);
            Assert.AreNotEqual(newHashCreated, newHashUpdated);
            Assert.AreNotEqual(originalHash, newHashUpdated);

            // Delete created dependency, this cleans up test but also we need to verify that the scene returns to the original hash
            AssetDatabase.DeleteAsset(EntitiesCacheUtility.globalEntitiesDependencyDir);
            AssetDatabase.Refresh();

            // With the dependency deleted, the hash should return to the original
            var finalHash = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.Synchronous);
            Assert.AreEqual(originalHash, finalHash);
        }

        [Test]
        public void SubScene_WithDependencyOnAsset_IsInvalidatedWhenAssetChanges()
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, () =>
            {
                var go = new GameObject();
                var authoring = go.AddComponent<DependencyTestAuthoring>();
                authoring.Asset = m_Texture1;
                return new List<GameObject> { go };
            });

            var buildSettings = default(Unity.Entities.Hash128);
            var hash = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.Synchronous);
            Assert.IsTrue(hash.IsValid);

            m_Texture1.wrapMode = m_Texture1.wrapMode == TextureWrapMode.Repeat ? TextureWrapMode.Mirror : TextureWrapMode.Repeat;
            AssetDatabase.SaveAssets();

            var newHash = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.NoImport);
            Assert.AreNotEqual(hash, newHash);
            Assert.IsFalse(newHash.IsValid);
        }

        [Test]
        public void SubScene_WithoutContents_ImportsAndLoadsAndUnloads([Values]TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, null);
            subScene.AutoLoadScene = false;
            subScene.gameObject.SetActive(false);

            using (var world = TestWorldSetup.CreateEntityWorld("World", testFilterFlags))
            {
                var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID,
                    new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                    });

                // TODO: Editor doesn't update if it doesn't have focus, so we must explicitly update the world to process the load.
                world.Update();

                Assert.IsTrue(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity), "Failed to load scene");
                CheckPublicRefs(world.EntityManager);

                SceneSystem.UnloadScene(world.Unmanaged, sceneEntity);

                world.Update();

                Assert.IsFalse(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity), "Failed to unload scene");
            }
        }

        [Test]
        [Ignore("Unstable on CI, DOTS-3750")]
        public void SubScene_WithoutContents_DeletingSceneAssetUnloadsScene([Values]TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, null);
            subScene.AutoLoadScene = false;
            subScene.gameObject.SetActive(false);

            using (var world = TestWorldSetup.CreateEntityWorld("World", testFilterFlags))
            {
                var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID,
                    new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                    });

                Assert.IsFalse(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity),
                    "Scene should not be loaded yet.");

                // TODO: Editor doesn't update if it doesn't have focus, so we must explicitly update the world to process the load.
                world.Update();
                Assert.IsTrue(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity), "Failed to load scene");
                CheckPublicRefs(world.EntityManager);

                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(subScene.SceneAsset));

                // Block the import of this subscene so that we can get a single-frame result for this test
                var hash = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID,
                    world.EntityManager.GetComponentData<SceneSystemData>(world.GetOrCreateSystem<SceneSystem>())
                        .BuildConfigurationGUID,
                    true,
                    ImportMode.Synchronous);
                Assert.IsTrue(hash.IsValid, "Failed to import SubScene.");

                LogAssert.Expect(LogType.Error, new Regex("Loading Entity Scene failed.*"));

                // TODO: Editor doesn't update if it doesn't have focus, so we must explicitly update the world to process the load.
                world.Update();

                Assert.IsFalse(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity), "Scene should not be loaded");
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void SubScene_WithNullAsset_ImportsAndLoads([Values]TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
            => SubScene_WithAsset_ImportsAndLoads(null, testFilterFlags);

        [Test]
        public void SubScene_WithTextureAsset_ImportsAndLoads([Values]TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
            => SubScene_WithAsset_ImportsAndLoads(m_Texture1, testFilterFlags);

        void SubScene_WithAsset_ImportsAndLoads(UnityEngine.Object asset, TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, () =>
            {
                var go = new GameObject("GO1");
                var managed = go.AddComponent<SubSceneLoadTestAssetAuthoring>();
                managed.Asset = asset;
                return new List<GameObject> { go };
            });
            subScene.AutoLoadScene = false;
            subScene.gameObject.SetActive(false);

            using (var world = TestWorldSetup.CreateEntityWorld("World", testFilterFlags))
            {
                var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID,
                    new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                    });

                // TODO: Editor doesn't update if it doesn't have focus, so we must explicitly update the world to process the load.
                world.Update();

                Assert.IsTrue(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity), "Failed to load scene");
                CheckPublicRefs(world.EntityManager);

                {
                    var entities = DebugEntity.GetAllEntitiesWithSystems(world.EntityManager);
                    var entity = entities.FirstOrDefault(de => de.HasComponent<SubSceneLoadTestAssetComponent>());
                    Assert.IsNotNull(entity, "Failed to find converted GameObject");

                    var component = world.EntityManager.GetComponentData<SubSceneLoadTestAssetComponent>(entity.Entity);
                    Assert.AreEqual(asset, component.Asset);
                }

                SceneSystem.UnloadScene(world.Unmanaged, sceneEntity);
            }
        }

#endif

        [Test]
        public void SubScene_WithNullBlobAsset_ImportsAndLoads([Values]TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
            => SubScene_WithBlobAsset_ImportsAndLoads_Impl(false, testFilterFlags);

        [Test]
        public void SubScene_WithBlobAsset_ImportsAndLoads([Values]TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
            => SubScene_WithBlobAsset_ImportsAndLoads_Impl(true, testFilterFlags);

        void SubScene_WithBlobAsset_ImportsAndLoads_Impl(bool useBlobAsset, TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, () =>
            {
                var go = new GameObject("GO1");
                var component = go.AddComponent<SubSceneLoadTestBlobAssetAuthoring>();
                component.Int = 1;
                component.PtrInt = 2;
                component.String = "Test";
                component.Strings = new[] { "A", null, "B" };
                component.UseNullBlobAsset = !useBlobAsset;
                return new List<GameObject> { go };
            });
            subScene.AutoLoadScene = false;
            subScene.gameObject.SetActive(false);

            using (var world = TestWorldSetup.CreateEntityWorld("World", testFilterFlags))
            {
                var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID,
                    new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                    });

                // TODO: Editor doesn't update if it doesn't have focus, so we must explicitly update the world to process the load.
                world.Update();

                Assert.IsTrue(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity), "Failed to load scene");
                CheckPublicRefs(world.EntityManager);

                {
                    var entities = DebugEntity.GetAllEntitiesWithSystems(world.EntityManager);
                    var entity = entities.FirstOrDefault(de => de.HasComponent<SubSceneLoadTestBlobAssetComponent>());
                    Assert.IsNotNull(entity, "Failed to find converted GameObject");

                    var component =
                        world.EntityManager.GetComponentData<SubSceneLoadTestBlobAssetComponent>(entity.Entity);
                    if (useBlobAsset)
                    {
                        Assert.AreEqual(1, component.BlobAsset.Value.Int);
                        Assert.AreEqual(2, component.BlobAsset.Value.Ptr.Value);
                        Assert.AreEqual("Test", component.BlobAsset.Value.String.ToString());
                        Assert.AreEqual(3, component.BlobAsset.Value.Strings.Length);
                        Assert.AreEqual("A", component.BlobAsset.Value.Strings[0].ToString());
                        Assert.AreEqual("", component.BlobAsset.Value.Strings[1].ToString());
                        Assert.AreEqual("B", component.BlobAsset.Value.Strings[2].ToString());
                    }
                    else
                    {
                        // TODO: this sometimes fails
                        Assert.AreEqual(BlobAssetReference<SubSceneLoadTestBlobAsset>.Null, component.BlobAsset,
                            "Blob asset should be null");
                        Assert.IsFalse(component.BlobAsset.IsCreated, "Blob asset should not be created, but it was");
                    }
                }

                SceneSystem.UnloadScene(world.Unmanaged, sceneEntity);
            }
        }

        [Test]
        public void SubScene_WithUnmanagedComponent_ImportsAndLoads([Values]TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, () =>
            {
                var go = new GameObject("GO1");
                var unmanaged = go.AddComponent<SubSceneLoadTestUnmanagedAuthoring>();
                unmanaged.Int = 1;
                unmanaged.Entity = go;
                return new List<GameObject> { go };
            });
            subScene.AutoLoadScene = false;
            subScene.gameObject.SetActive(false);

            using (var world = TestWorldSetup.CreateEntityWorld("World", testFilterFlags))
            {
                var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID,
                    new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                    });

                // TODO: Editor doesn't update if it doesn't have focus, so we must explicitly update the world to process the load.
                world.Update();

                Assert.IsTrue(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity), "Failed to load scene");
                CheckPublicRefs(world.EntityManager);

                {
                    var query = world.EntityManager.CreateEntityQuery(typeof(SubSceneLoadTestUnmanagedComponent));
                    var comp = query.GetSingleton<SubSceneLoadTestUnmanagedComponent>();
                    var entity = query.GetSingletonEntity();
                    Assert.AreEqual(1, comp.Int);
                    Assert.AreEqual(entity, comp.Entity);
                    VerifyBlobAsset(comp.BlobAsset, 1, "GO1", 1, "GO blob asset");
                }
            }
        }

        [Test]
        public void SubScene_WithSharedComponent_ImportsAndLoads([Values]TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, () =>
            {
                var go = new GameObject("GO1");
                var comp = go.AddComponent<SubSceneLoadTestSharedAuthoring>();
                comp.Int = 1;
                comp.String = "Test";
                comp.Asset = m_Texture1;
                return new List<GameObject> { go };
            });
            subScene.AutoLoadScene = false;
            subScene.gameObject.SetActive(false);

            using (var world = TestWorldSetup.CreateEntityWorld("World", testFilterFlags))
            {
                var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID,
                    new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                    });

                // TODO: Editor doesn't update if it doesn't have focus, so we must explicitly update the world to process the load.
                world.Update();

                Assert.IsTrue(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity), "Failed to load scene");
                CheckPublicRefs(world.EntityManager);

                {
                    var query = world.EntityManager.CreateEntityQuery(typeof(SubSceneLoadTestSharedComponent));
                    var entity = query.GetSingletonEntity();
                    var comp = world.EntityManager.GetSharedComponentManaged<SubSceneLoadTestSharedComponent>(entity);
                    Assert.AreEqual(1, comp.Int);
                    Assert.AreEqual("Test", comp.String);
                    Assert.AreEqual(m_Texture1, comp.Asset);
                }
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void SubScene_WithComplexComponents_ImportsAndLoads([Values]TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, () =>
            {
                var go1 = new GameObject("GO1");
                {
                    var unmanaged = go1.AddComponent<SubSceneLoadTestUnmanagedAuthoring>();
                    unmanaged.Int = 1;
                    unmanaged.Entity = go1;

                    var shared = go1.AddComponent<SubSceneLoadTestSharedAuthoring>();
                    shared.Int = 42;
                    shared.String = "Test";
                    shared.Asset = m_Texture2;

                    var buffer = go1.AddComponent<SubSceneLoadTestBufferAuthoring>();
                    buffer.Ints = new List<int>();
                    buffer.Entities = new List<GameObject>();
                }
                go1.AddComponent<TestComponentAuthoring>();

                var go2 = new GameObject("GO2");
                {
                    var unmanaged = go2.AddComponent<SubSceneLoadTestUnmanagedAuthoring>();
                    unmanaged.Int = 2;
                    unmanaged.Entity = go2;

                    var managed = go2.AddComponent<SubSceneLoadTestManagedAuthoring>();
                    managed.Int = 2;
                    managed.String = "Test2";
                    managed.Entity = go2;
                    managed.Asset = m_Texture1;

                    var shared = go2.AddComponent<SubSceneLoadTestSharedAuthoring>();
                    shared.Int = 42;
                    shared.String = "Test";
                    shared.Asset = m_Texture1;
                }

                var go3 = new GameObject("GO3");
                {
                    var managed = go3.AddComponent<SubSceneLoadTestManagedAuthoring>();
                    managed.Int = 3;
                    managed.String = "Test3";
                    managed.Entity = go3;
                    managed.Asset = m_Texture1;

                    var shared = go3.AddComponent<SubSceneLoadTestSharedAuthoring>();
                    shared.Int = 42;
                    shared.String = "Test Different";
                    shared.Asset = m_Texture1;

                    var buffer = go3.AddComponent<SubSceneLoadTestBufferAuthoring>();
                    buffer.Ints = new List<int> { 5 };
                    buffer.Entities = new List<GameObject> { go2 };
                }

                var go4 = new GameObject("GO4");
                {
                    var unmanaged = go4.AddComponent<SubSceneLoadTestUnmanagedAuthoring>();
                    unmanaged.Int = 4;
                    unmanaged.Entity = go4;

                    var managed = go4.AddComponent<SubSceneLoadTestManagedAuthoring>();
                    managed.Int = 4;
                    managed.String = "Test4";
                    managed.Entity = go4;
                    managed.Asset = m_Texture1;

                    var buffer = go4.AddComponent<SubSceneLoadTestBufferAuthoring>();
                    buffer.Ints = new List<int> { 3, 17, 2 };
                    buffer.Entities = new List<GameObject> { go1.gameObject, go2.gameObject, go3.gameObject };
                }

                return new List<GameObject> { go1, go2, go3, go4 };
            });

            subScene.AutoLoadScene = false;
            subScene.gameObject.SetActive(false);

            using (var world = TestWorldSetup.CreateEntityWorld("World", testFilterFlags))
            {
                ref var state = ref world.Unmanaged.GetExistingSystemState<SceneSystem>();

                var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID,
                    new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                    });

                // TODO: Editor doesn't update if it doesn't have focus, so we must explicitly update the world to process the load.
                world.Update();

                Assert.IsTrue(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity), "Failed to load scene");
                CheckPublicRefs(world.EntityManager);

                {
                    var entities = DebugEntity.GetAllEntitiesWithSystems(world.EntityManager);
                    var unmanagedQuery =
                        world.EntityManager.CreateEntityQuery(typeof(SubSceneLoadTestUnmanagedComponent));
                    var managedQuery = world.EntityManager.CreateEntityQuery(typeof(SubSceneLoadTestManagedComponent));
                    var sharedQuery = world.EntityManager.CreateEntityQuery(typeof(SubSceneLoadTestSharedComponent));
                    var bufferQuery = world.EntityManager.CreateEntityQuery(typeof(SubSceneLoadTestBufferComponent));

                    Assert.AreEqual(3, unmanagedQuery.CalculateEntityCount(),
                        $"Expected exactly 3 entities with {typeof(SubSceneLoadTestUnmanagedComponent).FullName}");
                    Assert.AreEqual(3, managedQuery.CalculateEntityCount(),
                        $"Expected exactly 3 entities with {typeof(SubSceneLoadTestManagedComponent).FullName}");
                    Assert.AreEqual(3, sharedQuery.CalculateEntityCount(),
                        $"Expected exactly 3 entities with {typeof(SubSceneLoadTestSharedComponent).FullName}");
                    Assert.AreEqual(3, bufferQuery.CalculateEntityCount(),
                        $"Expected exactly 3 entities with {typeof(SubSceneLoadTestBufferComponent).FullName}");


                    var e1 = entities.FirstOrDefault(de =>
                        de.HasComponent<SubSceneLoadTestUnmanagedComponent>() &&
                        de.HasComponent<SubSceneLoadTestSharedComponent>() &&
                        de.HasComponent<SubSceneLoadTestBufferComponent>());
                    Assert.IsNotNull(e1, "Could not find entity corresponding to GO1");
                    var e2 = entities.FirstOrDefault(de => de.HasComponent<SubSceneLoadTestUnmanagedComponent>()
                                                           && de.HasComponent<SubSceneLoadTestManagedComponent>()
                                                           && de.HasComponent<SubSceneLoadTestSharedComponent>());
                    Assert.IsNotNull(e2, "Could not find entity corresponding to GO2");
                    var e3 = entities.FirstOrDefault(de =>
                        de.HasComponent<SubSceneLoadTestManagedComponent>() &&
                        de.HasComponent<SubSceneLoadTestSharedComponent>() &&
                        de.HasComponent<SubSceneLoadTestBufferComponent>());
                    Assert.IsNotNull(e3, "Could not find entity corresponding to GO3");
                    var e4 = entities.FirstOrDefault(de => de.HasComponent<SubSceneLoadTestUnmanagedComponent>()
                                                           && de.HasComponent<SubSceneLoadTestManagedComponent>()
                                                           && de.HasComponent<SubSceneLoadTestBufferComponent>());
                    Assert.IsNotNull(e4, "Could not find entity corresponding to GO4");

                    {
                        var unmanaged =
                            world.EntityManager.GetComponentData<SubSceneLoadTestUnmanagedComponent>(e1.Entity);
                        Assert.AreEqual(e1.Entity, unmanaged.Entity);
                        Assert.AreEqual(1, unmanaged.Int);
                        VerifyBlobAsset(unmanaged.BlobAsset, 1, "GO1", 1, "GO1-unmanaged");

                        var shared =
                            world.EntityManager.GetSharedComponentManaged<SubSceneLoadTestSharedComponent>(e1.Entity);
                        Assert.AreEqual(42, shared.Int);
                        Assert.AreEqual("Test", shared.String);
                        Assert.AreEqual(m_Texture2, shared.Asset);

                        var buffer = world.EntityManager.GetBuffer<SubSceneLoadTestBufferComponent>(e1.Entity);
                        Assert.AreEqual(0, buffer.Length);
                    }

                    {
                        var unmanaged =
                            world.EntityManager.GetComponentData<SubSceneLoadTestUnmanagedComponent>(e2.Entity);
                        Assert.AreEqual(e2.Entity, unmanaged.Entity);
                        Assert.AreEqual(2, unmanaged.Int);
                        VerifyBlobAsset(unmanaged.BlobAsset, 2, "GO2", 1, "GO2-unmanaged");

                        var managed = world.EntityManager.GetComponentData<SubSceneLoadTestManagedComponent>(e2.Entity);
                        Assert.AreEqual(e2.Entity, managed.Entity);
                        Assert.AreEqual(2, managed.Int);
                        Assert.AreEqual("Test2", managed.String);
                        Assert.AreEqual(m_Texture1, managed.Asset);

                        var shared =
                            world.EntityManager.GetSharedComponentManaged<SubSceneLoadTestSharedComponent>(e2.Entity);
                        Assert.AreEqual(42, shared.Int);
                        Assert.AreEqual("Test", shared.String);
                        Assert.AreEqual(m_Texture1, shared.Asset);
                    }

                    {
                        var managed = world.EntityManager.GetComponentData<SubSceneLoadTestManagedComponent>(e3.Entity);
                        Assert.AreEqual(e3.Entity, managed.Entity);
                        Assert.AreEqual(3, managed.Int);
                        Assert.AreEqual("Test3", managed.String);
                        Assert.AreEqual(m_Texture1, managed.Asset);

                        var shared =
                            world.EntityManager.GetSharedComponentManaged<SubSceneLoadTestSharedComponent>(e3.Entity);
                        Assert.AreEqual(42, shared.Int);
                        Assert.AreEqual("Test Different", shared.String);
                        Assert.AreEqual(m_Texture1, shared.Asset);

                        var buffer = world.EntityManager.GetBuffer<SubSceneLoadTestBufferComponent>(e3.Entity);
                        Assert.AreEqual(1, buffer.Length);
                        Assert.AreEqual(e2.Entity, buffer[0].Entity);
                        Assert.AreEqual(5, buffer[0].Int);
                        VerifyBlobAsset(buffer[0].BlobAsset, 5, "GO30", 0, "GO3-buffer0");
                    }

                    {
                        var unmanaged =
                            world.EntityManager.GetComponentData<SubSceneLoadTestUnmanagedComponent>(e4.Entity);
                        Assert.AreEqual(e4.Entity, unmanaged.Entity);
                        Assert.AreEqual(4, unmanaged.Int);
                        VerifyBlobAsset(unmanaged.BlobAsset, 4, "GO4", 1, "GO4-unmanaged");

                        var managed = world.EntityManager.GetComponentData<SubSceneLoadTestManagedComponent>(e4.Entity);
                        Assert.AreEqual(e4.Entity, managed.Entity);
                        Assert.AreEqual(4, managed.Int);
                        Assert.AreEqual("Test4", managed.String);
                        Assert.AreEqual(m_Texture1, managed.Asset);

                        var buffer = world.EntityManager.GetBuffer<SubSceneLoadTestBufferComponent>(e4.Entity);
                        Assert.AreEqual(3, buffer.Length);

                        Assert.AreEqual(e1.Entity, buffer[0].Entity);
                        Assert.AreEqual(3, buffer[0].Int);
                        VerifyBlobAsset(buffer[0].BlobAsset, 3, "GO40", 0, "GO4-buffer0");

                        Assert.AreEqual(e2.Entity, buffer[1].Entity);
                        Assert.AreEqual(17, buffer[1].Int);
                        VerifyBlobAsset(buffer[1].BlobAsset, 17, "GO41", 1, "GO4-buffer1");

                        Assert.AreEqual(e3.Entity, buffer[2].Entity);
                        Assert.AreEqual(2, buffer[2].Int);
                        VerifyBlobAsset(buffer[2].BlobAsset, 2, "GO42", 2, "GO4-buffer2");
                    }
                }

                SceneSystem.UnloadScene(world.Unmanaged, sceneEntity);
                Assert.IsFalse(SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity), "Failed to unload scene");
            }
        }

        #endif

        static void VerifyBlobAsset(BlobAssetReference<SubSceneLoadTestBlobAsset> asset, int n, string str, int arrayLength, string assertReference)
        {
            Assert.AreNotEqual(BlobAssetReference<SubSceneLoadTestBlobAsset>.Null, asset, $"The blob asset {assertReference} should not be null");
            Assert.AreEqual(n, asset.Value.Int, $"The int stored on {assertReference} wasn't the expected value");
            Assert.AreEqual(str, asset.Value.String.ToString(), $"The string stored on {assertReference} wasn't the expected value");
            Assert.AreEqual(n + 1, asset.Value.Ptr.Value, $"The int stored on {assertReference} via a pointer wasn't the expected value");
            Assert.AreEqual(arrayLength, asset.Value.Strings.Length, $"The array of strings stored on {assertReference} wasn't of the expected length");
            for (int i = 0; i < arrayLength; i++)
                Assert.AreEqual(i.ToString(), asset.Value.Strings[i].ToString(), $"The {i}th string stored on {assertReference} wasn't the expected value");
        }

        [Test]
        public void SubScene_WithDependencyOnAssetInScene_StillImports()
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, () =>
            {
                var go = new GameObject();
                var authoring = go.AddComponent<DependencyTestAuthoring>();
                var texture = new Texture2D(64, 64);
                authoring.Asset = texture;
                return new List<GameObject> { go };
            });

            var buildSettings = default(Unity.Entities.Hash128);
            var hash = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.Synchronous);
            Assert.IsTrue(hash.IsValid);
        }

        [Test]
        public void SubScene_WithDependencyOnBuiltInAsset_StillImports()
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, () =>
            {
                var go = new GameObject();
                var authoring = go.AddComponent<DependencyTestAuthoring>();
                var sphereHolder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                authoring.Asset = sphereHolder.GetComponent<MeshFilter>().sharedMesh;
                UnityEngine.Object.DestroyImmediate(sphereHolder);
                return new List<GameObject> { go };
            });

            var buildSettings = default(Unity.Entities.Hash128);
            var hash = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.Synchronous);
            Assert.IsTrue(hash.IsValid);
        }

        [Test]
        public void SubScene_WithDependencyOnAssetInScene_Reimport_EndToEnd()
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SubScene", false, () =>
            {
                var go = new GameObject();
                var authoring = go.AddComponent<DependencyTestAuthoring>();
                var sphereHolder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                authoring.Asset = sphereHolder.GetComponent<MeshFilter>().sharedMesh;
                UnityEngine.Object.DestroyImmediate(sphereHolder);
                return new List<GameObject> { go };
            });

            var buildSettings = default(Unity.Entities.Hash128);
            var originalHash = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.Synchronous);
            Assert.IsTrue(originalHash.IsValid);

            SubSceneInspectorUtility.ForceReimport(new []{subScene});

            var newHashCreated = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.Synchronous);
            Assert.IsTrue(newHashCreated.IsValid);
            Assert.AreNotEqual(originalHash, newHashCreated);

            SubSceneInspectorUtility.ForceReimport(new []{subScene});

            var newHashUpdated = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.Synchronous);
            Assert.IsTrue(newHashUpdated.IsValid);
            Assert.AreNotEqual(newHashCreated, newHashUpdated);
            Assert.AreNotEqual(originalHash, newHashUpdated);
        }

        [Test]
        public void SubScene_NegativeSceneSectionValuesGetStripped_EndToEnd([Values]TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "NegativeSceneSection", false, () =>
            {
                var go0 = new GameObject();
                var authoring0 = go0.AddComponent<SceneSectionValueAuthoring>();
                authoring0.Value = -1;

                var go1 = new GameObject();
                var authoring1 = go1.AddComponent<SceneSectionValueAuthoring>();
                authoring1.Value = 1;

                return new List<GameObject> { go0, go1 };
            });

            SubSceneInspectorUtility.ForceReimport(new []{subScene});

            {
                using (var world = TestWorldSetup.CreateEntityWorld("World", testFilterFlags))
                {
                    var manager = world.EntityManager;

                    var resolveParams = new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                    };

                    Unity.Entities.Hash128 sceneGuid = AssetDatabaseCompatibility.PathToGUID(AssetDatabase.GetAssetPath(subScene.SceneAsset));
                    var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, sceneGuid, resolveParams);
                    world.Update();

                    EntitiesAssert.Contains(manager, EntityMatch.Partial(new SceneSectionValue(1)));
                }
            }
        }

        [Test]
        public void SubScene_WithLongName_ImportsAndLoads([Values]TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
        {
            string sceneName = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, sceneName, false, () =>
            {
                var go = new GameObject("GO");
                var component = go.AddComponent<SceneSectionValueAuthoring>();
                component.Value = 1;
                return new List<GameObject> { go };
            });
            subScene.AutoLoadScene = false;
            subScene.gameObject.SetActive(false);

            SubSceneInspectorUtility.ForceReimport(new []{subScene});

            {
                using (var world = TestWorldSetup.CreateEntityWorld("World", testFilterFlags))
                {
                    var manager = world.EntityManager;

                    var resolveParams = new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                    };

                    Unity.Entities.Hash128 sceneGuid = AssetDatabaseCompatibility.PathToGUID(AssetDatabase.GetAssetPath(subScene.SceneAsset));
                    var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, sceneGuid, resolveParams);
                    world.Update();

                    EntitiesAssert.Contains(manager, EntityMatch.Partial(new SceneSectionValue(1)));
                }
            }

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SubScene_GetSceneStreamingState([Values]TestWorldSetup.TestWorldSystemFilterFlags testFilterFlags)
        {
            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SceneStreamingState", false, () =>
            {
                var go0 = new GameObject("GO_0");
                var component = go0.AddComponent<SceneSectionComponent>();
                go0.AddComponent<DependencyTestAuthoring>();
                component.SectionIndex = 0;

                var go1 = new GameObject("GO_1");
                component = go1.AddComponent<SceneSectionComponent>();
                go1.AddComponent<DependencyTestAuthoring>();
                component.SectionIndex = 1;

                var go2 = new GameObject("GO_2");
                component = go2.AddComponent<SceneSectionComponent>();
                go2.AddComponent<DependencyTestAuthoring>();
                component.SectionIndex = 2;

                return new List<GameObject> { go0, go1, go2 };
            });
            subScene.AutoLoadScene = false;
            subScene.gameObject.SetActive(false);

            SubSceneInspectorUtility.ForceReimport(new []{subScene});

            {
                using (var world = TestWorldSetup.CreateEntityWorld("World", testFilterFlags))
                using (var queryDependencyTest = world.EntityManager.CreateEntityQuery(new EntityQueryDesc
                           {
                               All = new[] {ComponentType.ReadOnly<ConversionDependencyData>()},
                           }
                       ))
                {
                    var manager = world.EntityManager;

                    var resolveParamsNoAutoload = new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn | SceneLoadFlags.DisableAutoLoad
                    };

                    var resolveParamsAutoload = new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                    };

                    // Invalid entity
                    Entity sceneEntity = default;
                    Assert.AreEqual(SceneSystem.SceneStreamingState.Unloaded, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));

                    Unity.Entities.Hash128 sceneGuid = AssetDatabaseCompatibility.PathToGUID(AssetDatabase.GetAssetPath(subScene.SceneAsset));
                    sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, sceneGuid, resolveParamsNoAutoload);
                    Assert.AreEqual(SceneSystem.SceneStreamingState.Loading, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));
                    world.Update();
                    Assert.AreEqual(SceneSystem.SceneStreamingState.LoadedSectionEntities, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));
                    Assert.AreEqual(0, queryDependencyTest.CalculateEntityCount());

                    // Test unloading from SceneSystem.SceneStreamingState.LoadedSectionEntities
                    SceneSystem.UnloadScene(world.Unmanaged, sceneEntity, SceneSystem.UnloadParameters.DestroyMetaEntities);
                    world.Update();
                    Assert.AreEqual(SceneSystem.SceneStreamingState.Unloaded, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));
                    Assert.AreEqual(0, queryDependencyTest.CalculateEntityCount());

                    // Test loading the whole scene from SceneSystem.SceneStreamingState.LoadedSectionEntities
                    sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, sceneGuid, resolveParamsNoAutoload);
                    world.Update();
                    Assert.AreEqual(SceneSystem.SceneStreamingState.LoadedSectionEntities, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));
                    Assert.AreEqual(0, queryDependencyTest.CalculateEntityCount());

                    SceneSystem.LoadSceneAsync(world.Unmanaged, sceneEntity, resolveParamsAutoload);
                    world.Update();
                    Assert.AreEqual(SceneSystem.SceneStreamingState.LoadedSuccessfully, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));
                    Assert.AreEqual(3, queryDependencyTest.CalculateEntityCount());

                    // Test unloading the content, but leaving the scene and section entities
                    SceneSystem.UnloadScene(world.Unmanaged, sceneEntity);
                    world.Update();
                    Assert.AreEqual(SceneSystem.SceneStreamingState.LoadedSectionEntities, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));
                    Assert.AreEqual(0, queryDependencyTest.CalculateEntityCount());

                    // Test loading one section individually
                    var sections = manager.GetBuffer<ResolvedSectionEntity>(sceneEntity, true);
                    var section0Entity = sections[0].SectionEntity;
                    var section1Entity = sections[1].SectionEntity;
                    manager.AddComponentData(section0Entity, new RequestSceneLoaded {LoadFlags = resolveParamsAutoload.Flags });
                    Assert.AreEqual(SceneSystem.SectionStreamingState.LoadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                    Assert.AreEqual(SceneSystem.SceneStreamingState.Loading, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));
                    world.Update();
                    Assert.AreEqual(SceneSystem.SceneStreamingState.LoadedSuccessfully, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));
                    Assert.AreEqual(SceneSystem.SectionStreamingState.Loaded, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                    Assert.AreEqual(1, queryDependencyTest.CalculateEntityCount());

                    // Test unloading one section individually
                    manager.RemoveComponent<RequestSceneLoaded>(section0Entity);
                    Assert.AreEqual(SceneSystem.SectionStreamingState.UnloadRequested, SceneSystem.GetSectionStreamingState(world.Unmanaged, section0Entity));
                    world.Update();
                    Assert.AreEqual(SceneSystem.SceneStreamingState.LoadedSectionEntities, SceneSystem.GetSceneStreamingState(world.Unmanaged, sceneEntity));
                    Assert.AreEqual(0, queryDependencyTest.CalculateEntityCount());
                }
            }

            LogAssert.NoUnexpectedReceived();
        }

        // ReSharper disable once InconsistentNaming
        class RegressionTest_UUM56543_AuthoringComponent : MonoBehaviour
        {
            class Baker : Baker<RegressionTest_UUM56543_AuthoringComponent>
            {
                public override void Bake(RegressionTest_UUM56543_AuthoringComponent authoring)
                {
                    var entities = new NativeArray<Entity>(10, Allocator.Temp);
                    CreateAdditionalEntities(entities, TransformUsageFlags.None);

                    // There's a very high chance that the entities are already sorted by indices, but we
                    // cannot rely on it so we search the highest index instead of taking the last one.

                    var highestIndex = entities[0];
                    for (int i = 1; i < entities.Length; i++)
                    {
                        if (entities[i].Index > highestIndex.Index)
                        {
                            highestIndex = entities[i];
                        }
                    }

                    // Putting the highest index entity in a section above zero will require the remapping
                    // table for external references to exceed the highest index in the section itself.
                    // This case wasn't handled properly and caused the issue UUM-56543.

                    AddSharedComponent(highestIndex, new SceneSection { SceneGUID = GetSceneGUID(), Section = 1 });
                }
            }
        }

        [Test]
        public void RegressionTest_UUM56543()
        {
            // Because of UUM-56542, this test might not always execute properly.
            // When it does, it validates that UUM-56543 has been properly fixed.
            // When it doesn't, the worker process will log a warning (missing script) but the test will pass.
            // NOTE - UUM-56542 and UUM-56543 are two different tickets, it's not a typo.

            var subScene = SubSceneTestsHelper.CreateSubSceneFromObjects(ref m_TempAssets, "SceneStreamingState", false, () =>
            {
                var go = new GameObject("GO");
                go.AddComponent<RegressionTest_UUM56543_AuthoringComponent>();
                return new List<GameObject> { go };
            });

            // Note that the problem only happens when loading a file from disk. If this tests gets refactored,
            // please ensure that it isn't converted into a live baking one.

            SubSceneInspectorUtility.ForceReimport(subScene);

            using var world = TestWorldSetup.CreateEntityWorld("World", TestWorldSetup.TestWorldSystemFilterFlags.OnlyStreaming);
            var resolveParamsAutoload = new SceneSystem.LoadParameters
            {
                Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
            };

            var sceneGuid = AssetDatabaseCompatibility.PathToGUID(AssetDatabase.GetAssetPath(subScene.SceneAsset));
            SceneSystem.LoadSceneAsync(world.Unmanaged, sceneGuid, resolveParamsAutoload);

            // When UUM-56543 triggers, an exception will be thrown in the background import worker.
            // As a result the baked file won't be written to disk and the load will fail with an exception.
            // That latter exception is what causes the current test to fail.

            world.Update();
        }
    }
}
