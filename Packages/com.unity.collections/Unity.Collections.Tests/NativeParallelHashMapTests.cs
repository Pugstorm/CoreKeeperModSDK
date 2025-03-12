using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.Tests;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Collections.LowLevel.Unsafe;
using System.Text.RegularExpressions;
using Assert = FastAssert;

internal class NativeParallelHashMapTests : CollectionsTestFixture
{
#pragma warning disable 0649 // always default value

    struct NonBlittableStruct : IEquatable<NonBlittableStruct>
    {
        object o;

        public bool Equals(NonBlittableStruct other)
        {
            return Equals(o, other.o);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is NonBlittableStruct other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (o != null ? o.GetHashCode() : 0);
        }
    }

#pragma warning restore 0649

    static void ExpectedCount<TKey, TValue>(ref NativeParallelHashMap<TKey, TValue> container, int expected)
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        Assert.AreEqual(expected == 0, container.IsEmpty);
        Assert.AreEqual(expected, container.Count());
    }

    [Test]
    public void NativeParallelHashMap_TryAdd_TryGetValue_Clear()
    {
        var hashMap = new NativeParallelHashMap<int, int>(16, Allocator.Temp);
        ExpectedCount(ref hashMap, 0);

        int iSquared;
        // Make sure GetValue fails if hash map is empty
        Assert.IsFalse(hashMap.TryGetValue(0, out iSquared), "TryGetValue on empty hash map did not fail");

        // Make sure inserting values work
        for (int i = 0; i < 16; ++i)
            Assert.IsTrue(hashMap.TryAdd(i, i * i), "Failed to add value");
        ExpectedCount(ref hashMap, 16);

        // Make sure inserting duplicate keys fails
        for (int i = 0; i < 16; ++i)
            Assert.IsFalse(hashMap.TryAdd(i, i), "Adding duplicate keys did not fail");
        ExpectedCount(ref hashMap, 16);

        // Make sure reading the inserted values work
        for (int i = 0; i < 16; ++i)
        {
            Assert.IsTrue(hashMap.TryGetValue(i, out iSquared), "Failed get value from hash table");
            Assert.AreEqual(iSquared, i * i, "Got the wrong value from the hash table");
        }

        // Make sure clearing removes all keys
        hashMap.Clear();
        ExpectedCount(ref hashMap, 0);

        for (int i = 0; i < 16; ++i)
            Assert.IsFalse(hashMap.TryGetValue(i, out iSquared), "Got value from hash table after clearing");

        hashMap.Dispose();
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeParallelHashMap_Full_HashMap_Throws()
    {
        var hashMap = new NativeParallelHashMap<int, int>(16, Allocator.Temp);
        // Fill the hash map
        for (int i = 0; i < 16; ++i)
            Assert.IsTrue(hashMap.TryAdd(i, i), "Failed to add value");
        // Make sure overallocating throws and exception if using the Concurrent version - normal hash map would grow
        var cHashMap = hashMap.AsParallelWriter();
        Assert.Throws<System.InvalidOperationException>(() => { cHashMap.TryAdd(100, 100); });
        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_Key_Collisions()
    {
        var hashMap = new NativeParallelHashMap<int, int>(16, Allocator.Temp);
        int iSquared;
        // Make sure GetValue fails if hash map is empty
        Assert.IsFalse(hashMap.TryGetValue(0, out iSquared), "TryGetValue on empty hash map did not fail");
        // Make sure inserting values work
        for (int i = 0; i < 8; ++i)
            Assert.IsTrue(hashMap.TryAdd(i, i * i), "Failed to add value");
        // The bucket size is capacity * 2, adding that number should result in hash collisions
        for (int i = 0; i < 8; ++i)
            Assert.IsTrue(hashMap.TryAdd(i + 32, i), "Failed to add value with potential hash collision");
        // Make sure reading the inserted values work
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(hashMap.TryGetValue(i, out iSquared), "Failed get value from hash table");
            Assert.AreEqual(iSquared, i * i, "Got the wrong value from the hash table");
        }
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(hashMap.TryGetValue(i + 32, out iSquared), "Failed get value from hash table");
            Assert.AreEqual(iSquared, i, "Got the wrong value from the hash table");
        }
        hashMap.Dispose();
    }

    [StructLayout(LayoutKind.Explicit)]
    unsafe struct LargeKey : IEquatable<LargeKey>
    {
        [FieldOffset(0)]
        public ulong Ptr;

        [FieldOffset(300)]
        int x;

        public bool Equals(LargeKey rhs)
        {
            return Ptr == rhs.Ptr;
        }
        public override int GetHashCode()
        {
            return ((int)(Ptr>>32) * 1327) ^ (int)(Ptr & 0x7FFFFFFF);
        }
    }

    [BurstCompile]
    struct HashMapTryAddAtomic : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<LargeKey> keys;

        [WriteOnly]
        public NativeParallelHashMap<LargeKey, bool>.ParallelWriter hashMap;

        public void Execute(int index)
        {
            hashMap.TryAdd(keys[index & (keys.Length - 1)], false);
        }
    }

    [Test]
    public unsafe void NativeParallelHashMap_Key_Collisions_FromJobs()
    {
        var keys = CollectionHelper.CreateNativeArray<LargeKey>(4, CommonRwdAllocator.Handle);
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = new LargeKey { Ptr = (((ulong)i) << 32) };
        }

        for (var spin = 0; spin < 1024; spin++)
        {
            var hashMap = new NativeParallelHashMap<LargeKey, bool>(32, CommonRwdAllocator.Handle);

            var jobHandle = new HashMapTryAddAtomic
            {
                keys = keys,
                hashMap = hashMap.AsParallelWriter(),
            }
            .Schedule(8, 1);

            jobHandle.Complete();

            Assert.AreEqual(keys.Length, hashMap.Count());

            for (var i = 0; i < keys.Length; ++i)
            {
                var key = new LargeKey { Ptr = (((ulong)i) << 32) };
                Assert.IsTrue(hashMap.ContainsKey(key));
            }

            hashMap.Dispose();
        }

        keys.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_HashMapSupportsAutomaticCapacityChange()
    {
        var hashMap = new NativeParallelHashMap<int, int>(1, Allocator.Temp);
        int iSquared;
        // Make sure inserting values work and grows the capacity
        for (int i = 0; i < 8; ++i)
            Assert.IsTrue(hashMap.TryAdd(i, i * i), "Failed to add value");
        Assert.IsTrue(hashMap.Capacity >= 8, "Capacity was not updated correctly");
        // Make sure reading the inserted values work
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(hashMap.TryGetValue(i, out iSquared), "Failed get value from hash table");
            Assert.AreEqual(iSquared, i * i, "Got the wrong value from the hash table");
        }
        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_HashMapSameKey()
    {
        using (var hashMap = new NativeParallelHashMap<int, int>(0, Allocator.Persistent))
        {
            Assert.DoesNotThrow(() => hashMap.Add(0, 0));
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<ArgumentException>(() => hashMap.Add(0, 0));
#endif
        }

        using (var hashMap = new NativeParallelHashMap<int, int>(0, Allocator.Persistent))
        {
            Assert.IsTrue(hashMap.TryAdd(0, 0));
            Assert.IsFalse(hashMap.TryAdd(0, 0));
        }
    }

    [Test]
    public void NativeParallelHashMap_IsEmpty()
    {
        var container = new NativeParallelHashMap<int, int>(0, Allocator.Persistent);
        Assert.IsTrue(container.IsEmpty);

        container.TryAdd(0, 0);
        Assert.IsFalse(container.IsEmpty);
        Assert.AreEqual(1, container.Capacity);
        ExpectedCount(ref container, 1);

        container.Remove(0);
        Assert.IsTrue(container.IsEmpty);

        container.TryAdd(0, 0);
        container.Clear();
        Assert.IsTrue(container.IsEmpty);

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_HashMapEmptyCapacity()
    {
        var hashMap = new NativeParallelHashMap<int, int>(0, Allocator.Persistent);
        hashMap.TryAdd(0, 0);
        Assert.AreEqual(1, hashMap.Capacity);
        ExpectedCount(ref hashMap, 1);
        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_Remove()
    {
        var hashMap = new NativeParallelHashMap<int, int>(8, Allocator.Temp);
        int iSquared;
        // Make sure inserting values work
        for (int i = 0; i < 8; ++i)
            Assert.IsTrue(hashMap.TryAdd(i, i * i), "Failed to add value");
        Assert.AreEqual(8, hashMap.Capacity, "HashMap grew larger than expected");
        // Make sure reading the inserted values work
        for (int i = 0; i < 8; ++i)
        {
            Assert.IsTrue(hashMap.TryGetValue(i, out iSquared), "Failed get value from hash table");
            Assert.AreEqual(iSquared, i * i, "Got the wrong value from the hash table");
        }
        for (int rm = 0; rm < 8; ++rm)
        {
            Assert.IsTrue(hashMap.Remove(rm));
            Assert.IsFalse(hashMap.TryGetValue(rm, out iSquared), "Failed to remove value from hash table");
            for (int i = rm + 1; i < 8; ++i)
            {
                Assert.IsTrue(hashMap.TryGetValue(i, out iSquared), "Failed get value from hash table");
                Assert.AreEqual(iSquared, i * i, "Got the wrong value from the hash table");
            }
        }
        // Make sure entries were freed
        for (int i = 0; i < 8; ++i)
            Assert.IsTrue(hashMap.TryAdd(i, i * i), "Failed to add value");
        Assert.AreEqual(8, hashMap.Capacity, "HashMap grew larger than expected");
        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_RemoveOnEmptyMap_DoesNotThrow()
    {
        var hashMap = new NativeParallelHashMap<int, int>(0, Allocator.Temp);
        Assert.DoesNotThrow(() => hashMap.Remove(0));
        Assert.DoesNotThrow(() => hashMap.Remove(-425196));
        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_TryAddScalability()
    {
        var hashMap = new NativeParallelHashMap<int, int>(1, Allocator.Persistent);
        for (int i = 0; i != 1000 * 100; i++)
        {
            hashMap.TryAdd(i, i);
        }

        int value;
        Assert.IsFalse(hashMap.TryGetValue(-1, out value));
        Assert.IsFalse(hashMap.TryGetValue(1000 * 1000, out value));

        for (int i = 0; i != 1000 * 100; i++)
        {
            Assert.IsTrue(hashMap.TryGetValue(i, out value));
            Assert.AreEqual(i, value);
        }

        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_GetKeysEmpty()
    {
        var hashMap = new NativeParallelHashMap<int, int>(1, Allocator.Temp);
        var keys = hashMap.GetKeyArray(Allocator.Temp);
        hashMap.Dispose();

        Assert.AreEqual(0, keys.Length);
        keys.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_GetKeys()
    {
        var hashMap = new NativeParallelHashMap<int, int>(1, Allocator.Temp);
        for (int i = 0; i < 30; ++i)
        {
            hashMap.TryAdd(i, 2 * i);
        }
        var keys = hashMap.GetKeyArray(Allocator.Temp);
        hashMap.Dispose();

        Assert.AreEqual(30, keys.Length);
        keys.Sort();
        for (int i = 0; i < 30; ++i)
        {
            Assert.AreEqual(i, keys[i]);
        }
        keys.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_GetValues()
    {
        var hashMap = new NativeParallelHashMap<int, int>(1, Allocator.Temp);
        for (int i = 0; i < 30; ++i)
        {
            hashMap.TryAdd(i, 2 * i);
        }
        var values = hashMap.GetValueArray(Allocator.Temp);
        hashMap.Dispose();

        Assert.AreEqual(30, values.Length);
        values.Sort();
        for (int i = 0; i < 30; ++i)
        {
            Assert.AreEqual(2 * i, values[i]);
        }
        values.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_GetKeysAndValues()
    {
        var hashMap = new NativeParallelHashMap<int, int>(1, Allocator.Temp);
        for (int i = 0; i < 30; ++i)
        {
            hashMap.TryAdd(i, 2 * i);
        }
        var keysValues = hashMap.GetKeyValueArrays(Allocator.Temp);
        hashMap.Dispose();

        Assert.AreEqual(30, keysValues.Keys.Length);
        Assert.AreEqual(30, keysValues.Values.Length);

        // ensure keys and matching values are aligned
        for (int i = 0; i < 30; ++i)
        {
            Assert.AreEqual(2 * keysValues.Keys[i], keysValues.Values[i]);
        }

        keysValues.Keys.Sort();
        for (int i = 0; i < 30; ++i)
        {
            Assert.AreEqual(i, keysValues.Keys[i]);
        }

        keysValues.Values.Sort();
        for (int i = 0; i < 30; ++i)
        {
            Assert.AreEqual(2 * i, keysValues.Values[i]);
        }

        keysValues.Dispose();
    }

    public struct TestEntityGuid : IEquatable<TestEntityGuid>, IComparable<TestEntityGuid>
    {
        public ulong a;
        public ulong b;

        public bool Equals(TestEntityGuid other)
        {
            return a == other.a && b == other.b;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (a.GetHashCode() * 397) ^ b.GetHashCode();
            }
        }

        public int CompareTo(TestEntityGuid other)
        {
            var aComparison = a.CompareTo(other.a);
            if (aComparison != 0) return aComparison;
            return b.CompareTo(other.b);
        }
    }

    [Test]
    public void NativeParallelHashMap_GetKeysGuid()
    {
        var hashMap = new NativeParallelHashMap<TestEntityGuid, int>(1, Allocator.Temp);
        for (int i = 0; i < 30; ++i)
        {
            var didAdd = hashMap.TryAdd(new TestEntityGuid() { a = (ulong)i * 5, b = 3 * (ulong)i }, 2 * i);
            Assert.IsTrue(didAdd);
        }

        // Validate Hashtable has all the expected values
        ExpectedCount(ref hashMap, 30);
        for (int i = 0; i < 30; ++i)
        {
            int output;
            var exists = hashMap.TryGetValue(new TestEntityGuid() { a = (ulong)i * 5, b = 3 * (ulong)i }, out output);
            Assert.IsTrue(exists);
            Assert.AreEqual(2 * i, output);
        }

        // Validate keys array
        var keys = hashMap.GetKeyArray(Allocator.Temp);
        Assert.AreEqual(30, keys.Length);

        keys.Sort();
        for (int i = 0; i < 30; ++i)
        {
            Assert.AreEqual(new TestEntityGuid() { a = (ulong)i * 5, b = 3 * (ulong)i }, keys[i]);
        }

        hashMap.Dispose();
        keys.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_IndexerWorks()
    {
        var hashMap = new NativeParallelHashMap<int, int>(1, Allocator.Temp);
        hashMap[5] = 7;
        Assert.AreEqual(7, hashMap[5]);

        hashMap[5] = 9;
        Assert.AreEqual(9, hashMap[5]);

        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_ContainsKeyHashMap()
    {
        var hashMap = new NativeParallelHashMap<int, int>(1, Allocator.Temp);
        hashMap[5] = 7;

        Assert.IsTrue(hashMap.ContainsKey(5));
        Assert.IsFalse(hashMap.ContainsKey(6));

        hashMap.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_NativeKeyValueArrays_DisposeJob()
    {
        var container = new NativeParallelHashMap<int, int>(1, Allocator.Persistent);
        Assert.True(container.IsCreated);
        Assert.DoesNotThrow(() => { container[0] = 0; });
        Assert.DoesNotThrow(() => { container[1] = 1; });
        Assert.DoesNotThrow(() => { container[2] = 2; });
        Assert.DoesNotThrow(() => { container[3] = 3; });

        var kv = container.GetKeyValueArrays(Allocator.Persistent);

        var disposeJob = container.Dispose(default);
        Assert.False(container.IsCreated);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        Assert.Throws<ObjectDisposedException>(
            () => { container[0] = 2; });
#endif

        kv.Dispose(disposeJob);

        disposeJob.Complete();
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeParallelHashMap_UseAfterFree_UsesCustomOwnerTypeName()
    {
        var container = new NativeParallelHashMap<int, int>(10, CommonRwdAllocator.Handle);
        container[0] = 123;
        container.Dispose();
        NUnit.Framework.Assert.That(() => container[0],
            Throws.Exception.TypeOf<ObjectDisposedException>()
                .With.Message.Contains($"The {container.GetType()} has been deallocated"));
    }

    [BurstCompile(CompileSynchronously = true)]
    struct NativeParallelHashMap_CreateAndUseAfterFreeBurst : IJob
    {
        public void Execute()
        {
            var container = new NativeParallelHashMap<int, int>(10, Allocator.Temp);
            container[0] = 17;
            container.Dispose();
            container[1] = 42;
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeParallelHashMap_CreateAndUseAfterFreeInBurstJob_UsesCustomOwnerTypeName()
    {
        // Make sure this isn't the first container of this type ever created, so that valid static safety data exists
        var container = new NativeParallelHashMap<int, int>(10, CommonRwdAllocator.Handle);
        container.Dispose();

        var job = new NativeParallelHashMap_CreateAndUseAfterFreeBurst
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

    [Test]
    public void NativeParallelHashMap_ForEach_FixedStringInHashMap()
    {
        using (var stringList = new NativeList<FixedString32Bytes>(10, Allocator.Persistent) { "Hello", ",", "World", "!" })
        {
            var seen = new NativeArray<int>(stringList.Length, Allocator.Temp);
            var container = new NativeParallelHashMap<FixedString128Bytes, float>(50, Allocator.Temp);
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
    public void NativeParallelHashMap_EnumeratorDoesNotReturnRemovedElementsTest()
    {
        NativeParallelHashMap<int, int> container = new NativeParallelHashMap<int, int>(5, Allocator.Temp);
        for (int i = 0; i < 5; i++)
        {
            container.Add(i, i);
        }

        int elementToRemove = 2;
        container.Remove(elementToRemove);

        using (var enumerator = container.GetEnumerator())
        {
            while (enumerator.MoveNext())
            {
                Assert.AreNotEqual(elementToRemove, enumerator.Current.Key);
            }
        }

        container.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_EnumeratorInfiniteIterationTest()
    {
        NativeParallelHashMap<int, int> container = new NativeParallelHashMap<int, int>(5, Allocator.Temp);
        for (int i = 0; i < 5; i++)
        {
            container.Add(i, i);
        }

        for (int i = 0; i < 2; i++)
        {
            container.Remove(i);
        }

        var expected = container.Count();
        int count = 0;
        using (var enumerator = container.GetEnumerator())
        {
            while (enumerator.MoveNext())
            {
                if (count++ > expected)
                {
                    break;
                }
            }
        }

        Assert.AreEqual(expected, count);
        container.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_ForEach([Values(10, 1000)]int n)
    {
        var seen = new NativeArray<int>(n, Allocator.Temp);
        using (var container = new NativeParallelHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < n; i++)
            {
                container.Add(i, i * 37);
            }

            var count = 0;
            foreach (var kv in container)
            {
                int value;
                Assert.True(container.TryGetValue(kv.Key, out value));
                Assert.AreEqual(value, kv.Value);
                Assert.AreEqual(kv.Key * 37, kv.Value);

                seen[kv.Key] = seen[kv.Key] + 1;
                ++count;
            }

            Assert.AreEqual(container.Count(), count);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect key count {i}");
            }
        }
    }

    struct NativeParallelHashMap_ForEach_Job : IJob
    {
        public NativeParallelHashMap<int, int>.ReadOnly Input;

        [ReadOnly]
        public int Num;

        public void Execute()
        {
            var seen = new NativeArray<int>(Num, Allocator.Temp);

            var count = 0;
            foreach (var kv in Input)
            {
                int value;
                Assert.True(Input.TryGetValue(kv.Key, out value));
                Assert.AreEqual(value, kv.Value);
                Assert.AreEqual(kv.Key * 37, kv.Value);

                seen[kv.Key] = seen[kv.Key] + 1;
                ++count;
            }

            Assert.AreEqual(Input.Count(), count);
            for (int i = 0; i < Num; i++)
            {
                Assert.AreEqual(1, seen[i], $"Incorrect key count {i}");
            }

            seen.Dispose();
        }
    }

    [Test]
    public void NativeParallelHashMap_ForEach_From_Job([Values(10, 1000)] int n)
    {
        var seen = new NativeArray<int>(n, Allocator.Temp);
        using (var container = new NativeParallelHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            for (int i = 0; i < n; i++)
            {
                container.Add(i, i * 37);
            }

            new NativeParallelHashMap_ForEach_Job
            {
                Input = container.AsReadOnly(),
                Num = n,

            }.Run();
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeParallelHashMap_ForEach_Throws_When_Modified()
    {
        using (var container = new NativeParallelHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            container.Add(0, 012);
            container.Add(1, 123);
            container.Add(2, 234);
            container.Add(3, 345);
            container.Add(4, 456);
            container.Add(5, 567);
            container.Add(6, 678);
            container.Add(7, 789);
            container.Add(8, 890);
            container.Add(9, 901);

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

    struct NativeParallelHashMap_ForEachIterator : IJob
    {
        [ReadOnly]
        public NativeParallelHashMap<int, int>.Enumerator Iter;

        public void Execute()
        {
            while (Iter.MoveNext())
            {
            }
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeParallelHashMap_ForEach_Throws_Job_Iterator()
    {
        using (var container = new NativeParallelHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            var jobHandle = new NativeParallelHashMap_ForEachIterator
            {
                Iter = container.GetEnumerator()

            }.Schedule();

            Assert.Throws<InvalidOperationException>(() => { container.Add(1, 1); });

            jobHandle.Complete();
        }
    }

    struct ParallelWriteToHashMapJob : IJobParallelFor
    {
        [WriteOnly]
        public NativeParallelHashMap<int, int>.ParallelWriter Writer;

        public void Execute(int index)
        {
            Writer.TryAdd(index, 0);
        }
    }

    [Test]
    [TestRequiresCollectionChecks]
    public void NativeParallelHashMap_ForEach_Throws()
    {
        using (var container = new NativeParallelHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            var iter = container.GetEnumerator();

            var jobHandle = new ParallelWriteToHashMapJob
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
    public unsafe void NativeParallelHashMap_GetUnsafeBucketData()
    {
        using (var container = new NativeParallelHashMap<int, int>(32, CommonRwdAllocator.Handle))
        {
            container.Add(0, 012);
            container.Add(1, 123);
            container.Add(2, 234);
            container.Add(3, 345);
            container.Add(4, 456);
            container.Add(5, 567);
            container.Add(6, 678);
            container.Add(7, 789);
            container.Add(8, 890);
            container.Add(9, 901);

            var bucketData = container.GetUnsafeBucketData();

            var buckets = (int*)bucketData.buckets;
            var nextPtrs = (int*)bucketData.next;
            var keys = bucketData.keys;
            var values = bucketData.values;

            var other = new NativeParallelHashMap<int, int>(32, CommonRwdAllocator.Handle);

            for (int i = 0, count = container.Count(); i < count; i++)
            {
                int entryIndex = buckets[i];

                while (entryIndex != -1)
                {
                    var bdKey = UnsafeUtility.ReadArrayElement<int>(keys, entryIndex);
                    var bdValue = UnsafeUtility.ArrayElementAsRef<int>(values, entryIndex);

                    other.Add(bdKey, bdValue);

                    entryIndex = nextPtrs[entryIndex];
                }
            }

            Assert.AreEqual(container.Count(), other.Count());

            var kvArray = container.GetKeyValueArrays(CommonRwdAllocator.Handle);

            for (int i = 0, count = kvArray.Length; i < count; i++)
            {
                int value;
                Assert.True(other.TryGetValue(kvArray.Keys[i], out value));
                Assert.AreEqual(value, kvArray.Values[i]);
            }

            kvArray.Dispose();
            other.Dispose();
        }
    }

    [Test]
    public void NativeParallelHashMap_CustomAllocatorTest()
    {
        AllocatorManager.Initialize();
        var allocatorHelper = new AllocatorHelper<CustomAllocatorTests.CountingAllocator>(AllocatorManager.Persistent);
        ref var allocator = ref allocatorHelper.Allocator;
        allocator.Initialize();

        using (var container = new NativeParallelHashMap<int, int>(1, allocator.Handle))
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
                using (var container = new NativeParallelHashMap<int, int>(1, Allocator->Handle))
                {
                }
            }
        }
    }

    [Test]
    public unsafe void NativeParallelHashMap_BurstedCustomAllocatorTest()
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
        public NativeParallelHashMap<int, int> map;
    }

    [Test]
    public void NativeParallelHashMap_Nested()
    {
        var mapInner = new NativeParallelHashMap<int, int>(16, CommonRwdAllocator.Handle);
        NestedHashMap mapStruct = new NestedHashMap { map = mapInner };

        var mapNestedStruct = new NativeParallelHashMap<int, NestedHashMap>(16, CommonRwdAllocator.Handle);
        var mapNested = new NativeParallelHashMap<int, NativeParallelHashMap<int, int>>(16, CommonRwdAllocator.Handle);

        mapNested[14] = mapInner;
        mapNestedStruct[17] = mapStruct;

        mapNested.Dispose();
        mapNestedStruct.Dispose();
        mapInner.Dispose();
    }

    [Test]
    public void NativeParallelHashMap_IndexerAdd_ResizesContainer()
    {
        var container = new NativeParallelHashMap<int, int>(8, Allocator.Persistent);
        for (int i = 0; i < 1024; i++)
        {
            container[i] = i;
        }
        Assert.AreEqual(1024, container.Count());
        container.Dispose();
    }
}
