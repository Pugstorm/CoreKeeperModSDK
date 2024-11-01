#if ENABLE_PROFILER
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace Unity.Entities
{
    static partial class MemoryProfiler
    {
        [BurstCompile]
        unsafe struct MemoryProfilerJob : IJob
        {
            [ReadOnly] public Guid Guid;
            [ReadOnly] public ulong WorldSequenceNumber;
            [ReadOnly] public UnsafePtrList<Archetype> Archetypes;
            public BytesCounter AllocatedBytesCounter;
            public BytesCounter UnusedBytesCounter;
            public UnsafeList<ArchetypeMemoryData> ArchetypeMemoryData;

            public void Execute()
            {
                // Prepare data for emitting
                var allocatedBtyes = 0UL;
                var unusedBytes = 0UL;
                for (var index = 0; index < Archetypes.Length; ++index)
                {
                    var archetype = Archetypes[index];
                    var memoryData = new ArchetypeMemoryData(WorldSequenceNumber, archetype);
                    if (archetype->EntityCount != 0)
                    {
                        allocatedBtyes += memoryData.CalculateAllocatedBytes();
                        unusedBytes += memoryData.CalculateUnusedBytes(archetype);
                    }
                    ArchetypeMemoryData[index] = memoryData;
                }

                // Emit data to profiler
                AllocatedBytesCounter.Value += allocatedBtyes;
                UnusedBytesCounter.Value += unusedBytes;
                Profiler.EmitFrameMetaData(Guid, 0, ArchetypeMemoryData.AsNativeArray());
            }
        }

        internal const string k_CategoryName = "Entities Memory";
        internal const string k_AllocatedMemoryCounterName = "Allocated Memory";
        internal const string k_UnusedMemoryCounterName = "Unused Memory";

        public static bool Enabled => Profiler.enabled && Profiler.IsCategoryEnabled(Category);
        public static Guid Guid { get; private set; }
        public static ProfilerCategory Category { get; private set; }
        public static BytesCounter AllocatedBytesCounter { get; private set; }
        public static BytesCounter UnusedBytesCounter { get; private set; }

        static bool s_Initialized;
        static UnsafeList<ArchetypeMemoryData> s_ArchetypeMemoryData;

        public static void Initialize()
        {
            if (s_Initialized)
                return;

            Guid = new Guid("d1589a720beb45b78a4087311ae83a2c");
            Category = new ProfilerCategory(k_CategoryName);
            AllocatedBytesCounter = new BytesCounter(k_AllocatedMemoryCounterName);
            UnusedBytesCounter = new BytesCounter(k_UnusedMemoryCounterName);
            s_ArchetypeMemoryData = new UnsafeList<ArchetypeMemoryData>(64, Allocator.Persistent);
            s_Initialized = true;
        }

        public static void Shutdown()
        {
            if (!s_Initialized)
                return;

            s_ArchetypeMemoryData.Dispose();
            s_Initialized = false;
        }

        public static void Update()
        {
            if (!s_Initialized || !Enabled)
                return;

            var worlds = World.All;
            for (int i = 0, count = worlds.Count; i < count; ++i)
            {
                var world = worlds[i];
                if (!world.IsCreated)
                    continue;

                Internal_UpdateWorld(world);
            }
        }

        /// <summary>
        /// For internal use only, use <see cref="Update"/> instead.
        /// </summary>
        internal static unsafe void Internal_UpdateWorld(World world)
        {
            if (!world.EntityManager.CanBeginExclusiveEntityTransaction())
                return;

            var access = world.EntityManager.GetCheckedEntityDataAccess();
            if (access == null)
                return;

            var archetypes = access->EntityComponentStore->m_Archetypes;
            if (archetypes.Length == 0)
                return;

            s_ArchetypeMemoryData.Resize(archetypes.Length);
            new MemoryProfilerJob
            {
                Guid = Guid,
                WorldSequenceNumber = world.SequenceNumber,
                Archetypes = archetypes,
                AllocatedBytesCounter = AllocatedBytesCounter,
                UnusedBytesCounter = UnusedBytesCounter,
                ArchetypeMemoryData = s_ArchetypeMemoryData
            }.Run();
        }

        static unsafe NativeArray<T> AsNativeArray<T>(this UnsafeList<T> list) where T : unmanaged
        {
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(list.Ptr, list.Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
            return array;
        }
    }
}
#endif
