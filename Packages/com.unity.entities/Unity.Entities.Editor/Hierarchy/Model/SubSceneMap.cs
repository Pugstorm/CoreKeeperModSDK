using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Editor.Bridge;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEngine.Assertions;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

namespace Unity.Entities.Editor
{
    class SubSceneMap : IDisposable
    {
        static int s_SubSceneCounter = 0;

        Dictionary<SceneTag, (Entity, SceneReference)> m_SceneTagToSceneReference = new();

        Dictionary<Hash128, SubScene> m_PreviousSceneGuidToSubScene = new();
        Dictionary<Hash128, SubScene> m_SceneGuidToSubScene = new();
        Dictionary<Entity, SubScene> m_EntityToSubScene = new();
        Dictionary<Hash128, HierarchyNodeHandle> m_SubScenes = new();
        Dictionary<Entity, HierarchyNodeHandle> m_EntityScenes = new();

        public void IntegrateChanges(World world, HierarchyNodeStore nodeStore, HierarchyNameStore nameStore, SubSceneChangeTracker.SubSceneMapChanges changes)
        {
            using var tracker = EditorPerformanceTrackerBridge.CreateEditorPerformanceTracker($"{nameof(SubSceneMap)}.{nameof(IntegrateChanges)}");
            using var handlesToRemovePoolHandle = HashSetPool<HierarchyNodeHandle>.Get(out var handlesToRemove);

            foreach (var sceneTag in changes.RemovedSceneTags)
            {
                m_SceneTagToSceneReference.Remove(sceneTag);
            }

            foreach (var sceneTag in changes.CreatedSceneTags)
            {
                if (!world.EntityManager.Exists(sceneTag.SceneEntity) || !world.EntityManager.HasComponent<SceneEntityReference>(sceneTag.SceneEntity))
                    continue;

                var sceneEntityReference = world.EntityManager.GetComponentData<SceneEntityReference>(sceneTag.SceneEntity).SceneEntity;
                if (!world.EntityManager.Exists(sceneEntityReference) || !world.EntityManager.HasComponent<SceneReference>(sceneEntityReference))
                    continue;

                m_SceneTagToSceneReference[sceneTag] = (sceneEntityReference, world.EntityManager.GetComponentData<SceneReference>(sceneEntityReference));
            }

            var allSubScenes = (List<SubScene>)SubScene.AllSubScenes;
            foreach (var subScene in allSubScenes)
            {
                m_SceneGuidToSubScene[subScene.SceneGUID] = subScene;
            }

            foreach (var (entity, sceneReference) in changes.CreatedEntityScenes)
            {
                var isSubScene = world.EntityManager.HasComponent<SubScene>(entity);
                Assert.IsFalse(m_EntityScenes.ContainsKey(entity));
                HierarchyNodeHandle handle;
                if (isSubScene)
                {
                    var subScene = world.EntityManager.GetComponentObject<SubScene>(entity);
                    if (subScene.SceneGUID == default)
                        continue;

                    if (!m_SceneGuidToSubScene.ContainsKey(subScene.SceneGUID))
                    {
                        // We have entity scenes pointing to subscenes that are not currently known by AllSubScenes
                        // to maintain a coherent state we add them to the current state.
                        m_SceneGuidToSubScene[subScene.SceneGUID] = subScene;
                    }

                    if (!m_SubScenes.TryGetValue(subScene.SceneGUID, out handle))
                    {
                        m_EntityToSubScene[entity] = subScene;
                        handle = AddSubScene(subScene, nodeStore, nameStore);
                    }
                }
                else // it's an entity scene
                {
                    if (sceneReference.SceneGUID == default)
                        continue;
                    handle = HierarchyNodeHandle.FromSubScene(s_SubSceneCounter++);
                    nodeStore.AddNode(handle);
                    nameStore.SetName(handle,  $"Entity Scene {sceneReference.SceneGUID}"); // todo find entity scene name?
                }

                m_EntityScenes.Add(entity, handle);
            }

            foreach (var (entity, _ /*sceneReference*/) in changes.RemovedEntityScenes)
            {
                var isSubScene = m_EntityToSubScene.TryGetValue(entity, out var subScene);
                if (isSubScene)
                {
                    m_EntityToSubScene.Remove(entity);
                    // do not remove the subscene node yet, the SubScene might still exists
                }
                else
                {
                    // Entity scene must exist
                    var handle = m_EntityScenes[entity];
                    handlesToRemove.Add(handle);
                }
                m_EntityScenes.Remove(entity);
            }

            foreach (var sceneGuid in changes.CreatedSubScenes)
            {
                var subScene = m_SceneGuidToSubScene[sceneGuid];

                // GameObject to SubScene transition
                var gameObjectHandle = HierarchyNodeHandle.FromGameObject(subScene.gameObject);
                HierarchyNodeHandle parentHandle = default;
                if (nodeStore.Exists(gameObjectHandle))
                {
                    parentHandle = nodeStore.GetParent(gameObjectHandle);
                    nodeStore.RemoveNode(gameObjectHandle);
                }

                // SubScene could have been already created by entity
                if (!m_SubScenes.TryGetValue(subScene.SceneGUID, out var handle))
                {
                    handle = AddSubScene(subScene, nodeStore, nameStore);
                }

                if (parentHandle != default)
                    nodeStore.SetParent(handle, parentHandle);
            }

            foreach (var sceneGuid in changes.RemovedSubScenes)
            {
                var handle = m_SubScenes[sceneGuid];
                handlesToRemove.Add(handle);
                m_SubScenes.Remove(sceneGuid);
            }

            // swap by deconstruction 🤯
            (m_PreviousSceneGuidToSubScene, m_SceneGuidToSubScene) = (m_SceneGuidToSubScene, m_PreviousSceneGuidToSubScene);
            m_SceneGuidToSubScene.Clear();

            // Remove unused handles
            if (handlesToRemove.Count == 0)
                return;

            using var poolHandle = HashSetPool<HierarchyNodeHandle>.Get(out var handles);
            foreach (var handle in m_SubScenes.Values)
            {
                handles.Add(handle);
            }

            foreach (var handle in m_EntityScenes.Values)
            {
                handles.Add(handle);
            }

            foreach (var handle in handlesToRemove)
            {
                if (handles.Contains(handle))
                    continue;

                nodeStore.RemoveNode(handle, removeChildrenRecursively: true);
                nameStore.RemoveName(handle);
            }
        }

