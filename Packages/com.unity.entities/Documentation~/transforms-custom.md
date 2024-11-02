# Custom transforms

You can customize the built-in transform system to address the specific transform functionality needs of your project. This section explains how to create a custom transform system, and uses the [2D custom transform system](https://github.com/Unity-Technologies/EntityComponentSystemSamples/tree/master/EntitiesSamples/Assets/Miscellaneous/CustomTransforms) as a concrete example of one.

## Write groups

[Write groups](systems-write-groups.md) enable you to override the built-in transform system with your own transforms. The built-in transform system uses write groups internally and you can configure them to make the built-in transform system ignore entities that you want to process with your custom transform system.

More precisely, write groups exclude specific entities from queries. These queries are passed to the jobs that the built-in transform system uses. You can use write groups on certain components to exclude entities with those components from being processed by the jobs of the built-in transform system, and those entities can instead be processed by your own transform systems. For more information, refer to the documentation on [write groups](systems-write-groups.md).

## Create a custom transform system

The following steps outline how to create a custom transform system:

* Substitute the [`LocalTransform`](xref:Unity.Transforms.LocalTransform) component.
* Create an authoring component to receive your custom transforms.
* Replace the [`LocalToWorldSystem`](xref:Unity.Transforms.LocalToWorldSystem).

### Substitute the LocalTransform component

The built-in transform system adds a [`LocalTransform`](xref:Unity.Transforms.LocalTransform) component to each entity by default. It stores the data that represents an entity's position, rotation, and scale. There are also a variety of static helper methods defined on this component.

To create your own custom transform system, you have to substitute the `LocalTransform` component with your own.

1. Create a .cs file that defines a substitute for the built-in `LocalTransform` component. You can copy the built-in `LocalTransform.cs` file from the Entities package into your assets folder and then edit the contents. To do this, go to **Packages &gt; Entities &gt; Unity.Transforms** in your project, copy the `LocalTransform.cs` file, and rename it.
1. Change the properties and methods to suit your needs. See the following example of a custom `LocalTransform2D` component:

[!code-cs[LocalTransform2DDocsSnippet](../DocCodeSamples.Tests/TransformsCustom.cs#LocalTransform2D)]

The above example modifies the built-in `LocalTransform` in the following ways:

* Adds the `[WriteGroup(typeof(LocalToWorld))]` attribute.
* Reduces the `Position` field from a `float3` to a `float2`. This is because in the 2D sample, entities only move along the XY plane.
* Reduces the `Rotation` field to a `float` that represents the number of degrees of rotation around the z-axis. The built-in transform system's `Rotation` property is a quaternion that represents a rotation in 3D space.
* Removed all methods apart from `ToMatrix` and `ToString`. The `ToMatrix` method has been modified to work in 2D. The other methods aren't needed for the custom 2D transform system.

> [!NOTE]
> `LocalTransform2D` is in the global namespace. In the above linked sample project it's in a sub-namespace to ensure that it doesn't interfere with the other samples in the same project. Both options work as long as all the files of the custom transform system are within the same namespace.

### Create an authoring component

Each entity that your custom transform system needs to process must fulfill the following criteria:

* Has a custom replacement for the `LocalTransform` component, with a different name.
* Has a `LocalToWorld` component
* If the entity has a parent entity, then it must have a `Parent` component that points to it.

To meet this criteria, add an authoring component to each entity, and use [transform usage flags](xref:Unity.Entities.TransformUsageFlags) to prevent the entity from receiving any components from the built-in transform system:

[!code-cs[Transform2DAuthoringDocsSnippet](../DocCodeSamples.Tests/TransformsCustom.cs#Transform2DAuthoring)]

The above example adds the custom `LocalTransform2D` component and the built-in `LocalToWorld` component to the authoring component. If applicable, it also adds a `Parent` component that points to the entity's parent entity.

### Replace the LocalToWorldSystem

The built-in `LocalToWorldSystem` computes the `LocalToWorld` matrices of root and child entities in the two corresponding jobs `ComputeRootLocalToWorldJob` and `ComputeChildLocalToWorldJob`. You need to replace this system with your own transform system.

1. Copy the built-in `LocalToWorldSystem.cs` file into your assets folder and then edit the contents. To do this, go to **Packages &gt; Entities &gt; Unity.Transforms** in your project, copy the `LocalToWorldSystem.cs` file, and rename it.
1. Replace all instances of the `LocalTransform` component with the name of your custom transform component (`LocalTransform2D` in the example).
1. Remove the `WithOptions(EntityQueryOptions.FilterWriteGroup);` lines from the queries. If you don't remove these lines, your system excludes the corresponding entities like the built-in transform system does.

> [!NOTE]
> `LocalToWorldSystem` uses [unsafe](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/unsafe-code) native code so, to avoid errors, enable the [Allow unsafe code](xref:UnityEditor.Compilation.ScriptCompilerOptions.AllowUnsafeCode) property in your project. To enable this property, go to **Edit** &gt; **Project Settings** &gt; **Player** &gt; **Other Settings** and select **Allow unsafe code**.

## Additional resources

- [Using transforms](transforms-using.md)
- [Write groups overview](systems-write-groups.md)
- [`TransformUsageFlags` API documentation](xref:Unity.Entities.TransformUsageFlags)
