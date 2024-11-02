// Copyright (c) Tunnel Vision Laboratories, LLC. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Unity.Entities.SourceGen.Common;
using static Unity.Entities.SourceGen.SystemGenerator.LambdaJobs.LambdaParamDescription_EntityCommandBuffer;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

// Find all cases where a member access could be used with explicit `this`
sealed class LambdaBodySyntaxReplacer : CSharpSyntaxWalker
{
    private readonly LambdaJobDescription _lambdaJobDescription;
    private bool MustReplaceEcbNodesWithJobChunkParallelWriterNodes =>
        _lambdaJobDescription.EntityCommandBufferParameter is {Playback: {ScheduleMode: ScheduleMode.ScheduleParallel}};

    private bool MustReplaceEcbNodesWithJobChunkEcbNodes =>
        _lambdaJobDescription.EntityCommandBufferParameter != null &&
        _lambdaJobDescription.EntityCommandBufferParameter.Playback.ScheduleMode != ScheduleMode.ScheduleParallel;

    public bool NeedsTimeData { get; private set; }

    readonly List<SyntaxNode> _lineStatementNodesNeedingReplacement = new List<SyntaxNode>();
    readonly List<SyntaxNode> _thisNodesNeedingReplacement = new List<SyntaxNode>();
    readonly List<SyntaxNode> _constantNodesNeedingReplacement = new List<SyntaxNode>();
    readonly List<SyntaxNode> _ecbNodesToReplaceWithJobChunkEcbNodes = new List<SyntaxNode>();
    readonly List<SyntaxNode> _SimpleMemberAccessExpressionsNeedingReplacement = new List<SyntaxNode>();

    // These two lists are populated iff MustReplaceEntityCommandsMemberAccessNodesWithParallelWriterNodes is true.
    // A node with index i in _entityCommandInvocationExpressionNodesToReplace is to be replaced by the node with the same index in _parallelWriterInvocationNodes.
    // We use two lists instead of a dictionary because we need to access items in _parallelWriterInvocationNodes by index.
    readonly List<SyntaxNode> _ecbNodesToReplaceWithJobChunkParallelWriterNodes = new List<SyntaxNode>();
    readonly List<SyntaxNode> _parallelWriterInvocationNodes = new List<SyntaxNode>();

