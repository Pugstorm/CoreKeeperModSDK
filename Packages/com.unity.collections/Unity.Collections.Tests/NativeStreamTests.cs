using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;
using Unity.Jobs;
using UnityEngine;
using Assert = FastAssert;

internal class NativeStreamTests : CollectionsTestFixture
{
    [BurstCompile(CompileSynchronously = true)]
    struct WriteInts : IJobParallelFor
    {
        public NativeStream.Writer Writer;

        public void Execute(int index)
        {
            Writer.BeginForEachIndex(index);
            for (int i = 0; i != index; i++)
                Writer.Write(i);
            Writer.EndForEachIndex();
        }
    }

    struct ReadInts : IJobParallelFor
    {
        public NativeStream.Reader Reader;

        public void Execute(int index)
        {
            int count = Reader.BeginForEachIndex(index);
            Assert.AreEqual(count, index);

            for (int i = 0; i != index; i++)
            {
                Assert.AreEqual(index - i, Reader.RemainingItemCount);
                var peekedValue = Reader.Peek<int>();
                var value = Reader.Read<int>();
                Assert.AreEqual(i, value);
                Assert.AreEqual(i, peekedValue);
            }

            Reader.EndForEachIndex();
        }
    }

    [Test]
    public void NativeStream_PopulateInts([Values(1, 100, 200)] int count, [Values(1, 3, 10)] int batchSize)
    {
        var stream = new NativeStream(count, CommonRwdAllocator.Handle);
        var fillInts = new WriteInts {Writer = stream.AsWriter()};
        var jobHandle = fillInts.Schedule(count, batchSize);

        var compareInts = new ReadInts {Reader = stream.AsReader()};
        var res0 = compareInts.Schedule(count, batchSize, jobHandle);
        var res1 = compareInts.Schedule(count, batchSize, jobHandle);

        res0.Complete();
        res1.Complete();

        stream.Dispose();
    }

    static void ExpectedCount(ref NativeStream container, int expected)
    {
        Assert.AreEqual(expected == 0, container.IsEmpty());
        Assert.AreEqual(expected, container.Count());
    }

    [Test]
    public void NativeStream_CreateAndDestroy([Values(1, 100, 200)] int count)
    {
        var stream = new NativeStream(count, Allocator.Temp);

        Assert.IsTrue(stream.IsCreated);
        Assert.IsTrue(stream.ForEachCount == count);
        ExpectedCount(ref stream, 0);

        stream.Dispose();
        Assert.IsFalse(stream.IsCreated);
    }

    [Test]
    public void NativeStream_ItemCount([Values(1, 100, 200)] int count, [Values(1, 3, 10)] int batchSize)
    {
        var stream = new NativeStream(count, CommonRwdAllocator.Handle);
        var fillInts = new WriteInts {Writer = stream.AsWriter()};
        fillInts.Schedule(count, batchSize).Complete();

        ExpectedCount(ref stream, count * (count - 1) / 2);

        stream.Dispose();
    }

    [Test]
    public void NativeStream_ToArray([Values(1, 100, 200)] int count, [Values(1, 3, 10)] int batchSize)
    {
        var stream = new NativeStream(count, CommonRwdAllocator.Handle);
        var fillInts = new WriteInts {Writer = stream.AsWriter()};
        fillInts.Schedule(count, batchSize).Complete();
        ExpectedCount(ref stream, count * (count - 1) / 2);

        var array = stream.ToNativeArray<int>(Allocator.Temp);
        int itemIndex = 0;

        for (int i = 0; i != count; ++i)
        {
            for (int j = 0; j < i; ++j)
            {
                Assert.AreEqual(j, array[itemIndex]);
                itemIndex++;
            }
        }

        array.Dispose();
        stream.Dispose();
    }

