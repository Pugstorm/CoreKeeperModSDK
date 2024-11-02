# Implement IJobChunk

To implement `IJobChunk`:

1. [Query data with an EntityQuery](#query-data-with-an-entityquery) to identify the entities that you want to process.
2. Use `IJobChunk` to [define the job struct](#define-the-job-struct).
3. [Declare the data your job accesses](#declare-the-data-your-job-accesses). On the job structure, include fields for [ComponentTypeHandle](xref:Unity.Entities.ComponentTypeHandle`1) objects that identify the types of components the job must directly access. Also, specify whether the job reads or writes to those components. You can also include fields that identify data you want to look up for entities that aren’t part of the query, and fields for non-entity data.
4. [Write the Execute method](#write-the-execute-method) of the job struct to transform your data. Get the [NativeArray](xref:Unity.Collections.NativeArray`1) instances for the components the job reads or writes, and then use [ChunkEntityEnumerator](xref:Unity.Entities.ChunkEntityEnumerator) to iterate over the current chunk and perform the desired work.
5. [Schedule the job](#schedule-the-job) in the system's `OnUpdate` method, and pass the EntityQuery that identifies the entities to process to the Schedule method.

## Query data with an EntityQuery

An [EntityQuery](systems-entityquery-intro.md) defines the set of component types that an [EntityArchetype](xref:Unity.Entities.EntityArchetype) must contain for the system to process its associated chunks and entities. An archetype can have additional components, but it must have at least those that the query defines. You can also exclude archetypes that contain specific types of components. 

Pass the query that selects the entities your job should process to the schedule method that you use to schedule the job.

See [Create an EntityQuery](systems-entityquery-create.md) for information about how to define an entity query.

### Optional components

Don't include optional components in the `EntityQuery`. To handle optional components, use the [ArchetypeChunk.Has](xref:Unity.Entities.ArchetypeChunk.Has*) method inside [IJobChunk.Execute](xref:Unity.Entities.IJobChunk.Execute*). This determines whether the current [ArchetypeChunk](xref:Unity.Entities.ArchetypeChunk) has the optional component or not. Because all entities in the same chunk have the same components, you only need to check whether an optional component exists once per chunk, not once per entity.

## Define the job struct

A job struct consists of an [Execute](xref:Unity.Entities.IJobChunk.Execute*) method that does the work to be performed, and fields that declare the data that the `Execute` method uses.

A typical `IJobChunk` job struct looks like this:

[!code-cs[typical-struct](../DocCodeSamples.Tests/JobChunkExamples.cs#typical-struct)]

This example accesses the data for two components of an entity, VelocityVector, and Translation. It then calculates a new translation based on the time elapsed since the last update.

### Compute the indices of matching chunks and entities

Sometimes, an `IJobChunk` needs an individual index for each entity or chunk that matches the provided `EntityQuery`. For example, if you calculate a unique result for each entity, you could use this index to write each result to a different element of a [NativeArray](xref:Unity.Collections.NativeArray`1).

Computing these indices requires a separate pass over the query's matching chunks before the job runs. `EntityQuery` has a helper method that computes this array. The following is an example of how to do this:

1. Add a `NativeArray<int> ChunkBaseEntityIndices` field to your `IJobChunk` implementation. This array contains the base entity index for each chunk. This is the index of the first entity in the chunk, relative to the set of all entities that match a query including any active filters or enableable components.
2. On the main thread, call [EntityQuery.CalculateBaseEntityIndexArrayAsync](xref:Unity.Entities.EntityQuery.CalculateBaseEntityIndexArrayAsync*) on the `EntityQuery`. This allocates a NativeArray of `int`s (one per matching chunk in the query), and schedules a job to populate the array.
3. Assign the output array to the new `ChunkBaseEntityIndices` field, and schedule the `IJobChunk`. Add the `JobHandle` that `EntityQuery.CalculateBaseEntityIndexArrayAsync` returns to the `IJobChunk`'s input dependencies. This ensures the helper job completes before the main job runs.
4. Inside the job's [Execute](xref:Unity.Entities.IJobChunk.Execute*) method, look up the current chunk's base entity index with `baseEntityIndex = ChunkBaseEntityIndices[unfilteredChunkIndex]`. This is the index for the first entity the chunk processes. The second entity that the chunk processes is `baseEntityIndex+1`, and so on.
5. Call `.Dispose()` on the NativeArray from step 2 if necessary, after the job has finished executing. The easiest way to avoid this step is to use an allocator that doesn't require explicit disposal of allocations, such as `World.UpdateAllocator`.

When you add commands to an [EntityCommandBuffer.ParallelWriter](xref:Unity.Entities.EntityCommandBuffer.ParallelWriter), use the `unfilteredChunkIndex` parameter as the `sortKey` argument of the command buffer methods. You don't need to compute and pass unique sort key for each entity.

## Declare the data your job accesses

The fields in your job struct declare the data available to your `Execute` methods. These fields fall into the following categories:

* **[ComponentTypeHandle](xref:Unity.Entities.ComponentTypeHandle`1) fields**: Allows your Execute method to access the entity components and buffers stored in the current chunk. 
* **[ComponentLookup](xref:Unity.Entities.ComponentLookup`1) and [BufferLookup](xref:Unity.Entities.BufferLookup`1) fields**: Allows your Execute method to look up data for any entity no matter where it's stored. This random access type is the least efficient way to access data and you should only use it when necessary.
* **Other fields**: You can declare other fields for your struct as needed. You can set the value of such fields each time you schedule the job.
* **Output fields:** You can also write to [NativeContainer](xref:JobSystemNativeContainer) fields declared for the job struct. These fields must be a NativeContainer, such as a `NativeArray`, and you can't use other data types.

### ComponentTypeHandle fields

To access data stored in a component of one of the entities in the query perform the following steps:

1. Define a [ComponentTypeHandle](xref:Unity.Entities.ComponentTypeHandle`1) field on the job struct, and set `T` to the data type of the component. For example:

    [!code-cs[component-handle](../DocCodeSamples.Tests/JobChunkExamples.cs#component-handle)]

1. Use this handle field inside the job’s `Execute` method to access the array that contains the data for that type component as a `NativeArray`. This array contains an element for every entity in the chunk:

    [!code-cs[component-array](../DocCodeSamples.Tests/JobChunkExamples.cs#component-array)]

1. Declare a `ComponentTypeHandle` field on the system that you want to schedule the job. It's more efficient to create type handles once and update them each frame, rather than creating them just-in-time.

    [!code-cs[component-handle-system-declaration](../DocCodeSamples.Tests/JobChunkExamples.cs#component-handle-system-declaration)]

1. Use [ComponentSystemBase.GetComponentTypeHandle](xref:Unity.Entities.ComponentSystemBase.GetComponentTypeHandle*) to initialize the system's type handle fields in the system's `OnCreate` method.

    [!code-cs[component-handle-system-initialization](../DocCodeSamples.Tests/JobChunkExamples.cs#component-handle-system-initialization)]

1. Schedule the job in the system’s `OnUpdate` method, update the system's type handle field, and assign a value to the type handle field:

    [!code-cs[component-set-handle](../DocCodeSamples.Tests/JobChunkExamples.cs#component-set-handle)]

Always update and set the component handle fields of a job every time you schedule the job. A type handle that isn't updated has a stale [version number](systems-version-numbers.md), and is flagged as an error.

Each array of component data in a chunk is aligned so that a given index corresponds to the same entity in all arrays. This means that if your job uses two components of an entity, you can use the same array index in both data arrays to access data for the same entity.  

You can use [ComponentTypeHandle](xref:Unity.Entities.ComponentTypeHandle`1) variables to access component types that you don't include in the [EntityQuery](xref:Unity.Entities.EntityQuery). However, you should use the [Has](xref:Unity.Entities.ArchetypeChunk.Has*) method to check whether the current chunk contains the component before you try to access it.

The `ComponentTypeHandle` fields are part of the ECS job safety system that prevents race conditions when reading and writing data in jobs. Always set the `isReadOnly` argument of the [GetComponentTypeHandle](xref:Unity.Entities.ComponentSystemBase.GetComponentTypeHandle*) method to accurately reflect how the component is accessed in a job. 

### ComponentLookup and BufferLookup fields

Accessing component data through an `EntityQuery` and an `IJobChunk` job (or with [Entities.ForEach](iterating-data-entities-foreach.md)) is the most efficient way to access your data. However, there are often cases where you need to look up data in a random-access fashion, for example, when one entity depends on data in another. To perform this data lookup, you must pass a different handle to your job through the job struct:

* [ComponentLookup](xref:Unity.Entities.ComponentLookup`1): Access the component of any entity with that component type.
* [BufferLookup](xref:Unity.Entities.BufferLookup`1): Access a buffer of any entity with that buffer type.

These types provide an array-like interface to components and buffers, indexed by [Entity](xref:Unity.Entities.Entity) object. 

Looking up data this way is inefficient because it uses random data access, and it increases the chances that you run into the job safety system's safeguards. For example, if you try to set the transform of one entity based on the transform of another entity, the job safety system can't tell if this is safe, because you have access to all transforms through the [ComponentLookup](xref:Unity.Entities.ComponentLookup`1) object. You could be writing to the same data you are reading and create a race condition. 

To use `ComponentLookup` and `BufferLookup`, declare a field of type `ComponentLookup` or `BufferLookup` on the job struct and set the value of the field before scheduling the job.

### Accessing other data

To access other information when a job executes, define a field on the job struct and then access the field inside the `Execute` method. You can only set the value when scheduling the job and that value remains the same for all chunks.

For example, if you want to update moving objects, you can pass in the time elapsed since the previous update. To do this, define a field named `DeltaTime`, set its value in `OnUpdate` and use that value in the job's `Execute` method. At each frame, you would calculate and assign a new value to your `DeltaTime` field before scheduling the job for the new frame.

## Write the Execute method

Write the `Execute` method of your job struct to transform your data from its input state to the desired output state. 

The signature of the [IJobChunk.Execute](xref:Unity.Entities.IJobChunk.Execute*) method is:

```cs
void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
```

### The chunk parameter

The `chunk` parameter provides the [ArchetypeChunk](xref:Unity.Entities.ArchetypeChunk) instance that contains the entities and components for this iteration of the job. Because a chunk can only be a member of a single archetype, all the entities in a chunk have the same set of components.

Use the `chunk` parameter to get the NativeArray instances you need to access the component data. You must also declare a field with the corresponding component type handle and set that field when scheduling the job.

### The unfilteredChunkIndex parameter

The `unfilteredChunkIndex` parameter is the index of the current chunk in the list of all chunks that match the query that schedules the job. These chunks aren't necessarily processed in the indexed order.

You can use the `unfilteredChunkIndex` value in situations where you have a native container with one element per chunk to which you want to write a value computed in your `Execute` method. Use the `unfilteredChunkIndex` as the array index into this container.

If you use a parallel writing [entity command buffer](systems-entity-command-buffers.md), pass the `unfilteredChunkIndex` argument as the `sortKey` parameter to the command buffer methods.

This index doesn't take query filtering into account. The job skips any chunks which don't match any of the query's active filters and it doesn't pass them to the `Execute` method. This means that your job should assume that all unfiltered chunk indices might not be processed. If you require a filtered chunk index relative to the list of chunks that the job will pass to `Execute`, use [EntityQuery.CalculateFilteredChunkIndexArrayAsync](xref:Unity.Entities.EntityQuery.CalculateFilteredChunkIndexArrayAsync*).

### The useEnabledMask and chunkEnabledMask parameters

If the EntityQuery that schedules the job includes any [enableable components](components-enableable.md), the entities in a given chunk might not match the query. For example, if an entity in a matching chunk has a required component disabled, that entity doesn't match the query and the job shouldn't process it. `IJobChunk` doesn't automatically skip these entities. This means that you must correctly handle enableable components, and only process entities which match the query.

> [!NOTE]
> Other job types such as [IJobEntity](iterating-data-ijobentity.md) and [Entities.ForEach](iterating-data-entities-foreach.md) automatically skip individual entities which don't match the provided query due to their enableable components.

An `IJobChunk`'s `Execute` method takes two additional parameters to help efficiently identify the entities that it should process:

* `chunkEnabledMask`: Contains a bitmask. If bit N in the mask is set, then entity N matches the query and the job should process it.
* `useEnabledMask`: A `bool` that provides an early-out in cases where `chunkEnabledMask` should be safely ignored. For example, if the query contains no enableable components, or if all entities in the chunk match the query, `useEnabledMask` is false and the contents of the `chunkEnabledMask` is undefined.

The easiest way to handle these parameters is to pass them to a new [ChunkEntityEnumerator](xref:Unity.Entities.ChunkEntityEnumerator) object, which you can use to safely and efficiently iterate over the matching entities within a chunk. This efficiently handles cases with and without a valid `chunkEnabledMask` in a single code path.

[!code-cs[chunk-entity-enumerator](../DocCodeSamples.Tests/JobChunkExamples.cs#chunk-entity-enumerator)]

In cases where you're sure that no enableable components are present, you can use a `for` loop to iterate over all entities in a chunk from index `0` to `chunk.Count`. However, in this case, you should add `Assert.IsFalse(useEnabledMask)` (or a similar validation) to the [Execute](xref:Unity.Entities.IJobChunk.Execute*) method. If you later modify the query to include an enableable component, or if a component later becomes enableable, a job that uses this shortcut incorrectly processes non-matching entities, and you must modify it to use [ChunkEntityEnumerator](xref:Unity.Entities.ChunkEntityEnumerator). The assert provides a quick way to detect this case.

### Optional components

If you have the [Any](xref:Unity.Entities.EntityQueryBuilder.WithAny*) filter in your entity query or have completely optional components that don’t appear in the query at all, use the [ArchetypeChunk.Has](xref:Unity.Entities.ArchetypeChunk.Has*) method to test whether the current chunk contains one of those components before you use it:

[!code-cs[chunk-has-component](../DocCodeSamples.Tests/JobChunkExamples.cs#chunk-has-component)]

## Schedule the job

To run an `IJobChunk` job, create an instance of your job struct, set the struct fields, and then schedule the job. When you do this in the `OnUpdate` method of a [SystemBase](xref:Unity.Entities.SystemBase) implementation, the system schedules the job to run every frame.

[!code-cs[schedule-job](../DocCodeSamples.Tests/JobChunkExamples.cs#schedule-job)]

When you call the [GetComponentTypeHandle](xref:Unity.Entities.ComponentSystemBase.GetComponentTypeHandle*) method to set your component type variables, make sure that you set the `isReadOnly` parameter to true for components that the job reads, but doesn’t write. Setting these parameters correctly has a significant impact on how efficiently the ECS framework can schedule your jobs. These access mode settings must match their equivalents in both the struct definition, and in the `EntityQuery`.

### Scheduling options

To control how a job executes by choose the appropriate method when you schedule the job:

* [Run](xref:Unity.Entities.JobChunkExtensions.Run*): Executes the job immediately on the current (main) thread. `Run` also completes any scheduled jobs that the current job depends upon.
* [Schedule](xref:Unity.Entities.JobChunkExtensions.Schedule*): Schedules the job to run on a worker thread after any scheduled jobs that the current job depends upon. The job's `Execute` method is called once for each chunk that the `EntityQuery` selects. Chunks are processed in sequence.
* [ScheduleParallel](xref:Unity.Entities.JobChunkExtensions.ScheduleParallel*): Like `Schedule`, except that the chunks are processed in parallel (assuming worker threads are available) rather than sequentially.

## Skipping chunks with unchanged entities

If you only need to update entities when a component value has changed, you can add that component type to the change filter of the `EntityQuery` that selects the entities and chunks for the job. For example, if you have a system that reads two components and only needs to update a third when one of the first two has changed, you can use an `EntityQuery` as follows:

[!code-cs[filter-query](../DocCodeSamples.Tests/JobChunkExamples.cs#filter-query)]

The `EntityQuery` change filter supports up to two components. If you want to check more or you aren't using an `EntityQuery`, you can make the check manually. To make this check, use the [ArchetypeChunk.DidChange](xref:Unity.Entities.ArchetypeChunk.DidChange*) method to compare the chunk’s change version for the component to the system's [LastSystemVersion](xref:Unity.Entities.ComponentSystemBase.LastSystemVersion). If this method returns false, you can skip the current chunk because none of the components of that type have changed since the last time the system ran.

You must use a struct field to pass `LastSystemVersion` from the system into the job, as follows:

[!code-cs[skip-unchanged-chunks-job](../DocCodeSamples.Tests/JobChunkExamples.cs#skip-unchanged-chunks-job)]

As with all the job struct fields, you must assign its value before you schedule the job:

[!code-cs[skip-unchanged-chunks-system](../DocCodeSamples.Tests/JobChunkExamples.cs#skip-unchanged-chunks-system)]

For efficiency, the change version applies to whole chunks and not individual entities. If another job which has the ability to write to that type of component accesses a chunk, then ECS increments the [change version](systems-version-numbers.md) for that component and the `DidChange` method returns true. ECS increments the change version even if the job that declares write access to a component doesn't change the component value. This is one of the reasons you should always read-only when you are reading component data and not updating it.
