using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    [Category("Performance")]
    public class ChunkStorePerformanceTests
    {
        unsafe struct ChunkStoreTestHarness : IDisposable
        {
            public NativeList<ChunkIndex> Chunks;
            Mathematics.Random Seed;
            readonly int ChunkCount;
            public ChunkStoreTestHarness(int chunkCount, uint seed)
            {
                EntityComponentStore.PerChunkArray.Initialize();
                Chunks = new NativeList<ChunkIndex>(chunkCount, Allocator.Persistent);
                Seed = new Mathematics.Random(seed);
                ChunkCount = chunkCount;
                for(var i = 0; i < ChunkCount; ++i)
                    AllocateOne();
            }
            public void AllocateOne()
            {
                var error = EntityComponentStore.s_chunkStore.Data.AllocateContiguousChunks(out ChunkIndex value, 1, out int _);
                Assert.AreEqual(0, error);
                Chunks.Add(value);
            }
            public void FreeAtIndex(int index)
            {
                var value = Chunks[index];
                var error = EntityComponentStore.s_chunkStore.Data.FreeContiguousChunks(value, 1);
                Assert.AreEqual(0, error);
            }
            public void FreeOne()
            {
                int index = Seed.NextInt(0, Chunks.Length-1);
                FreeAtIndex(index);
                Chunks.RemoveAtSwapBack(index);
            }
            public void Exercise()
            {
                for(var i = 0; i < ChunkCount/2; ++i)
                    FreeOne();
                for(var i = 0; i < ChunkCount/2; ++i)
                    AllocateOne();
                for(var i = 0; i < ChunkCount/2; ++i)
                    FreeOne();
                for(var i = 0; i < ChunkCount/2; ++i)
                    AllocateOne();
            }
            public void Dispose()
            {
                for(var i = 0; i < Chunks.Length; ++i)
                    FreeAtIndex(i);
                Chunks.Dispose();
                this = default;
            }
        }

        [Test, Performance]
        [Category("Performance")] // bug: this redundant category here required because our current test runner ignores Category on a fixture for generated test methods
        public void Exercise()
        {
            ChunkStoreTestHarness harness = default;
            Measure.Method(() =>
            {
                harness.Exercise();
            })
            .SetUp(() =>
            {
                harness = new ChunkStoreTestHarness(100000, 0xDEADBEEF);
            })
            .CleanUp(() =>
            {
                harness.Dispose();
            })
            .MeasurementCount(10)
            .IterationsPerMeasurement(1)
            .Run();
        }

    }
}
