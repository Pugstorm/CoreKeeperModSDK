using System;
using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common
{
    public enum QueryType
    {
        All, None, Any, ChangeFilter, Disabled, Absent, Present
    }

    public struct Query : IEquatable<Query>
    {
        public QueryType Type;
        public ITypeSymbol TypeSymbol;
        public bool IsReadOnly;

        public bool Equals(Query other) => SymbolEqualityComparer.Default.Equals(TypeSymbol, other.TypeSymbol)
                                           && IsReadOnly == other.IsReadOnly;
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = TypeSymbol != null ? SymbolEqualityComparer.Default.GetHashCode(TypeSymbol) : 0;
                hashCode = (hashCode * 397) ^ IsReadOnly.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString() =>
            IsReadOnly
                ? $"Unity.Entities.ComponentType.ReadOnly<{TypeSymbol.ToFullName()}>()"
                : $"Unity.Entities.ComponentType.ReadWrite<{TypeSymbol.ToFullName()}>()";
    }
}
