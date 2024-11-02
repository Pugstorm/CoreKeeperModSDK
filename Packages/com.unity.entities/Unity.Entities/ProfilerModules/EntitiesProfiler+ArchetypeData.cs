using System;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Unity.Entities
{
    partial class EntitiesProfiler
    {
        /// <summary>
        /// Struct used to store per archetype information.
        /// The total size is 1024 bytes, which leaves enough room to store up to 111 component types.
        /// </summary>
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "ENABLE_PROFILER")]
        [StructLayout(LayoutKind.Explicit, Size = 1024)]
        public unsafe struct ArchetypeData : IEquatable<ArchetypeData>
        {
            [FieldOffset(0)] // 8 bytes
            public readonly ulong StableHash;

            [FieldOffset(8)] // 4 bytes
            public readonly int ChunkCapacity;

            [FieldOffset(12)] // 4 bytes
            public readonly int InstanceSize;

            [FieldOffset(16)] // 1008 bytes
            public readonly FixedComponentTypeDataList ComponentTypes;

            public ArchetypeData(Archetype* archetype)
            {
                StableHash = archetype->StableHash;
                ChunkCapacity = archetype->ChunkCapacity;
                InstanceSize = archetype->InstanceSize;
                ComponentTypes = new FixedComponentTypeDataList();
                for (var i = 0; i < archetype->TypesCount && i < ComponentTypes.Capacity; ++i)
                {
                    var typeIndex = archetype->Types[i].TypeIndex;
                    var stableTypeHash = TypeManager.GetTypeInfo(typeIndex).StableTypeHash;
                    var flags = TypeManager.IsChunkComponent(typeIndex) ? ComponentTypeFlags.ChunkComponent : ComponentTypeFlags.None;
                    ComponentTypes.Add(stableTypeHash, flags);
                }
            }

            public bool Equals(ArchetypeData other)
            {
                return StableHash == other.StableHash;
            }

            [ExcludeFromBurstCompatTesting("Takes managed object")]
            public override bool Equals(object obj)
            {
                return obj is ArchetypeData archetypeData ? Equals(archetypeData) : false;
            }

            public override int GetHashCode()
            {
                return StableHash.GetHashCode();
            }

            public static bool operator ==(ArchetypeData lhs, ArchetypeData rhs)
            {
                return lhs.Equals(rhs);
            }

            public static bool operator !=(ArchetypeData lhs, ArchetypeData rhs)
            {
                return !lhs.Equals(rhs);
            }
        }
    }
}
