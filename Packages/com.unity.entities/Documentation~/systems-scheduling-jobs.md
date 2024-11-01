# Use the job system with Entities

The Entities package uses the [C# Job System](https://docs.unity3d.com/Manual/JobSystem.html) extensively. Whenever possible, you should use jobs in your system code. 

For main thread access, you can use C# `foreach` over the `Query` objects in [SystemAPI](xref:Unity.Entities.SystemAPI). For convenient job scheduling, you can use [IJobEntity](xref:Unity.Entities.IJobEntity). In niche situations, or when you want manual control, you can use [`IJobChunk`](iterating-data-ijobchunk.md)'s `Schedule()` and `ScheduleParallel()` methods, to transform data outside the main thread. 

When you schedule jobs, ECS keeps track of which systems read and write which components. Later systems' `Dependency` property will include the job handles of earlier systems' scheduled jobs, in cases where the set of components read and written overlap. 

For more information about systems, see [System concepts](concepts-systems.md).
