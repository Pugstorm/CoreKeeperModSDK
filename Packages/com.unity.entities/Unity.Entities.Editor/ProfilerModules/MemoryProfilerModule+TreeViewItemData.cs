using static Unity.Entities.EntitiesProfiler;
using static Unity.Entities.MemoryProfiler;

namespace Unity.Entities.Editor
{
    partial class MemoryProfilerModule
    {
        internal readonly struct MemoryProfilerTreeViewItemData
        {
            public readonly string WorldName;
            public readonly ulong StableHash;
            public readonly ulong AllocatedBytes;
            public readonly ulong UnusedBytes;
            public readonly int EntityCount;
            public readonly int UnusedEntityCount;
            public readonly int ChunkCount;
            public readonly int ChunkCapacity;
            public readonly int SegmentCount;
            public readonly int InstanceSize;
            public readonly TypeIndex[] ComponentTypes;

            public MemoryProfilerTreeViewItemData(string worldName, ArchetypeData archetypeData, ArchetypeMemoryData archetypeMemoryData)
            {
                WorldName = worldName;
                StableHash = archetypeData.StableHash;
                AllocatedBytes = archetypeMemoryData.CalculateAllocatedBytes();
                UnusedBytes = archetypeMemoryData.CalculateUnusedBytes(archetypeData);
                EntityCount = archetypeMemoryData.EntityCount;
                UnusedEntityCount = archetypeMemoryData.CalculateUnusedEntityCount(archetypeData);
                ChunkCount = archetypeMemoryData.ChunkCount;
                ChunkCapacity = archetypeData.ChunkCapacity;
                SegmentCount = archetypeMemoryData.SegmentCount;
                InstanceSize = archetypeData.InstanceSize;
                ComponentTypes = new TypeIndex[archetypeData.ComponentTypes.Length];
                for (var i = 0; i < archetypeData.ComponentTypes.Length; ++i)
                    ComponentTypes[i] = archetypeData.ComponentTypes[i].GetTypeIndex();
            }
        }
    }
}
