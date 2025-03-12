using NUnit.Framework;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Benchmark;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Unity.Collections.PerformanceTests
{
    static class QueueUtil
    {
        static public void AllocInt(ref NativeQueue<int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Random.InitState(0);
                container = new NativeQueue<int>(Allocator.Persistent);
                if (addValues)
                {
                    for (int i = 0; i < capacity; i++)
                        container.Enqueue(i);
                }
            }
            else
                container.Dispose();
        }
        static public void AllocInt(ref UnsafeQueue<int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Random.InitState(0);
                container = new UnsafeQueue<int>(Allocator.Persistent);
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
            var bclContainer = new System.Collections.Generic.Queue<int>();
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

    struct QueueIsEmpty100k : IBenchmarkContainer
    {
        const int kIterations = 100_000;
        NativeQueue<int> nativeContainer;
        UnsafeQueue<int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => QueueUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => QueueUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => QueueUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer()
        {
            for (int i = 0; i < kIterations; i++)
                _ = nativeContainer.IsEmpty();
        }
        public void MeasureUnsafeContainer()
        {
            for (int i = 0; i < kIterations; i++)
                _ = unsafeContainer.IsEmpty();
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Queue<int>)container;
            for (int i = 0; i < kIterations; i++)
                _ = bclContainer.Count == 0;
        }
    }

    struct QueueCount100k : IBenchmarkContainer
    {
        const int kIterations = 100_000;
        NativeQueue<int> nativeContainer;
        UnsafeQueue<int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => QueueUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => QueueUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => QueueUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer()
        {
            for (int i = 0; i < kIterations; i++)
                _ = nativeContainer.Count;
        }
        public void MeasureUnsafeContainer()
        {
            for (int i = 0; i < kIterations; i++)
                _ = unsafeContainer.Count;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Queue<int>)container;
            for (int i = 0; i < kIterations; i++)
                _ = bclContainer.Count;
        }
    }

    struct QueueToNativeArray : IBenchmarkContainer
    {
        NativeQueue<int> nativeContainer;
        UnsafeQueue<int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => QueueUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => QueueUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => QueueUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer()
        {
            var asArray = nativeContainer.ToArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureUnsafeContainer()
        {
            var asArray = unsafeContainer.ToArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Queue<int>)container;
            int[] asArray = new int[bclContainer.Count];
            bclContainer.CopyTo(asArray, 0);
        }
    }

    struct QueueEnqueueGrow : IBenchmarkContainer
    {
        int capacity;
        int workers;
        NativeQueue<int> nativeContainer;
        UnsafeQueue<int> unsafeContainer;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args) => this.capacity = capacity;

        public void AllocNativeContainer(int capacity) => QueueUtil.AllocInt(ref nativeContainer, capacity >= 0 ? 0 : -1, false);
        public void AllocUnsafeContainer(int capacity) => QueueUtil.AllocInt(ref unsafeContainer, capacity >= 0 ? 0 : -1, false);
        public object AllocBclContainer(int capacity) => QueueUtil.AllocBclContainer(capacity >= 0 ? 0 : -1, false);

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

    struct QueueEnqueue : IBenchmarkContainer
    {
        int capacity;
        int workers;
        NativeQueue<int> nativeContainer;
        UnsafeQueue<int> unsafeContainer;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args) => this.capacity = capacity;

        public void AllocNativeContainer(int capacity) => QueueUtil.AllocInt(ref nativeContainer, capacity, false);
        public void AllocUnsafeContainer(int capacity) => QueueUtil.AllocInt(ref unsafeContainer, capacity, false);
        public object AllocBclContainer(int capacity) => QueueUtil.AllocBclContainer(capacity, false);

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

    struct QueueDequeue : IBenchmarkContainer
    {
        int capacity;
        NativeQueue<int> nativeContainer;
        UnsafeQueue<int> unsafeContainer;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args) => this.capacity = capacity;

        public void AllocNativeContainer(int capacity) => QueueUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => QueueUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => QueueUtil.AllocBclContainer(capacity, true);

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
            var bclContainer = (System.Collections.Generic.Queue<int>)container;
            for (int i = 0; i < capacity; i++)
            {
                bclContainer.TryDequeue(out int value);
                Volatile.Read(ref value);                
            }
        }
    }

    struct QueuePeek: IBenchmarkContainer
    {
        int capacity;
        NativeQueue<int> nativeContainer;
        UnsafeQueue<int> unsafeContainer;

        void IBenchmarkContainer.SetParams(int capacity, params int[] args) => this.capacity = capacity;

        public void AllocNativeContainer(int capacity) => QueueUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => QueueUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => QueueUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer()
        {
            for (int i = 0; i < capacity; i++)
                _ = nativeContainer.Peek();
        }
        public void MeasureUnsafeContainer()
        {
            for (int i = 0; i < capacity; i++)
                _ = unsafeContainer.Peek();
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container)
        {
            var bclContainer = (System.Collections.Generic.Queue<int>)container;
            for (int i = 0; i < capacity; i++)
            {
                bclContainer.TryPeek(out int value);
                Volatile.Read(ref value);
            }
        }
    }

    struct QueueForEach : IBenchmarkContainer
    {
        NativeQueue<int> nativeContainer;
        UnsafeQueue<int> unsafeContainer;
        public int total;

        public void AllocNativeContainer(int capacity) => QueueUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => QueueUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => QueueUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer()
        {
            int value = 0;
            var ro = nativeContainer.AsReadOnly();
            foreach (var element in ro)
                Volatile.Write(ref value, element);
        }
        public void MeasureUnsafeContainer()
        {
            int value = 0;
            var ro = unsafeContainer.AsReadOnly();
            foreach (var element in ro)
                Volatile.Write(ref value, element);
        }
        public void MeasureBclContainer(object container)
        {
            int value = 0;
            var bclContainer = (System.Collections.Generic.Queue<int>)container;
            foreach (var element in bclContainer)
                Volatile.Write(ref value, element);
        }
    }


    [Benchmark(typeof(BenchmarkContainerType))]
    class Queue
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem(BenchmarkContainerConfig.kMenuItemIndividual + nameof(Queue))]
        static void RunIndividual()
            => BenchmarkContainerConfig.RunBenchmark(typeof(Queue));
#endif

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IsEmpty_x_100k(
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<QueueIsEmpty100k>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Count_x_100k(
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<QueueCount100k>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void ToNativeArray(
            [Values(10000, 100000, 1000000)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<QueueToNativeArray>.Run(capacity, type);
        }

        [Test, Performance]
        [Category("Performance")]
        [BenchmarkTestFootnote]
        public unsafe void EnqueueGrow(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<QueueEnqueueGrow>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        [BenchmarkTestFootnote]
        public unsafe void Enqueue(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<QueueEnqueue>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Dequeue(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<QueueDequeue>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Peek(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<QueuePeek>.Run(insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Foreach(
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunner<QueueForEach>.Run(insertions, type);
        }
    }
}
