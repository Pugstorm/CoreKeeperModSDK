using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.PerformanceTesting.Benchmark
{
    /// <summary>
    /// Specifies the statistic used for benchmark comparisons.
    /// </summary>
    public enum BenchmarkRankingStatistic
    {
        /// <summary>Compare the minimum time from a set of samples</summary>
        Min,
        /// <summary>Compare the maximum time from a set of samples</summary>
        Max,
        /// <summary>Compare the median time from a set of samples</summary>
        Median,
        /// <summary>Compare the average time from a set of samples</summary>
        Average,
        /// <summary>Compare the standard deviation of time from a set of samples</summary>
        StdDev,
        /// <summary>Compare the sum time of a set of samples</summary>
        Sum,
    }

    internal enum BenchmarkResultType
    {
        Ignored,
        Normal,
        NormalBaseline,
        External,
        ExternalBaseline,
    }

    internal enum BenchmarkRankingType
    {
        Ignored,
        Normal,
        Best,
        Worst,
    }

    internal struct BenchmarkResults
    {
        public const uint kFlagNoOptimization = 0x01;
        public const uint kFlagParallelJobs = 0x02;
        public const uint kFlagFootnotes = 0x04;  // this must always be the last predefined flag bit

        public SampleUnit unit;
        public double min;
        public double max;
        public double median;
        public double average;
        public double standardDeviation;
        public double sum;
        public BenchmarkRankingType ranking;
        public BenchmarkRankingStatistic statistic;
        public double baselineRatio;
        public uint resultFlags;

        internal static readonly BenchmarkResults Ignored = new BenchmarkResults { ranking = BenchmarkRankingType.Ignored };

        internal BenchmarkResults(SampleGroup sampleGroup, BenchmarkRankingStatistic rankingStatistic, uint flags)
        {
            unit = sampleGroup.Unit;
            min = sampleGroup.Min;
            max = sampleGroup.Max;
            median = sampleGroup.Median;
            average = sampleGroup.Average;
            standardDeviation = sampleGroup.StandardDeviation;
            sum = sampleGroup.Sum;
            ranking = BenchmarkRankingType.Normal;
            statistic = rankingStatistic;
            baselineRatio = 0;
            resultFlags = flags;
        }

        public double Comparator
        {
            get
            {
                switch (statistic)
                {
                    case BenchmarkRankingStatistic.Min: return min;
                    case BenchmarkRankingStatistic.Max: return max;
                    case BenchmarkRankingStatistic.Median: return median;
                    case BenchmarkRankingStatistic.Average: return average;
                    case BenchmarkRankingStatistic.StdDev: return standardDeviation;
                    case BenchmarkRankingStatistic.Sum: return sum;
                }
                return median;
            }
        }

        public string UnitSuffix
        {
            get
            {
                switch (unit)
                {
                    case SampleUnit.Nanosecond: return "ns";
                    case SampleUnit.Microsecond: return "µs";
                    case SampleUnit.Millisecond: return "ms";
                    case SampleUnit.Second: return "s";
                    case SampleUnit.Byte: return "b";
                    case SampleUnit.Kilobyte: return "kb";
                    case SampleUnit.Megabyte: return "mb";
                    case SampleUnit.Gigabyte: return "gb";
                    case SampleUnit.Undefined:
                        break;
                }
                return "";
            }
        }
    }

    internal struct BenchmarkReportComparison : IDisposable
    {
        public UnsafeList<BenchmarkResults> results;
        public FixedString512Bytes comparisonName;
        public uint footnoteFlags;

        public BenchmarkReportComparison(string name)
        {
            results = new UnsafeList<BenchmarkResults>(1, Allocator.Persistent);
            comparisonName = name;
            footnoteFlags = 0;
        }

        public void Dispose()
        {
            if (results.IsCreated)
                results.Dispose();
        }

        public void RankResults(BenchmarkResultType[] resultTypes)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            int baselineJ = -1;
            int firstJ = -1;

            for (int j = 0; j < results.Length; j++)
            {
                if (results[j].ranking == BenchmarkRankingType.Ignored)
                    continue;
                if (firstJ == -1)
                    firstJ = j;

                double result = results[j].Comparator;
                if (result < min)
                    min = result;
                if (result > max)
                    max = result;

                if (resultTypes[j] == BenchmarkResultType.ExternalBaseline || resultTypes[j] == BenchmarkResultType.NormalBaseline)
                {
                    if (baselineJ == -1)
                        baselineJ = j;
                    else
                        throw new Exception("[INTERNAL ERROR] More than one baseline found - this should have been caught during initialization");
                }
            }

            bool same = true;
            for (int prevJ = firstJ, j = firstJ + 1; j < results.Length; j++)
            {
                if (results[j].ranking == BenchmarkRankingType.Ignored)
                    continue;
                if (results[prevJ].Comparator != results[j].Comparator)
                    same = false;
                prevJ = j;
            }
            if (!same)
            {
                for (int j = 0; j < results.Length; j++)
                {
                    if (results[j].ranking == BenchmarkRankingType.Ignored)
                        continue;
                    if (results[j].Comparator == min)
                        results.ElementAt(j).ranking = BenchmarkRankingType.Best;
                    else if (results[j].Comparator == max)
                        results.ElementAt(j).ranking = BenchmarkRankingType.Worst;
                }
            }

            if (baselineJ == -1)
                throw new Exception("[INTERNAL ERROR] No baseline found - this should have been caught during initialization");

            for (int j = 0; j < results.Length; j++)
            {
                if (results[j].ranking == BenchmarkRankingType.Ignored)
                    continue;
                if (results[j].Comparator != 0)
                    results.ElementAt(j).baselineRatio = results[baselineJ].Comparator / results[j].Comparator;
            }
        }
    }

    internal struct BenchmarkReportGroup : IDisposable
    {
        public UnsafeList<BenchmarkReportComparison> comparisons;
        public FixedString512Bytes groupName;
        public UnsafeList<FixedString64Bytes> variantNames;
        public UnsafeList<BenchmarkResultType> resultTypes;
        public int resultDecimalPlaces;
        public UnsafeHashMap<uint, NativeText> customFootnotes;

        public BenchmarkReportGroup(string name, string[] variantNameArray, BenchmarkResultType[] resultTypeArray, int resultDecimalPlaces)
        {
            comparisons = new UnsafeList<BenchmarkReportComparison>(1, Allocator.Persistent);
            groupName = name;
            variantNames = new UnsafeList<FixedString64Bytes>(variantNameArray.Length, Allocator.Persistent);
            resultTypes = new UnsafeList<BenchmarkResultType>(resultTypeArray.Length, Allocator.Persistent);
            this.resultDecimalPlaces = resultDecimalPlaces;
            foreach (var title in variantNameArray)
                variantNames.Add(title);
            foreach (var resultType in resultTypeArray)
                resultTypes.Add(resultType);
            customFootnotes = new UnsafeHashMap<uint, NativeText>(30, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (comparisons.IsCreated)
            {
                for (int i = 0; i < comparisons.Length; i++)
                    comparisons[i].Dispose();
                comparisons.Dispose();
            }
            if (variantNames.IsCreated)
                variantNames.Dispose();
            if (customFootnotes.IsCreated)
            {
                foreach (var pair in customFootnotes)
                    pair.Value.Dispose();
                customFootnotes.Dispose();
            }
        }
    }

    internal struct BenchmarkReports : IDisposable
    {
        public UnsafeList<BenchmarkReportGroup> groups;
        public FixedString512Bytes reportName;
        
        public BenchmarkReports(string name)
        {
            groups = new UnsafeList<BenchmarkReportGroup>(1, Allocator.Persistent);
            reportName = name;
        }

        public void Dispose()
        {
            if (groups.IsCreated)
            {
                for (int i = 0; i < groups.Length; i++)
                    groups[i].Dispose();
                groups.Dispose();
            }
        }
    }
}
