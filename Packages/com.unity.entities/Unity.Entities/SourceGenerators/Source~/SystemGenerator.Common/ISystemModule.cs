using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common
{
    public interface ISystemModule
    {
        IEnumerable<(SyntaxNode SyntaxNode, TypeDeclarationSyntax SystemType)> Candidates { get; }
        bool RequiresReferenceToBurst { get; }

        void OnReceiveSyntaxNode(SyntaxNode node, Dictionary<SyntaxNode, CandidateSyntax> candidateOwnership);
        bool RegisterChangesInSystem(SystemDescription systemDescription);
    }
}
