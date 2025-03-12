using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace Unity.PerformanceTesting.Benchmark
{
    /// <summary>
    /// Generates and saves a markdown file after running benchmarks.
    /// </summary>
    public static class BenchmarkGenerator
    {
        // This must have the same number of elements as there are bits in the flags parameter for GetFlagSuperscripts
        static string[] superscripts = { "¹", "²", "³", "⁴", "⁵", "⁶", "⁷", "⁸", "⁹",
            "¹⁰", "¹¹", "¹²", "¹³", "¹⁴", "¹⁵", "¹⁶", "¹⁷", "¹⁸", "¹⁹",
            "²⁰", "²¹", "²²", "²³", "²⁴", "²⁵", "²⁶", "²⁷", "²⁸", "²⁹",
            "³⁰", "³¹", "³²"
        };
        static string[] superscriptDesc =
        {
            "Optimizations were disabled to perform this benchmark",
            "Benchmark run on parallel job workers - results may vary",
        };
        static string GetFlagSuperscripts(uint flags)
        {
            string ret = "";
            for (int f = 0; f < sizeof(uint) * 8; f++)
            {
                if ((flags & (1 << f)) != 0)
                {
                    if (ret.Length > 0)
                        ret += "˒";
                    ret += superscripts[f];
                }
            }
            return ret;
        }

        /// <summary>
        /// First, runs benchmarks for all benchmark methods in all types attributed with [Benchmark(benchmarkEnumType)].
        /// Then, generates a report in markdown with these results, and saves to the requested file path.<para />
        /// A common integration method is to call this directly from a menu item handler.
        /// </summary>
        /// <param name="title">The title of the entire benchmark report</param>
        /// <param name="benchmarkEnumType">An enum with a <see cref="BenchmarkComparisonAttribute"/> which is specified in all <see cref="BenchmarkAttribute"/>s marking
        /// classes which contain performance methods to be benchmarked. All performance test methods in the class
        /// must contain a parameter of the enum marked with <see cref="BenchmarkComparisonAttribute"/> which is specified in the class's
        /// <see cref="BenchmarkAttribute"/>, and may not contain any other parameter with another enum marked with <see cref="BenchmarkComparisonAttribute"/>.</param>
        /// <param name="filePath">The output file path to save the generated markdown to.</param>
        /// <param name="description">A global description for the entire benchmark report, or null.</param>
        /// <param name="notesTitle">The title for a global "notes" section for the entire benchmark report, or null.</param>
        /// <param name="notes">An array of notes in the previously mentioned global "notes" section for the entire benchmark report, or null.</param>
        /// <exception cref="ArgumentException">Thrown for any errors in defining the benchmarks.</exception>
        public static void GenerateMarkdown(string title, Type benchmarkEnumType, string filePath, string description = null, string notesTitle = null, string[] notes = null)
        {
            var attrBenchmarkComparison = benchmarkEnumType.GetCustomAttribute<BenchmarkComparisonAttribute>();
            if (attrBenchmarkComparison == null)
                throw new ArgumentException($"{benchmarkEnumType.Name} is not a valid benchmark comparison enum type as it is not decorated with [{nameof(BenchmarkComparisonAttribute)}]");

            Stopwatch timer = new Stopwatch();
            timer.Start();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var benchmarkTypes = new List<Type>();

            foreach (Assembly assembly in assemblies)
            {
                var types = assembly.GetTypes();
                foreach(var t in types)
                {
                    var cads = t.GetCustomAttributesData();
                    foreach (var cad in cads)
                    {
                        if (cad.AttributeType != typeof(BenchmarkAttribute))
                            continue;

                        if ((Type)cad.ConstructorArguments[0].Value == benchmarkEnumType &&
                                (bool)cad.ConstructorArguments[1].Value == false)
                            benchmarkTypes.Add(t);
                    }
                }
            }
            UnityEngine.Debug.Log($"Took {timer.Elapsed}s to find all types with [Benchmark(typeof({benchmarkEnumType.Name}))]");

            timer.Restart();
            GenerateMarkdown(title, benchmarkTypes.ToArray(), filePath, description, notesTitle, notes);
            UnityEngine.Debug.Log($"Took {timer.Elapsed}s to benchmark all types with [Benchmark(typeof({benchmarkEnumType.Name}))]");
        }

        /// <summary>
        /// First, runs benchmarks for all benchmark methods in all given types.<br />
        /// Then, generates a report in markdown with these results, and saves to the requested file path.
        /// </summary>
        /// <param name="title">The title of the entire benchmark report</param>
        /// <param name="benchmarkTypes">An array of Types each annotated with a <see cref="BenchmarkAttribute"/> for comparison. Each Type may
        /// refer to a class with different arguments to the <see cref="BenchmarkAttribute"/> if desired, but all performance test methods in the class
        /// must each contain a parameter of the enum marked with <see cref="BenchmarkComparisonAttribute"/> which is specified in the class's
        /// <see cref="BenchmarkAttribute"/>, and may not contain any other parameter with another enum marked with <see cref="BenchmarkComparisonAttribute"/>.</param>
        /// <param name="filePath">The output file path to save the generated markdown to.</param>
        /// <param name="description">A global description for the entire benchmark report, or null.</param>
        /// <param name="notesTitle">The title for a global "notes" section for the entire benchmark report, or null.</param>
        /// <param name="notes">An array of notes in the previously mentioned global "notes" section for the entire benchmark report, or null.</param>
        /// <exception cref="ArgumentException">Thrown for any errors in defining the benchmarks.</exception>
        public static void GenerateMarkdown(string title, Type[] benchmarkTypes, string filePath, string description = null, string notesTitle = null, string[] notes = null)
        {
            using (var reports = BenchmarkRunner.RunBenchmarks(title, benchmarkTypes))
            {
                MarkdownBuilder md = new MarkdownBuilder();
                md.Header(1, $"Performance Comparison: {reports.reportName}");

                int versionFilter  = Application.unityVersion.IndexOf('-');
                md.Note($"<span style=\"color:red\">This file is auto-generated</span>",
                    $"All measurments were taken on {SystemInfo.processorType} with {SystemInfo.processorCount} logical cores.",
                    $"Unity Editor version: {Application.unityVersion.Substring(0, versionFilter == -1 ? Application.unityVersion.Length : versionFilter)}",
                    "To regenerate this file locally use: **DOTS -> Unity.Collections -> Generate &ast;&ast;&ast;** menu.");

                // Generate ToC

                const string kSectionBenchmarkResults = "Benchmark Results";

                md.Header(2, "Table of Contents");
                md.ListItem(0).LinkHeader(kSectionBenchmarkResults).Br();
                foreach (var group in reports.groups)
                    md.ListItem(1).LinkHeader(group.groupName.ToString()).Br();

                // Generate benchmark tables

                md.Header(2, kSectionBenchmarkResults);

                // Report description and notes first
                if (description != null && description.Length > 0)
                {
                    md.AppendLine(description);
                    md.BrParagraph();
                }

                if (notes != null && notes.Length > 0)
                {
                    if (notesTitle != null && notesTitle.Length > 0)
                        md.Note(notesTitle, notes);
                    else
                        md.Note(notes);
                }

                // Report each group results as ordered in the table of contents
                foreach (var group in reports.groups)
                {
                    md.BrParagraph().Header(3, $"*{group.groupName}*");
                    string[] titles = new string[group.variantNames.Length];
                    for (int i = 0; i < titles.Length; i++)
                    {
                        titles[i] = group.variantNames[i].ToString();
                        switch (group.resultTypes[i])
                        {
                            case BenchmarkResultType.ExternalBaseline:
                            case BenchmarkResultType.External:
                                titles[i] = $"*{titles[i]}*";
                                break;
                        }
                    }
                    md.TableHeader(false, "Functionality", true, titles);
                    uint tableFlags = 0;

                    // Find max amount of alignment spacing needed
                    int[] ratioSpace = new int[group.variantNames.Length];
                    foreach (var comparison in group.comparisons)
                    {
                        for (int i = 0; i < ratioSpace.Length; i++)
                        {
                            if (comparison.results[i].ranking == BenchmarkRankingType.Ignored)
                                continue;
                            int ratio10 = Mathf.RoundToInt((float)(comparison.results[i].baselineRatio * 10));
                            int pow10 = 0;
                            while (ratio10 >= 100)
                            {
                                pow10++;
                                ratio10 /= 10;
                            }
                            ratioSpace[i] = Mathf.Max(ratioSpace[i], pow10);
                        }
                    }

                    foreach (var comparison in group.comparisons)
                    {
                        uint rowFlags = comparison.footnoteFlags;
                        int items = comparison.results.Length;
                        var tableData = new string[items];
                        for (int i = 0; i < items; i++)
                        {
                            if (comparison.results[i].ranking == BenchmarkRankingType.Ignored)
                            {
                                tableData[i] = "---";
                                continue;
                            }

                            string format = $"{{0:F{group.resultDecimalPlaces}}}";
                            string result = $"{string.Format(format, comparison.results[i].Comparator)}{comparison.results[i].UnitSuffix}";
                            string speedup = $"({comparison.results[i].baselineRatio:F1}x)";
                            rowFlags |= comparison.results[i].resultFlags;

                            int ratio10 = Mathf.RoundToInt((float)(comparison.results[i].baselineRatio * 10));

                            if (ratio10 > 10)
                                speedup = $"<span style=\"color:green\">{speedup}</span>";
                            else if (ratio10 < 10)
                                speedup = $"<span style=\"color:red\">{speedup}</span>";
                            else
                                speedup = $"<span style=\"color:grey\">{speedup}</span>";

                            int alignSpaces = ratioSpace[i];
                            while (ratio10 >= 100)
                            {
                                alignSpaces--;
                                ratio10 /= 10;
                            }

                            speedup = $"{new string(' ', alignSpaces)}{speedup}";

                            tableData[i] = $"{result} {speedup}";

                            switch (group.resultTypes[i])
                            {
                                case BenchmarkResultType.ExternalBaseline:
                                case BenchmarkResultType.External:
                                    tableData[i] = $"*{tableData[i]}*";
                                    break;
                            }
                            switch (comparison.results[i].ranking)
                            {
                                case BenchmarkRankingType.Normal:
                                    tableData[i] = $"{tableData[i]}&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;";  // those 2 spaces are unicode en-space because >1 ASCII code spaces collapse
                                    break;
                                case BenchmarkRankingType.Best:
                                    tableData[i] = $"{tableData[i]}&nbsp;🟢";
                                    break;
                                case BenchmarkRankingType.Worst:
                                    tableData[i] = $"{tableData[i]}&nbsp;🟠";
                                    break;
                            }
                        }

                        tableFlags |= rowFlags;
                        if (rowFlags != 0)
                            md.TableRow($"`{comparison.comparisonName}`*{GetFlagSuperscripts(rowFlags)}*", tableData);
                        else
                            md.TableRow($"`{comparison.comparisonName}`", tableData);
                    }

                    md.Br();
                    for (int f = 0; f < 32; f++)
                    {
                        if ((tableFlags & (1 << f)) != 0)
                        {
                            if (f < superscriptDesc.Length)
                                md.AppendLine($"*{superscripts[f]}* {superscriptDesc[f]}");
                            else
                                md.AppendLine($"*{superscripts[f]}* {group.customFootnotes[1u << f]}");
                        }
                    }
                    md.HorizontalLine();
                }

                md.Save(filePath);

            }
        }
    }
}
