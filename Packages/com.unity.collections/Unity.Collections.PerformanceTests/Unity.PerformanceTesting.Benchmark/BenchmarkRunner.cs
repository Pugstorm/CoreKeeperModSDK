using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.PerformanceTesting.Benchmark
{
    internal static class BenchmarkRunner
    {
        static string progressTitle;

        static void StartProgress(string title, int typeIndex, int typeCount, string typeName) =>
            progressTitle = $"Benchmarking {title} {typeIndex + 1}/{typeCount} - {typeName}";

        static void EndProgress()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.ClearProgressBar();
#endif
        }

        static void SetProgressText(string text, float unitProgress)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.DisplayCancelableProgressBar(progressTitle, text, unitProgress))
                throw new Exception("User cancelled benchmark operation");
#endif
        }

        /// <summary>
        /// Contains a combination of a BenchmarkComparison attributed enum and the Type with perf. measurements
        /// to determine names for the benchmark.
        ///
        /// Also contains reflected info on the enum defined and external benchmark values used to organize
        /// benchmark tests and results, though this will not vary between different Types with perf. measurments.
        /// These constant values are also associated with a classification of enum-defined vs external, and
        /// baseline vs not.
        ///
        /// There may only be one baseline per benchmark comparison type.
        /// </summary>
        class BenchmarkComparisonTypeData
        {
            public string defaultName;
            public Type enumType;

            public string[] names;
            public int[] values;
            public BenchmarkResultType[] resultTypes;

            public SampleUnit resultUnit;
            public int resultDecimalPlaces;
            public BenchmarkRankingStatistic resultStatistic;

            public BenchmarkComparisonTypeData(int variants)
            {
                names = new string[variants];
                values = new int[variants];
                resultTypes = new BenchmarkResultType[variants];
                enumType = null;
                defaultName = null;
                resultUnit = SampleUnit.Millisecond;
                resultDecimalPlaces = 3;
                resultStatistic = BenchmarkRankingStatistic.Median;
            }
        }

        /// <summary>
        /// Given a System.Type that contains performance test methods, reflect the setup to a benchmark comparison.
        /// Throws on any errors with the setup.
        /// </summary>
        unsafe static BenchmarkComparisonTypeData GatherComparisonStructure(Type t)
        {
            //--------
            // Determine and validate the benchmark comparison this type is intended for
            //--------
            Type benchmarkEnumType = null;
            foreach(var attributeData in t.GetCustomAttributesData())
            {
                if (attributeData.AttributeType == typeof(BenchmarkAttribute))
                {
                    benchmarkEnumType = (Type)attributeData.ConstructorArguments[0].Value;
                    break;
                }
            }
            if (benchmarkEnumType == null)
                throw new ArgumentException($"Exactly one [{nameof(BenchmarkAttribute)}] must exist on the type {t.Name} to generate benchmark data");

            // Find the baseline and the formatting for its title name (could be external to the enum or included)
            CustomAttributeData attrBenchmarkComparison = null;
            List<CustomAttributeData> attrBenchmarkComparisonExternal = new List<CustomAttributeData>();
            CustomAttributeData attrBenchmarkFormat = null;
            foreach (var attributeData in benchmarkEnumType.GetCustomAttributesData())
            {
                if (attributeData.AttributeType == typeof(BenchmarkComparisonAttribute))
                {
                    attrBenchmarkComparison = attributeData;
                }
                // Find any other external comparisons
                else if (attributeData.AttributeType == typeof(BenchmarkComparisonExternalAttribute))
                {
                    attrBenchmarkComparisonExternal.Add(attributeData);
                }
                // Find optional formatting of table results
                else if (attributeData.AttributeType == typeof(BenchmarkComparisonDisplayAttribute))
                {
                    attrBenchmarkFormat = attributeData;
                }
            }
            if (attrBenchmarkComparison == null)
                throw new ArgumentException($"Exactly one [{nameof(BenchmarkComparisonAttribute)}] must exist on the enum {benchmarkEnumType.Name} to generate benchmark data and define the baseline");

            //--------
            // Collect values and name formatting for enum and external
            //--------

            // Enum field values
            var enumFields = benchmarkEnumType.GetFields(BindingFlags.Static | BindingFlags.Public);
            var enumCount = enumFields.Length;
            var enumValues = stackalloc int[enumCount];
            var enumValuesSet = new HashSet<int>(enumCount);
            for (int i = 0; i < enumCount; i++)
            {
                int value = (int)enumFields[i].GetRawConstantValue();
                enumValues[i] = value;
                enumValuesSet.Add(value);
            }

            var enumFormats = new List<string>(enumCount);
            foreach(var x in enumFields)
            {
                int oldCount = enumFormats.Count;
                foreach (var attributeData in x.GetCustomAttributesData())
                {
                    if (attributeData.AttributeType == typeof(BenchmarkNameAttribute))
                    {
                        enumFormats.Add((string)attributeData.ConstructorArguments[0].Value);
                        break;
                    }
                }
                if (oldCount == enumFormats.Count)
                    throw new ArgumentException($"{x.Name} as well as all other enum values in {benchmarkEnumType.Name} must have a single [{nameof(BenchmarkNameAttribute)}] defined");
            }

            // External values
            var externalValues = new List<int>(attrBenchmarkComparisonExternal.Count);
            foreach(var x in attrBenchmarkComparisonExternal)
            {
                var externalValue = (int)x.ConstructorArguments[0].Value;
                if (enumValuesSet.Contains(externalValue))
                    throw new ArgumentException($"Externally-defined benchmark values for {benchmarkEnumType.Name} must not be a duplicate of another enum-defined or externally-defined benchmark value for {benchmarkEnumType.Name}");
            }
            var externalFormats = new List<string>(attrBenchmarkComparisonExternal.Count);
            foreach(var x in attrBenchmarkComparisonExternal)
            {
                externalFormats.Add((string)x.ConstructorArguments[1].Value);
            }
            
            var externalCount = externalValues.Count;

            // Baseline value
            int baselineValue = (int)attrBenchmarkComparison.ConstructorArguments[0].Value;
            string externalBaselineFormat = null;
            if (attrBenchmarkComparison.ConstructorArguments.Count == 1)
            {
                if (!enumValuesSet.Contains(baselineValue))
                    throw new ArgumentException($"{baselineValue} not found in enum {benchmarkEnumType.Name}. Either specify an existing value as the baseline, or add a formatting string for the externally defined baseline value.");
            }
            else
            {
                if (enumValuesSet.Contains(baselineValue))
                    throw new ArgumentException($"To specify an enum-defined benchmark baseline in {benchmarkEnumType.Name}, pass only the argument {baselineValue} without a name, as the name requires definition in the enum");
                if (externalValues.Contains(baselineValue))
                    throw new ArgumentException($"To specify an external-defined benchmark baseline in {benchmarkEnumType.Name}, define only in [{nameof(BenchmarkComparisonAttribute)}] and omit also defining with [{nameof(BenchmarkComparisonExternalAttribute)}]");
                externalBaselineFormat = (string)attrBenchmarkComparison.ConstructorArguments[1].Value;
            }

            // Total
            int variantCount = enumCount + externalCount + (externalBaselineFormat == null ? 0 : 1);

            //--------
            // Collect name overrides on the specific type with benchmarking methods
            //--------

            string defaultNameOverride = null;
            var nameOverride = new Dictionary<int, string>();
            foreach (var attr in t.CustomAttributes)
            {
                if (attr.AttributeType == typeof(BenchmarkNameOverrideAttribute))
                {
                    if (attr.ConstructorArguments.Count == 1)
                    {
                        if (defaultNameOverride != null)
                            throw new ArgumentException($"No more than one default name override is allowed for {t.Name} using [{nameof(BenchmarkNameOverrideAttribute)}]");
                        defaultNameOverride = (string)attr.ConstructorArguments[0].Value;
                    }
                    else
                    {
                        int valueOverride = (int)attr.ConstructorArguments[0].Value;
                        if (nameOverride.ContainsKey(valueOverride))
                            throw new ArgumentException($"No more than one name override is allowed for benchmark comparison value {valueOverride} using [{nameof(BenchmarkNameOverrideAttribute)}]");
                        nameOverride[valueOverride] = (string)attr.ConstructorArguments[1].Value;
                    }
                }
            }

            //--------
            // Record all the information
            //--------

            var ret = new BenchmarkComparisonTypeData(variantCount);
            ret.defaultName = defaultNameOverride ?? t.Name;
            ret.enumType = benchmarkEnumType;

            // Result optional custom formatting
            if (attrBenchmarkFormat != null)
            {
                ret.resultUnit = (SampleUnit)attrBenchmarkFormat.ConstructorArguments[0].Value;
                ret.resultDecimalPlaces = (int)attrBenchmarkFormat.ConstructorArguments[1].Value;
                ret.resultStatistic = (BenchmarkRankingStatistic)attrBenchmarkFormat.ConstructorArguments[2].Value;
            }

            // Enum field values
            for (int i = 0; i < enumCount; i++)
            {
                ret.names[i] = enumFormats[i];
                ret.values[i] = enumValues[i];
                ret.resultTypes[i] = baselineValue == ret.values[i] ? BenchmarkResultType.NormalBaseline : BenchmarkResultType.Normal;
            }

            // External values
            for (int i = 0; i < externalCount; i++)
            {
                ret.names[enumCount + i] = externalFormats[i];
                ret.values[enumCount + i] = externalValues[i];
                ret.resultTypes[enumCount + i] = BenchmarkResultType.External;
            }

            // External baseline value if it exists
            if (externalBaselineFormat != null)
            {
                ret.names[variantCount - 1] = externalBaselineFormat;
                ret.values[variantCount - 1] = baselineValue;
                ret.resultTypes[variantCount - 1] = BenchmarkResultType.ExternalBaseline;
            }

            for (int i = 0; i < variantCount; i++)
            {
                if (nameOverride.TryGetValue(ret.values[i], out string name))
                    ret.names[i] = string.Format(ret.names[i], name);
                else
                    ret.names[i] = string.Format(ret.names[i], ret.defaultName);
            }
            
            if (new HashSet<int>(ret.values).Count != ret.values.Length)
                throw new ArgumentException($"Each enum value and external value in {benchmarkEnumType.Name} must be unique");

            return ret;
        }

        /// <summary>
        /// Reflects all possible arguments to a performance test method. Finds the parameter which benchmark comparisons
        /// are based around (must be an enum type decorated with [BenchmarkComparison] attribute).
        ///
        /// There is a (usually small) finite set of arguments possible in performance test methods due to
        /// requiring [Values(a, b, c)] attribute on any parameter that isn't a bool or enum.
        /// </summary>
        static void GatherAllArguments(ParameterInfo[] paramInfo, string methodName, BenchmarkComparisonTypeData structure, out int[] argCounts, out CustomAttributeTypedArgument[][] argValues, out string[] argNames, out int paramForComparison)
        {
            paramForComparison = -1;

            argCounts = new int[paramInfo.Length];
            argValues = new CustomAttributeTypedArgument[paramInfo.Length][];
            argNames = new string[paramInfo.Length];
            for (int p = 0; p < paramInfo.Length; p++)
            {
                // It is correct to throw if a parameter doesn't include Values attribute, NUnit errors as well
                CustomAttributeData valuesAttribute = null;
                foreach (var cad in paramInfo[p].GetCustomAttributesData())
                {
                    if (cad.AttributeType == typeof(NUnit.Framework.ValuesAttribute))
                    {
                        valuesAttribute = cad;
                        break;
                    }
                }
                if (valuesAttribute == null)
                    throw new ArgumentException($"No [Values(...)] attribute found for parameter {paramInfo[p].Name} in {methodName}");

                var values = valuesAttribute.ConstructorArguments;

                argNames[p] = paramInfo[p].Name;

                if (paramInfo[p].ParameterType.IsEnum && paramInfo[p].ParameterType.GetCustomAttribute<BenchmarkComparisonAttribute>() != null)
                {
                    // [Values] <comparisonEnumType> <paramName>
                    //
                    // values.Count must be 0 or inconsistent benchmark measurements might be made.
                    // Alternatively, we could treat as if it had no arguments for benchmarks, and allow performance testing for regressions
                    // to be more specific, but for now it seems like a good idea to perf. test all valid combinations we offer, and in fact
                    // a good idea to enforce that in some manner.

                    if (paramInfo[p].ParameterType != structure.enumType)
                        throw new ArgumentException($"The method {methodName} parameterizes benchmark comparison type {paramInfo[p].ParameterType.Name} but only supports {structure.enumType.Name}.");

                    if (paramForComparison != -1)
                        throw new ArgumentException($"More than one parameter specifies {structure.enumType.Name}. Only one may exist.");

                    paramForComparison = p;

                    argCounts[p] = structure.resultTypes.Length;
                    argValues[p] = new CustomAttributeTypedArgument[argCounts[p]];

                    // [Values(...)] <comparisonEnumType> <paramName>
                    // This specifies comparison critera, and any excluded values will be shown as not available in the results report

                    if (values.Count == 0)
                    {
                        // [Values]
                        // This is the normal usage encompassing all comparison types

                        for (int e = 0; e < argCounts[p]; e++)
                            argValues[p][e] = new CustomAttributeTypedArgument(structure.values[e]);
                    }
                    else
                    {
                        // [Values(1-to-3-arguments)] <comparisonEnumType> <paramName>
                        var ctorValues = values;

                        if (values.Count == 1 && values[0].ArgumentType == typeof(object[]))
                        {
                            // [Values(more-than-3-arguments)] <comparisonEnumType> <paramName>
                            //
                            // This is for ValuesAttribute(params object[] args)

                            var arrayValue = values[0].Value as System.Collections.Generic.IList<CustomAttributeTypedArgument>;
                            ctorValues = arrayValue;
                        }

                        for (int e = 0; e < argCounts[p]; e++)
                        {
                            if (structure.resultTypes[e] == BenchmarkResultType.External || structure.resultTypes[e] == BenchmarkResultType.ExternalBaseline)
                                argValues[p][e] = new CustomAttributeTypedArgument(structure.values[e]);
                            else
                                argValues[p][e] = default;  // We can later check if ArgumentType is null to determine an unused comparison test
                        }

                        // If we don't include NormalBaseline values, it is an error - you can't not include a baseline
                        bool hasNormalBaseline = false;
                        string normalBaselineName = null;
                        for (int i = 0; i < structure.resultTypes.Length; i++)
                        {
                            if (structure.resultTypes[i] == BenchmarkResultType.NormalBaseline)
                            {
                                hasNormalBaseline = true;
                                normalBaselineName = structure.enumType.GetEnumNames()[i];
                            }
                        }

                        bool specifiedBaseline = !hasNormalBaseline;
                        for (int ca = 0; ca < ctorValues.Count; ca++)
                        {
                            // Ensure it's not some alternative value cast to the enum type such as an external baseline identifying value
                            // because that would end up as part of the Performance Test Framework tests.
                            if (ctorValues[ca].ArgumentType != structure.enumType)
                                throw new ArgumentException($"Only {structure.enumType} values may be specified. External comparison types are always added automatically.");

                            // Find the index this value would have been at, and set the argValue there to the struct.values for it
                            for (int v = 0; v < structure.values.Length; v++)
                            {
                                if (structure.values[v] == (int)ctorValues[ca].Value)
                                {
                                    argValues[p][v] = new CustomAttributeTypedArgument(structure.values[v]);
                                    if (structure.resultTypes[v] == BenchmarkResultType.NormalBaseline)
                                        specifiedBaseline = true;
                                }
                            }
                        }

                        if (!specifiedBaseline)
                            throw new ArgumentException($"This comparison type requires the baseline {structure.enumType.Name}.{normalBaselineName} to be measured.");
                    }
                }
                else if (values.Count == 0)
                {
                    // [Values] <type> <paramName>
                    //
                    // This has default behaviour for bools and enums, otherwise error

                    if (paramInfo[p].ParameterType == typeof(bool))
                    {
                        argCounts[p] = 2;
                        argValues[p] = new CustomAttributeTypedArgument[]
                        {
                            new CustomAttributeTypedArgument(true),
                            new CustomAttributeTypedArgument(false)
                        };
                    }
                    else if (paramInfo[p].ParameterType.IsEnum)
                    {
                        var enumValues = Enum.GetValues(paramInfo[p].ParameterType);
                        argCounts[p] = enumValues.Length;
                        argValues[p] = new CustomAttributeTypedArgument[argCounts[p]];
                        for (int e = 0; e < argCounts[p]; e++)
                            argValues[p][e] = new CustomAttributeTypedArgument(enumValues.GetValue(e));
                    }
                    else
                        throw new ArgumentException($"[Values] attribute of parameter {paramInfo[p].Name} in {methodName} is empty");
                }
                else if (values.Count == 1 && values[0].ArgumentType == typeof(object[]))
                {
                    // [Values(more-than-3-arguments)] <type> <paramName>
                    //
                    // This is for ValuesAttribute(params object[] args)

                    var arrayValue = values[0].Value as System.Collections.Generic.IList<CustomAttributeTypedArgument>;
                    argValues[p] = new CustomAttributeTypedArgument[arrayValue.Count];
                    arrayValue.CopyTo(argValues[p], 0);
                    argCounts[p] = arrayValue.Count;
                }
                else
                {
                    // [Values(1-to-3-arguments)] <type> <paramName>
                    argValues[p] = new CustomAttributeTypedArgument[values.Count];
                    values.CopyTo(argValues[p], 0);
                    argCounts[p] = values.Count;
                }
            }

            if (paramForComparison == -1)
                throw new ArgumentException($"No benchmark comparison is parameterized. One must be specified");
        }

        /// <summary>
        /// Given
        /// a) X number of permutations for all arguments to each parameter in a performance test method
        /// b) the possible arguments to each parameter
        /// c) the parameter defining the benchmark comparison
        /// 
        /// Return
        /// a) the argument set (called variant) for Permutation[0 to X-1]
        /// b) the isolated benchmark comparison index, based on the benchmark comparison enum values, for this variant
        /// </summary>
        static BenchmarkResultType GetVariantArguments(int variantIndex, BenchmarkComparisonTypeData structure, int paramForComparison, CustomAttributeTypedArgument[][] argValues, int[] argCounts, out object[] args, out int comparisonIndex)
        {
            comparisonIndex = 0;

            int numParams = argValues.Length;

            // Calculate ValuesAttribute indices for each parameter
            // Calculate actual comparison index to ensure only benchmarks comparison are bunched together
            int[] argValueIndex = new int[numParams];
            for (int p = 0, argSet = variantIndex, comparisonMult = 1; p < numParams; p++)
            {
                argValueIndex[p] = argSet % argCounts[p];
                argSet = (argSet - argValueIndex[p]) / argCounts[p];

                if (p != paramForComparison)
                {
                    comparisonIndex += argValueIndex[p] * comparisonMult;
                    comparisonMult *= argCounts[p];
                }
            }

            // Find each argument using above ValuesAttribute indices
            args = new object[numParams];
            if (argValues[paramForComparison][argValueIndex[paramForComparison]].ArgumentType == null)
                return BenchmarkResultType.Ignored;

            for (int p = 0; p < numParams; p++)
                args[p] = argValues[p][argValueIndex[p]].Value;

            return structure.resultTypes[argValueIndex[paramForComparison]];
        }

        /// <summary>
        /// Runs benchmarking for all defined benchmark methods in a type.
        /// </summary>
        static BenchmarkReportGroup GatherGroupData(Type t, BenchmarkComparisonTypeData structure)
        {
            var group = new BenchmarkReportGroup(structure.defaultName, structure.names, structure.resultTypes, structure.resultDecimalPlaces);
            uint groupFootnoteBit = BenchmarkResults.kFlagFootnotes;

            var allMethods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var methods = new List<MethodInfo>(allMethods.Length);
            foreach (var m in allMethods)
            {
                if (m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null && m.GetCustomAttribute<PerformanceAttribute>() != null)
                    methods.Add(m);
            }

            var inst = Activator.CreateInstance(t);
            for (int m = 0; m < methods.Count; m++)
            {
                var method = methods[m];

                // Get ValueAttributes information for all parameters
                GatherAllArguments(method.GetParameters(), $"{t.Name}.{method.Name}", structure,
                out var argCounts, out var argValues, out var argNames, out var paramForComparison);

                // Record any footnotes for this method
                uint comparisonFootnoteFlags = 0;
                foreach (var cad in method.GetCustomAttributesData())
                {
                    if (cad.AttributeType != typeof(BenchmarkTestFootnoteAttribute))
                        continue;

                    var footnoteText = new NativeText($"{method.Name}(", Allocator.Persistent);
                    int paramsShown = 0;
                    for (int p = 0; p < argNames.Length; p++)
                    {
                        if (p == paramForComparison)
                            continue;

                        if (paramsShown++ > 0)
                            footnoteText.Append(", ");
                        footnoteText.Append(argNames[p]);
                    }
                    footnoteText.Append(")");
                    if (cad.ConstructorArguments.Count == 1)
                        footnoteText.Append($" -- {(string)cad.ConstructorArguments[0].Value}");
                    group.customFootnotes.Add(groupFootnoteBit, footnoteText);
                    comparisonFootnoteFlags |= groupFootnoteBit;
                    groupFootnoteBit <<= 1;
                }

                // Calculate number of variations based on all ValuesAttributes + parameters
                int totalVariants = 1;
                for (int p = 0; p < argCounts.Length; p++)
                    totalVariants *= argCounts[p];
                int numComparisons = totalVariants / argCounts[paramForComparison];

                BenchmarkReportComparison[] comparison = new BenchmarkReportComparison[numComparisons];

                for (int i = 0; i < totalVariants; i++)
                {
                    SetProgressText($"Running benchmark {i + 1}/{totalVariants} for {method.Name}", (float)(m + 1) / methods.Count);

                    // comparisonIndex indicates the variation of a complete benchmark comparison. i.e.
                    // you could be benchmarking between 3 different variants (such as NativeArray vs UnsafeArray vs C# Array)
                    // but you may also have 4 versions of that (such as 1000 elements, 10000 elements, 100000, and 1000000)
                    BenchmarkResultType resultType = GetVariantArguments(i, structure, paramForComparison, argValues, argCounts,
                        out var args, out int comparisonIndex);
                    if (resultType == BenchmarkResultType.Ignored)
                    {
                        if (comparison[comparisonIndex].comparisonName.IsEmpty)
                            comparison[comparisonIndex] = new BenchmarkReportComparison(method.Name);
                        comparison[comparisonIndex].results.Add(BenchmarkResults.Ignored);
                        continue;
                    }

                    if (comparison[comparisonIndex].comparisonName.IsEmpty)
                    {
                        string paramsString = null;
                        for (int p = 0; p < argCounts.Length; p++)
                        {
                            if (p == paramForComparison)
                                continue;
                            if (paramsString == null)
                                paramsString = $"({args[p]}";
                            else
                                paramsString += $", {args[p]}";
                        }

                        if (paramsString != null)
                            comparison[comparisonIndex] = new BenchmarkReportComparison($"{method.Name}{paramsString})");
                        else
                            comparison[comparisonIndex] = new BenchmarkReportComparison(method.Name);
                    }

                    // Call the performance method
                    method.Invoke(inst, args);

                    var results = BenchmarkMeasure.CalculateLastResults(structure.resultUnit, structure.resultStatistic);
                    comparison[comparisonIndex].results.Add(results);
                }

                // Add all sets of comparisons to the full group
                for (int i = 0; i < numComparisons; i++)
                {
                    comparison[i].footnoteFlags |= comparisonFootnoteFlags;
                    comparison[i].RankResults(structure.resultTypes);
                    group.comparisons.Add(comparison[i]);
                }
            }

            return group;
        }

        /// <summary>
        /// Runs benchmarking for all given types.
        /// </summary>
        /// <param name="title">The title to the full report</param>
        /// <param name="benchmarkTypes">An array of types each marked with <see cref="BenchmarkAttribute"/></param>
        /// <returns></returns>
        public static BenchmarkReports RunBenchmarks(string title, Type[] benchmarkTypes)
        {
            BenchmarkMeasure.ForBenchmarks = true;
            BenchmarkReports reports = default;

            try
            {
                reports = new BenchmarkReports(title);

                for (int i = 0; i < benchmarkTypes.Length; i++)
                {
                    StartProgress(title, i, benchmarkTypes.Length, benchmarkTypes[i].Name);
                    SetProgressText("Gathering benchmark data", 0);
                    var benchmarkStructure = GatherComparisonStructure(benchmarkTypes[i]);
                    var group = GatherGroupData(benchmarkTypes[i], benchmarkStructure);
                    reports.groups.Add(group);
                }
            }
            finally
            {
                BenchmarkMeasure.ForBenchmarks = false;
                EndProgress();
            }

            return reports;
        }
    }
}
