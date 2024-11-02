using System;
using Unity.Properties;
using UnityEngine.Profiling;

namespace Unity.Entities.Editor
{
    struct ScheduledSystemData
    {
        public readonly SystemCategory Category;
        public readonly int ParentIndex;
        public readonly SystemHandle WorldSystemHandle;
        public int ChildIndex;
        public int ChildCount;

        public int[] UpdateBeforeIndices;
        public int[] UpdateAfterIndices;

        public readonly string NicifiedDisplayName;
        public readonly string TypeName;
        public readonly string FullName;
        public readonly string Namespace;

        public Recorder Recorder;
        public ComponentSystemBase Managed;

        public ScheduledSystemData(ComponentSystemBase m, int parentIndex) // managed systems
        {
            Managed = m;
            WorldSystemHandle = m.SystemHandle;

            Category = SystemUtils.GetSystemCategory(m);

            var systemType = m.GetType();
            NicifiedDisplayName = ContentUtilities.NicifySystemTypeName(systemType);
            TypeName = TypeUtility.GetTypeDisplayName(systemType);
            FullName = systemType.FullName;
            Namespace = systemType.Namespace;

            ParentIndex = parentIndex;
            ChildIndex = 0;
            ChildCount = 0;

            UpdateAfterIndices = Array.Empty<int>();
            UpdateBeforeIndices = Array.Empty<int>();
            Recorder = Recorder.Get($"{m.World?.Name ?? "none"} {FullName}");
        }

        public unsafe ScheduledSystemData(SystemHandle u, World w, int parentIndex) // unmanaged systems
        {
            Managed = null;
            WorldSystemHandle = u;

            Category = SystemCategory.Unmanaged;

            var systemType = SystemBaseRegistry.GetStructType(w.Unmanaged.ResolveSystemState(u)->UnmanagedMetaIndex);
            NicifiedDisplayName = ContentUtilities.NicifySystemTypeName(systemType);
            TypeName = TypeUtility.GetTypeDisplayName(systemType);
            FullName = systemType.FullName;
            Namespace = systemType.Namespace;

            ParentIndex = parentIndex;
            ChildIndex = 0;
            ChildCount = 0;

            UpdateAfterIndices = Array.Empty<int>();
            UpdateBeforeIndices = Array.Empty<int>();
            Recorder = Recorder.Get($"{w.Name ?? "none"} {FullName}");
        }
    }
}
