---
uid: accessing-looking-up-data
---

# Look up arbitrary data

The most efficient way to access and change data is to use a [system](concepts-systems.md) with an [entity query](systems-entityquery.md) and a job. This utilizes the CPU resources in the most efficient way, with minimal memory cache misses. It's best practice to use the most efficient, fastest path to perform the bulk of data transformations. However, there are times when you might need to access an arbitrary component of an arbitrary entity at an arbitrary point in your program.

You can look up data in an entity's [`IComponentData`](xref:Unity.Entities.IComponentData) and its [dynamic buffers](components-buffer-introducing.md). The way you look up data depends on whether your code uses [`Entities.ForEach`](xref:Unity.Entities.SystemBase.Entities), or an `IJobChunk` job, or some other method on the main thread to execute in a system.

## Look up entity data in a system

To look up data stored in a component of an arbitrary entity from inside a system's `Entities.ForEach` or `Job.WithCode` method, use [`GetComponent<T>(Entity)`](xref:Unity.Entities.SystemBase.GetComponent``1(Unity.Entities.Entity)) 

For example, the following code uses `GetComponent<T>(Entity)` to get a `Target` component, which has an entity field that identifies the entity to target. It then rotates the tracking entities towards their target:

[!code-cs[lookup-foreach](../DocCodeSamples.Tests/LookupDataExamples.cs#lookup-foreach)]

If you want to access data stored in a dynamic buffer, you also need to declare a local variable of type [`BufferLookup`](xref:Unity.Entities.BufferLookup`1) in the `SystemBase` [`OnUpdate`](xref:Unity.Entities.SystemBase.OnUpdate*) method. You can then capture the local variable in a lambda expression. For example: 

[!code-cs[lookup-foreach-buffer](../DocCodeSamples.Tests/LookupDataExamples.cs#lookup-foreach-buffer)]


## Look up entity data in a job

To access component data at random in a job struct such as [`IJobChunk`](xref:Unity.Entities.IJobChunk), use one of the following types:  

* [`ComponentLookup`](xref:Unity.Entities.ComponentLookup`1)
* [`BufferLookup`](xref:Unity.Entities.BufferLookup`1 )

These types get an array-like interface to component, indexed by [`Entity`](xref:Unity.Entities.Entity) object. You can also use `ComponentLookup` to determine whether an entity's [enableable components](components-enableable-intro.md) are enabled or disabled, or to toggle the state of these components.

To use them, declare a field of type `ComponentLookup` or `BufferLookup`, set the value of the field, and then schedule the job.

For example, you can use the `ComponentLookup` field to look up the world position of entities:

[!code-cs[lookup-ijobchunk-declare](../DocCodeSamples.Tests/LookupDataExamples.cs#lookup-ijobchunk-declare)]

>[!NOTE]
>This declaration uses the [`ReadOnly`](https://docs.unity3d.com/ScriptReference/Unity.Collections.ReadOnlyAttribute.html) attribute. Always declare `ComponentLookup` objects as read-only unless you want to write to the components you access.
    
The following example illustrates how to set the data fields and schedule the job:

[!code-cs[lookup-ijobchunk-set](../DocCodeSamples.Tests/LookupDataExamples.cs#lookup-ijobchunk-set)]

To look up the value of a component, use an entity object inside the job's `Execute` method:

[!code-cs[lookup-ijobchunk-read](../DocCodeSamples.Tests/LookupDataExamples.cs#lookup-ijobchunk-read)]
  
The following, full example shows a system that moves entities that have a `Target` field that contains the entity object of their target towards the current location of the target:
 
[!code-cs[lookup-ijobchunk](../DocCodeSamples.Tests/LookupDataExamples.cs#lookup-ijobchunk)]

## Data access errors

If the data you look up overlaps the data you want to read and write to  in the job, then random access might lead to race conditions. 

You can mark an accessor object with the [`NativeDisableParallelForRestriction`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeDisableParallelForRestrictionAttribute.html) attribute, if you're sure that there's no overlap between the entity data you want to read or write to directly, and the specific entity data you want to read or write to at random.
