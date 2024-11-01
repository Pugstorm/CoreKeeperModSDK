using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;
using static Unity.Entities.SourceGen.Common.SourceGenHelpers;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

public class LambdaJobDescription
{
    public struct BurstSettings
    {
        public BurstFloatMode? BurstFloatMode;
        public BurstFloatPrecision? BurstFloatPrecision;
        public bool? SynchronousCompilation;
    }

    public LambdaParamDescription_EntityCommandBuffer EntityCommandBufferParameter { get; }
    public SystemDescription SystemDescription { get; }
    public MethodDeclarationSyntax ContainingMethod { get; }
    public InvocationExpressionSyntax ContainingInvocationExpression { get; }
    public Dictionary<string, List<InvocationExpressionSyntax>> MethodInvocations { get; }
    public (bool IsEnabled, BurstSettings Settings) Burst { get; }
    public (ScheduleMode Mode, ArgumentSyntax DependencyArgument) Schedule { get; }

    public Query[] WithAllTypes { get; }
    public Query[] WithNoneTypes { get ; }
    public Query[] WithAnyTypes { get ; }
    public Query[] WithDisabledTypes { get ; }
    public Query[] WithAbsentTypes { get ; }
    public Query[] WithPresentTypes { get ; }
    public Query[] WithChangeFilterTypes { get ; }
    public Query[] WithSharedComponentFilterTypes { get ; }
    public IReadOnlyCollection<ArgumentSyntax> WithSharedComponentFilterArgumentSyntaxes { get; }
    public IReadOnlyCollection<ArgumentSyntax> WithStoreEntityQueryInFieldArgumentSyntaxes { get; }
    public IReadOnlyCollection<ArgumentSyntax> WithScheduleGranularityArgumentSyntaxes { get; }
    public Location Location { get; }
    public EntityQueryOptions EntityQueryOptions { get; }

    public string Name { get; }
    public bool Success { get; internal set; } = true;
    public string EntityQueryFieldName { get; set; }
    public string ExecuteInSystemMethodName => $"{Name}_Execute";

    bool CanContainReferenceTypes => !Burst.IsEnabled && Schedule.Mode == ScheduleMode.Run;

    public readonly ParenthesizedLambdaExpressionSyntax OriginalLambdaExpression;
    public readonly List<LambdaCapturedVariableDescription> VariablesCaptured = new List<LambdaCapturedVariableDescription>();
    public readonly List<LambdaCapturedVariableDescription> DisposeOnJobCompletionVariables = new List<LambdaCapturedVariableDescription>();
    public readonly List<(string Name, ISymbol Symbol)> AdditionalVariablesCapturedForScheduling = new List<(string, ISymbol)>();

    public bool WithStructuralChanges { get; }
    public readonly LambdaJobKind LambdaJobKind;

    public readonly ArgumentSyntax WithFilterEntityArray;

    internal readonly List<DataLookupFieldDescription> AdditionalFields;
    public readonly BlockSyntax RewrittenLambdaBody;

    internal readonly List<LambdaParamDescription> LambdaParameters = new List<LambdaParamDescription>();
    public string JobStructName => $"{Name}_Job";
    public bool NeedsEntityInQueryIndex => LambdaParameters.OfType<LambdaParamDescription_EntityInQueryIndex>().Any();
    public string ChunkBaseEntityIndexFieldName => $"{Name}_ChunkBaseEntityIndexArray";

