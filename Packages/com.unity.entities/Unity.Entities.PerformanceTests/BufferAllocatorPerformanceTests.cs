using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Tests;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    [Category("Performance")]
    public class BufferAllocatorPerformanceTests : BufferAllocatorTestsBase
    {
        const int kBufferBytes = 16 * 1024;

        /// <summary>
        /// Run set up code after allocator is created but before running tests.
        /// </summary>
        /// <param name="allocator">Allocator to set up.</param>
        internal delegate void SetUpDelegate(IBufferAllocator allocator);

        internal static void RunPerformanceTest(int bufferBytes, int bufferCount, int warmupCount, int measureCount, SetUpDelegate setup, RunTestDelegate f)
        {
            // Explicitly test the heap BufferAllocator, even if virtual memory is supported by the platform.
            {
                BufferAllocatorHeap allocator = default;

                Measure.Method(() => { TestBufferAllocatorHeap(allocator, f); })
                    .SampleGroup("Heap")
                    .WarmupCount(warmupCount)
                    .MeasurementCount(measureCount)
                    .SetUp(() =>
                    {
                        allocator = new BufferAllocatorHeap(bufferCount * bufferBytes, bufferBytes, RwdAllocator.ToAllocator);
                        setup(allocator);
                    })
                    .CleanUp(() => { allocator.Dispose(); })
                    .Run();
            }

            // Test the generic BufferAllocator which selects either virtual memory or heap backed versions, depending on the platform.
            {
                BufferAllocator allocator = default;

                Measure.Method(() => { TestBufferAllocator(allocator, f); })
                    .SampleGroup("BufferAllocator")
                    .WarmupCount(warmupCount)
                    .MeasurementCount(measureCount)
                    .SetUp(() =>
                    {
                        allocator = new BufferAllocator(bufferCount * bufferBytes, bufferBytes, RwdAllocator.ToAllocator);
                        setup(allocator);
                    })
                    .CleanUp(() => { allocator.Dispose(); })
                    .Run();
            }
        }

        [Test, Performance]
        public static void AllocateThenFree()
        {
            const int kBufferCount = 5;
            const int kAllocateCount = 3000;
            RunPerformanceTest(kBufferBytes, kBufferCount, 1, 10, (allocator) => { }, (allocator) =>
            {
                for (int i = 0; i < kAllocateCount; ++i)
                {
                    var bufferIndex = allocator.Allocate();
                    allocator.Free(bufferIndex);
                }
            });
        }

        [Test, Performance]
        public static void AllocateAll()
        {
            const int kBufferCount = 4096;
            RunPerformanceTest(kBufferBytes, kBufferCount, 1, 10, (allocator) => { }, (allocator) =>
            {
                for (int i = 0; i < kBufferCount; ++i)
                {
                    var bufferIndex = allocator.Allocate();
                }
            });
        }

        [Test, Performance]
        public static void FreeAll()
        {
            const int kBufferCount = 4096;
            RunPerformanceTest(kBufferBytes, kBufferCount, 1, 10, (allocator) =>
            {
                for (int i = 0; i < kBufferCount; ++i)
                {
                    allocator.Allocate();
                }
            }, (allocator) =>
            {
                for (int i = 0; i < kBufferCount; ++i)
                {
                    allocator.Free(i);
                }
            });
        }

        [Test, Performance]
        public static void FreeRandomly()
        {
            const int kBufferCount = 4096;
            var indices = CollectionHelper.CreateNativeArray<int>(kBufferCount, RwdAllocator.ToAllocator, NativeArrayOptions.UninitializedMemory);

            RunPerformanceTest(kBufferBytes, kBufferCount, 1, 10, (allocator) =>
            {
                for (int i = 0; i < kBufferCount; ++i)
                {
                    indices[i] = allocator.Allocate();
                }

                const uint kArbitrarySeed = 178391u;
                var rng = new Random(kArbitrarySeed);
                FisherYatesShuffle(indices, ref rng);
            }, (allocator) =>
            {
                for (int i = 0; i < kBufferCount; ++i)
                {
                    allocator.Free(indices[i]);
                }
            });

            indices.Dispose();
        }
    }
}
