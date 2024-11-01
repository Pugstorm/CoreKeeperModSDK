using System.Collections.Generic;

namespace Unity.Entities.SourceGen.SystemGenerator.Common;

public struct PreprocessorInfo
{
    public bool IsForDotsRuntime { get; private set; }
    public bool IsDotsRuntimeProfilerEnabled { get; private set; }
    public bool IsProfilerEnabled { get; private set; }
    public bool IsDotsDebugMode { get; private set; }
    public bool IsUnityCollectionChecksEnabled { get; private set; }
    public bool IsForUnityEditor { get; private set; }
    public bool IsDevelopmentBuildEnabled { get; private set; }

    public static PreprocessorInfo From(IEnumerable<string> preprocessorSymbolNames)
    {
        bool isUnityCollectionChecksEnabled = false;
        bool isForDotsRuntime = false;
        bool isDotsRuntimeProfilerEnabled = false;
        bool isProfilerEnabled = false;
        bool isDotsDebugMode = false;
        bool isForUnityEditor = false;
        bool isDevelopmentBuildEnabled = false;

        foreach (var name in preprocessorSymbolNames)
        {
            switch (name)
            {
                case "DEVELOPMENT_BUILD":
                    isDevelopmentBuildEnabled = true;
                    break;
                case "UNITY_EDITOR":
                    isForUnityEditor = true;
                    break;
                case "ENABLE_UNITY_COLLECTIONS_CHECKS":
                    isUnityCollectionChecksEnabled = true;
                    break;
                case "UNITY_DOTSRUNTIME":
                    isForDotsRuntime = true;
                    break;
                case "ENABLE_DOTSRUNTIME_PROFILER":
                    isDotsRuntimeProfilerEnabled = true;
                    break;
                case "ENABLE_PROFILER":
                    isProfilerEnabled = true;
                    break;
                case "UNITY_DOTS_DEBUG":
                    isDotsDebugMode = true;
                    break;
            }
        }

        return new PreprocessorInfo
        {
            IsProfilerEnabled = isProfilerEnabled,
            IsDotsRuntimeProfilerEnabled = isDotsRuntimeProfilerEnabled,
            IsForDotsRuntime = isForDotsRuntime,
            IsDotsDebugMode = isDotsDebugMode,
            IsUnityCollectionChecksEnabled = isUnityCollectionChecksEnabled,
            IsForUnityEditor = isForUnityEditor,
            IsDevelopmentBuildEnabled = isDevelopmentBuildEnabled
        };
    }
}