    public bool IsForDOTSRuntime => SystemDescription.PreprocessorInfo.IsForDotsRuntime;
    public bool SafetyChecksEnabled => SystemDescription.PreprocessorInfo.IsUnityCollectionChecksEnabled;
    public bool DOTSRuntimeProfilerEnabled => SystemDescription.PreprocessorInfo.IsDotsRuntimeProfilerEnabled;
    public bool ProfilerEnabled =>
        SystemDescription.PreprocessorInfo.IsProfilerEnabled || SystemDescription.PreprocessorInfo.IsForUnityEditor || SystemDescription.PreprocessorInfo.IsDevelopmentBuildEnabled;
    public bool NeedsUnsafe => ContainingMethod.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.UnsafeKeyword)) ||
                               OriginalLambdaExpression.AncestorOfKindOrDefault<UnsafeStatementSyntax>() != null;
    public bool NeedsTimeData { get; }

    bool NeedsToPassSortKeyToOriginalLambdaBody => EntityCommandBufferParameter is {Playback: {ScheduleMode: ScheduleMode.ScheduleParallel}};
    bool IsDeferredPlaybackSystemSpecified => WithDeferredPlaybackSystemTypes.Any();
    bool HasManagedParameters => LambdaParameters.OfType<LambdaParamDescription_ManagedComponent>().Any();
    bool HasSharedComponentParameters => LambdaParameters.OfType<LambdaParamDescription_SharedComponent>().Any();

    List<INamedTypeSymbol> WithDeferredPlaybackSystemTypes { get; }
    bool WithImmediatePlayback { get; }

    public LambdaJobDescription(SystemDescription systemDescription, LambdaJobsCandidate candidate, MethodDeclarationSyntax containingMethod, int id)
    {
        try
        {
            SystemDescription = systemDescription;
            Location = candidate.Node.GetLocation();
            ContainingMethod = containingMethod;
            MethodInvocations = candidate.MethodInvocations;
            Schedule = GetScheduleModeAndDependencyArgument();
            ContainingInvocationExpression = MethodInvocations[Schedule.Mode.ToString()].FirstOrDefault();

            WithPresentTypes =
                AllTypeArgumentSymbolsOfMethod("WithPresent").Select(symbol =>
                    new Query
                    {
                        TypeSymbol = symbol,
                        Type = QueryType.Present,
                        IsReadOnly = true
                    }).ToArray();
            WithAbsentTypes =
                AllTypeArgumentSymbolsOfMethod("WithAbsent").Select(symbol =>
                    new Query
                    {
                        TypeSymbol = symbol,
                        Type = QueryType.Absent,
                        IsReadOnly = true
                    }).ToArray();
            WithDisabledTypes =
                AllTypeArgumentSymbolsOfMethod("WithDisabled").Select(symbol =>
                    new Query
                    {
                        TypeSymbol = symbol,
                        Type = QueryType.Disabled,
                        IsReadOnly = true
                    }).ToArray();
            WithAllTypes =
                AllTypeArgumentSymbolsOfMethod("WithAll").Select(symbol =>
                    new Query
                    {
                        TypeSymbol = symbol,
                        Type = QueryType.All,
                        IsReadOnly = true
                    }).ToArray();
            WithAnyTypes =
                AllTypeArgumentSymbolsOfMethod("WithAny").Select(symbol =>
                    new Query
                    {
                        TypeSymbol = symbol,
                        Type = QueryType.Any,
                        IsReadOnly = true
                    }
                ).ToArray();
            WithNoneTypes =
                AllTypeArgumentSymbolsOfMethod("WithNone").Select(symbol =>
                    new Query
                    {
                        TypeSymbol = symbol,
                        Type = QueryType.None,
                        IsReadOnly = true
                    }
                ).ToArray();
            WithChangeFilterTypes =
                AllTypeArgumentSymbolsOfMethod("WithChangeFilter").Select(symbol =>
                    new Query
                    {
                        TypeSymbol = symbol,
                        Type = QueryType.ChangeFilter,
                        IsReadOnly = true
                    }
                ).ToArray();
            WithSharedComponentFilterTypes =
                AllTypeArgumentSymbolsOfMethod("WithSharedComponentFilter").Select(symbol =>
                    new Query
                    {
                        TypeSymbol = symbol,
                        Type = QueryType.All,
                        IsReadOnly = true
                    }
                ).ToArray();

            WithDeferredPlaybackSystemTypes = AllTypeArgumentSymbolsOfMethod("WithDeferredPlaybackSystem");
            WithSharedComponentFilterArgumentSyntaxes = AllArgumentSyntaxesOfMethod("WithSharedComponentFilter").ToArray();
            WithStoreEntityQueryInFieldArgumentSyntaxes = AllArgumentSyntaxesOfMethod("WithStoreEntityQueryInField").ToArray();
            WithScheduleGranularityArgumentSyntaxes = AllArgumentSyntaxesOfMethod("WithScheduleGranularity").ToArray();

            EntityQueryOptions = GetEntityQueryOptions();

            AdditionalFields = new List<DataLookupFieldDescription>();

            var methodSymbol = systemDescription.SemanticModel.GetDeclaredSymbol(ContainingMethod);
            var stableHashCodeForMethod = GetStableHashCode($"{methodSymbol.ContainingType.ToFullName()}_{methodSymbol.GetMethodAndParamsAsString(this.SystemDescription)}") & 0x7fffffff;
            Name = GetName( $"{systemDescription.SystemTypeSyntax.Identifier}_{stableHashCodeForMethod:X}_LambdaJob_{id}");
            LambdaJobKind = candidate.LambdaJobKind;
            WithStructuralChanges = MethodInvocations.ContainsKey("WithStructuralChanges");
            WithImmediatePlayback = MethodInvocations.ContainsKey("WithImmediatePlayback");
            WithFilterEntityArray = SingleOptionalArgumentSyntaxOfMethod("WithFilter");
            Burst = GetBurstSettings();

            var methodInvocationWithLambdaExpression = LambdaJobKind switch
            {
                LambdaJobKind.Entities => MethodInvocations["ForEach"].FirstOrDefault(),
                LambdaJobKind.Job => MethodInvocations["WithCode"].FirstOrDefault(),
                _ => throw new InvalidOperationException("LambdaJob does not include required ForEach or WithCode invocation.")
            };

            // Check to see if the lambda expression is not valid, need to do this before we continue analyzing lambda
            var lambdaArgument = methodInvocationWithLambdaExpression.ArgumentList.Arguments.FirstOrDefault();
            if (lambdaArgument.Expression is ParenthesizedLambdaExpressionSyntax parenthesizedLambdaExpressionSyntax)
            {
                OriginalLambdaExpression = parenthesizedLambdaExpressionSyntax;
            }
            else
            {
                LambdaJobsErrors.DC0044(SystemDescription, Location, LambdaJobKind);
                throw new InvalidDescriptionException();
            }


            // Create parameter description from lambda parameters.
            if (OriginalLambdaExpression?.ParameterList.Parameters is {} parameters)
            {
                LambdaParameters = new List<LambdaParamDescription>(parameters.Count);
                foreach (var param in parameters)
                {
                    var paramDescription = LambdaParamDescription.From(this, param, Name);
                    if (paramDescription != null)
                        LambdaParameters.Add(paramDescription);
                    else
                        Success = false;
                }
            }

            var entityCommandsParameters =
                LambdaParameters.OfType<LambdaParamDescription_EntityCommandBuffer>().ToArray();
            if (entityCommandsParameters.Any())
            {
                var result = VerifyEcbCommandParameter(systemDescription, entityCommandsParameters);
                if (result.IsSuccess)
                {
                    result.Command.Playback = (IsImmediate: WithImmediatePlayback, Schedule.Mode, WithDeferredPlaybackSystemTypes.SingleOrDefault());
                    EntityCommandBufferParameter = result.Command;
                }
                else
                {
                    Success = false;
                    throw new InvalidDescriptionException();
                }
            }

            if (NeedsToPassSortKeyToOriginalLambdaBody)
            {
                LambdaParameters.Add(new LambdaParamDescription_BatchIndex());
            }

            var lambdaSyntax = OriginalLambdaExpression.Block ?? (SyntaxNode) OriginalLambdaExpression.ExpressionBody;

            // Can early out of a lot of analysis if we are only dealing with identifiers that are lambda params
            // (no captured variables or additional method calls)
            var hasNonParameterIdentifier = lambdaSyntax.DescendantNodes().OfType<SimpleNameSyntax>().Any(identifier => LambdaParameters.All(param => param.Name != identifier.Identifier.ToString()));

            if (hasNonParameterIdentifier)
            {
                // Discover captured variables
                // this must not include parameters as they can be captured by inner lambdas
                // or variables declared inside of lamba
                var dataFlowAnalysis = systemDescription.SemanticModel.AnalyzeDataFlow(lambdaSyntax);
                if (dataFlowAnalysis.Succeeded)
                {
                    foreach (var capturedVariable in dataFlowAnalysis.CapturedInside)
                    {
                        // Make sure not already captured or a lambda param
                        if (LambdaParameters.All(param => param.Name != capturedVariable.Name) &&
                            VariablesCaptured.All(param => param.Symbol.Name != capturedVariable.Name))
                        {
                            var capturedVariableDescription = new LambdaCapturedVariableDescription(capturedVariable);

                            if (capturedVariableDescription.Type is IErrorTypeSymbol errorTypeSymbol)
                            {
                                var location = errorTypeSymbol.Locations.FirstOrDefault();
                                if (location == null) // Could be null if type has never been defined
                                    location = lambdaSyntax.GetLocation();
                                LambdaJobsErrors.DC0086(SystemDescription, location, errorTypeSymbol);
                                Success = false;
                                return;  // We will hit further errors, so lets not soldier on
                            }

                            VariablesCaptured.Add(capturedVariableDescription);
                            if (Schedule.Mode != ScheduleMode.Run && !capturedVariableDescription.IsNativeContainer &&
                                dataFlowAnalysis.AlwaysAssigned.Contains(capturedVariable) && dataFlowAnalysis.DataFlowsOut.Contains(capturedVariable))
                            {
                                LambdaJobsErrors.DC0013(SystemDescription, Location, capturedVariable.Name, LambdaJobKind);
                                Success = false;
                            }
                        }
                    }

                    foreach (var localFunc in dataFlowAnalysis.UsedLocalFunctions)
                    {
                        var location = OriginalLambdaExpression.GetLocation();

                        // Find first invocation in lambda using that MethodSymbol and return location of it.
                        foreach (var node in lambdaSyntax.DescendantNodes())
                            if (node is InvocationExpressionSyntax { Expression: SimpleNameSyntax nameSyntax } invocation && nameSyntax.Identifier.ValueText == localFunc.Name
                                && SymbolEqualityComparer.Default.Equals(SystemDescription.SemanticModel.GetSymbolInfo(invocation.Expression).Symbol.OriginalDefinition, localFunc))
                            {
                                location = invocation.GetLocation();
                                break;
                            }

                        LambdaJobsErrors.DC0083(SystemDescription, location, LambdaJobKind, Schedule.Mode);
                        Success = false;
                    }
                }
            }

            // If we are also using any managed components or doing structural changes, we also need to capture this
            if ((HasManagedParameters || HasSharedComponentParameters || WithStructuralChanges)
                && VariablesCaptured.All(variable => !variable.IsThis))
            {
                VariablesCaptured.Add(new LambdaCapturedVariableDescription(systemDescription.SystemTypeSymbol, true));
            }

            // Also captured any variables used in expressions that construct shared component filters
            foreach (var sharedComponentFilterArgumentSyntax in WithSharedComponentFilterArgumentSyntaxes)
            {
                foreach (var identifier in sharedComponentFilterArgumentSyntax.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    var identifierSymbol = ModelExtensions.GetSymbolInfo(systemDescription.SemanticModel, identifier);
                    if (identifierSymbol.Symbol is ILocalSymbol || identifierSymbol.Symbol is IParameterSymbol &&
                        VariablesCaptured.All(variable => variable.OriginalVariableName != identifier.Identifier.Text))
                        AdditionalVariablesCapturedForScheduling.Add((identifier.Identifier.Text, identifierSymbol.Symbol));
                }
            }

            foreach (var entityAttribute in LambdaCapturedVariableDescription.AttributesDescriptions)
            {
                foreach (var argumentSyntax in AllArgumentSyntaxesOfMethod(entityAttribute.MethodName))
                {
                    var expression = argumentSyntax.Expression;
                    if (expression is IdentifierNameSyntax identifier)
                    {
                        var capturedVariable = VariablesCaptured.FirstOrDefault(v => v.Symbol.Name == identifier.Identifier.Text);
                        if (capturedVariable != null)
                        {
                            if (entityAttribute.IsApplicableToCaptured(SystemDescription, capturedVariable))
                                capturedVariable.Attributes.Add(entityAttribute.AttributeName);
                            else
                                Success = false;
                        }
                        else
                        {
                            var symbolInfo = systemDescription.SemanticModel.GetSymbolInfo(identifier);
                            if (symbolInfo.Symbol is ILocalSymbol)
                            {
                                // Captured variable is not used
                                LambdaJobsErrors.DC0035(SystemDescription, argumentSyntax.GetLocation(), identifier.ToString(), entityAttribute.MethodName);
                            }
                            else
                            {
                                // Not a local variable
                                LambdaJobsErrors.DC0012(SystemDescription, argumentSyntax.GetLocation(), identifier.ToString(), entityAttribute.MethodName);
                            }
                            Success = false;
                        }
                    }
                    else
                    {
                        LambdaJobsErrors.DC0012(SystemDescription, argumentSyntax.GetLocation(), expression.ToString(), entityAttribute.MethodName);
                        Success = false;
                    }
                }
            }

            // Either add DeallocateOnJobCompletion attributes to variables or add to list of variables that need to be disposed
            // (depending of if they support DeallocateOnJobCompletion and if we are running as a job)
            foreach (var argumentSyntax in AllArgumentSyntaxesOfMethod("WithDisposeOnCompletion"))
            {
                var expression = argumentSyntax.Expression;
                if (expression is IdentifierNameSyntax identifier)
                {
                    var capturedVariable = VariablesCaptured.FirstOrDefault(var => var.Symbol.Name == identifier.Identifier.Text);
                    if (capturedVariable != null)
                    {
                        if (Schedule.Mode != ScheduleMode.Run && capturedVariable.SupportsDeallocateOnJobCompletion())
                        {
                            capturedVariable.Attributes.Add("Unity.Collections.DeallocateOnJobCompletion");
                        }
                        else
                        {
                            DisposeOnJobCompletionVariables.Add(capturedVariable);
                        }
                    }
                    else
                    {
                        LambdaJobsErrors.DC0012(SystemDescription, argumentSyntax.GetLocation(), identifier.ToString(), "WithDisposeOnCompletion");
                        Success = false;
                    }
                }
                else
                {
                    LambdaJobsErrors.DC0012(SystemDescription, expression.GetLocation(), expression.ToString(), "WithDisposeOnCompletion");
                    Success = false;
                }
            }

            // Rewrite lambda body and get additional fields that are needed if lambda body is not emitted into system
            if (!hasNonParameterIdentifier)
                RewrittenLambdaBody = OriginalLambdaExpression.ToBlockSyntax();
            else
            {
                SyntaxNode rewrittenLambdaExpression;
                var rewriter = new LambdaBodyRewriter(this);
                (rewrittenLambdaExpression, AdditionalFields) = rewriter.Rewrite();
                NeedsTimeData = rewriter.NeedsTimeData;
                RewrittenLambdaBody = ((ParenthesizedLambdaExpressionSyntax) rewrittenLambdaExpression).ToBlockSyntax();
            }

            // Remove source that has been been disable with preprocessor directives (but remains as disabled text)
            RewrittenLambdaBody = (BlockSyntax)new DisabledTextTriviaRemover().Visit(RewrittenLambdaBody);

            // Check to see if we have any references to __this in our rewritten lambda body and we can't contain reference types
            // if there is none remove the capture this reference
            if (!CanContainReferenceTypes
                && RewrittenLambdaBody
                    .DescendantNodes()
                    .OfType<SimpleNameSyntax>()
                    .All(syntax => syntax.Identifier.ToString() != "__this"))
            {
                VariablesCaptured.RemoveAll(variable => variable.IsThis);
            }

            this.Verify();
        }
        catch (InvalidDescriptionException)
        {
            Success = false;
        }
    }

    private (bool IsSuccess, LambdaParamDescription_EntityCommandBuffer Command)
        VerifyEcbCommandParameter(SystemDescription systemDescription, IReadOnlyCollection<LambdaParamDescription_EntityCommandBuffer> entityCommandsParameters)
    {
        bool isSuccess = true;
        var ecbCommandParameter = entityCommandsParameters.First();

        if (!IsDeferredPlaybackSystemSpecified && !WithImmediatePlayback) // Missing playback instructions
        {
            LambdaJobsErrors.DC0074(systemDescription, ecbCommandParameter.Syntax.GetLocation());
            isSuccess = false;
        }

        if (IsDeferredPlaybackSystemSpecified && WithImmediatePlayback) // Conflicting playback instructions
        {
            LambdaJobsErrors.DC0075(systemDescription, WithDeferredPlaybackSystemTypes.First().Locations.First());
            isSuccess = false;
        }

        if (WithDeferredPlaybackSystemTypes.Count > 1) // More than one playback systems specified
        {
            LambdaJobsErrors.DC0078(systemDescription, WithDeferredPlaybackSystemTypes.First().Locations.First());
            isSuccess = false;
        }

        if (entityCommandsParameters.Count > 1)
        {
            LambdaJobsErrors.DC0076(systemDescription, entityCommandsParameters.First().Syntax.GetLocation());
            isSuccess = false;
        }

        if (WithImmediatePlayback && Schedule.Mode != ScheduleMode.Run)
        {
            LambdaJobsErrors.DC0077(systemDescription, ecbCommandParameter.Syntax.GetLocation());
            isSuccess = false;
        }

        if (isSuccess)
        {
            ecbCommandParameter = entityCommandsParameters.Single();
        }

        return (IsSuccess: isSuccess, ecbCommandParameter);
    }

    ArgumentSyntax SingleOptionalArgumentSyntaxOfMethod(string methodName)
    {
        return
            !MethodInvocations.TryGetValue(methodName, out var invocations)
                ? null
                : invocations.Select(methodInvocation => methodInvocation.ArgumentList.Arguments.First()).FirstOrDefault(arg => arg != null);
    }

    (bool IsEnabled, BurstSettings Settings) GetBurstSettings()
    {
        if (MethodInvocations.ContainsKey("WithoutBurst") || WithStructuralChanges)
            return (false, default);

        if (!MethodInvocations.TryGetValue("WithBurst", out var burstInvocations))
            return (true, default);

        BurstFloatMode? burstFloatMode = null;
        BurstFloatPrecision? burstFloatPrecision = null;
        bool? synchronousCompilation = null;
        var invocation = burstInvocations.First();

        // handle both named and unnamed arguments
        var argIndex = 0;
        var invalidBurstArg = false;
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var argumentName = argument.DescendantNodes().OfType<NameColonSyntax>().FirstOrDefault()?.Name;
            if (argumentName != null)
            {
                argIndex = argumentName.Identifier.ValueText switch
                {
                    "floatMode" => 0,
                    "floatPrecision" => 1,
                    "synchronousCompilation" => 2,
                    _ => argIndex
                };
            }

            var argValue = argument.Expression.ToString();
            switch (argIndex)
            {
                case 0:
                    if (TryParseQualifiedEnumValue(argValue, out BurstFloatMode mode))
                        burstFloatMode = mode;
                    else
                        invalidBurstArg = true;
                    break;
                case 1:
                    if (TryParseQualifiedEnumValue(argValue, out BurstFloatPrecision precision))
                        burstFloatPrecision = precision;
                    else
                        invalidBurstArg = true;
                    break;
                case 2:
                    if (bool.TryParse(argValue, out var synchronous))
                        synchronousCompilation = synchronous;
                    else
                        invalidBurstArg = true;
                    break;
            }

            argIndex++;
        }

        if (invalidBurstArg)
        {
            LambdaJobsErrors.DC0008(SystemDescription, invocation.GetLocation(), "WithBurst");
            Success = false;
        }

        return
            (true,
                new BurstSettings
                {
                    SynchronousCompilation = synchronousCompilation,
                    BurstFloatMode = burstFloatMode,
                    BurstFloatPrecision = burstFloatPrecision
                });
    }

    EntityQueryOptions GetEntityQueryOptions()
    {
        var options = EntityQueryOptions.Default;

        if (!MethodInvocations.TryGetValue("WithEntityQueryOptions", out var invocations))
        {
            return options;
        }

        foreach (var invocation in invocations)
        {
            var entityQueryOptionArgument = invocation.ArgumentList.Arguments.ElementAtOrDefault(0);
            if (entityQueryOptionArgument == null)
            {
                continue;
            }

            EntityQueryOptions option;
            var argExpr = entityQueryOptionArgument.Expression;

            while (argExpr is BinaryExpressionSyntax binSyntax)
            {
                if (TryParseQualifiedEnumValue(binSyntax.Right.ToString(), out option))
                {
                    options |= option;
                } else
                {
                    // !!! Need a test for this error
                    SystemGeneratorErrors.DC0064(SystemDescription, invocation.GetLocation());
                }

                argExpr = binSyntax.Left;
            }

            if (TryParseQualifiedEnumValue(argExpr.ToString(), out option))
            {
                options |= option;
            } else
            {
                // !!! Need a test for this error
                SystemGeneratorErrors.DC0064(SystemDescription, invocation.GetLocation());
            }
        }
        return options;
    }

    (ScheduleMode Mode, ArgumentSyntax Dependency) GetScheduleModeAndDependencyArgument()
    {
        if (MethodInvocations.ContainsKey("Run"))
            return (ScheduleMode.Run, default);
        if (MethodInvocations.TryGetValue("Schedule", out var schedulingInvocations))
            return (ScheduleMode.Schedule, schedulingInvocations.First().ArgumentList.Arguments.SingleOrDefault());
        if (MethodInvocations.TryGetValue("ScheduleParallel", out var schedulingParallelInvocations))
            return (ScheduleMode.ScheduleParallel, schedulingParallelInvocations.First().ArgumentList.Arguments.SingleOrDefault());

        Success = false;
        throw new InvalidDescriptionException();
    }

    // In the case of some copied source (lambda bodies in particular), we need to ensure that we remove any disabled text.
    // If we emit this back as source, the metadata marking the source as disabled text will be ignored and the it will include the
    // disabled text as valid source.
    class DisabledTextTriviaRemover : CSharpSyntaxRewriter
    {
        static bool IsTriviaToRemove(SyntaxTrivia syntaxTrivia) =>
            (syntaxTrivia.IsKind(SyntaxKind.DisabledTextTrivia) ||
             syntaxTrivia.IsKind(SyntaxKind.IfDirectiveTrivia) ||
             syntaxTrivia.IsKind(SyntaxKind.ElseDirectiveTrivia) ||
             syntaxTrivia.IsKind(SyntaxKind.ElifDirectiveTrivia) ||
             syntaxTrivia.IsKind(SyntaxKind.EndIfDirectiveTrivia));

        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            return base.VisitToken(token)
                .WithLeadingTrivia(token.LeadingTrivia.Where(trivia => !IsTriviaToRemove(trivia)))
                .WithTrailingTrivia(token.TrailingTrivia.Where(trivia => !IsTriviaToRemove(trivia)));
        }
    }

    IEnumerable<ArgumentSyntax> AllArgumentSyntaxesOfMethod(string methodName)
    {
        if (MethodInvocations.TryGetValue(methodName, out var invocations))
            foreach (var inv in invocations)
            foreach (var arg in inv.ArgumentList.Arguments)
                yield return arg;
    }

    List<INamedTypeSymbol> AllTypeArgumentSymbolsOfMethod(string methodName)
    {
        var result = new List<INamedTypeSymbol>();
        if (!MethodInvocations.ContainsKey(methodName))
        {
            return result;
        }

        foreach (var methodInvocation in MethodInvocations[methodName])
        {
            var symbol = (IMethodSymbol)SystemDescription.SemanticModel.GetSymbolInfo(methodInvocation).Symbol;

            // We can fail to get the symbol here, in that case we don't have access to the type
            // this will be reported by Roslyn with
            if (symbol == null)
            {
                Success = false;
                continue;
            }

            foreach (var argumentType in symbol.TypeArguments.OfType<ITypeParameterSymbol>())
            {
                SystemGeneratorErrors.DC0051(SystemDescription, Location, argumentType.Name, methodName);
                Success = false;
            }

            foreach (var argumentType in symbol.TypeArguments.OfType<INamedTypeSymbol>())
            {
                if (argumentType.IsGenericType)
                {
                    SystemGeneratorErrors.DC0051(SystemDescription, Location, argumentType.Name, methodName);
                    Success = false;
                    continue;
                }

                result.Add(argumentType);
            }
        }

        return result;
    }

    string GetName(string defaultName)
    {
        if (!MethodInvocations.TryGetValue("WithName", out var withNameInvocations))
            return defaultName;

        var invocation = withNameInvocations.First();
        var literalArgument = invocation.ArgumentList.Arguments.FirstOrDefault()?.DescendantNodes().OfType<LiteralExpressionSyntax>().FirstOrDefault();

        if (literalArgument == null)
        {
            LambdaJobsErrors.DC0008(SystemDescription, invocation.GetLocation(), "WithName");
            Success = false;
            return defaultName;
        }

        var customName = literalArgument.Token.ValueText;
        if (!IsValidLambdaName(customName))
        {
            LambdaJobsErrors.DC0043(SystemDescription, Location, customName);
            return defaultName;
        }

        return literalArgument.Token.ValueText;

        static bool IsValidLambdaName(string jobName)
        {
            if (jobName.Length == 0)
                return false;
            if (char.IsDigit(jobName[0]))
                return false;
            if (jobName.Any(t => t != '_' && !char.IsLetterOrDigit(t)))
                return false;

            // names with __ are reserved for the compiler by convention
            return !jobName.Contains("__");
        }
    }

    internal void AppendSchedulingInvocationTo(StringBuilder stringBuilder)
    {
        foreach (var leadingTrivia in ContainingInvocationExpression.GetLeadingTrivia())
            // Ensure neat indentation
            if (leadingTrivia.IsKind(SyntaxKind.WhitespaceTrivia))
                stringBuilder.Append(leadingTrivia);

        stringBuilder.Append($"{ExecuteInSystemMethodName}(");

        var args = GetExecuteMethodArgs().ToArray();
        for (var index = 0; index < args.Length; index++)
        {
            stringBuilder.Append(args[index]);

            if (index < args.Length - 1)
                stringBuilder.Append(", ");
        }
        stringBuilder.Append(")");

        IEnumerable<string> GetExecuteMethodArgs()
        {
            var argStrings = new HashSet<string>();
            foreach (var variable in VariablesCaptured)
            {
                if (!variable.IsThis)
                    argStrings.Add(Schedule.Mode == ScheduleMode.Run && variable.IsWritable
                        ? $"ref {variable.OriginalVariableName}"
                        : variable.OriginalVariableName);
            }

            if (Schedule.DependencyArgument != null)
                argStrings.Add(Schedule.DependencyArgument.ToString());

            foreach (var argument in AdditionalVariablesCapturedForScheduling)
                argStrings.Add(argument.Name);

            return argStrings;
        }
    }
}
