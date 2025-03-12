using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Benchmark;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using System.Runtime.InteropServices;

namespace Unity.Collections.PerformanceTests
{
    /// <summary>
    /// Interface to implement container performance tests which will run using <see cref="BenchmarkContainerRunnerParallel{T}.Run))"/>.
    /// Deriving tests from this interface enables both Performance Test Framework and Benchmark Framework to generate and run
    /// tests for the contexts described by <see cref="BenchmarkContainerType"/>.
    /// </summary>
    public interface IBenchmarkContainerParallel
    {
        /// <summary>
        /// Override this to add extra int arguments to a performance test implementation as fields in the implementing type. These arguments
        /// are optionally passed in through <see cref="BenchmarkContainerRunner{T}.Run(int, BenchmarkContainerType, int[])"/>.
        /// </summary>
        /// <param name="capacity">The initial capacity to requested for the container.</param>
        /// <param name="args">A variable number of extra arguments to passed through to the test implementation</param>
        public void SetParams(int capacity, params int[] args) { }

        /// <summary>
        /// Called during setup for each measurement in a sample set with the capacity to allocate to the native container
        /// when the benchmark type is <see cref="BenchmarkContainerType.Native"/>, <see cref="BenchmarkContainerType.NativeBurstNoSafety"/>,
        /// or <see cref="BenchmarkContainerType.NativeBurstSafety"/>.<para />
        /// This is also called during teardown for each measurement in a sample set with '-1' to indicate freeing the container.
        /// </summary>
        /// <param name="capacity">The capacity to allocate for the managed container. Capacity of 0 will still create a container,
        /// but it will be empty. A capacity of -1 will dispose the container and free associated allocation(s).</param>
        public void AllocNativeContainer(int capacity);

        /// <summary>
        /// Called during setup for each measurement in a sample set with the capacity to allocate to the unsafe container
        /// when the benchmark type is <see cref="BenchmarkContainerType.Unsafe"/>, <see cref="BenchmarkContainerType.UnsafeBurstNoSafety"/>,
        /// or <see cref="BenchmarkContainerType.UnsafeBurstSafety"/>.<para />
        /// This is also called during teardown for each measurement in a sample set with '-1' to indicate freeing the container.
        /// </summary>
        /// <param name="capacity">The capacity to allocate for the managed container. Capacity of 0 will still create a container,
        /// but it will be empty. A capacity of -1 will dispose the container and free associated allocation(s).</param>
        public void AllocUnsafeContainer(int capacity);

        /// <summary>
        /// Called during setup for each measurement in a sample set with the capacity to allocate to the managed container
        /// when the benchmark type is <see cref="BenchmarkContainerConfig.BCL"/>.<para />
        /// This is also called during teardown for each measurement in a sample set with '-1' to indicate freeing the container.
        /// </summary>
        /// <param name="capacity">The capacity to allocate for the managed container. Capacity of 0 will still create a container,
        /// but it will be empty. A capacity of -1 will dispose the container and free associated allocation(s).</param>
        /// <returns>A reference to the allocated container when capacity &gt;= 0, and `null` when capacity &lt; 0.</returns>
        public object AllocBclContainer(int capacity);

        /// <summary>
        /// The code which will be executed during performance measurement. This should usually be general enough to
        /// work with any native container.
        /// </summary>
        /// <param name="worker">The worker index out of the number of job workers requested for parallel benchmarking</param>
        /// <param name="threadIndex">The job system thread index which must be specified in some cases for a container's ParallelWriter</param>
        public void MeasureNativeContainer(int worker, int threadIndex);

        /// <summary>
        /// The code which will be executed during performance measurement. This should usually be general enough to
        /// work with any unsafe container.
        /// </summary>
        /// <param name="worker">The worker index out of the number of job workers requested for parallel benchmarking</param>
        /// <param name="threadIndex">The job system thread index which must be specified in some cases for a container's ParallelWriter</param>
        public void MeasureUnsafeContainer(int worker, int threadIndex);

        /// <summary>
        /// The code which will be executed during performance measurement. This should usually be general enough to
        /// work with any managed container provided by the Base Class Library (BCL).
        /// </summary>
        /// <param name="container">A reference to the managed container allocated in <see cref="AllocBclContainer(int)"/></param>
        /// <param name="worker">The worker index out of the number of job workers requested for parallel benchmarking</param>
        public void MeasureBclContainer(object container, int worker);
    }

