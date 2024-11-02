using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator;

public static class SystemGeneratorHelper
{
    public static IEnumerable<SyntaxTree> GetSyntaxTreesWithCandidates(IEnumerable<ISystemModule> allModules)
        => allModules.SelectMany(module => module.Candidates).Select(node => node.SyntaxNode.SyntaxTree).Distinct();

    public static HashSet<TypeDeclarationSyntax> GetSystemTypesInTree(SyntaxTree syntaxTree, IEnumerable<ISystemModule> allModules)
    {
        var uniqueSystems = new HashSet<TypeDeclarationSyntax>();

        foreach (var module in allModules)
        foreach (var candidate in module.Candidates)
            if (candidate.SyntaxNode.SyntaxTree == syntaxTree)
                uniqueSystems.Add(candidate.SystemType);

        return uniqueSystems;
    }

    public static IEnumerable<ISystemModule> GetAllModulesWithCandidatesInSystemType(
        TypeDeclarationSyntax systemTypeWithCandidate, IEnumerable<ISystemModule> allModules, GeneratorExecutionContext generatorExecutionContext)
    {
        return
            allModules
                .Where(m => m.Candidates.Any(c => c.SystemType == systemTypeWithCandidate))
                .Distinct();
    }
}
