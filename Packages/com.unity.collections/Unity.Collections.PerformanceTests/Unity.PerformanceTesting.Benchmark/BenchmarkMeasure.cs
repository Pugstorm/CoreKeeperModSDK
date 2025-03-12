using Unity.PerformanceTesting.Runtime;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;

namespace Unity.PerformanceTesting.Benchmark
{
    /// <summary>
    /// An interface for performing measurements which works from Performance Test Framework or from the Benchmark Framework.<para />
    /// This functionality is intended to be wrapped in an implemenation specific to a type of benchmark comparison. See some of the included
    /// benchmarking implementations in <see cref="Unity.Collections.PerformanceTests"/> such as BenchmarkContainerRunner or BenchmarkAllocatorRunner, as well as
    /// the documentation in the Benchmark Framework repository for examples.
    /// </summary>
    public static class BenchmarkMeasure
    {
        internal static bool ForBenchmarks = false;
        private static SampleGroup LastResultsInternal;
        private static uint LastResultsFootnotes;

        internal static BenchmarkResults CalculateLastResults(SampleUnit unit, BenchmarkRankingStatistic statistic)
        {
            for (int i = 0; i < LastResultsInternal.Samples.Count; i++)
                LastResultsInternal.Samples[i] = Utils.ConvertSample(SampleUnit.Second, unit, LastResultsInternal.Samples[i]);
            LastResultsInternal.Unit = unit;
            Utils.UpdateStatistics(LastResultsInternal);

            return new BenchmarkResults(LastResultsInternal, statistic, LastResultsFootnotes);
        }

        /// <summary>
        /// Measure a set of samples for a given performance test. This functions correctly whether called through the Performance Test Framework
        /// by the Unity Test Runner, or if it is called through the Benchmark framework.<para />
        /// This must be called when a test will be run in a parallel job, as it marks the results with a note specifying the irregularity
        /// of parallel jobs due to work stealing.<para />
        /// When running a single threaded test (job or otherwise), use <see cref="Measure(Type, int, int, Action, Action, Action)"/>
        /// </summary>
        /// <param name="perfMeasureType">A type which contains a single performance test's implementation</param>
        /// <param name="warmup">The number of warm up runs prior to collecting sample data</param>
        /// <param name="measurements">The number of runs to collect sample data from</param>
        /// <param name="action">The specific per-sample method to run for measurement</param>
        /// <param name="setup">A per-sample setup method that will not be part of measurement</param>
        /// <param name="teardown">A per-sample teardown method that will not be part of measurement</param>
        public static void MeasureParallel(Type perfMeasureType, int warmup, int measurements, Action action, Action setup = null, Action teardown = null)
        {
            Measure(perfMeasureType, warmup, measurements, action, setup, teardown);
            if (ForBenchmarks)
                LastResultsFootnotes |= BenchmarkResults.kFlagParallelJobs;
        }

        /// <summary>
        /// Measure a set of samples for a given performance test. This functions correctly whether called through the Performance Test Framework
        /// by the Unity Test Runner, or if it is called through the Benchmark framework.<para />
        /// This must not be called when a test will be run in a parallel job, as this does not mark the results with a note specifying the irregularity
        /// of parallel jobs due to work stealing.<para />
        /// When running a multithreaded test, use <see cref="MeasureParallel(Type, int, int, Action, Action, Action)"/>
        /// </summary>
        /// <param name="perfMeasureType">A type which contains a single performance test's implementation</param>
        /// <param name="warmup">The number of warm up runs prior to collecting sample data</param>
        /// <param name="measurements">The number of runs to collect sample data from</param>
        /// <param name="action">The specific per-sample method to run for measurement</param>
        /// <param name="setup">A per-sample setup method that will not be part of measurement</param>
        /// <param name="teardown">A per-sample teardown method that will not be part of measurement</param>
        public static void Measure(Type perfMeasureType, int warmup, int measurements, Action action, Action setup = null, Action teardown = null)
        {
            if (ForBenchmarks)
            {
                SampleGroup results = new SampleGroup(perfMeasureType.Name, SampleUnit.Second, false);
                results.Samples = new List<double>(measurements);

                Stopwatch stopwatch = Stopwatch.StartNew();

                for (int i = 0; i < warmup; i++)
                {
                    setup?.Invoke();
                    action();
                    teardown?.Invoke();
                }
                for (int i = 0; i < measurements; i++)
                {
                    setup?.Invoke();

                    stopwatch.Restart();
                    action();
                    results.Samples.Add(stopwatch.Elapsed.TotalSeconds);

                    teardown?.Invoke();
                }

                LastResultsInternal = results;
                LastResultsFootnotes = 0;

                // Check if NoOptimization is part of this measurement. MethodImplAttribute is not found in
                // CustomAttributes, and instead is a special-case found in MethodImplementationFlags.
                var methods = perfMeasureType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (var m in methods)
                {
                    if (m.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoOptimization))
                    {
                        LastResultsFootnotes |= BenchmarkResults.kFlagNoOptimization;
                        break;
                    }
                }
            }
            else
            {
                PerformanceTesting.Measure.Method(action)
                    .SampleGroup(perfMeasureType.Name)
                    .SetUp(setup)
                    .CleanUp(teardown)
                    .WarmupCount(warmup)
                    .MeasurementCount(measurements)
                    .Run();
            }
        }
    }
}
