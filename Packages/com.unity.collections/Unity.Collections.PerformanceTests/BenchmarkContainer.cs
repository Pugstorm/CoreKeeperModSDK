using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Benchmark;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Collections.PerformanceTests
{
    /// <summary>
    /// Specifies a class containing performance test methods which should be included in container benchmarking.<para />
    /// The values specified in this enum are unlikely to be needed in user code, but user code will specify the enum type
    /// in a couple places:<para />
    /// <c>[Benchmark(typeof(BenchmarkContainerType))]  // &lt;---- HERE<br />
    /// class FooContainerPerformanceTestMethods</c><para />
    /// and<para />
    /// <c>[Test, Performance]<br />
    /// public unsafe void ContainerPerfTestExample(<br />
    ///     [Values(100000, 1000000, 10000000)] int capacity,<br />
    ///     [Values] BenchmarkContainerType type)  // &lt;---- HERE<br />
    /// {</c><para />
    /// Though values may be specified in the performance test method parameter, it is recommended to leave the argument implicitly
    /// covering all enum values as seen in the example above.
    /// </summary>
    [BenchmarkComparison(BenchmarkContainerConfig.BCL, "{0} (BCL)")]
    [BenchmarkComparisonDisplay(SampleUnit.Millisecond, 3, BenchmarkContainerConfig.kRankingMethod)]
    public enum BenchmarkContainerType : int
    {
        /// <summary>Native container performance test will execute on a managed (not burst compiled) code path</summary>
        [BenchmarkName("Native{0} (S)")] Native,
        /// <summary>Native container performance test will execute on a burst compile code path, with safety checks enabled</summary>
        [BenchmarkName("Native{0} (S+B)")] NativeBurstSafety,
        /// <summary>Native container performance test will execute on a burst compile code path, with safety checks disabled</summary>
        [BenchmarkName("Native{0} (B)")] NativeBurstNoSafety,
        /// <summary>Unsafe container performance test will execute on a managed (not burst compiled) code path</summary>
        [BenchmarkName("Unsafe{0} (S)")] Unsafe,
        /// <summary>Unsafe container performance test will execute on a burst compile code path, with safety checks enabled</summary>
        [BenchmarkName("Unsafe{0} (S+B)")] UnsafeBurstSafety,
        /// <summary>Unsafe container performance test will execute on a burst compile code path, with safety checks disabled</summary>
        [BenchmarkName("Unsafe{0} (B)")] UnsafeBurstNoSafety,
    }

    /// <summary>
    /// Configuration settings for benchmarking containers.
    /// </summary>
    public static class BenchmarkContainerConfig
    {
        /// <summary>
        /// An additional value to the enum values defined in <see cref="BenchmarkContainerType"/> which will not be included
        /// in Performance Test Framework test generation but will be included in Benchmark Framework result generation.
        /// </summary>
        public const int BCL = -1;

        internal const BenchmarkRankingStatistic kRankingMethod = BenchmarkRankingStatistic.Median;
        internal const int kCountWarmup = 5;
        internal const int kCountMeasure = 10;

        /// <summary>
        /// Prefix string for individual benchmark menu items
        /// </summary>
        public const string kMenuItemIndividual = "DOTS/Unity.Collections/Generate Individual Container Benchmark/";

#if UNITY_EDITOR
        [UnityEditor.MenuItem("DOTS/Unity.Collections/Generate Container Benchmarks")]
#endif
        static void RunBenchmarks() =>
            BenchmarkGenerator.GenerateMarkdown(
                "Containers",
                typeof(BenchmarkContainerType),
                "../../Packages/com.unity.collections/Documentation~/performance-comparison-containers.md",
                $"The **{kRankingMethod} of {kCountMeasure} sample sets** is compared against the baseline on the far right side of the table."
                    + $"<br/>Multithreaded benchmarks divide the processing amongst the specified number of workers."
                    + $"<br/>{kCountWarmup} extra sample sets are run as warmup."
                    ,
                "Legend",
                new string[]
                {
                    "`(S)` = Safety Enabled",
                    "`(B)` = Burst Compiled *with Safety Disabled*",
                    "`(S+B)` = Burst Compiled *with Safety Enabled*",
                    "`(BCL)` = Base Class Library implementation (such as provided by Mono or .NET)",
                    "",
                    "*`italic`* results are for benchmarking comparison only; these are not included in standard Performance Framework tests",
                });

#if UNITY_EDITOR
        /// <summary>
        /// Runs a benchmark for a particular class with Performance Test Framework/Benchmark methods. For calling
        /// from custom menu items, for instance.
        /// </summary>
        /// <param name="testClassType"></param>
        public static void RunBenchmark(System.Type testClassType)
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(
                UnityEditor.FileUtil.GetUniqueTempPathInProject()), $"Benchmark{testClassType.Name}.md");
            BenchmarkGenerator.GenerateMarkdown(
                $"Individual -- {testClassType.Name}",
                new System.Type[] { testClassType },
                tempPath,
                $"The **{kRankingMethod} of {kCountMeasure} sample sets** is compared against the baseline on the far right side of the table."
                    + $"<br/>Multithreaded benchmarks divide the processing amongst the specified number of workers."
                    + $"<br/>{kCountWarmup} extra sample sets are run as warmup."
                    ,
                "Legend",
                new string[]
                {
                "`(S)` = Safety Enabled",
                "`(B)` = Burst Compiled *with Safety Disabled*",
                "`(S+B)` = Burst Compiled *with Safety Enabled*",
                "`(BCL)` = Base Class Library implementation (such as provided by Mono or .NET)",
                "",
                "*`italic`* results are for benchmarking comparison only; these are not included in standard Performance Framework tests",
                });
            UnityEditor.EditorUtility.RevealInFinder(tempPath);
        }
