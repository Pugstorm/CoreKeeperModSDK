using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

static class LambdaJobsVerification
{
    public static void Verify(this LambdaJobDescription description)
    {
        // If our lambda expression is badly formed, early out as we will likely flag other false errors (capturing of this)
        if (!VerifyLambdaExpression(description))
        {
            description.Success = false;
            return;
        }

        // Fail if we have other issues with captured variables or are capturing a reference type in burst or non-run
        foreach (var variableCaptured in description.VariablesCaptured)
        {
            if (variableCaptured.Type.TypeKind == TypeKind.Error)
                description.Success = false;
            else if (variableCaptured.Type.IsReferenceType &&
                     (description.Burst.IsEnabled || description.Schedule.Mode != ScheduleMode.Run))
            {
                LambdaJobsErrors.DC0004(description.SystemDescription, description.Location,
                    variableCaptured.OriginalVariableName, description.LambdaJobKind);
                description.Success = false;
            }
        }

        if (description.LambdaJobKind == LambdaJobKind.Job && description.WithStructuralChanges)
        {
            LambdaJobsErrors.DC0057(description.SystemDescription, description.Location);
            description.Success = false;
        }

        if (!VerifyDC0073(description))
        {
            description.Success = false;
        }
    }

    static bool VerifyLambdaExpression(LambdaJobDescription description)
    {
        var success = true;

        // Check method calls inside lambda for:
        // a) if any structural changes are made
        // b) nested Entities.ForEach
        foreach (var invocationExpression in
                 description.OriginalLambdaExpression.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if ((!description.WithStructuralChanges || description.Schedule.Mode != ScheduleMode.Run) &&
                DoesPerformStructuralChange(invocationExpression, description.SystemDescription.SemanticModel))
            {
                LambdaJobsErrors.DC0027(description.SystemDescription, invocationExpression.GetLocation(), description.LambdaJobKind);
                success = false;
            }
        }

        foreach (var methodInvocations in description.MethodInvocations)
        {
            if (methodInvocations.Key != "ForEach" && methodInvocations.Key != "WithCode")
            {
                foreach (var methodInvocation in methodInvocations.Value.Where(methodInvocation => methodInvocation.ContainsDynamicCode()))
                {
                    LambdaJobsErrors.DC0010(description.SystemDescription, methodInvocation.GetLocation(), methodInvocations.Key, description.LambdaJobKind);
                    description.Success = false;
                }
            }
        }

        foreach (var methods in description.MethodInvocations.Where(methods => methods.Value.Count > 1))
        {
            var methodInvocationSymbol = description.SystemDescription.SemanticModel.GetSymbolInfo(methods.Value.First()).Symbol;
            if (!methodInvocationSymbol.HasAttribute(
                    "Unity.Entities.LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute"))
            {
                LambdaJobsErrors.DC0009(description.SystemDescription, description.Location, methods.Key);
                description.Success = false;
            }
        }

        foreach (var node in description.OriginalLambdaExpression.DescendantNodes())
        {
            switch (node)
            {
                case AnonymousFunctionExpressionSyntax anonymousFunction:
                    // Give better error if user tries to nest EFE/JWC.
                    if (anonymousFunction is ParenthesizedLambdaExpressionSyntax
                        && node.Parent.Parent.Parent is InvocationExpressionSyntax invocation) // read node.Parent.Parent.Parent as lambda.argument.argumentlist.invocation
                    {
                        var theInvocationSymbol = description.SystemDescription.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol;

                        var isForEach = theInvocationSymbol is IMethodSymbol {Name: "ForEach"} methodSymbolForEach // Is ForEach method called as LambdaForEach
                                        && methodSymbolForEach.ContainingType.Is("LambdaForEachDescriptionConstructionMethods");

                        var isJobWithCode =  theInvocationSymbol is IMethodSymbol {Name: "WithCode"} methodSymbolWithCode // Is WithCode method called as LambdaSingleJob
                                             && methodSymbolWithCode.ContainingType.Is("LambdaSingleJobDescriptionConstructionMethods");

                        if (isForEach || isJobWithCode)
                        {
                            LambdaJobsErrors.DC0029(description.SystemDescription, invocation.GetLocation(), description.LambdaJobKind);
                            success = false;
                            break;
                        }
                    }

                    LambdaJobsErrors.DC0084(description.SystemDescription, anonymousFunction.GetLocation(), description.LambdaJobKind, description.Schedule.Mode);
                    description.Success = false;
                    break;
                case LocalFunctionStatementSyntax localFunction:
                    LambdaJobsErrors.DC0085(description.SystemDescription, localFunction.GetLocation(), description.LambdaJobKind, description.Schedule.Mode);
                    description.Success = false;
                    break;
            }
        }

        foreach (var storeInQuerySyntax in description.WithStoreEntityQueryInFieldArgumentSyntaxes)
        {
            if (!(storeInQuerySyntax.Expression is IdentifierNameSyntax storeInQueryIdentifier) || !(description.SystemDescription.SemanticModel.GetSymbolInfo(storeInQueryIdentifier).Symbol is IFieldSymbol))
            {
                LambdaJobsErrors.DC0031(description.SystemDescription, description.Location);
                description.Success = false;
            }
        }

        if (description.Burst.IsEnabled || description.Schedule.Mode is ScheduleMode.Schedule || description.Schedule.Mode is ScheduleMode.ScheduleParallel)
        {
            foreach (var param in description.LambdaParameters.OfType<IManagedComponentParamDescription>())
            {
                LambdaJobsErrors.DC0223(description.SystemDescription, description.Location, param.Name, true, description.LambdaJobKind);
                description.Success = false;
            }
            foreach (var param in description.LambdaParameters.OfType<ISharedComponentParamDescription>())
            {
                LambdaJobsErrors.DC0223(description.SystemDescription, description.Location, param.TypeSymbol.Name, false, description.LambdaJobKind);
                description.Success = false;
            }
        }

        var duplicateTypes =
            description
                .LambdaParameters
                .Where(desc => !desc.AllowDuplicateTypes && !desc.IsSourceGeneratedParam)
                .FindDuplicatesBy(desc => desc.TypeSymbol.ToFullName());

        foreach (var duplicate in duplicateTypes)
        {
            LambdaJobsErrors.DC0070(description.SystemDescription, description.Location, duplicate.TypeSymbol);
            description.Success = false;
        }

        foreach (var param in description.LambdaParameters.OfType<IComponentParamDescription>().Where(param => string.IsNullOrEmpty(param.Syntax.GetModifierString())))
        {
            LambdaJobsErrors.DC0055(description.SystemDescription, description.Location, param.Name, description.LambdaJobKind);
        }

        foreach (var param in description.LambdaParameters.OfType<IManagedComponentParamDescription>().Where(param => param.IsByRef()))
        {
            if (param.IsByRef())
            {
                LambdaJobsErrors.DC0024(description.SystemDescription, description.Location, param.Name);
                description.Success = false;
            }
        }

        foreach (var param in description.LambdaParameters.OfType<ISharedComponentParamDescription>().Where(param => param.IsByRef()))
        {
            LambdaJobsErrors.DC0020(description.SystemDescription, description.Location, param.TypeSymbol.Name);
            description.Success = false;
        }

        foreach (var param in description.LambdaParameters.OfType<IDynamicBufferParamDescription>())
        {
            if (param.TypeSymbol is INamedTypeSymbol namedTypeSymbol)
            {
                foreach (var typeArg in namedTypeSymbol.TypeArguments.OfType<INamedTypeSymbol>().Where(
                             typeArg => typeArg is { } namedTypeArg && namedTypeArg.TypeArguments.Any()))
                {
                    LambdaJobsErrors.DC0050(description.SystemDescription, description.Location, typeArg.Name, description.LambdaJobKind);
                    description.Success = false;
                }
            }
        }

        description.Success &=
            QueryVerification.VerifyQueryTypeCorrectness(description.SystemDescription, description.Location, description.WithAllTypes, invokedMethodName: "WithAll");
        description.Success &=
            QueryVerification.VerifyQueryTypeCorrectness(description.SystemDescription, description.Location, description.WithNoneTypes, invokedMethodName: "WithNone");
        description.Success &=
            QueryVerification.VerifyQueryTypeCorrectness(description.SystemDescription, description.Location, description.WithAnyTypes, invokedMethodName: "WithAny");
        description.Success &=
            QueryVerification.VerifyQueryTypeCorrectness(description.SystemDescription, description.Location, description.WithDisabledTypes, invokedMethodName: "WithDisabled");
        description.Success &=
            QueryVerification.VerifyQueryTypeCorrectness(description.SystemDescription, description.Location, description.WithAbsentTypes, invokedMethodName: "WithAbsent");
        description.Success &=
            QueryVerification.VerifyQueryTypeCorrectness(description.SystemDescription, description.Location, description.WithPresentTypes, invokedMethodName: "WithPresent");

        description.Success &=
            QueryVerification.VerifySharedComponentFilterTypesAgainstOtherQueryTypes(description.SystemDescription, description.Location, description.WithSharedComponentFilterTypes, description.WithAllTypes);
        description.Success &=
            QueryVerification.VerifySharedComponentFilterTypesAgainstOtherQueryTypes(description.SystemDescription, description.Location, description.WithSharedComponentFilterTypes, description.WithAnyTypes);
        description.Success &=
            QueryVerification.VerifySharedComponentFilterTypesAgainstOtherQueryTypes(description.SystemDescription, description.Location, description.WithSharedComponentFilterTypes, description.WithNoneTypes);
        description.Success &=
            QueryVerification.VerifySharedComponentFilterTypesAgainstOtherQueryTypes(description.SystemDescription, description.Location, description.WithSharedComponentFilterTypes, description.WithDisabledTypes);
        description.Success &=
            QueryVerification.VerifySharedComponentFilterTypesAgainstOtherQueryTypes(description.SystemDescription, description.Location, description.WithSharedComponentFilterTypes, description.WithAbsentTypes);
        description.Success &=
            QueryVerification.VerifySharedComponentFilterTypesAgainstOtherQueryTypes(description.SystemDescription, description.Location, description.WithSharedComponentFilterTypes, description.WithPresentTypes);

        description.Success &=
            QueryVerification.VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.WithAbsentTypes, description.WithPresentTypes, "WithAbsent", "WithPresent");
        description.Success &=
            QueryVerification.VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.WithNoneTypes, description.WithPresentTypes, "WithNone", "WithPresent");
        description.Success &=
            QueryVerification.VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.WithAnyTypes, description.WithPresentTypes, "WithAny", "WithPresent");
        description.Success &=
            QueryVerification.VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.WithAbsentTypes, description.WithDisabledTypes, "WithAbsent", "WithDisabled");
        description.Success &=
            QueryVerification.VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.WithAbsentTypes, description.WithAllTypes, "WithAbsent", "WithAll");
        description.Success &=
            QueryVerification.VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.WithAbsentTypes, description.WithAnyTypes, "WithAbsent", "WithAny");
        description.Success &=
            QueryVerification.VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.WithNoneTypes, description.WithAllTypes, "WithNone", "WithAll");
        description.Success &=
            QueryVerification.VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.WithNoneTypes, description.WithAnyTypes, "WithNone", "WithAny");
        description.Success &=
            QueryVerification.VerifyNoMutuallyExclusiveQueries(description.SystemDescription, description.Location, description.WithAnyTypes, description.WithAllTypes, "WithAny", "WithAll");

        var lambdaParameters =
            description.LambdaParameters
                .Where(param => param.IsQueryableType)
                .Select(param =>
                    new Query
                    {
                        TypeSymbol = param.TypeSymbol,
                        Type = QueryType.All,
                        IsReadOnly = param.QueryTypeIsReadOnly()
                    })
                .ToArray();

        description.Success &= QueryVerification.VerifyNoMutuallyExclusiveQueries(
            description.SystemDescription,
            description.Location,
            description.WithNoneTypes,
            lambdaParameters,
            "WithNone",
            "lambda parameter",
            compareTypeSymbolsOnly: true);

        description.Success &= QueryVerification.VerifyNoMutuallyExclusiveQueries(
            description.SystemDescription,
            description.Location,
            description.WithAnyTypes,
            lambdaParameters,
            "WithAny",
            "lambda parameter",
            compareTypeSymbolsOnly: true);

        var containingSystemTypeSymbol = (INamedTypeSymbol)description.SystemDescription.SemanticModel.GetDeclaredSymbol(description.SystemDescription.SystemTypeSyntax);
        if (containingSystemTypeSymbol.GetMembers().OfType<IMethodSymbol>().Any(method => method.Name == "OnCreateForCompiler"))
        {
            LambdaJobsErrors.DC0025(description.SystemDescription, description.Location, containingSystemTypeSymbol.Name);
            description.Success = false;
        }

        if (containingSystemTypeSymbol.TypeParameters.Any())
        {
            LambdaJobsErrors.DC0053(description.SystemDescription, description.Location, containingSystemTypeSymbol.Name, description.LambdaJobKind);
            description.Success = false;
        }

        var containingMethodSymbol = description.SystemDescription.SemanticModel.GetDeclaredSymbol(description.ContainingMethod);
        if (containingMethodSymbol is IMethodSymbol methodSymbol && methodSymbol.TypeArguments.Any())
        {
            LambdaJobsErrors.DC0054(description.SystemDescription, description.Location, methodSymbol.Name, description.LambdaJobKind);
            description.Success = false;
        }

        return success;
    }

    static bool DoesPerformStructuralChange(InvocationExpressionSyntax syntax, SemanticModel model)
    {
        return model.GetSymbolInfo(syntax.Expression).Symbol is IMethodSymbol methodSymbol &&
               methodSymbol.ContainingType.Is("Unity.Entities.EntityManager") &&
               methodSymbol.HasAttribute("Unity.Entities.EntityManager.StructuralChangeMethodAttribute");
    }

    static bool VerifyDC0073(LambdaJobDescription description)
    {
        if ( description.WithScheduleGranularityArgumentSyntaxes.Count > 0
             && description.Schedule.Mode != ScheduleMode.ScheduleParallel)
        {
            LambdaJobsErrors.DC0073(description.SystemDescription, description.Location);
            return false;
        }
        return true;
    }
}
