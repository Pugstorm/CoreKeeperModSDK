#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.JobEntityGenerator;

public partial class JobEntityModule : ISystemModule
{
    readonly Dictionary<TypeDeclarationSyntax, List<JobEntityCandidate>> _jobEntityInvocationCandidates = new();
    static readonly ImmutableHashSet<string> ScheduleModes = Enum.GetNames(typeof(ScheduleMode)).ToImmutableHashSet();

    public IEnumerable<(SyntaxNode SyntaxNode, TypeDeclarationSyntax SystemType)> Candidates
    {
        get
        {
            foreach (var kvp in _jobEntityInvocationCandidates)
            foreach (var candidate in kvp.Value)
                yield return (candidate.Node, kvp.Key);
        }
    }
    public bool RequiresReferenceToBurst => true;

    internal readonly struct JobEntityCandidate : ISystemCandidate
    {
        public MemberAccessExpressionSyntax MemberAccessExpressionSyntax { get; }
        public JobEntityCandidate(MemberAccessExpressionSyntax node) => MemberAccessExpressionSyntax = node;
        public string CandidateTypeName => "IJobEntity";
        public SyntaxNode Node => MemberAccessExpressionSyntax;
        public InvocationExpressionSyntax Invocation => (MemberAccessExpressionSyntax.Parent as InvocationExpressionSyntax)!;
    }

    public void OnReceiveSyntaxNode(SyntaxNode node, Dictionary<SyntaxNode, CandidateSyntax> candidateOwnership)
    {
        if (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccessExpressionSyntax }
            && memberAccessExpressionSyntax.Kind() == SyntaxKind.SimpleMemberAccessExpression
            && memberAccessExpressionSyntax.Expression is IdentifierNameSyntax or ObjectCreationExpressionSyntax)
        {
            var schedulingMethodName = memberAccessExpressionSyntax.Name.Identifier.ValueText;

            if (ScheduleModes.Contains(schedulingMethodName))
            {
                var containingType = node.AncestorOfKind<TypeDeclarationSyntax>();

                // Discard if no base type, meaning it can't possible inherit from a System
                if (containingType.BaseList == null || containingType.BaseList.Types.Count == 0)
                    return;

                _jobEntityInvocationCandidates.Add(containingType, new JobEntityCandidate(memberAccessExpressionSyntax));
            }
        }
    }

    public bool RegisterChangesInSystem(SystemDescription systemDescription)
    {
        var candidates = _jobEntityInvocationCandidates[systemDescription.SystemTypeSyntax];
        var validCandidates = new List<JobEntityInstanceInfo>(candidates.Count);

        foreach (var candidate in candidates)
            if (JobEntityInstanceInfo.TryCreate(ref systemDescription, candidate, out var knownJobEntityInfo))
            {
                validCandidates.Add(knownJobEntityInfo);
                systemDescription.CandidateNodes.Add(
                    key: candidate.Invocation,
                    value: new CandidateSyntax(CandidateType.IJobEntity, CandidateFlags.None, candidate.Invocation));
            }

        if (validCandidates.Count > 0)
            systemDescription.SyntaxWalkers.Add(Module.IJobEntity, new IjeSchedulingSyntaxWalker(ref systemDescription, validCandidates));

        return validCandidates.Count > 0;
    }
}
