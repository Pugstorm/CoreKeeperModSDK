using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DocCodeSamples.Tests
{

    #region SimpleSample
    public struct SampleComponent : IComponentData { public float Value; }
    public partial struct ASampleJob : IJobEntity
    {
        // Adds one to every SampleComponent value
        void Execute(ref SampleComponent sample)
        {
            sample.Value += 1f;
        }
    }

    public partial class ASample : SystemBase
    {
        protected override void OnUpdate()
        {
            // Schedules the job
            new ASampleJob().ScheduleParallel();
        }
    }
    #endregion

    #region Query
    partial struct QueryJob : IJobEntity
    {
        // Iterates over all SampleComponents and increments their value
        public void Execute(ref SampleComponent sample)
        {
            sample.Value += 1;
        }
    }

    [RequireMatchingQueriesForUpdate]
    public partial class QuerySystem : SystemBase
    {
        // Query that matches QueryJob, specified for `BoidTarget`
        EntityQuery query_boidtarget;

        // Query that matches QueryJob, specified for `BoidObstacle`
        EntityQuery query_boidobstacle;
        protected override void OnCreate()
        {
            // Query that contains all of Execute params found in `QueryJob` - as well as additional user specified component `BoidTarget`.
            query_boidtarget = GetEntityQuery(ComponentType.ReadWrite<SampleComponent>(),ComponentType.ReadOnly<BoidTarget>());

            // Query that contains all of Execute params found in `QueryJob` - as well as additional user specified component `BoidObstacle`.
            query_boidobstacle = GetEntityQuery(ComponentType.ReadWrite<SampleComponent>(),ComponentType.ReadOnly<BoidObstacle>());
        }

        protected override void OnUpdate()
        {
            // Uses the BoidTarget query
            new QueryJob().ScheduleParallel(query_boidtarget);

            // Uses the BoidObstacle query
            new QueryJob().ScheduleParallel(query_boidobstacle);

            // Uses query created automatically that matches parameters found in `QueryJob`.
            new QueryJob().ScheduleParallel();
        }
    }
    #endregion

    #region EntityIndexInQuery
    [RequireMatchingQueriesForUpdate]
    public partial class EntityInQuerySystem : SystemBase
    {
        // This query should match `CopyPositionsJob` parameters
        EntityQuery query;
        protected override void OnCreate()
        {
            // Get query that matches `CopyPositionsJob` parameters
            query = GetEntityQuery(ComponentType.ReadOnly<LocalToWorld>());
        }

        protected override void OnUpdate()
        {
            // Get a native array equal to the size of the amount of entities found by the query.
            var positions = new NativeArray<float3>(query.CalculateEntityCount(), World.UpdateAllocator.ToAllocator);

            // Schedule job on parallel threads for this array.
            new CopyPositionsJob{copyPositions = positions}.ScheduleParallel();

            // Dispose the array of positions found by the job.
            positions.Dispose(Dependency);
        }
    }
    #endregion

    #region Boids
    [BurstCompile]
    partial struct CopyPositionsJob : IJobEntity
    {
        public NativeArray<float3> copyPositions;

        // Iterates over all `LocalToWorld` and stores their position inside `copyPositions`.
        public void Execute([EntityIndexInQuery] int entityIndexInQuery, in LocalToWorld localToWorld)
        {
            copyPositions[entityIndexInQuery] = localToWorld.Position;
        }
    }
    
    [RequireMatchingQueriesForUpdate]
    public partial class BoidJobEntitySystem : SystemBase
    {
        EntityQuery m_BoidQuery;
        EntityQuery m_ObstacleQuery;
        EntityQuery m_TargetQuery;

        protected override void OnUpdate()
        {
            // Calculate amount of entities in respective queries.
            var boidCount = m_BoidQuery.CalculateEntityCount();
            var obstacleCount = m_ObstacleQuery.CalculateEntityCount();
            var targetCount = m_TargetQuery.CalculateEntityCount();

            // Allocate arrays to store data equal to the amount of entities matching respective queries.
            var cellSeparation = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(boidCount, ref World.UpdateAllocator);
            var copyTargetPositions = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(targetCount, ref World.UpdateAllocator);
            var copyObstaclePositions = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(obstacleCount, ref World.UpdateAllocator);

            // Schedule job for respective arrays to be stored with respective queries.
            new CopyPositionsJob { copyPositions = cellSeparation}.ScheduleParallel(m_BoidQuery);
            new CopyPositionsJob { copyPositions = copyTargetPositions}.ScheduleParallel(m_TargetQuery);
            new CopyPositionsJob { copyPositions = copyObstaclePositions}.ScheduleParallel(m_ObstacleQuery);
        }

        protected override void OnCreate()
        {
            // Get respective queries, that includes components required by `CopyPositionsJob` described earlier.
            m_BoidQuery = GetEntityQuery(typeof(LocalToWorld));
            m_BoidQuery.SetSharedComponentFilter(new BoidSetting{num=1});
            m_ObstacleQuery = GetEntityQuery(typeof(LocalToWorld), typeof(BoidObstacle));
            m_TargetQuery = GetEntityQuery(typeof(LocalToWorld), typeof(BoidTarget));;
        }
    }
    #endregion

    #region BoidsForEach
    [RequireMatchingQueriesForUpdate]
    public partial class BoidForEachSystem : SystemBase
    {
        EntityQuery m_BoidQuery;
        EntityQuery m_ObstacleQuery;
        EntityQuery m_TargetQuery;
        protected override void OnUpdate()
        {
            // Calculate amount of entities in respective queries.
            var boidCount = m_BoidQuery.CalculateEntityCount();
            var obstacleCount = m_ObstacleQuery.CalculateEntityCount();
            var targetCount = m_TargetQuery.CalculateEntityCount();

            // Allocate arrays to store data equal to the amount of entities matching respective queries.
            var cellSeparation = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(boidCount, ref World.UpdateAllocator);
            var copyTargetPositions = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(targetCount, ref World.UpdateAllocator);
            var copyObstaclePositions = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(obstacleCount, ref World.UpdateAllocator);

            // Schedule job for respective arrays to be stored with respective queries.
            Entities
                .WithSharedComponentFilter(new BoidSetting{num=1})
                .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld) =>
                {
                    cellSeparation[entityInQueryIndex] = localToWorld.Position;
                })
                .ScheduleParallel();

            Entities
                .WithAll<BoidTarget>()
                .WithStoreEntityQueryInField(ref m_TargetQuery)
                .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld) =>
                {
                    copyTargetPositions[entityInQueryIndex] = localToWorld.Position;
                })
                .ScheduleParallel();

            Entities
                .WithAll<BoidObstacle>()
                .WithStoreEntityQueryInField(ref m_ObstacleQuery)
                .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld) =>
                {
                    copyObstaclePositions[entityInQueryIndex] = localToWorld.Position;
                })
                .ScheduleParallel();
        }
    }
    #endregion

    public struct BoidObstacle : IComponentData { }
    public struct BoidTarget : IComponentData { }
    public struct BoidSetting : ISharedComponentData
    {
        public int num;
    }

    [InternalBufferCapacity(8)]
    public struct MyBufferInt : IBufferElementData
    {
        public int Value;
    }
}
