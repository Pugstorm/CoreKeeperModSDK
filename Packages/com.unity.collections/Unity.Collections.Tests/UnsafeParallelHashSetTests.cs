using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.Tests;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
internal class UnsafeParallelHashSetTests : CollectionsTestCommonBase
{
    static void ExpectedCount<T>(ref UnsafeParallelHashSet<T> container, int expected)
        where T : unmanaged, IEquatable<T>
    {
        Assert.AreEqual(expected == 0, container.IsEmpty);
        Assert.AreEqual(expected, container.Count());
    }

    [Test]
    public void UnsafeParallelHashSet_IsEmpty()
    {
        var container = new UnsafeParallelHashSet<int>(0, Allocator.Persistent);
        Assert.IsTrue(container.IsEmpty);

        Assert.IsTrue(container.Add(0));
        Assert.IsFalse(container.IsEmpty);
        Assert.AreEqual(1, container.Capacity);
        ExpectedCount(ref container, 1);

        container.Remove(0);
        Assert.IsTrue(container.IsEmpty);

        Assert.IsTrue(container.Add(0));
        container.Clear();
        Assert.IsTrue(container.IsEmpty);

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_Capacity()
    {
        var container = new UnsafeParallelHashSet<int>(0, Allocator.Persistent);
        Assert.IsTrue(container.IsEmpty);
        Assert.AreEqual(0, container.Capacity);

        container.Capacity = 10;
        Assert.AreEqual(10, container.Capacity);

        container.Dispose();
    }

#if !UNITY_DOTSRUNTIME    // DOTS-Runtime has an assertion in the C++ layer, that can't be caught in C#
    [Test]
    [TestRequiresCollectionChecks]
    public void UnsafeParallelHashSet_Full_Throws()
    {
        var container = new UnsafeParallelHashSet<int>(16, Allocator.Temp);
        ExpectedCount(ref container, 0);

        for (int i = 0, capacity = container.Capacity; i < capacity; ++i)
        {
            Assert.DoesNotThrow(() => { container.Add(i); });
        }
        ExpectedCount(ref container, container.Capacity);

        // Make sure overallocating throws and exception if using the Concurrent version - normal hash map would grow
        var writer = container.AsParallelWriter();
        Assert.Throws<System.InvalidOperationException>(() => { writer.Add(100); });
        ExpectedCount(ref container, container.Capacity);

        container.Clear();
        ExpectedCount(ref container, 0);

        container.Dispose();
    }
#endif

    [Test]
    public void UnsafeParallelHashSet_RemoveOnEmptyMap_DoesNotThrow()
    {
        var container = new UnsafeParallelHashSet<int>(0, Allocator.Temp);
        Assert.DoesNotThrow(() => container.Remove(0));
        Assert.DoesNotThrow(() => container.Remove(-425196));
        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_Collisions()
    {
        var container = new UnsafeParallelHashSet<int>(16, Allocator.Temp);

        Assert.IsFalse(container.Contains(0), "Contains on empty hash map did not fail");
        ExpectedCount(ref container, 0);

        // Make sure inserting values work
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.Add(i), "Failed to add value");
        }
        ExpectedCount(ref container, 8);

        // The bucket size is capacity * 2, adding that number should result in hash collisions
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.Add(i + 32), "Failed to add value with potential hash collision");
        }

        // Make sure reading the inserted values work
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.Contains(i), "Failed get value from hash set");
        }

        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.Contains(i + 32), "Failed get value from hash set");
        }

        container.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_SameElement()
    {
        using (var container = new UnsafeParallelHashSet<int>(0, Allocator.Persistent))
        {
            Assert.IsTrue(container.Add(0));
            Assert.IsFalse(container.Add(0));
        }
    }

    [Test]
    public void UnsafeParallelHashSet_ForEach_FixedStringInHashMap()
    {
        using (var stringList = new NativeList<FixedString32Bytes>(10, Allocator.Persistent) { "Hello", ",", "World", "!" })
        {
            var container = new NativeParallelHashSet<FixedString128Bytes>(50, Allocator.Temp);
            var seen = new NativeArray<int>(stringList.Length, Allocator.Temp);
            foreach (var str in stringList)
            {
                container.Add(str);
            }

            foreach (var value in container)
            {
                int index = stringList.IndexOf(value);
                Assert.AreEqual(stringList[index], value.ToString());
                seen[index] = seen[index] + 1;
            }

            for (int i = 0; i < stringList.Length; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect value count {stringList[i]}");
            }
        }
    }

    [Test]
    public void UnsafeParallelHashSet_ForEach([Values(10, 1000)]int n)
    {
        var seen = new NativeArray<int>(n, Allocator.Temp);
        using (var container = new UnsafeParallelHashSet<int>(32, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < n; i++)
            {
                container.Add(i);
            }

            var count = 0;
            foreach (var item in container)
            {
                Assert.True(container.Contains(item));
                seen[item] = seen[item] + 1;
                ++count;
            }

            Assert.AreEqual(container.Count(), count);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect item count {i}");
            }
        }
    }

    [Test]
    public void UnsafeParallelHashSet_EIU_ExceptWith_Empty()
    {
        var setA = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var setB = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        setA.ExceptWith(setB);

        ExpectedCount(ref setA, 0);

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_EIU_ExceptWith_AxB()
    {
        var setA = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var setB = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        setA.ExceptWith(setB);

        ExpectedCount(ref setA, 3);
        Assert.True(setA.Contains(0));
        Assert.True(setA.Contains(1));
        Assert.True(setA.Contains(2));

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_EIU_ExceptWith_BxA()
    {
        var setA = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var setB = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        setB.ExceptWith(setA);

        ExpectedCount(ref setB, 3);
        Assert.True(setB.Contains(6));
        Assert.True(setB.Contains(7));
        Assert.True(setB.Contains(8));

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_EIU_IntersectWith_Empty()
    {
        var setA = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var setB = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        setA.IntersectWith(setB);

        ExpectedCount(ref setA, 0);

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_EIU_IntersectWith()
    {
        var setA = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var setB = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        setA.IntersectWith(setB);

        ExpectedCount(ref setA, 3);
        Assert.True(setA.Contains(3));
        Assert.True(setA.Contains(4));
        Assert.True(setA.Contains(5));

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_EIU_UnionWith_Empty()
    {
        var setA = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var setB = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { };
        setA.UnionWith(setB);

        ExpectedCount(ref setA, 0);

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_EIU_UnionWith()
    {
        var setA = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var setB = new UnsafeParallelHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        setA.UnionWith(setB);

        ExpectedCount(ref setA, 9);
        Assert.True(setA.Contains(0));
        Assert.True(setA.Contains(1));
        Assert.True(setA.Contains(2));
        Assert.True(setA.Contains(3));
        Assert.True(setA.Contains(4));
        Assert.True(setA.Contains(5));
        Assert.True(setA.Contains(6));
        Assert.True(setA.Contains(7));
        Assert.True(setA.Contains(8));

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void UnsafeParallelHashSet_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new UnsafeParallelHashSet<int>(1, allocator.Handle))
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
                using (var container = new UnsafeParallelHashSet<int>(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void UnsafeParallelHashSet_BurstedCustomAllocatorTest()
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

    struct UnsafeParallelHashSet_ForEach_Job : IJob
    {
        [ReadOnly]
        public UnsafeParallelHashSet<int>.ReadOnly Input;

        [ReadOnly]
        public int Num;

        public void Execute()
        {
            var seen = new NativeArray<int>(Num, Allocator.Temp);

            var count = 0;
            foreach (var item in Input)
            {
                Assert.True(Input.Contains(item));
                seen[item] = seen[item] + 1;
                ++count;
            }

            Assert.AreEqual(Input.Count(), count);
            for (int i = 0; i < Num; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect item count {i}");
            }

            seen.Dispose();
        }
    }

    [Test]
    public void UnsafeParallelHashSet_ForEach_From_Job([Values(10, 1000)] int n)
    {
        using (var container = new UnsafeParallelHashSet<int>(32, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < n; i++)
            {
                container.Add(i);
            }

            new UnsafeParallelHashSet_ForEach_Job
            {
                Input = container.AsReadOnly(),
                Num = n,

            }.Run();
        }
    }
}
