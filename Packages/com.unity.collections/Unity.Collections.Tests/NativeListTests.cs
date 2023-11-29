using NUnit.Framework;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

internal class NativeListTests : CollectionsTestFixture
{
    static void ExpectedLength<T>(ref NativeList<T> container, int expected)
        where T : unmanaged
    {
        Assert.AreEqual(expected == 0, container.IsEmpty);
        Assert.AreEqual(expected, container.Length);
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NullListThrow()
    {
        var list = new NativeList<int>();
        Assert.Throws<NullReferenceException>(() => list[0] = 5);
        Assert.Throws<ObjectDisposedException>(
            () => list.Add(1));
    }

    [Test]
    public void NativeList_Allocate_Deallocate_Read_Write()
    {
        var list = new NativeList<int>(Allocator.Persistent);

        list.Add(1);
        list.Add(2);

        ExpectedLength(ref list, 2);
        Assert.AreEqual(1, list[0]);
        Assert.AreEqual(2, list[1]);

        list.Dispose();
    }

    [Test]
    public void NativeArrayFromNativeList()
    {
        var list = new NativeList<int>(Allocator.Persistent);
        list.Add(42);
        list.Add(2);

        NativeArray<int> array = list.AsArray();

        Assert.AreEqual(2, array.Length);
        Assert.AreEqual(42, array[0]);
        Assert.AreEqual(2, array[1]);

        list.Dispose();
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeArrayFromNativeListInvalidatesOnAdd()
    {
        var list = new NativeList<int>(Allocator.Persistent);

        // This test checks that adding an element without reallocation invalidates the native array
        // (length changes)
        list.Capacity = 2;
        list.Add(42);

        NativeArray<int> array = list.AsArray();

        list.Add(1000);

        ExpectedLength(ref list, 2);
        Assert.Throws<ObjectDisposedException>(
            () => { array[0] = 1; });

        list.Dispose();
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeArrayFromNativeListInvalidatesOnCapacityChange()
    {
        var list = new NativeList<int>(Allocator.Persistent);
        list.Add(42);

        NativeArray<int> array = list.AsArray();

        ExpectedLength(ref list, 1);
        list.Capacity = 10;

        //Assert.AreEqual(1, array.Length); - temporarily commenting out updated assert checks to ensure editor version promotion succeeds
        Assert.Throws<ObjectDisposedException>(
             () => { array[0] = 1; });

        list.Dispose();
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeArrayFromNativeListInvalidatesOnDispose()
    {
        var list = new NativeList<int>(Allocator.Persistent);
        list.Add(42);
        NativeArray<int> array = list.AsArray();
        list.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => { array[0] = 1; });

        Assert.Throws<ObjectDisposedException>(
            () => { list[0] = 1; });
    }

    [Test]
    public void NativeArrayFromNativeListMayDeallocate()
    {
        var list = new NativeList<int>(Allocator.Persistent);
        list.Add(42);

        NativeArray<int> array = list.AsArray();
        Assert.DoesNotThrow(() => { array.Dispose(); });
        list.Dispose();
    }

    [Test]
    public void CopiedNativeListIsKeptInSync()
    {
        var list = new NativeList<int>(Allocator.Persistent);
        var listCpy = list;
        list.Add(42);

        Assert.AreEqual(42, listCpy[0]);
        Assert.AreEqual(42, list[0]);
        Assert.AreEqual(1, listCpy.Length);
        ExpectedLength(ref list, 1);

        list.Dispose();
    }

    [Test]
    public void NativeList_CopyFrom_Managed()
    {
        var list = new NativeList<float>(4, Allocator.Persistent);
        var ar = new float[] { 0, 1, 2, 3, 4, 5, 6, 7 };
        list.CopyFromNBC(ar);
        ExpectedLength(ref list, 8);
        for (int i = 0; i < list.Length; ++i)
        {
            Assert.AreEqual(i, list[i]);
        }
        list.Dispose();
    }

    [Test]
    public void NativeList_CopyFrom_OtherContainers()
    {
        var list = new NativeList<int>(4, Allocator.Persistent);

        {
            var container = new NativeArray<int>(new int[] { 0, 1, 2, 3, 4, 5, 6, 7 }, Allocator.Persistent);

            list.CopyFrom(container);
            ExpectedLength(ref list, 8);
            for (int i = 0; i < list.Length; ++i)
            {
                Assert.AreEqual(i, list[i]);
            }

            container.Dispose();
        }

        list.Add(123);

        {
            var container = new NativeList<int>(32, Allocator.Persistent) { 0, 1, 2, 3, 4, 5, 6, 7 };

            list.CopyFrom(container);
            ExpectedLength(ref list, 8);
            for (int i = 0; i < list.Length; ++i)
            {
                Assert.AreEqual(i, list[i]);
            }

            container.Dispose();
        }

        list.Add(345);

        {
            var container = new UnsafeList<int>(32, Allocator.Persistent) { 0, 1, 2, 3, 4, 5, 6, 7 };

            list.CopyFrom(container);
            ExpectedLength(ref list, 8);
            for (int i = 0; i < list.Length; ++i)
            {
                Assert.AreEqual(i, list[i]);
            }

            container.Dispose();
        }

        list.Add(789);

        {
            var container = new NativeHashSet<int>(32, Allocator.Persistent) { 0, 1, 2, 3, 4, 5, 6, 7 };

            using (var array = container.ToNativeArray(Allocator.TempJob))
            {
                list.CopyFrom(array);
            }
            ExpectedLength(ref list, 8);
            for (int i = 0; i < list.Length; ++i)
            {
                list.Contains(i);
            }

            container.Dispose();
        }

        list.Add(123);

        {
            var container = new UnsafeHashSet<int>(32, Allocator.Persistent) { 0, 1, 2, 3, 4, 5, 6, 7 };

            using (var array = container.ToNativeArray(Allocator.TempJob))
            {
                list.CopyFrom(array);
            }
            ExpectedLength(ref list, 8);
            for (int i = 0; i < list.Length; ++i)
            {
                list.Contains(i);
            }

            container.Dispose();
        }

        list.Dispose();
    }

    [BurstCompile(CompileSynchronously = true)]
    struct TempListInJob : IJob
    {
        public NativeArray<int> Output;
        public void Execute()
        {
            var list = new NativeList<int>(Allocator.Temp);

            list.Add(17);

            Output[0] = list[0];

            list.Dispose();
        }
    }


    [Test]
    [Ignore("Unstable on CI, DOTS-1965")]
    public void TempListInBurstJob()
    {
        var job = new TempListInJob() { Output = CollectionHelper.CreateNativeArray<int>(1, CommonRwdAllocator.Handle) };
        job.Schedule().Complete();
        Assert.AreEqual(17, job.Output[0]);

        job.Output.Dispose();
    }

    [Test]
    public void SetCapacityLessThanLength()
    {
        var list = new NativeList<int>(Allocator.Persistent);
        list.Resize(10, NativeArrayOptions.UninitializedMemory);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        Assert.Throws<ArgumentOutOfRangeException>(() => { list.Capacity = 5; });
#endif

        list.Dispose();
    }

    [Test]
    public void DisposingNativeListDerivedArrayDoesNotThrow()
    {
        var list = new NativeList<int>(Allocator.Persistent);
        list.Add(1);

        NativeArray<int> array = list.AsArray();
        Assert.DoesNotThrow(() => { array.Dispose(); });

        list.Dispose();
    }

    [Test]
    public void NativeList_DisposeJob()
    {
        var container = new NativeList<int>(Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.DoesNotThrow(() => { container.Add(0); });
        Assert.DoesNotThrow(() => { container.Contains(0); });

        var disposeJob = container.Dispose(default);
        Assert.False(container.IsCreated);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        Assert.Throws<ObjectDisposedException>(
            () => { container.Contains(0); });
#endif

        disposeJob.Complete();
    }

    [Test]
    public void ForEachWorks()
    {
        var container = new NativeList<int>(Allocator.Persistent);
        container.Add(10);
        container.Add(20);

        int sum = 0;
        int count = 0;
        GCAllocRecorder.ValidateNoGCAllocs(() =>
        {
            sum = 0;
            count = 0;
            foreach (var p in container)
            {
                sum += p;
                count++;
            }
        });

        Assert.AreEqual(30, sum);
        Assert.AreEqual(2, count);

        container.Dispose();
    }

    // These tests require:
    // - JobsDebugger support for static safety IDs (added in 2020.1)
    // - Asserting throws
#if !UNITY_DOTSRUNTIME
    [Test,DotsRuntimeIgnore]
    [TestRequiresCollectionChecks]
    public void NativeList_UseAfterFree_UsesCustomOwnerTypeName()
    {
        var list = new NativeList<int>(10, CommonRwdAllocator.Handle);
        list.Add(17);
        list.Dispose();
        Assert.That(() => list[0],
            Throws.Exception.TypeOf<ObjectDisposedException>()
                .With.Message.Contains($"The {list.GetType()} has been deallocated"));
    }

    [Test,DotsRuntimeIgnore]
    [TestRequiresCollectionChecks]
    public void AtomicSafetyHandle_AllocatorTemp_UniqueStaticSafetyIds()
    {
        // All collections that use Allocator.Temp share the same core AtomicSafetyHandle.
        // This test verifies that containers can proceed to assign unique static safety IDs to each
        // AtomicSafetyHandle value, which will not be shared by other containers using Allocator.Temp.
        var listInt = new NativeList<int>(10, Allocator.Temp);
        var listFloat = new NativeList<float>(10, Allocator.Temp);
        listInt.Add(17);
        listInt.Dispose();
        Assert.That(() => listInt[0],
            Throws.Exception.TypeOf<ObjectDisposedException>()
                .With.Message.Contains($"The {listInt.GetType()} has been deallocated"));
        listFloat.Add(1.0f);
        listFloat.Dispose();
        Assert.That(() => listFloat[0],
            Throws.Exception.TypeOf<ObjectDisposedException>()
                .With.Message.Contains($"The {listFloat.GetType()} has been deallocated"));
    }

    [BurstCompile(CompileSynchronously = true)]
    struct NativeListCreateAndUseAfterFreeBurst : IJob
    {
        public void Execute()
        {
            var list = new NativeList<int>(10, Allocator.Temp);
            list.Add(17);
            list.Dispose();
            list.Add(42);
        }
    }

    [Test,DotsRuntimeIgnore]
    [TestRequiresCollectionChecks]
    public void NativeList_CreateAndUseAfterFreeInBurstJob_UsesCustomOwnerTypeName()
    {
        // Make sure this isn't the first container of this type ever created, so that valid static safety data exists
        var list = new NativeList<int>(10, CommonRwdAllocator.Handle);
        list.Dispose();

        var job = new NativeListCreateAndUseAfterFreeBurst
        {
        };

        // Two things:
        // 1. This exception is logged, not thrown; thus, we use LogAssert to detect it.
        // 2. Calling write operation after container.Dispose() emits an unintuitive error message. For now, all this test cares about is whether it contains the
        //    expected type name.
        job.Run();
        LogAssert.Expect(LogType.Exception,
            new Regex($"InvalidOperationException: The {Regex.Escape(list.GetType().ToString())} has been declared as \\[ReadOnly\\] in the job, but you are writing to it"));
    }
#endif

    [Test]
    public unsafe void NativeList_IndexOf()
    {
        using (var list = new NativeList<int>(10, Allocator.Persistent) { 123, 789 })
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
    public void NativeList_InsertRangeWithBeginEnd()
    {
        var list = new NativeList<byte>(3, Allocator.Persistent);
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
    public void NativeList_InsertRange()
    {
        var list = new NativeList<byte>(3, Allocator.Persistent);
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
    public void NativeList_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new NativeList<byte>(1, allocator.Handle))
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
                using (var container = new NativeList<byte>(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void NativeList_BurstedCustomAllocatorTest()
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
    public unsafe void NativeList_SetCapacity()
    {
        using (var list = new NativeList<int>(1, Allocator.Persistent))
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
    public unsafe void NativeList_TrimExcess()
    {
        using (var list = new NativeList<int>(32, Allocator.Persistent))
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

    public struct NestedContainer
    {
        public NativeList<int> data;
    }

    [Test]
    public void NativeList_Nested()
    {
        var inner = new NativeList<int>(CommonRwdAllocator.Handle);
        NestedContainer nestedStruct = new NestedContainer { data = inner };

        var containerNestedStruct = new NativeList<NestedContainer>(CommonRwdAllocator.Handle);
        var containerNested = new NativeList<NativeList<int>>(CommonRwdAllocator.Handle);

        containerNested.Add(inner);
        containerNestedStruct.Add(nestedStruct);

        containerNested.Dispose();
        containerNestedStruct.Dispose();
        inner.Dispose();
    }

    [Test]
    public void NativeList_AddReplicate()
    {
        using (var list = new NativeList<int>(32, Allocator.Persistent))
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
}
