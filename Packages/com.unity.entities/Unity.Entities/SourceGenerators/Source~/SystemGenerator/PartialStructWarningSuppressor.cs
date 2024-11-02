using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PartialStructWarningSuppressor : DiagnosticSuppressor
    {
        static readonly SuppressionDescriptor PartialStructWarningRule = new SuppressionDescriptor("SPDC0282", "CS0282",
            "Some DOTS types utilize codegen requiring the type to be partial.");

        static readonly string[] _allowedPartialStructInterfaces = { "ISystem", "ISystemBase", "IJobEntity", "IAspect" };

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray.Create(PartialStructWarningRule);

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            foreach (var diagnostic in context.ReportedDiagnostics.Where(diagnostic => diagnostic.Id == PartialStructWarningRule.SuppressedDiagnosticId))
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var node = diagnostic.Location.SourceTree?.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan);
                if (node is StructDeclarationSyntax {BaseList: { } baseList} structSyntax)
                    if (baseList.Types.Any(type => _allowedPartialStructInterfaces.Contains(type.ToString())))
                        context.ReportSuppression(Suppression.Create(PartialStructWarningRule, diagnostic));
                    else
                    {
                        var semanticModel = context.GetSemanticModel(structSyntax.SyntaxTree);
                        var typeSymbol = semanticModel.GetDeclaredSymbol(structSyntax);

                        if (_allowedPartialStructInterfaces.Select(@interface => $"Unity.Entities.{@interface}").Any(@interface => typeSymbol.InheritsFromInterface(@interface)))
                            context.ReportSuppression(Suppression.Create(PartialStructWarningRule, diagnostic));
                    }
            }
        }
    }
}