        HierarchyNodeHandle AddSubScene(SubScene subScene, HierarchyNodeStore nodeStore, HierarchyNameStore nameStore)
        {
            var handle = HierarchyNodeHandle.FromSubScene(s_SubSceneCounter++);
            Assert.IsFalse(nodeStore.Exists(handle));
            nodeStore.AddNode(handle);
            nameStore.SetName(handle, subScene.SceneName);

            m_SubScenes[subScene.SceneGUID] = handle;

            return handle;
        }

        public void Dispose()
        {
        }

        public NativeParallelHashMap<HierarchyNodeHandle, bool /*IsLoaded*/> GetSubSceneStateMap()
        {
            // Map of SubScene/EntityScene node to state being loaded or not

            var map = new NativeParallelHashMap<HierarchyNodeHandle, bool>(m_SubScenes.Count, Allocator.TempJob);
            foreach (var (subSceneGuid, handle) in m_SubScenes)
            {
                //When laoding a new scene, the SubScene list may contains more entries than the m_PreviousSceneGuidToSubScene because
                //the m_SubScene list is updated only when the SubScene change is detected. In this spurious frame, the
                //m_PreviousSceneGuidToSubScene may not contains yet the guid of the new loaded sub-scenes. As such, a try-get
                //here is the only possible choice, unless we keep the m_PreviousSceneGuidToSubScene and m_SubScenes in sync (that it is
                //not the intent).
                if (m_PreviousSceneGuidToSubScene.TryGetValue(subSceneGuid, out var subScene))
                    map[handle] = subScene is not null && subScene.IsLoaded;
            }
            return map;
        }

