using System.CodeDom.Compiler;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

public class EntitiesForEachSyntaxWalker : CSharpSyntaxWalker, IModuleSyntaxWalker
{
    private readonly Dictionary<SyntaxNode, string> _originalToReplacementNodes;

    private IndentedTextWriter _writer;
    private bool _hasWrittenSyntax;

    public EntitiesForEachSyntaxWalker(Dictionary<SyntaxNode, string> originalToReplacementNodes) : base(SyntaxWalkerDepth.Trivia) =>
        _originalToReplacementNodes = originalToReplacementNodes;

    public bool TryWriteSyntax(IndentedTextWriter writer, CandidateSyntax candidateSyntax)
    {
        _writer = writer;
        _hasWrittenSyntax = false;

        // Begin depth-first traversal of the candidate node
        Visit(candidateSyntax.Node);

        return _hasWrittenSyntax;
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (_originalToReplacementNodes.TryGetValue(node, out var replacement))
        {
            _writer.WriteLine();
            _writer.Write(replacement);
            _hasWrittenSyntax = true;
        }
        else
            _hasWrittenSyntax = false;
    }
}
