using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.Tests;
using Unity.Jobs.LowLevel.Unsafe;

#if !UNITY_DOTSRUNTIME && ENABLE_UNITY_COLLECTIONS_CHECKS
internal class RewindableAllocatorTests
{
    AllocatorHelper<RewindableAllocator> m_AllocatorHelper;
    protected ref RewindableAllocator RwdAllocator => ref m_AllocatorHelper.Allocator;

    [SetUp]
    public void Setup()
    {
        m_AllocatorHelper = new AllocatorHelper<RewindableAllocator>(Allocator.Persistent);
        m_AllocatorHelper.Allocator.Initialize(128 * 1024, true);
    }

    [TearDown]
    public void TearDown()
    {
        m_AllocatorHelper.Allocator.Dispose();
        m_AllocatorHelper.Dispose();
    }

    [Test]
    public unsafe void RewindTestVersionOverflow()
    {
        // Check allocator version overflow
        for (int i = 0; i < 65536 + 100; i++)
        {
            var container = RwdAllocator.AllocateNativeList<byte>(RwdAllocator.InitialSizeInBytes / 1000);
            container.Resize(1, NativeArrayOptions.ClearMemory);
            container[0] = 0xFE;
            RwdAllocator.Rewind();
            CollectionHelper.CheckAllocator(RwdAllocator.ToAllocator);
        }
    }

#if UNITY_2022_3_OR_NEWER
    [Test]
    public unsafe void NativeArrayCustomAllocatorExceptionWorks()
    {
        NativeArray<int> array = default;
        Assert.Throws<ArgumentException>(() =>
        {
            array = new NativeArray<int>(2, RwdAllocator.ToAllocator);
        });
    }
#endif

    [TestRequiresCollectionChecks]
    public unsafe void RewindInvalidatesNativeList()
    {
        var container = RwdAllocator.AllocateNativeList<byte>(RwdAllocator.InitialSizeInBytes / 1000);
        container.Resize(1, NativeArrayOptions.ClearMemory);
        container[0] = 0xFE;
        RwdAllocator.Rewind();
        Assert.Throws<ObjectDisposedException>(() =>
        {
            container[0] = 0xEF;
        });
    }

