using System;
using System.IO;
using Unity.Collections;
using Unity.Editor.Bridge;
using Unity.Scenes;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Unity.Entities.Editor
{
    class SceneAssetPostProcessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            using var pooledList = PooledList<GameObjectChangeTrackerEvent>.Make();
            foreach (var asset in movedAssets)
            {
                if (Path.GetExtension(asset) == ".unity")
                {
                    var scene = SceneManager.GetSceneByPath(asset);
                    if (scene is { isSubScene: true, isLoaded: false })
                    {
                        Hash128 sceneGuid = AssetDatabase.GUIDFromAssetPath(asset);
                        foreach (var subScene in SubScene.AllSubScenes)
                        {
                            if (subScene.IsLoaded || subScene.SceneGUID != sceneGuid)
                                continue;

                            pooledList.List.Add(new GameObjectChangeTrackerEvent(subScene.gameObject.GetInstanceID(), GameObjectChangeTrackerEventType.UnloadedSubSceneWasRenamed));
                            break;
                        }
                    }
                    else
                        pooledList.List.Add(new GameObjectChangeTrackerEvent(scene.handle, GameObjectChangeTrackerEventType.SceneWasRenamed));
                }
            }

            using var events = pooledList.List.ToNativeArray(AllocatorManager.Temp);
            GameObjectChangeTrackerBridge.PublishEvents(events);
        }
    }
}
