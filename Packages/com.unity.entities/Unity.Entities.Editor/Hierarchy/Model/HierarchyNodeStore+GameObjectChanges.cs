using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Editor.Bridge;
using Unity.Mathematics;
using Unity.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameObjectChangeTrackerEventType = Unity.Editor.Bridge.GameObjectChangeTrackerEventType;

namespace Unity.Entities.Editor
{
    partial struct HierarchyNodeStore
    {
        public IntegrateGameObjectChangesEnumerator CreateIntegrateGameObjectChangesEnumerator(HierarchyGameObjectChanges changes, SubSceneMap subSceneMap, int batchSize)
        {
            return new IntegrateGameObjectChangesEnumerator(this, subSceneMap, changes, batchSize);
        }

        public struct IntegrateGameObjectChangesEnumerator : IEnumerator
        {
            internal enum Step
            {
                HandleUnloadedScenes,
                HandleLoadedScenes,
                IntegrateChanges,
                Complete
            }

            readonly HierarchyNodeStore m_Hierarchy;
            readonly SubSceneMap m_SubSceneMap;
            readonly HierarchyGameObjectChanges m_Changes;

            NativeArray<GameObjectChangeTrackerEvent> m_Events;

            Step m_Step;

            int m_TotalCount;
            int m_CurrentPosition;
            int m_BatchSize;

            public IntegrateGameObjectChangesEnumerator(HierarchyNodeStore hierarchy, SubSceneMap subSceneMap, HierarchyGameObjectChanges changes, int batchSize)
            {
                m_Hierarchy = hierarchy;
                m_SubSceneMap = subSceneMap;
                m_Changes = changes;
                m_TotalCount = changes.GameObjectChangeTrackerEvents.Length;
                m_CurrentPosition = 0;
                m_BatchSize = batchSize > 0 ? math.min(batchSize, m_TotalCount) : m_TotalCount;
                m_Events = changes.GameObjectChangeTrackerEvents.Length > 0 ? m_Changes.GameObjectChangeTrackerEvents.AsArray() : default;
                m_Step = default;

                SetStep(Step.HandleUnloadedScenes);
            }

            public float Progress => m_TotalCount > 0 ? m_CurrentPosition / (float)m_TotalCount : 0;

            void SetStep(Step step)
            {
                while (true)
                {
                    m_Step = step;

                    switch (step)
                    {
                        case Step.HandleUnloadedScenes:
                            if (m_Changes.UnloadedScenes.Length == 0)
                            {
                                step = Step.HandleLoadedScenes;
                                continue;
                            }
                            return;
                        case Step.HandleLoadedScenes:
                            if (m_Changes.LoadedScenes.Length == 0)
                            {
                                step = Step.IntegrateChanges;
                                continue;
                            }
                            return;
                        case Step.IntegrateChanges:
                            if (m_Changes.GameObjectChangeTrackerEvents.Length == 0)
                            {
                                step = Step.Complete;
                                continue;
                            }
                            return;
                        case Step.Complete:
                            return;
                    }
                }
            }

