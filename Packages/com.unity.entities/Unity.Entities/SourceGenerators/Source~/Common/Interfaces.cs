using System.CodeDom.Compiler;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Unity.Entities.SourceGen.Common;

public interface ISystemCandidate
{
    public string CandidateTypeName { get; }
    public SyntaxNode Node { get; }
}

public interface IModuleSyntaxWalker
{
    bool TryWriteSyntax(IndentedTextWriter writer, CandidateSyntax candidateSyntax);
}

public interface IQueryFieldDescription
{
    public void WriteEntityQueryFieldAssignment(IndentedTextWriter writer, string generatedQueryFieldName);
    public string GetFieldDeclaration(string generatedQueryFieldName, bool forcePublic = false);
}
