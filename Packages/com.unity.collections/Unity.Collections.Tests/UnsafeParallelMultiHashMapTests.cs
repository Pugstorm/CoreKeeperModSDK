using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Collections.Tests;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

internal class UnsafeParallelMultiHashMapTests : CollectionsTestCommonBase
{
    [BurstCompile(CompileSynchronously = true)]
    public struct UnsafeParallelMultiHashMapAddJob : IJobParallelFor
    {
        public UnsafeParallelMultiHashMap<int, int>.ParallelWriter Writer;

        public void Execute(int index)
        {
            Writer.Add(123, index);
        }
    }

    [Test]
    public void UnsafeParallelMultiHashMap_AddJob()
    {
        var container = new UnsafeParallelMultiHashMap<int, int>(32, CommonRwdAllocator.Handle);

        var job = new UnsafeParallelMultiHashMapAddJob()
        {
            Writer = container.AsParallelWriter(),
        };

        job.Schedule(3, 1).Complete();

        Assert.True(container.ContainsKey(123));
        Assert.AreEqual(container.CountValuesForKey(123), 3);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashMap_RemoveOnEmptyMap_DoesNotThrow()
    {
        var container = new UnsafeParallelHashMap<int, int>(0, Allocator.Temp);
        Assert.DoesNotThrow(() => container.Remove(0));
        Assert.DoesNotThrow(() => container.Remove(-425196));
        container.Dispose();
    }

    [Test]
    public void UnsafeParallelMultiHashMap_RemoveOnEmptyMap_DoesNotThrow()
    {
        var container = new UnsafeParallelMultiHashMap<int, int>(0, Allocator.Temp);

        Assert.DoesNotThrow(() => container.Remove(0));
        Assert.DoesNotThrow(() => container.Remove(-425196));
        Assert.DoesNotThrow(() => container.Remove(0, 0));
        Assert.DoesNotThrow(() => container.Remove(-425196, 0));

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelMultiHashMap_ForEach_FixedStringInHashMap()
    {
        using (var stringList = new NativeList<FixedString32Bytes>(10, Allocator.Persistent) { "Hello", ",", "World", "!" })
        {
            var container = new UnsafeParallelMultiHashMap<FixedString128Bytes, float>(50, Allocator.Temp);
            var seen = new NativeArray<int>(stringList.Length, Allocator.Temp);
            foreach (var str in stringList)
            {
                container.Add(str, 0);
            }

            foreach (var pair in container)
            {
                int index = stringList.IndexOf(pair.Key);
                Assert.AreEqual(stringList[index], pair.Key.ToString());
                seen[index] = seen[index] + 1;
            }

            for (int i = 0; i < stringList.Length; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect value count {stringList[i]}");
            }
        }
    }

    [Test]
    public void UnsafeParallelMultiHashMap_ForEach([Values(10, 1000)]int n)
    {
        var seenKeys = new NativeArray<int>(n, Allocator.Temp);
        var seenValues = new NativeArray<int>(n * 2, Allocator.Temp);
        using (var container = new UnsafeParallelMultiHashMap<int, int>(1, Allocator.Temp))
        {
            for (int i = 0; i < n; ++i)
            {
                container.Add(i, i);
                container.Add(i, i + n);
            }

            var count = 0;
            foreach (var kv in container)
            {
                if (kv.Value < n)
                {
                    Assert.AreEqual(kv.Key, kv.Value);
                }
                else
                {
                    Assert.AreEqual(kv.Key + n, kv.Value);
                }

                seenKeys[kv.Key] = seenKeys[kv.Key] + 1;
                seenValues[kv.Value] = seenValues[kv.Value] + 1;

                ++count;
            }

            Assert.AreEqual(container.Count(), count);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(2, seenKeys[i], $"Incorrect key count {i}");
                Assert.AreEqual(1, seenValues[i], $"Incorrect value count {i}");
                Assert.AreEqual(1, seenValues[i + n], $"Incorrect value count {i + n}");
            }
        }
    }

    [Test]
    public void UnsafeParallelMultiHashMap_GetKeys()
    {
        var container = new UnsafeParallelMultiHashMap<int, int>(1, Allocator.Temp);
        for (int i = 0; i < 30; ++i)
        {
            container.Add(i, 2 * i);
            container.Add(i, 3 * i);
        }
        var keys = container.GetKeyArray(Allocator.Temp);
        var (unique, uniqueLength) = container.GetUniqueKeyArray(Allocator.Temp);
        Assert.AreEqual(30, uniqueLength);

        Assert.AreEqual(60, keys.Length);
        keys.Sort();
        for (int i = 0; i < 30; ++i)
        {
            Assert.AreEqual(i, keys[i * 2 + 0]);
            Assert.AreEqual(i, keys[i * 2 + 1]);
            Assert.AreEqual(i, unique[i]);
        }
    }

    [Test]
    public void UnsafeParallelMultiHashMap_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new UnsafeParallelMultiHashMap<int, int>(1, allocator.Handle))
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
                using (var container = new UnsafeParallelMultiHashMap<int, int>(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void UnsafeParallelMultiHashMap_BurstedCustomAllocatorTest()
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
}
