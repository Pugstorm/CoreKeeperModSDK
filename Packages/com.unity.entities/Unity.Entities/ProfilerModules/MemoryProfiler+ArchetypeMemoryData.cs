#if ENABLE_PROFILER
using System;
using Unity.Collections;
using static Unity.Entities.EntitiesProfiler;

namespace Unity.Entities
{
    static partial class MemoryProfiler
    {
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "ENABLE_PROFILER")]
        public readonly unsafe struct ArchetypeMemoryData : IEquatable<ArchetypeMemoryData>
        {
            public readonly ulong WorldSequenceNumber;
            public readonly ulong StableHash;
            public readonly int EntityCount;
            public readonly int ChunkCount;
            public readonly int SegmentCount;

            public ArchetypeMemoryData(ulong worldSeqNumber, Archetype* archetype)
            {
                WorldSequenceNumber = worldSeqNumber;
                StableHash = archetype->StableHash;
                EntityCount = archetype->EntityCount;
                ChunkCount = archetype->Chunks.Count;
                SegmentCount = 0; // TODO: find a faster way to get this value
            }

            public ulong CalculateAllocatedBytes()
            {
                return (ulong)ChunkCount * Chunk.kChunkSize;
            }

            public ulong CalculateUnusedBytes(Archetype* archetype)
            {
                return (ulong)CalculateUnusedEntityCount(archetype) * (ulong)archetype->InstanceSize;
            }

            public ulong CalculateUnusedBytes(ArchetypeData archetypeData)
            {
                return (ulong)CalculateUnusedEntityCount(archetypeData) * (ulong)archetypeData.InstanceSize;
            }

            public int CalculateUnusedEntityCount(Archetype* archetype)
            {
                return (ChunkCount * archetype->ChunkCapacity) - EntityCount;
            }

            public int CalculateUnusedEntityCount(ArchetypeData archetypeData)
            {
                return (ChunkCount * archetypeData.ChunkCapacity) - EntityCount;
            }

            public bool Equals(ArchetypeMemoryData other)
            {
                return StableHash == other.StableHash;
            }

            [ExcludeFromBurstCompatTesting("Takes managed object")]
            public override bool Equals(object obj)
            {
                return obj is ArchetypeMemoryData archetypeData ? Equals(archetypeData) : false;
            }

            public override int GetHashCode()
            {
                return StableHash.GetHashCode();
            }

            public static bool operator ==(ArchetypeMemoryData lhs, ArchetypeMemoryData rhs)
            {
                return lhs.Equals(rhs);
            }

            public static bool operator !=(ArchetypeMemoryData lhs, ArchetypeMemoryData rhs)
            {
                return !lhs.Equals(rhs);
            }
        }
    }
}
#endif
