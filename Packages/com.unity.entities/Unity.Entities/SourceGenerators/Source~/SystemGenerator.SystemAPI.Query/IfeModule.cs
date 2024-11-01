using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;
using static Unity.Entities.SourceGen.SystemGenerator.Common.QueryVerification;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.Query;

public class IfeModule : ISystemModule
{
    private readonly List<QueryCandidate> _queryCandidates = new();
    private Dictionary<TypeDeclarationSyntax, QueryCandidate[]> _candidatesGroupedByContainingSystemTypes;

    private Dictionary<TypeDeclarationSyntax, QueryCandidate[]> CandidatesGroupedByContainingSystemTypes
    {
        get
        {
            if (_candidatesGroupedByContainingSystemTypes == null)
            {
                _candidatesGroupedByContainingSystemTypes =
                    _queryCandidates
                        .GroupBy(c => c.ContainingTypeNode)
                        .ToDictionary(group => group.Key, group => group.ToArray());
            }
            return _candidatesGroupedByContainingSystemTypes;
        }
    }

    public IEnumerable<(SyntaxNode SyntaxNode, TypeDeclarationSyntax SystemType)> Candidates
        => _queryCandidates.Select(candidate => (SyntaxNode: candidate.FullInvocationChainSyntaxNode, ContainingType: candidate.ContainingTypeNode));

    public bool RequiresReferenceToBurst { get; private set; }

    public void OnReceiveSyntaxNode(SyntaxNode node, Dictionary<SyntaxNode, CandidateSyntax> candidateOwnership)
    {
        if (node is InvocationExpressionSyntax invocationExpressionSyntax)
        {
            switch (invocationExpressionSyntax.Expression)
            {
                case MemberAccessExpressionSyntax memberAccessExpressionSyntax:
                {
                    switch (memberAccessExpressionSyntax.Name)
                    {
                        case GenericNameSyntax { Identifier: { ValueText: "Query" } } genericNameSyntax:
                        {
                            var fullInvocationChainSyntaxNode = invocationExpressionSyntax.Ancestors().OfType<InvocationExpressionSyntax>().LastOrDefault() ?? invocationExpressionSyntax;

                            var candidate = QueryCandidate.From(fullInvocationChainSyntaxNode, genericNameSyntax.TypeArgumentList.Arguments);
                            _queryCandidates.Add(candidate);

                            var candidateSyntax = new CandidateSyntax(CandidateType.Ife, CandidateFlags.None, fullInvocationChainSyntaxNode);
                            candidateOwnership[fullInvocationChainSyntaxNode] = candidateSyntax;
                            break;
                        }
                    }
                    break;
                }
                case GenericNameSyntax { Identifier: { ValueText: "Query" } } genericNameSyntax:
                {
                    var fullInvocationChainSyntaxNode = invocationExpressionSyntax.Ancestors().OfType<InvocationExpressionSyntax>().LastOrDefault() ?? invocationExpressionSyntax;

                    var candidate = QueryCandidate.From(fullInvocationChainSyntaxNode, genericNameSyntax.TypeArgumentList.Arguments);
                    _queryCandidates.Add(candidate);

                    var candidateSyntax = new CandidateSyntax(CandidateType.Ife, CandidateFlags.None, fullInvocationChainSyntaxNode);
                    candidateOwnership[fullInvocationChainSyntaxNode] = candidateSyntax;
                    break;
                }
            }
        }
    }

