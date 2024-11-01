using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.EntityQueryBulkOperations
{
    public class EntityQueryModule : ISystemModule
    {
        static bool TryGetAllTypeArgumentSymbolsOfMethod(
            SystemDescription systemDescription,
            SyntaxNode node,
            IReadOnlyDictionary<string, List<InvocationExpressionSyntax>> allMethodInvocations,
            string methodName,
            QueryType resultsShouldHaveThisQueryType,
            out List<Query> result)
        {
            result = new List<Query>();

            var isValid = true;
            var location = node.GetLocation();

            if (!allMethodInvocations.ContainsKey(methodName))
                return true;

            var symbols =
                allMethodInvocations[methodName]
                    .Select(methodInvocation => (IMethodSymbol)systemDescription.SemanticModel.GetSymbolInfo(methodInvocation).Symbol)
                    .Where(symbol => symbol != null);

            foreach (var symbol in symbols)
            {
                foreach (var argumentType in symbol.TypeArguments.OfType<ITypeParameterSymbol>())
                {
                    SystemGeneratorErrors.DC0051(systemDescription, location, argumentType.Name, methodName);
                    isValid = false;
                }

                foreach (var argumentType in symbol.TypeArguments.OfType<INamedTypeSymbol>())
                {
                    if (argumentType.IsGenericType)
                    {
                        SystemGeneratorErrors.DC0051(systemDescription, location, argumentType.Name, methodName);
                        isValid = false;
                        continue;
                    }
                    result.Add(new Query { IsReadOnly = true, Type = resultsShouldHaveThisQueryType, TypeSymbol = argumentType});
                }
            }

            return isValid;
        }

        public IEnumerable<(SyntaxNode SyntaxNode, TypeDeclarationSyntax SystemType)> Candidates
        {
            get
            {
                foreach (var kvp in EntityQueryCandidatesGroupedBySystemType)
                    foreach (var node in kvp.Value)
                        yield return (node, kvp.Key);
            }
        }

        public bool RequiresReferenceToBurst => false;
        Dictionary<TypeDeclarationSyntax, List<SyntaxNode>> EntityQueryCandidatesGroupedBySystemType { get; } = new Dictionary<TypeDeclarationSyntax, List<SyntaxNode>>();

        static string[] BulkOperationMethodNames { get; } =
        {
            "AddChunkComponentData",
            "AddComponent",
            "AddComponentData",
            "AddSharedComponent",
            "AddSharedComponentManaged",
            "DestroyEntity",
            "RemoveChunkComponentData",
            "RemoveComponent",
            "SetSharedComponent",
            "SetSharedComponentManaged",
            "ToQuery",
        };

        public void OnReceiveSyntaxNode(SyntaxNode entitiesSyntaxNode, Dictionary<SyntaxNode, CandidateSyntax> candidateOwnership)
        {
            if (entitiesSyntaxNode is IdentifierNameSyntax identifierNameSyntax
                && identifierNameSyntax.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                && identifierNameSyntax.Identifier.Text == "Entities")
            {
                EntityQueryCandidatesGroupedBySystemType.Add(entitiesSyntaxNode.Ancestors().OfType<TypeDeclarationSyntax>().First(), entitiesSyntaxNode);
            }
        }

        public bool RegisterChangesInSystem(SystemDescription systemDescription)
        {
            var success = true;
            var originalToReplacementNodes = new Dictionary<InvocationExpressionSyntax, SyntaxNode>();

            foreach (var candidate in EntityQueryCandidatesGroupedBySystemType[systemDescription.SystemTypeSyntax])
            {
                InvocationExpressionSyntax bulkOperationInvocationNodeToReplace = null;
                string bulkOperationInvocationText = null;

                bool foundMethodInvocationsDisallowedByBulkOperations = false;

                var bulkOperationQueryMethodInvocations = new Dictionary<string, List<InvocationExpressionSyntax>>();

                foreach (var invocationExpressionSyntax in candidate.Ancestors().OfType<InvocationExpressionSyntax>())
                {
                    bool isEntitiesForEachInvocation = false;

                    if (invocationExpressionSyntax.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
                    {
                        switch (memberAccessExpressionSyntax.Name)
                        {
                            case IdentifierNameSyntax identifierNameSyntax:
                                var identifierValueText = identifierNameSyntax.Identifier.ValueText;
                                if (identifierValueText == "ForEach")
                                {
                                    isEntitiesForEachInvocation = true;
                                    break;
                                }
                                if (BulkOperationMethodNames.Contains(identifierValueText))
                                {
                                    bulkOperationInvocationNodeToReplace = invocationExpressionSyntax;
                                    bulkOperationInvocationText = identifierValueText;
                                    break;
                                }

                                switch (identifierNameSyntax.Identifier.ValueText)
                                {
                                    case "WithSharedComponentFilter":
                                        bulkOperationQueryMethodInvocations.Add("WithSharedComponentFilter", invocationExpressionSyntax);
                                        break;
                                    default:
                                        foundMethodInvocationsDisallowedByBulkOperations = true;
                                        break;
                                }
                                break;
                            case GenericNameSyntax genericNameSyntax:
                                if (BulkOperationMethodNames.Contains(genericNameSyntax.Identifier.ValueText))
                                {
                                    bulkOperationInvocationNodeToReplace = invocationExpressionSyntax;
                                    bulkOperationInvocationText = genericNameSyntax.ToString();
                                    break;
                                }

                                switch (genericNameSyntax.Identifier.ValueText)
                                {
                                    case "WithAll":
                                        bulkOperationQueryMethodInvocations.Add("WithAll", invocationExpressionSyntax);
                                        break;
                                    case "WithAny":
                                        bulkOperationQueryMethodInvocations.Add("WithAny", invocationExpressionSyntax);
                                        break;
                                    case "WithNone":
                                        bulkOperationQueryMethodInvocations.Add("WithNone", invocationExpressionSyntax);
                                        break;
                                    case "WithDisabled":
                                        bulkOperationQueryMethodInvocations.Add("WithDisabled", invocationExpressionSyntax);
                                        break;
                                    case "WithAbsent":
                                        bulkOperationQueryMethodInvocations.Add("WithAbsent", invocationExpressionSyntax);
                                        break;
                                    case "WithPresent":
                                        bulkOperationQueryMethodInvocations.Add("WithPresent", invocationExpressionSyntax);
                                        break;
                                    case "WithChangeFilter":
                                        bulkOperationQueryMethodInvocations.Add("WithChangeFilter", invocationExpressionSyntax);
                                        break;
                                    default:
                                        foundMethodInvocationsDisallowedByBulkOperations = true;
                                        break;
                                }
                                break;
                        }
                    }

                    if (isEntitiesForEachInvocation)
                        break;

                    if (bulkOperationInvocationNodeToReplace != null)
                        break;
                }

                if (bulkOperationInvocationNodeToReplace == null)
                    continue;

                if (foundMethodInvocationsDisallowedByBulkOperations)
                {
                    SystemGeneratorErrors.DC0062(systemDescription, candidate.GetLocation());
                    break;
                }

                success &= TryGetAllTypeArgumentSymbolsOfMethod(systemDescription, candidate, bulkOperationQueryMethodInvocations, "WithAll", QueryType.All, out var withAllTypes);
                success &= TryGetAllTypeArgumentSymbolsOfMethod(systemDescription, candidate, bulkOperationQueryMethodInvocations, "WithAny", QueryType.Any, out var withAnyTypes);
                success &= TryGetAllTypeArgumentSymbolsOfMethod(systemDescription, candidate, bulkOperationQueryMethodInvocations, "WithNone", QueryType.None, out var withNoneTypes);
                success &= TryGetAllTypeArgumentSymbolsOfMethod(systemDescription, candidate, bulkOperationQueryMethodInvocations, "WithDisabled", QueryType.Disabled, out var withDisabledTypes);
                success &= TryGetAllTypeArgumentSymbolsOfMethod(systemDescription, candidate, bulkOperationQueryMethodInvocations, "WithAbsent", QueryType.Absent, out var withAbsentTypes);
                success &= TryGetAllTypeArgumentSymbolsOfMethod(systemDescription, candidate, bulkOperationQueryMethodInvocations, "WithPresent", QueryType.Absent, out var withPresentTypes);
                success &= TryGetAllTypeArgumentSymbolsOfMethod(systemDescription, candidate, bulkOperationQueryMethodInvocations, "WithChangeFilter", QueryType.ChangeFilter, out var withChangeFilterTypes);
                success &= TryGetAllTypeArgumentSymbolsOfMethod(systemDescription, candidate, bulkOperationQueryMethodInvocations, "WithSharedComponentFilter", QueryType.All, out var withSharedComponentFilterTypes);

                var queryDescription =
                    new SingleArchetypeQueryFieldDescription(
                        new Archetype(
                            withAllTypes.Concat(withSharedComponentFilterTypes).ToArray(),
                            withAnyTypes,
                            withNoneTypes,
                            withDisabledTypes,
                            withPresentTypes,
                            withAbsentTypes),
                        changeFilterTypes: withChangeFilterTypes);

                var generatedQueryFieldName = systemDescription.QueriesAndHandles.GetOrCreateQueryField(queryDescription);

                systemDescription.CandidateNodes.Add(
                    bulkOperationInvocationNodeToReplace,
                    new CandidateSyntax(CandidateType.EntityQueryBulkOps, CandidateFlags.None, bulkOperationInvocationNodeToReplace));

                if (bulkOperationInvocationText != "ToQuery")
                {
                    var args = new List<ArgumentSyntax> { SyntaxFactory.Argument(SyntaxFactory.IdentifierName(generatedQueryFieldName)) };
                    args.AddRange(bulkOperationInvocationNodeToReplace.ArgumentList.Arguments);

                    var replacementSyntaxNode =
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("EntityManager"),
                                SyntaxFactory.IdentifierName(bulkOperationInvocationText)),
                            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args)));

                    originalToReplacementNodes.Add(bulkOperationInvocationNodeToReplace, replacementSyntaxNode);
                }
                else
                    originalToReplacementNodes.Add(bulkOperationInvocationNodeToReplace, SyntaxFactory.IdentifierName(generatedQueryFieldName));
            }

            if (originalToReplacementNodes.Count > 0)
                systemDescription.SyntaxWalkers.Add(Module.EntityQueryBulkOps, new EntityQueryBulkOperationSyntaxWalker(originalToReplacementNodes));

            return success;
        }
    }
}
