using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator;

[Generator]
public class SystemGenerator : ISourceGenerator
{
    const string GeneratorName = "System";

    public void Initialize(GeneratorInitializationContext context) =>
        context.RegisterForSyntaxNotifications(() => new SystemSyntaxReceiver(context.CancellationToken));

    public void Execute(GeneratorExecutionContext context)
    {
        if (!SourceGenHelpers.IsBuildTime || !SourceGenHelpers.ShouldRun(context.Compilation, context.CancellationToken))
            return;

        SourceOutputHelpers.Setup(context.ParseOptions, context.AdditionalFiles);

        Location lastLocation = null;
        SourceOutputHelpers.LogInfoToSourceGenLog($"Source generating assembly {context.Compilation.Assembly.Name}...");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var systemReceiver = (SystemSyntaxReceiver)context.SyntaxReceiver;
            var allModules = systemReceiver.SystemModules;
            var syntaxTreesWithCandidate = SystemGeneratorHelper.GetSyntaxTreesWithCandidates(allModules);
            var assemblyHasReferenceToBurst = context.Compilation.ReferencedAssemblyNames.Any(n => n.Name == "Unity.Burst");
            var assemblyHasReferenceToCollections = context.Compilation.ReferencedAssemblyNames.Any(n => n.Name == "Unity.Collections");
            var requiresMissingReferenceToBurst = false;
            var preprocessorInfo = PreprocessorInfo.From(context.ParseOptions.PreprocessorSymbolNames);

            // If multiple user-written parts exist for a given partial type, then each part might have its own syntax tree and semantic model, and thus its own `SystemDescription`.
            // Therefore we cannot group `SystemDescription`s by syntax tree or by semantic model, because we might end up wrongly treating partial parts of the same type
            // as distinct types. Instead, we group `SystemDescription`s by the fully qualified names of the system types they handle.
            var systemTypeFullNamesToDescriptions = new Dictionary<string, List<SystemDescription>>();

            // Process all candidates and create `SystemDescription`s for viable candidates
            // Store results in `systemTypeFullNamesToDescriptions`.
            foreach (var syntaxTree in syntaxTreesWithCandidate)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var syntaxTreeInfo = new SyntaxTreeInfo { Tree = syntaxTree, IsSourceGenerationSuccessful = true };
                var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);

                foreach (var systemTypeSyntax in SystemGeneratorHelper.GetSystemTypesInTree(syntaxTree, allModules))
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    if (systemTypeSyntax.Identifier.ValueText == "SystemBase")
                        continue;

                    lastLocation = systemTypeSyntax.GetLocation();

                    if (systemReceiver.ISystemDefinedAsClass.Contains(systemTypeSyntax))
                        continue;

                    var systemTypeSymbol = semanticModel.GetDeclaredSymbol(systemTypeSyntax);
                    var systemTypeInfo = systemTypeSymbol.TryGetSystemType();
                    if (!systemTypeInfo.IsSystemType)
                        continue;

                    SystemDescription systemDescription;
                    if (systemReceiver.CandidateNodesGroupedBySystemType.TryGetValue(systemTypeSyntax, out var candidates))
                    {
                        systemDescription =
                            new SystemDescription(
                                systemTypeSyntax,
                                systemTypeInfo.SystemType,
                                systemTypeSymbol,
                                semanticModel,
                                syntaxTreeInfo,
                                candidates,
                                preprocessorInfo);
                    }
                    else
                    {
                        systemDescription =
                            new SystemDescription(
                                systemTypeSyntax,
                                systemTypeInfo.SystemType,
                                systemTypeSymbol,
                                semanticModel,
                                syntaxTreeInfo,
                                default,
                                preprocessorInfo);
                    }

                    foreach (var module in SystemGeneratorHelper.GetAllModulesWithCandidatesInSystemType(systemTypeSyntax, allModules, context))
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();

                        if (!module.RegisterChangesInSystem(systemDescription))
                            continue;

