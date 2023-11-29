using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

internal class ConcurrentMaskTests
{
    internal struct Test
    {
        internal long value;
        internal int bits;
        internal long expectedModified;
        internal int offset;
        internal bool expectedWorked;
        internal Test(ulong uValue, ulong uExpectedModified, bool w)
        {
            this = default;
            value = (long)uValue;
            expectedModified = (long)uExpectedModified;
            expectedWorked = w;
        }
        internal Test(ulong uValue, int bits, ulong uExpectedModified, bool w)
        {
            this = default;
            value = (long)uValue;
            this.bits = bits;
            expectedModified = (long)uExpectedModified;
            expectedWorked = w;
        }
        internal Test(ulong uValue, ulong uExpectedModified, int offset, bool w)
        {
            this = default;
            value = (long)uValue;
            expectedModified = (long)uExpectedModified;
            this.offset = offset;
            expectedWorked = w;
        }
        internal Test(ulong uValue, int bits, ulong uExpectedModified, int offset, bool w)
        {
            value = (long)uValue;
            this.bits = bits;
            expectedModified = (long)uExpectedModified;
            this.offset = offset;
            expectedWorked = w;
        }
    }
    
    [Test]
    public void AllocatesOneBitFromLong()
    {
        foreach(var test in new Test[] {        
            new Test(0x0000000000000000UL,0x0000000000000001UL, true),
            new Test(0x0000000000000001UL,0x0000000000000003UL, true),
            new Test(0x00000000000000FFUL,0x00000000000001FFUL, true),
            new Test(0x0000000000000100UL,0x0000000000000101UL, true),
            new Test(0x7FFFFFFFFFFFFFFFUL,0xFFFFFFFFFFFFFFFFUL, true),
            new Test(0x8000000000000000UL,0x8000000000000001UL, true),
        }) {
            long value = test.value;
            long expectedModified = test.expectedModified;
            long modified = value;
            var worked = ConcurrentMask.TryAllocate(ref modified, out int _, 1);
            Assert.AreEqual(expectedModified, modified);
            Assert.AreEqual(test.expectedWorked, ConcurrentMask.Succeeded(worked));
        }
    }    

    [Test]  
    public void FailsToAllocateOneBitFromLong()
    {        
        foreach(var test in new Test[] {        
            new Test(0xFFFFFFFFFFFFFFFFUL,0xFFFFFFFFFFFFFFFFUL, false)  
        }) {
            long value = test.value;
            long expectedModified = test.expectedModified;
            long modified = value;
            var worked = ConcurrentMask.TryAllocate(ref modified, out int _, 1);
            Assert.AreEqual(expectedModified, modified);
            Assert.AreEqual(test.expectedWorked, ConcurrentMask.Succeeded(worked));
        }
    }    

    [Test]
    public void AllocatesMultipleBitsFromLong()
    {        
        foreach(var test in new Test[] {        
            new Test(0x0000000000000000UL, 64,0xFFFFFFFFFFFFFFFFUL, true),
            new Test(0x0000000000000000UL, 2,0x0000000000000003UL, true),
            new Test(0x0000000000000001UL,63,0xFFFFFFFFFFFFFFFFUL, true),
            new Test(0x00000000000000FFUL,8, 0x000000000000FFFFUL, true),
            new Test(0x00000000000FF0FFUL,8, 0x000000000FFFF0FFUL, true),
        }) {
            long value = test.value;
            long expectedModified = test.expectedModified;
            long modified = value;
            var worked = ConcurrentMask.TryAllocate(ref modified, out int _, test.bits);
            Assert.AreEqual(expectedModified, modified);
            Assert.AreEqual(test.expectedWorked, ConcurrentMask.Succeeded(worked));
        }
    }    

    [Test]
    public void FailsToAllocateMultipleBitsFromLong()
    {     
        foreach(var test in new Test[] {        
            new Test(0x0000000000000000UL, 65,0x0000000000000000UL, false),
            new Test(0x0000000000000001UL,64,0x0000000000000001UL, false),
            new Test(0xFF000000000000FFUL,49, 0xFF000000000000FFUL, false),
        }) {       
            long value = test.value;
            long expectedModified = test.expectedModified;
            long modified = value;
            var worked = ConcurrentMask.TryAllocate(ref modified, out int _, test.bits);
            Assert.AreEqual(expectedModified, modified);
            Assert.AreEqual(test.expectedWorked, ConcurrentMask.Succeeded(worked));
        }
    }    

    [Test]
    public void FreesOneBitFromLong()
    {     
        foreach(var test in new Test[] {        
            new Test(0x0000000000000000UL,0x0000000000000001UL, 0, true),
            new Test(0x0000000000000001UL,0x0000000000000003UL, 1, true),
            new Test(0x00000000000000FFUL,0x00000000000001FFUL, 8, true),
            new Test(0x0000000000000100UL,0x0000000000000101UL, 0, true),
            new Test(0x7FFFFFFFFFFFFFFFUL,0xFFFFFFFFFFFFFFFFUL, 63, true),
            new Test(0x8000000000000000UL,0x8000000000000001UL, 0, true),
        }) {
            long expectedModified = (long)test.value;
            long modified = (long)test.expectedModified;
            var worked = ConcurrentMask.TryFree(ref modified, test.offset, 1);
            Assert.AreEqual(expectedModified, modified);
            Assert.AreEqual(test.expectedWorked, ConcurrentMask.Succeeded(worked));
        }
    }    

