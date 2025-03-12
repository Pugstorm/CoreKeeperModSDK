using NUnit.Framework;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Benchmark;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Unity.Collections.PerformanceTests
{
    static class ParallelHashMapUtil
    {
        static public void AllocInt(ref NativeParallelHashMap<int, int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Unity.Mathematics.Random random = new Unity.Mathematics.Random(HashMapUtil.K_RANDOM_SEED_1);
                container = new NativeParallelHashMap<int, int>(capacity, Allocator.Persistent);
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
        static public void AllocInt(ref UnsafeParallelHashMap<int, int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Unity.Mathematics.Random random = new Unity.Mathematics.Random(HashMapUtil.K_RANDOM_SEED_1);
                container = new UnsafeParallelHashMap<int, int>(capacity, Allocator.Persistent);
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

            Unity.Mathematics.Random random = new Unity.Mathematics.Random(HashMapUtil.K_RANDOM_SEED_1);

            // FROM MICROSOFT DOCUMENTATION
            // The higher the concurrencyLevel, the higher the theoretical number of operations
            // that could be performed concurrently on the ConcurrentDictionary.  However, global
            // operations like resizing the dictionary take longer as the concurrencyLevel rises.
            // For the purposes of this example, we'll compromise at numCores * 2.
            var bclContainer = new System.Collections.Concurrent.ConcurrentDictionary<int, int>(System.Environment.ProcessorCount * 2, capacity);

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
                    Unity.Mathematics.Random random = new Unity.Mathematics.Random(HashMapUtil.K_RANDOM_SEED_2);
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

        static public void CreateRandomKeys(int capacity, ref UnsafeList<int> keys, ref UnsafeParallelHashMap<int, int> hashMap)
        {
            if (capacity >= 0)
            {
                keys = new UnsafeList<int>(capacity, Allocator.Persistent);
                using (UnsafeHashSet<int> randomFilter = new UnsafeHashSet<int>(capacity, Allocator.Persistent))
                {
                    Unity.Mathematics.Random random = new Unity.Mathematics.Random(HashMapUtil.K_RANDOM_SEED_2);
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

        static public void CreateRandomKeys(int capacity, ref UnsafeList<int> keys, ref System.Collections.Concurrent.ConcurrentDictionary<int, int> hashMap)
        {
            if (capacity >= 0)
            {
                keys = new UnsafeList<int>(capacity, Allocator.Persistent);
                using (UnsafeHashSet<int> randomFilter = new UnsafeHashSet<int>(capacity, Allocator.Persistent))
                {
                    Unity.Mathematics.Random random = new Unity.Mathematics.Random(HashMapUtil.K_RANDOM_SEED_2);
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

        static public void CreateRandomKeys(int capacity, ref UnsafeList<int> keys, ref NativeParallelHashMap<int, int> hashMap)
        {
            if (capacity >= 0)
            {
                keys = new UnsafeList<int>(capacity, Allocator.Persistent);
                using (UnsafeHashSet<int> randomFilter = new UnsafeHashSet<int>(capacity, Allocator.Persistent))
                {
                    Unity.Mathematics.Random random = new Unity.Mathematics.Random(HashMapUtil.K_RANDOM_SEED_2);
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

        static public void RandomlyShuffleKeys(int capacity, ref UnsafeList<int> keys)
        {
            if (capacity >= 0)
            {
                Unity.Mathematics.Random random = new Mathematics.Random(HashMapUtil.K_RANDOM_SEED_3);
                for (int i = 0; i < capacity; i++)
                {
                    int keyAt = keys[i];
                    int randomIndex = random.NextInt(0, capacity - 1);
                    keys[i] = keys[randomIndex];
                    keys[randomIndex] = keyAt;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public void SplitForWorkers(int count, int worker, int workers, out int startInclusive, out int endExclusive)
        {
            startInclusive = count * worker / workers;
            endExclusive = count * (worker + 1) / workers;
        }
    }

    struct ParallelHashMapIsEmpty100k : IBenchmarkContainerParallel
    {
        const int kIterations = 100_000;
        int workers;
        NativeParallelHashMap<int, int> nativeContainer;
        UnsafeParallelHashMap<int, int> unsafeContainer;

        void IBenchmarkContainerParallel.SetParams(int capacity, params int[] args) => workers = args[0];
        public void AllocNativeContainer(int capacity) => ParallelHashMapUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashMapUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashMapUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var reader = nativeContainer.AsReadOnly();
            ParallelHashMapUtil.SplitForWorkers(kIterations, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                _ = reader.IsEmpty;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            ParallelHashMapUtil.SplitForWorkers(kIterations, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                _ = unsafeContainer.IsEmpty;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            ParallelHashMapUtil.SplitForWorkers(kIterations, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                _ = bclContainer.IsEmpty;
        }
    }

    struct ParallelHashMapCount100k : IBenchmarkContainerParallel
    {
        const int kIterations = 100_000;
        int workers;
        NativeParallelHashMap<int, int> nativeContainer;
        UnsafeParallelHashMap<int, int> unsafeContainer;

        void IBenchmarkContainerParallel.SetParams(int capacity, params int[] args) => workers = args[0];
        public void AllocNativeContainer(int capacity) => ParallelHashMapUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashMapUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashMapUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var reader = nativeContainer.AsReadOnly();
            ParallelHashMapUtil.SplitForWorkers(kIterations, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                _ = reader.Count();
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            ParallelHashMapUtil.SplitForWorkers(kIterations, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                _ = unsafeContainer.Count();
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            ParallelHashMapUtil.SplitForWorkers(kIterations, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                _ = bclContainer.Count;
        }
    }

    struct ParallelHashMapToNativeArrayKeys : IBenchmarkContainerParallel
    {
        NativeParallelHashMap<int, int> nativeContainer;
        UnsafeParallelHashMap<int, int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => ParallelHashMapUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashMapUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashMapUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var asArray = nativeContainer.GetKeyArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            var asArray = unsafeContainer.GetKeyArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            int[] asArray = new int[bclContainer.Count];
            bclContainer.Keys.CopyTo(asArray, 0);
        }
    }

    struct ParallelHashMapToNativeArrayValues : IBenchmarkContainerParallel
    {
        NativeParallelHashMap<int, int> nativeContainer;
        UnsafeParallelHashMap<int, int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => ParallelHashMapUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashMapUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashMapUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var asArray = nativeContainer.GetValueArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            var asArray = unsafeContainer.GetValueArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            int[] asArray = new int[bclContainer.Count];
            bclContainer.Values.CopyTo(asArray, 0);
        }
    }

    struct ParallelHashMapInsert : IBenchmarkContainerParallel
    {
        int capacity;
        int workers;
        NativeParallelHashMap<int, int> nativeContainer;
        UnsafeParallelHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        void IBenchmarkContainerParallel.SetParams(int capacity, params int[] args)
        {
            this.capacity = capacity;
            workers = args[0];
        }

        public void AllocNativeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref nativeContainer, capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
        }

        public void AllocUnsafeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref unsafeContainer, capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
        }

        public object AllocBclContainer(int capacity)
        {
            object container = ParallelHashMapUtil.AllocBclContainer(capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            return container;
        }

        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var writer = nativeContainer.AsParallelWriter();
            ParallelHashMapUtil.SplitForWorkers(capacity, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                writer.TryAdd(keys[i], i, threadIndex);
        }
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            var writer = unsafeContainer.AsParallelWriter();
            ParallelHashMapUtil.SplitForWorkers(capacity, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                writer.TryAdd(keys[i], i, threadIndex);
        }
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            ParallelHashMapUtil.SplitForWorkers(capacity, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                bclContainer.TryAdd(keys[i], i);
        }
    }

    struct ParallelHashMapAddGrow : IBenchmarkContainerParallel
    {
        int capacity;
        int toAdd;
        NativeParallelHashMap<int, int> nativeContainer;
        UnsafeParallelHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        void IBenchmarkContainerParallel.SetParams(int capacity, params int[] args)
        {
            this.capacity = capacity;
            toAdd = args[0] - capacity;
        }

        public void AllocNativeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref nativeContainer, capacity, true);
            int toAddCount = capacity < 0 ? -1 : toAdd;
            ParallelHashMapUtil.CreateRandomKeys(toAddCount, ref keys, ref nativeContainer);
        }

        public void AllocUnsafeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref unsafeContainer, capacity, true);
            int toAddCount = capacity < 0 ? -1 : toAdd;
            ParallelHashMapUtil.CreateRandomKeys(toAddCount, ref keys, ref unsafeContainer);
        }

        public object AllocBclContainer(int capacity)
        {
            object container = ParallelHashMapUtil.AllocBclContainer(capacity, true);
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            int toAddCount = capacity < 0 ? -1 : toAdd;
            ParallelHashMapUtil.CreateRandomKeys(toAddCount, ref keys, ref bclContainer);
            return container;
        }

        public void MeasureNativeContainer(int _, int __)
        {
            // Intentionally setting capacity small and growing by adding more items
            for (int i = 0; i < toAdd; i++)
                nativeContainer.Add(keys[i], i);
        }
        public void MeasureUnsafeContainer(int _, int __)
        {
            // Intentionally setting capacity small and growing by adding more items
            for (int i = 0; i < toAdd; i++)
                unsafeContainer.Add(keys[i], i);
        }
        public void MeasureBclContainer(object container, int _)
        {
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            // Intentionally setting capacity small and growing by adding more items
            for (int i = 0; i < toAdd; i++)
                bclContainer.TryAdd(keys[i], i);
        }
    }

    struct ParallelHashMapContains : IBenchmarkContainerParallel
    {
        int capacity;
        int workers;
        NativeParallelHashMap<int, int> nativeContainer;
        UnsafeParallelHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        void IBenchmarkContainerParallel.SetParams(int capacity, params int[] args)
        {
            this.capacity = capacity;
            workers = args[0];
        }

        public void AllocNativeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref nativeContainer, capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref unsafeContainer, capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = ParallelHashMapUtil.AllocBclContainer(capacity, false);
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
            return container;
        }

        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var reader = nativeContainer.AsReadOnly();
            ParallelHashMapUtil.SplitForWorkers(capacity, worker, workers, out int start, out int end);
            bool data = false;
            for (int i = start; i < end; i++)
                Volatile.Write(ref data, reader.ContainsKey(keys[i]));
        }
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            ParallelHashMapUtil.SplitForWorkers(capacity, worker, workers, out int start, out int end);
            bool data = false;
            for (int i = start; i < end; i++)
                Volatile.Write(ref data, unsafeContainer.ContainsKey(keys[i]));
        }
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            ParallelHashMapUtil.SplitForWorkers(capacity, worker, workers, out int start, out int end);
            bool data = false;
            for (int i = start; i < end; i++)
                Volatile.Write(ref data, bclContainer.ContainsKey(keys[i]));
        }
    }

    struct ParallelHashMapIndexedRead : IBenchmarkContainerParallel
    {
        NativeParallelHashMap<int, int> nativeContainer;
        UnsafeParallelHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        public void AllocNativeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref nativeContainer, capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref unsafeContainer, capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = ParallelHashMapUtil.AllocBclContainer(capacity, false);
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
            return container;
        }

        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var reader = nativeContainer.AsReadOnly();
            int insertions = keys.Length;
            int value = 0;
            for (int i = 0; i < insertions; i++)
                Volatile.Write(ref value, reader[keys[i]]);
        }
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            int insertions = keys.Length;
            int value = 0;
            for (int i = 0; i < insertions; i++)
                Volatile.Write(ref value, unsafeContainer[keys[i]]);
        }
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            int insertions = keys.Length;
            int value = 0;
            for (int i = 0; i < insertions; i++)
                Volatile.Write(ref value, bclContainer[keys[i]]);
        }
    }

    struct ParallelHashMapIndexedWrite : IBenchmarkContainerParallel
    {
        NativeParallelHashMap<int, int> nativeContainer;
        UnsafeParallelHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        public void AllocNativeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref nativeContainer, capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref unsafeContainer, capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = ParallelHashMapUtil.AllocBclContainer(capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            for (int i = 0; i < capacity; i++)
                bclContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
            return container;
        }

        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                nativeContainer[keys[i]] = i;
        }
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                unsafeContainer[keys[i]] = i;
        }
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                bclContainer[keys[i]] = i;
        }
    }

    struct ParallelHashMapTryGetValue : IBenchmarkContainerParallel
    {
        int workers;
        NativeParallelHashMap<int, int> nativeContainer;
        UnsafeParallelHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        void IBenchmarkContainerParallel.SetParams(int capacity, params int[] args) => workers = args[0];

        public void AllocNativeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref nativeContainer, capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref unsafeContainer, capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = ParallelHashMapUtil.AllocBclContainer(capacity, false);
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
            return container;
        }

        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var reader = nativeContainer.AsReadOnly();
            ParallelHashMapUtil.SplitForWorkers(keys.Length, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
            {
                reader.TryGetValue(keys[i], out var value);
                Volatile.Read(ref value);
            }
        }
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            ParallelHashMapUtil.SplitForWorkers(keys.Length, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
            {
                unsafeContainer.TryGetValue(keys[i], out var value);
                Volatile.Read(ref value);
            }
        }
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            ParallelHashMapUtil.SplitForWorkers(keys.Length, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
            {
                bclContainer.TryGetValue(keys[i], out var value);
                Volatile.Read(ref value);
            }
        }
    }

    struct ParallelHashMapRemove : IBenchmarkContainerParallel
    {
        NativeParallelHashMap<int, int> nativeContainer;
        UnsafeParallelHashMap<int, int> unsafeContainer;
        UnsafeList<int> keys;

        public void AllocNativeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref nativeContainer, capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            ParallelHashMapUtil.AllocInt(ref unsafeContainer, capacity, false);
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = ParallelHashMapUtil.AllocBclContainer(capacity, false);
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            ParallelHashMapUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.TryAdd(keys[i], i);
            ParallelHashMapUtil.RandomlyShuffleKeys(capacity, ref keys);
            return container;
        }

        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                nativeContainer.Remove(keys[i]);
        }
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                unsafeContainer.Remove(keys[i]);
        }
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                bclContainer.TryRemove(keys[i], out _);
        }
    }

    struct ParallelHashMapForEach : IBenchmarkContainerParallel
    {
        NativeParallelHashMap<int, int> nativeContainer;
        UnsafeParallelHashMap<int, int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => ParallelHashMapUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashMapUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashMapUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer(int _, int __)
        {
            foreach (var pair in nativeContainer)
                Volatile.Read(ref pair.Value);
        }
        public void MeasureUnsafeContainer(int _, int __)
        {
            foreach (var pair in unsafeContainer)
                Volatile.Read(ref pair.Value);
        }
        public void MeasureBclContainer(object container, int _)
        {
            int value = 0;
            var bclContainer = (System.Collections.Concurrent.ConcurrentDictionary<int, int>)container;
            foreach (var pair in bclContainer)
                Volatile.Write(ref value, pair.Value);
        }
    }


    [Benchmark(typeof(BenchmarkContainerType))]
    [BenchmarkNameOverride(BenchmarkContainerConfig.BCL, "ConcurrentDictionary")]
    class ParallelHashMap
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem(BenchmarkContainerConfig.kMenuItemIndividual + nameof(ParallelHashMap))]
        static void RunIndividual()
            => BenchmarkContainerConfig.RunBenchmark(typeof(ParallelHashMap));
