using UnityEditor.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Stores a property path which should be used as search data.
    /// </summary>
    readonly struct SearchDataProperty
    {
        public readonly PropertyPath Path;

        public SearchDataProperty(PropertyPath path)
        {
            Path = path;
        }
    }

    interface ISearchDataCallback
    {
        void Register(ISearchBackend backend);
    }

    class SearchDataCallback<TData> : ISearchDataCallback
    {
        public Func<TData, IEnumerable<string>> GetSearchDataFunc { get; set; }

        public void Register(ISearchBackend backend)
        {
            if (backend is ISearchBackend<TData> typed)
                typed.AddSearchDataCallback(GetSearchDataFunc);
        }
    }

    interface ISearchFilterCallback
    {
        void Register(ISearchBackend backend);
    }

    class SearchFilterCallback<TData, TFilter> : ISearchFilterCallback
    {
        public string Token { get; set; }
        public Func<TData, TFilter> GetSearchFilterFunc { get; set; }
        public SearchFilterOptions Options { get; set; }

        public void Register(ISearchBackend backend)
        {
            if (backend is ISearchBackend<TData> typed)
                typed.AddSearchFilterCallback(Token, GetSearchFilterFunc, Options);
        }
    }

    /// <summary>
    /// Stores a token to property path which should be used for filtering.
    /// </summary>
    readonly struct SearchFilterProperty
    {
        public readonly string Token;
        public readonly PropertyPath Path;
        public readonly SearchFilterOptions Options;

        public SearchFilterProperty(string token, PropertyPath path, SearchFilterOptions options)
        {
            Token = token;
            Path = path;
            Options = options;
        }
    }

    interface ISearchOperatorHandler
    {
        void Register(ISearchBackend backend);
    }

    class SearchOperatorHandler<TFilterVariable, TFilterConstant> : ISearchOperatorHandler
    {
        public string Operator;
        public Func<TFilterVariable, TFilterConstant, bool> Handler;
        public Func<TFilterVariable, TFilterConstant, StringComparison, bool> HandlerWithStringComparison;

        public void Register(ISearchBackend backend)
        {
            if (null != Handler) backend.AddSearchOperatorHandler(Operator, Handler);
            if (null != HandlerWithStringComparison) backend.AddSearchOperatorHandler(Operator, HandlerWithStringComparison);
        }
    }

    class SearchEngine
    {
        StringComparison m_GlobalStringComparison = SearchElement.DefaultGlobalStringComparison;

        readonly Dictionary<Type, ISearchBackend> m_SearchBackends = new Dictionary<Type, ISearchBackend>();

        List<SearchFilterProperty> SearchFilterProperties { get; } = new List<SearchFilterProperty>();
        List<SearchDataProperty> SearchDataProperties { get; } = new List<SearchDataProperty>();
        List<ISearchDataCallback> SearchDataCallbacks { get; } = new List<ISearchDataCallback>();
        List<ISearchFilterCallback> SearchFilterCallbacks { get; } = new List<ISearchFilterCallback>();
        List<ISearchOperatorHandler> SearchOperatorHandlers { get; } = new List<ISearchOperatorHandler>();

        public List<string> SearchFilterTokens { get; } = new List<string>();

        /// <summary>
        /// Global string comparison options for word matching and filter handling (if not overridden).
        /// </summary>
        public StringComparison GlobalStringComparison
        {
            get
            {
                return m_GlobalStringComparison;
            }
            set
            {
                m_GlobalStringComparison = value;

                foreach (var backend in m_SearchBackends.Values)
                {
                    backend.GlobalStringComparison = value;
                }
            }
        }

        /// <summary>
        /// Clears the internal state of the <see cref="SearchEngine"/>.
        /// </summary>
        public void Clear()
        {
            SearchDataProperties.Clear();
            SearchFilterProperties.Clear();
            m_SearchBackends.Clear();
        }

        /// <summary>
        /// Adds a binding path to the search data. The property at the specified <paramref name="path"/> will be compared to the non-tokenized portion of the search string.
        /// </summary>
        /// <remarks>
        /// The search data should generally include things like id and/or name.
        /// </remarks>
        /// <param name="path">The property path to pull search data from.</param>
        public void AddSearchDataProperty(PropertyPath path)
        {
            SearchDataProperties.Add(new SearchDataProperty(path));

            foreach (var backend in m_SearchBackends.Values)
                backend.AddSearchDataProperty(path);
        }

        public void AddSearchDataCallback<TData>(Func<TData, IEnumerable<string>> getSearchDataFunc)
        {
            SearchDataCallbacks.Add(new SearchDataCallback<TData>
            {
                GetSearchDataFunc = getSearchDataFunc
            });

            foreach (var backend in m_SearchBackends.Values.OfType<ISearchBackend<TData>>())
                backend.AddSearchDataCallback(getSearchDataFunc);
        }

        /// <summary>
        /// Adds a filter based on a binding path. The given token will resolve to a property at the specified <paramref name="path"/>.
        /// </summary>
        /// <param name="token">The identifier of the filter. Typically what precedes the operator in a filter.</param>
        /// <param name="path">The property this token should resolve to.</param>
        /// <param name="options">The set of filter options.</param>
        public void AddSearchFilterProperty(string token, PropertyPath path, SearchFilterOptions options)
        {
            SearchFilterTokens.Add(token);
            SearchFilterProperties.Add(new SearchFilterProperty(token, path, options));

            foreach (var backend in m_SearchBackends.Values)
                backend.AddSearchFilterProperty(token, path, options);
        }

        /// <summary>
        /// Adds a search filter based on a callback function. The given token will resolve to the result of the specified <paramref name="getSearchFilterFunc"/>.
        /// </summary>
        /// <param name="token">The identifier of the filter. Typically what precedes the operator in a filter.</param>
        /// <param name="getSearchFilterFunc">Callback used to get the object that is used in the filter. Takes an object of type TData and returns an object of type TFilter.</param>
        /// <param name="options">The set of filter options.</param>
        /// <typeparam name="TData">The data type being searched.</typeparam>
        /// <typeparam name="TFilter">The return type for the filter.</typeparam>
        public void AddSearchFilterCallback<TData, TFilter>(string token, Func<TData, TFilter> getSearchFilterFunc, SearchFilterOptions options)
        {
            SearchFilterTokens.Add(token);
            SearchFilterCallbacks.Add(new SearchFilterCallback<TData, TFilter>
            {
                Token = token,
                GetSearchFilterFunc = getSearchFilterFunc,
                Options = options
            });

            foreach (var backend in m_SearchBackends.Values.OfType<ISearchBackend<TData>>())
                backend.AddSearchFilterCallback(token, getSearchFilterFunc, options);
        }

        /// <summary>
        /// Add a custom filter operator handler.
        /// </summary>
        /// <typeparam name="TFilterVariable">The operator's left hand side type. This is the type returned by a filter handler.</typeparam>
        /// <typeparam name="TFilterConstant">The operator's right hand side type.</typeparam>
        /// <param name="op">The filter operator.</param>
        /// <param name="handler">Callback to handle the operation. Takes a TFilterVariable (value returned by the filter handler, will vary for each element) and a TFilterConstant (right hand side value of the operator, which is constant), and returns a boolean indicating if the filter passes or not.</param>
        public void AddSearchOperatorHandler<TFilterVariable, TFilterConstant>(string op, Func<TFilterVariable, TFilterConstant, bool> handler)
        {
            SearchOperatorHandlers.Add(new SearchOperatorHandler<TFilterVariable, TFilterConstant>
            {
                Operator = op,
                Handler = handler
            });

            foreach (var backend in m_SearchBackends.Values)
                backend.AddSearchOperatorHandler(op, handler);
        }

        /// <summary>
        /// Add a custom filter operator handler.
        /// </summary>
        /// <typeparam name="TFilterVariable">The operator's left hand side type. This is the type returned by a filter handler.</typeparam>
        /// <typeparam name="TFilterConstant">The operator's right hand side type.</typeparam>
        /// <param name="op">The filter operator.</param>
        /// <param name="handler">Callback to handle the operation. Takes a TFilterVariable (value returned by the filter handler, will vary for each element), a TFilterConstant (right hand side value of the operator, which is constant), a StringComparison option and returns a boolean indicating if the filter passes or not.</param>
        public void AddSearchOperatorHandler<TFilterVariable, TFilterConstant>(string op, Func<TFilterVariable, TFilterConstant, StringComparison, bool> handler)
        {
            SearchOperatorHandlers.Add(new SearchOperatorHandler<TFilterVariable, TFilterConstant>
            {
                Operator = op,
                HandlerWithStringComparison = handler
            });

            foreach (var backend in m_SearchBackends.Values)
                backend.AddSearchOperatorHandler(op, handler);
        }

        /// <summary>
        /// Apply the filtering on an IEnumerable data set.
        /// </summary>
        /// <param name="text">The search string.</param>
        /// <returns>A filtered IEnumerable.</returns>
        public ISearchQuery<TData> Parse<TData>(string text)
        {
            return GetBackend<TData>().Parse(text);
        }

        public void RegisterBackend<TData>(ISearchBackend<TData> backend)
        {
            if (null == backend)
                throw new ArgumentNullException(nameof(backend));

            // NOTE: This method can overwrite the built in backend which was registered but not another custom backend.
            if (m_SearchBackends.TryGetValue(typeof(TData), out var instance) && !(instance is SearchBackend<TData>))
                throw new InvalidOperationException($"Failed to register ISearchBackend for Type=[{typeof(TData)}]. Type has already been registered.");

            m_SearchBackends[typeof(TData)] = backend;
            backend.GlobalStringComparison = GlobalStringComparison;

            // Register any property based search data.
            foreach (var searchData in SearchDataProperties)
                backend.AddSearchDataProperty(searchData.Path);

            // Register any property based filters.
            foreach (var filter in SearchFilterProperties)
                backend.AddSearchFilterProperty(filter.Token, filter.Path, filter.Options);

            // Register any search data callbacks. This is done via interface to invoke using the strongly typed interface.
            foreach (var searchDataCallback in SearchDataCallbacks)
                searchDataCallback.Register(backend);

            // Register any search filter callbacks. This is done via interface to invoke using the strongly typed interface.
            foreach (var searchFilterCallback in SearchFilterCallbacks)
                searchFilterCallback.Register(backend);

            // Register any search operator handlers. This is done via interface to invoke using the strongly typed interface.
            foreach (var handler in SearchOperatorHandlers)
                handler.Register(backend);
        }

        public void UnregisterBackend<TData>(ISearchBackend<TData> backend)
        {
            if (null == backend)
                throw new ArgumentNullException(nameof(backend));

            if (!m_SearchBackends.TryGetValue(typeof(TData), out var instance))
                throw new InvalidOperationException($"Failed to unregister ISearchBackend for Type=[{typeof(TData)}]. Backend has not been registered.");

            if (instance != backend)
                throw new InvalidOperationException($"Failed to unregister ISearchBackend for Type=[{typeof(TData)}]. The specified backend does not match the registered instance.");

            m_SearchBackends.Remove(typeof(TData));
        }

        static ISearchBackend<TData> CreateDefaultBackend<TData>()
        {
            return new QuickSearchBackend<TData>();
        }

        ISearchBackend<TData> GetBackend<TData>()
        {
            if (m_SearchBackends.TryGetValue(typeof(TData), out var value))
            {
                return value as ISearchBackend<TData>;
            }

            var backend = CreateDefaultBackend<TData>();

            m_SearchBackends[typeof(TData)] = backend;
            backend.GlobalStringComparison = GlobalStringComparison;

            // Register any property based search data.
            foreach (var searchDataProperty in SearchDataProperties)
                backend.AddSearchDataProperty(searchDataProperty.Path);

            // Register any property based filters.
            foreach (var searchFilterProperty in SearchFilterProperties)
                backend.AddSearchFilterProperty(searchFilterProperty.Token, searchFilterProperty.Path, searchFilterProperty.Options);

            // Register any search data callbacks. This is done via interface to invoke using the strongly typed interface.
            foreach (var searchDataCallback in SearchDataCallbacks)
                searchDataCallback.Register(backend);

            // Register any search filter callbacks. This is done via interface to invoke using the strongly typed interface.
            foreach (var searchFilterCallback in SearchFilterCallbacks)
                searchFilterCallback.Register(backend);

            // Register any search operator handlers. This is done via interface to invoke using the strongly typed interface.
            foreach (var handler in SearchOperatorHandlers)
                handler.Register(backend);

            backend.GlobalStringComparison = GlobalStringComparison;

            return backend;
        }

        // Internal for tests
        internal IEnumerable<ISearchBackend> GetRegisteredBackends()
        {
            foreach (var backend in m_SearchBackends.Values)
            {
                yield return backend;
            }
        }

        internal QueryEngine<TData> GetQueryEngine<TData>()
        {
            if (GetBackend<TData>() is QuickSearchBackend<TData> backend)
                return backend.QueryEngine;

            return null;
        }
    }

    /// <summary>
    /// Common interface to abstract the query engine backend.
    /// </summary>
    /// <typeparam name="TData">The strongly typed data this engine can filter.</typeparam>
    abstract class SearchBackend<TData> : ISearchBackend<TData>
    {
        class SearchDataVisitor : PathVisitor
        {
            class CollectionSearchDataVisitor : ICollectionPropertyBagVisitor
            {
                public List<string> SearchData;

                void ICollectionPropertyBagVisitor.Visit<TCollection, TElement>(ICollectionPropertyBag<TCollection, TElement> properties, ref TCollection container)
                {
                    if (null == container)
                        return;

                    foreach (var element in container)
                        SearchData.Add(element?.ToString());
                }
            }

            readonly CollectionSearchDataVisitor m_CollectionSearchDataVisitor = new CollectionSearchDataVisitor();

            public readonly List<string> SearchData = new List<string>();

            protected override void VisitPath<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container, ref TValue value)
            {
                if (PropertyBag.GetPropertyBag<TValue>() is ICollectionPropertyBagAccept<TValue> collectionPropertyBagAccept)
                {
                    m_CollectionSearchDataVisitor.SearchData = SearchData;
                    collectionPropertyBagAccept.Accept(m_CollectionSearchDataVisitor, ref value);
                }
                else
                {
                    SearchData.Add(value?.ToString());
                }
            }

            public override void Reset()
            {
                SearchData.Clear();
                base.Reset();
            }
        }

        readonly List<PropertyPath> m_SearchDataProperties = new List<PropertyPath>();
        readonly List<Func<TData, IEnumerable<string>>> m_SearchDataFunc = new List<Func<TData, IEnumerable<string>>>();
        readonly SearchDataVisitor m_SearchDataVisitor = new SearchDataVisitor();

        public StringComparison GlobalStringComparison { get; set; }

        // ReSharper disable once StaticMemberInGenericType
        static readonly List<string> s_SearchData = new List<string>();

        /// <summary>
        /// Returns all search data strings for the given <typeparamref name="TData"/> instance.
        /// </summary>
        /// <remarks>
        /// The search data strings are extracted based on the <see cref="SearchDataProperty"/> elements registered.
        /// </remarks>
        /// <param name="data">The instance to gather data from.</param>
        /// <typeparam name="TData">The instance type.</typeparam>
        /// <returns>An <see cref="IEnumerator{T}"/> over the search data strings for the specified data.</returns>
        protected IEnumerable<string> GetSearchData(TData data)
        {
            s_SearchData.Clear();

            if (TypeTraits<TData>.CanBeNull)
            {
                if (null == data)
                {
                    return s_SearchData;
                }
            }

            foreach (var func in m_SearchDataFunc)
            {
                var enumerable = func(data);

                if (null == enumerable)
                    continue;

                foreach (var searchData in enumerable)
                {
                    if (string.IsNullOrEmpty(searchData))
                        continue;

                    s_SearchData.Add(searchData);
                }
            }

            foreach (var searchDataPath in m_SearchDataProperties)
            {
                m_SearchDataVisitor.Reset();
                m_SearchDataVisitor.Path = searchDataPath;

                PropertyContainer.TryAccept(m_SearchDataVisitor, ref data);

                if (m_SearchDataVisitor.ReturnCode != VisitReturnCode.Ok)
                    continue;

                foreach (var element in m_SearchDataVisitor.SearchData)
                {
                    if (null != element)
                    {
                        s_SearchData.Add(element);
                    }
                }
            }

            return s_SearchData;
        }

        public void AddSearchDataProperty(PropertyPath path)
        {
            m_SearchDataProperties.Add(path);
        }

        public void AddSearchDataCallback(Func<TData, IEnumerable<string>> getSearchDataFunc)
        {
            if (null == getSearchDataFunc)
                throw new ArgumentNullException(nameof(getSearchDataFunc));

            m_SearchDataFunc.Add(getSearchDataFunc);
        }

        public abstract void AddSearchFilterProperty(string token, PropertyPath path, SearchFilterOptions options);
        public abstract void AddSearchFilterCallback<TFilter>(string token, Func<TData, TFilter> getFilterDataFunc, SearchFilterOptions options);
        public abstract void AddSearchOperatorHandler<TFilterVariable, TFilterConstant>(string op, Func<TFilterVariable, TFilterConstant, bool> handler);
        public abstract void AddSearchOperatorHandler<TFilterVariable, TFilterConstant>(string op, Func<TFilterVariable, TFilterConstant, StringComparison, bool> handler);

        /// <summary>
        /// Applies the given search text to the specified data set.
        /// </summary>
        /// <param name="text">The search string.</param>
        /// <returns>A <see cref="ISearchQuery{TData}"/> which can be applied to data to generate a filtered set.</returns>
        public abstract ISearchQuery<TData> Parse(string text);
    }
}
