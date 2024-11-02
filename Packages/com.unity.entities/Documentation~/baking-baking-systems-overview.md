# Baking systems overview

Baking systems operate on ECS data through queries and process components and entities in batches. Because a baking system is a [system](concepts-systems.md), it can make use of jobs and Burst compilation, which is ideal for heavy processing. 

A baking system differs from a [baker](baking-baker-overview.md) by the way that it processes data. A baker reads from managed authoring data and processes components one by one, whereas baking systems process data in batches.

The baking process creates all entities before it runs the baking systems (except for the systems in [`PreBakingSystemGroup`](baking-phases.md#baking-systems-phase)). This means that baking systems can process all entities initially created, and those that bakers create.

Baking systems don't automatically track dependencies and [structural changes](concepts-structural-changes.md). As such, you have to declare dependencies explicitly. Also, you must manually track and revert changes when adding components to maintain [incremental baking](baking-overview.md#incremental-baking).

Baking systems can alter the [world](concepts-worlds.md) in any way, including creating new entities. However, any entities you create in a baking system won't end up in a baked entity scene. You can create an entity in a baking system to transfer data between baking systems, but if you want the entity to end up in the baked entity scene, you must create it in a [baker](baking-baker-overview.md). When you create an entity in a baker, [`CreateAdditionalEntity`](xref:Unity.Entities.IBaker.CreateAdditionalEntity) configures the entity to properly work with baking and live baking.

## Create a baking system

To create a Baking system, mark it with the `[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]` attribute. This lets the baking process discover baking systems and add them to the [baking world](baking-baking-worlds-overview.md). Unity updates baking systems on every single baking pass.

The following is an example of a baking system that adds a [tag component](components-tag.md) to every entity that has another component. It includes code that removes the tag when the component isn't present. Without this, removing the component leaves the tag on the entity:

[!code-cs[](../DocCodeSamples.Tests/BakingExamples.cs#BakingSystem)]

## Additional resources

* [Baker overview](baking-baker-overview.md)
* [Baking world overview](baking-baking-worlds-overview.md)