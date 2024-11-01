using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common;

public static class SystemTypeHelpers
{
    public static (bool IsSystemType, SystemType SystemType) TryGetSystemType(this ITypeSymbol namedSystemTypeSymbol)
    {
        if (namedSystemTypeSymbol.Is("Unity.Entities.SystemBase"))
            return (true, SystemType.SystemBase);
        if (namedSystemTypeSymbol.InheritsFromInterface("Unity.Entities.ISystem"))
            return (true, SystemType.ISystem);
        return (false, default);
    }
}
