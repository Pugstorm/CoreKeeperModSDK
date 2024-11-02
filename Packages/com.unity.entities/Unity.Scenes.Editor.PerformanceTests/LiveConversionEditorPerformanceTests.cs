using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Conversion;
using Unity.Entities.Tests;
using Unity.Entities.Tests.Conversion;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Scenes.Editor.Tests
{
    [TestFixture]
    class LiveBakingEditorPerformanceTests : LiveBakingAndConversionEditorPerformanceTestsBase
    {
        private LiveConversionSettings.ConversionMode _previousLiveConversionSettings;

        [OneTimeSetUp]
        public new void OneTimeSetUp()
        {
            base.m_Test.IsBakingEnabled = true;
            base.OneTimeSetUp();
        }

        [OneTimeTearDown]
        public new void OneTimeTearDown()
        {
            base.OneTimeTearDown();
        }

        [SetUp]
        public new void SetUp()
        {
            base.SetUp();
            _previousLiveConversionSettings = LiveConversionSettings.Mode;
        }

        [TearDown]
        public new void TearDown()
        {
            base.TearDown();
            LiveConversionSettings.Mode = _previousLiveConversionSettings;
        }

        public enum ChunkDistributionTest
        {
            OneEntityPerChunk,
            PackedChunks
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_TemporaryBakingType([Values(100, 1000, 10000)]int numObjects, [Values]ChunkDistributionTest distribution)
        {
            LiveConversionSettings.Mode = LiveConversionSettings.ConversionMode.AlwaysCleanConvert;

            var objList = CreateObjectSoupSubSceneObjects(numObjects, typeof(TempBakingPerformanceAuthoring));
            if (distribution == ChunkDistributionTest.OneEntityPerChunk)
            {
                for (int index = 0; index < objList.Count; ++index)
                {
                    var go = objList[index];
                    var component = go.GetComponent<TempBakingPerformanceAuthoring>();
                    component.Field = index;
                }
            }

            var objectArray = objList.ToArray();

            using var blobAssetStore = new BlobAssetStore(128);

            var bakingSettings = new BakingSettings
            {
                BakingFlags = BakingUtility.BakingFlags.AssignName | BakingUtility.BakingFlags.AddEntityGUID,
                BlobAssetStore = blobAssetStore
            };

            SampleGroup sampleGroup = new SampleGroup(nameof(EditorSubSceneLiveConversionSystem));
            using var world = new World("TestWorld");

            BakingStripSystem strippingSystem = world.GetOrCreateSystemManaged<BakingStripSystem>();
            var strippingSystemProfileMarkerName = strippingSystem.GetProfilerMarkerName();

            BakingUtility.BakeGameObjects(world, objectArray, bakingSettings);

            for (int i = 0; i < MaxIterations; i++)
            {
                using (Measure.ProfilerMarkers(strippingSystemProfileMarkerName))
                {
                    BakingUtility.BakeGameObjects(world, objectArray, bakingSettings);
                }
            }
            yield break;
        }
    }

    [Serializable]
    abstract class LiveBakingAndConversionEditorPerformanceTestsBase
    {
        [SerializeField] protected TestWithEditorLiveConversion m_Test;
        [SerializeField] protected TestWithObjects m_Objects;
        protected const int MaxIterations = 30;

        public void OneTimeSetUp()
        {
            m_Test.OneTimeSetUp();
            LiveConversionSettings.AdditionalConversionSystems.Clear();
            LiveConversionSettings.Mode = LiveConversionSettings.ConversionMode.IncrementalConversion;
            LiveConversionSettings.EnableInternalDebugValidation = false;
        }

        public void OneTimeTearDown()
        {
            m_Test.OneTimeTearDown();
            LiveConversionSettings.AdditionalConversionSystems.Clear();
        }

        public void SetUp()
        {
            m_Test.SetUp();
            m_Objects.SetUp();
            MeasureLiveConversionTime.IsFirst = true;
        }

        public void TearDown()
        {
            m_Objects.TearDown();
        }

        protected static string AssetPath(string name) => "Packages/com.unity.entities/Unity.Scenes.Editor.PerformanceTests/Assets/" + name;

        public enum ObjectKind
        {
            Empty, Sphere
#if ENABLE_ASPECTS
            ,
            Tagged
#endif
        }

        protected GameObject CreateGameObject(ObjectKind objectKind)
        {
            switch (objectKind)
            {
                case ObjectKind.Empty:
                    return m_Objects.CreateGameObject();
                case ObjectKind.Sphere:
                    return m_Objects.CreatePrimitive(PrimitiveType.Sphere);
#if ENABLE_ASPECTS
                case ObjectKind.Tagged:
                    return m_Objects.CreateGameObject("", typeof(TestMonoBehaviour));
#endif
            }
            return null;
        }

        protected GameObject CreateGameObject(Type component)
        {
            return m_Objects.CreateGameObject(component);
        }

        protected GameObject LoadPrefab(ObjectKind objectKind)
        {
            switch (objectKind)
            {
                case ObjectKind.Empty:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath("Empty.prefab"));
                case ObjectKind.Sphere:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath("Sphere.prefab"));
#if ENABLE_ASPECTS
                case ObjectKind.Tagged:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath("Tagged.prefab"));
#endif
            }

            return null;
        }

        protected GameObject CreateHierarchy(int depth, int branching, ObjectKind kind, List<GameObject> output = null)
        {
            return CreateSubHierarchy(depth, branching, null, output);
            GameObject CreateSubHierarchy(int depth, int branching, Transform parent, List<GameObject> output)
            {
                var go = CreateGameObject(kind);
                var t = go.transform;
                if (parent != null)
                    t.SetParent(parent);
                output?.Add(go);
                if (depth > 0)
                {
                    for (int i = 0; i < branching; i++)
                        CreateSubHierarchy(depth - 1, branching, t, output);
                }
                return go;
            }
        }

        protected List<GameObject> CreateObjectSoupSubSceneObjects(int numObjects, ObjectKind kind)
        {
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            var objects = new List<GameObject>();
            for (int i = 0; i < numObjects; i++)
            {
                var obj = CreateGameObject(kind);
                objects.Add(obj);
                SceneManager.MoveGameObjectToScene(obj, subScene.EditingScene);
            }

            var target = CreateGameObject(kind);
            SceneManager.MoveGameObjectToScene(target, subScene.EditingScene);
            objects.Add(target);
            return objects;
        }

        protected List<GameObject> CreateObjectSoupSubSceneObjects(int numObjects, Type component)
        {
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            var objects = new List<GameObject>();
            for (int i = 0; i < numObjects; i++)
            {
                var obj = CreateGameObject(component);
                objects.Add(obj);
                SceneManager.MoveGameObjectToScene(obj, subScene.EditingScene);
            }

            var target = CreateGameObject(component);
            SceneManager.MoveGameObjectToScene(target, subScene.EditingScene);
            objects.Add(target);
            return objects;
        }

        protected GameObject CreateObjectSoupSubScene(int numObjects, ObjectKind kind, SubScene subScene)
        {
            for (int i = 0; i < numObjects; i++)
                SceneManager.MoveGameObjectToScene(CreateGameObject(kind), subScene.EditingScene);
            var target = CreateGameObject(kind);
            SceneManager.MoveGameObjectToScene(target, subScene.EditingScene);
            return target;
        }

        // This machinery for running performance tests is necessary for two reasons:
        // (a) as of writing, the performance test package doesn't support capturing profiler marker in editor tests
        // (b) running a performance test like this only once means that it potentially triggers JIT compilation. I've
        //     tried this many times now and it consistently increases runtimes by 10x-50x, so that means that it will
        //     be unstable based on the order that tests run in.
        protected struct MeasureLiveConversionTime
        {
            internal static bool IsFirst;
            private readonly EditorSubSceneLiveConversionSystem m_System;
            private readonly SampleGroup m_SampleGroup;
            public MeasureLiveConversionTime(World w)
            {
                m_System = w.GetExistingSystemManaged<EditorSubSceneLiveConversionSystem>();
                m_System.MillisecondsTakenByUpdate = 0;
                m_SampleGroup = new SampleGroup(nameof(EditorSubSceneLiveConversionSystem));
            }

            public void Commit()
            {
                // intentionally skip the first sample
                if (!IsFirst)
                    Measure.Custom(m_SampleGroup, m_System.MillisecondsTakenByUpdate);
                IsFirst = false;
                m_System.MillisecondsTakenByUpdate = 0;
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_MoveHierarchyRoot([Values(1, 5, 10)]int depth, [Values] ObjectKind kind)
        {
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            var root = CreateHierarchy(depth, 2, kind);
            SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);

            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                for (int i = 0; i < MaxIterations; i++)
                {
                    Undo.RecordObject(root.transform, "Change Transform");
                    root.transform.position += Vector3.one;
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_MoveHierarchyChildren([Values(1, 5, 10)]int depth, [Values] ObjectKind kind)
        {
            var objects = new List<GameObject>();
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            var root = CreateHierarchy(depth, 2, kind, objects);
            SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);

            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                int n = MaxIterations;
                if (n > objects.Count)
                    n = objects.Count;
                for (int i = 0; i < n; i++)
                {
                    var p = objects[objects.Count - i - 1];
                    Undo.RecordObject(p.transform, "Change Transform");
                    p.transform.position += Vector3.one;
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_MoveOneOfMany([Values(1, 10, 100, 1000)]int numObjects, [Values] ObjectKind kind)
        {
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            GameObject target = CreateObjectSoupSubScene(numObjects, kind, subScene);
            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                for (int i = 0; i < MaxIterations; i++)
                {
                    Undo.RecordObject(target.transform, "Change Transform");
                    target.transform.position += Vector3.one;
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_EnableDisableHierarchyRoot([Values(1, 5, 8)]int depth, [Values] ObjectKind kind)
        {
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            var root = CreateHierarchy(depth, 2, kind);
            SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);

            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                for (int i = 0; i < MaxIterations; i++)
                {
                    Undo.RecordObject(root, "Change Root");
                    root.SetActive(!root.activeInHierarchy);
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_EnableDisableOneOfMany([Values(1, 10, 100, 1000)]int numObjects, [Values] ObjectKind kind)
        {
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            GameObject target = CreateObjectSoupSubScene(numObjects, kind, subScene);
            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                for (int i = 0; i < MaxIterations; i++)
                {
                    Undo.RecordObject(target, "Change Transform");
                    target.SetActive(!target.activeInHierarchy);
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_CreateOneAmongMany([Values(1, 10, 100, 1000)]int numObjects, [Values] ObjectKind kind)
        {
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            CreateObjectSoupSubScene(numObjects, kind, subScene);
            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                var scene = Object.FindFirstObjectByType<SubScene>().EditingScene;
                for (int i = 0; i < MaxIterations; i++)
                {
                    var go = CreateGameObject(kind);
                    SceneManager.MoveGameObjectToScene(go, scene);
                    Undo.RegisterCreatedObjectUndo(go, "Create one");
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_ReparentOneAmongMany([Values(100, 1000)]int numObjects, [Values] ObjectKind kind)
        {
            var list = CreateObjectSoupSubSceneObjects(numObjects, kind);
            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                for (int i = 0; i < MaxIterations; i++)
                {
                    Undo.SetTransformParent(list[i].transform, list[i + 1].transform, "Reparent");
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_ReparentHierarchy([Values(1, 5, 10)]int depth, [Values] ObjectKind kind)
        {
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            var root = CreateHierarchy(depth, 2, kind);
            SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);
            var target = CreateGameObject(kind);
            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                for (int i = 0; i < MaxIterations; i++)
                {
                    if (root.transform.parent == null)
                        Undo.SetTransformParent(root.transform, target.transform, "Reparent");
                    else
                        Undo.SetTransformParent(root.transform, null, "Reparent");
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_ReparentIntoHierarchy([Values(5, 10)]int depth, [Values] ObjectKind kind)
        {
            var objects = new List<GameObject>();
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            var root = CreateHierarchy(depth, 2, kind, objects);
            SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);
            var target = CreateGameObject(kind);
            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                for (int i = 0; i < MaxIterations; i++)
                {
                    if (i >= objects.Count)
                        break;
                    Undo.SetTransformParent(target.transform, objects[objects.Count - i - 1].transform, "reparent");
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_DeleteChildrenFromHierarchy([Values(5, 10)]int depth, [Values] ObjectKind kind)
        {
            var objects = new List<GameObject>();
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            var root = CreateHierarchy(depth, 2, kind, objects);
            SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);
            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);

            var measure = new MeasureLiveConversionTime(w);
            {
                for (int i = 0; i < MaxIterations; i++)
                {
                    if (i >= objects.Count)
                        break;
                    int last = objects.Count - 1;
                    Undo.DestroyObjectImmediate(objects[last]);
                    objects.RemoveAt(last);
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_DeleteHierarchyRoot([Values(5, 8)]int depth, [Values] ObjectKind kind)
        {
            var objects = new List<GameObject>();
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            for (int i = 0; i < 5; i++)
            {
                var root = CreateHierarchy(depth, 2, kind);
                objects.Add(root);
                SceneManager.MoveGameObjectToScene(root, subScene.EditingScene);
            }

            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                foreach (var root in objects)
                {
                    Undo.DestroyObjectImmediate(root);
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_DeleteFromMany([Values(100, 1000)]int numObjects, [Values] ObjectKind kind)
        {
            var objects = CreateObjectSoupSubSceneObjects(numObjects, kind);
            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                for (int i = 0; i < MaxIterations; i++)
                {
                    if (i >= objects.Count)
                        break;
                    int last = objects.Count - 1;
                    Undo.DestroyObjectImmediate(objects[last]);
                    objects.RemoveAt(last);
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveConversion_Performance_CreateHierarchy([Values(1, 5, 8)]int depth, [Values] ObjectKind kind)
        {
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                var scene = subScene.EditingScene;
                for (int i = 0; i < 5; i++)
                {
                    var root = CreateHierarchy(depth, 2, kind);
                    SceneManager.MoveGameObjectToScene(root, scene);
                    Undo.RegisterCreatedObjectUndo(root, "Create hierarchy");
                    Undo.FlushUndoRecordObjects();
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }

        [UnityTest, Performance, EmbeddedPackageOnlyTest]
        public IEnumerator LiveConversion_Performance_TouchOnePrefabAmongManyObjects([Values(100, 1000)]int numObjects, [Values] ObjectKind prefabKind)
        {
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            var kind = ObjectKind.Empty;
#if ENABLE_ASPECTS
            kind = ObjectKind.Tagged;
#endif
            CreateObjectSoupSubScene(numObjects, kind, subScene);
            var prefab = LoadPrefab(prefabKind);
            var go = Object.Instantiate(prefab);
            m_Objects.RegisterForDestruction(go);
            SceneManager.MoveGameObjectToScene(go, subScene.EditingScene);
            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);

            var measure = new MeasureLiveConversionTime(w);
            {
                for (int i = 0; i < 10; i++)
                {
                    prefab.isStatic = !prefab.isStatic;
                    PrefabUtility.SavePrefabAsset(prefab);
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }

            prefab.isStatic = false;
            PrefabUtility.SavePrefabAsset(prefab);
        }

        [UnityTest, Performance, EmbeddedPackageOnlyTest]
        public IEnumerator LiveConversion_Performance_UpdateManyPrefabs([Values(100)]int numObjects)
        {
            var subScene = m_Test.CreateEmptySubScene("TestSubScene", true);
            var prefab = LoadPrefab(ObjectKind.Sphere);
            for (int i = 0; i < numObjects; i++)
            {
                var go = Object.Instantiate(prefab);
                m_Objects.RegisterForDestruction(go);
                SceneManager.MoveGameObjectToScene(go, subScene.EditingScene);
            }

            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);
            var measure = new MeasureLiveConversionTime(w);
            {
                for (int i = 0; i < 10; i++)
                {
                    prefab.isStatic = !prefab.isStatic;
                    PrefabUtility.SavePrefabAsset(prefab);
                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }

            prefab.isStatic = false;
            PrefabUtility.SavePrefabAsset(prefab);
        }

        internal class AdditionalEntitiesAuthoring : MonoBehaviour
        {
            public int additionalEntitiesCount;
            public int measurementTrigger;

            class AdditionalEntitiesBaker : Baker<AdditionalEntitiesAuthoring>
            {
                public override void Bake(AdditionalEntitiesAuthoring authoring)
                {
                    float3 axisX = Vector3.right;

                    for (int i = 0; i < authoring.additionalEntitiesCount; ++i)
                    {
                        var entity = CreateAdditionalEntity(TransformUsageFlags.ManualOverride);

                        float3 localPosition = (axisX * i);
                        AddComponent(entity, LocalTransform.FromPosition(localPosition));
                        AddComponent<LocalToWorld>(entity);
                    }
                }
            }
        }

        [UnityTest, Performance]
        public IEnumerator LiveBaking_Performance_Baker_CreateAdditionalEntities([Values(100, 1000, 10000)]int numObjects)
        {
            LiveConversionSettings.Mode = LiveConversionSettings.ConversionMode.AlwaysCleanConvert;

            var subScene = m_Test.CreateEmptySubScene("AdditionalEntitiesTest", true);
            var go = CreateGameObject(typeof(AdditionalEntitiesAuthoring));
            m_Objects.RegisterForDestruction(go);

            var component = go.GetComponent<AdditionalEntitiesAuthoring>();
            component.additionalEntitiesCount = numObjects;

            SceneManager.MoveGameObjectToScene(go, subScene.EditingScene);
            var w = m_Test.GetLiveConversionWorld(TestWithEditorLiveConversion.Mode.Edit);
            yield return m_Test.UpdateEditorAndWorld(w);

            var measure = new MeasureLiveConversionTime(w);
            {
                for (int i = 0; i <= 20; i++)
                {
                    Undo.RecordObject(component, "Update Authoring Component");
                    component.measurementTrigger = i;
                    Undo.FlushUndoRecordObjects();

                    yield return m_Test.UpdateEditorAndWorld(w);
                    measure.Commit();
                }
            }
        }
    }
}
