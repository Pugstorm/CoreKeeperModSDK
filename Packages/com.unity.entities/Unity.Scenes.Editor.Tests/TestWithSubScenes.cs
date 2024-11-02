using System;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unity.Scenes.Editor.Tests
{
    [Serializable]
    public struct TestWithSubScenes
    {
        public void Setup()
        {
            SceneManager.SetActiveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene));
        }

        public void TearDown()
        {
            SceneWithBuildConfigurationGUIDs.ClearBuildSettingsCache();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }
    }
}
