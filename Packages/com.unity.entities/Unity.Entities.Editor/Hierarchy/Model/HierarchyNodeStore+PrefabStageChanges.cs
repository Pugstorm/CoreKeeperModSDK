using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GameObjectChangeTrackerEventType = Unity.Editor.Bridge.GameObjectChangeTrackerEventType;

namespace Unity.Entities.Editor
{
    partial struct HierarchyNodeStore
    {
        internal void IntegratePrefabStageChanges(HierarchyPrefabStageChanges changes, SubSceneMap subSceneMap)
        {
            if (changes.GameObjectChangeTrackerEvents.Length == 0)
                return;

            var events = changes.GameObjectChangeTrackerEvents.AsArray();

            for (var i = 0; i < events.Length; i++)
            {
                var changeTrackerEvent = events[i];
                var gameObject = EditorUtility.InstanceIDToObject(changeTrackerEvent.InstanceId) as GameObject;

                if ((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.Destroyed) != 0)
                {
                    var deletedHandle = HierarchyNodeHandle.FromGameObject(changeTrackerEvent.InstanceId);

                    if (Exists(deletedHandle))
                        RemoveNode(deletedHandle);

                    continue;
                }

                if (!gameObject)
                    continue;

                HierarchyNodeHandle parent = gameObject.transform.parent ? GetNodeHandle(gameObject.transform.parent.gameObject, subSceneMap) : HierarchyNodeHandle.Root;

                if (null != gameObject.transform.parent)
                {
                    if (!Exists(parent))
                    {
                        // Check in the rest of the buffer if the parent is created
                        for (var j = i; j < events.Length; j++)
                        {
                            var evt = events[j];
                            if (evt.InstanceId == parent.Index && (evt.EventType & GameObjectChangeTrackerEventType.Destroyed) == 0)
                            {
                                // replace the current event with the one found
                                events[i] = evt;
                                // put the current event later in the buffer
                                events[j] = changeTrackerEvent;
                                // rewind
                                i--;
                                break;
                            }
                        }

                        continue;
                    }
                }

                var handle = GetNodeHandle(gameObject, subSceneMap);

                if ((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.CreatedOrChanged) != 0)
                {
                    if (!Exists(handle))
                    {
                        // the node doesn't exist
                        // if the handle is a subscene, we try to create a go node to check if it exists
                        // this would mean a go has been converted to a subscene
                        if (handle.Kind == NodeKind.SubScene)
                        {
                            var goHandle = HierarchyNodeHandle.FromGameObject(gameObject);
                            if (Exists(goHandle))
                                RemoveNode(goHandle);
                        }

                        AddNode(handle, parent);
                        SetSortIndex(handle, gameObject.transform.GetSiblingIndex());

                        SetFlag(handle, HierarchyNodeFlags.IsPrefabStage);

                        var stage = PrefabStageUtility.GetCurrentPrefabStage();

                        if (stage && !stage.IsPartOfPrefabContents(gameObject))
                            SetFlag(handle, HierarchyNodeFlags.Disabled);
                    }
                    else
                    {
                        // Fix for the context menu create. In this case the object is created via the GameObjectChangeTracker
                        // In this case we can just promote the object to be part of the prefab stage.

                        SetFlag(handle, HierarchyNodeFlags.IsPrefabStage);

                        var stage = PrefabStageUtility.GetCurrentPrefabStage();

                        if (stage && !stage.IsPartOfPrefabContents(gameObject))
                            SetFlag(handle, HierarchyNodeFlags.Disabled);
                    }
                }

                if (((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.ChangedScene) != 0 || ((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.ChangedParent) != 0)))
                {
                    if (!Exists(parent))
                    {
                        Debug.Log($"[{changeTrackerEvent.EventType}]: Ignoring GameObject {gameObject.name} ({gameObject.GetInstanceID()}), expected parent {parent} does not exist in the hierarchy");
                    }
                    else
                    {
                        SetParent(handle, parent);
                        SetSortIndex(handle, gameObject.transform.GetSiblingIndex());
                    }
                }
                else
                {
                    if ((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.OrderChanged) != 0)
                    {
                        SetSortIndex(handle, gameObject.transform.GetSiblingIndex());
                    }
                }
            }
        }
    }
}
