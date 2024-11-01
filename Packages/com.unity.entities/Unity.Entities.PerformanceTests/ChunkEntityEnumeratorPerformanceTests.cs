using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.PerformanceTesting;
using Assert = UnityEngine.Assertions.Assert;


namespace Unity.Entities.PerformanceTests
{
    [Category("Performance")]
    internal class ChunkEntityEnumeratorPerformanceTests : EntitiesTestsFixture
    {
        [BurstCompile(CompileSynchronously = true)]
        public struct EnabledBitsJob_Enumerator : IJobChunk
        {
            public ComponentTypeHandle<EcsTestDataEnableable> TestDataHandleRW;

            public unsafe void Execute(in ArchetypeChunk chunk, int chunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var components = (EcsTestDataEnableable*)chunk.GetComponentDataPtrRW(ref TestDataHandleRW);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask,chunkEnabledMask,chunk.Count);
                while(enumerator.NextEntityIndex(out var i))
                {
                    components[i].value++;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct EnabledBitsJob_ForLoop : IJobChunk
        {
            public ComponentTypeHandle<EcsTestDataEnableable> TestDataHandleRW;

            public unsafe void Execute(in ArchetypeChunk chunk, int chunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                var components = (EcsTestDataEnableable*)chunk.GetComponentDataPtrRW(ref TestDataHandleRW);
                int chunkEntityCount = chunk.Count;
                for(int i=0; i<chunkEntityCount; ++i)
                {
                    components[i].value++;
                }
            }
        }

        [Test, Performance]
        [Category("Performance")]
        public void ChunkEntityEnumerator_Performance()
        {
            EntityArchetype archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            int entityCount = 1000 * archetype.ChunkCapacity;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Temp);
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            var ecsDataTypeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);

            var enumeratorJob = new EnabledBitsJob_Enumerator
            {
                TestDataHandleRW = ecsDataTypeHandle
            };
            var forLoopJob = new EnabledBitsJob_ForLoop
            {
                TestDataHandleRW = ecsDataTypeHandle
            };

            Measure
                .Method(() =>
                {
                    forLoopJob.Run(query);
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup("ForLoop", SampleUnit.Microsecond))
                .Run();
            Measure
                .Method(() =>
                {
                    enumeratorJob.Run(query);
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup("Enumerator_AllEnabled", SampleUnit.Microsecond))
                .Run();

            for (int i = 1; i < entities.Length; i += 2)
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
            Measure
                .Method(() =>
                {
                    enumeratorJob.Run(query);
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup("Enumerator_Alternating", SampleUnit.Microsecond))
                .Run();

            for (int i = 1; i < entities.Length; ++i)
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], true);
            for (int i = 1; i < entities.Length; i += 10)
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
            Measure
                .Method(() =>
                {
                    enumeratorJob.Run(query);
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup("Enumerator_Ranges", SampleUnit.Microsecond))
                .Run();
        }
    }
}

