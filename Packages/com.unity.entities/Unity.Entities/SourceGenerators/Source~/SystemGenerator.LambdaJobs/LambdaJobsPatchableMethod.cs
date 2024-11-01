using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

class LambdaJobsPatchableMethod
{
    public Func<IMethodSymbol, LambdaBodyRewriter, InvocationExpressionSyntax, SyntaxNode> GeneratePatchedReplacementSyntax;
    public ComponentAccessRights AccessRights { get; private set; }

    public enum AccessorDataType
    {
        ComponentLookup,
        BufferLookup,
        AspectLookup,
        EntityStorageInfoLookup
    }

    public enum ComponentAccessRights
    {
        ReadOnly,
        ReadWrite,
        GetFromFirstMethodParam
    }

    static string[] GetArgumentsInOrder(InvocationExpressionSyntax originalNode, params string[] namedParameters)
    {
        var arguments = originalNode.ArgumentList.Arguments;
        if (arguments.Count > 1 && arguments.Count != namedParameters.Length)
            throw new InvalidOperationException(
                $"Must supply named parameters if there is more than one argument: {string.Join(", ", arguments)} {string.Join(", ", namedParameters)}.");

        var orderedArguments = new string[arguments.Count];
        var argumentName = arguments.Select(arg => arg.NameColon?.Name);
        var argumentsAndName = arguments.Zip(argumentName, (arg, name) => (arg, name));

        for (var i = 0; i < 2; i++)
        {
            if (i == 0) // First pass, go through named arguments and fill them in in correct placement
            {
                foreach (var arg in argumentsAndName.Where(arg => arg.name != null))
                {
                    var foundIdx = Array.FindIndex(namedParameters, checkArg => checkArg == arg.name.ToString());
                    if (foundIdx != -1)
                        orderedArguments[foundIdx] = arg.arg.Expression.ToString();
                    else
                        throw new InvalidOperationException($"Could not find named parameters {arg.name} in list of expected named parameters.");
                }
            }
            else // Second pass, fill in the rest of the arguments in first missing spot
            {
                var idx = 0;
                foreach (var arg in argumentsAndName.Where(arg => arg.name == null))
                {
                    while (!string.IsNullOrEmpty(orderedArguments[idx]))
                        idx++;
                    if (idx >= orderedArguments.Length)
                        throw new InvalidOperationException($"Could not fit named and unnamed arguments.");
                    orderedArguments[idx] = arg.arg.Expression.ToString();
                }
            }
        }

        return orderedArguments;
    }

