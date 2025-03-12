using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;
using Unity.Jobs;
using Assert = FastAssert;

[BurstCompile]
internal class UnsafeQueueTests : CollectionsTestCommonBase
{
    static void ExpectedCount<T>(ref UnsafeQueue<T> container, int expected) where T : unmanaged
    {
        Assert.AreEqual(expected == 0, container.IsEmpty());
        Assert.AreEqual(expected, container.Count);
    }

    [Test]
    public void Enqueue_Dequeue()
    {
        var queue = new UnsafeQueue<int>(Allocator.Temp);
        ExpectedCount(ref queue, 0);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
#endif

        for (int i = 0; i < 16; ++i)
            queue.Enqueue(i);
        ExpectedCount(ref queue, 16);
        for (int i = 0; i < 16; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 0);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
#endif

        queue.Dispose();
    }

    [Test]
    public void ConcurrentEnqueue_Dequeue()
    {
        var queue = new UnsafeQueue<int>(Allocator.Temp);
        var cQueue = queue.AsParallelWriter();
        ExpectedCount(ref queue, 0);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
#endif

        for (int i = 0; i < 16; ++i)
            cQueue.Enqueue(i);
        ExpectedCount(ref queue, 16);
        for (int i = 0; i < 16; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 0);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
#endif

        queue.Dispose();
    }

    [Test]
    public void Enqueue_Dequeue_Peek()
    {
        var queue = new UnsafeQueue<int>(Allocator.Temp);
        ExpectedCount(ref queue, 0);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
#endif

        for (int i = 0; i < 16; ++i)
            queue.Enqueue(i);
        ExpectedCount(ref queue, 16);
        for (int i = 0; i < 16; ++i)
        {
            Assert.AreEqual(i, queue.Peek(), "Got the wrong value from the queue");
            queue.Dequeue();
        }
        ExpectedCount(ref queue, 0);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
#endif

        queue.Dispose();
    }

    [Test]
    public void Enqueue_Dequeue_Clear()
    {
        var queue = new UnsafeQueue<int>(Allocator.Temp);
        ExpectedCount(ref queue, 0);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
#endif

        for (int i = 0; i < 16; ++i)
            queue.Enqueue(i);
        ExpectedCount(ref queue, 16);
        for (int i = 0; i < 8; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 8);
        queue.Clear();
        ExpectedCount(ref queue, 0);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
#endif

        queue.Dispose();
    }

    [Test]
    public void Double_Deallocate_DoesNotThrow()
    {
        var queue = new UnsafeQueue<int>(CommonRwdAllocator.Handle);
        queue.Dispose();
        Assert.DoesNotThrow(
            () => { queue.Dispose(); });
    }

    [Test]
    public void EnqueueScalability()
    {
        var queue = new UnsafeQueue<int>(Allocator.Persistent);
        for (int i = 0; i != 1000 * 100; i++)
        {
            queue.Enqueue(i);
        }

        ExpectedCount(ref queue, 1000 * 100);

        for (int i = 0; i != 1000 * 100; i++)
            Assert.AreEqual(i, queue.Dequeue());
        ExpectedCount(ref queue, 0);

        queue.Dispose();
    }

    [Test]
    public void Enqueue_Wrap()
    {
        var queue = new UnsafeQueue<int>(Allocator.Temp);
        ExpectedCount(ref queue, 0);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
#endif

        for (int i = 0; i < 256; ++i)
            queue.Enqueue(i);
        ExpectedCount(ref queue, 256);

        for (int i = 0; i < 128; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 128);

        for (int i = 0; i < 128; ++i)
            queue.Enqueue(i);
        ExpectedCount(ref queue, 256);

        for (int i = 128; i < 256; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 128);

        for (int i = 0; i < 128; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 0);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
#endif

        queue.Dispose();
    }

