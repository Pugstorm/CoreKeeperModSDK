using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Unity.Entities.Editor
{
    static class HierarchyQueryBuilder
    {
        static readonly Regex k_Regex = new Regex(
            @$"\b(?<token>[{Constants.ComponentSearch.TokenCaseInsensitive}]{Constants.ComponentSearch.Op})\s*(?<componentType>(\S)*)",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);

        static readonly StringBuilder k_UnmatchedInputBuilder = new StringBuilder();

        public static Result BuildQuery(string input)
        {
            if (string.IsNullOrEmpty(input))
                return Result.ValidBecauseEmpty;

            var matches = k_Regex.Matches(input);
            if (matches.Count == 0)
                return Result.Valid(null, input);

            using var componentTypes = PooledHashSet<ComponentType>.Make();

            k_UnmatchedInputBuilder.Clear();

            var pos = 0;
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var matchGroup = match.Groups["componentType"];

                var length = match.Index - pos;
                if (length > 0)
                    k_UnmatchedInputBuilder.Append(input.Substring(pos, length));

                pos = match.Index + match.Length;

                if (matchGroup.Value.Length == 0)
                    continue;

                var results = ComponentTypeCache.GetExactMatchingTypes(matchGroup.Value);
                var resultFound = false;
                foreach (var result in results)
                {
                    resultFound = true;
                    componentTypes.Set.Add(result);
                }

                if (!resultFound)
                    return Result.Invalid(matchGroup.Value);
            }

            if (input.Length - pos > 0)
                k_UnmatchedInputBuilder.Append(input.Substring(pos));

            if (componentTypes.Set.Count == 0 && k_UnmatchedInputBuilder.Length == 0)
                return Result.Invalid(string.Empty);

            // Entity type is legal in UI, but not allowed in EntityQuery, so remove it.
            var entityTypeIndex = TypeManager.GetTypeIndex<Entity>();
            componentTypes.Set.RemoveWhere(t => t.TypeIndex == entityTypeIndex);

            return Result.Valid(new EntityQueryDesc
            {
                // Temp patch: Using `All` since most users seem to prefer that behaviour.
                // The real solution is to properly support entity queries in search.
                All = componentTypes.Set.ToArray(),
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
            }, k_UnmatchedInputBuilder.ToString());
        }

        public struct Result
        {
            public bool IsValid;
            public EntityQueryDesc EntityQueryDesc;
            public string ErrorComponentType;
            public string Filter;

            public static readonly Result ValidBecauseEmpty = new Result { IsValid = true, EntityQueryDesc = null, Filter = string.Empty, ErrorComponentType = string.Empty };

            public static Result Invalid(string errorComponentType)
                => new Result { IsValid = false, EntityQueryDesc = null, Filter = string.Empty, ErrorComponentType = errorComponentType };

            public static Result Valid(EntityQueryDesc queryDesc, string filter)
                => new Result { IsValid = true, EntityQueryDesc = queryDesc, Filter = filter, ErrorComponentType = string.Empty };
        }
    }
}
