#nullable enable
using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unity.Entities.SourceGen.JobEntityGenerator;

enum ScheduleMode
{
    Run,
    RunByRef,
    Schedule,
    ScheduleByRef,
    ScheduleParallel,
    ScheduleParallelByRef,
}

static class ScheduleModeHelpers
{
    internal static string GetScheduleMethodWithArguments(this ScheduleMode scheduleMode) =>
        scheduleMode switch
        {
            ScheduleMode.Run => "Run(ref job, query)",
            ScheduleMode.RunByRef => "Run(ref job, query)",

            ScheduleMode.Schedule => "Schedule(ref job, query, dependency)",
            ScheduleMode.ScheduleByRef => "Schedule(ref job, query, dependency)",

            ScheduleMode.ScheduleParallel => "ScheduleParallel(ref job, query, dependency)",
            ScheduleMode.ScheduleParallelByRef => "ScheduleParallel(ref job, query, dependency)",
            _ => throw new ArgumentOutOfRangeException()
        };

    // scheduleType 0:Run, 1:Schedule, 2:ScheduleParallel
    internal static int GetScheduleTypeAsNumber(this ScheduleMode scheduleMode)
        => (int) scheduleMode / 2;

    internal static ScheduleMode GetScheduleModeFromNameOfMemberAccess(MemberAccessExpressionSyntax memberAccessExpressionSyntax)
        => memberAccessExpressionSyntax.Name.Identifier.ValueText switch
        {
            "Run" => ScheduleMode.Run,
            "RunByRef" => ScheduleMode.RunByRef,
            "Schedule" => ScheduleMode.Schedule,
            "ScheduleByRef" => ScheduleMode.ScheduleByRef,
            "ScheduleParallel" => ScheduleMode.ScheduleParallel,
            "ScheduleParallelByRef" => ScheduleMode.ScheduleParallelByRef,
            _ => throw new ArgumentOutOfRangeException()
        };

    public static bool IsRun(this ScheduleMode mode) => mode == ScheduleMode.Run || mode == ScheduleMode.RunByRef;
    public static bool IsByRef(this ScheduleMode mode) => mode == ScheduleMode.RunByRef || mode == ScheduleMode.ScheduleByRef || mode == ScheduleMode.ScheduleParallelByRef;
}
