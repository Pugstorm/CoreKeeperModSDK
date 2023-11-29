using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe.NotBurstCompatible;
using Unity.Collections.Tests;
using System;
using Unity.Burst;
using System.Diagnostics;

internal class UnsafeHashMapTests : CollectionsTestCommonBase
{
    [Test]
    public void UnsafeHashMap_ForEach([Values(10, 1000)] int n)
    {
        var seen = new NativeArray<int>(n, Allocator.Temp);
        using (var container = new UnsafeHashMap<int, int>(32, CommonRwdAllocator.Handle))
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

            Assert.AreEqual(container.Count, count);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect key count {i}");
            }
        }
    }

    [Test]
    public void UnsafeHashMap_ForEach_FixedStringKey()
    {
        using (var stringList = new NativeList<FixedString32Bytes>(10, Allocator.Persistent) { "Hello", ",", "World", "!" })
        {
            var seen = new NativeArray<int>(stringList.Length, Allocator.Temp);
            var container = new UnsafeHashMap<FixedString128Bytes, float>(50, Allocator.Temp);
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

    struct UnsafeHashMap_ForEachIterator : IJob
    {
        [ReadOnly]
        public UnsafeHashMap<int, int>.Enumerator Iter;

        public void Execute()
        {
            while (Iter.MoveNext())
            {
            }
        }
    }

    [Test]
    public void UnsafeHashMap_ForEach_Throws_Job_Iterator()
    {
        using (var container = new UnsafeHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            var jobHandle = new UnsafeHashMap_ForEachIterator
            {
                Iter = container.GetEnumerator()

            }.Schedule();

            jobHandle.Complete();
        }
    }

    struct UnsafeHashMap_ForEach_Job : IJob
    {
        public UnsafeHashMap<int, int>.ReadOnly Input;

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

            Assert.AreEqual(Input.Count, count);
            for (int i = 0; i < Num; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect key count {i}");
            }

            seen.Dispose();
        }
    }

    [Test]
    public void UnsafeHashMap_ForEach_From_Job([Values(10, 1000)] int n)
    {
        var seen = new NativeArray<int>(n, Allocator.Temp);
        using (var container = new UnsafeHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < n; i++)
            {
                container.Add(i, i * 37);
            }

            new UnsafeHashMap_ForEach_Job
            {
                Input = container.AsReadOnly(),
                Num = n,

            }.Run();
        }
    }

    [Test]
    public void UnsafeHashMap_EnumeratorDoesNotReturnRemovedElementsTest()
    {
        UnsafeHashMap<int, int> container = new UnsafeHashMap<int, int>(5, Allocator.Temp);
        for (int i = 0; i < 5; i++)
        {
            container.Add(i, i);
        }

        int elementToRemove = 2;
        Assert.IsTrue(container.Remove(elementToRemove));
        Assert.IsFalse(container.Remove(elementToRemove));

        using (var enumerator = container.GetEnumerator())
        {
            while (enumerator.MoveNext())
            {
                Assert.AreNotEqual(elementToRemove, enumerator.Current.Key);
            }
        }

        container.Dispose();
    }

    [Test]
    public void UnsafeHashMap_EnumeratorInfiniteIterationTest()
    {
        UnsafeHashMap<int, int> container = new UnsafeHashMap<int, int>(5, Allocator.Temp);
        for (int i = 0; i < 5; i++)
        {
            container.Add(i, i);
        }

        for (int i = 0; i < 2; i++)
        {
            Assert.IsTrue(container.Remove(i));
        }

        var expected = container.Count;
        int count = 0;
        using (var enumerator = container.GetEnumerator())
        {
            while (enumerator.MoveNext())
            {
                if (count++ > expected)
                {
                    break;
                }
            }
        }

        Assert.AreEqual(expected, count);
        container.Dispose();
    }

    [Test]
    public void UnsafeHashMap_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new UnsafeHashMap<int, int>(1, allocator.Handle))
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
                using (var container = new UnsafeHashMap<int, int>(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void UnsafeHashMap_BurstedCustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        var allocatorPtr = (CustomAllocatorTests.CountingAllocator*)UnsafeUtility.AddressOf<CustomAllocatorTests.CountingAllocator>(ref allocator);
        unsafe
        {
            var handle = new BurstedCustomAllocatorJob { Allocator = allocatorPtr }.Schedule();
            handle.Complete();
        }

        Assert.IsTrue(allocator.WasUsed);
        allocator.Dispose();
        allocatorHelper.Dispose();
        AllocatorManager.Shutdown();
    }

    static void ExpectedCount<TKey, TValue>(ref UnsafeHashMap<TKey, TValue> container, int expected)
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        Assert.AreEqual(expected == 0, container.IsEmpty);
        Assert.AreEqual(expected, container.Count);
    }

    [Test]
    public void UnsafeHashMap_TryAdd_TryGetValue_Clear()
    {
        var container = new UnsafeHashMap<int, int>(16, Allocator.Temp);
        ExpectedCount(ref container, 0);

        int iSquared;
        // Make sure GetValue fails if container is empty
        Assert.IsFalse(container.TryGetValue(0, out iSquared), "TryGetValue on empty container did not fail");

        // Make sure inserting values work
        for (int i = 0; i < 16; ++i)
            Assert.IsTrue(container.TryAdd(i, i * i), "Failed to add value");
        ExpectedCount(ref container, 16);

        // Make sure inserting duplicate keys fails
        for (int i = 0; i < 16; ++i)
            Assert.IsFalse(container.TryAdd(i, i), "Adding duplicate keys did not fail");
        ExpectedCount(ref container, 16);

        // Make sure reading the inserted values work
        for (int i = 0; i < 16; ++i)
        {
            Assert.IsTrue(container.TryGetValue(i, out iSquared), "Failed get value from hash table");
            Assert.AreEqual(iSquared, i * i, "Got the wrong value from the hash table");
        }

        // Make sure clearing removes all keys
        container.Clear();
        ExpectedCount(ref container, 0);

        for (int i = 0; i < 16; ++i)
            Assert.IsFalse(container.TryGetValue(i, out iSquared), "Got value from hash table after clearing");

        container.Dispose();
    }

    [Test]
    public void UnsafeHashMap_Key_Collisions()
    {
        var container = new UnsafeHashMap<int, int>(16, Allocator.Temp);
        int iSquared;

        // Make sure GetValue fails if container is empty
        Assert.IsFalse(container.TryGetValue(0, out iSquared), "TryGetValue on empty container did not fail");

        // Make sure inserting values work
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.TryAdd(i, i * i), "Failed to add value");
        }

        // The bucket size is capacity * 2, adding that number should result in hash collisions
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.TryAdd(i + 32, i), "Failed to add value with potential hash collision");
        }

        // Make sure reading the inserted values work
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.TryGetValue(i, out iSquared), "Failed get value from hash table");
            Assert.AreEqual(iSquared, i * i, "Got the wrong value from the hash table");
        }

        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.TryGetValue(i + 32, out iSquared), "Failed get value from hash table");
            Assert.AreEqual(iSquared, i, "Got the wrong value from the hash table");
        }

        container.Dispose();
    }

    [Test]
    public void UnsafeHashMap_SupportsAutomaticCapacityChange()
    {
        var container = new UnsafeHashMap<int, int>(1, Allocator.Temp);
        int iSquared;

        // Make sure inserting values work and grows the capacity
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.TryAdd(i, i * i), "Failed to add value");
        }
        Assert.IsTrue(container.Capacity >= 8, "Capacity was not updated correctly");

        // Make sure reading the inserted values work
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.TryGetValue(i, out iSquared), "Failed get value from hash table");
            Assert.AreEqual(iSquared, i * i, "Got the wrong value from the hash table");
        }

        container.Dispose();
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public void UnsafeHashMap_SameKey()
    {
        using (var container = new UnsafeHashMap<int, int>(0, Allocator.Persistent))
        {
            Assert.DoesNotThrow(() => container.Add(0, 0));
            Assert.Throws<ArgumentException>(() => container.Add(0, 0));
        }

        using (var container = new UnsafeHashMap<int, int>(0, Allocator.Persistent))
        {
            Assert.IsTrue(container.TryAdd(0, 0));
            Assert.IsFalse(container.TryAdd(0, 0));
        }
    }

    [Test]
    public void UnsafeHashMap_IsEmpty()
    {
        var container = new UnsafeHashMap<int, int>(0, Allocator.Persistent);
        Assert.IsTrue(container.IsEmpty);

        container.TryAdd(0, 0);
        Assert.IsFalse(container.IsEmpty);
        ExpectedCount(ref container, 1);

        Assert.IsTrue(container.Remove(0));
        Assert.IsFalse(container.Remove(0));
        Assert.IsTrue(container.IsEmpty);

        container.TryAdd(0, 0);
        container.Clear();
        Assert.IsTrue(container.IsEmpty);

        container.Dispose();
    }

    [Test]
    public void UnsafeHashMap_EmptyCapacity()
    {
        var container = new UnsafeHashMap<int, int>(0, Allocator.Persistent);
        container.TryAdd(0, 0);
        ExpectedCount(ref container, 1);
        container.Dispose();
    }

    [Test]
    public void UnsafeHashMap_Remove()
    {
        var container = new UnsafeHashMap<int, int>(8, Allocator.Temp);
        int iSquared;

        for (int rm = 0; rm < 8; ++rm)
        {
            Assert.IsFalse(container.Remove(rm));
        }

        // Make sure inserting values work
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.TryAdd(i, i * i), "Failed to add value");
        }
        Assert.AreEqual(8, container.Count);

        // Make sure reading the inserted values work
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.TryGetValue(i, out iSquared), "Failed get value from hash table");
            Assert.AreEqual(iSquared, i * i, "Got the wrong value from the hash table");
        }

        for (int rm = 0; rm < 8; ++rm)
        {
            Assert.IsTrue(container.Remove(rm));
            Assert.IsFalse(container.TryGetValue(rm, out iSquared), "Failed to remove value from hash table");
            for (int i = rm + 1; i < 8; ++i)
            {
                Assert.IsTrue(container.TryGetValue(i, out iSquared), "Failed get value from hash table");
                Assert.AreEqual(iSquared, i * i, "Got the wrong value from the hash table");
            }
        }
        Assert.AreEqual(0, container.Count);

        // Make sure entries were freed
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(container.TryAdd(i, i * i), "Failed to add value");
        }

        Assert.AreEqual(8, container.Count);
        container.Dispose();
    }

    [Test]
    public void UnsafeHashMap_RemoveOnEmptyMap_DoesNotThrow()
    {
        var container = new UnsafeHashMap<int, int>(0, Allocator.Temp);
        Assert.DoesNotThrow(() => container.Remove(0));
        Assert.DoesNotThrow(() => container.Remove(-425196));
        container.Dispose();
    }

    [Test]
    public void UnsafeHashMap_TryAddScalability()
    {
        var container = new UnsafeHashMap<int, int>(1, Allocator.Persistent);
        Assert.AreEqual(container.Capacity, (1 << container.m_Data.Log2MinGrowth));
        for (int i = 0; i != 1000 * 100; i++)
        {
            container.Add(i, i);
        }

        int value;
        Assert.AreEqual(container.Count, 100000);
        Assert.IsFalse(container.TryGetValue(-1, out value));
        Assert.IsFalse(container.TryGetValue(1000 * 1000, out value));

        for (int i = 0; i != 1000 * 100; i++)
        {
            Assert.IsTrue(container.TryGetValue(i, out value));
            Assert.AreEqual(i, value);
        }

        container.Dispose();
    }

    [Test]
    public unsafe void UnsafeHashMap_TrimExcess()
    {
        using (var container = new UnsafeHashMap<int, int>(1024, Allocator.Persistent))
        {
            var oldCapacity = container.Capacity;

            container.Add(123, 345);
            container.TrimExcess();
            Assert.AreEqual(container.Capacity, (1 << container.m_Data.Log2MinGrowth));
            Assert.AreEqual(1, container.Count);
            Assert.AreNotEqual(oldCapacity, container.Capacity);

            oldCapacity = container.Capacity;

            container.Remove(123);
            Assert.AreEqual(container.Count, 0);
            container.TrimExcess();
            Assert.AreEqual(oldCapacity, container.Capacity);

            container.Add(123, 345);
            Assert.AreEqual(container.Count, 1);
            Assert.AreEqual(oldCapacity, container.Capacity);

            container.Clear();
            Assert.AreEqual(container.Count, 0);
            Assert.AreEqual(oldCapacity, container.Capacity);
        }
    }

    [Test]
    public void UnsafeHashMap_IndexerAdd_ResizesContainer()
    {
        var container = new UnsafeHashMap<int, int>(8, Allocator.Persistent);
        for (int i = 0; i < 1024; i++)
        {
            container[i] = i;
        }
        Assert.AreEqual(1024, container.Count);
        container.Dispose();
    }
}
