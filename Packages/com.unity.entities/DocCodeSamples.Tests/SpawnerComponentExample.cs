namespace Doc.CodeSamples.Tests
{
    #region example
    using Unity.Entities;
    using Unity.Mathematics;

    public struct Spawner : IComponentData
    {
        public Entity Prefab;
        public float3 SpawnPosition;
        public float NextSpawnTime;
        public float SpawnRate;
    }
    #endregion
}
