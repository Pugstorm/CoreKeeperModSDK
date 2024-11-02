# EntityManager overview

[`EntityManager`](xref:Unity.Entities.EntityManager) is an API that provides utility methods to create, read, update, and destroy the entities within your project.

Each [world](concepts-worlds.md) has an `EntityManager` that you can use to manage all the entities within that world. Where possible, it’s best practice to use the methods in [`SystemAPI`](systems-systemapi.md) to access entity data in a world, over `EntityManager`. However, `EntityManager` is useful to manage [structural changes](concepts-structural-changes.md) on the main thread. 

## Structural changes

Some operations available in the `EntityManager` API cause [structural changes](concepts-structural-changes.md) to happen. To create structural changes, `EntityManager` waits for all running jobs to complete, which creates a sync point. This sync point blocks the main thread and prevents your application from taking advantage of all CPU cores, which might cause performance issues with your application. 

As an alternative, you can use an [entity command buffer](systems-entity-command-buffers.md) (ECB) to queue up structural changes so that they happen at one point, however ECBs have their own performance considerations. 

The main differences between using `EntityManager` and an ECB to manage the entities in your project are as follows:

* If you want to perform structural changes instantly on the main thread, use `EntityManager`. This is more efficient than using an ECB to do so.
* You can’t use `EntityManager` in jobs, so it’s incompatible with job types like `IJobChunk` and `IJobEntity`. You can use an ECB in jobs to queue structural changes but you must then execute the structural changes on the main thread after the jobs finish. For more information, refer to [Ways to schedule data changes](systems-schedule-changes-intro.md).
* You can only use `CreateEntity`, `CreateArchetype`, and `Instantiate` in [SystemAPI.Query](systems-systemapi-query.md). If you want to add a component in `SystemAPI.Query` then you need to use `EntityCommandBuffer.AddComponent`.

## Key EntityManager methods

The entities in a world are created, destroyed, and modified through the world's [`EntityManager`](xref:Unity.Entities.EntityManager). Key `EntityManager` methods include:

|**Method**|**Description**|
|---|---|
|[`CreateEntity`](xref:Unity.Entities.EntityManager.CreateEntity)|Creates a new entity.|
|[`Instantiate`](xref:Unity.Entities.EntityManager.Instantiate*)|Creates a new entity with a copy of all the components of an existing entity.|
|[`DestroyEntity`](xref:Unity.Entities.EntityManager.DestroyEntity*)|Destroys an existing entity.|
|[`AddComponent<T>`](xref:Unity.Entities.EntityManager.AddComponent*)|Adds a component of type T to an existing entity.|
|[`RemoveComponent<T>`](xref:Unity.Entities.EntityManager.RemoveComponent*)|Removes a component of type T from an existing entity.|
|[`HasComponent<T>`](xref:Unity.Entities.EntityManager.HasComponent*)|Returns true if an entity has a component of type T.|

All of the above methods are structural change operations.

## Additional resources

* [`EntityManager` API documentation](xref:Unity.Entities.EntityManager)
* [Structural changes overview](concepts-structural-changes.md)
* [Entity command buffer overview](systems-entity-command-buffers.md)