#endif
    }

    /// <summary>
    /// Interface to implement container performance tests which will run using <see cref="BenchmarkContainerRunner{T}.Run(int, BenchmarkContainerType, int[])"/>.
    /// Deriving tests from this interface enables both Performance Test Framework and Benchmark Framework to generate and run
    /// tests for the contexts described by <see cref="BenchmarkContainerType"/>.
    /// </summary>
    public interface IBenchmarkContainer
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
        public void AllocUnsafeContainer(int capacity);  // capacity 0 frees

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
        public void MeasureNativeContainer();

        /// <summary>
        /// The code which will be executed during performance measurement. This should usually be general enough to
        /// work with any unsafe container.
        /// </summary>
        public void MeasureUnsafeContainer();

        /// <summary>
        /// The code which will be executed during performance measurement. This should usually be general enough to
        /// work with any managed container provided by the Base Class Library (BCL).
        /// </summary>
        /// <param name="container">A reference to the managed container allocated in <see cref="AllocBclContainer(int)"/></param>
        public void MeasureBclContainer(object container);
    }

    /// <summary>
    /// Provides the API for running container based Performance Framework tests and Benchmark Framework measurements.
    /// This will typically be the sole call from a performance test. See <see cref="Run(int, BenchmarkContainerType, int[])"/>
    /// for more information.
    /// </summary>
    /// <typeparam name="T">An implementation conforming to the <see cref="IBenchmarkContainer"/> interface for running container performance tests and benchmarks.</typeparam>
    [BurstCompile(CompileSynchronously = true)]
    public static class BenchmarkContainerRunner<T> where T : unmanaged, IBenchmarkContainer
    {
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
        unsafe struct NativeJobBurstST : IJob
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute() => methods->MeasureNativeContainer();
        }
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = false)]
        unsafe struct NativeJobSafetyBurstST : IJob
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute() => methods->MeasureNativeContainer();
        }

        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
        unsafe struct UnsafeJobBurstST : IJob
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute() => methods->MeasureUnsafeContainer();
        }
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = false)]
        unsafe struct UnsafeJobSafetyBurstST : IJob
        {
            [NativeDisableUnsafePtrRestriction] public T* methods;
            public void Execute() => methods->MeasureUnsafeContainer();
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
                        () => methods.MeasureBclContainer(container),
                        () => container = methods.AllocBclContainer(capacity), () => container = methods.AllocBclContainer(-1));
                    break;
                case BenchmarkContainerType.Native:
                    BenchmarkMeasure.Measure(typeof(T),
                        BenchmarkContainerConfig.kCountWarmup, BenchmarkContainerConfig.kCountMeasure,
                        () => methods.MeasureNativeContainer(),
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
                        () => methods.MeasureUnsafeContainer(),
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
    }
}
