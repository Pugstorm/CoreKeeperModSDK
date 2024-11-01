# Weakly reference an object

A [weak object reference](content-management-intro.md#weakly-referenced-objects) is a handle to an object that is valid regardless of whether the object is loaded or unloaded. If you create a weak reference to an object, Unity includes that object in a content archive which makes the object available to you at runtime. You can then use the [RuntimeContentManager](xref:Unity.Entities.Content.RuntimeContentManager) API to load, use, and release the weakly-referenced objects. 

The content management system provides a way to weakly reference an object via the Inspector and via a C# script.

## Weakly reference an object from the Inspector

The [WeakObjectReference](xref:Unity.Entities.Content.WeakObjectReference`1) struct provides a wrapper around the `RuntimeContentManager` APIs that are responsible for managing weakly-referenced objects. It also makes it possible to create a weak reference to an object via the Inspector. The Inspector property drawer for a `WeakObjectReference` is an object field that you can drag objects onto. Internally, Unity generates a weak reference to the object you assign, which you can then pass to [ECS components](concepts-components.md) during the [baking process](baking-overview.md).

The `WeakObjectReference` wrapper also makes it easier to manage individual weakly-referenced objects at runtime. It provides methods and properties to load, use, and release the object that it weakly references.

The following code sample shows how to create a [Baker](xref:Unity.Entities.Baker`1) that passes a `WeakObjectReference` of a [mesh asset](xref:class-Mesh) to an [unmanaged component](components-unmanaged.md). The `mesh` property appears in the Inspector of the MeshRefSample component as an object field that you can assign a mesh asset to.

[!code-cs[](../DocCodeSamples.Tests/content-management/WeaklyReferenceFromInspector.cs#example)]

## Weakly reference an object from a C# script

The `RuntimeContentManager` APIs manage [weakly-referenced objects](content-management-intro.md#weakly-referenced-objects) by an [UntypedWeakReferenceId](xref:Unity.Entities.Serialization.UntypedWeakReferenceId). 

The following code sample shows how to get the `UntypedWeakReferenceId` of the objects currently selected in the [Project window](xref:ProjectView). To make Unity include these objects in content archives, you must bake the weak reference IDs into an ECS component. To pass weak reference IDs created in Editor scripts to a baker, you can use a [Scriptable Object](xref:class-ScriptableObject). An Editor script can write weak reference IDs to a ScriptableObject then later, during the baking process, a baker can read the IDs from the ScriptableObject and write them into an ECS component.

[!code-cs[](../DocCodeSamples.Tests/content-management/WeaklyReferenceFromScript.cs#example)]

## Additional resources

* [Load a weakly-referenced object at runtime](content-management-load-an-object.md)