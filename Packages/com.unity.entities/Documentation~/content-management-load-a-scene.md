# Load a weakly-referenced scene at runtime

Unity doesn't automatically load weakly-referenced scenes. This means you can't use [SceneManager](xref:UnityEngine.SceneManagement.SceneManager) APIs such as [LoadScene](xref:UnityEngine.SceneManagement.SceneManager.LoadScene(System.Int32,UnityEngine.SceneManagement.LoadSceneMode)) to open the scene. Instead, you must use [RuntimeContentManager](xref:Unity.Entities.Content.RuntimeContentManager) APIs to load the scene from the content archive. To load a scene at runtime, you must have a weak reference to the scene stored in an [ECS component](concepts-components.md). The weak reference can be either a [WeakObjectSceneReference](xref:Unity.Entities.Content.WeakObjectSceneReference) or an [UntypedWeakReferenceId](xref:Unity.Entities.Serialization.UntypedWeakReferenceId). For information on how to store a weak reference to an object, refer to [Weakly reference a scene](content-management-get-a-weak-reference-scene.md). 

## Load a scene at runtime with a WeakObjectSceneReference

The following code example shows how to use `WeakObjectSceneReferences` to load scenes from an [`ISystem`](systems-isystem.md). For information on how to pass a `WeakObjectSceneReference` to a component, refer to [Weakly reference a scene](content-management-get-a-weak-reference-scene.md).

[!code-cs[](../DocCodeSamples.Tests/content-management/LoadSceneWeakObjectReference.cs#example)]


## Load a scene at runtime with an UntypedWeakReferenceId

The following code example shows how to use the [RuntimeContentManager](xref:Unity.Entities.Content.RuntimeContentManager) APIs and an `UntypedWeakReferenceId` to load a scene from an [`ISystem`](systems-isystem.md). For information on how to pass a `UntypedWeakReferenceId` to a component, refer to [Weakly reference an object from a C# script](content-management-get-a-weak-reference.md#weakly-reference-an-object-from-a-c-script).


[!code-cs[](../DocCodeSamples.Tests/content-management/LoadSceneUntypedWeakReferenceId.cs#example)]

## Additional resource

* [Load a weakly-referenced object at runtime](content-management-load-an-object.md)