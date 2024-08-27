#if UNITY_EDITOR
using System;

namespace Unity.NetCode.Analytics
{
    [Serializable]
#if UNITY_2023_2_OR_NEWER
    struct GhostConfigurationAnalyticsData : UnityEngine.Analytics.IAnalytic.IData
#else
    struct GhostConfigurationAnalyticsData
#endif
    {
        public string id;
        public string ghostMode;
        public string optimizationMode;
        public int prespawnedCount;
        public bool autoCommandTarget;
        public int variance;
        public int importance;

        public override string ToString()
        {
            return $"{nameof(id)}: {id}, " +
                   $"{nameof(ghostMode)}: {ghostMode}, " +
                   $"{nameof(optimizationMode)}: {optimizationMode}, " +
                   $"{nameof(prespawnedCount)}: {prespawnedCount}, " +
                   $"{nameof(autoCommandTarget)}: {autoCommandTarget}, " +
                   $"{nameof(importance)}: {importance}, " +
                   $"{nameof(variance)}: {variance}";
        }
    }
}
#endif
