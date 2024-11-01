using static Unity.Entities.EntitiesProfiler;
using static Unity.Entities.StructuralChangesProfiler;

namespace Unity.Entities.Editor
{
    partial class StructuralChangesProfilerModule
    {
        readonly struct StructuralChangesProfilerTreeViewItemData
        {
            public readonly string WorldName;
            public readonly string SystemName;
            public readonly StructuralChangeType Type;
            public readonly long ElapsedNanoseconds;

            public StructuralChangesProfilerTreeViewItemData(WorldData worldData, SystemData systemData, StructuralChangeData structuralChangeData)
            {
                WorldName = worldData.Name;
                SystemName = !string.IsNullOrEmpty(systemData.Name) ? systemData.Name : "<No System>";
                Type = structuralChangeData.Type;
                ElapsedNanoseconds = structuralChangeData.ElapsedNanoseconds;
            }
        }
    }
}
