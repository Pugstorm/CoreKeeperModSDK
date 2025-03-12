using NUnit.Framework;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Benchmark;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Unity.Collections.PerformanceTests
{
    static class ArrayUtil
    {
        static public void AllocInt(ref NativeArray<int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Random.InitState(0);
                container = new NativeArray<int>(capacity, Allocator.Persistent);
                if (addValues)
                {
                    for (int i = 0; i < capacity; i++)
                        container[i] = i;
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
            var bclContainer = new int[capacity];
            if (addValues)
            {
                for (int i = 0; i < capacity; i++)
                    bclContainer[i] = i;
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


    struct ArrayLength100k : IBenchmarkContainer
    {
        const int kIterations = 100_000;
        NativeArray<int> nativeContainer;

        public void AllocNativeContainer(int capacity) => ArrayUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) { }
        public object AllocBclContainer(int capacity) => ArrayUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer()
        {
            var reader = nativeContainer.AsReadOnly();
            for (int i = 0; i < kIterations; i++)
                _ = reader.Length;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureUnsafeContainer() { }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (int[])container;
            for (int i = 0; i < kIterations; i++)
                _ = bclContainer.Length;
        }
    }

    struct ArrayIndexedRead : IBenchmarkContainer
    {
        NativeArray<int> nativeContainer;
        UnsafeList<int> values;

        public void AllocNativeContainer(int capacity)
        {
            ArrayUtil.AllocInt(ref nativeContainer, capacity, true);
            ArrayUtil.CreateRandomValues(capacity, ref values);
        }
        public void AllocUnsafeContainer(int capacity) { }
        public object AllocBclContainer(int capacity)
        {
            object container = ArrayUtil.AllocBclContainer(capacity, true);
            ArrayUtil.CreateRandomValues(capacity, ref values);
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
        public void MeasureUnsafeContainer() { }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (int[])container;
            int insertions = values.Length;
            int value = 0;
            for (int i = 0; i < insertions; i++)
                Volatile.Write(ref value, bclContainer[values[i]]);
        }
    }

    struct ArrayIndexedWrite : IBenchmarkContainer
    {
        NativeArray<int> nativeContainer;
        UnsafeList<int> values;

        public void AllocNativeContainer(int capacity)
        {
            ArrayUtil.AllocInt(ref nativeContainer, capacity, true);
            ArrayUtil.CreateRandomValues(capacity, ref values);
        }
        public void AllocUnsafeContainer(int capacity) { }
        public object AllocBclContainer(int capacity)
        {
            object container = ArrayUtil.AllocBclContainer(capacity, true);
            ArrayUtil.CreateRandomValues(capacity, ref values);
            return container;
        }

        public void MeasureNativeContainer()
        {
            int insertions = values.Length;
            for (int i = 0; i < insertions; i++)
                nativeContainer[values[i]] = i;
        }
        public void MeasureUnsafeContainer() { }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (int[])container;
            int insertions = values.Length;
            for (int i = 0; i < insertions; i++)
                bclContainer[values[i]] = i;
        }
    }

    struct ArrayForEach : IBenchmarkContainer
    {
        NativeArray<int> nativeContainer;
        public int total;

        public void AllocNativeContainer(int capacity) => ArrayUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) { }
        public object AllocBclContainer(int capacity) => ArrayUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer()
        {
            int value = 0;
            foreach (var element in nativeContainer)
                Volatile.Write(ref value, element);
        }
        public void MeasureUnsafeContainer() { }
        public void MeasureBclContainer(object container)
        {
            int value = 0;
            var bclContainer = (int[])container;
            foreach (var element in bclContainer)
                Volatile.Write(ref value, element);
        }
    }

    [Benchmark(typeof(BenchmarkContainerType), true)]
    class Array
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem(BenchmarkContainerConfig.kMenuItemIndividual + nameof(Array))]
        static void RunIndividual()
            => BenchmarkContainerConfig.RunBenchmark(typeof(Array));
#endif

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Length_x_100k(
            [Values(0, 100)] int capacity,
            [Values(BenchmarkContainerType.Native, BenchmarkContainerType.NativeBurstSafety,
                BenchmarkContainerType.NativeBurstNoSafety)] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ArrayLength100k>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IndexedRead(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values(BenchmarkContainerType.Native, BenchmarkContainerType.NativeBurstSafety,
                BenchmarkContainerType.NativeBurstNoSafety)] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ArrayIndexedRead>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IndexedWrite(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values(BenchmarkContainerType.Native, BenchmarkContainerType.NativeBurstSafety,
                BenchmarkContainerType.NativeBurstNoSafety)] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ArrayIndexedWrite>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Foreach(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values(BenchmarkContainerType.Native, BenchmarkContainerType.NativeBurstSafety,
                BenchmarkContainerType.NativeBurstNoSafety)] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<ArrayForEach>.Run(insertions, type);
        }
    }
}