    /// <summary>
    /// Provides the API for running container based Performance Framework tests and Benchmark Framework measurements.
    /// This will typically be the sole call from a performance test. See <see cref="Run(int, BenchmarkContainerType, int[])"/>
    /// for more information.
    /// </summary>
    /// <typeparam name="T">An implementation conforming to the <see cref="IBenchmarkContainer"/> interface for running container performance tests and benchmarks.</typeparam>
    [BurstCompile(CompileSynchronously = true)]
    public static class BenchmarkContainerRunnerParallel<T> where T : unmanaged, IBenchmarkContainerParallel
    {
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
        unsafe struct NativeJobBurstST : IJob
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute() => methods->MeasureNativeContainer(0, 0);
        }
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = false)]
        unsafe struct NativeJobSafetyBurstST : IJob
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute() => methods->MeasureNativeContainer(0, 0);
        }

        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
        unsafe struct UnsafeJobBurstST : IJob
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute() => methods->MeasureUnsafeContainer(0, 0);
        }
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = false)]
        unsafe struct UnsafeJobSafetyBurstST : IJob
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute() => methods->MeasureUnsafeContainer(0, 0);
        }

        unsafe struct NativeJobMT : IJobParallelFor
        {
            [NativeSetThreadIndex] int threadIndex;
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute(int index) => methods->MeasureNativeContainer(index, threadIndex);
        }
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
        unsafe struct NativeJobBurstMT : IJobParallelFor
        {
            [NativeSetThreadIndex] int threadIndex;
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute(int index) => methods->MeasureNativeContainer(index, threadIndex);
        }
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = false)]
        unsafe struct NativeJobSafetyBurstMT : IJobParallelFor
        {
            [NativeSetThreadIndex] int threadIndex;
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute(int index) => methods->MeasureNativeContainer(index, threadIndex);
        }

        unsafe struct UnsafeJobMT : IJobParallelFor
        {
            [NativeSetThreadIndex] int threadIndex;
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute(int index) => methods->MeasureUnsafeContainer(index, threadIndex);
        }
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
        unsafe struct UnsafeJobBurstMT : IJobParallelFor
        {
            [NativeSetThreadIndex] int threadIndex;
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute(int index) => methods->MeasureUnsafeContainer(index, threadIndex);
        }
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = false)]
        unsafe struct UnsafeJobSafetyBurstMT : IJobParallelFor
        {
            [NativeSetThreadIndex] int threadIndex;
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute(int index) => methods->MeasureUnsafeContainer(index, threadIndex);
        }

        unsafe struct BclJobMT : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            [NativeDisableUnsafePtrRestriction] public GCHandle* gcHandle;
            public void Execute(int index) => methods->MeasureBclContainer(gcHandle->Target, index);
        }

        static unsafe void RunMT(int workers, int capacity, BenchmarkContainerType type, params int[] args)
        {
            var methods = new T();
            methods.SetParams(capacity, args);

            switch (type)
            {
                case (BenchmarkContainerType)(BenchmarkContainerConfig.BCL):
                    object container = null;
                    GCHandle* gcHandle = default;
                    BenchmarkMeasure.MeasureParallel(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => new BclJobMT { methods = (T*)UnsafeUtility.AddressOf(ref methods), gcHandle = gcHandle }.Schedule(workers, 1).Complete(),
                        () =>
                        {
                            container = methods.AllocBclContainer(capacity);
                            gcHandle = (GCHandle*)UnsafeUtility.Malloc(sizeof(GCHandle), 0, Allocator.Persistent);
                            *gcHandle = GCHandle.Alloc(container);
                        },
                        () =>
                        {
                            gcHandle->Free();
                            UnsafeUtility.Free(gcHandle, Allocator.Persistent);
                            container = methods.AllocBclContainer(-1);
                        });
                    break;
                case BenchmarkContainerType.Native:
                    BenchmarkMeasure.MeasureParallel(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => new NativeJobMT { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule(workers, 1).Complete(),
                        () => methods.AllocNativeContainer(capacity), () => methods.AllocNativeContainer(-1));
                    break;
                case BenchmarkContainerType.NativeBurstSafety:
                    BenchmarkMeasure.MeasureParallel(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => new NativeJobSafetyBurstMT { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule(workers, 1).Complete(),
                        () => methods.AllocNativeContainer(capacity), () => methods.AllocNativeContainer(-1));
                    break;
                case BenchmarkContainerType.NativeBurstNoSafety:
                    BenchmarkMeasure.MeasureParallel(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => new NativeJobBurstMT { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule(workers, 1).Complete(),
                        () => methods.AllocNativeContainer(capacity), () => methods.AllocNativeContainer(-1));
                    break;
                case BenchmarkContainerType.Unsafe:
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => new UnsafeJobMT { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule(workers, 1).Complete(),
                        () => methods.AllocUnsafeContainer(capacity), () => methods.AllocUnsafeContainer(-1));
                    break;
                case BenchmarkContainerType.UnsafeBurstSafety:
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => new UnsafeJobSafetyBurstMT { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule(workers, 1).Complete(),
                        () => methods.AllocUnsafeContainer(capacity), () => methods.AllocUnsafeContainer(-1));
                    break;
                case BenchmarkContainerType.UnsafeBurstNoSafety:
                    BenchmarkMeasure.MeasureParallel(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => new UnsafeJobBurstMT { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule(workers, 1).Complete(),
                        () => methods.AllocUnsafeContainer(capacity), () => methods.AllocUnsafeContainer(-1));
                    break;
            }
        }

        /// <summary>
        /// Called from a typical performance test method to provide both Performance Framework measurements as well as
        /// Benchmark Framework measurements. A typical usage is similar to:
        /// <c>[Test, Performance]<br />
        /// [Category("Performance")]<br />
        /// public unsafe void ToNativeArray(<br />
        ///     [Values(100000, 1000000, 10000000)] int capacity,<br />
        ///     [Values] BenchmarkContainerType type)<br />
        /// {<br />
        ///     BenchmarkContainerRunner&lt;HashSetToNativeArray&gt;.RunST(capacity, type);<br />
        /// }</c>
        /// </summary>
        /// <param name="capacity">The capacity for the container(s) which will be passed to setup methods</param>
        /// <param name="type">The benchmark or performance measurement type to run for containers i.e. <see cref="BenchmarkContainerType.Native"/> etc.</param>
        /// <param name="args">Optional arguments that can be stored in a test implementation class.</param>
        /// <remarks>This will run measurements with <see cref="IJob"/> or directly called on the main thread.</remarks>
        public static unsafe void Run(int capacity, BenchmarkContainerType type, params int[] args)
        {
            var methods = new T();
            methods.SetParams(capacity, args);

            switch (type)
            {
                case (BenchmarkContainerType)(BenchmarkContainerConfig.BCL):
                    object container = null;
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => methods.MeasureBclContainer(container, 0),
                        () => container = methods.AllocBclContainer(capacity), () => container = methods.AllocBclContainer(-1));
                    break;
                case BenchmarkContainerType.Native:
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => methods.MeasureNativeContainer(0, 0),
                        () => methods.AllocNativeContainer(capacity), () => methods.AllocNativeContainer(-1));
                    break;
                case BenchmarkContainerType.NativeBurstSafety:
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => new NativeJobSafetyBurstST { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Run(),
                        () => methods.AllocNativeContainer(capacity), () => methods.AllocNativeContainer(-1));
                    break;
                case BenchmarkContainerType.NativeBurstNoSafety:
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => new NativeJobBurstST { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Run(),
                        () => methods.AllocNativeContainer(capacity), () => methods.AllocNativeContainer(-1));
                    break;
                case BenchmarkContainerType.Unsafe:
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => methods.MeasureUnsafeContainer(0, 0),
                        () => methods.AllocUnsafeContainer(capacity), () => methods.AllocUnsafeContainer(-1));
                    break;
                case BenchmarkContainerType.UnsafeBurstSafety:
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => new UnsafeJobSafetyBurstST { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Run(),
                        () => methods.AllocUnsafeContainer(capacity), () => methods.AllocUnsafeContainer(-1));
                    break;
                case BenchmarkContainerType.UnsafeBurstNoSafety:
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => new UnsafeJobBurstST { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Run(),
                        () => methods.AllocUnsafeContainer(capacity), () => methods.AllocUnsafeContainer(-1));
                    break;
            }
        }

        /// <summary>
        /// Called from a typical performance test method to provide both Performance Framework measurements as well as
        /// Benchmark Framework measurements. A typical usage is similar to:
        /// <c>[Test, Performance]<br />
        /// [Category("Performance")]<br />
        /// public unsafe void ToNativeArray(<br />
        ///     [Values(1, 2, 4, 8)] int workers,<br />
        ///     [Values(100000, 1000000, 10000000)] int capacity,<br />
        ///     [Values] BenchmarkContainerType type)<br />
        /// {<br />
        ///     BenchmarkContainerRunner&lt;HashSetToNativeArray&gt;.Run(workers, capacity, type);<br />
        /// }</c>
        /// </summary>
        /// <param name="workers">The number of job workers to run performance tests on. These are duplicated across workers rather than split across workers.</param>
        /// <param name="capacity">The capacity for the container(s) which will be passed to setup methods</param>
        /// <param name="type">The benchmark or performance measurement type to run for containers i.e. <see cref="BenchmarkContainerType.Native"/> etc.</param>
        /// <param name="args">Optional arguments that can be stored in a test implementation class.</param>
        /// <remarks>This will run measurements with <see cref="IJob"/> or <see cref="IJobParallelFor"/> based on the number of workers being 1 or 2+, respectively.</remarks>
        public static unsafe void Run(int workers, int capacity, BenchmarkContainerType type, params int[] args)
        {
            if (workers == 1)
                Run(capacity, type, args);
            else
                RunMT(workers, capacity, type, args);
        }
    }
}
