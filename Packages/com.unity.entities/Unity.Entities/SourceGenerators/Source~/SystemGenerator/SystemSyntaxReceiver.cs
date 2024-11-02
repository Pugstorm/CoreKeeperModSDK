using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.SystemAPI.Query;
using Unity.Entities.SourceGen.SystemGenerator.SystemAPI.QueryBuilder;
using Unity.Entities.SourceGen.SystemGenerator.SystemAPI;
using Unity.Entities.SourceGen.SystemGenerator.Common;
using Unity.Entities.SourceGen.SystemGenerator.EntityQueryBulkOperations;
using Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;
using JobEntityModule = Unity.Entities.SourceGen.JobEntityGenerator.JobEntityModule;

namespace Unity.Entities.SourceGen.SystemGenerator;

public class SystemSyntaxReceiver : ISyntaxReceiver
{
    internal readonly HashSet<TypeDeclarationSyntax> ISystemDefinedAsClass = new();

    readonly Dictionary<SyntaxNode, CandidateSyntax> _markedNodes = new();
    readonly CancellationToken _cancellationToken;

    internal Dictionary<TypeDeclarationSyntax, Dictionary<SyntaxNode, CandidateSyntax>> CandidateNodesGroupedBySystemType
        => _markedNodes
            .GroupBy(n => n.Key.AncestorOfKind<TypeDeclarationSyntax>())
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

    internal IReadOnlyCollection<ISystemModule> SystemModules { get; }

    public SystemSyntaxReceiver(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        SystemModules = new ISystemModule[]
        {
            new LambdaJobsModule(),
            new JobEntityModule(),
            new EntityQueryModule(),
            new IfeModule(),
            new SystemContextSystemModule(),
            new SystemApiQueryBuilderModule()
        };
    }

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (IsValidSystemType(syntaxNode))
        {
            foreach (var module in SystemModules)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                module.OnReceiveSyntaxNode(syntaxNode, _markedNodes);
            }
        }

        _cancellationToken.ThrowIfCancellationRequested();
    }

    bool IsValidSystemType(SyntaxNode syntaxNode)
    {
        if (syntaxNode is TypeDeclarationSyntax typeSyntax)
        {
            if (typeSyntax.BaseList == null)
                return false;

            var hasPartial = false;
            foreach (var modifier in typeSyntax.Modifiers)
                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                {
                    hasPartial = true;
                    break;
                }

            if (!hasPartial)
                return false;

            // error if class ISystem
            // this can also be an analyzer with codefix in future
            if (syntaxNode is ClassDeclarationSyntax && BaseListContains(typeSyntax, "ISystem"))
            {
                ISystemDefinedAsClass.Add(typeSyntax);
                return false;
            }
        }
        return true;
    }

    static bool BaseListContains(TypeDeclarationSyntax typeSyntax, string typeOrInterfaceName)
    {
        Debug.Assert(typeSyntax.BaseList != null, "typeSyntax.BaseList != null");
        foreach (var t in typeSyntax.BaseList.Types)
        {
            if (t.Type.ToString().Split('.').Last() == typeOrInterfaceName)
                return true;
        }
        return false;
    }
}
