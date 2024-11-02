#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace Unity.Entities.Conversion
{
    public static class LiveConversionSettings
    {
        private const string EditorPrefsConversionMode = "com.unity.entities.conversion_mode";
        private const string EditorPrefsConversionDebugLog = "com.unity.entities.conversion_debug_logging";
        private const string EditorPrefsBakingEnabled = "com.unity.entities.baking_enabled";
        private const string EditorPrefsLiveBakingLoggingEnabled = "com.unity.entities.baking_logging";
        private const string EditorPrefsBuiltinBuildsEnabled = "com.unity.entities.baking.builtin_builds_enabled";

        internal enum ConversionMode
        {
            /// <summary>
            /// All changes trigger a clean conversion.
            /// </summary>
            AlwaysCleanConvert = 0,

            /// <summary>
            /// All changes are handled via incremental conversion, except when there are failures in which case we
            /// trigger a clean conversion.
            /// </summary>
            IncrementalConversion,

            /// <summary>
            /// Like pure incremental conversion, but also performs a clean conversion and diffs against that.
            /// </summary>
            IncrementalConversionWithDebug,
        }

        /// <summary>
        /// When set to true, a failure during incremental conversion is treated as an error. Otherwise a failure leads
        /// to a clean conversion instead. This should only be enabled for testing purposes.
        /// </summary>
        internal static bool TreatIncrementalConversionFailureAsError { get; set; }

        internal static bool EnableInternalDebugValidation;
        internal static readonly List<Type> AdditionalConversionSystems = new List<Type>();

        internal static bool IsDebugLoggingEnabled
        {
            get => SessionState.GetBool(EditorPrefsConversionDebugLog, false);
            set => SessionState.SetBool(EditorPrefsConversionDebugLog, value);
        }

        internal static bool IsFullyIncremental => Mode == ConversionMode.IncrementalConversion ||
                                                 Mode == ConversionMode.IncrementalConversionWithDebug;

        internal static ConversionMode Mode
        {
            get => (ConversionMode) SessionState.GetInt(EditorPrefsConversionMode, (int) ConversionMode.IncrementalConversion);
            set => SessionState.SetInt(EditorPrefsConversionMode, (int) value);
        }

        public static bool IsLiveBakingLoggingEnabled
        {
            get => SessionState.GetBool(EditorPrefsLiveBakingLoggingEnabled, false);
            set => SessionState.SetBool(EditorPrefsLiveBakingLoggingEnabled, value);
        }
    }
}
#endif
