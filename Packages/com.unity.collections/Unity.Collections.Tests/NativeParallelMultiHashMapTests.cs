using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.NotBurstCompatible;
using Unity.Collections.Tests;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

internal class NativeParallelMultiHashMapTests : CollectionsTestFixture
{
    // These tests require:
    // - JobsDebugger support for static safety IDs (added in 2020.1)
    // - Asserting throws
#if !UNITY_DOTSRUNTIME
    [Test, DotsRuntimeIgnore]
    [TestRequiresCollectionChecks]
    public void NativeParallelMultiHashMap_UseAfterFree_UsesCustomOwnerTypeName()
    {
        var container = new NativeParallelMultiHashMap<int, int>(10, CommonRwdAllocator.Handle);
        container.Add(0, 123);
        container.Dispose();
        Assert.That(() => container.ContainsKey(0),
            Throws.Exception.TypeOf<ObjectDisposedException>()
                .With.Message.Contains($"The {container.GetType()} has been deallocated"));
    }

    [BurstCompile(CompileSynchronously = true)]
    struct NativeParallelMultiHashMap_CreateAndUseAfterFreeBurst : IJob
    {
        public void Execute()
        {
            var container = new NativeParallelMultiHashMap<int, int>(10, Allocator.Temp);
            container.Add(0, 17);
            container.Dispose();
            container.Add(1, 42);
        }
    }

    [Test, DotsRuntimeIgnore]
    [TestRequiresCollectionChecks]
    public void NativeParallelMultiHashMap_CreateAndUseAfterFreeInBurstJob_UsesCustomOwnerTypeName()
    {
        // Make sure this isn't the first container of this type ever created, so that valid static safety data exists
        var container = new NativeParallelMultiHashMap<int, int>(10, CommonRwdAllocator.Handle);
        container.Dispose();

        var job = new NativeParallelMultiHashMap_CreateAndUseAfterFreeBurst
        {
        };

        // Two things:
        // 1. This exception is logged, not thrown; thus, we use LogAssert to detect it.
        // 2. Calling write operation after container.Dispose() emits an unintuitive error message. For now, all this test cares about is whether it contains the
        //    expected type name.
        job.Run();
        LogAssert.Expect(LogType.Exception,
            new Regex($"InvalidOperationException: The {Regex.Escape(container.GetType().ToString())} has been declared as \\[ReadOnly\\] in the job, but you are writing to it"));
    }
#endif

    [Test]
    public void NativeParallelMultiHashMap_IsEmpty()
    {
        var container = new NativeParallelMultiHashMap<int, int>(0, Allocator.Persistent);
        Assert.IsTrue(container.IsEmpty);

        container.Add(0, 0);
        Assert.IsFalse(container.IsEmpty);
        Assert.AreEqual(1, container.Capacity);
        ExpectedCount(ref container, 1);

        container.Remove(0, 0);
        Assert.IsTrue(container.IsEmpty);

        container.Add(0, 0);
        container.Clear();
        Assert.IsTrue(container.IsEmpty);

        container.Dispose();
    }

