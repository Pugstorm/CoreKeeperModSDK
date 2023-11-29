#if UNITY_EDITOR && ENABLE_CLOUD_SERVICES_ANALYTICS
#define ENABLE_UNITY_COLLECTIONS_ANALYTICS
#endif

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Unity.Collections
{
    internal struct Telemetry
    {
        public enum Action
        {
            CreateAllocator,
            DestroyAllocator
        }

        [Serializable]
        internal struct Event
        {
            public Action action;
            public string typeName;
        }

        const string k_VendorKey = "unity.collections";
        const string k_EventTopicName = "collectionsAllocators";
        const int k_MaxEventsPerHour = 1000;
        const int k_MaxNumberOfElements = 1000;
        const int k_Version = 1;
#if ENABLE_UNITY_COLLECTIONS_ANALYTICS
        static bool s_EventsRegistered = false;
#endif

#if ENABLE_UNITY_COLLECTIONS_ANALYTICS
        /// <summary>
        /// Track only allocator type names for allocators that have been registered here.
        /// </summary>
        internal static readonly HashSet<Type> AllocatorTypesToTrack = new HashSet<Type>
        {
            typeof(RewindableAllocator),
            typeof(AutoFreeAllocator)
        };
#endif

        [BurstDiscard]
        internal static void SendEvent<T>(Action action) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_ANALYTICS
            if(!UnityEngine.Analytics.Analytics.enabled)
                return;

            if (!s_EventsRegistered)
                RegisterTelemetryEvents();

            var typeName = AllocatorTypesToTrack.Contains(typeof(T)) ? typeof(T).Name : "unregistered";
            var parameters = new Event{action=action,typeName=typeName};
            UnityEngine.Analytics.Analytics.SendEvent(k_EventTopicName, parameters, k_Version);
#endif
        }

        static void RegisterTelemetryEvents()
        {
#if ENABLE_UNITY_COLLECTIONS_ANALYTICS
            UnityEngine.Analytics.Analytics.RegisterEvent(k_EventTopicName, k_MaxEventsPerHour, k_MaxNumberOfElements, k_VendorKey, k_Version);

            UnityEditor.EditorAnalytics.RegisterEventWithLimit(k_Event_MenuPreferences, k_MaxEventsPerHour, k_MaxNumberOfElements, k_VendorKey);

            s_EventsRegistered = true;
#endif
        }

#if UNITY_EDITOR
        internal struct MenuPreferencesEvent
        {
            public bool enableJobsDebugger;
            public bool useJobsThreads;
            public NativeLeakDetectionMode nativeLeakDetectionMode;
        }

        const string k_Event_MenuPreferences = "collectionsMenuPreferences";

        internal static void LogMenuPreferences(MenuPreferencesEvent value)
        {
            SendEditorEvent(k_Event_MenuPreferences, value);
        }

        private static void SendEditorEvent<T>(string eventName, T eventData) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_ANALYTICS
            if (!s_EventsRegistered)
                RegisterTelemetryEvents();

            UnityEditor.EditorAnalytics.SendEventWithLimit(eventName, eventData, k_Version);
#endif
        }
#endif
    }
}
