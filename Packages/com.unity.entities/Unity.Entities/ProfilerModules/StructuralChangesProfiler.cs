#if ENABLE_PROFILER
using System;
using Unity.Burst;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace Unity.Entities
{
    static partial class StructuralChangesProfiler
    {
        internal const string k_CategoryName = "Entities Structural Changes";
        internal const string k_CreateEntityCounterName = "Create Entity";
        internal const string k_DestroyEntityCounterName = "Destroy Entity";
        internal const string k_AddComponentCounterName = "Add Component";
        internal const string k_RemoveComponentCounterName = "Remove Component";
        internal const string k_SetSharedComponentCounterName = "Set Shared Component";

        sealed class SharedInitialized { internal static readonly SharedStatic<bool> Ref = SharedStatic<bool>.GetOrCreate<bool>(); }
        sealed class SharedStaticData { internal static readonly SharedStatic<StaticData> Ref = SharedStatic<StaticData>.GetOrCreate<StaticData>(); }

        public static bool Enabled => Profiler.enabled && Profiler.IsCategoryEnabled(s_Data.Category);
        public static Guid Guid => s_Data.Guid;
        public static ProfilerCategory Category => s_Data.Category;

        static ref bool s_Initialized => ref SharedInitialized.Ref.Data;
        static ref StaticData s_Data => ref SharedStaticData.Ref.Data;

        public static void Initialize()
        {
            if (s_Initialized)
                return;

            s_Data = new StaticData(/*dummy*/0);
            s_Initialized = true;
        }

        public static void Shutdown()
        {
            if (!s_Initialized)
                return;

            s_Data.Dispose();
            s_Initialized = false;
        }

        public unsafe static void Flush()
        {
            if (!s_Initialized || !Enabled)
                return;

            var worlds = World.All;
            for (int i = 0, count = worlds.Count; i < count; ++i)
            {
                var world = worlds[i];
                if (!world.IsCreated)
                    continue;

                if (!world.EntityManager.CanBeginExclusiveEntityTransaction())
                    continue;

                var manager = world.EntityManager;
                var access = manager.GetCheckedEntityDataAccess();
                if (access == null)
                    continue;

                var store = access->EntityComponentStore;
                if (store == null)
                    continue;

                store->StructuralChangesRecorder.Flush();
            }
        }
    }
}
#endif
