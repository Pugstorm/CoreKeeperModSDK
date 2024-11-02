using System.CodeDom.Compiler;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.Query;

/*
 The `IFESyntaxWalker` traverses through syntax nodes that have been marked by the `IFEModule` as candidates for patching.
 It is much more straightforward than the `SystemApiWalker`, since it does not need to handle nested candidates. For illustration purposes,
 let's use `foreach ((MyAspect, RefRW<EcsTestData3>) queryReturnType in Query<MyAspect, RefRW<EcsTestData3>>())` as an example.

 The `SystemSyntaxWalker` walks the method that contains the `foreach` example above. When it reaches the `GenericNameSyntax` node
 `RefRW<EcsTestData3>`, which has been marked by the `IFEModule` as a candidate for patching, the `SystemSyntaxWalker` cedes write control to the `IFESyntaxWalker`
 by calling `IFESyntaxWalker.TryWriteSyntax()`. The `IFESyntaxWalker` appends `InternalCompilerInterface.UncheckedRefRW<EcsTestData3>`, and then returns control to
 the `SystemSyntaxWalker`. The `SystemSyntaxWalker` then continues walking the method, and when it reaches the `Query<MyAspect, RefRW<EcsTestData3>>()` node, which
 also has been marked by the `IFEModule` as a candidate for patching, it once again cedes write control to the `IFESyntaxWalker`. The `IFESyntaxWalker` appends

    IfeGeneratedType.Query(cachedQuery, cachedIfeTypeHandle, ref systemState)

 and immediately returns control again. The end result is that the `SystemSyntaxWalker` writes the following code:

    foreach ((MyAspect, InternalCompilerInterface.UncheckedRefRW<EcsTestData3>) queryReturnType in IfeGeneratedType.Query(cachedQuery, cachedIfeTypeHandle, ref systemState))
 */
public class IfeSyntaxWalker : CSharpSyntaxWalker, IModuleSyntaxWalker
{
    private IndentedTextWriter _writer;
    private readonly IDictionary<SyntaxNode, string> _candidateNodesToReplacementCode;
    private bool _hasWrittenSyntax;

    public IfeSyntaxWalker(IDictionary<SyntaxNode, string> candidateNodesToReplacementCode) : base(SyntaxWalkerDepth.Trivia) =>
        _candidateNodesToReplacementCode = candidateNodesToReplacementCode;

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
        if (_candidateNodesToReplacementCode.TryGetValue(node, out var replacementCode))
        {
            _writer.Write(replacementCode);
            _hasWrittenSyntax = true;
        }
        else
            _hasWrittenSyntax = false;
    }

    public override void VisitGenericName(GenericNameSyntax node)
    {
        if (_candidateNodesToReplacementCode.TryGetValue(node, out var replacementCode))
        {
            _writer.Write(replacementCode);
            _hasWrittenSyntax = true;
        }
        else
            _hasWrittenSyntax = false;
    }
}
