using NUnit.Framework;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;
using Unity.Jobs;

class NativeReferenceTests : CollectionsTestCommonBase
{
    [Test]
    public void NativeReference_AllocateDeallocate_ReadWrite()
    {
        var reference = new NativeReference<int>(Allocator.Persistent);
        reference.Value = 1;

        Assert.That(reference.Value, Is.EqualTo(1));

        reference.Dispose();
    }

    [Test]
    public void NativeReference_CopyFrom()
    {
        var referenceA = new NativeReference<TestData>(Allocator.Persistent);
        var referenceB = new NativeReference<TestData>(Allocator.Persistent);

        referenceA.Value = new TestData { Integer = 42, Float = 3.1416f };
        referenceB.CopyFrom(referenceA);

        Assert.That(referenceB.Value, Is.EqualTo(referenceA.Value));

        referenceA.Dispose();
        referenceB.Dispose();
    }

    [Test]
    public void NativeReference_CopyTo()
    {
        var referenceA = new NativeReference<TestData>(Allocator.Persistent);
        var referenceB = new NativeReference<TestData>(Allocator.Persistent);

        referenceA.Value = new TestData { Integer = 42, Float = 3.1416f };
        referenceA.CopyTo(referenceB);

        Assert.That(referenceB.Value, Is.EqualTo(referenceA.Value));

        referenceA.Dispose();
        referenceB.Dispose();
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeReference_NullThrows()
    {
        var reference = new NativeReference<int>();
        Assert.Throws<NullReferenceException>(() => reference.Value = 5);
    }

    [Test]
    public void NativeReference_CopiedIsKeptInSync()
    {
        var reference = new NativeReference<int>(Allocator.Persistent);
        var referenceCopy = reference;
        reference.Value = 42;

        Assert.That(reference.Value, Is.EqualTo(referenceCopy.Value));

        reference.Dispose();
    }

    struct TestData
    {
        public int Integer;
        public float Float;
    }

    [BurstCompile(CompileSynchronously = true)]
    struct TempNativeReferenceInJob : IJob
    {
        public NativeReference<int> Output;

        public void Execute()
        {
            var reference = new NativeReference<int>(Allocator.Temp);
            reference.Value = 42;
            Output.Value = reference.Value;
            reference.Dispose();
        }
    }

    [Test]
    public void NativeReference_TempInBurstJob()
    {
        var job = new TempNativeReferenceInJob() { Output = new NativeReference<int>(CommonRwdAllocator.Handle) };
        job.Schedule().Complete();

        Assert.That(job.Output.Value, Is.EqualTo(42));

        job.Output.Dispose();
    }

    [Test]
    public unsafe void NativeReference_UnsafePtr()
    {
        var reference = new NativeReference<int>(CommonRwdAllocator.Handle);
        var job = new TempNativeReferenceInJob() { Output = reference };
        var jobHandle = job.Schedule();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        Assert.Throws<InvalidOperationException>(() => reference.GetUnsafePtr());
        Assert.Throws<InvalidOperationException>(() => reference.GetUnsafeReadOnlyPtr());
#endif
        Assert.DoesNotThrow(() => reference.GetUnsafePtrWithoutChecks());

        jobHandle.Complete();

        Assert.AreEqual(*reference.GetUnsafePtr(), 42);
        Assert.AreEqual(*reference.GetUnsafeReadOnlyPtr(), 42);
        Assert.AreEqual(*reference.GetUnsafePtrWithoutChecks(), 42);

        Assert.That(job.Output.Value, Is.EqualTo(42));

        job.Output.Dispose();
    }

    [Test]
    public void NativeReference_DisposeJob()
    {
        var reference = new NativeReference<int>(Allocator.Persistent);
        Assert.That(reference.IsCreated, Is.True);
        Assert.DoesNotThrow(() => reference.Value = 99);

        var disposeJob = reference.Dispose(default);
        Assert.That(reference.IsCreated, Is.False);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        Assert.Throws<ObjectDisposedException>(() => reference.Value = 3);
#endif

        disposeJob.Complete();
    }

    [Test]
    public void NativeReference_NoGCAllocations()
    {
        var reference = new NativeReference<int>(Allocator.Persistent);

        GCAllocRecorder.ValidateNoGCAllocs(() =>
        {
            reference.Value = 1;
            reference.Value++;
        });

        Assert.That(reference.Value, Is.EqualTo(2));

        reference.Dispose();
    }

    [Test]
    public void NativeReference_Equals()
    {
        var referenceA = new NativeReference<int>(12345, Allocator.Persistent);
        var referenceB = new NativeReference<int>(Allocator.Persistent) { Value = 12345 };
        Assert.That(referenceA, Is.EqualTo(referenceB));

        referenceB.Value = 54321;
        Assert.AreNotEqual(referenceA, referenceB);

        referenceA.Dispose();
        referenceB.Dispose();
    }

    [Test]
    public void NativeReference_ReadOnly()
    {
        var referenceA = new NativeReference<int>(12345, Allocator.Persistent);
        var referenceB = new NativeReference<int>(Allocator.Persistent) { Value = 12345 };

        var referenceARO = referenceA.AsReadOnly();
        Assert.AreEqual(referenceARO.Value, referenceB.Value);

        referenceA.Dispose();
        referenceB.Dispose();
    }

    [Test]
    public void NativeReference_GetHashCode()
    {
        var integer = 42;
        var reference = new NativeReference<int>(integer, Allocator.Persistent);
        Assert.That(reference.GetHashCode(), Is.EqualTo(integer.GetHashCode()));

        reference.Dispose();
    }

    [Test]
    public void NativeReference_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new NativeReference<int>(allocator.Handle))
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
                using (var container = new NativeReference<int>(Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void NativeReference_BurstedCustomAllocatorTest()
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
        public NativeReference<int> data;
    }

    [Test]
    public void NativeReference_Nested()
    {
        var inner = new NativeReference<int>(CommonRwdAllocator.Handle);
        NestedContainer nestedStruct = new NestedContainer { data = inner };

        var containerNestedStruct = new NativeReference<NestedContainer>(CommonRwdAllocator.Handle);
        var containerNested = new NativeReference<NativeReference<int>>(CommonRwdAllocator.Handle);

        containerNested.Value = inner;
        containerNestedStruct.Value = nestedStruct;

        containerNested.Dispose();
        containerNestedStruct.Dispose();
        inner.Dispose();
    }

    struct NestedContainerJob : IJob
    {
        public NativeReference<NativeReference<int>> nestedContainer;

        public void Execute()
        {
            nestedContainer.Value = default;
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeReference_NestedJob_Error()
    {
        var container = new NativeReference<NativeReference<int>>(CommonRwdAllocator.Handle);

        var nestedJob = new NestedContainerJob
        {
            nestedContainer = container
        };

        JobHandle job = default;
        Assert.Throws<System.InvalidOperationException>(() => { job = nestedJob.Schedule(); });
        job.Complete();

        container.Dispose();
    }
}
