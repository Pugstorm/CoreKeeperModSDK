using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

internal static class ArgumentParserForEntityCommandBufferMethods
{
    internal class ParserException : Exception
    {
        public ParserException(string errorMessage) : base(errorMessage)
        {
        }
    }

    internal class ArgumentValues
    {
        public string BooleanValue { get; set; }
        public string BufferElementData { get; set; }
        public string ComponentData { get; set; }
        public string ComponentType { get; set; }
        public string ComponentTypeSet { get; set; }
        public string Entity { get; set; }
        public string EntityArchetype { get; set; }
        public string EntitiesNativeArray { get; set; }
        public string GenericTypeArgument { get; set; }
        public string FixedString64Bytes { get; set; }
        public string SharedComponentData { get; set; }
    }

    public static ArgumentValues Parse(InvocationExpressionSyntax invocationExpressionSyntax, IMethodSymbol method)
    {
        var results = new ArgumentValues();

        var passedArguments =
            invocationExpressionSyntax.ChildNodes().OfType<ArgumentListSyntax>().SelectMany(list => list.Arguments).ToArray();

        for (int i = 0; i < passedArguments.Length; i++)
        {
            var argument = passedArguments[i];
            var namedArgument = argument.ChildNodes().OfType<NameColonSyntax>().SingleOrDefault();

            var (type, value) = namedArgument == null
                ? ParseUnnamedArgument(argument, i, method.Parameters)
                : ParseNamedArgument(argument, argumentName: namedArgument.Name.Identifier.ValueText);

            switch (type)
            {
                case ArgumentType.Bool:
                    results.BooleanValue = value;
                    break;
                case ArgumentType.BufferElementData:
                    results.BufferElementData = value;
                    break;
                case ArgumentType.ComponentDataStruct:
                    results.ComponentData = value;
                    break;
                case ArgumentType.ComponentType:
                    results.ComponentType = value;
                    break;
                case ArgumentType.ComponentTypeSet:
                    results.ComponentTypeSet = value;
                    break;
                case ArgumentType.Entity:
                    results.Entity = value;
                    break;
                case ArgumentType.EntitiesNativeArray:
                    results.EntitiesNativeArray = value;
                    break;
                case ArgumentType.EntityArchetype:
                    results.EntityArchetype = value;
                    break;
                case ArgumentType.FixedString64Bytes:
                    results.FixedString64Bytes = value;
                    break;
                case ArgumentType.SharedComponentData:
                    results.SharedComponentData = value;
                    break;
            }
        }

        bool mustSpecifyGenericTypeArgument =
            method.Name != "CreateEntity"
            && method.Name != "DestroyEntity"
            && method.Name != "Instantiate"
            && String.IsNullOrEmpty(results.ComponentData)
            && String.IsNullOrEmpty(results.ComponentType)
            && String.IsNullOrEmpty(results.ComponentTypeSet)
            && String.IsNullOrEmpty(results.SharedComponentData)
            && String.IsNullOrEmpty(results.BufferElementData)
            && String.IsNullOrEmpty(results.FixedString64Bytes);

        if (mustSpecifyGenericTypeArgument)
        {
            var memberAccessExpressionSyntax = invocationExpressionSyntax.Expression as MemberAccessExpressionSyntax;
            results.GenericTypeArgument = ((GenericNameSyntax)memberAccessExpressionSyntax?.Name).ToString();
        }

        return results;
    }

    static (ArgumentType Type, string Value) ParseNamedArgument(ArgumentSyntax argument, string argumentName)
    {
        var argumentValue = argument.Expression.ToString();

        return argumentName switch
        {
            "archetype" => (ArgumentType.EntityArchetype, argumentValue),
            "e" => (ArgumentType.Entity, argumentValue),
            "entities" => (ArgumentType.EntitiesNativeArray, argumentValue),
            "component" => (ArgumentType.ComponentDataStruct, argumentValue),
            "sharedComponent" => (ArgumentType.SharedComponentData, argumentValue),
            "componentType" => (ArgumentType.ComponentType, argumentValue),
            "componentTypeSet" => (ArgumentType.ComponentTypeSet, argumentValue),
            "name" => (ArgumentType.FixedString64Bytes, argumentValue),
            "value" => (ArgumentType.Bool, argumentValue),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    static (ArgumentType Type, string Value) ParseUnnamedArgument(ArgumentSyntax argument, int position, IEnumerable<IParameterSymbol> methodParameters)
    {
        var argumentTypes = methodParameters.Select(GetArgumentType).ToArray();
        return (argumentTypes[position], argument.Expression.ToString());

        ArgumentType GetArgumentType(IParameterSymbol parameterSymbol)
        {
            switch (parameterSymbol.Type.ToFullName())
            {
                case "global::System.Boolean":
                case "bool":
                    return ArgumentType.Bool;
                case "global::Unity.Entities.ComponentType":
                    return ArgumentType.ComponentType;
                case "global::Unity.Entities.ComponentTypeSet":
                    return ArgumentType.ComponentTypeSet;
                case "global::Unity.Entities.Entity":
                    return ArgumentType.Entity;
                case "global::Unity.Entities.EntityArchetype":
                    return ArgumentType.EntityArchetype;
                case "global::Unity.Collections.NativeArray<global::Unity.Entities.Entity>":
                    return ArgumentType.EntitiesNativeArray;
                case "global::Unity.Collections.FixedString64Bytes":
                    return ArgumentType.FixedString64Bytes;
            }

            if (parameterSymbol.Type.ImplementsInterface("Unity.Entities.IComponentData"))
                return ArgumentType.ComponentDataStruct;

            if (parameterSymbol.Type.ImplementsInterface("Unity.Entities.IBufferElementData"))
                return ArgumentType.BufferElementData;

            if (parameterSymbol.Type.ImplementsInterface("Unity.Entities.ISharedComponentData"))
                return ArgumentType.SharedComponentData;

            throw new ArgumentOutOfRangeException();
        }
    }

    enum ArgumentType
    {
        Bool,
        BufferElementData,
        ComponentDataStruct,
        ComponentType,
        ComponentTypeSet,
        EntitiesNativeArray,
        Entity,
        EntityArchetype,
        FixedString64Bytes,
        SharedComponentData,
    }
}
