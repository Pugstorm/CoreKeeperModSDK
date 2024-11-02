using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Unity.Entities.Analyzer
{
    static class OperationExtensions
    {
        public static string GetVarTypeName(this IVariableDeclaratorOperation declarator)
        {
            if (declarator.Syntax.Parent is VariableDeclarationSyntax variableDeclaration)
                if (variableDeclaration.Type is RefTypeSyntax refTypeSyntax)
                    return refTypeSyntax.Type.ToString();
                else
                    return variableDeclaration.Type.ToString();
            return declarator.Symbol.Type.Name;
        }
    }
}
