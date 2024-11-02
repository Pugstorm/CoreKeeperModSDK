#nullable enable
using System;
using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.JobEntityGenerator;

public partial class JobEntityModule
{
    internal struct JobEntityInstanceInfo : ISystemCandidate
    {
        internal JobEntityCandidate Candidate { get; private set; }
        internal bool IsExtensionMethodUsed { get; private set; }
        TypeInfo _typeInfo;

        public string CandidateTypeName => Candidate.CandidateTypeName;
        public SyntaxNode Node => Candidate.Node;

        public static bool TryCreate(ref SystemDescription systemDescription, JobEntityCandidate candidate, out JobEntityInstanceInfo result)
        {
            result = new JobEntityInstanceInfo
            {
                Candidate = candidate
            };

            // Checks if the Candidate is JobEntity and Get Type info
            ExpressionSyntax? jobEntityInstance = null;

            switch (candidate.MemberAccessExpressionSyntax.Expression)
            {
                case IdentifierNameSyntax identifierNameSyntax:
                    jobEntityInstance = identifierNameSyntax;
                    break;
                case ObjectCreationExpressionSyntax objectCreationExpressionSyntax:
                    jobEntityInstance = objectCreationExpressionSyntax;
                    break;
            }

            if (jobEntityInstance == null)
                return false;

            result._typeInfo = systemDescription.SemanticModel.GetTypeInfo(jobEntityInstance);

            (bool isJobEntity, result.IsExtensionMethodUsed) = DoesTypeInheritJobEntityAndUseExtensionMethod(result._typeInfo);
            return isJobEntity;
        }

        public (bool Success, ObjectCreationExpressionSyntax? ObjectCreationExpressionSyntax, IdentifierNameSyntax? IdentifierNameSyntax, int JobArgumentIndexInExtensionMethod)
            TryGetJobArgumentUsedInSchedulingInvocation()
        {
            var result = Candidate.MemberAccessExpressionSyntax.Expression;

            if (IsExtensionMethodUsed)
            {
                // Find JobData Syntax Passed To ExtensionMethod
                int jobDataArgumentIndex = FindJobDataArgumentIndex(Candidate.Invocation);
                if (jobDataArgumentIndex == -1)
                    return default;

                result = Candidate.Invocation.ArgumentList.Arguments[jobDataArgumentIndex].Expression;
                return result switch
                {
                    ObjectCreationExpressionSyntax objectCreationExpressionSyntax => (true, objectCreationExpressionSyntax, null, jobDataArgumentIndex),
                    IdentifierNameSyntax identifierNameSyntax => (true, null, identifierNameSyntax, jobDataArgumentIndex),
                    _ => default
                };
            }

            return result switch
            {
                ObjectCreationExpressionSyntax objectCreationExpressionSyntax => (true, objectCreationExpressionSyntax, null, -1),
                IdentifierNameSyntax identifierNameSyntax => (true, null, identifierNameSyntax, -1),
                _ => default
            };
        }
        public string? GetAndAddScheduleExpression(
            ref SystemDescription systemDescription,
            int uniqueId,
            int jobArgumentIndexInExtensionMethod,
            string schedulingJobEntityInstanceArg,
            string? userDefinedQueryArg,
            string? userDefinedDependencyArg)
        {
            var jobEntityType = (INamedTypeSymbol)_typeInfo.Type!;

            // Update Job Info if Extension method is used
            if (IsExtensionMethodUsed)
            {
                // Get JobEntity Symbol Passed To ExtensionMethod - Using Candidate for Semantic Reference
                jobEntityType = Candidate.Invocation.ArgumentList.Arguments[jobArgumentIndexInExtensionMethod].Expression switch
                {
                    ObjectCreationExpressionSyntax objectCreationExpressionSyntax
                        when systemDescription.SemanticModel.GetSymbolInfo(objectCreationExpressionSyntax).Symbol is IMethodSymbol
                        {
                            ReceiverType: INamedTypeSymbol namedTypeSymbol
                        } => namedTypeSymbol,

                    IdentifierNameSyntax identifierNameSyntax
                        when systemDescription.SemanticModel.GetSymbolInfo(identifierNameSyntax).Symbol is ILocalSymbol
                        {
                            Type: INamedTypeSymbol namedTypeSymbol
                        } => namedTypeSymbol,
                    _ => null
                };
            }

            // Get Additional info
            var scheduleMode = ScheduleModeHelpers.GetScheduleModeFromNameOfMemberAccess(Candidate.MemberAccessExpressionSyntax);
            if (!systemDescription.TryGetSystemStateParameterName(Candidate, out var systemStateExpression))
                return null;

            // Get or create `__TypeHandle.MyJob__JobEntityHandle`
            var jobEntityHandle = systemDescription.QueriesAndHandles.GetOrCreateJobEntityHandle(jobEntityType, userDefinedQueryArg == null);
            var jobEntityHandleAccess = $"__TypeHandle.{jobEntityHandle}";

            // is DynamicQuery ? `userQuery` : `__TypeHandle.MyJob__JobEntityHandle.DefaultQuery`
            var entityQuery = userDefinedQueryArg ?? $"{jobEntityHandleAccess}.DefaultQuery";

            var schedulingMethodWriter = new SchedulingMethodWriter
            {
                ScheduleMode = scheduleMode,
                MethodId = uniqueId,
                FullTypeName =  jobEntityType.ToFullName(),
                JobEntityHandle = jobEntityHandle,
                CheckQuery = systemDescription.PreprocessorInfo.IsUnityCollectionChecksEnabled || systemDescription.PreprocessorInfo.IsDotsDebugMode
            };

            systemDescription.NewMiscellaneousMembers.Add(schedulingMethodWriter);

            return new SchedulingExpressionCreateInfo(
                schedulingMethodWriter.DefaultMethodName,
                scheduleMode,
                hasUserDefinedQuery: userDefinedQueryArg != null,
                schedulingJobEntityInstanceArg,
                entityQuery,
                userDefinedDependencyArg,
                systemStateExpression
            ).Write();
        }

