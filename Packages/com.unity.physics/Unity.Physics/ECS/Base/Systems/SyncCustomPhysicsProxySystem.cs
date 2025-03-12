using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;

namespace Unity.Physics.Systems
{
    /// <summary>
    /// Synchronize the movement of the custom physics proxy using kinematic velocities.
    /// The kinematic entity is moved from its current position/rotation to the position/rotation of the driving entity in one frame, by computing the
    /// necessary angular and linear velocities.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PhysicsInitializeGroup))]
    [BurstCompile]
    public partial struct SyncCustomPhysicsProxySystem : ISystem
    {

        private ComponentLookup<LocalTransform> m_LocalTransformLookup;

        private EntityQuery m_Query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {

            m_LocalTransformLookup = state.GetComponentLookup<LocalTransform>(false);


            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PhysicsMass, CustomPhysicsProxyDriver, PhysicsWorldIndex>()

                .WithAllRW<LocalTransform>()

                .WithAllRW<PhysicsVelocity>();

            m_Query = state.GetEntityQuery(builder);

            state.RequireForUpdate(m_Query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {

            m_LocalTransformLookup.Update(ref state);

            m_Query.SetSharedComponentFilter(SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorldIndex);

            var job = new SyncCustomPhysicsProxy
            {

                localTransformLookup = m_LocalTransformLookup,

                updateFrequency = math.rcp(SystemAPI.Time.DeltaTime)
            };
            state.Dependency = job.ScheduleParallel(m_Query, state.Dependency);
        }

        partial struct SyncCustomPhysicsProxy : IJobEntity
        {
            [NativeDisableParallelForRestriction]

            public ComponentLookup<LocalTransform> localTransformLookup;


            public float updateFrequency;

            public void Execute(Entity entity, ref PhysicsVelocity physicsVelocity,
                in PhysicsMass physicsMass, in CustomPhysicsProxyDriver proxyDriver)
            {

                var localTransform = localTransformLookup[entity];
                var targetTransform = new RigidTransform(localTransformLookup[proxyDriver.rootEntity].Rotation, localTransformLookup[proxyDriver.rootEntity].Position);


                // First order changes - position/rotation
                if (proxyDriver.FirstOrderGain != 0.0f)
                {

                    localTransform.Position = math.lerp(localTransform.Position, targetTransform.pos, proxyDriver.FirstOrderGain);
                    localTransform.Rotation = math.slerp(localTransform.Rotation, targetTransform.rot, proxyDriver.FirstOrderGain);

                    localTransformLookup[entity] = localTransform;

                }

                // Second order changes - velocity
                if (proxyDriver.FirstOrderGain != 1.0f)
                {

                    physicsVelocity = PhysicsVelocity.CalculateVelocityToTarget(physicsMass, localTransform.Position, localTransform.Rotation, targetTransform, updateFrequency);

                }
            }
        }
    }
}
