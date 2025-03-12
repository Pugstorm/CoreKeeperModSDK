using NUnit.Framework;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Benchmark;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Collections.Generic;

namespace Unity.Collections.PerformanceTests
{
    static class HashSetUtil
    {
        static public void AllocInt(ref NativeHashSet<int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Random.InitState(0);
                container = new NativeHashSet<int>(capacity, Allocator.Persistent);
                if (addValues)
                {
                    for (int i = 0; i < capacity; i++)
                        container.Add(i);
                }
            }
            else
                container.Dispose();
        }
        static public void AllocInt(ref NativeHashSet<int> containerA, ref NativeHashSet<int> containerB, int capacity, bool addValues)
        {
            AllocInt(ref containerA, capacity, false);
            AllocInt(ref containerB, capacity, false);
            if (!addValues)
                return;
            for (int i = 0; i < capacity; i++)
            {
                containerA.Add(Random.Range(0, capacity * 2));
                containerB.Add(Random.Range(0, capacity * 2));
            }
        }
        static public void AllocInt(ref UnsafeHashSet<int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Random.InitState(0);
                container = new UnsafeHashSet<int>(capacity, Allocator.Persistent);
                if (addValues)
                {
                    for (int i = 0; i < capacity; i++)
                        container.Add(i);
                }
            }
            else
                container.Dispose();
        }
        static public void AllocInt(ref UnsafeHashSet<int> containerA, ref UnsafeHashSet<int> containerB, int capacity, bool addValues)
        {
            AllocInt(ref containerA, capacity, false);
            AllocInt(ref containerB, capacity, false);
            if (!addValues)
                return;
            for (int i = 0; i < capacity; i++)
            {
                containerA.Add(Random.Range(0, capacity * 2));
                containerB.Add(Random.Range(0, capacity * 2));
            }
        }
        static public object AllocBclContainer(int capacity, bool addValues)
        {
            if (capacity < 0)
                return null;

            Random.InitState(0);
            var bclContainer = new HashSet<int>(capacity);
            if (addValues)
            {
                for (int i = 0; i < capacity; i++)
                    bclContainer.Add(i);
            }
            return bclContainer;
        }
        static public object AllocBclContainerTuple(int capacity, bool addValues)
        {
            var tuple = new System.Tuple<HashSet<int>, HashSet<int>>(
                (HashSet<int>)AllocBclContainer(capacity, false),
                (HashSet<int>)AllocBclContainer(capacity, false));
            if (addValues)
            {
                for (int i = 0; i < capacity; i++)
                {
                    tuple.Item1.Add(Random.Range(0, capacity * 2));
                    tuple.Item2.Add(Random.Range(0, capacity * 2));
                }
            }
            return tuple;
        }
        static public void CreateRandomKeys(int capacity, ref UnsafeList<int> keys)
        {
            if (capacity >= 0)
            {
                keys = new UnsafeList<int>(capacity, Allocator.Persistent);
                Random.InitState(0);
                for (int i = 0; i < capacity; i++)
                {
                    int randKey = Random.Range(0, capacity);
                    keys.Add(randKey);
                }
            }
            else
                keys.Dispose();
        }

    }

    struct HashSetIsEmpty100k : IBenchmarkContainer
    {
        const int kIterations = 100_000;
        NativeHashSet<int> nativeContainer;
        UnsafeHashSet<int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => HashSetUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashSetUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => HashSetUtil.AllocBclContainer(capacity, true);

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
            var bclContainer = (HashSet<int>)container;
            for (int i = 0; i < kIterations; i++)
                _ = bclContainer.Count == 0;
        }
    }

    struct HashSetCount100k : IBenchmarkContainer
    {
        const int kIterations = 100_000;
        NativeHashSet<int> nativeContainer;
        UnsafeHashSet<int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => HashSetUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashSetUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => HashSetUtil.AllocBclContainer(capacity, true);

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
            var bclContainer = (HashSet<int>)container;
            for (int i = 0; i < kIterations; i++)
                _ = bclContainer.Count;
        }
    }

    struct HashSetToNativeArray : IBenchmarkContainer
    {
        NativeHashSet<int> nativeContainer;
        UnsafeHashSet<int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => HashSetUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashSetUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => HashSetUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer()
        {
            var asArray = nativeContainer.ToNativeArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureUnsafeContainer()
        {
            var asArray = unsafeContainer.ToNativeArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (HashSet<int>)container;
            int[] asArray = new int[bclContainer.Count];
            bclContainer.CopyTo(asArray, 0);
        }
    }

    struct HashSetInsert : IBenchmarkContainer
    {
        int capacity;
        NativeHashSet<int> nativeContainer;
        UnsafeHashSet<int> unsafeContainer;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args) => this.capacity = capacity;

        public void AllocNativeContainer(int capacity) => HashSetUtil.AllocInt(ref nativeContainer, capacity, false);
        public void AllocUnsafeContainer(int capacity) => HashSetUtil.AllocInt(ref unsafeContainer, capacity, false);
        public object AllocBclContainer(int capacity) => HashSetUtil.AllocBclContainer(capacity, false);

        public void MeasureNativeContainer()
        {
            for (int i = 0; i < capacity; i++)
                nativeContainer.Add(i);
        }
        public void MeasureUnsafeContainer()
        {
            for (int i = 0; i < capacity; i++)
                unsafeContainer.Add(i);
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (HashSet<int>)container;
            for (int i = 0; i < capacity; i++)
                bclContainer.Add(i);
        }
    }

    struct HashSetAddGrow : IBenchmarkContainer
    {
        int capacity;
        int toAdd;
        NativeHashSet<int> nativeContainer;
        UnsafeHashSet<int> unsafeContainer;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args)
        {
            this.capacity = capacity;
            toAdd = args[0];
        }

        public void AllocNativeContainer(int capacity) => HashSetUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashSetUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => HashSetUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer()
        {
            // Intentionally setting capacity small and growing by adding more items
            for (int i = capacity; i < capacity + toAdd; i++)
                nativeContainer.Add(i);
        }
        public void MeasureUnsafeContainer()
        {
            // Intentionally setting capacity small and growing by adding more items
            for (int i = capacity; i < capacity + toAdd; i++)
                unsafeContainer.Add(i);
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (HashSet<int>)container;
            // Intentionally setting capacity small and growing by adding more items
            for (int i = capacity; i < capacity + toAdd; i++)
                bclContainer.Add(i);
        }
    }

    struct HashSetContains : IBenchmarkContainer
    {
        int capacity;
        NativeHashSet<int> nativeContainer;
        UnsafeHashSet<int> unsafeContainer;
        UnsafeList<int> keys;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args) => this.capacity = capacity;

        public void AllocNativeContainer(int capacity)
        {
            HashSetUtil.AllocInt(ref nativeContainer, capacity, false);
            HashSetUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.Add(keys[i]);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            HashSetUtil.AllocInt(ref unsafeContainer, capacity, false);
            HashSetUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.Add(keys[i]);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = HashSetUtil.AllocBclContainer(capacity, false);
            var bclContainer = (HashSet<int>)container;
            HashSetUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.Add(keys[i]);
            return container;
        }

        public void MeasureNativeContainer()
        {
            var reader = nativeContainer.AsReadOnly();
            bool data = false;
            for (int i = 0; i < capacity; i++)
                Volatile.Write(ref data, reader.Contains(keys[i]));
        }
        public void MeasureUnsafeContainer()
        {
            var reader = unsafeContainer.AsReadOnly();
            bool data = false;
            for (int i = 0; i < capacity; i++)
                Volatile.Write(ref data, reader.Contains(keys[i]));
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (HashSet<int>)container;
            bool data = false;
            for (int i = 0; i < capacity; i++)
                Volatile.Write(ref data, bclContainer.Contains(keys[i]));
        }
    }

    struct HashSetRemove : IBenchmarkContainer
    {
        NativeHashSet<int> nativeContainer;
        UnsafeHashSet<int> unsafeContainer;
        UnsafeList<int> keys;

        public void AllocNativeContainer(int capacity)
        {
            HashSetUtil.AllocInt(ref nativeContainer, capacity, false);
            HashSetUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.Add(keys[i]);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            HashSetUtil.AllocInt(ref unsafeContainer, capacity, false);
            HashSetUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.Add(keys[i]);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = HashSetUtil.AllocBclContainer(capacity, false);
            var bclContainer = (HashSet<int>)container;
            HashSetUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.Add(keys[i]);
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
            var bclContainer = (HashSet<int>)container;
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                bclContainer.Remove(keys[i]);
        }
    }

    struct HashSetForEach : IBenchmarkContainer
    {
        NativeHashSet<int> nativeContainer;
        UnsafeHashSet<int> unsafeContainer;
        public int total;

        public void AllocNativeContainer(int capacity) => HashSetUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashSetUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => HashSetUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer()
        {
            int keep = 0;
            foreach (var value in nativeContainer)
                Volatile.Write(ref keep, value);
        }
        public void MeasureUnsafeContainer()
        {
            int keep = 0;
            foreach (var value in unsafeContainer)
                Volatile.Write(ref keep, value);
        }
        public void MeasureBclContainer(object container)
        {
            int keep = 0;
            var bclContainer = (HashSet<int>)container;
            foreach (var value in bclContainer)
                Volatile.Write(ref keep, value);
        }
    }

    struct HashSetUnionWith : IBenchmarkContainer
    {
        NativeHashSet<int> nativeContainer;
        NativeHashSet<int> nativeContainerOther;
        UnsafeHashSet<int> unsafeContainer;
        UnsafeHashSet<int> unsafeContainerOther;
        public int total;

        public void AllocNativeContainer(int capacity) => HashSetUtil.AllocInt(ref nativeContainer, ref nativeContainerOther, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashSetUtil.AllocInt(ref unsafeContainer, ref unsafeContainerOther, capacity, true);
        public object AllocBclContainer(int capacity) => HashSetUtil.AllocBclContainerTuple(capacity, true);

        public void MeasureNativeContainer() => nativeContainer.UnionWith(nativeContainerOther);
        public void MeasureUnsafeContainer() => unsafeContainer.UnionWith(unsafeContainerOther);
        public void MeasureBclContainer(object container)
        {
            var dotnetContainer = (System.Tuple<HashSet<int>, HashSet<int>>)container;
            dotnetContainer.Item1.UnionWith(dotnetContainer.Item2);
        }
    }

    struct HashSetIntersectWith : IBenchmarkContainer
    {
        NativeHashSet<int> nativeContainer;
        NativeHashSet<int> nativeContainerOther;
        UnsafeHashSet<int> unsafeContainer;
        UnsafeHashSet<int> unsafeContainerOther;
        public int total;

        public void AllocNativeContainer(int capacity) => HashSetUtil.AllocInt(ref nativeContainer, ref nativeContainerOther, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashSetUtil.AllocInt(ref unsafeContainer, ref unsafeContainerOther, capacity, true);
        public object AllocBclContainer(int capacity) => HashSetUtil.AllocBclContainerTuple(capacity, true);

        public void MeasureNativeContainer() => nativeContainer.IntersectWith(nativeContainerOther);
        public void MeasureUnsafeContainer() => unsafeContainer.IntersectWith(unsafeContainerOther);
        public void MeasureBclContainer(object container)
        {
            var dotnetContainer = (System.Tuple<HashSet<int>, HashSet<int>>)container;
            dotnetContainer.Item1.IntersectWith(dotnetContainer.Item2);
        }
    }

    struct HashSetExceptWith : IBenchmarkContainer
    {
        NativeHashSet<int> nativeContainer;
        NativeHashSet<int> nativeContainerOther;
        UnsafeHashSet<int> unsafeContainer;
        UnsafeHashSet<int> unsafeContainerOther;
        public int total;

        public void AllocNativeContainer(int capacity) => HashSetUtil.AllocInt(ref nativeContainer, ref nativeContainerOther, capacity, true);
        public void AllocUnsafeContainer(int capacity) => HashSetUtil.AllocInt(ref unsafeContainer, ref unsafeContainerOther, capacity, true);
        public object AllocBclContainer(int capacity) => HashSetUtil.AllocBclContainerTuple(capacity, true);

        public void MeasureNativeContainer() => nativeContainer.ExceptWith(nativeContainerOther);
        public void MeasureUnsafeContainer() => unsafeContainer.ExceptWith(unsafeContainerOther);
        public void MeasureBclContainer(object container)
        {
            var dotnetContainer = (System.Tuple<HashSet<int>, HashSet<int>>)container;
            dotnetContainer.Item1.ExceptWith(dotnetContainer.Item2);
        }
    }


    [Benchmark(typeof(BenchmarkContainerType))]
    class HashSet
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem(BenchmarkContainerConfig.kMenuItemIndividual + nameof(HashSet))]
        static void RunIndividual()
            => BenchmarkContainerConfig.RunBenchmark(typeof(HashSet));
#endif

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IsEmpty_x_100k(
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashSetIsEmpty100k>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Count_x_100k(
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashSetCount100k>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void ToNativeArray(
            [Values(10000, 100000, 1000000)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashSetToNativeArray>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Insert(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashSetInsert>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        [BenchmarkTestFootnote("Incrementally grows from `capacity` until reaching size of `growTo`")]
        public unsafe void AddGrow(
            [Values(4, 65536)] int capacity,
            [Values(1024 * 1024)] int growTo,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashSetAddGrow>.Run(capacity, type, growTo);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Contains(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashSetContains>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Remove(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashSetRemove>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Foreach(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashSetForEach>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void UnionWith(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashSetUnionWith>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IntersectWith(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashSetIntersectWith>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void ExceptWith(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<HashSetExceptWith>.Run(insertions, type);
        }
    }
}
