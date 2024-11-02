using System;
using System.CodeDom.Compiler;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI;

/*
 The `SystemApiWalker` traverses through syntax nodes that have been marked by the `SystemApiContextModule` as candidates for patching.
 Sometimes these candidate nodes might be nested within other candidate nodes -- e.g. in `SystemAPI.SetComponentEnabled<T>(entity, !SystemAPI.IsComponentEnabled<T>(entity));`,
 both `SystemAPI.SetComponentEnabled<T>(entity, !SystemAPI.GetComponentEnabled<T>(entity))` and `SystemAPI.GetComponentEnabled<T>(entity)`
 are marked as candidates, with the latter nested inside the former. The `SystemApiWalker` will first patch the outer invocation by appending

     global::Unity.Entities.Internal.InternalCompilerInterface.SetComponentEnabledAfterCompletingDependency<T>(ref cachedLookupHandle, ref systemState,

 before moving onwards to the `entity` argument originally passed to `SetComponentEnabled`. The walker will append the `entity` argument verbatim,
 since the argument does not involve any `SystemAPI` invocation. The walker then continues traversing, and when it finally reaches the
 `SystemAPI.IsComponentEnabled<T>(entity)` node, it appends

      global::Unity.Entities.Internal.InternalCompilerInterface.IsComponentEnabledAfterCompletingDependency<T>(ref cachedLookupHandle, ref systemState,

 before moving onwards to the `entity` argument originally passed to `IsComponentEnabled`. This argument will also be appended verbatim, since it
 does not involve any `SystemAPI` invocation either. Finally, the walker appends all the missing closing parentheses to ensure that all the `InternalCompilerInterface`
 invocations are correctly formed. The final code appended by the walker is thus:

      global::Unity.Entities.Internal.InternalCompilerInterface.SetComponentEnabledAfterCompletingDependency<T>(ref cachedLookupHandle, ref systemState, entity,
        global::Unity.Entities.Internal.InternalCompilerInterface.IsComponentEnabledAfterCompletingDependency<T>(ref cachedLookupHandle, ref systemState, entity));
 */
public partial class SystemApiContextSyntaxWalker : CSharpSyntaxWalker, IModuleSyntaxWalker
{
    private IndentedTextWriter _writer;
    private readonly SystemDescription _systemDescription;
    private bool _hasWrittenSyntax;
    private int _numClosingBracketsForNestedSystemApiInvocations;
    private bool _isWalkingNestedInvocation;

    public SystemApiContextSyntaxWalker(SystemDescription systemDescription) : base(SyntaxWalkerDepth.Trivia) =>
        _systemDescription = systemDescription;

