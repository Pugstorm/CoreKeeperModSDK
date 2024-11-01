# Load a weakly-referenced object at runtime

Unity doesn't automatically load weakly-referenced objects, so it's your responsibility to load them before you need them. To load an object at runtime, you must have a weak reference to the object stored in an [ECS component](concepts-components.md). The weak reference can be either a [WeakObjectReference](xref:Unity.Entities.Content.WeakObjectReference`1) or an [UntypedWeakReferenceId](xref:Unity.Entities.Serialization.UntypedWeakReferenceId). For information on how to store a weak reference to an object, refer to [Weakly reference an object](content-management-get-a-weak-reference.md). 

## Load an object at runtime with a WeakObjectReference

The following code example shows how to use `WeakObjectReferences` to load and render a mesh with a material from an [ISystem](systems-isystem.md). For information on how to pass a `WeakObjectRefrence` to a component, refer to [Weakly reference an object from the Inspector](content-management-get-a-weak-reference.md#weakly-reference-an-object-from-the-inspector).

[!code-cs[](../DocCodeSamples.Tests/content-management/LoadObjectWeakObjectReference.cs#example)]

## Load an object at runtime with an UntypedWeakReferenceId

The following code example shows how to use the [RuntimeContentManager](xref:Unity.Entities.Content.RuntimeContentManager) APIs and `UntypedWeakReferenceIds` to load and render a mesh with a material from an [ISystem](systems-isystem.md). For information on how to pass a `UntypedWeakReferenceId` to a component, refer to [Weakly reference an object from a C# script](content-management-get-a-weak-reference.md#weakly-reference-an-object-from-a-c-script).

[!code-cs[](../DocCodeSamples.Tests/content-management/LoadObjectUntypedWeakReferenceId.cs#example)]

## Additional resources

* [Load a weakly-referenced scene at runtime](content-management-load-a-scene.md)