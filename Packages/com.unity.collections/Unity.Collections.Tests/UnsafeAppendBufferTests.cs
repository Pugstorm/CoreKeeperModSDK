using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;
using Unity.Jobs;

internal class UnsafeAppendBufferTests : CollectionsTestCommonBase
{
    struct TestHeader
    {
        public int Type;
        public int PayloadSize;
    }

    [Test]
    public void UnsafeAppendBuffer_DisposeAllocated()
    {
        var buffer = new UnsafeAppendBuffer(1, 8, Allocator.Temp);
        Assert.True(buffer.IsCreated);
        Assert.True(buffer.IsEmpty);
        buffer.Dispose();
    }

    [Test]
    unsafe public void UnsafeAppendBuffer_DisposeExternal()
    {
        var data = stackalloc int[1];
        var buffer = new UnsafeAppendBuffer(data, sizeof(int));
        buffer.Add(5);
        buffer.Dispose();
        Assert.AreEqual(5, data[0]);
    }

    [Test]
    [TestRequiresDotsDebugOrCollectionChecks]
    public void UnsafeAppendBuffer_ThrowZeroAlignment()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var buffer = new UnsafeAppendBuffer(0, 0, Allocator.Temp);
        });
    }

    void AddAndVerify<T>(ref UnsafeAppendBuffer buffer, T value, ref int expectedLength) where T : unmanaged
    {
        buffer.Add(value);
        expectedLength += UnsafeUtility.SizeOf<T>();
        Assert.AreEqual(expectedLength, buffer.Length);
    }

    [Test]
    public unsafe void UnsafeAppendBuffer_AddAndPop_UnalignedRead()
    {
        int expectedLength = 0;
        byte value = 0;
        var buffer = new UnsafeAppendBuffer(0, 8, Allocator.Temp);
        Assert.IsTrue(buffer.IsEmpty);
        Assert.AreEqual(expectedLength, buffer.Length);

        AddAndVerify<int>(ref buffer, value++, ref expectedLength);
        AddAndVerify<byte>(ref buffer, value++, ref expectedLength);
        AddAndVerify<int>(ref buffer, value++, ref expectedLength);
        Assert.AreEqual(9, buffer.Length);
        AddAndVerify<byte>(ref buffer, value++, ref expectedLength);
        AddAndVerify<int>(ref buffer, value++, ref expectedLength);
        Assert.AreEqual(14, buffer.Length);

        {
            var array = new NativeArray<int>(3, Allocator.Temp);
            for (int i = 0; i < array.Length; ++i)
                array[i] = value++;

            int oldLen = buffer.Length;
            buffer.AddArray<int>(array.GetUnsafePtr(), 3);

            Assert.AreEqual(30, buffer.Length);
        }

        {
            var array = new NativeArray<int>(3, Allocator.Temp);
            for (int i = 0; i < array.Length; ++i)
                array[i] = value++;

            int oldLen = buffer.Length;
            buffer.AddArray<int>(array.GetUnsafePtr(), 3);

            Assert.AreEqual(46, buffer.Length);
        }

        // popping

        // array elements + count
        for (int i = 0; i < 3; ++i)
        {
            Assert.AreEqual(--value, buffer.Pop<int>());
        }
        Assert.AreEqual(3, buffer.Pop<int>());

        // array elements + count
        for (int i = 0; i < 3; ++i)
        {
            Assert.AreEqual(--value, buffer.Pop<int>());
        }
        Assert.AreEqual(3, buffer.Pop<int>());

        Assert.AreEqual(--value, buffer.Pop<int>());
        Assert.AreEqual(--value, buffer.Pop<byte>());
        Assert.AreEqual(--value, buffer.Pop<int>());
        Assert.AreEqual(--value, buffer.Pop<byte>());
        Assert.AreEqual(--value, buffer.Pop<int>());

        Assert.IsTrue(buffer.IsEmpty);
        Assert.AreEqual(0, buffer.Length);
    }

    [Test]
    public unsafe void UnsafeAppendBuffer_AddAndRead_UnalignedRead()
    {
        var buffer = new UnsafeAppendBuffer(0, 8, Allocator.Temp);
        buffer.Add<byte>(1);
        buffer.Add<float>(1.0f);
        var reader = buffer.AsReader();
        Assert.AreEqual(reader.ReadNext<byte>(), 1);
        Assert.AreEqual(reader.ReadNext<float>(), 1.0f);
        buffer.Dispose();
    }

    [Test]
    public unsafe void UnsafeAppendBuffer_PushHeadersWithPackets()
    {
        var buffer = new UnsafeAppendBuffer(0, 8, Allocator.Temp);
        var scratchPayload = stackalloc byte[1024];
        var expectedSize = 0;
        for (int i = 0; i < 1024; i++)
        {
            var packeType = i;
            var packetSize = i;

            buffer.Add(new TestHeader
            {
                Type = packeType,
                PayloadSize = packetSize
            });
            expectedSize += UnsafeUtility.SizeOf<TestHeader>();

            buffer.Add(scratchPayload, i);
            expectedSize += i;
        }
        Assert.True(expectedSize == buffer.Length);

        buffer.Dispose();
    }

    [Test]
    public unsafe void UnsafeAppendBuffer_ReadHeadersWithPackets()
    {
        var buffer = new UnsafeAppendBuffer(0, 8, Allocator.Temp);
        var scratchPayload = stackalloc byte[1024];
        for (int i = 0; i < 1024; i++)
        {
            var packeType = i;
            var packetSize = i;

            buffer.Add(new TestHeader
            {
                Type = packeType,
                PayloadSize = packetSize
            });

            UnsafeUtility.MemSet(scratchPayload, (byte)(i & 0xff), packetSize);

            buffer.Add(scratchPayload, i);
        }

        var reader = buffer.AsReader();
        for (int i = 0; i < 1024; i++)
        {
            var packetHeader = reader.ReadNext<TestHeader>();
            Assert.AreEqual(i, packetHeader.Type);
            Assert.AreEqual(i, packetHeader.PayloadSize);
            if (packetHeader.PayloadSize > 0)
            {
                var packetPayload = reader.ReadNext(packetHeader.PayloadSize);
                Assert.AreEqual((byte)(i & 0xff), *(byte*)packetPayload);
            }
        }
        Assert.True(reader.EndOfBuffer);

        buffer.Dispose();
    }

    [Test]
    public unsafe void UnsafeAppendBuffer_AddAndPop()
    {
        var buffer = new UnsafeAppendBuffer(0, 8, Allocator.Temp);

        buffer.Add<int>(123);
        buffer.Add<int>(234);
        buffer.Add<int>(345);

        {
            var array = new NativeArray<int>(3, Allocator.Temp);
            buffer.Pop(array.GetUnsafePtr(), 3 * UnsafeUtility.SizeOf<int>());
            CollectionAssert.AreEqual(new[] {123, 234, 345}, array);
        }

        {
            var array = new NativeArray<int>(4, Allocator.Temp);
            array.CopyFrom(new[] {987, 876, 765, 654});
            buffer.Add(array.GetUnsafePtr(), 4 * UnsafeUtility.SizeOf<int>());
        }

        Assert.AreEqual(654, buffer.Pop<int>());
        Assert.AreEqual(765, buffer.Pop<int>());
        Assert.AreEqual(876, buffer.Pop<int>());
        Assert.AreEqual(987, buffer.Pop<int>());

        buffer.Dispose();
    }

    [Test]
    public unsafe void UnsafeAppendBuffer_ReadNextArray()
    {
        var values = new NativeArray<int>(new[] {123, 234, 345}, Allocator.Temp);
        var buffer = new UnsafeAppendBuffer(0, 8, Allocator.Temp);
        buffer.Add(values);

        var array = (int*)buffer.AsReader().ReadNextArray<int>(out var count);

        Assert.AreEqual(values.Length, count);
        for (int i = 0; i < count; ++i)
        {
            Assert.AreEqual(values[i], array[i]);
        }

        values.Dispose();
        buffer.Dispose();
    }

    [Test]
    public unsafe void UnsafeAppendBuffer_DisposeJob()
    {
        var sizeOf = UnsafeUtility.SizeOf<int>();
        var alignOf = UnsafeUtility.AlignOf<int>();

        var container = new UnsafeAppendBuffer(5, 16, Allocator.Persistent);

        var disposeJob = container.Dispose(default);

        Assert.IsTrue(container.Ptr == null);

        disposeJob.Complete();
    }

    [Test]
    public void UnsafeAppendBuffer_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new UnsafeAppendBuffer(1, 1, allocator.Handle))
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
                using (var container = new UnsafeAppendBuffer(1, 1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void UnsafeAppendBuffer_BurstedCustomAllocatorTest()
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
