# Systems comparison

To create a system, you can use either [`ISystem`](xref:Unity.Entities.ISystem) or [`SystemBase`](xref:Unity.Entities.SystemBase). `ISystem` provides access to unmanaged memory, whereas `SystemBase` is useful for storing managed data. You can use both system types with all of the Entities package and the job system. The following outlines the differences between the two system types

## Differences between systems

[`ISystem`](systems-isystem.md) is compatible with Burst, is faster than `SystemBase`, and has a value-based representation. In general, you should use `ISystem` over `SystemBase` to get better performance benefits. However, [`SystemBase`](systems-systembase.md) has convenient features at the compromise of using garbage collection allocations or increased `SourceGen` compilation time. 

The following table outlines their compatibility:

|**Feature**|**ISystem compatibility**|**SystemBase compatibility**|
|---|---|---|
|Burst compile `OnCreate`, `OnUpdate`, and `OnDestroy`|Yes|No|
|Unmanaged memory allocated|Yes|No|
|GC allocated|No|Yes|
|Can store managed data directly in system type|No|Yes|
|[Idiomatic `foreach`](systems-systemapi-query.md)|Yes|Yes|
|[`Entities.ForEach`](xref:Unity.Entities.SystemBase.Entities)|No|Yes|
|[`Job.WithCode`](xref:Unity.Entities.SystemBase.Job)|No|Yes|
|[`IJobEntity`](xref:Unity.Entities.IJobEntity)|Yes|Yes|
|[`IJobChunk`](xref:Unity.Entities.IJobChunk)|Yes|Yes|
|Supports inheritance|No|Yes|

## Multiple system instances

You can manually create multiple instances of the same system type in a [`World`](xref:Unity.Entities.World) at runtime and track the SystemHandle for each instance. However, general APIs such as [`GetExistingSystem`](xref:Unity.Entities.World.GetExistingSystem*) and [`GetOrCreateSystem`](xref:Unity.Entities.World.GetOrCreateSystem*) don't support multiple system instances.

You can use the [`CreateSystem`](xref:Unity.Entities.World.CreateSystem*) API to create runtime systems.

## Additional resources

* [Using `ISystem`](systems-isystem.md)
* [Using `SystemBase`](systems-systembase.md)
