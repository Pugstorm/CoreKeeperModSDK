using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Entities.SourceGen.Common
{
    public static class EnumerableHelpers
    {
        public static string SeparateBy(this IEnumerable<string> lines, string separator) => string.Join(separator, lines.Where(s => !string.IsNullOrEmpty(s)));
        public static string SeparateByComma(this IEnumerable<string> lines) => string.Join(",", lines.Where(s => !string.IsNullOrEmpty(s)));
        public static string SeparateByCommaAndSpace(this IEnumerable<string> lines) => string.Join(", ", lines.Where(s => !string.IsNullOrEmpty(s)));
        public static string SeparateByBinaryOr(this IEnumerable<string> lines) => string.Join("|", lines.Where(s => !string.IsNullOrEmpty(s)));
        public static string SeparateByNewLine(this IEnumerable<string> lines) => string.Join(Environment.NewLine, lines.Where(s => !string.IsNullOrEmpty(s)));

        public static IEnumerable<TSource> FindDuplicatesBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            var knownKeys = new HashSet<TKey>();
            foreach (TSource element in source)
                if (!knownKeys.Add(keySelector(element)))
                    yield return element;
        }
    }
}
