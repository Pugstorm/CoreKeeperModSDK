using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Baking;

namespace Unity.Entities
{
    /// <summary>
    /// Stores the state of the baker (Added components, Created entities and all recorded dependencies)
    /// The purpose of it is to be able to revert the changes when the baker needs to run again because a dependency changed
    /// or if the component / game object was destroyed.
    /// </summary>
    internal struct BakerState : IDisposable
    {
        internal UnsafeList<TypeIndex>                 AddedComponents;
        /// <summary>
        /// Unordered set of valid entities created by a baker. Includes the primary entity.
        /// </summary>
        /// <remarks>
        /// This set is always in sync with the ordered list of entities.
        /// Its main purpose is to speed up the checks of entity validity.
        /// </remarks>
        internal UnsafeHashSet<Entity>                 Entities;
        internal Entity                                PrimaryEntity;

#if UNITY_EDITOR
        internal UnsafeParallelHashSet<int>                    ReferencedPrefabs;
#endif

        internal BakeDependencies.RecordedDependencies Dependencies;
        internal BakerEntityUsage                      Usage;

        public BakerState(Entity entity, Allocator allocator)
        {
            AddedComponents = new UnsafeList<TypeIndex>(0, allocator);
            Entities = new UnsafeHashSet<Entity>(1, allocator);
            Entities.Add(entity);
            PrimaryEntity = entity;
#if UNITY_EDITOR
            ReferencedPrefabs = new UnsafeParallelHashSet<int>(1, allocator);
#endif
            Dependencies = new BakeDependencies.RecordedDependencies(0, allocator);

            Usage = new BakerEntityUsage(entity, 0, allocator);
        }

        public void Revert(EntityCommandBuffer ecb, Entity newPrimaryEntity, ref UnsafeParallelHashMap<Entity, TransformUsageFlagCounters> transformUsage, BlobAssetStore blobAssetStore, ref BakerDebugState bakerDebugState, ref bool dirtyEntityUsage, ref BakedEntityData bakedEntityData)
        {
            var oldPrimaryEntity = PrimaryEntity;

            // Revert components on primary entity
            foreach (var typeIndex in AddedComponents)
            {
                ecb.RemoveComponent(oldPrimaryEntity, ComponentType.FromTypeIndex(typeIndex));

                // Remove the entity-component pair from the debug state, so the component can be readded
                var entityComponentPair = new BakerDebugState.EntityComponentPair(oldPrimaryEntity, typeIndex);
                bakerDebugState.addedComponentsByEntity.Remove(entityComponentPair);
            }

            // Destroy additional entities
            foreach (var entity in Entities)
            {
                if (PrimaryEntity == entity)
                    continue;

                transformUsage.Remove(entity);
                ecb.DestroyEntity(entity);
                dirtyEntityUsage = true;
            }

#if UNITY_EDITOR
            var enumeratorPrefabs = ReferencedPrefabs.GetEnumerator();
            while (enumeratorPrefabs.MoveNext())
            {
                var prefabInstanceId = enumeratorPrefabs.Current;
                bakedEntityData.RemovePrefabRef(prefabInstanceId);
            }
            enumeratorPrefabs.Dispose();
            ReferencedPrefabs.Clear();
#endif

            AddedComponents.Clear();
            PrimaryEntity = newPrimaryEntity;
            Entities.Clear();
            Entities.Add(newPrimaryEntity);
        }

        public void Dispose()
        {
            AddedComponents.Dispose();
            Entities.Dispose();
            Dependencies.Dispose();
            Usage.Dispose();

#if UNITY_EDITOR
            ReferencedPrefabs.Dispose();
#endif
        }

        public UnsafeHashSet<Entity> GetEntities()
        {
            return Entities;
        }

        public Entity GetPrimaryEntity()
        {
            return PrimaryEntity;
        }
    }
}
