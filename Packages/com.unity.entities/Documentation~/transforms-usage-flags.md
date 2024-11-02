# Transform usage flags 

Transform usage flags control how Unity converts [Transform MonoBehaviour components](xref:UnityEngine.Transform) to entity data. You can use the values in [`TransformUsageFlags`](xref:Unity.Entities.TransformUsageFlags) to define what transform components are added to the entities during the [baking process](baking.md).

The flags help reduce the number of unnecessary transform components in baked entities. The following flags are available:

* `None`: Indicates that there are no specific transform component requirements. However, other [bakers](baking-baker-overview.md) can add `TransformUsageFlags` values to the entity.
* `Renderable`: Indicates that an entity requires the necessary transform components to be rendered, but it doesn't require the transform components to move the entity at runtime.
* `Dynamic`: Indicates that an entity requires transform components to move at runtime.
* `WorldSpace`: Indicates that an entity must be in world space, even if it has a dynamic entity as a parent.
* `NonUniformScale`: Indicates that an entity requires transform components that represent non uniform scale.
* `ManualOverride`: Ignore all `TransformUsageFlags` values from other bakers on the same GameObject. No transform components are added to the entity.

Unity requires these flags whenever an entity is accessed in a baker. Also, the bakers for default GameObject components automatically add the appropriate transform usage flags to the baked entities. For example, the baker for `MeshRenderer` adds `Renderable` as a transform usage flag.

## Use transform usage flags

You can use more than one flag on an entity and Unity combines the flags before adding the transform components to the entity. For example, if the `Dynamic` and `WorldSpace` flags are on an entity, Unity considers the entity dynamic and in world space at runtime.

Transform usage flags are helpful if you want to reduce the number of unnecessary transform components in baked entities. For example, if you have a GameObject that represents a building, and the building has a child GameObject that represents a window, because these GameObjects won't move at runtime, both of these GameObjects have the `Renderable` flag. When the baking process runs, both of the entities for the these GameObjects don't need to be in a hierarchy, and their transform information can be combined in a `LocalToWorld` component in world space. Unity then doesn't generate a `LocalTransform` or a `Parent` for these components, saving on unnecessary data.

Similarly, if the window GameObject is on a GameObject that represents a ship, you can mark the ship as `Dynamic` and keep the window as `Renderable`. At runtime, Unity gives the window the correct transform components (`LocalToWorld`, `LocalTransform`, and `Parent`) to make sure that it follows the ship around:

[!code-cs[Transform usage flags](../DocCodeSamples.Tests/TransformUsageFlagsExamples.cs#ship-example)]

>[!IMPORTANT]
> Entity prefabs are automatically marked as Dynamic so the instances can be placed in the world.

## Additional resources

* [`TransformUsageFlags` API documentation](xref:Unity.Entities.TransformUsageFlags)
* [Baker overview](baking-baker-overview.md)
