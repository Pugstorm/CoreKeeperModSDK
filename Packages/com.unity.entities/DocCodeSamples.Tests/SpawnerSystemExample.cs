namespace Doc.CodeSamples.Tests
{
    #region example
    using Unity.Entities;
    using Unity.Transforms;
    using Unity.Burst;

    [BurstCompile]
    public partial struct SpawnerSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Queries for all Spawner components. Uses RefRW because this system wants
            // to read from and write to the component. If the system only needed read-only
            // access, it would use RefRO instead.
            foreach (RefRW<Spawner> spawner in SystemAPI.Query<RefRW<Spawner>>())
            {
                ProcessSpawner(ref state, spawner);
            }
        }

        private void ProcessSpawner(ref SystemState state, RefRW<Spawner> spawner)
        {
            // If the next spawn time has passed.
            if (spawner.ValueRO.NextSpawnTime < SystemAPI.Time.ElapsedTime)
            {
                // Spawns a new entity and positions it at the spawner.
                Entity newEntity = state.EntityManager.Instantiate(spawner.ValueRO.Prefab);
                // LocalPosition.FromPosition returns a Transform initialized with the given position.
                state.EntityManager.SetComponentData(newEntity, LocalTransform.FromPosition(spawner.ValueRO.SpawnPosition));

                // Resets the next spawn time.
                spawner.ValueRW.NextSpawnTime = (float)SystemAPI.Time.ElapsedTime + spawner.ValueRO.SpawnRate;
            }
        }
    }
    #endregion
}
