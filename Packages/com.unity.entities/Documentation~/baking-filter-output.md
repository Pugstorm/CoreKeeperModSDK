# Filter baking output

By default, every entity and every component created in the [conversion world](baking-baking-worlds-overview.md) is part of the baking output.

However, not every GameObject in an authoring scene is relevant to keep as an entity. For example, a control point on a spline might only be used during authoring, and can be discarded at the end of baking.

To exclude entities from the baking output, you can add the [`BakingOnlyEntity`](xref:Unity.Entities.BakingOnlyEntity) tag component to the entity. When you add this tag component, Unity doesn't store the entity in the entity scene, and doesn't merge it into the main world. A [baker](baking-baker-overview.md) can directly add `BakingOnlyEntity` to an entity, or you can add the [`BakingOnlyEntityAuthoring`](xref:Unity.Entities.Hybrid.Baking.BakingOnlyEntityAuthoring) to a GameObject to achieve the same effect.

You can also exclude components with the following attributes:

* [`[BakingType]`](xref:Unity.Entities.BakingTypeAttribute): Filters any components marked with this attribute from the baking output.
* [`[TemporaryBakingType]`](xref:Unity.Entities.TemporaryBakingTypeAttribute): Destroys any components marked with this attribute from the baking output. This means that components marked with this attribute don't remain from one baking pass to the next, and only exist during the time that a particular baker ran. 

You can exclude components to pass information from a [baker](baking-baker-overview.md) to a [baking system](baking-baking-systems-overview.md). For example, a baker can record the bounding box of an entity as a baking type, and later in the baking process, a baking system can collect all the bounding boxes and compute a convex hull. If only the hull is useful, the bounding boxes can be discarded.

In this particular example, you have to use the `[BakingType]` attribute rather than `[TemporaryBakingType]`. This is because the convex hull system need to access all bounding boxes, and not only the ones that changed. However, if it was a system that needed to process all the entities whose bounding boxes intersect with the ground plane, it would be better to only process those entities whose bounding boxes effectively changed. Therefore you could use `[TemporaryBakingType]`. 

The following is an example that uses `[TemporaryBakingType]`:

[!code-cs[](../DocCodeSamples.Tests/BakingExamples.cs#TemporaryBakingType)]

## Additional resources

* [Baking systems overview](baking-baking-systems-overview.md)
* [Baking worlds overview](baking-baking-worlds-overview.md)