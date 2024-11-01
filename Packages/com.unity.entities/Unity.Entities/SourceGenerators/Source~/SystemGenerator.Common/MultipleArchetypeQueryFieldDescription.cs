using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.Common;

public readonly struct MultipleArchetypeQueryFieldDescription : IEquatable<MultipleArchetypeQueryFieldDescription>, IQueryFieldDescription
{
    readonly IReadOnlyCollection<Archetype> _archetypes;
    readonly string _entityQueryBuilderBodyBeforeBuild;

    public MultipleArchetypeQueryFieldDescription(IReadOnlyCollection<Archetype> archetypes, string entityQueryBuilderBodyBeforeBuild)
    {
        _archetypes = archetypes;
        _entityQueryBuilderBodyBeforeBuild = entityQueryBuilderBodyBeforeBuild;
    }

    public void WriteEntityQueryFieldAssignment(IndentedTextWriter writer, string generatedQueryFieldName)
    {
        writer.WriteLine($"{generatedQueryFieldName} = entityQueryBuilder{_entityQueryBuilderBodyBeforeBuild}.Build(ref state);");
        writer.WriteLine("entityQueryBuilder.Reset();");
    }

    public string GetFieldDeclaration(string generatedQueryFieldName, bool forcePublic = false)
        => $"{(forcePublic ? "public" : "")} Unity.Entities.EntityQuery {generatedQueryFieldName};";

    public bool Equals(MultipleArchetypeQueryFieldDescription other)
        => _archetypes.SequenceEqual(other._archetypes);

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
            return false;

        return obj.GetType() == GetType() && Equals((MultipleArchetypeQueryFieldDescription)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 19;

            foreach (var archetype in _archetypes)
                hash = hash * 31 + archetype.GetHashCode();

            return hash;
        }
    }

    public static bool operator ==(MultipleArchetypeQueryFieldDescription left, MultipleArchetypeQueryFieldDescription right) => Equals(left, right);
    public static bool operator !=(MultipleArchetypeQueryFieldDescription left, MultipleArchetypeQueryFieldDescription right) => !Equals(left, right);
}
