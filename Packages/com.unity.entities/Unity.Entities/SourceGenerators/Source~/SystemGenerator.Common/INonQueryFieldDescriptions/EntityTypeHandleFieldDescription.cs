using System;
using System.CodeDom.Compiler;

namespace Unity.Entities.SourceGen.SystemGenerator.Common;

public readonly struct EntityTypeHandleFieldDescription : IMemberDescription, IEquatable<EntityTypeHandleFieldDescription>
{
    public string GeneratedFieldName => "__Unity_Entities_Entity_TypeHandle";
    public void AppendMemberDeclaration(IndentedTextWriter w, bool forcePublic = false)
    {
        w.Write("[global::Unity.Collections.ReadOnly] ");
        if (forcePublic)
            w.Write("public ");
        w.Write($"global::Unity.Entities.EntityTypeHandle {GeneratedFieldName};");
        w.WriteLine();
    }
    public string GetMemberAssignment() => $@"{GeneratedFieldName} = state.GetEntityTypeHandle();";
    public bool Equals(EntityTypeHandleFieldDescription other) => true;
    public override int GetHashCode() => GeneratedFieldName.GetHashCode();
}
