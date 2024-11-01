using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.IO;
using Unity.Entities.Tests;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text.RegularExpressions;
using Unity.Entities.TestComponents;
using Unity.Scenes.Editor.Tests;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;
using Assert = UnityEngine.Assertions.Assert;
using BindingCache = Unity.Entities.Editor.BindingRegistryLiveProperties.BindingCache;
using Object = UnityEngine.Object;

namespace Unity.Entities.Editor.Tests
{
    class LivePropertiesTests
    {
        const string k_TestWorldName = "Live Properties Test World";
        string m_TempAssetDir;
        World m_DefaultWorld;
        World m_PreviousWorld;
        PlayerLoopSystem m_PreviousPlayerLoop;
        TestLiveConversionSettings m_Settings;

        static Type GetAuthoringComponentType<T>()
        {
            var typeName = $"Unity.Entities.Tests.{typeof(T).Name}Authoring, Unity.Entities.TestComponents";
            var authoringType = Type.GetType(typeName);
            Assert.IsNotNull(authoringType, $"Could not find generated authoring type for {typeof(T).Name}, looked for {typeName}.");
            return authoringType;
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            var guid = AssetDatabase.CreateFolder("Assets", nameof(LivePropertiesTests));
            m_TempAssetDir = AssetDatabase.GUIDToAssetPath(guid);

            m_Settings.Setup();

            m_PreviousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;

            DefaultWorldInitialization.Initialize(k_TestWorldName, true);
            m_DefaultWorld = World.DefaultGameObjectInjectionWorld;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_TempAssetDir))
            {
                Directory.Delete(m_TempAssetDir, true);
                File.Delete(m_TempAssetDir + ".meta");
            }

