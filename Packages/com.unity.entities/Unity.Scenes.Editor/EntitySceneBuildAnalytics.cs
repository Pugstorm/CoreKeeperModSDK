using System;
using System.Collections.Generic;
using Unity.Entities.Content;
using UnityEditor;
using UnityEngine.Analytics;


namespace Unity.Scenes.Editor
{
    internal static class EntitySceneBuildAnalytics
    {
        private const string VendorKey = "unity.entitySceneAnalytics";
        private const string BuildEvent = "entitySceneBuildContentEvent";
        private static HashSet<string> registeredEvents = new HashSet<string>();

        internal struct BuildData
#if UNITY_2023_2_OR_NEWER
            : IAnalytic.IData
#endif
        {
            public int NumberOfContentArchives;
            public int NumberOfWeakReferences;
            public int NumberOfSubScenes;
            public int NumberOfAssets;
            public int NumberOfContentFilesBuilt;
            public int NumberOfAssetsInSubScenes;
            public bool IsUsingContentArchives;
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName: BuildEvent, vendorKey: VendorKey, maxEventsPerHour: 100, maxNumberOfElements: 100)]
        class BuildDataAnalytic : IAnalytic
        {
            readonly BuildData _data;

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                data = _data;
                error = null;
                return true;
            }

            public BuildDataAnalytic(BuildData data) => _data = data;
        }
#endif

        private static bool RegisterEvent(string eventName)
        {
#if !UNITY_2023_2_OR_NEWER
            bool eventSuccessfullyRegistered = false;
            UnityEngine.Analytics.AnalyticsResult registerEvent = EditorAnalytics.RegisterEventWithLimit(eventName, 100, 100, VendorKey);
            if (registerEvent == UnityEngine.Analytics.AnalyticsResult.Ok)
            {
                registeredEvents.Add(eventName);
                eventSuccessfullyRegistered = true;
            }
            return eventSuccessfullyRegistered;
#else
            return true;
#endif
        }

        private static bool EventIsRegistered(string eventName)
        {
            return registeredEvents.Contains(eventName);
        }


        internal static void ReportBuildEvent(IRuntimeCatalogDataSource buildResultsCatalogDataSource, int numberOfAssetsInSubscenes, int numberOfWeakAssetReferences, int numberOfSubScenesInBuild, int numberOfAssets, bool isUsingContentArchives)
        {
            if (!EditorAnalytics.enabled)
                return;

            if (!EventIsRegistered(BuildEvent))
                if (!RegisterEvent(BuildEvent))
                    return;

            int numberOfContentFiles = 0;
            int numberOfContentArchives = 0;

            if (buildResultsCatalogDataSource != null)
            {
                var archiveIds = buildResultsCatalogDataSource.GetArchiveIds();
                foreach (var archiveId in archiveIds)
                {
                    if (archiveId.IsValid)
                    {
                        numberOfContentArchives++;
                        var fileIds = buildResultsCatalogDataSource.GetFileIds(archiveId);

                        foreach (var fileId in fileIds)
                            if (fileId.IsValid)
                                numberOfContentFiles++;
                    }
                }
            }

            BuildData data = new BuildData()
            {
                NumberOfAssets = numberOfAssets,
                NumberOfContentArchives = numberOfContentArchives,
                NumberOfWeakReferences = numberOfWeakAssetReferences,
                NumberOfSubScenes = numberOfSubScenesInBuild,
                NumberOfContentFilesBuilt = numberOfContentFiles,
                NumberOfAssetsInSubScenes = numberOfAssetsInSubscenes,
                IsUsingContentArchives = isUsingContentArchives
            };

#if !UNITY_2023_2_OR_NEWER
            EditorAnalytics.SendEventWithLimit(BuildEvent, data);
#else
            EditorAnalytics.SendAnalytic(new BuildDataAnalytic(data));
#endif
        }
    }
}

