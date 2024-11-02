using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.QueryBuilder
{
    public struct QueryCandidate : ISystemCandidate
    {
        public TypeDeclarationSyntax ContainingTypeNode { get; private set; }
        public SyntaxNode BuildNode { get; private set; }
        public InvocationExpressionSyntax SystemAPIQueryBuilderNode { get; private set; }

        public SyntaxNode Node => BuildNode;
        public string CandidateTypeName => "SystemAPI.QueryBuilder";

        public static (bool Success, QueryCandidate Result) TryCreateFrom(InvocationExpressionSyntax systemAPIQueryBuilderNode)
        {
            var buildNode =
                systemAPIQueryBuilderNode
                    .Ancestors()
                    .OfType<InvocationExpressionSyntax>()
                    .LastOrDefault(IsBuildNode);

            var hasValidInvocationChain = buildNode != null;

            if (!hasValidInvocationChain)
                return (false, default);

            return (true, new QueryCandidate
            {
                SystemAPIQueryBuilderNode = systemAPIQueryBuilderNode,
                BuildNode = buildNode,
                ContainingTypeNode = systemAPIQueryBuilderNode.Ancestors().OfType<TypeDeclarationSyntax>().First()
            });

            bool IsBuildNode(InvocationExpressionSyntax invocationExpressionSyntax) =>
                invocationExpressionSyntax.Expression is MemberAccessExpressionSyntax { Name: { Identifier:
                {
                    ValueText: "Build"
                } } };
        }
    }
}
