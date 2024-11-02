using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unity.Entities.SourceGen.Aspect
{
    public class AspectReceiver : ISyntaxReceiver
    {
        internal List<StructDeclarationSyntax> _AspectCandidates = new List<StructDeclarationSyntax>();
        readonly CancellationToken _cancellationToken;

        public AspectReceiver(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Find all the candidate SyntaxNode that are candidate declarations for an aspect type.
        /// The SyntaxNode must be a StructDeclarationSyntax that implement Unity.Entities.IAspect<T>
        /// </summary>
        /// <param name="syntaxNode"></param>
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (syntaxNode is StructDeclarationSyntax {BaseList: {}} structDeclaration)
            {
                foreach (var type in structDeclaration.BaseList.Types)
                {
                    if (type.Type.IsTypeNameCandidate("Unity.Entities", "IAspect"))
                    {
                        var hasPartial = false;
                        foreach (var modifier in structDeclaration.Modifiers)
                            if (modifier.IsKind(SyntaxKind.PartialKeyword))
                            {
                                hasPartial = true;
                                break;
                            }
                        if (!hasPartial)
                            return;

                        _AspectCandidates.Add(structDeclaration);
                        return;
                    }
                }
            }
        }
    }
}
