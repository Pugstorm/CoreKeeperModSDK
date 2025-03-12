using System;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Unity.Physics.GraphicsIntegration
{
    /// <summary>
    /// A system that writes to a rigid body's <see cref="PhysicsGraphicalInterpolationBuffer"/>
    /// component by copying its <see cref="Unity.Transforms.LocalTransform"/> and <see cref="PhysicsVelocity"/>
    /// before physics steps. These values are used for bodies whose graphics representations will be
    /// interpolated by the <see cref="SmoothRigidBodiesGraphicalMotion"/> system. Add a <c>
    /// WriteGroupAttribute</c> to your own component if you need to use different values (as with a
    /// character controller).
    ///
    /// NOTE: Consider the case when an interpolated rigid body needs to be teleported (i.e. have its
    /// <see cref="Unity.Transforms.LocalTransform"/> or <see cref="PhysicsVelocity"/> components changed directly), specifically
    /// after this system is updated and before <see cref="SmoothRigidBodiesGraphicalMotion"/> is
    /// updated. In that case, you should set associated <see cref="PhysicsGraphicalSmoothing.ApplySmoothing"/>
    /// to 0. or assign the appropriate new <see cref="PhysicsGraphicalInterpolationBuffer"/>
    /// component values as well.
    /// </summary>
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsInitializeGroup)), UpdateBefore(typeof(ExportPhysicsWorld))]
    [BurstCompile]
    public partial struct BufferInterpolatedRigidBodiesMotion : ISystem
    {
        ComponentTypeHandle<LocalTransform> m_LocalTransformType;
        ComponentTypeHandle<PhysicsVelocity> m_PhysicsVelocityType;
        ComponentTypeHandle<PhysicsGraphicalInterpolationBuffer> m_InterpolationBufferType;

        /// <summary>
        /// An entity query matching dynamic rigid bodies whose graphical motion should be interpolated.
        /// </summary>
        public EntityQuery InterpolatedDynamicBodiesQuery { get; private set; }

        public void OnCreate(ref SystemState state)
        {
            InterpolatedDynamicBodiesQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PhysicsVelocity, LocalTransform,PhysicsWorldIndex>()
                .WithAllRW<PhysicsGraphicalInterpolationBuffer>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(ref state);
            state.RequireForUpdate(InterpolatedDynamicBodiesQuery);

            m_LocalTransformType = state.GetComponentTypeHandle<LocalTransform>(true);
            m_PhysicsVelocityType = state.GetComponentTypeHandle<PhysicsVelocity>(true);
            m_InterpolationBufferType = state.GetComponentTypeHandle<PhysicsGraphicalInterpolationBuffer>();

            // UpdateInterpolationBuffersJob copies from specific byte offsets of the transform components, so
            // let's make sure the offsets haven't changed!
            Assert.AreEqual(0, UnsafeUtility.GetFieldOffset(typeof(LocalTransform).GetField("Position")));
            Assert.AreEqual(UnsafeUtility.SizeOf<float3>() + UnsafeUtility.SizeOf<float>(),
                UnsafeUtility.GetFieldOffset(typeof(LocalTransform).GetField("Rotation")));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var bpwd = SystemAPI.GetSingleton<BuildPhysicsWorldData>();
            InterpolatedDynamicBodiesQuery.SetSharedComponentFilter(bpwd.WorldFilter);
            m_LocalTransformType.Update(ref state);
            m_PhysicsVelocityType.Update(ref state);
            m_InterpolationBufferType.Update(ref state);
            state.Dependency = new UpdateInterpolationBuffersJob
            {
                LocalTransformType = m_LocalTransformType,
                PhysicsVelocityType = m_PhysicsVelocityType,
                InterpolationBufferType = m_InterpolationBufferType
            }.ScheduleParallel(InterpolatedDynamicBodiesQuery, state.Dependency);
        }

        /// <summary>
        /// An <see cref="IJobChunk"/> which updates <see cref="PhysicsGraphicalInterpolationBuffer"/> of the entities in
        /// chunk specified by <see cref="PhysicsVelocity"/>, <see cref="Unity.Transforms.LocalTransform"/>
        /// and <see cref="PhysicsGraphicalInterpolationBuffer"/>.
        /// </summary>
        [BurstCompile]
        public struct UpdateInterpolationBuffersJob : IJobChunk
        {
            /// <summary>   Physics velocity component type handle. (Readonly) </summary>
            [ReadOnly] public ComponentTypeHandle<PhysicsVelocity> PhysicsVelocityType;

            /// <summary>   Transform component type handle. (Readonly) </summary>
            [ReadOnly] public ComponentTypeHandle<LocalTransform> LocalTransformType;

            /// <summary>   PhysicsGraphicalInterpolationBuffer component type handle.. </summary>
            public ComponentTypeHandle<PhysicsGraphicalInterpolationBuffer> InterpolationBufferType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                NativeArray<PhysicsVelocity> physicsVelocities = chunk.GetNativeArray(ref PhysicsVelocityType);

                NativeArray<LocalTransform> localTransforms = chunk.GetNativeArray(ref LocalTransformType);

                NativeArray<PhysicsGraphicalInterpolationBuffer> interpolationBuffers = chunk.GetNativeArray(ref InterpolationBufferType);

                unsafe
                {

                    var srcTransforms = localTransforms.GetUnsafeReadOnlyPtr();

                    var dst = interpolationBuffers.GetUnsafePtr();
                    var count = chunk.Count;

                    var sizeBuffer = UnsafeUtility.SizeOf<PhysicsGraphicalInterpolationBuffer>();

                    var sizeTransform = UnsafeUtility.SizeOf<LocalTransform>();

                    var sizeOrientation = UnsafeUtility.SizeOf<quaternion>();
                    var sizePosition = UnsafeUtility.SizeOf<float3>();
                    var sizeVelocity = UnsafeUtility.SizeOf<PhysicsVelocity>();

                    // These hardcoded byte offsets into LocalTransform are validated in OnCreate()
                    int srcPositionOffset = 0;
                    int srcOrientationOffset = sizePosition + UnsafeUtility.SizeOf<float>();
                    var srcPositions = (void*)((long)localTransforms.GetUnsafeReadOnlyPtr() + srcPositionOffset);
                    var srcOrientations = (void*)((long)localTransforms.GetUnsafeReadOnlyPtr() + srcOrientationOffset);
                    var dstOrientations = dst;
                    var dstPositions = (void*)((long)dst + sizeOrientation);
                    var dstVelocities = (void*)((long)dst + sizeOrientation + sizePosition);
                    UnsafeUtility.MemCpyStride(
                        dstOrientations, sizeBuffer,
                        srcOrientations, sizeTransform,
                        sizeOrientation, count
                    );
                    UnsafeUtility.MemCpyStride(
                        dstPositions, sizeBuffer,
                        srcPositions, sizeTransform,
                        sizePosition, count
                    );
                    UnsafeUtility.MemCpyStride(
                        dstVelocities, sizeBuffer,
                        physicsVelocities.GetUnsafeReadOnlyPtr(), sizeVelocity,
                        sizeVelocity, count
                    );

                }
            }
        }
    }
}
