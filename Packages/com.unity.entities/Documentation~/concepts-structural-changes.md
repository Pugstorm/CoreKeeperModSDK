---
uid: concepts-structural-changes
---

# Structural changes concepts

Operations that cause Unity to reorganize [chunks of memory](concepts-archetypes.md#archetype-chunks) or the contents of chunks in memory are called **structural changes**. It's important to be aware of which operations are structural changes because they can be resource-intensive and you can only perform them on the main thread; not from jobs.

The following operations are considered structural changes:

* Creating or destroying an [entity](concepts-entities.md).
* Adding or removing [components](concepts-components.md).
* Setting a shared component value.

## Create an entity

When you create an entity, Unity either adds the entity to an existing chunk or, if no chunks are available for the entity's [archetype](concepts-archetypes.md), creates a new chunk and adds the entity to that.

## Destroy an entity

When you destroy an entity, Unity removes the entity from its chunk. If removing the entity leaves a gap in the chunk, Unity moves the last entity in the chunk to fill the gap. If removing the entity leaves the chunk empty, Unity deallocates the chunk.

## Add or remove components

When you add or remove components from an entity, you change the entity's archetype. Unity stores each entity in a chunk that matches the entity's archetype. This means that if you change an entity's archetype, Unity must move the entity to another chunk. If a suitable chunk doesn't exist, Unity creates a new one. If the move leaves the previous chunk with a gap or leaves it empty, Unity moves the last entity in the chunk to fill the gap or deallocates the chunk respectively.

## Set a shared component value

When you set the value of an entity's [shared component](components-shared.md), Unity moves the entity to a chunk that matches the new shared component value. If a suitable chunk doesn't exist, Unity creates a new one. If the move leaves the previous chunk either with a gap or empty, Unity moves the last entity in the chunk to fill the gap or deallocates the chunk respectively.

> [!NOTE]
> Setting a regular component value isn't a structural change because it doesn't require Unity to move the entity.

## Sync points

You can't make structural changes directly in a job because it might invalidate other jobs that are already scheduled, and creates a [sync point](#sync-points). 

A synchronization point (sync point) is a point in program execution that waits for the completion of all jobs that have been scheduled so far. Sync points limit your ability to use all worker threads available in the job system for a period of time. As such, you should aim to avoid sync points. Structural changes to the data in ECS are the primary cause of sync points.

Structural changes not only require a sync point, but they also invalidate all direct references to any component data. This includes instances of [`DynamicBuffer`](xref:Unity.Entities.DynamicBuffer`1) and the result of methods that provide direct access to the components such as [`ComponentSystemBase.GetComponentDataFromEntity`](xref:Unity.Entities.ComponentSystemBase.GetComponentDataFromEntity*).

### Avoiding sync points

You can use [entity command buffers](systems-entity-command-buffers.md) to queue up structural changes instead of performing them instantly. You can play back commands stored in an entity command buffer at a later point during the frame. This reduces multiple sync points spread across the frame to a single sync point.

Each of the standard [`ComponentSystemGroup`](xref:Unity.Entities.ComponentSystemGroup) instances provide an [`EntityCommandBufferSystem`](xref:Unity.Entities.EntityCommandBuffer) as the first and last systems updated in the group. If you get an entity command buffer object from one of these standard systems, all structural changes happen at the same point in the frame, which results in one sync point. You can also use entity command buffers to record structural changes within a job, rather than only making structural changes on the main thread.

If you can't use an entity command buffer for a task, group any systems that make structural changes together in the system execution order. Two systems that both make structural changes only create one sync point if they update sequentially.

## Additional resources

* [Archetype concepts](concepts-archetypes.md)
* [Entity command buffers](systems-entity-command-buffers.md)
* [System update order](systems-update-order.md)
* [Shared components](components-shared.md)
