using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unity.NetCode.Generators
{
    internal class NetCodeSyntaxReceiver : ISyntaxReceiver
    {
        readonly public List<SyntaxNode> Variants;
        readonly public List<SyntaxNode> Candidates;

        public NetCodeSyntaxReceiver()
        {
            Variants = new List<SyntaxNode>();
            Candidates = new List<SyntaxNode>();
        }

        ///<summary>
        /// Analyze the all the nodes and build a list of candidates
        /// for rcps, commands and components
        /// The minimal requirement for a type to be considered a potential candidates are:
        /// - Must be a struct
        /// - The struct declaration must be public
        /// - Must implement either RpcCommandData, ICommandData, ComponentData or IBufferElementData
        /// There are no check at that level for the ghost fields since it would require the ghost modifiers that
        /// aren't available yet (until we can finally use a real config file in the unity editor that make the workflow cohese)
        ///
        /// The check is a little limited at the moment since we can't test here for interface inheritance at syntax level. Witch means that,
        /// with the current logic, we can't detect the type/category of a component if they implement an interface that inherit from IBufferElementData
        /// or IComponentData.
        ///
        /// This is quite limiting and may be improved by just just collecting the structs that have at least one interface here
        /// and do the proper checks via semantic model in the second pass. The code for doing that is pretty straightforward since all the utility are
        /// present.
        ///</summary>
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            using (new Profiler.Auto("OnVisitSyntaxNode"))
            {
                if (!(syntaxNode is StructDeclarationSyntax))
                {
                    // The node must be either a struct, or a class with a [GhostComponent] attribute
                    if (!(syntaxNode is ClassDeclarationSyntax))
                        return;
                    if (!ComponentFactory.HasGhostComponentAttribute((TypeDeclarationSyntax)syntaxNode))
                        return;
                }

                var structNode = (TypeDeclarationSyntax) syntaxNode;
                
                if(structNode.TypeParameterList != null)
                    return;

                //Check for Variant attributes
                if (structNode.AttributeLists.Count > 0)
                {
                    var attributes = structNode.AttributeLists.SelectMany(list => list.Attributes.Select(a =>
                            (a.Name.IsKind(SyntaxKind.QualifiedName) ? ((QualifiedNameSyntax) a.Name).Right : a.Name)
                            .ToString()));

                    if (attributes.Any(attr => attr == "GhostComponentVariation" || attr == "GhostComponentVariationAttribute"))
                    {
                        Variants.Add(structNode);
                        return;
                    }
                }

                if (structNode.BaseList == null || structNode.BaseList.Types.Count == 0)
                    return;

                using (new Profiler.Auto("Collect"))
                {
                    bool shouldAddType = true;
                    foreach (var b in structNode.BaseList.Types)
                    {
                        var interfaceType = b.Type;
                        //discard qualification
                        if(interfaceType.IsKind(SyntaxKind.QualifiedName))
                            interfaceType = ((QualifiedNameSyntax)interfaceType).Right;

                        if (interfaceType.IsKind(SyntaxKind.GenericName))
                        {
                            if (((GenericNameSyntax)interfaceType).TypeArgumentList.Arguments.Count == 0)
                            {
                                shouldAddType = false;
                                break;
                            }
                        }
                        else if (!interfaceType.IsKind(SyntaxKind.IdentifierName))
                        {
                            shouldAddType = false;
                            break;
                        }
                    }
                    if(shouldAddType)
                        Candidates.Add(structNode);
                }
            }
        }
    }
}
