using System.IO;
using System.Text.RegularExpressions;

namespace Unity.Entities.Editor
{
    static class StringExtensions
    {
        static readonly Regex s_ToWordRegex = new Regex(@"[^\w]", RegexOptions.Compiled);
        static readonly Regex s_SplitCaseRegex = new Regex(@"(\B[A-Z]+?(?=[A-Z][^A-Z])|\B[A-Z]+?(?=[^A-Z]))");

        public static string SingleQuoted(this string value, bool onlyIfSpaces = false)
        {
            if (onlyIfSpaces && !value.Contains(' '))
                return value;

            return $"'{value.Trim('\'')}'";
        }

        public static string DoubleQuoted(this string value, bool onlyIfSpaces = false)
        {
            if (onlyIfSpaces && !value.Contains(' '))
                return value;

            return $"\"{value.Trim('\"')}\"";
        }

        public static string ToHyperLink(this string value, string key = null)
        {
            return string.IsNullOrEmpty(key) ? $"<a>{value}</a>" : $"<a {key}={value.DoubleQuoted()}>{value}</a>";
        }

        public static string ToIdentifier(this string value)
        {
            return s_ToWordRegex.Replace(value, "_");
        }

        public static string ToForwardSlash(this string value) => value.Replace('\\', '/');

        public static string ReplaceLastOccurrence(this string value, string oldValue, string newValue)
        {
            var index = value.LastIndexOf(oldValue);
            return index >= 0 ? value.Remove(index, oldValue.Length).Insert(index, newValue) : value;
        }

        /// <summary>
        /// Given a pascal case or camel case string this method will add spaces between the capital letters.
        ///
        /// e.g.
        /// "someField"    -> "Some Field"
        /// "layoutWidth"  -> "Layout Width"
        /// "TargetCount"  -> "Target Count"
        /// </summary>
        public static string SplitPascalCase(this string str)
        {
            str = s_SplitCaseRegex.Replace(str, " $1");
            return str.Substring(0, 1).ToUpper() + str.Substring(1);
        }
    }
}
