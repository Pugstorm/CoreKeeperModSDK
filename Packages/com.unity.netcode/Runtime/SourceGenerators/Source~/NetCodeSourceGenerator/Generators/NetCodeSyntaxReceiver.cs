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

                if (structNode.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
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
                    foreach (var b in structNode.BaseList.Types)
                    {
                        var interfaceType = b.Type;
                        //Grab the Indentifier  and discard the qualifcation
                        if (interfaceType.IsKind(SyntaxKind.QualifiedName))
                            interfaceType = ((QualifiedNameSyntax) interfaceType).Right;

                        //Don't consider generic interface has candidate
                        if (interfaceType.IsKind(SyntaxKind.GenericName))
                            continue;
                        if (!interfaceType.IsKind(SyntaxKind.IdentifierName))
                            continue;

                        //The biggest limitation of these checks during syntax traversal is that
                        //we can't detect if a type is an Rpc,Command,Component or Buffers if the struct
                        //has an interface that inherit/derive from one of base entities or net-code ones.
                        //ex:
                        // interface KuKu : IComponentData
                        // {}
                        //
                        // struct WhoAmI : KuKu {
                        // ..
                        // }
                        // Technically WhoAmI is a IComponentData but the following test whould not recognize that.
                        // Is not possible to make an interface sealed (like a class) so there is not way to prevent
                        // that.
                        // In order to be 100% sure to catch all possible use cases, an unknownCandidate array is populate
                        // with all the struct witch have at least one element in the baselist.
                        // That is unfornate, but without strong guarantee this is necessary. If the user respect the
                        // guidelines, the checks will always works and less works is necessary. If they don't,
                        // at least we are providing consistent logic, at the expenses of more costly checks.
                        Candidates.Add(syntaxNode);
                        break;
                    }
                }
            }
        }
    }
}
