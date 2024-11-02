# Scene instancing

To create multiple instances of the same scene in a [world](concepts-worlds.md), use [`SceneSystem.LoadSceneAsync`](xref:Unity.Scenes.SceneSystem.LoadSceneAsync*) with the flag [`SceneLoadFlags.NewInstance`](xref:Unity.Entities.SceneLoadFlags). This is useful if for example you have a different tiles (each tile represented by a scene) and you want to populate the world with those tiles.

When you create a scene in this way, the scene meta entity returned from the load call will refer to the newly created instance.  

The instances from a scene are exact copies of each other, because the streaming system loads the exact same data multiple times from the entity scene file. To make sure that each instance isn't exactly the same, you can apply a unique transform on each instance by combining the [`ProcessAfterLoadGroup`](xref:Unity.Scenes.ProcessAfterLoadGroup) system group and [`PostLoadCommandBuffer`](xref:Unity.Entities.PostLoadCommandBuffer). You can apply any other kind of changes to the entities in the scene, not just a transform.

>[!NOTE]
> Any [custom section metadata](streaming-meta-entities.md#custom-section-metadata) is exactly the same on each instance because the meta data is stored in the entity scene file.

## ProcessAfterLoadGroup system group

The loading of a section doesn't happen in the main world, but on a separate world called the **streaming world**. Each [section](streaming-scene-sections.md) loads into its own streaming world. When the load is complete, the content of the streaming world is moved into the main world.

The system group [`ProcessAfterLoadGroup`](xref:Unity.Scenes.ProcessAfterLoadGroup) runs in the streaming world when all the content is loaded, but before the final move into the main world is performed. You can add custom systems into that group to apply transformations to scene instances.

For example, you could create a system to offset all the entities in the instance to a certain position of the world. In this case you need to pass to the system the offset that you want to apply to the instance. This offset can't be stored inside the entity scene file because it needs to be different for each instance. You can use [`PostLoadCommandBuffer`](xref:Unity.Entities.PostLoadCommandBuffer) to provide this data during loading. 

## PostLoadCommandBuffer

[`PostLoadCommandBuffer`](xref:Unity.Entities.PostLoadCommandBuffer) is a managed component that contains an [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer). During the section loading, the streaming system checks if there is a [`PostLoadCommandBuffer`](xref:Unity.Entities.PostLoadCommandBuffer) present in the section meta entity. If the component is found, the streaming system executes its [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer) just before running the system group [`ProcessAfterLoadGroup`](xref:Unity.Scenes.ProcessAfterLoadGroup).

If you add a [`PostLoadCommandBuffer`](xref:Unity.Entities.PostLoadCommandBuffer) to the scene meta entity instead, all the sections in the scene apply the [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer) when they're loaded.

The [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer) inside this component is just a regular entity command buffer and it can be used for any kind of command that we want to execute on the streaming world. For example, if you want to supply per instance information to systems, you can create a new entity for this:

1. Use the [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer) inside a [`PostLoadCommandBuffer`](xref:Unity.Entities.PostLoadCommandBuffer) to create a new entity
1. Add components with the instance information to that entity. 
 
This means that you can add a component to a new entity with the offset that you want to apply to the instance.

### Scene instancing overview

As a summary, these are the steps to instantiate scenes and apply unique transformations to them:

1. Use [`SceneSystem.LoadSceneAsync`](xref:Unity.Scenes.SceneSystem.LoadSceneAsync*) with the flag [`SceneLoadFlags.NewInstance`](xref:Unity.Entities.SceneLoadFlags) to load a scene. 
1. Store the returned scene meta entity to use it in the next steps.
1. Add a [`PostLoadCommandBuffer`](xref:Unity.Entities.PostLoadCommandBuffer) to the scene meta entity:
   1. Create an [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer).
   1. Create a new entity using that [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer). Add components to it with unique instance information.
   1. Create a [`PostLoadCommandBuffer`](xref:Unity.Entities.PostLoadCommandBuffer) and store the [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer) inside.
   1. Add the [`PostLoadCommandBuffer`](xref:Unity.Entities.PostLoadCommandBuffer) component to the scene meta entity. Don't dispose the [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer). 
1. Write a system to apply a unique transformation to the instanced scene. 
   1. Create the system and assign it to the [`ProcessAfterLoadGroup`](xref:Unity.Scenes.ProcessAfterLoadGroup). 
   1. Query the instance information from the entity created in the [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer).
   1. Use that information to apply the transforms to the entities in the instance.

For example, to instantiate a scene on a certain position in the world you can do the following:

[!code-cs[sample](../DocCodeSamples.Tests/StreamingExamples.cs#sceneloading_instancing1_2)]

The code before uses a component called `PostLoadOffset` to store the offset to apply to the instance.

[!code-cs[sample](../DocCodeSamples.Tests/StreamingExamples.cs#sceneloading_instance_data)]

Finally, use this system to apply the transformation:

[!code-cs[sample](../DocCodeSamples.Tests/StreamingExamples.cs#sceneloading_instancing3)]

## Addional resources

* [Custom section metadata](streaming-meta-entities.md#custom-section-metadata)
* [Scene sections](streaming-scene-sections.md)