#endif

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IsEmpty_x_100k(
            [Values(1, 2, 4)] int workers,
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashMapIsEmpty100k>.Run(workers, capacity, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Count_x_100k(
            [Values(1, 2, 4)] int workers,
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashMapCount100k>.Run(workers, capacity, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void ToNativeArrayKeys(
            [Values(1)] int workers,
            [Values(10000, 100000, 1000000)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashMapToNativeArrayKeys>.Run(workers, capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void ToNativeArrayValues(
            [Values(1)] int workers,
            [Values(10000, 100000, 1000000)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashMapToNativeArrayValues>.Run(workers, capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Insert(
            [Values(1, 2, 4)] int workers,
#if UNITY_STANDALONE || UNITY_EDITOR
            [Values(10000, 100000, 1000000)] int insertions,
#else
            [Values(10000, 100000)] int insertions,  // Observe potential lower memory requirement on non-desktop platforms
#endif
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashMapInsert>.Run(workers, insertions, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        [BenchmarkTestFootnote("Incrementally grows from `capacity` until reaching size of `growTo`")]
        public unsafe void AddGrow(
            [Values(1)] int workers,  // Can't grow capacity in parallel
            [Values(4, 65536)] int capacity,
            [Values(1024 * 1024)] int growTo,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashMapAddGrow>.Run(workers, capacity, type, growTo);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Contains(
            [Values(1, 2, 4)] int workers,
#if UNITY_STANDALONE || UNITY_EDITOR
            [Values(10000, 100000, 1000000)] int insertions,
#else
            [Values(10000, 100000)] int insertions,  // Observe potential lower memory requirement on non-desktop platforms
#endif
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashMapContains>.Run(workers, insertions, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IndexedRead(
            [Values(1, 2, 4)] int workers,
#if UNITY_STANDALONE || UNITY_EDITOR
            [Values(10000, 100000, 1000000)] int insertions,
#else
            [Values(10000, 100000)] int insertions,  // Observe potential lower memory requirement on non-desktop platforms
#endif
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashMapIndexedRead>.Run(workers, insertions, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IndexedWrite(
            [Values(1)] int workers,  // Indexed write only available in single thread
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashMapIndexedWrite>.Run(workers, insertions, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void TryGetValue(
            [Values(1, 2, 4)] int workers,
#if UNITY_STANDALONE || UNITY_EDITOR
            [Values(10000, 100000, 1000000)] int insertions,
#else
            [Values(10000, 100000)] int insertions,  // Observe potential lower memory requirement on non-desktop platforms
#endif
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashMapTryGetValue>.Run(workers, insertions, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Remove(
            [Values(1)] int workers,  // No API for ParallelWriter.TryRemove currently
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashMapRemove>.Run(workers, insertions, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Foreach(
            [Values(1)] int workers,  // This work can't be split
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashMapForEach>.Run(workers, insertions, type, workers);
        }
    }
}
