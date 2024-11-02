# Using transforms

To use transforms in your project, use the [`Unity.Transforms`](xref:Unity.Transforms) namespace to control the position, rotation, and scale of any entity in your project.

`LocalTransform` represents the relative position, rotation, and scale of the entity. If there is a parent, the transform is relative to that parent. If there is no parent, the transform is relative to the world origin. You can read and write to this component.

```c#
public struct LocalTransform : IComponentData
{
    public float3 Position;
    public float Scale;
    public quaternion Rotation;
}
```

## Using the API

There are no methods in the API to modify `LocalTransform`. All methods return a new value, and do not change the transform itself. So if you want to modify the transform, you must use the assignment operator. For example, to rotate a transform around the Z axis:

```c#
    myTransform = myTransform.RotateZ(someAngle);
```

The only way to modify `LocalTransform` directly is by writing to the Position, Rotation, and Scale properties. For example:

```c#
    myTransform.Position += math.up();
```

This code is equivalent to:

```c#
    myTransform = myTransform.Translate(math.up());
```

There are several methods to construct a transform for you. So if you want to create a `LocalTransform` with a specified position, but using default rotation and scale, use this:

```c#
    var myTransform = LocalTransform.FromPosition(1, 2, 3);
```

## Using a hierarchy

You can use `LocalTransform` by itself. However, if you want to use a hierarchy of Entities, you must also use `Parent`. To set a parent of a child entity use [`Parent`](xref:Unity.Transforms.Parent):

```c#
public struct Parent : IComponentData
{
    public Entity Value;
}
```

To make sure that the parents find their children, and to set up their child component, [`ParentSystem`](xref:Unity.Transforms.ParentSystem) must run.

Use the `static` flag for everything that isn't going to move. This improves performance and reduces memory consumption.

The transform system is optimized for large numbers of hierarchies at the root level. A root level transform is a transform with no parent. Avoid having large hierarchies under a single root. The work of concatenating hierarchical transforms is divided across jobs at the root level. So the worst case scenario is to keep large numbers of non-static entities under a single root.
