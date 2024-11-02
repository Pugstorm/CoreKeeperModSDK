#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING

using Unity.Mathematics;
using UnityEngine;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// Class that stores journaling preferences used during initialization.
        /// </summary>
        public static class Preferences
        {
            internal const bool k_EnabledDefault = false;
            internal const int k_TotalMemoryMBDefault = 64;
            internal const int k_TotalMemoryMBMin = 1;
            internal const int k_TotalMemoryMBMax = 1024;
            internal const bool k_PostProcessDefault = true;

            const string k_EnabledKey = nameof(EntitiesJournaling) + "." + nameof(Enabled);
            const string k_TotalMemoryMBKey = nameof(EntitiesJournaling) + nameof(TotalMemoryMB);
            const string k_PostProcessKey = nameof(EntitiesJournaling) + "." + nameof(PostProcess);

            /// <summary>
            /// Whether or not entities journaling events are recorded.
            /// </summary>
            /// <remarks>
            /// This value is only read during journaling initialization.
            /// The new value will take effect when journaling initializes again.
            /// </remarks>
            public static bool Enabled
            {
                get => PlayerPrefs.GetInt(k_EnabledKey, k_EnabledDefault ? 1 : 0) == 1;
                set => PlayerPrefs.SetInt(k_EnabledKey, value ? 1 : 0);
            }

            /// <summary>
            /// Total amount of memory in megabytes allocated for journaling.
            /// </summary>
            /// <remarks>
            /// This value is only read during journaling initialization.
            /// The new value will take effect when journaling initializes again.
            /// </remarks>
            public static int TotalMemoryMB
            {
                get => math.clamp(PlayerPrefs.GetInt(k_TotalMemoryMBKey, k_TotalMemoryMBDefault), k_TotalMemoryMBMin, k_TotalMemoryMBMax);
                set => PlayerPrefs.SetInt(k_TotalMemoryMBKey, math.clamp(value, k_TotalMemoryMBMin, k_TotalMemoryMBMax));
            }

            /// <summary>
            /// Apply post-processing to journaling records.
            /// Converts <see cref="RecordType.GetComponentDataRW"/> records into <see cref="RecordType.SetComponentData"/> whenever possible.
            /// </summary>
            public static bool PostProcess
            {
                get => PlayerPrefs.GetInt(k_PostProcessKey, k_PostProcessDefault ? 1 : 0) == 1;
                set => PlayerPrefs.SetInt(k_PostProcessKey, value ? 1 : 0);
            }
        }
    }
}
#endif
