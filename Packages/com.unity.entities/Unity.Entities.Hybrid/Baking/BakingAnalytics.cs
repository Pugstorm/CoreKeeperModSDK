#if UNITY_EDITOR
#if ENABLE_CLOUD_SERVICES_ANALYTICS
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;

namespace Unity.Entities
{
    [InitializeOnLoad]
    static class BakingAnalytics
    {
        static bool s_EventsRegistered = false;
        const int k_MaxEventsPerHour = 1000;
        const int k_MaxNumberOfElements = 1000;
        const string k_VendorKey = "unity.entities";

        const string k_EventNameComplexity = "subSceneComplexity";
        const string k_EventNameIncremental = "incrementalBaking";
        const string k_EventNameOpen = "openSubScene";
        const string k_EventNameImporter = "backgroundImporter";

        static TypeIndex k_SkinnedMeshRendererTypeIndex;

        static ProjectComplexityData s_ProjectComplexityData;
        static NativeList<TypeIndex> s_BakeTypeIndices;
        static IReadOnlyList<System.Type> s_BakingSystemTypes;

        public static IReadOnlyList<System.Type> BakingSystemTypes
        {
            set { s_BakingSystemTypes = value; }
        }

        public static void LogBakerTypeIndex(TypeIndex bakeTypeIndex)
        {
            s_BakeTypeIndices.Add(bakeTypeIndex);
        }

        public static void LogBlobAssetCount(int blobAssetCount)
        {
            s_ProjectComplexityData.blob_assets_count = blobAssetCount;
        }

        public static void LogPrefabCount(int prefabCount)
        {
            s_ProjectComplexityData.prefabs_count = prefabCount;
        }

        static BakingAnalytics()
        {
            s_BakeTypeIndices = new NativeList<TypeIndex>(Allocator.Persistent);// de-allocate
            s_ProjectComplexityData = new ProjectComplexityData()
            {
                default_components_count = 0,
                custom_bakers_count = 0,
                custom_baking_systems_count = 0,
                blob_assets_count = 0,
                prefabs_count = 0,
                skinned_mesh_renderer_component_count = 0,
            };

            AppDomain.CurrentDomain.DomainUnload += (_, __) => { s_BakeTypeIndices.Dispose(); };
        }

        static bool EnableAnalytics()
        {
#if !UNITY_2023_2_OR_NEWER
            if (!s_EventsRegistered)
            {
                k_SkinnedMeshRendererTypeIndex = TypeManager.GetTypeIndex(typeof(SkinnedMeshRenderer));
                AnalyticsResult resultComplexity = EditorAnalytics.RegisterEventWithLimit(k_EventNameComplexity, k_MaxEventsPerHour,
                    k_MaxNumberOfElements, k_VendorKey);
                AnalyticsResult resultIncremental = EditorAnalytics.RegisterEventWithLimit(k_EventNameIncremental, k_MaxEventsPerHour,
                    k_MaxNumberOfElements, k_VendorKey);
                AnalyticsResult resultOpen = EditorAnalytics.RegisterEventWithLimit(k_EventNameOpen, k_MaxEventsPerHour,
                    k_MaxNumberOfElements, k_VendorKey);
                AnalyticsResult resultImporter = EditorAnalytics.RegisterEventWithLimit(k_EventNameImporter, k_MaxEventsPerHour,
                    k_MaxNumberOfElements, k_VendorKey);

                if (resultComplexity == AnalyticsResult.Ok ||resultIncremental == AnalyticsResult.Ok ||
                    resultOpen == AnalyticsResult.Ok || resultImporter == AnalyticsResult.Ok)
                    s_EventsRegistered = true;
            }
#else
            s_EventsRegistered = true;
#endif

            return s_EventsRegistered;
        }

        public enum EventType
        {
            IncrementalBaking,
            OpeningSubScene,
            BackgroundImporter
        }

        public static void SendAnalyticsEvent(float elapsedMs, EventType eventType)
        {
            //The event shouldn't be able to report if this is disabled but if we know we're not going to report
            //Lets early out and not waste time gathering all the data
            if (!EditorAnalytics.enabled)
                return;

            if (!EnableAnalytics())
                return;

            if (eventType == EventType.IncrementalBaking)
            {
                SendIncrementalBakingPerformanceEvents(elapsedMs);
            }else if (eventType == EventType.OpeningSubScene)
            {
                SendComplexityEvent();
                SendOpenSubScenePerformanceEvents(elapsedMs);
            }
            else
            {
                SendBackgroundImporterPerformanceEvents(elapsedMs);
            }

            s_BakeTypeIndices.Clear();
            s_ProjectComplexityData.Clear();
        }


