using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.Query;

public struct QueryCandidate : ISystemCandidate
{
    public IReadOnlyCollection<TypeSyntax> QueryTypeNodes { get; private set; }
    public SyntaxNode FullInvocationChainSyntaxNode { get; private set; }
    public TypeDeclarationSyntax ContainingTypeNode { get; private set; }

    public static QueryCandidate From(
        InvocationExpressionSyntax fullInvocationChainSyntaxNode,
        IReadOnlyCollection<TypeSyntax> queryTypeSyntaxNodes)
    {
        return new QueryCandidate
        {
            FullInvocationChainSyntaxNode = fullInvocationChainSyntaxNode,
            QueryTypeNodes = queryTypeSyntaxNodes,
            ContainingTypeNode = fullInvocationChainSyntaxNode.Ancestors().OfType<TypeDeclarationSyntax>().First(),
        };
    }

    public string CandidateTypeName => "SystemAPI.Query";
    public SyntaxNode Node => FullInvocationChainSyntaxNode;
}
