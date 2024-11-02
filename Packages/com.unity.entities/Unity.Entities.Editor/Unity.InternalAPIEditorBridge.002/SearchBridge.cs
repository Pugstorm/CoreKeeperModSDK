using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace Unity.Editor.Bridge
{
    static class SearchBridge
    {
        public static IQueryEngineFilter SetFilter<TFilter, TData>(QueryEngine<TData> queryEngine, string token, Func<TData, TFilter> getDataFunc, string[] supportedOperatorType = null)
        {
            return queryEngine.SetFilter(token, getDataFunc, supportedOperatorType);
        }

        public static void AddFilter<TFilter, TData>(QueryEngine<TData> queryEngine, string token, Func<TData, QueryFilterOperator, TFilter, bool> filterResolver, string[] supportedOperatorType = null)
        {
            queryEngine.AddFilter(token, filterResolver, supportedOperatorType);
        }

        public static IQueryEngineFilter AddOrUpdateProposition(this IQueryEngineFilter filter, string label, string category = null, string replacement = null, string help = null, string data = null,
                                       int priority = 0, Texture2D icon = null, System.Type type = null, Color color = default, TextCursorPlacement moveCursor = TextCursorPlacement.MoveAutoComplete)
        {
            return filter.AddOrUpdatePropositionData(label, category, replacement, help, data, priority, icon, type, color, moveCursor);
        }

        public static IEnumerable<SearchProposition> GetPropositionsFromListBlockType(Type t)
        {
            return QueryListBlockAttribute.GetPropositions(t);
        }

        public static IEnumerable<SearchProposition> GetPropositions<TData>(QueryEngine<TData> qe)
        {
            return qe.GetPropositions();
        }

        public static IEnumerable<SearchProposition> GetAndOrQueryBlockPropositions()
        {
            return QueryAndOrBlock.BuiltInQueryBuilderPropositions();
        }

        public static Texture2D LoadIcon(string iconName)
        {
            return Utils.LoadIcon(iconName);
        }

        public static void SetTableConfig(SearchProvider p, Func<SearchContext, SearchTable> tableConfig)
        {
            p.tableConfig = tableConfig;
        }

        public static bool CompareWords(QueryFilterOperator op, string value, IEnumerable<string> words, StringComparison comp = StringComparison.CurrentCultureIgnoreCase)
        {
            if (words == null || string.IsNullOrEmpty(value))
                return false;

            value = value.ToLowerInvariant();
            if (op.type == FilterOperatorType.Equal)
            {
                return words.Any(r => r.Equals(value, comp));
            }
            else
            {
                return words.Any(t => t.IndexOf(value, comp) != -1);
            }
        }

        public static ISearchView OpenContextual(string providerId, string searchText, Action<SearchViewState> setup = null)
        {
            return OpenContextualTable(providerId, searchText, null, setup);
        }

        public static ISearchView OpenContextualTable(string providerId, string searchText, SearchTable table, Action<SearchViewState> setup = null)
        {
            var win = FindWindowWithSingleProvider(providerId);
            if (win != null)
            {
                // Recycle this SearchView
                win.SetSearchText(searchText);
                return win;
            }


            var searchContext = SearchService.CreateContext(providerId, searchText, SearchFlags.Default);
            var viewState = SearchViewState.LoadDefaults();
            viewState.queryBuilderEnabled = true;
            viewState.context = searchContext;
            viewState.ignoreSaveSearches = true;
            if (table != null)
            {
                viewState.itemSize = (float)DisplayMode.Table;
                viewState.tableConfig = table;
            }

            var provider = searchContext.providers.FirstOrDefault();
            if (provider != null)
                viewState.windowTitle = new GUIContent($"Search {provider.name}");
            setup?.Invoke(viewState);
            return SearchService.ShowWindow(viewState);
        }

        public static Color GetBackgroundColor(QueryBlock b)
        {
            return b.GetBackgroundColor();
        }

        public static IEnumerable<SearchProposition> GetEnumToggle<T>(string category, string help, params T[] enumValues) where T : Enum
        {
            foreach (var e in enumValues)
            {
                var enumName = Enum.GetName(typeof(T), e);
                yield return new SearchProposition(category: category, enumName, $"+{e}",
                    help, priority: 0, moveCursor: TextCursorPlacement.MoveAutoComplete,
                    color: QueryColors.toggle);
            }
        }

        public static IEnumerable<SearchProposition> GetEnumToggle<T>(string category, string help) where T : Enum
        {
            return GetEnumToggle(category, help, Enum.GetValues(typeof(T)) as T[]);
        }

        public static void RefreshWindowsWithProvider(string providerId)
        {
#if UNITY_2023_1_OR_NEWER
            var windows = Resources.FindObjectsOfTypeAll<SearchWindow>();
#else
            var windows = Resources.FindObjectsOfTypeAll<QuickSearch>();
#endif
            if (windows == null)
                return;
            foreach (var win in windows)
            {
                if (win.context.providers.FirstOrDefault(p => p.id == providerId) != null)
                    win.Refresh();
            }
        }

#if UNITY_2023_1_OR_NEWER
        public static SearchWindow FindWindowWithSingleProvider(string providerId)
        {
            var windows = Resources.FindObjectsOfTypeAll<SearchWindow>();
#else
        public static QuickSearch FindWindowWithSingleProvider(string providerId)
        {
            var windows = Resources.FindObjectsOfTypeAll<QuickSearch>();
#endif
            if (windows == null)
                return null;
            foreach (var win in windows)
            {
                if (win.context.providers.Count() == 1 && win.context.providers.First().id == providerId)
                    return win;
            }
            return null;
        }
    }
}
