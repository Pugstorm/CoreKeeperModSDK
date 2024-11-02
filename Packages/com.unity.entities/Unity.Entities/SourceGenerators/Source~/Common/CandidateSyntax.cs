using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unity.Entities.SourceGen.Common;

public enum Module
{
    SystemApiContext,
    SystemApiQueryBuilder,
    Ife,
    IJobEntity,
    EntityQueryBulkOps,
    EntitiesForEach
}

public readonly struct CandidateSyntax : ISystemCandidate
{
    public CandidateSyntax(CandidateType type, CandidateFlags flags, SyntaxNode node)
    {
        Type = type;
        Flags = flags;
        Node = node;
    }

    public string CandidateTypeName => Type switch
    {
        <= CandidateType.MaxSystemAPI => $"SystemAPI.{Type.ToString()}",
        CandidateType.Ife => "SystemAPI.Query",
        CandidateType.QueryBuilder => "SystemAPI.QueryBuilder",
        CandidateType.EntityQueryBulkOps => "EntityQueryBulkOps",
        CandidateType.IJobEntity => "IJobEntity",
        CandidateType.EntitiesForEach => "Entities.ForEach",
        _ => throw new ArgumentOutOfRangeException()
    };

    public SyntaxNode Node { get; }
    public readonly CandidateType Type;
    public readonly CandidateFlags Flags;

    public static SimpleNameSyntax GetSimpleName(SyntaxNode newestNode) {
        if (newestNode is not InvocationExpressionSyntax invocation)
            return newestNode as SimpleNameSyntax;
        return invocation.Expression switch {
            MemberAccessExpressionSyntax member => member.Name,
            SimpleNameSyntax sn => sn,
            _ => null
        };
    }

    public Module GetOwningModule() =>
        Type switch
        {
            <= CandidateType.MaxSystemAPI => Module.SystemApiContext,
            CandidateType.Ife => Module.Ife,
            CandidateType.QueryBuilder => Module.SystemApiQueryBuilder,
            CandidateType.EntityQueryBulkOps => Module.EntityQueryBulkOps,
            CandidateType.IJobEntity => Module.IJobEntity,
            CandidateType.EntitiesForEach => Module.EntitiesForEach,
            _ => throw new ArgumentOutOfRangeException()
        };
}

public enum CandidateType
{
    TimeData = 1,
    GetComponentLookup = 2,
    GetComponent = 3,
    GetComponentRO = 4,
    GetComponentRW = 5,
    SetComponent = 6,
    HasComponent = 7,
    IsComponentEnabled = 8,
    SetComponentEnabled = 10,
    SingletonWithArgument = 11,
    GetBufferLookup = 12,
    GetBuffer = 13,
    HasBuffer = 14,
    IsBufferEnabled = 15,
    SetBufferEnabled = 16,
    GetEntityStorageInfoLookup = 17,
    Exists = 18,
    Aspect = 19,
    ComponentTypeHandle = 20,
    BufferTypeHandle = 21,
    SharedComponentTypeHandle = 22,
    EntityTypeHandle = 23,
    SingletonWithoutArgument = 24,
    MaxSystemAPI = 24,
    Ife = 25,
    IJobEntity = 26,
    QueryBuilder = 27,
    EntityQueryBulkOps = 28,
    EntitiesForEach = 29
}

[Flags]
public enum CandidateFlags {
    None = 0,
    ReadOnly = 1,
    NoGenericGeneration = 2,
    All = int.MaxValue
}
