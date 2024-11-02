using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Build;
using Unity.Entities.Conversion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Assert = Unity.Assertions.Assert;
using Hash128 = Unity.Entities.Hash128;
using Object = UnityEngine.Object;

namespace Unity.Scenes.Editor
{
    //@TODO: LiveConversionConnection is starting to be a relatively complex statemachine. Lets have unit tests for it in isolation...

    // A connection to a Player or Editor with a specific build configuration.
    // Each destination world in each player/editor, has it's own LiveConversionConnection so we can generate different data for different worlds.
    // For example server world vs client world.
    class LiveConversionConnection
    {
        static int                                 GlobalDirtyID = 0;

        HashSet<Hash128>                           _LoadedScenes = new HashSet<Hash128>();
        HashSet<Hash128>                           _SentLoadScenes = new HashSet<Hash128>();
        NativeList<Hash128>                        _RemovedScenes;
        Dictionary<Hash128, LiveConversionDiffGenerator> _SceneGUIDToLiveConversion = new Dictionary<Hash128, LiveConversionDiffGenerator>();
        int                                        _PreviousGlobalDirtyID;
        Dictionary<Hash128, Scene>                 _GUIDToEditScene = new Dictionary<Hash128, Scene>();

        internal readonly Hash128                  _ConfigurationGUID;
        IEntitiesPlayerSettings                    _SettingAsset;
        UnityEngine.Hash128                        _BuildConfigurationArtifactHash;

        static readonly List<LiveConversionConnection>   k_AllConnections = new List<LiveConversionConnection>();

        public LiveConversionConnection(Hash128 configGuid)
        {
            _ConfigurationGUID = configGuid;
            if (configGuid != default)
            {
                _SettingAsset = DotsGlobalSettings.Instance.GetSettingsAsset(configGuid);
            }
            if (_SettingAsset == null)
            {
                // default to the standard entities client settings asset
                _SettingAsset = DotsGlobalSettings.Instance.GetClientSettingAsset();
                _ConfigurationGUID = _SettingAsset.GUID;
            }

            Lightmapping.bakeCompleted += OnLightBakingCompleted;
            ObjectChangeEvents.changesPublished += OnEditorChangeEvents;
            EditorSceneManager.sceneOpened += SceneOpened;
            EditorSceneManager.sceneClosed += SceneClosed;

            _RemovedScenes = new NativeList<Hash128>(Allocator.Persistent);
            k_AllConnections.Add(this);
        }

        public void Dispose()
        {
            k_AllConnections.Remove(this);
            Lightmapping.bakeCompleted -= OnLightBakingCompleted;
            EditorSceneManager.sceneOpened -= SceneOpened;
            EditorSceneManager.sceneClosed -= SceneClosed;
            ObjectChangeEvents.changesPublished -= OnEditorChangeEvents;
            foreach (var LiveConversion in _SceneGUIDToLiveConversion.Values)
                LiveConversion.Dispose();
            _SceneGUIDToLiveConversion.Clear();
            _SceneGUIDToLiveConversion = null;
            _RemovedScenes.Dispose();
        }

        internal World GetConvertedWorldForScene(Hash128 sceneGUID)
        {
            if (!_SceneGUIDToLiveConversion.TryGetValue(sceneGUID, out var LiveConversionDiffGenerator))
                return null;
            return LiveConversionDiffGenerator.ConvertedWorld;
        }

        void SceneOpened(Scene scene, OpenSceneMode mode)
        {
            {
                // @TODO: This is a temporary workaround until ObjectChangeEventStream handles this
                // When a scene is re-loaded, we need to re-convert it. This happens for example when the changes in a scene
                // are discarded.
                GetLiveConversion(scene)?.RequestCleanConversion();
            }
            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
        }

        void SceneClosed(Scene scene)
        {
            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
        }

