using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Editor.Bridge;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Entities.Editor
{
    readonly struct HierarchyGameObjectChanges : IDisposable
    {
        public readonly NativeList<Scene> LoadedScenes;
        public readonly NativeList<UnloadedScene> UnloadedScenes;
        public readonly NativeList<GameObjectChangeTrackerEvent> GameObjectChangeTrackerEvents;

        public bool HasChanges() => LoadedScenes.Length > 0 || UnloadedScenes.Length > 0 || GameObjectChangeTrackerEvents.Length > 0;

        public HierarchyGameObjectChanges(Allocator allocator)
        {
            LoadedScenes = new NativeList<Scene>(allocator);
            UnloadedScenes = new NativeList<UnloadedScene>(allocator);
            GameObjectChangeTrackerEvents = new NativeList<GameObjectChangeTrackerEvent>(allocator);
        }

        public void Clear()
        {
            LoadedScenes.Clear();
            UnloadedScenes.Clear();
            GameObjectChangeTrackerEvents.Clear();
        }

        public void Dispose()
        {
            LoadedScenes.Dispose();
            UnloadedScenes.Dispose();
            GameObjectChangeTrackerEvents.Dispose();
        }
    }

    readonly struct UnloadedScene : IEquatable<UnloadedScene>
    {
        readonly Scene m_Scene;

        public readonly bool isSubScene;
        public string path => m_Scene.path;
        public readonly int handle;
        public readonly bool isRemoved;

        public UnloadedScene(Scene scene, bool removed)
        {
            m_Scene = scene;
            isRemoved = removed;
            isSubScene = scene.isSubScene;
            handle = scene.handle;
        }

        public override int GetHashCode()
            => handle.GetHashCode();

        public bool Equals(UnloadedScene other)
            => handle == other.handle;

        public override bool Equals(object obj)
            => obj is UnloadedScene other && Equals(other);

        public static implicit operator UnloadedScene(Scene scene)
            => new UnloadedScene(scene, false);
    }

    class HierarchyGameObjectChangeTracker : IDisposable
    {
        const int k_MinimumQueueLengthForBackgroundUpdate = 1000;

        readonly HashSet<Scene> m_LoadedScenes = new HashSet<Scene>();
        readonly HashSet<UnloadedScene> m_UnloadedScenes = new HashSet<UnloadedScene>();
        readonly NativeList<GameObjectChangeTrackerEvent> m_ChangeEventsQueue;
        readonly NativeParallelHashMap<int,int> m_ChangeEventsIndex;
        NativeArray<GameObjectChangeTrackerEvent> m_SingleGameObjectChangeTrackerEvents;

        public HierarchyGameObjectChangeTracker(Allocator allocator)
        {
            m_ChangeEventsQueue = new NativeList<GameObjectChangeTrackerEvent>(2048, allocator);
            m_ChangeEventsIndex = new NativeParallelHashMap<int, int>(2048, allocator);

            m_SingleGameObjectChangeTrackerEvents = new NativeArray<GameObjectChangeTrackerEvent>(1, Allocator.Persistent);

            GameObjectChangeTrackerBridge.GameObjectsChanged += OnGameObjectsChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorSceneManager.newSceneCreated += OnNewSceneCreated;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorSceneManager.sceneManagerSetupRestored += OnSceneManagerSetupRestored;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            Clear();
        }

        void OnSceneManagerSetupRestored(Scene[] scenes)
        {
            foreach (var scene in scenes)
            {
                if (scene.isLoaded)
                    m_LoadedScenes.Add(scene);
            }
        }

        public void Clear()
        {
            m_LoadedScenes.Clear();
            m_UnloadedScenes.Clear();
            m_ChangeEventsQueue.Clear();
            m_ChangeEventsIndex.Clear();

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                if (scene.isSubScene)
                    continue;

                m_LoadedScenes.Add(scene);
            }
        }

        void OnGameObjectsChanged(in NativeArray<GameObjectChangeTrackerEvent> events)
            => new MergeEventsJob { EventsIndex = m_ChangeEventsIndex, Events = m_ChangeEventsQueue, EventsToAdd = events }.Run();

        public void Dispose()
        {
            m_ChangeEventsQueue.Dispose();
            m_ChangeEventsIndex.Dispose();
            m_SingleGameObjectChangeTrackerEvents.Dispose();

            GameObjectChangeTrackerBridge.GameObjectsChanged -= OnGameObjectsChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            EditorSceneManager.newSceneCreated -= OnNewSceneCreated;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneManagerSetupRestored -= OnSceneManagerSetupRestored;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            m_UnloadedScenes.Remove(scene);
            m_LoadedScenes.Add(scene);
        }

        void OnSceneSaved(Scene scene)
        {
            // This callback is only used when a new scene is saved.

            // We detect subScene renames via SceneAssetPostProcessor.
            if (scene.isSubScene)
                return;

            m_SingleGameObjectChangeTrackerEvents[0] = new GameObjectChangeTrackerEvent(scene.handle, GameObjectChangeTrackerEventType.SceneWasRenamed);
            GameObjectChangeTrackerBridge.PublishEvents(m_SingleGameObjectChangeTrackerEvents);
        }

        void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            m_UnloadedScenes.Remove(scene);
            m_LoadedScenes.Add(scene);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            m_UnloadedScenes.Remove(scene);
            m_LoadedScenes.Add(scene);
        }

        void OnSceneClosing(Scene scene, bool removingScene)
        {
            m_UnloadedScenes.Add(new UnloadedScene(scene, removingScene));
            m_LoadedScenes.Remove(scene);
        }

        void OnSceneUnloaded(Scene scene)
        {
            m_UnloadedScenes.Add(new UnloadedScene(scene, true));
            m_LoadedScenes.Remove(scene);
        }

        public void GetChanges(HierarchyGameObjectChanges changes, bool isBackgroundUpdate)
        {
            changes.Clear();

            if (m_LoadedScenes.Count > 0)
            {
                foreach (var scene in m_LoadedScenes)
                    changes.LoadedScenes.Add(scene);

                m_LoadedScenes.Clear();
            }

            if (m_UnloadedScenes.Count > 0)
            {
                foreach (var scene in m_UnloadedScenes)
                    changes.UnloadedScenes.Add(scene);

                m_UnloadedScenes.Clear();
            }

            if (m_ChangeEventsQueue.Length > (isBackgroundUpdate ? k_MinimumQueueLengthForBackgroundUpdate : 0))
            {
                changes.GameObjectChangeTrackerEvents.CopyFrom(m_ChangeEventsQueue.AsArray());
                m_ChangeEventsQueue.Clear();
                m_ChangeEventsIndex.Clear();
            }
        }

        [BurstCompile]
        internal struct MergeEventsJob : IJob
        {
            public NativeList<GameObjectChangeTrackerEvent> Events;
            public NativeParallelHashMap<int,int> EventsIndex;

            [ReadOnly] public NativeArray<GameObjectChangeTrackerEvent> EventsToAdd;

            public unsafe void Execute()
            {
                var eventsToAddPtr = (GameObjectChangeTrackerEvent*)EventsToAdd.GetUnsafeReadOnlyPtr();

                for (var i = 0; i < EventsToAdd.Length; i++)
                {
                    ref var evtToAdd = ref eventsToAddPtr[i];
                    if (!EventsIndex.TryGetValue(evtToAdd.InstanceId, out var existingIndex))
                    {
                        EventsIndex.Add(evtToAdd.InstanceId, Events.Length);
                        Events.Add(evtToAdd);
                    }
                    else
                    {
                        ref var existingEvent = ref Events.ElementAt(existingIndex);
                        if ((existingEvent.EventType & evtToAdd.EventType) != evtToAdd.EventType)
                        {
                            Events[existingIndex] = new GameObjectChangeTrackerEvent(existingEvent.InstanceId, existingEvent.EventType | evtToAdd.EventType);
                        }
                    }
                }
            }
        }
    }
}
