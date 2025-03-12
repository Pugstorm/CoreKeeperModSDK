using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.NotBurstCompatible;
using Unity.Jobs;
using Unity.Collections.Tests;

internal class NativeHashSetTests: CollectionsTestFixture
{
    static void ExpectedCount<T>(ref NativeHashSet<T> container, int expected)
        where T : unmanaged, IEquatable<T>
    {
        Assert.AreEqual(expected == 0, container.IsEmpty);
        Assert.AreEqual(expected, container.Count);
    }

    [Test]
    public void NativeHashSet_IsEmpty()
    {
        var container = new NativeHashSet<int>(0, Allocator.Persistent);
        Assert.IsTrue(container.IsEmpty);

        Assert.IsTrue(container.Add(0));
        Assert.IsFalse(container.IsEmpty);
        Assert.AreNotEqual(0, container.Capacity);
        ExpectedCount(ref container, 1);

        container.Remove(0);
        Assert.IsTrue(container.IsEmpty);

        Assert.IsTrue(container.Add(0));
        container.Clear();
        Assert.IsTrue(container.IsEmpty);

        container.Dispose();
    }

    [Test]
    public void NativeHashSet_Capacity()
    {
        var container = new NativeHashSet<int>(0, Allocator.Persistent);
        Assert.IsTrue(container.IsEmpty);

        container.Capacity = 10;
        Assert.AreNotEqual(0, container.Capacity);

        container.Dispose();
    }

    [Test]
    public void NativeHashSet_CapacityAtLeastCount()
    {
        var container = new NativeHashSet<int>(0, Allocator.Persistent);

        for (int i = 0; i < 300; i++)
            container.Add(i);

        container.Capacity = 3;
        Assert.IsTrue(container.Capacity >= container.Count);

        container.Dispose();
    }

    [Test]
    public void NativeHashSet_RemoveOnEmptyMap_DoesNotThrow()
    {
        var container = new NativeHashSet<int>(0, Allocator.Temp);
        Assert.DoesNotThrow(() => container.Remove(0));
        Assert.DoesNotThrow(() => container.Remove(-425196));
        container.Dispose();
    }

