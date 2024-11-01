using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using File = System.IO.File;
using Hash128 = Unity.Entities.Hash128;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
#endif
#pragma warning disable 649


namespace Unity.Scenes
{
    #if UNITY_EDITOR
    [InitializeOnLoad]
    internal static class SubSceneUtility
    {
        static SceneSavedAtNewPath s_SceneSavedAtNewPath;

        static SubSceneUtility()
        {
            EditorApplication.update += SanitiseOpenedSubScenes;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        // This fixes an issue with Unity automatically loading whatever scene you had opened when you last closed it.
        // If you have a SubScene opened for edit and close Unity then remove this SubScene from the Scene (source control revert, manually edit scene file)
        // Then when you next open Unity, it will automatically load the GameObject scene additively, even without a SubScene referencing it
        // TODO: Currently this shows the dangling Scene before removing it (some frame(s) of delay here), needs a better solution
        // TODO: DOTS-3474
        static void SanitiseOpenedSubScenes()
        {
            EditorApplication.update -= SanitiseOpenedSubScenes;

            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);

                if (scene.isSubScene)
                {
                    bool found = false;
                    foreach (var subScene in SubScene.AllSubScenes)
                    {
                        if (subScene.EditingScene == scene)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        EditorSceneManager.UnloadSceneAsync(scene);
                    }
                }
            }
        }

        class SceneSavedAtNewPath
        {
            public string NewPath;
            public List<SubScene> SubScenes = new List<SubScene>();
        }

        static void OnSceneSaving(Scene scene, string path)
        {
            if (SubScene.AllSubScenes.Count == 0)
                return;

            if (scene.path == path)
                return;

            s_SceneSavedAtNewPath = new SceneSavedAtNewPath();
            s_SceneSavedAtNewPath.NewPath = path;
            foreach (var subScene in SubScene.AllSubScenes)
            {
                if (subScene.EditingScene == scene)
                {
                    if (scene.path != path)
                        s_SceneSavedAtNewPath.SubScenes.Add(subScene);
                }
            }

            if (s_SceneSavedAtNewPath.SubScenes.Count == 0)
                s_SceneSavedAtNewPath = null;
        }

        static void OnSceneSaved(Scene scene)
        {
            if (s_SceneSavedAtNewPath == null)
                return;

            var newSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(s_SceneSavedAtNewPath.NewPath);
            if (newSceneAsset != null && scene.path == s_SceneSavedAtNewPath.NewPath)
            {
                foreach (var subscene in s_SceneSavedAtNewPath.SubScenes)
                {
                    subscene.SceneAsset = newSceneAsset;
                    EditorUtility.SetDirty(subscene);
                }
            }

            s_SceneSavedAtNewPath = null;
        }
    }
