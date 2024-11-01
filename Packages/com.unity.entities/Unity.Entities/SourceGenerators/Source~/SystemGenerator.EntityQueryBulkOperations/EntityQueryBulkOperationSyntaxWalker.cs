using System.CodeDom.Compiler;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.EntityQueryBulkOperations
{
    public class EntityQueryBulkOperationSyntaxWalker : CSharpSyntaxWalker, IModuleSyntaxWalker
    {
        private IndentedTextWriter _writer;
        private readonly IDictionary<InvocationExpressionSyntax, SyntaxNode> _originalToReplacementNodes;
        private bool _hasWrittenSyntax;

        public EntityQueryBulkOperationSyntaxWalker(IDictionary<InvocationExpressionSyntax, SyntaxNode> originalToReplacementNodes) : base(SyntaxWalkerDepth.Trivia) =>
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
                _writer.Write(replacement.ToString());
                _hasWrittenSyntax = true;
            }
            else
                _hasWrittenSyntax = false;
        }
    }
}
