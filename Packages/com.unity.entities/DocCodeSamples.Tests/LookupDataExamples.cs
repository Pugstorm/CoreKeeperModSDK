using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using NUnit.Framework;

// The files in this namespace are used to compile/test the code samples in the documentation.
namespace Doc.CodeSamples.Tests
{
    #region lookup-foreach
    [RequireMatchingQueriesForUpdate]
    public partial class TrackingSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            Entities
                .ForEach((ref Rotation orientation,
                in LocalToWorld transform,
                in Target target) =>
                {
                    // Check to make sure the target Entity still exists and has
                    // the needed component
                    if (!SystemAPI.HasComponent<LocalToWorld>(target.entity))
                        return;

                    // Look up the entity data
                    LocalToWorld targetTransform
                        = SystemAPI.GetComponent<LocalToWorld>(target.entity);
                    float3 targetPosition = targetTransform.Position;

                    // Calculate the rotation
                    float3 displacement = targetPosition - transform.Position;
                    float3 upReference = new float3(0, 1, 0);
                    quaternion lookRotation =
                        quaternion.LookRotationSafe(displacement, upReference);

                    orientation.Value =
                        math.slerp(orientation.Value, lookRotation, deltaTime);
                })
                .ScheduleParallel();
        }
    }
    #endregion
    #region lookup-foreach-buffer

    public struct BufferData : IBufferElementData
    {
        public float Value;
    }
    [RequireMatchingQueriesForUpdate]
    public partial class BufferLookupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            BufferLookup<BufferData> buffersOfAllEntities
                = this.GetBufferLookup<BufferData>(true);

            Entities
                .ForEach((ref Rotation orientation,
                in LocalToWorld transform,
                in Target target) =>
                {
                    // Check to make sure the target Entity with this buffer type still exists
                    if (!buffersOfAllEntities.HasBuffer(target.entity))
                        return;

                    // Get a reference to the buffer
                    DynamicBuffer<BufferData> bufferOfOneEntity =
                        buffersOfAllEntities[target.entity];

                    // Use the data in the buffer
                    float avg = 0;
                    for (var i = 0; i < bufferOfOneEntity.Length; i++)
                    {
                        avg += bufferOfOneEntity[i].Value;
                    }
                    if (bufferOfOneEntity.Length > 0)
                        avg /= bufferOfOneEntity.Length;
                })
                .ScheduleParallel();
        }
    }
    #endregion

    #region lookup-ijobchunk
    [RequireMatchingQueriesForUpdate]
    public partial class MoveTowardsEntitySystem : SystemBase
    {
        private EntityQuery query;

        [BurstCompile]
        private partial struct MoveTowardsJob : IJobEntity
        {

            // Read-only data stored (potentially) in other chunks
            #region lookup-ijobchunk-declare
            [ReadOnly]
            public ComponentLookup<LocalToWorld> EntityPositions;
            #endregion

            // Non-entity data
            public float deltaTime;

            public void Execute(ref LocalTransform transform, in Target target, in LocalToWorld entityPosition)
            {
                // Get the target Entity object
                Entity targetEntity = target.entity;

                // Check that the target still exists
                if (!EntityPositions.HasComponent(targetEntity))
                    return;

                // Update translation to move the chasing entity toward the target
                float3 targetPosition = entityPosition.Position;
                float3 chaserPosition = transform.Position;

                float3 displacement = targetPosition - chaserPosition;
                transform.Position = chaserPosition + displacement * deltaTime;
            }
        }

        protected override void OnCreate()
        {
            // Select all entities that have Translation and Target Component
            query = this.GetEntityQuery
                (
                    typeof(LocalTransform),
                    ComponentType.ReadOnly<Target>()
                );
        }

        protected override void OnUpdate()
        {
            // Create the job
            var job = new MoveTowardsJob();

            // Set the component data lookup field
            job.EntityPositions = GetComponentLookup<LocalToWorld>(true);

            // Set non-ECS data fields
            job.deltaTime = SystemAPI.Time.DeltaTime;

            // Schedule the job using Dependency property
            Dependency = job.ScheduleParallel(query, Dependency);
        }
    }
    #endregion

    [RequireMatchingQueriesForUpdate]
    public partial class Snippets : SystemBase
    {
        private EntityQuery query;
        protected override void OnCreate()
        {
            // Select all entities that have LocalTransform and Target Component
            query = this.GetEntityQuery(typeof(LocalTransform), ComponentType.ReadOnly<Target>());
        }

        [BurstCompile]
        private partial struct ChaserSystemJob : IJobEntity
        {
            // Non-entity data
            public float deltaTime;

            [ReadOnly]
            public ComponentLookup<LocalToWorld> EntityPositions;

            public void Execute(ref LocalTransform transform, in Target target, in LocalToWorld entityPosition)
            {
                var targetEntity = target.entity;

                // Check that the target still exists
                if (!EntityPositions.HasComponent(targetEntity))
                    return;

                // Update translation to move the chasing enitity toward the target
                #region lookup-ijobchunk-read
                float3 targetPosition = entityPosition.Position;
                float3 chaserPosition = transform.Position;
                float3 displacement = targetPosition - chaserPosition;
                float3 newPosition = chaserPosition + displacement * deltaTime;
                transform.Position = newPosition;
                #endregion

            }
        }

        #region lookup-ijobchunk-set
        protected override void OnUpdate()
        {
            var job = new ChaserSystemJob();

            // Set non-ECS data fields
            job.deltaTime = SystemAPI.Time.DeltaTime;

            // Schedule the job using Dependency property
            Dependency = job.ScheduleParallel(query, this.Dependency);
        }
        #endregion
    }
}
