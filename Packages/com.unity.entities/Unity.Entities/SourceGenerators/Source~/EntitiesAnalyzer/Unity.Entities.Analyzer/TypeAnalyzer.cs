using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TypeAnalyzer : DiagnosticAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeType, SyntaxKind.StructDeclaration, SyntaxKind.ClassDeclaration);
        }

        static void AnalyzeType(SyntaxNodeAnalysisContext context)
        {
            var typeDeclaration = context.Node as TypeDeclarationSyntax;
            Debug.Assert(typeDeclaration != null, nameof(typeDeclaration) + " != null");
            if (typeDeclaration.BaseList == null || typeDeclaration.BaseList.Types.Count == 0)
                return;

            // Error on missing IJobEntity or IAspect
            foreach (var type in typeDeclaration.BaseList.Types)
                if (type.Type is IdentifierNameSyntax { Identifier: { ValueText: "IAspect" or "IJobEntity" } })
                {
                    var declaredType = context.SemanticModel.GetTypeInfo(type.Type).Type;
                    var fullName = declaredType.ToFullName();
                    if (fullName is not ("global::Unity.Entities.IAspect" or "global::Unity.Entities.IJobEntity"))
                        continue;

                    for (var parent = typeDeclaration.Parent; parent is TypeDeclarationSyntax parentType; parent = parent.Parent)
                    {
                        // If we have partial continue to next parent
                        foreach (var modifier in parentType.Modifiers)
                            if (modifier.IsKind(SyntaxKind.PartialKeyword))
                                goto NextParent;

                        var declaredInnerSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
                        var declaredParentSymbol = context.SemanticModel.GetDeclaredSymbol(parentType);
                        context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0008Descriptor, parentType.Identifier.GetLocation(), type.Type, declaredInnerSymbol.ToFullName(), declaredParentSymbol.ToFullName()));
                        NextParent:;
                    }

                    foreach (var modifier in typeDeclaration.Modifiers)
                        if (modifier.IsKind(SyntaxKind.PartialKeyword))
                            return;

                    var declaredSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
                    context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0007Descriptor, typeDeclaration.Identifier.GetLocation(), type.Type, declaredSymbol.ToFullName()));
                    return;
                }

            foreach (var modifier in typeDeclaration.Modifiers)
                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                    return;

            // Error on missing System
            var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration); // Because of SystemBase supporting inheritance
            var (isSystem, systemType) = typeSymbol.TryGetSystemType();
            if (isSystem)
                context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0007Descriptor, typeDeclaration.Identifier.GetLocation(), systemType.ToString(), typeSymbol.ToFullName()));
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            EntitiesDiagnostics.k_Ea0007Descriptor, EntitiesDiagnostics.k_Ea0008Descriptor);
    }
}
