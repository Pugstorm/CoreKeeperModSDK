using System;
using System.Text;

namespace Unity.Entities.SourceGen.Common;

[Flags]
public enum EntityQueryOptions
{
    Default = 0,
    IncludePrefab = 1,
    IncludeDisabledEntities = 2,
    FilterWriteGroup = 4,
    IgnoreComponentEnabledState = 8,
    IncludeSystems = 16,
}


public static class EntityQueryOptionsExtensions
{

    public static string GetAsFlagStringSeperatedByOr(this EntityQueryOptions options)
    {
        var orBuilder = new StringBuilder();
        for (var bitIndex = 0; bitIndex < 32; bitIndex++)
        {
            var currentBit = (EntityQueryOptions)(1<<bitIndex) & options;
            if (currentBit != 0)
            {
                if (orBuilder.Length!=0)
                    orBuilder.Append('|');
                orBuilder.Append($"global::Unity.Entities.EntityQueryOptions.{currentBit.ToString()}");
            }
        }
        return orBuilder.ToString();
    }
}