    [Test]
    [TestRequiresCollectionChecks]
    public unsafe void RewindInvalidatesNativeArray()
    {
        var container = RwdAllocator.AllocateNativeArray<byte>(RwdAllocator.InitialSizeInBytes / 1000);
        container[0] = 0xFE;
        RwdAllocator.Rewind();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            container[0] = 0xEF;
        });
    }

    [Test]
    public unsafe void NativeListCanBeCreatedViaMemberFunction()
    {
        var container = RwdAllocator.AllocateNativeList<byte>(RwdAllocator.InitialSizeInBytes / 1000);
        container.Resize(1, NativeArrayOptions.ClearMemory);
        container[0] = 0xFE;
    }

    [Test]
    public unsafe void NativeListCanBeDisposed()
    {
        var container = RwdAllocator.AllocateNativeList<byte>(RwdAllocator.InitialSizeInBytes / 1000);
        container.Resize(1, NativeArrayOptions.ClearMemory);
        container[0] = 0xFE;
        container.Dispose();
        RwdAllocator.Rewind();
    }

    [Test]
    public void NativeArrayCanBeDisposed()
    {
        var container = RwdAllocator.AllocateNativeArray<byte>(RwdAllocator.InitialSizeInBytes / 1000);
        container[0] = 0xFE;
        container.Dispose();
        RwdAllocator.Rewind();
    }

    [Test]
    public void NumberOfBlocksIsTemporarilyStable()
    {
        RwdAllocator.AllocateNativeList<byte>(RwdAllocator.InitialSizeInBytes * 10);
        var blocksBefore = RwdAllocator.BlocksAllocated;
        RwdAllocator.Rewind();
        var blocksAfter = RwdAllocator.BlocksAllocated;
        Assert.AreEqual(blocksAfter, blocksBefore);
    }

    [Test]
    public void NumberOfBlocksEventuallyDrops()
    {
        RwdAllocator.AllocateNativeList<byte>(RwdAllocator.InitialSizeInBytes * 10);
        var blocksBefore = RwdAllocator.BlocksAllocated;
        RwdAllocator.Rewind();
        RwdAllocator.Rewind();
        var blocksAfter = RwdAllocator.BlocksAllocated;
        Assert.IsTrue(blocksAfter < blocksBefore);
    }

    [Test]
    public void PossibleToAllocateGigabytes()
    {
        const int giga = 1024 * 1024 * 1024;
        var container0 = RwdAllocator.AllocateNativeList<byte>(giga);
        var container1 = RwdAllocator.AllocateNativeList<byte>(giga);
        var container2 = RwdAllocator.AllocateNativeList<byte>(giga);
        container0.Resize(1, NativeArrayOptions.ClearMemory);
        container1.Resize(1, NativeArrayOptions.ClearMemory);
        container2.Resize(1, NativeArrayOptions.ClearMemory);
        container0[0] = 0;
        container1[0] = 1;
        container2[0] = 2;
        Assert.AreEqual((byte)0, container0[0]);
        Assert.AreEqual((byte)1, container1[0]);
        Assert.AreEqual((byte)2, container2[0]);
    }

    [Test]
    public void ExhaustsFirstBlockBeforeAllocatingMore()
    {
        for (var i = 0; i < 50; ++i)
        {
            RwdAllocator.AllocateNativeList<byte>(RwdAllocator.InitialSizeInBytes / 100);
            Assert.AreEqual(1, RwdAllocator.BlocksAllocated);
        }
        RwdAllocator.AllocateNativeList<byte>(RwdAllocator.InitialSizeInBytes);
        Assert.AreEqual(2, RwdAllocator.BlocksAllocated);
    }

    unsafe struct ListProvider
    {
        NativeList<byte> m_Bytes;

        public ListProvider(AllocatorManager.AllocatorHandle allocatorHandle) => m_Bytes = new NativeList<byte>(allocatorHandle);

        public void Append<T>(ref T data) where T : unmanaged =>
            m_Bytes.AddRange(UnsafeUtility.AddressOf(ref data), UnsafeUtility.SizeOf<T>());
    }

    static void TriggerBug(AllocatorManager.AllocatorHandle allocatorHandle, NativeArray<byte> data)
    {
        var listProvider = new ListProvider(allocatorHandle);

        var datum = 0u;
        listProvider.Append(ref datum); // 'data' is now invalid after call to AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);

        Assert.That(data[0], Is.EqualTo(0));
    }

    [Test]
    public void AddRange_WhenCalledOnStructMember_DoesNotInvalidateUnrelatedListHigherOnCallStack()
    {
        AllocatorManager.AllocatorHandle allocatorHandle = RwdAllocator.Handle;

        var unrelatedList = new NativeList<byte>(allocatorHandle) { 0, 0 };
        Assert.That(unrelatedList.Length, Is.EqualTo(2));
        Assert.That(unrelatedList[0], Is.EqualTo(0));

        TriggerBug(allocatorHandle, unrelatedList.AsArray());
    }

    [Test]
    unsafe public void ExceedMaxBlockSize_BlockSizeLinearGrow()
    {
        AllocatorManager.AllocatorHandle allocatorHandle = RwdAllocator.Handle;

        var allocationSizes = new NativeList<int>(Allocator.Persistent);
        allocationSizes.Add(1);
        allocationSizes.Add((int)RwdAllocator.MaxMemoryBlockSize + 256);
        allocationSizes.Add(1);
        allocationSizes.Add(RwdAllocator.InitialSizeInBytes);

        int mask = JobsUtility.CacheLineSize - 1;

        var expectedBlockSizes = new NativeList<int>(Allocator.Persistent);
        expectedBlockSizes.Add(RwdAllocator.InitialSizeInBytes);
        expectedBlockSizes.Add(RwdAllocator.InitialSizeInBytes + (((int)RwdAllocator.MaxMemoryBlockSize + 256 + mask) & ~mask));
        expectedBlockSizes.Add(RwdAllocator.InitialSizeInBytes + (((int)RwdAllocator.MaxMemoryBlockSize + 256 + mask) & ~mask));
        var expected = RwdAllocator.InitialSizeInBytes + (((int)RwdAllocator.MaxMemoryBlockSize + 256 + mask) & ~mask) +
                            (((int)RwdAllocator.MaxMemoryBlockSize + 256 + mask) & ~mask) +(int)RwdAllocator.MaxMemoryBlockSize;
        expectedBlockSizes.Add(expected);

        for(int i = 0; i < allocationSizes.Length; i++)
        {
            AllocatorManager.Allocate(allocatorHandle, sizeof(byte), sizeof(byte), allocationSizes[i]);
            int bytesUsed = (int)RwdAllocator.BytesAllocated;
            Assert.AreEqual(bytesUsed, expectedBlockSizes[i]);
        }

        allocationSizes.Dispose();
        expectedBlockSizes.Dispose();
    }
}

#endif
