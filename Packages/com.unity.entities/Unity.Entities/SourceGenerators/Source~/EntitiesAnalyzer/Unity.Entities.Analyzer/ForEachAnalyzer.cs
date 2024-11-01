using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Operations;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ForEachAnalyzer : DiagnosticAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterOperationAction(Analyze, OperationKind.PropertyReference);
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            EntitiesDiagnostics.k_Ea0011Descriptor,
            EntitiesDiagnostics.k_Ea0012Descriptor,
            EntitiesDiagnostics.k_Ea0013Descriptor,
            EntitiesDiagnostics.k_Ea0014Descriptor,
            EntitiesDiagnostics.k_Ea0015Descriptor);

        static void Analyze(OperationAnalysisContext context)
        {
            var propertyReferenceOperation = context.Operation as IPropertyReferenceOperation;
            if (propertyReferenceOperation == null)
                return;

            var isJob = propertyReferenceOperation.Property.Name == "Job";
            var isEntities = propertyReferenceOperation.Property.Name == "Entities";
            if (!isEntities && !isJob)
                return;
            if (propertyReferenceOperation.Property.ContainingSymbol.ToFullName() != "global::Unity.Entities.SystemBase")
                return;

            var parent = propertyReferenceOperation.Parent;
            var penultimateParent = parent;
            var countForEach = 0;
            var countTerminator = 0;
            var countTotal = 0;
            while (parent is IInvocationOperation or IArgumentOperation)
            {
                if (parent is IInvocationOperation invocation)
                {
                    switch (invocation.TargetMethod.Name)
                    {
                        case "ForEach":
                            countForEach += 1;
                            break;
                        case "Run":
                        case "Schedule":
                        case "ScheduleParallel":
                            break;
                        case "ToQuery":
                        case "DestroyEntity":
                        case "AddComponent":
                        case "RemoveComponent":
                        case "AddComponentData":
                        case "AddChunkComponentData":
                        case "RemoveChunkComponentData":
                        case "AddSharedComponent":
                        case "SetSharedComponent":
                            countTerminator += 1;
                            break;
                    }
                    countTotal += 1;
                }

                penultimateParent = parent;
                parent = parent.Parent;
            }

            if (countTotal == 0)
            {
                // We have a solitary "Entities" or "Job" statement.
                if (isJob)
                {
                    context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0015Descriptor,
                        propertyReferenceOperation.Syntax.GetLocation()));
                }
                else
                {
                    context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0014Descriptor,
                        propertyReferenceOperation.Syntax.GetLocation()));
                }
                return;
            }

            if (penultimateParent is IInvocationOperation invocationOperation)
            {
                if (isEntities && countForEach == 0 && countTerminator == 0)
                {
                    // We have an "Entities" statement, but no "ForEach()" or any terminating statement.
                    context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0011Descriptor,
                        invocationOperation.Syntax.GetLocation()));
                    return;
                }

                var returnType = invocationOperation.TargetMethod.ReturnType.ContainingNamespace?.ToFullName();
                if (returnType == "global::Unity.Entities.CodeGeneratedJobForEach")
                {
                    // The chain is not terminated.
                    if (isJob)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0013Descriptor,
                            invocationOperation.Syntax.GetLocation()));
                    }
                    else
                    {
                        context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0012Descriptor,
                            invocationOperation.Syntax.GetLocation()));
                    }
                }
            }
        }
    }
}
