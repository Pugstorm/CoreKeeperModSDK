using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe.NotBurstCompatible;
using Unity.Collections.Tests;
using System;
using Unity.Burst;

internal class UnsafeParallelHashMapTests : CollectionsTestCommonBase
{
    // Burst error BC1071: Unsupported assert type
    // [BurstCompile(CompileSynchronously = true)]
    public struct UnsafeParallelHashMapAddJob : IJob
    {
        public UnsafeParallelHashMap<int, int>.ParallelWriter Writer;

        public void Execute()
        {
            Assert.True(Writer.TryAdd(123, 1));
        }
    }

    [Test]
    public void UnsafeParallelHashMap_AddJob()
    {
        var container = new UnsafeParallelHashMap<int, int>(32, CommonRwdAllocator.Handle);

        var job = new UnsafeParallelHashMapAddJob()
        {
            Writer = container.AsParallelWriter(),
        };

        job.Schedule().Complete();

        Assert.True(container.ContainsKey(123));

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashMap_ForEach([Values(10, 1000)]int n)
    {
        var seen = new NativeArray<int>(n, Allocator.Temp);
        using (var container = new UnsafeParallelHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < n; i++)
            {
                container.Add(i, i * 37);
            }

            var count = 0;
            foreach (var kv in container)
            {
                int value;
                Assert.True(container.TryGetValue(kv.Key, out value));
                Assert.AreEqual(value, kv.Value);
                Assert.AreEqual(kv.Key * 37, kv.Value);

                seen[kv.Key] = seen[kv.Key] + 1;
                ++count;
            }

            Assert.AreEqual(container.Count(), count);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect key count {i}");
            }
        }
    }

    [Test]
    public void UnsafeParallelHashSet_ToArray()
    {
        using (var set = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 })
        {
            var array = set.ToArray();
            Array.Sort(array);
            for (int i = 0, num = set.Count(); i < num; i++)
            {
                Assert.AreEqual(array[i], i);
            }
        }
    }

    [Test]
    public void UnsafeParallelHashMap_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new UnsafeParallelHashMap<int, int>(1, allocator.Handle))
        {
        }

        Assert.IsTrue(allocator.WasUsed);
        allocator.Dispose();
        allocatorHelper.Dispose();
        AllocatorManager.Shutdown();
    }

    [BurstCompile]
    struct BurstedCustomAllocatorJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe CustomAllocatorTests.CountingAllocator* Allocator;

        public void Execute()
        {
            unsafe
            {
                using (var container = new UnsafeParallelHashMap<int, int>(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void UnsafeParallelHashMap_BurstedCustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        var allocatorPtr = (CustomAllocatorTests.CountingAllocator*)UnsafeUtility.AddressOf<CustomAllocatorTests.CountingAllocator>(ref allocator);
        unsafe
        {
            var handle = new BurstedCustomAllocatorJob {Allocator = allocatorPtr}.Schedule();
            handle.Complete();
        }

        Assert.IsTrue(allocator.WasUsed);
        allocator.Dispose();
        allocatorHelper.Dispose();
        AllocatorManager.Shutdown();
    }

    [Test]
    public void UnsafeParallelHashMap_IndexerAdd_ResizesContainer()
    {
        var container = new UnsafeParallelHashMap<int, int>(8, Allocator.Persistent);
        for (int i = 0; i < 1024; i++)
        {
            container[i] = i;
        }
        Assert.AreEqual(1024, container.Count());
        container.Dispose();
    }

    struct UnsafeParallelHashMap_ForEach_Job : IJob
    {
        public UnsafeParallelHashMap<int, int>.ReadOnly Input;

        [ReadOnly]
        public int Num;

        public void Execute()
        {
            var seen = new NativeArray<int>(Num, Allocator.Temp);

            var count = 0;
            foreach (var kv in Input)
            {
                int value;
                Assert.True(Input.TryGetValue(kv.Key, out value));
                Assert.AreEqual(value, kv.Value);
                Assert.AreEqual(kv.Key * 37, kv.Value);

                seen[kv.Key] = seen[kv.Key] + 1;
                ++count;
            }

            Assert.AreEqual(Input.Count(), count);
            for (int i = 0; i < Num; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect key count {i}");
            }

            seen.Dispose();
        }
    }

    [Test]
    public void UnsafeParallelHashMap_ForEach_From_Job([Values(10, 1000)] int n)
    {
        var seen = new NativeArray<int>(n, Allocator.Temp);
        using (var container = new UnsafeParallelHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < n; i++)
            {
                container.Add(i, i * 37);
            }

            new UnsafeParallelHashMap_ForEach_Job
            {
                Input = container.AsReadOnly(),
                Num = n,

            }.Run();
        }
    }
}
