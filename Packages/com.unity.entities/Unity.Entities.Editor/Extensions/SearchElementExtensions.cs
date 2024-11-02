using System;

namespace Unity.Entities.Editor
{
    static class SearchElementExtensions
    {
        public static void AddSearchFilterCallbackWithPopupItem<TData, TFilter>(this SearchElement searchElement, string token, Func<TData, TFilter> getSearchDataFunc, string filterText, string filterTooltip = "", SearchFilterOptions searchFilterOptions = default, string defaultOperator = ":")
        {
            searchElement.AddSearchFilterCallback(token, getSearchDataFunc, searchFilterOptions);
            searchElement.AddSearchFilterPopupItem(token, filterText, filterTooltip, defaultOperator);
        }
    }
}
