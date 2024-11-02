using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI;

public partial class SystemApiContextSyntaxWalker
{
    private enum ReplacedWith
    {
        NotReplaced,
        InvocationWithMissingArgumentList,
        InvocationWithFullArgumentList,
        InvocationWithMissingSystemApiArguments
    }

    private string TryGetSystemApiTimeReplacementCode(CandidateSyntax candidateSyntax)
    {
        var semanticModel = _systemDescription.SemanticModel;

        var resolveCandidateSymbol = semanticModel.GetSymbolInfo(candidateSyntax.Node);
        var nodeSymbol = resolveCandidateSymbol.Symbol ?? resolveCandidateSymbol.CandidateSymbols.FirstOrDefault();
        var parentTypeInfo = nodeSymbol?.ContainingType;
        var isSystemApi = parentTypeInfo?.ToFullName() == "global::Unity.Entities.SystemAPI";

        if (!isSystemApi)
            return default;

        return _systemDescription.TryGetSystemStateParameterName(candidateSyntax, out var systemStateExpression)
            ? $"{systemStateExpression}.WorldUnmanaged.Time"
            : null;
    }

    private (string Replacement,
        ReplacedWith ReplacedWith,
        ArgumentSyntax ArgumentThatMightInvolveSystemApiInvocation1,
        ArgumentSyntax ArgumentThatMightInvolveSystemApiInvocation2)
        TryGetReplacementCode(InvocationExpressionSyntax invocationExpressionSyntax, CandidateSyntax candidateSyntax)
    {
        var semanticModel = _systemDescription.SemanticModel;

        var resolveCandidateSymbol = semanticModel.GetSymbolInfo(candidateSyntax.Node);
        var nodeSymbol = resolveCandidateSymbol.Symbol ?? resolveCandidateSymbol.CandidateSymbols.FirstOrDefault();
        var parentTypeInfo = nodeSymbol?.ContainingType;

        var fullName = parentTypeInfo?.ToFullName();
        var isSystemApi = fullName == "global::Unity.Entities.SystemAPI";
        var isManagedApi = fullName == "global::Unity.Entities.SystemAPI.ManagedAPI";

        if (!isSystemApi && !isManagedApi && !(IsSingleton(candidateSyntax) && parentTypeInfo.Is("Unity.Entities.ComponentSystemBase")))
            return default;

        bool IsSingleton(CandidateSyntax syntax) => syntax.Type is CandidateType.SingletonWithArgument or CandidateType.SingletonWithoutArgument;

        switch (nodeSymbol)
        {
            // No type argument (EntityStorageInfoLookup, Exists)
            case IMethodSymbol { TypeArguments.Length: 0 }:
            {
                switch (candidateSyntax.Type)
                {
                    case CandidateType.GetEntityStorageInfoLookup:
                    {
                        var storageInfoLookupField = _systemDescription.QueriesAndHandles
                            .GetOrCreateEntityStorageInfoLookupField();

                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax, out var systemState))
                            return default;

                        return (
                            $"global::Unity.Entities.Internal.InternalCompilerInterface.GetEntityStorageInfoLookup(ref __TypeHandle.{storageInfoLookupField}, ref {systemState})",
                            ReplacedWith.InvocationWithFullArgumentList,
                            ArgumentThatMightInvolveSystemApiInvocation1: default,
                            ArgumentThatMightInvolveSystemApiInvocation2: default
                        );
                    }

                    case CandidateType.Exists:
                    {
                        var storageInfoLookupField = _systemDescription.QueriesAndHandles
                            .GetOrCreateEntityStorageInfoLookupField();

                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax, out var systemState))
                            return default;

                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.Single();

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return (
                            $"global::Unity.Entities.Internal.InternalCompilerInterface.DoesEntityExist(ref __TypeHandle.{storageInfoLookupField}, ref {systemState}, ",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: default
                        );
                    }

                    case CandidateType.EntityTypeHandle:
                    {
                        var typeHandleField =
                            _systemDescription.QueriesAndHandles.GetOrCreateEntityTypeHandleField();

                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        return (
                            $"global::Unity.Entities.Internal.InternalCompilerInterface.GetEntityTypeHandle(ref __TypeHandle.{typeHandleField}, ref {systemStateExpression})",
                            ReplacedWith.InvocationWithFullArgumentList,
                            ArgumentThatMightInvolveSystemApiInvocation1: default,
                            ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }
                }