    internal static readonly Dictionary<string, LambdaJobsPatchableMethod> PatchableMethods =
        new Dictionary<string, LambdaJobsPatchableMethod>
        {
            {
                "GetComponentLookup",
                new LambdaJobsPatchableMethod
                {
                    AccessRights = ComponentAccessRights.GetFromFirstMethodParam,
                    GeneratePatchedReplacementSyntax = (methodSymbol, rewriter, originalNode) =>
                    {
                        var arguments = GetArgumentsInOrder(originalNode, "isReadOnly");
                        var isReadOnly = arguments.Length > 0 && bool.Parse(arguments[0].ToLower());
                        var dataAccessField = rewriter.GetOrCreateDataAccessField(methodSymbol.TypeArguments.First(), isReadOnly, AccessorDataType.ComponentLookup);
                        return SyntaxFactory.ParseExpression($"{dataAccessField.FieldName}");
                    }
                }
            },
            {
                "GetComponent",
                new LambdaJobsPatchableMethod
                {
                    AccessRights = ComponentAccessRights.ReadOnly,
                    GeneratePatchedReplacementSyntax = (methodSymbol, rewriter, originalNode) =>
                    {
                        var dataAccessField = rewriter.GetOrCreateDataAccessField(
                            methodSymbol.TypeArguments.First(), true, AccessorDataType.ComponentLookup);
                        var entityArgument = GetArgumentsInOrder(originalNode, "entity").First();
                        return SyntaxFactory.ParseExpression($"{dataAccessField.FieldName}[{entityArgument}]");
                    }
                }
            },
            {
                "SetComponent",
                new LambdaJobsPatchableMethod
                {
                    AccessRights = ComponentAccessRights.ReadWrite,
                    GeneratePatchedReplacementSyntax = (methodSymbol, rewriter, originalNode) =>
                    {
                        var dataAccessField = rewriter.GetOrCreateDataAccessField(
                            methodSymbol.TypeArguments.First(), false, AccessorDataType.ComponentLookup);
                        var arguments = GetArgumentsInOrder(originalNode, "entity", "component");
                        return SyntaxFactory.ParseExpression($"{dataAccessField.FieldName}[{arguments[0]}] = {arguments[1]}");
                    }
                }
            },
            {
                "HasComponent",
                new LambdaJobsPatchableMethod
                {
                    AccessRights = ComponentAccessRights.ReadOnly,
                    GeneratePatchedReplacementSyntax = (methodSymbol, rewriter, originalNode) =>
                    {
                        var dataAccessField = rewriter.GetOrCreateDataAccessField(
                            methodSymbol.TypeArguments.First(), true, AccessorDataType.ComponentLookup);
                        var entityArgument = GetArgumentsInOrder(originalNode, "entity").First();
                        return SyntaxFactory.ParseExpression($"{dataAccessField.FieldName}.HasComponent({entityArgument})");
                    }
                }
            },
            {
                "GetBufferLookup",
                new LambdaJobsPatchableMethod
                {
                    AccessRights =  ComponentAccessRights.GetFromFirstMethodParam,
                    GeneratePatchedReplacementSyntax = (methodSymbol, rewriter, originalNode) =>
                    {
                        var arguments = GetArgumentsInOrder(originalNode, "isReadOnly");
                        var isReadOnly = arguments.Length > 0 && bool.Parse(arguments[0].ToLower());
                        var dataAccessField = rewriter.GetOrCreateDataAccessField(
                            methodSymbol.TypeArguments.First(), isReadOnly, AccessorDataType.BufferLookup);
                        return SyntaxFactory.ParseExpression($"{dataAccessField.FieldName}");
                    }
                }
            },
            {
                "GetBuffer",
                new LambdaJobsPatchableMethod
                {
                    AccessRights = ComponentAccessRights.ReadWrite,
                    GeneratePatchedReplacementSyntax = (methodSymbol, rewriter, originalNode) =>
                    {
                        var dataAccessField = rewriter.GetOrCreateDataAccessField(
                            methodSymbol.TypeArguments.First(), false, AccessorDataType.BufferLookup);
                        var entityArgument = GetArgumentsInOrder(originalNode, "entity", "isReadOnly").First();
                        return SyntaxFactory.ParseExpression($"{dataAccessField.FieldName}[{entityArgument}]");
                    }
                }
            },
            {
                "HasBuffer",
                new LambdaJobsPatchableMethod
                {
                    AccessRights = ComponentAccessRights.ReadOnly,
                    GeneratePatchedReplacementSyntax = (methodSymbol, rewriter, originalNode) =>
                    {
                        var dataAccessField = rewriter.GetOrCreateDataAccessField(
                            methodSymbol.TypeArguments.First(), true, AccessorDataType.BufferLookup);
                        var entityArgument = GetArgumentsInOrder(originalNode, "entity").First();
                        return SyntaxFactory.ParseExpression($"{dataAccessField.FieldName}.HasBuffer({entityArgument})");
                    }
                }
            },
            {
                "GetEntityStorageInfoLookup",
                new LambdaJobsPatchableMethod
                {
                    AccessRights = ComponentAccessRights.ReadOnly,
                    GeneratePatchedReplacementSyntax = (methodSymbol, rewriter, _) =>
                    {
                        var dataAccessField = rewriter.GetOrCreateDataAccessField(
                            methodSymbol.ContainingType, false, AccessorDataType.EntityStorageInfoLookup);
                        return SyntaxFactory.ParseExpression($"{dataAccessField.FieldName}");
                    }
                }
            },
            {
                "Exists",
                new LambdaJobsPatchableMethod
                {
                    AccessRights =  ComponentAccessRights.ReadOnly,
                    GeneratePatchedReplacementSyntax = (methodSymbol, rewriter, originalNode) =>
                    {
                        var entityArgument = GetArgumentsInOrder(originalNode, "entity").First();
                        var dataAccessField = rewriter.GetOrCreateDataAccessField(
                            methodSymbol.ContainingType, false, AccessorDataType.EntityStorageInfoLookup);
                        return SyntaxFactory.ParseExpression($"{dataAccessField.FieldName}.Exists({entityArgument})");
                    }
                }
            },
            {
                "GetAspect",
                new LambdaJobsPatchableMethod
                {
                    AccessRights = ComponentAccessRights.ReadWrite,
                    GeneratePatchedReplacementSyntax = (methodSymbol, rewriter, originalNode) =>
                    {
                        var dataAccessField = rewriter.GetOrCreateDataAccessField(
                            methodSymbol.TypeArguments.First(), false, AccessorDataType.AspectLookup);
                        var entityArgument = GetArgumentsInOrder(originalNode, "entity").First();
                        return SyntaxFactory.ParseExpression($"{dataAccessField.FieldName}[{entityArgument}]");
                    }
                }
            },
            {
                "GetComponentRO",
                new LambdaJobsPatchableMethod
                {
                    AccessRights = ComponentAccessRights.ReadOnly,
                    GeneratePatchedReplacementSyntax = (methodSymbol, rewriter, originalNode) =>
                    {
                        var dataAccessField = rewriter.GetOrCreateDataAccessField(
                            methodSymbol.TypeArguments.First(), true, AccessorDataType.ComponentLookup);
                        var entityArgument = GetArgumentsInOrder(originalNode, "entity").First();
                        return SyntaxFactory.ParseExpression($"{dataAccessField.FieldName}[{entityArgument}]");
                    }
                }
            },
            {
                "GetComponentRW",
                new LambdaJobsPatchableMethod
                {
                    AccessRights = ComponentAccessRights.ReadWrite,
                    GeneratePatchedReplacementSyntax = (methodSymbol, rewriter, originalNode) =>
                    {
                        var dataAccessField = rewriter.GetOrCreateDataAccessField(
                            methodSymbol.TypeArguments.First(), false, AccessorDataType.ComponentLookup);
                        var entityArgument = GetArgumentsInOrder(originalNode, "entity").First();
                        return SyntaxFactory.ParseExpression($"{dataAccessField.FieldName}[{entityArgument}]");
                    }
                }
            }
        };
}