        private static int FindJobDataArgumentIndex(InvocationExpressionSyntax invocationExpression)
        {
            int jobDataArgumentIndex = -1;

            for (var argumentIndex = 0; argumentIndex < invocationExpression.ArgumentList.Arguments.Count; argumentIndex++)
            {
                var argument = invocationExpression.ArgumentList.Arguments[argumentIndex];
                if (argument.NameColon == null)
                    if (argumentIndex == 0)
                        jobDataArgumentIndex = argumentIndex;
                    else
                        continue;
                else if (argument.NameColon.Name.Identifier.ValueText == "jobData")
                    jobDataArgumentIndex = argumentIndex;
            }

            return jobDataArgumentIndex;
        }

        enum ArgumentType
        {
            EntityQuery,
            Dependency,
            JobEntity
        }

        internal static (ExpressionSyntax? UserDefinedEntityQuery, ExpressionSyntax? UserDefinedDependency)
            GetUserDefinedQueryAndDependency(ref SystemDescription context, bool isExtensionMethodUsed, InvocationExpressionSyntax schedulingInvocation)
        {
            ExpressionSyntax? entityQueryArgument = null;
            ExpressionSyntax? dependencyArgument = null;

            for (var i = 0; i < schedulingInvocation.ArgumentList.Arguments.Count; i++)
            {
                var arg = schedulingInvocation.ArgumentList.Arguments[i];

                var type = arg.NameColon == null
                    ? ParseUnnamedArgument(ref context, i, isExtensionMethodUsed, arg)
                    : ParseNamedArgument(arg);

                switch (type)
                {
                    case ArgumentType.EntityQuery:
                        entityQueryArgument = arg.Expression;
                        continue;
                    case ArgumentType.Dependency:
                        dependencyArgument = arg.Expression;
                        continue;
                }
            }

            return (entityQueryArgument, dependencyArgument);
        }

        static ArgumentType ParseUnnamedArgument(ref SystemDescription context, int argumentPosition, bool isExtensionMethodUsed, ArgumentSyntax semanticOriginalArgument)
        {
            switch (argumentPosition+(isExtensionMethodUsed?0:1))
            {
                case 0:
                    return ArgumentType.JobEntity;
                case 1: // Could be EntityQuery or dependsOn
                {
                    if (semanticOriginalArgument.Expression is DefaultExpressionSyntax {Type: var defaultType})
                        return defaultType.ToString().Contains("JobHandle") ? ArgumentType.Dependency : ArgumentType.EntityQuery;

                    return context.SemanticModel.GetTypeInfo(semanticOriginalArgument.Expression).Type.ToFullName() == "global::Unity.Entities.EntityQuery"
                        ? ArgumentType.EntityQuery : ArgumentType.Dependency;
                }
                case 2: // dependsOn
                    return ArgumentType.Dependency;
            }

            throw new ArgumentOutOfRangeException();
        }

