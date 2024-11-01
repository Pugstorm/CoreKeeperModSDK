# Introducing cleanup components

Cleanup components are like regular components, but when you destroy an entity that contains one, Unity removes all non-cleanup components instead. The entity still exists until you remove all cleanup components from it. This is useful to tag entities that require cleanup when destroyed. For information on how to do this, refer to [Use cleanup components](components-cleanup-create.md#perform-cleanup).

## Cleanup component lifecycle

The following code sample explains the lifecycle of an entity that contains a cleanup component:

```c#
// Creates an entity that contains a cleanup component.
Entity e = EntityManager.CreateEntity(
    typeof(Translation), typeof(Rotation), typeof(ExampleCleanup));

// Attempts to destroy the entity but, because the entity has a cleanup component, Unity doesn't actually destroy the entity. Instead, Unity just removes the Translation and Rotation components. 
EntityManager.DestroyEntity(e);

// The entity still exists so this demonstrates that you can still use the entity normally.
EntityManager.AddComponent<Translation>(e);

// Removes all the remaining components from the entity.
// Removing the final cleanup component (ExampleCleanup) automatically destroys the entity.
EntityManager.RemoveComponent(e, new ComponentTypeSet(typeof(ExampleCleanup), typeof(Translation)));

// Demonstrates that the entity no longer exists. entityExists is false. 
bool entityExists = EntityManager.Exists(e);
```

> [!NOTE]
> Cleanup components are unmanaged and have all of the same restrictions as [unmanaged components](components-unmanaged.md).
>
> The following limitations also apply:
> - Cleanup components are not included when entities are copied between Worlds.
>   - As a consequence, cleanup components added at baking time will not be serialized.
> - Cleanup components on prefab entities will not be included on instantiated instances of that prefab.

## Additional resources
* [Unmanaged components](components-unmanaged.md)