    public (SyntaxNode rewrittenLambdaExpression, List<SyntaxNode> thisAccessNodesNeedingReplacement) Rewrite()
    {
        Visit(_lambdaJobDescription.OriginalLambdaExpression);

        var allTrackedNodes =
            _lineStatementNodesNeedingReplacement
                .Concat(_thisNodesNeedingReplacement)
                .Concat(_constantNodesNeedingReplacement)
                .Concat(_ecbNodesToReplaceWithJobChunkEcbNodes)
                .Concat(_ecbNodesToReplaceWithJobChunkParallelWriterNodes)
                .Concat(_SimpleMemberAccessExpressionsNeedingReplacement);

        if (!allTrackedNodes.Any())
        {
            return (_lambdaJobDescription.OriginalLambdaExpression, new List<SyntaxNode>());
        }

        // Track nodes for later rewriting
        var rewrittenBody = _lambdaJobDescription.OriginalLambdaExpression.TrackNodes(allTrackedNodes);

        if (MustReplaceEcbNodesWithJobChunkEcbNodes)
        {
            var ecbParameterNodesToReplace = rewrittenBody.GetCurrentNodes(_ecbNodesToReplaceWithJobChunkEcbNodes);

            rewrittenBody = rewrittenBody.ReplaceNodes(
                ecbParameterNodesToReplace,
                (current, replacement) => SyntaxFactory.IdentifierName(GeneratedEcbFieldNameInJobChunkType));
        }
        else if (MustReplaceEcbNodesWithJobChunkParallelWriterNodes)
        {
            var currentEntityCommandInvocationNodes =
                rewrittenBody.GetCurrentNodes(_ecbNodesToReplaceWithJobChunkParallelWriterNodes).ToArray();

            rewrittenBody =
                rewrittenBody.ReplaceNodes(
                    currentEntityCommandInvocationNodes,
                    (current, _) =>
                    {
                        var index = Array.IndexOf(currentEntityCommandInvocationNodes, current);
                        return _parallelWriterInvocationNodes[index];
                    });
        }

        // Replace use of constants in the lambda with the actual constant value
        var currentConstantNodesNeedingReplacement = rewrittenBody.GetCurrentNodes(_constantNodesNeedingReplacement);

        var oldToNewConstantNodesNeedingReplacement =
            currentConstantNodesNeedingReplacement
                .Zip(
                    _constantNodesNeedingReplacement,
                    (k, v) => new {key = k, value = v})
                .ToDictionary(x => x.key, x => x.value);

        rewrittenBody =
            rewrittenBody.ReplaceNodes(
                currentConstantNodesNeedingReplacement,
                (originalNode, _) =>
                    ReplaceConstantNodesWithConstantValue(_lambdaJobDescription.SystemDescription.SemanticModel, originalNode, oldToNewConstantNodesNeedingReplacement));

        // Replace MemberAccess that isn't invocationsyntax
        if (_SimpleMemberAccessExpressionsNeedingReplacement.Count > 0)
        {
            rewrittenBody = rewrittenBody.ReplaceNodes(
                rewrittenBody.GetCurrentNodes(_SimpleMemberAccessExpressionsNeedingReplacement),
                (originalNode, _) =>
                {
                    switch (originalNode)
                    {
                        case IdentifierNameSyntax {Identifier: {ValueText: "Time"}}:
                            NeedsTimeData = true;
                            return SyntaxFactory.ParseExpression($"__Time");
                        case MemberAccessExpressionSyntax {Name: {Identifier: {ValueText: "Time"}}}:
                            NeedsTimeData = true;
                            return SyntaxFactory.ParseExpression($"__Time");
                        default:
                            return originalNode;
                    }
                });
        }

        // Replace those locations to access through "__this" instead (since they need to access through a stored field on job struct).
        // Also replace data access methods on SystemBase that need to be patched (GetComponent/SetComponent/etc)
        var currentThisNodesNeedingReplacement = rewrittenBody.GetCurrentNodes(_thisNodesNeedingReplacement);
        rewrittenBody =
            rewrittenBody.ReplaceNodes(currentThisNodesNeedingReplacement,
                (originalNode, _) => originalNode is MemberAccessExpressionSyntax ? originalNode
                    : SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,k_LocalThisFieldSyntax, (SimpleNameSyntax) originalNode));

        // Also rewrite any remaining references to `this` to our local this
        rewrittenBody =
            rewrittenBody.ReplaceNodes(rewrittenBody.DescendantNodes().OfType<ThisExpressionSyntax>(),
                (old, _ ) => k_LocalThisFieldSyntax);

        // Replace line statements with one's with line directive trivia
        foreach (var originalNode in _lineStatementNodesNeedingReplacement)
        {
            var currentNode = rewrittenBody.GetCurrentNode(originalNode);
            rewrittenBody = rewrittenBody.ReplaceNode(currentNode, WithLineTrivia(currentNode, originalNode.SyntaxTree.FilePath, originalNode.GetLineNumber()));
        }

