---
uid: iterating-data-ijobentity
---

# Iterate over component data with IJobEntity

You can use [`IJobEntity`](xref:Unity.Entities.IJobEntity) to iterate across `ComponentData` when you have a data transformation that you want to use in multiple systems, with different invocations. It creates an [`IJobChunk`](xref:Unity.Entities.IJobChunk) job, so you only have to consider what data you want to transform.

Note that `IJobEntity` works identically with SystemBase and ISystem. 

## Create an IJobEntity job

To create an `IJobEntity` job, write a struct that uses the `IJobEntity` interface, and implement your own custom `Execute` method. 

Use the `partial` keyword because source generation creates a struct that implements `IJobChunk` in a separate file in `project/Temp/GeneratedCode/....`.

The following example adds one to every SampleComponent every frame.
[!code-cs[SimpleSample](../DocCodeSamples.Tests/JobEntityExamples.cs#SimpleSample)]

## Specify a query

You can specify a query for `IJobEntity` in the following ways:

* Create a query manually, to specify different invocation requirements.
* Use the `IJobEntity` [attributes](#attributes) to create a query based on its given `Execute` parameters, and specifications on the job struct.

The following example shows both options:

[!code-cs[Query](../DocCodeSamples.Tests/JobEntityExamples.cs#Query)]

### Attributes

`IJobEntity` has the following built-in attributes:

|**Attribute**|**Description**|
|---|---|
|[`Unity.Entities.WithAll(params Type[])`](xref:Unity.Entities.WithAllAttribute)| Set on the job struct. Narrows the query so that the entities have to match all the types provided.|
|[`Unity.Entities.WithAny(params Type[])`](xref:Unity.Entities.WithAnyAttribute)| Set on the job struct. Narrows the query so that the entities have to match any of the types provided.|
|[`Unity.Entities.WithNone(params Type[])`](xref:Unity.Entities.WithNoneAttribute)| Set on the job struct. Narrows the query so that the entities have to match none of the types provided.|
|[`Unity.Entities.WithChangeFilter(params Type[])`](xref:Unity.Entities.WithChangeFilterAttribute)| Set on the job struct or attach to an argument in `Execute`. Narrows the query so that the entities have to have had changes in the archetype chunk for the given components.|
|[`Unity.Entities.WithOptions(params EntityQueryOptions[])`](xref:Unity.Entities.WithOptionsAttribute)| Set on the job struct. Changes the scope of the query to use the [`EntityQueryOptions`](xref:Unity.Entities.SystemBase.Entities) described.|
|[`Unity.Entities.EntityIndexInQuery`](xref:Unity.Entities.EntityIndexInQuery)|  Set on the `int` parameter in `Execute` to get the current index in query, for the current entity iteration. This is the same as `entityInQueryIndex` in `Entities.ForEach`.|

The following is an example of `EntityIndexInQuery`:

[!code-cs[EntityIndexInQuery](../DocCodeSamples.Tests/JobEntityExamples.cs#EntityIndexInQuery)]

Because `IJobEntity` is a job, you can also use all attributes that work on a job:

* `Unity.Burst.BurstCompile`
* `Unity.Collections.DeallocateOnJobCompletion`
* `Unity.Collections.NativeDisableParallelForRestriction`
* `Unity.Burst.BurstDiscard`
* `Unity.Collections.LowLevel.Unsafe.NativeSetThreadIndex` 
* `Unity.Collections.NativeDisableParallelForRestriction`
* `Unity.Burst.NoAlias`

### Execute parameters

The following is a list of all the supported `Execute` parameters you can use in `IJobEntity`:

|**Parameter**|**Description**|
|---|---|
|`IComponentData`| Mark as `ref` for read-write access, or `in` for read-only access to the `ComponentData`.|
|`ICleanupComponentData`|Mark as `ref` for read-write access, or `in` for read-only access to the `ComponentData`.|
|`ISharedComponent`| Mark `in` for read-only access to a `SharedComponentData`. If this is managed you can't Burst compile or schedule it. Use `.Run` instead.|
|[Managed components](components-managed.md)| Use a value-copy for read-write access or mark with `in` for read-only access of managed components. For example, `UnityEngine.Transform`. Marking a managed component as `ref` is an error, and you can't Burst-compile or schedule it. Use `.Run` instead.|
|`Entity`| Gets the current entity. This is a value copy only, so don't mark with `ref` or `in`.|
|`DynamicBuffer<T>`| Gets the `DynamicBuffer`. Mark with `ref` for read-write access and `in` for read-only access.|
|`IAspect`| Gets the Aspect. Aspects act as references so you can't assign them. However, you can use `ref` and value-copy to mark it as read-write, and `in` for read-only access.|
|`int`| There are three supported ints:|
|| Mark the `int` with the attribute `[Unity.Entities.ChunkIndexInQuery]` to get the current archetype chunk index in a query.  |
||Mark the `int` with the attribute `[Unity.Entities.EntityIndexInChunk]` to get the current entity index in the current archetype chunk. You can add `EntityIndexInChunk` and `ChunkIndexInQuery` to get a unique identifier per entity.|
||Mark the `int` with the attribute `[Unity.Entities.EntityIndexInQuery]` to get the packed index of the query. This parameter internally uses `EntityQuery.CalculateBaseEntityIndexArray[Async]` which negatively affects performance.|

## Comparison between IJobEntity and Entities.ForEach

`IJobEntity` is similar to [`Entities.ForEach`](iterating-data-entities-foreach.md), but you can reuse `IJobEntity` throughout several systems, so you should use it over `Entities.ForEach` where possible. For example, this is an `Entities.ForEach` example:

[!code-cs[BoidsForEach](../DocCodeSamples.Tests/JobEntityExamples.cs#BoidsForEach)]

You can rewrite it as the following with `IJobEntity`:

[!code-cs[Boids](../DocCodeSamples.Tests/JobEntityExamples.cs#Boids)]

Also, `IJobEntity` takes much less time to compile than `Entities.ForEach`, so it's also recommended for that reason. 

## Additional resources

* [`IJobEntity` API documentation](xref:Unity.Entities.IJobEntity)