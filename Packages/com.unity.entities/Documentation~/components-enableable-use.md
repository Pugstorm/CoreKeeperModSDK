# Use enableable components

You can only make [IComponentData](xref:Unity.Entities.IComponentData) and [IBufferElementData](xref:Unity.Entities.IBufferElementData) components enableable. To do this, implement the [IEnableableComponent](xref:Unity.Entities.IEnableableComponent) interface.

When you use enableable components, the target entity doesn't change its archetype, ECS doesn't move any data, and the component's existing value remains the same. This means that you can enable and disable components on jobs running on worker threads without using an [entity command buffer](systems-entity-command-buffers.md) or creating a [sync point](concepts-structural-changes.md#sync-points). 

However, to prevent race conditions, jobs with write access to enableable components might cause main-thread operations to block until the job completes, even if the job doesn't enable or disable the component on any entities.

All enableable components are enabled by default on new entities created with `CreateEntity()`. Entities which are instantiated from prefabs inherit the enabled or disabled state of the prefab.

## Enableable component methods

To work with enableable components, you can use the following methods on `EntityManager`, `ComponentLookup<T>`, `EntityCommandBuffer`, and `ArchetypeChunk`:

* `IsComponentEnabled<T>(Entity e)`: Returns true if entity `e` has component `T`, and it's enabled. Returns false if entity `e` has component `T`, but it's disabled. Asserts if entity `e` doesn't have component `T`, or if `T` doesn't implement `IEnableableComponent`.
* `SetComponentEnabled<T>(Entity e, bool enable)`: If entity `e` has component `T`, it's either enabled or disabled based on the value of enable. Asserts if entity `e` doesn't have component `T`, or if `T`doesn't implement `IEnableableComponent`.

For example:

[!code-cs[Enableable component example](../DocCodeSamples.Tests/EnableableComponentExample.cs#enableable-example)]

You can use `ComponentLookup<T>.SetComponentEnabled(Entity,bool)` to safely enable or disable entities from worker threads, because no structural change is needed. The job must have write access to component `T`. Avoid enabling or disabling a component on an entity that another thread might process in a job because this often leads to a race condition.

## Querying enableable components

An entity with component `T` disabled matches queries as if it doesn't have component `T` at all. For example, if entity `E` has components `T1` (enabled), `T2` (disabled), and `T3` (disabled):

* It doesn't match a query that requires both `T1` and `T2`
* It matches a query that requires `T1` and excludes `T2`
* It doesn't match a query with `T2` and `T3` as optional components because it doesn't have at least one of these components enabled.

All `EntityQuery` methods automatically handle enableable components. For example, `query.CalculateEntityCount()` computes the number of entities that match the query, taking into account which of their components are enabled and disabled. There are two exceptions:

* Method names that end in `IgnoreFilter` treat all components as if they’re enabled. These methods don't require a sync point, because only a structural change affects their results. They tend to be more efficient than variants that respect filtering.
* Queries created with `EntityQueryOptions.IgnoreComponentEnabledState`, ignore the current enabled/disabled state of all entities in matching archetypes when determining whether they match the query.

The following is an example of querying a component that has been disabled with [`EntityManager.IsComponentEnabled`](xref:Unity.Entities.EntityManager.IsComponentEnabled*):

[!code-cs[Enableable component example](../DocCodeSamples.Tests/EnableableComponentExample.cs#enableable-health-example)]

## Asynchronous operations

To safely and deterministically handle enableable components, all synchronous `EntityQuery` operations (except those which ignore filtering) automatically wait for any running jobs to complete which have write access to the query’s enableable components. All asynchronous `EntityQuery` operations (those ending in `Async`) automatically insert an input dependency on these running jobs as well.

Asynchronous `EntityQuery` gather and scatter operations such as [`EntityQuery.ToEntityArrayAsync()`](xref:Unity.Entities.EntityQuery.ToEntityArrayAsync*) schedule a job to perform the requested operation. These methods must return a `NativeList` instead of a `NativeArray`, because the number of entities the query matches isn't known until the job is running, but the container must be returned to the caller immediately. 

This list has its initial capacity conservatively sized based on the maximum number of entities that could match the query, though its final length might be lower. Until the async gather or scatter job completes, any reads or writes to the list (including its current length, capacity, or base pointer) result in a JobsDebugger safety error. However, you can safely pass the list to a dependent follow-up job.