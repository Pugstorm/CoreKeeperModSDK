using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Assert = FastAssert;
using Unity.Collections.Tests;

internal class NativeParallelHashMapTests_InJobs : NativeParallelHashMapTestsFixture
{
    struct NestedMapJob : IJob
    {
        public NativeParallelHashMap<int, NativeParallelHashMap<int, int>> nestedMap;

        public void Execute()
        {
            nestedMap.Clear();
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeParallelHashMap_NestedJob_Error()
    {
        var map = new NativeParallelHashMap<int, NativeParallelHashMap<int, int>>(hashMapSize, CommonRwdAllocator.Handle);

        var nestedJob = new NestedMapJob
        {
            nestedMap = map
        };

        JobHandle job = default;
        Assert.Throws<InvalidOperationException>(() => { job = nestedJob.Schedule(); }); 
        job.Complete();

        map.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_Read_And_Write()
    {
        var hashMap = new NativeParallelHashMap<int, int>(hashMapSize, CommonRwdAllocator.Handle);
        var writeStatus = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);
        var readValues = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);

        var writeData = new HashMapWriteJob()
        {
            hashMap = hashMap.AsParallelWriter(),
            status = writeStatus,
            keyMod = hashMapSize,
        };

        var readData = new HashMapReadParallelForJob()
        {
            hashMap = hashMap,
            values = readValues,
            keyMod = writeData.keyMod,
        };

        var writeJob = writeData.Schedule();
        var readJob = readData.Schedule(hashMapSize, 1, writeJob);
        readJob.Complete();

        for (int i = 0; i < hashMapSize; ++i)
        {
            Assert.AreEqual(0, writeStatus[i], "Job failed to write value to hash map");
            Assert.AreEqual(i, readValues[i], "Job failed to read from hash map");
        }

        hashMap.Dispose();
        writeStatus.Dispose();
        readValues.Dispose();
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeParallelHashMap_Read_And_Write_Full()
    {
        var hashMap = new NativeParallelHashMap<int, int>(hashMapSize / 2, CommonRwdAllocator.Handle);
        var writeStatus = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);
        var readValues = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);

        var writeData = new HashMapWriteJob()
        {
            hashMap = hashMap.AsParallelWriter(),
            status = writeStatus,
            keyMod = hashMapSize,
        };

        var readData = new HashMapReadParallelForJob()
        {
            hashMap = hashMap,
            values = readValues,
            keyMod = writeData.keyMod,
        };

        var writeJob = writeData.Schedule();
        var readJob = readData.Schedule(hashMapSize, 1, writeJob);
        readJob.Complete();

        var missing = new Dictionary<int, bool>();
        for (int i = 0; i < hashMapSize; ++i)
        {
            if (writeStatus[i] == -2)
            {
                missing[i] = true;
                Assert.AreEqual(-1, readValues[i], "Job read a value form hash map which should not be there");
            }
            else
            {
                Assert.AreEqual(0, writeStatus[i], "Job failed to write value to hash map");
                Assert.AreEqual(i, readValues[i], "Job failed to read from hash map");
            }
        }
        Assert.AreEqual(hashMapSize - hashMapSize / 2, missing.Count, "Wrong indices written to hash map");

        hashMap.Dispose();
        writeStatus.Dispose();
        readValues.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_Key_Collisions()
    {
        var hashMap = new NativeParallelHashMap<int, int>(hashMapSize, CommonRwdAllocator.Handle);
        var writeStatus = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);
        var readValues = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);

        var writeData = new HashMapWriteJob()
        {
            hashMap = hashMap.AsParallelWriter(),
            status = writeStatus,
            keyMod = 16,
        };

        var readData = new HashMapReadParallelForJob()
        {
            hashMap = hashMap,
            values = readValues,
            keyMod = writeData.keyMod,
        };

        var writeJob = writeData.Schedule();
        var readJob = readData.Schedule(hashMapSize, 1, writeJob);
        readJob.Complete();

        var missing = new Dictionary<int, bool>();
        for (int i = 0; i < hashMapSize; ++i)
        {
            if (writeStatus[i] == -1)
            {
                missing[i] = true;
                Assert.AreNotEqual(i, readValues[i], "Job read a value form hash map which should not be there");
            }
            else
            {
                Assert.AreEqual(0, writeStatus[i], "Job failed to write value to hash map");
                Assert.AreEqual(i, readValues[i], "Job failed to read from hash map");
            }
        }
        Assert.AreEqual(hashMapSize - writeData.keyMod, missing.Count, "Wrong indices written to hash map");

        hashMap.Dispose();
        writeStatus.Dispose();
        readValues.Dispose();
    }

    [BurstCompile(CompileSynchronously = true)]
    struct Clear : IJob
    {
        public NativeParallelHashMap<int, int> hashMap;

        public void Execute()
        {
            hashMap.Clear();
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeParallelHashMap_Clear_And_Write()
    {
        var hashMap = new NativeParallelHashMap<int, int>(hashMapSize / 2, CommonRwdAllocator.Handle);
        var writeStatus = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);

        var clearJob = new Clear
        {
            hashMap = hashMap
        };

        var clearJobHandle = clearJob.Schedule();

        var writeJob = new HashMapWriteJob
        {
            hashMap = hashMap.AsParallelWriter(),
            status = writeStatus,
            keyMod = hashMapSize,
        };

        var writeJobHandle = writeJob.Schedule(clearJobHandle);
        writeJobHandle.Complete();

        writeStatus.Dispose();
        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_DisposeJob()
    {
        var container0 = new NativeParallelHashMap<int, int>(1, Allocator.Persistent);
        Assert.True(container0.IsCreated);
        Assert.DoesNotThrow(() => { container0.Add(0, 1); });
        Assert.True(container0.ContainsKey(0));

        var container1 = new NativeParallelMultiHashMap<int, int>(1, Allocator.Persistent);
        Assert.True(container1.IsCreated);
        Assert.DoesNotThrow(() => { container1.Add(1, 2); });
        Assert.True(container1.ContainsKey(1));

        var disposeJob0 = container0.Dispose(default);
        Assert.False(container0.IsCreated);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        Assert.Throws<ObjectDisposedException>(
            () => { container0.ContainsKey(0); });
#endif

        var disposeJob = container1.Dispose(disposeJob0);
        Assert.False(container1.IsCreated);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        Assert.Throws<ObjectDisposedException>(
            () => { container1.ContainsKey(1); });
#endif

        disposeJob.Complete();
    }
}
