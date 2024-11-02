using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Conversion;
using Unity.Entities.Hybrid.Baking;
using Unity.Entities.Hybrid.Tests.Baking;
using Unity.Entities.TestComponents;
using Unity.Entities.Tests;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using static Unity.Scenes.Editor.Tests.TestWithEditorLiveConversion;

namespace Unity.Scenes.Editor.Tests
{
    /*
     * These tests provide some coverage for LiveConversion in the editor. LiveConversion, by default, is used in edit mode and in
     * play mode whenever there is an open subscene. Its contents are converted to entities in the background.
     *
     * The setup here is as follows:
     *  - all subscenes are created in a new temporary directory per test,
     *  - that directory is cleaned up when the test finished,
     *  - we also flush the entity scene paths cache to get rid of any subscene build files,
     *  - we clearly separate all tests into setup and test, because the latter might run in play mode.
     * That last point is crucial: Entering playmode serializes the test fixture, but not the contents of variables
     * within the coroutine that represents a test. This means that you cannot rely on the values of any variables and
     * you can get very nasty exceptions by assigning a variable from setup in play mode (due to the way enumerator
     * functions are compiled). Any data that needs to persist between edit and play mode must be stored on the class
     * itself.
     */
    [Serializable]
    [TestFixture]
    // Disabled on Linux because these tests generate too many file handles: DPE-568
    [UnityPlatform(exclude = new[] {RuntimePlatform.LinuxEditor})]
#if !UNITY_WEBGL
    [ConditionalIgnore("IgnoreForCoverage", "Fails randonly when ran with code coverage enabled")]
#endif
    abstract class LiveBakingAndConversionBase
    {
        [SerializeField] protected TestWithEditorLiveConversion LiveConversionTest;
        [SerializeField] protected string m_PrefabPath;
        [SerializeField] protected Material m_TestMaterial;
        [SerializeField] protected Texture m_TestTexture;

        public void OneTimeSetUp()
        {
            if (!LiveConversionTest.OneTimeSetUp())
                return;
            m_TestTexture = AssetDatabase.LoadAssetAtPath<Texture>(AssetPath("TestTexture.asset"));
            m_TestMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetPath("TestMaterial.mat"));

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        }

        public void OneTimeTearDown()
        {
            LiveConversionTest.OneTimeTearDown();

            // Clean up in case this test failed
            IncrementalConversion_WithDependencyOnDeletedAsset_ReconvertsAllDependents_Edit_TearDown();
        }

        [SetUp]
        public void SetUp()
        {
            LiveConversionTest.SetUp();
        }

        static string AssetPath(string name) => "Packages/com.unity.entities/Unity.Scenes.Editor.Tests/Assets/" + name;
        static string AssetFullPath(string name) => $"{Application.dataPath}/../../../{AssetPath(name)}";
        static string TempFolderPath() => $"{Application.dataPath}/../Temp";
        static string ScenePath(string name) => AssetPath(name) + ".unity";

        static void OpenAllSubScenes() => SubSceneUtility.EditScene(SubScene.AllSubScenes.ToArray());

        protected Scene CreateTmpScene() => LiveConversionTest.CreateTmpScene();

        protected SubScene CreateSubSceneFromObjects(string name, bool keepOpen, Func<List<GameObject>> createObjects) =>
            LiveConversionTest.CreateSubSceneFromObjects(name, keepOpen, createObjects);

        protected SubScene CreateEmptySubScene(string name, bool keepOpen) => LiveConversionTest.CreateEmptySubScene(name, keepOpen);

        protected World GetLiveConversionWorld(Mode playmode, bool removeWorldFromPlayerLoop = true) =>
            LiveConversionTest.GetLiveConversionWorld(playmode, removeWorldFromPlayerLoop);

        protected IEditModeTestYieldInstruction GetEnterPlayMode(Mode playmode) => LiveConversionTest.GetEnterPlayMode(playmode);

        protected IEnumerator UpdateEditorAndWorld(World w) => LiveConversionTest.UpdateEditorAndWorld(w);

        public static void SetTransformUsageFlags(GameObject go, TransformUsageFlags flags)
        {
            var component = go.AddComponent<AddTransformUsageFlag>();
            component.flags = flags;
        }

        [UnityTest]
        public IEnumerator OpenSubScene_StaysOpen_WhenEnteringPlayMode()
        {
            {
                CreateEmptySubScene("TestSubScene", true);
            }

            yield return new EnterPlayMode();

            {
                var subScene = Object.FindFirstObjectByType<SubScene>();
                Assert.IsTrue(subScene.IsLoaded);
            }
        }

        [UnityTest]
        public IEnumerator ClosedSubScene_StaysClosed_WhenEnteringPlayMode()
        {
            {
                CreateEmptySubScene("TestSubScene", false);
            }

            yield return new EnterPlayMode();

            {
                var subScene = Object.FindFirstObjectByType<SubScene>();
                Assert.IsFalse(subScene.IsLoaded);
            }
        }

        [UnityTest]
        public IEnumerator ClosedSubScene_CanBeOpened_InPlayMode()
        {
            {
                CreateEmptySubScene("TestSubScene", false);
            }

            yield return new EnterPlayMode();

            {
                var subScene = Object.FindFirstObjectByType<SubScene>();
                Assert.IsFalse(subScene.IsLoaded);
                SubSceneUtility.EditScene(subScene);
                yield return null;
                Assert.IsTrue(subScene.IsLoaded);
            }
        }

        [UnityTest]
        public IEnumerator LiveConversion_ConvertsSubScenes([Values]Mode mode)
        {
            {
                var scene = CreateTmpScene();
                SubSceneTestsHelper.CreateSubSceneInSceneFromObjects("TestSubScene1", true, scene, () =>
                {
                    var go = new GameObject("TestGameObject1");
                    go.AddComponent<TestComponentAuthoring>().IntValue = 1;
                    return new List<GameObject> {go};
                });
                SubSceneTestsHelper.CreateSubSceneInSceneFromObjects("TestSubScene2", true, scene, () =>
                {
                    var go = new GameObject("TestGameObject2");
                    go.AddComponent<TestComponentAuthoring>().IntValue = 2;
                    return new List<GameObject> {go};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var subSceneQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<SubScene>());
                var subScenes = subSceneQuery.ToComponentArray<SubScene>();
                var subSceneObjects = Object.FindObjectsByType<SubScene>(FindObjectsSortMode.None);
                foreach (var subScene in subSceneObjects)
                    Assert.Contains(subScene, subScenes);

                var componentQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());

                Assert.AreEqual(2, componentQuery.CalculateEntityCount(), "Expected a game object to be converted");
                using (var components =
                    componentQuery.ToComponentDataArray<TestComponentAuthoring.UnmanagedTestComponent>(
                        w.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(components.Contains(new TestComponentAuthoring.UnmanagedTestComponent {IntValue = 1}),
                        "Failed to find contents of subscene 1");
                    Assert.IsTrue(components.Contains(new TestComponentAuthoring.UnmanagedTestComponent {IntValue = 2}),
                        "Failed to find contents of subscene 2");
                }
            }
        }

        [UnityTest]
        public IEnumerator LiveConversion_RemovesDeletedSubScene([Values]Mode mode)
        {
            {
                var scene = CreateTmpScene();
                SubSceneTestsHelper.CreateSubSceneInSceneFromObjects("TestSubScene1", true, scene, () =>
                {
                    var go = new GameObject("TestGameObject");
                    go.AddComponent<TestComponentAuthoring>().IntValue = 1;
                    return new List<GameObject> {go};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var subScene = Object.FindFirstObjectByType<SubScene>();
                var subSceneQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<SubScene>());
                Assert.Contains(subScene, subSceneQuery.ToComponentArray<SubScene>(), "SubScene was not loaded");

                var componentQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(1, componentQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(1,
                    componentQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue);

                Object.DestroyImmediate(subScene.gameObject);

                yield return UpdateEditorAndWorld(w);

                Assert.IsTrue(subSceneQuery.IsEmptyIgnoreFilter, "SubScene was not unloaded");
                Assert.AreEqual(0, componentQuery.CalculateEntityCount());
            }
        }

        [UnityTest]
        public IEnumerator LiveConversion_ConvertsObjects([Values]Mode mode)
        {
            {
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    var go = new GameObject("TestGameObject");
                    go.AddComponent<TestComponentAuthoring>();
                    return new List<GameObject> {go};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
            }
        }



        [UnityTest, TestCaseSource(typeof(TestWithEditorLiveConversion), nameof(TestCases))]
        public IEnumerator LiveConversion_CreatesEntities_WhenObjectIsCreated(Mode mode)
        {
            {
                CreateEmptySubScene("TestSubScene", true);
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var subScene = Object.FindFirstObjectByType<SubScene>();
                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(0, testTagQuery.CalculateEntityCount());

                SceneManager.SetActiveScene(subScene.EditingScene);
                var go = new GameObject("Parent", typeof(TestComponentAuthoring));
                var subGo = new GameObject("Child", typeof(TestComponentAuthoring));
                subGo.transform.parent = go.transform;
                Undo.RegisterCreatedObjectUndo(go, "Create new object");
                Assert.AreEqual(go.scene, subScene.EditingScene);

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(2, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Undo.PerformUndo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(),
                    "Expected an entity to be removed, undo failed");
                Undo.PerformRedo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(2, testTagQuery.CalculateEntityCount(),
                    "Expected a game object to be converted, redo failed");
            }
        }

        [UnityTest]
        public IEnumerator LiveConversion_DisableLiveConversionComponentWorks([Values]Mode mode)
        {
            {
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    var go = new GameObject("TestGameObject");
                    var authoringComponent = go.AddComponent<TestComponentAuthoring>();
                    authoringComponent.IntValue = 42;
                    return new List<GameObject> {go};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);
                var manager = w.EntityManager;

                var subScene = Object.FindFirstObjectByType<SubScene>();

                var sceneEntity = SceneSystem.GetSceneEntity(w.Unmanaged, subScene.SceneGUID);
                var sectionEntity = manager.GetBuffer<ResolvedSectionEntity>(sceneEntity)[0].SectionEntity;

                Assert.AreNotEqual(Entity.Null, sceneEntity);

                var sceneInstance = SceneSystem.LoadSceneAsync(w.Unmanaged, subScene.SceneGUID,
                    new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.NewInstance | SceneLoadFlags.BlockOnStreamIn |
                                SceneLoadFlags.BlockOnImport
                    });

                yield return UpdateEditorAndWorld(w);

                var sectionInstance = manager.GetBuffer<ResolvedSectionEntity>(sceneInstance)[0].SectionEntity;

                var sceneQuery = w.EntityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>(),
                    ComponentType.ReadWrite<SceneTag>());
                sceneQuery.SetSharedComponentFilter(new SceneTag {SceneEntity = sectionEntity});

                var instanceQuery = w.EntityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>(),
                    ComponentType.ReadWrite<SceneTag>());
                instanceQuery.SetSharedComponentFilter(new SceneTag {SceneEntity = sectionInstance});

                Assert.AreEqual(42, sceneQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue);
                Assert.AreEqual(42,
                    instanceQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue);

                manager.AddComponent<DisableLiveConversion>(sceneInstance);

                var authoring = Object.FindFirstObjectByType<TestComponentAuthoring>();

                Undo.RecordObject(authoring, "Change component value");
                authoring.IntValue = 117;
                Undo.FlushUndoRecordObjects();

                Undo.IncrementCurrentGroup();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(117, sceneQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue);
                Assert.AreEqual(42,
                    instanceQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue);

                w.EntityManager.DestroyEntity(sceneInstance);
                yield return UpdateEditorAndWorld(w);
            }
        }

        [UnityTest, TestCaseSource(typeof(TestWithEditorLiveConversion), nameof(TestCases))]
        public IEnumerator LiveConversion_CreatesEntities_WhenObjectMovesBetweenScenes(Mode mode)
        {
            {
                CreateEmptySubScene("TestSubScene", true);
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var subScene = Object.FindFirstObjectByType<SubScene>();
                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(0, testTagQuery.CalculateEntityCount());

                var go = new GameObject("TestGameObject");
                go.AddComponent<TestComponentAuthoring>();
                Undo.MoveGameObjectToScene(go, subScene.EditingScene, "Test Move1");

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Undo.PerformUndo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(),
                    "Expected an entity to be removed, undo failed");
                Undo.PerformRedo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(),
                    "Expected a game object to be converted, redo failed");
            }
        }

        [UnityTest, TestCaseSource(typeof(TestWithEditorLiveConversion), nameof(TestCases))]
        public IEnumerator LiveConversion_CreatesAndDestroyEntities_WhenObjectMovesBetweenSubScenes_FromAToB(Mode mode) =>
            LiveConversion_CreatesAndDestroyEntities_WhenObjectMovesBetweenSubScenes(mode, true);

        [UnityTest,TestCaseSource(typeof(TestWithEditorLiveConversion), nameof(TestCases))]
        public IEnumerator LiveConversion_CreatesAndDestroyEntities_WhenObjectMovesBetweenSubScenes_FromBToA(Mode mode) =>
            LiveConversion_CreatesAndDestroyEntities_WhenObjectMovesBetweenSubScenes(mode, false);

        IEnumerator LiveConversion_CreatesAndDestroyEntities_WhenObjectMovesBetweenSubScenes(Mode mode, bool forward)
        {
            var scene = CreateTmpScene();
            var goA = new GameObject("TestGameObjectA");
            var goB = new GameObject("TestGameObjectB");
            goA.AddComponent<TestComponentAuthoring>().IntValue = 1;
            goB.AddComponent<TestComponentAuthoring>().IntValue = 2;

            var subSceneA = SubSceneTestsHelper.CreateSubSceneInSceneFromObjects("TestSubSceneA", true, scene, () => new List<GameObject> { goA });
            var subSceneB = SubSceneTestsHelper.CreateSubSceneInSceneFromObjects("TestSubSceneB", true, scene, () => new List<GameObject> { goB });

            var w = GetLiveConversionWorld(mode);
            yield return UpdateEditorAndWorld(w);

            using (var q = w.EntityManager.CreateEntityQuery(typeof(SceneTag), typeof(EntityGuid)))
            using (var entities = q.ToEntityArray(w.UpdateAllocator.ToAllocator))
            {
                Assert.That(entities.Length, Is.EqualTo(2));
                Assert.That(w.EntityManager.GetSharedComponent<SceneTag>(entities[0]).SceneEntity, Is.Not.EqualTo(w.EntityManager.GetSharedComponent<SceneTag>(entities[1]).SceneEntity));
            }

            Undo.MoveGameObjectToScene(goA, subSceneB.EditingScene, "Move from A to B");

            yield return UpdateEditorAndWorld(w);

            using (var q = w.EntityManager.CreateEntityQuery(typeof(SceneTag), typeof(EntityGuid)))
            using (var entities = q.ToEntityArray(w.UpdateAllocator.ToAllocator))
            {
                Assert.That(entities.Length, Is.EqualTo(2));
                Assert.That(w.EntityManager.GetSharedComponent<SceneTag>(entities[0]).SceneEntity, Is.EqualTo(w.EntityManager.GetSharedComponent<SceneTag>(entities[1]).SceneEntity));
            }
        }

        // This test documents a fix for DOTS-3020, but the failure only occurs when the world is part of the player
        // loop. This also means that this test will fail when the editor does not have focus, which is why it is
        // disabled by default.
        [Ignore("Needs DOTS-3216 to work reliably on CI.")]
        [Explicit]
        [UnityTest]
        [Repeat(10)]
        public IEnumerator LiveConversion_DestroysAndCreatesEntities_WhenClosingThenOpeningSubScene([Values(1,2,3,4,5,6,7,8,9,10)]int framesBetweenSteps)
        {
            if (!Application.isFocused)
                throw new Exception("This test can only run when the editor has focus. The test needs the player loop to be updated and this does not happen when the editor is not in focus.");
            var scene = CreateTmpScene();
            var go = new GameObject("TestGameObjectA");
            go.AddComponent<TestComponentAuthoring>().IntValue = 1;
            var subScene = SubSceneTestsHelper.CreateSubSceneInSceneFromObjects("TestSubSceneA", true, scene, () => new List<GameObject> { go });

            // This error only happens when the editor itself triggers an update via the player loop.
            var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit, false);
            for (var i = 0; i < framesBetweenSteps; i++)
            {
                yield return null;
            }

            var openedSceneEntityCount = w.EntityManager.UniversalQueryWithSystems.CalculateEntityCount();
            SubSceneInspectorUtility.CloseSceneWithoutSaving(subScene);

            for (var i = 0; i < framesBetweenSteps; i++)
            {
                yield return null;
            }

            var closedSceneEntityCount = w.EntityManager.UniversalQueryWithSystems.CalculateEntityCount();
            Assert.That(closedSceneEntityCount, Is.Not.EqualTo(openedSceneEntityCount));

            Assert.That(SubSceneInspectorUtility.CanEditScene(subScene), Is.True);
            SubSceneUtility.EditScene(subScene);

            for (var i = 0; i < framesBetweenSteps; i++)
            {
                yield return null;
            }

            var reopenedSceneEntityCount = w.EntityManager.UniversalQueryWithSystems.CalculateEntityCount();
            Assert.That(reopenedSceneEntityCount, Is.EqualTo(openedSceneEntityCount));
        }

        [UnityTest, TestCaseSource(typeof(TestWithEditorLiveConversion), nameof(TestCases))]
        public IEnumerator LiveConversion_DestroysEntities_WhenObjectMovesScenes(Mode mode)
        {
            var mainScene = CreateTmpScene();

            {
                SubSceneTestsHelper.CreateSubSceneInSceneFromObjects("TestSubScene", true, mainScene, () =>
                {
                    var go = new GameObject("TestGameObject");
                    go.AddComponent<TestComponentAuthoring>();
                    return new List<GameObject> {go};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");

                var go = Object.FindFirstObjectByType<TestComponentAuthoring>().gameObject;
                var scene = SceneManager.GetActiveScene();
                Undo.MoveGameObjectToScene(go, scene, "Move out of subscene");

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected an entity to be removed");
                Undo.PerformUndo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(),
                    "Expected a game object to be converted, undo failed");
                Undo.PerformRedo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(),
                    "Expected an entity to be removed, redo failed");
            }
        }

        [UnityTest, TestCaseSource(typeof(TestWithEditorLiveConversion), nameof(TestCases))]
        public IEnumerator LiveConversion_DestroysEntities_WhenObjectIsDestroyed(Mode mode)
        {
            {
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    var go = new GameObject("TestGameObject");
                    go.AddComponent<TestComponentAuthoring>();
                    var childGo = new GameObject("Child");
                    childGo.transform.SetParent(go.transform);
                    childGo.AddComponent<TestComponentAuthoring>();
                    return new List<GameObject> {go};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);
                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(2, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");

                var go = Object.FindFirstObjectByType<TestComponentAuthoring>().gameObject;
                if (go.transform.parent != null)
                    go = go.transform.parent.gameObject;
                Undo.DestroyObjectImmediate(go);

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected an entity to be removed");
                Undo.PerformUndo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(2, testTagQuery.CalculateEntityCount(),
                    "Expected a game object to be converted, undo failed");
                Undo.PerformRedo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(),
                    "Expected an entity to be removed, redo failed");
            }
        }

        [UnityTest, TestCaseSource(typeof(TestWithEditorLiveConversion), nameof(TestCases))]
        public IEnumerator LiveConversion_ComponentConditionallyAdded(Mode mode)
        {
            {
                CreateEmptySubScene("TestSubScene", true);
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var subScene = Object.FindFirstObjectByType<SubScene>();
                var go = new GameObject("TestGameObject");
                var authoring = go.AddComponent<TestConditionalComponentAuthoring>();

                Undo.MoveGameObjectToScene(go, subScene.EditingScene, "Test Move");

                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadOnly<TestConditionalComponentAuthoring.TestComponent>());

                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected no entity with the TestComponent");

                Undo.RecordObject(authoring, "Change component value");
                authoring.condition = true;

                // it takes an extra frame to establish that something has changed when using RecordObject unless Flush is called
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected 1 entity with the TestComponent");

                Undo.RecordObject(authoring, "Change component value");
                authoring.condition = false;

                // it takes an extra frame to establish that something has changed when using RecordObject unless Flush is called
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected 0 entity with the TestComponent");
            }
        }

        [UnityTest]
        public IEnumerator LiveConversion_DestroysEntities_WhenSubSceneBehaviourIsDisabled([Values]Mode mode)
        {
            {
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    var go = new GameObject("TestGameObject");
                    go.AddComponent<TestComponentAuthoring>();
                    return new List<GameObject> {go};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);
                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");

                var subscene = Object.FindFirstObjectByType<SubScene>();

                subscene.enabled = false;
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected an entity to be removed");

                subscene.enabled = true;
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
            }
        }

        [UnityTest]
        public IEnumerator LiveConversion_DestroysEntities_WhenSubSceneObjectIsDisabled([Values]Mode mode)
        {
            {
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    var go = new GameObject("TestGameObject");
                    go.AddComponent<TestComponentAuthoring>();
                    return new List<GameObject> {go};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);
                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");

                var subscene = Object.FindFirstObjectByType<SubScene>();

                subscene.gameObject.SetActive(false);
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected an entity to be removed");

                subscene.gameObject.SetActive(true);
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted, undo failed");
            }
        }

        [UnityTest, TestCaseSource(typeof(TestWithEditorLiveConversion), nameof(TestCases))]
        public IEnumerator LiveConversion_SupportsAddComponentAndUndo(Mode mode)
        {
            {
                CreateEmptySubScene("TestSubScene", true);
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var subScene = Object.FindFirstObjectByType<SubScene>();
                var go = new GameObject("TestGameObject");
                Undo.MoveGameObjectToScene(go, subScene.EditingScene, "Test Move");
                Undo.IncrementCurrentGroup();

                yield return UpdateEditorAndWorld(w);

                Undo.AddComponent<TestComponentAuthoring>(go);
                Undo.IncrementCurrentGroup();
                Assert.IsNotNull(go.GetComponent<TestComponentAuthoring>());

                yield return UpdateEditorAndWorld(w);

                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(),
                    "Expected a game object to be converted and gain a component");

                Undo.PerformUndo();
                Assert.IsNull(go.GetComponent<TestComponentAuthoring>());

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(),
                    "Expected a game object to be converted and lose a component, undo add failed");

                Undo.PerformRedo();
                Assert.IsNotNull(go.GetComponent<TestComponentAuthoring>());

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(),
                    "Expected a game object to be converted and gain a component, redo add failed");
            }
        }


        [UnityTest, TestCaseSource(typeof(TestWithEditorLiveConversion), nameof(TestCases))]
        public IEnumerator LiveConversion_SupportsRemoveComponentAndUndo(Mode mode)
        {
            {
                CreateEmptySubScene("TestSubScene", true);
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var subScene = Object.FindFirstObjectByType<SubScene>();
                var go = new GameObject("TestGameObject");
                go.AddComponent<TestComponentAuthoring>();
                Undo.MoveGameObjectToScene(go, subScene.EditingScene, "Test Move");
                Undo.IncrementCurrentGroup();

                yield return UpdateEditorAndWorld(w);

                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(),
                    "Expected a game object to be converted with a component");

                Undo.DestroyObjectImmediate(go.GetComponent<TestComponentAuthoring>());
                Undo.IncrementCurrentGroup();

                Assert.IsNull(go.GetComponent<TestComponentAuthoring>());

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(),
                    "Expected a game object to be converted and lose a component");

                Undo.PerformUndo();
                Assert.IsNotNull(go.GetComponent<TestComponentAuthoring>());

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(),
                    "Expected a game object to be converted and gain a component, undo remove failed");

                Undo.PerformRedo();
                Assert.IsNull(go.GetComponent<TestComponentAuthoring>());

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(),
                    "Expected a game object to be converted and lose a component, redo remove failed");
            }
        }

        [UnityTest, TestCaseSource(typeof(TestWithEditorLiveConversion), nameof(TestCases))]
        public IEnumerator LiveConversion_ReflectsChangedComponentValues(Mode mode)
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                var go = new GameObject("TestGameObject");
                var authoring = go.AddComponent<TestComponentAuthoring>();
                authoring.IntValue = 15;
                SceneManager.MoveGameObjectToScene(go, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                var authoring = Object.FindFirstObjectByType<TestComponentAuthoring>();
                Assert.AreEqual(authoring.IntValue, 15);

                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(15,
                    testTagQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue);

                Undo.RecordObject(authoring, "Change component value");
                authoring.IntValue = 2;

                // it takes an extra frame to establish that something has changed when using RecordObject unless Flush is called
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(2, testTagQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue,
                    "Expected a component value to change");

                Undo.PerformUndo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(15, testTagQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue,
                    "Expected a component value to change, undo failed");

                Undo.PerformRedo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(2, testTagQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue,
                    "Expected a component value to change, redo failed");
            }
        }

        [UnityTest]
        public IEnumerator LiveConversion_ReEnableEntityHierarchy_WhenParentGameObjectIsReEnabledFromSavedSubScene()
        {
            var subscene = CreateSubSceneFromObjects("TestSubScene", false, () =>
            {
                var parent = new GameObject("Parent");
                parent.AddComponent<TestComponentAuthoring>().IntValue = 1;
                var child = new GameObject("Child");
                child.AddComponent<TestComponentAuthoring>().IntValue = 2;
                child.transform.SetParent(parent.transform);
                parent.SetActive(false);
                return new List<GameObject> { parent };
            });

            SubSceneUtility.EditScene(subscene);

            var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

            yield return UpdateEditorAndWorld(w);

            using (var q = w.EntityManager.CreateEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(Disabled) }, Options = EntityQueryOptions.IncludeDisabledEntities }))
            {
                Assert.That(q.CalculateEntityCount(), Is.EqualTo(2));

                using var entityArray = q.ToEntityArray(w.UpdateAllocator.ToAllocator);
                var entities = entityArray.ToArray();
                Assert.That(entities.Select(e => w.EntityManager.GetComponentData<TestComponentAuthoring.UnmanagedTestComponent>(e).IntValue), Is.EquivalentTo(new[] { 1, 2 }));
            }

#if !UNITY_2023_2_OR_NEWER
            var parent = Object.FindObjectsOfType<TestComponentAuthoring>(true).Single(c => c.IntValue == 1).gameObject;
#else
            var parent = Object.FindObjectsByType<TestComponentAuthoring>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(c => c.IntValue == 1).gameObject;
