using System;
using UnityEditor;
using UnityEngine.Analytics;

namespace Unity.Entities.Editor
{
    static class Analytics
    {
        // NOTE: Don't change names or numbers here because
        // these names and numbers are used in the cloud
        // analytics tables and any change would invalidate
        // previously recorded data. Should only add new enums.
        public enum Window
        {
            Unknown = 0,
            Inspector = 1,
            Hierarchy = 2,
            Components = 3,
            Systems = 4,
            Query = 5,
            Archetypes = 6,
            Journaling = 7,
            Profiler = 8
        }

        // NOTE: Don't change names or numbers here because
        // these names and numbers are used in the cloud
        // analytics tables and any change would invalidate
        // previously recorded data. Should only add new enums.
        public enum EventType
        {
            WindowOpen = 0,
            WindowFocus = 1,
            ProfilerModuleCreate = 2,
            InspectorTabFocus = 3,
            RelationshipGoTo = 4,
            DataModeSwitch = 5,
            DataModeManualSwitch = 6
        }

        // NOTE: Don't change existing fields here because
        // these existing fields are used in the cloud
        // analytics tables and any change would invalidate
        // previously recorded data. Should only add new fields.
#if !UNITY_2023_2_OR_NEWER
        [Serializable]
#endif
        public struct EventPayload
#if UNITY_2023_2_OR_NEWER
        : IAnalytic.IData
#endif
        {
            public string window_name;
            public string event_type;
            public string context;
        };

        // Constants
        // NOTE: Don't change existing strings here because
        // these existing strings are used in the cloud
        // analytics tables and any change would invalidate
        // previously recorded data. Should only add new strings.
        public const string MemoryProfilerModuleName = "Memory";
        public const string StructuralChangesProfilerModuleName = "StructuralChanges";
        public const string AspectsTabName = "Aspects";
        public const string ComponentsTabName = "Components";
        public const string RelationshipsTabName = "Relationships";
        public const string GoToEntityDestination = "Entity";
        public const string GoToComponentDestination = "Component";
        public const string GoToSystemDestination = "System";

        const string k_EditorEventName = "uEntitiesEditorUsage";
        const string k_VendorKey = "unity.entities";
        const int k_MaxEventsPerHour = 1000;
        const int k_MaxNumberOfElements = 1000;

#if !UNITY_2023_2_OR_NEWER
        static bool s_EditorEventRegistered = false;

        static bool EnableEditorAnalytics()
        {
            AnalyticsResult result = EditorAnalytics.RegisterEventWithLimit(k_EditorEventName, k_MaxEventsPerHour, k_MaxNumberOfElements, k_VendorKey, 2);
            if (result == AnalyticsResult.Ok)
                s_EditorEventRegistered = true;

            return s_EditorEventRegistered;
        }
#endif

        public static void SendEditorEvent(Window window, EventType eventType, string context = default)
        {
#if !UNITY_2023_2_OR_NEWER
            // The event shouldn't be able to report if this is disabled but
            // if we know we're not going to report lets early out and not waste
            // time gathering all the data
            if (!EditorAnalytics.enabled)
                return;

            if (!EnableEditorAnalytics())
                return;

            var data = new EventPayload()
            {
                window_name = window.ToString(),
                event_type = eventType.ToString(),
                context = context
            };

            EditorAnalytics.SendEventWithLimit(k_EditorEventName, data, 2);
#else
            // The event shouldn't be able to report if this is disabled but
            // if we know we're not going to report lets early out and not waste
            // time gathering all the data
            if (!EditorAnalytics.enabled || !EditorAnalytics.recordEventsEnabled)
                return;

            var data = new EventPayload()
            {
                window_name = window.ToString(),
                event_type = eventType.ToString(),
                context = context
            };

            EditorAnalytics.SendAnalytic(new EntitiesEditorUsageAnalytic(data));
#endif
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName: k_EditorEventName, vendorKey: k_VendorKey, version: 2, maxEventsPerHour: k_MaxEventsPerHour, maxNumberOfElements: k_MaxNumberOfElements)]
        internal class EntitiesEditorUsageAnalytic : IAnalytic
        {
            private readonly EventPayload m_Data;

            public EntitiesEditorUsageAnalytic(EventPayload data)
                => m_Data = data;

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = m_Data;
                return true;
            }
        }
#endif
    }
}
