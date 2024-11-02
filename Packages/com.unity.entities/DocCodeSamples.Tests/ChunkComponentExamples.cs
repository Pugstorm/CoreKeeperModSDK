using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;

namespace Doc.CodeSamples.Tests
{
    #region declare-chunk-component

    public struct ChunkComponentA : IComponentData
    {
        public float Value;
    }
    #endregion

    #region full-chunk-example

    [RequireMatchingQueriesForUpdate]
    public partial class ChunkComponentExamples : SystemBase
    {
        private EntityQuery ChunksWithChunkComponentA;
        protected override void OnCreate()
        {
            ChunksWithChunkComponentA = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllChunkComponentRW<ChunkComponentA>()
                    .Build(this);
        }

        [BurstCompile]
        struct ChunkComponentCheckerJob : IJobChunk
        {
            public ComponentTypeHandle<ChunkComponentA> ChunkComponentATypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var compValue = chunk.GetChunkComponentData(ref ChunkComponentATypeHandle);
                var squared = compValue.Value * compValue.Value;

                chunk.SetChunkComponentData(ref ChunkComponentATypeHandle, new ChunkComponentA() { Value = squared });
            }
        }

        protected override void OnUpdate()
        {
            var job = new ChunkComponentCheckerJob()
            {
                ChunkComponentATypeHandle
                    = GetComponentTypeHandle<ChunkComponentA>()
            };
            this.Dependency
                = job.ScheduleParallel(ChunksWithChunkComponentA, this.Dependency);
        }
    }
    #endregion

    #region aabb-chunk-component

    public struct ChunkAABB : IComponentData
    {
        public AABB Value;
    }

    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(UpdateAABBSystem))]
    public partial class AddAABBSystem : SystemBase
    {
        EntityQuery queryWithoutChunkComponent;
        protected override void OnCreate()
        {
            queryWithoutChunkComponent = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<LocalToWorld>()
                    .WithNoneChunkComponent<ChunkAABB>()
                    .Build(this);
        }

        protected override void OnUpdate()
        {
            // This is a structural change and a sync point
            EntityManager.AddChunkComponentData<ChunkAABB>(
                queryWithoutChunkComponent,
                new ChunkAABB()
            );
        }
    }

    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class UpdateAABBSystem : SystemBase
    {
        EntityQuery queryWithChunkComponent;
        protected override void OnCreate()
        {
            queryWithChunkComponent = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<LocalToWorld>()
                    .WithAllChunkComponentRW<ChunkAABB>()
                    .Build(this);
        }

        [BurstCompile]
        struct AABBJob : IJobChunk
        {
            [ReadOnly]
            public ComponentTypeHandle<LocalToWorld> LocalToWorldTypeHandleInfo;
            public ComponentTypeHandle<ChunkAABB> ChunkAabbTypeHandleInfo;
            public uint L2WChangeVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);

                bool chunkHasChanges
                    = chunk.DidChange(ref LocalToWorldTypeHandleInfo,
                        L2WChangeVersion);

                if (!chunkHasChanges)
                    return; // early out if the chunk transforms haven't changed

                NativeArray<LocalToWorld> transforms
                    = chunk.GetNativeArray<LocalToWorld>(ref LocalToWorldTypeHandleInfo);
                UnityEngine.Bounds bounds = new UnityEngine.Bounds();
                bounds.center = transforms[0].Position;
                for (int i = 1; i < transforms.Length; i++)
                {
                    bounds.Encapsulate(transforms[i].Position);
                }
                chunk.SetChunkComponentData(
                    ref ChunkAabbTypeHandleInfo,
                    new ChunkAABB() { Value = bounds.ToAABB() });
            }
        }

        protected override void OnUpdate()
        {
            var job = new AABBJob()
            {
                LocalToWorldTypeHandleInfo
                    = GetComponentTypeHandle<LocalToWorld>(true),
                ChunkAabbTypeHandleInfo
                    = GetComponentTypeHandle<ChunkAABB>(false),
                L2WChangeVersion = this.LastSystemVersion
            };
            this.Dependency
                = job.ScheduleParallel(queryWithChunkComponent, this.Dependency);
        }
    }
    #endregion

    //snippets
    public partial class ChunkComponentSnippets : SystemBase
    {
        protected override void OnUpdate()
        {
            throw new System.NotImplementedException();
        }

        private void snippets()
        {
            #region component-list-chunk-component

            ComponentType[] compTypes = {
                ComponentType.ChunkComponent<ChunkComponentA>(),
                ComponentType.ReadOnly<GeneralPurposeComponentA>()
            };
            Entity entity = EntityManager.CreateEntity(compTypes);
            #endregion

            #region em-snippet

            EntityManager.AddChunkComponentData<ChunkComponentA>(entity);
            #endregion

            #region desc-chunk-component

            EntityQuery ChunksWithoutChunkComponentA = new EntityQueryBuilder(Allocator.Temp)
                    .WithNoneChunkComponent<ChunkComponentA>()
                    .Build(this);

            EntityManager.AddChunkComponentData<ChunkComponentA>(
                ChunksWithoutChunkComponentA,
                new ChunkComponentA() { Value = 4 });
            #endregion

            #region use-chunk-component

            EntityQuery ChunksWithChunkComponentA = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllChunkComponentRW<ChunkComponentA>()
                    .Build(this);
            #endregion

            #region archetype-chunk-component

            EntityArchetype ArchetypeWithChunkComponent
                = EntityManager.CreateArchetype(
                ComponentType.ChunkComponent(typeof(ChunkComponentA)),
                ComponentType.ReadWrite<GeneralPurposeComponentA>());
            Entity newEntity
                = EntityManager.CreateEntity(ArchetypeWithChunkComponent);
            #endregion
            {
                #region read-chunk-component

                NativeArray<ArchetypeChunk> chunks
                    = ChunksWithChunkComponentA.ToArchetypeChunkArray(
                        Allocator.TempJob);

                foreach (var chunk in chunks)
                {
                    var compValue =
                     EntityManager.GetChunkComponentData<ChunkComponentA>(chunk);
                    //..
                }
                chunks.Dispose();
                #endregion
            }

            #region read-entity-chunk-component

            if (EntityManager.HasChunkComponent<ChunkComponentA>(entity))
            {
                ChunkComponentA chunkComponentValue =
                 EntityManager.GetChunkComponentData<ChunkComponentA>(entity);
            }
            #endregion

            {
                ArchetypeChunk chunk = default;
                #region set-chunk-component

                EntityManager.SetChunkComponentData<ChunkComponentA>(
                    chunk, new ChunkComponentA() { Value = 7 });
                #endregion
            }

            #region set-entity-chunk-component

            var entityChunk = EntityManager.GetChunk(entity);
            EntityManager.SetChunkComponentData<ChunkComponentA>(
                entityChunk,
                new ChunkComponentA() { Value = 8 });
            #endregion
        }
    }
}
