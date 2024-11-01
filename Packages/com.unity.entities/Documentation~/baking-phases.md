# Baking phases

[Baking](baking-overview.md) has multiple phases, but there are two key steps:

* **Bakers:** During this phase, [bakers](baking-baker-overview.md) take the authoring components on GameObjects and convert them into entities and components.
* **Baking systems:** During this phase, [baking systems](baking-baking-systems-overview.md) perform additional processing on these entities.

## Entity creation

Before the bakers run, Unity creates an entity for every authoring GameObject in a [subscene](conversion-subscenes.md). At this stage, the entity doesn’t have any components except for some internal metadata.

## Baker phase

After Unity creates the entities, it runs the bakers. Each baker processes a specific authoring component type, and multiple bakers can consume the same authoring component type. 

The Entities package and any package that uses entities provides a set of default bakers. For example, the Entities Graphics package has bakers for renderers, and the Unity Physics has bakers for rigid bodies. This doesn’t prevent you from adding more bakers to do further processing of those same types.

There is no guarantee in which order Unity runs the bakers. Because of this, no interdependency between bakers is allowed. This means that bakers can't read or change the components of an entity, they can only add new components.

Also, each baker is only allowed to change its own entity, or the entities it produces. Accessing another entity and modifying it at this stage breaks the logic and causes undefined behavior.

## Baking systems phase

After all the bakers have run, Unity runs the baking systems. Baking systems are [ECS systems](concepts-systems.md) that have a `BakingSystem` attribute to specify that they must only run during the baking process. You can order baking systems with the `UpdateAfter`, `UpdateBefore`, and `UpdateInGroup` attributes. The following default groups are provided, and run in the order below:

1. [`PreBakingSystemGroup`](xref:Unity.Entities.PreBakingSystemGroup) (this executes before the bakers)
1. [`TransformBakingSystemGroup`](xref:Unity.Entities.TransformBakingSystemGroup)
1. [`BakingSystemGroup`](xref:Unity.Entities.BakingSystemGroup) (the default baking system group)
1. [`PostBakingSystemGroup`](xref:Unity.Entities.PostBakingSystemGroup)

> [!NOTE]
> All baking systems run after any bakers, apart from baking systems from the `PreBakingSystemGroup`. `PreBakingSystemGroup` runs not only before the bakers, but also before the creation of the entities.

After Unity runs all the baking system groups, it stores the entity data  either in an [entity scene](conversion-scene-overview.md) and serializes it to disk, or directly reflects the changes into the main ECS world in the case of [live baking](baking-overview.md).

## Additional resources

* [Baker overview](baking-baker-overview.md)
* [Baking systems overview](baking-baking-systems-overview.md)