# Load a scene

To load a scene, you can either use [subscenes](conversion-subscenes.md), or the [`SceneSystem`](xref:Unity.Scenes.SceneSystem) API. 

If you use a subscene, when the [`AutoLoadScene`](xref:Unity.Scenes.SubScene.AutoLoadScene) field is set to `true`, Unity streams the referenced scene in when the `SubScene` component is enabled.

To directly control streaming without a [`SubScene`](xref:Unity.Scenes.SubScene) component, use the [`SceneSystem`](xref:Unity.Scenes.SceneSystem) high level API. The static method to load a scene asynchronously is [`SceneSystem.LoadSceneAsync`](xref:Unity.Scenes.SceneSystem.LoadSceneAsync*). By default, a call to [`SceneSystem.LoadSceneAsync`](xref:Unity.Scenes.SceneSystem.LoadSceneAsync*) loads the meta entities and all the contents for the sections. Calls to this method should happen inside the `OnUpdate` method of a system.

All versions of this method need to receive a parameter that can uniquely identify the scene that you want to load. You can use the following to identify a scene:

* An [`EntitySceneReference`](xref:Unity.Entities.Serialization.EntitySceneReference).
* A [`Hash128`](xref:Unity.Entities.Hash128) GUID.
* A scene meta [`Entity`](xref:Unity.Entities.Entity).

>[!NOTE]
>The build process only detects authoring scenes that are referenced by [`EntitySceneReference`](xref:Unity.Entities.Serialization.EntitySceneReference) and [`SubScene`](xref:Unity.Scenes.SubScene) MonoBehaviours. The build process doesn't detect scenes referenced by GUIDs, and their entity scene files will be missing from builds. For this reason, you should avoid using GUIDs to identify scenes.

In Play mode, all scenes are always available independently from the method used to reference the scenes. If the corresponding entity scene file is missing or outdated, baking is triggered during loading.

[`SceneSystem.LoadSceneAsync`](xref:Unity.Scenes.SceneSystem.LoadSceneAsync*) returns the scene meta [`Entity`](xref:Unity.Entities.Entity) for the loading scene when a GUID or an [`EntitySceneReference`](xref:Unity.Entities.Serialization.EntitySceneReference) is used as a parameter. You can use this scene meta [`Entity`](xref:Unity.Entities.Entity) for subsequent calls to refer to the loading scene, for example, to unload the content of a scene and to reload it after. 

## Use EntitySceneReference to load a scene

Using [`EntitySceneReference`](xref:Unity.Entities.Serialization.EntitySceneReference) is the recommended way to keep a reference to scenes during baking and to load them at runtime. The following example shows how to store a reference to a scene during baking:

[!code-cs[sample](../DocCodeSamples.Tests/StreamingExamples.cs#sceneloader_component)]

This example shows how to use the stored reference in a system to load the scene:

[!code-cs[sample](../DocCodeSamples.Tests/StreamingExamples.cs#sceneloadersystem)]

During the call to [`SceneSystem.LoadSceneAsync`](xref:Unity.Scenes.SceneSystem.LoadSceneAsync*), only the scene entity is created. Unity uses this entity to control the rest of the loading process internally. 

The scene header, the section entities, and their content aren't loaded during this call and they are ready a few frames later.

[`SceneSystem.LoadSceneAsync`](xref:Unity.Scenes.SceneSystem.LoadSceneAsync*) can perform [structural changes](concepts-structural-changes.md). These structural changes prevent us from calling this function inside a foreach with an query. 

## Load parameters

The [`LoadParameters`](xref:Unity.Scenes.SceneSystem.LoadParameters) struct is optional, and by default [`SceneSystem.LoadSceneAsync`](xref:Unity.Scenes.SceneSystem.LoadSceneAsync*) loads the meta entities and all the content for the sections.

The [`SceneLoadFlags`](xref:Unity.Entities.SceneLoadFlags) enum controls the loading, and has the following fields:

* [`DisableAutoLoad`](xref:Unity.Entities.SceneLoadFlags): Unity creates the scene and section meta entities, but doesn't load the content for the sections. When the scene loading is finished, you can access the [`ResolvedSectionEntity`](xref:Unity.Scenes.ResolvedSectionEntity) buffer to [load the content for individual sections](streaming-scene-sections.md#section-loading). The [`AutoLoad`](xref:Unity.Scenes.SceneSystem.LoadParameters.AutoLoad) property in [`LoadParameters`](xref:Unity.Scenes.SceneSystem.LoadParameters) is a helper to set up `DisableAutoLoad`.
* [`BlockOnStreamIn`](xref:Unity.Entities.SceneLoadFlags): Unity performs the loading of the scene synchronously. The call to [`SceneSystem.LoadSceneAsync`](xref:Unity.Scenes.SceneSystem.LoadSceneAsync*) only returns when the scene is fully loaded.
* [`NewInstance`](xref:Unity.Entities.SceneLoadFlags): Creates a new copy of the scene in the world. This flag is used for [scene instancing](streaming-scene-instancing.md#scene-instancing).

## Unload scenes

To unload a scene, use [`SceneSystem.UnloadScene`](xref:Unity.Scenes.SceneSystem.UnloadScene*):

[!code-cs[sample](../DocCodeSamples.Tests/StreamingExamples.cs#sceneloading_unloadscene)]

You can call [`SceneSystem.UnloadScene`](xref:Unity.Scenes.SceneSystem.UnloadScene*) with a scene's GUID or an [`EntitySceneReference`](xref:Unity.Entities.Serialization.EntitySceneReference) instead of the scene meta entity, but this has the following disadvantages:

* The method has to search for the meta entity that represents the scene that matches the GUID, which might affect performance.
* If multiple instances of the same scene are loaded, then unloading by GUID only unloads one instance.

By default, [`SceneSystem.UnloadScene`](xref:Unity.Scenes.SceneSystem.UnloadScene*) only unloads the content for the sections, but it keeps the meta entities for the scene and the sections. This is useful if the scene is going to be loaded again later, because having these meta entities ready speeds up the loading of the scene.

To unload the content and delete the meta entities, call [`SceneSystem.UnloadScene`](xref:Unity.Scenes.SceneSystem.UnloadScene*) with [`UnloadParameters.DestroyMetaEntities`](xref:Unity.Scenes.SceneSystem.UnloadParameters).    

[`SceneSystem.UnloadScene`](xref:Unity.Scenes.SceneSystem.UnloadScene*) can perform [structural changes](concepts-structural-changes.md).
