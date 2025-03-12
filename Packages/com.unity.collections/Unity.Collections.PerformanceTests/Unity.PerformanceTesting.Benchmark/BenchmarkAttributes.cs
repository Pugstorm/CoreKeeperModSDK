using System;

namespace Unity.PerformanceTesting.Benchmark
{
    /// <summary>
    /// Mark a class containing performance tests for use in benchmark comparison generation. Each variant defined in the enum Type
    /// will be ran and measured for comparison when running benchmarking. The variants in the enum also create mulitple
    /// appropriate Performance Test Framework tests for regression testing. See <see cref="BenchmarkComparisonAttribute"/> for more information
    /// on the enum definition.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class BenchmarkAttribute : Attribute
    {
        /// <summary>
        /// Specify the enum Type to form benchmark comparisons around.
        /// </summary>
        /// <param name="benchmarkComparisonEnum">The enum Type which defines variants of a performance test to compare with each other in benchmarking</param>
        /// <param name="ignoreInSuite">If true, when <see cref="BenchmarkGenerator.GenerateMarkdown(string, Type, string, string, string, string[])"/> is called
        /// with this `benchmarkComparisonEnum` type, don't include this class of performance in benchmark report generation.</param>
        public BenchmarkAttribute(Type benchmarkComparisonEnum, bool ignoreInSuite = false) { }
    }

    /// <summary>
    /// Mark an enum as defining benchmarking variants.<para />
    /// Each variant defined in the enum Type will be ran and measured for comparison when running benchmarking. The variants in the
    /// enum also create multiple appropriate Performance Test Framework tests for regression testing.<para />
    /// When defining a benchmark, a baseline must also be specified. This can be part of the enum values, or external. Any non-baseline variants meant
    /// for benchmarking only and not for Performance Test Framework tests can be defined external to the enum using <see cref="BenchmarkComparisonExternalAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
    public class BenchmarkComparisonAttribute : Attribute
    {
        /// <summary>
        /// Mark the enum for use in Benchmark comparisons and specify the enum value which will also serve as the baseline for speed-up calculations
        /// </summary>
        /// <param name="baselineEnumValue">The enum value, cast to int, to be the baseline</param>
        public BenchmarkComparisonAttribute(int baselineEnumValue) { }

        /// <summary>
        /// Mark the enum for use in Benchmark comparisons and specify the a non-enum value which will also serve as the baseline for speed-up calculations.
        /// This external value will not be included in Performance Test Framework testing.
        /// </summary>
        /// <param name="externalBaselineValue">The external value, unique from any of the enum values, to be the baseline</param>
        /// <param name="externalBaselineFormat">The string format such as "Native{0}" or "Unsafe{0}" for name formatting in report generation. Only index 0 is supported here.</param>
        public BenchmarkComparisonAttribute(int externalBaselineValue, string externalBaselineFormat) { }
    }

    /// <summary>
    /// Further define a benchmark comparison (see <see cref="BenchmarkComparisonAttribute"/>) which is not defined in the enum and is not a baseline
    /// measurement. Some benchmarks may want to compare against multiple other implementations that aren't intended for Performance Test Framework
    /// and regression testing, so this provides a means of specifying these.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum, AllowMultiple = true, Inherited = false)]
    public class BenchmarkComparisonExternalAttribute : Attribute
    {
        /// <summary>
        /// Specify the value to include in benchmarking which is not defined by the enum itself. See <see cref="BenchmarkComparisonAttribute"/>
        /// </summary>
        /// <param name="externalValue">A value unique to both other external values as well as the enum values</param>
        /// <param name="externalFormat">The string format such as "Native{0}" or "Unsafe{0}" for name formatting in report generation. Only index 0 is supported here.
        /// See <see cref="BenchmarkNameAttribute"/> for more information.</param>
        public BenchmarkComparisonExternalAttribute(int externalValue, string externalFormat) { }
    }

