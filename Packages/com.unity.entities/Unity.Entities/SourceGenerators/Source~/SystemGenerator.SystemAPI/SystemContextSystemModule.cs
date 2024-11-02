using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI;

public class SystemContextSystemModule : ISystemModule
{
    readonly Dictionary<TypeDeclarationSyntax, List<CandidateSyntax>> m_Candidates = new Dictionary<TypeDeclarationSyntax, List<CandidateSyntax>>();

    public IEnumerable<(SyntaxNode SyntaxNode, TypeDeclarationSyntax SystemType)> Candidates
    {
        get {
            foreach (var type in m_Candidates) {
                foreach (var candidate in type.Value) {
                    yield return (candidate.Node, type.Key);
                }
            }
        }
    }

    public bool RequiresReferenceToBurst => false;

    public void OnReceiveSyntaxNode(SyntaxNode node, Dictionary<SyntaxNode, SourceGen.Common.CandidateSyntax> candidateOwnership)
    {
        switch (node)
        {
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: { } nameSyntax } } invocation: // makes sure to use SystemAPI.*** as the reference instead of *** as the SystemAPI part should also dissapear
                InvocationWithNameOrMember(invocation, nameSyntax);
                break;
            case InvocationExpressionSyntax { Expression: SimpleNameSyntax nameSyntax } invocation:
                InvocationWithNameOrMember(invocation, nameSyntax);
                break;
            case IdentifierNameSyntax nameSyntax:
                PropertyWithNameOrMember(nameSyntax);
                break;
        }

        void PropertyWithNameOrMember(SimpleNameSyntax nameSyntax)
        {
            switch (nameSyntax.Identifier.ValueText)
            {
                case "Time":
                    var systemTypeSyntax = nameSyntax.AncestorOfKindOrDefault<TypeDeclarationSyntax>();
                    if (systemTypeSyntax != null)
                    {
                        SyntaxNode candidateNode;
                        if (nameSyntax.Parent is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
                        {
                            // E.g. In the case of "Time.DeltaTime", "Time" is the expression and what we want to replace
                            if (memberAccessExpressionSyntax.Expression == nameSyntax)
                                candidateNode = nameSyntax;
                            else
                                // E.g. In the case of "SystemAPI.Time", we want to replace the entire expression
                                candidateNode = memberAccessExpressionSyntax;
                        }
                        else
                            // E.g. In the case of "Time" (where SystemAPI is not explicitly written), we want to replace the entire expression
                            candidateNode = nameSyntax;

                        var candidateSyntax = new CandidateSyntax(CandidateType.TimeData, CandidateFlags.None, candidateNode);
                        candidateOwnership[candidateNode] = candidateSyntax;
                        m_Candidates.Add(systemTypeSyntax, candidateSyntax);
                    }
                    break;
            }
        }

        void InvocationWithNameOrMember(InvocationExpressionSyntax invocation, SimpleNameSyntax nodeContainedByInvocation) {
            switch (nodeContainedByInvocation.Identifier.ValueText) {
                // Component
                case "GetComponentLookup":
                    AddCandidate(CandidateFlags.None, CandidateType.GetComponentLookup);
                    break;
                case "GetComponent":
                    AddCandidate(CandidateFlags.None, CandidateType.GetComponent);
                    break;
                case "GetComponentRO":
                    AddCandidate(CandidateFlags.None, CandidateType.GetComponentRO);
                    break;
                case "GetComponentRW":
                    AddCandidate(CandidateFlags.None, CandidateType.GetComponentRW);
                    break;
                case "SetComponent":
                    AddCandidate(CandidateFlags.None, CandidateType.SetComponent);
                    break;
                case "HasComponent":
                    AddCandidate(CandidateFlags.None, CandidateType.HasComponent);
                    break;
                case "IsComponentEnabled":
                    AddCandidate(CandidateFlags.None, CandidateType.IsComponentEnabled);
                    break;
                case "SetComponentEnabled":
                    AddCandidate(CandidateFlags.None, CandidateType.SetComponentEnabled);
                    break;

                // Buffer
                case "GetBufferLookup":
                    AddCandidate(CandidateFlags.None, CandidateType.GetBufferLookup);
                    break;
                case "GetBuffer":
                    AddCandidate(CandidateFlags.None, CandidateType.GetBuffer);
                    break;
                case "HasBuffer":
                    AddCandidate(CandidateFlags.None, CandidateType.HasBuffer);
                    break;
                case "IsBufferEnabled":
                    AddCandidate(CandidateFlags.None, CandidateType.IsBufferEnabled);
                    break;
                case "SetBufferEnabled":
                    AddCandidate(CandidateFlags.None, CandidateType.SetBufferEnabled);
                    break;

                // StorageInfo/Exists
                case "GetEntityStorageInfoLookup":
                    AddCandidate(CandidateFlags.None, CandidateType.GetEntityStorageInfoLookup);
                    break;
                case "Exists":
                    AddCandidate(CandidateFlags.None, CandidateType.Exists);
                    break;

                // Singleton
                case "GetSingleton":
                    AddCandidate(CandidateFlags.ReadOnly, CandidateType.SingletonWithoutArgument);
                    break;
                case "GetSingletonEntity":
                    AddCandidate(CandidateFlags.ReadOnly | CandidateFlags.NoGenericGeneration, CandidateType.SingletonWithoutArgument);
                    break;
                case "SetSingleton":
                    AddCandidate(CandidateFlags.None, CandidateType.SingletonWithArgument);
                    break;
                case "GetSingletonRW":
                    AddCandidate(CandidateFlags.None, CandidateType.SingletonWithoutArgument);
                    break;
                case "TryGetSingletonRW":
                    AddCandidate(CandidateFlags.None, CandidateType.SingletonWithArgument);
                    break;
                case "TryGetSingletonBuffer":
                    AddCandidate(CandidateFlags.None, CandidateType.SingletonWithArgument);
                    break;
                case "TryGetSingletonEntity":
                    AddCandidate(CandidateFlags.ReadOnly, CandidateType.SingletonWithArgument);
                    break;
                case "GetSingletonBuffer":
                    AddCandidate(CandidateFlags.None, CandidateType.SingletonWithoutArgument);
                    break;
                case "TryGetSingleton":
                    AddCandidate(CandidateFlags.ReadOnly, CandidateType.SingletonWithArgument);
                    break;
                case "HasSingleton":
                    AddCandidate(CandidateFlags.ReadOnly, CandidateType.SingletonWithoutArgument);
                    break;

                // Aspect
                case "GetAspect":
                    AddCandidate(CandidateFlags.None, CandidateType.Aspect);
                    break;

                // TypeHandle
                case "GetEntityTypeHandle":
                    AddCandidate(CandidateFlags.None, CandidateType.EntityTypeHandle);
                    break;
                case "GetComponentTypeHandle":
                    AddCandidate(CandidateFlags.None, CandidateType.ComponentTypeHandle);
                    break;
                case "GetBufferTypeHandle":
                    AddCandidate(CandidateFlags.None, CandidateType.BufferTypeHandle);
                    break;
                case "GetSharedComponentTypeHandle":
                    AddCandidate(CandidateFlags.None, CandidateType.SharedComponentTypeHandle);
                    break;

                    void AddCandidate(CandidateFlags flags, CandidateType type) {
                        var candidateSyntax = new CandidateSyntax(type, flags, invocation);
                        candidateOwnership[invocation] = candidateSyntax;

                        m_Candidates.Add(
                            nodeContainedByInvocation.AncestorOfKind<TypeDeclarationSyntax>(),
                            candidateSyntax);
                    }
            }
        }
    }

    public bool RegisterChangesInSystem(SystemDescription desc)
    {
        if (!m_Candidates.ContainsKey(desc.SystemTypeSyntax))
            return false;

        desc.SyntaxWalkers.Add(Module.SystemApiContext, new SystemApiContextSyntaxWalker(desc));
        return true;
    }
}
