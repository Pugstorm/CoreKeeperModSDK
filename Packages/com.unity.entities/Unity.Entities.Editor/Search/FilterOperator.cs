using System;
using System.Collections.Generic;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Helper class to get and apply filters without using the com.unity.quicksearch package.
    /// </summary>
    static class FilterOperator
    {
        /// <summary>
        /// Gets the available operator types for the specified type.
        /// </summary>
        /// <typeparam name="T">The type to get filters for.</typeparam>
        /// <returns>An array of operator tokens.</returns>
        public static string[] GetSupportedOperators<T>()
        {
            var operators = new List<string>
            {
                ":"
            };

            var equatable = typeof(IEquatable<T>).IsAssignableFrom(typeof(T));
            var comparable = typeof(IComparable<T>).IsAssignableFrom(typeof(T));

            if (equatable)
            {
                operators.Add("!=");
                operators.Add("=");
            }

            if (comparable)
            {
                operators.Add(">");
                operators.Add(">=");
                operators.Add("<");
                operators.Add("<=");
            }

            return operators.ToArray();
        }

        public static bool ApplyOperator<T>(string token, T value, T input, StringComparison sc)
        {
            switch (token)
            {
                case ":":
                    return value?.ToString().IndexOf(input?.ToString() ?? string.Empty, sc) >= 0;
                case "=":
                    return (value as IEquatable<T>).Equals(input);
                case "!=":
                    return !(value as IEquatable<T>).Equals(input);
                case ">":
                    return (value as IComparable<T>).CompareTo(input) > 0;
                case ">=":
                    return (value as IComparable<T>).CompareTo(input) >= 0;
                case "<":
                    return (value as IComparable<T>).CompareTo(input) < 0;
                case "<=":
                    return (value as IComparable<T>).CompareTo(input) <= 0;
            }

            return false;
        }
    }
}
