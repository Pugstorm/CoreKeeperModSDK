#define ENABLE_PROFILER
#if ENABLE_PROFILER
using System;
using Unity.Collections;
using Unity.Burst;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Unity.Entities.Serialization;

namespace Unity.Entities.Content
{
    struct RuntimeContentManagerProfilerFrameData
    {
        public UntypedWeakReferenceId id;
        public int parent;
        public int refCount;
    }

    static partial class RuntimeContentManagerProfiler
    {
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "ENABLE_PROFILER")]
        public readonly struct Counter
        {
            readonly ProfilerCounterValue<long> m_Counter;

            public long Value
            {
                get => m_Counter.Value;
                set => m_Counter.Value = value;
            }

            [ExcludeFromBurstCompatTesting("Takes managed string")]
            public Counter(string name, bool perFrame)
            {
                m_Counter = new ProfilerCounterValue<long>(ProfilerCategory.Loading, name, ProfilerMarkerDataUnit.Count, perFrame ? (ProfilerCounterOptions.ResetToZeroOnFlush | ProfilerCounterOptions.FlushOnEndOfFrame) : ProfilerCounterOptions.FlushOnEndOfFrame);
                m_Counter.Value = 0;
            }
        }
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "ENABLE_PROFILER")]
        public readonly struct TimeCounter
        {
            readonly ProfilerCounterValue<long> m_Counter;

            public long Value
            {
                get => m_Counter.Value;
                set => m_Counter.Value = value;
            }

            [ExcludeFromBurstCompatTesting("Takes managed string")]
            public TimeCounter(string name)
            {
                m_Counter = new ProfilerCounterValue<long>(ProfilerCategory.Loading, name, ProfilerMarkerDataUnit.TimeNanoseconds,
                    ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);
            }
        }

        internal const string k_CategoryName = "Runtime Content";
        internal const string k_LoadedObjectsCounterName = "Loaded Objects";
        internal const string k_LoadedScenesCounterName = "Loaded Scenes";
        internal const string k_LoadSceneRequestsCounterName = "Scene Load Requests";
        internal const string k_UnloadSceneRequestsCounterName = "Scene Unload Requests";
        internal const string k_LoadedFilesCounterName = "Loaded Files";
        internal const string k_LoadedArchivesCounterName = "Loaded Archives";
        internal const string k_LoadObjectRequestsCounterName = "Object Load Requests";
        internal const string k_ReleaseObjectRequestsCounterName = "Object Release Requests";
        internal const string k_ObjectRefsCounterName = "Object Ref Count";
        internal const string k_ProcessCommandsFrameTimeCounterName = "Command Process Time";

        sealed class SharedGuid { internal static readonly SharedStatic<Guid> Ref = SharedStatic<Guid>.GetOrCreate<SharedGuid>(); }
        sealed class SharedInit { internal static readonly SharedStatic<bool> Ref = SharedStatic<bool>.GetOrCreate<SharedInit>(); }
        sealed class SharedLoadedObjectsCounter { internal static readonly SharedStatic<Counter> Ref = SharedStatic<Counter>.GetOrCreate<SharedLoadedObjectsCounter>(); }
        sealed class SharedLoadedFilesCounter { internal static readonly SharedStatic<Counter> Ref = SharedStatic<Counter>.GetOrCreate<SharedLoadedFilesCounter>(); }
        sealed class SharedLoadedArchivesCounter { internal static readonly SharedStatic<Counter> Ref = SharedStatic<Counter>.GetOrCreate<SharedLoadedArchivesCounter>(); }
        sealed class SharedLoadObjectRequestsCounter { internal static readonly SharedStatic<Counter> Ref = SharedStatic<Counter>.GetOrCreate<SharedLoadObjectRequestsCounter>(); }
        sealed class SharedReleaseObjectRequestsCounter { internal static readonly SharedStatic<Counter> Ref = SharedStatic<Counter>.GetOrCreate<SharedReleaseObjectRequestsCounter>(); }
        sealed class SharedObjectRefCountCounter { internal static readonly SharedStatic<Counter> Ref = SharedStatic<Counter>.GetOrCreate<SharedObjectRefCountCounter>(); }
        sealed class SharedProcessTimeCounter { internal static readonly SharedStatic<TimeCounter> Ref = SharedStatic<TimeCounter>.GetOrCreate<SharedProcessTimeCounter>(); }
        sealed class SharedProcessStartTime { internal static readonly SharedStatic<long> Ref = SharedStatic<long>.GetOrCreate<SharedProcessStartTime>(); }
        sealed class SharedLoadedScenesCounter { internal static readonly SharedStatic<Counter> Ref = SharedStatic<Counter>.GetOrCreate<SharedLoadedScenesCounter>(); }
        sealed class SharedLoadSceneRequestsCounter { internal static readonly SharedStatic<Counter> Ref = SharedStatic<Counter>.GetOrCreate<SharedLoadSceneRequestsCounter>(); }
        sealed class SharedUnloadSceneRequestsCounter { internal static readonly SharedStatic<Counter> Ref = SharedStatic<Counter>.GetOrCreate<SharedUnloadSceneRequestsCounter>(); }

        static ref bool s_Initialized => ref SharedInit.Ref.Data;
        public static Guid Guid => SharedGuid.Ref.Data;

        public static void Initialize()
        {
            if (s_Initialized)
                return;

            SharedGuid.Ref.Data = new Guid("db99cf5bb18a4a68898de7504dca6985");
            SharedLoadedObjectsCounter.Ref.Data = new Counter(k_LoadedObjectsCounterName, false);
            SharedLoadedFilesCounter.Ref.Data = new Counter(k_LoadedFilesCounterName, false);
            SharedLoadedArchivesCounter.Ref.Data = new Counter(k_LoadedArchivesCounterName, false);
            SharedLoadObjectRequestsCounter.Ref.Data = new Counter(k_LoadObjectRequestsCounterName, true);
            SharedReleaseObjectRequestsCounter.Ref.Data = new Counter(k_ReleaseObjectRequestsCounterName, true);
            SharedObjectRefCountCounter.Ref.Data = new Counter(k_ObjectRefsCounterName, false);
            SharedProcessTimeCounter.Ref.Data = new TimeCounter(k_ProcessCommandsFrameTimeCounterName);
            SharedProcessStartTime.Ref.Data = 0;
            SharedLoadedScenesCounter.Ref.Data = new Counter(k_LoadedScenesCounterName, false);
            SharedLoadSceneRequestsCounter.Ref.Data = new Counter(k_LoadSceneRequestsCounterName, true);
            SharedUnloadSceneRequestsCounter.Ref.Data = new Counter(k_UnloadSceneRequestsCounterName, true);
            s_Initialized = true;
        }

        internal static void Cleanup()
        {
            if (s_Initialized)
                return;
            SharedLoadedObjectsCounter.Ref.Data.Value = 0;
            SharedLoadedFilesCounter.Ref.Data.Value = 0;
            SharedLoadedArchivesCounter.Ref.Data.Value = 0;
            SharedLoadedScenesCounter.Ref.Data.Value = 0;
            s_Initialized = false;
        }

        public static void EnterProcessCommands()
        {
            SharedProcessStartTime.Ref.Data = ProfilerUnsafeUtility.Timestamp;
        }

        public static void ExitProcessCommands()
        {
            if (!s_Initialized)
                return;
            var conversionRatio = ProfilerUnsafeUtility.TimestampToNanosecondsConversionRatio;
            var elapsed = (ProfilerUnsafeUtility.Timestamp - SharedProcessStartTime.Ref.Data) * conversionRatio.Numerator / conversionRatio.Denominator;
            SharedProcessTimeCounter.Ref.Data.Value += elapsed;
        }

        public static void RecordLoadSceneRequest()
        {
            if (!s_Initialized)
                return;
            SharedLoadSceneRequestsCounter.Ref.Data.Value++;
            SharedLoadedScenesCounter.Ref.Data.Value++;
        }


        public static void RecordUnloadSceneRequest()
        {
            if (!s_Initialized)
                return;
            SharedUnloadSceneRequestsCounter.Ref.Data.Value++;
            SharedLoadedScenesCounter.Ref.Data.Value--;
        }

        public static void RecordLoadObjectRequest()
        {
            if (!s_Initialized)
                return;
            SharedLoadObjectRequestsCounter.Ref.Data.Value++;
            SharedObjectRefCountCounter.Ref.Data.Value++;
        }

        internal static void RecordReleaseObjectRequest()
        {
            if (!s_Initialized)
                return;
            SharedReleaseObjectRequestsCounter.Ref.Data.Value++;
            SharedObjectRefCountCounter.Ref.Data.Value--;
        }

        public static void RecordLoadObject()
        {
            if (!s_Initialized)
                return;
            SharedLoadedObjectsCounter.Ref.Data.Value++;
        }

        public static void RecordReleaseObject()
        {
            if (!s_Initialized)
                return;
            SharedLoadedObjectsCounter.Ref.Data.Value--;
        }

        public static void RecordLoadFile()
        {
            if (!s_Initialized)
                return;
            SharedLoadedFilesCounter.Ref.Data.Value++;
        }

        public static void RecordUnloadFile()
        {
            if (!s_Initialized)
                return;
            SharedLoadedFilesCounter.Ref.Data.Value--;
        }

        public static void RecordLoadArchive()
        {
            if (!s_Initialized)
                return;
            SharedLoadedArchivesCounter.Ref.Data.Value++;
        }

        public static void RecordUnloadArchive()
        {
            if (!s_Initialized)
                return;
            SharedLoadedArchivesCounter.Ref.Data.Value--;
        }
    }
}
#endif
