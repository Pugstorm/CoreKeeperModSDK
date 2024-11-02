using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.Query;

public partial class IfeDescription
{
    struct QueryData
    {
        public string TypeSymbolFullName { get; set; }
        public ITypeSymbol TypeSymbol { get; set; }
        public ITypeSymbol TypeParameterSymbol { get; set; }
        public QueryType QueryType { get; set; }
        public bool IsReadOnly => QueryType is QueryType.RefRO or QueryType.EnabledRefRO or QueryType.ValueTypeComponent or QueryType.UnmanagedSharedComponent or QueryType.ManagedSharedComponent;
        public ITypeSymbol QueriedTypeSymbol => TypeParameterSymbol ?? TypeSymbol;
    }

    private bool TryGetQueryDatas()
    {
#pragma warning disable RS1024
        InitialIterableEnableableTypeSymbols = new HashSet<ITypeSymbol>();
#pragma warning restore RS1024

        InitialIterableEnableableQueryDatas = new List<QueryData>();
        IterableEnableableQueryDatasToBeTreatedAsAllComponents = new List<QueryData>();

        AllIterableQueryDatas = new List<QueryData>();

        foreach (var typeSyntax in QueryCandidate.QueryTypeNodes)
        {
            var typeSymbol = SystemDescription.SemanticModel.GetTypeInfo(typeSyntax).Type;
            var typeParameterSymbol = default(ITypeSymbol);

            var genericNameCandidate = typeSyntax;
            if (typeSyntax is QualifiedNameSyntax qualifiedNameSyntax) // This is the case when people type out their syntax Query<MyNameSpace.MyThing>
                genericNameCandidate = qualifiedNameSyntax.Right;
            if (genericNameCandidate is GenericNameSyntax genericNameSyntax)
            {
                var typeArg = genericNameSyntax.TypeArgumentList.Arguments.Single();
                typeParameterSymbol = SystemDescription.SemanticModel.GetTypeInfo(typeArg).Type;
            }

            var result = TryGetIfeQueryType(typeSymbol, typeSyntax.GetLocation());

            switch (result.QueryType)
            {
                case QueryType.Invalid:
                    return false;
                case QueryType.ValueTypeComponent:
                    IfeCompilerMessages.SGFE009(SystemDescription, typeSymbol.ToFullName(), Location);
                    break;
            }

            var queryData = new QueryData
            {
                TypeParameterSymbol = typeParameterSymbol,
                TypeSymbol = typeSymbol,
                TypeSymbolFullName = typeSymbol.ToFullName(),
                QueryType = result.QueryType,
            };
            if (result.IsTypeEnableable)
            {
                InitialIterableEnableableQueryDatas.Add(queryData);
                IterableEnableableQueryDatasToBeTreatedAsAllComponents.Add(queryData);

                InitialIterableEnableableTypeSymbols.Add(queryData.QueriedTypeSymbol);

                AllIterableQueryDatas.Add(queryData);
            }
            else
            {
                AllIterableQueryDatas.Add(queryData);

                _iterableNonEnableableTypes.Add(new Common.Query()
                {
                    IsReadOnly = queryData.IsReadOnly,
                    TypeSymbol = queryData.QueriedTypeSymbol,
                    Type = Common.QueryType.All
                });
            }
        }
        return true;

        // Example: `foreach (var result in SystemAPI.Query<MyResult>())`
        // `typeSymbol` refers to the symbol for `MyResult`
        (QueryType QueryType, bool IsTypeEnableable) TryGetIfeQueryType(ITypeSymbol typeSymbol, Location errorLocation)
        {
            // `MyResult` is an aspect
            if (typeSymbol.IsAspect())
                return (QueryType.Aspect, false);

            // `MyResult` is a shared component
            if (typeSymbol.IsSharedComponent())
                return (typeSymbol.IsUnmanagedType ? QueryType.UnmanagedSharedComponent : QueryType.ManagedSharedComponent, false);

            // `MyResult` implements `IComponentData`
            if (typeSymbol.IsComponent())
            {
                // `MyResult` is a struct
                if (typeSymbol.InheritsFromType("System.ValueType"))
                    return (typeSymbol.IsZeroSizedComponent() ? QueryType.TagComponent : QueryType.ValueTypeComponent, typeSymbol.IsEnableableComponent());
                // `MyResult` is a class
                return(QueryType.ManagedComponent, false);
            }

            bool isQueryTypeEnableable = false;

            switch (typeSymbol)
            {
                // `MyResult` is an error type.  This is usually caused by an ambiguous type.
                // Go ahead and mark the query as invalid and let roslyn report the other error.
                case IErrorTypeSymbol:
                    return (QueryType.Invalid, false);

                // `MyResult` is `T`
                case ITypeParameterSymbol:
                    IfeCompilerMessages.SGFE013(SystemDescription, errorLocation);
                    return (QueryType.Invalid, false);

                // `MyResult` is `RefRO<T>`, `RefRW<T>`, `EnabledRefRW<T>`, `EnabledRefRO<T>` or `DynamicBuffer<T>`
                case INamedTypeSymbol namedTypeSymbol:
                    // `typeArgument` refers to `T` in `RefRO<T>`, `RefRW<T>`, `EnabledRefRW<T>`, `EnabledRefRO<T>` or `DynamicBuffer<T>`
                    var typeArgument = namedTypeSymbol.TypeArguments[0];

                    switch (typeArgument)
                    {
                        // If `typeArgument` is generic, i.e. not a concrete type
                        case ITypeParameterSymbol:
                        {
                            IfeCompilerMessages.SGFE011(SystemDescription, errorLocation);
                            return (QueryType.Invalid, false);
                        }
                        // If `typeArgument` is a concrete type that expects a generic type, e.g. `MyBufferElement<T>`
                        case INamedTypeSymbol { Arity: > 0 } _namedTypeSymbol:
                        {
                            if (HasTypeParameter(_namedTypeSymbol))
                            {
                                IfeCompilerMessages.SGFE010(SystemDescription, errorLocation);
                                return (QueryType.Invalid, false);
                            }
                            // If `typeArgument` is a fully valid type
                            isQueryTypeEnableable = typeArgument.IsEnableableComponent();
                            break;
                        }
                        case INamedTypeSymbol { Arity: 0 }:
                            isQueryTypeEnableable = typeArgument.IsEnableableComponent();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Unable to parse {typeArgument.ToFullName()}.");
                    }
                    break;
            }
            return typeSymbol.Name switch
            {
                "DynamicBuffer" => (QueryType.DynamicBuffer, false),
                "RefRW" => (QueryType.RefRW, isQueryTypeEnableable),
                "RefRO" => (QueryType.RefRO, isQueryTypeEnableable),
                "EnabledRefRW" => (QueryType.EnabledRefRW, true),
                "EnabledRefRO" => (QueryType.EnabledRefRO, true),
                "UnityEngineComponent" => (QueryType.UnityEngineComponent, false),
                _ => throw new ArgumentOutOfRangeException()
            };

            static bool HasTypeParameter(INamedTypeSymbol typeArgument)
            {
                foreach (var typeArg in typeArgument.TypeArguments)
                    if (typeArg is ITypeParameterSymbol or INamedTypeSymbol { IsGenericType: true })
                        return true;

                return false;
            }
        }
    }
}
