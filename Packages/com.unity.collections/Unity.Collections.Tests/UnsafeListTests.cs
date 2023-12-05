using NUnit.Framework;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.Tests;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

internal class UnsafeListTests : CollectionsTestCommonBase
{
    [Test]
    public void UnsafeListT_Init()
    {
        var container = new UnsafeList<int>(0, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        Assert.True(container.IsCreated);
        Assert.True(container.IsEmpty);
        Assert.DoesNotThrow(() => container.Dispose());
    }

    [Test]
    public unsafe void UnsafeListT_Init_ClearMemory()
    {
        var list = new UnsafeList<int>(10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        for (var i = 0; i < list.Length; ++i)
        {
            Assert.AreEqual(0, UnsafeUtility.ReadArrayElement<int>(list.Ptr, i));
        }

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeListT_Allocate_Deallocate_Read_Write()
    {
        var list = new UnsafeList<int>(0, Allocator.Persistent);
        Assert.True(list.IsCreated);
        Assert.True(list.IsEmpty);

        list.Add(1);
        list.Add(2);

        Assert.AreEqual(2, list.Length);
        Assert.AreEqual(1, UnsafeUtility.ReadArrayElement<int>(list.Ptr, 0));
        Assert.AreEqual(2, UnsafeUtility.ReadArrayElement<int>(list.Ptr, 1));

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeListT_Resize_ClearMemory()
    {
        var list = new UnsafeList<int>(5, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        list.SetCapacity(32);
        var capacity = list.Capacity;

        list.Resize(5, NativeArrayOptions.UninitializedMemory);
        Assert.AreEqual(capacity, list.Capacity); // list capacity should not change on resize

        for (var i = 0; i < 5; ++i)
        {
            UnsafeUtility.WriteArrayElement(list.Ptr, i, i);
        }

        list.Resize(10, NativeArrayOptions.ClearMemory);
        Assert.AreEqual(capacity, list.Capacity); // list capacity should not change on resize

        for (var i = 0; i < 5; ++i)
        {
            Assert.AreEqual(i, UnsafeUtility.ReadArrayElement<int>(list.Ptr, i));
        }

        for (var i = 5; i < list.Length; ++i)
        {
            Assert.AreEqual(0, UnsafeUtility.ReadArrayElement<int>(list.Ptr, i));
        }

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeListT_Resize_Zero()
    {
        var list = new UnsafeList<int>(5, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        var capacity = list.Capacity;

        list.Add(1);
        list.Resize(0);
        Assert.AreEqual(0, list.Length);
        Assert.AreEqual(capacity, list.Capacity); // list capacity should not change on resize

        list.Add(2);
        list.Clear();
        Assert.AreEqual(0, list.Length);
        Assert.AreEqual(capacity, list.Capacity); // list capacity should not change on resize

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeListT_SetCapacity()
    {
        using (var list = new UnsafeList<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory))
        {
            list.Add(1);
            Assert.DoesNotThrow(() => list.SetCapacity(128));

            list.Add(1);
            Assert.AreEqual(2, list.Length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<ArgumentOutOfRangeException>(() => list.SetCapacity(1));
#endif

            list.RemoveAtSwapBack(0);
            Assert.AreEqual(1, list.Length);
            Assert.DoesNotThrow(() => list.SetCapacity(1));

            list.TrimExcess();
            Assert.AreEqual(1, list.Capacity);
        }
    }

    [Test]
    public unsafe void UnsafeListT_TrimExcess()
    {
        using (var list = new UnsafeList<int>(32, Allocator.Persistent, NativeArrayOptions.ClearMemory))
        {
            list.Add(1);
            list.TrimExcess();
            Assert.AreEqual(1, list.Length);
            Assert.AreEqual(1, list.Capacity);

            list.RemoveAtSwapBack(0);
            Assert.AreEqual(list.Length, 0);
            list.TrimExcess();
            Assert.AreEqual(list.Capacity, 0);

            list.Add(1);
            Assert.AreEqual(list.Length, 1);
            Assert.AreNotEqual(list.Capacity, 0);

            list.Clear();
        }
    }

    [Test]
    public unsafe void UnsafeListT_DisposeJob()
    {
        var list = new UnsafeList<int>(5, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        var disposeJob = list.Dispose(default);

        Assert.IsTrue(list.Ptr == null);

        disposeJob.Complete();
    }

    unsafe void Expected(ref UnsafeList<int> list, int expectedLength, int[] expected)
    {
        Assert.AreEqual(0 == expectedLength, list.IsEmpty);
        Assert.AreEqual(list.Length, expectedLength);
        for (var i = 0; i < list.Length; ++i)
        {
            var value = UnsafeUtility.ReadArrayElement<int>(list.Ptr, i);
            Assert.AreEqual(expected[i], value);
        }
    }

    [Test]
    public void UnsafeListT_AddReplicate()
    {
        using (var list = new UnsafeList<int>(32, Allocator.Persistent))
        {
            list.AddReplicate(value: 42, count: 10);
            Assert.AreEqual(10, list.Length);
            foreach (var item in list)
                Assert.AreEqual(42, item);

            list.AddReplicate(value: 42, count: 100);
            Assert.AreEqual(110, list.Length);
            foreach (var item in list)
                Assert.AreEqual(42, item);
        }
    }

    [Test]
    public unsafe void UnsafeListT_AddNoResize()
    {
        var list = new UnsafeList<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        // List's capacity is always cache-line aligned, number of items fills up whole cache-line.
        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<InvalidOperationException>(() => { fixed (int* r = range) list.AddRangeNoResize(r, 17); });
#endif

        list.SetCapacity(17);
        Assert.DoesNotThrow(() => { fixed (int* r = range) list.AddRangeNoResize(r, 17); });

        list.Length = 16;
        list.TrimExcess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<InvalidOperationException>(() => { list.AddNoResize(16); });
#endif
    }

    [Test]
    public unsafe void UnsafeListT_AddNoResize_Read()
    {
        var list = new UnsafeList<int>(4, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        list.AddNoResize(4);
        list.AddNoResize(6);
        list.AddNoResize(4);
        list.AddNoResize(9);
        Expected(ref list, 4, new int[] { 4, 6, 4, 9 });

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeListT_RemoveAtSwapBack()
    {
        var list = new UnsafeList<int>(10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        // test removing from the end
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveAtSwapBack(list.Length - 1);
        Expected(ref list, 9, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
        list.Clear();

        // test removing from the end
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveAtSwapBack(5);
        Expected(ref list, 9, new int[] { 0, 1, 2, 3, 4, 9, 6, 7, 8 });
        list.Clear();

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeListT_RemoveRangeSwapBackBE()
    {
        var list = new UnsafeList<int>(10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        // test removing from the end
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveRangeSwapBack(6, 3);
        Expected(ref list, 7, new int[] { 0, 1, 2, 3, 4, 5, 9 });
        list.Clear();

        // test removing all but one
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveRangeSwapBack(0, 9);
        Expected(ref list, 1, new int[] { 9 });
        list.Clear();

        // test removing from the front
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveRangeSwapBack(0, 3);
        Expected(ref list, 7, new int[] { 7, 8, 9, 3, 4, 5, 6 });
        list.Clear();

        // test removing from the middle
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveRangeSwapBack(0, 3);
        Expected(ref list, 7, new int[] { 7, 8, 9, 3, 4, 5, 6 });
        list.Clear();

        // test removing whole range
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveRangeSwapBack(0, 10);
        Expected(ref list, 0, new int[] { 0 });
        list.Clear();

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeListT_RemoveAt()
    {
        var list = new UnsafeList<int>(10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        // test removing from the end
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveAt(list.Length - 1);
        Expected(ref list, 9, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 });
        list.Clear();

        // test removing from the end
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveAt(5);
        Expected(ref list, 9, new int[] { 0, 1, 2, 3, 4, 6, 7, 8, 9 });
        list.Clear();

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeListT_RemoveRange()
    {
        var list = new UnsafeList<int>(10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        // test removing from the end
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveRange(6, 3);
        Expected(ref list, 7, new int[] { 0, 1, 2, 3, 4, 5, 9 });
        list.Clear();

        // test removing all but one
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveRange(0, 9);
        Expected(ref list, 1, new int[] { 9 });
        list.Clear();

        // test removing from the front
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveRange(0, 3);
        Expected(ref list, 7, new int[] { 3, 4, 5, 6, 7, 8, 9 });
        list.Clear();

        // test removing from the middle
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveRange(0, 3);
        Expected(ref list, 7, new int[] { 3, 4, 5, 6, 7, 8, 9 });
        list.Clear();

        // test removing whole range
        fixed (int* r = range) list.AddRange(r, 10);
        list.RemoveRange(0, 10);
        Expected(ref list, 0, new int[] { 0 });
        list.Clear();

        list.Dispose();
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void UnsafeListT_Remove_Throws()
    {
        var list = new UnsafeList<int>(10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        Assert.Throws<IndexOutOfRangeException>(() => { list.RemoveAt(0); });
        Assert.AreEqual(0, list.Length);

        Assert.Throws<IndexOutOfRangeException>(() => { list.RemoveAtSwapBack(0); });
        Assert.AreEqual(0, list.Length);

        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        fixed (int* r = range) list.AddRange(r, 10);

        Assert.Throws<IndexOutOfRangeException>(() => { list.RemoveAt(100); });
        Assert.AreEqual(10, list.Length);

        Assert.Throws<IndexOutOfRangeException>(() => { list.RemoveAtSwapBack(100); });
        Assert.AreEqual(10, list.Length);

        Assert.Throws<ArgumentOutOfRangeException>(() => { list.RemoveRange(0, 100); });
        Assert.AreEqual(10, list.Length);

        Assert.Throws<ArgumentOutOfRangeException>(() => { list.RemoveRangeSwapBack(0, 100); });
        Assert.AreEqual(10, list.Length);

        Assert.Throws<ArgumentOutOfRangeException>(() => { list.RemoveRange(100, -1); });
        Assert.AreEqual(10, list.Length);

        Assert.Throws<ArgumentOutOfRangeException>(() => { list.RemoveRangeSwapBack(100, -1); });
        Assert.AreEqual(10, list.Length);

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeListT_PtrLength()
    {
        var list = new UnsafeList<int>(10, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        int[] range = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        fixed (int* r = range) list.AddRange(r, 10);

        var listView = new UnsafeList<int>(list.Ptr + 4, 2);
        Expected(ref listView, 2, new int[] { 4, 5 });

        listView.Dispose();
        list.Dispose();
    }

    // Burst error BC1071: Unsupported assert type
    // [BurstCompile(CompileSynchronously = true)]
    struct UnsafeListAsReadOnly : IJob
    {
        public UnsafeList<int>.ReadOnly list;

        public void Execute()
        {
            Assert.True(list.Contains(123));
        }
    }

    [Test]
    public void UnsafeListT_AsReadOnly()
    {
        var list = new UnsafeList<int>(10, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        list.Add(123);

        var job = new UnsafeListAsReadOnly
        {
            list = list.AsReadOnly(),
        };

        list.Dispose(job.Schedule()).Complete();
    }

    [BurstCompile(CompileSynchronously = true)]
    struct UnsafeListParallelWriter : IJobParallelFor
    {
        public UnsafeList<int>.ParallelWriter list;

        public void Execute(int index)
        {
            list.AddNoResize(index);
        }
    }

    [Test]
    public void UnsafeListT_ParallelWriter()
    {
        var list = new UnsafeList<int>(256, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        var job = new UnsafeListParallelWriter
        {
            list = list.AsParallelWriter(),
        };

        job.Schedule(list.Capacity, 1).Complete();

        Assert.AreEqual(list.Length, list.Capacity);

        list.Sort<int>();

        for (int i = 0; i < list.Length; i++)
        {
            unsafe
            {
                var value = UnsafeUtility.ReadArrayElement<int>(list.Ptr, i);
                Assert.AreEqual(i, value);
            }
        }

        list.Dispose();
    }

    [BurstCompile(CompileSynchronously = true)]
    struct UnsafeListTestParallelWriter : IJob
    {
        [WriteOnly]
        public UnsafeList<int>.ParallelWriter writer;

        public unsafe void Execute()
        {
            var range = stackalloc int[2] { 7, 3 };

            writer.AddNoResize(range[0]);
            writer.AddRangeNoResize(range, 1);
        }
    }

    [Test]
    public void UnsafeListT_ParallelWriter_NoPtrCaching()
    {
        UnsafeList<int> list;

        {
            list = new UnsafeList<int>(2, Allocator.Persistent);
            var writer = list.AsParallelWriter();
            list.SetCapacity(100);
            var writerJob = new UnsafeListTestParallelWriter { writer = writer }.Schedule();
            writerJob.Complete();
        }

        Assert.AreEqual(2, list.Length);
        Assert.AreEqual(7, list[0]);
        Assert.AreEqual(7, list[1]);

        list.Dispose();
    }

    [Test]
    public unsafe void UnsafeListT_IndexOf()
    {
        using (var list = new UnsafeList<int>(10, Allocator.Persistent) { 123, 789 })
        {
            bool r0 = false, r1 = false, r2 = false;

            GCAllocRecorder.ValidateNoGCAllocs(() =>
            {
                r0 = -1 != list.IndexOf(456);
                r1 = list.Contains(123);
                r2 = list.Contains(789);
            });

            Assert.False(r0);
            Assert.True(r1);
            Assert.True(r2);
        }
    }

    [Test]
    public void UnsafeListT_InsertRangeWithBeginEnd()
    {
        var list = new UnsafeList<byte>(3, Allocator.Persistent);
        list.Add(0);
        list.Add(3);
        list.Add(4);
        Assert.AreEqual(3, list.Length);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<ArgumentOutOfRangeException>(() => list.InsertRangeWithBeginEnd(-1, 8));
        Assert.Throws<ArgumentException>(() => list.InsertRangeWithBeginEnd(3, 1));
#endif

        Assert.DoesNotThrow(() => list.InsertRangeWithBeginEnd(1, 3));
        Assert.AreEqual(5, list.Length);

        list[1] = 1;
        list[2] = 2;

        for (var i = 0; i < list.Length; ++i)
        {
            Assert.AreEqual(i, list[i]);
        }

        Assert.DoesNotThrow(() => list.InsertRangeWithBeginEnd(5, 8));
        Assert.AreEqual(8, list.Length);

        list[5] = 5;
        list[6] = 6;
        list[7] = 7;

        for (var i = 0; i < list.Length; ++i)
        {
            Assert.AreEqual(i, list[i]);
        }

        list.Dispose();
    }

    [Test]
    public void UnsafeListT_InsertRange()
    {
        var list = new UnsafeList<byte>(3, Allocator.Persistent);
        list.Add(0);
        list.Add(3);
        list.Add(4);
        Assert.AreEqual(3, list.Length);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<ArgumentOutOfRangeException>(() => list.InsertRange(-1, 8));
        Assert.Throws<ArgumentException>(() => list.InsertRange(3, -1));
#endif

        Assert.DoesNotThrow(() => list.InsertRange(1, 0));
        Assert.AreEqual(3, list.Length);

        Assert.DoesNotThrow(() => list.InsertRange(1, 2));
        Assert.AreEqual(5, list.Length);

        list[1] = 1;
        list[2] = 2;

        for (var i = 0; i < list.Length; ++i)
        {
            Assert.AreEqual(i, list[i]);
        }

        Assert.DoesNotThrow(() => list.InsertRange(5, 3));
        Assert.AreEqual(8, list.Length);

        list[5] = 5;
        list[6] = 6;
        list[7] = 7;

        for (var i = 0; i < list.Length; ++i)
        {
            Assert.AreEqual(i, list[i]);
        }

        list.Dispose();
    }

    [Test]
    public void UnsafeListT_ForEach([Values(10, 1000)] int n)
    {
        var seen = new NativeArray<int>(n, Allocator.Temp);
        using (var container = new UnsafeList<int>(32, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < n; i++)
            {
                container.Add(i);
            }

            var count = 0;
            unsafe
            {
                UnsafeList<int>* test = &container;

                foreach (var item in *test)
                {
                    Assert.True(test->Contains(item));
                    seen[item] = seen[item] + 1;
                    ++count;
                }
            }

            Assert.AreEqual(container.Length, count);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect item count {i}");
            }
        }
    }

    [Test]
    public void UnsafeListT_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new UnsafeList<byte>(1, allocator.Handle))
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
                using (var container = new UnsafeList<byte>(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void UnsafeListT_BurstedCustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        var allocatorPtr = (CustomAllocatorTests.CountingAllocator*)UnsafeUtility.AddressOf(ref allocator);
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

    void IIndexableTest<T>(T container)
        where T : unmanaged, IIndexable<int>
    {
        var length = container.Length;

        Assert.Throws<IndexOutOfRangeException>(() => container.ElementAt(-1));
        Assert.Throws<IndexOutOfRangeException>(() => container.ElementAt(container.Length));

        Assert.DoesNotThrow(() => { for (int i = 0, len = container.Length; i < len; ++i) { container.ElementAt(i) = 4; } });

        for (int i = 0, len = container.Length; i < len; ++i)
        {
            Assert.AreEqual(4, container.ElementAt(i));
        }
    }

    void INativeListTest<T>(T container)
        where T : unmanaged, INativeList<int>
    {
        var length = container.Length;

        Assert.Throws<IndexOutOfRangeException>(() => container[-1] = 1);
        Assert.Throws<IndexOutOfRangeException>(() => container[container.Length] = 1);

        Assert.DoesNotThrow(() => { for (int i = 0, len = container.Length; i < len; ++i) { container[i] = 4; } });

        Assert.Throws<ArgumentOutOfRangeException>(() => container.Capacity = container.Length - 1);
        Assert.DoesNotThrow(() => container.Capacity = container.Length);
        Assert.DoesNotThrow(() => container.Capacity = container.Length + 1);

        for (int i = 0, len = container.Length; i < len; ++i)
        {
            Assert.AreEqual(4, container[i]);
        }
    }

    private unsafe void TestInterfaces<T>(T container)
        where T : unmanaged, IIndexable<int>, INativeList<int>
	{
        container.Length = 4;
        Assert.DoesNotThrow(() => { for (int i = 0, len = container.Length; i < len; ++i) { container.ElementAt(i) = i; } });

        IIndexableTest(container);
        INativeListTest(container);
    }
    private unsafe void TestInterfacesDispose<T>(T container)
        where T : unmanaged, IIndexable<int>, INativeList<int>, IDisposable
    {
        TestInterfaces(container);
        container.Dispose();
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void UnsafeListT_TestInterfaces() => TestInterfacesDispose(new UnsafeList<int>(1, CommonRwdAllocator.Handle));

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void NativeList_TestInterfaces() => TestInterfacesDispose(new NativeList<int>(1, CommonRwdAllocator.Handle));

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void FixedList32Bytes_TestInterfaces() => TestInterfaces(new FixedList32Bytes<int>());

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void FixedList64Bytes_TestInterfaces() => TestInterfaces(new FixedList64Bytes<int>());

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void FixedList128Bytes_TestInterfaces() => TestInterfaces(new FixedList128Bytes<int>());

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void FixedList512Bytes_TestInterfaces() => TestInterfaces(new FixedList512Bytes<int>());

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public unsafe void FixedList4096Bytes_TestInterfaces() => TestInterfaces(new FixedList4096Bytes<int>());
}
