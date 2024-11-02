---
uid: iterating-data-ijobchunk
---

# Iterate over chunks of data with IJobChunk

To iterate through your data at the level of entire [chunks](concepts-archetypes.md#archetype-chunks) of entities, implement [IJobChunk](xref:Unity.Entities.IJobChunk) inside a system  

When you schedule an `IJobChunk` job in the [OnUpdate](xref:Unity.Entities.SystemBase.OnUpdate*) method of a system, the system uses the entity query you pass to the schedule method to identify the chunks that it should pass to the job.

The job invokes your `Execute` method once for each matching chunk, and excludes those where no entities match the query because of [enableable components](components-enableable.md). In your job’s `Execute` method, you can iterate over the data inside each chunk, entity by entity. 

> [!NOTE]
> Iterating with `IJobChunk` is more complicated and requires more code setup than using `IJobEntity` (which generates an `IJobChunk` under the hood). To automatically benefit from any future source-generation improvements, most jobs that perform a single iteration over a chunk's entities should prefer `IJobEntity` (optionally implementing the `IJobEntityChunkBeginEnd` interface as well, for any custom chunk-level operations before and after the core entity iteration loop).
> 
> Some examples of use cases which require `IJobChunk`:
> - jobs which do not iterate over each chunk's entities at all (e.g. gathering per-chunk statistics)
> - jobs which perform multiple iterations over a chunk's entities, or which iterate in a unusual order.

For more information, the [ECS samples repository](https://github.com/Unity-Technologies/EntityComponentSystemSamples) contains a simple HelloCube example that demonstrates how to use `IJobChunk`.

## Additional resources

* [Implementing IJobChunk](iterating-data-ijobchunk-implement.md)
