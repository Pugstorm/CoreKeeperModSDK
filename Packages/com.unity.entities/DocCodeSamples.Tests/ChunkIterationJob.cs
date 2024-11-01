using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst.Intrinsics;
using Unity.Assertions;

// The files in this namespace are used to compile/test the code samples in the documentation.
namespace Doc.CodeSamples.Tests
{
    //Snippets used in chunk_iteration_job.md
    public struct Rotation : IComponentData
    {
        public quaternion Value;
    }

    public struct RotationSpeed : IComponentData
    {
        public float RadiansPerSecond;
    }

    #region rotationspeedsystem

    public partial class RotationSpeedSystem : SystemBase
    {
        private EntityQuery m_Query;

        protected override void OnCreate()
        {
            m_Query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Rotation, RotationSpeed>()
                .Build(this);
            //...
        }

        #endregion

        [BurstCompile]
        struct RotationSpeedJob : IJobChunk
        {
            public float DeltaTime;
            public ComponentTypeHandle<Rotation> RotationTypeHandle;
            [ReadOnly] public ComponentTypeHandle<RotationSpeed> RotationSpeedTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                #region chunkiteration
                Assert.IsFalse(useEnabledMask); // this job is not written with enabled-bit support

                var chunkRotations = chunk.GetNativeArray(ref RotationTypeHandle);
                var chunkRotationSpeeds = chunk.GetNativeArray(ref RotationSpeedTypeHandle);
                for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; i++)
                {
                    var rotation = chunkRotations[i];
                    var rotationSpeed = chunkRotationSpeeds[i];

                    // Rotate something about its up vector at the speed given by RotationSpeed.
                    chunkRotations[i] = new Rotation
                    {
                        Value = math.mul(math.normalize(rotation.Value),
                            quaternion.AxisAngle(math.up(), rotationSpeed.RadiansPerSecond * DeltaTime))
                    };
                }

