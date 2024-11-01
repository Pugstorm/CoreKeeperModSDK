# Safety in Entities

The Entities package provides a framework and set of APIs so that you can use data-oriented design principles to transform data efficiently. This involves leveraging the [Burst compiler](https://docs.unity3d.com/Packages/com.unity.burst@latest) and native-interop to access data directly whenever possible. This approach sometimes goes against the safety mechanisms built into the C# language.

Many of the Entities package's internal APIs use unsafe blocks of code and raw pointers to data to get the best performance possible. Some APIs return references to data that might outlive the data referenced. This page contains information on how safety works with Entities and some pitfalls that you might encounter.

## Guarded safety violation

In most cases, the Entities framework and supporting packages try to guard against safety issues in the Editor and when safety checks are enabled. Safety errors in these situations should throw valid errors that give information on how to fix and prevent the Editor from crashing. However, in runtime builds there aren't any guarantees that these cases won't cause crashes or memory corruption.  You can also disable some of these safety checks for jobs and via the **Safety Checks** setting in the Editor (**Jobs** &gt; **Burst** &gt; **Safety Checks**). For more information, see the documentation on [Data access errors](systems-looking-up-data.md#data-access-errors).

### Structural changes

One of the most common issues with safety in Entities is when [structural changes](concepts-structural-changes.md) invalidate data. This happens because a structural change modifies an entity’s archetype which moves the entity to another chunk.

>[!NOTE]
>Enabling and disabling [enableable components](components-enableable.md) isn't a structural change. However, all jobs that enable or disable components must complete before checking the enabled status to ensure that all changes to the component's enabled status have completed.

The Entities API stores data in chunks that are typically accessed through the [job system](xref:JobSystem) or the main thread. The job system typically handles all safety of data that's passed in with NativeContainers, and uses notations to mark if the data is read from, written to, or both. However, any API that causes a structural change might make this data move in memory and invalidate any reference held to that data.

### RefRW/RefRO

The Entities package contains explicit reference types that you can use to mark the contained type to be accessed as ReadWrite (`RefRW`) or ReadOnly (`RefRO`). These reference types have checks to ensure that the contained type is still valid when running with safety checks enabled. [Structural changes](concepts-structural-changes.md) might cause the contained type to no longer be valid.

## Unguarded safety violation

There are a few cases that aren't guarded against. This section outlines any cases where crashes or memory corruption might happen due to Entities APIs in the Editor.

### IJobEntity

[IJobEntity](iterating-data-ijobentity.md) allows you to schedule jobs with an external [EntityQuery](systems-entityquery.md). This uses the `EntityQuery` to retrieve entities and then executes the `IJobEntity` `Execute` method with those entities. ECS doesn't check if the entities actually have the component arguments, so you must ensure that these stay in sync. If the `Execute` parameters don’t match the query's components, this might result in a crash or memory corruption.

### InternalCompilerInterface

The `InternalCompilerInterface` static class includes a number of methods that expose some of the DOTS internals to source-generated code. This is necessary because generated code can only typically call public APIs. 

>[!WARNING]
>Do not use the APIs contained in InternalCompilerInterface. They are only in the context of being called from generated code and are likely to change in the future.