#endif

            Undo.RecordObject(parent, "ReEnableObject");
            parent.SetActive(true);
            Undo.FlushUndoRecordObjects();

            yield return UpdateEditorAndWorld(w);

            using (var q = w.EntityManager.CreateEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(Disabled) }, Options = EntityQueryOptions.IncludeDisabledEntities }))
            {
                Assert.That(q.CalculateEntityCount(), Is.EqualTo(0));
            }
        }

        [UnityTest, TestCaseSource(typeof(TestWithEditorLiveConversion), nameof(TestCases))]
        public IEnumerator LiveConversion_DisablesEntity_WhenGameObjectIsDisabled(Mode mode)
        {
            {
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    var go = new GameObject("TestGameObject");
                    go.AddComponent<TestComponentAuthoring>();
                    var child = new GameObject("Child");
                    child.transform.SetParent(go.transform);
                    return new List<GameObject> {go};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var queryWithoutDisabled =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(1, queryWithoutDisabled.CalculateEntityCount(),
                    "Expected a game object to be converted");

                var go = Object.FindFirstObjectByType<TestComponentAuthoring>().gameObject;
                Undo.RecordObject(go, "DisableObject");
                go.SetActive(false);
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                var queryWithDisabled = w.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>(),
                        ComponentType.ReadWrite<Disabled>()
                    },
                    Options = EntityQueryOptions.IncludeDisabledEntities
                });
                Assert.AreEqual(1, queryWithDisabled.CalculateEntityCount(),
                    "Expected a game object to be converted and disabled");
                Assert.AreEqual(0, queryWithoutDisabled.CalculateEntityCount(),
                    "Expected a game object to be converted and disabled");

                Undo.PerformUndo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, queryWithDisabled.CalculateEntityCount(),
                    "Expected a game object to be converted and enabled");
                Assert.AreEqual(1, queryWithoutDisabled.CalculateEntityCount(),
                    "Expected a game object to be converted and enabled");

                Undo.PerformRedo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, queryWithDisabled.CalculateEntityCount(),
                    "Expected a game object to be converted and disabled");
                Assert.AreEqual(0, queryWithoutDisabled.CalculateEntityCount(),
                    "Expected a game object to be converted and disabled");
            }
        }

        [UnityTest, EmbeddedPackageOnlyTest]
        public IEnumerator LiveConversion_WithTextureDependency_ChangeCausesReconversion([Values]Mode mode)
        {
            {
                EditorSceneManager.OpenScene(ScenePath("SceneWithTextureDependency"));
                OpenAllSubScenes();
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var testQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ConversionDependencyData>());
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.IsTrue(testQuery.GetSingleton<ConversionDependencyData>().HasTexture);
                Assert.AreEqual(m_TestTexture.filterMode,
                    testQuery.GetSingleton<ConversionDependencyData>().TextureFilterMode,
                    "Initial conversion reported the wrong value");

                m_TestTexture.filterMode = m_TestTexture.filterMode == FilterMode.Bilinear
                    ? FilterMode.Point
                    : FilterMode.Bilinear;
                AssetDatabase.SaveAssets();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(m_TestTexture.filterMode,
                    testQuery.GetSingleton<ConversionDependencyData>().TextureFilterMode,
                    "Updated conversion shows the wrong value");
            }
        }

        public enum ChangeMode
        {
            ChangeOnDisk,
            ChangeAndWrite,
            ChangeInMemory
        }

        [UnityTest, EmbeddedPackageOnlyTest]
        public IEnumerator LiveConversion_WithMaterialDependency_ChangeCausesReconversion([Values] Mode mode, [Values]ChangeMode change)
        {
            m_TestMaterial.SetColor("_BaseColor", Color.white);
            AssetDatabase.SaveAssetIfDirty(m_TestMaterial);
            AssetDatabase.Refresh();

            {
                EditorSceneManager.OpenScene(ScenePath("SceneWithMaterialDependency"));
                OpenAllSubScenes();
            }

            yield return GetEnterPlayMode(mode);

            Assert.AreEqual(Color.white, m_TestMaterial.GetColor("_BaseColor"), "The Material color was supposed to be initialized with white. This is likely a bug in the test or Unity itself, not in the actual code being tested.");

            {
                var w = GetLiveConversionWorld(mode);

                var testQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ConversionDependencyData>());
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(Color.white, testQuery.GetSingleton<ConversionDependencyData>().MaterialColor);

                var newColor = new Color(0, 0, 0.5F, 1.0F);
                if (change == ChangeMode.ChangeOnDisk)
                {
                    var materialPath = AssetDatabase.GetAssetPath(m_TestMaterial);
                    var text = File.ReadAllText(materialPath);

                    string replaced = text.Replace("_BaseColor: {r: 1, g: 1, b: 1, a: 1}", "_BaseColor: {r: 0, g: 0, b: 0.5, a: 1}");
                    Assert.AreNotEqual(replaced, text, "Replacing the contents of the yaml file using string search failed.");

                    File.WriteAllText(materialPath, replaced);
                    AssetDatabase.Refresh();
                }
                else if (change == ChangeMode.ChangeInMemory)
                {
                    Undo.RegisterCompleteObjectUndo(m_TestMaterial, "undo");
                    m_TestMaterial.SetColor("_BaseColor", newColor);
                }
                else if (change == ChangeMode.ChangeAndWrite)
                {
                    m_TestMaterial.SetColor("_BaseColor", newColor);
                    AssetDatabase.SaveAssets();
                }

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(newColor, m_TestMaterial.GetColor("_BaseColor"),
                    "The color of the material hasn't changed to the expected value. This is likely a bug in the test or Unity itself, not in the actual code being tested.");
                Assert.AreEqual(newColor, testQuery.GetSingleton<ConversionDependencyData>().MaterialColor,
                    "The game object with the asset dependency has not been reconverted");
            }
        }

        [UnityTest, EmbeddedPackageOnlyTest]
        public IEnumerator LiveConversion_WithMultipleScenes_WithAssetDependencies_ChangeCausesReconversion([Values]Mode mode)
        {
            {
                EditorSceneManager.OpenScene(ScenePath("SceneWithMaterialDependency"));
                EditorSceneManager.OpenScene(ScenePath("SceneWithTextureDependency"), OpenSceneMode.Additive);
                OpenAllSubScenes();
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var testQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ConversionDependencyData>());
                Assert.AreEqual(2, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Entity textureEntity, materialEntity;
                using (var entities = testQuery.ToEntityArray(w.UpdateAllocator.ToAllocator))
                {
                    if (GetData(entities[0]).HasMaterial)
                    {
                        materialEntity = entities[0];
                        textureEntity = entities[1];
                    }
                    else
                    {
                        materialEntity = entities[1];
                        textureEntity = entities[0];
                    }
                }

                Assert.AreEqual(m_TestMaterial.color, GetData(materialEntity).MaterialColor);
                Assert.AreEqual(m_TestTexture.filterMode, GetData(textureEntity).TextureFilterMode);

                m_TestMaterial.color = m_TestMaterial.color == Color.blue ? Color.red : Color.blue;
                AssetDatabase.SaveAssets();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(m_TestMaterial.color, GetData(materialEntity).MaterialColor,
                    "The game object with the material asset dependency has not been reconverted");

                m_TestTexture.filterMode = m_TestTexture.filterMode == FilterMode.Bilinear
                    ? FilterMode.Point
                    : FilterMode.Bilinear;
                AssetDatabase.SaveAssets();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(m_TestTexture.filterMode, GetData(textureEntity).TextureFilterMode,
                    "The game object with the texture asset dependency has not been reconverted.");

                ConversionDependencyData GetData(Entity e) =>
                    w.EntityManager.GetComponentData<ConversionDependencyData>(e);
            }
        }

        [UnityTest]
        public IEnumerator LiveConversion_LoadAndUnload_WithChanges([Values] Mode mode)
        {
            {
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    var go = new GameObject("TestGameObject");
                    var authoring = go.AddComponent<TestComponentAuthoring>();
                    authoring.Material = m_TestMaterial;
                    authoring.IntValue = 15;

                    return new List<GameObject> {go};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var authoring = Object.FindFirstObjectByType<TestComponentAuthoring>();
                Assert.AreEqual(authoring.IntValue, 15);
                Assert.AreEqual(authoring.Material, m_TestMaterial);

                var w = GetLiveConversionWorld(mode);

                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(15,
                    testTagQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue);

                var testSceneQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<SceneReference>());
                Assert.AreEqual(1, testSceneQuery.CalculateEntityCount());

                Undo.RecordObject(authoring, "Change component value");
                authoring.IntValue = 2;

                // it takes an extra frame to establish that something has changed when using RecordObject unless Flush is called
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(2, testTagQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue,
                    "Expected a component value to change");

                var subScene = Object.FindFirstObjectByType<SubScene>();
                Assert.IsNotNull(subScene);

                subScene.gameObject.SetActive(false);
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(0, testSceneQuery.CalculateEntityCount(),
                    "Expected no Scene Entities after disabling the SubScene MonoBehaviour");

                subScene.gameObject.SetActive(true);
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(1, testSceneQuery.CalculateEntityCount(),
                    "Expected Scene Entity after enabling the SubScene MonoBehaviour");

                // Do conversion again
                Undo.RecordObject(authoring, "Change component value");
                authoring.IntValue = 42;

                // it takes an extra frame to establish that something has changed when using RecordObject unless Flush is called
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(),
                    "Expected a game object to be converted after unloading and loading subscene");
                Assert.AreEqual(42, testTagQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue,
                    "Expected a component value to change after unloading and loading subscene");
            }
        }

        [UnityTest]
        public IEnumerator LiveConversion_ReconvertsBlobAssets()
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                var go = new GameObject("TestGameObject");
                var authoring = go.AddComponent<TestComponentWithBlobAssetAuthoring>();
                authoring.Version = 1;
                SceneManager.MoveGameObjectToScene(go, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var authoring = Object.FindFirstObjectByType<TestComponentWithBlobAssetAuthoring>();

                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentWithBlobAssetAuthoring.Component>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(1,
                    testTagQuery.GetSingleton<TestComponentWithBlobAssetAuthoring.Component>().BlobAssetRef.Value);

                Undo.RecordObject(authoring, "Change component value");
                authoring.Version = 2;

                // it takes an extra frame to establish that something has changed when using RecordObject unless Flush is called
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(2,
                    testTagQuery.GetSingleton<TestComponentWithBlobAssetAuthoring.Component>().BlobAssetRef.Value,
                    "Expected a blob asset value to change");

                Undo.PerformUndo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(1,
                    testTagQuery.GetSingleton<TestComponentWithBlobAssetAuthoring.Component>().BlobAssetRef.Value,
                    "Expected a blob asset value to change, undo failed");

                Undo.PerformRedo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(2,
                    testTagQuery.GetSingleton<TestComponentWithBlobAssetAuthoring.Component>().BlobAssetRef.Value,
                    "Expected a blob asset value to change, redo failed");
            }
        }

        [UnityTest]
        [Ignore("CI Instability DOTS-8126")]
        public IEnumerator LiveConversion_ChangingAssetInPrefab_Doesnt_Throw()
        {
            var subScene = CreateEmptySubScene("TestSubScene3", true);

            var assetPath = AssetPath("Smoke.fbx");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            PrefabUtility.InstantiatePrefab(prefab, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                var modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (modelImporter != null)
                {
                    modelImporter.globalScale += 10;
                    modelImporter.SaveAndReimport();
                }

                yield return UpdateEditorAndWorld(w);
                var query = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<LocalToWorld>());

                //Check the prefab instance has been converted and baking/conversion didn't throw any errors (like for baking we run twice the same component on the same baker)
                Assert.AreEqual(1, query.CalculateEntityCount(), "Expected a game object to be converted");
            }
        }

        [UnityTest, EmbeddedPackageOnlyTest]
        public IEnumerator LiveConversion_ChangingPrefabInstanceWorks()
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                m_PrefabPath = LiveConversionTest.Assets.GetNextPath("Test.prefab");
                var root = new GameObject();
                var child = new GameObject();
                child.AddComponent<TestComponentAuthoring>().IntValue = 3;
                child.transform.SetParent(root.transform);
                SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, m_PrefabPath, InteractionMode.AutomatedAction);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var authoring = Object.FindFirstObjectByType<TestComponentAuthoring>();
                Assert.AreEqual(authoring.IntValue, 3);

                var testTagQuery = w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(3, testTagQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(m_PrefabPath);
                prefab.GetComponentInChildren<TestComponentAuthoring>().IntValue = 15;
                PrefabUtility.SavePrefabAsset(prefab, out var success);
                Assert.IsTrue(success, "Failed to save prefab asset");
                AssetDatabase.SaveAssets();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(15, testTagQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue);
            }
        }

        [UnityTest, EmbeddedPackageOnlyTest]
        public IEnumerator LiveConversion_DeletingFromPrefabInstanceWorks()
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);
                m_PrefabPath = LiveConversionTest.Assets.GetNextPath("Test.prefab");
                var root = new GameObject("Root");
                root.AddComponent<TestComponentAuthoring>().IntValue = 42;
                var child = new GameObject("Child");
                child.AddComponent<TestComponentAuthoring>().IntValue = 3;
                child.transform.SetParent(root.transform);
                SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, m_PrefabPath, InteractionMode.AutomatedAction);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var testTagQuery = w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>());
                Assert.AreEqual(2, testTagQuery.CalculateEntityCount(), "Expected two game objects to be converted");

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(m_PrefabPath);
                Object.DestroyImmediate(prefab.transform.GetChild(0).gameObject, true);
                PrefabUtility.SavePrefabAsset(prefab, out var success);
                Assert.IsTrue(success, "Failed to save prefab asset");
                AssetDatabase.SaveAssets();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(42, testTagQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue);
            }
        }

        [UnityTest]
        public IEnumerator LiveConversion_RunsWithSectionsNotYetLoaded([Values]Mode mode)
        {
            {
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    var go = new GameObject("TestGameObject");
                    var authoringComponent = go.AddComponent<TestComponentAuthoring>();
                    authoringComponent.IntValue = 42;
                    return new List<GameObject> { go };
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);
                var manager = w.EntityManager;

                var sceneSystem = w.GetExistingSystem<SceneSystem>();
                var subScene = Object.FindFirstObjectByType<SubScene>();

                var sceneEntity = SceneSystem.GetSceneEntity(w.Unmanaged, subScene.SceneGUID);
                Assert.AreNotEqual(Entity.Null, sceneEntity);

                var sceneInstance = SceneSystem.LoadSceneAsync(w.Unmanaged, subScene.SceneGUID,
                    new SceneSystem.LoadParameters
                    {
                        Flags = SceneLoadFlags.NewInstance | SceneLoadFlags.BlockOnStreamIn |
                                SceneLoadFlags.BlockOnImport
                    });

                yield return UpdateEditorAndWorld(w);

                var sectionInstance = manager.GetBuffer<ResolvedSectionEntity>(sceneInstance)[0].SectionEntity;

                var instanceQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<TestComponentAuthoring.UnmanagedTestComponent>(), ComponentType.ReadWrite<SceneTag>());
                instanceQuery.SetSharedComponentFilter(new SceneTag {SceneEntity = sectionInstance});

                Assert.AreEqual(42, instanceQuery.GetSingleton<TestComponentAuthoring.UnmanagedTestComponent>().IntValue);

                //Clear resolved scene sections and ensure the patcher doesn't error
                //this emulates an async scene not yet fully loaded
                manager.GetBuffer<ResolvedSectionEntity>(sceneInstance).Clear();

                var authoring = Object.FindFirstObjectByType<TestComponentAuthoring>();

                //Change the authoring component value in order to force the LiveConversion patcher to run
                //Expect no errors
                Undo.RecordObject(authoring, "Change component value");
                authoring.IntValue = 117;
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                //Clean up scene instance
                w.EntityManager.DestroyEntity(sceneInstance);
                yield return UpdateEditorAndWorld(w);
            }
        }


        [UnityTest]
        public IEnumerator LiveConversion_SceneWithIsBuildingForEditorConversion([Values]Mode mode)
        {
            var subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
            {
                var go = new GameObject("TestGameObject", typeof(TestComponentAuthoringIsBuildingForEditor));
                return new List<GameObject> { go };
            });

            var buildSettings = default(Unity.Entities.Hash128);
            var originalHash = EntityScenesPaths.GetSubSceneArtifactHash(subScene.SceneGUID, buildSettings, true, ImportMode.Synchronous);
            Assert.IsTrue(originalHash.IsValid);

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);
                var componentQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<IntTestData>());
                Assert.AreEqual(1, componentQuery.GetSingleton<IntTestData>().Value);
            }
        }

        [UnityTest]
        public IEnumerator IncrementalConversion_WithDependencyOnDeletedComponent_ReconvertsAllDependents_Edit()
        {
            // This is a test for a very specific case: Declaring dependencies on components that are deleted at the
            // time of conversion but that are later restored.
            // This is happening in this case because:
            //  "B" has a dependency on "A".
            //  We delete "A" before the conversion happens.
            //  We then restore "A", which must trigger a reconversion of "B".

            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aAuthoring = a.AddComponent<DependsOnComponentTransitiveTestAuthoring>();
                aAuthoring.SelfValue = 4;
                var b = new GameObject("B");
                var bAuthoring = b.AddComponent<DependsOnComponentTransitiveTestAuthoring>();
                bAuthoring.Dependency = aAuthoring;
                bAuthoring.SelfValue = 15;
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
                SceneManager.MoveGameObjectToScene(b, subScene.EditingScene);
                Undo.DestroyObjectImmediate(a);
                // ensure that we have processed the destroy event from above
                yield return null;
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<DependsOnComponentTransitiveTestAuthoring.Component>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                EntitiesAssert.Contains(w.EntityManager,
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 15}));

                Undo.PerformUndo();

                // In the editor, undoing the deletion would restore the reference, but this doesn't immediately work
                // in code. So we're doing it manually for now.
                var a = GameObject.Find("A");
                var b = GameObject.Find("B");
                b.GetComponent<DependsOnComponentTransitiveTestAuthoring>().Dependency =
                    a.GetComponent<DependsOnComponentTransitiveTestAuthoring>();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(2, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                EntitiesAssert.Contains(w.EntityManager,
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 4}),
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 5}));
            }
        }

        [UnityTest]
        public IEnumerator IncrementalConversion_WithDependencyOnDeletedAsset_ReconvertsAllDependents_Edit()
        {
            // This is a test for a very specific case: Declaring dependencies on assets that are deleted at the
            // time of conversion but that are later restored.
            // This is happening in this case because:
            //  "B" has a dependency on "Asset".
            //  We delete "asset" before the conversion happens.
            //  We then restore "asset", which must trigger a reconversion of "B".

            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                DependsOnAssetTransitiveTestScriptableObject asset = ScriptableObject.CreateInstance<DependsOnAssetTransitiveTestScriptableObject>();
                asset.SelfValue = 2;
                asset.name = "RuntimeAsset";

                var b = new GameObject("B");
                var bAuthoring = b.AddComponent<DependsOnAssetTransitiveTestAuthoring>();
                bAuthoring.Dependency = asset;
                bAuthoring.SelfValue = 15;

                SceneManager.MoveGameObjectToScene(b, subScene.EditingScene);
                Undo.DestroyObjectImmediate(asset);

                // ensure that we have processed the destroy event from above
                yield return null;
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<DependsOnAssetTransitiveTestAuthoring.Component>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                EntitiesAssert.Contains(w.EntityManager,
                    EntityMatch.Partial(new DependsOnAssetTransitiveTestAuthoring.Component { Value = 15 }));

                Undo.PerformUndo();

                // In the editor, undoing the deletion would restore the reference, but this doesn't immediately work
                // in code. So we're doing it manually for now.
                var b = GameObject.Find("B");
                var assets = Object.FindObjectsByType<DependsOnAssetTransitiveTestScriptableObject>(FindObjectsSortMode.None);

                // Make sure we find the right asset
                DependsOnAssetTransitiveTestScriptableObject asset = null;
                foreach (var currentAsset in assets)
                {
                    if (currentAsset.name == "RuntimeAsset")
                    {
                        asset = currentAsset;
                    }
                }
                Assert.NotNull(asset, "Asset shouldn't be null");

                b.GetComponent<DependsOnAssetTransitiveTestAuthoring>().Dependency = asset;

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                EntitiesAssert.Contains(w.EntityManager,
                    EntityMatch.Partial(new DependsOnAssetTransitiveTestAuthoring.Component { Value = 17 }));
            }
        }

        public void IncrementalConversion_WithDependencyOnDeletedAsset_ReconvertsAllDependents_Edit_TearDown()
        {
            string fileTempPath = TempFolderPath() + "/TestScriptableObject.asset";
            if (File.Exists(fileTempPath))
            {
                File.Delete(fileTempPath);
                File.Delete(fileTempPath + ".meta");
            }
        }


        [UnityTest]
        public IEnumerator IncrementalConversion_WithDependencyOnDiskDeletedAsset_ReconvertsAllDependents_Edit()
        {
            // This is a test for a very specific case: Declaring dependencies on assets on disk that they are deleted at the
            // time of conversion but that are later restored.
            // This is happening in this case because:
            //  "B" has a dependency on "Asset".
            //  We delete "asset" before the conversion happens.
            //  We then restore "asset", which must trigger a reconversion of "B".

            string path = LiveConversionTest.Assets.GetNextPath(".asset");
            string fileTempPath = TempFolderPath() + "/TestScriptableObject.asset";
            string assetName = "TestScriptableObject";

            {
                DependsOnAssetTransitiveTestScriptableObject asset = ScriptableObject.CreateInstance<DependsOnAssetTransitiveTestScriptableObject>();
                asset.SelfValue = 2;
                asset.name = assetName;

                // Save to the asset database
                AssetDatabase.CreateAsset(asset, path);
            }

            {
                var subScene = CreateEmptySubScene("TestSubScene", true);
                var asset = AssetDatabase.LoadAssetAtPath<DependsOnAssetTransitiveTestScriptableObject>(path);

                var b = new GameObject("B");
                var bAuthoring = b.AddComponent<DependsOnAssetTransitiveTestAuthoring>();
                bAuthoring.Dependency = asset;
                bAuthoring.SelfValue = 15;

                SceneManager.MoveGameObjectToScene(b, subScene.EditingScene);

                // Move assets out of unity view. In this case, we are moving the files to the temp folder
                Assert.IsTrue(File.Exists(path), $"'{path}' doesn't exist");

                File.Move(path, fileTempPath);
                File.Move(path + ".meta", fileTempPath + ".meta");
                AssetDatabase.Refresh();

                // ensure that we have processed the destroy event from above
                yield return null;
            }

            yield return GetEnterPlayMode(Mode.Edit);
            {
                var w = GetLiveConversionWorld(Mode.Edit);
                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<DependsOnAssetTransitiveTestAuthoring.Component>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                EntitiesAssert.Contains(w.EntityManager,
                    EntityMatch.Partial(new DependsOnAssetTransitiveTestAuthoring.Component { Value = 15 }));

                // Move assets out of unity view. In this case, we are moving the files to the temp folder
                Assert.IsTrue(File.Exists(fileTempPath), $"'{fileTempPath}' doesn't exist");
                File.Move(fileTempPath, path);
                File.Move(fileTempPath + ".meta", path + ".meta");
                AssetDatabase.Refresh();

                // In the editor, undoing the deletion would restore the reference, but this doesn't immediately work
                // in code. So we're doing it manually for now.
                var b = GameObject.Find("B");

                // Make sure we find the right asset
                DependsOnAssetTransitiveTestScriptableObject asset = AssetDatabase.LoadAssetAtPath<DependsOnAssetTransitiveTestScriptableObject>(path);
                Assert.NotNull(asset, "Asset shouldn't be null");

                b.GetComponent<DependsOnAssetTransitiveTestAuthoring>().Dependency = asset;

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                EntitiesAssert.Contains(w.EntityManager,
                    EntityMatch.Partial(new DependsOnAssetTransitiveTestAuthoring.Component { Value = 17 }));
            }
        }

        [UnityTest]
        public IEnumerator IncrementalConversion_WithComponentDependencies_ReconvertsAllDependents([Values]Mode mode)
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aAuthoring = a.AddComponent<DependsOnComponentTransitiveTestAuthoring>();
                aAuthoring.SelfValue = 1;
                var b = new GameObject("B");
                var bAuthoring = b.AddComponent<DependsOnComponentTransitiveTestAuthoring>();
                bAuthoring.Dependency = aAuthoring;
                var c = new GameObject("C");
                var cAuthoring = c.AddComponent<DependsOnComponentTransitiveTestAuthoring>();
                cAuthoring.Dependency = bAuthoring;
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
                SceneManager.MoveGameObjectToScene(b, subScene.EditingScene);
                SceneManager.MoveGameObjectToScene(c, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                var a = GameObject.Find("A");
                var authoring = a.GetComponent<DependsOnComponentTransitiveTestAuthoring>();
                Assert.AreEqual(1, authoring.SelfValue);

                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType
                        .ReadWrite<DependsOnComponentTransitiveTestAuthoring.Component>());
                Assert.AreEqual(3, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                EntitiesAssert.Contains(w.EntityManager,
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 1}),
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 2}),
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 3}));

                Undo.RecordObject(authoring, "Change component value");
                authoring.SelfValue = 42;

                // it takes an extra frame to establish that something has changed when using RecordObject unless Flush is called
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(3, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                EntitiesAssert.Contains(w.EntityManager,
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 42}),
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 43}),
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 44}));

                Undo.PerformUndo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(3, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                EntitiesAssert.Contains(w.EntityManager,
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 1}),
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 2}),
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 3}));

                Undo.PerformRedo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(3, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                EntitiesAssert.Contains(w.EntityManager,
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 42}),
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 43}),
                    EntityMatch.Partial(new DependsOnComponentTransitiveTestAuthoring.Component {Value = 44}));
            }
        }

        [UnityTest]
        public IEnumerator IncrementalConversion_BakingType([Values] Mode mode)
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("Root");
                a.AddComponent<BakingTypeTestAuthoring>();
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                // We test if BakingTypeTestComponent is not present in the destination world, as it is a BakingOnlyType and it should be removed during the diff
                var testBakingOnlyQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<BakingTypeTestAuthoring.BakingTypeTestComponent>());
                Assert.AreEqual(0, testBakingOnlyQuery.CalculateEntityCount(), "BakingTypeTestComponent is a BakingOnlyType and should not be present in the destination world");
            }
        }

        [UnityTest]
        public IEnumerator BlobAssetStore_EndToEnd_Test()
        {
            var subScene = CreateEmptySubScene("TestSubScene", true);

            var go1 = new GameObject("go1");
            go1.AddComponent<BlobAssetStore_Test_Authoring>();
            SceneManager.MoveGameObjectToScene(go1, subScene.EditingScene);

            var go2 = new GameObject("go2");
            go2.AddComponent<BlobAssetStore_Test_Authoring>();
            SceneManager.MoveGameObjectToScene(go2, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var test = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<BlobAssetStore_Test_Component>());
                Assert.AreEqual(2, test.CalculateEntityCount());

                using (var entityArray = test.ToEntityArray(Allocator.Temp))
                {
                    var comp = w.EntityManager.GetComponentData<BlobAssetStore_Test_Component>(entityArray[0]);
                    Assert.AreEqual(3, comp.BlobData.Value);

                    comp = w.EntityManager.GetComponentData<BlobAssetStore_Test_Component>(entityArray[1]);
                    Assert.AreEqual(3, comp.BlobData.Value);

                    LogAssert.Expect(LogType.Log, "Retrieve blobasset from store");
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalConversion_AddingStaticOptimizeEntity_ReconvertsObject([Values] Mode mode)
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("Root");
                SetTransformUsageFlags(a, TransformUsageFlags.Dynamic);
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                var testTagQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<LocalToWorld>());
                var staticTagQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<Static>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(0, staticTagQuery.CalculateEntityCount(), "Expected a non-static entity");

                Undo.AddComponent<StaticOptimizeEntity>(GameObject.Find("Root"));

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, staticTagQuery.CalculateEntityCount(), "Expected a static entity");

                Undo.PerformUndo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(0, staticTagQuery.CalculateEntityCount(), "Expected a non-static entity");

                Undo.PerformRedo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, staticTagQuery.CalculateEntityCount(), "Expected a static entity");
            }
        }

        [UnityTest]
        // Unstable on Linux: DOTS-5341
        [UnityPlatform(exclude = new[] {RuntimePlatform.LinuxEditor})]
        public IEnumerator IncrementalConversion_AddingStaticOptimizeEntityToChild_ReconvertsObject([Values]Mode mode)
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("Root");
                SetTransformUsageFlags(a, TransformUsageFlags.Dynamic);
                var c = new GameObject("Child");
                SetTransformUsageFlags(c, TransformUsageFlags.Dynamic);
                c.transform.SetParent(a.transform);
                var d = new GameObject("ChildChild");
                d.transform.SetParent(c.transform);
                SetTransformUsageFlags(d, TransformUsageFlags.Dynamic);

                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                var testTagQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<LocalToWorld>());
                var staticTagQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<Static>());
                Assert.AreEqual(3, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(0, staticTagQuery.CalculateEntityCount(), "Expected a non-static entity");

                Undo.AddComponent<StaticOptimizeEntity>(GameObject.Find("Child"));

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(2, staticTagQuery.CalculateEntityCount(), "Expected a static entity");

                Undo.PerformUndo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(3, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(0, staticTagQuery.CalculateEntityCount(), "Expected a non-static entity");

                Undo.PerformRedo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(2, staticTagQuery.CalculateEntityCount(), "Expected a static entity");
            }
        }

        [UnityTest]
        public IEnumerator IncrementalConversion_WithTransformDependency_ReconvertsAllDependents([Values]Mode mode)
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aAuthoring = a.AddComponent<DependsOnTransformTestAuthoring>();
                aAuthoring.Dependency = a.transform;
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                var a = GameObject.Find("A");

                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(
                        ComponentType.ReadWrite<DependsOnTransformTestAuthoring.Component>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(a.transform.localToWorldMatrix,
                    testTagQuery.GetSingleton<DependsOnTransformTestAuthoring.Component>().LocalToWorld);

                Undo.RecordObject(a.transform, "Change component value");
                a.transform.position = new Vector3(1, 2, 3);

                // it takes an extra frame to establish that something has changed when using RecordObject unless Flush is called
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(a.transform.localToWorldMatrix,
                    testTagQuery.GetSingleton<DependsOnTransformTestAuthoring.Component>().LocalToWorld);

                Undo.PerformUndo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(a.transform.localToWorldMatrix,
                    testTagQuery.GetSingleton<DependsOnTransformTestAuthoring.Component>().LocalToWorld);

                Undo.PerformRedo();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(a.transform.localToWorldMatrix,
                    testTagQuery.GetSingleton<DependsOnTransformTestAuthoring.Component>().LocalToWorld);
            }
        }

        [UnityTest]
        public IEnumerator IncrementalConversion_WithTextureDependencyInScene_ChangeCausesReconversion()
        {

            {
                var subScene = CreateEmptySubScene("TestSubScene", true);
                var a = new GameObject("A");
                var aAuthoring = a.AddComponent<DependencyTestAuthoring>();
                var texture = new Texture2D(16, 16) {filterMode = FilterMode.Bilinear};
                aAuthoring.Texture = texture;
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var a = GameObject.Find("A");
                var texture = a.GetComponent<DependencyTestAuthoring>().Texture;
                Assert.IsTrue(texture != null);
                var testQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ConversionDependencyData>());
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.IsTrue(testQuery.GetSingleton<ConversionDependencyData>().HasTexture);
                Assert.AreEqual(texture.filterMode, testQuery.GetSingleton<ConversionDependencyData>().TextureFilterMode,
                    "Initial conversion reported the wrong value");

                Undo.RecordObject(texture, "Change texture filtering");
                texture.filterMode = FilterMode.Point;
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(texture.filterMode, testQuery.GetSingleton<ConversionDependencyData>().TextureFilterMode,
                    "Updated conversion shows the wrong value");

                Undo.PerformUndo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(texture.filterMode, testQuery.GetSingleton<ConversionDependencyData>().TextureFilterMode,
                    "Updated conversion shows the wrong value");

                Undo.PerformRedo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(texture.filterMode, testQuery.GetSingleton<ConversionDependencyData>().TextureFilterMode,
                    "Updated conversion shows the wrong value");

                Undo.PerformUndo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(texture.filterMode, testQuery.GetSingleton<ConversionDependencyData>().TextureFilterMode,
                    "Updated conversion shows the wrong value");
            }
        }

        [UnityTest]
        public IEnumerator IncrementalConversion_WithTextureDependencyInScene_DestroyAndRecreateCausesReconversion()
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);
                var a = new GameObject("A");
                var aAuthoring = a.AddComponent<DependencyTestAuthoring>();
                var texture = new Texture2D(16, 16);
                aAuthoring.Texture = texture;
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var a = GameObject.Find("A");
                var texture = a.GetComponent<DependencyTestAuthoring>().Texture;
                Assert.IsTrue(texture != null);
                var testQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ConversionDependencyData>());
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.IsTrue(testQuery.GetSingleton<ConversionDependencyData>().HasTexture);

                Undo.DestroyObjectImmediate(texture);

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.IsFalse(testQuery.GetSingleton<ConversionDependencyData>().HasTexture);

                Undo.PerformUndo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.IsTrue(testQuery.GetSingleton<ConversionDependencyData>().HasTexture);

                Undo.PerformRedo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.IsFalse(testQuery.GetSingleton<ConversionDependencyData>().HasTexture);

                Undo.PerformUndo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.IsTrue(testQuery.GetSingleton<ConversionDependencyData>().HasTexture);
            }
        }

#if !DOTS_DISABLE_DEBUG_NAMES
        [UnityTest]
        public IEnumerator IncrementalConversion_WhenGameObjectIsRenamed_TargetEntityIsRenamed([Values]Mode mode)
        {
            {
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    var go = new GameObject("TestGameObject");
                    go.AddComponent<TestComponentAuthoring>();
                    return new List<GameObject> { go };
                });
            }

            yield return GetEnterPlayMode(mode);
            var w = GetLiveConversionWorld(mode);

            {
                var e = GetTargetEntity(w);
                Assert.That(w.EntityManager.GetName(e), Is.EqualTo("TestGameObject"));
                var go = Object.FindFirstObjectByType<TestComponentAuthoring>().gameObject;

                Undo.RecordObject(go, "Renaming");
                go.name = "The renamed GameObject";
                Undo.FlushUndoRecordObjects();
            }

            yield return UpdateEditorAndWorld(w);

            {
                var e = GetTargetEntity(w);
                Assert.That(w.EntityManager.GetName(e), Is.EqualTo("The renamed GameObject"));
            }

            static Entity GetTargetEntity(World w)
            {
                using var testTagQuery = w.EntityManager.CreateEntityQuery(typeof(TestComponentAuthoring.UnmanagedTestComponent));
                Assert.That(testTagQuery.CalculateEntityCount(), Is.EqualTo(1), "Expected a game object to be converted");
                return testTagQuery.GetSingletonEntity();
            }
        }
#endif

        private static readonly (string, Func<GameObject>)[] TransformTestCaseData =
        {
            ("SingleObject", () => new GameObject("Root")),
            ("RootAndChild", () =>
            {
                var root = new GameObject("Root");
                var c = new GameObject("Child");
                c.transform.SetParent(root.transform);
                c.transform.localPosition = new Vector3(1, 1, 1);
                return root;
            }),
            ("RootAndChildren", () =>
            {
                var root = new GameObject("Root");
                for (int i = 0; i < 5; i++)
                {
                    var c = new GameObject("Child " + i);
                    c.transform.SetParent(root.transform);
                    c.transform.localPosition = new Vector3(i, i, i);
                    c.transform.localScale = new Vector3(i, i, 1);
                    c.transform.localRotation = Quaternion.Euler(i * 15, 0, -i * 15);
                }
                return root;
            }),
            ("DeepHierarchy", () =>
            {
                var root = new GameObject("Root");
                var current = root;
                for (int i = 0; i < 10; i++)
                {
                    var c = new GameObject("Child " + i);
                    c.transform.SetParent(current.transform);
                    c.transform.localPosition = new Vector3(1, 1, 1);
                    current = c;
                }
                return root;
            }),
            ("SingleStaticObject", () =>
            {
                var root = new GameObject("Root");
                root.AddComponent<StaticOptimizeEntity>();
                return root;
            }),
            ("RootAndStaticChild", () =>
            {
                var root = new GameObject("Root");
                var c = new GameObject("Child");
                c.transform.SetParent(root.transform);
                c.transform.localPosition = new Vector3(1, 1, 1);
                c.AddComponent<StaticOptimizeEntity>();
                return root;
            }),
            ("RootAndStaticChildWithChild", () =>
            {
                var root = new GameObject("Root");
                var c = new GameObject("Child");
                c.transform.SetParent(root.transform);
                c.transform.localPosition = new Vector3(1, 1, 1);
                c.AddComponent<StaticOptimizeEntity>();
                var cc = new GameObject("ChildChild");
                cc.transform.SetParent(root.transform);
                cc.transform.localPosition = new Vector3(1, 1, 1);
                return root;
            }),
            ("StaticRootAndChild", () =>
            {
                var root = new GameObject("Root");
                root.AddComponent<StaticOptimizeEntity>();
                var c = new GameObject("Child");
                c.transform.SetParent(root.transform);
                c.transform.localPosition = new Vector3(1, 1, 1);
                return root;
            }),
            ("RootAndChildren_WithSomeStatic", () =>
            {
                var root = new GameObject("Root");
                for (int i = 0; i < 5; i++)
                {
                    var c = new GameObject("Child " + i);
                    c.transform.SetParent(root.transform);
                    c.transform.localPosition = new Vector3(i, i, i);
                    c.transform.localScale = new Vector3(i, i, 1);
                    c.transform.localRotation = Quaternion.Euler(i * 15, 0, -i * 15);
                    if (i % 2 == 0)
                        c.AddComponent<StaticOptimizeEntity>();
                }
                return root;
            }),
        };

        public static IEnumerable TransformTestCases
        {
            get
            {
                foreach (var entry in TransformTestCaseData)
                {
                    var tc = new TestCaseData(entry.Item2).SetName(entry.Item1);
                    tc.HasExpectedResult = true;
                    yield return tc;
                }
            }
        }

        [UnityTest, TestCaseSource(typeof(LiveBakingAndConversionBase), nameof(TransformTestCases))]
        public IEnumerator IncrementalConversion_WithHierarchy_PatchesTransforms(Func<GameObject> makeGameObject)
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                var root = makeGameObject();
                SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var roots = Object.FindFirstObjectByType<SubScene>().EditingScene.GetRootGameObjects();
                var stack = new Stack<GameObject>(roots);
                var objs = new List<Transform>();
                while (stack.Count > 0)
                {
                    var top = stack.Pop();
                    objs.Add(top.transform);
                    var transform = top.transform;
                    int n = transform.childCount;
                    for (int i = 0; i < n; i++)
                        stack.Push(transform.GetChild(i).gameObject);
                }

                foreach (var t in objs)
                {
                    Undo.RecordObject(t, "change transform");
                    t.position += new Vector3(1, -1, 0);
                    t.rotation *= Quaternion.Euler(15, 132, 0.1f);
                    t.localScale *= 1.5f;
                    Undo.FlushUndoRecordObjects();

                    yield return UpdateEditorAndWorld(w);
                }
            }
        }

        protected static readonly IEnumerable<Type> k_DefaultEntitySceneComponentsBaking = new[] { typeof(EntityGuid), typeof(SceneSection), typeof(SceneTag), typeof(LocalToWorld), typeof(LocalTransform), typeof(Simulate), typeof(TransformAuthoringCopyForTest)};

        [UnityTest]
        public IEnumerator IncrementalConversion_DefaultEntitySceneComponents()
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("Root");
                SetTransformUsageFlags(a, TransformUsageFlags.Dynamic);
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
                var query = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<EntityGuid>());
                var entity = query.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(1, entity.Length);

                EntitiesAssert.Contains(w.EntityManager, EntityMatch.Exact(entity[0], k_DefaultEntitySceneComponentsBaking));
            }
        }

        [Description("Ensure the SceneSectionComponent is updated in incremental conversion")]
        [UnityTest]
        public IEnumerator IncrementalConversion_SceneSectionComponent_IsBaked()
        {
            var subScene = CreateEmptySubScene("TestSubScene", true);
            var sceneObject = new GameObject("SceneObject");
            var aAuthoring = sceneObject.AddComponent<SceneSectionComponent>();
            aAuthoring.SectionIndex = 1;
            SceneManager.MoveGameObjectToScene(sceneObject, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                // Verify it baked first
                var testQuery = w.EntityManager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<SceneSection>().WithOptions(EntityQueryOptions.IncludePrefab));
                {
                    Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                    var prefabEntity = testQuery.GetSingletonEntity();
                    var sectionComponent = w.EntityManager.GetSharedComponent<SceneSection>(prefabEntity);
                    Assert.AreEqual(1, sectionComponent.Section);
                }

                // Remove reference to prefab and rebake
                Undo.RecordObject(aAuthoring, "Change value");
                aAuthoring.SectionIndex = 2;
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                {
                    Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted after rebake");
                    var prefabEntity = testQuery.GetSingletonEntity();
                    var sectionComponent = w.EntityManager.GetSharedComponent<SceneSection>(prefabEntity);
                    Assert.AreEqual(2, sectionComponent.Section, "The SceneSection component was not updated");
                }
            }
        }

        //@TODO: DOTS-5459
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [UnityTest]
        public IEnumerator IncrementalConversion_WithCompanionComponent_RemoveComponentCausesReconversion()
        {
            var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            BakingUtility.AddAdditionalCompanionComponentType(typeof(CompanionComponentTestAuthoring));

            {
                var subScene = CreateEmptySubScene("TestSubScene", true);
                var a = new GameObject("A");
                SetTransformUsageFlags(a, TransformUsageFlags.Dynamic);
                var aAuthoring = a.AddComponent<CompanionComponentTestAuthoring>();
                aAuthoring.Value = 16;
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var a = GameObject.Find("A");
                var authoring = a.GetComponent<CompanionComponentTestAuthoring>();
                var testQuery = w.EntityManager.CreateEntityQuery(typeof(CompanionComponentTestAuthoring));
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(16, GetCompanionComponent().Value);

                Undo.DestroyObjectImmediate(authoring);
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testQuery.CalculateEntityCount(), "Expected no game object to be converted");

                Undo.PerformUndo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(16, GetCompanionComponent().Value);

                Undo.PerformRedo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testQuery.CalculateEntityCount(), "Expected no game object to be converted");

                Undo.PerformUndo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(16, GetCompanionComponent().Value);

                CompanionComponentTestAuthoring GetCompanionComponent() =>
                    w.EntityManager.GetComponentObject<CompanionComponentTestAuthoring>(testQuery.GetSingletonEntity());
            }
        }

        [UnityTest]
        public IEnumerator IncrementalConversion_WithCompanionComponent_ChangeCausesReconversion()
        {
            var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            BakingUtility.AddAdditionalCompanionComponentType(typeof(CompanionComponentTestAuthoring));

            {
                var subScene = CreateEmptySubScene("TestSubScene", true);
                var a = new GameObject("A");
                SetTransformUsageFlags(a, TransformUsageFlags.Dynamic);
                var aAuthoring = a.AddComponent<CompanionComponentTestAuthoring>();
                aAuthoring.Value = 16;
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var a = GameObject.Find("A");
                var authoring = a.GetComponent<CompanionComponentTestAuthoring>();
                var testQuery = w.EntityManager.CreateEntityQuery(typeof(CompanionComponentTestAuthoring));
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(16, GetCompanionComponent().Value);

                Undo.RecordObject(authoring, "Change value");
                authoring.Value = 7;
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(7, GetCompanionComponent().Value);

                Undo.PerformUndo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(16, GetCompanionComponent().Value);

                Undo.PerformRedo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(7, GetCompanionComponent().Value);

                Undo.PerformUndo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(16, GetCompanionComponent().Value);

                CompanionComponentTestAuthoring GetCompanionComponent() =>
                    w.EntityManager.GetComponentObject<CompanionComponentTestAuthoring>(testQuery.GetSingletonEntity());
            }
        }

        [UnityTest]
        public IEnumerator IncrementalConversion_WithCompanionComponent_UnrelatedChangeDoesNotCauseReconversion()
        {
            var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            BakingUtility.AddAdditionalCompanionComponentType(typeof(CompanionComponentTestAuthoring));

            {
                var subScene = CreateEmptySubScene("TestSubScene", true);
                var a = new GameObject("A");
                var aAuthoring = a.AddComponent<CompanionComponentTestAuthoring>();
                aAuthoring.Value = 16;
                var b = new GameObject("B");
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
                SceneManager.MoveGameObjectToScene(b, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var a = GameObject.Find("A");
                var authoring = a.GetComponent<CompanionComponentTestAuthoring>();
                var b = GameObject.Find("B");
                var testQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<CompanionComponentTestAuthoring>());
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(16, GetCompanionComponent().Value);

                // Change the value, but don't record any undo events. This way the change is not propagated, but can be
                // used as a sentinel.
                authoring.Value = 7;

                Undo.RegisterCompleteObjectUndo(b, "Test Undo");
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(16, GetCompanionComponent().Value);

                Undo.PerformUndo();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Assert.AreEqual(16, GetCompanionComponent().Value);

                CompanionComponentTestAuthoring GetCompanionComponent() =>
                    w.EntityManager.GetComponentObject<CompanionComponentTestAuthoring>(testQuery.GetSingletonEntity());
            }
        }

        [UnityTest]
        public IEnumerator IncrementalConversion_WithCompanionComponent_TransformUpdatesWhenCompanionGameObjectChanges()
        {
            // Regression test for DOTS-7375, live baking of companion components would break transform update.

            var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            BakingUtility.AddAdditionalCompanionComponentType(typeof(CompanionComponentTestAuthoring));

            var subScene = CreateEmptySubScene("TestSubScene", true);

            // Creating two authoring GameObjects with a companion component each. The transform update system is
            // sensitive to ordering so there's a higher chance of triggering an issue with more than one entity.

            var authoringA = new GameObject("authoringA");
            authoringA.AddComponent<CompanionComponentTestAuthoring>().Value = 123;
            authoringA.transform.position = new Vector3(1, 2, 3);
            SceneManager.MoveGameObjectToScene(authoringA, subScene.EditingScene);

            var authoringB = new GameObject("authoringB");
            authoringB.AddComponent<CompanionComponentTestAuthoring>().Value = 567;
            authoringB.transform.position = new Vector3(5, 6, 7);
            SceneManager.MoveGameObjectToScene(authoringB, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            // First step, find the runtime companion GameObjects and entities by matching the values in the companion components.

            GameObject runtimeA = default;
            GameObject runtimeB = default;

            Entity entityA = Entity.Null;
            Entity entityB = Entity.Null;

            var testQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CompanionComponentTestAuthoring>());
            var entities = testQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                var companion = w.EntityManager.GetComponentData<CompanionLink>(entity).Companion.Value;
                var value = companion.GetComponent<CompanionComponentTestAuthoring>().Value;

                switch(value)
                {
                    case 123:
                    {
                        entityA = entity;
                        runtimeA = companion;
                        break;
                    }
                    case 567:
                    {
                        entityB = entity;
                        runtimeB = companion;
                        break;
                    }
                }
            }

            // At this point we expect the transform on the companion GameObjects to be unchanged.

            Assert.AreEqual(runtimeA.transform.position, new Vector3(1, 2, 3));
            Assert.AreEqual(runtimeB.transform.position, new Vector3(5, 6, 7));

            // Let's change the transform of the entities.

            w.EntityManager.SetComponentData(entityA,
                w.EntityManager.GetComponentData<LocalTransform>(entityA).WithPosition(new float3(2,3,4)));
            w.EntityManager.SetComponentData(entityB,
                w.EntityManager.GetComponentData<LocalTransform>(entityB).WithPosition(new float3(6,7,8)));

            // Without a world update, the transforms shouldn't be propagated and remain unchanged.

            Assert.AreEqual(runtimeA.transform.position, new Vector3(1, 2, 3));
            Assert.AreEqual(runtimeB.transform.position, new Vector3(5, 6, 7));

            // Tick the world, this runs the transform update system.

            yield return UpdateEditorAndWorld(w);

            // Check that the entity transforms have been propagated to the companion GameObjects.

            Assert.AreEqual(runtimeA.transform.position, new Vector3(2, 3, 4));
            Assert.AreEqual(runtimeB.transform.position, new Vector3(6, 7, 8));

            // Let's trigger a live baking change by editing one of the companion GameObjects.

            var component = authoringA.GetComponent<CompanionComponentTestAuthoring>();
            Undo.RecordObject(component, "Change AuthoringA value");
            component.Value = 112233; // Whatever, doesn't matter.
            Undo.FlushUndoRecordObjects();

            // Tick the world to cause live baking to run and propagate to the runtime.

            yield return UpdateEditorAndWorld(w);

            // At the time of writing this test, both companion GameObjects change. This is
            // excessive since we only changed one of the two but that's a separate problem.
            // In terms of correctness, refreshing our references to both GameObjects is valid.

            runtimeA = w.EntityManager.GetComponentData<CompanionLink>(entityA).Companion;
            runtimeB = w.EntityManager.GetComponentData<CompanionLink>(entityB).Companion;

            // Ensure the transform changes have not been reverted on the companion GameObjects.

            Assert.AreEqual(runtimeA.transform.position, new Vector3(2, 3, 4));
            Assert.AreEqual(runtimeB.transform.position, new Vector3(6, 7, 8));

            // Let's change the transform of the entities again.

            w.EntityManager.SetComponentData(entityA,
                w.EntityManager.GetComponentData<LocalTransform>(entityA).WithPosition(new float3(3,4,5)));
            w.EntityManager.SetComponentData(entityB,
                w.EntityManager.GetComponentData<LocalTransform>(entityB).WithPosition(new float3(7,8,9)));

            // Without a world update, the transforms shouldn't be propagated and remain unchanged.

            Assert.AreEqual(runtimeA.transform.position, new Vector3(2, 3, 4));
            Assert.AreEqual(runtimeB.transform.position, new Vector3(6, 7, 8));

            // Tick the world, this runs the transform update system.

            yield return UpdateEditorAndWorld(w);

            // Check that the entity transforms have been propagated to the companion GameObjects.

            Assert.AreEqual(runtimeA.transform.position, new Vector3(3, 4, 5));
            Assert.AreEqual(runtimeB.transform.position, new Vector3(7, 8, 9));
        }