    [Test]
    public void FreesMultipleBitsFromLong()
    {        
        foreach(var test in new Test[] {        
            new Test(0x0000000000000000UL, 64,0xFFFFFFFFFFFFFFFFUL, 0, true),
            new Test(0x0000000000000000UL, 2,0x0000000000000003UL, 0, true),
            new Test(0x0000000000000001UL,63,0xFFFFFFFFFFFFFFFFUL, 1, true),
            new Test(0x00000000000000FFUL,8, 0x000000000000FFFFUL, 8, true),
            new Test(0x00000000000FF0FFUL,8, 0x000000000FFFF0FFUL, 20, true),
        }) {
            long expectedModified = test.value;
            long modified = test.expectedModified;
            var worked = ConcurrentMask.TryFree(ref modified, test.offset, test.bits);
            Assert.AreEqual(expectedModified, modified);
            Assert.AreEqual(test.expectedWorked, ConcurrentMask.Succeeded(worked));
        }
    }    

    [Test]
    public void AllocatesOneBitFromArray()
    {        
        var storage = new NativeList<long>(3, Allocator.Persistent);
        storage.Length = 3;
        for(var i = 0; i < 64; ++i)
        {
            var worked = ConcurrentMask.TryAllocate(ref storage, out int offset, 1);
            Assert.AreEqual(true, ConcurrentMask.Succeeded(worked));
            Assert.AreEqual(i, offset);
        }
        Assert.AreEqual(-1L, storage[0]);
        Assert.AreEqual(0L, storage[1]);
        Assert.AreEqual(0L, storage[2]);
        for(var i = 0; i < 64; ++i)
        {
            var worked = ConcurrentMask.TryAllocate(ref storage, out int offset, 1);
            Assert.AreEqual(true, ConcurrentMask.Succeeded(worked));
            Assert.AreEqual(64+i, offset);
        }
        Assert.AreEqual(-1L, storage[0]);
        Assert.AreEqual(-1L, storage[1]);
        Assert.AreEqual(0L, storage[2]);
        storage.Dispose();
    }    

    [Test]
    public void AllocatesMultipleBitsFromArray()
    {        
        var storage = new NativeList<long>(3, Allocator.Persistent);
        storage.Length = 3;
        for(var i = 0; i < 3; ++i)
        {
            var worked = ConcurrentMask.TryAllocate(ref storage, out int offset, 33);
            Assert.AreEqual(true, ConcurrentMask.Succeeded(worked));
            Assert.AreEqual(i * 64, offset);
        }        
        {
            var worked = ConcurrentMask.TryAllocate(ref storage, out int offset, 33);
            Assert.AreEqual(false, ConcurrentMask.Succeeded(worked));
        }
        storage.Dispose();
    }    

    [Test]
    public void FreesOneBitFromArray()
    {        
        var storage = new NativeList<long>(3, Allocator.Persistent);
        storage.Length = 3;
        ConcurrentMask.TryAllocate(ref storage, out int _, 64);
        ConcurrentMask.TryAllocate(ref storage, out int _, 64);
        ConcurrentMask.TryAllocate(ref storage, out int _, 64);
        for(var i = 0; i < 64 * 3; ++i)
        {
            var worked = ConcurrentMask.TryFree(ref storage, i, 1);
            Assert.AreEqual(true, ConcurrentMask.Succeeded(worked));
        }
        Assert.AreEqual(0L, storage[0]);
        Assert.AreEqual(0L, storage[1]);
        Assert.AreEqual(0L, storage[2]);
        storage.Dispose();
    }    

    [Test]
    public void FreesMultipleBitsFromArray()
    {        
        var storage = new NativeList<long>(3, Allocator.Persistent);
        storage.Length = 3;
        ConcurrentMask.TryAllocate(ref storage, out int _, 64);
        ConcurrentMask.TryAllocate(ref storage, out int _, 64);
        ConcurrentMask.TryAllocate(ref storage, out int _, 64);
        for(var i = 0; i < 3; ++i)
        {
            var worked = ConcurrentMask.TryFree(ref storage, i * 64 + 1, 63);
            Assert.AreEqual(true, ConcurrentMask.Succeeded(worked));
        }
        Assert.AreEqual(1L, storage[0]);
        Assert.AreEqual(1L, storage[1]);
        Assert.AreEqual(1L, storage[2]);
        storage.Dispose();
    }    
            
    [BurstCompile(CompileSynchronously = true)]
    struct AllocateJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction] public NativeList<long> m_storage;
        public void Execute(int index)
        {
            ConcurrentMask.TryAllocate(ref m_storage, out int _, 1);
        }
    }
          
    [Test]
    public void AllocatesFromJob()
    {
        const int kLengthInWords = 10;
        const int kLengthInBits = kLengthInWords * 64;
        var storage = new NativeList<long>(kLengthInWords, Allocator.Persistent);
        storage.Length = kLengthInWords;
        
        for (int i = 0; i < kLengthInWords; ++i)
            Assert.AreEqual(0L, storage[i]);

        var allocateJob = new AllocateJob();
        allocateJob.m_storage = storage;
        allocateJob.Schedule(kLengthInBits, 1).Complete();

        for (int i = 0; i < kLengthInWords; ++i)
            Assert.AreEqual(~0L, storage[i]);

        storage.Dispose();
    }
            
}
