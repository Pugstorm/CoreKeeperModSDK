# Filtering data

You can filter the data in an `Entities.ForEach` expression, either by change, or by shared component. 

## Change filtering

You can use `WithChangeFilter<T>` to enable change filtering, which processes components only if another component in the entity has changed since the last time the current `SystemBase` instance has run. The component type in the change filter must either be in the lambda expression parameter list, or part of a `WithAll<T>` statement. For example:

[!code-cs[with-change-filter](../DocCodeSamples.Tests/LambdaJobExamples.cs#with-change-filter)]

An entity query supports change filtering on up to two component types.

Unity applies change filtering at the archetype chunk level. If any code accesses a component in a chunk that has write access, then Unity marks that component type in that archetype chunk as changed, even if the code didn’t change any data. 

## Shared component filtering

Unity groups entities with [shared components](components-shared.md) into chunks with other entities that have the same value for their shared components. To select groups of entities that have specific shared component values, use the [`WithSharedComponentFilter`](xref:Unity.Entities.LambdaJobQueryConstructionMethods.WithSharedComponentFilter*) method.

The following example selects entities grouped by a `Cohort ISharedComponentData`. The lambda expression in this example sets a `DisplayColor IComponentData` component based on the entity’s cohort:

[!code-cs[with-shared-component](../DocCodeSamples.Tests/LambdaJobExamples.cs#with-shared-component)]

The example uses the `EntityManager` to get all the unique cohort values. It then schedules a lambda job for each cohort, and passes the new color to the lambda expression as a captured variable. 