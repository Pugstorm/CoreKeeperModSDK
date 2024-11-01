# Add components to an entity

To add components to an entity, use the [`EntityManager`](xref:Unity.Entities.EntityManager) for the [world](concepts-worlds.md) that the entity is in. You can add components to an individual entity, or to several entities at the same time.

Adding a component to an entity is a [structural change](concepts-structural-changes.md) which means that the entity moves to a different chunk. This means that you can't directly add components to an entity from a job. Instead, you must use an [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer) to record your intention to add components later.

### Add a component to a single entity

The following code sample creates a new entity then adds a component to the entity from the main thread.

[!code-cs[Add a component](../DocCodeSamples.Tests/GeneralComponentExamples.cs#add-component-single-entity)]

### Add a component to multiple entities

The following code sample gets every entity with an attached `ComponentA` component and adds a `ComponentB` component to them from the main thread.

[!code-cs[Add a component](../DocCodeSamples.Tests/GeneralComponentExamples.cs#add-component-multiple-entities)]

## Additional resources

* [`EntityManager.AddComponent`](xref:Unity.Entities.EntityManager.AddComponent*)
