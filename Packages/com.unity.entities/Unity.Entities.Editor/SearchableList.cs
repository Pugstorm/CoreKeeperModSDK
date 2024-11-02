using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Provides basic searchable list handling.
    /// </summary>
    class SearchableList<TList, TItem> where TList : BaseListView
    {
        readonly TList m_List;
        readonly SearchElement m_SearchElement;
        readonly Func<IEnumerable<TItem>> m_GetSourceItems;
        readonly List<TItem> m_FilteredItems;
        readonly Stopwatch m_RefreshTimer;

        /// <summary>
        /// The list connected to the search element.
        /// </summary>
        public TList List => m_List;

        /// <summary>
        /// Count of source items in the list.
        /// </summary>
        public int Count => m_List.itemsSource?.Count ?? 0;

        /// <summary>
        /// The search element connected to the list.
        /// </summary>
        public SearchElement SearchElement => m_SearchElement;

        /// <summary>
        /// Whether or not the search element contains a search value.
        /// </summary>
        public bool HasFilter => !string.IsNullOrEmpty(m_SearchElement.value);

        /// <summary>
        /// Time between each list refresh when searching asynchronously.
        /// </summary>
        public long RefreshDelay { get; set; } = 1000;

        public SearchableList(
            TList list,
            SearchElement search,
            Func<IEnumerable<TItem>> getSourceItems,
            Func<TItem, IEnumerable<string>> getSearchData)
        {
            m_List = list;
            m_SearchElement = search;
            m_GetSourceItems = getSourceItems;
            m_FilteredItems = new List<TItem>();
            m_RefreshTimer = new Stopwatch();

            m_SearchElement.SearchDelay = 250;
            m_SearchElement.FilterPopupWidth = 250;
            m_SearchElement.AddSearchDataCallback(getSearchData);

            var searchHandler = new SearchHandler<TItem>(m_SearchElement) { Mode = SearchHandlerType.async };
            searchHandler.SetSearchDataProvider(m_GetSourceItems);
            searchHandler.OnBeginSearch += OnBeginSearch;
            searchHandler.OnFilter += OnFilter;
            searchHandler.OnEndSearch += OnSearchEnd;
        }

        public TItem this[int index]
        {
            get
            {
                var items = (IList<TItem>)m_List.itemsSource;
                if (items == null)
                    throw new NullReferenceException(nameof(items));

                if (index < 0 || index >= items.Count)
                    throw new IndexOutOfRangeException(nameof(index));

                return items[index];
            }
        }

        public void Refresh()
        {
            if (HasFilter)
                m_SearchElement.Search();
            else
                Rebuild();
        }

        public void AddDefaultEnumerableOperatorHandlers()
        {
            AddEnumerableOperatorHandler<string>(":", (lhs, rhs, options) => lhs.IndexOf(rhs, options) >= 0);
            AddEnumerableOperatorHandler<string>("=", (lhs, rhs, options) => lhs.Equals(rhs, options));
            AddEnumerableOperatorHandler<string>("!=", (lhs, rhs, options) => !lhs.Equals(rhs, options));

            AddEnumerableOperatorHandler<double>("=", (lhs, rhs) => lhs == rhs);
            AddEnumerableOperatorHandler<double>("!=", (lhs, rhs) => lhs != rhs);
            AddEnumerableOperatorHandler<double>("<", (lhs, rhs) => lhs < rhs);
            AddEnumerableOperatorHandler<double>("<=", (lhs, rhs) => lhs <= rhs);
            AddEnumerableOperatorHandler<double>(">", (lhs, rhs) => lhs > rhs);
            AddEnumerableOperatorHandler<double>(">=", (lhs, rhs) => lhs >= rhs);

            AddEnumerableOperatorHandler<float>("=", (lhs, rhs) => lhs == rhs);
            AddEnumerableOperatorHandler<float>("!=", (lhs, rhs) => lhs != rhs);
            AddEnumerableOperatorHandler<float>("<", (lhs, rhs) => lhs < rhs);
            AddEnumerableOperatorHandler<float>("<=", (lhs, rhs) => lhs <= rhs);
            AddEnumerableOperatorHandler<float>(">", (lhs, rhs) => lhs > rhs);
            AddEnumerableOperatorHandler<float>(">=", (lhs, rhs) => lhs >= rhs);

            AddEnumerableOperatorHandler<long>("=", (lhs, rhs) => lhs == rhs);
            AddEnumerableOperatorHandler<long>("!=", (lhs, rhs) => lhs != rhs);
            AddEnumerableOperatorHandler<long>("<", (lhs, rhs) => lhs < rhs);
            AddEnumerableOperatorHandler<long>("<=", (lhs, rhs) => lhs <= rhs);
            AddEnumerableOperatorHandler<long>(">", (lhs, rhs) => lhs > rhs);
            AddEnumerableOperatorHandler<long>(">=", (lhs, rhs) => lhs >= rhs);

            AddEnumerableOperatorHandler<ulong>("=", (lhs, rhs) => lhs == rhs);
            AddEnumerableOperatorHandler<ulong>("!=", (lhs, rhs) => lhs != rhs);
            AddEnumerableOperatorHandler<ulong>("<", (lhs, rhs) => lhs < rhs);
            AddEnumerableOperatorHandler<ulong>("<=", (lhs, rhs) => lhs <= rhs);
            AddEnumerableOperatorHandler<ulong>(">", (lhs, rhs) => lhs > rhs);
            AddEnumerableOperatorHandler<ulong>(">=", (lhs, rhs) => lhs >= rhs);

            AddEnumerableOperatorHandler<int>("=", (lhs, rhs) => lhs == rhs);
            AddEnumerableOperatorHandler<int>("!=", (lhs, rhs) => lhs != rhs);
            AddEnumerableOperatorHandler<int>("<", (lhs, rhs) => lhs < rhs);
            AddEnumerableOperatorHandler<int>("<=", (lhs, rhs) => lhs <= rhs);
            AddEnumerableOperatorHandler<int>(">", (lhs, rhs) => lhs > rhs);
            AddEnumerableOperatorHandler<int>(">=", (lhs, rhs) => lhs >= rhs);

            AddEnumerableOperatorHandler<uint>("=", (lhs, rhs) => lhs == rhs);
            AddEnumerableOperatorHandler<uint>("!=", (lhs, rhs) => lhs != rhs);
            AddEnumerableOperatorHandler<uint>("<", (lhs, rhs) => lhs < rhs);
            AddEnumerableOperatorHandler<uint>("<=", (lhs, rhs) => lhs <= rhs);
            AddEnumerableOperatorHandler<uint>(">", (lhs, rhs) => lhs > rhs);
            AddEnumerableOperatorHandler<uint>(">=", (lhs, rhs) => lhs >= rhs);

            AddEnumerableOperatorHandler<short>("=", (lhs, rhs) => lhs == rhs);
            AddEnumerableOperatorHandler<short>("!=", (lhs, rhs) => lhs != rhs);
            AddEnumerableOperatorHandler<short>("<", (lhs, rhs) => lhs < rhs);
            AddEnumerableOperatorHandler<short>("<=", (lhs, rhs) => lhs <= rhs);
            AddEnumerableOperatorHandler<short>(">", (lhs, rhs) => lhs > rhs);
            AddEnumerableOperatorHandler<short>(">=", (lhs, rhs) => lhs >= rhs);

            AddEnumerableOperatorHandler<ushort>("=", (lhs, rhs) => lhs == rhs);
            AddEnumerableOperatorHandler<ushort>("!=", (lhs, rhs) => lhs != rhs);
            AddEnumerableOperatorHandler<ushort>("<", (lhs, rhs) => lhs < rhs);
            AddEnumerableOperatorHandler<ushort>("<=", (lhs, rhs) => lhs <= rhs);
            AddEnumerableOperatorHandler<ushort>(">", (lhs, rhs) => lhs > rhs);
            AddEnumerableOperatorHandler<ushort>(">=", (lhs, rhs) => lhs >= rhs);

            AddEnumerableOperatorHandler<sbyte>("=", (lhs, rhs) => lhs == rhs);
            AddEnumerableOperatorHandler<sbyte>("!=", (lhs, rhs) => lhs != rhs);
            AddEnumerableOperatorHandler<sbyte>("<", (lhs, rhs) => lhs < rhs);
            AddEnumerableOperatorHandler<sbyte>("<=", (lhs, rhs) => lhs <= rhs);
            AddEnumerableOperatorHandler<sbyte>(">", (lhs, rhs) => lhs > rhs);
            AddEnumerableOperatorHandler<sbyte>(">=", (lhs, rhs) => lhs >= rhs);

            AddEnumerableOperatorHandler<byte>("=", (lhs, rhs) => lhs == rhs);
            AddEnumerableOperatorHandler<byte>("!=", (lhs, rhs) => lhs != rhs);
            AddEnumerableOperatorHandler<byte>("<", (lhs, rhs) => lhs < rhs);
            AddEnumerableOperatorHandler<byte>("<=", (lhs, rhs) => lhs <= rhs);
            AddEnumerableOperatorHandler<byte>(">", (lhs, rhs) => lhs > rhs);
            AddEnumerableOperatorHandler<byte>(">=", (lhs, rhs) => lhs >= rhs);
        }

        void OnBeginSearch(ISearchQuery<TItem> query)
        {
            m_FilteredItems.Clear();
            m_RefreshTimer.Restart();
            Rebuild();
        }

        void OnFilter(ISearchQuery<TItem> query, IEnumerable<TItem> items)
        {
            if (!HasFilter)
                return;

            m_FilteredItems.AddRange(items);
            if (m_RefreshTimer.ElapsedMilliseconds >= RefreshDelay)
            {
                m_RefreshTimer.Restart();
                Rebuild();
            }
        }

        void OnSearchEnd(ISearchQuery<TItem> query)
        {
            m_RefreshTimer.Reset();
            Rebuild();
        }

        void Rebuild()
        {
            m_List.itemsSource = HasFilter ? m_FilteredItems : (IList)m_GetSourceItems();
            m_List.Rebuild();
        }

        void AddEnumerableOperatorHandler<T>(string op, Func<T, T, bool> handler)
        {
            m_SearchElement
                .GetQueryEngine<TItem>()
                .AddOperatorHandler<IEnumerable<T>, T>(op, (lhs, rhs) => lhs.Any(element => handler(element, rhs)));
        }

        void AddEnumerableOperatorHandler<T>(string op, Func<T, T, StringComparison, bool> handler)
        {
            m_SearchElement
                .GetQueryEngine<TItem>()
                .AddOperatorHandler<IEnumerable<T>, T>(op, (lhs, rhs, options) => lhs.Any(element => handler(element, rhs, options)));
        }
    }
}