    [Test]
    public void NativeStream_DisposeJob()
    {
        var stream = new NativeStream(100, CommonRwdAllocator.Handle);
        Assert.IsTrue(stream.IsCreated);

        var fillInts = new WriteInts {Writer = stream.AsWriter()};
        var writerJob = fillInts.Schedule(100, 16);

        var disposeJob = stream.Dispose(writerJob);
        Assert.IsFalse(stream.IsCreated);

        disposeJob.Complete();
    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    [Test]
    public void NativeStream_ParallelWriteThrows()
    {
        var stream = new NativeStream(100, CommonRwdAllocator.Handle);
        var fillInts = new WriteInts {Writer = stream.AsWriter()};

        var writerJob = fillInts.Schedule(100, 16);
        Assert.Throws<InvalidOperationException>(() => fillInts.Schedule(100, 16));

        writerJob.Complete();
        stream.Dispose();
    }

    [Test]
    public void NativeStream_ScheduleCreateThrows_NativeList()
    {
        var container = new NativeList<int>(Allocator.Persistent);
        container.Add(2);

        NativeStream stream;
        var jobHandle = NativeStream.ScheduleConstruct(out stream, container, default, CommonRwdAllocator.Handle);

        Assert.Throws<InvalidOperationException>(() => { int val = stream.ForEachCount; });

        jobHandle.Complete();

        Assert.AreEqual(1, stream.ForEachCount);

        stream.Dispose();
        container.Dispose();
    }

    [Test]
    public void NativeStream_ScheduleCreateThrows_NativeArray()
    {
        var container = new NativeArray<int>(1, Allocator.Persistent);
        container[0] = 1;

        NativeStream stream;
        var jobHandle = NativeStream.ScheduleConstruct(out stream, container, default, CommonRwdAllocator.Handle);

        Assert.Throws<InvalidOperationException>(() => { int val = stream.ForEachCount; });

        jobHandle.Complete();

        Assert.AreEqual(1, stream.ForEachCount);

        stream.Dispose();
        container.Dispose();
    }

    [Test]
    public void NativeStream_OutOfBoundsWriteThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();
        Assert.Throws<ArgumentException>(() => writer.BeginForEachIndex(-1));
        Assert.Throws<ArgumentException>(() => writer.BeginForEachIndex(2));

        stream.Dispose();
    }

    [Test]
    public void NativeStream_EndForEachIndexWithoutBeginThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();
        Assert.Throws<ArgumentException>(() => writer.EndForEachIndex());

        stream.Dispose();
    }

    [Test]
    public void NativeStream_WriteWithoutBeginThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();
        Assert.Throws<ArgumentException>(() => writer.Write(5));

        stream.Dispose();
    }

    [Test]
    public void NativeStream_WriteAfterEndThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();
        writer.BeginForEachIndex(0);
        writer.Write(2);
        Assert.AreEqual(1, writer.ForEachCount);
        writer.EndForEachIndex();

        Assert.AreEqual(1, writer.ForEachCount);
        Assert.Throws<ArgumentException>(() => writer.Write(5));

        stream.Dispose();
    }

    [Test]
    public void NativeStream_UnbalancedBeginThrows()
    {
        var stream = new NativeStream(2, Allocator.Temp);
        var writer = stream.AsWriter();
        writer.BeginForEachIndex(0);
        // Missing EndForEachIndex();
        Assert.Throws<ArgumentException>(() => writer.BeginForEachIndex(1));

        stream.Dispose();
    }

    static void CreateBlockStream1And2Int(out NativeStream stream)
    {
        stream = new NativeStream(2, Allocator.Temp);

        var writer = stream.AsWriter();
        writer.BeginForEachIndex(0);
        writer.Write(0);
        writer.EndForEachIndex();

        writer.BeginForEachIndex(1);
        writer.Write(1);
        writer.Write(2);
        writer.EndForEachIndex();
    }

    [Test]
    public void NativeStream_IncompleteReadThrows()
    {
        NativeStream stream;
        CreateBlockStream1And2Int(out stream);

        var reader = stream.AsReader();

        reader.BeginForEachIndex(0);
        reader.Read<byte>();
        Assert.Throws<ArgumentException>(() => reader.EndForEachIndex());

        reader.BeginForEachIndex(1);

        stream.Dispose();
    }

    [Test]
    public void NativeStream_ReadWithoutBeginThrows()
    {
        NativeStream stream;
        CreateBlockStream1And2Int(out stream);

        var reader = stream.AsReader();
        Assert.Throws<ArgumentException>(() => reader.Read<int>());

        stream.Dispose();
    }

    [Test]
    public void NativeStream_TooManyReadsThrows()
    {
        NativeStream stream;
        CreateBlockStream1And2Int(out stream);

        var reader = stream.AsReader();

        reader.BeginForEachIndex(0);
        reader.Read<byte>();
        Assert.Throws<ArgumentException>(() => reader.Read<byte>());

        stream.Dispose();
    }

    [Test]
    public void NativeStream_OutOfBoundsReadThrows()
    {
        NativeStream stream;
        CreateBlockStream1And2Int(out stream);

        var reader = stream.AsReader();

        reader.BeginForEachIndex(0);
        Assert.Throws<ArgumentException>(() => reader.Read<long>());

        stream.Dispose();
    }

    [Test]
    public void NativeStream_CopyWriterByValueThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();

        writer.BeginForEachIndex(0);

        Assert.Throws<ArgumentException>(() =>
        {
            var writerCopy = writer;
            writerCopy.Write(5);
        });

        Assert.Throws<ArgumentException>(() =>
        {
            var writerCopy = writer;
            writerCopy.BeginForEachIndex(1);
            writerCopy.Write(5);
        });

        stream.Dispose();
    }

    [Test]
    public void NativeStream_WriteSameIndexTwiceThrows()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();

        writer.BeginForEachIndex(0);
        writer.Write(1);
        writer.EndForEachIndex();

        Assert.Throws<ArgumentException>(() =>
        {
            writer.BeginForEachIndex(0);
            writer.Write(2);
        });

        stream.Dispose();
    }

    static void WriteNotPassedByRef(NativeStream.Writer notPassedByRef)
    {
        notPassedByRef.Write(10);
    }

    static void WritePassedByRef(ref NativeStream.Writer passedByRef)
    {
        passedByRef.Write(10);
    }

    [Test]
    public void NativeStream_ThrowsOnIncorrectUsage()
    {
        var stream = new NativeStream(1, Allocator.Temp);
        var writer = stream.AsWriter();

        Assert.Throws<ArgumentException>(() => stream.AsWriter().Write(10));

        writer.BeginForEachIndex(0);
        Assert.Throws<ArgumentException>(() => WriteNotPassedByRef(writer));
        Assert.DoesNotThrow(() => WritePassedByRef(ref writer));
        writer.EndForEachIndex();

        stream.Dispose();
    }

