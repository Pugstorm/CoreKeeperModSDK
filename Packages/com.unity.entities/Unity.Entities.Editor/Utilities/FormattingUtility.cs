using System;
using System.Globalization;

namespace Unity.Entities.Editor
{
    static class FormattingUtility
    {
        static readonly string[] s_BytesToStringSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

        public static string CountToString(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
        public static string CountToString(uint value) => value.ToString("N0", CultureInfo.InvariantCulture);
        public static string CountToString(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
        public static string CountToString(ulong value) => value.ToString("N0", CultureInfo.InvariantCulture);

        public static string BytesToString(ulong value)
        {
            if (value < 1024)
                return value + " B";

            var place = Convert.ToInt32(Math.Floor(Math.Log(value, 1024)));
            var num = Math.Round(value / Math.Pow(1024, place), 1).ToString($"0.0", CultureInfo.InvariantCulture);
            return num + " " + s_BytesToStringSuffixes[place];
        }

        public static string HashToString(ulong hash)
        {
            return hash.ToString("x", CultureInfo.InvariantCulture);
        }

        public static string NsToMsString(long nanoseconds)
        {
            if (nanoseconds >= 1e3)
                return (nanoseconds * 1e-6).ToString("F3", CultureInfo.InvariantCulture);
            else if (nanoseconds > 0)
                return "<0.001";
            return "-";
        }
    }
}
