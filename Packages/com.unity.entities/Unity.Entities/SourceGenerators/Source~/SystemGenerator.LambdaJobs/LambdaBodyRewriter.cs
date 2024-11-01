using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;
using InvalidDescriptionException = Unity.Entities.SourceGen.Common.InvalidDescriptionException;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

// Describes the field in the LambdaJob struct that holds the accessor type for accessing data from entities
// (ComponentLookup, BufferLookup)
// These get added as members into the LambdaJob struct.

// Rewrite the original lambda body with a few changes:
// 1. If we are accessing any methods/fields of our declaring type, make sure it is explicit
//   ("this.EntityManager.DoThing()" instead of "EntityManager.DoThing()")
// 2. Replace all member access with "this" identifiers to "__this" (to access through stored field on job struct)
// 3. Patch all access through data access through entity methods (GetComponent, SetComponent, etc)
// 4. Adds trivia for line numbers from original syntax statements
// 5. Replace ForEach.Method() and ForEach.Property with either state.Method()/Property or just Method()/Property
//    for ISystem or SystemBase, respectively
sealed class LambdaBodyRewriter
{
    readonly LambdaJobDescription _lambdaJobDescription;

// TODO: This was recently fixed (https://github.com/dotnet/roslyn-analyzers/issues/5804), remove pragmas after we update .net
#pragma warning disable RS1024
    Dictionary<ITypeSymbol, DataLookupFieldDescription> DataLookupFields { get; } =
        new Dictionary<ITypeSymbol, DataLookupFieldDescription>(SymbolEqualityComparer.Default);
#pragma warning restore RS1024

    public bool NeedsTimeData { get; private set; }

    public LambdaBodyRewriter(LambdaJobDescription lambdaJobDescription)
    {
        _lambdaJobDescription = lambdaJobDescription;
    }

    internal (SyntaxNode rewrittenLambdaExpression, List<DataLookupFieldDescription> additionalFields) Rewrite()
    {
        // Find all locations where we are accessing a member on the declaring SystemBase
        // and change them to access through "__this" instead.
        // This also annotates the changed nodes so that we can find them later for patching (and get their original symbols).
        var replacer  = new LambdaBodySyntaxReplacer(_lambdaJobDescription);
        var rewrittenLambdaBodyData = replacer.Rewrite();
        NeedsTimeData = replacer.NeedsTimeData;
        var rewrittenLambdaExpression = rewrittenLambdaBodyData.rewrittenLambdaExpression; ;

        // Go through all changed nodes and check to see if they are a component access method that we need to patch (GetComponent/SetComponent/etc)
        // Only need to do this if we are not doing structural changes (in which case we can't as structural changes will invalidate)
        var replacedToOriginal = new Dictionary<SyntaxNode, SyntaxNode>(rewrittenLambdaBodyData.thisAccessNodesNeedingReplacement.Count);
        foreach (var originalNode in rewrittenLambdaBodyData.thisAccessNodesNeedingReplacement)
        {
            var originalInvocation = originalNode.AncestorOfKind<InvocationExpressionSyntax>();

            var currentNode = rewrittenLambdaExpression.GetCurrentNode(originalNode);
            var currentNodeInvocationExpression = currentNode.AncestorOfKindOrDefault<InvocationExpressionSyntax>();
            if (currentNodeInvocationExpression == null)
                continue;
            replacedToOriginal[currentNodeInvocationExpression] = originalInvocation;
        }
        rewrittenLambdaExpression = rewrittenLambdaExpression.ReplaceNodes(replacedToOriginal.Keys, (node, replacedNode)
            => CreateDataLookupFields_AndReplaceMemberAccessExpressionNodes(_lambdaJobDescription, replacedToOriginal[node], replacedNode, _lambdaJobDescription.SystemDescription.SemanticModel) ?? replacedNode);

        return (rewrittenLambdaExpression, DataLookupFields.Values.ToList());
    }

