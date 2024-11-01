using System;
using Unity.Collections;
using UnityEditor;

namespace Unity.Editor.Bridge
{
    class GameObjectChangeTrackerBridge
    {
        internal delegate void GameObjectChangeTrackerEventHandler(in NativeArray<GameObjectChangeTrackerEvent> events);

        static event GameObjectChangeTrackerEventHandler GameObjectsChangedUnderlyingEvent;

        internal static event GameObjectChangeTrackerEventHandler GameObjectsChanged
        {
            add
            {
                if (GameObjectsChangedUnderlyingEvent == null)
                    GameObjectChangeTracker.GameObjectsChanged += OnInternalGameObjectsChanged;

                GameObjectsChangedUnderlyingEvent += value;
            }
            remove
            {
                GameObjectsChangedUnderlyingEvent -= value;

                if (GameObjectsChangedUnderlyingEvent == null)
                    GameObjectChangeTracker.GameObjectsChanged -= OnInternalGameObjectsChanged;
            }
        }

        public static void PublishEvents(NativeArray<GameObjectChangeTrackerEvent> events)
            => GameObjectChangeTracker.PublishEvents(events.Reinterpret<UnityEditor.GameObjectChangeTrackerEvent>());

        static void OnInternalGameObjectsChanged(in NativeArray<UnityEditor.GameObjectChangeTrackerEvent> events)
        {
            var reinterpretedEvents = events.Reinterpret<GameObjectChangeTrackerEvent>();
            GameObjectsChangedUnderlyingEvent?.Invoke(reinterpretedEvents);
        }
    }

    readonly struct GameObjectChangeTrackerEvent
    {
        public readonly int InstanceId;
        public readonly GameObjectChangeTrackerEventType EventType;

        public GameObjectChangeTrackerEvent(int instanceId, GameObjectChangeTrackerEventType eventType)
        {
            InstanceId = instanceId;
            EventType = eventType;
        }

        public override string ToString()
            => $"GameObjectChangeTrackerEvent(InstanceId: {InstanceId}, EventType: {EventType})";
    }

    [Flags]
    enum GameObjectChangeTrackerEventType : ushort
    {
        CreatedOrChanged = 1 << 0,
        Destroyed = 1 << 1,
        OrderChanged = 1 << 2,
        ChangedParent = 1 << 3,
        ChangedScene = 1 << 4,
        SceneOrderChanged = 1 << 5,
        SceneWasRenamed = 1 << 6,
        UnloadedSubSceneWasRenamed = SceneWasRenamed | 1 << 7,
    }
}
