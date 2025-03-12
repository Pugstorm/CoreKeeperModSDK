using System;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Benchmark;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Collections.PerformanceTests
{
    /// <summary>
    /// Specifies a class containing performance test methods which should be included in allocator benchmarking.<para />
    /// The values specified in this enum are unlikely to be needed in user code, but user code will specify the enum type
    /// in a couple places:<para />
    /// <c>[Benchmark(typeof(BenchmarkAllocatorType))]  // &lt;---- HERE<br />
    /// class FooAllocatorPerformanceTestMethods</c><para />
    /// and<para />
    /// <c>[Test, Performance]<br />
    /// public unsafe void AllocatorPerfTestExample(<br />
    ///     [Values(1, 2, 4, 8)] int workerThreads,<br />
    ///     [Values(1024, 1024 * 1024)] int allocSize,<br />
    ///     [Values] BenchmarkAllocatorType type)  // &lt;---- HERE<br />
    /// {</c><para />
    /// Though values may be specified in the performance test method parameter, it is recommended to leave the argument implicitly
    /// covering all enum values as seen in the example above.
    /// </summary>
    [BenchmarkComparison(BenchmarkAllocatorConfig.Persistent, "Persistent (E)")]
    [BenchmarkComparisonExternal(BenchmarkAllocatorConfig.TempJob, "TempJob (E)")]
    [BenchmarkComparisonExternal(BenchmarkAllocatorConfig.Temp, "Temp (E)")]
    [BenchmarkComparisonDisplay(SampleUnit.Microsecond, 1, BenchmarkAllocatorConfig.kRankingStat)]
    public enum BenchmarkAllocatorType : int
    {
        /// <summary>Allocator performance test will execute on a managed (not burst compiled) code path</summary>
        [BenchmarkName("{0} (S)")] Managed,
        /// <summary>Allocator performance test will execute on a burst compile code path, with safety checks enabled</summary>
        [BenchmarkName("{0} (S+B)")] BurstSafety,
        /// <summary>Allocator performance test will execute on a burst compile code path, with safety checks disabled</summary>
        [BenchmarkName("{0} (B)")] BurstNoSafety,
    }

    internal static class BenchmarkAllocatorConfig
    {
        internal const int Temp = -1;
        internal const int TempJob = -2;
        internal const int Persistent = -3;

        internal const BenchmarkRankingStatistic kRankingStat = BenchmarkRankingStatistic.Min;
        internal const int kCountWarmup = 5;
        internal const int kCountMeasure = 50;
#if UNITY_STANDALONE || UNITY_EDITOR
        internal const int kCountAllocations = 150;
#else
        // Still allows allocator tests on non-desktop platforms, but with a much lower memory requirement
        internal const int kCountAllocations = 25;
#endif

#if UNITY_EDITOR
        [UnityEditor.MenuItem("DOTS/Unity.Collections/Generate Allocator Benchmarks")]
#endif
        static void RunBenchmarks()
        {
            BenchmarkGenerator.GenerateMarkdown(
                "Allocators",
                typeof(BenchmarkAllocatorType),
                "../../Packages/com.unity.collections/Documentation~/performance-comparison-allocators.md",
                $"The following benchmarks make **{kCountAllocations} consecutive allocations** per sample set."
                    + $"<br/>Multithreaded benchmarks make the full **{kCountAllocations} consecutive allocations *per worker thread*** per sample set."
                    + $"<br/>The **{kRankingStat} of {kCountMeasure} sample sets** is compared against the baseline on the far right side of the table."
                    + $"<br/>{kCountWarmup} extra sample sets are run as warmup."
                    ,
                "Legend",
                new string[]
                {
                    "`(S)` = Safety Enabled",
                    "`(B)` = Burst Compiled *with Safety Disabled*",
                    "`(S+B)` = Burst Compiled *with Safety Enabled*",
                    "`(E)` = Engine Provided",
                    "",
                    "*`italic`* results are for benchmarking comparison only; these are not included in standard Performance Framework tests",
                });
        }
    }

    /// <summary>
    /// Interface to implement allocator performance tests which will run using <see cref="BenchmarkAllocatorRunner{T}.Run(BenchmarkAllocatorType, int, int, int[])"/>.
    /// Deriving tests from this interface enables both Performance Test Framework and Benchmark Framework to generate and run
    /// tests for the contexts described by <see cref="BenchmarkAllocatorType"/>.
    /// </summary>
    public interface IBenchmarkAllocator
    {
        /// <summary>
        /// Override this to add extra int arguments to a performance test implementation as fields in the implementing type. These arguments
        /// are optionally passed in through <see cref="BenchmarkAllocatorRunner{T}.Run(BenchmarkAllocatorType, int, int, int[])"/>.
        /// </summary>
        /// <param name="args">A variable number of extra arguments to passed through to the test implementation</param>
        public void SetParams(params int[] args) { }

        /// <summary>
        /// Used to create the allocator used in performance testing.
        /// </summary>
        /// <param name="builtinOverride">When this is <see cref="Allocator.None"/>, create the custom allocator type.
        /// Otherwise use the provided <see cref="Allocator"/> enum for allocations in performance testing.</param>
        public void CreateAllocator(Allocator builtinOverride);

        /// <summary>
        /// Used to free memory and destroy the custom allocator if it wasn't allocated with an <see cref="Allocator"/> type.
        /// </summary>
        public void DestroyAllocator();

        /// <summary>
        /// Actions performed prior to each measurement of a sample set. Typically used to set up initial state to ensure each sample measured is executed in the same way.
        /// </summary>
        /// <param name="workers">Number of job workers for this allocation test. Work is duplicated across job workers rather than split across job workers.</param>
        /// <param name="size">The base size of each allocation in a single measurement.</param>
        /// <param name="allocations">The number of allocations in a single measurement.</param>
        public void Setup(int workers, int size, int allocations);

        /// <summary>
        /// Actions performed following each measurement of a sample set. Typically used to dispose or invalidate the state set up during <see cref="Setup(int, int, int)"/>.
        /// </summary>
        public void Teardown();

        /// <summary>
        /// The code which will be executed during performance measurement. This should usually be general enough to work with any allocator, so if making
        /// allocations or freeing, the recommendation is to interface through <see cref="AllocatorManager"/>.
        /// </summary>
        /// <param name="workerI"></param>
        public void Measure(int workerI);
    }

    /// <summary>
    /// Provides the API for running allocator based Performance Framework tests and Benchmark Framework measurements.
    /// This will typically be the sole call from a performance test. See <see cref="Run(BenchmarkAllocatorType, int, int, int[])"/>
    /// for more information.
    /// </summary>
    /// <typeparam name="T">An implementation conforming to the <see cref="IBenchmarkAllocator"/> interface for running allocator performance tests and benchmarks.</typeparam>
    [BurstCompile(CompileSynchronously = true)]
    public static class BenchmarkAllocatorRunner<T> where T : unmanaged, IBenchmarkAllocator
    {
        internal unsafe struct JobST : IJob
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute() => methods->Measure(0);
        }
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
        internal unsafe struct JobBurstST : IJob
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute() => methods->Measure(0);
        }
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = false)]
        internal unsafe struct JobSafetyBurstST : IJob
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute() => methods->Measure(0);
        }
        internal unsafe struct JobMT : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute(int index) => methods->Measure(index);
        }
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
        internal unsafe struct JobBurstMT : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute(int index) => methods->Measure(index);
        }
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = false)]
        internal unsafe struct JobSafetyBurstMT : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute(int index) => methods->Measure(index);
        }

        static unsafe void RunST(BenchmarkAllocatorType type, int baseSize, int allocations, params int[] args)
        {
            var methods = new T();
            methods.SetParams(args);

            switch (type)
            {
                case (BenchmarkAllocatorType)(BenchmarkAllocatorConfig.Temp):
                    methods.CreateAllocator(Allocator.Temp);
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkAllocatorConfig.kCountWarmup, BenchmarkAllocatorConfig.kCountMeasure,
                        () => new JobST { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule().Complete(),
                        () => methods.Setup(1, baseSize, allocations), () => methods.Teardown());
                    break;
                case (BenchmarkAllocatorType)(BenchmarkAllocatorConfig.TempJob):
                    methods.CreateAllocator(Allocator.TempJob);
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkAllocatorConfig.kCountWarmup, BenchmarkAllocatorConfig.kCountMeasure,
                        () => new JobST { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule().Complete(),
                        () => methods.Setup(1, baseSize, allocations), () => methods.Teardown());
                    break;
                case (BenchmarkAllocatorType)(BenchmarkAllocatorConfig.Persistent):
                    methods.CreateAllocator(Allocator.Persistent);
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkAllocatorConfig.kCountWarmup, BenchmarkAllocatorConfig.kCountMeasure,
                        () => new JobST { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule().Complete(),
                        () => methods.Setup(1, baseSize, allocations), () => methods.Teardown());
                    break;
                case BenchmarkAllocatorType.Managed:
                    methods.CreateAllocator(Allocator.None);
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkAllocatorConfig.kCountWarmup, BenchmarkAllocatorConfig.kCountMeasure,
                        () => new JobST { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule().Complete(),
                        () => methods.Setup(1, baseSize, allocations), () => methods.Teardown());
                    break;
                case BenchmarkAllocatorType.BurstSafety:
                    methods.CreateAllocator(Allocator.None);
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkAllocatorConfig.kCountWarmup, BenchmarkAllocatorConfig.kCountMeasure,
                        () => new JobSafetyBurstST { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Run(),
                        () => methods.Setup(1, baseSize, allocations), () => methods.Teardown());
                    break;
                case BenchmarkAllocatorType.BurstNoSafety:
                    methods.CreateAllocator(Allocator.None);
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkAllocatorConfig.kCountWarmup, BenchmarkAllocatorConfig.kCountMeasure,
                        () => new JobBurstST { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Run(),
                        () => methods.Setup(1, baseSize, allocations), () => methods.Teardown());
                    break;
            }

            methods.DestroyAllocator();
        }

        static unsafe void RunMT(BenchmarkAllocatorType type, int baseSize, int allocations, int workers, params int[] args)
        {
            var methods = new T();
            methods.SetParams(args);

            switch (type)
            {
                case (BenchmarkAllocatorType)(BenchmarkAllocatorConfig.Temp):
                    methods.CreateAllocator(Allocator.Temp);
                    BenchmarkMeasure.MeasureParallel(typeof(T),
                        BenchmarkAllocatorConfig.kCountWarmup, BenchmarkAllocatorConfig.kCountMeasure,
                        () => new JobMT { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule(workers, 1).Complete(),
                        () => methods.Setup(workers, baseSize, allocations), () => methods.Teardown());
                    break;
                case (BenchmarkAllocatorType)(BenchmarkAllocatorConfig.TempJob):
                    methods.CreateAllocator(Allocator.TempJob);
                    BenchmarkMeasure.MeasureParallel(typeof(T),
                        BenchmarkAllocatorConfig.kCountWarmup, BenchmarkAllocatorConfig.kCountMeasure,
                        () => new JobMT { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule(workers, 1).Complete(),
                        () => methods.Setup(workers, baseSize, allocations), () => methods.Teardown());
                    break;
                case (BenchmarkAllocatorType)(BenchmarkAllocatorConfig.Persistent):
                    methods.CreateAllocator(Allocator.Persistent);
                    BenchmarkMeasure.MeasureParallel(typeof(T),
                        BenchmarkAllocatorConfig.kCountWarmup, BenchmarkAllocatorConfig.kCountMeasure,
                        () => new JobMT { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule(workers, 1).Complete(),
                        () => methods.Setup(workers, baseSize, allocations), () => methods.Teardown());
                    break;
                case BenchmarkAllocatorType.Managed:
                    methods.CreateAllocator(Allocator.None);
                    BenchmarkMeasure.MeasureParallel(typeof(T),
                        BenchmarkAllocatorConfig.kCountWarmup, BenchmarkAllocatorConfig.kCountMeasure,
                        () => new JobMT { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule(workers, 1).Complete(),
                        () => methods.Setup(workers, baseSize, allocations), () => methods.Teardown());
                    break;
                case BenchmarkAllocatorType.BurstSafety:
                    methods.CreateAllocator(Allocator.None);
                    BenchmarkMeasure.MeasureParallel(typeof(T),
                        BenchmarkAllocatorConfig.kCountWarmup, BenchmarkAllocatorConfig.kCountMeasure,
                        () => new JobSafetyBurstMT { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule(workers, 1).Complete(),
                        () => methods.Setup(workers, baseSize, allocations), () => methods.Teardown());
                    break;
                case BenchmarkAllocatorType.BurstNoSafety:
                    methods.CreateAllocator(Allocator.None);
                    BenchmarkMeasure.MeasureParallel(typeof(T),
                        BenchmarkAllocatorConfig.kCountWarmup, BenchmarkAllocatorConfig.kCountMeasure,
                        () => new JobBurstMT { methods = (T*)UnsafeUtility.AddressOf(ref methods) }.Schedule(workers, 1).Complete(),
                        () => methods.Setup(workers, baseSize, allocations), () => methods.Teardown());
                    break;
            }

            methods.DestroyAllocator();
        }

        /// <summary>
        /// Called from a typical performance test method to provide both Performance Framework measurements as well as
        /// Benchmark Framework measurements. A typical usage is similar to:
        /// <c>[Test, Performance]<br />
        /// [Category("Performance")]<br />
        /// [BenchmarkTestFootnote]<br />
        /// public unsafe void FixedSize(<br />
        ///     [Values(1, 2, 4, 8)] int workerThreads,<br />
        ///     [Values(1024, 1024 * 1024)] int allocSize,<br />
        ///     [Values] BenchmarkAllocatorType type)<br />
        /// {<br />
        ///     BenchmarkAllocatorRunner&lt;Rewindable_FixedSize&gt;.Run(type, allocSize, workerThreads);<br />
        /// }</c>
        /// </summary>
        /// <param name="type">The benchmark or performance measurement type to run for allocators i.e. <see cref="BenchmarkAllocatorType.Managed"/> etc.</param>
        /// <param name="baseSize">The size to base allocations off of, whether fixed for all allocations, increasing in size, or anything else.</param>
        /// <param name="workers">The number of job workers to run performance tests on. These are duplicated across workers rather than split across workers.</param>
        /// <param name="args">Optional arguments that can be stored in a test implementation class.</param>
        public static unsafe void Run(BenchmarkAllocatorType type, int baseSize, int workers, params int[] args)
        {
            if (workers == 1)
                RunST(type, baseSize, BenchmarkAllocatorConfig.kCountAllocations, args);
            else
                RunMT(type, baseSize, BenchmarkAllocatorConfig.kCountAllocations, workers, args);
        }
    }

    /// <summary>
    /// A useful set of functionality commonly found in allocator performance and benchmark tests for most allocator types. Typically
    /// wrapped in a separate utility class for a set of tests to a specific allocator type.
    /// </summary>
    public struct BenchmarkAllocatorUtil
    {
        /// <summary>
        /// [worker][sequential allocation]<para />
        /// Used to store the pointer from allocations so it may be freed later.
        /// </summary>
        public NativeArray<NativeArray<IntPtr>> AllocPtr { get; private set; }

        /// <summary>
        /// [sequential allocation]<para />
        /// Used to store the size of allocations so it may be freed later, as some allocators require the size to be explicitly given when freed.
        /// Separate arrays for each worker are not provided because workers duplicate the same work rather than splitting it in some manner.
        /// </summary>
        public NativeArray<int> AllocSize { get; private set; }

        /// <summary>
        /// To be called prior to each measurement. Sets up the allocation and size storage used for freeing allocations, whether this happens
        /// during teardown following each measurement, or freeing is the functionality being measured itself.
        /// </summary>
        /// <param name="workers">The number of job workers to run performance tests on. These are duplicated across workers rather than split across workers.</param>
        /// <param name="baseSize">The size to base allocations off of, whether fixed for all allocations, increasing in size, or anything else.</param>
        /// <param name="growthRate">
        /// - If &lt; 0, a performance measurement's allocations start at the largest size and decrease linearly to the `baseSize`.
        /// - If &gt; 0, a performance measurement's allocations start at the `baseSize` and increase linearly
        /// - If 0, the allocation size is equivalent to the `baseSize` for all of a performance measurement's allocations
        /// </param>
        /// <param name="allocations">The number of allocations in a single measurement.</param>
        public void Setup(int workers, int baseSize, int growthRate, int allocations)
        {
            var allocStorage = new NativeArray<NativeArray<IntPtr>>(workers, Allocator.Persistent);
            for (int i = 0; i < workers; i++)
                allocStorage[i] = new NativeArray<IntPtr>(allocations, Allocator.Persistent);
            AllocPtr = allocStorage;

            var sizeStorage = new NativeArray<int>(allocations, Allocator.Persistent);
            for (int i = 0; i < allocations; i++)
            {
                if (growthRate >= 0)
                    sizeStorage[i] = baseSize + growthRate * i;
                else
                    sizeStorage[i] = baseSize + (-growthRate * (allocations - 1)) + growthRate * i;
            }
            AllocSize = sizeStorage;
        }

        /// <summary>
        /// To be called following each measurement. Frees the memory allocated in the <see cref="Setup(int, int, int, int)"/> method.
        /// This also frees the memory allocated by the given allocator using the stored information in this class.
        /// </summary>
        /// <param name="allocator">A handle to the allocator being measured.</param>
        unsafe public void Teardown(AllocatorManager.AllocatorHandle allocator)
        {
            if (AllocPtr.IsCreated)
            {
                for (int i = 0; i < AllocPtr.Length; i++)
                {
                    var inner = AllocPtr[i];
                    for (int j = 0; j < inner.Length; j++)
                    {
                        AllocatorManager.Free(allocator, (void*)inner[j], AllocSize[j], 0);
                        inner[j] = IntPtr.Zero;
                    }
                }
            }
            Teardown();
        }

        /// <summary>
        /// To be called following each measurement. Frees the memory allocated in the <see cref="Setup(int, int, int, int)"/> method.
        /// This does not free the memory allocated by a given allocator type used in measurement tests.
        /// </summary>
        public void Teardown()
        {
            if (AllocPtr.IsCreated)
            {
                for (int i = 0; i < AllocPtr.Length; i++)
                {
                    if (AllocPtr[i].IsCreated)
                        AllocPtr[i].Dispose();
                }
                AllocPtr.Dispose();
            }
            if (AllocSize.IsCreated)
                AllocSize.Dispose();
        }
    }
}
