using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Editor.Bridge;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Entities.Editor
{
    struct HierarchyPrefabStageChanges : IDisposable
    {
        public NativeList<GameObjectChangeTrackerEvent> GameObjectChangeTrackerEvents;

        public HierarchyPrefabStageChanges(Allocator allocator)
        {
            GameObjectChangeTrackerEvents = new NativeList<GameObjectChangeTrackerEvent>(allocator);
        }

        public void Dispose()
        {
            GameObjectChangeTrackerEvents.Dispose();
        }

        public void Clear()
        {
            GameObjectChangeTrackerEvents.Clear();
        }
    }

    class HierarchyPrefabStageChangeTracker : IDisposable
    {
        NativeParallelHashSet<int> m_InstanceId;
        NativeParallelHashMap<int, int> m_Parents;
        NativeParallelHashSet<int> m_Existing;

        List<int> m_Removed = new List<int>();

        public HierarchyPrefabStageChangeTracker(Allocator allocator)
        {
            m_InstanceId = new NativeParallelHashSet<int>(16, allocator);
            m_Parents = new NativeParallelHashMap<int, int>(16, allocator);
            m_Existing = new NativeParallelHashSet<int>(16, allocator);
        }

        public void Clear()
        {
            m_InstanceId.Clear();
            m_Parents.Clear();
            m_Existing.Clear();
        }

        public void Dispose()
        {
            m_InstanceId.Dispose();
            m_Parents.Dispose();
            m_Existing.Dispose();
        }

        public void GetChanges(HierarchyPrefabStageChanges changes)
        {
            changes.Clear();

            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            m_Existing.Clear();
            m_Removed.Clear();

            var events = changes.GameObjectChangeTrackerEvents;

            if (null != stage)
            {
                var root = stage.prefabContentsRoot.transform.parent ? stage.prefabContentsRoot.transform.parent.gameObject : stage.prefabContentsRoot;
                GatherChangesRecursive(root, events, m_Existing);
            }

            foreach (var id in m_InstanceId)
            {
                if (!m_Existing.Contains(id))
                    m_Removed.Add(id);
            }

            foreach (var id in m_Removed)
            {
                m_InstanceId.Remove(id);
                events.Add(new GameObjectChangeTrackerEvent(id, GameObjectChangeTrackerEventType.Destroyed));
            }

            if (null == stage)
            {
                m_InstanceId.Clear();
                m_Parents.Clear();
            }
        }

        void GatherChangesRecursive(GameObject obj, NativeList<GameObjectChangeTrackerEvent> events, NativeParallelHashSet<int> existing)
        {
            var instanceId = obj.GetInstanceID();

            existing.Add(instanceId);

            if (!m_InstanceId.Contains(instanceId))
            {
                events.Add(new GameObjectChangeTrackerEvent(instanceId, GameObjectChangeTrackerEventType.CreatedOrChanged));
                m_InstanceId.Add(instanceId);
            }

            var parentId = obj.transform.parent ? obj.transform.parent.gameObject.GetInstanceID() : 0;

            if (m_Parents.TryGetValue(instanceId, out var currentParentId))
            {
                if (currentParentId != parentId)
                {
                    events.Add(new GameObjectChangeTrackerEvent(instanceId, GameObjectChangeTrackerEventType.ChangedParent));
                    m_Parents[instanceId] = parentId;
                }
            }
            else if (parentId != 0)
            {
                events.Add(new GameObjectChangeTrackerEvent(instanceId, GameObjectChangeTrackerEventType.ChangedParent));
                m_Parents.Add(instanceId, parentId);
            }

            foreach (Transform child in obj.transform)
                GatherChangesRecursive(child.gameObject, events, existing);
        }
    }
}
