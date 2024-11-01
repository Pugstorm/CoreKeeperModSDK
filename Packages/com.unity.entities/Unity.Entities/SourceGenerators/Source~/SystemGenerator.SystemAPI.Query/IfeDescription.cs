using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

using static Unity.Entities.SourceGen.Common.SourceGenHelpers;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.Query;

public partial class IfeDescription
{
    internal IfeType IfeType { get; }
    internal bool IsBurstEnabled { get; }
    internal Location Location { get; }
    internal SystemDescription SystemDescription { get; }

    internal IReadOnlyList<Common.Query> NoneQueryTypes => _noneQueryTypes;
    internal IReadOnlyList<Common.Query> AnyQueryTypes => _anyQueryTypes;
    internal IReadOnlyList<Common.Query> AllQueryTypes => _allQueryTypes;
    internal IReadOnlyList<Common.Query> DisabledQueryTypes => _disabledQueryTypes;
    internal IReadOnlyList<Common.Query> AbsentQueryTypes => _absentQueryTypes;
    internal IReadOnlyList<Common.Query> PresentQueryTypes => _presentQueryTypes;
    internal IReadOnlyList<Common.Query> ChangeFilterQueryTypes => _changeFilterQueryTypes;
    internal IReadOnlyList<Common.Query> IterableNonEnableableTypes => _iterableNonEnableableTypes;

    internal IReadOnlyList<SharedComponentFilterInfo> SharedComponentFilterInfos => _sharedComponentFilterInfos;

    internal HashSet<Common.Query> GetDistinctAllQueryTypes()
    {
        var distinctAll = new HashSet<Common.Query>();

        // Add all .WithAll<T> types
        foreach (var q in _allQueryTypes)
            distinctAll.Add(q);

        // Add all shared component filter types
        foreach (var q in SharedComponentFilterInfos)
            distinctAll.Add(new Common.Query
            {
                IsReadOnly = true,
                Type = Common.QueryType.All,
                TypeSymbol = q.TypeSymbol
            });

        // Add all SystemAPI.Query<T> types, where T is not enableable
        foreach (var q in IterableNonEnableableTypes)
            distinctAll.Add(q);

        // Add all SystemAPI.Query<T> types, where T is enableable *and* not used in `.WithAny<T>`, `.WithNone<T>` and `.WithDisabled<T>`
        foreach (var q in IterableEnableableQueryDatasToBeTreatedAsAllComponents)
        {
            distinctAll.Add(new Common.Query
            {
                IsReadOnly = q.IsReadOnly,
                Type = Common.QueryType.All,
                TypeSymbol = q.QueriedTypeSymbol
            });
        }

        return distinctAll;
    }

    internal EntityQueryOptions GetEntityQueryOptionsArgument()
    {
        if (!_entityQueryOptionsArguments.Any())
            return EntityQueryOptions.Default;

        var options = EntityQueryOptions.Default;

        var argumentExpression = _entityQueryOptionsArguments.First().Expression;

        while (argumentExpression is BinaryExpressionSyntax binaryExpressionSyntax)
        {
            if (TryParseQualifiedEnumValue(binaryExpressionSyntax.Right.ToString(), out EntityQueryOptions optionArg))
                options |= optionArg;

            argumentExpression = binaryExpressionSyntax.Left;
        }

        if (TryParseQualifiedEnumValue(argumentExpression.ToString(), out EntityQueryOptions option))
            options |= option;

        return options;
    }

    readonly List<Common.Query> _noneQueryTypes = new();
    readonly List<Common.Query> _anyQueryTypes = new();
    readonly List<Common.Query> _allQueryTypes = new();
    readonly List<Common.Query> _disabledQueryTypes = new();
    readonly List<Common.Query> _absentQueryTypes = new();
    readonly List<Common.Query> _presentQueryTypes = new();
    readonly List<Common.Query> _changeFilterQueryTypes = new();
    readonly List<Common.Query> _iterableNonEnableableTypes = new();
    readonly List<SharedComponentFilterInfo> _sharedComponentFilterInfos = new();
    readonly List<ArgumentSyntax> _entityQueryOptionsArguments = new();
    readonly bool _entityAccessRequired;
    readonly string _systemStateName;
    readonly string _generatedIfeTypeFullyQualifiedName;

    IList<QueryData> AllIterableQueryDatas { get; set; }
    HashSet<ITypeSymbol> InitialIterableEnableableTypeSymbols { get; set; }
    IList<QueryData> InitialIterableEnableableQueryDatas { get; set; }
    List<QueryData> IterableEnableableQueryDatasToBeTreatedAsAllComponents { get; set; }
    AttributeData BurstCompileAttribute { get; }

