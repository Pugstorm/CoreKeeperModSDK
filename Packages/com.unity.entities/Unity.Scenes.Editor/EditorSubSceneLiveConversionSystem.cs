using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Conversion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes.Editor
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(LiveConversionEditorSystemGroup))]
    partial class EditorSubSceneLiveConversionSystem : SystemBase
    {
        LiveConversionConnection         _EditorLiveConversion;
        LiveConversionPatcher            _Patcher;
        LiveConversionSceneChangeTracker _SceneChangeTracker;

        // Temp data cached to reduce gc allocations
        List<LiveConversionChangeSet>    _ChangeSets;
        NativeList<Hash128>        _UnloadScenes;
        NativeList<Hash128>        _LoadScenes;

        System.Diagnostics.Stopwatch m_Watch;
        internal double MillisecondsTakenByUpdate { get; set; }

        internal World GetConvertedWorldForEntity(Entity entity)
        {
            if (!EntityManager.HasComponent<SceneSection>(entity))
                return null;
            var section = EntityManager.GetSharedComponent<SceneSection>(entity);
            return _EditorLiveConversion.GetConvertedWorldForScene(section.SceneGUID);
        }

        internal World GetConvertedWorldForScene(Hash128 sceneGUID)
        {
            return _EditorLiveConversion.GetConvertedWorldForScene(sceneGUID);
        }

        protected override void OnUpdate()
        {
            m_Watch.Restart();
            try
            {
                // We can't initialize live link in OnCreate because other systems might configure BuildConfigurationGUID from OnCreate
                if (_EditorLiveConversion == null)
                    _EditorLiveConversion = new LiveConversionConnection(EntityManager.GetComponentData<SceneSystemData>(World.GetExistingSystem<SceneSystem>()).BuildConfigurationGUID);

                try
                {
                    if (_SceneChangeTracker.GetSceneMessage(out var msg))
                    {
                        using (msg)
                        {
                            _EditorLiveConversion.ApplyLiveConversionSceneMsg(msg);
                        }
                    }

                    _EditorLiveConversion.Update(_ChangeSets, _LoadScenes, _UnloadScenes, LiveConversionEditorSettings.LiveConversionMode);

                    // Unload scenes that are no longer being edited / need to be reloaded etc
                    foreach (var change in _UnloadScenes)
                    {
                        _Patcher.UnloadScene(change);
                    }

                    // Apply changes to scenes that are being edited
                    foreach (var change in _ChangeSets)
                    {
                        try
                        {
                            _Patcher.ApplyPatch(change);
                        }
                        catch (System.Exception exc)
                        {
                            Debug.LogException(exc);
                        }
                    }
                }
                finally
                {
                    _LoadScenes.Clear();

                    foreach (var change in _ChangeSets)
                    {
                        change.Dispose();
                    }
                    _ChangeSets.Clear();

                    _UnloadScenes.Clear();
                }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
                CompanionGameObjectUtility.UpdateLiveConversionCulling(LiveConversionEditorSettings.LiveConversionMode);
#endif

                if (_EditorLiveConversion.HasLoadedScenes())
                {
                    // Configure scene culling masks so that gameobjects & entities are rendered exlusively to each other
                    for (int i = 0; i != EditorSceneManager.sceneCount; i++)
                    {
                        var scene = EditorSceneManager.GetSceneAt(i);

                        // This is to avoid trying to get the guid of a scene loaded from a content archive during play mode
                        var scenepath = scene.path;
                        if (!scenepath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase) &&
                            !scenepath.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase))
                            continue;

                        var sceneGUID = AssetDatabaseCompatibility.PathToGUID(scenepath);
                        if (_EditorLiveConversion.HasScene(sceneGUID))
                        {
                            switch (LiveConversionEditorSettings.LiveConversionMode)
                            {
                                case LiveConversionMode.SceneViewShowsAuthoring:
                                    // Render gameobjects in SceneView but hide them in GameView
                                    EditorSceneManager.SetSceneCullingMask(scene, SceneCullingMasks.MainStageSceneViewObjects);
                                    break;

                                case LiveConversionMode.SceneViewShowsRuntime:
                                    // Hide gameobjects in SceneView and GameView
                                    EditorSceneManager.SetSceneCullingMask(scene, 0);
                                    break;

                                case LiveConversionMode.Disabled:
                                case LiveConversionMode.LiveConvertStandalonePlayer:
                                    // Render gameobjects in SceneView and GameView
                                    EditorSceneManager.SetSceneCullingMask(scene, EditorSceneManager.DefaultSceneCullingMask);
                                    break;

                                default:
                                    Debug.LogError("Missing handling of: " + LiveConversionEditorSettings.LiveConversionMode);
                                    break;
                            }
                        }
                    }
                }
            }
            finally
            {
                m_Watch.Stop();
                MillisecondsTakenByUpdate += m_Watch.Elapsed.TotalMilliseconds;
            }
        }

        protected override void OnCreate()
        {
            m_Watch = new Stopwatch();

            _SceneChangeTracker = new LiveConversionSceneChangeTracker(EntityManager);

            _Patcher = new LiveConversionPatcher(World);
            _UnloadScenes = new NativeList<Hash128>(Allocator.Persistent);
            _LoadScenes = new NativeList<Hash128>(Allocator.Persistent);
            _ChangeSets = new List<LiveConversionChangeSet>();
        }

        protected override void OnDestroy()
        {
            if (_EditorLiveConversion != null)
                _EditorLiveConversion.Dispose();
            _SceneChangeTracker.Dispose();
            _Patcher.Dispose();
            _UnloadScenes.Dispose();
            _LoadScenes.Dispose();
        }
    }
}
