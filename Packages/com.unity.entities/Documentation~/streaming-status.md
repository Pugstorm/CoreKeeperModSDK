## Streaming status

Streaming is asynchronous, so there's no guarantee of how long it takes for the data to load after it's been requested. If needed, you can use [`SceneSystem`](xref:Unity.Scenes.SceneSystem) to query the loading status of scenes and [sections](streaming-scene-sections.md).

It's best practice to make your systems react to the presence or absence of data they need, and not to whether certain scenes have loaded or not. If the data that a system requires to run is part of a particular scene, then the system should check whether that specific data has loaded, and not whether the scene has loaded.

This approach avoids tying systems to particular scenes. For example, if the data that a system needs is moved to a different scene, downloaded from the network, or procedurally generated, the system still works the same without changes to its code.

## Check loading status

Sometimes there are cases where you might need to check whether a scene or section has loaded. For example, if you want to implement a loading screen that remains visible until all the scheduled streaming is complete.

The static method [`SceneSystem.IsSceneLoaded`](xref:Unity.Scenes.SceneSystem.IsSceneLoaded*) checks if a scene is fully loaded (with all the content for all the sections). The static method [`SceneSystem.IsSectionLoaded`](xref:Unity.Scenes.SceneSystem.IsSectionLoaded*) checks if a specific section is loaded. The following example shows how to do this:

[!code-cs[sample](../DocCodeSamples.Tests/StreamingExamples.cs#sceneloading_isstuffloaded)]

>[!IMPORTANT]
> [`SceneSystem.IsSceneLoaded`](xref:Unity.Scenes.SceneSystem.IsSceneLoaded*) doesn't return `true` when loading with `DisableAutoLoad`. It only returns `true` if the content for all sections has loaded.

If you need more information about the loading status of a scene or a section, you can use [`SceneSystem.GetSceneStreamingState`](xref:Unity.Scenes.SceneSystem.GetSceneStreamingState*) and [`SceneSystem.GetSectionStreamingState`](xref:Unity.Scenes.SceneSystem.GetSectionStreamingState*) to get the following information about the loading status:

* Any errors during the loading process.
* The metadata entities' loaded status.
* If scene load is complete, even if all the sections weren't instructed to load.

[!code-cs[sample](../DocCodeSamples.Tests/StreamingExamples.cs#sceneloading_state)]

## Additional resources

* [Scene sections](streaming-scene-sections.md)
* [`SceneSystem.IsSceneLoaded` API documentation](xref:Unity.Scenes.SceneSystem.IsSceneLoaded*)