        void OnEditorChangeEvents(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                var type = stream.GetEventType(i);
                switch (type)
                {
                    case ObjectChangeKind.None:
                        break;
                    case ObjectChangeKind.ChangeScene:
                    {
                        stream.GetChangeSceneEvent(i, out var evt);
                        if (evt.scene.IsValid())
                            GetLiveConversion(evt.scene)?.RequestCleanConversion();
                        break;
                    }
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    {
                        stream.GetCreateGameObjectHierarchyEvent(i, out var evt);
                        GetLiveConversion(evt.scene)?.ChangeTracker.MarkBakeHierarchy(evt.instanceId);
                        break;
                    }
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    {
                        stream.GetChangeGameObjectStructureHierarchyEvent(i, out var evt);
                        GetLiveConversion(evt.scene)?.ChangeTracker.MarkBakeHierarchy(evt.instanceId);
                        break;
                    }
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    {
                        stream.GetChangeGameObjectStructureEvent(i, out var evt);
                        GetLiveConversion(evt.scene)?.ChangeTracker.MarkChanged(evt.instanceId);
                        break;
                    }
                    case ObjectChangeKind.ChangeGameObjectParent:
                    {
                        stream.GetChangeGameObjectParentEvent(i, out var evt);
                        if (evt.newScene != evt.previousScene)
                        {
                            GetLiveConversion(evt.newScene)?.ChangeTracker.MarkBakeHierarchy(evt.instanceId);
                            GetLiveConversion(evt.previousScene)?.ChangeTracker.MarkRemoved(evt.instanceId);
                        }
                        else
                            GetLiveConversion(evt.newScene)?.ChangeTracker.MarkParentChanged(evt.instanceId, evt.newParentInstanceId);
                        break;
                    }
                    case ObjectChangeKind.ChangeChildrenOrder:
                    {
                        stream.GetChangeChildrenOrderEvent(i, out var evt);
                        GetLiveConversion(evt.scene)?.ChangeTracker.MarkChildrenOrderChange(evt.instanceId);
                        break;
                    }
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    {
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var evt);
                        var target = EditorUtility.InstanceIDToObject(evt.instanceId);
                        if (target is Component c)
                            GetLiveConversion(evt.scene)?.ChangeTracker.MarkComponentChanged(c);
                        else
                            GetLiveConversion(evt.scene)?.ChangeTracker.MarkChanged(evt.instanceId);
                        break;
                    }
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                    {
                        stream.GetDestroyGameObjectHierarchyEvent(i, out var evt);
                        GetLiveConversion(evt.scene)?.ChangeTracker.MarkRemoved(evt.instanceId);
                        break;
                    }
                    case ObjectChangeKind.CreateAssetObject:
                    {
                        stream.GetCreateAssetObjectEvent(i, out var evt);
                        MarkAssetChanged(evt.instanceId, evt.scene);
                        break;
                    }
                    case ObjectChangeKind.DestroyAssetObject:
                    {
                        stream.GetDestroyAssetObjectEvent(i, out var evt);
                        MarkAssetChanged(evt.instanceId, evt.scene);
                        break;
                    }
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                    {
                        stream.GetChangeAssetObjectPropertiesEvent(i, out var evt);
                        MarkAssetChanged(evt.instanceId, evt.scene);
                        break;
                    }
                    case ObjectChangeKind.UpdatePrefabInstances:
                    {
                        stream.GetUpdatePrefabInstancesEvent(i, out var evt);
                        var diffGenerator = GetLiveConversion(evt.scene);
                        if (diffGenerator != null)
                        {
                            for (int k = 0; k < evt.instanceIds.Length; k++)
                                diffGenerator.ChangeTracker.MarkForceBakeHierarchy(evt.instanceIds[k]);
                        }
                        break;
                    }
                    default:
#if UNITY_2023_3_OR_NEWER
                        // Adding a new enum to the next editor. Temporarily disable this throw for now until it lands.
                        break;
#else
                        throw new ArgumentOutOfRangeException();
#endif
                }
            }
            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
        }

        void MarkAssetChanged(int assetInstanceId, Scene scene)
        {
            if (scene.IsValid())
                GetLiveConversion(scene)?.ChangeTracker.MarkAssetChanged(assetInstanceId);
            else
            {
                foreach (var diffGenerator in _SceneGUIDToLiveConversion.Values)
                    diffGenerator.ChangeTracker.MarkAssetChanged(assetInstanceId);
            }
        }

        public static void GlobalDirtyLiveConversion()
        {
            GlobalDirtyID++;
            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
        }

        static bool IsHotControlActive()
        {
            return GUIUtility.hotControl != 0;
        }

        LiveConversionDiffGenerator GetLiveConversion(Hash128 sceneGUID)
        {
            _SceneGUIDToLiveConversion.TryGetValue(sceneGUID, out var liveConversion);
            return liveConversion;
        }

        LiveConversionDiffGenerator GetLiveConversion(Scene scene)
        {
            return GetLiveConversion(AssetDatabaseCompatibility.PathToGUID(scene.path));
        }

        public void ApplyLiveConversionSceneMsg(LiveConversionSceneMsg msg)
        {
            SetLoadedScenes(msg.LoadedScenes);
            QueueRemovedScenes(msg.RemovedScenes);
        }

        void SetLoadedScenes(NativeArray<Hash128> loadedScenes)
        {
            _LoadedScenes.Clear();
            foreach (var scene in loadedScenes)
            {
                if (scene != default)
                    _LoadedScenes.Add(scene);
            }
        }

        void QueueRemovedScenes(NativeArray<Hash128> removedScenes)
        {
            _RemovedScenes.AddRange(removedScenes);
        }

        public bool HasScene(Hash128 sceneGuid)
        {
            return _LoadedScenes.Contains(sceneGuid);
        }

        public bool HasLoadedScenes()
        {
            return _LoadedScenes.Count > 0;
        }

        void RequestCleanConversion()
        {
            foreach (var liveConversion in _SceneGUIDToLiveConversion.Values)
                liveConversion.RequestCleanConversion();
        }

        void OnLightBakingCompleted()
        {
            foreach (var diffGenerator in _SceneGUIDToLiveConversion.Values)
                diffGenerator.ChangeTracker.MarkLightBakingChanged();
        }

        public void Update(List<LiveConversionChangeSet> changeSets, NativeList<Hash128> loadScenes, NativeList<Hash128> unloadScenes, LiveConversionMode mode)
        {
            if (_LoadedScenes.Count == 0 && _SceneGUIDToLiveConversion.Count == 0 && _RemovedScenes.Length == 0)
                return;

            // If build configuration changed, we need to trigger a full conversion
            if (_ConfigurationGUID != default)
            {
                var buildConfigurationDependencyHash = DotsGlobalSettings.Instance.GetSettingsAsset(_ConfigurationGUID)?.GetHash() ?? default;
                if (_BuildConfigurationArtifactHash != buildConfigurationDependencyHash)
                {
                    _BuildConfigurationArtifactHash = buildConfigurationDependencyHash;
                    RequestCleanConversion();
                }
            }

            if (_PreviousGlobalDirtyID != GlobalDirtyID)
            {
                RequestCleanConversion();
                _PreviousGlobalDirtyID = GlobalDirtyID;
            }

            // By default all scenes need to have m_GameObjectSceneCullingMask, otherwise they won't show up in game view
            _GUIDToEditScene.Clear();
            for (int i = 0; i != EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);

                //this is to avoid trying to get the guid of a scene loaded from a content archive during play mode
                if (!scene.path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase) &&
                    !scene.path.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var sceneGUID = AssetDatabaseCompatibility.PathToGUID(scene.path);

                if (_LoadedScenes.Contains(sceneGUID))
                {
                    if (scene.isLoaded && sceneGUID != default(GUID))
                        _GUIDToEditScene.Add(sceneGUID, scene);
                }
            }

            foreach (var scene in _SceneGUIDToLiveConversion)
            {
                if (!_GUIDToEditScene.ContainsKey(scene.Key))
                    unloadScenes.Add(scene.Key);
            }

            // Process scenes that are no longer loaded
            foreach (var scene in unloadScenes)
            {
                var liveConversion = _SceneGUIDToLiveConversion[scene];
                liveConversion.Dispose();
                _SceneGUIDToLiveConversion.Remove(scene);
                _SentLoadScenes.Remove(scene);
            }
            foreach (var scene in _RemovedScenes)
            {
                if (_SceneGUIDToLiveConversion.TryGetValue(scene, out var liveConversion))
                {
                    liveConversion.Dispose();
                    _SceneGUIDToLiveConversion.Remove(scene);
                }

                unloadScenes.Add(scene);
                _SentLoadScenes.Remove(scene);
            }
            _RemovedScenes.Clear();

            _SentLoadScenes.RemoveWhere(scene => !_LoadedScenes.Contains(scene));

            // Process all scenes that the player needs
            var conversionMode = LiveConversionSettings.Mode;
            var globalArtifactDependencyVersion = AssetDatabase.GlobalArtifactDependencyVersion;
            foreach (var sceneGuid in _LoadedScenes)
            {
                var isLoaded = _GUIDToEditScene.TryGetValue(sceneGuid, out var scene);

                // We are editing with live link. Ensure it is active & up to date
                if (isLoaded)
                {
                    var liveConversion = GetLiveConversion(sceneGuid);

                    if (liveConversion == null || liveConversion.DidRequestUpdate(globalArtifactDependencyVersion))
                        AddLiveConversionChangeSet(ref liveConversion, sceneGuid, changeSets, globalArtifactDependencyVersion, mode);

                    if (conversionMode == LiveConversionSettings.ConversionMode.IncrementalConversionWithDebug)
                    {
                        if (liveConversion != null && liveConversion.DidRequestDebugConversion())
                        {
                            if (IsHotControlActive())
                                EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
                            else
                                liveConversion.DebugIncrementalConversion();
                        }
                    }
                }
                else
                {
                    if (_SentLoadScenes.Add(sceneGuid))
                        loadScenes.Add(sceneGuid);
                }
            }
        }

        void AddLiveConversionChangeSet(ref LiveConversionDiffGenerator liveConversion, Hash128 sceneGUID, List<LiveConversionChangeSet> changeSets, ulong globalArtifactVersion, LiveConversionMode mode)
        {
            //@TODO: need one place that LiveConversionDiffGenerators are managed. UpdateLiveConversion does a Dispose()
            // but this must be paired with membership in _SceneGUIDToLiveConversion. not good to have multiple places
            // doing ownership management.
            //
            // also: when implementing an improvement to this, be sure to deal with exceptions, which can occur
            // during conversion.

            if (liveConversion != null)
                _SceneGUIDToLiveConversion.Remove(sceneGUID);
            try
            {
                var editScene = _GUIDToEditScene[sceneGUID];
                var didChange = LiveConversionDiffGenerator.UpdateLiveConversion(editScene, sceneGUID, ref liveConversion, mode, globalArtifactVersion, _ConfigurationGUID, _SettingAsset, out var changes);
                if (didChange)
                    changeSets.Add(changes);
            }
            finally
            {
                if (liveConversion != null)
                    _SceneGUIDToLiveConversion.Add(sceneGUID, liveConversion);
            }
        }
    }
}
