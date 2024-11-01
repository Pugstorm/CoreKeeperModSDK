using System;
using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common;

public readonly struct AspectLookupFieldDescription : IEquatable<AspectLookupFieldDescription>, IMemberDescription
{
    ITypeSymbol TypeSymbol { get; }
    bool IsReadOnly { get; }
    public string GeneratedFieldName { get; }

    public void AppendMemberDeclaration(IndentedTextWriter w, bool forcePublic = false)
    {
        if (IsReadOnly)
            w.Write("[global::Unity.Collections.ReadOnly] ");
        if (forcePublic)
            w.Write("public ");
        w.Write($"{TypeSymbol.ToFullName()}.Lookup {GeneratedFieldName};");
        w.WriteLine();
    }

    public string GetMemberAssignment() =>
        $@"{GeneratedFieldName} = new {TypeSymbol.ToFullName()}.Lookup(ref state);";

    public AspectLookupFieldDescription(ITypeSymbol typeSymbol, bool isReadOnly)
    {
        TypeSymbol = typeSymbol;
        IsReadOnly = isReadOnly;

        GeneratedFieldName = $"__{TypeSymbol.ToValidIdentifier()}_{(IsReadOnly ? "RO" : "RW")}_AspectLookup";
    }

    public bool Equals(AspectLookupFieldDescription other) =>
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
