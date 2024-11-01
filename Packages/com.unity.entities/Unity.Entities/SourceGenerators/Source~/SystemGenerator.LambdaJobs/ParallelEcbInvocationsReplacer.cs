using System;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Unity.Entities.SourceGen.SystemGenerator.LambdaJobs.ArgumentParserForEntityCommandBufferMethods;
using static Unity.Entities.SourceGen.SystemGenerator.LambdaJobs.LambdaParamDescription_EntityCommandBuffer;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

internal static class ParallelEcbInvocationsReplacer
{
    public static InvocationExpressionSyntax CreateReplacement(InvocationExpressionSyntax originalNode, IInvocationOperation invocationOperation)
    {
        string replacementCode;
        var parsedArguments = Parse(originalNode, invocationOperation.TargetMethod);

        switch (invocationOperation.TargetMethod.Name)
        {
            case "AddComponent":
            {
                replacementCode = GetReplacementCodeForAddComponentInvocation(parsedArguments);
                break;
            }
            case "AddBuffer":
            {
                replacementCode = $"{GeneratedParallelWriterFieldNameInJobChunkType}.{parsedArguments.GenericTypeArgument}(__sortKey, e: {parsedArguments.Entity})";
                break;
            }
            case "AddSharedComponent":
            {
                replacementCode =
                    String.IsNullOrEmpty(parsedArguments.EntitiesNativeArray)
                        ? $"{GeneratedParallelWriterFieldNameInJobChunkType}.AddSharedComponent(__sortKey, e: {parsedArguments.Entity}, sharedComponent: {parsedArguments.SharedComponentData})"
                        : $"{GeneratedParallelWriterFieldNameInJobChunkType}.AddSharedComponent(__sortKey, entities: {parsedArguments.EntitiesNativeArray}, sharedComponent: {parsedArguments.SharedComponentData})";
                break;
            }
            case "AppendToBuffer":
            {
                replacementCode = $"{GeneratedParallelWriterFieldNameInJobChunkType}.AppendToBuffer(__sortKey, e: {parsedArguments.Entity}, element: {parsedArguments.BufferElementData})";
                break;
            }
            case "CreateEntity":
            {
                replacementCode =
                    parsedArguments.EntityArchetype == null
                        ? $"{GeneratedParallelWriterFieldNameInJobChunkType}.CreateEntity(__sortKey)"
                        : $"{GeneratedParallelWriterFieldNameInJobChunkType}.CreateEntity(__sortKey, {parsedArguments.EntityArchetype})";
                break;
            }
            case "DestroyEntity":
            {
                replacementCode =
                    String.IsNullOrEmpty(parsedArguments.EntitiesNativeArray)
                        ? $"{GeneratedParallelWriterFieldNameInJobChunkType}.DestroyEntity(__sortKey, e: {parsedArguments.Entity})"
                        : $"{GeneratedParallelWriterFieldNameInJobChunkType}.DestroyEntity(__sortKey, entities: {parsedArguments.EntitiesNativeArray})";
                break;
            }
            case "Instantiate":
            {
                replacementCode =
                    string.IsNullOrEmpty(parsedArguments.EntitiesNativeArray)
                        ? $"{GeneratedParallelWriterFieldNameInJobChunkType}.Instantiate(__sortKey, e: {parsedArguments.Entity})"
                        : $"{GeneratedParallelWriterFieldNameInJobChunkType}.Instantiate(__sortKey, e: {parsedArguments.Entity}, entities: {parsedArguments.EntitiesNativeArray})";
                break;
            }
            case "RemoveComponent":
            {
                replacementCode = GetReplacementCodeForRemoveComponentInvocation(parsedArguments);
                break;
            }
            case "SetBuffer":
            {
                replacementCode = $"{GeneratedParallelWriterFieldNameInJobChunkType}.{parsedArguments.GenericTypeArgument}(__sortKey, e: {parsedArguments.Entity})";
                break;
            }
            case "SetComponent":
            {
                replacementCode = $"{GeneratedParallelWriterFieldNameInJobChunkType}.SetComponent(__sortKey, e: {parsedArguments.Entity}, component: {parsedArguments.ComponentData})";
                break;
            }
            case "SetComponentEnabled":
            {
                replacementCode =
                    !String.IsNullOrEmpty(parsedArguments.GenericTypeArgument)
                        ? $"{GeneratedParallelWriterFieldNameInJobChunkType}.{parsedArguments.GenericTypeArgument}(__sortKey, e: {parsedArguments.Entity}, value: {parsedArguments.BooleanValue})"
                        : $"{GeneratedParallelWriterFieldNameInJobChunkType}.SetComponentEnabled(__sortKey, e: {parsedArguments.Entity}, componentType: {parsedArguments.ComponentType}, value: {parsedArguments.BooleanValue})";
                break;
            }
            case "SetName":
            {
                replacementCode = $"{GeneratedParallelWriterFieldNameInJobChunkType}.SetName(__sortKey, e: {parsedArguments.Entity}, name: {parsedArguments.FixedString64Bytes})";
                break;
            }
            case "SetSharedComponent":
            {
                replacementCode =
                    String.IsNullOrEmpty(parsedArguments.EntitiesNativeArray)
                        ? $"{GeneratedParallelWriterFieldNameInJobChunkType}.SetSharedComponent(__sortKey, e: {parsedArguments.Entity}, sharedComponent: {parsedArguments.SharedComponentData})"
                        : $"{GeneratedParallelWriterFieldNameInJobChunkType}.SetSharedComponent(__sortKey, entities: {parsedArguments.EntitiesNativeArray}, sharedComponent: {parsedArguments.SharedComponentData})";
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
        return (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(replacementCode);
    }

    static string GetReplacementCodeForAddComponentInvocation(ArgumentValues parsedArguments)
    {
        const string errorMessage = "Failed to parse invocation of RemoveComponent() with a generic type argument, an IComponentData argument, a ComponentType argument, OR a ComponentTypeSet argument.";

        if (String.IsNullOrEmpty(parsedArguments.EntitiesNativeArray))
        {
            if (!String.IsNullOrEmpty(parsedArguments.GenericTypeArgument))
                return $"{GeneratedParallelWriterFieldNameInJobChunkType}.{parsedArguments.GenericTypeArgument}(__sortKey, e: {parsedArguments.Entity})";

            if (!String.IsNullOrEmpty(parsedArguments.ComponentType))
                return $"{GeneratedParallelWriterFieldNameInJobChunkType}.AddComponent(__sortKey, e: {parsedArguments.Entity}, componentType: {parsedArguments.ComponentType})";

            if (!String.IsNullOrEmpty(parsedArguments.ComponentTypeSet))
                return $"{GeneratedParallelWriterFieldNameInJobChunkType}.AddComponent(__sortKey, e: {parsedArguments.Entity}, typeSet: {parsedArguments.ComponentTypeSet})";

            if (!String.IsNullOrEmpty(parsedArguments.ComponentData))
                return $"{GeneratedParallelWriterFieldNameInJobChunkType}.AddComponent(__sortKey, e: {parsedArguments.Entity}, component: {parsedArguments.ComponentData})";

            throw new ParserException(errorMessage);
        }

        if (!String.IsNullOrEmpty(parsedArguments.GenericTypeArgument))
            return $"{GeneratedParallelWriterFieldNameInJobChunkType}.{parsedArguments.GenericTypeArgument}(__sortKey, entities: {parsedArguments.EntitiesNativeArray})";

        if (!String.IsNullOrEmpty(parsedArguments.ComponentType))
            return $"{GeneratedParallelWriterFieldNameInJobChunkType}.AddComponent(__sortKey, entities: {parsedArguments.EntitiesNativeArray}, componentType: {parsedArguments.ComponentType})";

        if (!String.IsNullOrEmpty(parsedArguments.ComponentTypeSet))
            return $"{GeneratedParallelWriterFieldNameInJobChunkType}.AddComponent(__sortKey, entities: {parsedArguments.EntitiesNativeArray}, typeSet: {parsedArguments.ComponentTypeSet})";

        if (!String.IsNullOrEmpty(parsedArguments.ComponentData))
            return $"{GeneratedParallelWriterFieldNameInJobChunkType}.AddComponent(__sortKey, entities: {parsedArguments.EntitiesNativeArray}, component: {parsedArguments.ComponentData})";

        throw new ParserException(errorMessage);
    }

    static string GetReplacementCodeForRemoveComponentInvocation(ArgumentValues parsedArguments)
    {
        const string errorMessage = "Failed to parse invocation of RemoveComponent with a generic type argument, a ComponentType argument, OR a ComponentTypeSet argument.";

        if (String.IsNullOrEmpty(parsedArguments.EntitiesNativeArray))
        {
            if (!String.IsNullOrEmpty(parsedArguments.GenericTypeArgument))
                return $"{GeneratedParallelWriterFieldNameInJobChunkType}.{parsedArguments.GenericTypeArgument}(__sortKey, e: {parsedArguments.Entity})";

            if (!String.IsNullOrEmpty(parsedArguments.ComponentType))
                return $"{GeneratedParallelWriterFieldNameInJobChunkType}.RemoveComponent(__sortKey, e: {parsedArguments.Entity}, componentType: {parsedArguments.ComponentType})";

            if (!String.IsNullOrEmpty(parsedArguments.ComponentTypeSet))
                return $"{GeneratedParallelWriterFieldNameInJobChunkType}.RemoveComponent(__sortKey, e: {parsedArguments.Entity}, typeSet: {parsedArguments.ComponentTypeSet})";

            throw new ParserException(errorMessage);
        }

        if (!String.IsNullOrEmpty(parsedArguments.GenericTypeArgument))
            return $"{GeneratedParallelWriterFieldNameInJobChunkType}.{parsedArguments.GenericTypeArgument}(__sortKey, entities: {parsedArguments.EntitiesNativeArray})";

        if (!String.IsNullOrEmpty(parsedArguments.ComponentType))
            return $"{GeneratedParallelWriterFieldNameInJobChunkType}.RemoveComponent(__sortKey, entities: {parsedArguments.EntitiesNativeArray}, componentType: {parsedArguments.ComponentType})";

        if (!String.IsNullOrEmpty(parsedArguments.ComponentTypeSet))
            return $"{GeneratedParallelWriterFieldNameInJobChunkType}.RemoveComponent(__sortKey, entities: {parsedArguments.EntitiesNativeArray}, typeSet: {parsedArguments.ComponentTypeSet})";

        throw new ParserException(errorMessage);
    }
}
