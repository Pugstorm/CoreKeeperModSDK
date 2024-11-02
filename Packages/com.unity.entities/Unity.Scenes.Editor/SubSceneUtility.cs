using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Scenes.Editor
{
    /// <summary>
    /// Utility methods for handling subscene behavior
    /// </summary>
    public static class SubSceneUtility
    {
        /// <summary>
        /// Marks a set of subscenes as editable if possible
        /// </summary>
        /// <param name="scenes">The list of subscenes to mark as editable</param>
        public static void EditScene(params SubScene[] scenes)
        {
            foreach (var subScene in scenes)
            {
                if (SubSceneInspectorUtility.CanEditScene(subScene))
                {
                    Scene scene;
                    if (Application.isPlaying)
                        scene = EditorSceneManager.LoadSceneInPlayMode(subScene.EditableScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
                    else
                        scene = EditorSceneManager.OpenScene(subScene.EditableScenePath, OpenSceneMode.Additive);
                    SubSceneInspectorUtility.SetSceneAsSubScene(scene);
                }
            }
        }
    }
}
