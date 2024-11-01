# Remove components from an entity

To remove components from an entity, use the [`EntityManager`](xref:Unity.Entities.EntityManager) for the [World](concepts-worlds.md) that the entity is in.

> [!IMPORTANT]
> Removing a component from an entity is a [structural change](concepts-structural-changes.md) which means that the entity moves to a different archetype chunk.

## From the main thread
You can directly remove components from an entity from the main thread. The following code sample gets every entity with an attached [`Rotation`](xref:Unity.Entities.TransformAuthoring.Rotation) component and then removes the `Rotation` component.

[!code-cs[Remove a component](../DocCodeSamples.Tests/GeneralComponentExamples.cs#remove-Component)]

## From a job
Because removing a component from an entity is a structural change, you can't directly do it in a job. Instead, you must use an `EntityCommandBuffer` to record your intention to remove components later.