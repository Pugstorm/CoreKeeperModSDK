# Introducing chunk components

Chunk components store values per chunk instead of per entity. Their primary purpose is to act as an optimization because you can run code on a per-chunk level to check whether to process some behavior for all entities in each. For example, a chunk component can store the bounds of all the entities in it. You can check if the bounds are on-screen and only process the entities in that chunk if they are.

Chunk components offer similar functionality to [shared components](components-shared.md), but differ in the following ways:

* A chunk component value conceptually belongs to the chunk itself, not the individual entities of the chunk.
* Setting a chunk component value isn't a [structural change](concepts-structural-changes.md).
* Unlike with shared components, Unity doesn't deduplicate unique chunk component values: chunks with equal chunk component values store their own separate copies.
* Chunk components are always unmanaged: you can't create a managed chunk component.
* Unity moves an entity to a new chunk when the entity's archetype changes or when the entity's shared component values change, but these moves never modify the chunk component values of either the source or destination chunk.