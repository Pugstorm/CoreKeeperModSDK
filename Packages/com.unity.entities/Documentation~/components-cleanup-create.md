# Create a cleanup component

To create a cleanup component, create a struct that inherits from `ICleanupComponentData`. Make sure to add it to entities at runtime, because cleanup components cannot be baked.

The following code sample shows an empty cleanup component:

[!code-cs[Create a cleanup Component](../DocCodeSamples.Tests/CreateComponentExamples.cs#system-state)]

> [!NOTE]
> Empty cleanup components are often sufficient, but you can add properties to store information required to cleanup the target archetypes.

## Perform cleanup

You can use cleanup components to help you manage entities that require cleanup when destroyed. Unity prevents you from destroying an entity that contains a cleanup component.

When you try to destroy an entity with an attached cleanup component, Unity removes all non-cleanup components instead. The entity still exists until you remove all cleanup components from it.

To perform cleanup for entities of a specific archetype:

1. [Create a new tag component](components-tag.md) and add the tag component to the archetype.
1. Create a new cleanup component that contains information required to clean up a certain entity archetype.
1. Create a system that:
   1. Gets newly created entities of the target [archetype](concepts-archetypes.md). These are entities that contain the tag component but not the cleanup component.
   1. Adds the cleanup component to these entities.
1. Create a system to handle the cleanup component removal:
   1. In OnUpdate, handle entities which need cleanup at runtime:
      1. Get the entities that have been provisionally destroyed and require cleanup. These are entities that contain the cleanup component, but not the tag component.
      1. Perform the appropriate cleanup work for the entities.
      1. Remove the relevant cleanup component(s) from the entities.
   1. In OnDestroy, handle entities which need cleanup at shutdown:
      1. Get all entities with the cleanup component, including those that still have the tag component.
      1. Perform the appropriate cleanup work for the entities.
      1. Remove the relevant cleanup component(s) from the entities.

## Additional resources

* [Tag components](components-tag.md)