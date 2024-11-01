using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using NUnit.Framework;
using Unity.Burst.Intrinsics;

// The files in this namespace are used to compile/test the code samples in the documentation.
namespace Doc.CodeSamples.Tests
{
    public struct VelocityVector: IComponentData
    {
        public float3 Value;
    }

    #region typical-struct
    [BurstCompile]
    public struct UpdateTranslationFromVelocityJob : IJobChunk
    {
        public ComponentTypeHandle<VelocityVector> VelocityTypeHandle;
        public ComponentTypeHandle<ObjectPosition> PositionTypeHandle;
        public float DeltaTime;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<VelocityVector> velocityVectors = chunk.GetNativeArray(ref VelocityTypeHandle);
            NativeArray<ObjectPosition> translations = chunk.GetNativeArray(ref PositionTypeHandle);

            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while(enumerator.NextEntityIndex(out var i))
            {
                float3 translation = translations[i].Value;
                float3 velocity = velocityVectors[i].Value;
                float3 newTranslation = translation + velocity * DeltaTime;

                translations[i] = new ObjectPosition() { Value = newTranslation };
            }
        }
    }
    #endregion

    #region schedule-job
    [RequireMatchingQueriesForUpdate]
    public partial class UpdateTranslationFromVelocitySystem : SystemBase
    {
        EntityQuery query;

        protected override void OnCreate()
        {
            // Set up the query
            query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<ObjectPosition>()
                .WithAll<VelocityVector>()
                .Build(this);
        }

        protected override void OnUpdate()
        {
            // Instantiate the job struct
            var updateFromVelocityJob
                = new UpdateTranslationFromVelocityJob();

            // Set the job component type handles
            // "this" is your SystemBase subclass
            updateFromVelocityJob.PositionTypeHandle
                = this.GetComponentTypeHandle<ObjectPosition>(false);
            updateFromVelocityJob.VelocityTypeHandle
                = this.GetComponentTypeHandle<VelocityVector>(true);

            // Set other data need in job, such as time
            updateFromVelocityJob.DeltaTime = World.Time.DeltaTime;

            // Schedule the job
            this.Dependency
                = updateFromVelocityJob.ScheduleParallel(query, this.Dependency);
        }
        #endregion
    }

    //For extracting one or a few lines for the docs
    [BurstCompile]
    public struct UpdateTranslationAndAlignToVelocityJob : IJobChunk
    {
        public ComponentTypeHandle<VelocityVector> VelocityTypeHandle;
        #region component-handle
        public ComponentTypeHandle<ObjectPosition> PositionTypeHandle;
        #endregion
        public ComponentTypeHandle<Rotation> RotationTypeHandle;
        public ComponentTypeHandle<LocalToWorld> LocalToWorldTypeHandle;
        public float DeltaTime;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<VelocityVector> velocityVectors = chunk.GetNativeArray(ref VelocityTypeHandle);
            #region component-array
            NativeArray<ObjectPosition> translations = chunk.GetNativeArray(ref PositionTypeHandle);
            #endregion
            #region chunk-entity-enumerator
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while(enumerator.NextEntityIndex(out var i))
            #endregion
            {
                float3 translation = translations[i].Value;
                float3 velocity = velocityVectors[i].Value;
                float3 newTranslation = translation + velocity * DeltaTime;

                translations[i] = new ObjectPosition() { Value = newTranslation };
            }

            #region chunk-has-component
            // If entity has Rotation and LocalToWorld components,
            // slerp to align to the velocity vector
            NativeArray<Rotation> rotations = chunk.GetNativeArray(ref RotationTypeHandle);
            NativeArray<LocalToWorld> transforms = chunk.GetNativeArray(ref LocalToWorldTypeHandle);
            if (rotations.IsCreated && transforms.IsCreated)
            {
                // By putting the loop inside the check for the
                // optional components, we can check once per batch
                // rather than once per entity.
                var enumerator2 = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while(enumerator2.NextEntityIndex(out var i))
                {
                    float3 direction = math.normalize(velocityVectors[i].Value);
                    float3 up = transforms[i].Up;
                    quaternion rotation = rotations[i].Value;

                    quaternion look = quaternion.LookRotation(direction, up);
                    quaternion newRotation = math.slerp(rotation, look, DeltaTime);

                    rotations[i] = new Rotation() { Value = newRotation };
                }
            }
            #endregion
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial class OneLinerSystem : SystemBase
    {
        EntityQuery query;
        #region component-handle-system-declaration
        ComponentTypeHandle<ObjectPosition> positionTypeHandle;
        #endregion
        ComponentTypeHandle<Rotation> rotationTypeHandle;
        ComponentTypeHandle<VelocityVector> velocityTypeHandle;
        ComponentTypeHandle<LocalToWorld> localToWorldTypeHandle;

        protected override void OnCreate()
        {
            query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<ObjectPosition>()
                .WithAll<VelocityVector>()
                .Build(this);
            #region component-handle-system-initialization
            // "this" is your SystemBase subclass
            positionTypeHandle = this.GetComponentTypeHandle<ObjectPosition>(false);
            #endregion
            rotationTypeHandle = this.GetComponentTypeHandle<Rotation>(false);
            velocityTypeHandle = this.GetComponentTypeHandle<VelocityVector>(true);
            localToWorldTypeHandle = this.GetComponentTypeHandle<LocalToWorld>(true);
        }

        protected override void OnUpdate()
        {
            var updateFromVelocityJob
                = new UpdateTranslationAndAlignToVelocityJob();
            #region component-set-handle
            // "this" is your SystemBase subclass
            positionTypeHandle.Update(this);
            updateFromVelocityJob.PositionTypeHandle = positionTypeHandle;
            #endregion
            rotationTypeHandle.Update(this);
            velocityTypeHandle.Update(this);
            localToWorldTypeHandle.Update(this);
            updateFromVelocityJob.RotationTypeHandle = rotationTypeHandle;
            updateFromVelocityJob.VelocityTypeHandle = velocityTypeHandle;
            updateFromVelocityJob.LocalToWorldTypeHandle = localToWorldTypeHandle;
            updateFromVelocityJob.DeltaTime = World.Time.DeltaTime;

            this.Dependency = updateFromVelocityJob.ScheduleParallel(query, this.Dependency);
        }
    }

    #region skip-unchanged-chunks-job
    [BurstCompile]
    struct UpdateOnChangeJob : IJobChunk
    {
        public ComponentTypeHandle<InputA> InputATypeHandle;
        public ComponentTypeHandle<InputB> InputBTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Output> OutputTypeHandle;
        public uint LastSystemVersion;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var inputAChanged = chunk.DidChange(ref InputATypeHandle, LastSystemVersion);
            var inputBChanged = chunk.DidChange(ref InputBTypeHandle, LastSystemVersion);

            // If neither component changed, skip the current batch
            if (!(inputAChanged || inputBChanged))
                return;

            var inputAs = chunk.GetNativeArray(ref InputATypeHandle);
            var inputBs = chunk.GetNativeArray(ref InputBTypeHandle);
            var outputs = chunk.GetNativeArray(ref OutputTypeHandle);

            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while(enumerator.NextEntityIndex(out var i))
            {
                outputs[i] = new Output { Value = inputAs[i].Value + inputBs[i].Value };
            }
        }
    }
    #endregion

    #region skip-unchanged-chunks-system
    [RequireMatchingQueriesForUpdate]
    public partial class UpdateDataOnChangeSystem : SystemBase {

        EntityQuery query;

        protected override void OnUpdate()
        {
            var job = new UpdateOnChangeJob();

            job.LastSystemVersion = this.LastSystemVersion;

            job.InputATypeHandle = GetComponentTypeHandle<InputA>(true);
            job.InputBTypeHandle = GetComponentTypeHandle<InputB>(true);
            job.OutputTypeHandle = GetComponentTypeHandle<Output>(false);

            this.Dependency = job.ScheduleParallel(query, this.Dependency);
        }

        protected override void OnCreate()
        {
            query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<Output>()
                .WithAll<InputA, InputB>()
                .Build(this);
        }
    }
    #endregion
    [RequireMatchingQueriesForUpdate]
    public partial class UpdateFilteredDataSystem : SystemBase
    {

        #region filter-query
        EntityQuery query;

        protected override void OnCreate()
        {
            query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<Output>()
                .WithAll<InputA, InputB>()
                .Build(this);
            query.SetChangedVersionFilter(
                    new ComponentType[]
                    {
                        typeof(InputA),
                        typeof(InputB)
                    }
                );
        }
        #endregion

        protected override void OnUpdate()
        {
            var job = new UpdateOnChangeJob();

            job.LastSystemVersion = this.LastSystemVersion;

            job.InputATypeHandle = GetComponentTypeHandle<InputA>(true);
            job.InputBTypeHandle = GetComponentTypeHandle<InputB>(true);
            job.OutputTypeHandle = GetComponentTypeHandle<Output>(false);

            this.Dependency = job.ScheduleParallel(query, this.Dependency);
        }

    }
}