                break;
            }

            // Based on type argument
            case IMethodSymbol { TypeArguments.Length: 1 } namedTypeSymbolWithTypeArg:
            {
                var typeArgument = namedTypeSymbolWithTypeArg.TypeArguments.Single();

                if (TryGetSystemBaseGeneric(out string replacementCode, out ReplacedWith replacedWith))
                    return (
                        replacementCode,
                        replacedWith,
                        ArgumentThatMightInvolveSystemApiInvocation1: default,
                        ArgumentThatMightInvolveSystemApiInvocation2: default);

                switch (candidateSyntax.Type)
                {
                    case CandidateType.GetComponentLookup:
                    {
                        var @readonly = false;
                        var args = invocationExpressionSyntax.ArgumentList.Arguments.ToArray();
                        if (args.Length == 0 || bool.TryParse(args[0].Expression.ToString(), out @readonly))
                        {
                            var lookup =
                                _systemDescription.QueriesAndHandles.GetOrCreateComponentLookupField(typeArgument,
                                    @readonly);

                            if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                    out var systemStateExpression))
                                return default;

                            return (
                                $"global::Unity.Entities.Internal.InternalCompilerInterface.GetComponentLookup<{typeArgument.ToFullName()}>(ref __TypeHandle.{lookup}, ref {systemStateExpression})",
                                ReplacedWith.InvocationWithFullArgumentList,
                                ArgumentThatMightInvolveSystemApiInvocation1: default,
                                ArgumentThatMightInvolveSystemApiInvocation2: default);
                        }

                        var methodDeclarationSyntax = candidateSyntax.Node.AncestorOfKind<MethodDeclarationSyntax>();
                        if (methodDeclarationSyntax.Identifier.ValueText == "OnCreate")
                        {
                            var containingMethodSymbol = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax);
                            if (containingMethodSymbol.Parameters.Length == 0 ||
                                (containingMethodSymbol.Parameters.Length == 1 && containingMethodSymbol.Parameters[0]
                                    .Type.Is("global::Unity.Entities.SystemState")))
                            {
                                _systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                    out var systemStateExpression); // Ok to not handle as you can't be in OnCreate without it. By definition of above SystemState constraint.
                                return (
                                    $"{systemStateExpression}.{CandidateSyntax.GetSimpleName(candidateSyntax.Node)}({invocationExpressionSyntax.ArgumentList.ToFullString()})",
                                    ReplacedWith.InvocationWithFullArgumentList,
                                    ArgumentThatMightInvolveSystemApiInvocation1: default,
                                    ArgumentThatMightInvolveSystemApiInvocation2: default);
                            }
                        }

                        SystemApiContextErrors.SGSA0002(_systemDescription, candidateSyntax);
                        break;
                    }
                    case CandidateType.GetComponent when isManagedApi:
                    {
                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.Single();
                        var typeArg = candidateSyntax.Node.DescendantNodes().OfType<GenericNameSyntax>().First()
                            .TypeArgumentList.Arguments.Single();

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return ($"{systemStateExpression}.EntityManager.GetComponentObject<{typeArg}>(",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }
                    case CandidateType.GetComponent:
                    {
                        var lookup =
                            _systemDescription.QueriesAndHandles.GetOrCreateComponentLookupField(typeArgument, true);
                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.Single();

                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return (
                            $"global::Unity.Entities.Internal.InternalCompilerInterface.GetComponentAfterCompletingDependency<{typeArgument.ToFullName()}>(ref __TypeHandle.{lookup}, ref {systemStateExpression}, ",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }
                    case CandidateType.GetComponentRO:
                    {
                        var lookup =
                            _systemDescription.QueriesAndHandles.GetOrCreateComponentLookupField(typeArgument, true);

                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.Single();

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return (
                            $"global::Unity.Entities.Internal.InternalCompilerInterface.GetComponentROAfterCompletingDependency<{typeArgument.ToFullName()}>(ref __TypeHandle.{lookup}, ref {systemStateExpression}, ",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }
                    case CandidateType.GetComponentRW:
                    {
                        var lookup =
                            _systemDescription.QueriesAndHandles
                                .GetOrCreateComponentLookupField(typeArgument, false);

                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.Single();

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return (
                            $"global::Unity.Entities.Internal.InternalCompilerInterface.GetComponentRWAfterCompletingDependency<{typeArgument.ToFullName()}>(ref __TypeHandle.{lookup}, ref {systemStateExpression}, ",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }
                    case CandidateType.SetComponent:
                    {
                        var args = invocationExpressionSyntax.ArgumentList.Arguments.ToArray();

                        if (args.Length != 2)
                            return default;

                        var (entityArg, componentArg) =
                            args[0].NameColon?.Name.Identifier.ValueText == "component"
                                ? (args[1], args[0])
                                : (args[0], args[1]);

                        typeArgument = typeArgument.TypeKind == TypeKind.TypeParameter
                            ? semanticModel.GetTypeInfo(componentArg.Expression).Type
                            : typeArgument;

                        var lookup =
                            _systemDescription.QueriesAndHandles
                                .GetOrCreateComponentLookupField(typeArgument, false);

                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return (
                            $"global::Unity.Entities.Internal.InternalCompilerInterface.SetComponentAfterCompletingDependency<{typeArgument.ToFullName()}>(ref __TypeHandle.{lookup}, ref {systemStateExpression}, ",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: componentArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: entityArg);
                    }
                    case CandidateType.HasComponent when isManagedApi:
                    {
                        var typeArg = candidateSyntax.Node.DescendantNodes().OfType<GenericNameSyntax>().Single()
                            .TypeArgumentList.Arguments.Single();
                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.Single();

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return _systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                            out var systemStateExpression)
                            ? ($"{systemStateExpression}.EntityManager.HasComponent<{typeArg}>(",
                                ReplacedWith: ReplacedWith.InvocationWithMissingSystemApiArguments,
                                ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                                ArgumentThatMightInvolveSystemApiInvocation2: default)
                            : default;
                    }
                    case CandidateType.HasComponent:
                    {
                        var lookup =
                            _systemDescription.QueriesAndHandles.GetOrCreateComponentLookupField(typeArgument, true);

                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.Single();

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return (
                            $"global::Unity.Entities.Internal.InternalCompilerInterface.HasComponentAfterCompletingDependency<{typeArgument.ToFullName()}>(ref __TypeHandle.{lookup}, ref {systemStateExpression}, ",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }
                    case CandidateType.IsComponentEnabled when isManagedApi:
                    {
                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        var typeArg = candidateSyntax.Node.DescendantNodes().OfType<GenericNameSyntax>().Single()
                            .TypeArgumentList.Arguments.Single();
                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.Single();

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return ($"{systemStateExpression}.EntityManager.IsComponentEnabled<{typeArg}>(",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }
                    case CandidateType.IsComponentEnabled:
                    {
                        var lookup =
                            _systemDescription.QueriesAndHandles.GetOrCreateComponentLookupField(typeArgument, true);
                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.Single();

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return
                            ($"global::Unity.Entities.Internal.InternalCompilerInterface.IsComponentEnabledAfterCompletingDependency<{typeArgument.ToFullName()}>(ref __TypeHandle.{lookup}, ref {systemStateExpression}, ",
                                ReplacedWith.InvocationWithMissingSystemApiArguments,
                                ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                                ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }
                    case CandidateType.SetComponentEnabled:
                    {
                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        var args = invocationExpressionSyntax.ArgumentList.Arguments.ToArray();

                        if (args.Length != 2)
                            return default;

                        var (entityArg, enabledArg) =
                            args[0].NameColon?.Name.Identifier.ValueText == "value"
                                ? (args[1], args[0])
                                : (args[0], args[1]);

                        if (isSystemApi)
                        {
                            var lookup =
                                _systemDescription.QueriesAndHandles.GetOrCreateComponentLookupField(typeArgument,
                                    false);

                            // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                            _numClosingBracketsForNestedSystemApiInvocations++;

                            return (
                                $"global::Unity.Entities.Internal.InternalCompilerInterface.SetComponentEnabledAfterCompletingDependency<{typeArgument.ToFullName()}>(ref __TypeHandle.{lookup}, ref {systemStateExpression}, ",
                                ReplacedWith.InvocationWithMissingSystemApiArguments,
                                ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                                ArgumentThatMightInvolveSystemApiInvocation2: enabledArg);
                        }

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        var typeArg = candidateSyntax.Node.DescendantNodes().OfType<GenericNameSyntax>().First()
                            .TypeArgumentList;
                        return ($"{systemStateExpression}.EntityManager.SetComponentEnabled{typeArg}(",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: enabledArg);
                    }

                    // Buffer
                    case CandidateType.GetBufferLookup:
                    {
                        var @readonly = false;
                        var args = invocationExpressionSyntax.ArgumentList.Arguments.ToArray();
                        if (args.Length == 0 || bool.TryParse(args[0].Expression.ToString(), out @readonly))
                        {
                            var bufferLookup =
                                _systemDescription.QueriesAndHandles.GetOrCreateBufferLookupField(typeArgument,
                                    @readonly);
                            if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                    out var systemStateExpression))
                                return default;

                            return
                                ($"global::Unity.Entities.Internal.InternalCompilerInterface.GetBufferLookup<{typeArgument.ToFullName()}>(ref __TypeHandle.{bufferLookup}, ref {systemStateExpression})",
                                    ReplacedWith.InvocationWithFullArgumentList,
                                    ArgumentThatMightInvolveSystemApiInvocation1: default,
                                    ArgumentThatMightInvolveSystemApiInvocation2: default);
                        }

                        var methodDeclarationSyntax = candidateSyntax.Node.AncestorOfKind<MethodDeclarationSyntax>();
                        if (methodDeclarationSyntax.Identifier.ValueText == "OnCreate")
                        {
                            var containingMethodSymbol = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax);
                            if (containingMethodSymbol.Parameters.Length == 0 ||
                                (containingMethodSymbol.Parameters.Length == 1 && containingMethodSymbol.Parameters[0]
                                    .Type.Is("Unity.Entities.SystemState")))
                            {
                                _systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                    out var systemStateExpression);
                                return (
                                    $"{systemStateExpression}.{CandidateSyntax.GetSimpleName(candidateSyntax.Node)}",
                                    ReplacedWith.InvocationWithMissingArgumentList,
                                    ArgumentThatMightInvolveSystemApiInvocation1: default,
                                    ArgumentThatMightInvolveSystemApiInvocation2: default);
                            }
                        }

                        SystemApiContextErrors.SGSA0002(_systemDescription, candidateSyntax);
                        break;
                    }
                    case CandidateType.GetBuffer:
                    {
                        var bufferLookup =
                            _systemDescription.QueriesAndHandles.GetOrCreateBufferLookupField(typeArgument, false);
                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.Single();
                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return (
                            $"global::Unity.Entities.Internal.InternalCompilerInterface.GetBufferAfterCompletingDependency<{typeArgument.ToFullName()}>(ref __TypeHandle.{bufferLookup}, ref {systemStateExpression}, ",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }
                    case CandidateType.HasBuffer:
                    {
                        var bufferLookup =
                            _systemDescription.QueriesAndHandles.GetOrCreateBufferLookupField(typeArgument, true);

                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.Single();

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return
                            ($"global::Unity.Entities.Internal.InternalCompilerInterface.HasBufferAfterCompletingDependency<{typeArgument.ToFullName()}>(ref __TypeHandle.{bufferLookup}, ref {systemStateExpression}, ",
                                ReplacedWith.InvocationWithMissingSystemApiArguments,
                                ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                                ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }
                    case CandidateType.IsBufferEnabled:
                    {
                        var bufferLookup =
                            _systemDescription.QueriesAndHandles.GetOrCreateBufferLookupField(typeArgument, true);
                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.Single();

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return (
                            $"global::Unity.Entities.Internal.InternalCompilerInterface.IsBufferEnabledAfterCompletingDependency<{typeArgument.ToFullName()}>(ref __TypeHandle.{bufferLookup}, ref {systemStateExpression}, ",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }
                    case CandidateType.SetBufferEnabled:
                    {
                        var bufferLookup =
                            _systemDescription.QueriesAndHandles.GetOrCreateBufferLookupField(typeArgument, false);
                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        var args = invocationExpressionSyntax.ArgumentList.Arguments.ToArray();

                        if (args.Length != 2)
                            return default;

                        var (entityArg, enabledArg) =
                            args[0].NameColon?.Name.Identifier.ValueText == "value"
                                ? (args[1], args[0])
                                : (args[0], args[1]);

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return (
                            $"global::Unity.Entities.Internal.InternalCompilerInterface.SetBufferEnabledAfterCompletingDependency<{typeArgument.ToFullName()}>(ref __TypeHandle.{bufferLookup}, ref {systemStateExpression}, ",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: enabledArg);
                    }

                    // Singleton
                    case CandidateType.SingletonWithArgument:
                    case CandidateType.SingletonWithoutArgument:
                    {
                        var queryFieldName = _systemDescription.QueriesAndHandles
                            .GetOrCreateQueryField(
                                new SingleArchetypeQueryFieldDescription(
                                    new Archetype(
                                        new[]
                                        {
                                            new Query
                                            {
                                                IsReadOnly =
                                                    (candidateSyntax.Flags & CandidateFlags.ReadOnly) ==
                                                    CandidateFlags.ReadOnly,
                                                Type = QueryType.All,
                                                TypeSymbol = typeArgument
                                            }
                                        },
                                        Array.Empty<Query>(),
                                        Array.Empty<Query>(),
                                        Array.Empty<Query>(),
                                        Array.Empty<Query>(),
                                        Array.Empty<Query>(),
                                        EntityQueryOptions.Default | EntityQueryOptions.IncludeSystems)
                                ));

                        var sn = CandidateSyntax.GetSimpleName(candidateSyntax.Node);
                        var noGenericGeneration = (candidateSyntax.Flags & CandidateFlags.NoGenericGeneration) ==
                                                  CandidateFlags.NoGenericGeneration;
                        var memberAccess =
                            noGenericGeneration
                                ? sn.Identifier.ValueText // e.g. GetSingletonEntity<T> -> query.GetSingletonEntity (with no generic)
                                : sn.ToString();

                        if (candidateSyntax.Type == CandidateType.SingletonWithArgument)
                        {
                            return ($"{queryFieldName}.{memberAccess}",
                                ReplacedWith.InvocationWithMissingArgumentList,
                                ArgumentThatMightInvolveSystemApiInvocation1: default,
                                ArgumentThatMightInvolveSystemApiInvocation2: default);
                        }
                        return ($"{queryFieldName}.{memberAccess}()",
                            ReplacedWith.InvocationWithFullArgumentList,
                            ArgumentThatMightInvolveSystemApiInvocation1: default,
                            ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }

                    // Aspect
                    case CandidateType.Aspect:
                    {
                        var @readonly = candidateSyntax.Flags == CandidateFlags.ReadOnly;

                        if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                out var systemStateExpression))
                            return default;

                        var entityArg = invocationExpressionSyntax.ArgumentList.Arguments.First();
                        var aspectLookup =
                            _systemDescription.QueriesAndHandles.GetOrCreateAspectLookup(typeArgument, @readonly);

                        var typeFullName = typeArgument.ToFullName();

                        // Because we are partially patching the node with an open parenthesis with no accompanying closing parenthesis, we need to increment `_numClosingBracketsForNestedSystemApiInvocations` by one.
                        _numClosingBracketsForNestedSystemApiInvocations++;

                        return (
                            $"global::Unity.Entities.Internal.InternalCompilerInterface.GetAspectAfterCompletingDependency<{typeFullName}.Lookup, {typeFullName}>(ref __TypeHandle.{aspectLookup}, ref {systemStateExpression}, {(@readonly ? "true" : "false")}, ",
                            ReplacedWith.InvocationWithMissingSystemApiArguments,
                            ArgumentThatMightInvolveSystemApiInvocation1: entityArg,
                            ArgumentThatMightInvolveSystemApiInvocation2: default);
                    }

                    // TypeHandle
                    case CandidateType.ComponentTypeHandle:
                    {
                        var @readonly = false;
                        var args = invocationExpressionSyntax.ArgumentList.Arguments.ToArray();
                        if (args.Length == 0 || bool.TryParse(args[0].Expression.ToString(), out @readonly))
                        {
                            var result =
                                _systemDescription.QueriesAndHandles.GetOrCreateTypeHandleField(typeArgument,
                                    @readonly, TypeHandleFieldDescription.TypeHandleSource.Component);
                            if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                    out var systemStateExpression))
                                return default;

                            return (
                                $"global::Unity.Entities.Internal.InternalCompilerInterface.GetComponentTypeHandle(ref __TypeHandle.{result}, ref {systemStateExpression})",
                                ReplacedWith.InvocationWithFullArgumentList,
                                ArgumentThatMightInvolveSystemApiInvocation1: default,
                                ArgumentThatMightInvolveSystemApiInvocation2: default);
                        }

                        var methodDeclarationSyntax = candidateSyntax.Node.AncestorOfKind<MethodDeclarationSyntax>();
                        if (methodDeclarationSyntax.Identifier.ValueText == "OnCreate")
                        {
                            var containingMethodSymbol = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax);
                            if (containingMethodSymbol.Parameters.Length == 0 ||
                                (containingMethodSymbol.Parameters.Length == 1 && containingMethodSymbol.Parameters[0]
                                    .Type.Is("Unity.Entities.SystemState")))
                            {
                                _systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                    out var systemStateExpression);
                                return (
                                    $"{systemStateExpression}.{CandidateSyntax.GetSimpleName(candidateSyntax.Node)}",
                                    ReplacedWith.InvocationWithMissingArgumentList,
                                    ArgumentThatMightInvolveSystemApiInvocation1: default,
                                    ArgumentThatMightInvolveSystemApiInvocation2: default);
                            }
                        }

                        SystemApiContextErrors.SGSA0002(_systemDescription, candidateSyntax);
                        break;
                    }
                    case CandidateType.BufferTypeHandle:
                    {
                        var @readonly = false;
                        var args = invocationExpressionSyntax.ArgumentList.Arguments.ToArray();
                        if (args.Length == 0 || bool.TryParse(args[0].Expression.ToString(), out @readonly))
                        {
                            var result =
                                _systemDescription.QueriesAndHandles.GetOrCreateTypeHandleField(typeArgument,
                                    @readonly, TypeHandleFieldDescription.TypeHandleSource.BufferElement);
                            if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                    out var systemStateExpression))
                                return default;

                            return (
                                $"global::Unity.Entities.Internal.InternalCompilerInterface.GetBufferTypeHandle(ref __TypeHandle.{result}, ref {systemStateExpression})",
                                ReplacedWith.InvocationWithFullArgumentList,
                                ArgumentThatMightInvolveSystemApiInvocation1: default,
                                ArgumentThatMightInvolveSystemApiInvocation2: default);
                        }

                        var methodDeclarationSyntax = candidateSyntax.Node.AncestorOfKind<MethodDeclarationSyntax>();
                        if (methodDeclarationSyntax.Identifier.ValueText == "OnCreate")
                        {
                            var containingMethodSymbol = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax);
                            if (containingMethodSymbol.Parameters.Length == 0 ||
                                (containingMethodSymbol.Parameters.Length == 1 && containingMethodSymbol.Parameters[0]
                                    .Type.Is("Unity.Entities.SystemState")))
                            {
                                _systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                    out var systemStateExpression);
                                return (
                                    $"{systemStateExpression}.{CandidateSyntax.GetSimpleName(candidateSyntax.Node)}",
                                    ReplacedWith.InvocationWithMissingArgumentList,
                                    ArgumentThatMightInvolveSystemApiInvocation1: default,
                                    ArgumentThatMightInvolveSystemApiInvocation2: default);
                            }
                        }

                        SystemApiContextErrors.SGSA0002(_systemDescription, candidateSyntax);
                        break;
                    }
                    case CandidateType.SharedComponentTypeHandle:
                    {
                        var @readonly = false;
                        var args = invocationExpressionSyntax.ArgumentList.Arguments.ToArray();
                        if (args.Length == 0 || bool.TryParse(args[0].Expression.ToString(), out @readonly))
                        {
                            var result =
                                _systemDescription.QueriesAndHandles.GetOrCreateTypeHandleField(typeArgument,
                                    @readonly, TypeHandleFieldDescription.TypeHandleSource.SharedComponent);
                            if (!_systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                    out var systemStateExpression))
                                return default;

                            return (
                                $"global::Unity.Entities.Internal.InternalCompilerInterface.GetSharedComponentTypeHandle(ref __TypeHandle.{result}, ref {systemStateExpression})",
                                ReplacedWith.InvocationWithFullArgumentList,
                                ArgumentThatMightInvolveSystemApiInvocation1: default,
                                ArgumentThatMightInvolveSystemApiInvocation2: default);
                        }

                        var methodDeclarationSyntax = candidateSyntax.Node.AncestorOfKind<MethodDeclarationSyntax>();
                        if (methodDeclarationSyntax.Identifier.ValueText == "OnCreate")
                        {
                            var containingMethodSymbol = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax);
                            if (containingMethodSymbol.Parameters.Length == 0 ||
                                (containingMethodSymbol.Parameters.Length == 1 && containingMethodSymbol.Parameters[0]
                                    .Type.Is("Unity.Entities.SystemState")))
                            {
                                _systemDescription.TryGetSystemStateParameterName(candidateSyntax,
                                    out var systemStateExpression);
                                return (
                                    $"{systemStateExpression}.{CandidateSyntax.GetSimpleName(candidateSyntax.Node)}",
                                    ReplacedWith.InvocationWithMissingArgumentList,
                                    ArgumentThatMightInvolveSystemApiInvocation1: default,
                                    ArgumentThatMightInvolveSystemApiInvocation2: default);
                            }
                        }

                        SystemApiContextErrors.SGSA0002(_systemDescription, candidateSyntax);
                        break;
                    }
                }

                // If using a generic that takes part of a method, then it should default to a `InternalCompilerInterface.DontUseThisGetSingleQuery<T>(this).` reference in SystemBase, and cause a compile error in ISystem.
                bool TryGetSystemBaseGeneric(out string replacement, out ReplacedWith replacedWith)
                {
                    replacement = invocationExpressionSyntax.Expression.ToString();
                    replacedWith = ReplacedWith.InvocationWithMissingArgumentList;

                    var usesUnknownTypeArgument = typeArgument is ITypeParameterSymbol;

                    var usingUnkownTypeArgumentIsValid = false;
                    var containingTypeTypeList =
                        _systemDescription.SystemTypeSyntax
                            .TypeParameterList; // Can support parents but better restrictive now.

                    if (containingTypeTypeList != null)
                    {
                        var validConstraints = containingTypeTypeList.Parameters;
                        foreach (var validConstraint in validConstraints)
                            usingUnkownTypeArgumentIsValid |= validConstraint.Identifier.ValueText == typeArgument.Name;
                    }

                    if (usesUnknownTypeArgument && !usingUnkownTypeArgumentIsValid)
                    {
                        if (_systemDescription.SystemType == SystemType.ISystem)
                            SystemApiContextErrors.SGSA0001(_systemDescription, candidateSyntax);

                        else if (isSystemApi && IsSingleton(candidateSyntax)) // Enabled you to use type parameters for SystemAPI singletons inside SystemBase
                        {
                            var sn = CandidateSyntax.GetSimpleName(candidateSyntax.Node);
                            var noGenericGeneration = (candidateSyntax.Flags & CandidateFlags.NoGenericGeneration) == CandidateFlags.NoGenericGeneration;
                            var memberAccessGeneric =
                                noGenericGeneration
                                    ? sn.Identifier.ValueText // e.g. GetSingletonEntity<T> -> query.GetSingletonEntity (with no generic)
                                    : sn.ToString();

                            replacement =
                                $"global::Unity.Entities.Internal.InternalCompilerInterface.OnlyAllowedInSourceGeneratedCodeGetSingleQuery<{typeArgument.ToFullName()}>(this).{memberAccessGeneric}{invocationExpressionSyntax.ArgumentList.ToFullString()}";
                            replacedWith = ReplacedWith.InvocationWithFullArgumentList;
                        }

                        return true;
                    }

                    return false;
                }

                break;
            }
        }

        return default;
    }
}
