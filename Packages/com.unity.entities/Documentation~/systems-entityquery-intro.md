# EntityQuery overview

An [`EntityQuery`](xref:Unity.Entities.EntityQuery) finds [archetypes](concepts-archetypes.md) that have a specified set of component types. It then gathers the archetype's chunks into an array which a system can process. 

For example, if a query matches component types A and B, then the query gathers the chunks of all the archetypes that have those two component types, regardless of whatever other component types those archetypes might have. Therefore, an archetype with component types A, B, and C would match the query.

You can use `EntityQuery` to do the following: 

* Run a job to process the selected entities and components
* Get a `NativeArray` that contains all the selected entities
* Get a `NativeArray` of the selected entities by component type

The entity and component arrays that `EntityQuery` returns are parallel. This means that the same index value always applies to the same entity in any array. 

## Queries in the Editor

In the Editor, the following icon represents a query: ![](images/editor-query-icon.png) . You’ll see this when you use the specific [Entities windows and Inspectors](editor-workflows.md). You can also use the [Query window](editor-query-window.md) to see the Components and Entities that match the selected query.
