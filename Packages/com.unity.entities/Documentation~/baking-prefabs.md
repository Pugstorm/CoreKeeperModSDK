#  Prefabs in baking

During the baking process, [prefabs](xref:Prefabs) are baked into **entity prefabs**. An entity prefab is an entity that has the following components:

* A prefab tag: Identifies the entity as a prefab and excludes them from queries by default. 
* A [`LinkedEntityGroup`](xref:Unity.Entities.LinkedEntityGroup) buffer: Stores all children within the prefab in a flat list. For example, to quickly create the whole set of entities within a prefab without having to traverse the hierarchy. 

You can use an entity prefabs in a similar way to GameObject prefabs, because they can be instantiated at runtime. However, to use them at runtime, you must [bake](baking-overview.md) the GameObject Prefabs and make them available in the [entity scene](conversion-scene-overview.md).

> [!NOTE]
> When prefab instances are present in the subscene hierarchy, baking treats them as normal GameObjects because they don't have the `Prefab` or `LinkedEntityGroup` components.

## Create and register an Entity prefab

To ensure that prefabs are baked and available in the entity scene, you must register them to a [baker](baking-baker-overview.md). This makes sure that there is a dependency on the prefab object, and that the prefab is baked and receives the proper components. When you reference the entity prefab in a component, Unity serializes the content into the subscene that uses it.

[!code-cs[PrefabInSubScene](../DocCodeSamples.Tests/BakingPrefabExamples.cs#PrefabInSubScene)]

Alternatively, to reference the entity prefab during baking, use the [`EntityPrefabReference`](xref:Unity.Entities.Serialization.EntityPrefabReference) struct. This serializes the ECS content of the prefab into a separate entity scene file that can be loaded at runtime before using the prefab. This prevents Unity from duplicating the entity prefab in every subscene that it's used in.

[!code-cs[PrefabReferenceInSubScene](../DocCodeSamples.Tests/BakingPrefabExamples.cs#PrefabReferenceInSubScene)]

## Instantiate prefabs

To instantiate prefabs that are referenced in components, use an [`EntityManager`](xref:Unity.Entities.EntityManager) or an [entity command buffer](systems-entity-command-buffers.md):

[!code-cs[InstantiateEmbeddedPrefabs](../DocCodeSamples.Tests/BakingPrefabExamples.cs#InstantiateEmbeddedPrefabs)]

> [!NOTE]
> Instanced prefabs will contain a [SceneSection component](streaming-scene-sections.md#entity-prefabs-and-sections). This could affect the lifetime of the entity.

To instantiate a prefab referenced with `EntityPrefabReference`, you must also add the [`RequestEntityPrefabLoaded`](xref:Unity.Scenes.RequestEntityPrefabLoaded) struct to the entity. This is because Unity needs to load the prefab before it can use it. `RequestEntityPrefabLoaded` ensures that the prefab is loaded and the result is added to the `PrefabLoadResult` component. Unity adds the [`PrefabLoadResult`](xref:Unity.Scenes.PrefabLoadResult) component to the same entity that contains the `RequestEntityPrefabLoaded`.

[!code-cs[InstantiateLoadedPrefabs](../DocCodeSamples.Tests/BakingPrefabExamples.cs#InstantiateLoadedPrefabs)]

## Prefabs in queries

By default, Unity excludes prefabs from queries. To include entity prefabs in queries, use the [`IncludePrefab`](xref:Unity.Entities.EntityQueryOptions) field in the query:

[!code-cs[PrefabsInQueries](../DocCodeSamples.Tests/BakingPrefabExamples.cs#PrefabsInQueries)]

## Destroy prefab instances

To destroy a prefab instance, use an [`EntityManager`](xref:Unity.Entities.EntityManager) or an [entity command buffer](systems-entity-command-buffers.md), in the same way that you destroy an entity. Also, destroying a prefab has [structural change](concepts-structural-changes.md) costs. 

[!code-cs[DestroyPrefabs](../DocCodeSamples.Tests/BakingPrefabExamples.cs#DestroyPrefabs)]

## Additional resources

* [Baker overview](baking-baker-overview.md)
