using System;
using System.IO;
using NUnit.Framework;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities.Tests;
using SubSceneUtility = Unity.Scenes.Editor.SubSceneUtility;

public class SubSceneNonDestructiveEditingTests
{
    const int s_OriginalValue = 42;
    const int s_BeforePlayValue = 43;
    const int s_NewValue = 69;

    static readonly string s_IntValuePath = nameof(TestComponentAuthoring.IntValue);

    string m_TempAssetDir;

    Scene m_MainScene;
    GameObject m_MainSceneGO;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var guid = AssetDatabase.CreateFolder("Assets", "_SubSceneNonDestructiveEditingTests_Temp");
        m_TempAssetDir = AssetDatabase.GUIDToAssetPath(guid);

        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

        AssetDatabase.Refresh();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        AssetDatabase.DeleteAsset(m_TempAssetDir);
    }

    [SetUp]
    public void SetUp()
    {
        // Create and save the main scene.
        m_MainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        EditorSceneManager.SetActiveScene(m_MainScene);
        var path = Path.Combine(m_TempAssetDir, "MainTest.unity");
        EditorSceneManager.SaveScene(m_MainScene, path);

        // Create main scene GO.
        m_MainSceneGO = new GameObject("MainSceneGO");
        var testComponent = m_MainSceneGO.AddComponent<TestComponentAuthoring>();
        testComponent.IntValue = s_OriginalValue;

        Assert.IsFalse(m_MainScene.isSubScene);
        Assert.IsFalse(m_MainScene.isDirty);
        Assert.IsTrue(m_MainScene.isLoaded);

        AssetDatabase.Refresh();
    }

    enum S
    {
        Closed,
        Clean,
        Dirty
    }

    struct SubSceneTestSetup
    {
        public string name;
        public string path;
        public Scene subScene;
        public GameObject subSceneGO;
        public SubScene subSceneMB => subSceneGO.GetComponent<SubScene>();
        public GameObject testGO;

        public S before;
        public S during;
        public S after;
    }

    SubSceneTestSetup CreateSubScene(S before, S during, S after)
    {
        string name = before.ToString() + "_" + during.ToString() + "_" + after.ToString();

        var subSceneOriginalGO = new GameObject(name);
        Selection.activeGameObject = subSceneOriginalGO;

        var go = new GameObject(name + "_ChildGO");
        go.transform.parent = subSceneOriginalGO.transform;
        var testComponent = go.AddComponent<TestComponentAuthoring>();
        testComponent.IntValue = s_OriginalValue;

        var args = new SubSceneContextMenu.NewSubSceneArgs
        {
            target = subSceneOriginalGO,
            newSubSceneMode = SubSceneContextMenu.NewSubSceneMode.MoveSelectionToScene
        };
        var subSceneComponent = SubSceneContextMenu.CreateNewSubScene(subSceneOriginalGO.name, args, InteractionMode.AutomatedAction);
        var subScene = subSceneComponent.EditingScene;

        Assert.IsTrue(subScene.isSubScene);

        return new SubSceneTestSetup()
        {
            name = name,
            path = subScene.path,
            subScene = subScene,
            subSceneGO = subSceneComponent.gameObject,
            testGO = go,
            before = before,
            during = during,
            after = after
        };
    }

    void ChangeToNewValue(GameObject go, int newValue = s_NewValue)
    {
        var tc = go.GetComponent<TestComponentAuthoring>();
        var so = new SerializedObject(tc);
        var sp = so.FindProperty(s_IntValuePath);

        sp.intValue = newValue;
        so.ApplyModifiedProperties();
    }

    void ChangeToNewValue(SubSceneTestSetup setup, int newValue = s_NewValue) => ChangeToNewValue(setup.testGO, newValue);

    int GetIntValue(GameObject go) => go.GetComponent<TestComponentAuthoring>().IntValue;
    int GetIntValue(SubSceneTestSetup setup) => GetIntValue(setup.testGO);

    void Stage_LoadUnloadBeforePlay(SubSceneTestSetup setup)
    {
        if (setup.before == S.Closed)
            SubSceneInspectorUtility.CloseAndAskSaveIfUserWantsTo(setup.subSceneMB);
    }

    void Stage_SetsBeforePlay(SubSceneTestSetup setup)
    {
        if (setup.before == S.Dirty)
            ChangeToNewValue(setup, s_BeforePlayValue);
    }

    void Stage_ChecksBeforePlay(SubSceneTestSetup setup)
    {
        Assert.AreEqual(setup.before == S.Dirty, setup.subScene.isDirty, setup.name);
        //Assert.AreEqual(setup.before == S.Closed, !setup.subScene.isLoaded, setup.name);

        if (setup.before == S.Dirty)
            Assert.AreEqual(s_BeforePlayValue, GetIntValue(setup), setup.name);
        else if (setup.before == S.Clean)
            Assert.AreEqual(s_OriginalValue, GetIntValue(setup), setup.name);
    }

    void Stage_LoadUnloadDuringPlay(SubSceneTestSetup setup)
    {
        if (setup.before == S.Closed && setup.during != S.Closed)
            SubSceneUtility.EditScene(setup.subSceneMB);
        else if (setup.before != S.Closed && setup.during == S.Closed)
        {
            EditorSceneManager.SaveScene(setup.subScene, setup.path); // We don't want to prompt during test.
            SubSceneInspectorUtility.CloseAndAskSaveIfUserWantsTo(setup.subSceneMB);
        }
    }

    void Stage_SetsDuringPlay(SubSceneTestSetup setup)
    {
        if (setup.during == S.Dirty)
            ChangeToNewValue(setup);
    }

    void Stage_ChecksDuringPlay(SubSceneTestSetup setup)
    {
        if (setup.during == S.Dirty)
            Assert.AreEqual(s_NewValue, GetIntValue(setup), setup.name);
        else if (setup.before == S.Dirty && setup.during == S.Clean)
            Assert.AreEqual(s_BeforePlayValue, GetIntValue(setup), setup.name);
        else if (setup.during == S.Clean)
            Assert.AreEqual(s_OriginalValue, GetIntValue(setup), setup.name);
    }

    void Stage_SaveDuringPlay(SubSceneTestSetup setup)
    {
        if (setup.during == S.Dirty && setup.after == S.Clean)
            EditorSceneManager.SaveScene(setup.subScene, setup.path);
    }

    void Stage_LoadUnloadAfterPlay(SubSceneTestSetup setup)
    {
        if (setup.during == S.Closed && setup.after != S.Closed)
            SubSceneUtility.EditScene(setup.subSceneMB);
        else if (setup.during != S.Closed && setup.after == S.Closed)
        {
            EditorSceneManager.SaveScene(setup.subScene, setup.path); // We don't want to prompt during test.
            SubSceneInspectorUtility.CloseAndAskSaveIfUserWantsTo(setup.subSceneMB);
        }
    }

    void Stage_ChecksAfterPlay(SubSceneTestSetup setup)
    {
        Assert.AreEqual(setup.after == S.Dirty, setup.subScene.isDirty, setup.name);
        //Assert.AreEqual(setup.after == S.Closed, !setup.subScene.isLoaded, setup.name);

        if (setup.during == S.Dirty)
            Assert.AreEqual(s_NewValue, GetIntValue(setup), setup.name);
        else if (setup.after == S.Clean)
            Assert.AreEqual(s_OriginalValue, GetIntValue(setup), setup.name);
    }

    [UnityTest]
    public IEnumerator AllCombinationsOfChanges()
    {
        Debug.Log("1");
        var setups = new List<SubSceneTestSetup>()
        {
            CreateSubScene(S.Clean, S.Clean, S.Clean),
            //CreateSubScene(S.Clean, S.Clean, S.Dirty), // Invalid combination.
            CreateSubScene(S.Clean, S.Dirty, S.Clean),
            CreateSubScene(S.Clean, S.Dirty, S.Dirty),
            //CreateSubScene(S.Dirty, S.Clean, S.Clean), // Invalid combination.
            //CreateSubScene(S.Dirty, S.Clean, S.Dirty), // Invalid combination.
            CreateSubScene(S.Dirty, S.Dirty, S.Clean),
            CreateSubScene(S.Dirty, S.Dirty, S.Dirty)
        };
#if false
        var possibleStates = new List<S>(){ S.Clean, S.Dirty }; // S.Closed not yet working (for tests only)
        foreach (var s1 in possibleStates)
            foreach (var s2 in possibleStates)
                foreach (var s3 in possibleStates)
                    setups.Add(CreateSubScene(s1, s2, s3));
#endif

        EditorSceneManager.SaveScene(m_MainScene, m_MainScene.path);

        // Avoid redrawing the Inspector while values change.
        Selection.activeObject = null;

        {
            foreach (var setup in setups)
            {
                Stage_LoadUnloadBeforePlay(setup);
                yield return null;
            }

            foreach (var setup in setups)
                Stage_SetsBeforePlay(setup);

            yield return null;
            Debug.Log("2");

            Assert.IsFalse(m_MainScene.isDirty);
            Assert.AreEqual(s_OriginalValue, GetIntValue(m_MainSceneGO));
            foreach (var setup in setups)
                Stage_ChecksBeforePlay(setup);

            Debug.Log("3");
        }
        yield return new EnterPlayMode();
        {
            foreach (var setup in setups)
            {
                Stage_LoadUnloadDuringPlay(setup);
                yield return null;
            }

            // All checks should be identical to before enter play mode.
            Assert.IsFalse(m_MainScene.isDirty);
            Assert.AreEqual(s_OriginalValue, GetIntValue(m_MainSceneGO));
            foreach (var setup in setups)
                Stage_ChecksBeforePlay(setup);

            // Make changes.
            ChangeToNewValue(m_MainSceneGO);
            foreach (var setup in setups)
                Stage_SetsDuringPlay(setup);

            Debug.Log("4");

            yield return null;

            Assert.IsFalse(m_MainScene.isDirty); // Main scene should not be dirtyable in play mode.
            Assert.AreEqual(s_NewValue, GetIntValue(m_MainSceneGO));
            foreach (var setup in setups)
                Stage_ChecksDuringPlay(setup);

            // Save some sub scenes.
            foreach (var setup in setups)
                Stage_SaveDuringPlay(setup);

            Assert.IsFalse(m_MainScene.isDirty);
            foreach (var setup in setups)
                Stage_ChecksAfterPlay(setup);

            Debug.Log("5");
        }
        yield return new ExitPlayMode();
        {
            foreach (var setup in setups)
            {
                Stage_LoadUnloadAfterPlay(setup);
                yield return null;
            }

            Assert.IsFalse(m_MainScene.isDirty);
            Assert.AreEqual(s_OriginalValue, GetIntValue(m_MainSceneGO)); // Restored correctly.
            foreach (var setup in setups)
                Stage_ChecksAfterPlay(setup);

            Debug.Log("6");
        }
        yield return null;
    }

}
