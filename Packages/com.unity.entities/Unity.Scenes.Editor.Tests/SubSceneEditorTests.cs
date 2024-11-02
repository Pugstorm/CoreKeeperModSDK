using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Scenes;
using Unity.Scenes.Editor;
using Unity.Scenes.Editor.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SubSceneEditorTests
{
    string m_TempAssetDir;

    [OneTimeSetUp]
    public void SetUp()
    {
        var guid = AssetDatabase.CreateFolder("Assets", nameof(SubSceneEditorTests));
        m_TempAssetDir = AssetDatabase.GUIDToAssetPath(guid);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        AssetDatabase.DeleteAsset(m_TempAssetDir);
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

    [Test]
    public void CreateEmptySubScene()
    {
        Assert.DoesNotThrow(() => CreateSubScene("EmptySubScene", "ParentScene", InteractionMode.AutomatedAction, SubSceneContextMenu.NewSubSceneMode.EmptyScene));
    }

    [Test]
    public void MissingSubSceneFolder()
    {
        Assert.DoesNotThrow(() => CreateSubScene("SubScene", "whatever"));
    }

    [Test]
    public void ExistingSubSceneFolder()
    {
        Directory.CreateDirectory(Path.Combine(m_TempAssetDir, "MatchingCapitalization"));
        Assert.DoesNotThrow(() => CreateSubScene("SubScene", "MatchingCapitalization"));
    }

    [Test]
    public void WrongCapitalizationSubSceneFolder()
    {
        Directory.CreateDirectory(Path.Combine(m_TempAssetDir, "LOWERCASE"));
        Assert.DoesNotThrow(() =>  CreateSubScene("SubScene", "lowercase"));
    }

    [Test]
    public void InvalidFileNameCharInGameObjectNameThrows()
    {
        Assert.Throws<ArgumentException>(
            () => { CreateSubScene("SubScene/Something:", "ParentScene"); }
            , "Invalid file characters should be handled gracefully");
    }

    [Test]
    public void EmptySubSceneNameThrows()
    {
        Assert.Throws<ArgumentException>(
            () => { CreateSubScene("", "ParentScene"); }
            , "Empty SubScene name is handled gracefully");
    }

    [Test]
    public void MissingSceneForSubScene_GetSceneName_ReturnsEmptyString()
    {
        var go = new GameObject();
        var subscene = go.AddComponent<SubScene>();
        Assert.IsNull(subscene.SceneAsset);
        Assert.AreEqual(string.Empty, subscene.SceneName);
    }

    [Test]
    public void LeadingAndTrailingWhiteSpacesAreTrimmedFromSubSceneName()
    {
        string subSceneName = " SubScene ";
        SubScene subSceneComponent = CreateSubScene(subSceneName, "ParentScene");
        Assert.IsTrue(subSceneComponent.EditingScene.IsValid(), "Leading and trailing white spaces should be trimmed before creating the Scene asset file");
        Assert.AreEqual(subSceneComponent.EditingScene.name, subSceneName.Trim(), "Leading and trailing white spaces should be trimmed before creating the Scene asset file");
    }

    [Test]
    public void OverwritingExistingSceneFilesArePrevented()
    {
        Assert.IsTrue(CreateSubScene("SubScene", "SameParentScene").EditingScene.IsValid(), "First SubScene should be created");
        Assert.Throws<ArgumentException>(
            () => { CreateSubScene("SubScene", "SameParentScene"); }
            , "Trying to create a SubScene with same path as an exising SubScene should be prevented");
    }

    [Test]
    public void RemovingSceneAssetReferenceUnloadsScene()
    {
        var subsceneComponent = CreateSubScene("SubSceneToUnload", "ParentScene");
        Assert.IsTrue(subsceneComponent.EditingScene.isLoaded);

        subsceneComponent.SceneAsset = null;
        Assert.IsFalse(subsceneComponent.EditingScene.isLoaded, "The loaded sub scene should have been unloaded since it is no longer shown in the Hierarchy");
    }

    [Test]
    public void CreateSubSceneFromSelectionKeepsSiblingIndexInHierarchy()
    {
        var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        EditorSceneManager.SetActiveScene(mainScene);

        var path = Path.Combine(m_TempAssetDir, "ParentScene.unity");
        EditorSceneManager.SaveScene(mainScene, path);

        var go1 = new GameObject("go1");
        var go2 = new GameObject("go2");
        var go3 = new GameObject("go3");

        var siblingIndex = go2.transform.GetSiblingIndex();

        Selection.activeGameObject = go2;

        var args = new SubSceneContextMenu.NewSubSceneArgs
        {
            target = Selection.activeGameObject,
            newSubSceneMode = SubSceneContextMenu.NewSubSceneMode.MoveSelectionToScene
        };
        var subsceneComponent = SubSceneContextMenu.CreateNewSubScene(args.target.name, args, InteractionMode.AutomatedAction);

        Assert.AreEqual(siblingIndex, subsceneComponent.transform.GetSiblingIndex(), "The resulting SubScene GameObject should have the sibling order in the Hierarchy as the input GameObject.");
    }

    [Test]
    public void CreatingSubSceneFromPartialPrefabInstanceIsNotAllowed()
    {
        var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        EditorSceneManager.SetActiveScene(mainScene);

        var path = Path.Combine(m_TempAssetDir, "ParentScene.unity");
        EditorSceneManager.SaveScene(mainScene, path);

        var go1 = new GameObject("go1");
        var go2 = new GameObject("go2");
        var go3 = new GameObject("go3");
        go2.transform.parent = go1.transform;
        go3.transform.parent = go2.transform;
        PrefabUtility.SaveAsPrefabAssetAndConnect(go1, m_TempAssetDir + "/TestPrefab.prefab", InteractionMode.AutomatedAction);

        Selection.activeGameObject = go2;
        var args = new SubSceneContextMenu.NewSubSceneArgs
        {
            target = Selection.activeGameObject,
            newSubSceneMode = SubSceneContextMenu.NewSubSceneMode.MoveSelectionToScene
        };

        Assert.Throws<ArgumentException>(
            () => { SubSceneContextMenu.CreateNewSubScene(args.target.name, args, InteractionMode.AutomatedAction); }
            , "Creating a SubScene from a partial Prefab selection should fail");
    }

    [Test]
    public void CreateSubSceneSupportsUndo()
    {
        var subSceneComponent = CreateSubScene("SubSceneToUndo", "ParentScene", InteractionMode.UserAction);
        Assert.IsTrue(subSceneComponent.EditingScene.isLoaded);

        var rootTransform = subSceneComponent.EditingScene.GetRootGameObjects()[0];
        Assert.IsNotNull(rootTransform, "SubScene should have a root GameObject");
        Assert.IsTrue(rootTransform.gameObject.scene.isSubScene, "The GameObject should now live in a SubScene");
        Assert.AreEqual(subSceneComponent.EditingScene, rootTransform.gameObject.scene);

        Undo.PerformUndo();
        Assert.IsTrue(subSceneComponent == null, "The SubScene component should have been destroyed as part of Undo");
        Assert.IsNotNull(rootTransform, "The root should still be valid after Undo");
        Assert.IsFalse(rootTransform.gameObject.scene.isSubScene, "The GameObject moved to the SubScene should now be back in the parent scene");
    }

    [Test]
    public void SubSceneAssetSavedToNewAssetPath_WillFixUpItsSceneAssetReference()
    {
        var mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var path = Path.Combine(m_TempAssetDir, "MainScene_SavedAs.unity");
        EditorSceneManager.SaveScene(mainScene, path);

        var subSceneComponent = SubSceneTestsHelper.CreateSubSceneInSceneFromObjects("SubScene1", true, mainScene, () =>
        {
            var go = new GameObject("GameObject1");
            return new List<GameObject> { go };
        });

        var subScenePath = subSceneComponent.EditableScenePath;
        var dir = Path.GetDirectoryName(subScenePath);
        var ext = Path.GetExtension(subScenePath);
        var newPath = Path.Combine(dir, "SubScene2" + ext);
        newPath = newPath.Replace("\\", "/");

        Assert.IsFalse(subSceneComponent.gameObject.scene.isDirty);

        // Save scene to a new path (Simulating File -> Save As menu item for an SubScene set as the Active Scene)
        var subScene = subSceneComponent.EditingScene;
        EditorSceneManager.SaveScene(subScene, newPath, /*saveAsCopy =*/ false);

        Assert.IsTrue(subSceneComponent.gameObject.scene.isDirty);
        var canBeFoundScene = SceneManager.GetSceneByPath(newPath);
        Assert.IsTrue(canBeFoundScene.IsValid());
        Assert.IsTrue(!string.IsNullOrEmpty(subSceneComponent.EditingScene.path), "The SubScene lost its editing scene after it was saved to a new path. This will break authoring.");
        Assert.AreEqual(canBeFoundScene.path, subSceneComponent.EditingScene.path);
        Assert.IsNotNull(subSceneComponent.EditingScene.GetRootGameObjects()[0]);
    }
}
