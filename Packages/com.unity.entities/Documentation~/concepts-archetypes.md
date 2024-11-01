---
uid: concepts-archetypes
---

# Archetypes concepts

An **archetype** is a unique identifier for all the [entities](concepts-entities.md) in a [world](concepts-worlds.md) that have the same unique combination of [component](concepts-components.md) types. For example, all the entities in a world that have component types A and B share an archetype. All the entities that have component types A, B, and C share a different archetype, and all the entities that have component types A and Z share yet another archetype.

When you add or remove component types from an entity, the world's [`EntityManager`](xref:Unity.Entities.EntityManager) moves the entity to the appropriate archetype. For example, if an entity has component types A, B, and C and you remove its B component, the `EntityManager` moves the entity to the archetype that has component types A and C. If no such archetyps exists, the `EntityManager` creates it. 


>[!IMPORTANT]
>Moving entities frequently is resource-intensive and reduces the performance of your application. For more information, see the documentation on  [Structural change concepts](concepts-structural-changes.md).

The archetype-based organization of entities means that it's efficient to query entities by their component types. For example, if you want to find all entities that have component types A and B, you can find all the archetypes with those component types, which is more performant than scanning through all individual entities. The set of existing archetypes in a world tends to stabilize early in the lifetime of a program, so you can cache queries to get faster performance.

An archetype is only destroyed when its world is destroyed.

## Archetype chunks

All entities and components with the same archetype are stored in uniform blocks of memory called **chunks**. Each chunk consists of 16KiB and the number of entities that they can store depends on the number and size of the components in the chunk's archetype. The [`EntityManager`](xref:Unity.Entities.EntityManager) creates and destroys chunks as needed.

A chunk contains an array for each component type, plus an additional array to store the entity IDs. For example, in an archetype with component types A and B, the chunks each have three arrays: one array for the A component values, one array for the B component values, and one array for the entity IDs.

The arrays of a chunk are tightly packed: the first entity of the chunk is stored at index 0 of these arrays, the second entity of the chunk is stored at index 1, and subsequent entities are stored in the consecutive indexes. When a new entity is added to the chunk, it's stored in the first available index. When an entity is removed from the chunk (either because it's being destroyed or being moved to another archetype), the last entity of the chunk is moved to fill the gap.

When an entity is added to an archetype, the `EntityManager` creates a new chunk if the archetype's existing chunks are all full. When the last entity is removed from a chunk, the `EntityManager` destroys the chunk.

## Archetypes in the Editor

The [Archetypes window](editor-archetypes-window.md) lists the archetypes of all the worlds in your project and it shows the amount of allocated and unused memory of each archetype.

In the Editor, the following icon represents an Archetype: ![](images/editor-archetype-icon.png) . 

## Additional resources

* [Entities concepts](concepts-entities.md)
* [Structural changes concepts](concepts-structural-changes.md)
* [Archetypes window](editor-archetypes-window.md)