# Aspect concepts

An aspect is an object-like wrapper that you can use to group together a subset of an entity's components into a single C# struct. Aspects are useful for organizing component code and simplifying queries in your systems. Unity provides predefined aspects for groups of related components or you can define your own with the [`IAspect`](xref:Unity.Entities.IAspect) interface.

Aspects can include items such as the following:

* A single `Entity` field to store the entity's ID
* `RefRW<T>` and `RefRO<T>` fields to access component data of type `T`, where `T` implements [`IComponentData`](xref:Unity.Entities.IComponentData).
* `EnabledRefRW` and `EnabledRefRO` fields to access the enabled state of components that implement [`IEnableableComponent`](xref:Unity.Entities.IEnableableComponent).
* `DynamicBuffer<T>` fields to access the buffer elements that implement `IBufferElementData`
* Any `ISharedComponent` fields to access the shared component value as read-only.
* Other aspect types

## Additional resources

* [Create an aspect](aspects-create.md)
* [`IAspect` API documentation](xref:Unity.Entities.IAspect)
