# Enableable components overview

You can use enableable components on [IComponentData](xref:Unity.Entities.IComponentData) and [IBufferElementData](xref:Unity.Entities.IBufferElementData) components to disable or enable individual components on an entity at runtime. To make components enableable, inherit them from [IEnableableComponent](xref:Unity.Entities.IEnableableComponent).

Enableable components are ideal for states that you expect to change often and unpredictably, or where the number of state permutations are high on a frame-by-frame basis. [Adding](components-add-to-entity.md) and [removing components](components-remove-from-entity.md) is the preferable way to manage components for low-frequency state changes, where you expect the state to persist for many frames.

You can also use enableable components instead of a set of zero-size [tag components](components-tag.md) to represent entity states. This reduces the number of unique entity archetypes, and encourages better [chunk](concepts-archetypes.md#archetype-chunks) usage to reduce memory consumption.

## Structural changes

Enableable components don't create [structural changes](concepts-structural-changes.md), unlike adding and removing components. ECS treats a disabled component as though the entity doesn't have that component when determining if an entity matches an [entity query](systems-entityquery.md). This means that an entity with a disabled component doesn't match a query that requires the component, and matches a query that excludes the component, assuming it meets all other query criteria.

The semantics of existing component operations don't change.  `EntityManager` considers an entity with a disabled component to still have the component. 

For example, if component `T` is disabled on entity `E` these methods do the following:

|**Method**|**Outcome**|
|---|---|
|`HasComponent<T>(E)` |Returns true.|
|`GetComponent<T>(E)` |Returns component `T`’s current value.|
|`SetComponent<T>(E,value)`| Updates the component `T`’s value.|
|`RemoveComponent<T>(E)`| Removes component `T` from `E`.|
|`AddComponent<T>(E)`| Quietly does nothing, because the component already exists.|

## Additional resources

* [Use enableable components](components-enableable-use.md)
* [Look up arbitrary data](systems-looking-up-data.md)