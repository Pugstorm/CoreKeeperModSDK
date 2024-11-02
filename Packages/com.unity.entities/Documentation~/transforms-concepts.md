# Transform concepts

You can use the [`Unity.Transforms`](xref:Unity.Transforms) namespace to control the position, rotation, and scale of any entity in your project.

The following components are used in the transform system:
* [`LocalToWorld`](xref:Unity.Entities.TransformAuthoring.LocalToWorld)
* [`LocalTransform`](xref:Unity.Transforms.LocalTransform)
* [`PostTransformMatrix`](xref:Unity.Transforms.PostTransformMatrix)
* [`Parent`](xref:Unity.Transforms.Parent)
* [`Child`](xref:Unity.Transforms.Child)

There are two systems that update the transform system:
* [`ParentSystem`](xref:Unity.Transforms.ParentSystem)
* [`LocalToWorldSystem`](xref:Unity.Transforms.LocalToWorldSystem)

## Transform hierarchy

`Unity.Transforms` is hierarchical, which means that you can transform Entities based on their relationship to each other.

For example, a car body can be the parent of its wheels. The wheels are children of the car body. When the car body moves, the wheels move with it. You can also move and rotate the wheels relative to the car body.

An entity can have multiple children, but only one parent. Children can have their own child entities. These multiple levels of parent-child relationships form a transform hierarchy. The entity at the top of a hierarchy, without a parent, is the **root**.

To declare a Transform hierarchy, you must do this from the bottom up. This means that you use [`Parent`](xref:Unity.Transforms.Parent) to declare an Entity's parent, rather than declare its children. If you want to declare a child of an Entity, find the Entities that you want to be children and set their parent to be the target Entity. For more information, see the [Using a hierarchy](transforms-using.md#using-a-hierarchy) documentation.

## The LocalToWorld component

The `LocalToWorld` matrix represents the transform from local space to world space. This matrix is what the rendering system uses to render the entity's geometry. By default, the `LocalToWorldSystem` updates this component from the `LocalToWorld` component. This default update means that you don't need to update `LocalToWorld`, and the system does that for you. To disable this automatic update use a `[WriteGroup(typeof(LocalToWorld))]` attribute on a component. With this write group, you have complete control over `LocalToWorld`. For more information, see the documentation on [write groups](systems-write-groups.md).

>[!NOTE]
>The `LocalToWorld` component value might be out of date or invalid while the `SimulationSystemGroup` is running. This is because the transform system only updates the component value when the `TransformSystemGroup` runs. It might also contain additional offsets applied for graphical smoothing purposes. Therefore, while the `LocalToWorld` component might be useful as a fast approximation of an entity's world-space transformation when its latency is acceptable, you shouldn't rely on it if you need an accurate, up-to-date world transform for simulation purposes. In those cases, use the [`ComputeWorldTransformMatrix`](xref:Unity.Transforms.TransformHelpers.ComputeWorldTransformMatrix*) method.

## The LocalTransform component

The `LocalTransform` component has three properties. They control the position, rotation, and scale of the entity. When this component is present, it controls `LocalToWorld`: 

```c#
public struct LocalTransform : IComponentData
{
    public float3 Position;
    public float Scale;
    public quaternion Rotation;
}
```

If the entity has a `Parent` component, Position, Rotation, and Scale are relative to that parent. If the entity doesn't have a `Parent` component, the transform is relative to the origin of the world.

## The PostTransformMatrix component

`LocalTransform` only supports uniform scale. To render geometry with nonuniform scale, you must use a `PostTransformMatrix` matrix. This is applied after `LocalTransform`. You can also use this component to introduce shear transforms, or to translate the entity relative to its pivot.

## The Parent component

The `Parent` component defines the hierarchy. You put this on every child that you want to be part of a hierarchy.

## The Child component

The `Child` component buffer holds all children of a parent. `ParentSystem` manages this buffer and its contents. You only need to manage the `Parent` component. The system will take care of maintaining the corresponding `Child` component.

## The ParentSystem

The `ParentSystem` maintains the `Child` component buffer, based on the `Parent` component of the children. When you set a `Parent` component on a child, Unity only updates the parent's `Child` component when the `ParentSystem` has run.

## The LocalToWorldSystem

`LocalToWorldSystem` computes and updates the `LocalToWorld` component, based on the `LocalTransform` component and the hierarchy. When you set a `LocalTransform` component on an entity, Unity only updates its `LocalToWorld` component when the `LocalToWorldSystem` has run.
