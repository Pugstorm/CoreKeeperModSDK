using Unity.Assertions;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;

namespace Unity.NetCode
{
    /// <summary>
    /// A struct that is temporarily added to a ghosts entity when it switching between predicted / interpolated mode.
    /// Added by <see cref="GhostPredictionSwitchingSystem"/> while processing the <see cref="GhostPredictionSwitchingQueues"/>.
    /// </summary>
    [WriteGroup(typeof(LocalToWorld))]
    public struct SwitchPredictionSmoothing : IComponentData
    {
        /// <summary>
        /// The initial position of the ghost (in world space).
        /// </summary>
        public float3 InitialPosition;
        /// <summary>
        /// The initial rotation of the ghost (in world space).
        /// </summary>
        public quaternion InitialRotation;
        /// <summary>
        /// The smoothing fraction to apply to the current transform. Always in between 0 and 1f.
        /// </summary>
        public float CurrentFactor;
        /// <summary>
        /// The duration in second of the transition. Setup when the component is added and then remain constant.
        /// </summary>
        public float Duration;
        /// <summary>
        /// The current version of the system when the component added to entity.
        /// </summary>
        public uint SkipVersion;
    }

    /// <summary>
    /// System that manage the prediction transition for all ghost that present a <see cref="SwitchPredictionSmoothing"/>
    /// components.
    /// <para>
    /// The system applying a visual smoohting to the ghost, by modifying the entity <see cref="LocalToWorld"/> matrix.
    /// When the transition is completed, the system removes the <see cref="SwitchPredictionSmoothing"/> component.
    /// </para>
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(LocalToWorldSystem))]
    [BurstCompile]
    public partial struct SwitchPredictionSmoothingSystem : ISystem
    {
        EntityQuery m_SwitchPredictionSmoothingQuery;

        EntityTypeHandle m_EntityTypeHandle;
        ComponentTypeHandle<LocalTransform> m_TransformHandle;
        ComponentTypeHandle<PostTransformMatrix> m_PostTransformMatrixType;
        ComponentTypeHandle<SwitchPredictionSmoothing> m_SwitchPredictionSmoothingHandle;
        ComponentTypeHandle<LocalToWorld> m_LocalToWorldHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<LocalTransform>()
                .WithAllRW<SwitchPredictionSmoothing, LocalToWorld>();
            m_SwitchPredictionSmoothingQuery = state.GetEntityQuery(builder);
            state.RequireForUpdate(m_SwitchPredictionSmoothingQuery);

            m_EntityTypeHandle = state.GetEntityTypeHandle();
            m_TransformHandle = state.GetComponentTypeHandle<LocalTransform>(true);
            m_PostTransformMatrixType = state.GetComponentTypeHandle<PostTransformMatrix>(true);
            m_SwitchPredictionSmoothingHandle = state.GetComponentTypeHandle<SwitchPredictionSmoothing>();
            m_LocalToWorldHandle = state.GetComponentTypeHandle<LocalToWorld>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            m_EntityTypeHandle.Update(ref state);
            m_TransformHandle.Update(ref state);
            m_PostTransformMatrixType.Update(ref state);
            m_SwitchPredictionSmoothingHandle.Update(ref state);
            m_LocalToWorldHandle.Update(ref state);

            state.Dependency = new SwitchPredictionSmoothingJob
            {
                EntityType = m_EntityTypeHandle,
                TransformType = m_TransformHandle,
                PostTransformMatrixType = m_PostTransformMatrixType,
                SwitchPredictionSmoothingType = m_SwitchPredictionSmoothingHandle,
                LocalToWorldType = m_LocalToWorldHandle,
                DeltaTime = deltaTime,
                AppliedVersion = SystemAPI.GetSingleton<GhostUpdateVersion>().LastSystemVersion,
                CommandBuffer = commandBuffer.AsParallelWriter(),
            }.ScheduleParallel(m_SwitchPredictionSmoothingQuery, state.Dependency);
        }

        [BurstCompile]
        struct SwitchPredictionSmoothingJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformType;
            [ReadOnly] public ComponentTypeHandle<PostTransformMatrix> PostTransformMatrixType;
            public ComponentTypeHandle<SwitchPredictionSmoothing> SwitchPredictionSmoothingType;
            public ComponentTypeHandle<LocalToWorld> LocalToWorldType;
            public float DeltaTime;
            public uint AppliedVersion;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);

                NativeArray<LocalTransform> transforms = chunk.GetNativeArray(ref TransformType);
                NativeArray<PostTransformMatrix> postTransformMatrices = new NativeArray<PostTransformMatrix>();
                if (chunk.Has(ref PostTransformMatrixType))
                    postTransformMatrices = chunk.GetNativeArray(ref PostTransformMatrixType);

                NativeArray<SwitchPredictionSmoothing> switchPredictionSmoothings = chunk.GetNativeArray(ref SwitchPredictionSmoothingType);
                NativeArray<LocalToWorld> localToWorlds = chunk.GetNativeArray(ref LocalToWorldType);
                NativeArray<Entity> chunkEntities = chunk.GetNativeArray(EntityType);

                for (int i = 0, count = chunk.Count; i < count; ++i)
                {
                    var currentPosition = transforms[i].Position;
                    var currentRotation = transforms[i].Rotation;

                    var smoothing = switchPredictionSmoothings[i];
                    if (smoothing.SkipVersion != AppliedVersion)
                    {
                        if (smoothing.CurrentFactor == 0)
                        {
                            smoothing.InitialPosition = transforms[i].Position - smoothing.InitialPosition;
                            smoothing.InitialRotation = math.mul(transforms[i].Rotation, math.inverse(smoothing.InitialRotation));
                        }

                        smoothing.CurrentFactor = math.saturate(smoothing.CurrentFactor + DeltaTime / smoothing.Duration);
                        switchPredictionSmoothings[i] = smoothing;
                        if (smoothing.CurrentFactor == 1)
                        {
                            CommandBuffer.RemoveComponent<SwitchPredictionSmoothing>(unfilteredChunkIndex, chunkEntities[i]);
                        }

                        currentPosition -= math.lerp(smoothing.InitialPosition, new float3(0,0,0), smoothing.CurrentFactor);
                        currentRotation = math.mul(currentRotation, math.inverse(math.slerp(smoothing.InitialRotation, quaternion.identity, smoothing.CurrentFactor)));
                    }

                    var tr = new float4x4(currentRotation, currentPosition);
                    if (math.distance(transforms[i].Scale, 1f) > 1e-4f)
                    {
                        var scale = float4x4.Scale(new float3(transforms[i].Scale));
                        tr = math.mul(tr, scale);
                    }
                    //TODO: is there a fast way to check if the postTransformMatrix is the identity?
                    if(postTransformMatrices.IsCreated)
                        tr = math.mul(tr, postTransformMatrices[i].Value);

                    localToWorlds[i] = new LocalToWorld { Value = tr };
                }
            }
        }
    }
}
