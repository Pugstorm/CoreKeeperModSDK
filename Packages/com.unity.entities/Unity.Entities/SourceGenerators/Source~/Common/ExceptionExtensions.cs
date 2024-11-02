#nullable enable
using System;

namespace Unity.Entities.SourceGen.Common
{
    public static class ExceptionExtensions
    {
        public static string ToUnityPrintableString(this Exception exception) => exception.ToString().Replace(Environment.NewLine, " |--| ");
    }
}