            m_DefaultWorld.Dispose();
            World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
            m_PreviousWorld = null;
            PlayerLoop.SetPlayerLoop(m_PreviousPlayerLoop);
            m_Settings.TearDown();
        }

        SubScene CreateSubScene(string subSceneName, string parentSceneName, InteractionMode interactionMode = InteractionMode.AutomatedAction, SubSceneContextMenu.NewSubSceneMode mode = SubSceneContextMenu.NewSubSceneMode.MoveSelectionToScene)
        {
            var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorSceneManager.SetActiveScene(mainScene);

            var path = Path.Combine(m_TempAssetDir, $"{parentSceneName}.unity");
            EditorSceneManager.SaveScene(mainScene, path);

            var go = new GameObject();
            go.name = subSceneName;
            Selection.activeGameObject = go;

            var args = new SubSceneContextMenu.NewSubSceneArgs
            {
                target = go,
                newSubSceneMode = mode
            };
            return SubSceneContextMenu.CreateNewSubScene(go.name, args, interactionMode);
        }

        protected IEnumerator RunLiveConversion()
        {
            LiveConversionConnection.GlobalDirtyLiveConversion();
            m_DefaultWorld.Update();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShouldUpdateLiveProperties_ManualConversion_Test()
        {
            var subscene = CreateSubScene("subscene1", "parentScene1");
            EditorSceneManager.SetActiveScene(subscene.EditingScene);

            var go = new GameObject("goo");
            var comp = go.AddComponent<ManualConversionTestAuthoring>();

            SceneManager.MoveGameObjectToScene(go, subscene.EditingScene);

            yield return RunLiveConversion();

            var authoringType = typeof(ManualConversionTestAuthoring);
            var cache = new BindingCache(authoringType);
            var objects = new Object[] {comp};
            Assert.IsFalse(cache.ShouldUpdateLiveProperties(objects));

            var world = World.DefaultGameObjectInjectionWorld;
            world.GetOrCreateSystemManaged<UpdateSingleLiveProperties>().Update();
            world.Update();


            Assert.IsTrue(cache.ShouldUpdateLiveProperties(objects));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(subscene.gameObject);
        }

        [UnityTest]
        public IEnumerator ShouldUpdateProperties_GenericConversion_test()
        {
            var subscene = CreateSubScene("subscene2", "parentScene2");
            EditorSceneManager.SetActiveScene(subscene.EditingScene);

            var authoringType = GetAuthoringComponentType<BindingRegistryIntComponent>();
            var go = new GameObject("go", authoringType);

            SceneManager.MoveGameObjectToScene(go, subscene.EditingScene);

            yield return RunLiveConversion();

            var cache = new BindingCache(authoringType);
            var objects = new Object[] {go.GetComponent(authoringType)};
            Assert.IsFalse(cache.ShouldUpdateLiveProperties(objects));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(subscene.gameObject);
        }

        [UnityTest]
        public IEnumerator ShouldUpdateProperties_MissingConversion_Test()
        {
            var subscene = CreateSubScene("subscene3", "parentScene3");
            EditorSceneManager.SetActiveScene(subscene.EditingScene);

            var go = new GameObject("go");
            var comp = go.AddComponent<MissingConversionMonobehaviourTest>();
            var transformUsageFlagComponent = go.AddComponent<AddTransformUsageFlag>();
            transformUsageFlagComponent.flags = TransformUsageFlags.Dynamic;

            SceneManager.MoveGameObjectToScene(go, subscene.EditingScene);

            yield return RunLiveConversion();

            var authoringType = typeof(MissingConversionMonobehaviourTest);
            var cache = new BindingCache(authoringType);
            var objects = new Object[] {comp};
            Assert.IsFalse(cache.ShouldUpdateLiveProperties(objects));
            LogAssert.Expect(LogType.Error, new Regex("Can't update live properties on the authoring component"));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(subscene.gameObject);
        }

        [UnityTest]
        public IEnumerator UpdateProperties_For_SingleRuntimeField_Per_AuthoringField_Test()
        {
            var subscene = CreateSubScene("subscene4", "parentScene4");
            EditorSceneManager.SetActiveScene(subscene.EditingScene);

            var authoringType = typeof(ManualConversionTestAuthoring);
            var go = new GameObject("go", authoringType);

            SceneManager.MoveGameObjectToScene(go, subscene.EditingScene);

            yield return RunLiveConversion();

            var cache = new BindingCache(authoringType);
            var serializedObject = new SerializedObject(go.GetComponent(authoringType));

            cache.UpdateLiveProperties(serializedObject, true);

            Assert.IsTrue(serializedObject.FindProperty("IntField").intValue == 5);
            Assert.IsTrue(Mathf.Approximately(serializedObject.FindProperty("FloatField").floatValue, 10.0f));
            Assert.IsTrue(serializedObject.FindProperty("BoolField").boolValue);

            var world = World.DefaultGameObjectInjectionWorld;
            world.GetOrCreateSystemManaged<UpdateSingleLiveProperties>().Update();
            world.Update();

            cache.UpdateLiveProperties(serializedObject, true);

            Assert.IsTrue(serializedObject.FindProperty("IntField").intValue == 1);
            Assert.IsTrue(Mathf.Approximately(serializedObject.FindProperty("FloatField").floatValue, 1.5f));
            Assert.IsFalse(serializedObject.FindProperty("BoolField").boolValue);
            Assert.IsTrue(serializedObject.FindProperty("QuaternionField").quaternionValue == new Quaternion(3.0f, 4.0f, 5.0f, 6.0f));
            Assert.IsTrue(serializedObject.FindProperty("Vector3Field").vector3Value == new Vector3(3.0f, 4.0f, 5.0f));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(subscene.gameObject);
        }

        [UnityTest]
        public IEnumerator UpdateProperties_For_MultipleRuntimeFields_Per_AuthoringField_Test()
        {
            var subscene = CreateSubScene("subscene5", "parentScene5");
            EditorSceneManager.SetActiveScene(subscene.EditingScene);

            var authoringType = GetAuthoringComponentType<BindingRegistryIntComponent>();
            var go = new GameObject("go", authoringType);

            SceneManager.MoveGameObjectToScene(go, subscene.EditingScene);

            yield return RunLiveConversion();

            var cache = new BindingCache(authoringType);
            var serializedObject = new SerializedObject(go.GetComponent(authoringType));

            cache.UpdateLiveProperties(serializedObject, true);

            Assert.IsTrue(serializedObject.FindProperty("Int1").intValue == 0);

            Assert.IsTrue(serializedObject.FindProperty("Int2.x").intValue == 0);
            Assert.IsTrue(serializedObject.FindProperty("Int2.y").intValue == 0);

            Assert.IsTrue(serializedObject.FindProperty("Int3.x").intValue == 0);
            Assert.IsTrue(serializedObject.FindProperty("Int3.y").intValue == 0);
            Assert.IsTrue(serializedObject.FindProperty("Int3.z").intValue == 0);

            Assert.IsTrue(serializedObject.FindProperty("Int4.x").intValue == 0);
            Assert.IsTrue(serializedObject.FindProperty("Int4.y").intValue == 0);
            Assert.IsTrue(serializedObject.FindProperty("Int4.z").intValue == 0);
            Assert.IsTrue(serializedObject.FindProperty("Int4.w").intValue == 0);

            var world = World.DefaultGameObjectInjectionWorld;
            world.GetOrCreateSystemManaged<UpdateMultipleLiveProperties>().Update();
            world.Update();

            cache.UpdateLiveProperties(serializedObject, true);

            Assert.IsTrue(serializedObject.FindProperty("Int1").intValue == 1);

            Assert.IsTrue(serializedObject.FindProperty("Int2.x").intValue == 1);
            Assert.IsTrue(serializedObject.FindProperty("Int2.y").intValue == 2);

            Assert.IsTrue(serializedObject.FindProperty("Int3.x").intValue == 1);
            Assert.IsTrue(serializedObject.FindProperty("Int3.y").intValue == 2);
            Assert.IsTrue(serializedObject.FindProperty("Int3.z").intValue == 3);

            Assert.IsTrue(serializedObject.FindProperty("Int4.x").intValue == 1);
            Assert.IsTrue(serializedObject.FindProperty("Int4.y").intValue == 2);
            Assert.IsTrue(serializedObject.FindProperty("Int4.z").intValue == 3);
            Assert.IsTrue(serializedObject.FindProperty("Int4.w").intValue == 4);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(subscene.gameObject);
        }
    }
}
