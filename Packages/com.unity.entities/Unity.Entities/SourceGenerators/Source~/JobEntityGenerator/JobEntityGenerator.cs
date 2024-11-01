using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

/*
    This is the JobEntityGenerator for the IJobEntity feature.
    Please read the comment in JobEntityModule for an overview of how these two generators interacts
    to make this feature work.
*/

namespace Unity.Entities.SourceGen.JobEntityGenerator;

[Generator]
public class JobEntityGenerator : IIncrementalGenerator
{
    public static readonly string GeneratorName = "JobEntity";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var projectPathProvider = IncrementalSourceGenHelpers.GetSourceGenConfigProvider(context);

        // Do a simple filter for enums
        var candidateProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: JobEntitySyntaxFinder.IsSyntaxTargetForGeneration,
                transform: JobEntitySyntaxFinder.GetSemanticTargetForGeneration)
            .Where(t => t is {});

        var compilationProvider = context.CompilationProvider;
        var combined = candidateProvider.Combine(compilationProvider).Combine(projectPathProvider);

        context.RegisterSourceOutput(combined, (productionContext, sourceProviderTuple) =>
        {
            var ((structDeclarationSyntax, compilation), sourceGenConfig) = sourceProviderTuple;

            Execute(productionContext,
                compilation,
                structDeclarationSyntax,
                checkUserDefinedQueriesForSchedulingJobs: sourceGenConfig.performSafetyChecks || sourceGenConfig.isDotsDebugMode);
        });
    }

    static void Execute(SourceProductionContext context, Compilation compilation,
        StructDeclarationSyntax candidate, bool checkUserDefinedQueriesForSchedulingJobs)
    {
        if (!SourceGenHelpers.ShouldRun(compilation, context.CancellationToken))
            return;

        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var syntaxTree = candidate.SyntaxTree;
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            var jobEntityDescription = new JobEntityDescription(candidate, semanticModel, checkUserDefinedQueriesForSchedulingJobs);
            if (jobEntityDescription.Invalid)
            {
                foreach (var diagnostic in jobEntityDescription.SourceGenDiagnostics)
                    context.ReportDiagnostic(diagnostic);
                return;
            }
            var generatedJobEntity = jobEntityDescription.Generate();
            var sourceFilePath = syntaxTree.GetGeneratedSourceFilePath(compilation.Assembly.Name, GeneratorName, candidate.GetLocation().GetLineSpan().StartLinePosition.Line);
            var outputSource = TypeCreationHelpers.GenerateSourceTextForRootNodes(sourceFilePath.FullFilePath, candidate,generatedJobEntity, context.CancellationToken);

            context.AddSource(syntaxTree.GetGeneratedSourceFileName(GeneratorName, candidate), outputSource);

            SourceOutputHelpers.OutputSourceToFile(sourceFilePath.FullFilePath, () => outputSource.ToString());
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException)
                throw;

            context.ReportDiagnostic(
                Diagnostic.Create(JobEntityDiagnostics.SGICE003Descriptor,
                    compilation.SyntaxTrees.First().GetRoot().GetLocation(),
                    exception.ToUnityPrintableString()));
        }
    }
}

public static class JobEntityDiagnostics
{
    const string ID_SGICE003 = "SGICE003";

    public static readonly DiagnosticDescriptor SGICE003Descriptor
        = new DiagnosticDescriptor(ID_SGICE003, "IJobEntity Generator",
            "This error indicates a bug in the DOTS source generators. We'd appreciate a bug report (Help -> Report a Bug...). Thanks! Error message: '{0}'.",
            JobEntityGenerator.GeneratorName, DiagnosticSeverity.Error, isEnabledByDefault: true, description: "");
}
