# Weakly reference a scene

Weak references to scenes work in the same way as weak references to objects. For more information, refer to [Weakly reference an object](content-management-get-a-weak-reference.md).

Unity uses an `UntypedWeakReferenceId` to weakly reference [scenes](xref:CreatingScenes) so, to weakly reference a scene from a C# script, you can use the same workflow as described in [Weakly reference an object from a C# script](content-management-get-a-weak-reference.md#weakly-reference-an-object-from-a-c-script).

## Weakly reference a scene from the Inspector

The [RuntimeContentManager](xref:Unity.Entities.Content.RuntimeContentManager) has a specific set of APIs to manage weakly-referenced scenes at runtime. This means that the[WeakObjectReference](xref:Unity.Entities.Content.WeakObjectReference`1) wrapper doesn't work for scenes. The scene equivalent to this wrapper is [WeakObjectSceneReference](xref:Unity.Entities.Content.WeakObjectSceneReference) which provides the same runtime and editor workflow benefits as `WeakObjectReference`, but for scenes.

To weakly reference a scene from the Inspector, substitute `WeakObjectSceneReference` for `WeakObjectReference` in the [Weakly reference an object from the Inspector](content-management-get-a-weak-reference.md#weakly-reference-an-object-from-the-inspector) workflow. The following code sample shows how to do this.

[!code-cs[](../DocCodeSamples.Tests/content-management/WeaklyReferenceSceneFromInpsector.cs#example)]

## Additional resources

* [Weakly reference an object](content-management-get-a-weak-reference.md)