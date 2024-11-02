using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Unity.Entities.SourceGen.Common
{
    public static class SyntaxExtensions
    {
        public static bool IsReadOnly(this ParameterSyntax parameter) => parameter.Modifiers.Any(mod => mod.IsKind(SyntaxKind.InKeyword));
        public static bool IsReadOnly(this IParameterSymbol parameter) => parameter.RefKind == RefKind.In;

        class PreprocessorTriviaRemover : CSharpSyntaxRewriter
        {
            public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
            {
                if (trivia.IsKind(SyntaxKind.DisabledTextTrivia) ||
                    trivia.IsKind(SyntaxKind.PreprocessingMessageTrivia) ||
                    trivia.IsKind(SyntaxKind.IfDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.ElifDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.ElseDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.RegionDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.DefineDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.UndefDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.ErrorDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.WarningDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.PragmaChecksumDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.ReferenceDirectiveTrivia) ||
                    trivia.IsKind(SyntaxKind.BadDirectiveTrivia))
                    return default;
                return trivia;
            }
        }

        public static T WithoutPreprocessorTrivia<T>(this T node) where T : SyntaxNode
        {
            var preprocessorTriviaRemover = new PreprocessorTriviaRemover();
            return (T)preprocessorTriviaRemover.Visit(node);
        }

        // Lambda body can exist as block, statement or just expression (create BodyBlock in the last two cases)
        public static BlockSyntax ToBlockSyntax(this ParenthesizedLambdaExpressionSyntax node)
        {
            if (node.Block != null)
                return node.Block;

            return node.Body switch
            {
                StatementSyntax lambdaBodyStatement => Block(lambdaBodyStatement),
                ExpressionSyntax lambdaBodyExpression => Block(ExpressionStatement(lambdaBodyExpression)),
                _ => throw new InvalidOperationException($"Invalid lambda body: {node.Body}")
            };
        }

        public static bool ContainsDynamicCode(this InvocationExpressionSyntax invoke)
        {
            var argumentList = invoke.DescendantNodes().OfType<ArgumentListSyntax>().LastOrDefault();
            return argumentList?.DescendantNodes().OfType<ConditionalExpressionSyntax>().FirstOrDefault() != null;
        }

        public static T AncestorOfKind<T>(this SyntaxNode node) where T : SyntaxNode
        {
            foreach (var ancestor in node.Ancestors())
                if (ancestor is T t)
                    return t;
            throw new InvalidOperationException($"No Ancestor {nameof(T)} found.");
        }

        public static T AncestorOfKindOrDefault<T>(this SyntaxNode node) where T : SyntaxNode
        {
            foreach (var ancestor in node.Ancestors())
                if (ancestor is T t)
                    return t;
            return null;
        }

        public static int GetLineNumber(this SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line;

        public static string GetModifierString(this ParameterSyntax parameter)
        {
            foreach (var mod in parameter.Modifiers)
            {
                if (mod.IsKind(SyntaxKind.InKeyword))
                    return "in";
                if (mod.IsKind(SyntaxKind.RefKeyword))
                    return "ref";
            }
            return "";
        }
    }
}
