using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SystemStateByRefAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            EntitiesDiagnostics.k_Ea0016Descriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Parameter);
        }

        static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var parameterSymbol = (IParameterSymbol)context.Symbol;
            var typeSymbol = parameterSymbol.Type;
            if (parameterSymbol.RefKind != RefKind.Ref && typeSymbol.Is("global::Unity.Entities.SystemState") && parameterSymbol.DeclaringSyntaxReferences.Length > 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(EntitiesDiagnostics.k_Ea0016Descriptor, parameterSymbol.DeclaringSyntaxReferences[0].GetSyntax().GetLocation()));
            }
        }
    }
}
