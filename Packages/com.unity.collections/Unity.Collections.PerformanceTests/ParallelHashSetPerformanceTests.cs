using NUnit.Framework;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Benchmark;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Unity.Collections.PerformanceTests
{
    static class ParallelHashSetUtil
    {
        static public void AllocInt(ref NativeParallelHashSet<int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Random.InitState(0);
                container = new NativeParallelHashSet<int>(capacity, Allocator.Persistent);
                if (addValues)
                {
                    for (int i = 0; i < capacity; i++)
                        container.Add(i);
                }
            }
            else
                container.Dispose();
        }
        static public void AllocInt(ref NativeParallelHashSet<int> containerA, ref NativeParallelHashSet<int> containerB, int capacity, bool addValues)
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
        static public void AllocInt(ref UnsafeParallelHashSet<int> container, int capacity, bool addValues)
        {
            if (capacity >= 0)
            {
                Random.InitState(0);
                container = new UnsafeParallelHashSet<int>(capacity, Allocator.Persistent);
                if (addValues)
                {
                    for (int i = 0; i < capacity; i++)
                        container.Add(i);
                }
            }
            else
                container.Dispose();
        }
        static public void AllocInt(ref UnsafeParallelHashSet<int> containerA, ref UnsafeParallelHashSet<int> containerB, int capacity, bool addValues)
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

            var bclContainer = new FakeConcurrentHashSet<int>();

            if (addValues)
            {
                for (int i = 0; i < capacity; i++)
                    bclContainer.Add(i);
            }
            return bclContainer;
        }
        static public object AllocBclContainerTuple(int capacity, bool addValues)
        {
            var tuple = new System.Tuple<FakeConcurrentHashSet<int>, FakeConcurrentHashSet<int>>(
                (FakeConcurrentHashSet<int>)AllocBclContainer(capacity, false),
                (FakeConcurrentHashSet<int>)AllocBclContainer(capacity, false));
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
            if (!keys.IsCreated)
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public void SplitForWorkers(int count, int worker, int workers, out int startInclusive, out int endExclusive)
        {
            startInclusive = count * worker / workers;
            endExclusive = count * (worker + 1) / workers;
        }
    }

    // A generic HashSet with a lock is generally recommended as most performant way to obtain a thread safe HashSet in C#.
    internal class FakeConcurrentHashSet<T> : System.IDisposable
    {
        private readonly ReaderWriterLockSlim m_Lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly System.Collections.Generic.HashSet<T> m_HashSet = new System.Collections.Generic.HashSet<T>();

        ~FakeConcurrentHashSet() => Dispose(false);

        public bool Add(T item)
        {
            m_Lock.EnterWriteLock();
            try
            {
                return m_HashSet.Add(item);
            }
            finally
            {
                if (m_Lock.IsWriteLockHeld)
                    m_Lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            m_Lock.EnterWriteLock();
            try
            {
                m_HashSet.Clear();
            }
            finally
            {
                if (m_Lock.IsWriteLockHeld)
                    m_Lock.ExitWriteLock();
            }
        }

        public bool Contains(T item)
        {
            m_Lock.EnterReadLock();
            try
            {
                return m_HashSet.Contains(item);
            }
            finally
            {
                if (m_Lock.IsReadLockHeld)
                    m_Lock.ExitReadLock();
            }
        }

        public bool Remove(T item)
        {
            m_Lock.EnterWriteLock();
            try
            {
                return m_HashSet.Remove(item);
            }
            finally
            {
                if (m_Lock.IsWriteLockHeld)
                    m_Lock.ExitWriteLock();
            }
        }

        public int Count
        {
            get
            {
                m_Lock.EnterReadLock();
                try
                {
                    return m_HashSet.Count;
                }
                finally
                {
                    if (m_Lock.IsReadLockHeld)
                        m_Lock.ExitReadLock();
                }
            }
        }

        public void CopyTo(T[] array)
        {
            m_Lock.EnterReadLock();
            try
            {
                m_HashSet.CopyTo(array);
            }
            finally
            {
                if (m_Lock.IsReadLockHeld)
                    m_Lock.ExitReadLock();
            }
        }

        public System.Collections.Generic.HashSet<T>.Enumerator GetEnumerator()
        {
            m_Lock.EnterReadLock();
            try
            {
                return m_HashSet.GetEnumerator();
            }
            finally
            {
                if (m_Lock.IsReadLockHeld)
                    m_Lock.ExitReadLock();
            }
        }

        public void UnionWith(FakeConcurrentHashSet<T> other)
        {
            m_Lock.EnterReadLock();
            try
            {
                m_HashSet.UnionWith(other.m_HashSet);
            }
            finally
            {
                if (m_Lock.IsReadLockHeld)
                    m_Lock.ExitReadLock();
            }
        }

        public void IntersectWith(FakeConcurrentHashSet<T> other)
        {
            m_Lock.EnterReadLock();
            try
            {
                m_HashSet.IntersectWith(other.m_HashSet);
            }
            finally
            {
                if (m_Lock.IsReadLockHeld)
                    m_Lock.ExitReadLock();
            }
        }

        public void ExceptWith(FakeConcurrentHashSet<T> other)
        {
            m_Lock.EnterReadLock();
            try
            {
                m_HashSet.ExceptWith(other.m_HashSet);
            }
            finally
            {
                if (m_Lock.IsReadLockHeld)
                    m_Lock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && m_Lock != null)
                m_Lock.Dispose();
        }
    }

    struct ParallelHashSetIsEmpty100k : IBenchmarkContainerParallel
    {
        const int kIterations = 100_000;
        int workers;
        NativeParallelHashSet<int> nativeContainer;
        UnsafeParallelHashSet<int> unsafeContainer;

        void IBenchmarkContainerParallel.SetParams(int capacity, params int[] args) => workers = args[0];
        public void AllocNativeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashSetUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var reader = nativeContainer.AsReadOnly();
            ParallelHashSetUtil.SplitForWorkers(kIterations, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                _ = reader.IsEmpty;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            ParallelHashSetUtil.SplitForWorkers(kIterations, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                _ = unsafeContainer.IsEmpty;
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (FakeConcurrentHashSet<int>)container;
            ParallelHashSetUtil.SplitForWorkers(kIterations, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                _ = bclContainer.Count == 0;
        }
    }

    struct ParallelHashSetCount100k : IBenchmarkContainerParallel
    {
        const int kIterations = 100_000;
        int workers;
        NativeParallelHashSet<int> nativeContainer;
        UnsafeParallelHashSet<int> unsafeContainer;

        void IBenchmarkContainerParallel.SetParams(int capacity, params int[] args) => workers = args[0];
        public void AllocNativeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashSetUtil.AllocBclContainer(capacity, true);

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var reader = nativeContainer.AsReadOnly();
            ParallelHashSetUtil.SplitForWorkers(kIterations, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                _ = reader.Count();
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            ParallelHashSetUtil.SplitForWorkers(kIterations, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                _ = unsafeContainer.Count();
        }
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (FakeConcurrentHashSet<int>)container;
            ParallelHashSetUtil.SplitForWorkers(kIterations, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                _ = bclContainer.Count;
        }
    }

    struct ParallelHashSetToNativeArray : IBenchmarkContainerParallel
    {
        NativeParallelHashSet<int> nativeContainer;
        UnsafeParallelHashSet<int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashSetUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var asArray = nativeContainer.ToNativeArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            var asArray = unsafeContainer.ToNativeArray(Allocator.Temp);
            asArray.Dispose();
        }
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (FakeConcurrentHashSet<int>)container;
            int[] asArray = new int[bclContainer.Count];
            bclContainer.CopyTo(asArray);
        }
    }

    struct ParallelHashSetInsert : IBenchmarkContainerParallel
    {
        int capacity;
        int workers;
        NativeParallelHashSet<int> nativeContainer;
        UnsafeParallelHashSet<int> unsafeContainer;

        void IBenchmarkContainerParallel.SetParams(int capacity, params int[] args)
        {
            this.capacity = capacity;
            workers = args[0];
        }

        public void AllocNativeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref nativeContainer, capacity, false);
        public void AllocUnsafeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref unsafeContainer, capacity, false);
        public object AllocBclContainer(int capacity) => ParallelHashSetUtil.AllocBclContainer(capacity, false);

        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var writer = nativeContainer.AsParallelWriter();
            ParallelHashSetUtil.SplitForWorkers(capacity, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                writer.Add(i, threadIndex);
        }
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            var writer = unsafeContainer.AsParallelWriter();
            ParallelHashSetUtil.SplitForWorkers(capacity, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                writer.Add(i, threadIndex);
        }
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (FakeConcurrentHashSet<int>)container;
            ParallelHashSetUtil.SplitForWorkers(capacity, worker, workers, out int start, out int end);
            for (int i = start; i < end; i++)
                bclContainer.Add(i);
        }
    }

    struct ParallelHashSetAddGrow : IBenchmarkContainerParallel
    {
        int capacity;
        int toAdd;
        NativeParallelHashSet<int> nativeContainer;
        UnsafeParallelHashSet<int> unsafeContainer;

        void IBenchmarkContainerParallel.SetParams(int capacity, params int[] args)
        {
            this.capacity = capacity;
            toAdd = args[0] - capacity;
        }

        public void AllocNativeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashSetUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer(int _, int __)
        {
            // Intentionally setting capacity small and growing by adding more items
            for (int i = capacity; i < capacity + toAdd; i++)
                nativeContainer.Add(i);
        }
        public void MeasureUnsafeContainer(int _, int __)
        {
            // Intentionally setting capacity small and growing by adding more items
            for (int i = capacity; i < capacity + toAdd; i++)
                unsafeContainer.Add(i);
        }
        public void MeasureBclContainer(object container, int _)
        {
            var bclContainer = (FakeConcurrentHashSet<int>)container;
            // Intentionally setting capacity small and growing by adding more items
            for (int i = capacity; i < capacity + toAdd; i++)
                bclContainer.Add(i);
        }
    }

    struct ParallelHashSetContains : IBenchmarkContainerParallel
    {
        int capacity;
        int workers;
        NativeParallelHashSet<int> nativeContainer;
        UnsafeParallelHashSet<int> unsafeContainer;
        UnsafeList<int> keys;

        void IBenchmarkContainerParallel.SetParams(int capacity, params int[] args)
        {
            this.capacity = capacity;
            workers = args[0];
        }

        public void AllocNativeContainer(int capacity)
        {
            ParallelHashSetUtil.AllocInt(ref nativeContainer, capacity, false);
            ParallelHashSetUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.Add(keys[i]);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            ParallelHashSetUtil.AllocInt(ref unsafeContainer, capacity, false);
            ParallelHashSetUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.Add(keys[i]);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = ParallelHashSetUtil.AllocBclContainer(capacity, false);
            var bclContainer = (FakeConcurrentHashSet<int>)container;
            ParallelHashSetUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.Add(keys[i]);
            return container;
        }

        public void MeasureNativeContainer(int worker, int threadIndex)
        {
            var reader = nativeContainer.AsReadOnly();
            ParallelHashSetUtil.SplitForWorkers(capacity, worker, workers, out int start, out int end);
            bool data = false;
            for (int i = start; i < end; i++)
                Volatile.Write(ref data, reader.Contains(keys[i]));
        }
        public void MeasureUnsafeContainer(int worker, int threadIndex)
        {
            ParallelHashSetUtil.SplitForWorkers(capacity, worker, workers, out int start, out int end);
            bool data = false;
            for (int i = start; i < end; i++)
                Volatile.Write(ref data, unsafeContainer.Contains(keys[i]));
        }
        public void MeasureBclContainer(object container, int worker)
        {
            var bclContainer = (FakeConcurrentHashSet<int>)container;
            ParallelHashSetUtil.SplitForWorkers(capacity, worker, workers, out int start, out int end);
            bool data = false;
            for (int i = start; i < end; i++)
                Volatile.Write(ref data, bclContainer.Contains(keys[i]));
        }
    }

    struct ParallelHashSetRemove : IBenchmarkContainerParallel
    {
        NativeParallelHashSet<int> nativeContainer;
        UnsafeParallelHashSet<int> unsafeContainer;
        UnsafeList<int> keys;

        public void AllocNativeContainer(int capacity)
        {
            ParallelHashSetUtil.AllocInt(ref nativeContainer, capacity, false);
            ParallelHashSetUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                nativeContainer.Add(keys[i]);
        }
        public void AllocUnsafeContainer(int capacity)
        {
            ParallelHashSetUtil.AllocInt(ref unsafeContainer, capacity, false);
            ParallelHashSetUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                unsafeContainer.Add(keys[i]);
        }
        public object AllocBclContainer(int capacity)
        {
            object container = ParallelHashSetUtil.AllocBclContainer(capacity, false);
            var bclContainer = (FakeConcurrentHashSet<int>)container;
            ParallelHashSetUtil.CreateRandomKeys(capacity, ref keys);
            for (int i = 0; i < capacity; i++)
                bclContainer.Add(keys[i]);
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
            var bclContainer = (FakeConcurrentHashSet<int>)container;
            int insertions = keys.Length;
            for (int i = 0; i < insertions; i++)
                bclContainer.Remove(keys[i]);
        }
    }

    struct ParallelHashSetForEach : IBenchmarkContainerParallel
    {
        NativeParallelHashSet<int> nativeContainer;
        UnsafeParallelHashSet<int> unsafeContainer;

        public void AllocNativeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref nativeContainer, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref unsafeContainer, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashSetUtil.AllocBclContainer(capacity, true);

        public void MeasureNativeContainer(int _, int __)
        {
            int keep = 0;
            foreach (var value in nativeContainer)
                Volatile.Write(ref keep, value);
        }
        public void MeasureUnsafeContainer(int _, int __)
        {
            int keep = 0;
            foreach (var value in unsafeContainer)
                Volatile.Write(ref keep, value);
        }
        public void MeasureBclContainer(object container, int _)
        {
            int keep = 0;
            var bclContainer = (FakeConcurrentHashSet<int>)container;
            foreach (var value in bclContainer)
                Volatile.Write(ref keep, value);
        }
    }

    struct ParallelHashSetUnionWith : IBenchmarkContainerParallel
    {
        NativeParallelHashSet<int> nativeContainer;
        NativeParallelHashSet<int> nativeContainerOther;
        UnsafeParallelHashSet<int> unsafeContainer;
        UnsafeParallelHashSet<int> unsafeContainerOther;
        public int total;

        public void AllocNativeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref nativeContainer, ref nativeContainerOther, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref unsafeContainer, ref unsafeContainerOther, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashSetUtil.AllocBclContainerTuple(capacity, true);

        public void MeasureNativeContainer(int _, int __) => nativeContainer.UnionWith(nativeContainerOther);
        public void MeasureUnsafeContainer(int _, int __) => unsafeContainer.UnionWith(unsafeContainerOther);
        public void MeasureBclContainer(object container, int _)
        {
            var dotnetContainer = (System.Tuple<FakeConcurrentHashSet<int>, FakeConcurrentHashSet<int>>)container;
            dotnetContainer.Item1.UnionWith(dotnetContainer.Item2);
        }
    }

    struct ParallelHashSetIntersectWith : IBenchmarkContainerParallel
    {
        NativeParallelHashSet<int> nativeContainer;
        NativeParallelHashSet<int> nativeContainerOther;
        UnsafeParallelHashSet<int> unsafeContainer;
        UnsafeParallelHashSet<int> unsafeContainerOther;
        public int total;

        public void AllocNativeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref nativeContainer, ref nativeContainerOther, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref unsafeContainer, ref unsafeContainerOther, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashSetUtil.AllocBclContainerTuple(capacity, true);

        public void MeasureNativeContainer(int _, int __) => nativeContainer.IntersectWith(nativeContainerOther);
        public void MeasureUnsafeContainer(int _, int __) => unsafeContainer.IntersectWith(unsafeContainerOther);
        public void MeasureBclContainer(object container, int _)
        {
            var dotnetContainer = (System.Tuple<FakeConcurrentHashSet<int>, FakeConcurrentHashSet<int>>)container;
            dotnetContainer.Item1.IntersectWith(dotnetContainer.Item2);
        }
    }

    struct ParallelHashSetExceptWith : IBenchmarkContainerParallel
    {
        NativeParallelHashSet<int> nativeContainer;
        NativeParallelHashSet<int> nativeContainerOther;
        UnsafeParallelHashSet<int> unsafeContainer;
        UnsafeParallelHashSet<int> unsafeContainerOther;
        public int total;

        public void AllocNativeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref nativeContainer, ref nativeContainerOther, capacity, true);
        public void AllocUnsafeContainer(int capacity) => ParallelHashSetUtil.AllocInt(ref unsafeContainer, ref unsafeContainerOther, capacity, true);
        public object AllocBclContainer(int capacity) => ParallelHashSetUtil.AllocBclContainerTuple(capacity, true);

        public void MeasureNativeContainer(int _, int __) => nativeContainer.ExceptWith(nativeContainerOther);
        public void MeasureUnsafeContainer(int _, int __) => unsafeContainer.ExceptWith(unsafeContainerOther);
        public void MeasureBclContainer(object container, int _)
        {
            var dotnetContainer = (System.Tuple<FakeConcurrentHashSet<int>, FakeConcurrentHashSet<int>>)container;
            dotnetContainer.Item1.ExceptWith(dotnetContainer.Item2);
        }
    }


    [Benchmark(typeof(BenchmarkContainerType))]
    [BenchmarkNameOverride(BenchmarkContainerConfig.BCL, "HashSet w/lock")]
    class ParallelHashSet
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem(BenchmarkContainerConfig.kMenuItemIndividual + nameof(ParallelHashSet))]
        static void RunIndividual()
            => BenchmarkContainerConfig.RunBenchmark(typeof(ParallelHashSet));
