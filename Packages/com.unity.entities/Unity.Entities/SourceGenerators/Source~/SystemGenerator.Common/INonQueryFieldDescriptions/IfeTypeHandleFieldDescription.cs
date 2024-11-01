using System;
using System.CodeDom.Compiler;

namespace Unity.Entities.SourceGen.SystemGenerator.Common;

public readonly struct IfeTypeHandleFieldDescription : IEquatable<IfeTypeHandleFieldDescription>, IMemberDescription
{
    string ContainerTypeName { get; }
    public string GeneratedFieldName { get; }
    public void AppendMemberDeclaration(IndentedTextWriter w, bool forcePublic = false)
    {
        if (forcePublic)
            w.Write("public ");
        w.Write($"{ContainerTypeName}.TypeHandle {GeneratedFieldName};");
        w.WriteLine();
    }

    public string GetMemberAssignment()
        => $"{GeneratedFieldName} = new {ContainerTypeName}.TypeHandle(ref state);";

    public IfeTypeHandleFieldDescription(string containerTypeName)
    {
        ContainerTypeName = containerTypeName;
        GeneratedFieldName = $"__{containerTypeName.Replace(".", "_")}_TypeHandle";
    }

    public bool Equals(IfeTypeHandleFieldDescription other) => ContainerTypeName == other.ContainerTypeName;
    public override int GetHashCode() => ContainerTypeName != null ? ContainerTypeName.GetHashCode() : 0;
}