#endif

    /// <summary>
    /// A component representing a subscene reference.
    /// </summary>
    /// <remarks>Subscenes are SceneAssets which are loaded on demand by the SubScene component.</remarks>
    // TODO: worth adding more information here
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SubScene : MonoBehaviour
    {
#if UNITY_EDITOR
        [FormerlySerializedAs("sceneAsset")]
        [SerializeField] SceneAsset _SceneAsset;
        [SerializeField] Color _HierarchyColor = Color.gray;

        static List<SubScene> m_AllSubScenes = new List<SubScene>();

        /// <summary>
        /// The list of loaded sub scenes.
        /// </summary>
        public static IReadOnlyCollection<SubScene> AllSubScenes { get { return m_AllSubScenes; } }
#endif

        /// <summary>Set when the scene should load.</summary>
        /// <remarks>
        /// Set to true to load the scene automatically when entering Play mode.
        /// Set to false to explicitly load the scene.
        /// </remarks>
        public bool AutoLoadScene = true;

        [SerializeField]
        [HideInInspector]
        Hash128 _SceneGUID;

        [NonSerialized]
        Hash128 _AddedSceneGUID;

#if UNITY_EDITOR

        [NonSerialized]
        bool _IsAddedToListOfAllSubScenes;

        /// <summary>
        /// Represents the scene asset.
        /// </summary>
        public SceneAsset SceneAsset
        {
            get { return _SceneAsset; }
            set
            {
                if (_SceneAsset == value)
                    return;

                // If the SceneAsset has been loaded we need to close it before changing to a new SceneAsset reference so we
                // don't end up with loaded scenes which are not visible in the Hierarchy.
                if (_SceneAsset != null)
                {
                    Scene scene = EditingScene;
                    if (scene.isLoaded && scene.isSubScene)
                        EditorSceneManager.CloseScene(scene, true);
                }

                _SceneAsset = value;
                OnValidate();
            }
        }

        /// <summary>
        /// Represents the scene name.
        /// </summary>
        public string SceneName
        {
            get { return _SceneAsset != null ? _SceneAsset.name : ""; }
        }

        /// <summary>
        /// Represents the color of the Hierarchy panel.
        /// </summary>
        public Color HierarchyColor
        {
            get { return _HierarchyColor; }
            set { _HierarchyColor = value; }
        }

        /// <summary>
        /// Represents the path of the SceneAsset.
        /// </summary>
        public string EditableScenePath
        {
            get
            {
                return _SceneAsset != null ? AssetDatabase.GetAssetPath(_SceneAsset) : "";
            }
        }

        /// <summary>
        /// Represents the editing scene.
        /// </summary>
        public Scene EditingScene
        {
            get
            {
                if (_SceneAsset == null)
                    return default(Scene);

                return EditorSceneManager.GetSceneByPath(AssetDatabase.GetAssetPath(_SceneAsset));
            }
        }

        /// <summary>
        /// Returns whether the subscene is open for editing.
        /// </summary>
        public bool IsLoaded
        {
            get { return EditingScene.isLoaded; }
        }

        void WarnIfNeeded()
        {
            if (!IsInMainStage())
                return;

            if (SceneAsset != null)
            {
                foreach (var subscene in m_AllSubScenes)
                {
                    if (!subscene.IsInMainStage())
                        continue;

                    if (subscene.SceneAsset == SceneAsset)
                    {
                        UnityEngine.Debug.LogWarning($"Sub Scenes can not reference the same scene ('{EditableScenePath}') multiple times.", this);
                        return;
                    }
                }
            }
        }

        void OnValidate()
        {
            _SceneGUID = AssetDatabaseCompatibility.PathToGUID(AssetDatabase.GetAssetPath(_SceneAsset));

            if (_IsAddedToListOfAllSubScenes && IsInMainStage())
            {
                if (_SceneGUID != _AddedSceneGUID)
                {
                    RebuildSceneEntities();
                }
            }
        }

        internal void RebuildSceneEntities()
        {
            RemoveSceneEntities();
            if (_SceneGUID != default)
                AddSceneEntities();
        }

        internal bool CanBeLoaded()
        {
            if (SceneAsset == null)
                return false;

            if (!_IsAddedToListOfAllSubScenes)
                return false;

            if (!IsInMainStage())
                return false;

            if (!isActiveAndEnabled)
                return false;

            return true;
        }

        internal bool IsInMainStage()
        {
            return !EditorUtility.IsPersistent(gameObject) && StageUtility.GetStageHandle(gameObject) == StageUtility.GetMainStageHandle();
        }

#endif

        /// <summary>
        /// Represents the GUID of the SceneAsset.
        /// </summary>
        public Hash128 SceneGUID => _SceneGUID;

        void OnEnable()
        {
#if UNITY_EDITOR
            WarnIfNeeded();

            _IsAddedToListOfAllSubScenes = true;
            m_AllSubScenes.Add(this);

            // If this is an import worker, we do not want to initialise an Entity world
            if (AssetDatabaseCompatibility.IsAssetImportWorkerProcess())
                return;
#endif

            // Do not move the default initialization below the early out, it's important for the world to exist
            // if the Subscene gets assigned later, otherwise the change won't trigger an import/conversion.
            DefaultWorldInitialization.DefaultLazyEditModeInitialize();

#if UNITY_EDITOR
            if (_SceneGUID == default(Hash128))
                return;

            if (!IsInMainStage())
                return;
#endif

            AddSceneEntities();
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            _IsAddedToListOfAllSubScenes = false;
            m_AllSubScenes.Remove(this);

            // We don't want to do any Entity work if we're in the worker
            if (AssetDatabaseCompatibility.IsAssetImportWorkerProcess())
                return;
#endif

            RemoveSceneEntities();
        }

        unsafe void AddSceneEntities()
        {
            Assert.IsTrue(_AddedSceneGUID == default);
            Assert.IsFalse(_SceneGUID == default);

            var flags = AutoLoadScene ? 0 : SceneLoadFlags.DisableAutoLoad;
#if UNITY_EDITOR
            flags |= EditorApplication.isPlaying ? SceneLoadFlags.BlockOnImport : 0;
#else
            flags |= SceneLoadFlags.BlockOnImport;
#endif
            foreach (var world in World.All)
            {
                var sceneSystem = world.GetExistingSystem<SceneSystem>();

                var stateptr = world.Unmanaged.ResolveSystemState(sceneSystem);
                if (stateptr != null)
                {
                    var loadParams = new SceneSystem.LoadParameters
                    {
                        Flags = flags
                    };

                    var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, _SceneGUID, loadParams);
                    stateptr->EntityManager.AddComponentObject(sceneEntity, this);
                    _AddedSceneGUID = _SceneGUID;
                }
            }
        }

        unsafe void RemoveSceneEntities()
        {
            if (_AddedSceneGUID != default)
            {
                var sceneGUID = _AddedSceneGUID;
                _AddedSceneGUID = default;

                foreach (var world in World.All)
                {
                    var sceneSystem = world.GetExistingSystem<SceneSystem>();

                    var stateptr = world.Unmanaged.ResolveSystemState(sceneSystem);
                    if (stateptr != null)
                        SceneSystem.UnloadScene(world.Unmanaged, sceneGUID, SceneSystem.UnloadParameters.DestroyMetaEntities);
                }
            }
        }

        void UnloadScene()
        {
#if UNITY_EDITOR
            var scene = EditingScene;
            if (scene.IsValid())
            {
                // If there is only one scene left in the editor, we create a new empty scene
                // before unloading this sub scene
                if (SceneManager.loadedSceneCount == 1 && !EditorApplication.isPlaying)
                {
                    Debug.Log("Unloading last scene, creating new scene");
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                }

                SceneManager.UnloadSceneAsync(scene);
            }
#endif
        }

        private void OnDestroy()
        {
            UnloadScene();
        }
    }
}
