using NUnit.Framework;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Benchmark;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Unity.Collections.PerformanceTests
{
    static class ListUtil
    {
        static public void AllocInt(ref NativeList<int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Random.InitState(0);
                container = new NativeList<int>(capacity, Allocator.Persistent);
                if (addValues)
                {
                    for (int i = 0; i < capacity; i++)
                        container.Add(i);
                }
            }
            else
                container.Dispose();
        }
        static public void AllocInt(ref UnsafeList<int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Random.InitState(0);
                container = new UnsafeList<int>(capacity, Allocator.Persistent);
                if (addValues)
                {
                    for (int i = 0; i < capacity; i++)
                        container.Add(i);
                }
            }
            else
                container.Dispose();
        }
        static public object AllocBclContainer(int capacity, bool addValues)
        {
            if (capacity < 0)
                return null;

            Random.InitState(0);
            var bclContainer = new System.Collections.Generic.List<int>(capacity);
            if (addValues)
            {
                for (int i = 0; i < capacity; i++)
                    bclContainer.Add(i);
            }
            return bclContainer;
        }

        static public void CreateRandomValues(int capacity, ref UnsafeList<int> values)
        {
            if (capacity >= 0)
            {
                values = new UnsafeList<int>(capacity, Allocator.Persistent);
                Random.InitState(0);
                for (int i = 0; i < capacity; i++)
                {
                    int randKey = Random.Range(0, capacity);
                    values.Add(randKey);
                }
            }
            else
                values.Dispose();
        }
    }

    struct ListIsEmpty100k : IBenchmarkContainer
    {
        const int kIterations = 100_000;
        NativeList<int> nativeContainer;
        UnsafeList<int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => ListUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ListUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ListUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer()
        {
            for (int i = 0; i < kIterations; i++)
                _ = nativeContainer.IsEmpty;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureUnsafeContainer()
        {
            for (int i = 0; i < kIterations; i++)
                _ = unsafeContainer.IsEmpty;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.List<int>)container;
            for (int i = 0; i < kIterations; i++)
                _ = bclContainer.Count == 0;
        }
    }

    struct ListCount100k : IBenchmarkContainer
    {
        const int kIterations = 100_000;
        NativeList<int> nativeContainer;
        UnsafeList<int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => ListUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ListUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ListUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer()
        {
            var reader = nativeContainer.AsReadOnly();
            for (int i = 0; i < kIterations; i++)
                _ = reader.Length;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureUnsafeContainer()
        {
            var reader = unsafeContainer.AsReadOnly();
            for (int i = 0; i < kIterations; i++)
                _ = reader.Length;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.List<int>)container;
            for (int i = 0; i < kIterations; i++)
                _ = bclContainer.Count;
        }
    }

    struct ListToNativeArray : IBenchmarkContainer
    {
        NativeList<int> nativeContainer;

        public void AllocNativeContainer(int capacity) => ListUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) { }
        public object AllocBclContainer(int capacity) => ListUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer()
        {
            var asArray = nativeContainer.ToArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureUnsafeContainer() { }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.List<int>)container;
            int[] asArray = new int[bclContainer.Count];
            bclContainer.CopyTo(asArray, 0);
        }
    }

    struct ListAdd : IBenchmarkContainer
    {
        int capacity;
        NativeList<int> nativeContainer;
        UnsafeList<int> unsafeContainer;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args) => this.capacity = capacity;

        public void AllocNativeContainer(int capacity) => ListUtil.AllocInt(ref nativeContainer, capacity, false);
        public void AllocUnsafeContainer(int capacity) => ListUtil.AllocInt(ref unsafeContainer, capacity, false);
        public object AllocBclContainer(int capacity) => ListUtil.AllocBclContainer(capacity, false);

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
            var bclContainer = (System.Collections.Generic.List<int>)container;
            for (int i = 0; i < capacity; i++)
                bclContainer.Add(i);
        }
    }

    struct ListAddGrow : IBenchmarkContainer
    {
        int capacity;
        int toAdd;
        NativeList<int> nativeContainer;
        UnsafeList<int> unsafeContainer;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args)
        {
            this.capacity = capacity;
            toAdd = args[0];
        }

        public void AllocNativeContainer(int capacity) => ListUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ListUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ListUtil.AllocBclContainer(capacity, true);

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
            var bclContainer = (System.Collections.Generic.List<int>)container;
            // Intentionally setting capacity small and growing by adding more items
            for (int i = capacity; i < capacity + toAdd; i++)
                bclContainer.Add(i);
        }
    }

    struct ListContains : IBenchmarkContainer
    {
        int capacity;
        NativeList<int> nativeContainer;
        UnsafeList<int> unsafeContainer;
        UnsafeList<int> values;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args) => this.capacity = capacity;

        public void AllocNativeContainer(int capacity)
        {
            ListUtil.AllocInt(ref nativeContainer, capacity, false);
            ListUtil.CreateRandomValues(capacity, ref values);
            for (int i = 0; i < capacity; i++)
                nativeContainer.Add(values[i]);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            ListUtil.AllocInt(ref unsafeContainer, capacity, false);
            ListUtil.CreateRandomValues(capacity, ref values);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.Add(values[i]);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = ListUtil.AllocBclContainer(capacity, false);
            var bclContainer = (System.Collections.Generic.List<int>)container;
            ListUtil.CreateRandomValues(capacity, ref values);
            for (int i = 0; i < capacity; i++)
                bclContainer.Add(values[i]);
            return container;
        }

        public void MeasureNativeContainer()
        {
            var reader = nativeContainer.AsReadOnly();
            bool data = false;
            for (int i = 0; i < capacity; i++)
                Volatile.Write(ref data, reader.Contains(values[i]));
        }
        public void MeasureUnsafeContainer()
        {
            var reader = unsafeContainer.AsReadOnly();
            bool data = false;
            for (int i = 0; i < capacity; i++)
                Volatile.Write(ref data, reader.Contains(values[i]));
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.List<int>)container;
            bool data = false;
            for (int i = 0; i < capacity; i++)
                Volatile.Write(ref data, bclContainer.Contains(values[i]));
        }
    }

    struct ListIndexedRead : IBenchmarkContainer
    {
        NativeList<int> nativeContainer;
        UnsafeList<int> unsafeContainer;
        UnsafeList<int> values;

        public void AllocNativeContainer(int capacity)
        {
            ListUtil.AllocInt(ref nativeContainer, capacity, true);
            ListUtil.CreateRandomValues(capacity, ref values);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            ListUtil.AllocInt(ref unsafeContainer, capacity, true);
            ListUtil.CreateRandomValues(capacity, ref values);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = ListUtil.AllocBclContainer(capacity, true);
            ListUtil.CreateRandomValues(capacity, ref values);
            return container;
        }

        public void MeasureNativeContainer()
        {
            var reader = nativeContainer.AsReadOnly();
            int insertions = values.Length;
            int value = 0;
            for (int i = 0; i < insertions; i++)
                Volatile.Write(ref value, reader[values[i]]);
        }
        public void MeasureUnsafeContainer()
        {
            int insertions = values.Length;
            int value = 0;
            for (int i = 0; i < insertions; i++)
                Volatile.Write(ref value, unsafeContainer[values[i]]);
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.List<int>)container;
            int insertions = values.Length;
            int value = 0;
            for (int i = 0; i < insertions; i++)
                Volatile.Write(ref value, bclContainer[values[i]]);
        }
    }

    struct ListIndexedWrite : IBenchmarkContainer
    {
        NativeList<int> nativeContainer;
        UnsafeList<int> unsafeContainer;
        UnsafeList<int> values;

        public void AllocNativeContainer(int capacity)
        {
            ListUtil.AllocInt(ref nativeContainer, capacity, true);
            ListUtil.CreateRandomValues(capacity, ref values);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            ListUtil.AllocInt(ref unsafeContainer, capacity, true);
            ListUtil.CreateRandomValues(capacity, ref values);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = ListUtil.AllocBclContainer(capacity, true);
            ListUtil.CreateRandomValues(capacity, ref values);
            return container;
        }

        public void MeasureNativeContainer()
        {
            int insertions = values.Length;
            for (int i = 0; i < insertions; i++)
                nativeContainer[values[i]] = i;
        }
        public void MeasureUnsafeContainer()
        {
            int insertions = values.Length;
            for (int i = 0; i < insertions; i++)
                unsafeContainer[values[i]] = i;
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.List<int>)container;
            int insertions = values.Length;
            for (int i = 0; i < insertions; i++)
                bclContainer[values[i]] = i;
        }
    }

    struct ListRemove : IBenchmarkContainer
    {
        NativeList<int> nativeContainer;
        UnsafeList<int> unsafeContainer;
        UnsafeList<int> values;

        void FixValues()
        {
            // Ensure if we iterate this list and remove a random index, it will always be a valid index given how many elements still remain.
            int max = values.Length;
            while (--max >= 0)
            {
                int reverseIndex = values.Length - 1 - max;
                int value = values[reverseIndex];
                if (value > max)
                    values[reverseIndex] = max;
            }
        }
        public void AllocNativeContainer(int capacity)
        {
            ListUtil.AllocInt(ref nativeContainer, capacity, true);
            ListUtil.CreateRandomValues(capacity, ref values);
            FixValues();
        }
        public void AllocUnsafeContainer(int capacity)
        {
            ListUtil.AllocInt(ref unsafeContainer, capacity, true);
            ListUtil.CreateRandomValues(capacity, ref values);
            FixValues();
        }
        public object AllocBclContainer(int capacity)
        {
            object container = ListUtil.AllocBclContainer(capacity, true);
            ListUtil.CreateRandomValues(capacity, ref values);
            FixValues();
            return container;
        }

        public void MeasureNativeContainer()
        {
            int insertions = values.Length;
            for (int i = 0; i < insertions; i++)
                nativeContainer.RemoveAt(values[i]);
        }
        public void MeasureUnsafeContainer()
        {
            int insertions = values.Length;
            for (int i = 0; i < insertions; i++)
                unsafeContainer.RemoveAt(values[i]);
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.List<int>)container;
            int insertions = values.Length;
            for (int i = 0; i < insertions; i++)
                bclContainer.RemoveAt(values[i]);
        }
    }

    struct ListForEach : IBenchmarkContainer
    {
        NativeList<int> nativeContainer;
        UnsafeList<int> unsafeContainer;
        public int total;

        public void AllocNativeContainer(int capacity) => ListUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ListUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ListUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer()
        {
            int value = 0;
            foreach (var element in nativeContainer)
                Volatile.Write(ref value, element);
        }
        public void MeasureUnsafeContainer()
        {
            int value = 0;
            foreach (var element in unsafeContainer)
                Volatile.Write(ref value, element);
        }
        public void MeasureBclContainer(object container)
        {
            int value = 0;
            var bclContainer = (System.Collections.Generic.List<int>)container;
            foreach (var element in bclContainer)
                Volatile.Write(ref value, element);
        }
    }


    [Benchmark(typeof(BenchmarkContainerType))]
    class List
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem(BenchmarkContainerConfig.kMenuItemIndividual + nameof(List))]
        static void RunIndividual()
            => BenchmarkContainerConfig.RunBenchmark(typeof(List));
#endif

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IsEmpty_x_100k(
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ListIsEmpty100k>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Count_x_100k(
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ListCount100k>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void ToNativeArray(
            [Values(10000, 100000, 1000000)] int capacity,
            [Values(BenchmarkContainerType.Native, BenchmarkContainerType.NativeBurstSafety,
                BenchmarkContainerType.NativeBurstNoSafety)] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ListToNativeArray>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Add(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ListAdd>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        [BenchmarkTestFootnote("Incrementally grows from `capacity` until reaching size of `growTo`")]
        public unsafe void AddGrow(
            [Values(4, 65536)] int capacity,
            [Values(1024 * 1024)] int growTo,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ListAddGrow>.Run(capacity, type, growTo);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Contains(
            [Values(1000, 10000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ListContains>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IndexedRead(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ListIndexedRead>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IndexedWrite(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ListIndexedWrite>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Remove(
            [Values(1000, 10000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ListRemove>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Foreach(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ListForEach>.Run(insertions, type);
        }
    }
}
