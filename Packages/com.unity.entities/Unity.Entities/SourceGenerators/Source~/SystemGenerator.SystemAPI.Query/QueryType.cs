namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI.Query;

enum QueryType
{
    RefRW,
    RefRO,
    UnmanagedSharedComponent,
    ManagedSharedComponent,
    Aspect,
    DynamicBuffer,
    ValueTypeComponent,
    ManagedComponent,
    UnityEngineComponent,
    EnabledRefRW,
    EnabledRefRO,
    TagComponent,
    Invalid
}
