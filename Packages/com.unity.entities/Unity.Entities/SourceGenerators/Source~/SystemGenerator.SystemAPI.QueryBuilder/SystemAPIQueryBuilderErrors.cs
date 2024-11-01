using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.QueryBuilder
{
   public static class SystemApiQueryBuilderErrors
    {
        private const string ErrorTitle = "SystemAPIQueryBuilderError";

        public static void SGQB001(SystemDescription systemDescription, Location errorLocation)
        {
            systemDescription.LogError(
                nameof(SGQB001),
                ErrorTitle,
                "`SystemAPI.QueryBuilder().WithOptions()` should only be called once per query. Subsequent calls will override previous options, rather than adding to them. " +
                "Use the bitwise OR operator '|' to combine multiple options.",
                errorLocation);
        }
    }
}
