# EntityQuery filters

To further sort entities, you can use a filter to exclude entities based on the following:
 
* [Shared component filter](xref:Unity.Entities.EntityQuery.SetSharedComponentFilter*): Filter the set of entities based on specific values of a shared component.
* [Change filter](xref:Unity.Entities.EntityQuery.SetChangedVersionFilter*): Filter the set of Entities based on whether the value of a specific component type has changed.
* [Enableable components](components-enableable.md)

The filters you set remain in effect until you call [`ResetFilter`](xref:Unity.Entities.EntityQuery.ResetFilter) on the query object.

To ignore the query's active chunk filters, use the `EntityQuery` methods that have names ending in `IgnoreFilter`.  These methods are generally more efficient than the filtering equivalents. For example, see [`IsEmpty`](xref:Unity.Entities.EntityQuery.IsEmpty*) vs. [`IsEmptyIgnoreFilter`](xref:Unity.Entities.EntityQuery.IsEmptyIgnoreFilter). 

## Use a shared component filter

To use a shared component filter, include the [shared component](components-shared.md) in the `EntityQuery` along with any other needed components and call the [`SetSharedComponentFilter`](xref:Unity.Entities.EntityQuery.SetSharedComponentFilter*) method. Then pass in a struct of the same `ISharedComponent` type that contains the values to select. All values must match. You can add up to two different shared components to the filter.

You can change the filter at any time, but if you change the filter, it doesn't change any existing arrays of entities or components that you received from the group [`ToComponentDataArray<T>`](xref:Unity.Entities.EntityQuery.ToComponentDataArray*) or [`ToEntityArray`](xref:Unity.Entities.EntityQuery.ToEntityArray*) methods. You must recreate these arrays.

The following example defines a shared component named `SharedGrouping` and a system that only processes entities that have the group field set to `1`.

[!code-cs[shared-component-filter](../DocCodeSamples.Tests/EntityQueryExamples.cs#shared-component-filter)]

## Use a change filter

If you only need to update entities when a component value has changed, use the [`SetChangedVersionFilter`](xref:Unity.Entities.EntityQuery.SetSharedComponentFilter*) method to add that component to the `EntityQuery` filter. For example, the following `EntityQuery` only includes entities from chunks that another system has already written to the `Translation` component: 

[!code-cs[change-filter](../DocCodeSamples.Tests/EntityQueryExamples.cs#change-filter)]

For efficiency, the change filter applies to whole archetype chunks, and not individual entities. The change filter also only checks whether a system that declared write access to the component has run, and not whether it changed any data. For example, if another job that can write to that component type accesses the chunk, then the change filter includes all entities in that chunk. This is why you should always declare read-only access to components that you don't need to modify.

## Filtering by enableable components

[Enableable components](components-enableable.md) allow components on individual entities to be enabled and disabled at runtime. Disabling components on an entity doesn't move that entity into a new archetype, but for the purposes of `EntityQuery` matching, the entity is treated as if it doesn't have the component. Specifically:

* If an entity has component `T` disabled, it will not match queries that require component `T` (using `WithAll<T>()`).
* If an entity has component `T` disabled, it will match queries that exclude component `T` (using `WithNone<T>()`).

Most `EntityQuery` operations, such as [`ToEntityArray`](xref:Unity.Entities.EntityQuery.ToEntityArray*) and [`CalculateEntityCount`](xref:Unity.Entities.EntityQuery.CalculateEntityCount*), automatically filter out entities whose enableable components would cause them not to match the query. To disable this filtering, use the `IgnoreFilter` variants of these operations, or pass the [`EntityQueryOptions.IgnoreComponentEnabledState`](xref:Unity.Entities.EntityQueryOptions.IgnoreComponentEnabledState) at query creation time.

See the [enableable components](components-enableable.md) documentation for more details.

## Further resources

* [Create an EntityQuery](systems-entityquery-create.md)
* [Shared components](components-shared.md)
* [Change filter](xref:Unity.Entities.EntityQuery.SetChangedVersionFilter*)
* [Enableable components](components-enableable.md)