    /// <summary>
    /// Override the display behaviour of benchmarking results. Be default, results are displayed as the median sample in milliseconds with 3 decimal places.
    /// This is a global setting which effects any benchmark defined by this enum. See <see cref="BenchmarkComparisonAttribute"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
    public class BenchmarkComparisonDisplayAttribute : Attribute
    {
        /// <summary>
        /// Specify the display configuration for this benchmark.
        /// </summary>
        /// <param name="unit">Specify the unit type for time measurements, such as milliseconds, seconds, etc. See <see cref="SampleUnit"/></param>
        /// <param name="decimalPlaces">Specify the decimal places for measurement results. Note this is constant and will fill with 0s if needed.</param>
        /// <param name="rankingStatistic">The statistic to display in benchmarking comparisons, such as median, max, etc. See <see cref="BenchmarkRankingStatistic"/></param>
        public BenchmarkComparisonDisplayAttribute(SampleUnit unit, int decimalPlaces, BenchmarkRankingStatistic rankingStatistic) { }
    }

    /// <summary>
    /// Required with each benchmark enum value. Describes the formatting string for naming benchmarks for this comparison type.<para />
    /// For example `BenchmarkName["Native{0}"]` combined with a class named "HashSet" containing performance test/benchmark methods
    /// will generate "NativeHashSet" in the benchmark results table header. To override the behaviour of inserting the class name
    /// into the {0} format parameter, such as if the class is named "HashSetBenchmarks" and the table header should just insert "HashSet"
    /// for the {0} format parameter, use <see cref="BenchmarkNameOverrideAttribute"/> with the class ("HashSetBenchmarks" in this example).<para />
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class BenchmarkNameAttribute : Attribute
    {
        /// <summary>
        /// Define the formatting string for an enum value.
        /// </summary>
        /// <param name="name">The string format such as "Native{0}" or "Unsafe{0}" for name formatting in report generation. Only index 0 is supported here.</param>
        public BenchmarkNameAttribute(string name) { }
    }

    /// <summary>
    /// Overrides the name used in benchmark report table headers as defined with <see cref="BenchmarkNameAttribute"/>. By default the name of the class containing tests is used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class BenchmarkNameOverrideAttribute : Attribute
    {
        /// <summary>
        /// Override the name for all benchmark variants.
        /// </summary>
        /// <param name="name">The name to be used in place of the class name. See <see cref="BenchmarkNameAttribute"/>.</param>
        public BenchmarkNameOverrideAttribute(string name) { }

        /// <summary>
        /// Override the name for a specified benchmark variant.
        /// </summary>
        /// <param name="benchmarkComparisonValue">The enum defined comparison value (<see cref="BenchmarkComparisonAttribute"/> cast to int or the externally defined
        /// comparison value (<see cref="BenchmarkComparisonExternalAttribute"/>)</param>
        /// <param name="name">The name to be used in place of the class name. See <see cref="BenchmarkNameAttribute"/>.</param>
        public BenchmarkNameOverrideAttribute(int benchmarkComparisonValue, string name) { }
    }

    /// <summary>
    /// Generate a footnote for this performance test when used in benchmark report generation. This attribute will always insert
    /// a footnote describing the parameters in the performance test, sans the benchmark comparison enum. For example:<para />
    /// <c>public unsafe void AddGrow(<br />
    /// [Values(4, 65536)] int capacity,<br />
    /// [Values(1024 * 1024)] int growTo,<br />
    /// [Values] BenchmarkContainerType type)<br />
    /// {</c><para />
    /// will generate a footnote with "AddGrow(capacity, growTo)" with any use of this attribute on the `AddGrow` method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class BenchmarkTestFootnoteAttribute : Attribute
    {
        /// <summary>
        /// Generate a footnote describing the parameter names used in the method.
        /// </summary>
        public BenchmarkTestFootnoteAttribute() { }

        /// <summary>
        /// Generate a footnote describing the parameters used in the method, as well as a user-defined description.
        /// </summary>
        /// <param name="description">The user defined description to follow the automatically generated method parameters</param>
        public BenchmarkTestFootnoteAttribute(string description) { }
    }
}
