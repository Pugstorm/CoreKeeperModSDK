using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Entities
{
    static unsafe partial class EntityDiffer
    {
        [BurstCompile]
        struct ClearMissingReferencesJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeList<ArchetypeChunk> Chunks;

            public void Execute(int index)
            {
                var chunk = Chunks[index].m_Chunk;

                ChunkDataUtility.ClearMissingReferences(Chunks[index].Archetype.Archetype, chunk);
            }
        }

        static void ClearMissingReferences(EntityManager entityManager, NativeList<ArchetypeChunk> chunks, out JobHandle jobHandle, JobHandle dependsOn)
        {
            jobHandle = new ClearMissingReferencesJob
            {
                Chunks = chunks,
            }.Schedule(chunks, 64, dependsOn);
        }
    }
}
