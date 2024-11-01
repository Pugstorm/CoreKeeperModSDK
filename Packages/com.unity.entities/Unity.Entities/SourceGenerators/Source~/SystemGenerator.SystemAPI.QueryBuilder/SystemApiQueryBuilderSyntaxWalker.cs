using System.CodeDom.Compiler;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.QueryBuilder
{
/*
 The `QueryBuilderWalker` traverses through syntax nodes that have been marked by the `QueryBuilderModule` as candidates for patching.
 It is much more straightforward than the `SystemApiWalker`, since it does not need to handle nested candidates. For illustration purposes,
 let's use `var query = SystemAPI.QueryBuilder().WithAspect<MyAspect>().Build();` as an example.

 The `SystemSyntaxWalker` walks the method that contains the line above. When it reaches the `InvocationExpressionSyntax` node
 `SystemAPI.QueryBuilder().WithAspect<MyAspect>().Build()`, which has been marked by the `QueryBuilderModule` as a candidate for patching, the `SystemSyntaxWalker`
 cedes write control to the `QueryBuilderWalker` by calling `QueryBuilderWalker.TryWriteSyntax()`. The `QueryBuilderWalker` appends `__generatedAndCachedInSystemQuery`,
 and then returns control to the `SystemSyntaxWalker`. The end result is that the `SystemSyntaxWalker` writes the following code:

    var query = __generatedAndCachedInSystemQuery;
 */
    public class SystemApiQueryBuilderSyntaxWalker : CSharpSyntaxWalker, IModuleSyntaxWalker
    {
        private readonly Dictionary<SyntaxNode, SystemApiQueryBuilderDescription> _descriptionsGroupedByNodesToReplace;

        private IndentedTextWriter _writer;
        private bool _hasWrittenSyntax;

        public SystemApiQueryBuilderSyntaxWalker(Dictionary<SyntaxNode, SystemApiQueryBuilderDescription> descriptionsGroupedByNodesToReplace) : base(SyntaxWalkerDepth.Trivia)
            => _descriptionsGroupedByNodesToReplace = descriptionsGroupedByNodesToReplace;

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
            if (_descriptionsGroupedByNodesToReplace.TryGetValue(node, out var description))
            {
                _writer.Write($" {description.GeneratedEntityQueryFieldName}");
                _hasWrittenSyntax = true;
            }
            else
                _hasWrittenSyntax = false;
        }
    }
}
