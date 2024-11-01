#if ENABLE_PROFILER
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Profiling;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Entities
{
    static partial class EntitiesProfiler
    {
        static bool s_Initialized;
        static ulong s_LastWorldSequenceNumber;

        sealed class SharedStaticData { internal static readonly SharedStatic<StaticData> Ref = SharedStatic<StaticData>.GetOrCreate<SharedStaticData>(); }

        public static Guid Guid => s_Data.Guid;

        static ref StaticData s_Data => ref SharedStaticData.Ref.Data;

        public static void Initialize()
        {
            if (s_Initialized)
                return;

            // Initialize static data
            s_Data = new StaticData(16, 1024, 1024);

            // Initialize profiler modules
            StructuralChangesProfiler.Initialize();
            MemoryProfiler.Initialize();

            // Register global events
#if UNITY_EDITOR
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                RuntimeApplication.PostFrameUpdate += Update;
            else
                EditorApplication.update += Update;
#else
            RuntimeApplication.PostFrameUpdate += Update;
#endif

            s_Initialized = true;
        }

        public static void Shutdown()
        {
            if (!s_Initialized)
                return;

            // Unregister global events
#if UNITY_EDITOR
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.update -= Update;
            else
                RuntimeApplication.PostFrameUpdate -= Update;
#else
            RuntimeApplication.PostFrameUpdate -= Update;
#endif

            // Shutdown profiler modules
            MemoryProfiler.Shutdown();
            StructuralChangesProfiler.Shutdown();

            // Dispose static data
            s_Data.Dispose();

            s_Initialized = false;
            s_LastWorldSequenceNumber = 0;
        }

        [ExcludeFromBurstCompatTesting("Takes managed World")]
        public static void OnWorldCreated(World world) => s_Data.AddWorld(world);

        public static void OnSystemCreated(SystemTypeIndex systemType, in SystemHandle systemHandle) => s_Data.AddSystem(systemType, in systemHandle);

        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "ENABLE_PROFILER")]
        public static unsafe void ArchetypeAdded(Archetype* archetype) => s_Data.AddArchetype(archetype);

        internal static void Update()
        {
            if (World.NextSequenceNumber != s_LastWorldSequenceNumber)
            {
                // Iterate forward to keep creation order in our events.
                foreach (var world in World.All)
                {
                    if(world.SequenceNumber >= s_LastWorldSequenceNumber)
                        OnWorldCreated(world);
                }
                s_LastWorldSequenceNumber = World.NextSequenceNumber;
            }

            MemoryProfiler.Update();
            StructuralChangesProfiler.Flush();
            s_Data.Flush();
        }

        internal static unsafe void FlushSessionMetaData<T>(in Guid guid, int tag, ref UnsafeList<T> list) where T : unmanaged
        {
            if (!Profiler.enabled || list.IsEmpty)
                return;

            Profiler.EmitSessionMetaData(guid, tag, list.AsNativeArray());
            list.Clear();
        }

        internal static unsafe void FlushFrameMetaData<T>(in Guid guid, int tag, ref UnsafeList<T> list) where T : unmanaged
        {
            if (!Profiler.enabled || list.IsEmpty)
                return;

            Profiler.EmitFrameMetaData(guid, tag, list.AsNativeArray());
            list.Clear();
        }

        static unsafe NativeArray<T> AsNativeArray<T>(this UnsafeList<T> list) where T : unmanaged
        {
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(list.Ptr, list.Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
            return array;
        }

        static FixedString128Bytes ToFixedString128(this string value)
        {
            return value.Substring(0, math.min(value.Length, FixedString128Bytes.UTF8MaxLengthInBytes));
        }
    }
}
#endif