#endif

#if false // remove to enable fuzz testing
        static IEnumerable<int> FuzzTestingSeeds()
        {
            for (int i = 0; i < 300; i++)
                yield return i;
        }

        [UnityTest, Explicit]
        public IEnumerator IncrementalConversion_FuzzTesting_Edit([ValueSource(nameof(FuzzTestingSeeds))]int seed) => IncrementalConversion_FuzzTesting(Fuzz.FullFuzzer, seed);
        [UnityTest, Explicit]
        public IEnumerator IncrementalConversion_FuzzTesting_Hierarchy_Edit([ValueSource(nameof(FuzzTestingSeeds))]int seed) => IncrementalConversion_FuzzTesting(Fuzz.HierarchyFuzzer, seed);
        [UnityTest, Explicit]
        public IEnumerator IncrementalConversion_FuzzTesting_HierarchyWithStatic_Edit([ValueSource(nameof(FuzzTestingSeeds))]int seed) => IncrementalConversion_FuzzTesting(Fuzz.HierarchyWithStaticFuzzer, seed);
        [UnityTest, Explicit]
        public IEnumerator IncrementalConversion_FuzzTesting_Transform_Edit([ValueSource(nameof(FuzzTestingSeeds))]int seed) => IncrementalConversion_FuzzTesting(Fuzz.TransformFuzzer, seed);
        [UnityTest, Explicit]
        public IEnumerator IncrementalConversion_FuzzTesting_Dependencies_Edit([ValueSource(nameof(FuzzTestingSeeds))]int seed) => IncrementalConversion_FuzzTesting(Fuzz.DependenciesFuzzer, seed);
        [UnityTest, Explicit]
        public IEnumerator IncrementalConversion_FuzzTesting_EnabledDisabled_Edit([ValueSource(nameof(FuzzTestingSeeds))]int seed) => IncrementalConversion_FuzzTesting(Fuzz.HierarchyWithToggleEnabled, seed);
