using System;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics.Systems;

namespace Unity.Physics.GraphicsIntegration
{
    /// <summary>
    /// A system that writes to a body's <see cref="PhysicsGraphicalSmoothing"/> component by copying its <see cref="PhysicsVelocity"/> after physics has stepped.
    /// These values are used for bodies whose graphics representations will be smoothed by the <see cref="SmoothRigidBodiesGraphicalMotion"/> system.
    /// Add a <c>WriteGroupAttribute</c> to your own component if you need to use a different value (as with a character controller).
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup), OrderLast = true)]
    public partial struct CopyPhysicsVelocityToSmoothing : ISystem
    {
        ComponentTypeHandle<PhysicsVelocity> m_ComponentTypeHandle;
        ComponentTypeHandle<PhysicsGraphicalSmoothing> m_PhysicsGraphicalSmoothingType;

        /// <summary>
        /// An entity query matching dynamic rigid bodies whose motion should be smoothed.
        /// </summary>
        public EntityQuery SmoothedDynamicBodiesQuery { get; private set; }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            SmoothedDynamicBodiesQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PhysicsVelocity, PhysicsWorldIndex>()
                .WithAllRW<PhysicsGraphicalSmoothing>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(ref state);
            state.RequireForUpdate(SmoothedDynamicBodiesQuery);

            m_ComponentTypeHandle = state.GetComponentTypeHandle<PhysicsVelocity>(true);
            m_PhysicsGraphicalSmoothingType = state.GetComponentTypeHandle<PhysicsGraphicalSmoothing>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var bpwd = SystemAPI.GetSingleton<BuildPhysicsWorldData>();
            SmoothedDynamicBodiesQuery.SetSharedComponentFilter(bpwd.WorldFilter);
            m_ComponentTypeHandle.Update(ref state);
            m_PhysicsGraphicalSmoothingType.Update(ref state);
            state.Dependency = new CopyPhysicsVelocityJob
            {
                PhysicsVelocityType = m_ComponentTypeHandle,
                PhysicsGraphicalSmoothingType = m_PhysicsGraphicalSmoothingType
            }.ScheduleParallel(SmoothedDynamicBodiesQuery, state.Dependency);
        }

        /// <summary>   An <see cref="IJobChunk"/> which writes to a body's <see cref="PhysicsGraphicalSmoothing"/> by copying it's <see cref="PhysicsVelocity"/>. </summary>
        [BurstCompile]
        public struct CopyPhysicsVelocityJob : IJobChunk
        {
            /// <summary>   <see cref="PhysicsVelocity"/> component type handle. </summary>
            [ReadOnly] public ComponentTypeHandle<PhysicsVelocity> PhysicsVelocityType;
            /// <summary>   <see cref="PhysicsGraphicalSmoothing"/> component type handle. </summary>
            public ComponentTypeHandle<PhysicsGraphicalSmoothing> PhysicsGraphicalSmoothingType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                NativeArray<PhysicsVelocity> physicsVelocities = chunk.GetNativeArray(ref PhysicsVelocityType);
                NativeArray<PhysicsGraphicalSmoothing> physicsGraphicalSmoothings = chunk.GetNativeArray(ref PhysicsGraphicalSmoothingType);
                unsafe
                {
                    UnsafeUtility.MemCpyStride(
                        physicsGraphicalSmoothings.GetUnsafePtr(), UnsafeUtility.SizeOf<PhysicsGraphicalSmoothing>(),
                        physicsVelocities.GetUnsafeReadOnlyPtr(), UnsafeUtility.SizeOf<PhysicsVelocity>(),
                        UnsafeUtility.SizeOf<PhysicsVelocity>(),
                        physicsVelocities.Length
                    );
                }
            }
        }
    }
}
