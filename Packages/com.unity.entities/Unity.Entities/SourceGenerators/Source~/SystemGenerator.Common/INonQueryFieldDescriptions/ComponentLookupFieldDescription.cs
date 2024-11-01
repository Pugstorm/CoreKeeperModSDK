using System;
using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common;

public readonly struct ComponentLookupFieldDescription : IEquatable<ComponentLookupFieldDescription>, IMemberDescription
{
    ITypeSymbol TypeSymbol { get; }
    bool IsReadOnly { get; }
    public string GeneratedFieldName{ get; }

    public void AppendMemberDeclaration(IndentedTextWriter w, bool forcePublic = false)
    {
        if (IsReadOnly)
            w.Write("[global::Unity.Collections.ReadOnly] ");
        if (forcePublic)
            w.Write("public ");
        w.Write($"global::Unity.Entities.ComponentLookup<{TypeSymbol.ToFullName()}> {GeneratedFieldName};");
        w.WriteLine();
    }

    public string GetMemberAssignment() =>
        $@"{GeneratedFieldName} = state.GetComponentLookup<{TypeSymbol.ToFullName()}>({(IsReadOnly ? "true" : "false")});";

    public ComponentLookupFieldDescription(ITypeSymbol typeSymbol, bool isReadOnly)
    {
        TypeSymbol = typeSymbol;
        IsReadOnly = isReadOnly;

        GeneratedFieldName = $"__{TypeSymbol.ToValidIdentifier()}_{(IsReadOnly ? "RO" : "RW")}_ComponentLookup";
    }

    public bool Equals(ComponentLookupFieldDescription other) =>
        SymbolEqualityComparer.Default.Equals(TypeSymbol, other.TypeSymbol) && IsReadOnly == other.IsReadOnly;

    public override int GetHashCode()
    {
        unchecked
        {
            return ((TypeSymbol != null ?
                SymbolEqualityComparer.Default.GetHashCode(TypeSymbol) : 0) * 397) ^ IsReadOnly.GetHashCode();
        }
    }
}
