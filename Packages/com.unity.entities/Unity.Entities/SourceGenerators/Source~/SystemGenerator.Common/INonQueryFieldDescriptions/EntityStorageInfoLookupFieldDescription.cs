using System;
using System.CodeDom.Compiler;

namespace Unity.Entities.SourceGen.SystemGenerator.Common;

public struct EntityStorageInfoLookupFieldDescription : IEquatable<EntityStorageInfoLookupFieldDescription>, IMemberDescription
{
    public string GeneratedFieldName => "__EntityStorageInfoLookup";
    public void AppendMemberDeclaration(IndentedTextWriter w, bool forcePublic = false)
    {
        w.Write("[global::Unity.Collections.ReadOnly] ");
        if (forcePublic)
            w.Write("public ");
        w.Write($"Unity.Entities.EntityStorageInfoLookup {GeneratedFieldName};");
        w.WriteLine();
    }
    public string GetMemberAssignment() =>
        $@"{GeneratedFieldName} = state.GetEntityStorageInfoLookup();";
    public bool Equals(EntityStorageInfoLookupFieldDescription other) => true;
    public override int GetHashCode() => 0;
}