    public bool Success { get; internal set; } = true;

    public readonly QueryCandidate QueryCandidate;
    public readonly List<CandidateSyntax> AdditionalCandidates = new();
    public readonly Dictionary<SyntaxNode, string> CandidateNodesToReplacementCode;

    public string CreateQueryInvocationNodeReplacementCode(string entityQueryFieldName, string ifeTypeHandleFieldName) =>
        $"{_generatedIfeTypeFullyQualifiedName}.Query({entityQueryFieldName}, __TypeHandle.{ifeTypeHandleFieldName}, ref {_systemStateName}" +
        $"{string.Join("", SharedComponentFilterInfos.Select(p => $", {p.Argument}"))})";

    public IfeDescription(SystemDescription systemDescription, QueryCandidate queryCandidate, int numForEachsPreviouslySeenInSystem)
    {
        if (systemDescription.SemanticModel.GetOperation(queryCandidate.FullInvocationChainSyntaxNode) is IInvocationOperation invocationOperation
            && IsSystemApiQueryInvocation(invocationOperation))
        {
            QueryCandidate = queryCandidate;
            var queryCandidateContainingStatement = queryCandidate.FullInvocationChainSyntaxNode.AncestorOfKindOrDefault<StatementSyntax>();

            SystemDescription = systemDescription;
            Location = queryCandidate.FullInvocationChainSyntaxNode.GetLocation();

            if (!TryGetQueryDatas())
            {
                Success = false;
                return;
            }

            var containingMethod = queryCandidate.FullInvocationChainSyntaxNode.AncestorOfKindOrDefault<MethodDeclarationSyntax>();
            if (containingMethod != null)
            {
                var methodSymbol = SystemDescription.SemanticModel.GetDeclaredSymbol(containingMethod);

                var queryExtensionInvocations = queryCandidate.FullInvocationChainSyntaxNode.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();
                foreach (var node in queryExtensionInvocations)
                {
                    switch (node.Expression)
                    {
                        case MemberAccessExpressionSyntax { Name: GenericNameSyntax genericNameSyntax }:
                        {
                            var extensionMethodName = genericNameSyntax.Identifier.ValueText;
                            switch (extensionMethodName)
                            {
                                case "WithAny":
                                case "WithNone":
                                case "WithDisabled":
                                case "WithPresent":
                                    foreach (var typeArg in genericNameSyntax.TypeArgumentList.Arguments)
                                    {
                                        var typeArgSymbol = systemDescription.SemanticModel.GetTypeInfo(typeArg).Type;

                                        bool isReadOnly = true;

                                        // We support writing:
                                        // `SystemAPI.Query<EnabledRefRW<T>>().WithDisabled<T>()`,
                                        // `SystemAPI.Query<EnabledRefRW<T>>().WithAny<T>()`,
                                        // `SystemAPI.Query<EnabledRefRW<T>>().WithPresent<T>()`,
                                        // and `SystemAPI.Query<EnabledRefRW<T>>().WithNone<T>()`.
                                        // If the type passed to e.g. `.WithDisabled<T>()` is also something which the user wants to iterate through
                                        if (InitialIterableEnableableTypeSymbols.Contains(typeArgSymbol))
                                        {
                                            foreach (var iterableQueryData in InitialIterableEnableableQueryDatas)
                                            {
                                                if (SymbolEqualityComparer.Default.Equals(iterableQueryData.QueriedTypeSymbol, typeArgSymbol))
                                                {
                                                    // Figure out whether the user wants to iterate through it with readonly access.
                                                    // E.g. The user might write: `SystemAPI.Query<EnabledRefRW<T>, RefRO<T>>().WithDisabled<T>()`
                                                    // Even though`RefRO` requires read-only access, `EnabledRefRW` requires read-write access,
                                                    // which means that the query we create needs to request read-write access to T.
                                                    isReadOnly &= iterableQueryData.IsReadOnly;

                                                    // All types remaining in `IterableEnableableQueryDatasToBeTreatedAsAllComponents` will be treated as `All` components when creating an EntityQuery.
                                                    // Since the current type must be treated as `Any`, `None`, or `Disabled`, we must remove it from `IterableEnableableQueryDatasToBeTreatedAsAllComponents`.
                                                    IterableEnableableQueryDatasToBeTreatedAsAllComponents.RemoveAll(
                                                        q => SymbolEqualityComparer.Default.Equals(q.QueriedTypeSymbol, typeArgSymbol));
                                                }
                                            }
                                        }

                                        if (extensionMethodName == "WithAny")
                                            _anyQueryTypes.Add(new Common.Query
                                            {
                                                TypeSymbol = typeArgSymbol,
                                                Type = Common.QueryType.Any,
                                                IsReadOnly = isReadOnly
                                            });
                                        else if (extensionMethodName == "WithNone")
                                            _noneQueryTypes.Add(new Common.Query
                                            {
                                                TypeSymbol = typeArgSymbol,
                                                Type = Common.QueryType.None,
                                                IsReadOnly = isReadOnly
                                            });
                                        else if (extensionMethodName == "WithPresent")
                                            _presentQueryTypes.Add(new Common.Query
                                            {
                                                TypeSymbol = typeArgSymbol,
                                                Type = Common.QueryType.Present,
                                                IsReadOnly = isReadOnly
                                            });
                                        else
                                            _disabledQueryTypes.Add(new Common.Query
                                            {
                                                TypeSymbol = typeArgSymbol,
                                                Type = Common.QueryType.Disabled,
                                                IsReadOnly = isReadOnly
                                            });
                                    }
                                    break;
                                case "WithAbsent":
                                    foreach (var typeArg in genericNameSyntax.TypeArgumentList.Arguments)
                                    {
                                        var typeArgSymbol = systemDescription.SemanticModel.GetTypeInfo(typeArg).Type;

                                        // E.g. if users write `foreach (var x in SystemAPI.Query<EnabledRefRW<T>>().WithAbsent<T>())`
                                        if (InitialIterableEnableableTypeSymbols.Contains(typeArgSymbol))
                                            IfeCompilerMessages.SGFE012(SystemDescription, typeArgSymbol.ToFullName(), genericNameSyntax.GetLocation());
                                        else
                                            _absentQueryTypes.Add(new Common.Query
                                            {
                                                TypeSymbol = typeArgSymbol,
                                                Type = Common.QueryType.Absent,
                                                IsReadOnly = true
                                            });
                                    }
                                    break;
                                case "WithAll":
                                    foreach (var typeArg in genericNameSyntax.TypeArgumentList.Arguments)
                                    {
                                        var typeArgSymbol = systemDescription.SemanticModel.GetTypeInfo(typeArg).Type;

                                        // We support writing `SystemAPI.Query<EnabledRefRW<T>>().WithAll<T>()`.
                                        // If the type passed to e.g. `.WithAll<T>()` is also something which the user wants to iterate through, then we don't need to do anything --
                                        // we have already previously registered T.
                                        if (InitialIterableEnableableTypeSymbols.Contains(typeArgSymbol))
                                            continue;

                                        _allQueryTypes.Add(new Common.Query
                                        {
                                            TypeSymbol = typeArgSymbol,
                                            Type = Common.QueryType.All,
                                            IsReadOnly = true
                                        });
                                    }
                                    break;
                                case "WithChangeFilter":
                                    _changeFilterQueryTypes.AddRange(
                                        genericNameSyntax.TypeArgumentList.Arguments.Select(typeArg =>
                                            new Common.Query
                                            {
                                                TypeSymbol = systemDescription.SemanticModel.GetTypeInfo(typeArg).Type,
                                                Type = Common.QueryType.ChangeFilter,
                                                IsReadOnly = true
                                            }));
                                    break;
                                case "WithSharedComponentFilterManaged":
                                case "WithSharedComponentFilter":
                                    var typeArgs = genericNameSyntax.TypeArgumentList.Arguments;
                                    var args = node.ArgumentList.Arguments;
                                    for (var index = 0; index < typeArgs.Count; index++)
                                    {
                                        var typeArg = typeArgs[index];
                                        var typeSymbol = systemDescription.SemanticModel.GetTypeInfo(typeArg).Type;
                                        var arg = args[index];
                                        _sharedComponentFilterInfos.Add(
                                            new SharedComponentFilterInfo
                                            {
                                                Argument = arg,
                                                TypeSymbol = typeSymbol,
                                                IsManaged = genericNameSyntax.Identifier.ValueText == "WithSharedComponentFilterManaged"
                                            });
                                    }
                                    break;
                            }
                            break;
                        }
                        case MemberAccessExpressionSyntax { Name: IdentifierNameSyntax identifierNameSyntax }:
                        {
                            switch (identifierNameSyntax.Identifier.ValueText)
                            {
                                case "WithSharedComponentFilterManaged":
                                case "WithSharedComponentFilter":
                                    var args = node.ArgumentList.Arguments;
                                    foreach (var arg in args)
                                    {
                                        var typeSymbol = systemDescription.SemanticModel.GetTypeInfo(arg.Expression).Type;
                                        _sharedComponentFilterInfos.Add(
                                            new SharedComponentFilterInfo
                                            {
                                                Argument = arg,
                                                TypeSymbol = typeSymbol,
                                                IsManaged = identifierNameSyntax.Identifier.ValueText == "WithSharedComponentFilterManaged"
                                            });
                                    }
                                    break;
                                case "WithOptions":
                                    _entityQueryOptionsArguments.Add(node.ArgumentList.Arguments.Single());
                                    break;
                                case "WithEntityAccess":
                                    _entityAccessRequired = true;
                                    break;
                            }
                            break;
                        }
                    }
                }

                bool isQueryInForEach = queryCandidateContainingStatement is CommonForEachStatementSyntax;
                if (!isQueryInForEach)
                {
                    IfeCompilerMessages.SGFE001(SystemDescription, Location);
                    Success = false;
                    return;
                }

                if (_changeFilterQueryTypes.Count > 2)
                    IfeCompilerMessages.SGFE003(SystemDescription, _changeFilterQueryTypes.Count, Location);
                if (SharedComponentFilterInfos.Count > 2)
                    IfeCompilerMessages.SGFE007(SystemDescription, SharedComponentFilterInfos.Count, Location);
                if (_entityQueryOptionsArguments.Count > 1)
                    IfeCompilerMessages.SGFE008(SystemDescription, _entityQueryOptionsArguments.Count, Location);

                if (SystemDescription.TryGetSystemStateParameterName(QueryCandidate, out var systemStateExpression))
                    _systemStateName = systemStateExpression.ToFullString();
                else
                {
                    Success = false;
                    return;
                }

                var burstCompileAttribute = methodSymbol.GetAttributes().SingleOrDefault(a => a.AttributeClass.ToFullName() == "Unity.Burst.BurstCompileAttribute");

                IsBurstEnabled = burstCompileAttribute != null;
                BurstCompileAttribute = burstCompileAttribute;
                string ifeTypeName = $"IFE_{systemDescription.QueriesAndHandles.UniqueId}_{numForEachsPreviouslySeenInSystem}";

                var ifeType = new IfeType
                {
                    PerformsCollectionChecks = SystemDescription.PreprocessorInfo.IsUnityCollectionChecksEnabled,
                    MustReturnEntityDuringIteration = _entityAccessRequired,
                    TypeName = ifeTypeName,
                    BurstCompileAttribute = BurstCompileAttribute,
                    FullyQualifiedTypeName = GetIfeTypeFullyQualifiedTypeName(queryCandidate, ifeTypeName),
                    ReturnedTupleElementsDuringEnumeration =
                        AllIterableQueryDatas
                            .Select((queryData, index) =>
                            {
                                string typeArgumentFullName;
                                switch (queryData.QueryType)
                                {
                                    case QueryType.RefRO:
                                        typeArgumentFullName = queryData.TypeParameterSymbol.ToFullName();
                                        return new ReturnedTupleElementDuringEnumeration(
                                            $"Unity.Entities.Internal.InternalCompilerInterface.UncheckedRefRO<{typeArgumentFullName}>",
                                            typeArgumentFullName: typeArgumentFullName,
                                            elementName: $"item{index + 1}",
                                            type: queryData.QueryType);
                                    case QueryType.RefRW:
                                        typeArgumentFullName = queryData.TypeParameterSymbol.ToFullName();
                                        return new ReturnedTupleElementDuringEnumeration(
                                            $"Unity.Entities.Internal.InternalCompilerInterface.UncheckedRefRW<{typeArgumentFullName}>",
                                            typeArgumentFullName: typeArgumentFullName,
                                            elementName: $"item{index + 1}",
                                            type: queryData.QueryType);
                                    default:
                                        typeArgumentFullName = queryData.TypeParameterSymbol is { } symbol ? symbol.ToFullName() : string.Empty;
                                        return new ReturnedTupleElementDuringEnumeration(
                                            queryData.TypeSymbolFullName,
                                            typeArgumentFullName: typeArgumentFullName,
                                            elementName: $"item{index + 1}",
                                            type: queryData.QueryType);
                                }
                            })
                            .ToArray()
                };

                IfeType = ifeType;
                _generatedIfeTypeFullyQualifiedName = ifeType.FullyQualifiedTypeName;
                CandidateNodesToReplacementCode = new Dictionary<SyntaxNode, string>();

                switch (queryCandidateContainingStatement)
                {
                    case ForEachVariableStatementSyntax { Variable: TupleExpressionSyntax tupleExpressionSyntax }:
                    {
                        // Original code: foreach ((RefRW<TypeA> a, RefRO<TypeB> b) in SystemAPI.Query<RefRW<TypeA>, RefRO<TypeB>>())
                        // Patched code: foreach ((UncheckedRefRW<TypeA> a, UncheckedRefRO<TypeB> b) in generatedIfeEnumerator)
                        for (var index = 0; index < tupleExpressionSyntax.Arguments.Count; index++)
                        {
                            if (tupleExpressionSyntax.Arguments[index].Expression is DeclarationExpressionSyntax declarationExpressionSyntax)
                            {
                                var typeSyntax = declarationExpressionSyntax.Type;
                                if (typeSyntax is GenericNameSyntax genericNameSyntax && genericNameSyntax.Identifier.ValueText != "var")
                                {
                                    var queryData = AllIterableQueryDatas.ElementAt(index);
                                    var typeParamSymbolFullName = queryData.TypeParameterSymbol.ToFullName();

                                    switch (queryData.QueryType)
                                    {
                                        case QueryType.RefRO:
                                            CandidateNodesToReplacementCode.Add(genericNameSyntax, $"Unity.Entities.Internal.InternalCompilerInterface.UncheckedRefRO<{typeParamSymbolFullName}> ");
                                            AdditionalCandidates.Add(new CandidateSyntax(CandidateType.Ife, CandidateFlags.None, genericNameSyntax));
                                            break;
                                        case QueryType.RefRW:
                                            CandidateNodesToReplacementCode.Add(genericNameSyntax, $"Unity.Entities.Internal.InternalCompilerInterface.UncheckedRefRW<{typeParamSymbolFullName}> ");
                                            AdditionalCandidates.Add(new CandidateSyntax(CandidateType.Ife, CandidateFlags.None, genericNameSyntax));
                                            break;
                                    }
                                }
                            }
                        }
                        break;
                    }
                    case ForEachStatementSyntax forEachStatementSyntax:
                    {
                        switch (forEachStatementSyntax.Type)
                        {
                            // Original code: foreach ((RefRW<TypeA>, RefRO<TypeB>) result in SystemAPI.Query<RefRW<TypeA>, RefRO<TypeB>>())
                            // Patched code: foreach ((UncheckedRefRW<TypeA>, UncheckedRefRO<TypeB>) result in generatedIfeEnumerator)
                            case TupleTypeSyntax tupleTypeSyntax:
                            {
                                for (int i = 0; i < AllIterableQueryDatas.Count; i++)
                                {
                                    var queryData = AllIterableQueryDatas.ElementAt(i);
                                    var typeSyntax = tupleTypeSyntax.Elements[i].Type;

                                    if (typeSyntax is GenericNameSyntax genericNameSyntax)
                                    {
                                        var typeParamSymbolFullName = queryData.TypeParameterSymbol.ToFullName();
                                        switch (queryData.QueryType)
                                        {
                                            case QueryType.RefRW:
                                                CandidateNodesToReplacementCode.Add(genericNameSyntax, $"Unity.Entities.Internal.InternalCompilerInterface.UncheckedRefRW<{typeParamSymbolFullName}> ");
                                                AdditionalCandidates.Add(new CandidateSyntax(CandidateType.Ife, CandidateFlags.None, genericNameSyntax));
                                                break;
                                            case QueryType.RefRO:
                                                CandidateNodesToReplacementCode.Add(genericNameSyntax, $"Unity.Entities.Internal.InternalCompilerInterface.UncheckedRefRO<{typeParamSymbolFullName}> ");
                                                AdditionalCandidates.Add(new CandidateSyntax(CandidateType.Ife, CandidateFlags.None, genericNameSyntax));
                                                break;
                                        }
                                    }
                                }
                                break;
                            }
                            case GenericNameSyntax genericNameSyntax:
                            {
                                var queryData = AllIterableQueryDatas.Single();
                                var queryType = queryData.QueryType;

                                // Original code: foreach (RefRW<TypeA> result in SystemAPI.Query<RefRW<TypeA>>())
                                // Patched code: foreach (UncheckedRefRW<TypeA> result in generatedIfeEnumerator)
                                if (genericNameSyntax.Identifier.ValueText != "var")
                                {
                                    var typeParamSymbolFullName = queryData.TypeParameterSymbol.ToFullName();
                                    switch (queryType)
                                    {
                                        case QueryType.RefRO:
                                            CandidateNodesToReplacementCode.Add(genericNameSyntax, $"Unity.Entities.Internal.InternalCompilerInterface.UncheckedRefRO<{typeParamSymbolFullName}> ");
                                            AdditionalCandidates.Add(new CandidateSyntax(CandidateType.Ife, CandidateFlags.None, genericNameSyntax));
                                            break;
                                        case QueryType.RefRW:
                                            CandidateNodesToReplacementCode.Add(genericNameSyntax, $"Unity.Entities.Internal.InternalCompilerInterface.UncheckedRefRW<{typeParamSymbolFullName}> ");
                                            AdditionalCandidates.Add(new CandidateSyntax(CandidateType.Ife, CandidateFlags.None, genericNameSyntax));
                                            break;
                                    }
                                }
                                break;
                            }
                        }
                        break;
                    }
                }
            }
            else
            {
                var propertyDeclarationSyntax = queryCandidate.FullInvocationChainSyntaxNode.AncestorOfKind<PropertyDeclarationSyntax>();
                var propertySymbol = ModelExtensions.GetDeclaredSymbol(systemDescription.SemanticModel, propertyDeclarationSyntax);

                IfeCompilerMessages.SGFE002(
                    systemDescription,
                    propertySymbol.OriginalDefinition.ToString(),
                    queryCandidate.FullInvocationChainSyntaxNode.GetLocation());
                Success = false;
            }
        }
        else
            Success = false;

        static bool IsSystemApiQueryInvocation(IInvocationOperation operation)
        {
            var constructedFrom = operation.TargetMethod.ConstructedFrom.ToString();
            if (constructedFrom.StartsWith("Unity.Entities.QueryEnumerable<"))
                return true;
            if (constructedFrom.StartsWith("Unity.Entities.QueryEnumerableWithEntity<"))
                return true;
            return constructedFrom.StartsWith("Unity.Entities.SystemAPI.Query<");
        }

        static string GetIdentifier(MemberDeclarationSyntax memberDeclarationSyntax)
        {
            switch (memberDeclarationSyntax)
            {
                case ClassDeclarationSyntax classDeclarationSyntax:
                    return classDeclarationSyntax.Identifier.ValueText;
                case StructDeclarationSyntax structDeclarationSyntax:
                    return structDeclarationSyntax.Identifier.ValueText;
                case NamespaceDeclarationSyntax namespaceDeclarationSyntax:
                    var identifierName = namespaceDeclarationSyntax.ChildNodes().OfType<IdentifierNameSyntax>().FirstOrDefault();
                    return
                        identifierName != null
                            ? identifierName.Identifier.ValueText
                            : namespaceDeclarationSyntax.ChildNodes().OfType<QualifiedNameSyntax>().First().ToString();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        static string GetIfeTypeFullyQualifiedTypeName(QueryCandidate queryCandidate, string unqualifiedTypeName)
        {
            var stack = new Stack<string>();

            foreach (var parentClassOrNamespace in GetContainingTypesAndNamespacesFromMostToLeastNested(queryCandidate.FullInvocationChainSyntaxNode))
            {
                var identifier = GetIdentifier(parentClassOrNamespace);
                stack.Push(identifier);
            }

            if (stack.Count == 0)
                return unqualifiedTypeName;

            using var fullyQualifiedNameWriter = new StringWriter();

            while (stack.Count > 0)
            {
                var name = stack.Pop();
                fullyQualifiedNameWriter.Write(name);
                fullyQualifiedNameWriter.Write(".");
            }
            fullyQualifiedNameWriter.Write(unqualifiedTypeName);
            return fullyQualifiedNameWriter.ToString();

            IEnumerable<MemberDeclarationSyntax> GetContainingTypesAndNamespacesFromMostToLeastNested(SyntaxNode syntaxNode)
            {
                var current = syntaxNode;
                while (current.Parent is NamespaceDeclarationSyntax or ClassDeclarationSyntax or StructDeclarationSyntax)
                {
                    yield return current.Parent as MemberDeclarationSyntax;
                    current = current.Parent;
                }
            }
        }
    }
}
