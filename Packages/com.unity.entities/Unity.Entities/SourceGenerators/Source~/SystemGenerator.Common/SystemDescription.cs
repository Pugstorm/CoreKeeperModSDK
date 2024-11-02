using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.Common;

public readonly struct SystemDescription : ISourceGeneratorDiagnosable
{
    public List<Diagnostic> SourceGenDiagnostics { get; }

    public readonly QueriesAndHandles QueriesAndHandles;
    public readonly TypeDeclarationSyntax SystemTypeSyntax;
    public readonly SystemType SystemType;
    public readonly SemanticModel SemanticModel;
    public readonly INamedTypeSymbol SystemTypeSymbol;
    public readonly string SystemTypeFullName;
    public readonly SyntaxTreeInfo SyntaxTreeInfo;
    public readonly Dictionary<Module, IModuleSyntaxWalker> SyntaxWalkers;
    public readonly Dictionary<SyntaxNode, CandidateSyntax> CandidateNodes;
    public readonly Dictionary<string, string> FullEcbSystemTypeNamesToGeneratedFieldNames;
    public readonly HashSet<string> AdditionalStatementsInOnCreateForCompilerMethod;
    public readonly List<IMemberWriter> NewMiscellaneousMembers;
    public readonly PreprocessorInfo PreprocessorInfo;

    public SystemDescription(
        TypeDeclarationSyntax systemTypeSyntax,
        SystemType systemType,
        INamedTypeSymbol systemTypeSymbol,
        SemanticModel semanticModel,
        SyntaxTreeInfo syntaxTreeInfo,
        Dictionary<SyntaxNode, CandidateSyntax> candidateNodes,
        PreprocessorInfo preprocessorInfo)
    {
        SystemTypeSyntax = systemTypeSyntax;
        SemanticModel = semanticModel;
        SystemTypeSymbol = systemTypeSymbol;
        SystemType = systemType;
        SystemTypeFullName = SystemTypeSymbol.ToFullName();
        SyntaxTreeInfo = syntaxTreeInfo;
        SourceGenDiagnostics = new List<Diagnostic>();
        NewMiscellaneousMembers = new List<IMemberWriter>();
        AdditionalStatementsInOnCreateForCompilerMethod = new HashSet<string>();
        FullEcbSystemTypeNamesToGeneratedFieldNames = new Dictionary<string, string>();
        QueriesAndHandles = QueriesAndHandles.Create(systemTypeSyntax);
        CandidateNodes = candidateNodes ?? new Dictionary<SyntaxNode, CandidateSyntax>();
        SyntaxWalkers = new Dictionary<Module, IModuleSyntaxWalker>();
        PreprocessorInfo = preprocessorInfo;
    }

    public bool ContainsChangesToSystem() =>
        QueriesAndHandles.QueryFieldsToFieldNames.Any()
        || QueriesAndHandles.TypeHandleStructNestedFields.Any()
        || FullEcbSystemTypeNamesToGeneratedFieldNames.Any()
        || SyntaxWalkers.Any()
        || NewMiscellaneousMembers.Any();

    public string GetOrCreateEntityCommandBufferSystemField(ITypeSymbol ecbSystemTypeSymbol)
    {
        string fullEcbSystemTypeName = ecbSystemTypeSymbol.ToFullName();

        if (FullEcbSystemTypeNamesToGeneratedFieldNames.TryGetValue(fullEcbSystemTypeName, out var generatedFieldName))
            return generatedFieldName;

        generatedFieldName = $"__{ecbSystemTypeSymbol.ToValidIdentifier()}";
        FullEcbSystemTypeNamesToGeneratedFieldNames[fullEcbSystemTypeName] = generatedFieldName;

        return generatedFieldName;
    }

    public string OriginalFilePath => SyntaxTreeInfo.Tree.FilePath.Replace('\\', '/');

    public (HashSet<StatementSyntax>, HashSet<StatementSyntax>) GetStatementsRequiringLineDirectivesAndHiddenDirectives()
    {
        var lineDirectiveStatements = new HashSet<StatementSyntax>();
        var hiddenDirectiveStatements = new HashSet<StatementSyntax>();

        foreach (var node in CandidateNodes.Keys)
        {
            var containingMember = node.AncestorOfKind<MemberDeclarationSyntax>();
            if (containingMember is MethodDeclarationSyntax { Body: not null } methodDeclarationSyntax)
            {
                var methodStatements = methodDeclarationSyntax.Body.DescendantNodes().OfType<StatementSyntax>();
                StatementSyntax lastStatement = null;
                foreach (var statement in methodStatements)
                {
                    lineDirectiveStatements.Add(statement);
                    lastStatement = statement;
                }
                if (lastStatement != null)
                    hiddenDirectiveStatements.Add(lastStatement);
            }
        }

        return (lineDirectiveStatements, hiddenDirectiveStatements);
    }

    public Dictionary<MemberDeclarationSyntax, Dictionary<SyntaxNode, CandidateSyntax>> CandidateNodesGroupedByMethodOrProperty
    {
        get
        {
            var candidateNodesGroupedByContainingMember = new Dictionary<MemberDeclarationSyntax, Dictionary<SyntaxNode, CandidateSyntax>>();
            foreach (var kvp in CandidateNodes)
            {
                var node = kvp.Key;
                var candidate = kvp.Value;

                var containingMember = node.AncestorOfKind<MemberDeclarationSyntax>();

                if (candidateNodesGroupedByContainingMember.TryGetValue(containingMember, out var candidateNodes))
                    candidateNodes.Add(node, candidate);
                else
                    candidateNodesGroupedByContainingMember.Add(containingMember, new()
                    {
                        { node, candidate }
                    });
            }

            return candidateNodesGroupedByContainingMember;
        }
    }

    public bool TryGetSystemStateParameterName(ISystemCandidate candidate, out ExpressionSyntax systemStateExpression)
    {
        switch (SystemType)
        {
            case SystemType.ISystem:
            {
                var methodDeclarationSyntax = candidate.Node.AncestorOfKindOrDefault<MethodDeclarationSyntax>();
                if (methodDeclarationSyntax == null) {
                    SystemGeneratorErrors.SGSG0001(this, candidate);
                    systemStateExpression = null;
                    return false;
                }

                var containingMethodSymbol = (IMethodSymbol)ModelExtensions.GetDeclaredSymbol(SemanticModel, methodDeclarationSyntax);

                var systemStateParameterName = containingMethodSymbol?.Parameters.FirstOrDefault(p => p.Type.Is("Unity.Entities.SystemState"))?.Name;
                if (systemStateParameterName != null)
                {
                    systemStateExpression = SyntaxFactory.IdentifierName(systemStateParameterName);
                    return true;
                }

                SystemGeneratorErrors.SGSG0002(this, candidate);
                systemStateExpression = null;
                return false;
            }
            case SystemType.Unknown:
                systemStateExpression = SyntaxFactory.IdentifierName("state");
                return true;
        }

        // this.CheckedStateRef
        systemStateExpression = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.ThisExpression(), SyntaxFactory.IdentifierName("CheckedStateRef"));
        return true;
    }
}