#endif

    [Test]
    public void NativeStream_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new NativeStream(1, allocator.Handle))
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
                using (var container = new NativeStream(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void NativeStream_BurstedCustomAllocatorTest()
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
        public NativeList<int> data;
    }

    [Test]
    public void NativeStream_Nested()
    {
        var inner = new NativeList<int>(CommonRwdAllocator.Handle);
        NestedContainer nestedStruct = new NestedContainer { data = inner };

        var containerNestedStruct = new NativeStream(100, CommonRwdAllocator.Handle);
        var containerNested = new NativeStream(100, CommonRwdAllocator.Handle);

        var writer = containerNested.AsWriter();
        writer.BeginForEachIndex(0);
        writer.Write(inner);
        writer.EndForEachIndex();
        var writerStruct = containerNestedStruct.AsWriter();
        writerStruct.BeginForEachIndex(0);
        writerStruct.Write(nestedStruct);
        writerStruct.EndForEachIndex();

        containerNested.Dispose();
        containerNestedStruct.Dispose();
        inner.Dispose();
    }

    [Test]
    public unsafe void NativeStream_Continue_Append()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        for (var i = 0; i < 1024; i++)
        {
            var stream = new NativeStream(2, allocator.Handle);

            var writer = stream.AsWriter();
            writer.BeginForEachIndex(0);
            writer.Allocate(4000);
            writer.EndForEachIndex();

            var writer2 = stream.AsWriter();
            writer2.BeginForEachIndex(1);
            writer2.Allocate(4000);
            writer2.Allocate(4000);
            writer2.EndForEachIndex();

            stream.Dispose();

            Assert.AreEqual(0, allocatorHelper.Allocator.Used);
        }

        allocator.Dispose();
        allocatorHelper.Dispose();
        AllocatorManager.Shutdown();
    }

// DOTS-6203 Nested containers aren't detected in DOTS Runtime currently
#if !UNITY_DOTSRUNTIME
    struct NestedContainerJob : IJob
    {
        public NativeStream nestedContainer;

        public void Execute()
        {
            var writer = nestedContainer.AsWriter();
            writer.BeginForEachIndex(0);
            writer.Write(1);
            writer.EndForEachIndex();
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeStream_NestedJob_Error()
    {
        var inner = new NativeList<int>(CommonRwdAllocator.Handle);
        var container = new NativeStream(100, CommonRwdAllocator.Handle);

        // This should mark the NativeStream as having nested containers and therefore should not be able to be scheduled
        var writer = container.AsWriter();
        writer.BeginForEachIndex(0);
        writer.Write(inner);
        writer.EndForEachIndex();

        var nestedJob = new NestedContainerJob
        {
            nestedContainer = container
        };

        JobHandle job = default;
        Assert.Throws<System.InvalidOperationException>(() => { job = nestedJob.Schedule(); });
        job.Complete();

        container.Dispose();
    }
#endif
}
