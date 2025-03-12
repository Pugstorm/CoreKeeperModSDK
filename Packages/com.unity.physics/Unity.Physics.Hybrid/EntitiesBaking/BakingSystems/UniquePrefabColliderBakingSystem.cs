using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Assertions;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// System which ensures that colliders within entity prefabs that are flagged as "force unique" are tagged for
    /// cloning upon entity prefab instantiation, to ensure their uniqueness.
    /// The reason for this special treatment is that by design collider blobs in entity prefab instances can never be
    /// unique, as the blob references are copied upon entity prefab instantiation but not the blobs themselves.
    /// This leads to collider blobs being shared between entity prefab instances by definition, which is not the desired
    /// behavior for colliders that are flagged as "force unique".
    /// </summary>
    [UpdateAfter(typeof(EndColliderBakingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial struct UniquePrefabColliderBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Obtain all entities with colliders in baked entity prefabs and check if these have a "force unique" collider
            foreach (var(collider, entity) in SystemAPI.Query<RefRW<PhysicsCollider>>()
                     .WithAll<Prefab>()
                     .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
                     .WithEntityAccess())
            {
                ref var physicsCollider = ref collider.ValueRW;
                if (physicsCollider.IsUnique)
                {
                    ref var colliderBlob = ref physicsCollider.Value.Value;

                    // Reset the force unique id to the default (shared) to indicate to the user that the collider is not unique immediately after prefab instantiation.
                    colliderBlob.SetForceUniqueID(ColliderConstants.k_SharedBlobID);

                    // Make sure this worked
                    Assert.IsFalse(colliderBlob.IsUnique || physicsCollider.IsUnique);

                    // If the collider is unique, we need to tag the entity for later collider cloning to ensure the collider is effectively unique
                    // when the prefab gets instantiated.
                    ecb.AddComponent(entity, new EnsureUniqueColliderBlobTag());
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