                        if (module.RequiresReferenceToBurst && !assemblyHasReferenceToBurst)
                        {
                            requiresMissingReferenceToBurst = true;
                            syntaxTreeInfo.IsSourceGenerationSuccessful = false;
                        }
                    }

                    foreach (var systemDescriptionDiagnostic in systemDescription.SourceGenDiagnostics)
                        context.ReportDiagnostic(systemDescriptionDiagnostic);

                    systemDescription.SourceGenDiagnostics.Clear();

                    if (!systemDescription.ContainsChangesToSystem())
                        continue;

                    systemTypeFullNamesToDescriptions.Add(systemDescription.SystemTypeFullName, systemDescription);
                }
            }

            var partialSystemTypesGroupedBySyntaxTrees = new Dictionary<SyntaxTreeInfo, Dictionary<TypeDeclarationSyntax, string>>();

            // Generate partial types and store results in `partialSystemTypesGroupedBySyntaxTrees`
            foreach (var kvp in systemTypeFullNamesToDescriptions)
            {
                var allDescriptionsForSameSystemType = kvp.Value;
                var generatedPart = PartialSystemTypeGenerator.Generate(allDescriptionsForSameSystemType.ToArray());

                foreach (var desc in allDescriptionsForSameSystemType)
                foreach (var diag in desc.SourceGenDiagnostics)
                    context.ReportDiagnostic(diag);

                if (partialSystemTypesGroupedBySyntaxTrees.TryGetValue(generatedPart.SyntaxTreeInfo, out var partialSystemTypes))
                    partialSystemTypes[generatedPart.OriginalSystem] = generatedPart.GeneratedSyntaxTreeContainingGeneratedPartialSystem;
                else
                {
                    partialSystemTypesGroupedBySyntaxTrees.Add(
                        generatedPart.SyntaxTreeInfo, new Dictionary<TypeDeclarationSyntax, string>
                            { { generatedPart.OriginalSystem, generatedPart.GeneratedSyntaxTreeContainingGeneratedPartialSystem } });
                }
            }

            // Generate source files in parallel for debugging purposes (very useful to be able to visually inspect generated code!).
            // Add generated source to compilation only if there are no errors.
            foreach (var kvp in partialSystemTypesGroupedBySyntaxTrees)
            {
                var syntaxTreeInfo = kvp.Key;
                var originalSystemToGeneratedPartialSystem = kvp.Value;

                for (int i = 0; i < originalSystemToGeneratedPartialSystem.Count; i++)
                {
                    var originalToGeneratedPartial = originalSystemToGeneratedPartialSystem.ElementAt(i);
                    var generatedFile = syntaxTreeInfo.Tree.GetGeneratedSourceFilePath(context.Compilation.Assembly.Name, GeneratorName, salting: i);
                    var outputSource = TypeCreationHelpers.FixUpLineDirectivesAndOutputSource(generatedFile.FullFilePath, originalToGeneratedPartial.Value);

                    if (syntaxTreeInfo.IsSourceGenerationSuccessful)
                        context.AddSource(generatedFile.FileNameOnly, outputSource);

                    SourceOutputHelpers.OutputSourceToFile(
                        generatedFile.FullFilePath,
                        () => outputSource.ToString());
                }
            }

            foreach (var iSystemDefinedAsClass in systemReceiver.ISystemDefinedAsClass)
                SystemGeneratorErrors.DC0065(context, iSystemDefinedAsClass.GetLocation(), iSystemDefinedAsClass.Identifier.ValueText);

            if (requiresMissingReferenceToBurst)
                SystemGeneratorErrors.DC0060(context, context.Compilation.SyntaxTrees.First().GetRoot().GetLocation(), context.Compilation.AssemblyName);
            else if (!assemblyHasReferenceToCollections)
                SystemGeneratorErrors.DC0061(context, context.Compilation.SyntaxTrees.First().GetRoot().GetLocation(), context.Compilation.AssemblyName);

            stopwatch.Stop();

            SourceOutputHelpers.LogInfoToSourceGenLog($"TIME : SystemGenerator : {context.Compilation.Assembly.Name} : {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException)
                throw;

            context.LogError("SGICE002", "SystemGenerator", exception.ToUnityPrintableString(), lastLocation ?? context.Compilation.SyntaxTrees.First().GetRoot().GetLocation());
        }
    }
}
