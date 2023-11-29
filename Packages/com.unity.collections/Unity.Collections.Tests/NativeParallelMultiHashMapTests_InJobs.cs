using NUnit.Framework;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Assert = FastAssert;

namespace Unity.Collections.Tests
{
    internal class NativeParallelMultiHashMapTests_InJobs : NativeParallelMultiHashMapTestsFixture
    {
        [Test]
        public void NativeParallelMultiHashMap_Read_And_Write()
        {
            var hashMap = new NativeParallelMultiHashMap<int, int>(hashMapSize, CommonRwdAllocator.Handle);
            var writeStatus = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);
            var readValues = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);

            var writeData = new MultiHashMapWriteParallelForJob()
            {
                hashMap = hashMap.AsParallelWriter(),
                status = writeStatus,
                keyMod = hashMapSize,
            };

            var readData = new MultiHashMapReadParallelForJob()
            {
                hashMap = hashMap,
                values = readValues,
                keyMod = writeData.keyMod,
            };

            var writeJob = writeData.Schedule(hashMapSize, 1);
            var readJob = readData.Schedule(hashMapSize, 1, writeJob);
            readJob.Complete();

            for (int i = 0; i < hashMapSize; ++i)
            {
                Assert.AreEqual(0, writeStatus[i], "Job failed to write value to hash map");
                Assert.AreEqual(1, readValues[i], "Job failed to read from hash map");
            }

            hashMap.Dispose();
            writeStatus.Dispose();
            readValues.Dispose();
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void NativeParallelMultiHashMap_Read_And_Write_Full()
        {
            var hashMap = new NativeParallelMultiHashMap<int, int>(hashMapSize / 2, CommonRwdAllocator.Handle);
            var writeStatus = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);
            var readValues = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);

            var writeData = new MultiHashMapWriteParallelForJob()
            {
                hashMap = hashMap.AsParallelWriter(),
                status = writeStatus,
                keyMod = hashMapSize,
            };

            var readData = new MultiHashMapReadParallelForJob()
            {
                hashMap = hashMap,
                values = readValues,
                keyMod = writeData.keyMod,
            };

            var writeJob = writeData.Schedule(hashMapSize, 1);
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
                    Assert.AreEqual(1, readValues[i], "Job failed to read from hash map");
                }
            }
            Assert.AreEqual(hashMapSize - hashMapSize / 2, missing.Count, "Wrong indices written to hash map");

            hashMap.Dispose();
            writeStatus.Dispose();
            readValues.Dispose();
        }

        [Test]
        public void NativeParallelMultiHashMap_Key_Collisions()
        {
            var hashMap = new NativeParallelMultiHashMap<int, int>(hashMapSize, CommonRwdAllocator.Handle);
            var writeStatus = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);
            var readValues = CollectionHelper.CreateNativeArray<int>(hashMapSize, CommonRwdAllocator.Handle);

            var writeData = new MultiHashMapWriteParallelForJob()
            {
                hashMap = hashMap.AsParallelWriter(),
                status = writeStatus,
                keyMod = 16,
            };

            var readData = new MultiHashMapReadParallelForJob()
            {
                hashMap = hashMap,
                values = readValues,
                keyMod = writeData.keyMod,
            };

            var writeJob = writeData.Schedule(hashMapSize, 1);
            var readJob = readData.Schedule(hashMapSize, 1, writeJob);
            readJob.Complete();

            for (int i = 0; i < hashMapSize; ++i)
            {
                Assert.AreEqual(0, writeStatus[i], "Job failed to write value to hash map");
                Assert.AreEqual(hashMapSize / readData.keyMod, readValues[i], "Job failed to read from hash map");
            }

            hashMap.Dispose();
            writeStatus.Dispose();
            readValues.Dispose();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct AddMultiIndex : IJobParallelFor
        {
            public NativeParallelMultiHashMap<int, int>.ParallelWriter hashMap;

            public void Execute(int index)
            {
                hashMap.Add(index, index);
            }
        }

        [Test]
        public void NativeParallelMultiHashMap_TryMultiAddScalabilityConcurrent()
        {
            for (int count = 0; count < 1024; count++)
            {
                var hashMap = new NativeParallelMultiHashMap<int, int>(count, CommonRwdAllocator.Handle);
                var addIndexJob = new AddMultiIndex
                {
                    hashMap = hashMap.AsParallelWriter()
                };

                var addIndexJobHandle = addIndexJob.Schedule(count, 64);
                addIndexJobHandle.Complete();
                hashMap.Dispose();
            }
        }
    }
}
