using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

class DataLookupFieldDescription
{
    public bool IsReadOnly { get; }
    public ITypeSymbol Type { get; }
    public LambdaJobsPatchableMethod.AccessorDataType AccessorDataType { get; }
    public string FieldName { get; }

    public DataLookupFieldDescription(bool isReadOnly, ITypeSymbol type, LambdaJobsPatchableMethod.AccessorDataType accessorDataType)
    {
        IsReadOnly = isReadOnly;
        Type = type;
        AccessorDataType = accessorDataType;
        FieldName = AccessorDataType switch
        {
            LambdaJobsPatchableMethod.AccessorDataType.ComponentLookup =>
                $"__{Type.ToValidIdentifier()}_ComponentLookup",
            LambdaJobsPatchableMethod.AccessorDataType.BufferLookup =>
                $"__{Type.ToValidIdentifier()}_BufferLookup",
            LambdaJobsPatchableMethod.AccessorDataType.AspectLookup =>
                $"__{Type.ToValidIdentifier()}_AspectLookup",
            LambdaJobsPatchableMethod.AccessorDataType.EntityStorageInfoLookup =>
                "__EntityStorageInfoLookup",
            _ => throw new ArgumentOutOfRangeException($"Passed in invalid {nameof(LambdaJobsPatchableMethod.AccessorDataType)} enum")
        };
    }

    public string JobStructAssign()
    {
        return AccessorDataType switch
        {
            LambdaJobsPatchableMethod.AccessorDataType.ComponentLookup =>
                $"{FieldName} = __TypeHandle.__{Type.ToValidIdentifier()}_{(IsReadOnly ? "RO" : "RW")}_ComponentLookup",
            LambdaJobsPatchableMethod.AccessorDataType.BufferLookup =>
                $"{FieldName} = __TypeHandle.__{Type.ToValidIdentifier()}_{(IsReadOnly ? "RO" : "RW")}_BufferLookup",
            LambdaJobsPatchableMethod.AccessorDataType.AspectLookup =>
                $"{FieldName} = __TypeHandle.__{Type.ToValidIdentifier()}_{(IsReadOnly ? "RO" : "RW")}_AspectLookup",
            LambdaJobsPatchableMethod.AccessorDataType.EntityStorageInfoLookup =>
                $"{FieldName} = __TypeHandle.__EntityStorageInfoLookup",
            _ => throw new ArgumentOutOfRangeException($"Passed in invalid {nameof(LambdaJobsPatchableMethod.AccessorDataType)} enum")
        };
    }
    public string FormatUpdateInvocation()
    {
        var fieldNameWithReadAccess = AccessorDataType switch
        {
            LambdaJobsPatchableMethod.AccessorDataType.ComponentLookup =>
                $"__{Type.ToValidIdentifier()}_{(IsReadOnly ? "RO" : "RW")}_ComponentLookup",
            LambdaJobsPatchableMethod.AccessorDataType.BufferLookup =>
                $"__{Type.ToValidIdentifier()}_{(IsReadOnly ? "RO" : "RW")}_BufferLookup",
            LambdaJobsPatchableMethod.AccessorDataType.AspectLookup =>
                $"__{Type.ToValidIdentifier()}_{(IsReadOnly ? "RO" : "RW")}_AspectLookup",
            LambdaJobsPatchableMethod.AccessorDataType.EntityStorageInfoLookup =>
                "__EntityStorageInfoLookup",
            _ => throw new ArgumentOutOfRangeException($"Passed in invalid {nameof(LambdaJobsPatchableMethod.AccessorDataType)} enum")
        };
        return $@"__TypeHandle.{fieldNameWithReadAccess}.Update(ref this.CheckedStateRef);";
    }

    public FieldDeclarationSyntax ToFieldDeclaration()
    {
        var accessAttribute = IsReadOnly ? "[global::Unity.Collections.ReadOnly]" : string.Empty;

        var dataLookupType = AccessorDataType switch
        {
            LambdaJobsPatchableMethod.AccessorDataType.ComponentLookup => "global::Unity.Entities.ComponentLookup",
            LambdaJobsPatchableMethod.AccessorDataType.BufferLookup => "global::Unity.Entities.BufferLookup",
            LambdaJobsPatchableMethod.AccessorDataType.EntityStorageInfoLookup => "global::Unity.Entities.EntityStorageInfoLookup",
            LambdaJobsPatchableMethod.AccessorDataType.AspectLookup => $"{Type.ToFullName()}.Lookup",
            _ => throw new ArgumentOutOfRangeException($"Passed in invalid {nameof(LambdaJobsPatchableMethod.AccessorDataType)} enum")
        };

        var typeArgumentSnippet = $"<{Type.ToFullName()}>".EmitIfTrue(AccessorDataType == LambdaJobsPatchableMethod.AccessorDataType.ComponentLookup || AccessorDataType == LambdaJobsPatchableMethod.AccessorDataType.BufferLookup);
        var template = $@"{accessAttribute} public {dataLookupType}{typeArgumentSnippet} {FieldName};";


        return (FieldDeclarationSyntax) SyntaxFactory.ParseMemberDeclaration(template);
    }
}