        public void Clear()
        {
            m_SceneTagToSceneReference.Clear();
            m_PreviousSceneGuidToSubScene.Clear();
            m_SceneGuidToSubScene.Clear();
            m_EntityToSubScene.Clear();
            m_SubScenes.Clear();
            m_EntityScenes.Clear();
        }

        public SubSceneLoadedState GetSubSceneStateImmediate(SubScene subScene, World world)
        {
            if (subScene is null || !m_SubScenes.ContainsKey(subScene.SceneGUID))
                return SubSceneLoadedState.None;

            var entityScene = Entity.Null;
            foreach (var entityToSubScene in m_EntityToSubScene)
            {
                if (entityToSubScene.Value != subScene) continue;

                entityScene = entityToSubScene.Key;
                break;
            }

            var unitySceneIsLoaded = subScene.IsLoaded;
            var entitySceneIsLoaded = entityScene != Entity.Null && SceneSystem.IsSceneLoaded(world.Unmanaged, entityScene);

            return (unitySceneIsLoaded, entitySceneIsLoaded, liveConversionEnabled: LiveConversionEditorSettings.LiveConversionEnabled) switch
            {
                (unitySceneIsLoaded: true,  entitySceneIsLoaded: _,     liveConversionEnabled: true)  => SubSceneLoadedState.LiveConverted,
                (unitySceneIsLoaded: true,  entitySceneIsLoaded: _,     liveConversionEnabled: false) => SubSceneLoadedState.Opened,
                (unitySceneIsLoaded: false, entitySceneIsLoaded: true,  liveConversionEnabled: _)     => SubSceneLoadedState.Closed,
                (unitySceneIsLoaded: false, entitySceneIsLoaded: false, liveConversionEnabled: _)     => SubSceneLoadedState.NotLoaded,
            };
        }

        public SubSceneLoadedState GetSubSceneStateImmediate(HierarchyNodeHandle handle, World world)
        {
            foreach (var (sceneGuid, subSceneHandle) in m_SubScenes)
            {
                if (subSceneHandle == handle)
                    return GetSubSceneStateImmediate(m_PreviousSceneGuidToSubScene[sceneGuid], world);
            }

            return SubSceneLoadedState.None;
        }

        public SubScene GetSubSceneMonobehaviourFromHandle(HierarchyNodeHandle handle)
        {
            Assert.IsTrue(handle.Kind == NodeKind.SubScene);
            foreach (var (subScene, h) in m_SubScenes)
            {
                if (handle == h)
                    return m_PreviousSceneGuidToSubScene[subScene];
            }

            return null;
        }

        public bool TryGetSubSceneNodeHandle(SubScene subScene, out HierarchyNodeHandle handle) =>
            m_SubScenes.TryGetValue(subScene.SceneGUID, out handle);

        public HierarchyNodeHandle GetSubSceneNodeHandleFromScene(Scene scene)
        {
            foreach (var (subSceneSceneGuid, subScene) in m_PreviousSceneGuidToSubScene)
            {
                if (subScene == null)
                    continue;
                if (subScene.EditingScene == scene)
                {
                    return m_SubScenes[subScene.SceneGUID];
                }
            }

            throw new KeyNotFoundException();
        }

        public Entity GetEntityFromHandle(HierarchyNodeHandle handle)
        {
            foreach (var (entity, hierarchyNodeHandle) in m_EntityScenes)
            {
                if (handle == hierarchyNodeHandle)
                    return entity;
            }

            return Entity.Null;
        }

        public NativeParallelHashMap<SceneTag, HierarchyNodeHandle> GetSceneTagToSubSceneHandleMap()
        {
            var map = new NativeParallelHashMap<SceneTag, HierarchyNodeHandle>(m_EntityScenes.Count, Allocator.TempJob);
            foreach (var (sceneTag, (entity, _)) in m_SceneTagToSceneReference)
            {
                if (entity == Entity.Null)
                    continue;

                map[sceneTag] = m_EntityScenes[entity];
            }

            return map;
        }
    }

    enum SubSceneLoadedState
    {
        None,
        Closed,
        NotLoaded,
        LiveConverted,
        Opened
    }
}
