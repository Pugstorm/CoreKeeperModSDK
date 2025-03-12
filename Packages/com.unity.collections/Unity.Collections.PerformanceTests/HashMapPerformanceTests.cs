using NUnit.Framework;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Benchmark;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Mathematics;
using UnityEditor;
using Random = Unity.Mathematics.Random;

namespace Unity.Collections.PerformanceTests
{
    static class HashMapUtil
    {
        internal const uint K_RANDOM_SEED_1 = 2210602657;
        internal const uint K_RANDOM_SEED_2 = 2210602658;
        internal const uint K_RANDOM_SEED_3 = 2210602659;
        static public void AllocInt(ref NativeHashMap<int, int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Unity.Mathematics.Random random = new Mathematics.Random(K_RANDOM_SEED_1);
                container = new NativeHashMap<int, int>(capacity, Allocator.Persistent);
                if (addValues)
                {
                    int keysAdded = 0;

                    while (keysAdded < capacity)
                    {
                        int randKey = random.NextInt();
                        if (container.TryAdd(randKey, keysAdded))
                        {
                            ++keysAdded;
                        }
                    }
                }
            }
            else
                container.Dispose();
        }
        static public void AllocInt(ref UnsafeHashMap<int, int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Unity.Mathematics.Random random = new Mathematics.Random(K_RANDOM_SEED_1);
                container = new UnsafeHashMap<int, int>(capacity, Allocator.Persistent);
                if (addValues)
                {
                    int keysAdded = 0;

                    while (keysAdded < capacity)
                    {
                        int randKey = random.NextInt();
                        if (container.TryAdd(randKey, keysAdded))
                        {
                            ++keysAdded;
                        }
                    }
                }
            }
            else
                container.Dispose();
        }
        static public object AllocBclContainer(int capacity, bool addValues)
        {
            if (capacity < 0)
                return null;

            Unity.Mathematics.Random random = new Mathematics.Random(K_RANDOM_SEED_1);
            var bclContainer = new System.Collections.Generic.Dictionary<int, int>(capacity);
            if (addValues)
            {
                int keysAdded = 0;

                while (keysAdded < capacity)
                {
                    int randKey = random.NextInt();
                    if (bclContainer.TryAdd(randKey, keysAdded))
                    {
                        ++keysAdded;
                    }
                }
            }
            return bclContainer;
        }
        static public void CreateRandomKeys(int capacity, ref UnsafeList<int> keys)
        {
            if (capacity >= 0)
            {
                keys = new UnsafeList<int>(capacity, Allocator.Persistent);
                using (UnsafeHashSet<int> randomFilter = new UnsafeHashSet<int>(capacity, Allocator.Persistent))
                {
                    Unity.Mathematics.Random random = new Mathematics.Random(K_RANDOM_SEED_2);
                    int keysAdded = 0;

                    while (keysAdded < capacity)
                    {
                        int randKey = random.NextInt();
                        if (randomFilter.Add(randKey))
                        {
                            keys.Add(randKey);
                            ++keysAdded;
                        }
                    }
                }

            }
            else
                keys.Dispose();
        }

        static public void CreateRandomKeys(int capacity, ref UnsafeList<int> keys, ref UnsafeHashMap<int, int> hashMap)
        {
            if (capacity >= 0)
            {
                keys = new UnsafeList<int>(capacity, Allocator.Persistent);
                using (UnsafeHashSet<int> randomFilter = new UnsafeHashSet<int>(capacity, Allocator.Persistent))
                {
                    Unity.Mathematics.Random random = new Mathematics.Random(K_RANDOM_SEED_2);
                    int keysAdded = 0;

                    while (keysAdded < capacity)
                    {
                        int randKey = random.NextInt();
                        if (randomFilter.Add(randKey) && !hashMap.ContainsKey(randKey))
                        {
                            keys.Add(randKey);
                            ++keysAdded;
                        }
                    }
                }

            }
            else
                keys.Dispose();
        }

        static public void CreateRandomKeys(int capacity, ref UnsafeList<int> keys, ref System.Collections.Generic.Dictionary<int, int> hashMap)
        {
            if (capacity >= 0)
            {
                keys = new UnsafeList<int>(capacity, Allocator.Persistent);
                using (UnsafeHashSet<int> randomFilter = new UnsafeHashSet<int>(capacity, Allocator.Persistent))
                {
                    Unity.Mathematics.Random random = new Mathematics.Random(K_RANDOM_SEED_2);
                    int keysAdded = 0;

                    while (keysAdded < capacity)
                    {
                        int randKey = random.NextInt();
                        if (randomFilter.Add(randKey) && !hashMap.ContainsKey(randKey))
                        {
                            keys.Add(randKey);
                            ++keysAdded;
                        }
                    }
                }

            }
            else
                keys.Dispose();
        }

        static public void CreateRandomKeys(int capacity, ref UnsafeList<int> keys, ref NativeHashMap<int, int> hashMap)
        {
            if (capacity >= 0)
            {
                keys = new UnsafeList<int>(capacity, Allocator.Persistent);
                using (UnsafeHashSet<int> randomFilter = new UnsafeHashSet<int>(capacity, Allocator.Persistent))
                {
                    Unity.Mathematics.Random random = new Mathematics.Random(K_RANDOM_SEED_2);
                    int keysAdded = 0;

                    while (keysAdded < capacity)
                    {
                        int randKey = random.NextInt();
                        if (randomFilter.Add(randKey) && !hashMap.ContainsKey(randKey))
                        {
                            keys.Add(randKey);
                            ++keysAdded;
                        }
                    }
                }

            }
            else
                keys.Dispose();
        }

        static public void RandomlyShuffleKeys(int capacity, ref UnsafeList<int> keys)
        {
            if (capacity >= 0)
            {
                Unity.Mathematics.Random random = new Mathematics.Random(K_RANDOM_SEED_3);
                for (int i = 0; i < capacity; i++)
                {
                    int keyAt = keys[i];
                    int randomIndex = random.NextInt(0, capacity - 1);
                    keys[i] = keys[randomIndex];
                    keys[randomIndex] = keyAt;
                }
            }
        }
    }

    struct HashMapIsEmpty100k : IBenchmarkContainer
    {
        const int kIterations = 100_000;
        NativeHashMap<int, int> nativeContainer;
        UnsafeHashMap<int, int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => HashMapUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashMapUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => HashMapUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer()
        {
            var reader = nativeContainer.AsReadOnly();
            for (int i = 0; i < kIterations; i++)
                _ = reader.IsEmpty;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureUnsafeContainer()
        {
            var reader = unsafeContainer.AsReadOnly();
            for (int i = 0; i < kIterations; i++)
                _ = reader.IsEmpty;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            for (int i = 0; i < kIterations; i++)
                _ = bclContainer.Count == 0;
        }
    }

    struct HashMapCount100k : IBenchmarkContainer
    {
        const int kIterations = 100_000;
        NativeHashMap<int, int> nativeContainer;
        UnsafeHashMap<int, int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => HashMapUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashMapUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => HashMapUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer()
        {
            var reader = nativeContainer.AsReadOnly();
            for (int i = 0; i < kIterations; i++)
                _ = reader.Count;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureUnsafeContainer()
        {
            var reader = unsafeContainer.AsReadOnly();
            for (int i = 0; i < kIterations; i++)
                _ = reader.Count;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            for (int i = 0; i < kIterations; i++)
                _ = bclContainer.Count;
        }
    }

    struct HashMapToNativeArrayKeys : IBenchmarkContainer
    {
        NativeHashMap<int, int> nativeContainer;
        UnsafeHashMap<int, int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => HashMapUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashMapUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => HashMapUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer()
        {
            var asArray = nativeContainer.GetKeyArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureUnsafeContainer()
        {
            var asArray = unsafeContainer.GetKeyArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            int[] asArray = new int[bclContainer.Count];
            bclContainer.Keys.CopyTo(asArray, 0);
        }
    }

    struct HashMapToNativeArrayValues : IBenchmarkContainer
    {
        NativeHashMap<int, int> nativeContainer;
        UnsafeHashMap<int, int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => HashMapUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashMapUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => HashMapUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer()
        {
            var asArray = nativeContainer.GetValueArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureUnsafeContainer()
        {
            var asArray = unsafeContainer.GetValueArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            int[] asArray = new int[bclContainer.Count];
            bclContainer.Values.CopyTo(asArray, 0);
        }
    }

    struct HashMapInsert : IBenchmarkContainer
    {
        int capacity;
        NativeHashMap<int, int> nativeContainer;
        UnsafeHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args) => this.capacity = capacity;

        public void AllocNativeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref nativeContainer, capacity, false);
            HashMapUtil.CreateRandomKeys(capacity, ref keys, ref nativeContainer);
        }

        public void AllocUnsafeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref unsafeContainer, capacity, false);
            HashMapUtil.CreateRandomKeys(capacity, ref keys, ref unsafeContainer);
        }

        public object AllocBclContainer(int capacity)
        {
            object container = HashMapUtil.AllocBclContainer(capacity, false);
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            HashMapUtil.CreateRandomKeys(capacity, ref keys, ref bclContainer);
            return container;
        }

        public void MeasureNativeContainer()
        {
            for (int i = 0; i < capacity; i++)
                nativeContainer.Add(keys[i], i);
        }
        public void MeasureUnsafeContainer()
        {
            for (int i = 0; i < capacity; i++)
                unsafeContainer.Add(keys[i], i);
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            for (int i = 0; i < capacity; i++)
                bclContainer.Add(keys[i], i);
        }
    }

    struct HashMapAddGrow : IBenchmarkContainer
    {
        int capacity;
        int toAdd;
        NativeHashMap<int, int> nativeContainer;
        UnsafeHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args)
        {
            this.capacity = capacity;
            toAdd = args[0];
        }

        public void AllocNativeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref nativeContainer, capacity, true);
            int toAddCount = capacity < 0 ? -1 : toAdd;
            HashMapUtil.CreateRandomKeys(toAddCount, ref keys, ref nativeContainer);
        }

        public void AllocUnsafeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref unsafeContainer, capacity, true);
            int toAddCount = capacity < 0 ? -1 : toAdd;
            HashMapUtil.CreateRandomKeys(toAddCount, ref keys, ref unsafeContainer);
        }

        public object AllocBclContainer(int capacity)
        {
            object container = HashMapUtil.AllocBclContainer(capacity, true);
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            int toAddCount = capacity < 0 ? -1 : toAdd;
            HashMapUtil.CreateRandomKeys(toAddCount, ref keys, ref bclContainer);
            return container;
        }

        public void MeasureNativeContainer()
        {
            // Intentionally setting capacity small and growing by adding more items
            for (int i = 0; i < toAdd; i++)
                nativeContainer.Add(keys[i], i);
        }
        public void MeasureUnsafeContainer()
        {
            // Intentionally setting capacity small and growing by adding more items
            for (int i = 0; i < toAdd; i++)
                unsafeContainer.Add(keys[i], i);
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            // Intentionally setting capacity small and growing by adding more items
            for (int i = 0; i < toAdd; i++)
                bclContainer.Add(keys[i], i);
        }
    }

    struct HashMapContains : IBenchmarkContainer
    {
        int capacity;
        NativeHashMap<int, int> nativeContainer;
        UnsafeHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;
        void IBenchmarkContainer.SetParams(int capacity, params int[] args) => this.capacity = capacity;

        public void AllocNativeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref nativeContainer, capacity, false);
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref unsafeContainer, capacity, false);
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = HashMapUtil.AllocBclContainer(capacity, false);
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
            return container;
        }

        public void MeasureNativeContainer()
        {
            var reader = nativeContainer.AsReadOnly();
            bool data = false;
            for (int i = 0; i < capacity; i++)
                Volatile.Write(ref data, reader.ContainsKey(keys[i]));
        }
        public void MeasureUnsafeContainer()
        {
            var reader = unsafeContainer.AsReadOnly();
            bool data = false;
            for (int i = 0; i < capacity; i++)
                Volatile.Write(ref data, reader.ContainsKey(keys[i]));
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            bool data = false;
            for (int i = 0; i < capacity; i++)
                Volatile.Write(ref data, bclContainer.ContainsKey(keys[i]));
        }
    }

    struct HashMapIndexedRead : IBenchmarkContainer
    {
        NativeHashMap<int, int> nativeContainer;
        UnsafeHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        public void AllocNativeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref nativeContainer, capacity, false);
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref unsafeContainer, capacity, false);
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = HashMapUtil.AllocBclContainer(capacity, false);
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
            return container;
        }

        public void MeasureNativeContainer()
        {
            var reader = nativeContainer.AsReadOnly();
            int insertions = keys.Length;
            int value = 0;
            for (int i = 0; i < insertions; i++)
                Volatile.Write(ref value, reader[keys[i]]);
        }
        public void MeasureUnsafeContainer()
        {
            var reader = unsafeContainer.AsReadOnly();
            int insertions = keys.Length;
            int value = 0;
            for (int i = 0; i < insertions; i++)
                Volatile.Write(ref value, reader[keys[i]]);
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            int insertions = keys.Length;
            int value = 0;
            for (int i = 0; i < insertions; i++)
                Volatile.Write(ref value, bclContainer[keys[i]]);
        }
    }

    struct HashMapIndexedWrite : IBenchmarkContainer
    {
        NativeHashMap<int, int> nativeContainer;
        UnsafeHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        public void AllocNativeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref nativeContainer, capacity, false);
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref unsafeContainer, capacity, false);
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public object AllocBclContainer(int capacity)
        {
            var container = HashMapUtil.AllocBclContainer(capacity, false);
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
            return container;
        }

        public void MeasureNativeContainer()
        {
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                nativeContainer[keys[i]] = i;
        }
        public void MeasureUnsafeContainer()
        {
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                unsafeContainer[keys[i]] = i;
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                bclContainer[keys[i]] = i;
        }
    }

    struct HashMapTryGetValue : IBenchmarkContainer
    {
        NativeHashMap<int, int> nativeContainer;
        UnsafeHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        public void AllocNativeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref nativeContainer, capacity, false);
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref unsafeContainer, capacity, false);
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = HashMapUtil.AllocBclContainer(capacity, false);
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
            return container;
        }

        public void MeasureNativeContainer()
        {
            var reader = nativeContainer.AsReadOnly();
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
            {
                reader.TryGetValue(keys[i], out var value);
                Volatile.Read(ref value);
            }
        }
        public void MeasureUnsafeContainer()
        {
            var reader = unsafeContainer.AsReadOnly();
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
            {
                reader.TryGetValue(keys[i], out var value);
                Volatile.Read(ref value);
            }
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
            {
                bclContainer.TryGetValue(keys[i], out var value);
                Volatile.Read(ref value);
            }
        }
    }

    struct HashMapRemove : IBenchmarkContainer
    {
        NativeHashMap<int, int> nativeContainer;
        UnsafeHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        public void AllocNativeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref nativeContainer, capacity, false);
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            HashMapUtil.AllocInt(ref unsafeContainer, capacity, false);
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = HashMapUtil.AllocBclContainer(capacity, false);
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            HashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.TryAdd(keys[i], i);
            HashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
            return container;
        }

        public void MeasureNativeContainer()
        {
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                nativeContainer.Remove(keys[i]);
        }
        public void MeasureUnsafeContainer()
        {
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                unsafeContainer.Remove(keys[i]);
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                bclContainer.Remove(keys[i]);
        }
    }

    struct HashMapForEach : IBenchmarkContainer
    {
        NativeHashMap<int, int> nativeContainer;
        UnsafeHashMap<int, int> unsafeContainer;
        public int total;

        public void AllocNativeContainer(int capacity) => HashMapUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashMapUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => HashMapUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer()
        {
            foreach (var pair in nativeContainer)
                Volatile.Read(ref pair.Value);
        }
        public void MeasureUnsafeContainer()
        {
            foreach (var pair in unsafeContainer)
                Volatile.Read(ref pair.Value);
        }
        public void MeasureBclContainer(object container)
        {
            int value = 0;
            var bclContainer = (System.Collections.Generic.Dictionary<int, int>)container;
            foreach (var pair in bclContainer)
                Volatile.Write(ref value, pair.Value);
        }
    }


    [Benchmark(typeof(BenchmarkContainerType))]
    [BenchmarkNameOverride(BenchmarkContainerConfig.BCL, "Dictionary")]
    class HashMap
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem(BenchmarkContainerConfig.kMenuItemIndividual + nameof(HashMap))]
        static void RunIndividual()
            => BenchmarkContainerConfig.RunBenchmark(typeof(HashMap));
#endif

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IsEmpty_x_100k(
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashMapIsEmpty100k>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Count_x_100k(
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashMapCount100k>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void ToNativeArrayKeys(
            [Values(10000, 100000, 1000000)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashMapToNativeArrayKeys>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void ToNativeArrayValues(
            [Values(10000, 100000, 1000000)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashMapToNativeArrayValues>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Insert(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashMapInsert>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        [BenchmarkTestFootnote("Incrementally grows from `capacity` until reaching size of `growTo`")]
        public unsafe void AddGrow(
            [Values(4, 65536)] int capacity,
            [Values(1024 * 1024)] int growTo,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashMapAddGrow>.Run(capacity, type, growTo);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Contains(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashMapContains>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IndexedRead(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashMapIndexedRead>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IndexedWrite(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashMapIndexedWrite>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void TryGetValue(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashMapTryGetValue>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Remove(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashMapRemove>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Foreach(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashMapForEach>.Run(insertions, type);
        }
    }
}