        return (rewrittenBody, _thisNodesNeedingReplacement);
    }

    static SyntaxNode WithLineTrivia(SyntaxNode node, string originalFilePath, int originalLineNumber, int offsetLineNumber = 1)
    {
        if (string.IsNullOrEmpty(originalFilePath))
            return node;

        var lineTrivia = SyntaxFactory.Comment($"#line {originalLineNumber + offsetLineNumber} \"{originalFilePath}\"");
        return node.WithLeadingTrivia(lineTrivia, SyntaxFactory.EndOfLine(Environment.NewLine));
    }

    static readonly ExpressionSyntax k_LocalThisFieldSyntax = SyntaxFactory.IdentifierName("__this");
    static SyntaxNode ReplaceConstantNodesWithConstantValue(SemanticModel model, SyntaxNode node, Dictionary<SyntaxNode, SyntaxNode> oldToNewConstantNodesNeedingReplacement)
    {
        var originalNode = oldToNewConstantNodesNeedingReplacement[node];
        var symbolInfo = model.GetSymbolInfo(originalNode);

        if (symbolInfo.Symbol is ILocalSymbol {HasConstantValue: true} localSymbol)
        {
            try
            {
                // Need to special case float or else it is output as a double value
                if (localSymbol.ConstantValue is float floatConstant)
                    return SyntaxFactory.ParseExpression($"{floatConstant.ToString(CultureInfo.InvariantCulture)}f");
                return
                    SyntaxFactory.ParseExpression(GetTypedConstantKind(localSymbol.Type) == TypedConstantKind.Enum
                        ? $"({localSymbol.Type.ToFullName()}){localSymbol.ConstantValue}"
                        : localSymbol.ConstantValue.ToString());
            }
            catch (Exception)
            {
                throw new InvalidOperationException($"Could not parse constant literal expression for symbol in node {originalNode}");
            }
        }
        throw new InvalidOperationException($"Unable to find symbol info with constant value for symbol in node {originalNode}");

        static TypedConstantKind GetTypedConstantKind(ITypeSymbol type)
        {
            return type.SpecialType switch
            {
                SpecialType.System_Boolean => TypedConstantKind.Primitive,
                SpecialType.System_SByte => TypedConstantKind.Primitive,
                SpecialType.System_Int16 => TypedConstantKind.Primitive,
                SpecialType.System_Int32 => TypedConstantKind.Primitive,
                SpecialType.System_Int64 => TypedConstantKind.Primitive,
                SpecialType.System_Byte => TypedConstantKind.Primitive,
                SpecialType.System_UInt16 => TypedConstantKind.Primitive,
                SpecialType.System_UInt32 => TypedConstantKind.Primitive,
                SpecialType.System_UInt64 => TypedConstantKind.Primitive,
                SpecialType.System_Single => TypedConstantKind.Primitive,
                SpecialType.System_Double => TypedConstantKind.Primitive,
                SpecialType.System_Char => TypedConstantKind.Primitive,
                SpecialType.System_String => TypedConstantKind.Primitive,
                SpecialType.System_Object => TypedConstantKind.Primitive,
                _ => type.TypeKind switch
                {
                    TypeKind.Array => TypedConstantKind.Array,
                    TypeKind.Enum => TypedConstantKind.Enum,
                    TypeKind.Error => TypedConstantKind.Error,
                    _ => TypedConstantKind.Type
                }
            };
        }
    }

    public LambdaBodySyntaxReplacer(LambdaJobDescription lambdaJobDescription)
    {
        _lambdaJobDescription = lambdaJobDescription;
    }

    public override void Visit(SyntaxNode node)
    {
        switch (node)
        {
            case StatementSyntax statementSyntax when !(node is BlockSyntax):
                HandleStatement(statementSyntax);
                break;
            case SimpleNameSyntax simpleNameSyntax:
                HandleSimpleName(simpleNameSyntax);
                break;
            case MemberAccessExpressionSyntax {Expression: IdentifierNameSyntax identifierNameSyntax}:
                HandleIdentifierName(identifierNameSyntax);
                break;
            case InvocationExpressionSyntax invocationExpressionSyntax:
                HandleInvocationExpressionSyntax(invocationExpressionSyntax);
                break;
        }

        base.Visit(node);
    }

    void HandleInvocationExpressionSyntax(InvocationExpressionSyntax originalNode)
    {
        if (_lambdaJobDescription.SystemDescription.SemanticModel.GetOperation(originalNode) is IInvocationOperation invocationOperation)
        {
            var isEcbMethod = invocationOperation.TargetMethod.ContainingType.Is("global::Unity.Entities.EntityCommandBuffer")
                              || invocationOperation.TargetMethod.ContainingType.Is("Unity.Entities.EntityCommandBufferManagedComponentExtensions");

            if (!isEcbMethod)
            {
                return;
            }

            if (originalNode.Expression is MemberAccessExpressionSyntax {Expression: IdentifierNameSyntax identifierNameSyntax}
                && _lambdaJobDescription.SystemDescription.SemanticModel.GetOperation(identifierNameSyntax) is IParameterReferenceOperation)
            {
                bool isSupportedInEntitiesForEach = invocationOperation.TargetMethod.GetAttributes().Any(a => a.AttributeClass.Is("Unity.Entities.SupportedInEntitiesForEach"));

                if (!isSupportedInEntitiesForEach)
                {
                    LambdaJobsErrors.DC0079(
                        _lambdaJobDescription.SystemDescription,
                        invocationOperation.TargetMethod.ToDisplayString(),
                        originalNode.GetLocation());

                    throw new InvalidDescriptionException();
                }

                if (_lambdaJobDescription.Schedule.Mode != ScheduleMode.Run && !CanRunOutsideOfMainThread(invocationOperation))
                {
                    LambdaJobsErrors.DC0080(
                        _lambdaJobDescription.SystemDescription,
                        invocationOperation.TargetMethod.ToDisplayString(),
                        originalNode.GetLocation());

                    throw new InvalidDescriptionException();
                }

                if (MustReplaceEcbNodesWithJobChunkParallelWriterNodes)
                {
                    _ecbNodesToReplaceWithJobChunkParallelWriterNodes.Add(originalNode);

                    var replacementNode = ParallelEcbInvocationsReplacer.CreateReplacement(originalNode, invocationOperation);
                    _parallelWriterInvocationNodes.Add(replacementNode);
                }
            }
        }
    }

    bool CanRunOutsideOfMainThread(IInvocationOperation operation)
    {
        if (operation.TargetMethod.ContainingType.Is("Unity.Entities.EntityCommandBufferManagedComponentExtensions"))
            return false;

        return !operation.TargetMethod.Parameters.Any(p => p.Type.Is("Unity.Entities.EntityQuery"));
    }

    void HandleStatement(StatementSyntax statementSyntax)
    {
        _lineStatementNodesNeedingReplacement.Add(statementSyntax);
    }

    void HandleSimpleName(SimpleNameSyntax node)
    {
        switch (node?.Parent?.Kind() ?? SyntaxKind.None)
        {
            case SyntaxKind.SimpleMemberAccessExpression:
                // this is handled separately
                return;

            case SyntaxKind.MemberBindingExpression:
            case SyntaxKind.NameColon:
            case SyntaxKind.PointerMemberAccessExpression:
                // this doesn't need to be handled
                return;

            case SyntaxKind.QualifiedCref:
            case SyntaxKind.NameMemberCref:
                // documentation comments don't use 'this.'
                return;

            case SyntaxKind.SimpleAssignmentExpression:
                if (((AssignmentExpressionSyntax)node.Parent).Left == node
                    && (node.Parent.Parent?.IsKind(SyntaxKind.ObjectInitializerExpression) ?? true))
                {
                    /* Handle 'X' in:
                     *   new TypeName() { X = 3 }
                     */
                    return;
                }

                break;

            case SyntaxKind.NameEquals:
                if (((NameEqualsSyntax)node.Parent).Name != node)
                {
                    break;
                }

                switch (node?.Parent?.Parent?.Kind())
                {
                    case SyntaxKind.AttributeArgument:
                    case SyntaxKind.AnonymousObjectMemberDeclarator:
                        return;
                }

                break;

            case SyntaxKind.Argument when IsPartOfConstructorInitializer(node):
                // constructor invocations cannot contain this.
                return;
        }

        HandleIdentifierName(node);
    }

    void HandleIdentifierName(SimpleNameSyntax nameSyntax)
    {
        if (nameSyntax == null)
            return;

        var symbolInfo = _lambdaJobDescription.SystemDescription.SemanticModel.GetSymbolInfo(nameSyntax);

        // Support `using static SystemAPI`
        if (symbolInfo.Symbol != null && symbolInfo.Symbol.ContainingType.Is("Unity.Entities.SystemAPI"))
        {
            if (symbolInfo.Symbol.Kind == SymbolKind.Method)
                _thisNodesNeedingReplacement.Add(nameSyntax);
            else
                _SimpleMemberAccessExpressionsNeedingReplacement.Add(nameSyntax);

            return;
        }

        // Support `SystemAPI.***`
        if (symbolInfo.Symbol is {IsStatic: true} && symbolInfo.Symbol.ToDisplayString() == "Unity.Entities.SystemAPI")
        {
            if (nameSyntax.Parent != null)
            {
                var parentSymbolInfo = _lambdaJobDescription.SystemDescription.SemanticModel.GetSymbolInfo(nameSyntax.Parent);
                if (parentSymbolInfo.Symbol is {Kind: SymbolKind.Method})
                    _thisNodesNeedingReplacement.Add(nameSyntax.Parent);
                else
                    _SimpleMemberAccessExpressionsNeedingReplacement.Add(nameSyntax.Parent);
            }

            return;
        }

        if (!HasThis(nameSyntax))
            return;

        if (symbolInfo.Symbol is ILocalSymbol {IsConst: true, HasConstantValue: true})
        {
            _constantNodesNeedingReplacement.Add(nameSyntax);
            return;
        }

        if (symbolInfo.Symbol is IParameterSymbol parameterSymbol
            && parameterSymbol.Type.Is("Unity.Entities.EntityCommandBuffer")
            && MustReplaceEcbNodesWithJobChunkEcbNodes)
        {
            _ecbNodesToReplaceWithJobChunkEcbNodes.Add(nameSyntax);
            return;
        }

        ImmutableArray<ISymbol> symbolsToAnalyze;
        if (symbolInfo.Symbol != null)
            symbolsToAnalyze = ImmutableArray.Create(symbolInfo.Symbol);
        // Bug in roslyn when resolving multiple constraint generic method symbols (causes OverloadResolutionFailure): https://github.com/dotnet/roslyn/issues/61504
        else if (symbolInfo.CandidateReason == CandidateReason.MemberGroup || symbolInfo.CandidateReason == CandidateReason.OverloadResolutionFailure)
            // analyze the complete set of candidates, and use 'this.' if it applies to all
            symbolsToAnalyze = symbolInfo.CandidateSymbols;
        else
            return;

        foreach (var symbol in symbolsToAnalyze)
        {
            if (symbol is ITypeSymbol)
                return;

            if (symbol.IsStatic)
                return;

            if (!(symbol.ContainingSymbol is ITypeSymbol))
                // covers local variables, parameters, etc.
                return;

            if (symbol is IMethodSymbol methodSymbol)
            {
                switch (methodSymbol.MethodKind)
                {
                    case MethodKind.Constructor:
                    case MethodKind.LocalFunction:
                    case MethodKind.LambdaMethod:
                        return;
                }
            }

            // This is a workaround for:
            // - https://github.com/DotNetAnalyzers/StyleCopAnalyzers/issues/1501
            // - https://github.com/DotNetAnalyzers/StyleCopAnalyzers/issues/2093
            // and can be removed when the underlying bug in roslyn is resolved
            if (nameSyntax.Parent is MemberAccessExpressionSyntax)
            {
                var memberAccessSymbol = _lambdaJobDescription.SystemDescription.SemanticModel.GetSymbolInfo(nameSyntax.Parent).Symbol;

                switch (memberAccessSymbol?.Kind)
                {
                    case null:
                        break;

                    case SymbolKind.Field:
                    case SymbolKind.Method:
                    case SymbolKind.Property:
                        if (memberAccessSymbol.IsStatic && (memberAccessSymbol.ContainingType.Name == symbol.Name))
                        {
                            return;
                        }

                        break;
                }
            }

            // End of workaround
        }

        _thisNodesNeedingReplacement.Add(nameSyntax);
    }

    static bool HasThis(SyntaxNode node)
    {
        for (; node != null; node = node.Parent)
        {
            switch (node.Kind())
            {
                case SyntaxKind.ClassDeclaration:
                case SyntaxKind.InterfaceDeclaration:
                case SyntaxKind.StructDeclaration:
                case SyntaxKind.DelegateDeclaration:
                case SyntaxKind.EnumDeclaration:
                case SyntaxKind.NamespaceDeclaration:
                    return false;

                case SyntaxKind.FieldDeclaration:
                case SyntaxKind.EventFieldDeclaration:
                    return false;

                case SyntaxKind.EventDeclaration:
                case SyntaxKind.IndexerDeclaration:
                    var basePropertySyntax = (BasePropertyDeclarationSyntax)node;
                    return !basePropertySyntax.Modifiers.Any(SyntaxKind.StaticKeyword);

                case SyntaxKind.PropertyDeclaration:
                    var propertySyntax = (PropertyDeclarationSyntax)node;
                    return !propertySyntax.Modifiers.Any(SyntaxKind.StaticKeyword) && propertySyntax.Initializer == null;

                case SyntaxKind.MultiLineDocumentationCommentTrivia:
                case SyntaxKind.SingleLineDocumentationCommentTrivia:
                    return false;

                case SyntaxKind.ConstructorDeclaration:
                case SyntaxKind.DestructorDeclaration:
                case SyntaxKind.MethodDeclaration:
                    var baseMethodSyntax = (BaseMethodDeclarationSyntax)node;
                    return !baseMethodSyntax.Modifiers.Any(SyntaxKind.StaticKeyword);

                case SyntaxKind.Attribute:
                    return false;

                default:
                    continue;
            }
        }

        return false;
    }

    static bool IsPartOfConstructorInitializer(SyntaxNode node)
    {
        for (; node != null; node = node.Parent)
        {
            switch (node.Kind())
            {
                case SyntaxKind.ThisConstructorInitializer:
                case SyntaxKind.BaseConstructorInitializer:
                    return true;
            }
        }

        return false;
    }
}