    [Test]
    public void ConcurrentEnqueue_Wrap()
    {
        var queue = new UnsafeQueue<int>(Allocator.Temp);
        var cQueue = queue.AsParallelWriter();
        ExpectedCount(ref queue, 0);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
#endif

        for (int i = 0; i < 256; ++i)
            cQueue.Enqueue(i);
        ExpectedCount(ref queue, 256);

        for (int i = 0; i < 128; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 128);

        for (int i = 0; i < 128; ++i)
            cQueue.Enqueue(i);
        ExpectedCount(ref queue, 256);

        for (int i = 128; i < 256; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 128);

        for (int i = 0; i < 128; ++i)
            Assert.AreEqual(i, queue.Dequeue(), "Got the wrong value from the queue");
        ExpectedCount(ref queue, 0);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        Assert.Throws<System.InvalidOperationException>(() => {queue.Dequeue(); });
#endif

        queue.Dispose();
    }

    [Test]
    public void TryDequeue_OnEmptyQueueWhichHadElements_RetainsValidState()
    {
        using (var queue = new UnsafeQueue<int>(Allocator.Temp))
        {
            for (int i = 0; i < 3; i++)
            {
                queue.Enqueue(i);
                Assert.AreEqual(1, queue.Count);

                int value;
                while (queue.TryDequeue(out value))
                {
                    Assert.AreEqual(i, value);
                }

                Assert.AreEqual(0, queue.Count);
            }
        }
    }

    [Test]
    public void TryDequeue_OnEmptyQueue_RetainsValidState()
    {
        using (var queue = new UnsafeQueue<int>(Allocator.Temp))
        {
            Assert.IsFalse(queue.TryDequeue(out _));
            queue.Enqueue(1);
            Assert.AreEqual(1, queue.Count);
        }
    }

    [Test]
    public void ToArray_ContainsCorrectElements()
    {
        using (var queue = new UnsafeQueue<int>(Allocator.Temp))
        {
            for (int i = 0; i < 100; i++)
                queue.Enqueue(i);
            using (var array = queue.ToArray(Allocator.Temp))
            {
                Assert.AreEqual(queue.Count, array.Length);
                for (int i = 0; i < array.Length; i++)
                    Assert.AreEqual(i, array[i]);
            }
        }
    }

    [Test]
    public void ToArray_RespectsDequeue()
    {
        using (var queue = new UnsafeQueue<int>(Allocator.Temp))
        {
            for (int i = 0; i < 100; i++)
                queue.Enqueue(i);
            for (int i = 0; i < 50; i++)
                queue.Dequeue();
            using (var array = queue.ToArray(Allocator.Temp))
            {
                Assert.AreEqual(queue.Count, array.Length);
                for (int i = 0; i < array.Length; i++)
                    Assert.AreEqual(50 + i, array[i]);
            }
        }
    }

    [Test]
    public void UnsafeQueue_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new UnsafeQueue<int>(allocator.Handle))
        {
        }

        Assert.IsTrue(allocator.WasUsed);
        allocator.Dispose();
        allocatorHelper.Dispose();
        AllocatorManager.Shutdown();
    }

    [BurstCompile(CompileSynchronously = true)]
    struct BurstedCustomAllocatorJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe CustomAllocatorTests.CountingAllocator* Allocator;

        public void Execute()
        {
            unsafe
            {
                using (var container = new UnsafeQueue<int>(Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void UnsafeQueue_BurstedCustomAllocatorTest()
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

    public struct NestedContainer
    {
        public UnsafeQueue<int> data;
    }

    [Test]
    public void UnsafeQueue_Nested()
    {
        var inner = new UnsafeQueue<int>(CommonRwdAllocator.Handle);
        NestedContainer nestedStruct = new NestedContainer { data = inner };

        var containerNestedStruct = new UnsafeQueue<NestedContainer>(CommonRwdAllocator.Handle);
        var containerNested = new UnsafeQueue<UnsafeQueue<int>>(CommonRwdAllocator.Handle);

        containerNested.Enqueue(inner);
        containerNestedStruct.Enqueue(nestedStruct);

        containerNested.Dispose();
        containerNestedStruct.Dispose();
        inner.Dispose();
    }
}
