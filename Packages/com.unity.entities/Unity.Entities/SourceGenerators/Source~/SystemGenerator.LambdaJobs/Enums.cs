namespace Unity.Entities.SourceGen.SystemGenerator.LambdaJobs;

public enum ScheduleMode
{
    ScheduleParallel,
    Schedule,
    Run
}

public enum BurstFloatMode
{
    Default,
    Strict,
    Deterministic,
    Fast,
}

public enum BurstFloatPrecision
{
    Standard,
    High,
    Medium,
    Low,
}