                #endregion
            }
        }

        #region schedulequery

        protected override void OnUpdate()
        {
            var job = new RotationSpeedJob()
            {
                RotationTypeHandle = GetComponentTypeHandle<Rotation>(false),
                RotationSpeedTypeHandle = GetComponentTypeHandle<RotationSpeed>(true),
                DeltaTime = SystemAPI.Time.DeltaTime
            };
            this.Dependency =  job.ScheduleParallel(m_Query, this.Dependency);
        }

        #endregion
    }


    public partial class RotationSpeedSystemExample2 : SystemBase
    {
        private EntityQuery m_Query;

        #region oncreate2

        protected override void OnCreate()
        {
            m_Query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<Rotation>()
                .WithAll<RotationSpeed>()
                .WithNone<Static>()
                .Build(this);
        }

        #endregion

        #region speedjob

        [BurstCompile]
        struct RotationSpeedJob : IJobChunk
        {
            public float DeltaTime;
            public ComponentTypeHandle<Rotation> RotationTypeHandle;
            [ReadOnly] public ComponentTypeHandle<RotationSpeed> RotationSpeedTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // ...
            }
        }

        #endregion

        protected override void OnUpdate()
        {
            var job = new RotationSpeedJob()
            {
                RotationTypeHandle = GetComponentTypeHandle<Rotation>(false),
                RotationSpeedTypeHandle = GetComponentTypeHandle<RotationSpeed>(true),
                DeltaTime = SystemAPI.Time.DeltaTime
            };

            this.Dependency =  job.ScheduleParallel(m_Query, this.Dependency);
        }
    }

    public partial class RotationSpeedSystemExample3 : SystemBase
    {
        private EntityQuery m_Query;

        #region oncreate3

        protected override void OnCreate()
        {
            m_Query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<Rotation>()
                .AddAdditionalQuery()
                .WithAllRW<RotationSpeed>()
                .Build(this);
        }

        #endregion

        [BurstCompile]
        struct RotationSpeedJob : IJobChunk
        {
            public float DeltaTime;
            public ComponentTypeHandle<Rotation> RotationTypeHandle;
            [ReadOnly] public ComponentTypeHandle<RotationSpeed> RotationSpeedTypeHandle;

            #region execsignature

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)

                #endregion
            {
                #region getcomponents

                var chunkRotations = chunk.GetNativeArray(ref RotationTypeHandle);
                var chunkRotationSpeeds = chunk.GetNativeArray(ref RotationSpeedTypeHandle);

                #endregion
            }
        }

        protected override void OnUpdate()
        {
            var job = new RotationSpeedJob()
            {
                RotationTypeHandle = GetComponentTypeHandle<Rotation>(false),
                RotationSpeedTypeHandle = GetComponentTypeHandle<RotationSpeed>(true),
                DeltaTime = SystemAPI.Time.DeltaTime
            };

            this.Dependency = job.ScheduleParallel(m_Query, this.Dependency);
        }
    }

    public struct Output : IComponentData
    {
        public float Value;
    }

    public struct InputA : IComponentData
    {
        public float Value;
    }

    public struct InputB : IComponentData
    {
        public float Value;
    }

    public partial class UpdateSystemExample : SystemBase
    {
        #region changefilter

        private EntityQuery m_Query;

        protected override void OnCreate()
        {
            m_Query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<Output>()
                .WithAll<InputA, InputB>()
                .Build(this);
            m_Query.SetChangedVersionFilter(
                new ComponentType[]
                {
                    ComponentType.ReadWrite<InputA>(),
                    ComponentType.ReadWrite<InputB>()
                });
        }

        #endregion

        #region changefilterjobstruct

        [BurstCompile]
        struct UpdateJob : IJobChunk
        {
            public ComponentTypeHandle<InputA> InputATypeHandle;
            public ComponentTypeHandle<InputB> InputBTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Output> OutputTypeHandle;
            public uint LastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask); // this job is not written with enabled-bit support
                var inputAChanged = chunk.DidChange(ref InputATypeHandle, LastSystemVersion);
                var inputBChanged = chunk.DidChange(ref InputBTypeHandle, LastSystemVersion);

                // If neither component changed, skip the current chunk
                if (!(inputAChanged || inputBChanged))
                    return;

                var inputAs = chunk.GetNativeArray(ref InputATypeHandle);
                var inputBs = chunk.GetNativeArray(ref InputBTypeHandle);
                var outputs = chunk.GetNativeArray(ref OutputTypeHandle);

                for (var i = 0; i < outputs.Length; i++)
                {
                    outputs[i] = new Output { Value = inputAs[i].Value + inputBs[i].Value };
                }
            }
        }

        #endregion

        #region changefilteronupdate

        protected override void OnUpdate()
        {
            var job = new UpdateJob();

            job.LastSystemVersion = this.LastSystemVersion;

            job.InputATypeHandle = GetComponentTypeHandle<InputA>(true);
            job.InputBTypeHandle = GetComponentTypeHandle<InputB>(true);
            job.OutputTypeHandle = GetComponentTypeHandle<Output>(false);

            this.Dependency = job.ScheduleParallel(m_Query, this.Dependency);
        }

        #endregion
    }

    #region basic-ijobchunk

    public struct ChaserPosition : IComponentData
    {
        public float3 Value;
    }

    public struct Target : IComponentData
    {
        public Entity entity;
    }

    public partial class ChaserSystem : SystemBase
    {
        private EntityQuery query; // Initialized in Oncreate()

        [BurstCompile]
        private struct ChaserSystemJob : IJobChunk
        {
            // Read-write data in the current chunk
            public ComponentTypeHandle<ChaserPosition> PositionTypeHandle;

            // Read-only data in the current chunk
            [ReadOnly]
            public ComponentTypeHandle<Target> TargetTypeHandle;

            // Read-only data stored (potentially) in other chunks
            [ReadOnly]
            //[NativeDisableParallelForRestriction]
            public ComponentLookup<LocalToWorld> EntityPositions;

            // Non-entity data
            public float deltaTime;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask); // this job is not written with enabled-bit support
                NativeArray<Target> targets = chunk.GetNativeArray<Target>(ref TargetTypeHandle);
                NativeArray<ChaserPosition> positions = chunk.GetNativeArray<ChaserPosition>(ref PositionTypeHandle);
                for (int i = 0; i < targets.Length; i++)
                {
                    Entity targetEntity = targets[i].entity;
                    float3 targetPosition = EntityPositions[targetEntity].Position;
                    float3 chaserPosition =
                        positions[i].Value;

                    float3 displacement = (targetPosition - chaserPosition);
                    positions[i] = new ChaserPosition { Value = chaserPosition + displacement * deltaTime };
                }
            }
        }

        protected override void OnCreate()
        {
            query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<ChaserPosition>()
                .WithAll<Target>()
                .Build(this);
        }

        protected override void OnUpdate()
        {
            var job = new ChaserSystemJob();
            job.PositionTypeHandle = this.GetComponentTypeHandle<ChaserPosition>(false);
            job.TargetTypeHandle = this.GetComponentTypeHandle<Target>(true);

            job.EntityPositions = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            job.deltaTime = SystemAPI.Time.DeltaTime;

            this.Dependency = job.ScheduleParallel(query, this.Dependency);
        }
    }
    #endregion

}

