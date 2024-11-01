# Component concepts

In the Entity Component System (ECS) architecture, **components** contain [entity](concepts-entities.md) data that [systems](concepts-systems.md) can read or write. 

Use the [`IComponentData`](xref:Unity.Entities.IComponentData) interface, which has no methods, to mark a struct as a component type. This component type can only contain unmanaged data, and they can contain methods, but it's best practice for them to just be pure data. If you want to create a managed component, you define this as a class. For more information, refer to [Managed components](components-managed.md).

A unique set of an entity's components is called an **archetype**. The ECS architecture stores component data by archetype in 16KiB blocks of memory called chunks. For more information on how ECS stores component data refer to the documentation on [Archetype concepts](concepts-archetypes.md).

## Component types

There are different types of component that serve different purposes. Depending on how you want to manage the data in your project, certain components allow for more fine-tuned control over the performance of your application:

|**Component**|**Description**|
|---|---|
| [Unmanaged components](components-unmanaged.md)| The most common component type, but can only store fields of certain types.|
| [Managed components](components-managed.md)| A managed component type that can store any field type.|
| [Shared components](components-shared.md)| Components that group entities in chunks based on their values.|
| [Cleanup components](components-cleanup.md)| When you destroy an entity that contains a cleanup component, Unity removes all non-cleanup components. This is useful to tag entities that require cleanup when destroyed. |
| [Tag components](components-tag.md)| An unmanaged component that stores no data and takes up no space. You can use tag components in [entity queries](systems-entityquery-intro.md) to filter entities.|
| [Buffer components](components-buffer.md)  | A component that acts as a resizable array.|
| [Chunk components](components-chunk.md)  | A component that stores a value associated with an entire chunk, instead of a single entity. |
| [Enableable components](components-enableable.md)| A component that can be enabled or disabled on an entity at runtime, without requiring an expensive structural change. |
| [Singleton components](components-singleton.md)| A component that only has one instance in a given world.|

## Additional resources

* [Working with components](components-intro.md)
* [Component types](components-type.md)
* [Entity concepts](concepts-entities.md)
* [Archetype concepts](concepts-archetypes.md)
