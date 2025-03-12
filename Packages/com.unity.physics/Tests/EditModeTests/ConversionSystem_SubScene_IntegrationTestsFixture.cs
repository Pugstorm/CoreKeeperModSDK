using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Entities;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;

namespace Unity.Physics.Tests.Authoring
{
    // Base fixture for conversion system tests involving sub-scenes.
    // Note: class mirrors some functionality in com.unity.entities' SubSceneConversionTests
    class ConversionSystem_SubScene_IntegrationTestsFixture
    {
        EnterPlayModeOptions m_EnterPlayModeOptions;
        bool m_EnterPlayModeOptionsEnabled;

        protected string TemporaryAssetsPath { get; set; }

        static readonly Regex k_NonWords = new Regex(@"\W");

        protected Entity SubSceneEntity { get; set; }
        protected SubScene SubSceneManaged { get; set; }

        [OneTimeSetUp]
        protected void OneTimeSetUp()
        {
            SubSceneEntity = Entity.Null;

            m_EnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            m_EnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

            // create folder for temporary assets
            TemporaryAssetsPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder("Assets", "SubScene_IntegrationTests"));
        }

        [OneTimeTearDown]
        protected void OneTimeTearDown()
        {
            // open an empty scene
            EditorSceneManager.SetActiveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene));

            // clean up scene dependency cache
            const string k_SceneDependencyCachePath = "Assets/SceneDependencyCache";
            if (AssetDatabase.IsValidFolder(k_SceneDependencyCachePath))
                AssetDatabase.DeleteAsset(k_SceneDependencyCachePath);

            // delete all temporary assets
            AssetDatabase.DeleteAsset(TemporaryAssetsPath);

            EditorSettings.enterPlayModeOptionsEnabled = m_EnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = m_EnterPlayModeOptions;
        }

        [TearDown]
        protected void TearDown()
        {
            if (SubSceneEntity != Entity.Null)
            {
                SceneSystem.UnloadScene(World.DefaultGameObjectInjectionWorld.Unmanaged, SubSceneEntity);
                SubSceneEntity = Entity.Null;
                SubSceneManaged = null;
            }
        }

        protected string TestNameWithoutSpecialCharacters =>
            k_NonWords.Replace(TestContext.CurrentContext.Test.Name, string.Empty);

        protected SubScene CreateSubScene(Action createSubSceneObjects)
        {
            // create sub-scene with objects
            var subScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var subScenePath = $"{TemporaryAssetsPath}/{TestNameWithoutSpecialCharacters}-subScene.unity";
            AssetDatabase.DeleteAsset(subScenePath);
            createSubSceneObjects.Invoke();
            EditorSceneManager.SaveScene(subScene, subScenePath);

            // create parent scene
            var parentScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var parentScenePath = $"{TemporaryAssetsPath}/{TestNameWithoutSpecialCharacters}-parentScene.unity";
            AssetDatabase.DeleteAsset(parentScenePath);

            // create GameObject with SubScene component
            var subSceneMB = new GameObject(subScene.name).AddComponent<SubScene>();
            Undo.RecordObject(subSceneMB, "Assign sub-scene");
            subSceneMB.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath);
            subSceneMB.AutoLoadScene = true;
            EditorSceneManager.SaveScene(parentScene, parentScenePath);
            SceneManager.SetActiveScene(parentScene);

            return subSceneMB;
        }

        protected void CreateAndLoadSubScene<T>(Action<T> configureSubSceneObject)
            where T : Component
        {
            Assert.IsNull(SubSceneManaged);
            Assert.AreEqual(Entity.Null, SubSceneEntity);

            // create sub-scene
            SubSceneManaged = CreateSubScene(() =>
            {
                var component = new GameObject(TestNameWithoutSpecialCharacters).AddComponent<T>();
                configureSubSceneObject(component);
            });

            // convert and load sub-scene
            var world = World.DefaultGameObjectInjectionWorld;
            SubSceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, SubSceneManaged.SceneGUID, new SceneSystem.LoadParameters
            {
                Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
            });
            // TODO: Editor doesn't update if it doesn't have focus, so we must explicitly update the world to process the load.
            world.Update();
        }
    }
}