#endif
        IEnumerator IncrementalConversion_FuzzTesting(Fuzz.FuzzerSetup fuzzer, int seed)
        {
            {
                CreateEmptySubScene("TestSubScene", true);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var subScene = Object.FindFirstObjectByType<SubScene>();

                DependsOnComponentTestAuthoring.Versions.Clear();
                Console.WriteLine($"Running fuzz test with seed {seed} and the following actions:");
                foreach (var wa in fuzzer.WeightedActions)
                    Console.WriteLine(" - " + wa.Item1);
                var state = Fuzz.NewState(seed, SceneManager.GetActiveScene(), subScene.EditingScene);
                SceneManager.SetActiveScene(subScene.EditingScene);
                int stepsWithoutUpdate = 0;
                const int steps = 100;
                const int updateThreshold = 20;
                for (int s = 0; s < steps; s++)
                {
                    Fuzz.FuzzerAction fuzzerAction = stepsWithoutUpdate >= updateThreshold ? Fuzz.FuzzerAction.UpdateFrame : fuzzer.SampleAction(ref state.Rng);
                    Fuzz.Command cmd;
                    while (!Fuzz.BuildCommand(ref state, fuzzerAction, out cmd))
                        fuzzerAction = fuzzer.SampleAction(ref state.Rng);

                    Console.WriteLine("Fuzz." + Fuzz.FormatCommand(cmd) + ",");
                    Fuzz.ApplyCommand(ref state, cmd);
                    if (fuzzerAction == Fuzz.FuzzerAction.UpdateFrame)
                    {
                        stepsWithoutUpdate = 0;
                        yield return UpdateEditorAndWorld(w);
                    }
                    else
                        stepsWithoutUpdate++;
                }
            }
        }

        [UnityTest, TestCaseSource(typeof(LiveLinkBakingEditorTests), nameof(FuzzTestCases))]
        public IEnumerator IncrementalConversionTests(List<Fuzz.Command> commands) => RunCommands(commands);

        public static IEnumerable FuzzTestCases
        {
            get
            {
                foreach (var entry in FuzzerTestCaseData)
                {
                    var tc = new TestCaseData(entry.Item2).SetName(entry.Item1);
                    tc.HasExpectedResult = true;
                    yield return tc;
                }
            }
        }

        private static readonly ValueTuple<string, List<Fuzz.Command>>[] FuzzerTestCaseData =
        {
            ("Create_ThenMoveOutOfSubscene", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.UpdateFrameCommand(),
            }),
            ("Reparent_ThenDeleteChild", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 2),
                Fuzz.ReparentCommand(1, 2),
                Fuzz.DeleteGameObjectCommand(2),
                Fuzz.UpdateFrameCommand(),
            }),
            ("Create_ThenInvalidateHierarchy", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.InvalidateHierarchyCommand(1),
                Fuzz.UpdateFrameCommand(),
            }),
            ("Create_Invalidate_ThenMoveParent", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.InvalidateGameObjectCommand(1),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.UpdateFrameCommand(),
            }),
            ("Create_InvalidateHierarchy_ThenMoveParent", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.InvalidateHierarchyCommand(1),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.UpdateFrameCommand(),
            }),
            ("Create_ThenDelete", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.InvalidateGameObjectCommand(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.DeleteGameObjectCommand(1),
                Fuzz.UpdateFrameCommand(),
            }),
            ("Create_ThenInvalidateTwice", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 0),
                Fuzz.InvalidateGameObjectCommand(0),
                Fuzz.InvalidateGameObjectCommand(0),
                Fuzz.UpdateFrameCommand(),
            }),
            ("MoveToRoot_ThenBackToSubScene", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.MoveToSubSceneCommand(0),
                Fuzz.UpdateFrameCommand(),
            }),
            ("Reparent_ThenMoveNewParentToRootScene", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 0),
                Fuzz.CreateGameObjectCommand(1, 0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ReparentCommand(1, 0),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.UpdateFrameCommand(),
            }),
            ("MoveToRootSceneAndBack_ThenReparent", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 0),
                Fuzz.CreateGameObjectCommand(1, 0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.MoveToSubSceneCommand(0),
                Fuzz.ReparentCommand(1, 0),
                Fuzz.UpdateFrameCommand(),
            }),
            ("MoveToRootScene_ThenCreate", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.CreateGameObjectCommand(1, 0),
                Fuzz.UpdateFrameCommand(),
            }),
            ("MoveBetweenScenes_ThenAddChild", new List<Fuzz.Command>
            {
               Fuzz.CreateGameObjectCommand(0, 0),
               Fuzz.CreateGameObjectCommand(1, 0),
               Fuzz.UpdateFrameCommand(),
               Fuzz.MoveToRootSceneCommand(0),
               Fuzz.MoveToSubSceneCommand(0),
               Fuzz.MoveToRootSceneCommand(0),
               Fuzz.MoveToSubSceneCommand(0),
               Fuzz.UpdateFrameCommand(),
               Fuzz.ReparentCommand(1, 0)
            }),
            ("Reparent_ThenDeleteParentsParent", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 0),
                Fuzz.CreateGameObjectCommand(1, 0),
                Fuzz.CreateGameObjectCommand(2, 0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ReparentCommand(2, 1),
                Fuzz.DeleteGameObjectCommand(0),
                Fuzz.UpdateFrameCommand(),
            }),
            ("MoveBetweenScenes_ThenDeleteChild", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.MoveToSubSceneCommand(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.DeleteGameObjectCommand(1),
                Fuzz.UpdateFrameCommand(),
            }),
            ("Reparent_ThenMoveWithChildren", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 0),
                Fuzz.CreateGameObjectCommand(1, 1),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ReparentCommand(1, 0),
                Fuzz.MoveToRootSceneCommand(0),
            }),
            ("ReparentDirectlyAfterCreate_ThenDelete", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.UpdateFrameCommand(),
                Fuzz.CreateGameObjectCommand(2, 1),
                Fuzz.ReparentCommand(3, 1),
                Fuzz.UpdateFrameCommand(),
                Fuzz.DeleteGameObjectCommand(3),
            }),
            ("MoveIn_ThenDirtyChild_ThenMoveOut", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.MoveToSubSceneCommand(0),
                Fuzz.InvalidateHierarchyCommand(1),
                Fuzz.MoveToRootSceneCommand(0),
            }),
            ("Create_ThenInvalidate_ThenMoveOut_ThenMakeChildARoot", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.InvalidateHierarchyCommand(1),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.ReparentCommand(1, -1),
            }),
            //TODO: Disabled as this dependency does not trigger in Baking in the same way
            //TODO: Jira was down when I made this comment
            /*
            ("CreateTwo_ThenAddDependency_ThenMoveOutAndBackIn", new List<Fuzz.Command>
            {
                // This test is challenging because it adds a dependency to something that is then moved out of the
                // scene and returned later.
                Fuzz.CreateGameObjectCommand(0, 0),
                Fuzz.CreateGameObjectCommand(1, 0),
                Fuzz.SetDependencyCommand(0, 1),
                Fuzz.MoveToRootSceneCommand(1),
                Fuzz.UpdateFrameCommand(),
                Fuzz.MoveToSubSceneCommand(1),
            }),
            */
            ("CreateTwo_ThenAddDependency_ThenChangeAndDelete_ThenUndo", new List<Fuzz.Command>
            {
                // This test is similar to the previous test but again very challenging: We set up a dependency of 0 on
                // 1, then we modify and delete 1. This causes a reconversion of 0 during which 1 doesn't exist anymore.
                // Finally, we undo the deletion of 1 which then _cannot_ cause a reconversion of 0.
                Fuzz.CreateGameObjectCommand(0, 0),
                Fuzz.CreateGameObjectCommand(1, 0),
                Fuzz.SetDependencyCommand(0, 1),
                Fuzz.UpdateFrameCommand(),
                Fuzz.InvalidateGameObjectCommand(1),
                Fuzz.DeleteGameObjectCommand(1),
                Fuzz.UpdateFrameCommand(),
                Fuzz.UndoCommand()
            }),
            ("CreateAndAddSelfDependency_ThenInvalidate", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 0),
                Fuzz.SetDependencyCommand(0, 0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.TouchComponent(0),
            }),
            ("CreateTwoWithDependency_ThenMoveInAndOutAndDelete", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0),
                Fuzz.CreateGameObjectCommand(1),
                Fuzz.SetDependencyCommand(1, 0),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.MoveToSubSceneCommand(0),
                Fuzz.DeleteGameObjectCommand(0),
            }),
            ("CreateTwoWithMutualDependency_ThenReSetDependencyAndCreateAnother", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0),
                Fuzz.CreateGameObjectCommand(1),
                Fuzz.SetDependencyCommand(0, 1),
                Fuzz.SetDependencyCommand(1, 0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.SetDependencyCommand(0, 1),
                Fuzz.SetDependencyCommand(1, 0),
                Fuzz.CreateGameObjectCommand(2),
            }),
            ("CreateObjectWithChild_ThenMarkStatic", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ToggleStaticOptimizeEntity(0),
            }),
            ("CreateAndDisable_ThenAddChild", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0),
                Fuzz.ToggleObjectActive(0),
                Fuzz.CreateGameObjectCommand(1),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ReparentCommand(1, 0),
            }),
            ("CreateHierarchyAndDisable_ThenRemoveChild", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.ToggleObjectActive(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ReparentCommand(1, -1),
            }),
            ("EntangleHierarchy_ThenDisable_ThenEntangleMore", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.CreateGameObjectCommand(2),
                Fuzz.CreateGameObjectCommand(3, 1),
                Fuzz.ReparentCommand(3, 1),

                Fuzz.UpdateFrameCommand(),
                Fuzz.ToggleObjectActive(2),
                Fuzz.ToggleObjectActive(4),
                Fuzz.ToggleObjectActive(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ToggleObjectActive(1),

                Fuzz.ReparentCommand(2, 4),
            }),
            ("CreateAndDisableRootWithChild_ThenAttachChildToChild", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.ToggleObjectActive(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.CreateGameObjectCommand(2),
                Fuzz.ReparentCommand(2, 1),
            }),
            ("CreateAndDeactivateRoot_ThenMakeChildARoot", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.ToggleObjectActive(0),
                Fuzz.ReparentCommand(1, -1),
            }),
            ("CreateHierarchyWithDisabledGroups_ThenMoveRootOutOfSubscene", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 2),
                Fuzz.ReparentCommand(1, 2),
                Fuzz.ToggleObjectActive(0),
                Fuzz.ToggleObjectActive(2),
                Fuzz.UpdateFrameCommand(),
                Fuzz.MoveToRootSceneCommand(0),
                Fuzz.ToggleObjectActive(2),
            }),
            ("CreateDisabledHierarchy_ThenEnable_AndThenDisableChild", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.CreateGameObjectCommand(2, 0),
                Fuzz.ReparentCommand(2, 1),
                Fuzz.ToggleObjectActive(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ToggleObjectActive(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ToggleObjectActive(1),
            }),
            ("CreateInactiveHierarchy_ThenEnableWhileMovingChildOut", new List<Fuzz.Command>
            {
                // This tests that a child is not removed due to being contained in the LinkedEntityGroup of the root
                // when it is moved elsewhere before the root is deleted.
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.ToggleObjectActive(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ToggleObjectActive(0),
                Fuzz.ReparentCommand(1, -1),
                Fuzz.DeleteGameObjectCommand(0),
            }),
            ("LongIntricateSetup_ThenFinallyReparentToChangeALinkedEntityGroup", new List<Fuzz.Command>
            {
                // This test checks that entity reference change patches are applied to an entity, even when that entity
                // is removed from a LinkedEntityGroup at the same time.
                Fuzz.CreateGameObjectCommand(0),
                Fuzz.CreateGameObjectCommand(1),
                Fuzz.CreateGameObjectCommand(2, 1),
                Fuzz.ToggleObjectActive(3),
                Fuzz.DeleteGameObjectCommand(1),
                Fuzz.UpdateFrameCommand(),
                Fuzz.CreateGameObjectCommand(4, 1),
                Fuzz.ReparentCommand(4, 3),
                Fuzz.MoveToRootSceneCommand(2),
                Fuzz.UpdateFrameCommand(),
                Fuzz.CreateGameObjectCommand(6, 0),
                Fuzz.MoveToSubSceneCommand(2),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ReparentCommand(5, 6),
            }),
            ("CreateTwoDisabledRoots_ThenMoveChildFromOneToAnotherAndDeletePreviousRoot", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0, 1),
                Fuzz.ToggleObjectActive(0),
                Fuzz.CreateGameObjectCommand(2),
                Fuzz.ToggleObjectActive(2),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ReparentCommand(1, 2),
                Fuzz.DeleteGameObjectCommand(0),
            }),
            ("DeletingChild_UpdatesLinkedEntityGroupInRoot", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0,1),
                Fuzz.AddLinkedEntityGroupRootComponent(0),
                Fuzz.UpdateFrameCommand(),
                Fuzz.DeleteGameObjectCommand(1)
            }),
            ("MovingChild_UpdatesLinkedEntityGroupInRoot", new List<Fuzz.Command>
            {
                Fuzz.CreateGameObjectCommand(0,1),
                Fuzz.AddLinkedEntityGroupRootComponent(0),
                Fuzz.AddCreateAdditionalEntitiesComponent(1),
                Fuzz.UpdateFrameCommand(),
                Fuzz.ReparentCommand(1, -1),
                Fuzz.UpdateFrameCommand(),
            })
        };

        IEnumerator RunCommands(List<Fuzz.Command> commands)
        {
            {
                CreateEmptySubScene("TestSubScene", true);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
                var subScene = Object.FindFirstObjectByType<SubScene>();

                DependsOnComponentTestAuthoring.Versions.Clear();
                Console.WriteLine($"Running test with {commands.Count} commands");
                var state = Fuzz.NewState(0, SceneManager.GetActiveScene(), subScene.EditingScene);
                SceneManager.SetActiveScene(subScene.EditingScene);
                for (int c = 0; c < commands.Count; c++)
                {
                    var cmd = commands[c];
                    Console.WriteLine("Fuzz." + Fuzz.FormatCommand(cmd) + ",");
                    Fuzz.ApplyCommand(ref state, cmd);
                    if (cmd.FuzzerAction == Fuzz.FuzzerAction.UpdateFrame)
                        yield return UpdateEditorAndWorld(w);
                }

                yield return UpdateEditorAndWorld(w);
            }
        }

        internal static class Fuzz
        {
            public static State NewState(int seed, Scene rootScene, Scene subScene) => new State
            {
                RootScene = rootScene,
                SubScene = subScene,
                GameObjectsInSubscene = new List<int>(),
                GameObjectsInRootScene = new List<int>(),
                IdToGameObject = new List<GameObject>(),
                GameObjectToId = new Dictionary<GameObject, int>(),
                Rng = Mathematics.Random.CreateFromIndex((uint)seed)
            };

            public struct State
            {
                public Scene RootScene;
                public Scene SubScene;
                public int UndoStackSize;
                public int RedoStackSize;
                public int NextGameObject;
                public int Step;
                public List<int> GameObjectsInSubscene;
                public List<int> GameObjectsInRootScene;
                public List<GameObject> IdToGameObject;
                public Dictionary<GameObject, int> GameObjectToId;
                public Mathematics.Random Rng;
                public int TotalNumObjects => GameObjectsInSubscene.Count + GameObjectsInRootScene.Count;
            }

            public struct Command
            {
                public FuzzerAction FuzzerAction;
                public int TargetGameObjectId;
                public int AdditionalData;

                public Command(FuzzerAction a, int target, int data = 0)
                {
                    FuzzerAction = a;
                    TargetGameObjectId = target;
                    AdditionalData = data;
                }

                public override string ToString() => $"{FuzzerAction}({TargetGameObjectId}, {AdditionalData})";
            }

            public static string FormatCommand(Command cmd)
            {
                switch (cmd.FuzzerAction)
                {
                    case FuzzerAction.CreateGameObject:
                        return nameof(CreateGameObjectCommand) + $"({cmd.TargetGameObjectId}, {cmd.AdditionalData})";
                    case FuzzerAction.DeleteGameObject:
                        return nameof(DeleteGameObjectCommand) + $"({cmd.TargetGameObjectId})";
                    case FuzzerAction.ReparentGameObject:
                        return nameof(ReparentCommand) + $"({cmd.TargetGameObjectId}, {cmd.AdditionalData})";
                    case FuzzerAction.Undo:
                        return nameof(UndoCommand) + "()";
                    case FuzzerAction.Redo:
                        return nameof(RedoCommand) + "()";
                    case FuzzerAction.TouchComponent:
                        return nameof(TouchComponent) + $"({cmd.TargetGameObjectId})";
                    case FuzzerAction.MoveGameObjectToRootScene:
                        return nameof(MoveToRootSceneCommand) + $"({cmd.TargetGameObjectId})";
                    case FuzzerAction.MoveGameObjectToSubScene:
                        return nameof(MoveToSubSceneCommand) + $"({cmd.TargetGameObjectId})";
                    case FuzzerAction.InvalidateGameObject:
                        return nameof(InvalidateGameObjectCommand) + $"({cmd.TargetGameObjectId})";
                    case FuzzerAction.InvalidateGameObjectHierarchy:
                        return nameof(InvalidateHierarchyCommand) + $"({cmd.TargetGameObjectId})";
                    case FuzzerAction.SetDependency:
                        return nameof(SetDependencyCommand) + $"({cmd.TargetGameObjectId}, {cmd.AdditionalData})";
                    case FuzzerAction.ToggleStaticOptimizeEntity:
                        return nameof(ToggleStaticOptimizeEntity) + $"({cmd.TargetGameObjectId})";
                    case FuzzerAction.ToggleObjectActive:
                        return nameof(ToggleObjectActive) + $"({cmd.TargetGameObjectId})";
                    case FuzzerAction.Translate:
                        return nameof(Translate) + $"({cmd.TargetGameObjectId}, {cmd.AdditionalData})";
                    case FuzzerAction.Scale:
                        return nameof(Scale) + $"({cmd.TargetGameObjectId}, {cmd.AdditionalData})";
                    case FuzzerAction.Rotate:
                        return nameof(Rotate) + $"({cmd.TargetGameObjectId}, {cmd.AdditionalData})";
                    case FuzzerAction.UpdateFrame:
                        return nameof(UpdateFrameCommand) + "()";
                    case FuzzerAction.AddLinkedEntityGroupRootComponent:
                        return nameof(AddLinkedEntityGroupRootComponent) + $"({cmd.TargetGameObjectId})";
                    case FuzzerAction.AddCreateAdditionalEntitiesComponent:
                        return nameof(AddCreateAdditionalEntitiesComponent) + $"({cmd.TargetGameObjectId})";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public static Command CreateGameObjectCommand(int id, int numChildren = 0) =>
                new Command(FuzzerAction.CreateGameObject, id, numChildren);

            public static Command DeleteGameObjectCommand(int id) => new Command(FuzzerAction.DeleteGameObject, id);
            public static Command UpdateFrameCommand() => new Command(FuzzerAction.UpdateFrame, 0);
            public static Command MoveToRootSceneCommand(int id) => new Command(FuzzerAction.MoveGameObjectToRootScene, id);
            public static Command MoveToSubSceneCommand(int id) => new Command(FuzzerAction.MoveGameObjectToSubScene, id);
            public static Command RedoCommand() => new Command(FuzzerAction.Redo, 0);
            public static Command UndoCommand() => new Command(FuzzerAction.Undo, 0);
            public static Command InvalidateGameObjectCommand(int id) => new Command(FuzzerAction.InvalidateGameObject, id);

            public static Command ToggleStaticOptimizeEntity(int id) =>
                new Command(FuzzerAction.ToggleStaticOptimizeEntity, id);
            public static Command ToggleObjectActive(int id) => new Command(FuzzerAction.ToggleObjectActive, id);
            public static Command Translate(int id, int seed) =>
                new Command(FuzzerAction.ToggleStaticOptimizeEntity, id, seed);
            public static Command Rotate(int id, int seed) =>
                new Command(FuzzerAction.ToggleStaticOptimizeEntity, id, seed);
            public static Command Scale(int id, int seed) =>
                new Command(FuzzerAction.ToggleStaticOptimizeEntity, id, seed);
            public static Command InvalidateHierarchyCommand(int id) =>
                new Command(FuzzerAction.InvalidateGameObjectHierarchy, id);

            public static Command TouchComponent(int id) => new Command(FuzzerAction.TouchComponent, id);

            public static Command ReparentCommand(int id, int newParent) =>
                new Command(FuzzerAction.ReparentGameObject, id, newParent);

            public static Command SetDependencyCommand(int id, int dependsOn) =>
                new Command(FuzzerAction.SetDependency, id, dependsOn);

            public static Command AddLinkedEntityGroupRootComponent(int id) => new Command(FuzzerAction.AddLinkedEntityGroupRootComponent, id);

            public static Command AddCreateAdditionalEntitiesComponent(int id) => new Command(FuzzerAction.AddCreateAdditionalEntitiesComponent, id);

            static IEnumerable<(int, GameObject)> GetSubtree(State state, GameObject root)
            {
                var toDelete = new Stack<GameObject>();
                toDelete.Push(root);
                while (toDelete.Count > 0)
                {
                    var top = toDelete.Pop();
                    int n = top.transform.childCount;
                    for (int i = 0; i < n; i++)
                        toDelete.Push(top.transform.GetChild(i).gameObject);
                    yield return (state.GameObjectToId[top], top);
                }
            }

            static IEnumerable<(int, GameObject)> GetSubtree(State state, int id)
            {
                var root = state.IdToGameObject[id];
                return GetSubtree(state, root);
            }

            static void DeleteGameObject(ref State state, int id)
            {
                var root = state.IdToGameObject[id];
                foreach (var (c, go) in GetSubtree(state, id))
                {
                    state.GameObjectsInSubscene.Remove(c);
                    state.GameObjectsInRootScene.Remove(c);
                }

                Undo.DestroyObjectImmediate(root);
            }

            static GameObject CreateGameObject(ref State state)
            {
                int id = state.NextGameObject;
                state.NextGameObject += 1;
                var go = new GameObject(id.ToString());
                go.AddComponent<DependsOnComponentTestAuthoring>();
                state.GameObjectToId.Add(go, id);
                state.IdToGameObject.Add(go);
                state.GameObjectsInSubscene.Add(id);
                return go;
            }

            static void CreateGameObject(ref State state, int numChildren)
            {
                var go = CreateGameObject(ref state);
                for (int i = 0; i < numChildren; i++)
                {
                    var c = CreateGameObject(ref state);
                    c.transform.SetParent(go.transform);
                }

                Undo.RegisterCreatedObjectUndo(go, "step " + state.Step);
            }

            static void UpdateStateAfterRedoUndo(ref State state)
            {
                state.GameObjectsInRootScene.Clear();
                state.GameObjectsInSubscene.Clear();
                foreach (var root in state.SubScene.GetRootGameObjects())
                {
                    foreach (var (id, _) in GetSubtree(state, root))
                        state.GameObjectsInSubscene.Add(id);
                }

                foreach (var root in state.RootScene.GetRootGameObjects())
                {
                    if (root.TryGetComponent<SubScene>(out _))
                        continue;
                    foreach (var (id, _) in GetSubtree(state, root))
                        state.GameObjectsInRootScene.Add(id);
                }
            }

            static void BumpVersion(GameObject go)
            {
                var v = DependsOnComponentTestAuthoring.Versions;
                v.TryGetValue(go, out var version);
                version++;
                v[go] = version;
            }

            public static void ApplyCommand(ref State state, Command command)
            {
                var name = "step " + state.Step;
                var gos = state.IdToGameObject;
                GameObject target = null;
                if (command.TargetGameObjectId >= 0 && command.TargetGameObjectId < gos.Count)
                    target = gos[command.TargetGameObjectId];
                bool incrementUndo = true;
                switch (command.FuzzerAction)
                {
                    case FuzzerAction.CreateGameObject:
                    {
                        if (command.TargetGameObjectId > -1 && command.TargetGameObjectId != state.NextGameObject)
                            throw new InvalidOperationException(
                                $"The requested game object id {command.TargetGameObjectId} is invalid, use {state.NextGameObject} instead");
                        CreateGameObject(ref state, command.AdditionalData);
                        break;
                    }
                    case FuzzerAction.DeleteGameObject:
                    {
                        DeleteGameObject(ref state, command.TargetGameObjectId);
                        break;
                    }
                    case FuzzerAction.ReparentGameObject:
                    {
                        Transform other = null;
                        if (command.AdditionalData >= 0)
                            other = gos[command.AdditionalData].transform;
                        Undo.SetTransformParent(target.transform, other, name);
                        BumpVersion(target);
                        break;
                    }
                    case FuzzerAction.Undo:
                    {
                        Undo.PerformUndo();
                        UpdateStateAfterRedoUndo(ref state);
                        // unclear whose GO's version we should bump
                        state.RedoStackSize++;
                        state.UndoStackSize--;
                        incrementUndo = false;
                        break;
                    }
                    case FuzzerAction.Redo:
                    {
                        Undo.PerformRedo();
                        UpdateStateAfterRedoUndo(ref state);
                        // unclear whose GO's version we should bump
                        state.RedoStackSize--;
                        state.UndoStackSize++;
                        incrementUndo = false;
                        break;
                    }
                    case FuzzerAction.ToggleObjectActive:
                    {
                        Undo.RecordObject(target, name);
                        target.SetActive(!target.activeSelf);
                        BumpVersion(target);
                        break;
                    }
                    case FuzzerAction.TouchComponent:
                    {
                        Undo.RegisterCompleteObjectUndo(target.GetComponent<DependsOnComponentTestAuthoring>(), name);
                        BumpVersion(target);
                        break;
                    }
                    case FuzzerAction.MoveGameObjectToRootScene:
                    {
                        Undo.MoveGameObjectToScene(target, state.RootScene, name);
                        foreach (var (c, g) in GetSubtree(state, command.TargetGameObjectId))
                        {
                            BumpVersion(g);
                            state.GameObjectsInSubscene.Remove(c);
                            state.GameObjectsInRootScene.Add(c);
                        }
                        break;
                    }
                    case FuzzerAction.MoveGameObjectToSubScene:
                    {
                        Undo.MoveGameObjectToScene(target, state.SubScene, name);
                        foreach (var (c, g) in GetSubtree(state, command.TargetGameObjectId))
                        {
                            BumpVersion(g);
                            state.GameObjectsInRootScene.Remove(c);
                            state.GameObjectsInSubscene.Add(c);
                        }
                        break;
                    }
                    case FuzzerAction.InvalidateGameObject:
                    {
                        Undo.RegisterCompleteObjectUndo(target, name);
                        BumpVersion(target);
                        break;
                    }
                    case FuzzerAction.InvalidateGameObjectHierarchy:
                    {
                        Undo.RegisterFullObjectHierarchyUndo(target, name);
                        BumpVersion(target);
                        break;
                    }
                    case FuzzerAction.SetDependency:
                    {
                        var dependency = gos[command.AdditionalData];
                        var d = target.GetComponent<DependsOnComponentTestAuthoring>();
                        Undo.RegisterCompleteObjectUndo(d, name);
                        BumpVersion(target);
                        d.Other = dependency;
                        break;
                    }
                    case FuzzerAction.ToggleStaticOptimizeEntity:
                    {
                        var staticOpt = target.GetComponent<StaticOptimizeEntity>();
                        if (staticOpt == null)
                            Undo.AddComponent<StaticOptimizeEntity>(target);
                        else
                            Undo.DestroyObjectImmediate(staticOpt);
                        break;
                    }
                    case FuzzerAction.Translate:
                    {
                        var transform = target.transform;
                        Undo.RegisterCompleteObjectUndo(transform, name);
                        BumpVersion(target);
                        var x = command.AdditionalData;
                        transform.localPosition += new Vector3(x * 0.01f, x * 0.02f, x * -0.01f);
                        break;
                    }
                    case FuzzerAction.Rotate:
                    {
                        var transform = target.transform;
                        Undo.RegisterCompleteObjectUndo(transform, name);
                        BumpVersion(target);
                        var x = command.AdditionalData;
                        transform.localPosition += new Vector3(x * 0.01f, x * 0.02f, x * -0.01f);
                        break;
                    }
                    case FuzzerAction.Scale:
                    {
                        var transform = target.transform;
                        Undo.RegisterCompleteObjectUndo(transform, name);
                        BumpVersion(target);
                        var x = command.AdditionalData;
                        transform.localScale = Vector3.Scale(transform.localScale, new Vector3(1 + x * 0.01f, 1 + x * 0.02f, 1 + x * -0.01f));
                        break;
                    }
                    case FuzzerAction.UpdateFrame:
                        incrementUndo = false;
                        break;
                    case FuzzerAction.AddLinkedEntityGroupRootComponent:
                        Undo.AddComponent<LinkedEntityGroupAuthoring>(target);
                        break;
                    case FuzzerAction.AddCreateAdditionalEntitiesComponent:
                        Undo.AddComponent<CreateAdditionalEntitiesAuthoring>(target).number = 2;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (incrementUndo)
                {
                    state.UndoStackSize++;
                    Undo.IncrementCurrentGroup();
                    Undo.FlushUndoRecordObjects();
                }
                state.Step++;
            }

            static int Sample(ref Mathematics.Random rng, List<int> xs) => xs[rng.NextInt(0, xs.Count)];

            static int Sample(ref Mathematics.Random rng, List<int> xs, List<int> ys)
            {
                var idx = rng.NextInt(0, xs.Count + ys.Count);
                if (idx < xs.Count)
                    return xs[idx];
                idx -= xs.Count;
                return ys[idx];
            }

            static bool CanReparent(GameObject p, GameObject c)
            {
                // p should not already be the parent of c
                if (c.transform.parent == p.transform || p.scene != c.scene)
                    return false;
                // otherwise, make sure that c is not above p
                var t = p.transform;
                while (t != null)
                {
                    if (t == c.transform)
                        return false;
                    t = t.parent;
                }

                return true;
            }

            static int GetRoot(ref State state, int id)
            {
                var go = state.IdToGameObject[id];
                while (go.transform.parent != null)
                    go = go.transform.parent.gameObject;
                return state.GameObjectToId[go];
            }

            public static bool BuildCommand(ref State state, FuzzerAction fuzzerAction, out Command command)
            {
                command = new Command
                {
                    FuzzerAction = fuzzerAction
                };
                switch (fuzzerAction)
                {
                    case FuzzerAction.CreateGameObject:
                        command.TargetGameObjectId = state.NextGameObject;
                        // num children
                        command.AdditionalData = state.Rng.NextInt(0, 5);
                        return true;
                    case FuzzerAction.ReparentGameObject:
                    {
                        int totalCount = state.TotalNumObjects;
                        if (totalCount <= 1)
                            return false;

                        command.TargetGameObjectId = Sample(ref state.Rng, state.GameObjectsInSubscene, state.GameObjectsInRootScene);
                        var child = state.IdToGameObject[command.TargetGameObjectId];
                        if (child.transform.parent != null && state.Rng.NextFloat() < .1f + 1f / totalCount)
                        {
                            // make sure to also cover the case that game objects are moved to the root
                            command.AdditionalData = -1;
                            return true;
                        }

                        const int maxAttempts = 5;
                        for (int i = 0; i < maxAttempts; i++)
                        {
                            int idx = Sample(ref state.Rng, state.GameObjectsInSubscene, state.GameObjectsInRootScene);
                            if (idx == command.TargetGameObjectId)
                                continue;
                            var parent = state.IdToGameObject[idx];
                            if (CanReparent(parent, child))
                            {
                                command.AdditionalData = idx;
                                return true;
                            }
                        }

                        return false;
                    }
                    case FuzzerAction.Undo:
                        return state.UndoStackSize > 0;
                    case FuzzerAction.Redo:
                        return state.RedoStackSize > 0;
                    case FuzzerAction.MoveGameObjectToSubScene:
                    {
                        if (state.GameObjectsInRootScene.Count == 0)
                            return false;
                        command.TargetGameObjectId = GetRoot(ref state, Sample(ref state.Rng, state.GameObjectsInRootScene));
                        return true;
                    }
                    case FuzzerAction.ToggleObjectActive:
                    case FuzzerAction.DeleteGameObject:
                    {
                        if (state.TotalNumObjects == 0)
                            return false;
                        command.TargetGameObjectId = Sample(ref state.Rng, state.GameObjectsInSubscene, state.GameObjectsInRootScene);
                        return true;
                    }
                    case FuzzerAction.MoveGameObjectToRootScene:
                    {
                        if (state.GameObjectsInSubscene.Count == 0)
                            return false;
                        command.TargetGameObjectId = GetRoot(ref state, Sample(ref state.Rng, state.GameObjectsInSubscene));
                        return true;
                    }
                    case FuzzerAction.TouchComponent:
                    case FuzzerAction.InvalidateGameObject:
                    case FuzzerAction.InvalidateGameObjectHierarchy:
                    {
                        if (state.GameObjectsInSubscene.Count == 0)
                            return false;
                        command.TargetGameObjectId = Sample(ref state.Rng, state.GameObjectsInSubscene);
                        return true;
                    }
                    case FuzzerAction.SetDependency:
                    {
                        if (state.TotalNumObjects == 0)
                            return false;
                        command.TargetGameObjectId = Sample(ref state.Rng, state.GameObjectsInSubscene, state.GameObjectsInRootScene);
                        command.AdditionalData = Sample(ref state.Rng, state.GameObjectsInSubscene, state.GameObjectsInRootScene);
                        return true;
                    }
                    case FuzzerAction.ToggleStaticOptimizeEntity:
                    {
                        if (state.TotalNumObjects == 0)
                            return false;
                        command.TargetGameObjectId = Sample(ref state.Rng, state.GameObjectsInSubscene, state.GameObjectsInRootScene);
                        return true;
                    }
                    case FuzzerAction.Translate:
                    case FuzzerAction.Rotate:
                    case FuzzerAction.Scale:
                    {
                        if (state.TotalNumObjects == 0)
                            return false;
                        command.TargetGameObjectId = Sample(ref state.Rng, state.GameObjectsInSubscene, state.GameObjectsInRootScene);
                        // seed for transform change
                        command.AdditionalData = state.Rng.NextInt(-5, 6);
                        return true;
                    }
                    case FuzzerAction.UpdateFrame:
                        return true;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public enum FuzzerAction
            {
                CreateGameObject,
                DeleteGameObject,
                ReparentGameObject,
                Undo,
                Redo,
                TouchComponent,
                MoveGameObjectToRootScene,
                MoveGameObjectToSubScene,
                InvalidateGameObject,
                InvalidateGameObjectHierarchy,
                SetDependency,
                ToggleStaticOptimizeEntity,
                ToggleObjectActive,
                Translate,
                Rotate,
                Scale,
                UpdateFrame,
                AddLinkedEntityGroupRootComponent,
                AddCreateAdditionalEntitiesComponent
            }

            public static FuzzerSetup HierarchyFuzzer => new FuzzerSetup
            {
                WeightedActions = new List<(FuzzerAction, int)>
                {
                    (FuzzerAction.CreateGameObject, 15),
                    (FuzzerAction.DeleteGameObject, 10),
                    (FuzzerAction.ReparentGameObject, 10),
                    //(FuzzerAction.Undo, 10),
                    //(FuzzerAction.Redo, 10),
                    (FuzzerAction.MoveGameObjectToRootScene, 10),
                    (FuzzerAction.MoveGameObjectToSubScene, 10),
                    (FuzzerAction.UpdateFrame, 10),
                },
            };

            public static FuzzerSetup HierarchyWithToggleEnabled => HierarchyFuzzer.With(
                (FuzzerAction.ToggleObjectActive, 30)
            );

            public static FuzzerSetup HierarchyWithStaticFuzzer => HierarchyFuzzer.With(
                (FuzzerAction.ToggleStaticOptimizeEntity, 10)
            );

            public static FuzzerSetup TransformFuzzer => HierarchyFuzzer.With(
                (FuzzerAction.ToggleStaticOptimizeEntity, 10),
                (FuzzerAction.Translate, 10),
                (FuzzerAction.Rotate, 10),
                (FuzzerAction.Scale, 10)
            );

            public static FuzzerSetup DependenciesFuzzer => new FuzzerSetup
            {
                WeightedActions = new List<(FuzzerAction, int)>
                {
                    (FuzzerAction.CreateGameObject, 20),
                    (FuzzerAction.DeleteGameObject, 10),
                    (FuzzerAction.ReparentGameObject, 10),
                    (FuzzerAction.Undo, 10),
                    (FuzzerAction.Redo, 10),
                    (FuzzerAction.TouchComponent, 15),
                    (FuzzerAction.MoveGameObjectToRootScene, 10),
                    (FuzzerAction.MoveGameObjectToSubScene, 10),
                    (FuzzerAction.InvalidateGameObject, 15),
                    (FuzzerAction.InvalidateGameObjectHierarchy, 15),
                    (FuzzerAction.SetDependency, 20),
                    (FuzzerAction.UpdateFrame, 10)
                },
            };

            public static FuzzerSetup FullFuzzer => new FuzzerSetup
            {
                WeightedActions = new List<(FuzzerAction, int)>
                {
                    (FuzzerAction.CreateGameObject, 20),
                    (FuzzerAction.DeleteGameObject, 10),
                    (FuzzerAction.ReparentGameObject, 10),
                    (FuzzerAction.Undo, 10),
                    (FuzzerAction.Redo, 10),
                    (FuzzerAction.TouchComponent, 15),
                    (FuzzerAction.MoveGameObjectToRootScene, 10),
                    (FuzzerAction.MoveGameObjectToSubScene, 10),
                    (FuzzerAction.InvalidateGameObject, 15),
                    (FuzzerAction.InvalidateGameObjectHierarchy, 15),
                    (FuzzerAction.SetDependency, 20),
                    (FuzzerAction.ToggleStaticOptimizeEntity, 10),
                    (FuzzerAction.ToggleObjectActive, 10),
                    (FuzzerAction.Translate, 10),
                    (FuzzerAction.Rotate, 10),
                    (FuzzerAction.Scale, 10),
                    (FuzzerAction.UpdateFrame, 10),
                },
            };

            public struct FuzzerSetup
            {
                private int _totalWeight;
                private int[] _runningSumWeights;
                public List<(FuzzerAction, int)> WeightedActions;

                public FuzzerAction SampleAction(ref Mathematics.Random rng)
                {
                    if (_runningSumWeights == null)
                    {
                        _runningSumWeights = new int[WeightedActions.Count];
                        int s = 0;
                        for (int i = 0; i < _runningSumWeights.Length; i++)
                        {
                            s += WeightedActions[i].Item2;
                            _runningSumWeights[i] = s;
                        }

                        _totalWeight = s;
                    }

                    int w = rng.NextInt(0, _totalWeight);
                    for (int i = 0; i < _runningSumWeights.Length; i++)
                    {
                        if (w < _runningSumWeights[i])
                            return WeightedActions[i].Item1;
                    }

                    return WeightedActions[WeightedActions.Count - 1].Item1;
                }

                public FuzzerSetup With(params (FuzzerAction action, int weight)[] more)
                {
                    var fs = new FuzzerSetup {
                        WeightedActions = new List<(FuzzerAction, int)>(WeightedActions)
                    };
                    fs.WeightedActions.AddRange(more);
                    return fs;
                }
            }
        }
    }

    [TestFixture]
    // Unstable on Linux: DOTS-5341
    [UnityPlatform(exclude = new[] {RuntimePlatform.LinuxEditor})]
    class LiveLinkBakingEditorTests : LiveBakingAndConversionBase
    {
        static List<Type> PreviousAdditionalBakingSystems;

        [SetUp]
        public new void SetUp()
        {
            base.SetUp();
            PreviousAdditionalBakingSystems = new List<Type>(LiveConversionSettings.AdditionalConversionSystems);
        }

        [TearDown]
        public void TearDown()
        {
            //base.TearDown();
            LiveConversionSettings.AdditionalConversionSystems.Clear();
            LiveConversionSettings.AdditionalConversionSystems.AddRange(PreviousAdditionalBakingSystems);
        }

        [OneTimeSetUp]
        public new void OneTimeSetUp()
        {
            this.LiveConversionTest.IsBakingEnabled = true;
            base.OneTimeSetUp();

            Assert.AreEqual(0, LiveConversionSettings.AdditionalConversionSystems.Count);
            LiveConversionSettings.AdditionalConversionSystems.Add(typeof(TransformAuthoringCopyForTestSystem));
        }

        [OneTimeTearDown]
        public new void OneTimeTearDown()
        {
            base.OneTimeTearDown();
            LiveConversionSettings.AdditionalConversionSystems.Clear();
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_EnabledChanged([Values]bool initiallyEnabled, [Values]Mode mode)
        {
            bool enabled = initiallyEnabled;
            GameObject root = null;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("Root");
                    var authoring = root.AddComponent<TestComponentIsSelfEnabledAuthoring>();
                    authoring.enabled = enabled;
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var testActiveQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(TestComponentIsSelfEnabledAuthoring.SelfEnabled)}, Options = EntityQueryOptions.IncludeDisabledEntities});

                var bakingComponent = root.GetComponent<TestComponentIsSelfEnabledAuthoring>();
                int expected = bakingComponent.enabled ? 1 : 0;
                Assert.AreEqual(expected, testActiveQuery.CalculateEntityCount(), $"Expected {expected} Active Component");

                // Changing enable to false
                for (int index = 0; index < 3; ++index)
                {
                    Undo.RecordObject(bakingComponent, "Changing enable to false");
                    bakingComponent.enabled = !bakingComponent.enabled;
                    Undo.FlushUndoRecordObjects();
                    yield return UpdateEditorAndWorld(w);

                    expected = bakingComponent.enabled ? 1 : 0;
                    Assert.AreEqual(expected, testActiveQuery.CalculateEntityCount(), $"Expected {expected} Active Component");
                }
            }
        }
        [UnityTest]
        public IEnumerator IncrementalBaking_ForceBakingOnDisabledComponents_EnabledChanged([Values]Mode mode)
        {
            // One Authoring, two bakers, one Forcing on not-forcing
            GameObject root = null;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("Root");
                    var authoring = root.AddComponent<TestComponentForceBakingOnDisabledAuthoring>();
                    authoring.enabled = false;
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var testForcedBakingQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[]{typeof(TestComponentForceBakingOnDisabledAuthoring.ForcedBaking)},
                    Options = EntityQueryOptions.IncludeDisabledEntities
                });
                var testNotForcedBakingQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[]{typeof(TestComponentForceBakingOnDisabledAuthoring.NotForcedBaking)},
                    Options = EntityQueryOptions.IncludeDisabledEntities
                });

                Assert.AreEqual(1, testForcedBakingQuery.CalculateEntityCount(), $"Expected Baker to force baking on disabled component.");
                Assert.AreEqual(0, testNotForcedBakingQuery.CalculateEntityCount(), $"Expected normal Baker to not have baked.");

            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_IsActiveAndEnabledChanged([Values]Mode mode)
        {
            GameObject root = null;
            GameObject otherGO = null;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("Root");
                    otherGO = new GameObject("Child");
                    var authoring = root.AddComponent<TestComponentIsActiveAndEnabledAuthoring>();
                    authoring.go = otherGO;
                    otherGO.AddComponent<TestComponentEnableAuthoring>();
                    return new List<GameObject> {root, otherGO};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var testActiveQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(TestComponentIsActiveAndEnabledAuthoring.ActiveAndEnabled)}, Options = EntityQueryOptions.IncludeDisabledEntities});
                var testInactiveQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(TestComponentIsActiveAndEnabledAuthoring.NoActiveAndEnabled)}, Options = EntityQueryOptions.IncludeDisabledEntities});

                Assert.AreEqual(1, testActiveQuery.CalculateEntityCount(), "Expected 1 Active Component");
                Assert.AreEqual(0, testInactiveQuery.CalculateEntityCount(), "Expected 0 Inactive Component");

                var bakingComponent = root.GetComponent<TestComponentIsActiveAndEnabledAuthoring>();
                var enableComponent = otherGO.GetComponent<TestComponentEnableAuthoring>();

                // Changing enable to false
                Undo.RecordObject(enableComponent, "Changing enable to false");
                enableComponent.enabled = false;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);
                Assert.IsTrue(bakingSystem.DidBake(bakingComponent));
                Assert.AreEqual(0, testActiveQuery.CalculateEntityCount(), "Expected 0 Active Component");
                Assert.AreEqual(1, testInactiveQuery.CalculateEntityCount(), "Expected 1 Inactive Component");

                // Changing enable back to true
                Undo.RecordObject(enableComponent, "Changing enable back to true");
                enableComponent.enabled = true;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);
                Assert.IsTrue(bakingSystem.DidBake(bakingComponent));
                Assert.AreEqual(1, testActiveQuery.CalculateEntityCount(), "Expected 1 Active Component");
                Assert.AreEqual(0, testInactiveQuery.CalculateEntityCount(), "Expected 0 Inactive Component");

                // Changing gameObject active to false
                Undo.RecordObject(otherGO, "Changing gameObject active to false");
                otherGO.SetActive(false);
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);
                Assert.IsTrue(bakingSystem.DidBake(bakingComponent));
                Assert.AreEqual(0, testActiveQuery.CalculateEntityCount(), "Expected 0 Active Component");
                Assert.AreEqual(1, testInactiveQuery.CalculateEntityCount(), "Expected 1 Inactive Component");

                // Changing gameObject active back to true
                Undo.RecordObject(otherGO, "Changing gameObject back active to true");
                otherGO.SetActive(true);
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);
                Assert.IsTrue(bakingSystem.DidBake(bakingComponent));
                Assert.AreEqual(1, testActiveQuery.CalculateEntityCount(), "Expected 1 Active Component");
                Assert.AreEqual(0, testInactiveQuery.CalculateEntityCount(), "Expected 0 Inactive Component");
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_EnableableComponents()
        {
            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aAuthoring = a.AddComponent<EnableableTestAuthoring>();

                aAuthoring.emptyEnableComponent = true;
                aAuthoring.enableComponent = true;
                aAuthoring.enableBuffer = true;
                aAuthoring.value = 2;

                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                // Check that all components are set to true
                Assert.AreEqual(1, bakingSystem.EntityManager.UniversalQuery.CalculateEntityCount());
                var entities = bakingSystem.EntityManager.UniversalQuery.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(true, bakingSystem.EntityManager.IsComponentEnabled<EmptyEnableableComponent>(entities[0]), "The empty Enableable Component is not set correctly");
                Assert.AreEqual(true, bakingSystem.EntityManager.IsComponentEnabled<EnableableComponent>(entities[0]), "The Enableable Component is not set correctly");
                Assert.AreEqual(true, bakingSystem.EntityManager.IsComponentEnabled<EnableableBuffer>(entities[0]), "The Enableable Buffer is not set correctly");

                // First Rebake
                // Change the enabled bits for all components
                var a = GameObject.Find("A");
                var authoringA = a.GetComponent<EnableableTestAuthoring>();

                Undo.RecordObject(authoringA, "Change the enabled bits for all components");
                authoringA.emptyEnableComponent = false;
                authoringA.enableComponent = false;
                authoringA.enableBuffer = false;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                // Check that all components are set to false
                Assert.AreEqual(1, bakingSystem.EntityManager.UniversalQuery.CalculateEntityCount());
                entities = bakingSystem.EntityManager.UniversalQuery.ToEntityArray(bakingSystem.WorldUpdateAllocator);
                Assert.AreEqual(false, bakingSystem.EntityManager.IsComponentEnabled<EmptyEnableableComponent>(entities[0]), "The empty Enableable Component is not set correctly");
                Assert.AreEqual(false, bakingSystem.EntityManager.IsComponentEnabled<EnableableComponent>(entities[0]), "The Enableable Component is not set correctly");
                Assert.AreEqual(false, bakingSystem.EntityManager.IsComponentEnabled<EnableableBuffer>(entities[0]), "The Enableable Buffer is not set correctly");


                // Second Rebake
                // Change values of the data component and buffer
                a = GameObject.Find("A");
                authoringA = a.GetComponent<EnableableTestAuthoring>();

                Undo.RecordObject(authoringA, "Change the enabled bits for all components");
                authoringA.value = 5;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                // Check none of the components are changed when a non-enable-bit change happens
                Assert.AreEqual(1, bakingSystem.EntityManager.UniversalQuery.CalculateEntityCount());
                entities = bakingSystem.EntityManager.UniversalQuery.ToEntityArray(bakingSystem.WorldUpdateAllocator);
                Assert.AreEqual(false, bakingSystem.EntityManager.IsComponentEnabled<EmptyEnableableComponent>(entities[0]), "The empty Enableable Component is incorrectly changed");
                Assert.AreEqual(false, bakingSystem.EntityManager.IsComponentEnabled<EnableableComponent>(entities[0]), "The Enableable Component is incorrectly changed");
                Assert.AreEqual(false, bakingSystem.EntityManager.IsComponentEnabled<EnableableBuffer>(entities[0]), "The Enableable Buffer is incorrectly changed");


                // Third Rebake
                // Change the enabled bits for all components
                a = GameObject.Find("A");
                authoringA = a.GetComponent<EnableableTestAuthoring>();

                Undo.RecordObject(authoringA, "Change the enabled bits for all components");
                authoringA.emptyEnableComponent = true;
                authoringA.enableComponent = true;
                authoringA.enableBuffer = true;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                // Check that all components are set to true
                Assert.AreEqual(1, bakingSystem.EntityManager.UniversalQuery.CalculateEntityCount());
                entities = bakingSystem.EntityManager.UniversalQuery.ToEntityArray(bakingSystem.WorldUpdateAllocator);
                Assert.AreEqual(true, bakingSystem.EntityManager.IsComponentEnabled<EmptyEnableableComponent>(entities[0]), "The empty Enableable Component is not set correctly");
                Assert.AreEqual(true, bakingSystem.EntityManager.IsComponentEnabled<EnableableComponent>(entities[0]), "The Enableable Component is not set correctly");
                Assert.AreEqual(true, bakingSystem.EntityManager.IsComponentEnabled<EnableableBuffer>(entities[0]), "The Enableable Buffer is not set correctly");


            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_CreateAndSetIncremental([Values(1, EntityDiffer.ComponentChangesBatchCount + 1)] int totalUpdate, [Values]Mode mode)
        {
            int totalCreate = EntityDiffer.ComponentChangesBatchCount + 1;

            // Create an initial set of GameObjects. This will be the ones updated in incremental later.
            List<GameObject> rootList = new List<GameObject>();
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    for (int index = 0; index < totalUpdate; ++index)
                    {
                        var go = new GameObject($"Root {index}");
                        go.transform.localPosition = new Vector3(2f, 2f, 2f);
                        go.AddComponent<TestComponentAuthoring>();
                        rootList.Add(go);
                    }

                    return rootList;
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                // We create new game objects
                for (int index = 0; index < totalCreate; ++index)
                {
                    GameObject child = new GameObject($"Child {index}");
                    child.transform.SetParent(rootList[index % totalUpdate].transform);

                    Undo.RegisterCreatedObjectUndo(child, $"Child {index}");
                }

                // We modify the position of the initially created GameObjects
                foreach (var root in rootList)
                {
                    Undo.RecordObject(root.transform, "Changing local position of the root");
                    root.transform.localPosition = new Vector3(10f, 10f, 10f);
                    Undo.FlushUndoRecordObjects();
                }

                yield return UpdateEditorAndWorld(w);

                var testActiveQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(LocalTransform), typeof(TestComponentAuthoring.UnmanagedTestComponent)}});
                Assert.AreEqual(totalUpdate, testActiveQuery.CalculateEntityCount(), $"Expected {totalUpdate} Entity");

                var localTransforms = testActiveQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                foreach (var localTransform in localTransforms)
                    Assert.AreEqual(10f, localTransform.Position.x, "Local position should match between ECS and the gameobject");
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_EntityDiffer_PointerInComponentThrows([Values] Mode mode)
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(NonSerializableComponentsBaker));
            string mainEntityName = "A";

            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aComponent = a.AddComponent<NonSerializableComponentsAuthoring>();

                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            var componentType = TypeManager.GetTypeInfo(TypeManager.GetTypeIndex<EcsTestDataPointer>()).Type;
            var mainEntityVariables = $"'{componentType}' on GameObject '{mainEntityName}'";

            var regexMain = new Regex($"ArgumentException: Blittable component type {mainEntityVariables}");
            LogAssert.ignoreFailingMessages = true;
            LogAssert.Expect(LogType.Exception, regexMain);

            yield return GetEnterPlayMode(mode);
            {
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_EntityDiffer_PointerInComponentThrows_AdditionalEntity([Values] Mode mode)
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(NonSerializableComponentsAdditionalEntityBaker));
            string mainEntityName = "A";
            string addEntityMB;

            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aComponent = a.AddComponent<NonSerializableComponentsAuthoring>();
                addEntityMB = aComponent.GetType().ToString();

                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            var componentType = TypeManager.GetTypeInfo(TypeManager.GetTypeIndex<EcsTestDataPointer>()).Type;
            var addEntityVariables = $"'{componentType}' on GameObject '{mainEntityName}', '{addEntityMB}'";

            var regexAdd = new Regex($"ArgumentException: Blittable component type {addEntityVariables}");
            LogAssert.ignoreFailingMessages = true;
            LogAssert.Expect(LogType.Exception, regexAdd);

            yield return GetEnterPlayMode(mode);
            {
            }
        }


        [UnityTest]
        public IEnumerator IncrementalBaking_EntityDiffer_EntityInSharedComponentThrows([Values] Mode mode)
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(NonSerializableSharedComponentsBaker));
            string mainEntityName = "A";

            SubScene subScene;

            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aComponent = a.AddComponent<NonSerializableComponentsAuthoring>();

                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            var componentType = TypeManager.GetTypeInfo(TypeManager.GetTypeIndex<EcsTestDataEntityShared>()).Type;
            var mainEntityVariables = $"'{componentType}' on GameObject '{mainEntityName}'";

            var regexMain = new Regex($"ArgumentException: Shared component type {mainEntityVariables}");
            LogAssert.ignoreFailingMessages = true;
            LogAssert.Expect(LogType.Exception, regexMain);

            yield return GetEnterPlayMode(mode);
            {
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_EntityDiffer_EntityInSharedComponentThrows_AdditionalEntity([Values] Mode mode)
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(NonSerializableSharedComponentAdditionalEntityBaker));
            string mainEntityName = "A";
            string addEntityMB;

            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aComponent = a.AddComponent<NonSerializableComponentsAuthoring>();
                addEntityMB = aComponent.GetType().ToString();

                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            var componentType = TypeManager.GetTypeInfo(TypeManager.GetTypeIndex<EcsTestDataEntityShared>()).Type;
            var addEntityVariables = $"'{componentType}' on GameObject '{mainEntityName}', '{addEntityMB}'";

            var regexAdd = new Regex($"ArgumentException: Shared component type {addEntityVariables}");
            LogAssert.ignoreFailingMessages = true;
            LogAssert.Expect(LogType.Exception, regexAdd);

            yield return GetEnterPlayMode(mode);
            {
            }
        }


        [UnityTest]
        public IEnumerator IncrementalBaking_GameObjectPropertyChanged([Values]Mode mode)
        {
            GameObject root = null;
            GameObject reference = null;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    reference = new GameObject("TestReferenceGameObject");
                    var authoring = root.AddComponent<TestGameObjectPropertiesChangeAuthoring>();
                    authoring.reference = reference;
                    return new List<GameObject> {root, reference};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var bakingComponent = root.GetComponent<TestGameObjectPropertiesChangeAuthoring>();

                // Changing the name
                Undo.RecordObject(reference, "Reference changed");
                reference.name = "TestReferenceGameObject2";
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);
                Assert.IsTrue(bakingSystem.DidBake(bakingComponent));

                // Changing the static value
                Undo.RecordObject(reference, "Reference changed");
                reference.isStatic = !reference.isStatic;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);
                Assert.IsTrue(bakingSystem.DidBake(bakingComponent));

                // Changing the Layer value
                Undo.RecordObject(reference, "Reference changed");
                reference.layer = 5;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);
                Assert.IsTrue(bakingSystem.DidBake(bakingComponent));

                // Changing the Tag value
                Undo.RecordObject(reference, "Reference changed");
                reference.tag = "EditorOnly";
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);
                Assert.IsTrue(bakingSystem.DidBake(bakingComponent));

                // Change the active state
                Undo.RecordObject(reference, "Reference changed");
                reference.SetActive(false);
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);
                Assert.IsTrue(bakingSystem.DidBake(bakingComponent));
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetProperties_ReconvertsObject([Values] Mode mode)
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(TestNameBaker), typeof(TestTagBaker), typeof(TestLayerBaker), typeof(TestReferenceBaker));

            GameObject root = null;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    root.tag = "Respawn";
                    root.layer = 0;
                    root.AddComponent<MockDataAuthoring>();
                    root.AddComponent<TestNameAuthoring>();
                    root.AddComponent<TestLayerAuthoring>();
                    root.AddComponent<TestTagAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                var testTagQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<TestNameComponent>(), ComponentType.ReadWrite<TestLayerComponent>(), ComponentType.ReadWrite<TestTagComponent>());

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var nameAuthoring = root.GetComponent<TestNameAuthoring>();
                var mockAuthoring = root.GetComponent<MockDataAuthoring>();
                var layerAuthoring = root.GetComponent<TestLayerAuthoring>();
                var tagAuthoring = root.GetComponent<TestTagAuthoring>();

                // Change name
                Undo.RecordObject(root, "Changed Name");
                string newName = "Test";
                root.name = newName;
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);
                Assert.IsTrue(bakingSystem.DidBake(mockAuthoring));
                Assert.IsTrue(bakingSystem.DidBake(nameAuthoring));
                Assert.IsFalse(bakingSystem.DidBake(layerAuthoring));
                Assert.IsFalse(bakingSystem.DidBake(tagAuthoring));

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                using var nameComponents = testTagQuery.ToComponentDataArray<TestNameComponent>(Allocator.TempJob);
                Assert.AreEqual(newName.GetHashCode(), nameComponents[0].value, $"Expected Name hash to be {newName.GetHashCode()}");

                // Change Layer
                Undo.RecordObject(root, "Changed layer");
                int newLayer = 5;
                root.layer = newLayer;
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);
                Assert.IsTrue(bakingSystem.DidBake(mockAuthoring));
                Assert.IsFalse(bakingSystem.DidBake(nameAuthoring));
                Assert.IsTrue(bakingSystem.DidBake(layerAuthoring));
                Assert.IsFalse(bakingSystem.DidBake(tagAuthoring));

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                using var layerComponents = testTagQuery.ToComponentDataArray<TestLayerComponent>(Allocator.TempJob);
                Assert.AreEqual(newLayer, layerComponents[0].value, $"Expected Layer value to be {newLayer}");

                // Change Tag
                Undo.RecordObject(root, "Changed tag");
                string newTag = "Player";
                root.tag = newTag;
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);
                Assert.IsTrue(bakingSystem.DidBake(mockAuthoring));
                Assert.IsFalse(bakingSystem.DidBake(nameAuthoring));
                Assert.IsFalse(bakingSystem.DidBake(layerAuthoring));
                Assert.IsTrue(bakingSystem.DidBake(tagAuthoring));

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                using var tagComponents = testTagQuery.ToComponentDataArray<TestTagComponent>(Allocator.TempJob);
                Assert.AreEqual(newTag.GetHashCode(), tagComponents[0].value, $"Expected Layer value to be {newTag.GetHashCode()}");
            }
        }

        static void ApplyStaticMask(GameObject[] objectArray, uint configMask, bool useFlag)
        {
            if (useFlag)
            {
                uint bitMask = 1;
                for (int index = 0; index < objectArray.Length; ++index)
                {
                    Undo.RecordObject(objectArray[index], "Changed isStatic");
                    var previousValue = objectArray[index].isStatic;
                    var newValue = ((configMask & bitMask) != 0);
                    if (previousValue != newValue)
                        objectArray[index].isStatic = newValue;
                    bitMask <<= 1;
                }
            }
            else
            {
                uint bitMask = 1;
                for (int index = 0; index < objectArray.Length; ++index)
                {
                    Undo.RecordObject(objectArray[index], "Changed isStatic");
                    var component = objectArray[index].GetComponent<StaticOptimizeEntity>();
                    if ((configMask & bitMask) != 0)
                    {
                        // Add if does not exist
                        if (!component)
                        {
                            //objectArray[index].AddComponent<StaticOptimizeEntity>();
                            Undo.AddComponent<StaticOptimizeEntity>(objectArray[index]);
                        }
                    }
                    else
                    {
                        // Remove if does exist
                        if (component)
                        {
                            //Object.DestroyImmediate(component);
                            Undo.DestroyObjectImmediate(component);
                        }
                    }
                    bitMask <<= 1;
                }
            }
            Undo.FlushUndoRecordObjects();
        }

        public enum TestBakingIsStaticMode
        {
            IsStaticFlag = 0,
            StaticOptimizeEntity = 1
        }

        /*
         * In a 3 level deep hierarchy (Root -> Child -> ChildChild), this test checks for all the combination of the 3 objects being or not static
         * This is represented with a bit field where each bit represents if one of the objects is static or not.
         * For example 5 = 011 => Root and Child will be static
         * staticFlagMode determines if the object is set to static with the GameObject flag or with StaticOptimize
         */
        [UnityTest]
        public IEnumerator IncrementalBaking_IsStatic_ReconvertsObject([Values] Mode mode, [Values] TestBakingIsStaticMode staticFlagMode)
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(TestIsStaticBaker));
            bool staticFlag = (staticFlagMode == TestBakingIsStaticMode.IsStaticFlag);

            GameObject root = null;
            GameObject child = null;
            GameObject child_child = null;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("Root");

                    child = new GameObject("Child");
                    child.transform.SetParent(root.transform);

                    child_child = new GameObject("ChildChild");
                    child_child.transform.SetParent(child.transform);

                    child.AddComponent<MockDataAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                GameObject[] objectArray = new[] {root, child, child_child};

                var mockAuthoring = child.GetComponent<MockDataAuthoring>();

                uint maxMaskValue = 7;
                uint rebakeMask = 3;

                for (uint restMask = 0; restMask <= maxMaskValue; ++restMask)
                {
                    ApplyStaticMask(objectArray, restMask, staticFlag);
                    yield return UpdateEditorAndWorld(w);
                    bakingSystem.ClearDidBake();

                    for (uint iterationMask = 0; iterationMask <= maxMaskValue; ++iterationMask)
                    {
                        ApplyStaticMask(objectArray, iterationMask, staticFlag);
                        yield return UpdateEditorAndWorld(w);
                        bool wasStatic = (restMask & rebakeMask) != 0;
                        bool isStatic = (iterationMask & rebakeMask) != 0;
                        bool needsRebake = (wasStatic != isStatic);
                        Assert.AreEqual(needsRebake, bakingSystem.DidBake(mockAuthoring));
                        bakingSystem.ClearDidBake();

                        ApplyStaticMask(objectArray, restMask, staticFlag);
                        yield return UpdateEditorAndWorld(w);
                        Assert.AreEqual(needsRebake, bakingSystem.DidBake(mockAuthoring));
                        bakingSystem.ClearDidBake();
                    }
                }
            }
        }

        /*
         * In a 3 level deep hierarchy (Root -> Child -> ChildChild), this test checks for all the combination of the 3 objects being or not static,
         * mixing the usage of GameObject flag and StaticOptimize. The outer loop uses one method and the inner loop uses the other
         * This is represented with a bit field where each bit represents if one of the objects is static or not.
         * For example 5 = 011 => Root and Child will be static
         * outerLoopStaticFlagMode determines if the object is set to static in the outer loop using the GameObject flag or StaticOptimizeEntity
         * The inner loop will use the opposite method
         */
        [UnityTest]
        public IEnumerator IncrementalBaking_IsStaticMix_ReconvertsObject([Values] Mode mode, [Values] TestBakingIsStaticMode outerLoopStaticFlagMode)
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(TestIsStaticBaker));

            bool outerLoopMode = (outerLoopStaticFlagMode == TestBakingIsStaticMode.IsStaticFlag);
            bool innerLoopMode = !outerLoopMode;

            GameObject root = null;
            GameObject child = null;
            GameObject child_child = null;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("Root");

                    child = new GameObject("Child");
                    child.transform.SetParent(root.transform);

                    child_child = new GameObject("ChildChild");
                    child_child.transform.SetParent(child.transform);

                    child.AddComponent<MockDataAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                GameObject[] objectArray = new[] {root, child, child_child};

                var mockAuthoring = child.GetComponent<MockDataAuthoring>();

                uint maxMaskValue = 7;
                uint rebakeMask = 3;

                for (uint restMask = 0; restMask <= maxMaskValue; ++restMask)
                {
                    // Use outterLoopMode
                    ApplyStaticMask(objectArray, restMask, outerLoopMode);
                    yield return UpdateEditorAndWorld(w);
                    bakingSystem.ClearDidBake();

                    for (uint iterationMask = 0; iterationMask <= maxMaskValue; ++iterationMask)
                    {
                        // Use innerLoopMode
                        ApplyStaticMask(objectArray, iterationMask, innerLoopMode);
                        yield return UpdateEditorAndWorld(w);
                        bool wasStatic = (restMask & rebakeMask) != 0;
                        bool isStatic = ((iterationMask | restMask) & rebakeMask) != 0;
                        bool needsRebake = (wasStatic != isStatic);
                        Assert.AreEqual(needsRebake, bakingSystem.DidBake(mockAuthoring));
                        bakingSystem.ClearDidBake();

                        // Reset innerLoopMode
                        ApplyStaticMask(objectArray, 0, innerLoopMode);
                        yield return UpdateEditorAndWorld(w);
                        Assert.AreEqual(needsRebake, bakingSystem.DidBake(mockAuthoring));
                        bakingSystem.ClearDidBake();
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_Prefabs_RemovePrefabReference_PrefabIsRemoved()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker), typeof(BakerTests.BakerWithPrefabReference));

            var prefabPath = LiveConversionTest.Assets.GetNextPath("Test.prefab");
            var newObject = new GameObject("Prefab");
            var com = newObject.AddComponent<MockDataAuthoring>();
            com.Value = 42;
            var prefabObject = PrefabUtility.SaveAsPrefabAsset(newObject, prefabPath);

            var subScene = CreateEmptySubScene("TestSubScene", true);
            var sceneObject = new GameObject("SceneObject");
            var aAuthoring = sceneObject.AddComponent<Authoring_WithGameObjectField>();
            aAuthoring.GameObjectField = prefabObject;
            SceneManager.MoveGameObjectToScene(sceneObject, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                // Verify it baked first
                var testQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(MockData)}, Options = EntityQueryOptions.IncludePrefab});
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                var prefabEntity = testQuery.GetSingletonEntity();
                Assert.AreEqual(42, w.EntityManager.GetComponentData<MockData>(prefabEntity).Value);

                // Remove reference to prefab and rebake
                Undo.RecordObject(aAuthoring, "Change value");
                aAuthoring.GameObjectField = null;
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_Prefabs_DeletePrefabAsset_PrefabEntityRemoved()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker), typeof(BakerTests.BakerWithPrefabReference));

            var prefabPath = LiveConversionTest.Assets.GetNextPath("Test.prefab");
            var newObject = new GameObject("Prefab");
            var com = newObject.AddComponent<MockDataAuthoring>();
            com.Value = 42;
            var prefabObject = PrefabUtility.SaveAsPrefabAsset(newObject, prefabPath);

            var subScene = CreateEmptySubScene("TestSubScene", true);
            var sceneObject = new GameObject("SceneObject");
            var aAuthoring = sceneObject.AddComponent<Authoring_WithGameObjectField>();
            aAuthoring.GameObjectField = prefabObject;
            SceneManager.MoveGameObjectToScene(sceneObject, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                // Verify it baked first
                var testQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(MockData)}, Options = EntityQueryOptions.IncludePrefab});
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                var prefabEntity = testQuery.GetSingletonEntity();
                Assert.AreEqual(42, w.EntityManager.GetComponentData<MockData>(prefabEntity).Value);

                // Modify and check it no longer exists
                AssetDatabase.DeleteAsset(prefabPath);
                AssetDatabase.Refresh();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testQuery.CalculateEntityCount(), "Expected the prefab entity to be removed");
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_Prefabs_DeleteAndUndoGameObject_DoesntThrow()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker), typeof(BakerWithPrefabReferenceTestComponent));

            var subScene = CreateEmptySubScene("TestSubScene", true);
            var prefabOnePath = LiveConversionTest.Assets.GetNextPath("TestOne.prefab");
            var prefabTwoPath = LiveConversionTest.Assets.GetNextPath("TestTwo.prefab");

            // Create the Prefab
            var refGO = new GameObject("PrefabReferenced");
            refGO.AddComponent<MockDataAuthoring>();
            var refPrefabObject = PrefabUtility.SaveAsPrefabAsset(refGO, prefabOnePath);

            // Create the Prefab
            var root = new GameObject("PrefabRoot");
            var rootAuthoring = root.AddComponent<PrefabAuthoring>();
            rootAuthoring.Prefab = refPrefabObject;

            var child = new GameObject("PrefabChild");
            child.AddComponent<MockDataAuthoring>();
            child.transform.SetParent(root.transform);
            var prefabObject = PrefabUtility.SaveAsPrefabAsset(root, prefabTwoPath);

            // Make GO and add the Prefab in the Baker
            var sceneObject = new GameObject("GO");
            var aAuthoring = sceneObject.AddComponent<PrefabAuthoring>();
            aAuthoring.Prefab = prefabObject;

            SceneManager.MoveGameObjectToScene(sceneObject, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);
                var prefabQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{
                        All = new ComponentType[]{typeof(MockData)},
                        Options = EntityQueryOptions.IncludePrefab});
                Assert.AreEqual(4, prefabQuery.CalculateEntityCount(), "Expected four game objects to be converted");

                // Remove GO which removes prefab Entities
                Undo.DestroyObjectImmediate(sceneObject);
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(0, prefabQuery.CalculateEntityCount(), "All prefab Entities destroyed");

                Undo.PerformUndo();
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(4, prefabQuery.CalculateEntityCount(), "Expected four game objects to be converted");

                Undo.PerformRedo();
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(0, prefabQuery.CalculateEntityCount(), "All prefab Entities destroyed");

                Undo.PerformUndo();
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(4, prefabQuery.CalculateEntityCount(), "Expected four game objects to be converted");

                yield return UpdateEditorAndWorld(w);
                Undo.PerformRedo();
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(0, prefabQuery.CalculateEntityCount(), "All prefab Entities destroyed");
            }
        }

        public class BakerWithMultipleCallsToGetEntity : Baker<Authoring_WithGameObjectField>
        {
            public override void Bake(Authoring_WithGameObjectField authoring)
            {
                GetEntity(authoring.GameObjectField, TransformUsageFlags.None);
                GetEntity(authoring.GameObjectField, TransformUsageFlags.None);

                DependsOn(authoring.GameObjectField);
                DependsOn(authoring.GameObjectField);
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_Prefabs_RemovePrefabReference_MultipleGetEntity_PrefabIsRemoved()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker), typeof(BakerWithMultipleCallsToGetEntity));

            var prefabPath = LiveConversionTest.Assets.GetNextPath("Test.prefab");
            var newObject = new GameObject("Prefab");
            var com = newObject.AddComponent<MockDataAuthoring>();
            com.Value = 42;
            var prefabObject = PrefabUtility.SaveAsPrefabAsset(newObject, prefabPath);

            var subScene = CreateEmptySubScene("TestSubScene", true);
            var sceneObject = new GameObject("SceneObject");
            var aAuthoring = sceneObject.AddComponent<Authoring_WithGameObjectField>();
            aAuthoring.GameObjectField = prefabObject;
            SceneManager.MoveGameObjectToScene(sceneObject, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                // Verify it baked first
                var testQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(MockData)}, Options = EntityQueryOptions.IncludePrefab});
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                var prefabEntity = testQuery.GetSingletonEntity();
                Assert.AreEqual(42, w.EntityManager.GetComponentData<MockData>(prefabEntity).Value);

                // Remove reference to prefab and rebake
                Undo.RecordObject(aAuthoring, "Change value");
                aAuthoring.GameObjectField = null;
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, testQuery.CalculateEntityCount(), "Expected the prefab entity to be removed");
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_Prefabs_DisableChildrenBakes()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker), typeof(BakerTests.BakerWithPrefabReference));

            var prefabObject = default(GameObject);
            var prefabPath = LiveConversionTest.Assets.GetNextPath("Test.prefab");
            var newObject = new GameObject("Prefab");
            var com = newObject.AddComponent<MockDataAuthoring>();

            var child = new GameObject($"Child");
            child.transform.parent = newObject.transform;
            child.AddComponent<MockDataAuthoring>();
            child.SetActive(false);

            prefabObject = PrefabUtility.SaveAsPrefabAsset(newObject, prefabPath);

            var subScene = CreateEmptySubScene("TestSubScene", true);
            var sceneObject = new GameObject("SceneObject");
            var aAuthoring = sceneObject.AddComponent<Authoring_WithGameObjectField>();
            aAuthoring.GameObjectField = prefabObject;
            SceneManager.MoveGameObjectToScene(sceneObject, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                // Verify it baked first
                var testQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(MockData), typeof(LinkedEntityGroup)}, Options = EntityQueryOptions.IncludePrefab});
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");

                var prefabEntity = testQuery.GetSingletonEntity();

                // Prefab LinkedEntityGroup should contain all children + itself
                // Disabled children should be included
                var linkedEntityGroup = w.EntityManager.GetBuffer<LinkedEntityGroup>(prefabEntity);
                Assert.AreEqual(2, linkedEntityGroup.Length);
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_Prefabs_HierarchyValid()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker), typeof(BakerTests.BakerWithPrefabReference));

            var prefabObject = default(GameObject);
            var prefabPath = LiveConversionTest.Assets.GetNextPath("Test.prefab");
            var newObject = new GameObject("Prefab");
            var com = newObject.AddComponent<MockDataAuthoring>();
            com.Value = 42;

            void AddChildrenRecursive(GameObject parent, int maxDepth, int currentDepth)
            {
                var child = new GameObject($"Child{currentDepth}");
                child.transform.parent = parent.transform;

                var childCom = child.AddComponent<MockDataAuthoring>();
                childCom.Value = currentDepth;

                if(currentDepth < maxDepth)
                    AddChildrenRecursive(child, maxDepth, currentDepth+1);
            }

            int depth = 3;
            AddChildrenRecursive(newObject, depth, 1);

            prefabObject = PrefabUtility.SaveAsPrefabAsset(newObject, prefabPath);

            var subScene = CreateEmptySubScene("TestSubScene", true);
            var sceneObject = new GameObject("SceneObject");
            var aAuthoring = sceneObject.AddComponent<Authoring_WithGameObjectField>();
            aAuthoring.GameObjectField = prefabObject;
            SceneManager.MoveGameObjectToScene(sceneObject, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                // Verify it baked first
                var testQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(MockData), typeof(LinkedEntityGroup)}, Options = EntityQueryOptions.IncludePrefab});
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");

                var prefabEntity = testQuery.GetSingletonEntity();
                Assert.AreEqual(42, w.EntityManager.GetComponentData<MockData>(prefabEntity).Value);

                // Prefab LinkedEntityGroup should contain all children + itself
                // Checking in this test to ensure even deep hierarchies are correctly linked
                var linkedEntityGroup = w.EntityManager.GetBuffer<LinkedEntityGroup>(prefabEntity);
                Assert.AreEqual(depth + 1, linkedEntityGroup.Length);

                // Make sure first entry in group is the prefab entity itself
                Assert.AreEqual(prefabEntity, linkedEntityGroup[0].Value);

                // Walk the entities and make sure they are at the expected depth
                for(int i = 1; i < linkedEntityGroup.Length; i++)
                {
                    var childEntity = linkedEntityGroup[i].Value;
                    var childDepth = w.EntityManager.GetComponentData<MockData>(childEntity).Value;
                    var parentEntity = w.EntityManager.GetComponentData<Parent>(childEntity).Value;

                    Assert.AreNotEqual(0, childDepth);

                    var calculatedDepth = 1;
                    while (parentEntity != prefabEntity)
                    {
                        if (calculatedDepth > childDepth)
                            break;

                        calculatedDepth++;

                        parentEntity = w.EntityManager.GetComponentData<Parent>(parentEntity).Value;
                    }

                    Assert.AreEqual(childDepth, calculatedDepth);
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_Prefabs_RootNotStripped()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker), typeof(BakerTests.BakerWithPrefabReference));

            var prefabObject = default(GameObject);
            var prefabPath = LiveConversionTest.Assets.GetNextPath("Test.prefab");

            // Parent with no components, so technically unused. But it will be preserved because it will be a root for a prefab
            var newObject = new GameObject("Prefab");

            var child = new GameObject($"Child");
            child.transform.parent = newObject.transform;

            var childCom = child.AddComponent<MockDataAuthoring>();
            childCom.Value = 42;
            SetTransformUsageFlags(child, TransformUsageFlags.Dynamic);

            prefabObject = PrefabUtility.SaveAsPrefabAsset(newObject, prefabPath);

            var subScene = CreateEmptySubScene("TestSubScene", true);
            var sceneObject = new GameObject("SceneObject");
            var aAuthoring = sceneObject.AddComponent<Authoring_WithGameObjectField>();
            aAuthoring.GameObjectField = prefabObject;
            SceneManager.MoveGameObjectToScene(sceneObject, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                // Verify it baked first
                var testQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(LinkedEntityGroup)}, None = new ComponentType[]{typeof(MockData)}, Options = EntityQueryOptions.IncludePrefab});
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");

                var testQueryChild = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(MockData)}, None = new ComponentType[]{typeof(LinkedEntityGroup)}, Options = EntityQueryOptions.IncludePrefab});
                Assert.AreEqual(1, testQueryChild.CalculateEntityCount(), "Expected a game object to be converted");

                var rootEntity = testQuery.GetSingletonEntity();
                var childEntity = testQueryChild.GetSingletonEntity();
                Assert.AreEqual(42, w.EntityManager.GetComponentData<MockData>(childEntity).Value);

                // Prefab LinkedEntityGroup should contain all children + itself
                // Checking in this test to ensure even deep hierarchies are correctly linked
                var linkedEntityGroup = w.EntityManager.GetBuffer<LinkedEntityGroup>(rootEntity);
                Assert.AreEqual(2, linkedEntityGroup.Length);

                // Make sure first entry in group is the prefab entity itself
                Assert.AreEqual(rootEntity, linkedEntityGroup[0].Value);
                Assert.AreEqual(childEntity, linkedEntityGroup[1].Value);
            }
        }

        [UnityTest, TestMustExpectAllLogs]
        public IEnumerator IncrementalBaking_RectTransform_HierarchyValid()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(DefaultGameObjectBaker), typeof(AddTransformUsageFlag.Baker), typeof(MockDataAuthoringBaker), typeof(BakerTests.BakerWithPrefabReference));

            var prefabObject = default(GameObject);
            var prefabPath = LiveConversionTest.Assets.GetNextPath("Test.prefab");
            var newObject = new GameObject("Prefab");
            newObject.AddComponent<RectTransform>();
            var com = newObject.AddComponent<MockDataAuthoring>();
            SetTransformUsageFlags(newObject, TransformUsageFlags.Dynamic);
            com.Value = 42;

            void AddChildrenRecursive(GameObject parent, int maxDepth, int currentDepth)
            {
                var child1 = new GameObject($"Child1{currentDepth}");
                child1.AddComponent<RectTransform>();
                child1.transform.SetParent(parent.transform);
                SetTransformUsageFlags(child1, TransformUsageFlags.Dynamic);

                var child2 = new GameObject($"Child2{currentDepth}");
                child2.AddComponent<RectTransform>();
                child2.transform.SetParent(parent.transform);
                SetTransformUsageFlags(child2, TransformUsageFlags.Dynamic);

                var childCom = child1.AddComponent<MockDataAuthoring>();
                childCom.Value = currentDepth;

                if(currentDepth < maxDepth)
                    AddChildrenRecursive(child1, maxDepth, currentDepth+1);
            }

            int depth = 3;
            AddChildrenRecursive(newObject, depth, 1);

            prefabObject = PrefabUtility.SaveAsPrefabAsset(newObject, prefabPath);

            var subScene = CreateEmptySubScene("TestSubScene", true);
            var sceneObject = new GameObject("SceneObject");
            var aAuthoring = sceneObject.AddComponent<Authoring_WithGameObjectField>();
            SetTransformUsageFlags(sceneObject, TransformUsageFlags.Dynamic);
            aAuthoring.GameObjectField = prefabObject;
            SceneManager.MoveGameObjectToScene(sceneObject, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                // Verify it baked first
                var testQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<MockData, LinkedEntityGroup>().WithOptions(EntityQueryOptions.IncludePrefab).Build(w.EntityManager);
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");

                var prefabEntity = testQuery.GetSingletonEntity();
                Assert.AreEqual(42, w.EntityManager.GetComponentData<MockData>(prefabEntity).Value);

                // Prefab LinkedEntityGroup should contain all children + itself
                // Checking in this test to ensure even deep hierarchies are correctly linked
                var linkedEntityGroup = w.EntityManager.GetBuffer<LinkedEntityGroup>(prefabEntity);
                Assert.AreEqual(depth * 2 + 1, linkedEntityGroup.Length);

                // Make sure first entry in group is the prefab entity itself
                Assert.AreEqual(prefabEntity, linkedEntityGroup[0].Value);
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_Prefabs_LinkedEntityGroupValid()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker), typeof(BakerTests.BakerWithPrefabReference));

            var prefabObject = default(GameObject);
            var prefabPath = LiveConversionTest.Assets.GetNextPath("Test.prefab");
            var newObject = new GameObject("Prefab");
            var com = newObject.AddComponent<MockDataAuthoring>();
            com.Value = 42;

            int numChildren = 3;
            for (int i = 0; i < numChildren; i++)
            {
                var child = new GameObject($"Child{i}");
                child.transform.parent = newObject.transform;

                var childCom = child.AddComponent<MockDataAuthoring>();
                childCom.Value = i;
            }

            prefabObject = PrefabUtility.SaveAsPrefabAsset(newObject, prefabPath);

            var subScene = CreateEmptySubScene("TestSubScene", true);
            var sceneObject = new GameObject("SceneObject");
            var aAuthoring = sceneObject.AddComponent<Authoring_WithGameObjectField>();
            aAuthoring.GameObjectField = prefabObject;
            SceneManager.MoveGameObjectToScene(sceneObject, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                // Verify it baked first
                var testQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(MockData), typeof(LinkedEntityGroup)}, Options = EntityQueryOptions.IncludePrefab});
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");

                var prefabEntity = testQuery.GetSingletonEntity();
                Assert.AreEqual(42, w.EntityManager.GetComponentData<MockData>(prefabEntity).Value);

                // Prefab LinkedEntityGroup should contain all children + itself
                var linkedEntityGroup = w.EntityManager.GetBuffer<LinkedEntityGroup>(prefabEntity);
                Assert.AreEqual(numChildren + 1, linkedEntityGroup.Length);

                // Make sure first entry in group is the prefab entity itself
                Assert.AreEqual(prefabEntity, linkedEntityGroup[0].Value);

                // Make sure they are valid entities, skipping first as it should be the prefab itself
                for(int i = 1; i < linkedEntityGroup.Length; i++)
                {
                    var childEntity = linkedEntityGroup[i].Value;
                    int childIndex = i - 1;
                    Assert.AreEqual(childIndex, w.EntityManager.GetComponentData<MockData>(childEntity).Value);
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_Prefabs_EditPrefabAsset_CausesReBake()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker), typeof(BakerTests.BakerWithPrefabReference));

            var prefabPath = LiveConversionTest.Assets.GetNextPath("Test.prefab");
            var newObject = new GameObject("Prefab");
            var com = newObject.AddComponent<MockDataAuthoring>();
            com.Value = 42;
            var prefabObject = PrefabUtility.SaveAsPrefabAsset(newObject, prefabPath);

            var subScene = CreateEmptySubScene("TestSubScene", true);
            var sceneObject = new GameObject("SceneObject");
            var aAuthoring = sceneObject.AddComponent<Authoring_WithGameObjectField>();
            aAuthoring.GameObjectField = prefabObject;
            SceneManager.MoveGameObjectToScene(sceneObject, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                // Verify it baked first
                var testQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(MockData)}, Options = EntityQueryOptions.IncludePrefab});
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                var prefabEntity = testQuery.GetSingletonEntity();
                Assert.AreEqual(42, w.EntityManager.GetComponentData<MockData>(prefabEntity).Value);

                // Modify and check it re-baked
                var authoring = prefabObject.GetComponent<MockDataAuthoring>();
                Undo.RecordObject(authoring, "Change value");
                authoring.Value = 7;
                Undo.FlushUndoRecordObjects();
                EditorUtility.SetDirty(prefabObject);
                AssetDatabase.SaveAssetIfDirty(prefabObject);

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(7, w.EntityManager.GetComponentData<MockData>(prefabEntity).Value);
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_Prefabs_WithAdditionalEntities_CorrectlyBaked()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker_WithAdditionalEntities), typeof(BakerTests.BakerWithPrefabReference));

            var prefabObject = default(GameObject);
            var prefabPath = LiveConversionTest.Assets.GetNextPath("Test.prefab");
            var newObject = new GameObject("Prefab");
            var com = newObject.AddComponent<MockDataAuthoring>();

            int numAdditionalEntities = 3;
            com.Value = numAdditionalEntities;

            prefabObject = PrefabUtility.SaveAsPrefabAsset(newObject, prefabPath);

            var subScene = CreateEmptySubScene("TestSubScene", true);
            var sceneObject = new GameObject("SceneObject");
            var aAuthoring = sceneObject.AddComponent<Authoring_WithGameObjectField>();
            aAuthoring.GameObjectField = prefabObject;
            SceneManager.MoveGameObjectToScene(sceneObject, subScene.EditingScene);

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);

            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                // Verify it baked first
                var testQuery = w.EntityManager.CreateEntityQuery(new EntityQueryDesc{All = new ComponentType[]{typeof(MockData), typeof(LinkedEntityGroup)}, Options = EntityQueryOptions.IncludePrefab});
                Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");

                var prefabEntity = testQuery.GetSingletonEntity();
                Assert.AreEqual(numAdditionalEntities, w.EntityManager.GetComponentData<MockData>(prefabEntity).Value);

                // Prefab LinkedEntityGroup should contain all children + itself
                // Checking in this test to ensure even deep hierarchies are correctly linked
                var linkedEntityGroup = w.EntityManager.GetBuffer<LinkedEntityGroup>(prefabEntity);
                // Self + number in Value
                Assert.AreEqual(numAdditionalEntities + 1, linkedEntityGroup.Length);

                // Make sure first entry in group is the prefab entity itself
                Assert.AreEqual(prefabEntity, linkedEntityGroup[0].Value);

                // Walk the entities and make sure they have the expected values
                for(int i = 1; i < linkedEntityGroup.Length; i++)
                {
                    var childEntity = linkedEntityGroup[i].Value;
                    var value = w.EntityManager.GetComponentData<MockData>(childEntity).Value;
                    var parentEntity = w.EntityManager.GetComponentData<Parent>(childEntity).Value;
                    Assert.IsTrue(w.EntityManager.HasComponent<Prefab>(childEntity));
                    Assert.AreEqual(prefabEntity, parentEntity);
                    Assert.AreEqual(value, i);
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_ConvertsWithAdditionalEntity([Values]Mode mode)
        {
            GameObject go = null;
            {
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    go = new GameObject("TestGameObject");
                    var authoring = go.AddComponent<TestAdditionalEntityComponentAuthoring>();
                    authoring.value = 2;
                    return new List<GameObject> {go};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var authoring = go.GetComponent<TestAdditionalEntityComponentAuthoring>();
                Undo.RecordObject(authoring, "Value Changed");
                authoring.value = 3;
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_AdditionalEntity_StaticAndActiveState_MatchesPrimaryEntity(
            [Values]Mode mode,
            [Values(true, false)] bool isStatic,
            [Values(true, false)] bool isActive)
        {
            var additionalEntityCount = 2;
            GameObject go = null;
            {
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    go = new GameObject("TestGameObject");

                    go.isStatic = isStatic;
                    go.SetActive(isActive);

                    var authoring = go.AddComponent<TestAdditionalEntityComponentAuthoring>();
                    authoring.value = additionalEntityCount;
                    return new List<GameObject> {go};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                var staticQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Static>().WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build(w.EntityManager);
                var disabledQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Disabled>().Build(w.EntityManager);
                var additionalEntitiesQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<AdditionalEntity>().WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build(w.EntityManager);

                yield return UpdateEditorAndWorld(w);

                using var entities = additionalEntitiesQuery.ToEntityArray(Allocator.TempJob);
                Assert.AreEqual(additionalEntityCount, entities.Length, "Unexpected number of additional entities.");

                for (int i = 0, count = entities.Length; i < count; ++i)
                {
                    var additionalEntity = entities[i];
                    if (go.isStatic)
                        Assert.True(staticQuery.Matches(additionalEntity), "The additional entity is not static while the authoring object is static");
                    else
                        Assert.False(staticQuery.Matches(additionalEntity), "The additional entity is static while the authoring object is not");

                    if (go.activeSelf)
                        Assert.False(disabledQuery.Matches(additionalEntity), "The additional entity is disabled while the authoring object is active");
                    else
                        Assert.True(disabledQuery.Matches(additionalEntity), "The additional entity is active while the authoring object is disabled");
                }

                Undo.RecordObject(go, "Static and Active State Changed");
                go.isStatic = !isStatic;
                go.SetActive(!isActive);
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                for (int i = 0, count = entities.Length; i < count; ++i)
                {
                    var additionalEntity = entities[i];
                    if (go.isStatic)
                        Assert.True(staticQuery.Matches(additionalEntity), "The additional entity is not static while the authoring object is static");
                    else
                        Assert.False(staticQuery.Matches(additionalEntity), "The additional entity is static while the authoring object is not");

                    if (go.activeSelf)
                        Assert.False(disabledQuery.Matches(additionalEntity), "The additional entity is disabled while the authoring object is active");
                    else
                        Assert.True(disabledQuery.Matches(additionalEntity), "The additional entity is active while the authoring object is disabled");
                }
            }
        }

        [DisableAutoCreation]
        class TransformUsageBaker : Baker<Transform>
        {
            internal static TransformUsageFlags Flags;
            internal static bool Enabled = false;

            public override void Bake(Transform authoring)
            {
                if (Enabled)
                    GetEntity(authoring, Flags);

                // This ensures that GetEntityUnreferenced doesn't have any side effects for baking (As expected)
                GetEntityWithoutDependency();
            }
        }

        [DisableAutoCreation]
        class TransformUsageBaker2 : Baker<Transform>
        {
            internal static TransformUsageFlags Flags;
            internal static bool Enabled = false;

            public override void Bake(Transform authoring)
            {
                if (Enabled)
                    GetEntity(authoring, Flags);
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_TransformUsage()
        {
            // By default TransformBaker will cause every entity to have transform usage, so for now circumvent this by only running TransformUsageBaker and nothing else.
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(TransformUsageBaker), typeof(TransformUsageBaker2));

            TransformUsageBaker.Flags = TransformUsageFlags.Dynamic;
            TransformUsageBaker.Enabled = false;
            TransformUsageBaker2.Flags = TransformUsageFlags.NonUniformScale;
            TransformUsageBaker2.Enabled = false;

            GameObject a;
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                a = new GameObject("Root");
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            var w = GetLiveConversionWorld(Mode.Edit);
            var transformAuthoringQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<TransformAuthoringCopyForTest>());
            var anyConvertedEntities = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<EntityGuid>());

            yield return UpdateEditorAndWorld(w);

            Assert.AreEqual(0, transformAuthoringQuery.CalculateEntityCount());
            Assert.AreEqual(0, anyConvertedEntities.CalculateEntityCount());


            // Enable both TransformUsageFlags.Dynamic & TransformUsageFlags.NonUniformScale baker => Both usages are combined
            {
                Undo.RegisterCompleteObjectUndo(a.transform, "");
                TransformUsageBaker.Enabled = true;
                TransformUsageBaker2.Enabled = true;
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, transformAuthoringQuery.CalculateEntityCount());
                Assert.AreEqual(RuntimeTransformComponentFlags.LocalToWorld | RuntimeTransformComponentFlags.LocalTransform | RuntimeTransformComponentFlags.PostTransformMatrix, transformAuthoringQuery.GetSingleton<TransformAuthoringCopyForTest>().RuntimeTransformUsage);
            }

            // Disable TransformUsageFlags.Default baker => Just NonUniformScale remains
            {
                TransformUsageBaker.Enabled = false;
                Undo.RegisterCompleteObjectUndo(a.transform, "");
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(1, transformAuthoringQuery.CalculateEntityCount());
                Assert.AreEqual(RuntimeTransformComponentFlags.LocalToWorld | RuntimeTransformComponentFlags.PostTransformMatrix, transformAuthoringQuery.GetSingleton<TransformAuthoringCopyForTest>().RuntimeTransformUsage);
            }

            // Disable Both bakers => Entities get deleted
            {
                TransformUsageBaker2.Enabled = false;
                TransformUsageBaker.Enabled = false;
                Undo.RegisterCompleteObjectUndo(a.transform, "");
                yield return UpdateEditorAndWorld(w);
                Assert.AreEqual(0, transformAuthoringQuery.CalculateEntityCount());
                Assert.AreEqual(0, anyConvertedEntities.CalculateEntityCount());
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_TransformUsage_HierarchyDynamicStaticSwap([Values]Mode mode)
        {
            // By default TransformBaker will cause every entity to have transform usage, so for now circumvent this by only running TransformUsageBaker and nothing else.
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(AssignTransformUsageBaker));
            AssignTransformUsageBaker.Flags.Clear();
            AssignTransformUsageBaker.AddManualComponents.Clear();

            GameObject a = null;
            GameObject b = null;
            Unity.Scenes.SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    a = new GameObject("Root");
                    b = new GameObject("Child");

                    a.transform.localPosition = new float3(1f, 1f, 1f);
                    a.transform.localRotation = quaternion.Euler(45f,45f,45f);
                    a.transform.localScale = new float3(0.5f, 0.5f, 0.5f);
                    b.transform.localPosition = new float3(2f, 2f, 2f);
                    b.transform.localRotation = quaternion.Euler(10f,10f,10f);
                    b.transform.localScale = new float3(4f, 4f, 4f);
                    b.transform.SetParent(a.transform, false);

                    return new List<GameObject> {a};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(Mode.Edit);
                var bakingWorld = GetBakingWorld(w, subScene.SceneGUID);
                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var aEntity = bakingSystem.GetEntity(a);
                var bEntity = bakingSystem.GetEntity(b);
                Assert.AreNotEqual(Entity.Null, bEntity);

                // Renderable/Renderable
                ChangeFlag(a, TransformUsageFlags.Renderable);
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Dynamic/Renderable
                ChangeFlag(a, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform );

                // Dynamic/Dynamic
                ChangeFlag(b, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );

                // Renderable/Dynamic
                ChangeFlag(a, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Renderable
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_TransformUsage_ManualOverrideSwap([Values]Mode mode)
        {
            // By default TransformBaker will cause every entity to have transform usage, so for now circumvent this by only running TransformUsageBaker and nothing else.
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(AssignTransformUsageBaker));
            AssignTransformUsageBaker.Flags.Clear();
            AssignTransformUsageBaker.AddManualComponents.Clear();

            GameObject a = null;
            Unity.Scenes.SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    a = new GameObject("Root");

                    a.transform.localPosition = new float3(1f, 1f, 1f);
                    a.transform.localRotation = quaternion.Euler(45f,45f,45f);
                    a.transform.localScale = new float3(0.5f, 0.5f, 0.5f);

                    return new List<GameObject> {a};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(Mode.Edit);
                var bakingWorld = GetBakingWorld(w, subScene.SceneGUID);
                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var aEntity = bakingSystem.GetEntity(a);
                Assert.AreNotEqual(Entity.Null, aEntity);

                // Dynamic
                ChangeFlag(a, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform );

                // ManualOverride
                ChangeFlag(a, TransformUsageFlags.ManualOverride);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.Nothing );

                // Dynamic
                ChangeFlag(a, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform );

                // ManualOverride
                AssignTransformUsageBaker.AddManualComponents[a] = RuntimeTransformComponentFlags.LocalTransform;
                ChangeFlag(a, TransformUsageFlags.ManualOverride);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalTransform );
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_AddComponentWithManualOverride([Values]Mode mode)
        {
            // By default TransformBaker will cause every entity to have transform usage, so for now circumvent this by only running TransformUsageBaker and nothing else.
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker_AddComponentWithManualOverride), typeof(AssignTransformUsageBaker));
            AssignTransformUsageBaker.Flags.Clear();
            AssignTransformUsageBaker.AddManualComponents.Clear();

            GameObject a = null;
            Unity.Scenes.SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    a = new GameObject("Child");
                    return new List<GameObject> {a};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(Mode.Edit);
                var bakingWorld = GetBakingWorld(w, subScene.SceneGUID);
                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var aEntity = bakingSystem.GetEntity(a);
                Assert.AreNotEqual(Entity.Null, aEntity);

                // Dynamic
                ChangeFlag(a, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasLocalTransform );

                // This will add Manual Override
                Undo.AddComponent<MockDataAuthoring>(a);

                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.Nothing);

                // This will remove Manual Override
                Undo.DestroyObjectImmediate(a.GetComponent<MockDataAuthoring>());
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasLocalTransform );
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_TransformUsage_HierarchyIntermediateNones([Values]Mode mode, [Values(1,2)]int noneCount)
        {
            // By default TransformBaker will cause every entity to have transform usage, so for now circumvent this by only running TransformUsageBaker and nothing else.
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(AssignTransformUsageBaker));
            AssignTransformUsageBaker.Flags.Clear();
            AssignTransformUsageBaker.AddManualComponents.Clear();

            GameObject a = null;
            List<GameObject> intermediates = new List<GameObject>();
            GameObject b = null;
            Unity.Scenes.SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    a = new GameObject("Root");
                    var previous = a;

                    for (int index = 0; index < noneCount; ++index)
                    {
                        var intermediate = new GameObject($"Intermediate {index}");
                        intermediate.transform.localPosition = new float3(1f, 1f, 1f);
                        intermediate.transform.localRotation = quaternion.Euler(45f,45f,45f);
                        intermediate.transform.localScale = new float3(0.5f, 0.5f, 0.5f);
                        intermediate.transform.SetParent(previous.transform, false);
                        previous = intermediate;
                        intermediates.Add(intermediate);
                    }

                    b = new GameObject("Child");

                    a.transform.localPosition = new float3(1f, 1f, 1f);
                    a.transform.localRotation = quaternion.Euler(45f,45f,45f);
                    a.transform.localScale = new float3(0.5f, 0.5f, 0.5f);
                    b.transform.localPosition = new float3(2f, 2f, 2f);
                    b.transform.localRotation = quaternion.Euler(10f,10f,10f);
                    b.transform.localScale = new float3(4f, 4f, 4f);
                    b.transform.SetParent(previous.transform, false);

                    return new List<GameObject> {a};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(Mode.Edit);
                var bakingWorld = GetBakingWorld(w, subScene.SceneGUID);
                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var aEntity = bakingSystem.GetEntity(a);
                List<Entity> intermediateEntities = new List<Entity>();
                foreach (var intermediate in intermediates)
                {
                    intermediateEntities.Add(bakingSystem.GetEntity(intermediate));
                }
                var bEntity = bakingSystem.GetEntity(b);
                Assert.AreNotEqual(Entity.Null, bEntity);

                // Renderable/Renderable
                ChangeFlag(a, TransformUsageFlags.Renderable);
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.Nothing);

                // Dynamic/Renderable
                ChangeFlag(a, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.HasParent |
                                                                                                                    ExpectedConvertedTransformResults.HasLocalTransform |
                                                                                                                    ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                                                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                // Dynamic/Dynamic
                ChangeFlag(b, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.HasParent |
                                                                                                                    ExpectedConvertedTransformResults.HasLocalTransform |
                                                                                                                    ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                                                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                // Renderable/Dynamic
                ChangeFlag(a, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.Nothing);

                // Dynamic/Dynamic
                ChangeFlag(a, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                // Null/Dynamic
                ChangeFlag(a, TransformUsageFlags.Dynamic);
                ChangeParent(b, null);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.Nothing);

                // Dynamic/Dynamic
                ChangeParent(b, intermediates[intermediates.Count - 1]);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                // Dynamic/None
                ChangeFlag(b, TransformUsageFlags.None);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.Nothing);

                // Dynamic/Dynamic
                ChangeFlag(b, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                // Dynamic/None
                ChangeFlag(a, TransformUsageFlags.None);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.Nothing);

                // Dynamic/Dynamic
                ChangeFlag(a, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                // Dynamic/ManualOverride
                ChangeFlag(b, TransformUsageFlags.ManualOverride);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                // Dynamic/Dynamic
                ChangeFlag(b, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                // ManualOverride/Dynamic
                ChangeFlag(a, TransformUsageFlags.ManualOverride);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                // ManualOverride/ManualOverride
                ChangeFlag(b, TransformUsageFlags.ManualOverride);
                yield return UpdateEditorAndWorld(w);

                TestTransformUsageChangeIntermediates(bakingWorld, a, intermediates, aEntity, intermediateEntities, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_TransformUsage_HierarchyParentChangeIntermediateNone([Values]Mode mode)
        {
            // By default TransformBaker will cause every entity to have transform usage, so for now circumvent this by only running TransformUsageBaker and nothing else.
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(AssignTransformUsageBaker));
            AssignTransformUsageBaker.Flags.Clear();
            AssignTransformUsageBaker.AddManualComponents.Clear();

            GameObject a = null;
            GameObject b = null;
            Unity.Scenes.SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    a = new GameObject("Root");
                    b = new GameObject("Child");

                    a.transform.localPosition = new float3(1f, 1f, 1f);
                    a.transform.localRotation = quaternion.Euler(45f,45f,45f);
                    a.transform.localScale = new float3(0.5f, 0.5f, 0.5f);
                    b.transform.localPosition = new float3(2f, 2f, 2f);
                    b.transform.localRotation = quaternion.Euler(10f,10f,10f);
                    b.transform.localScale = new float3(4f, 4f, 4f);
                    b.transform.SetParent(a.transform, false);

                    return new List<GameObject> {a};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(Mode.Edit);
                var bakingWorld = GetBakingWorld(w, subScene.SceneGUID);
                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                // Root Dynamic, Child Renderable
                ChangeFlag(a, TransformUsageFlags.Dynamic);
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);

                var aEntity = bakingSystem.GetEntity(a);
                Assert.AreNotEqual(Entity.Null, aEntity);
                var bEntity = bakingSystem.GetEntity(b);
                Assert.AreNotEqual(Entity.Null, bEntity);

                // Create a new gameobject to become the intermediate one
                GameObject intermediate = new GameObject($"Intermediate");
                intermediate.transform.localPosition = new float3(1f, 1f, 1f);
                intermediate.transform.localRotation = quaternion.Euler(45f,45f,45f);
                intermediate.transform.localScale = new float3(0.5f, 0.5f, 0.5f);
                SceneManager.MoveGameObjectToScene(intermediate, subScene.EditingScene);
                Undo.RegisterCreatedObjectUndo(intermediate, "Creating intermediate");

                // Intermediate is at the root
                // a
                // -- b
                // intermediate
                yield return UpdateEditorAndWorld(w);

                // The intermediate entity should exist in the baking world
                var intermediateEntity = bakingSystem.GetEntity(intermediate);
                Assert.AreNotEqual(Entity.Null, intermediateEntity);

                var aGUID = bakingWorld.EntityManager.GetComponentData<EntityGuid>(aEntity);
                var bGUID = bakingWorld.EntityManager.GetComponentData<EntityGuid>(bEntity);

                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, aGUID));
                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, bGUID));

                // The intermediate entity should not have a runtime parent and it should not have any transform component on it
                TestTransformUsageChange(bakingWorld, null, intermediate, Entity.Null, intermediateEntity, ExpectedConvertedTransformResults.Nothing);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent);

                // The intermediate entity should not be in the final world because it has been stripped out
                var intermediateGUID = bakingWorld.EntityManager.GetComponentData<EntityGuid>(intermediateEntity);
                Assert.AreEqual(Entity.Null, GetEntityByGUID(w, intermediateGUID));

                // Intermediate becomes a child of a, but b is still a child of a
                // a
                // -- b
                // -- intermediate
                ChangeParent(intermediate, a);

                yield return UpdateEditorAndWorld(w);

                Assert.AreNotEqual(Entity.Null, intermediateEntity);

                // The intermediate entity should not have a runtime parent and it should not have any transform component on it
                TestTransformUsageChange(bakingWorld, a, intermediate, aEntity, intermediateEntity, ExpectedConvertedTransformResults.Nothing);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, aGUID));
                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, bGUID));

                // The intermediate entity should not be in the final world because it has been stripped out
                Assert.AreEqual(Entity.Null, GetEntityByGUID(w, intermediateGUID));

                // b becomes a child of intermediate
                // a
                // -- intermediate
                // ---- b
                ChangeParent(b, intermediate);

                yield return UpdateEditorAndWorld(w);

                Assert.AreNotEqual(Entity.Null, intermediateEntity);

                // The intermediate entity should not have a runtime parent and it should not have any transform component on it
                TestTransformUsageChange(bakingWorld, a, intermediate, aEntity, intermediateEntity, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);
                TestTransformUsageChange(bakingWorld, intermediate, b, intermediateEntity, bEntity, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, aGUID));
                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, bGUID));
                // The intermediate entity should be in the final world because now it has a parent that is dynamic and a child that is not World or None
                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, intermediateGUID));

                // Move the intermediate subtree to the root. Child still there
                // a
                // intermediate
                // -- b
                ChangeParent(intermediate, null);

                yield return UpdateEditorAndWorld(w);

                Assert.AreNotEqual(Entity.Null, intermediateEntity);

                // The intermediate entity should not have a runtime parent and it should not have any transform component on it
                TestTransformUsageChange(bakingWorld, null, intermediate, Entity.Null, intermediateEntity, ExpectedConvertedTransformResults.Nothing);
                TestTransformUsageChange(bakingWorld, intermediate, b, intermediateEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld);

                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, aGUID));
                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, bGUID));
                // The intermediate entity should not be in the final world because it hasn't got a parent
                Assert.AreEqual(Entity.Null, GetEntityByGUID(w, intermediateGUID));

                // Move the intermediate subtree back under a. Child still there
                // a
                // -- intermediate
                // ---- b
                ChangeParent(intermediate, a);

                yield return UpdateEditorAndWorld(w);

                Assert.AreNotEqual(Entity.Null, intermediateEntity);

                // The intermediate entity should not have a runtime parent and it should not have any transform component on it
                TestTransformUsageChange(bakingWorld, a, intermediate, aEntity, intermediateEntity, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);
                TestTransformUsageChange(bakingWorld, intermediate, b, intermediateEntity, bEntity, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, aGUID));
                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, bGUID));
                // The intermediate entity should be in the final world because now it has a parent that is dynamic and a child that is not World or None
                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, intermediateGUID));

                // b becomes a child of a again
                // a
                // -- b
                // -- intermediate
                ChangeParent(b, a);

                yield return UpdateEditorAndWorld(w);

                Assert.AreNotEqual(Entity.Null, intermediateEntity);

                // The intermediate entity should not have a runtime parent and it should not have any transform component on it
                TestTransformUsageChange(bakingWorld, a, intermediate, aEntity, intermediateEntity, ExpectedConvertedTransformResults.Nothing);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasParent |
                    ExpectedConvertedTransformResults.HasLocalTransform |
                    ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasValidRuntimeParent);

                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, aGUID));
                Assert.AreNotEqual(Entity.Null, GetEntityByGUID(w, bGUID));
                // The intermediate entity should not be in the final world because it hasn't got a child
                Assert.AreEqual(Entity.Null, GetEntityByGUID(w, intermediateGUID));
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_CreateParent([Values]Mode mode)
        {
            // By default TransformBaker will cause every entity to have transform usage, so for now circumvent this by only running TransformUsageBaker and nothing else.
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker_ManualOverrideCopyPosBaker));

            GameObject child = null;
            Unity.Scenes.SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    child = new GameObject("Child");
                    child.transform.localPosition = new float3(1f, 1f, 1f);
                    child.AddComponent<MockDataAuthoring>();
                    return new List<GameObject> {child};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(Mode.Edit);
                var bakingWorld = GetBakingWorld(w, subScene.SceneGUID);
                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                yield return UpdateEditorAndWorld(w);

                var childEntity = bakingSystem.GetEntity(child);
                Assert.AreNotEqual(Entity.Null, childEntity);

                // Create a new gameobject to become the root
                GameObject root = new GameObject($"Root");

                SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);

                Undo.RegisterCreatedObjectUndo(root, "Creating intermediate");
                Undo.AddComponent<MockDataAuthoring>(root);
                ChangeLocalPosition(root, new float3(1f, 1f, 1f));
                ChangeParent(child, root, false);

                yield return UpdateEditorAndWorld(w);

                var mockData = bakingWorld.EntityManager.GetComponentData<MockData>(childEntity);
                Assert.AreEqual(2, mockData.Value);
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_NewGameObjectDoesntTriggerDependOnParentTransformHierarchy([Values]Mode mode)
        {
            // By default TransformBaker will cause every entity to have transform usage, so for now circumvent this by only running TransformUsageBaker and nothing else.
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker_TransformDependencyBaker), typeof(AddTransformUsageFlag.Baker));

            GameObject refGo1 = null;
            GameObject refGo2 = null;
            GameObject rootRefGo2 = null;
            Unity.Scenes.SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    // Root Object
                    refGo1 = new GameObject("RefGo1");
                    refGo1.transform.localPosition = new float3(1f, 1f, 1f);
                    refGo1.AddComponent<MockDataAuthoring>();

                    // Child Object
                    rootRefGo2 = new GameObject("Root RefGo2");
                    rootRefGo2.transform.localPosition = new float3(1f, 1f, 1f);

                    refGo2 = new GameObject("RefGo2");
                    refGo2.transform.localPosition = new float3(1f, 1f, 1f);
                    refGo2.transform.parent = rootRefGo2.transform;
                    refGo2.AddComponent<MockDataAuthoring>();

                    return new List<GameObject> {refGo1, rootRefGo2};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(Mode.Edit);
                var bakingWorld = GetBakingWorld(w, subScene.SceneGUID);
                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                yield return UpdateEditorAndWorld(w);

                var refEntity = bakingSystem.GetEntity(refGo1);
                Assert.AreNotEqual(Entity.Null, refEntity);

                bakingSystem.ClearDidBake();

                // Create a new gameobject to become the root
                GameObject root = new GameObject($"Root");

                SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);

                Undo.RegisterCreatedObjectUndo(root, "Creating Other Root Object");

                var flagComponent = Undo.AddComponent<AddTransformUsageFlag>(root);
                flagComponent.flags = TransformUsageFlags.Dynamic;

                yield return UpdateEditorAndWorld(w);

                Assert.IsTrue(bakingSystem.DidBake(root));
                Assert.IsFalse(bakingSystem.DidBake(refGo1));
                Assert.IsFalse(bakingSystem.DidBake(refGo2));

                // Check that moving the rootRefGo2 triggers the baker in refGo2
                ChangeLocalPosition(rootRefGo2, new float3(2f, 2f, 2f));

                yield return UpdateEditorAndWorld(w);

                Assert.IsFalse(bakingSystem.DidBake(root));
                Assert.IsFalse(bakingSystem.DidBake(refGo1));
                Assert.IsTrue(bakingSystem.DidBake(refGo2));
                Assert.IsTrue(bakingSystem.DidBake(rootRefGo2));
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_NewGameObjectDoesntTriggerOtherTransformBakers([Values]Mode mode)
        {
            // By default TransformBaker will cause every entity to have transform usage, so for now circumvent this by only running TransformUsageBaker and nothing else.
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(MockDataAuthoringBaker_TransformBaker), typeof(AddTransformUsageFlag.Baker));

            GameObject refGo1 = null;
            GameObject refGo2 = null;
            Unity.Scenes.SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    // Root Object
                    refGo1 = new GameObject("RefGo1");
                    refGo1.transform.localPosition = new float3(1f, 1f, 1f);
                    refGo1.AddComponent<MockDataAuthoring>();

                    // Child Object
                    var rootRefGo2 = new GameObject("Root RefGo2");
                    rootRefGo2.transform.localPosition = new float3(1f, 1f, 1f);

                    refGo2 = new GameObject("RefGo2");
                    refGo2.transform.localPosition = new float3(1f, 1f, 1f);
                    refGo2.transform.parent = rootRefGo2.transform;
                    refGo2.AddComponent<MockDataAuthoring>();

                    return new List<GameObject> {refGo1, rootRefGo2};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(Mode.Edit);
                var bakingWorld = GetBakingWorld(w, subScene.SceneGUID);
                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                yield return UpdateEditorAndWorld(w);

                var refEntity = bakingSystem.GetEntity(refGo1);
                Assert.AreNotEqual(Entity.Null, refEntity);

                bakingSystem.ClearDidBake();

                // Create a new gameobject to become the root
                GameObject root = new GameObject($"Root");

                SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);

                Undo.RegisterCreatedObjectUndo(root, "Creating Other Root Object");

                var flagComponent = Undo.AddComponent<AddTransformUsageFlag>(root);
                flagComponent.flags = TransformUsageFlags.Dynamic;

                yield return UpdateEditorAndWorld(w);

                Assert.IsTrue(bakingSystem.DidBake(root));
                Assert.IsFalse(bakingSystem.DidBake(refGo1));
                Assert.IsFalse(bakingSystem.DidBake(refGo2));
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_TransformUsage_HierarchyParentSwap([Values]Mode mode)
        {
            // By default TransformBaker will cause every entity to have transform usage, so for now circumvent this by only running TransformUsageBaker and nothing else.
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(AssignTransformUsageBaker));
            AssignTransformUsageBaker.Flags.Clear();
            AssignTransformUsageBaker.AddManualComponents.Clear();

            GameObject a = null;
            GameObject a2 = null;
            GameObject b = null;
            Unity.Scenes.SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    a = new GameObject("Root");
                    a2 = new GameObject("Root2");
                    b = new GameObject("Child");

                    a.transform.localPosition = new float3(1f, 1f, 1f);
                    a.transform.localRotation = quaternion.Euler(45f,45f,45f);
                    a.transform.localScale = new float3(0.5f, 0.5f, 0.5f);
                    a2.transform.localPosition = new float3(1.5f, 1.5f, 1.5f);
                    a2.transform.localRotation = quaternion.Euler(40f,40f,40f);
                    a2.transform.localScale = new float3(1.5f, 1.5f, 1.5f);
                    b.transform.localPosition = new float3(2f, 2f, 2f);
                    b.transform.localRotation = quaternion.Euler(10f,10f,10f);
                    b.transform.localScale = new float3(4f, 4f, 4f);
                    b.transform.SetParent(a.transform, false);

                    return new List<GameObject> {a, a2};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(Mode.Edit);
                var bakingWorld = GetBakingWorld(w, subScene.SceneGUID);
                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var aEntity = bakingSystem.GetEntity(a);
                var a2Entity = bakingSystem.GetEntity(a2);
                var bEntity = bakingSystem.GetEntity(b);
                Assert.AreNotEqual(Entity.Null, bEntity);

                // Renderable A / Renderable
                ChangeFlag(a, TransformUsageFlags.Renderable);
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);

                // NULL / Renderable
                ChangeParent(b, null);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, null, b, Entity.Null, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);

                // Renderable A2 / Renderable
                ChangeParent(b, a2);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a2, b, a2Entity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasWorldSpaceData);

                // Dynamic A/Renderable
                ChangeParent(b, a);
                ChangeFlag(a, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform );

                // NULL / Renderable
                ChangeParent(b, null);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, null, b, Entity.Null, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasWorldSpaceData);

                // Renderable A2 / Renderable
                ChangeParent(b, a2);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a2, b, a2Entity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasWorldSpaceData);

                // Dynamic A/Renderable
                ChangeParent(b, a);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );

                // Dynamic A2 / Renderable
                ChangeFlag(a2, TransformUsageFlags.Dynamic);
                ChangeParent(b, a2);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a2, b, a2Entity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );

                // Dynamic A/Dynamic
                ChangeFlag(b, TransformUsageFlags.Dynamic);
                ChangeParent(b, a);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );

                // Null/Dynamic
                ChangeParent(b, null);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, null, b, Entity.Null, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );

                // Dynamic A2/Dynamic
                ChangeFlag(a2, TransformUsageFlags.Dynamic);
                ChangeParent(b, a2);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a2, b, a2Entity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );

                // Renderable A /Dynamic
                ChangeFlag(a, TransformUsageFlags.Renderable);
                ChangeParent(b, a);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);

                // NULL /Dynamic
                ChangeParent(b, null);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, null, b, Entity.Null, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);

                // Renderable A /Dynamic
                ChangeFlag(a, TransformUsageFlags.Renderable);
                ChangeParent(b, a);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);

                // Renderable A2 /Dynamic
                ChangeFlag(a2, TransformUsageFlags.Renderable);
                ChangeParent(b, a2);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a2, b, a2Entity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_TransformUsage_HierarchyWorldSwap([Values]Mode mode)
        {
            // By default TransformBaker will cause every entity to have transform usage, so for now circumvent this by only running TransformUsageBaker and nothing else.
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(AssignTransformUsageBaker));
            AssignTransformUsageBaker.Flags.Clear();
            AssignTransformUsageBaker.AddManualComponents.Clear();

            GameObject a = null;
            GameObject b = null;
            Unity.Scenes.SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    a = new GameObject("Root");
                    b = new GameObject("Child");

                    a.transform.localPosition = new float3(1f, 1f, 1f);
                    a.transform.localRotation = quaternion.Euler(45f,45f,45f);
                    a.transform.localScale = new float3(0.5f, 0.5f, 0.5f);
                    b.transform.localPosition = new float3(2f, 2f, 2f);
                    b.transform.localRotation = quaternion.Euler(10f,10f,10f);
                    b.transform.localScale = new float3(4f, 4f, 4f);
                    b.transform.SetParent(a.transform, false);

                    return new List<GameObject> {a};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(Mode.Edit);
                var bakingWorld = GetBakingWorld(w, subScene.SceneGUID);
                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var aEntity = bakingSystem.GetEntity(a);
                var bEntity = bakingSystem.GetEntity(b);
                Assert.AreNotEqual(Entity.Null, bEntity);

                // Renderable/Renderable
                ChangeFlag(a, TransformUsageFlags.Renderable);
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Renderable World
                ChangeFlag(b, TransformUsageFlags.Renderable | TransformUsageFlags.WorldSpace);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Renderable
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Dynamic/Renderable
                ChangeFlag(a, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform );

                // Dynamic/Renderable World
                ChangeFlag(b, TransformUsageFlags.Renderable | TransformUsageFlags.WorldSpace);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld  |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );

                // Dynamic/Renderable
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasLocalTransform );


                // Dynamic/Dynamic
                ChangeFlag(b, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );

                // Dynamic/Dynamic World
                ChangeFlag(b, TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform );

                // Dynamic/Dynamic
                ChangeFlag(b, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform );

                // Renderable/Dynamic
                ChangeFlag(a, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Dynamic World
                ChangeFlag(b, TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                    ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Dynamic
                ChangeFlag(b, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Renderable
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_TransformUsage_HierarchyNonUniformScale([Values]Mode mode)
        {
            // By default TransformBaker will cause every entity to have transform usage, so for now circumvent this by only running TransformUsageBaker and nothing else.
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(AssignTransformUsageBaker));
            AssignTransformUsageBaker.Flags.Clear();
            AssignTransformUsageBaker.AddManualComponents.Clear();

            GameObject a = null;
            GameObject b = null;
            Unity.Scenes.SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    a = new GameObject("Root");
                    b = new GameObject("Child");

                    a.transform.localPosition = new float3(1f, 1f, 1f);
                    a.transform.localRotation = quaternion.Euler(45f,45f,45f);
                    a.transform.localScale = new float3(0.5f, 0.5f, 0.5f);
                    b.transform.localPosition = new float3(2f, 2f, 2f);
                    b.transform.localRotation = quaternion.Euler(10f,10f,10f);
                    b.transform.localScale = new float3(4f, 4f, 4f);
                    b.transform.SetParent(a.transform, false);

                    return new List<GameObject> {a};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(Mode.Edit);
                var bakingWorld = GetBakingWorld(w, subScene.SceneGUID);
                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var aEntity = bakingSystem.GetEntity(a);
                var bEntity = bakingSystem.GetEntity(b);
                Assert.AreNotEqual(Entity.Null, bEntity);

                // First Set Renderable/Renderable
                #region Renderable_Renderable
                // Renderable/Renderable
                ChangeFlag(a, TransformUsageFlags.Renderable);
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Renderable NUS
                ChangeLocalScale(b, new float3(1f, 2f, 3f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Renderable NUS & NUS FLAG
                ChangeFlag(b, TransformUsageFlags.Renderable | TransformUsageFlags.NonUniformScale);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                              ExpectedConvertedTransformResults.HasNonUniformScale);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Renderable & NUS FLAG
                ChangeLocalScale(b, new float3(1f, 1f, 1f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Renderable
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Renderable & NUS FLAG
                ChangeFlag(b, TransformUsageFlags.Renderable | TransformUsageFlags.NonUniformScale);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Renderable NUS & NUS FLAG
                ChangeLocalScale(b, new float3(1f, 2f, 3f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                              ExpectedConvertedTransformResults.HasNonUniformScale);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Renderable NUS
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Renderable
                ChangeLocalScale(b, new float3(1f, 1f, 1f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable NUS/Renderable
                ChangeLocalScale(a, new float3(1f, 2f, 3f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                #endregion
                // Second Set Renderable/Dynamic
                #region Renderable_Dynamic
                ChangeLocalScale(a, new float3(1f, 1f, 1f));
                ChangeFlag(b, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Dynamic NUS
                ChangeLocalScale(b, new float3(1f, 2f, 3f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData|
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                              ExpectedConvertedTransformResults.HasNonUniformScale);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Dynamic NUS & NUS FLAG
                ChangeFlag(b, TransformUsageFlags.Dynamic | TransformUsageFlags.NonUniformScale);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                              ExpectedConvertedTransformResults.HasNonUniformScale);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Dynamic & NUS FLAG
                ChangeLocalScale(b, new float3(1f, 1f, 1f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Dynamic
                ChangeFlag(b, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Dynamic & NUS FLAG
                ChangeFlag(b, TransformUsageFlags.Dynamic | TransformUsageFlags.NonUniformScale);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Dynamic NUS & NUS FLAG
                ChangeLocalScale(b, new float3(1f, 2f, 3f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                              ExpectedConvertedTransformResults.HasNonUniformScale);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Dynamic NUS
                ChangeFlag(b, TransformUsageFlags.Dynamic);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData|
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                              ExpectedConvertedTransformResults.HasNonUniformScale);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable/Dynamic
                ChangeLocalScale(b, new float3(1f, 1f, 1f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                // Renderable NUS/Dynamic
                ChangeLocalScale(a, new float3(1f, 2f, 3f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                              ExpectedConvertedTransformResults.HasNonUniformScale |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasWorldSpaceData );

                #endregion
                // Third Set Dynamic/Renderable
                #region Dynamic_Renderable
                // Dynamic NUS/Renderable
                ChangeLocalScale(a, new float3(1f, 2f, 3f));
                ChangeFlag(a, TransformUsageFlags.Dynamic);
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform |
                                                                                     ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                                     ExpectedConvertedTransformResults.HasNonUniformScale );

                // Dynamic NUS/Renderable NUS
                ChangeLocalScale(b, new float3(1f, 2f, 3f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                              ExpectedConvertedTransformResults.HasNonUniformScale);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform |
                                                                                     ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                                     ExpectedConvertedTransformResults.HasNonUniformScale );

                // Dynamic NUS/Renderable NUS & NUS FLAG
                ChangeFlag(b, TransformUsageFlags.Renderable | TransformUsageFlags.NonUniformScale);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                              ExpectedConvertedTransformResults.HasNonUniformScale);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform |
                                                                                     ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                                     ExpectedConvertedTransformResults.HasNonUniformScale );

                // Dynamic NUS/Renderable & NUS FLAG
                ChangeLocalScale(b, new float3(1f, 1f, 1f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform |
                                                                                     ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                                     ExpectedConvertedTransformResults.HasNonUniformScale );

                // Dynamic NUS/Renderable
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform |
                                                                                     ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                                     ExpectedConvertedTransformResults.HasNonUniformScale );

                // Dynamic NUS/Renderable & NUS FLAG
                ChangeFlag(b, TransformUsageFlags.Renderable | TransformUsageFlags.NonUniformScale);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform |
                                                                                     ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                                     ExpectedConvertedTransformResults.HasNonUniformScale );

                // Dynamic NUS/Renderable NUS & NUS FLAG
                ChangeLocalScale(b, new float3(1f, 2f, 3f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                              ExpectedConvertedTransformResults.HasNonUniformScale);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform |
                                                                                     ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                                     ExpectedConvertedTransformResults.HasNonUniformScale );

                // Dynamic NUS/Renderable NUS
                ChangeFlag(b, TransformUsageFlags.Renderable);
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent |
                                                                              ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                              ExpectedConvertedTransformResults.HasNonUniformScale);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform |
                                                                                     ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                                     ExpectedConvertedTransformResults.HasNonUniformScale );

                // Dynamic NUS/Renderable
                ChangeLocalScale(b, new float3(1f, 1f, 1f));
                yield return UpdateEditorAndWorld(w);
                TestTransformUsageChange(bakingWorld, a, b, aEntity, bEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                              ExpectedConvertedTransformResults.HasLocalTransform |
                                                                              ExpectedConvertedTransformResults.HasParent |
                                                                              ExpectedConvertedTransformResults.HasValidRuntimeParent);
                TestTransformUsageChange(bakingWorld, null, a, Entity.Null, aEntity, ExpectedConvertedTransformResults.HasLocalToWorld |
                                                                                     ExpectedConvertedTransformResults.HasLocalTransform |
                                                                                     ExpectedConvertedTransformResults.HasPostTransformMatrix |
                                                                                     ExpectedConvertedTransformResults.HasNonUniformScale );
                #endregion
            }
        }

        void ChangeLocalPosition(GameObject obj, float3 position)
        {
            Undo.RecordObject(obj.transform, "Changing local position");
            obj.transform.localPosition = position;
            Undo.FlushUndoRecordObjects();
        }

        void ChangeLocalScale(GameObject obj, float3 scale)
        {
            Undo.RecordObject(obj.transform, "Changing local scale");
            obj.transform.localScale = scale;
            Undo.FlushUndoRecordObjects();
        }

        void ChangeLocalScale(List<GameObject> objs, float3 scale)
        {
            foreach (var obj in objs)
            {
                Undo.RecordObject(obj.transform, "Changing local scale");
                obj.transform.localScale = scale;
                Undo.FlushUndoRecordObjects();
            }
        }

        void ChangeParent(GameObject obj, GameObject parent, bool worldPositionStays = false)
        {
            var parentTransform = parent != null ? parent.transform : null;
            Undo.SetTransformParent(obj.transform, parentTransform, worldPositionStays, "");
        }

        void ChangeFlag(List<GameObject> objs, TransformUsageFlags flags)
        {
            foreach (var obj in objs)
            {
                AssignTransformUsageBaker.Flags[obj] = flags;
                Undo.RegisterCompleteObjectUndo(obj.transform, "");
            }
        }

        void ChangeFlag(GameObject obj, TransformUsageFlags flags)
        {
            AssignTransformUsageBaker.Flags[obj] = flags;
            Undo.RegisterCompleteObjectUndo(obj.transform, "");
        }

        void TestTransformUsageChangeIntermediates(World bakingWorld, GameObject rootGo, List<GameObject> intermediates, Entity rootEntity, List<Entity> intermediateEntities, ExpectedConvertedTransformResults expectedIntermediateDescription)
        {
            var parentEntity = rootEntity;
            var parent = rootGo;
            for (int index = 0; index < intermediates.Count; ++index)
            {
                var intermediate = intermediates[index];
                var intermediateEntity = intermediateEntities[index];

                TestTransformUsageChange(bakingWorld, parent, intermediate, parentEntity, intermediateEntity, expectedIntermediateDescription);

                // Prepare the next loop
                parent = intermediate;
                parentEntity = intermediateEntity;
            }
        }

        void TestTransformUsageChange(World bakingWorld, GameObject parentGo, GameObject childGo, Entity parentEntity, Entity childEntity, ExpectedConvertedTransformResults expectedDescriptionChild)
        {
            var bTransformAuthoring = bakingWorld.EntityManager.GetComponentData<TransformAuthoring>(childEntity);

            TestTransformUsageFlagsHelper.VerifyBakedTransformData(bakingWorld.EntityManager, expectedDescriptionChild, childGo.transform, bTransformAuthoring, childEntity, parentEntity);
        }

        Entity GetEntityByGUID(World w, EntityGuid guid)
        {
            Entity result = Entity.Null;
            var query = w.EntityManager.CreateEntityQuery(typeof(EntityGuid));
            if (!query.IsEmpty)
            {
                var entities = query.ToEntityArray(Allocator.Temp);
                var guids = query.ToComponentDataArray<EntityGuid>(Allocator.Temp);
                for (int index = 0; index < guids.Length; ++index)
                {
                    if (guids[index] == guid)
                    {
                        result = entities[index];
                        break;
                    }
                }
            }
            query.Dispose();
            return result;
        }

        public World GetBakingWorld(World w, Unity.Entities.Hash128 sceneGUID)
        {
            var editorSystem = w.GetExistingSystemManaged<EditorSubSceneLiveConversionSystem>();
            return editorSystem.GetConvertedWorldForScene(sceneGUID);
        }

        public BakingSystem GetBakingSystem(World w, Unity.Entities.Hash128 sceneGUID)
        {
            World world = GetBakingWorld(w, sceneGUID);
            return world.GetOrCreateSystemManaged<BakingSystem>();
        }

        // This starts with a tree 3 level deep with 2 children on each node. A BoxCollider will be added initially based on the parameter mask.
        // So it will run from different starting configurations.
        // After that, the test will do a pass over each node and:
        // - Add a BoxCollider is it didn't have one
        // - Remove the BoxCollider if one was in the node
        // It will rebake every time a node is changed
        [UnityTest]
        public IEnumerator IncrementalBaking_GetComponentsInChildren_Swap([Values] BakerTestsHierarchyHelper.HierarchyChildrenTests mask, [Values]Mode mode)
        {
            List<GameObject> goList = null;
            GameObject root = null;
            SubScene subScene;
            int added = 0;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    goList = BakerTestsHierarchyHelper.CreateChildrenHierarchyWithTypeList<BoxCollider>(3, 2, (uint)mask, root, out added);
                    root.AddComponent<TestGetComponentsInChildrenAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var bakingComponent = root.GetComponent<TestGetComponentsInChildrenAuthoring>();

                for (int index = 0; index < goList.Count; ++index)
                {
                    var component = goList[index].GetComponent<Collider>();
                    if (component != null)
                    {
                        Undo.DestroyObjectImmediate(component);
                        Undo.FlushUndoRecordObjects();
                    }
                    else
                    {
                        Undo.RecordObject(goList[index], "Added Component");
                        Undo.AddComponent<BoxCollider>(goList[index]);
                        Undo.FlushUndoRecordObjects();
                    }
                    yield return UpdateEditorAndWorld(w);
                    Assert.IsTrue(bakingSystem.DidBake(bakingComponent));
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetComponentsInChildren_NoRebake([Values] BakerTestsHierarchyHelper.HierarchyChildrenTests mask, [Values]Mode mode)
        {
            List<GameObject> goList = null;
            GameObject root = null;
            SubScene subScene;
            int added = 0;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    goList = BakerTestsHierarchyHelper.CreateChildrenHierarchyWithTypeList<BoxCollider>(3, 2, (uint)mask, root, out added);
                    root.AddComponent<TestGetComponentsInChildrenAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var bakingComponent = root.GetComponent<TestGetComponentsInChildrenAuthoring>();

                for (int index = 0; index < goList.Count; ++index)
                {
                    // Triggering a structural change, but not relevant to TestGetComponentsInParentAuthoring
                    Undo.RecordObject(goList[index], "Added Component");
                    Undo.AddComponent<TestMonoBehaviour>(goList[index]);
                    Undo.FlushUndoRecordObjects();

                    yield return UpdateEditorAndWorld(w);
                    Assert.IsFalse(bakingSystem.DidBake(bakingComponent));
                }
            }
        }

        // This test will start with a tree 3 levels deep with 4 children on each node. It will add a BoxCollider every "colliderStep" nodes,
        // so colliderSteps determine the starting point of the test.
        // After that it will extract all the leaf nodes and do passes swapping consecutive leafs (in a flatten structure) until all the leaf nodes in the tree are reversed
        // This will produce:
        // - Changes in the order of the children under the same parent
        // - Changes of parents
        // The test checks that the baker triggers only in the cases were the output of GetComponentsInChildren would have change
        [UnityTest]
        public IEnumerator IncrementalBaking_GetComponentsInChildren_ReverseLeafs([Values(1, 2)] int colliderStep, [Values]Mode mode)
        {
            List<GameObject> goList = new List<GameObject>();
            GameObject root = null;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    goList.Add(root);
                    BakerTestsHierarchyHelper.CreateChildrenHierarchy(root, 2, 4, goList);
                    root.AddComponent<TestGetComponentsInChildrenAuthoring>();

                    // We add one collider every step amount object
                    for (int index = 0; index < goList.Count; ++index)
                    {
                        if (index % colliderStep == 0)
                        {
                            goList[index].AddComponent<BoxCollider>();
                        }
                    }
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var bakingComponent = root.GetComponent<TestGetComponentsInChildrenAuthoring>();

                // Remove root and intermediate from the goList, so we leave only the leafs
                goList.RemoveAll(s => s.transform.childCount > 0);

                for (int upperLimit = goList.Count; upperLimit > 0; --upperLimit)
                {
                    for (int index0 = 0; index0 < upperLimit - 1; ++index0)
                    {
                        var before = root.GetComponentsInChildren<Collider>();

                        // We swap the current index with the next one
                        var go0 = goList[index0];
                        var go1 = goList[index0 + 1];

                        (goList[index0], goList[index0 + 1]) = (goList[index0 + 1], goList[index0]);

                        var parent0 = go0.transform.parent;
                        var parent1 = go1.transform.parent;

                        if (parent0 != parent1)
                        {
                            var siblingIndex0 = go0.transform.GetSiblingIndex();
                            var siblingIndex1 = go1.transform.GetSiblingIndex();

                            // Triggers change parent event
                            Undo.SetTransformParent(go0.transform, parent1, true, "Changed parent");
                            go0.transform.SetParent(parent1);
                            // Triggers change parent event
                            Undo.SetTransformParent(go1.transform, parent0, true, "Changed parent");
                            go1.transform.SetParent(parent0);

                            Undo.RegisterChildrenOrderUndo(parent1, "Update go0 sibling index");
                            go0.transform.SetSiblingIndex(siblingIndex1);
                            // Triggers change children order event
                            Undo.RegisterChildrenOrderUndo(parent0, "Update go1 sibling index");
                            go1.transform.SetSiblingIndex(siblingIndex0);

                            Undo.FlushUndoRecordObjects();
                            yield return UpdateEditorAndWorld(w);
                        }
                        else
                        {
                            var siblingIndex0 = go0.transform.GetSiblingIndex();
                            var siblingIndex1 = go1.transform.GetSiblingIndex();
                            // Triggers change children order event
                            Undo.RegisterChildrenOrderUndo(parent0, "Update go0 sibling index");
                            go0.transform.SetSiblingIndex(siblingIndex1);
                            // Triggers change children order event
                            Undo.RegisterChildrenOrderUndo(parent0, "Update go1 sibling index");
                            go1.transform.SetSiblingIndex(siblingIndex0);
                            Undo.FlushUndoRecordObjects();
                            yield return UpdateEditorAndWorld(w);
                        }

                        bool needsBaking = false;
                        var after = root.GetComponentsInChildren<Collider>();
                        for (int index = 0; index < after.Length; ++index)
                        {
                            if (before[index] != after[index])
                            {
                                needsBaking = true;
                                break;
                            }
                        }
                        // We expect it to bake if both have colliders as this will change the order fo the return value of GetComponents
                        var res = bakingSystem.DidBake(bakingComponent);
                        Assert.AreEqual(needsBaking, res);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetComponent_RebakeOnReorder()
        {
            GameObject root;
            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                root = new GameObject("TestGameObject");
                root.AddComponent<ChildATestClass>();
                root.AddComponent<ChildBTestClass>();
                root.AddComponent<ReorderComponentAuthoring>();

                SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(Mode.Edit);
            {
                var w = GetLiveConversionWorld(Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var worldBaking = GetBakingWorld(w, subScene.SceneGUID);

                // Pre-test to check the components were added correctly
                var query = new EntityQueryBuilder(Allocator.Temp).WithAll<ReorderTestComponent>().Build(worldBaking.EntityManager);
                Assert.AreEqual(1, query.CalculateEntityCount(), "Component was not correctly added to the entities");
                Assert.AreEqual(1, query.ToComponentDataArray<ReorderTestComponent>(Allocator.Temp)[0].Value, "The wrong component is found");


                // Move the current 'referenced' component down
                var childA = root.GetComponent<ChildATestClass>();

                Undo.RecordObject(root, "Move ChildA down (order A, B -> B, A)");
                UnityEditorInternal.ComponentUtility.MoveComponentDown(childA);
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, query.CalculateEntityCount(), "Component was not correctly added to the entities");
                Assert.AreEqual(2, query.ToComponentDataArray<ReorderTestComponent>(Allocator.Temp)[0].Value, "The wrong component is found");

                // Move the current 'un-referenced' component down
                var authoringRoot = root.GetComponent<ChildATestClass>();

                Undo.RecordObject(root, "Move ChildA up (order B, A -> A, B)");
                UnityEditorInternal.ComponentUtility.MoveComponentUp(authoringRoot);
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, query.CalculateEntityCount(), "Component was not correctly added to the entities");
                Assert.AreEqual(1, query.ToComponentDataArray<ReorderTestComponent>(Allocator.Temp)[0].Value, "The wrong component is found");

            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetComponent_Transform([Values]Mode mode, [Values(1,2,3)] int reparent)
        {
            // reparent = 1, change parent to my parent.parent
            // reparent = 2, change parent to root gameobject
            // reparent = 3, change parent to null
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(GetComponentTransformBaker));

            GameObject root;
            GameObject lastChild = null;
            int depth = 4;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    lastChild = BakerTestsHierarchyHelper.CreateParentHierarchy(depth, root);

                    // Add a position to every GO in the hierarchy
                    var current = lastChild.transform;
                    while (current != null)
                    {
                        current.localPosition = Vector3.one;
                        current = current.parent;
                    }

                    lastChild.AddComponent<TestComponentAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var worldBaking = GetBakingWorld(w, subScene.SceneGUID);

                // Pre-test to check the components were added correctly
                var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<GetComponentTransformBaker.Vector3Element>().Build(worldBaking.EntityManager);
                Assert.AreEqual(1, query.CalculateEntityCount(), "Components were not correctly added to the entities");


                // Choose new parent
                var current = lastChild.transform.parent;
                for (int index = 0; index < reparent; ++index)
                {
                    current = current.parent;
                }

                Undo.SetTransformParent(lastChild.transform, current, false, "Changing Parents" );
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                using var entities = query.ToEntityArray(Allocator.TempJob);
                var childEntity = entities[0];
                var position = worldBaking.EntityManager.GetComponentData<GetComponentTransformBaker.Vector3Element>(childEntity);
                Assert.AreEqual(position.Value.x, (float)(depth - reparent));
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetComponentsInParent_Swap([Values] BakerTestsHierarchyHelper.ParentHierarchyMaskTests mask, [Values]Mode mode)
        {
            List<GameObject> goList = null;
            GameObject lastChild = null;
            SubScene subScene;
            int added = 0;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    GameObject root = new GameObject("TestGameObject");
                    goList = BakerTestsHierarchyHelper.CreateHierarchyWithType<BoxCollider>(mask, root, out added);
                    lastChild = goList[goList.Count - 1];
                    lastChild.AddComponent<TestGetComponentsInParentAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var bakingComponent = lastChild.GetComponent<TestGetComponentsInParentAuthoring>();

                for (int index = 0; index < goList.Count; ++index)
                {
                    var component = goList[index].GetComponent<Collider>();
                    if (component != null)
                    {
                        Undo.DestroyObjectImmediate(component);
                        Undo.FlushUndoRecordObjects();
                    }
                    else
                    {
                        Undo.RecordObject(goList[index], "Added Component");
                        Undo.AddComponent<BoxCollider>(goList[index]);
                        Undo.FlushUndoRecordObjects();
                    }
                    yield return UpdateEditorAndWorld(w);
                    Assert.IsTrue(bakingSystem.DidBake(bakingComponent));
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetComponentsInParent_NoRebake([Values] BakerTestsHierarchyHelper.ParentHierarchyMaskTests mask, [Values]Mode mode)
        {
            List<GameObject> goList = null;
            GameObject lastChild = null;
            SubScene subScene;
            int added = 0;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    GameObject root = new GameObject("TestGameObject");
                    goList = BakerTestsHierarchyHelper.CreateHierarchyWithType<BoxCollider>(mask, root, out added);
                    lastChild = goList[goList.Count - 1];
                    lastChild.AddComponent<TestGetComponentsInParentAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var bakingComponent = lastChild.GetComponent<TestGetComponentsInParentAuthoring>();

                for (int index = 0; index < goList.Count; ++index)
                {
                    // Triggering a structural change, but not relevant to TestGetComponentsInParentAuthoring
                    Undo.RecordObject(goList[index], "Added Component");
                    Undo.AddComponent<TestMonoBehaviour>(goList[index]);
                    Undo.FlushUndoRecordObjects();

                    yield return UpdateEditorAndWorld(w);
                    Assert.IsFalse(bakingSystem.DidBake(bakingComponent));
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetParent([Values]Mode mode)
        {
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(GetParentBaker));

            GameObject root;
            GameObject lastChild = null;
            int depth = 4;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    lastChild = BakerTestsHierarchyHelper.CreateParentHierarchy(depth, root);

                    lastChild.AddComponent<TestComponentAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var worldBaking = GetBakingWorld(w, subScene.SceneGUID);

                // Pre-test to check the components were added correctly
                var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<GetParentBaker.IntElement>().Build(worldBaking.EntityManager);
                Assert.AreEqual(1, query.CalculateEntityCount(), "Components were not correctly added to the entities");

                using var entities = query.ToEntityArray(Allocator.TempJob);
                var bakingComponent = lastChild.GetComponent<TestComponentAuthoring>();

                for (int index = 0; index < depth; ++index)
                {
                    // Check that the parent is the expected one
                    var buffer = worldBaking.EntityManager.GetBuffer<GetParentBaker.IntElement>(entities[0]);
                    Assert.AreEqual(lastChild.transform.parent != null ? 1 : 0, buffer.Length, "Expected buffer with size to match");

                    if (lastChild.transform.parent != null)
                    {
                        var parentTransforms = lastChild.GetComponentsInParent<Transform>();
                        Assert.AreEqual(parentTransforms[1].gameObject.GetInstanceID(), buffer[0].Value, $"Expected the parent instance ID {parentTransforms[1].gameObject.GetInstanceID()} on iteration {index}");

                        // Check that moving the parent doesn't trigger baking
                        Undo.RecordObject(lastChild.transform.parent, "Change component value");
                        lastChild.transform.parent.position += new Vector3(1, 1, 1);
                        Undo.FlushUndoRecordObjects();

                        // Moving the parent's transform position should not rebake this component
                        yield return UpdateEditorAndWorld(w);
                        Assert.IsFalse(bakingSystem.DidBake(bakingComponent));

                        // Changing the parent the next one up the hierarchy
                        Undo.SetTransformParent(lastChild.transform, lastChild.transform.parent.parent, "Changing Parents" );
                        Undo.FlushUndoRecordObjects();

                        yield return UpdateEditorAndWorld(w);
                        Assert.IsTrue(bakingSystem.DidBake(bakingComponent));
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetParents([Values]Mode mode)
        {
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(GetParentsBaker));

            GameObject root;
            GameObject lastChild = null;
            int depth = 4;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    lastChild = BakerTestsHierarchyHelper.CreateParentHierarchy(depth, root);

                    lastChild.AddComponent<TestComponentAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var worldBaking = GetBakingWorld(w, subScene.SceneGUID);

                // Pre-test to check the components were added correctly
                var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<GetParentsBaker.IntElement>().Build(worldBaking.EntityManager);
                Assert.AreEqual(1, query.CalculateEntityCount(), "Components were not correctly added to the entities");

                using var entities = query.ToEntityArray(Allocator.TempJob);
                var bakingComponent = lastChild.GetComponent<TestComponentAuthoring>();

                for (int index = 0; index < depth; ++index)
                {
                    // Check that the parent is the expected one
                    var buffer = worldBaking.EntityManager.GetBuffer<GetParentsBaker.IntElement>(entities[0]);
                    var parentTransforms = lastChild.GetComponentsInParent<Transform>();
                    Assert.AreEqual(parentTransforms.Length - 1, buffer.Length, "Expected buffer with size to match");

                    if (lastChild.transform.parent != null)
                    {
                        for (int bufferIndex = 0; bufferIndex < buffer.Length; bufferIndex++)
                            Assert.AreEqual(parentTransforms[bufferIndex + 1].gameObject.GetInstanceID(), buffer[bufferIndex].Value, $"Expected the parent instance ID {parentTransforms[1].gameObject.GetInstanceID()} on iteration {index} - {bufferIndex}");

                        // Check that moving the parent doesn't trigger baking
                        Undo.RecordObject(lastChild.transform.parent, "Change component value");
                        lastChild.transform.parent.position += new Vector3(1, 1, 1);
                        Undo.FlushUndoRecordObjects();

                        // Moving the parent's transform position should not rebake this component
                        yield return UpdateEditorAndWorld(w);
                        Assert.IsFalse(bakingSystem.DidBake(bakingComponent));

                        // Changing the parent the next one up the hierarchy
                        Undo.SetTransformParent(lastChild.transform, lastChild.transform.parent.parent, "Changing Parents" );
                        Undo.FlushUndoRecordObjects();

                        yield return UpdateEditorAndWorld(w);
                        Assert.IsTrue(bakingSystem.DidBake(bakingComponent));
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetChildCount([Values]Mode mode)
        {
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(GetChildCountBaker));

            List<GameObject> goList = new List<GameObject>();
            GameObject root = null;
            SubScene subScene;
            uint depth = 3;
            int childrenCount = 2;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    BakerTestsHierarchyHelper.CreateChildrenHierarchy(root, depth - 1, childrenCount, goList);
                    root.AddComponent<TestComponentAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var worldBaking = GetBakingWorld(w, subScene.SceneGUID);

                // Pre-test to check the components were added correctly
                var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<GetChildCountBaker.IntComponent>().Build(worldBaking.EntityManager);
                Assert.AreEqual(1, query.CalculateEntityCount(), "Components were not correctly added to the entities");

                using var entities = query.ToEntityArray(Allocator.TempJob);
                var bakingComponent = root.GetComponent<TestComponentAuthoring>();

                for (int index = 0; index < depth; ++index)
                {
                    // Check that the parent is the expected one
                    var component = worldBaking.EntityManager.GetComponentData<GetChildCountBaker.IntComponent>(entities[0]);
                    Assert.AreEqual(bakingComponent.transform.childCount, component.Value, $"Expected the Value match the number of immediate Children {bakingComponent.transform.childCount}");

                    if (bakingComponent.transform.childCount > 0)
                    {
                        Transform firstChild = bakingComponent.transform.GetChild(0);

                        // Check that moving the parent doesn't trigger baking
                        Undo.RecordObject(firstChild, "Change component value");
                        firstChild.position += new Vector3(1, 1, 1);
                        Undo.FlushUndoRecordObjects();

                        // Moving the parent's transform position should not rebake this component
                        yield return UpdateEditorAndWorld(w);
                        Assert.IsFalse(bakingSystem.DidBake(bakingComponent));

                        // Changing the parent the next one up the hierarchy
                        Undo.DestroyObjectImmediate(firstChild.gameObject);
                        Undo.FlushUndoRecordObjects();

                        yield return UpdateEditorAndWorld(w);
                        Assert.IsTrue(bakingSystem.DidBake(bakingComponent));
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetChild([Values]Mode mode)
        {
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(GetChildBaker));

            List<GameObject> goList = new List<GameObject>();
            GameObject root = null;
            SubScene subScene;
            uint depth = 3;
            int childrenCount = 2;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    BakerTestsHierarchyHelper.CreateChildrenHierarchy(root, depth - 1, childrenCount, goList);
                    root.AddComponent<TestComponentAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var worldBaking = GetBakingWorld(w, subScene.SceneGUID);

                // Pre-test to check the components were added correctly
                var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<GetChildBaker.IntElement>().Build(worldBaking.EntityManager);
                Assert.AreEqual(1, query.CalculateEntityCount(), "Components were not correctly added to the entities");

                using var entities = query.ToEntityArray(Allocator.TempJob);
                var bakingComponent = root.GetComponent<TestComponentAuthoring>();

                for (int index = 0; index < depth; ++index)
                {
                    // Check that the parent is the expected one
                    var buffer = worldBaking.EntityManager.GetBuffer<GetChildBaker.IntElement>(entities[0]);
                    int expectedChildCount = bakingComponent.transform.childCount > 0 ? 1 : 0;
                    Assert.AreEqual(expectedChildCount, buffer.Length, $"Expected the buffer to be size {expectedChildCount}");

                    if (expectedChildCount > 0)
                    {
                        Transform firstChild = bakingComponent.transform.GetChild(0);
                        Assert.AreEqual(firstChild.gameObject.GetInstanceID(), buffer[0].Value, $"Expected GO Instance ID {firstChild.gameObject.GetInstanceID()}");

                        // Check that moving the parent doesn't trigger baking
                        Undo.RecordObject(firstChild, "Change component value");
                        firstChild.position += new Vector3(1, 1, 1);
                        Undo.FlushUndoRecordObjects();

                        // Moving the parent's transform position should not rebake this component
                        yield return UpdateEditorAndWorld(w);
                        Assert.IsFalse(bakingSystem.DidBake(bakingComponent));

                        // Changing the parent the next one up the hierarchy
                        Undo.DestroyObjectImmediate(firstChild.gameObject);
                        Undo.FlushUndoRecordObjects();

                        yield return UpdateEditorAndWorld(w);
                        Assert.IsTrue(bakingSystem.DidBake(bakingComponent));
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetChildren([Values]Mode mode, [Values]bool recursive)
        {
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(GetChildrenBaker));
            GetChildrenBaker.Recursive = recursive;

            List<GameObject> goList = new List<GameObject>();
            GameObject root = null;
            SubScene subScene;
            uint depth = 4;
            int childrenCount = 3;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    BakerTestsHierarchyHelper.CreateChildrenHierarchy(root, depth - 1, childrenCount, goList);
                    root.AddComponent<TestComponentAuthoring>();
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);

            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var worldBaking = GetBakingWorld(w, subScene.SceneGUID);

                // Pre-test to check the components were added correctly
                var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<GetChildrenBaker.IntElement>().Build(worldBaking.EntityManager);
                Assert.AreEqual(1, query.CalculateEntityCount(), "Components were not correctly added to the entities");

                using var entities = query.ToEntityArray(Allocator.TempJob);
                var bakingComponent = root.GetComponent<TestComponentAuthoring>();

                if (!recursive)
                {
                    // To use only immediate children
                    goList.Clear();
                    foreach (Transform t in root.transform)
                    {
                        goList.Add(t.gameObject);
                    }
                }

                for (int index = goList.Count - 1; index >= -1; --index)
                {
                    // Check that the parent is the expected one
                    var buffer = worldBaking.EntityManager.GetBuffer<GetChildrenBaker.IntElement>(entities[0]);
                    int expectedChildCount = goList.Count;
                    Assert.AreEqual(expectedChildCount, buffer.Length, $"Expected the buffer to be size {expectedChildCount}");

                    if (expectedChildCount > 0)
                    {
                        GameObject lastChildGo = goList[index];
                        for (int bufferIndex = 0; bufferIndex < goList.Count; ++bufferIndex)
                            Assert.AreEqual(goList[bufferIndex].GetInstanceID(), buffer[bufferIndex].Value, $"Expected GO Instance ID {goList[bufferIndex].GetInstanceID()} - Index: {bufferIndex}");

                        // Check that moving the parent doesn't trigger baking
                        Undo.RecordObject(lastChildGo.transform, "Change component value");
                        lastChildGo.transform.position += new Vector3(1, 1, 1);
                        Undo.FlushUndoRecordObjects();

                        // Moving the parent's transform position should not rebake this component
                        yield return UpdateEditorAndWorld(w);
                        Assert.IsFalse(bakingSystem.DidBake(bakingComponent));

                        // Changing the parent the next one up the hierarchy
                        Undo.DestroyObjectImmediate(lastChildGo);
                        goList.RemoveAt(index);
                        Undo.FlushUndoRecordObjects();

                        yield return UpdateEditorAndWorld(w);
                        Assert.IsTrue(bakingSystem.DidBake(bakingComponent));
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_LinkedEntityGroupRemoval([Values]Mode mode)
        {
            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(TestAdditionalEntityComponentAuthoring.Baker), typeof(LinkedEntityGroupAuthoringBaker));

            List<GameObject> goList = new List<GameObject>();
            GameObject root = null;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    goList.Add(root);
                    BakerTestsHierarchyHelper.CreateChildrenHierarchy(root, 2, 1, goList);
                    root.AddComponent<LinkedEntityGroupAuthoring>();

                    for (int index = 0; index < goList.Count; ++index)
                    {
                        var component = goList[index].AddComponent<TestAdditionalEntityComponentAuthoring>();
                        component.value = 1;
                    }
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                // Remove the LinkedEntity Group component
                Undo.DestroyObjectImmediate(root.GetComponent<LinkedEntityGroupAuthoring>());
                Assert.IsNull(root.GetComponent<LinkedEntityGroupAuthoring>());
                yield return UpdateEditorAndWorld(w);

                // Readded it
                Undo.AddComponent<LinkedEntityGroupAuthoring>(root);
                Assert.IsNotNull(root.GetComponent<LinkedEntityGroupAuthoring>());
                yield return UpdateEditorAndWorld(w);

                // Remove a child
                GameObject last = goList[goList.Count - 1];
                Undo.DestroyObjectImmediate(last);
                Undo.IncrementCurrentGroup();
                yield return UpdateEditorAndWorld(w);

                // Change number of additional entities
                GameObject secondToLast = goList[goList.Count - 2];
                var additionalEntityComponent = secondToLast.GetComponent<TestAdditionalEntityComponentAuthoring>();
                additionalEntityComponent.value = 10;
                Undo.RecordObject(additionalEntityComponent, "Changed additional entity count");
                yield return UpdateEditorAndWorld(w);

                // Change number of additional entities
                additionalEntityComponent.value = 2;
                Undo.RecordObject(additionalEntityComponent, "Changed additional entity count");
                yield return UpdateEditorAndWorld(w);

                // Remove additional entity component
                Undo.DestroyObjectImmediate(additionalEntityComponent);
                Undo.IncrementCurrentGroup();
                yield return UpdateEditorAndWorld(w);
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_PreprocessPosprocessConsistency_NoBakersRun()
        {
            Mode mode = Mode.Edit;

            using var baking = new BakerDataUtility.OverrideBakers(true, typeof(TestAdditionalEntityComponentAuthoring.Baker), typeof(LinkedEntityGroupAuthoringBaker));
            var originalColor = m_TestMaterial.GetColor("_BaseColor");

            List<GameObject> goList = new List<GameObject>();
            GameObject root = null;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    goList.Add(root);
                    BakerTestsHierarchyHelper.CreateChildrenHierarchy(root, 2, 1, goList);
                    root.AddComponent<LinkedEntityGroupAuthoring>();

                    // We add one collider every step amount object
                    for (int index = 0; index < goList.Count; ++index)
                    {
                        var component = goList[index].AddComponent<TestAdditionalEntityComponentAuthoring>();
                        component.value = 1;
                    }
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);
                Color modifiedColor = (originalColor != Color.white ? Color.white : Color.black);

                // We force the asset database to change the version, but with an asset that will not trigger any baker
                // This will check that Preprocess and Postprocess run consistently, even if no baker runs
                m_TestMaterial.SetColor("_BaseColor", modifiedColor);
                AssetDatabase.SaveAssetIfDirty(m_TestMaterial);
                AssetDatabase.Refresh();

                yield return UpdateEditorAndWorld(w);
            }

            // Restore original color
            m_TestMaterial.SetColor("_BaseColor", Color.white);
            AssetDatabase.SaveAssetIfDirty(m_TestMaterial);
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_PrimaryEntityDeletion([Values] Mode mode)
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(TestDeletePrimaryBaker));

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new Regex(@".*The primary entity for the GameObject .* was deleted in a previous baking pass\..* This forces to rebake the whole GameObject\..*"));

            GameObject go;
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                go = new GameObject("Root");
                var component = go.AddComponent<TestDeletePrimary>();
                component.value = 5;
                component.delete = true;
                SceneManager.MoveGameObjectToScene(go, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                // We test that the additional entity created as bake only is not present in the destination world
                // The primary entity and the other additional entity should be in the destination world
                var testBakingOnlyQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<TestDeletePrimaryComponent>());
                Assert.AreEqual(0, testBakingOnlyQuery.CalculateEntityCount(), "The entity should have been deleted");

                var component = go.GetComponent<TestDeletePrimary>();
                for (int index = 0; index < 3; ++index)
                {
                    Undo.RecordObject(component, "Changed delete value");
                    component.delete = !component.delete;
                    yield return UpdateEditorAndWorld(w);

                    Assert.AreEqual((index % 2 == 0) ? 1 : 0, testBakingOnlyQuery.CalculateEntityCount(), $"Unexpected result for index {index} delete {component.delete}");
                }
            }

        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetAbstractComponent([Values] Mode mode)
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(Unity.Entities.Hybrid.Tests.Baking.BakerTests.GetComponent_With_AbstractBaseClass_Baker));
            GameObject go;
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                go = new GameObject("Root");
                var component = go.AddComponent<Unity.Entities.Hybrid.Tests.Baking.BakerTests.GetComponent_With_AbstractBaseClass>();
                component.Field = 5;
                SceneManager.MoveGameObjectToScene(go, subScene.EditingScene);
            }
            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);
                if (w != null)
                {
                    var testQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<Unity.Entities.Hybrid.Tests.Baking.ComponentTest4>());
                    Assert.AreEqual(1, testQuery.CalculateEntityCount(), "The entity should have been created");

                    // Updating the abstract component should retrigger baking as it should have been recorded as a dependency to the component GetComponent_With_AbstractBaseClass
                    var component = go.GetComponent<Unity.Entities.Hybrid.Tests.Baking.Authoring_Abstract>();
                    Undo.RecordObjects(new Object[]{component}, "Changed values");
                    component.Field = 7;
                    Undo.FlushUndoRecordObjects();
                    yield return UpdateEditorAndWorld(w);

                    Assert.AreEqual(1, testQuery.CalculateEntityCount());
                    var entityArray = testQuery.ToEntityArray(Allocator.Temp);
                    Assert.AreEqual(7, w.EntityManager.GetComponentData<Unity.Entities.Hybrid.Tests.Baking.ComponentTest4>(entityArray[0]).Field);
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_GetAbstractComponentInParent([Values] Mode mode)
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(Unity.Entities.Hybrid.Tests.Baking.BakerTests.GetComponentInParent_With_AbstractBaseClass_Baker));
            GameObject parent;
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                parent = new GameObject("Root");
                var component = parent.AddComponent<Unity.Entities.Hybrid.Tests.Baking.Authoring_DerivedFromAbstract>();
                component.Field = 5;
                GameObject child = new GameObject();
                child.transform.SetParent(parent.transform);
                child.AddComponent<Unity.Entities.Hybrid.Tests.Baking.BakerTests.GetComponentInParent_With_AbstractBaseClass>();
                SceneManager.MoveGameObjectToScene(parent, subScene.EditingScene);
            }
            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);
                if (w != null)
                {
                    var testQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<Unity.Entities.Hybrid.Tests.Baking.ComponentTest4>());
                    Assert.AreEqual(1, testQuery.CalculateEntityCount(), "The entity should have been created");

                    // Updating the abstract component on the parent should retrigger baking as it should have been recorded as a dependency to the component GetComponentInParent_With_AbstractBaseClass_Baker
                    var component = parent.GetComponent<Unity.Entities.Hybrid.Tests.Baking.Authoring_DerivedFromAbstract>();
                    Undo.RecordObjects(new Object[]{component}, "Changed values");
                    component.Field = 7;
                    Undo.FlushUndoRecordObjects();
                    yield return UpdateEditorAndWorld(w);

                    Assert.AreEqual(1, testQuery.CalculateEntityCount());
                    var entityArray = testQuery.ToEntityArray(Allocator.Temp);
                    Assert.AreEqual(7, w.EntityManager.GetComponentData<Unity.Entities.Hybrid.Tests.Baking.ComponentTest4>(entityArray[0]).Field);
                }
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_BakingOnlyAdditionalEntity([Values] Mode mode)
        {
            {
                var subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("Root");
                a.AddComponent<BakingOnlyAdditionalEntityTestAuthoring>();
                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                // We test that the additional entity created as bake only is not present in the destination world
                // The primary entity and the other additional entity should be in the destination world
                var testBakingOnlyQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<BakingOnlyAdditionalEntityTestAuthoring.BakingOnlyEntityTestComponent>());
                Assert.AreEqual(2, testBakingOnlyQuery.CalculateEntityCount(), "The additional entity created as baking only should not be present in the destination world");
            }
        }


        [UnityTest]
        public IEnumerator IncrementalBaking_BakingOnlyPrimaryEntity([Values] Mode mode)
        {
            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var primary = new GameObject("Root");
                primary.AddComponent<BakingOnlyEntityAuthoring>();
                SceneManager.MoveGameObjectToScene(primary, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);
                var wB = GetBakingWorld(w, subScene.SceneGUID);

                // Pre-test to check the components were added correctly
                var testBakingOnlyQueryB =
                    wB.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<BakingOnlyEntity>());
                Assert.AreEqual(1, testBakingOnlyQueryB.CalculateEntityCount(),
                    "Components were not correctly added to the entities");

                // We test that the BakeOnly primary entity is not present in the destination world
                var testBakingOnlyQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<BakingOnlyEntity>());
                Assert.AreEqual(0, testBakingOnlyQuery.CalculateEntityCount(),
                    "The primary entity created as baking only should not be present in the destination world");
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_BakingOnlyPrimaryEntity_AdditionalEntities([Values] Mode mode)
        {
            SubScene subScene;
            {
                subScene  = CreateEmptySubScene("TestSubScene", true);

                var primary = new GameObject("Root");
                primary.AddComponent<BakingOnlyEntityAuthoring>();
                primary.AddComponent<BakingOnlyPrimaryWithAdditionalEntitiesTestAuthoring>();
                SceneManager.MoveGameObjectToScene(primary, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(mode);
            {
                var expectedEntities = 1;
                var w = GetLiveConversionWorld(mode);
                var wB = GetBakingWorld(w, subScene.SceneGUID);

                // Pre-test to check the components were added correctly
                var testBakingOnlyQueryB = wB.EntityManager.CreateEntityQuery(ComponentType
                    .ReadWrite<BakingOnlyPrimaryWithAdditionalEntitiesTestAuthoring.PrimaryBakeOnlyAdditionalEntityTestComponent>());
                Assert.AreEqual(3, testBakingOnlyQueryB.CalculateEntityCount(),
                    "Components were not correctly added to the entities");

                // We test that the BakeOnly primary entity is not present in the destination world
                // The BakeOnly additional entity created is not present in the destination world
                // The 'normal' additional entity created should be present in the destination world
                var testBakingOnlyQuery = w.EntityManager.CreateEntityQuery(ComponentType
                    .ReadWrite<BakingOnlyPrimaryWithAdditionalEntitiesTestAuthoring.PrimaryBakeOnlyAdditionalEntityTestComponent>());

                // If there are fewer entities than there should be
                Assert.GreaterOrEqual(expectedEntities, testBakingOnlyQuery.CalculateEntityCount(),
                    "The additional entity created should always be present in the destination world, regardless of if the primary entity is marked as baking only or not");

                // If there are more entities that there should be
                Assert.LessOrEqual(expectedEntities, testBakingOnlyQuery.CalculateEntityCount(),
                    "The primary entity created as baking only should not be present in the destination world");
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_BakingOnlyPrimaryEntity_PropagateToChildren([Values] Mode mode)
        {
            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var primary = new GameObject("Root");
                BakerTestsHierarchyHelper.CreateChildrenHierarchy(primary, 2, 2, new List<GameObject>());

                primary.AddComponent<BakingOnlyEntityAuthoring>();
                primary.AddComponent<BakingOnlyPrimaryChildrenTestAuthoring>();

                SceneManager.MoveGameObjectToScene(primary, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);
                var wB = GetBakingWorld(w, subScene.SceneGUID);

                // Pre-test to check the components were added correctly
                var testBakingOnlyQueryB = wB.EntityManager.CreateEntityQuery(ComponentType
                    .ReadWrite<BakingOnlyPrimaryChildrenTestAuthoring.PrimaryBakeOnlyChildrenTestComponent>());
                Assert.AreEqual(7, testBakingOnlyQueryB.CalculateEntityCount(),
                    "Components were not correctly added to the entities");


                // We test that the BakeOnly primary entity and all its children are not present in the destination world
                var testBakingOnlyQuery = w.EntityManager.CreateEntityQuery(ComponentType
                    .ReadWrite<BakingOnlyPrimaryChildrenTestAuthoring.PrimaryBakeOnlyChildrenTestComponent>());
                Assert.AreEqual(0, testBakingOnlyQuery.CalculateEntityCount(),
                    "The primary entity and the children of the primary entity created as baking only should not be present in the destination world");
            }
        }


        [UnityTest]
        public IEnumerator IncrementalBaking_BakingOnlyPrimaryEntity_AdditionalOnChildren([Values] Mode mode)
        {
            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var primary = new GameObject("Root");
                var children = new List<GameObject>();
                BakerTestsHierarchyHelper.CreateChildrenHierarchy(primary, 2, 2, children);

                primary.AddComponent<BakingOnlyEntityAuthoring>();
                primary.AddComponent<BakingOnlyPrimaryChildrenTestAuthoring>();

                for (int i = 0; i < children.Count; i++)
                {
                    children[i].AddComponent<BakingOnlyPrimaryWithAdditionalEntitiesTestAuthoring>();
                }

                SceneManager.MoveGameObjectToScene(primary, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(mode);
            {
                var expectedEntities = 6;
                var w = GetLiveConversionWorld(mode);
                var wB = GetBakingWorld(w, subScene.SceneGUID);

                // Pre-test to check the components were added correctly
                var testBakingOnlyQueryB = wB.EntityManager.CreateEntityQuery(ComponentType
                    .ReadWrite<BakingOnlyPrimaryWithAdditionalEntitiesTestAuthoring.PrimaryBakeOnlyAdditionalEntityTestComponent>());
                Assert.AreEqual(18, testBakingOnlyQueryB.CalculateEntityCount(),
                    "Components were not correctly added to the entities");


                // We test that the BakeOnly primary entity and all its children are not present in the destination world
                var testBakingOnlyQueryAdd = w.EntityManager.CreateEntityQuery(ComponentType
                    .ReadWrite<BakingOnlyPrimaryChildrenTestAuthoring.PrimaryBakeOnlyChildrenTestComponent>());
                Assert.AreEqual(0, testBakingOnlyQueryAdd.CalculateEntityCount(),
                    "The primary entity and the children of the primary entity created as baking only should not be present in the destination world");


                // We test that the BakeOnly primary entity and all its children are not present in the destination world
                // All additional entities of the children should pe present in the destination world
                var testBakingOnlyQuery = w.EntityManager.CreateEntityQuery(ComponentType
                    .ReadWrite<BakingOnlyPrimaryWithAdditionalEntitiesTestAuthoring.PrimaryBakeOnlyAdditionalEntityTestComponent>());

                // If there are fewer entities than there should be
                Assert.GreaterOrEqual(expectedEntities, testBakingOnlyQuery.CalculateEntityCount(),
                    "Additional entities created by children of the primary entity created as baking only should be present in the destination world");

                // If there are more entities that there should be
                Assert.LessOrEqual(expectedEntities, testBakingOnlyQuery.CalculateEntityCount(),
                    "The primary entity and the children of the primary entity created as baking only should not be present in the destination world");

            }
        }

        public static Entity GetDestEntityFromBakingEntity(Entity bakingEntity, World bakingWorld, ref EntityQuery entityGUIDWorldQuery)
        {
            var bakingGUID = bakingWorld.EntityManager.GetComponentData<EntityGuid>(bakingEntity);
            var entities = entityGUIDWorldQuery.ToEntityArray(Allocator.Temp);
            var entityGUIDArray = entityGUIDWorldQuery.ToComponentDataArray<EntityGuid>(Allocator.Temp);
            for (int index = 0; index < entityGUIDArray.Length; ++index)
            {
                if (entityGUIDArray[index] == bakingGUID)
                {
                    return entities[index];
                }
            }
            return Entity.Null;
        }

        public static void CheckLinkedEntityGroup(World world, Entity root, Entity[] legContent)
        {
            var buffer = world.EntityManager.GetBuffer<LinkedEntityGroup>(root).ToNativeArray(Allocator.Temp);
            Assert.AreEqual(legContent.Length, buffer.Length, "LinkedEntityGroup contains a different number of entities than expected");

            for (int index = 0; index < buffer.Length; ++index)
            {
                Assert.AreEqual(legContent[index], buffer[index].Value, "LinkedEntityGroup contains a different entity than expected");
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_LinkEntityGroupUpdate([Values]Mode mode)
        {
            GameObject root = null;
            GameObject child0 = null;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    var flagComponent = root.AddComponent<AddTransformUsageFlag>();
                    flagComponent.flags = TransformUsageFlags.Dynamic;

                    child0 = new GameObject("Child0");
                    flagComponent = child0.AddComponent<AddTransformUsageFlag>();
                    flagComponent.flags = TransformUsageFlags.Dynamic;

                    child0.transform.SetParent(root.transform);

                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var worldBaking = GetBakingWorld(w, subScene.SceneGUID);

                var bakingWorldQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<LinkedEntityGroup>().Build(worldBaking.EntityManager);
                var destWorldQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<LinkedEntityGroup>().Build(w.EntityManager);

                Assert.AreEqual(0, bakingWorldQuery.CalculateEntityCount(), "No LinkedEntityGroup expected in the baking world");
                Assert.AreEqual(0, destWorldQuery.CalculateEntityCount(), "No LinkedEntityGroup expected in the destination world");

                // Add the linked entity group to the root
                Undo.AddComponent<LinkedEntityGroupAuthoring>(root);
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(1, bakingWorldQuery.CalculateEntityCount(), "1 LinkedEntityGroup expected in the baking world");
                Assert.AreEqual(1, destWorldQuery.CalculateEntityCount(), "1 LinkedEntityGroup expected in the destination world");

                var rootBakingEntity = bakingSystem.GetEntity(root);
                var child0BakingEntity = bakingSystem.GetEntity(child0);

                var entityGUIDWorldQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<EntityGuid>().Build(w.EntityManager);
                Entity rootEntity = GetDestEntityFromBakingEntity(rootBakingEntity, worldBaking, ref entityGUIDWorldQuery);
                Entity child0Entity = GetDestEntityFromBakingEntity(child0BakingEntity, worldBaking, ref entityGUIDWorldQuery);

                // Check that the linked entity group in the baking world and the dest world contains the right entities
                CheckLinkedEntityGroup(worldBaking, rootBakingEntity, new[] {rootBakingEntity, child0BakingEntity});
                CheckLinkedEntityGroup(w, rootEntity, new[] {rootEntity, child0Entity});

                // Add an extra child to the linked entity group
                SceneManager.SetActiveScene(subScene.EditingScene);
                var child1 = new GameObject("Child1");
                var flagComponent = Undo.AddComponent<AddTransformUsageFlag>(child1);
                flagComponent.flags = TransformUsageFlags.Dynamic;

                child1.transform.SetParent(root.transform);
                Undo.SetTransformParent(child1.transform, root.transform, "Set Parent");

                Undo.RegisterCreatedObjectUndo(child1, "Create new object");
                Assert.AreEqual(child1.scene, subScene.EditingScene);

                yield return UpdateEditorAndWorld(w);

                var child1BakingEntity = bakingSystem.GetEntity(child1);
                Entity child1Entity = GetDestEntityFromBakingEntity(child1BakingEntity, worldBaking, ref entityGUIDWorldQuery);

                // Check that the linked entity group in the baking world and the dest world contains the right entities
                CheckLinkedEntityGroup(worldBaking, rootBakingEntity, new[] {rootBakingEntity, child0BakingEntity, child1BakingEntity});
                CheckLinkedEntityGroup(w, rootEntity, new[] {rootEntity, child0Entity, child1Entity});

                // Remove the first child from the linked entity group
                Undo.DestroyObjectImmediate(child0);
                yield return UpdateEditorAndWorld(w);

                // Check that the linked entity group in the baking world and the dest world contains the right entities
                CheckLinkedEntityGroup(worldBaking, rootBakingEntity, new[] {rootBakingEntity, child1BakingEntity});
                CheckLinkedEntityGroup(w, rootEntity, new[] {rootEntity, child1Entity});

                // Remove the linked entity group from the root
                Undo.DestroyObjectImmediate(root.GetComponent<LinkedEntityGroupAuthoring>());

                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(0, bakingWorldQuery.CalculateEntityCount(), "No LinkedEntityGroup expected in the baking world");
                Assert.AreEqual(0, destWorldQuery.CalculateEntityCount(), "No LinkedEntityGroup expected in the destination world");
            }
        }

        Entities.Hash128 CalculateBlobHash(int value) => CustomHashHelpers.Compute(value);

        [UnityTest]
        public IEnumerator IncrementalBaking_AddAndUpdateBlobAssetRefCount_BakerAndSystem([Values]Mode mode)
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(AddBlobAssetWithCustomHashBaker), typeof(BlobAssetTestSystemBaker));
            LiveConversionSettings.AdditionalConversionSystems.Add(typeof(BlobAssetStoreRefCountingBakingSystem));

            GameObject root = null;
            SubScene subScene;
            {
                subScene = CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    root = new GameObject("TestGameObject");
                    var bakerComponent = root.AddComponent<BlobAssetAddTestAuthoring>();
                    bakerComponent.blobValue = 1;
                    var systemComponent = root.AddComponent<BlobAssetTestSystemAuthoring>();
                    systemComponent.blobValue = 2;
                    return new List<GameObject> {root};
                });
            }

            yield return GetEnterPlayMode(mode);
            {
                var w = GetLiveConversionWorld(mode);

                yield return UpdateEditorAndWorld(w);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var blobAssetStore = bakingSystem.BlobAssetStore;
                Assert.IsNotNull(blobAssetStore);

                var hash1 = CalculateBlobHash(1);
                var hash2 = CalculateBlobHash(2);
                var hash3 = CalculateBlobHash(3);

                // Only hash1 and hash2 should initially exist
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(2, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(hash1));
                Assert.IsTrue(blobAssetStore.Contains<int>(hash2));

                // Change both blobs to the same value to make sure that the old ones get deleted and the new one has 2 references
                var bakerComponent = root.GetComponent<BlobAssetAddTestAuthoring>();
                var systemComponent = root.GetComponent<BlobAssetTestSystemAuthoring>();
                Undo.RecordObjects(new Object[]{bakerComponent, systemComponent}, "Changed values for blobs");
                bakerComponent.blobValue = 3;
                systemComponent.blobValue = 3;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                // Only hash3 should exist (referenced twice), hash1 and hash2 aren't used anymore
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(1, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(hash3));

                // Change the baker blob so the hash3 has again one ref
                Undo.RecordObjects(new Object[]{bakerComponent}, "Changed values for blobs");
                bakerComponent.blobValue = 1;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                // Only hash1 and hash3 should exist (referenced once each)
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(2, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(hash1));
                Assert.IsTrue(blobAssetStore.Contains<int>(hash3));

                // Change back to the same blob value
                Undo.RecordObjects(new Object[]{bakerComponent}, "Changed values for blobs");
                bakerComponent.blobValue = 3;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                // Only hash3 should exist (referenced twice)
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(1, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(hash3));

                // Change the system blob so the hash3 has again one refs
                Undo.RecordObjects(new Object[]{systemComponent}, "Changed values for blobs");
                systemComponent.blobValue = 2;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                // Only hash2 and hash3 should exist (referenced once each)
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(2, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(hash2));
                Assert.IsTrue(blobAssetStore.Contains<int>(hash3));

                // Change back system blob so the hash3 has again 2 refs
                Undo.RecordObjects(new Object[]{systemComponent}, "Changed values for blobs");
                systemComponent.blobValue = 3;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                // Only hash3 should exist (referenced twice)
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(1, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(hash3));

                // Change both to the same blob together from the same blob
                Undo.RecordObjects(new Object[]{bakerComponent, systemComponent}, "Changed values for blobs");
                bakerComponent.blobValue = 1;
                systemComponent.blobValue = 1;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                // Only hash1 should exist (referenced twice)
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(1, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(hash1));
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_AddAndUpdateBlobAssetRefCount_DefaultHash()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(AddBlobAssetWithDefaultHashBaker));
            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aComponent = a.AddComponent<BlobAssetAddTestAuthoring>();
                aComponent.blobValue = 5;

                var b = new GameObject("B");
                var bComponent = b.AddComponent<BlobAssetAddTestAuthoring>();
                bComponent.blobValue = 5;

                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
                SceneManager.MoveGameObjectToScene(b, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var blobAssetStore = bakingSystem.BlobAssetStore;
                Assert.IsNotNull(blobAssetStore);

                // We test that the identical Blob Assets are not put in memory twice, but only once with two entities referencing the same Blob Asset
                var testBlobAssetQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<BlobAssetReference>());
                Assert.AreEqual(2, testBlobAssetQuery.CalculateEntityCount(), "The Blob Asset Reference Component was not added correctly");

                {
                    var entityArray = testBlobAssetQuery.ToEntityArray(Allocator.Persistent);
                    var componentArray = new NativeArray<BlobAssetReference>(entityArray.Length, Allocator.Temp)
                    {
                        [0] = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[0]),
                        [1] = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[1])
                    };
                    entityArray.Dispose();

                    Assert.AreEqual(componentArray[0].blobHash, componentArray[1].blobHash, "The references do not point to the same Blob Asset");
                    Assert.AreEqual(componentArray[0].blobValue, componentArray[1].blobValue, "The references do not point to the same Blob Asset");
                    Assert.AreEqual(componentArray[0].blobReference, componentArray[1].blobReference, "The references do not point to the same Blob Asset");
                    Assert.DoesNotThrow(() => _ = componentArray[0].blobReference.Value, "The Blob Asset Reference is invalid");

                    // Only one blob should exist (referenced twice)
                    blobAssetStore.GarbageCollection(w.EntityManager);
                    Assert.AreEqual(1, blobAssetStore.BlobAssetCount);
                    Assert.AreEqual(componentArray[0].blobHash, componentArray[1].blobHash);
                    Assert.IsTrue(blobAssetStore.Contains<int>(componentArray[0].blobHash));

                    // If entity A no longer uses the Blob Asset the Blob Asset should still exist for entity B
                    // In addition, a new Blob Asset has to be added for entity A
                    var a = GameObject.Find("A");
                    var authoringA = a.GetComponent<BlobAssetAddTestAuthoring>();

                    Undo.RecordObject(authoringA, "change gameobject A's Blob Asset");
                    authoringA.blobValue = 1;
                    Undo.FlushUndoRecordObjects();
                }

                yield return UpdateEditorAndWorld(w);

                {
                    // The two blobs should exist (each referenced once)
                    blobAssetStore.GarbageCollection(w.EntityManager);
                    Assert.AreEqual(2, blobAssetStore.BlobAssetCount);
                    Assert.AreEqual(2, testBlobAssetQuery.CalculateEntityCount(), "The Blob Asset Reference Component was not added correctly");

                    var entityArray = testBlobAssetQuery.ToEntityArray(Allocator.Persistent);
                    var componentArray = new NativeArray<BlobAssetReference>(entityArray.Length, Allocator.Temp)
                    {
                        [0] = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[0]),
                        [1] = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[1])
                    };
                    entityArray.Dispose();

                    Assert.IsTrue(blobAssetStore.Contains<int>(componentArray[0].blobHash));
                    Assert.IsTrue(blobAssetStore.Contains<int>(componentArray[1].blobHash));

                    Assert.AreNotEqual(componentArray[0].blobHash, componentArray[1].blobHash, "The references still point to the same Blob Asset");
                    Assert.AreNotEqual(componentArray[0].blobValue, componentArray[1].blobValue, "The references still point to the same Blob Asset");
                    Assert.AreNotEqual(componentArray[0].blobReference, componentArray[1].blobReference, "The references still point to the same Blob Asset");
                    Assert.DoesNotThrow(() => _ = componentArray[0].blobReference.Value, "The original Blob Asset Reference is invalid");
                    Assert.DoesNotThrow(() => _ = componentArray[1].blobReference.Value, "The new Blob Asset Reference is invalid");

                    // If none of the entities uses the old Blob Asset, the old Blob Asset refCount should be removed from the BlobAssetStore
                    var b = GameObject.Find("B");
                    var authoringB = b.GetComponent<BlobAssetAddTestAuthoring>();

                    Undo.RecordObject(authoringB, "change gameobject B's Blob Asset");
                    authoringB.blobValue = 1;
                    Undo.FlushUndoRecordObjects();
                }

                yield return UpdateEditorAndWorld(w);

                {
                    // Only one blob should exist (referenced twice)
                    blobAssetStore.GarbageCollection(w.EntityManager);
                    Assert.AreEqual(1, blobAssetStore.BlobAssetCount);

                    Assert.AreEqual(2, testBlobAssetQuery.CalculateEntityCount(), "The Blob Asset Reference Component was not added correctly");

                    var entityArray = testBlobAssetQuery.ToEntityArray(Allocator.Persistent);
                    var componentArray = new NativeArray<BlobAssetReference>(entityArray.Length, Allocator.Temp)
                    {
                        [0] = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[0]),
                        [1] = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[1])
                    };
                    entityArray.Dispose();

                    Assert.IsTrue(blobAssetStore.Contains<int>(componentArray[0].blobHash));

                    Assert.AreEqual(componentArray[0].blobHash, componentArray[1].blobHash, "The references do not point to the same Blob Asset");
                    Assert.AreEqual(componentArray[0].blobValue, componentArray[1].blobValue, "The references do not point to the same Blob Asset");
                    Assert.AreEqual(componentArray[0].blobReference, componentArray[1].blobReference, "The references do not point to the same Blob Asset");
                    Assert.DoesNotThrow(() => _ = componentArray[0].blobReference.Value, "The Blob Asset Reference is invalid");
                }

                testBlobAssetQuery.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_AddAndUpdateBlobAssetRefCount_CustomHash()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(AddBlobAssetWithCustomHashBaker));
            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aComponent = a.AddComponent<BlobAssetAddTestAuthoring>();
                aComponent.blobValue = 5;

                var b = new GameObject("B");
                var bComponent = b.AddComponent<BlobAssetAddTestAuthoring>();
                bComponent.blobValue = 5;

                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
                SceneManager.MoveGameObjectToScene(b, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var blobAssetStore = bakingSystem.BlobAssetStore;
                Assert.IsNotNull(blobAssetStore);

                var hash5 = CalculateBlobHash(5);
                var hash1 = CalculateBlobHash(1);

                // We test that the identical Blob Assets are not put in memory twice, but only once with two entities referencing the same Blob Asset
                var testBlobAssetQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<BlobAssetReference>());

                Assert.AreEqual(2, testBlobAssetQuery.CalculateEntityCount(), "The Blob Asset Reference Component was not added correctly");

                var entityArray = testBlobAssetQuery.ToEntityArray(Allocator.Persistent);
                var componentArray = new NativeArray<BlobAssetReference>(entityArray.Length, Allocator.Persistent)
                {
                    [0] = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[0]),
                    [1] = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[1])
                };
                entityArray.Dispose();

                Assert.AreEqual(componentArray[0].blobHash, componentArray[1].blobHash, "The references do not point to the same Blob Asset");
                Assert.AreEqual(componentArray[0].blobValue, componentArray[1].blobValue, "The references do not point to the same Blob Asset");
                Assert.AreEqual(componentArray[0].blobReference, componentArray[1].blobReference, "The references do not point to the same Blob Asset");
                Assert.DoesNotThrow(() => _ = componentArray[0].blobReference.Value, "The Blob Asset Reference is invalid");

                Assert.AreEqual(hash5, componentArray[0].blobHash);

                // Only one blob should exist (referenced twice)
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(1, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(hash5));

                // If entity A no longer uses the Blob Asset the Blob Asset should still exist for entity B
                // In addition, a new Blob Asset has to be added for entity A
                var a = GameObject.Find("A");
                var authoringA = a.GetComponent<BlobAssetAddTestAuthoring>();

                Undo.RecordObject(authoringA, "change gameobject A's Blob Asset");
                authoringA.blobValue = 1;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(2, testBlobAssetQuery.CalculateEntityCount(), "The Blob Asset Reference Component was not added correctly");

                entityArray = testBlobAssetQuery.ToEntityArray(Allocator.Persistent);
                componentArray = new NativeArray<BlobAssetReference>(entityArray.Length, Allocator.Persistent)
                {
                    [0] = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[0]),
                    [1] = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[1])
                };
                entityArray.Dispose();

                Assert.AreNotEqual(componentArray[0].blobHash, componentArray[1].blobHash, "The references still point to the same Blob Asset");
                Assert.AreNotEqual(componentArray[0].blobValue, componentArray[1].blobValue, "The references still point to the same Blob Asset");
                Assert.AreNotEqual(componentArray[0].blobReference, componentArray[1].blobReference, "The references still point to the same Blob Asset");
                Assert.DoesNotThrow(() => _ = componentArray[0].blobReference.Value, "The original Blob Asset Reference is invalid");
                Assert.DoesNotThrow(() => _ = componentArray[1].blobReference.Value, "The new Blob Asset Reference is invalid");

                Assert.AreEqual(hash5, componentArray[0].blobHash);
                Assert.AreEqual(hash1, componentArray[1].blobHash);

                // Only two blobs should exist (referenced once each)
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(2, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(hash1));
                Assert.IsTrue(blobAssetStore.Contains<int>(hash5));

                // If none of the entities uses the old Blob Asset, the old Blob Asset refCount should be removed from the BlobAssetStore
                var b = GameObject.Find("B");
                var authoringB = b.GetComponent<BlobAssetAddTestAuthoring>();

                Undo.RecordObject(authoringB, "change gameobject B's Blob Asset");
                authoringB.blobValue = 1;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                Assert.AreEqual(2, testBlobAssetQuery.CalculateEntityCount(), "The Blob Asset Reference Component was not added correctly");

                entityArray = testBlobAssetQuery.ToEntityArray(Allocator.Temp);
                componentArray = new NativeArray<BlobAssetReference>(entityArray.Length, Allocator.Temp)
                {
                    [0] = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[0]),
                    [1] = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[1])
                };
                entityArray.Dispose();

                Assert.AreEqual(componentArray[0].blobHash, componentArray[1].blobHash, "The references do not point to the same Blob Asset");
                Assert.AreEqual(componentArray[0].blobValue, componentArray[1].blobValue, "The references do not point to the same Blob Asset");
                Assert.AreEqual(componentArray[0].blobReference, componentArray[1].blobReference, "The references do not point to the same Blob Asset");
                Assert.DoesNotThrow(() => _ = componentArray[0].blobReference.Value, "The Blob Asset Reference is invalid");

                Assert.AreEqual(hash1, componentArray[0].blobHash);

                // Only one blob should exist (referenced twice)
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(1, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(hash1));

                testBlobAssetQuery.Dispose();
            }
        }


        [UnityTest]
        public IEnumerator IncrementalBaking_AddAndUpdateBlobAssetRefCount_TryGetWithCustomHash()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(GetBlobAssetWithCustomHashBaker));
            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aComponent = a.AddComponent<BlobAssetAddTestAuthoring>();
                aComponent.blobValue = 5;

                var b = new GameObject("B");
                var bComponent = b.AddComponent<BlobAssetAddTestAuthoring>();
                bComponent.blobValue = 5;

                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
                SceneManager.MoveGameObjectToScene(b, subScene.EditingScene);
            }


            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var blobAssetStore = bakingSystem.BlobAssetStore;
                Assert.IsNotNull(blobAssetStore);

                // We test that the identical Blob Assets are not put in memory twice, but only once with two entities referencing the same Blob Asset
                var testBlobAssetQueryAdd = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<BlobAssetReference>());
                var testBlobAssetQueryGet = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<BlobAssetGetReference>());

                Assert.AreEqual(1, testBlobAssetQueryAdd.CalculateEntityCount(), "The Blob Asset Add Reference Component was not added correctly");
                Assert.AreEqual(1, testBlobAssetQueryGet.CalculateEntityCount(), "The Blob Asset Get Reference Component was not added correctly");

                var entityArrayAdd = testBlobAssetQueryAdd.ToEntityArray(Allocator.Persistent);
                var componentArrayAdd = w.EntityManager.GetComponentData<BlobAssetReference>(entityArrayAdd[0]);
                var entityArrayGet = testBlobAssetQueryGet.ToEntityArray(Allocator.Persistent);
                var componentArrayGet = w.EntityManager.GetComponentData<BlobAssetGetReference>(entityArrayGet[0]);

                Assert.AreEqual(componentArrayAdd.blobHash, componentArrayGet.blobHash, "The references do not point to the same Blob Asset");
                Assert.AreEqual(componentArrayAdd.blobValue, componentArrayGet.blobValue, "The references do not point to the same Blob Asset");
                Assert.AreEqual(componentArrayAdd.blobReference, componentArrayGet.blobReference, "The references do not point to the same Blob Asset");
                Assert.DoesNotThrow(() => _ = componentArrayAdd.blobReference.Value, "The Blob Asset Reference is invalid");

                // Only one blob should exist (referenced twice)
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(1, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(componentArrayAdd.blobHash));

                entityArrayAdd.Dispose();
                entityArrayGet.Dispose();

                testBlobAssetQueryAdd.Dispose();
                testBlobAssetQueryGet.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_MultipleTryGetRefcountingRemoval()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(MultipleTryGetRefCountingBaker));
            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aComponent = a.AddComponent<BlobAssetAddTestAuthoring>();
                aComponent.blobValue = 5;

                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var blobAssetStore = bakingSystem.BlobAssetStore;
                Assert.IsNotNull(blobAssetStore);

                // We test that if one Baker calls TryGet 5 times,
                // it only increases the refcount by 1 in both the Baker and BlobAssetStore
                var testBlobAssetQuery =
                    w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<BlobAssetGetReference>());

                Assert.AreEqual(1, testBlobAssetQuery.CalculateEntityCount(),
                    "The Blob Asset Reference Component was not added correctly");

                var entityArray = testBlobAssetQuery.ToEntityArray(Allocator.Temp);
                var component = w.EntityManager.GetComponentData<BlobAssetGetReference>(entityArray[0]);

                // Only one blob should exist (referenced once)
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(1, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(component.blobHash));

                var blobHash = component.blobHash;

                // Remove the authoring and check that the BlobAssetStore refcounting is correctly updated
                var a = GameObject.Find("A");
                var authoringA = a.GetComponent<BlobAssetAddTestAuthoring>();

                Undo.RecordObject(authoringA, "Delete gameobject A's Blob Asset");
                Undo.DestroyObjectImmediate(authoringA);
                Undo.FlushUndoRecordObjects();

                // Three are necessary to circumvent FramesToRetainBlobAssets
                yield return UpdateEditorAndWorld(w);
                yield return UpdateEditorAndWorld(w);
                yield return UpdateEditorAndWorld(w);

                // Check that the refcount for the BlobAsset is 0 (removed as many as added)
                Assert.IsFalse(blobAssetStore.Contains<int>(blobHash),
                    "The BlobAsset refCount for creation and removal are not equal.");
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_BlobAssetsRemoveFromBaker_BlobAssetDisposedCorrectly()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(AddBlobAssetWithDefaultHashBaker));
            SubScene subScene;
            {
                subScene = CreateEmptySubScene("TestSubScene", true);

                var a = new GameObject("A");
                var aComponent = a.AddComponent<BlobAssetAddTestAuthoring>();
                aComponent.blobValue = 5;

                SceneManager.MoveGameObjectToScene(a, subScene.EditingScene);
            }

            yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                var bakingSystem = GetBakingSystem(w, subScene.SceneGUID);
                Assert.IsNotNull(bakingSystem);

                var blobAssetStore = bakingSystem.BlobAssetStore;
                Assert.IsNotNull(blobAssetStore);

                // We test that the identical Blob Assets are not put in memory twice, but only once with two entities referencing the same Blob Asset
                var testBlobAssetQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<BlobAssetReference>());

                Assert.AreEqual(1, testBlobAssetQuery.CalculateEntityCount(), "The Blob Asset Reference Component was not added correctly");

                var entityArray = testBlobAssetQuery.ToEntityArray(Allocator.Temp);
                var component = w.EntityManager.GetComponentData<BlobAssetReference>(entityArray[0]);

                // Only one blob should exist (referenced once)
                blobAssetStore.GarbageCollection(w.EntityManager);
                Assert.AreEqual(1, blobAssetStore.BlobAssetCount);
                Assert.IsTrue(blobAssetStore.Contains<int>(component.blobHash));

                Assert.DoesNotThrow(() => _ = component.blobReference.Value, "The Blob Asset Reference is invalid");

                blobAssetStore.TryGet<int>(component.blobHash, out var bakerBlobAssetReference);
                var runtimeBlobAssetReference = component.blobReference;

                // If entity A no longer uses the Blob Asset the Blob Asset should still exist for entity B
                // In addition, a new Blob Asset has to be added for entity A
                var a = GameObject.Find("A");
                var authoringA = a.GetComponent<BlobAssetAddTestAuthoring>();

                Undo.RecordObject(authoringA, "Delete gameobject A's Blob Asset");
                Undo.DestroyObjectImmediate(authoringA);
                Undo.FlushUndoRecordObjects();

                // Three are necessary to circumvent FramesToRetainBlobAssets
                yield return UpdateEditorAndWorld(w);
                yield return UpdateEditorAndWorld(w);
                yield return UpdateEditorAndWorld(w);

                // Check that the baker and runtime Blob Assets are properly disposed
                Assert.Throws<InvalidOperationException>(() => bakerBlobAssetReference.m_data.ValidateNotNull(), "The original Baker Blob Asset Reference is still valid");
                Assert.Throws<InvalidOperationException>(() => runtimeBlobAssetReference.m_data.ValidateNotNull(), "The original Runtime Blob Asset Reference is still valid");
            }
        }

        [UnityTest]
        public IEnumerator IncrementalBaking_SubSceneWithLongName_BakesAndLoads()
        {
            string sceneName = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            SubScene subScene = CreateEmptySubScene(sceneName, true);

            var go = new GameObject("GO");
            var aAuthoring = go.AddComponent<SceneSectionValueAuthoring>();
            aAuthoring.Value = 1;
            SceneManager.MoveGameObjectToScene(go, subScene.EditingScene);

                yield return GetEnterPlayMode(TestWithEditorLiveConversion.Mode.Edit);
            {
                var w = GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);

                yield return UpdateEditorAndWorld(w);

                // Verify it baked first
                var testQuery = w.EntityManager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<SceneSection>().WithOptions(EntityQueryOptions.IncludePrefab));
                {
                    Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted");
                    var prefabEntity = testQuery.GetSingletonEntity();
                    var sectionComponent = w.EntityManager.GetComponentData<SceneSectionValue>(prefabEntity);
                    Assert.AreEqual(1, sectionComponent.Value);
                }

                // Remove reference to prefab and rebake
                Undo.RecordObject(aAuthoring, "Change value");
                aAuthoring.Value = 2;
                Undo.FlushUndoRecordObjects();

                yield return UpdateEditorAndWorld(w);

                {
                    Assert.AreEqual(1, testQuery.CalculateEntityCount(), "Expected a game object to be converted after rebake");
                    var prefabEntity = testQuery.GetSingletonEntity();
                    var sectionComponent = w.EntityManager.GetComponentData<SceneSectionValue>(prefabEntity);
                    Assert.AreEqual(2, sectionComponent.Value);
                }

            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// This test ensures that when a component is added by a baker it survives when a dependency that added it in a previous iteration is reverted.
        /// </summary>
        [UnityTest]
        [Description("Test that the baker revert function does not interfere with the components added by bakers. Jira: DOTS-7377")]
        public IEnumerator IncrementalBaking_RevertComponent_DoesNotInterfereWith_AddComponent([Values]Mode mode)
        {
            SubScene subScene = CreateEmptySubScene("TestSubScene", true);

            var go = new GameObject("Root", new []{ typeof(MockColliderComponent), typeof(MockBodyComponent) });

            SceneManager.MoveGameObjectToScene(go, subScene.EditingScene);

            yield return GetEnterPlayMode(mode);
            {
                // This test requires at least two cycles of toggling the active state of the body component
                var enableComponent = go.GetComponent<MockBodyComponent>();

                var w = GetLiveConversionWorld(mode);
                yield return UpdateEditorAndWorld(w);

                var qb = new EntityQueryBuilder(Allocator.Temp);
                var requiredComponentQuery = qb.WithAll<RequiredComponent>().Build(w.EntityManager);
                qb.Reset();
                var mockColliderQuery = qb.WithAll<MockCollider>().Build(w.EntityManager);
                qb.Reset();
                var mockBodyQuery = qb.WithAll<MockBody>().Build(w.EntityManager);

                Assert.IsFalse(mockColliderQuery.IsEmpty, "No MockCollider component found");
                Assert.IsFalse(mockBodyQuery.IsEmpty, "No MockBody component found");
                Assert.IsFalse(requiredComponentQuery.IsEmpty, "No RequiredComponent component found");
                Assert.AreEqual(nameof(MockBodyComponent), requiredComponentQuery.GetSingleton<RequiredComponent>().AddedBy);

                Undo.RecordObject(enableComponent, "Changing enable to false");
                enableComponent.enabled = false;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                Assert.IsFalse(mockColliderQuery.IsEmpty, "No MockCollider component found");
                Assert.IsTrue(mockBodyQuery.IsEmpty, "MockBody component found but was supposed to be reverted");
                Assert.IsFalse(requiredComponentQuery.IsEmpty, "No RequiredComponent component found");
                Assert.AreEqual(nameof(MockColliderComponent), requiredComponentQuery.GetSingleton<RequiredComponent>().AddedBy);

                Undo.RecordObject(enableComponent, "Changing enable to true");
                enableComponent.enabled = true;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                Assert.IsFalse(mockColliderQuery.IsEmpty, "No MockCollider component found");
                Assert.IsFalse(mockBodyQuery.IsEmpty, "No MockBody component found");
                Assert.IsFalse(requiredComponentQuery.IsEmpty, "No RequiredComponent component found");
                Assert.AreEqual(nameof(MockBodyComponent), requiredComponentQuery.GetSingleton<RequiredComponent>().AddedBy);

                Undo.RecordObject(enableComponent, "Changing enable to false");
                enableComponent.enabled = false;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                Assert.IsFalse(mockColliderQuery.IsEmpty, "No MockCollider component found");
                Assert.IsTrue(mockBodyQuery.IsEmpty, "MockBody component found but was supposed to be reverted");
                Assert.IsFalse(requiredComponentQuery.IsEmpty, "No RequiredComponent component found");
                Assert.AreEqual(nameof(MockColliderComponent), requiredComponentQuery.GetSingleton<RequiredComponent>().AddedBy);

                Undo.RecordObject(enableComponent, "Changing enable to true");
                enableComponent.enabled = true;
                Undo.FlushUndoRecordObjects();
                yield return UpdateEditorAndWorld(w);

                Assert.IsFalse(mockColliderQuery.IsEmpty, "No MockCollider component found");
                Assert.IsFalse(mockBodyQuery.IsEmpty, "No MockBody component found");
                Assert.IsFalse(requiredComponentQuery.IsEmpty, "No RequiredComponent component found");
                Assert.AreEqual(nameof(MockBodyComponent), requiredComponentQuery.GetSingleton<RequiredComponent>().AddedBy);
            }
        }

        [UnityTest]
        public IEnumerator LiveConversion_TestSectionCrossReferences()
        {
            GameObject go0 = null;
            {
                CreateSubSceneFromObjects("TestSubScene", false, () =>
                {
                    go0 = new GameObject("Section0");
                    SetTransformUsageFlags(go0, TransformUsageFlags.Dynamic);
                    // Just adding this component to identify the root entity
                    go0.AddComponent<TestComponentAuthoring>();

                    // Creating an object in section 1 with a parent reference to object in section 0. It shoudl be valid.
                    var go1 = new GameObject("Section1");
                    SetTransformUsageFlags(go1, TransformUsageFlags.Renderable);
                    go1.transform.SetParent(go0.transform);
                    var sectionComponent = go1.AddComponent<SceneSectionComponent>();
                    sectionComponent.SectionIndex = 1;

                    // Adding a null reference to check that Entity.Null doesn't get lost during serialisation.
                    var entityReference = go1.AddComponent<EntityRefTestDataAuthoring>();
                    entityReference.Value = null;
                    entityReference.AdditionalEntityCount = 0;
                    entityReference.DeclareLinkedEntityGroup = false;

                    // Creating an object in section 2 with a parent reference to object in section 1. It should be null.
                    var go2 = new GameObject("Section2");
                    SetTransformUsageFlags(go2, TransformUsageFlags.Renderable);
                    go2.transform.SetParent(go1.transform);
                    sectionComponent = go2.AddComponent<SceneSectionComponent>();
                    sectionComponent.SectionIndex = 2;

                    return new List<GameObject> {go0};
                });
            }

            yield return GetEnterPlayMode(Mode.Play);

            {
                var w = GetLiveConversionWorld(Mode.Play);
                var manager = w.EntityManager;

                var sceneSystem = w.GetExistingSystem<SceneSystem>();
                var subScene = Object.FindFirstObjectByType<SubScene>();

                var sceneEntity = SceneSystem.GetSceneEntity(w.Unmanaged, subScene.SceneGUID);
                Assert.AreNotEqual(Entity.Null, sceneEntity);

                int timeout = 1000;
                while (SceneSystem.GetSceneStreamingState(w.Unmanaged, sceneEntity) == SceneSystem.SceneStreamingState.Loading)
                {
                    yield return UpdateEditorAndWorld(w);
                    --timeout;
                    if (timeout <= 0)
                    {
                        UnityEngine.Debug.LogError("Scene not loaded sucessfully");
                        break;
                    }
                }

                Assert.AreEqual(SceneSystem.SceneStreamingState.LoadedSuccessfully, SceneSystem.GetSceneStreamingState(w.Unmanaged, sceneEntity));

                var rootQuery = w.EntityManager.CreateEntityQuery(
                   typeof(TestComponentAuthoring.UnmanagedTestComponent));

               var rootArray = rootQuery.ToEntityArray(Allocator.Temp);
               Assert.AreEqual(1, rootArray.Length);
               var rootEntity = rootArray[0];

               var sceneQuery = w.EntityManager.CreateEntityQuery(
                    typeof(SceneSection),
                    typeof(Parent));

                var entities = sceneQuery.ToEntityArray(Allocator.Temp);
                var parents = sceneQuery.ToComponentDataArray<Parent>(Allocator.Temp);

                bool foundSection1Entity = false;
                bool foundSection2Entity = false;

                for (int index = 0; index < entities.Length; ++index)
                {
                    var section = manager.GetSharedComponent<SceneSection>(entities[index]).Section;
                    if (section == 1)
                    {
                        // Check that the parent is not Entity.Null and it's the right value
                        Assert.AreNotEqual(Entity.Null, parents[index].Value);
                        Assert.AreEqual(rootEntity, parents[index].Value);
                        foundSection1Entity = true;

                        // Check that the null reference is still null and hasn't got a wrong value assigned
                        var entityRef = manager.GetComponentData<EntityRefTestData>(entities[index]);
                        Assert.AreEqual(Entity.Null, entityRef.Value);
                    }
                    else if (section == 2)
                    {
                        // Check that the parent is Entity.Null
                        Assert.AreEqual(Entity.Null, parents[index].Value);
                        foundSection2Entity = true;
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"Entity with unexpected section {section}");
                    }
                }

                // Check that entities in section 1 and 2 were found
                Assert.AreEqual(true, foundSection1Entity);
                Assert.AreEqual(true, foundSection2Entity);

                sceneQuery.Dispose();
                rootQuery.Dispose();
                entities.Dispose();
                parents.Dispose();
                rootArray.Dispose();

                yield return UpdateEditorAndWorld(w);
            }
        }
    }
}
