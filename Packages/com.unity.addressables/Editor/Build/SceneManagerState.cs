using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.Build
{
    /// <summary>
    /// Serializable object that can be used to save and restore the state of the editor scene manager.
    /// </summary>
    [Serializable]
    public class SceneManagerState
    {
        [Serializable]
        internal class SceneState
        {
            [FormerlySerializedAs("m_isActive")]
            [SerializeField]
            internal bool isActive;

            [FormerlySerializedAs("m_isLoaded")]
            [SerializeField]
            internal bool isLoaded;

            [FormerlySerializedAs("m_path")]
            [SerializeField]
            internal string path;

            internal SceneState()
            {
            }

            internal SceneState(SceneSetup s)
            {
                isActive = s.isActive;
                isLoaded = s.isLoaded;
                path = s.path;
            }

            internal SceneSetup ToSceneSetup()
            {
                var ss = new SceneSetup();
                ss.isActive = isActive;
                ss.isLoaded = isLoaded;
                ss.path = path;
                return ss;
            }
        }

        [Serializable]
        internal class EbsSceneState
        {
            [FormerlySerializedAs("m_guid")]
            [SerializeField]
            internal string guid;

            [FormerlySerializedAs("m_enabled")]
            [SerializeField]
            internal bool enabled;

            internal EbsSceneState()
            {
            }

            internal EbsSceneState(EditorBuildSettingsScene s)
            {
                guid = s.guid.ToString();
                enabled = s.enabled;
            }

            internal EditorBuildSettingsScene GetBuildSettingsScene()
            {
                return new EditorBuildSettingsScene(new GUID(guid), enabled);
            }
        }

        [SerializeField]
        internal SceneState[] openSceneState;

        [SerializeField]
        internal EbsSceneState[] editorBuildSettingsSceneState;

        static SceneManagerState Create(SceneSetup[] scenes)
        {
            var scenesList = new List<SceneState>();
            var state = new SceneManagerState();
            foreach (var s in scenes)
                scenesList.Add(new SceneState(s));
            state.openSceneState = scenesList.ToArray();
            var edbss = new List<EbsSceneState>();
            foreach (var s in BuiltinSceneCache.scenes)
                edbss.Add(new EbsSceneState(s));
            state.editorBuildSettingsSceneState = edbss.ToArray();
            return state;
        }

        internal SceneSetup[] GetSceneSetups()
        {
            var setups = new List<SceneSetup>();
            foreach (var s in openSceneState)
                setups.Add(s.ToSceneSetup());
            return setups.ToArray();
        }

        EditorBuildSettingsScene[] GetEditorBuildSettingScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var s in editorBuildSettingsSceneState)
                scenes.Add(s.GetBuildSettingsScene());
            return scenes.ToArray();
        }

        static string s_DefaultPath = Addressables.LibraryPath + "SceneManagerState.json";

        /// <summary>
        /// Record the state of the EditorSceneManager and save to a JSON file.
        /// </summary>
        /// <param name="path">The path to save the recorded state.</param>
        public static void Record(string path = "")
        {
            if (string.IsNullOrEmpty(path))
                path = s_DefaultPath;

            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonUtility.ToJson(Create(EditorSceneManager.GetSceneManagerSetup())));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Adds a set of scenes to the scene list for use in editor play mode.
        /// </summary>
        /// <param name="playModeScenes">The scenes to add to the editor scenes list.</param>
        public static void AddScenesForPlayMode(List<EditorBuildSettingsScene> playModeScenes)
        {
            if (playModeScenes != null)
            {
                List<EditorBuildSettingsScene> newScenesList = new List<EditorBuildSettingsScene>();
                newScenesList.AddRange(BuiltinSceneCache.scenes);
                newScenesList.AddRange(playModeScenes);
                BuiltinSceneCache.scenes = newScenesList.ToArray();
            }
        }

        /// <summary>
        /// Restore the state of the EditorSceneManager.
        /// </summary>
        /// <param name="path">The path to load the state data from.  This file is generated by calling SceneManagerState.Record.</param>
        /// <param name="restoreSceneManagerSetup">If true, the recorded active scenes are restored. EditorBuildSettings.scenes are always restored.</param>
        public static void Restore(string path = "", bool restoreSceneManagerSetup = false)
        {
            if (string.IsNullOrEmpty(path))
                path = s_DefaultPath;

            try
            {
                var state = JsonUtility.FromJson<SceneManagerState>(File.ReadAllText(path));
                if (restoreSceneManagerSetup)
                    EditorSceneManager.RestoreSceneManagerSetup(state.GetSceneSetups());
                BuiltinSceneCache.scenes = state.GetEditorBuildSettingScenes();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
