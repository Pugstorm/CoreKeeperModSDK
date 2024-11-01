# Select and access data

`Entities.ForEach` has its own mechanism to define the entity query that it uses to select the entities to process. The query automatically includes any components that you use as parameters of the lambda expression. 

You can use the `WithAll`, `WithAny`, and `WithNone` clauses to further refine which entities `Entities.ForEach` selects. Refer to the [`SystemBase.Entities`](xref:Unity.Entities.SystemBase.Entities) API documentation for the complete list of query options. 

The following example uses these clauses to select entities based on these parameters:

* The entity has the components, `Destination`, `Source`, and `LocalToWorld`
* The entity has at least one of the components, `ObjectRotation`, `ObjectPosition`, or `ObjectUniformScale`
* The entity doesn't have a `ObjectNonUniformScale` component.

[!code-cs[entity-query](../DocCodeSamples.Tests/LambdaJobExamples.cs#entity-query)]

In this example, only the `Destination` and `Source` components are accessed inside the lambda expression because they're the only components in the parameter list.

## Access the EntityQuery object 

`Entities.ForEach` creates an [`EntityQuery`](xref:Unity.Entities.EntityQuery) in [`OnCreate`](xref:Unity.Entities.ComponentSystemBase.OnCreate), which you can use a copy of at any time, even before `Entities.ForEach` is invoked.

To access this entity query, use [`WithStoreEntityQueryInField(ref query)`](xref:Unity.Entities.SystemBase.Entities) with the `ref` parameter modifier. This method assigns a reference to the query to the field you provide. However, this `EntityQuery` doesn't have any of the filters that the `Entities.ForEach` invocation sets up.

The following example illustrates how to access the `EntityQuery` object implicitly created for an `Entities.ForEach` construction. The example uses the `EntityQuery` object to invoke the [`CalculateEntityCount()`](xref:Unity.Entities.EntityQuery.CalculateEntityCount*) method and uses this count to create a native array with enough space to store one value per entity that the query selects:

[!code-cs[store-query](../DocCodeSamples.Tests/LambdaJobExamples.cs#store-query)]

## Access optional components

The `Entities.ForEach` lambda expression doesn't support querying and accessing optional components with `WithAny<T,U>`. 

If you want to read or write to an optional component, split the `Entities.ForEach` construction into multiple jobs for each combination of the optional components. For example, if you have two optional components, you need three `ForEach` constructions: one including the first optional component, one including the second, and one including both components. Another alternative is to use `IJobChunk `iterate by chunk. For more information, refer to [Iterating over data by chunk](iterating-data-ijobchunk.md).
