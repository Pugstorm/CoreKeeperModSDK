# Transform helper overview

The [`TransformHelpers`](xref:Unity.Transforms.TransformHelpers) class contains extensions to the math library that make it easier for you to work with transformation matrices. 

In particular, the extensions help you work with the `float4x4` contained in the [`LocalToWorld`](xref:Unity.Transforms.LocalToWorld) component.

## Extension methods

You can use the extension methods in `TransformHelpers` to minimize the usage of matrix math in your code. For example, to transform a point from local space into world space you can use [`TransformPoint`](xref:Unity.Transforms.TransformHelpers.TransformPoint*):

```c#
float3 myWorldPoint = myLocalToWorld.Value.TransformPoint(myLocalPoint);
```

Or, to transform a point from world space into local space you can use [`InverseTransformPoint`](xref:Unity.Transforms.TransformHelpers.InverseTransformPoint*):

```c#
float3 myLocalPoint = myLocalToWorld.Value.InverseTransformPoint(myWorldPoint);
```

The transformation of rotation and direction are handled in a similar way.

## Other methods

There are a few methods that aren't extensions outlined below.

### LookAtRotation

The [`LookAtRotation`](xref:Unity.Transforms.TransformHelpers.LookAtRotation*) method computes a rotation so that "forward" points to the target:

[!code-cs[conversion](../DocCodeSamples.Tests/TransformHelperExamples.cs#lookatrotation)]

### ComputeWorldTransformMatrix

You can use the [`ComputeWorldTransformMatrix`](xref:Unity.Transforms.TransformHelpers.ComputeWorldTransformMatrix*) method to immediately use an entity's precise world-space transformation matrix. For example:

* When performing a raycast from an entity which might be part of an entity hierarchy, such as the wheel of a car object. The ray origin must be in world-space, but the entity's [`LocalTransform`](xref:Unity.Transforms.LocalTransform) component might be relative to its parent.
* When an entity's transform needs to track another entity's transform in world-space, and the targeting entity or the targeted entity are in a transform hierarchy.
* When an entity's transform is modified in the [`LateSimulationSystemGroup`](xref:Unity.Entities.LateSimulationSystemGroup) (after the [`TransformSystemGroup`](xref:Unity.Transforms.TransformSystemGroup) has updated, but before the [`PresentationSystemGroup`](xref:Unity.Entities.PresentationSystemGroup) runs), you can use `ComputeWorldTransformMatrix` to compute a new `LocalToWorld` value for the affected entity.

[!code-cs[conversion](../DocCodeSamples.Tests/TransformHelperExamples.cs#computeworld)]

## Additional resources

* [`TransformHelpers` API documentation](xref:Unity.Transforms.TransformHelpers)