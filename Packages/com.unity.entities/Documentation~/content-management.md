# Content management

Unity's entity component system (ECS) includes its own content management and delivery system. This system provides a performant interface to load and release Unity objects and scenes in a data-oriented application. The API is available to both ECS [systems](concepts-systems.md) and MonoBehaviour code which means you can use it in [bakers](baking-baker-overview.md).

> [!NOTE]
> This system is built on top of Unity's [ContentLoadModule](https://docs.unity3d.com/2023.1/Documentation/ScriptReference/UnityEngine.ContentLoadModule.html) assembly.

| **Topic**                                                    | **Description**                                              |
| ------------------------------------------------------------ | ------------------------------------------------------------ |
| [Introduction to content management](content-management-intro.md) | Understand how content management works for entities-based applications. |
| [Weakly reference an object](content-management-get-a-weak-reference.md) | Get a weak reference to an object so you can load and use the object at runtime. |
| [Weakly reference a scene](content-management-get-a-weak-reference-scene.md) | Get a weak reference to a scene so you can load and use the scene at runtime. |
| [Load a weakly-referenced object at runtime](content-management-load-an-object.md) | Use a [system](concepts-systems.md) to load an object from a content archive. |
| [Load a weakly-referenced scene at runtime](content-management-load-a-scene.md) | Use a system to load a scene, and everything it contains, from a content archive. |
| [Create custom content archives](content-management-create-content-archives.md) | Create your own content archives to store objects ready for delivery to your application. |
| [Deliver content to an application](content-management-delivery.md) | Load content archives into your application at runtime.      |

## Additional resources

* [ContentLoadModule](https://docs.unity3d.com/2023.1/Documentation/ScriptReference/UnityEngine.ContentLoadModule.html)