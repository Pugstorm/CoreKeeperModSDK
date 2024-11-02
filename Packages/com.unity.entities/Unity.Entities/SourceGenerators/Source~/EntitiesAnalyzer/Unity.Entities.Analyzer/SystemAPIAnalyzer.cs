using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SystemAPIAnalyzer : DiagnosticAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(compilationCtx =>
            {
                compilationCtx.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
                compilationCtx.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
            });
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            EntitiesDiagnostics.k_Ea0004Descriptor,
            EntitiesDiagnostics.k_Ea0005Descriptor,
            EntitiesDiagnostics.k_Ea0006Descriptor);

        static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var invocationOperation = (IInvocationOperation)context.Operation;
            var targetMethod = invocationOperation.TargetMethod;

            AnalyzeMember(context, targetMethod.ContainingType, invocationOperation, targetMethod.Name);
        }

        void AnalyzePropertyReference(OperationAnalysisContext context)
        {
            var propertyReferenceOperation = (IPropertyReferenceOperation)context.Operation;

            var containingType = propertyReferenceOperation.Property.ContainingType;
            AnalyzeMember(context, containingType, propertyReferenceOperation,
                propertyReferenceOperation.Property.Name);
        }

        static void AnalyzeMember(OperationAnalysisContext context, INamedTypeSymbol containingType,
            IOperation targetOperation, string targetName)
        {
            if (containingType is {IsStatic: true} && containingType.Is("global::Unity.Entities.SystemAPI"))
            {
                var parentTypeDeclarationSyntax = targetOperation.Syntax.AncestorOfKind<TypeDeclarationSyntax>();
                var invocationContainingType = ModelExtensions.GetDeclaredSymbol(
                    targetOperation.SemanticModel, parentTypeDeclarationSyntax);

                if (invocationContainingType is INamedTypeSymbol namedContainingType &&
                    !namedContainingType.TryGetSystemType().IsSystemType)
                {
                    // Emit diagnostic if we are in a non-system type
                    EmitDiagnostic(EntitiesDiagnostics.k_Ea0004Descriptor, context,
                        targetOperation.Syntax.GetLocation(), targetName);
                }
                else
                {
                    // Emit diagnostic if we are in a static method
                    var methodDeclarationSyntax =
                        targetOperation.Syntax.AncestorOfKindOrDefault<MethodDeclarationSyntax>();
                    if (methodDeclarationSyntax != null &&
                        methodDeclarationSyntax.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword)))
                    {
                        EmitDiagnostic(EntitiesDiagnostics.k_Ea0006Descriptor, context,
                            targetOperation.Syntax.GetLocation(), targetName);
                    }

                    // Emit diagnostic if we are inside an Entities.ForEach (method invocations disallowed)
                    if (targetOperation is IInvocationOperation)
                    {
                        var parentLambdaDeclarationSyntax =
                            targetOperation.Syntax.AncestorOfKindOrDefault<ParenthesizedLambdaExpressionSyntax>();
                        var lambdaInvocationSyntax =
                            parentLambdaDeclarationSyntax?.AncestorOfKindOrDefault<InvocationExpressionSyntax>();
                        if (lambdaInvocationSyntax != null)
                        {
                            if (IsLambdaInvocationEntitiesForEach(lambdaInvocationSyntax))
                            {
                                if (SystemAPIMethods.EFEAllowedAPIMethods.All(method => method[0] != targetName))
                                {
                                    EmitDiagnostic(EntitiesDiagnostics.k_Ea0005Descriptor, context,
                                        targetOperation.Syntax.GetLocation(), targetName);
                                }
                            }
                        }
                    }
                }
            }

            // Incredibly low-tech but cheap way to detect if we are in an Entities.ForEach
            static bool IsLambdaInvocationEntitiesForEach(InvocationExpressionSyntax lambdaInvocationSyntax)
            {
                var lambdaInvocationString = lambdaInvocationSyntax.ToString();
                const string entitiesStr = "Entities";
                if (lambdaInvocationString.StartsWith(entitiesStr))
                {
                    var subString = lambdaInvocationString.Substring(entitiesStr.Length);
                    var charIdx = 0;
                    while (charIdx < subString.Length &&
                           (char.IsWhiteSpace(subString[charIdx]) || subString[charIdx] == '.'))
                        charIdx++;
                    if (subString.Substring(charIdx).StartsWith("ForEach"))
                        return true;
                }

                return false;
            }

            static void EmitDiagnostic(DiagnosticDescriptor descriptor, OperationAnalysisContext context, Location location, string name)
            {
                context.ReportDiagnostic(Diagnostic.Create(descriptor, location, name));
            }
        }
    }
}
