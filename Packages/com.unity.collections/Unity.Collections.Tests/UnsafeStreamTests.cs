using NUnit.Framework;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;
using Unity.Jobs;

internal class UnsafeStreamTests : CollectionsTestCommonBase
{
    [Test]
    public void UnsafeStream_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new UnsafeStream(1, allocator.Handle))
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
                using (var container = new UnsafeStream(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void UnsafeStream_BurstedCustomAllocatorTest()
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
    public void UnsafeStream_ScheduleCreate_NativeList()
    {
        var container = new NativeList<int>(Allocator.Persistent);
        container.Add(13);
        container.Add(13);
        container.Add(13);
        container.Add(13);

        UnsafeStream stream;
        var jobHandle = UnsafeStream.ScheduleConstruct(out stream, container, default, CommonRwdAllocator.Handle);
        jobHandle.Complete();

        Assert.AreEqual(4, stream.ForEachCount);

        stream.Dispose();
        container.Dispose();
    }

    [Test]
    public void UnsafeStream_ScheduleCreate_NativeArray()
    {
        var container = new NativeArray<int>(1, Allocator.Persistent);
        container[0] = 4;

        UnsafeStream stream;
        var jobHandle = UnsafeStream.ScheduleConstruct(out stream, container, default, CommonRwdAllocator.Handle);
        jobHandle.Complete();

        Assert.AreEqual(4, stream.ForEachCount);

        stream.Dispose();
        container.Dispose();
    }
}
