using System;
using System.Collections.Generic;
using Unity.Properties;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The <see cref="ISearchBackend"/> is responsible for parsing a given input string and generating an <see cref="ISearchQuery{TData}"/> object.
    /// </summary>
    /// <remarks>
    /// The backend is expected to manage the given search data properties and callback functions.
    /// </remarks>
    interface ISearchBackend
    {
        /// <summary>
        /// Adds the given <see cref="PropertyPath"/> to the set of searchable data.
        /// </summary>
        /// <param name="path">The path to a member within the search data type.</param>
        void AddSearchDataProperty(PropertyPath path);

        /// <summary>
        /// Adds a search filter callback to the backend. The value at the given <see cref="PropertyPath"/> will be compared in a strongly typed way with the parsed search text.
        /// </summary>
        /// <param name="token">The token which binds to the given path.</param>
        /// <param name="path">The path to a member within the search data that will be compared with the search text.</param>
        /// <param name="options">The set of filter options.</param>
        void AddSearchFilterProperty(string token, PropertyPath path, SearchFilterOptions options);

        /// <summary>
        /// Add a custom filter operator handler.
        /// </summary>
        /// <typeparam name="TFilterVariable">The operator's left hand side type. This is the type returned by a filter handler.</typeparam>
        /// <typeparam name="TFilterConstant">The operator's right hand side type.</typeparam>
        /// <param name="op">The filter operator.</param>
        /// <param name="handler">Callback to handle the operation. Takes a TFilterVariable (value returned by the filter handler, will vary for each element) and a TFilterConstant (right hand side value of the operator, which is constant), and returns a boolean indicating if the filter passes or not.</param>
        void AddSearchOperatorHandler<TFilterVariable, TFilterConstant>(string op, Func<TFilterVariable, TFilterConstant, bool> handler);

        /// <summary>
        /// Add a custom filter operator handler.
        /// </summary>
        /// <typeparam name="TFilterVariable">The operator's left hand side type. This is the type returned by a filter handler.</typeparam>
        /// <typeparam name="TFilterConstant">The operator's right hand side type.</typeparam>
        /// <param name="op">The filter operator.</param>
        /// <param name="handler">Callback to handle the operation. Takes a TFilterVariable (value returned by the filter handler, will vary for each element), a TFilterConstant (right hand side value of the operator, which is constant), a StringComparison option and returns a boolean indicating if the filter passes or not.</param>
        void AddSearchOperatorHandler<TFilterVariable, TFilterConstant>(string op, Func<TFilterVariable, TFilterConstant, StringComparison, bool> handler);

        /// <summary>
        /// String comparison options for word matching and filter handling (if not overridden).
        /// </summary>
        StringComparison GlobalStringComparison { get; set; }
    }

    /// <summary>
    /// The <see cref="ISearchBackend"/> is responsible for parsing a given input string and generating an <see cref="ISearchQuery{TData}"/> object.
    /// </summary>
    /// <remarks>
    /// The backend is expected to manage the given search data properties and callback functions.
    /// </remarks>
    /// <typeparam name="TData">The data type this backend handles.</typeparam>
    interface ISearchBackend<TData> : ISearchBackend
    {
        /// <summary>
        /// Adds a search data callback to the backend. This function can be used to return a set of data which will be compared against the search string.
        /// </summary>
        /// <param name="getSearchDataFunc">The callback which can be used to return a set of searchable data for a given <see cref="TData"/>.</param>
        void AddSearchDataCallback(Func<TData, IEnumerable<string>> getSearchDataFunc);

        /// <summary>
        /// Adds a search filter callback to the backend. This function can be used to return a strongly typed value which can be compared against the search string for a given token.
        /// </summary>
        /// <param name="token">The token which binds to the given filter function.</param>
        /// <param name="getFilterDataFunc">The filter callback which returns a strongly typed value from <see cref="TData"/>.</param>
        /// <param name="options">The set of filter options.</param>
        /// <typeparam name="TFilter">The strongly typed value.</typeparam>
        void AddSearchFilterCallback<TFilter>(string token, Func<TData, TFilter> getFilterDataFunc, SearchFilterOptions options);

        /// <summary>
        /// Applies the given search text and generates a query object which can be applied to a collection of <see cref="TData"/> objects.
        /// </summary>
        /// <param name="text">The search string.</param>
        /// <returns>A <see cref="ISearchQuery{TData}"/> which can be applied to data to generate a filtered set.</returns>
        ISearchQuery<TData> Parse(string text);
    }
}
