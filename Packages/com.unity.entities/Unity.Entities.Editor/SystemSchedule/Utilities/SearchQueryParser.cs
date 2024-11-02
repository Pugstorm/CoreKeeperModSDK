using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities.UI;

namespace Unity.Entities.Editor
{
    static class SearchQueryParser
    {
        public struct ParseResult : IEquatable<ParseResult>
        {
            public bool IsEmpty => string.IsNullOrWhiteSpace(Input);
            public string Input;
            public IEnumerable<string> Names;
            public IEnumerable<string> ComponentNames;
            public IEnumerable<string> DependencySystemNames;
            public string ErrorComponentType;

            public bool Equals(ParseResult other)
            {
                return Input == other.Input;
            }

            public static readonly ParseResult EmptyResult = new ParseResult
            {
                Input = string.Empty,
                ComponentNames = Array.Empty<string>(),
                DependencySystemNames = Array.Empty<string>(),
                ErrorComponentType = string.Empty,
            };
        }

        static string CheckComponentTypeExistence(IEnumerable<string> componentTypes)
        {
            foreach (var componentType in componentTypes)
            {
                if (!ComponentTypeCache.GetFuzzyMatchingTypes(componentType).Any())
                    return componentType;
            }

            return string.Empty;
        }

        public static ParseResult ParseSearchQuery(ISearchQuery<SystemForSearch> query)
        {
            if (string.IsNullOrWhiteSpace(query.SearchString))
                return ParseResult.EmptyResult;

            var nameList = new Lazy<List<string>>();
            var componentNameList = new Lazy<List<string>>();
            var dependencySystemNameList = new Lazy<List<string>>();

            foreach (var token in query.Tokens)
            {
                if (token.StartsWith(Constants.ComponentSearch.TokenOp, StringComparison.OrdinalIgnoreCase))
                {
                    componentNameList.Value.Add(token.Substring(Constants.ComponentSearch.TokenOp.Length));
                }
                else if (token.StartsWith(Constants.SystemSchedule.k_SystemDependencyToken, StringComparison.OrdinalIgnoreCase))
                {
                    var systemName = token.Substring(Constants.SystemSchedule.k_SystemDependencyToken.Length);
                    dependencySystemNameList.Value.Add(systemName);
                }
                else
                {
                    nameList.Value.Add(token);
                }
            }

            var errorComponentType = componentNameList.Value.Any() ? CheckComponentTypeExistence(componentNameList.Value) : string.Empty;

            return new ParseResult
            {
                Input = query.SearchString,
                Names = nameList.Value,
                ComponentNames = componentNameList.Value,
                DependencySystemNames = dependencySystemNameList.Value,
                ErrorComponentType = errorComponentType,
            };
        }
    }
}
