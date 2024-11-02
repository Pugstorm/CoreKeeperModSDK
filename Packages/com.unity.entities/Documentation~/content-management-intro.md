# Introduction to content management

For applications built with the Entities package, Unity stores assets, scenes, and other objects in content archives. Unity builds these content archives automatically during the Player build process and loads, uses, and releases the objects within them at runtime when the objects are required. This all happens internally, but Unity also provides APIs that you can use to interface with the content management system yourself.

This content archive-based content management system was designed to provide optimal performance in data-oriented Entities-based applications without much setup. It provides [Burst-compiled](https://docs.unity3d.com/Packages/com.unity.burst@latest) and thread-safe APIs that you can use within ECS [systems](concepts-systems.md), multithreaded job code, and, in some cases, MonoBehaviour components. The other content management solutions in Unity, such as [Resources](xref:UnityEngine.Resources), [AssetBundles](xref:AssetBundlesIntro), and [Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest), either don't work with Entities or are suboptimal solutions for data-oriented applications. This means that it's best practice to use this content archive-based content management system for applications that use Entities.

## How Unity generates content archives

During the Player build process, Unity generates content archives to store every object referenced in the [subscenes](conversion-subscenes.md) included in the build. Unity generates at least one content archive per subscene that references objects. If multiple subscenes reference the same object, Unity moves the object into a shared content archive. You can reference objects in the following ways:

* With a [strong reference](#strongly-referenced-objects): When you directly assign an object to a MonoBehaviour component property in a subscene.
* With a [weak reference](#weakly-referenced-objects): When you pass an object's [UntypedWeakReferenceId](xref:Unity.Entities.Serialization.UntypedWeakReferenceId) to an ECS component during the [baking process](baking-overview.md).

Unity bundles both strongly-referenced and weakly-referenced objects into the same content archives, but handles them differently at runtime.

### Strongly-referenced objects

Strongly-referenced objects are objects that you directly assign to a property of a MonoBehaviour component in the subscene. For example, if you assign a [mesh object](xref:class-Mesh) to a [Mesh Filter](xref:class-MeshFilter) component, Unity considers the mesh object to be strongly referenced.

At runtime, Unity automatically loads strongly-referenced objects when they're required, keeps track of where they're used, and unloads them when they're no longer needed. You don't need to worry about the asset lifecycle of any objects that you reference this way.

### Weakly-referenced objects

Weakly-referenced objects are objects that Unity detects an [UntypedWeakReferenceId](xref:Unity.Entities.Serialization.UntypedWeakReferenceId) to during the baking process. To weakly-reference an object, you pass an `UntypedWeakReferenceId`, or a [WeakObjectReference](xref:Unity.Entities.Content.WeakObjectReference`1) which is a wrapper for an `UntypedWeakReferenceId`, to an [ECS component](concepts-components.md) from a [Baker](xref:Unity.Entities.Baker`1). For more information, refer to [Weakly-reference an object](content-management-get-a-weak-reference.md)

At runtime, you are responsible for the lifecycle of weakly-referenced objects. You must use the content management APIs to load them before you need to use them, and then release them when you don't need them anymore. Unity counts the number of references to each weakly-referenced object and when you release the last instance, Unity unloads the object.

## The content management workflow

In the simplest content management workflow, you:

1. Weakly reference an object and bake the reference into a component. For information on how to do this, refer to [Weakly reference an object](content-management-get-a-weak-reference.md).
2. Use the weak reference to load, use, and release the object at runtime. For more information, refer to [Load a weakly-referenced object at runtime](content-management-load-an-object.md) and [Load a weakly-referenced scene at runtime](content-management-load-a-scene.md)

Unity automatically adds any object that you weakly reference from a subscene to a content archive, so you don't need to do any additional work to make the object available to load at runtime. However, to support more advanced use cases, you can also create custom content archives and load them either from the local device or from a remote content delivery service. This can help you structure additional downloadable content, reduce the initial install size of the application, or load assets optimized for the end user's platform. For more information, refer to [Create custom content archives](content-management-create-content-archives.md).

## Additional resources

* [Weakly reference an object](content-management-get-a-weak-reference.md)
* [Load a weakly-referenced object at runtime](content-management-load-an-object.md)