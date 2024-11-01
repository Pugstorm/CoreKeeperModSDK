# Read and write component values of entities

After you add Components to entities, your systems can access, read from, and write to the Component values. Depending on your use cases, there are several methods you can use to achieve this.

## Access a single component

Sometimes you might want to read or write a single component of one entity at a time. To do this, on the main thread, you can make the `EntityManager` to read or write a component value of an individual entity. The `EntityManager` keeps a lookup table to quickly find each entity's chunk and index within the chunk.

## Access multiple components

For most work, you'll want to read or write the components of all entities in a chunk or set of chunks:

* An `ArchetypeChunk` directly reads and writes the component arrays of a chunk.
* An `EntityQuery` efficiently retrieves the set of chunks which match the query.
* An [`IJobEntity`](xref:Unity.Entities.IJobEntity) iterates across components in a query using jobs.

## Deferring component value changes

To defer component value changes for later, use an [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer) which records your intention to write (but not read) component values. These changes only happen when you later play back the `EntityCommandBuffer` on the main thread.