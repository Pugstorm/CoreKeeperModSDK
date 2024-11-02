# Optimize shared components

Shared components have different performance considerations to other [component types](components-type.md). This page describes shared component-specific performance considerations and optimization techniques.

## Use unmanaged shared components

If possible, use unmanaged shared components over managed shared components. This is because Unity stores unmanaged shared components in a place accessible to [Burst compiled](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html) code via the unmanaged shared component APIs (such as [`SetUnmanagedSharedComponentData`](xref:Unity.Entities.EntityManager.SetUnmanagedSharedComponentData*)). This provides a performance benefit over managed components.

## Avoid frequent updates

Updating a shared component value for an entity is a [structural change](concepts-structural-changes.md) which means Unity moves the entity to another chunk. For performance reasons, try to avoid doing this frequently.

## Avoid lots of unique shared component values

All entities in a chunk must share the same shared component values. This means if you give unique shared component values to a high number of entities, it fragments those entities across lots of almost empty chunks.

For example, if there are 500 entities of an archetype with a shared component and each entity has a unique shared component value, Unity stores each entity in an individual chunk. This wastes most of the space in each chunk and also means, to loop through all entities of the archetype, Unity must loop through all 500 chunks. This negates the benefits of the ECS chunk layout and reduces performance. To avoid this problem, try to use as little unique shared component values as possible. If the 500 example entities share only ten unique shared component values, Unity can store them in as few as ten chunks.

Be careful with archetypes that have multiple shared component types. All entities in an archetype chunk must have the same combination of shared component values, so archetypes with multiple shared component types are susceptible to fragmentation.

> [!NOTE]
> To check for chunk fragmentation, you can view the chunk utilization in the [Archetypes window](editor-archetypes-window.md).