using System.Text.RegularExpressions;

namespace Unity.Entities.UI
{
    static class StringExtensions
    {
        static readonly Regex s_SplitCaseRegex = new Regex(@"(\B[A-Z]+?(?=[A-Z][^A-Z])|\B[A-Z]+?(?=[^A-Z]))");

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