            public bool MoveNext()
            {
                switch (m_Step)
                {
                    case Step.HandleUnloadedScenes:
                        HandleUnloadedScenes();
                        SetStep(Step.HandleLoadedScenes);
                        return true;
                    case Step.HandleLoadedScenes:
                        HandleLoadedScenes();
                        SetStep(Step.IntegrateChanges);
                        return true;
                    case Step.IntegrateChanges:
                        IntegrateChanges();
                        if (m_CurrentPosition < m_TotalCount)
                            return true;

                        SetStep(Step.Complete);
                        return true;

                    case Step.Complete:
                        return false;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            void HandleUnloadedScenes()
            {
                foreach (var scene in m_Changes.UnloadedScenes)
                {
                    if (!scene.isRemoved || scene.isSubScene)
                        continue;

                    var sceneNode = HierarchyNodeHandle.FromScene(scene);
                    if (m_Hierarchy.Exists(sceneNode))
                        m_Hierarchy.RemoveNode(sceneNode, removeChildrenRecursively: true);
                }
            }

            void HandleLoadedScenes()
            {
                if (m_Changes.LoadedScenes.Length == 0)
                    return;

                var rootGameObjects = new List<GameObject>();
                foreach (var scene in m_Changes.LoadedScenes)
                {
                    // can happen when running test data
                    if (!scene.isLoaded)
                        continue;

                    var node = scene.isSubScene ? m_SubSceneMap.GetSubSceneNodeHandleFromScene(scene) : HierarchyNodeHandle.FromScene(scene);

                    if (!scene.isSubScene)
                    {
                        if (!m_Hierarchy.Exists(node))
                            m_Hierarchy.AddNode(node);

                        m_Hierarchy.SetSortIndex(node, GetSceneIndex(scene));
                    }

                    scene.GetRootGameObjects(rootGameObjects);
                    foreach (var gameObject in rootGameObjects)
                        RecursivelyAddNodes(gameObject, node);
                }
            }

            void IntegrateChanges()
            {
                var batchEnd = math.min(m_CurrentPosition + m_BatchSize, m_TotalCount);
                for (; m_CurrentPosition < batchEnd; m_CurrentPosition++)
                {
                    var changeTrackerEvent = m_Events[m_CurrentPosition];
                    var gameObject = EditorUtility.InstanceIDToObject(changeTrackerEvent.InstanceId) as GameObject;

                    if ((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.SceneOrderChanged) != 0)
                    {
                        for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                        {
                            var scene = SceneManager.GetSceneAt(sceneIndex);
                            if (scene.isSubScene)
                                continue;
                            var sceneNode = HierarchyNodeHandle.FromScene(scene);
                            if (!m_Hierarchy.Exists(sceneNode))
                                continue;

                            m_Hierarchy.SetSortIndex(sceneNode, sceneIndex);
                        }
                    }

                    if ((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.Destroyed) != 0)
                    {
                        var deletedHandle = HierarchyNodeHandle.FromGameObject(changeTrackerEvent.InstanceId);
                        if (m_Hierarchy.Exists(deletedHandle))
                            m_Hierarchy.RemoveNode(deletedHandle);

                        continue;
                    }

                    if (!gameObject

// Invalid scenes should be ignored by default, with the option to fail when encountered.
#if !DOTS_HIERARCHY_FAIL_ON_INVALID_SCENES
                        || !gameObject.scene.IsValid()
#endif
                       )
                        continue;

                    var parent = GetParentNodeHandle(gameObject);
                    if (!m_Hierarchy.Exists(parent))
                    {
                        // Check in the rest of the buffer if the parent is created
                        for (var j = m_CurrentPosition; j < m_TotalCount; j++)
                        {
                            var evt = m_Events[j];
                            if (evt.InstanceId == parent.Index && (evt.EventType & GameObjectChangeTrackerEventType.Destroyed) == 0)
                            {
                                // replace the current event with the one found
                                m_Events[m_CurrentPosition] = evt;

                                // put the current event later in the buffer
                                m_Events[j] = changeTrackerEvent;

                                // rewind
                                m_CurrentPosition--;
                                break;
                            }
                        }

                        continue;
                    }

                    var handle = m_Hierarchy.GetNodeHandle(gameObject, m_SubSceneMap);
                    if ((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.CreatedOrChanged) != 0)
                    {
                        // the node doesn't exist
                        // if the handle is a subscene, we try to create a go node to check if it exists
                        // this would mean a go has been converted to a subscene
                        if (handle.Kind == NodeKind.SubScene)
                        {
                            var goHandle = HierarchyNodeHandle.FromGameObject(gameObject);
                            if (m_Hierarchy.Exists(goHandle))
                                m_Hierarchy.RemoveNode(goHandle);

                            m_Hierarchy.SetParent(handle, parent);
                            m_Hierarchy.SetSortIndex(handle, gameObject.transform.GetSiblingIndex());
                        }

                        if (!m_Hierarchy.Exists(handle))
                        {
                            m_Hierarchy.AddNode(handle, parent);
                            m_Hierarchy.SetSortIndex(handle, gameObject.transform.GetSiblingIndex());
                        }
                    }

                    if (((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.ChangedScene) != 0 || ((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.ChangedParent) != 0)))
                    {
                        if (!m_Hierarchy.Exists(parent))
                        {
                            Debug.Log($"[{changeTrackerEvent.EventType}]: Ignoring GameObject {gameObject.name} ({gameObject.GetInstanceID()}), expected parent {parent} does not exist in the hierarchy");
                        }
                        else
                        {
                            m_Hierarchy.SetParent(handle, parent);
                            m_Hierarchy.SetSortIndex(handle, gameObject.transform.GetSiblingIndex());
                        }
                    }
                    else
                    {
                        if ((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.OrderChanged) != 0)
                        {
                            if (!m_Hierarchy.Exists(handle))
                                continue;
                            m_Hierarchy.SetSortIndex(handle, gameObject.transform.GetSiblingIndex());
                        }
                    }
                }
            }

            public void Reset() => throw new InvalidOperationException($"{nameof(IntegrateGameObjectChangesEnumerator)} can not be reset. Instead a new instance should be used with a new change set.");

            public object Current => null;

            public Step CurrentStep => m_Step;

            HierarchyNodeHandle GetParentNodeHandle(GameObject gameObject)
            {
                if (!gameObject.transform.parent)
                {
// Invalid scenes should be ignored by default, with the option to fail when encountered.
#if DOTS_HIERARCHY_FAIL_ON_INVALID_SCENES
                if (!gameObject.scene.IsValid())
                    throw new System.InvalidOperationException($"GameObject {gameObject.name} ({gameObject.GetInstanceID()}) is at root of a scene marked as not valid");
#endif

                    // No GO parent, parent must be a scene
                    // Note that we know we have a valid scene at this point
                    return gameObject.scene.isSubScene
                        ? m_SubSceneMap.GetSubSceneNodeHandleFromScene(gameObject.scene)
                        : HierarchyNodeHandle.FromScene(gameObject.scene);
                }

                return m_Hierarchy.GetNodeHandle(gameObject.transform.parent.gameObject, m_SubSceneMap);
            }

            void RecursivelyAddNodes(GameObject gameObject, HierarchyNodeHandle parentHandle)
            {
                var handle = m_Hierarchy.GetNodeHandle(gameObject, m_SubSceneMap);

                if (handle.Kind == NodeKind.SubScene)
                {
                    m_Hierarchy.SetParent(handle, parentHandle);
                    m_Hierarchy.SetSortIndex(handle, gameObject.transform.GetSiblingIndex());

                    var subscene = gameObject.GetComponent<SubScene>();
                    if (!subscene.IsLoaded)
                        return;

                    var rootGameObjects = new List<GameObject>();
                    subscene.EditingScene.GetRootGameObjects(rootGameObjects);
                    foreach (var rootGameObject in rootGameObjects)
                    {
                        RecursivelyAddNodes(rootGameObject, handle);
                    }
                }
                else
                {
                    // Other nodes cannot be detected twice
                    if (m_Hierarchy.Exists(handle))
                        return;

                    m_Hierarchy.AddNode(handle, parentHandle);
                    m_Hierarchy.SetSortIndex(handle, gameObject.transform.GetSiblingIndex());

                    foreach (Transform child in gameObject.transform)
                    {
                        RecursivelyAddNodes(child.gameObject, handle);
                    }
                }
            }

            static int GetSceneIndex(Scene scene)
            {
                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    if (SceneManager.GetSceneAt(i) == scene)
                        return i;
                }

                return 0;
            }
        }

        HierarchyNodeHandle GetNodeHandle(GameObject gameObject, SubSceneMap subSceneMap)
        {
            if (gameObject.TryGetComponent<SubScene>(out var subScene) && subScene.SceneGUID != default && subSceneMap.TryGetSubSceneNodeHandle(subScene, out var handle))
                return handle;

            return HierarchyNodeHandle.FromGameObject(gameObject);
        }
    }
}