    public bool TryWriteSyntax(IndentedTextWriter writer, CandidateSyntax candidateSyntax)
    {
        _writer = writer;
        _hasWrittenSyntax = false;
        _numClosingBracketsForNestedSystemApiInvocations = 0;
        _isWalkingNestedInvocation = false;

        // Begin depth-first traversal of the candidate node
        Visit(candidateSyntax.Node);

        for (int i = 0; i < _numClosingBracketsForNestedSystemApiInvocations; i++)
            _writer.Write(")");

        // Cede write control back to the caller
        return _hasWrittenSyntax;
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // If the current node is a candidate for source generation
        if (_systemDescription.CandidateNodes.TryGetValue(node, out CandidateSyntax candidateSyntax))
        {
            var result = TryGetReplacementCode(node, candidateSyntax);
            switch (result.ReplacedWith)
            {
                case ReplacedWith.InvocationWithMissingArgumentList:
                    _writer.Write(result.Replacement);
                    _isWalkingNestedInvocation = true;
                    base.VisitArgumentList(node.ArgumentList);
                    _hasWrittenSyntax = true;
                    break;
                case ReplacedWith.InvocationWithMissingSystemApiArguments:
                    _writer.Write(result.Replacement);

                    // Visit the arguments of the current invocation to see whether they involve nested SystemAPI invocations.
                    // If yes, patch them accordingly.
                    if (result.ArgumentThatMightInvolveSystemApiInvocation1 != null)
                    {
                        _isWalkingNestedInvocation = true;
                        VisitArgument(result.ArgumentThatMightInvolveSystemApiInvocation1);
                    }

                    if (result.ArgumentThatMightInvolveSystemApiInvocation2 != null)
                    {
                        _isWalkingNestedInvocation = true;

                        _writer.Write(", ");
                        VisitArgument(result.ArgumentThatMightInvolveSystemApiInvocation2);
                    }
                    _writer.Write(")");
                    _numClosingBracketsForNestedSystemApiInvocations--;
                    _hasWrittenSyntax = true;
                    break;
                case ReplacedWith.NotReplaced:
                    if (_isWalkingNestedInvocation)
                    {
                        base.VisitInvocationExpression(node);
                        _hasWrittenSyntax = true;
                    }
                    else
                        // The current node does not require source generation -- it's thus the caller's job to write the current node
                        _hasWrittenSyntax = false;
                    break;
                default:
                    _writer.Write(result.Replacement);
                    _hasWrittenSyntax = true;
                    break;
            }
        }
        else
            base.VisitInvocationExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // If the current node is a candidate for source generation
        if (_systemDescription.CandidateNodes.TryGetValue(node, out CandidateSyntax candidateSyntax))
        {
            var result = TryGetSystemApiTimeReplacementCode(candidateSyntax);

            if (string.IsNullOrEmpty(result))
            {
                // The current node does not require source generation -- it's thus the caller's job to write the current node
                _hasWrittenSyntax = false;
            }
            else
            {
                _hasWrittenSyntax = true;
                _writer.Write(result);
            }
        }
        else
            base.VisitMemberAccessExpression(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        // If the current node is a candidate for source generation
        if (_systemDescription.CandidateNodes.TryGetValue(node, out CandidateSyntax candidateSyntax))
        {
            var result = TryGetSystemApiTimeReplacementCode(candidateSyntax);

            if (string.IsNullOrEmpty(result))
            {
                // The current node does not require source generation -- it's thus the caller's job to write the current node
                _hasWrittenSyntax = false;
            }
            else
            {
                _hasWrittenSyntax = true;
                _writer.Write(result);
            }
        }
        else
            base.VisitIdentifierName(node);
    }

    public override void VisitToken(SyntaxToken token)
    {
        VisitLeadingTrivia(token);
        _writer.Write(token.Text);
        VisitTrailingTrivia(token);
    }

    public override void VisitTrivia(SyntaxTrivia trivia)
    {
        var triviaKind = trivia.Kind();

        if (triviaKind == SyntaxKind.EndOfLineTrivia)
            _writer.WriteLine();

        else if (triviaKind != SyntaxKind.DisabledTextTrivia &&
                 triviaKind != SyntaxKind.PreprocessingMessageTrivia &&
                 triviaKind != SyntaxKind.IfDirectiveTrivia &&
                 triviaKind != SyntaxKind.ElifDirectiveTrivia &&
                 triviaKind != SyntaxKind.ElseDirectiveTrivia &&
                 triviaKind != SyntaxKind.EndIfDirectiveTrivia &&
                 triviaKind != SyntaxKind.RegionDirectiveTrivia &&
                 triviaKind != SyntaxKind.EndRegionDirectiveTrivia &&
                 triviaKind != SyntaxKind.DefineDirectiveTrivia &&
                 triviaKind != SyntaxKind.UndefDirectiveTrivia &&
                 triviaKind != SyntaxKind.ErrorDirectiveTrivia &&
                 triviaKind != SyntaxKind.WarningDirectiveTrivia &&
                 triviaKind != SyntaxKind.PragmaWarningDirectiveTrivia &&
                 triviaKind != SyntaxKind.PragmaChecksumDirectiveTrivia &&
                 triviaKind != SyntaxKind.ReferenceDirectiveTrivia &&
                 triviaKind != SyntaxKind.BadDirectiveTrivia &&
                 triviaKind != SyntaxKind.SingleLineCommentTrivia &&
                 triviaKind != SyntaxKind.MultiLineCommentTrivia)
        {
            if (!trivia.HasStructure)
                _writer.Write(trivia.ToString());
        }
    }
}