    public bool RegisterChangesInSystem(SystemDescription systemDescription)
    {
        var idiomaticCSharpForEachDescriptions = new List<IfeDescription>();
        foreach (var queryCandidate in CandidatesGroupedByContainingSystemTypes[systemDescription.SystemTypeSyntax])
        {
            var description = new IfeDescription(systemDescription, queryCandidate, idiomaticCSharpForEachDescriptions.Count);

            description.Success &=
                VerifyQueryTypeCorrectness(description.SystemDescription, description.Location, description.AllQueryTypes, invokedMethodName: "WithAll");
            description.Success &=
                VerifyQueryTypeCorrectness(description.SystemDescription, description.Location, description.NoneQueryTypes, invokedMethodName: "WithNone");
            description.Success &=
                VerifyQueryTypeCorrectness(description.SystemDescription, description.Location, description.AnyQueryTypes, invokedMethodName: "WithAny");

            description.Success &=
                VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.AbsentQueryTypes, description.PresentQueryTypes, "WithAbsent", "WithPresent");
            description.Success &=
                VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.NoneQueryTypes, description.PresentQueryTypes, "WithNone", "WithPresent");
            description.Success &=
                VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.AnyQueryTypes, description.PresentQueryTypes, "WithAny", "WithPresent");
            description.Success &=
                VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.AbsentQueryTypes, description.DisabledQueryTypes, "WithAbsent", "WithDisabled");
            description.Success &=
                VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.AbsentQueryTypes, description.AllQueryTypes, "WithAbsent", "WithAll");
            description.Success &=
                VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.AbsentQueryTypes, description.AnyQueryTypes, "WithAbsent", "WithAny");
            description.Success &=
                VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.NoneQueryTypes, description.AllQueryTypes, "WithNone", "WithAll");
            description.Success &=
                VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.NoneQueryTypes, description.AnyQueryTypes, "WithNone", "WithAny");
            description.Success &=
                VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.AnyQueryTypes, description.AllQueryTypes, "WithAny", "WithAll");

            if (!description.Success) // if !description.Success, TypeSymbolsForEntityQueryCreation might not be right, thus causing an exception
                continue;

            description.Success &= VerifyNoMutuallyExclusiveQueries(
                description.SystemDescription,
                description.Location,
                description.AbsentQueryTypes,
                description.IterableNonEnableableTypes,
                "WithAbsent",
                "Main query type in SystemAPI.Query",
                compareTypeSymbolsOnly: true);

            description.Success &= VerifyNoMutuallyExclusiveQueries(
                description.SystemDescription,
                description.Location,
                description.DisabledQueryTypes,
                description.IterableNonEnableableTypes,
                "WithDisabled",
                "Main query type in SystemAPI.Query",
                compareTypeSymbolsOnly: true);

            description.Success &= VerifyNoMutuallyExclusiveQueries(
                description.SystemDescription,
                description.Location,
                description.NoneQueryTypes,
                description.IterableNonEnableableTypes,
                "WithNone",
                "Main query type in SystemAPI.Query",
                compareTypeSymbolsOnly: true);

            description.Success &= VerifyNoMutuallyExclusiveQueries(
                description.SystemDescription,
                description.Location,
                description.AnyQueryTypes,
                description.IterableNonEnableableTypes,
                "WithAny",
                "Main query type in SystemAPI.Query",
                compareTypeSymbolsOnly: true);

            if (description.Success)
            {
                if (description.IsBurstEnabled)
                    RequiresReferenceToBurst = true;

                idiomaticCSharpForEachDescriptions.Add(description);
            }
        }

        var candidateNodesToReplacementCode = new Dictionary<SyntaxNode, string>();
        foreach (var description in idiomaticCSharpForEachDescriptions)
        {
            var ifeStructWriter =
                new IfeStructWriter
                {
                    IfeType = description.IfeType,
                    SharedComponentFilterInfos = description.SharedComponentFilterInfos
                };

            systemDescription.NewMiscellaneousMembers.Add(ifeStructWriter);

            var ifeTypeHandleFieldName =
                systemDescription.QueriesAndHandles.GetOrCreateSourceGeneratedIfeTypeHandleField(description.IfeType.FullyQualifiedTypeName);

            var entityQueryFieldName =
                systemDescription.QueriesAndHandles.GetOrCreateQueryField(
                    new SingleArchetypeQueryFieldDescription(
                        new Archetype(
                            description.GetDistinctAllQueryTypes(),
                            description.AnyQueryTypes,
                            description.NoneQueryTypes,
                            description.DisabledQueryTypes,
                            description.AbsentQueryTypes,
                            description.PresentQueryTypes,
                            description.GetEntityQueryOptionsArgument()),
                        description.ChangeFilterQueryTypes));

            description.CandidateNodesToReplacementCode.Add(
                key: description.QueryCandidate.FullInvocationChainSyntaxNode,
                value: description.CreateQueryInvocationNodeReplacementCode(entityQueryFieldName, ifeTypeHandleFieldName));

            foreach (var candidateSyntax in description.AdditionalCandidates)
                systemDescription.CandidateNodes.Add(candidateSyntax.Node, candidateSyntax);

            foreach (var kvp in description.CandidateNodesToReplacementCode)
                candidateNodesToReplacementCode.Add(kvp.Key, kvp.Value);
        }
        systemDescription.SyntaxWalkers.Add(Module.Ife, new IfeSyntaxWalker(candidateNodesToReplacementCode));
        return true;
    }
}
