using UnityEditor.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Properties;
using Unity.Entities.UI;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Full implementation of the query engine. This relies on the QuickSearch package.
    /// </summary>
    class QuickSearchBackend<TData> : SearchBackend<TData>
    {
        class SearchQuery : ISearchQuery<TData>
        {
            readonly ParsedQuery<TData> m_Query;

            public string SearchString { get; }

            public ICollection<string> Tokens => m_Query?.tokens ?? Array.Empty<string>();

            public SearchQuery(string searchString, ParsedQuery<TData> query)
            {
                SearchString = searchString;
                m_Query = query;
            }

            public IEnumerable<TData> Apply(IEnumerable<TData> data)
            {
                return m_Query?.Apply(data) ?? data;
            }
        }

        class FilterVisitor : IPropertyBagVisitor, IPropertyVisitor
        {
            class CollectionFilterVisitor : ICollectionPropertyBagVisitor
            {
                public PropertyPath Path;
                public QueryEngine<TData> QueryEngine;
                public string Token;

                void ICollectionPropertyBagVisitor.Visit<TCollection, TElement>(ICollectionPropertyBag<TCollection, TElement> properties, ref TCollection container)
                {
                    var path = Path;

                    // This query engine filter is provided with two delegates:
                    //
                    // parameterTransformer: This function is responsible for transforming the input string into a strong generic type.
                    //                       this is done via the `TypeConversion` API in properties.
                    //
                    // filterResolver:       This function takes the data for each element being filtered, along with the original token, and the
                    //                       transformed input. The function then applies the filter and returns true or false. In this particular case
                    //                       we want to test equality with each collection element and return true if ANY match.
                    //
                    QueryEngine.AddFilter<TElement, TElement>(Token, (data, _, token, transformedInput) =>
                    {
                        if (!PropertyContainer.TryGetValue<TData, TCollection>(ref data, path, out var collection))
                            return false;

                        if (TypeTraits<TCollection>.CanBeNull && null == collection)
                            return false;

                        return collection.Any(e => FilterOperator.ApplyOperator(token, e, transformedInput, QueryEngine.globalStringComparison));
                    }, s => TypeConversion.Convert<string, TElement>(ref s), FilterOperator.GetSupportedOperators<TElement>());
                }
            }

            static readonly MethodInfo s_AddEnumerableFilterMethod = typeof(FilterVisitor).GetMethod(nameof(AddEnumerableFilter), BindingFlags.NonPublic | BindingFlags.Static);

            readonly CollectionFilterVisitor m_CollectionFilterVisitor = new CollectionFilterVisitor();

            public int PathIndex;
            public PropertyPath Path;
            public VisitReturnCode ReturnCode;
            public QueryEngine<TData> QueryEngine;
            public string Token;
            public SearchFilterOptions Options;

            void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> properties, ref TContainer container)
            {
                var part = Path[PathIndex++];

                IProperty<TContainer> property;

                switch (part.Kind)
                {
                    case PropertyPathPartKind.Name:
                    {
                        if (properties is INamedProperties<TContainer> keyable && keyable.TryGetProperty(ref container, part.Name, out property))
                            property.Accept(this, ref container);
                        else
                            ReturnCode = VisitReturnCode.InvalidPath;
                    }
                    break;

                    case PropertyPathPartKind.Index:
                    {
                        if (properties is IIndexedProperties<TContainer> indexable && indexable.TryGetProperty(ref container, part.Index, out property))
                            property.Accept(this, ref container);
                        else
                            ReturnCode = VisitReturnCode.InvalidPath;
                    }
                        break;

                    case PropertyPathPartKind.Key:
                    {
                        if (properties is IKeyedProperties<TContainer, object> keyable && keyable.TryGetProperty(ref container, part.Key, out property))
                            property.Accept(this, ref container);
                        else
                            ReturnCode = VisitReturnCode.InvalidPath;
                    }
                        break;

                    default:
                        ReturnCode = VisitReturnCode.InvalidPath;
                        break;
                }
            }

            void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
            {
                var value = default(TValue);

                if (PathIndex >= Path.Length)
                {
                    if (PropertyBag.GetPropertyBag<TValue>() is ICollectionPropertyBagAccept<TValue> collectionPropertyBagAccept)
                    {
                        m_CollectionFilterVisitor.Path = Path;
                        m_CollectionFilterVisitor.QueryEngine = QueryEngine;
                        m_CollectionFilterVisitor.Token = Token;

                        // The final result of the path has resolved to an array type.
                        // In this case we assume the user wants to filter based on any element of the collection.
                        // But first we need to resolve the generic element type. We use a property visitor for this.
                        collectionPropertyBagAccept.Accept(m_CollectionFilterVisitor, ref value);
                        return;
                    }

                    // Explicit support for `IEnumerable<T>`
                    if (typeof(TValue).IsGenericType && typeof(TValue).GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        var elementType = typeof(TValue).GetGenericArguments()[0];
                        var genericMethod = s_AddEnumerableFilterMethod.MakeGenericMethod(elementType);

                        genericMethod.Invoke(this, new object[]
                        {
                            QueryEngine,
                            Token,
                            Path
                        });

                        return;
                    }

                    var path = Path;
                    var token = Token;
                    var supportedOperatorTypes = Options.SupportedOperatorTypes;

                    if (Options.StringComparison.HasValue)
                    {
                        QueryEngine.AddFilter(token, data => PropertyContainer.TryGetValue<TData, TValue>(ref data, path, out var v) ? v : default, Options.StringComparison.Value, supportedOperatorTypes);
                    }
                    else
                    {
                        QueryEngine.AddFilter(token, data => PropertyContainer.TryGetValue<TData, TValue>(ref data, path, out var v) ? v : default, supportedOperatorTypes);
                    }

                    return;
                }

                if (TypeTraits<TValue>.IsAbstractOrInterface || typeof(TValue) == typeof(object))
                {
                    throw new InvalidOperationException($"Failed to register filter with Token=[{Token}] at Path=[{Path}]. Unable to bind to polymorphic fields.");
                }

                var propertyBag = PropertyBag.GetPropertyBag<TValue>();

                if (null == propertyBag)
                {
                    ReturnCode = VisitReturnCode.InvalidPath;
                    return;
                }

                propertyBag.Accept(this, ref value);
            }

            static void AddEnumerableFilter<TElement>(QueryEngine<TData> queryEngine, string token, PropertyPath path)
            {
                var p = path;

                queryEngine.AddFilter<TElement, TElement>(token, (data, _, t, transformedInput) =>
                {
                    if (!PropertyContainer.TryGetValue<TData, IEnumerable<TElement>>(ref data, p, out var collection))
                        return false;

                    return null != collection && collection.Any(e => FilterOperator.ApplyOperator(t, e, transformedInput, queryEngine.globalStringComparison));
                }, s => TypeConversion.Convert<string, TElement>(ref s), FilterOperator.GetSupportedOperators<TElement>());
            }
        }

        readonly QueryEngine<TData> m_QueryEngine = new QueryEngine<TData>();
        readonly FilterVisitor m_FilterVisitor = new FilterVisitor();

        /// <summary>
        /// Returns the underlying <see cref="UnityEditor.Search.QueryEngine"/> object.
        /// </summary>
        /// <returns></returns>
        public QueryEngine<TData> QueryEngine => m_QueryEngine;

        public QuickSearchBackend()
        {
            m_QueryEngine.validateFilters = false;
            m_QueryEngine.skipUnknownFilters = false;
            m_QueryEngine.SetSearchDataCallback(GetSearchData);
        }

        public override ISearchQuery<TData> Parse(string text)
        {
            m_QueryEngine.SetGlobalStringComparisonOptions(GlobalStringComparison);
            return new SearchQuery(text, !string.IsNullOrWhiteSpace(text)
                ? m_QueryEngine.ParseQuery(text)
                : null);
        }

        public override void AddSearchFilterProperty(string token, PropertyPath path, SearchFilterOptions options)
        {
            m_FilterVisitor.PathIndex = 0;
            m_FilterVisitor.Path = path;
            m_FilterVisitor.ReturnCode = VisitReturnCode.Ok;
            m_FilterVisitor.QueryEngine = m_QueryEngine;
            m_FilterVisitor.Token = token;
            m_FilterVisitor.Options = options;

            var container = default(TData);

            var properties = PropertyBag.GetPropertyBag<TData>();

            if (null == properties)
            {
                throw new MissingPropertyBagException(typeof(TData));
            }

            properties.Accept(m_FilterVisitor, ref container);

            switch (m_FilterVisitor.ReturnCode)
            {
                case VisitReturnCode.Ok:
                    break;
                case VisitReturnCode.InvalidPath:
                    throw new InvalidPathException($"SearchElement Failed to AddSearchFilter for Type=[{typeof(TData)}] Token=[{token}] could not resolve Path=[{path}]");
                default:
                    throw new InvalidBindingException($"SearchElement Failed to AddSearchFilter for Type=[{typeof(TData)}] Token=[{token}] Path=[{path}] VisitErrorCode=[{m_FilterVisitor.ReturnCode}]");
            }
        }

        public override void AddSearchFilterCallback<TFilter>(string token, Func<TData, TFilter> getFilterDataFunc, SearchFilterOptions options)
        {
            if (options.StringComparison.HasValue)
                m_QueryEngine.AddFilter(token, getFilterDataFunc, options.StringComparison.Value, options.SupportedOperatorTypes);
            else
                m_QueryEngine.AddFilter(token, getFilterDataFunc, options.SupportedOperatorTypes);
        }

        public override void AddSearchOperatorHandler<TFilterVariable, TFilterConstant>(string op, Func<TFilterVariable, TFilterConstant, bool> handler)
        {
            m_QueryEngine.AddOperatorHandler(op, handler);
        }

        public override void AddSearchOperatorHandler<TFilterVariable, TFilterConstant>(string op, Func<TFilterVariable, TFilterConstant, StringComparison, bool> handler)
        {
            m_QueryEngine.AddOperatorHandler(op, handler);
        }
    }
}
