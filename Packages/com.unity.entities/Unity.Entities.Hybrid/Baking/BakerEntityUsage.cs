using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    /// <summary>
    /// Tracks the Entity / Transform Usage on a per baker instance basis.
    /// Similar to dependencies in that we know previous and new state, if it changed we revert the old one and apply the new one.
    /// </summary>
    struct BakerEntityUsage : IEquatable<BakerEntityUsage>
    {
        internal struct ReferencedEntityUsage : IEquatable<ReferencedEntityUsage>
        {
            public Entity              Entity;
            public TransformUsageFlags Usage;

            public ReferencedEntityUsage(Entity e, TransformUsageFlags flags)
            {
                Entity = e;
                Usage = flags;
            }

            public bool Equals(ReferencedEntityUsage other)
            {
                return Entity.Equals(other.Entity) && Usage == other.Usage;
            }
        }

        internal Entity                                PrimaryEntity;
        internal TransformUsageFlagCounters            PrimaryEntityFlags;
        internal UnsafeList<ReferencedEntityUsage>     ReferencedEntityUsages;

        public BakerEntityUsage(Entity primaryEntity, int capacity, Allocator allocator)
        {
            ReferencedEntityUsages = new UnsafeList<ReferencedEntityUsage>(capacity, allocator);
            PrimaryEntityFlags = default;
            PrimaryEntity = primaryEntity;
        }

        public void Dispose()
        {
            ReferencedEntityUsages.Dispose();
        }

        public void Clear(Entity primaryEntity)
        {
            PrimaryEntity = primaryEntity;
            PrimaryEntityFlags = default;
            ReferencedEntityUsages.Clear();
        }

        public void AddTransformUsage(ref UnsafeParallelHashMap<Entity, TransformUsageFlagCounters> bakedEntityData, ref bool usageDirty, int component)
        {
            // Revert primary entity transform usage flags
            if (!PrimaryEntityFlags.IsUnused)
            {
                bakedEntityData.TryGetValue(PrimaryEntity, out var oldFlags);
                oldFlags.Add(PrimaryEntityFlags);
                //NOTE: when hashmap adds a ref returns API, we could avoid looking up the hash table twice
                bakedEntityData[PrimaryEntity] = oldFlags;

                //Debug.Log("Changing usage primary: " + UnityEngine.Resources.InstanceIDToObject(component));
            }

            // Revert other referenced entities transform usage flags
            for (int i = 0; i != ReferencedEntityUsages.Length; i++)
            {
                var e = ReferencedEntityUsages[i].Entity;
                bakedEntityData.TryGetValue(e, out var oldFlags);
                oldFlags.Add(ReferencedEntityUsages[i].Usage);
                //NOTE: when hashmap adds a ref returns API, we could avoid looking up the hash table twice
                bakedEntityData[e] = oldFlags;

                //Debug.Log("Changing usage referenced: " + UnityEngine.Resources.InstanceIDToObject(component));
            }

            usageDirty = true;
        }

        public void Revert(Entity newPrimaryEntity, ref UnsafeParallelHashMap<Entity, TransformUsageFlagCounters> entityUsage, ref bool usageDirty)
        {
            // Revert primary entity transform usage flags
            if (entityUsage.TryGetValue(PrimaryEntity, out var oldFlags))
            {
                oldFlags.Remove(PrimaryEntityFlags);
                if (oldFlags.IsUnused)
                    entityUsage.Remove(PrimaryEntity);
                else
                    //NOTE: when hashmap adds a ref returns API, we could avoid looking up the hash table twice
                    entityUsage[PrimaryEntity] = oldFlags;
            }

            // Revert other referenced entities transform usage flags
            for (int i = 0; i != ReferencedEntityUsages.Length; i++)
            {
                var referencedEntity = ReferencedEntityUsages[i].Entity;
                if (entityUsage.TryGetValue(referencedEntity, out oldFlags))
                {
                    oldFlags.Remove(ReferencedEntityUsages[i].Usage);
                    if (oldFlags.IsUnused)
                        entityUsage.Remove(referencedEntity);
                    else
                        //NOTE: when hashmap adds a ref returns API, we could avoid looking up the hash table twice
                        entityUsage[referencedEntity] = oldFlags;
                }
            }

            usageDirty = true;
            PrimaryEntity = newPrimaryEntity;
            PrimaryEntityFlags = default;
            ReferencedEntityUsages.Clear();
        }

        public bool Equals(BakerEntityUsage other)
        {
            return PrimaryEntity.Equals(other.PrimaryEntity) && PrimaryEntityFlags.Equals(other.PrimaryEntityFlags) && ReferencedEntityUsages.ArraysEqual(other.ReferencedEntityUsages);
        }

        void CopyFrom(in BakerEntityUsage input)
        {
            PrimaryEntity = input.PrimaryEntity;
            PrimaryEntityFlags = input.PrimaryEntityFlags;
            ReferencedEntityUsages.CopyFrom(input.ReferencedEntityUsages);
        }

        public static bool Update(ref UnsafeParallelHashMap<Entity, TransformUsageFlagCounters> referencedEntities, ref bool dirtyUsage, ref BakerEntityUsage bakerStateUsage, ref BakerEntityUsage tempUsage, int component, out bool revertTransformComponents)
        {
            if (bakerStateUsage.Equals(tempUsage))
            {
                revertTransformComponents = false;
                return false;
            }

            // Check if we moved from something else to ManualOverride as we will need to revert previous components
            revertTransformComponents = (tempUsage.PrimaryEntityFlags.HasManualOverrideFlag() &&
                                         !bakerStateUsage.PrimaryEntityFlags.HasManualOverrideFlag());

            bakerStateUsage.Revert(bakerStateUsage.PrimaryEntity, ref referencedEntities, ref dirtyUsage);
            bakerStateUsage.CopyFrom(tempUsage);
            bakerStateUsage.AddTransformUsage(ref referencedEntities, ref dirtyUsage, component);
            return true;
        }
    }
}
