using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unity.Entities.SourceGen.Aspect
{
    public static class SyntaxNodeExt
    {
        /// <summary>
        /// Reduce to the subset of all the SyntaxNode of a matching SyntaxKind
        /// </summary>
        /// <param name="syntaxNode"></param>
        /// <param name="kind"></param>
        /// <returns></returns>
        static IEnumerable<SyntaxNode> OfKind(this IEnumerable<SyntaxNode> syntaxNode, SyntaxKind kind) => syntaxNode.Where(x => x.IsKind(kind));

        /// <summary>
        /// Reduce to the subset of all the SyntaxToken of a matching SyntaxKind
        /// </summary>
        /// <param name="syntaxToken"></param>
        /// <param name="kind"></param>
        /// <returns></returns>
        static IEnumerable<SyntaxToken> OfKind(this IEnumerable<SyntaxToken> syntaxToken, SyntaxKind kind) => syntaxToken.Where(x => x.IsKind(kind));

        /// <summary>
        /// Return if a SyntaxNode has a child token of a given SyntaxKind
        /// </summary>
        /// <param name="syntaxNode"></param>
        /// <param name="kind"></param>
        /// <returns></returns>
        public static bool HasTokenOfKind(this SyntaxNode syntaxNode, SyntaxKind kind) => syntaxNode.ChildTokens().OfKind(kind).Any();

        /// <summary>
        /// Try to retrieve the first child of this SyntaxNode that is of a given SyntaxKind
        /// </summary>
        /// <param name="syntaxNode">This SyntaxNode</param>
        /// <param name="kind">The SyntaxKind to look for</param>
        /// <param name="child">The first child of matching syntax kind if found</param>
        /// <returns>true if found. false if not.</returns>
        static bool TryGetFirstChildByKind(this SyntaxNode syntaxNode, SyntaxKind kind, out SyntaxNode child) => (child = syntaxNode.ChildNodes().OfKind(kind).FirstOrDefault()) != null;

        /// <summary>
        /// Test if a node represent an identifier by comparing string followed
        /// by a TypeArgumentListSyntax, which is returned through typeArgumentListSyntax.
        /// </summary>
        /// <param name="syntaxNode"></param>
        /// <param name="identifier"></param>
        /// <param name="typeArgumentListSyntax"></param>
        /// <returns></returns>
        static bool IsIdentifier(this SyntaxNode syntaxNode, string identifier, out TypeArgumentListSyntax typeArgumentListSyntax)
        {
            switch (syntaxNode)
            {
                case GenericNameSyntax genericNameSyntax:
                    typeArgumentListSyntax = genericNameSyntax.TypeArgumentList;
                    return genericNameSyntax.Identifier.ValueText == identifier;
                case SimpleNameSyntax simpleNameSyntax:
                    typeArgumentListSyntax = default;
                    return simpleNameSyntax.Identifier.ValueText == identifier;
            }
            typeArgumentListSyntax = default;
            return false;
        }

        /// <summary>
        /// Figures out as fast as possible if the syntax node does not represent a type name.
        /// Use for early-out tests within the OnVisitSyntaxNode calls.
        /// Use the SemanticModel from GeneratorExecutionContext.Compilation.GetSemanticModel() to get an accurate result during the Execute call.
        ///
        /// Returns false if the node is found to *not* be equal using fast early-out tests.
        /// Returns true if type name is likely equal.
        /// </summary>
        /// <param name="syntaxNode"></param>
        /// <param name="typeNameNamespace">The host namespace of the type name. e.g. "Unity.Entities"</param>
        /// <param name="typeName">The unqualified type name of the generic type. e.g. "Entity" </param>
        /// <returns></returns>
        public static bool IsTypeNameCandidate(this SyntaxNode syntaxNode, string typeNameNamespace, string typeName) => IsTypeNameCandidate(syntaxNode, typeNameNamespace, typeName, out var typeArgumentListSyntax);

        /// <summary>
        /// Figures out as fast as possible if the syntax node does not represent a type name.
        /// Use for early-out tests within the OnVisitSyntaxNode calls.
        /// Use the SemanticModel from GeneratorExecutionContext.Compilation.GetSemanticModel() to get an accurate result during the Execute call.
        ///
        /// Returns false if the node is found to *not* be equal using fast early-out tests.
        /// Returns true if type name is likely equal.
        /// </summary>
        /// <param name="syntaxNode"></param>
        /// <param name="typeNameNamespace">The host namespace of the type name. e.g. "Unity.Entities"</param>
        /// <param name="typeName">The unqualified type name of the generic type. e.g. "Entity" </param>
        /// <param name="typeArgumentListSyntax">output the TypeArgumentListSyntax node if the type represented by this SyntaxNode is generic</param>
        /// <returns></returns>
        static bool IsTypeNameCandidate(this SyntaxNode syntaxNode, string typeNameNamespace, string typeName, out TypeArgumentListSyntax typeArgumentListSyntax)
        {
            switch (syntaxNode)
            {
                case QualifiedNameSyntax qualifiedNameSyntax:
                    // Fast estimate right part and extract a possible TypeArgumentListSyntax to our own TypeArgumentListSyntax output
                    if (!IsIdentifier(qualifiedNameSyntax.Right, typeName, out typeArgumentListSyntax))
                    {
                        return false;
                    }
                    var iLastDot = typeNameNamespace.LastIndexOf('.');
                    if (iLastDot < 0)
                    {
                        //End of qualified names
                        var typename = qualifiedNameSyntax.Left.ToString();
                        if (typename.StartsWith("global::")) typename = typename.Substring(8);
                        typeArgumentListSyntax = default;
                        return typename == typeNameNamespace;
                    }

                    if (qualifiedNameSyntax.Left != null)
                    {
                        // Fast estimate left part without extracting any TypeArgumentListSyntax
                        return qualifiedNameSyntax.Left.IsTypeNameCandidate(typeNameNamespace.Substring(0, iLastDot), typeNameNamespace.Substring(iLastDot + 1));
                    }

                    // Limit the test here, any remaining qualified name is assumed to be a known scope. e.g. part of a using statement or other type defined withing the same unit.
                    return true;
                default:
                    // Check if current node is the identifier symbolName
                    // and if the current node's scope knows of the scope name symbolNamesapce
                    return IsIdentifier(syntaxNode, typeName, out typeArgumentListSyntax);
            }
        }

        /// <summary>
        /// Figures out as fast as possible if the node has an attribute that may be equal the to string provided.
        /// Use for early-out tests within the OnVisitSyntaxNode calls.
        /// Use the SemanticModel from GeneratorExecutionContext.Compilation.GetSemanticModel() to get an accurate result during the Execute call.
        ///
        /// Returns false if no attribute is likely equal using fast early-out tests.
        /// Returns true if an attribute is likely equal.
        /// </summary>
        /// <param name="syntaxNode">Node to test type name against</param>
        /// <param name="attributeNameSpace">The host namespace of the attribute type name. e.g. "Unity.Entities"</param>
        /// <param name="attributeName">The unqualified attribute name. e.g. "UpdateBefore" </param>
        /// <returns></returns>
        public static bool HasAttributeCandidate(this SyntaxNode syntaxNode, string attributeNameSpace, string attributeName)
        {
            if (syntaxNode.TryGetFirstChildByKind(SyntaxKind.AttributeList, out var fieldAttributeList)
                && fieldAttributeList.TryGetFirstChildByKind(SyntaxKind.Attribute, out var fieldAttribute))
            {
                var attribute = fieldAttribute as AttributeSyntax;
                if (attribute.Name.IsTypeNameCandidate(attributeNameSpace, attributeName))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
