using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Unity.Physics.Systems
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    partial struct ColliderBlobCleanupSystem : ISystem
    {
        EntityQuery m_ColliderBlobCleanupOnUpdateQuery;
        EntityQuery m_ColliderBlobCleanupOnDestroyQuery;

        partial struct ColliderBlobCleanupJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            // Process all ColliderBlobCleanupData components on entities that don't have a PhysicsCollider anymore.
            // That is, the containing entity was destroyed but there is still cleanup work to do.
            // For all those, dispose of the collider blob.
            void Execute(in Entity entity, ref ColliderBlobCleanupData collider, [ChunkIndexInQuery] int chunkIndex)
            {
                if (collider.Value.IsCreated)
                {
                    collider.Value.Dispose();
                }

                ECB.RemoveComponent<ColliderBlobCleanupData>(chunkIndex, entity);
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

            m_ColliderBlobCleanupOnUpdateQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<ColliderBlobCleanupData>()
                .WithAbsent<PhysicsCollider>()
                .Build(ref state);
            m_ColliderBlobCleanupOnDestroyQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<ColliderBlobCleanupData>()
                .Build(ref state);
            state.RequireForUpdate(m_ColliderBlobCleanupOnUpdateQuery);
        }

        public void OnDestroy(ref SystemState state)
        {
            foreach (var blobCleanup in SystemAPI.Query<ColliderBlobCleanupData>())
            {
                var colliderBlob = blobCleanup.Value;
                if (colliderBlob.IsCreated)
                {
                    colliderBlob.Dispose();
                }
            }
            state.EntityManager.RemoveComponent<ColliderBlobCleanupData>(m_ColliderBlobCleanupOnDestroyQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            state.Dependency = new ColliderBlobCleanupJob
            {
                ECB = ecb.AsParallelWriter()
            }.ScheduleParallel(m_ColliderBlobCleanupOnUpdateQuery, state.Dependency);
        }
    }
}
