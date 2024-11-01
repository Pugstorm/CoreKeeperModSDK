using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Tests
{
    public unsafe class BlockAllocatorTests
    {
        BlockAllocator Allocator;
        const int kAllocatorBlockBytes = 64 * 1024; // This value should be the same as BlockAllocator.ms_BlockSize.
        const int kBlockCount = 64;
        const int kBudgetBytes = kAllocatorBlockBytes * kBlockCount;

        bool AllSameValue(byte* ptr, long bytes, byte value)
        {
            for (long i = 0; i < bytes; ++i)
            {
                if (ptr[i] != value)
                {
                    return false;
                }
            }

            return true;
        }

        [SetUp]
        public void SetUp()
        {
            Allocator = new BlockAllocator(AllocatorManager.Persistent, kBudgetBytes);
        }

        [TearDown]
        public void TearDown()
        {
            Allocator.Dispose();
        }

        [Test]
        public void AllocateAligned([Values(1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536)] int alignment)
        {
            byte* ptr = Allocator.Allocate(1, alignment);
            Assert.IsTrue(ptr != null);
            Assert.IsTrue(CollectionHelper.IsAligned(ptr, alignment));
            *ptr = 123;
            Assert.AreEqual(*ptr, (byte)123);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires allocator safety checks")]
        public void UnsupportedAllocationSizeThrows([Values(2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536)] int alignment)
        {
            int maxSupportedAllocationSizeWithCurrentAlignment = kAllocatorBlockBytes - (alignment - 1);
            Assert.Throws<ArgumentException>(() => Allocator.Allocate(maxSupportedAllocationSizeWithCurrentAlignment + 1, alignment));
        }

        [Test]
        public void AllocTwoBlocksThenFreeAllocFirstBlockRepeatedly()
        {
            byte* ptr1 = Allocator.Allocate(kAllocatorBlockBytes, 1);
            byte* ptr2 = Allocator.Allocate(kAllocatorBlockBytes, 1);
            Assert.IsTrue(ptr1 != null);
            Assert.IsTrue(ptr2 != null);
            UnsafeUtility.MemSet(ptr1, 211, kAllocatorBlockBytes);
            UnsafeUtility.MemSet(ptr2, 222, kAllocatorBlockBytes);
            Assert.IsTrue(AllSameValue(ptr1, kAllocatorBlockBytes, 211));
            Assert.IsTrue(AllSameValue(ptr2, kAllocatorBlockBytes, 222));

            for (byte i = 0; i < 100; ++i)
            {
                Allocator.Free(ptr1);
                ptr1 = Allocator.Allocate(kAllocatorBlockBytes, 1);
                UnsafeUtility.MemSet(ptr1, i, kAllocatorBlockBytes);
                Assert.IsTrue(AllSameValue(ptr1, kAllocatorBlockBytes, i));
            }

            Assert.IsTrue(AllSameValue(ptr2, kAllocatorBlockBytes, 222));
        }

        [Test]
        public void AllocTwoBlocksThenFreeAllocSecondBlockRepeatedly()
        {
            byte* ptr1 = Allocator.Allocate(kAllocatorBlockBytes, 1);
            byte* ptr2 = Allocator.Allocate(kAllocatorBlockBytes, 1);
            Assert.IsTrue(ptr1 != null);
            Assert.IsTrue(ptr2 != null);
            UnsafeUtility.MemSet(ptr1, 211, kAllocatorBlockBytes);
            UnsafeUtility.MemSet(ptr2, 222, kAllocatorBlockBytes);
            Assert.IsTrue(AllSameValue(ptr1, kAllocatorBlockBytes, 211));
            Assert.IsTrue(AllSameValue(ptr2, kAllocatorBlockBytes, 222));

            for (byte i = 0; i < 100; ++i)
            {
                Allocator.Free(ptr2);
                ptr2 = Allocator.Allocate(kAllocatorBlockBytes, 1);
                UnsafeUtility.MemSet(ptr2, i, kAllocatorBlockBytes);
                Assert.IsTrue(AllSameValue(ptr2, kAllocatorBlockBytes, i));
            }

            Assert.IsTrue(AllSameValue(ptr1, kAllocatorBlockBytes, 211));
        }

        [Test]
        public void FreeNull()
        {
            Allocator.Free(null);
        }
    }
}