        static ArgumentType ParseNamedArgument(ArgumentSyntax argumentSyntax)
            => argumentSyntax.NameColon?.Name.Identifier.ValueText switch
            {
                "query" => ArgumentType.EntityQuery,
                "dependsOn" => ArgumentType.Dependency,
                "jobData" => ArgumentType.JobEntity,
                _ => throw new ArgumentOutOfRangeException()
            };

        struct SchedulingMethodWriter : IMemberWriter
        {
            public ScheduleMode ScheduleMode;
            public int MethodId;
            public string FullTypeName;
            public string JobEntityHandle;
            public bool CheckQuery;

            public string DefaultMethodName => $"__ScheduleViaJobChunkExtension_{MethodId}";

            public void WriteTo(IndentedTextWriter writer)
            {
                var containsReturn = !ScheduleMode.IsRun();

                var methodWithArguments = ScheduleMode.GetScheduleMethodWithArguments();
                var returnType = containsReturn ? "global::Unity.Jobs.JobHandle" : "void";
                var returnExpression = $"{"return ".EmitIfTrue(containsReturn)}__TypeHandle.{JobEntityHandle}.{methodWithArguments}";
                var refType = "ref ".EmitIfTrue(ScheduleMode.IsByRef());

                writer.WriteLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"{returnType} {DefaultMethodName}({refType}{FullTypeName} job, global::Unity.Entities.EntityQuery query, global::Unity.Jobs.JobHandle dependency, ref global::Unity.Entities.SystemState state, bool hasUserDefinedQuery)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"{FullTypeName}.InternalCompiler.CheckForErrors({ScheduleMode.GetScheduleTypeAsNumber()});");

                if (CheckQuery)
                {
                    writer.WriteLine("if (Unity.Burst.CompilerServices.Hint.Unlikely(hasUserDefinedQuery))");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine($"int requiredComponentCount = {FullTypeName}.InternalCompilerQueryAndHandleData.GetRequiredComponentTypeCount();");
                    writer.WriteLine("global::System.Span<Unity.Entities.ComponentType> requiredComponentTypes = stackalloc Unity.Entities.ComponentType[requiredComponentCount];");
                    writer.WriteLine($"{FullTypeName}.InternalCompilerQueryAndHandleData.AddRequiredComponentTypes(ref requiredComponentTypes);");
                    writer.WriteLine();
                    writer.WriteLine($"if (!{FullTypeName}.InternalCompilerQueryAndHandleData.QueryHasRequiredComponentsForExecuteMethodToRun(ref query, ref requiredComponentTypes))");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine("throw new global::System.InvalidOperationException(");
                    writer.WriteLine($"\"When scheduling an instance of `{FullTypeName}` with a custom query, the query must (at the very minimum) contain all the components required for `{FullTypeName}.Execute()` to run.\");");
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.Indent--;
                    writer.WriteLine("}");
                }

                if (containsReturn)
                    writer.WriteLine("dependency = ");
                writer.WriteLine($"__TypeHandle.{JobEntityHandle}.UpdateBaseEntityIndexArray(ref job, query, ");

                if (containsReturn)
                    writer.Write("dependency, ");

                writer.Write("ref state);");
                writer.WriteLine($"__TypeHandle.{JobEntityHandle}.AssignEntityManager(ref job, state.EntityManager);");
                writer.WriteLine($"__TypeHandle.{JobEntityHandle}.__TypeHandle.Update(ref state);");
                writer.WriteLine($"{returnExpression};");
                writer.Indent--;
                writer.WriteLine("}");
            }
        }

        static (bool IsKnownCandidate, bool IsExtensionMethodUsed) DoesTypeInheritJobEntityAndUseExtensionMethod(TypeInfo typeInfo) =>
            typeInfo.Type.InheritsFromInterface("Unity.Entities.IJobEntity")
                ? (IsKnownCandidate: true, IsExtensionMethodUsed: false) // IsExtensionMethodUsed is ignored if IsCandidate is false, so we don't need to test the same thing twice
                : (IsKnownCandidate: typeInfo.Type.ToFullName() == "global::Unity.Entities.IJobEntityExtensions", IsExtensionMethodUsed: true);
    }
}
