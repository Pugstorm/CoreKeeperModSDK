using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

public enum LambdaJobKind
{
    Job,
    Entities
}

public static class LambdaJobKindExtensions
{
    public static string ToName(this LambdaJobKind lambdaJobKind) => lambdaJobKind switch
    {
        LambdaJobKind.Job => "Job.WithCode",
        LambdaJobKind.Entities => "Entities.ForEach",
        _ => throw new ArgumentOutOfRangeException()
    };

    public static string ToNameOfValidAlternativeFeatures(this LambdaJobKind lambdaJobKind, ScheduleMode scheduleMode) => lambdaJobKind switch
    {
        LambdaJobKind.Job => "IJob",
        LambdaJobKind.Entities when scheduleMode == ScheduleMode.Run => "SystemAPI.Query, IJobEntity or IJobChunk",
        LambdaJobKind.Entities => "IJobEntity or IJobChunk",
        _ => throw new ArgumentOutOfRangeException(nameof(lambdaJobKind), lambdaJobKind, null)
    };
}

public struct LambdaJobsCandidate : ISystemCandidate
{
    public LambdaJobKind LambdaJobKind { get; set; }
    public SyntaxNode Node { get; set; }
    public TypeDeclarationSyntax ContainingSystemType { get; set; }
    public Dictionary<string, List<InvocationExpressionSyntax>> MethodInvocations { get; set; }
    public string CandidateTypeName => LambdaJobKind.ToName();
}
