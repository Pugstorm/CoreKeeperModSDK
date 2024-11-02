using System.CodeDom.Compiler;

namespace Unity.Entities.SourceGen.SystemGenerator.Common;

public interface IMemberWriter
{
    public void WriteTo(IndentedTextWriter writer);
}

public interface IMemberDescription
{
    string GeneratedFieldName { get; }
    void AppendMemberDeclaration(IndentedTextWriter w, bool forcePublic = false);
    string GetMemberAssignment();
}