    [Test]
    public void NativeParallelMultiHashMap_CountValuesForKey()
    {
        var hashMap = new NativeParallelMultiHashMap<int, int>(1, Allocator.Temp);
        hashMap.Add(5, 7);
        hashMap.Add(6, 9);
        hashMap.Add(6, 10);

        Assert.AreEqual(1, hashMap.CountValuesForKey(5));
        Assert.AreEqual(2, hashMap.CountValuesForKey(6));
        Assert.AreEqual(0, hashMap.CountValuesForKey(7));

        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelMultiHashMap_RemoveKeyAndValue()
    {
        var hashMap = new NativeParallelMultiHashMap<int, long>(1, Allocator.Temp);
        hashMap.Add(10, 0);
        hashMap.Add(10, 1);
        hashMap.Add(10, 2);

        hashMap.Add(20, 2);
        hashMap.Add(20, 2);
        hashMap.Add(20, 1);
        hashMap.Add(20, 2);
        hashMap.Add(20, 1);

        hashMap.Remove(10, 1L);
        ExpectValues(hashMap, 10, new[] { 0L, 2L });
        ExpectValues(hashMap, 20, new[] { 1L, 1L, 2L, 2L, 2L });

        hashMap.Remove(20, 2L);
        ExpectValues(hashMap, 10, new[] { 0L, 2L });
        ExpectValues(hashMap, 20, new[] { 1L, 1L });

        hashMap.Remove(20, 1L);
        ExpectValues(hashMap, 10, new[] { 0L, 2L });
        ExpectValues(hashMap, 20, new long[0]);

        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelMultiHashMap_ValueIterator()
    {
        var hashMap = new NativeParallelMultiHashMap<int, int>(1, Allocator.Temp);
        hashMap.Add(5, 0);
        hashMap.Add(5, 1);
        hashMap.Add(5, 2);

        var list = new NativeList<int>(CommonRwdAllocator.Handle);

        GCAllocRecorder.ValidateNoGCAllocs(() =>
        {
            list.Clear();
            foreach (var value in hashMap.GetValuesForKey(5))
                list.Add(value);
        });

        list.Sort();
        Assert.AreEqual(list.ToArrayNBC(), new int[] { 0, 1, 2 });

        foreach (var value in hashMap.GetValuesForKey(6))
            Assert.Fail();

        list.Dispose();
        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelMultiHashMap_RemoveKeyValueDoesntDeallocate()
    {
        var hashMap = new NativeParallelMultiHashMap<int, int>(1, Allocator.Temp) { { 5, 1 } };

        hashMap.Remove(5, 5);
        GCAllocRecorder.ValidateNoGCAllocs(() =>
        {
            hashMap.Remove(5, 1);
        });
        Assert.IsTrue(hashMap.IsEmpty);

        hashMap.Dispose();
    }

    static void ExpectedCount<TKey, TValue>(ref NativeParallelMultiHashMap<TKey, TValue> container, int expected)
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        Assert.AreEqual(expected == 0, container.IsEmpty);
        Assert.AreEqual(expected, container.Count());
    }

    [Test]
    public void NativeParallelMultiHashMap_RemoveOnEmptyMap_DoesNotThrow()
    {
        var hashMap = new NativeParallelMultiHashMap<int, int>(0, Allocator.Temp);

        Assert.DoesNotThrow(() => hashMap.Remove(0));
        Assert.DoesNotThrow(() => hashMap.Remove(-425196));
        Assert.DoesNotThrow(() => hashMap.Remove(0, 0));
        Assert.DoesNotThrow(() => hashMap.Remove(-425196, 0));

        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelMultiHashMap_RemoveFromMultiHashMap()
    {
        var hashMap = new NativeParallelMultiHashMap<int, int>(16, Allocator.Temp);
        int iSquared;
        // Make sure inserting values work
        for (int i = 0; i < 8; ++i)
            hashMap.Add(i, i * i);
        for (int i = 0; i < 8; ++i)
            hashMap.Add(i, i);
        Assert.AreEqual(16, hashMap.Capacity, "HashMap grew larger than expected");
        // Make sure reading the inserted values work
        for (int i = 0; i < 8; ++i)
        {
            NativeParallelMultiHashMapIterator<int> it;
            Assert.IsTrue(hashMap.TryGetFirstValue(i, out iSquared, out it), "Failed get value from hash table");
            Assert.AreEqual(iSquared, i, "Got the wrong value from the hash table");
            Assert.IsTrue(hashMap.TryGetNextValue(out iSquared, ref it), "Failed get value from hash table");
            Assert.AreEqual(iSquared, i * i, "Got the wrong value from the hash table");
        }
        for (int rm = 0; rm < 8; ++rm)
        {
            Assert.AreEqual(2, hashMap.Remove(rm));
            NativeParallelMultiHashMapIterator<int> it;
            Assert.IsFalse(hashMap.TryGetFirstValue(rm, out iSquared, out it), "Failed to remove value from hash table");
            for (int i = rm + 1; i < 8; ++i)
            {
                Assert.IsTrue(hashMap.TryGetFirstValue(i, out iSquared, out it), "Failed get value from hash table");
                Assert.AreEqual(iSquared, i, "Got the wrong value from the hash table");
                Assert.IsTrue(hashMap.TryGetNextValue(out iSquared, ref it), "Failed get value from hash table");
                Assert.AreEqual(iSquared, i * i, "Got the wrong value from the hash table");
            }
        }
        // Make sure entries were freed
        for (int i = 0; i < 8; ++i)
            hashMap.Add(i, i * i);
        for (int i = 0; i < 8; ++i)
            hashMap.Add(i, i);
        Assert.AreEqual(16, hashMap.Capacity, "HashMap grew larger than expected");
        hashMap.Dispose();
    }

    void ExpectValues(NativeParallelMultiHashMap<int, long> hashMap, int key, long[] expectedValues)
    {
        var list = new NativeList<long>(CommonRwdAllocator.Handle);
        foreach (var value in hashMap.GetValuesForKey(key))
            list.Add(value);

        list.Sort();
        Assert.AreEqual(list.ToArrayNBC(), expectedValues);
        list.Dispose();
    }

    [Test]
    public void NativeParallelMultiHashMap_GetKeys()
    {
        var container = new NativeParallelMultiHashMap<int, int>(1, Allocator.Temp);
        for (int i = 0; i < 30; ++i)
        {
            container.Add(i, 2 * i);
            container.Add(i, 3 * i);
        }
        var keys = container.GetKeyArray(Allocator.Temp);
        var (unique, uniqueLength) = container.GetUniqueKeyArray(Allocator.Temp);
        Assert.AreEqual(30, uniqueLength);

        Assert.AreEqual(60, keys.Length);
        keys.Sort();
        for (int i = 0; i < 30; ++i)
        {
            Assert.AreEqual(i, keys[i * 2 + 0]);
            Assert.AreEqual(i, keys[i * 2 + 1]);
            Assert.AreEqual(i, unique[i]);
        }
    }

    [Test]
    public void NativeParallelMultiHashMap_GetUniqueKeysEmpty()
    {
        var hashMap = new NativeParallelMultiHashMap<int, int>(1, Allocator.Temp);
        var keys = hashMap.GetUniqueKeyArray(Allocator.Temp);

        Assert.AreEqual(0, keys.Item1.Length);
        Assert.AreEqual(0, keys.Item2);
    }

    [Test]
    public void NativeParallelMultiHashMap_GetUniqueKeys()
    {
        var hashMap = new NativeParallelMultiHashMap<int, int>(1, Allocator.Temp);
        for (int i = 0; i < 30; ++i)
        {
            hashMap.Add(i, 2 * i);
            hashMap.Add(i, 3 * i);
        }
        var keys = hashMap.GetUniqueKeyArray(Allocator.Temp);
        hashMap.Dispose();
        Assert.AreEqual(30, keys.Item2);
        for (int i = 0; i < 30; ++i)
        {
            Assert.AreEqual(i, keys.Item1[i]);
        }
        keys.Item1.Dispose();
    }

    [Test]
    public void NativeParallelMultiHashMap_GetValues()
    {
        var hashMap = new NativeParallelMultiHashMap<int, int>(1, Allocator.Temp);
        for (int i = 0; i < 30; ++i)
        {
            hashMap.Add(i, 30 + i);
            hashMap.Add(i, 60 + i);
        }
        var values = hashMap.GetValueArray(Allocator.Temp);
        hashMap.Dispose();

        Assert.AreEqual(60, values.Length);
        values.Sort();
        for (int i = 0; i < 60; ++i)
        {
            Assert.AreEqual(30 + i, values[i]);
        }
        values.Dispose();
    }

    [Test]
    public void NativeParallelMultiHashMap_ForEach_FixedStringInHashMap()
    {
        using (var stringList = new NativeList<FixedString32Bytes>(10, Allocator.Persistent) { "Hello", ",", "World", "!" })
        {
            var container = new NativeParallelMultiHashMap<FixedString128Bytes, float>(50, Allocator.Temp);
            var seen = new NativeArray<int>(stringList.Length, Allocator.Temp);
            foreach (var str in stringList)
            {
                container.Add(str, 0);
            }

            foreach (var pair in container)
            {
                int index = stringList.IndexOf(pair.Key);
                Assert.AreEqual(stringList[index], pair.Key.ToString());
                seen[index] = seen[index] + 1;
            }

            for (int i = 0; i < stringList.Length; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect value count {stringList[i]}");
            }
        }
    }

    [Test]
    public void NativeParallelMultiHashMap_ForEach([Values(10, 1000)]int n)
    {
        var seenKeys = new NativeArray<int>(n, Allocator.Temp);
        var seenValues = new NativeArray<int>(n * 2, Allocator.Temp);
        using (var container = new NativeParallelMultiHashMap<int, int>(1, Allocator.Temp))
        {
            for (int i = 0; i < n; ++i)
            {
                container.Add(i, i);
                container.Add(i, i + n);
            }

            var count = 0;
            foreach (var kv in container)
            {
                if (kv.Value < n)
                {
                    Assert.AreEqual(kv.Key, kv.Value);
                }
                else
                {
                    Assert.AreEqual(kv.Key + n, kv.Value);
                }

                seenKeys[kv.Key] = seenKeys[kv.Key] + 1;
                seenValues[kv.Value] = seenValues[kv.Value] + 1;

                ++count;
            }

            Assert.AreEqual(container.Count(), count);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(2, seenKeys[i], $"Incorrect key count {i}");
                Assert.AreEqual(1, seenValues[i], $"Incorrect value count {i}");
                Assert.AreEqual(1, seenValues[i + n], $"Incorrect value count {i + n}");
            }
        }
    }

    struct NativeParallelMultiHashMap_ForEach_Job : IJob
    {
        [ReadOnly]
        public NativeParallelMultiHashMap<int, int> Input;

        [ReadOnly]
        public int Num;

        public void Execute()
        {
            var seenKeys = new NativeArray<int>(Num, Allocator.Temp);
            var seenValues = new NativeArray<int>(Num * 2, Allocator.Temp);

            var count = 0;
            foreach (var kv in Input)
            {
                if (kv.Value < Num)
                {
                    Assert.AreEqual(kv.Key, kv.Value);
                }
                else
                {
                    Assert.AreEqual(kv.Key + Num, kv.Value);
                }

                seenKeys[kv.Key] = seenKeys[kv.Key] + 1;
                seenValues[kv.Value] = seenValues[kv.Value] + 1;

                ++count;
            }

            Assert.AreEqual(Input.Count(), count);
            for (int i = 0; i < Num; i++)
            {
                Assert.AreEqual(2, seenKeys[i], $"Incorrect key count {i}");
                Assert.AreEqual(1, seenValues[i], $"Incorrect value count {i}");
                Assert.AreEqual(1, seenValues[i + Num], $"Incorrect value count {i + Num}");
            }

            seenKeys.Dispose();
            seenValues.Dispose();
        }
    }

    [Test]
    public void NativeParallelMultiHashMap_ForEach_From_Job([Values(10, 1000)] int n)
    {
        using (var container = new NativeParallelMultiHashMap<int, int>(1, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < n; ++i)
            {
                container.Add(i, i);
                container.Add(i, i + n);
            }

            new NativeParallelMultiHashMap_ForEach_Job
            {
                Input = container,
                Num = n,

            }.Run();
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeParallelMultiHashMap_ForEach_Throws_When_Modified()
    {
        using (var container = new NativeParallelMultiHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < 30; ++i)
            {
                container.Add(i, 30 + i);
                container.Add(i, 60 + i);
            }

            Assert.Throws<ObjectDisposedException>(() =>
            {
                foreach (var kv in container)
                {
                    container.Add(10, 10);
                }
            });

            Assert.Throws<ObjectDisposedException>(() =>
            {
                foreach (var kv in container)
                {
                    container.Remove(1);
                }
            });
        }
    }

    struct NativeParallelMultiHashMap_ForEachIterator : IJob
    {
        [ReadOnly]
        public NativeParallelMultiHashMap<int, int>.KeyValueEnumerator Iter;

        public void Execute()
        {
            while (Iter.MoveNext())
            {
            }
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeParallelMultiHashMap_ForEach_Throws_Job_Iterator()
    {
        using (var container = new NativeParallelMultiHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            var jobHandle = new NativeParallelMultiHashMap_ForEachIterator
            {
                Iter = container.GetEnumerator()

            }.Schedule();

            Assert.Throws<InvalidOperationException>(() => { container.Add(1, 1); });

            jobHandle.Complete();
        }
    }

    struct ParallelWriteToMultiHashMapJob : IJobParallelFor
    {
        [WriteOnly]
        public NativeParallelMultiHashMap<int, int>.ParallelWriter Writer;

        public void Execute(int index)
        {
            Writer.Add(index, 0);
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeParallelMultiHashMap_ForEach_Throws_When_Modified_From_Job()
    {
        using (var container = new NativeParallelMultiHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            var iter = container.GetEnumerator();

            var jobHandle = new ParallelWriteToMultiHashMapJob
            {
                Writer = container.AsParallelWriter()

            }.Schedule(1, 2);

            Assert.Throws<ObjectDisposedException>(() =>
            {
                while (iter.MoveNext())
                {
                }
            });

            jobHandle.Complete();
        }
    }

    [Test]
    public void NativeParallelMultiHashMap_GetKeysAndValues()
    {
        var container = new NativeParallelMultiHashMap<int, int>(1, Allocator.Temp);
        for (int i = 0; i < 30; ++i)
        {
            container.Add(i, 30 + i);
            container.Add(i, 60 + i);
        }
        var keysValues = container.GetKeyValueArrays(Allocator.Temp);
        container.Dispose();

        Assert.AreEqual(60, keysValues.Keys.Length);
        Assert.AreEqual(60, keysValues.Values.Length);

        // ensure keys and matching values are aligned (though unordered)
        for (int i = 0; i < 30; ++i)
        {
            var k0 = keysValues.Keys[i * 2 + 0];
            var k1 = keysValues.Keys[i * 2 + 1];
            var v0 = keysValues.Values[i * 2 + 0];
            var v1 = keysValues.Values[i * 2 + 1];

            if (v0 > v1)
                (v0, v1) = (v1, v0);

            Assert.AreEqual(k0, k1);
            Assert.AreEqual(30 + k0, v0);
            Assert.AreEqual(60 + k0, v1);
        }

        keysValues.Keys.Sort();
        for (int i = 0; i < 30; ++i)
        {
            Assert.AreEqual(i, keysValues.Keys[i * 2 + 0]);
            Assert.AreEqual(i, keysValues.Keys[i * 2 + 1]);
        }

        keysValues.Values.Sort();
        for (int i = 0; i < 60; ++i)
        {
            Assert.AreEqual(30 + i, keysValues.Values[i]);
        }

        keysValues.Dispose();
    }

    [Test]
    public void NativeParallelMultiHashMap_ContainsKeyMultiHashMap()
    {
        var container = new NativeParallelMultiHashMap<int, int>(1, Allocator.Temp);
        container.Add(5, 7);

        container.Add(6, 9);
        container.Add(6, 10);

        Assert.IsTrue(container.ContainsKey(5));
        Assert.IsTrue(container.ContainsKey(6));
        Assert.IsFalse(container.ContainsKey(4));

        container.Dispose();
    }

    [Test]
    public void NativeParallelMultiHashMap_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new NativeParallelMultiHashMap<int, int>(1, allocator.Handle))
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
                using (var container = new NativeParallelMultiHashMap<int, int>(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void NativeParallelMultiHashMap_BurstedCustomAllocatorTest()
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

    public struct NestedHashMap
    {
        public NativeParallelMultiHashMap<int, int> map;
    }

    [Test]
    public void NativeParallelMultiHashMap_Nested()
    {
        var mapInner = new NativeParallelMultiHashMap<int, int>(16, CommonRwdAllocator.Handle);
        NestedHashMap mapStruct = new NestedHashMap { map = mapInner };

        var mapNestedStruct = new NativeParallelMultiHashMap<int, NestedHashMap>(16, CommonRwdAllocator.Handle);
        var mapNested = new NativeParallelMultiHashMap<int, NativeParallelMultiHashMap<int, int>>(16, CommonRwdAllocator.Handle);

        mapNested.Add(14, mapInner);
        mapNestedStruct.Add(17, mapStruct);

        mapNested.Dispose();
        mapNestedStruct.Dispose();
        mapInner.Dispose();
    }
}
