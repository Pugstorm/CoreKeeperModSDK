using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class BurstCompilerAnalyzer : DiagnosticAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeType, SyntaxKind.MethodDeclaration);
        }

        static bool ContainsBurstCompilerAttribute(SemanticModel model, SyntaxList<AttributeListSyntax> attributeListList)
        {
            foreach (var attributeList in attributeListList)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    // Quick check to see if none of the attributes have burst compile at all in their name
                    // This might have false negatives, but should greatly speed up analyzer runs (and is worth it I think)
                    if (attribute.Name.ToString().Contains("BurstCompile"))
                    {
                        // Deeper check of semantic model
                        if (model.GetTypeInfo(attribute).Type.ToDisplayString() == "Unity.Burst.BurstCompileAttribute")
                            return true;
                    }
                }
            }

            return false;
        }

        static void AnalyzeType(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = context.Node as MethodDeclarationSyntax;
            Debug.Assert(methodDeclaration != null, nameof(methodDeclaration) + " != null");
            if (methodDeclaration.AttributeLists.Count == 0)
                return;

            if (!ContainsBurstCompilerAttribute(context.SemanticModel, methodDeclaration.AttributeLists))
                return;

            foreach (var ancestor in methodDeclaration.Ancestors())
            {
                if (ancestor is TypeDeclarationSyntax typeDeclaration)
                {
                    // Whitelist ISystem types (special case where we always check for BurstCompile in members)
                    if (typeDeclaration.BaseList != null)
                    {
                        foreach (var type in typeDeclaration.BaseList.Types)
                        {
                            if (type.Type is IdentifierNameSyntax {Identifier: {ValueText: "ISystem"}})
                            {
                                var declaredType = context.SemanticModel.GetTypeInfo(type.Type).Type;
                                var fullName = declaredType.ToFullName();
                                if (fullName is "global::Unity.Entities.ISystem")
                                    return;
                            }
                        }
                    }

                    if (!ContainsBurstCompilerAttribute(context.SemanticModel, typeDeclaration.AttributeLists))
                    {
                        // We might still have an attribute on another type if we are partial
                        if (typeDeclaration.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PartialKeyword)))
                        {
                            var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
                            if (typeSymbol.HasAttribute("Unity.Burst.BurstCompileAttribute"))
                                return;
                        }

                        EmitDiagnostic(context, typeDeclaration.Identifier, methodDeclaration.Identifier);
                    }
                }
            }
        }

        static void EmitDiagnostic(SyntaxNodeAnalysisContext context, SyntaxToken typeDeclarationIdentifier,
            SyntaxToken methodDeclarationIdentifier)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                EntitiesDiagnostics.k_Ea0010Descriptor, typeDeclarationIdentifier.GetLocation(),
                typeDeclarationIdentifier, methodDeclarationIdentifier));
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            EntitiesDiagnostics.k_Ea0010Descriptor);
    }
}