    SyntaxNode CreateDataLookupFields_AndReplaceMemberAccessExpressionNodes(LambdaJobDescription description, SyntaxNode originalNode, SyntaxNode replaced, SemanticModel model)
    {
        if (originalNode is InvocationExpressionSyntax originalInvocationNode && replaced is InvocationExpressionSyntax replacedExpression
                                                                              && model.GetSymbolInfo(originalInvocationNode.Expression) is var symbolInfo)
        {
            var methodSymbol = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.FirstOrDefault() as IMethodSymbol;
            if (methodSymbol == null)
                return null;

            if (!(methodSymbol.ContainingType.Is("Unity.Entities.SystemBase") || methodSymbol.ContainingType.Is("Unity.Entities.SystemAPI")))
                return null;

            if (!LambdaJobsPatchableMethod.PatchableMethods.TryGetValue(methodSymbol.Name, out var patchableMethod))
            {
                return null;
            }

            var readOnlyAccess = true;
            switch (patchableMethod.AccessRights)
            {
                case LambdaJobsPatchableMethod.ComponentAccessRights.ReadOnly:
                    readOnlyAccess = true;
                    break;
                case LambdaJobsPatchableMethod.ComponentAccessRights.ReadWrite:
                    readOnlyAccess = false;
                    break;

                // Get read-access from method's param
                case LambdaJobsPatchableMethod.ComponentAccessRights.GetFromFirstMethodParam:
                    if (originalInvocationNode.ArgumentList.Arguments.Count == 0)
                    {
                        // Default parameter value for GetComponentLookup/GetBufferLookup is false (aka `bool isReadOnly = false`)
                        readOnlyAccess = false;
                    }
                    else
                    {
                        var literalArgument = originalInvocationNode.ArgumentList.Arguments.FirstOrDefault()?.DescendantNodes().OfType<LiteralExpressionSyntax>().FirstOrDefault();
                        if (literalArgument != null && description.SystemDescription.SemanticModel.GetConstantValue(literalArgument).Value is bool boolValue)
                        {
                            readOnlyAccess = boolValue;
                        }
                        else
                        {
                            LambdaJobsErrors.DC0059(description.SystemDescription, description.Location, methodSymbol.Name, description.LambdaJobKind);
                            description.Success = false;
                            throw new InvalidDescriptionException();
                        }
                    }

                    break;
            }

            // If we have read/write access, we can only guaranteed safe access with sequential access (.Run or .Schedule)
            if (!readOnlyAccess && description.Schedule.Mode == ScheduleMode.ScheduleParallel)
            {
                var patchedMethodTypeArgument = methodSymbol.TypeArguments.First();
                SystemGeneratorErrors.DC0063(description.SystemDescription, description.Location, methodSymbol.Name, patchedMethodTypeArgument.Name);
                description.Success = false;
                throw new InvalidDescriptionException();
            }

            // Make sure our ComponentLookupField doesn't give write access to a lambda parameter of the same type
            // or there is a writable lambda parameter that gives access to this type (either could violate aliasing rules).
            if (methodSymbol.TypeArguments.Length == 1)
            {
                foreach (var parameter in description.LambdaParameters)
                {
                    var patchedMethodTypeArgument = methodSymbol.TypeArguments.First();
                    if (parameter.TypeSymbol.ToFullName() != patchedMethodTypeArgument.ToFullName())
                        continue;

                    if (!readOnlyAccess)
                    {
                        LambdaJobsErrors.DC0046(description.SystemDescription, description.Location, methodSymbol.Name, parameter.TypeSymbol.Name, description.LambdaJobKind);
                        description.Success = false;
                        throw new InvalidDescriptionException();
                    }

                    if (!parameter.QueryTypeIsReadOnly())
                    {
                        LambdaJobsErrors.DC0047(description.SystemDescription, description.Location, methodSymbol.Name, parameter.TypeSymbol.Name, description.LambdaJobKind);
                        description.Success = false;
                        throw new InvalidDescriptionException();
                    }
                }
            }

            return patchableMethod.GeneratePatchedReplacementSyntax(methodSymbol, this, replacedExpression);
        }

        return null;
    }

    // Gets or created a field declaration for a type as needed.
    // This will first check if a RW one is available, if that is the case we should use that.
    // If not it will check to see if a RO one is available, use that and promote to RW if needed.
    // Finally, if we don't have one at all, let's create one with the appropriate access rights
    internal DataLookupFieldDescription GetOrCreateDataAccessField(ITypeSymbol type, bool asReadOnly, LambdaJobsPatchableMethod.AccessorDataType patchableMethodDataType)
    {
        if (DataLookupFields.TryGetValue(type, out var result))
        {
            if (result.IsReadOnly && !asReadOnly)
                DataLookupFields[type] = new DataLookupFieldDescription(false, type, patchableMethodDataType);

            return DataLookupFields[type];
        }

        DataLookupFields[type] = new DataLookupFieldDescription(asReadOnly, type, patchableMethodDataType);
        return DataLookupFields[type];
    }
}
