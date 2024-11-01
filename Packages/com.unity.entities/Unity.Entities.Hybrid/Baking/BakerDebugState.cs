using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    /// <summary>
    /// Tracks the components that were added to each entity,
    /// this lets us give the user nice error messages if two bakers add the same component to an entity.
    /// </summary>
    internal struct BakerDebugState : IDisposable
    {
        internal struct EntityComponentPair : IEquatable<EntityComponentPair>
        {
            Entity    entity;
            TypeIndex componentTypeIndex;

            public EntityComponentPair(Entity _entity, TypeIndex _componentTypeIndex)
            {
                entity = _entity;
                componentTypeIndex = _componentTypeIndex;
            }

            public override int GetHashCode()
            {
                int mask = 0xffff & TypeManager.ClearFlagsMask;
                return entity.GetHashCode() ^ ((componentTypeIndex.Value & mask)<<16);
            }

            public bool Equals(EntityComponentPair rhs)
            {
                if (entity != rhs.entity)
                    return false;
                if (componentTypeIndex != rhs.componentTypeIndex)
                    return false;
                return true;
            }
        }

        public struct DebugState : IEquatable<DebugState>
        {
            public TypeIndex            TypeIndex;
            public int                  IndexInBakerArray;

            public bool Equals(DebugState other)
            {
                return TypeIndex.Equals(other.TypeIndex) && IndexInBakerArray == other.IndexInBakerArray;
            }
        }

        // map a tuple of (entity, component being added to entity) to authoring source instance id
        internal UnsafeParallelHashMap<EntityComponentPair, DebugState> addedComponentsByEntity;

        public BakerDebugState(Allocator allocator)
        {
            addedComponentsByEntity = new UnsafeParallelHashMap<EntityComponentPair, DebugState>(10, allocator);
        }

        public void Reset()
        {
            addedComponentsByEntity.Clear();
        }

        public void Dispose()
        {
            addedComponentsByEntity.Dispose();
        }

        public void Clear()
        {
            addedComponentsByEntity.Clear();
        }

        public bool IsCreated
        {
            get { return addedComponentsByEntity.IsCreated; }
        }
    }
}
