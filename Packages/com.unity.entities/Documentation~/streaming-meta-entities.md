# Scene and section meta entities

[Baking](baking-overview.md) an authoring scene produces an entity scene file. The header of each entity scene file contains:

* A list of [sections](streaming-scene-sections.md), with data such as file names, file sizes, and bounding volumes.
* A list of AssetBundle dependencies (GUIDs).
* Optional [custom metadata](#custom-section-metadata).

The list of sections and bundles determines the list of files that Unity needs to load when loading the scene. You can optionally use the custom metadata for game-specific purposes. For example, you can store PVS (Potentially Visible Set) information as custom metadata to decide when to stream the scene in, or you could store certain conditions to decide when to load the scene.

Loading an entity scene happens in two steps:

1. The resolve stage loads the header and creates one meta entity per scene and per section. 
1. Unity loads the contents of the sections.

Unity uses these scene and section meta entities to control streaming. Once the scene is loaded, you can then query the [`ResolvedSectionEntity`](xref:Unity.Scenes.ResolvedSectionEntity) buffer on the scene meta entity to access the individual section meta entities.

## Custom section metadata

The section meta entity is available even when the content of the section hasn't loaded, so it can be useful to store custom metadata in this section. For example, you could store the dimensions of a bounding box that the player must enter for that section to be loaded.

To store custom metadata, add a regular ECS component to the section meta entity during [baking](baking-overview.md). You can only do this in a baking system and not in a baker. To access the section meta entity during baking you need to use the [`SerializeUtility.GetSceneSectionEntity`](xref:Unity.Entities.Serialization.SerializeUtility.GetSceneSectionEntity*) method as shown in the following example:

[!code-cs[sample](../DocCodeSamples.Tests/StreamingExamples.cs#section_metadata)]

In the previous example the variables `section`, `radius` and `position` are local variables, but in a real world case scenario this information will come from an authoring component through a baker. 

As shown in the example, [`SerializeUtility.GetSceneSectionEntity`](xref:Unity.Entities.Serialization.SerializeUtility.GetSceneSectionEntity*) has an entity query as a parameter. The method creates the query internally if it isn't provided, but it's more efficient to create the query outside and pass it to the method.

## Additional resources

* [`ResolvedSectionEntity` API documentation](xref:Unity.Scenes.ResolvedSectionEntity)
* [Scene sections](streaming-scene-sections.md)
