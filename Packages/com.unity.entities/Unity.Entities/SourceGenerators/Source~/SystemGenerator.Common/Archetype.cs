using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common;

public readonly struct Archetype : IEquatable<Archetype>
{
    public readonly EntityQueryOptions Options;

    // Aspect types may be present in the All collection
    public readonly IReadOnlyCollection<Query> All;
    public readonly IReadOnlyCollection<Query> Any;
    public readonly IReadOnlyCollection<Query> None;
    public readonly IReadOnlyCollection<Query> Disabled;
    public readonly IReadOnlyCollection<Query> Absent;
    public readonly IReadOnlyCollection<Query> Present;

    public Archetype(
        IReadOnlyCollection<Query> all,
        IReadOnlyCollection<Query> any,
        IReadOnlyCollection<Query> none,
        IReadOnlyCollection<Query> disabled,
        IReadOnlyCollection<Query> absent,
        IReadOnlyCollection<Query> present,
        EntityQueryOptions options = default)
    {
        All = all;
        Any = any;
        None = none;
        Disabled = disabled;
        Absent = absent;
        Present = present;
        Options = options;
    }

    public bool Equals(Archetype other) =>
        Options == other.Options
        && All.SequenceEqual(other.All)
        && Any.SequenceEqual(other.Any)
        && None.SequenceEqual(other.None)
        && Disabled.SequenceEqual(other.Disabled)
        && Present.SequenceEqual(other.Present)
        && Absent.SequenceEqual(other.Absent);
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 19;

            foreach (var all in All)
                hash = hash * 31 + all.GetHashCode();
            foreach (var any in Any)
                hash = hash * 31 + any.GetHashCode();
            foreach (var none in None)
                hash = hash * 31 + none.GetHashCode();
            foreach (var disabled in Disabled)
                hash = hash * 31 + disabled.GetHashCode();
            foreach (var absent in Absent)
                hash = hash * 31 + absent.GetHashCode();
            foreach (var present in Present)
                hash = hash * 31 + present.GetHashCode();
            hash = hash * 31 + ((int)Options).GetHashCode();

            return hash;
        }
    }
}