    [Test]
    public void NativeHashSet_Collisions()
    {
        var container = new NativeHashSet<int>(16, Allocator.Temp);

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
    public void NativeHashSet_SameElement()
    {
        using (var container = new NativeHashSet<int>(0, Allocator.Persistent))
        {
            Assert.IsTrue(container.Add(0));
            Assert.IsFalse(container.Add(0));
        }
    }

    [Test]
    public void NativeHashSet_CanBeReadFromJob()
    {
        using (var hashSet = new NativeHashSet<int>(1, CommonRwdAllocator.Handle))
        using (var result = new NativeReference<int>(CommonRwdAllocator.Handle))
        {
            hashSet.Add(42);
            new ReadHashSetJob
            {
                Input = hashSet.AsReadOnly(),
                Output = result,
            }.Run();
            Assert.AreEqual(42, result.Value);
        }
    }

    struct TempHashSet : IJob
    {
        public void Execute()
        {
            using (var stringList = new NativeList<FixedString32Bytes>(10, Allocator.Persistent) { "Hello", ",", "World", "!" })
            {
                var container = new NativeHashSet<FixedString128Bytes>(50, Allocator.Temp);
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
    }

    [Test]
    public void NativeHashSet_TempHashSetInJob()
    {
        new TempHashSet { }.Schedule().Complete();
    }

    struct ReadHashSetJob : IJob
    {
        [ReadOnly]
        public NativeHashSet<int>.ReadOnly Input;

        public NativeReference<int> Output;
        public void Execute()
        {
            Output.Value = Input.ToNativeArray(Allocator.Temp)[0];

            foreach (var value in Input)
            {
                Assert.AreEqual(42, value);
            }
        }
    }

    [Test]
    public void NativeHashSet_ForEach_FixedStringInHashMap()
    {
        using (var stringList = new NativeList<FixedString32Bytes>(10, Allocator.Persistent) { "Hello", ",", "World", "!" })
        {
            var container = new NativeHashSet<FixedString128Bytes>(50, Allocator.Temp);
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
    public void NativeHashSet_ForEach([Values(10, 1000)]int n)
    {
        var seen = new NativeArray<int>(n, Allocator.Temp);
        using (var container = new NativeHashSet<int>(32, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < n; i++)
            {
                Assert.True(container.Add(i));
            }

            var count = 0;
            foreach (var item in container)
            {
                Assert.True(container.Contains(item));
                seen[item] = seen[item] + 1;
                ++count;
            }

            Assert.AreEqual(container.Count, count);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect item count {i}");
            }
        }
    }

    struct NativeHashSet_ForEach_Job : IJob
    {
        [ReadOnly]
        public NativeHashSet<int>.ReadOnly Input;

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

            Assert.AreEqual(Input.Count, count);
            for (int i = 0; i < Num; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect item count {i}");
            }

            seen.Dispose();
        }
    }

    [Test]
    public void NativeHashSet_ForEach_From_Job([Values(10, 1000)] int n)
    {
        using (var container = new NativeHashSet<int>(32, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < n; i++)
            {
                container.Add(i);
            }

            new NativeHashSet_ForEach_Job
            {
                Input = container.AsReadOnly(),
                Num = n,

            }.Run();
        }
    }

    struct NativeHashSet_Write_Job : IJob
    {
        public NativeHashSet<int> Input;

        public void Execute()
        {
            Input.Clear();
        }
    }

    [Test]
    public void NativeHashSet_Write_From_Job()
    {
        using (var container = new NativeHashSet<int>(32, CommonRwdAllocator.Handle))
        {
            container.Add(0);
            Assert.IsFalse(container.IsEmpty);
            new NativeHashSet_Write_Job
            {
                Input = container,
            }.Run();
            Assert.IsTrue(container.IsEmpty);
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeHashSet_ForEach_Throws_When_Modified()
    {
        using (var container = new NativeHashSet<int>(32, CommonRwdAllocator.Handle))
        {
            container.Add(0);
            container.Add(1);
            container.Add(2);
            container.Add(3);
            container.Add(4);
            container.Add(5);
            container.Add(6);
            container.Add(7);
            container.Add(8);
            container.Add(9);

            Assert.Throws<ObjectDisposedException>(() =>
            {
                foreach (var item in container)
                {
                    container.Add(10);
                }
            });

            Assert.Throws<ObjectDisposedException>(() =>
            {
                foreach (var item in container)
                {
                    container.Remove(1);
                }
            });
        }
    }

    struct ForEachIterator : IJob
    {
        [ReadOnly]
        public NativeHashSet<int>.Enumerator Iter;

        public void Execute()
        {
            while (Iter.MoveNext())
            {
            }
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeHashSet_ForEach_Throws_Job_Iterator()
    {
        using (var container = new NativeHashSet<int>(32, CommonRwdAllocator.Handle))
        {
            var jobHandle = new ForEachIterator
            {
                Iter = container.GetEnumerator()

            }.Schedule();

            Assert.Throws<InvalidOperationException>(() => { container.Add(1); });

            jobHandle.Complete();
        }
    }

    [Test]
    public void NativeHashSet_EIU_ExceptWith_Empty()
    {
        var setA = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var setB = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { };
        setA.ExceptWith(setB);

        ExpectedCount(ref setA, 0);

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void NativeHashSet_EIU_ExceptWith_AxB()
    {
        var setA = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var setB = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        setA.ExceptWith(setB);

        ExpectedCount(ref setA, 3);
        Assert.True(setA.Contains(0));
        Assert.True(setA.Contains(1));
        Assert.True(setA.Contains(2));

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void NativeHashSet_EIU_ExceptWith_BxA()
    {
        var setA = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var setB = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        setB.ExceptWith(setA);

        ExpectedCount(ref setB, 3);
        Assert.True(setB.Contains(6));
        Assert.True(setB.Contains(7));
        Assert.True(setB.Contains(8));

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void NativeHashSet_EIU_IntersectWith_Empty()
    {
        var setA = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var setB = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { };
        setA.IntersectWith(setB);

        ExpectedCount(ref setA, 0);

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void NativeHashSet_EIU_IntersectWith()
    {
        var setA = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var setB = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
        setA.IntersectWith(setB);

        ExpectedCount(ref setA, 3);
        Assert.True(setA.Contains(3));
        Assert.True(setA.Contains(4));
        Assert.True(setA.Contains(5));

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void NativeHashSet_EIU_UnionWith_Empty()
    {
        var setA = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { };
        var setB = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { };
        setA.UnionWith(setB);

        ExpectedCount(ref setA, 0);

        setA.Dispose();
        setB.Dispose();
    }

    [Test]
    public void NativeHashSet_EIU_UnionWith()
    {
        var setA = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 };
        var setB = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { 3, 4, 5, 6, 7, 8 };
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
    public void NativeHashSet_ToArray()
    {
        using (var set = new NativeHashSet<int>(8, CommonRwdAllocator.Handle) { 0, 1, 2, 3, 4, 5 })
        {
            var array = set.ToArray();
            Array.Sort(array);
            for (int i = 0, num = set.Count; i < num; i++)
            {
                Assert.AreEqual(array[i], i);
            }
        }
    }

    [Test]
    public void NativeHashSet_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new NativeHashSet<int>(1, allocator.Handle))
        {
        }

        FastAssert.IsTrue(allocator.WasUsed);
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
                using (var container = new NativeHashSet<int>(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void NativeHashSet_BurstedCustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        var allocatorPtr = (CustomAllocatorTests.CountingAllocator*)UnsafeUtility.AddressOf<CustomAllocatorTests.CountingAllocator>(ref allocator);
        unsafe
        {
            var handle = new BurstedCustomAllocatorJob {Allocator = allocatorPtr }.Schedule();
            handle.Complete();
        }

        FastAssert.IsTrue(allocator.WasUsed);
        allocator.Dispose();
        allocatorHelper.Dispose();
        AllocatorManager.Shutdown();
    }
}
