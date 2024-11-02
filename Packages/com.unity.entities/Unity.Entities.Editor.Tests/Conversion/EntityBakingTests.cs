using NUnit.Framework;
using System.IO;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Entities.Editor.Tests
{
    class EntityBakingTests
    {
        Scene m_MainScene;
        GameObject m_GrandParent;
        GameObject m_Parent;
        GameObject m_Child;
        string m_TempAssetDir;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var guid = AssetDatabase.CreateFolder("Assets", nameof(EntityBakingTests));
            m_TempAssetDir = AssetDatabase.GUIDToAssetPath(guid);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetDatabase.DeleteAsset(m_TempAssetDir);
        }

        [SetUp]
        public void SetUp()
        {
            m_MainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            EditorSceneManager.SetActiveScene(m_MainScene);

            var mainScenePath = Path.Combine(m_TempAssetDir, "test.unity");
            EditorSceneManager.SaveScene(m_MainScene, mainScenePath);

            m_GrandParent = new GameObject("GrandParent");
            m_Parent = new GameObject("Parent");
            m_Child = new GameObject("Child");

            m_Parent.transform.parent = m_GrandParent.transform;
            m_Child.transform.parent = m_Parent.transform;

            Selection.activeObject = m_GrandParent;
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(m_GrandParent);
            GameObject.DestroyImmediate(m_Parent);
            GameObject.DestroyImmediate(m_Child);
        }

        [Test]
        public void Should_Not_Bake_Outside_Subscene()
        {
            var conversionStatusGrandParent = GameObjectBakingEditorUtility.GetGameObjectBakingResultStatus(m_GrandParent);
            var conversionStatusParent = GameObjectBakingEditorUtility.GetGameObjectBakingResultStatus(m_Parent);
            var conversionStatusChild = GameObjectBakingEditorUtility.GetGameObjectBakingResultStatus(m_Child);

            Assert.That(conversionStatusGrandParent, Is.EqualTo(GameObjectBakingResultStatus.NotBaked));
            Assert.That(conversionStatusParent, Is.EqualTo(GameObjectBakingResultStatus.NotBaked));
            Assert.That(conversionStatusChild, Is.EqualTo(GameObjectBakingResultStatus.NotBaked));
        }

        [Test]
        public void Should_Bake_Under_Subscene()
        {
            SubSceneContextMenu.CreateSubSceneAndAddSelection(m_GrandParent);

            var conversionStatusGrandParent = GameObjectBakingEditorUtility.GetGameObjectBakingResultStatus(m_GrandParent);
            var conversionStatusParent = GameObjectBakingEditorUtility.GetGameObjectBakingResultStatus(m_Parent);
            var conversionStatusChild = GameObjectBakingEditorUtility.GetGameObjectBakingResultStatus(m_Child);

            Assert.That(conversionStatusGrandParent, Is.EqualTo(GameObjectBakingResultStatus.BakedBySubScene));
            Assert.That(conversionStatusParent, Is.EqualTo(GameObjectBakingResultStatus.BakedBySubScene));
            Assert.That(conversionStatusChild, Is.EqualTo(GameObjectBakingResultStatus.BakedBySubScene));
        }
    }
}
