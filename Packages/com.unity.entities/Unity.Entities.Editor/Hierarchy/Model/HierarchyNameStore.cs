using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Editor.Bridge;
using Unity.Scenes;
using UnityEditor;
using UnityEngine;
using GameObjectChangeTrackerEventType = Unity.Editor.Bridge.GameObjectChangeTrackerEventType;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The <see cref="HierarchyNameStore"/> struct represents a virtual view over node names.
    /// This can be used to efficiently access the name for a given node based on it's internal storage.
    /// </summary>
    unsafe class HierarchyNameStore : IDisposable
    {
        static readonly string k_UntitledScene = L10n.Tr("Untitled");
        static readonly string k_UnknownSubSceneName = L10n.Tr("<Unknown SubScene>");

        /// <summary>
        /// Bursted string formatting over native strings.
        /// </summary>
        [GenerateTestsForBurstCompatibility]
        internal struct Formatting
        {
            static readonly FixedString64Bytes k_EntityNull;
            static readonly FixedString64Bytes k_EntityFormat;
            static readonly FixedString64Bytes k_HandleFormat;

            static readonly FixedString64Bytes k_EntityNullLowerInvariant;
            static readonly FixedString64Bytes k_EntityLowerInvariantFormat;
            static readonly FixedString64Bytes k_HandleLowerInvariantFormat;

            static Formatting()
            {
                k_EntityNull = (FixedString64Bytes) "Entity.Null";
                k_EntityFormat = (FixedString64Bytes) "Entity({0}:{1})";
                k_HandleFormat = (FixedString64Bytes) "Handle({0}:{1}:{2})";

                k_EntityNullLowerInvariant = (FixedString64Bytes) "entity.null";
                k_EntityLowerInvariantFormat = (FixedString64Bytes) "entity({0}:{1})";
                k_HandleLowerInvariantFormat = (FixedString64Bytes) "handle({0}:{1}:{2})";
            }

            public static void Initialize()
            {
            }

            public static void FormatEntityNull(ref FixedString64Bytes name)
            {
                name = k_EntityNull;
            }

            public static void FormatEntity(in HierarchyNodeHandle handle, ref FixedString64Bytes name)
            {
                name.Clear();
                FixedString32Bytes index = default;
                index.Append(handle.Index);
                FixedString32Bytes version = default;
                version.Append(handle.Version);
                name.AppendFormat(k_EntityFormat, index, version);
            }

            public static void FormatEntityLowerInvariant(in HierarchyNodeHandle handle, ref FixedString64Bytes name)
            {
                name.Clear();
                FixedString32Bytes index = default;
                index.Append(handle.Index);
                FixedString32Bytes version = default;
                version.Append(handle.Version);
                name.AppendFormat(k_EntityLowerInvariantFormat, index, version);
            }

            public static void FormatHandle(in HierarchyNodeHandle handle, ref FixedString64Bytes name)
            {
                name.Clear();
                FixedString32Bytes index = default;
                index.Append(handle.Index);
                FixedString32Bytes version = default;
                version.Append(handle.Version);
                FixedString32Bytes kind = default;
                index.Append((int) handle.Kind);
                name.AppendFormat(k_HandleFormat, kind, index, version);
            }

            public static void FormatHandleLowerInvariant(in HierarchyNodeHandle handle, ref FixedString64Bytes name)
            {
                name.Clear();
                FixedString32Bytes index = default;
                index.Append(handle.Index);
                FixedString32Bytes version = default;
                version.Append(handle.Version);
                FixedString32Bytes kind = default;
                index.Append((int) handle.Kind);
                name.AppendFormat(k_HandleLowerInvariantFormat, kind, index, version);
            }
        }


        World m_World;

        NativeParallelHashMap<HierarchyNodeHandle, FixedString64Bytes> m_Names;
        NativeParallelHashMap<HierarchyNodeHandle, FixedString64Bytes> m_NamesLowerInvariant;

#if !DOTS_DISABLE_DEBUG_NAMES
        EntityNameStorageLowerInvariant m_EntityNameStorageLowerInvariant;
#endif

        internal NativeParallelHashMap<HierarchyNodeHandle, FixedString64Bytes> NameByHandle => m_Names;
        internal NativeParallelHashMap<HierarchyNodeHandle, FixedString64Bytes> NameByHandleLowerInvariant => m_NamesLowerInvariant;

#if !DOTS_DISABLE_DEBUG_NAMES
#if ENTITY_STORE_V1
        internal EntityName* NameByEntity => m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->NameByEntity;
#else
        internal EntityNameStoreAccess NameStoreAccess => m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->NameStoreAccess;
#endif
        internal EntityNameStorageLowerInvariant EntityNameStorageLowerInvariant => m_EntityNameStorageLowerInvariant;
#endif

        public HierarchyNameStore(Allocator allocator)
        {
            Formatting.Initialize();

            m_Names = new NativeParallelHashMap<HierarchyNodeHandle, FixedString64Bytes>(16, allocator);
            m_NamesLowerInvariant = new NativeParallelHashMap<HierarchyNodeHandle, FixedString64Bytes>(16, allocator);
#if !DOTS_DISABLE_DEBUG_NAMES
            m_EntityNameStorageLowerInvariant = new EntityNameStorageLowerInvariant(EntityNameStorage.kMaxChars, allocator);
#endif
        }

        public void Dispose()
        {
            m_Names.Dispose();
            m_NamesLowerInvariant.Dispose();
#if !DOTS_DISABLE_DEBUG_NAMES
            m_EntityNameStorageLowerInvariant.Dispose();
#endif
        }

        public void SetWorld(World world)
        {
            m_World = world;
            m_Names.Clear();
            m_NamesLowerInvariant.Clear();
        }

        public bool HasName(in HierarchyNodeHandle handle)
        {
            switch (handle.Kind)
            {
                case NodeKind.Entity:
                {
#if !DOTS_DISABLE_DEBUG_NAMES
                    if (!m_World.EntityManager.GetCheckedEntityDataAccess()->Exists(handle.ToEntity()))
                        return false;

                    return m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->GetEntityNameByEntityIndex(handle.Index).Index > 0;
#else
                    return false;
#endif // !DOTS_DISABLE_DEBUG_NAMES
                }

                default:
                {
                    return m_Names.ContainsKey(handle);
                }
            }
        }

        public void GetName(in HierarchyNodeHandle handle, ref FixedString64Bytes name)
        {
            switch (handle.Kind)
            {
                case NodeKind.Entity:
                {
                    var entity = handle.ToEntity();

                    if (null == m_World || !m_World.IsCreated || entity == Entity.Null)
                    {
                        Formatting.FormatEntityNull(ref name);
                        return;
                    }

#if !DOTS_DISABLE_DEBUG_NAMES
                    var entityComponentStore = m_World.EntityManager.GetCheckedEntityDataAccessExclusive()->EntityComponentStore;
                    var entry = entityComponentStore->GetEntityNameByEntityIndex(handle.Index);

                    if (entry.Index != 0 && entityComponentStore->Exists(handle.ToEntity()))
                    {
                        entry.ToFixedString(ref name);
                        return;
                    }
#endif // !DOTS_DISABLE_DEBUG_NAMES

                    Formatting.FormatEntity(handle, ref name);

                    break;
                }

                case NodeKind.SubScene:
                {
                    if (!m_Names.TryGetValue(handle, out name))
                    {
                        name = k_UnknownSubSceneName;
                    }

                    break;
                }

                default:
                {
                    if (!m_Names.TryGetValue(handle, out name))
                    {
                        Formatting.FormatHandle(handle, ref name);
                    }

                    break;
                }
            }
        }

        public void SetName(in HierarchyNodeHandle handle, string name)
        {
            switch (handle.Kind)
            {
                case NodeKind.Entity:
                    throw new InvalidOperationException($"Unable to assign a name to an entity in the hierarchy. This must be done through {nameof(EntityManager)}.{nameof(EntityManager.SetName)}.");
                default:
                    FixedString64Bytes fs = default;
                    fs.CopyFromTruncated(name);
                    m_Names[handle] = fs;
                    m_NamesLowerInvariant[handle] = FixedStringUtility.ToLower(fs);
                    break;
            }
        }

        public void RemoveName(in HierarchyNodeHandle handle)
        {
            switch (handle.Kind)
            {
                case NodeKind.Entity:
                    throw new InvalidOperationException($"Unable to remove a name for an entity in the hierarchy. This must be done through {nameof(EntityManager)}.{nameof(EntityManager.SetName)}.");
                default:
                    m_Names.Remove(handle);
                    m_NamesLowerInvariant.Remove(handle);
                    break;
            }
        }

        public void IntegrateGameObjectChanges(HierarchyGameObjectChanges changes)
        {
            foreach (var scene in changes.UnloadedScenes)
            {
                if (!scene.isRemoved || scene.isSubScene)
                    continue;

                RemoveName(HierarchyNodeHandle.FromScene(scene));
            }

            var rootGameObjects = new List<GameObject>();
            foreach (var scene in changes.LoadedScenes)
            {
                if (!scene.isSubScene)
                    SetName(HierarchyNodeHandle.FromScene(scene), string.IsNullOrEmpty(scene.name) ? k_UntitledScene : scene.name);

                if (!scene.isLoaded)
                    continue;

                scene.GetRootGameObjects(rootGameObjects);
                foreach (var rootGameObject in rootGameObjects)
                {
                    RecursivelyAddGameObjectNames(rootGameObject);
                }
            }

            IntegrateGameObjectChangeEvents(changes.GameObjectChangeTrackerEvents.AsArray());
        }

        public void IntegratePrefabStageChanges(HierarchyPrefabStageChanges changes)
        {
            IntegrateGameObjectChangeEvents(changes.GameObjectChangeTrackerEvents.AsArray());
        }

        void IntegrateGameObjectChangeEvents(NativeArray<GameObjectChangeTrackerEvent> events)
        {
            foreach (var changeTrackerEvent in events)
            {
                if ((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.SceneWasRenamed) != 0)
                {
                    var scene = EditorSceneManagerBridge.GetSceneByHandle(changeTrackerEvent.InstanceId);
                    var sceneName = string.IsNullOrEmpty(scene.name) ? k_UntitledScene : scene.name;
                    if (!scene.isSubScene)
                        SetName(HierarchyNodeHandle.FromScene(scene), sceneName);
                }
                else if((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.Destroyed) != 0)
                {
                    RemoveName(HierarchyNodeHandle.FromGameObject(changeTrackerEvent.InstanceId));
                }
                else if ((changeTrackerEvent.EventType & GameObjectChangeTrackerEventType.CreatedOrChanged) != 0)
                {
                    var idToObject = EditorUtility.InstanceIDToObject(changeTrackerEvent.InstanceId) as GameObject;
                    if (!idToObject)
                        continue;

                    SetName(HierarchyNodeHandle.FromGameObject(changeTrackerEvent.InstanceId), idToObject.name);
                }
            }
        }

        void RecursivelyAddGameObjectNames(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<SubScene>(out var subScene) && subScene.SceneGUID != default)
            {
                var rootGameObjects = new List<GameObject>();
                if (!subScene.IsLoaded)
                    return;

                subScene.EditingScene.GetRootGameObjects(rootGameObjects);
                foreach (var rootGameObject in rootGameObjects)
                {
                    RecursivelyAddGameObjectNames(rootGameObject);
                }
            }
            else
            {
                SetName(HierarchyNodeHandle.FromGameObject(gameObject.GetInstanceID()), gameObject.name);
                foreach (Transform child in gameObject.transform)
                {
                    RecursivelyAddGameObjectNames(child.gameObject);
                }
            }
        }

    }
}
