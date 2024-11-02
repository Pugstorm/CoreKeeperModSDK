# Create an EntityQuery

To create an entity query, you can pass component types to the [`EntityQueryBuilder`](xref:Unity.Entities.EntityQueryBuilder) helper type. The following example defines an `EntityQuery` that finds all entities with both `ObjectRotation` and `ObjectRotationSpeed` components:

[!code-cs[define-query](../DocCodeSamples.Tests/EntityQueryExamples.cs#define-query)]

The query uses [`EntityQueryBuilder.WithAllRW<T>`](xref:Unity.Entities.EntityQueryBuilder.WithAllRW*) to show that the system writes to `ObjectRotation`. You should always specify read-only access when possible, because there are fewer constraints on read access to data. This helps the job scheduler execute the jobs more efficiently. 

## Specify which archetypes the system selects

Queries only match archetypes that contain the components you specify. You can specify components with three different [`EntityQueryBuilder`](xref:Unity.Entities.EntityQueryBuilder) methods: 

* `WithAll<T>()`: To match the query, an archetype must contain all the query's required components.
* `WithAny<T>()`: To match the query, an archetype must contain at least one of the query's optional components.
* `WithNone<T>()`: To match the query, an archetype must not contain any of the query's excluded components.
* `WithAspect<T>()`: To match the query, an archetype must meet the [aspect’s](aspects-intro.md) component requirements. Use last when building a query to avoid component aliasing.

For example, the following query includes archetypes that contain the `ObjectRotation` and `ObjectRotationSpeed`components, but excludes any archetypes that contain the `Static` component:

[!code-cs[query-desc](../DocCodeSamples.Tests/EntityQueryExamples.cs#query-desc)]


> [!IMPORTANT]
> To handle optional components, use the [`ArchetypeChunk.Has<T>`](xref:Unity.Entities.ArchetypeChunk.Has*) method to determine whether a chunk contains the optional component or not. This is because all entities in the same chunk have the same components, so you only need to check whether an optional component exists once per chunk: not once per entity.

You can use [`EntityQueryBuilder.WithOptions()`](xref:Unity.Entities.EntityQueryBuilder.WithOptions*) to find specialized archetypes. For example:

* `IncludePrefab`: Includes archetypes that contain the Prefab [tag component](components-tag.md).
* `IncludeDisabledEntities`: Includes archetypes that contain the [`Disabled`](xref:Unity.Entities.Disabled) tag component.
* `FilterWriteGroup`: Includes only entities with components in a [`WriteGroup`](xref:Unity.Entities.WriteGroupAttribute) that are explicitly included in the query. Excludes entities that have any additional components from the same `WriteGroup`.

See [`EntityQueryOptions`](xref:Unity.Entities.EntityQueryOptions) for the full list of options.

## Filter by write group

In the following example `LuigiComponent` and `MarioComponent` are components in the same `WriteGroup` based on the `CharacterComponent` component. This query uses the `FilterWriteGroup` option that requires `CharacterComponent` and `MarioComponent`:

[!code-cs[query-writegroup](../DocCodeSamples.Tests/EntityQueryExamples.cs#query-writegroup)]

This query excludes any entities with both `LuigiComponent` and `MarioComponent` because `LuigiComponent` isn't explicitly included in the query. 

This is more efficient than the `None` field because you don't need to change the queries that the other systems use, as long as they also use write groups.

You can use write groups to extend existing systems. For example, if you've defined `CharacterComponent` and `LuigiComponent` in another system as part of a library you don't control, you can put `MarioComponent` in the same write group as `LuigiComponent` to change how `CharacterComponent` is updated. Then, for any entities you add to the `MarioComponent`, the system updates `CharacterComponent`, but the original system doesn't update it. For entities that don't have `MarioComponent`, the original system updates `CharacterComponent` as before. For more information, see the documentation on [Write groups](systems-write-groups.md).

## Combine queries

To effectively combine multiple queries into one, you can create a query that contains multiple query descriptions. The resulting query matches archetypes that match any of the provided query descriptions. Essentially, the combined query matches the union of the query descriptions. The following example selects any archetypes that contain a `ObjectRotation` component or a `ObjectRotationSpeed` component (or both):

[!code-cs[combine-query](../DocCodeSamples.Tests/EntityQueryExamples.cs#combine-query)]

## Execute the query

Typically, you execute an entity query when you schedule a job that uses it. You can also call one of the `EntityQuery` methods that returns arrays of entities, components, or archetype chunks:

* [`ToEntityArray`](xref:Unity.Entities.EntityQuery.ToEntityArray*): Returns an array of the selected entities.
* [`ToComponentDataArray`](xref:Unity.Entities.EntityQuery.ToComponentDataArray*): Returns an array of the components of type `T` for the selected entities.
* [`CreateArchetypeChunkArray`](xref:Unity.Entities.EntityQuery.CreateArchetypeChunkArray*): Returns all the chunks that contain the selected entities. Because a query operates on archetypes, shared component values, and change filters, which are all identical for all the entities in a chunk, the set of entities stored in the returned set of chunks is the same as the set of entities `ToEntityArray` returns.

Asynchronous versions of the above methods are also available, which schedule a job to gather the requested data. Some of these variants must return a `NativeList` instead of a `NativeArray` to support [enableable components](components-enableable.md). See [`ToEntityListAsync`](xref:Unity.Entities.EntityQuery.ToEntityListAsync*), [`ToComponentDataListAsync`](xref:Unity.Entities.EntityQuery.ToComponentDataListAsync*), and [`CreateArchetypeChunkArrayAsync`](xref:Unity.Entities.EntityQuery.CreateArchetypeChunkArrayAsync*).

## Additional information

* [EntityQuery filters](systems-entityquery-filters.md)