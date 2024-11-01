using Unity.Entities;
using Unity.Transforms;
using Unity.Burst;
using Unity.Collections;

namespace Doc.CodeSamples.Tests
{
    #region query-data
    public partial struct MyRotationSpeedSystem : ISystem
    {

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, speed) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>>())
                transform.ValueRW = transform.ValueRO.RotateY(speed.ValueRO.RadiansPerSecond * deltaTime);
        }
    }
    #endregion


    public partial struct RotationSpeedSystemAgain : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            #region query-data-alt
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, speed) in SystemAPI.Query<RefRW<LocalTransform>, RotationSpeed>())
                transform.ValueRW = transform.ValueRO.RotateY(speed.RadiansPerSecond * deltaTime);
            #endregion
        }
    }

    public partial struct AnotherRotationSpeedSystem : ISystem
    {

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            #region entity-access
            foreach (var (transform, speed, entity) in SystemAPI.Query<RefRW<LocalToWorld>, RefRO<RotationSpeed>>().WithEntityAccess())
            {
                // Do stuff;
            }
            #endregion
        }
    }

    
    public partial struct MyCoolSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            #region dynamic-buffer
            var bufferHandle = state.GetBufferTypeHandle<MyBufferElement>(isReadOnly: true);
            var myBufferElementQuery = SystemAPI.QueryBuilder().WithAll<MyBufferElement>().Build();
            var chunks = myBufferElementQuery.ToArchetypeChunkArray(Allocator.Temp);

            foreach (var chunk in chunks)
            {
                var numEntities = chunk.Count;
                var bufferAccessor = chunk.GetBufferAccessor(ref bufferHandle);

                for (int j = 0; j < numEntities; j++)
                {
                    var dynamicBuffer = bufferAccessor[j];
                    // Read from dynamicBuffer and perform various operations
                }
            }
            #endregion

        }

    }


}