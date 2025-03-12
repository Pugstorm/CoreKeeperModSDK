using NUnit.Framework;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Benchmark;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Unity.Collections.PerformanceTests
{
    static class RingQueueUtil
    {
        static public void AllocInt(ref NativeRingQueue<int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Random.InitState(0);
                container = new NativeRingQueue<int>(capacity, Allocator.Persistent);
                if (addValues)
                {
                    for (int i = 0; i < capacity; i++)
                        container.Enqueue(i);
                }
            }
            else
                container.Dispose();
        }
        static public void AllocInt(ref UnsafeRingQueue<int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Random.InitState(0);
                container = new UnsafeRingQueue<int>(capacity, Allocator.Persistent);
                if (addValues)
                {
                    for (int i = 0; i < capacity; i++)
                        container.Enqueue(i);
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
            var bclContainer = new System.Collections.Generic.Queue<int>(capacity);
            if (addValues)
            {
                for (int i = 0; i < capacity; i++)
                    bclContainer.Enqueue(i);
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

    struct RingQueueIsEmpty100k : IBenchmarkContainer
    {
        const int kIterations = 100_000;
        NativeRingQueue<int> nativeContainer;
        UnsafeRingQueue<int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => RingQueueUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => RingQueueUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => RingQueueUtil.AllocBclContainer(capacity, true);

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
            var bclContainer = (System.Collections.Generic.Queue<int>)container;
            for (int i = 0; i < kIterations; i++)
                _ = bclContainer.Count == 0;
        }
    }

    struct RingQueueCount100k : IBenchmarkContainer
    {
        const int kIterations = 100_000;
        NativeRingQueue<int> nativeContainer;
        UnsafeRingQueue<int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => RingQueueUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => RingQueueUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => RingQueueUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer()
        {
            for (int i = 0; i < kIterations; i++)
                _ = nativeContainer.Length;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureUnsafeContainer()
        {
            for (int i = 0; i < kIterations; i++)
                _ = unsafeContainer.Length;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Queue<int>)container;
            for (int i = 0; i < kIterations; i++)
                _ = bclContainer.Count;
        }
    }

    struct RingQueueEnqueue : IBenchmarkContainer
    {
        int capacity;
        NativeRingQueue<int> nativeContainer;
        UnsafeRingQueue<int> unsafeContainer;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args) => this.capacity = capacity;

        public void AllocNativeContainer(int capacity) => RingQueueUtil.AllocInt(ref nativeContainer, capacity, false);
        public void AllocUnsafeContainer(int capacity) => RingQueueUtil.AllocInt(ref unsafeContainer, capacity, false);
        public object AllocBclContainer(int capacity) => RingQueueUtil.AllocBclContainer(capacity, false);

        public void MeasureNativeContainer()
        {
            for (int i = 0; i < capacity; i++)
                nativeContainer.Enqueue(i);
        }
        public void MeasureUnsafeContainer()
        {
            for (int i = 0; i < capacity; i++)
                unsafeContainer.Enqueue(i);
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Queue<int>)container;
            for (int i = 0; i < capacity; i++)
                bclContainer.Enqueue(i);
        }
    }

    struct RingQueueDequeue : IBenchmarkContainer
    {
        int capacity;
        NativeRingQueue<int> nativeContainer;
        UnsafeRingQueue<int> unsafeContainer;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args) => this.capacity = capacity;

        public void AllocNativeContainer(int capacity) => RingQueueUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => RingQueueUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => RingQueueUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer()
        {
            int keep = 0;
            for (int i = 0; i < capacity; i++)
                Volatile.Write(ref keep, nativeContainer.Dequeue());
        }
        public void MeasureUnsafeContainer()
        {
            int keep = 0;
            for (int i = 0; i < capacity; i++)
                Volatile.Write(ref keep, unsafeContainer.Dequeue());
        }
        public void MeasureBclContainer(object container)
        {
            int keep = 0;
            var bclContainer = (System.Collections.Generic.Queue<int>)container;
            for (int i = 0; i < capacity; i++)
                Volatile.Write(ref keep, bclContainer.Dequeue());
        }
    }

    [Benchmark(typeof(BenchmarkContainerType))]
    [BenchmarkNameOverride(BenchmarkContainerConfig.BCL, "Queue")]
    class RingQueue
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem(BenchmarkContainerConfig.kMenuItemIndividual + nameof(RingQueue))]
        static void RunIndividual()
            => BenchmarkContainerConfig.RunBenchmark(typeof(RingQueue));
#endif

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IsEmpty_x_100k(
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<RingQueueIsEmpty100k>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Count_x_100k(
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<RingQueueCount100k>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Enqueue(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<RingQueueEnqueue>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Dequeue(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<RingQueueDequeue>.Run(insertions, type);
        }
    }
}