        static void SendComplexityEvent()
        {
            int defaultComponentCount = 0;
            int customBakerCount = 0;
            int customBakingSystemCount = 0;
            int skinnedMeshRendererComponentCount = 0;

            for (int i = 0; i < s_BakeTypeIndices.Length; i++)
            {
                var bakeTypeIndex = s_BakeTypeIndices[i];
                var bakers = BakerDataUtility.GetBakers(bakeTypeIndex);

                // If the Component is custom
                var assembly = TypeManager.GetType(bakeTypeIndex).Assembly;

                // Check if the Component/Bakers are from a Unity Assembly or a Custom one
                bool isUnityAssembly;
                if (BakerDataUtility._BakersByAssembly.TryGetValue(assembly, out var assemblyData))
                    isUnityAssembly = assemblyData.IsUnityAssembly;
                else
                {
                    var assemblyName = assembly.GetName().Name;
                    isUnityAssembly = assemblyName.StartsWith("Unity.") || assemblyName.StartsWith("UnityEngine.");
                }

                // Log the Component/Bakers according to Assembly
                if (isUnityAssembly)
                {
                    defaultComponentCount++;

                    if (bakeTypeIndex == k_SkinnedMeshRendererTypeIndex)
                        skinnedMeshRendererComponentCount++;
                }
                else
                {
                    customBakerCount += bakers.Length;
                }
            }


            for (int i = 0; i < s_BakingSystemTypes.Count; i++)
            {
                // If the BakingSystem is custom
                var assembly = s_BakingSystemTypes[i].Assembly;

                // Check if the BakingSystem is from a Unity Assembly or a Custom one
                bool isUnityAssembly;
                if (BakerDataUtility._BakersByAssembly.TryGetValue(assembly, out var assemblyData))
                    isUnityAssembly = assemblyData.IsUnityAssembly;
                else
                    isUnityAssembly = assembly.GetName().Name.StartsWith("Unity.") || assembly.GetName().Name.StartsWith("UnityEngine.");

                // Log the BakingSystem according to Assembly
                if (!isUnityAssembly)
                    customBakingSystemCount++;
            }

            s_ProjectComplexityData.default_components_count = defaultComponentCount;
            s_ProjectComplexityData.custom_bakers_count = customBakerCount;
            s_ProjectComplexityData.custom_baking_systems_count = customBakingSystemCount;
            s_ProjectComplexityData.skinned_mesh_renderer_component_count = skinnedMeshRendererComponentCount;

            // collect max every playmode enter, send when project is closed
#if !UNITY_2023_2_OR_NEWER
            EditorAnalytics.SendEventWithLimit(k_EventNameComplexity, s_ProjectComplexityData);
#else
            EditorAnalytics.SendAnalytic(new ProjectComplexityAnalytic(s_ProjectComplexityData));
#endif
        }

        static void SendIncrementalBakingPerformanceEvents(float elapsedMs)
        {
#if !UNITY_2023_2_OR_NEWER
            EditorAnalytics.SendEventWithLimit(k_EventNameIncremental, new PerformanceData(){elapsedMs = elapsedMs});
#else
            EditorAnalytics.SendAnalytic(new IncrementalAnalytic(new PerformanceData(){elapsedMs = elapsedMs}));
#endif
        }
        static void SendOpenSubScenePerformanceEvents(float elapsedMs)
        {
#if !UNITY_2023_2_OR_NEWER
            EditorAnalytics.SendEventWithLimit(k_EventNameOpen, new PerformanceData(){elapsedMs = elapsedMs});
#else
            EditorAnalytics.SendAnalytic(new OpenAnalytic(new PerformanceData(){elapsedMs = elapsedMs}));
#endif
        }
        static void SendBackgroundImporterPerformanceEvents(float elapsedMs)
        {
#if !UNITY_2023_2_OR_NEWER
            EditorAnalytics.SendEventWithLimit(k_EventNameImporter, new PerformanceData(){elapsedMs = elapsedMs});
#else
            EditorAnalytics.SendAnalytic(new ImporterAnalytic(new PerformanceData(){elapsedMs = elapsedMs}));
#endif
        }

        class ProjectComplexityData
#if UNITY_2023_2_OR_NEWER
            : IAnalytic.IData
#endif
        {
            public int default_components_count;
            public int custom_bakers_count;
            public int custom_baking_systems_count;
            public int blob_assets_count;
            public int prefabs_count;
            public int skinned_mesh_renderer_component_count;

            public void Clear()
            {
                default_components_count = 0;
                custom_bakers_count = 0;
                custom_baking_systems_count = 0;
                blob_assets_count = 0;
                prefabs_count = 0;
                skinned_mesh_renderer_component_count = 0;
            }
        }

        class PerformanceData
#if UNITY_2023_2_OR_NEWER
            : IAnalytic.IData
#endif
        {
            public float elapsedMs;
        }

#if UNITY_2023_2_OR_NEWER
        abstract class BakingAnalytic<T> : IAnalytic where T : class, IAnalytic.IData
        {
            readonly T _data;

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                data = _data;
                error = null;
                return true;
            }

            protected BakingAnalytic(T data) => _data = data;
        }

        [AnalyticInfo(eventName: k_EventNameComplexity, vendorKey: k_VendorKey, maxEventsPerHour: k_MaxEventsPerHour, maxNumberOfElements: k_MaxNumberOfElements)]
        class ProjectComplexityAnalytic : BakingAnalytic<ProjectComplexityData>
        {
            public ProjectComplexityAnalytic(ProjectComplexityData data) : base(data) {}
        }

        [AnalyticInfo(eventName: k_EventNameIncremental, vendorKey: k_VendorKey, maxEventsPerHour: k_MaxEventsPerHour, maxNumberOfElements: k_MaxNumberOfElements)]
        class IncrementalAnalytic : BakingAnalytic<PerformanceData>
        {
            public IncrementalAnalytic(PerformanceData data) : base(data) {}
        }

        [AnalyticInfo(eventName: k_EventNameOpen, vendorKey: k_VendorKey, maxEventsPerHour: k_MaxEventsPerHour, maxNumberOfElements: k_MaxNumberOfElements)]
        class OpenAnalytic : BakingAnalytic<PerformanceData>
        {
            public OpenAnalytic(PerformanceData data) : base(data) {}
        }

        [AnalyticInfo(eventName: k_EventNameImporter, vendorKey: k_VendorKey, maxEventsPerHour: k_MaxEventsPerHour, maxNumberOfElements: k_MaxNumberOfElements)]
        class ImporterAnalytic : BakingAnalytic<PerformanceData>
        {
            public ImporterAnalytic(PerformanceData data) : base(data) {}
        }
#endif
    }
}
#endif // ENABLE_CLOUD_SERVICES_ANALYTICS
#endif // UNITY_EDITOR