#endif

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IsEmpty_x_100k(
            [Values(1, 2, 4)] int workers,
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashSetIsEmpty100k>.Run(workers, capacity, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Count_x_100k(
            [Values(1, 2, 4)] int workers,
            [Values(0, 100)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashSetCount100k>.Run(workers, capacity, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void ToNativeArray(
            [Values(1)] int workers,
            [Values(10000, 100000, 1000000)] int capacity,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashSetToNativeArray>.Run(workers, capacity, type);
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
            BenchmarkContainerRunnerParallel<ParallelHashSetInsert>.Run(workers, insertions, type, workers);
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
            BenchmarkContainerRunnerParallel<ParallelHashSetAddGrow>.Run(workers, capacity, type, growTo);
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
            BenchmarkContainerRunnerParallel<ParallelHashSetContains>.Run(workers, insertions, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Remove(
            [Values(1)] int workers,  // No API for ParallelWriter.TryRemove currently
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashSetRemove>.Run(workers, insertions, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void Foreach(
            [Values(1)] int workers,  // This work can't be split
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashSetForEach>.Run(workers, insertions, type, workers);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void UnionWith(
            [Values(1)] int workers,  // This work is already split and unrelated to the parallelism of the container
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashSetUnionWith>.Run(workers, insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void IntersectWith(
            [Values(1)] int workers,  // This work is already split and unrelated to the parallelism of the container
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashSetIntersectWith>.Run(workers, insertions, type);
        }

        [Test, Performance]
        [Category("Performance")]
        public unsafe void ExceptWith(
            [Values(1)] int workers,  // This work is already split and unrelated to the parallelism of the container
            [Values(10000, 100000, 1000000)] int insertions,
            [Values] BenchmarkContainerType type)
        {
            BenchmarkContainerRunnerParallel<ParallelHashSetExceptWith>.Run(workers, insertions, type);
        }
    }
}
