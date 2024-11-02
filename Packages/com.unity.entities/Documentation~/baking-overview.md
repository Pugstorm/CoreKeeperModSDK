# Baking overview

Baking is a process that converts GameObject data in the Unity Editor (authoring data) into entities written to Entity Scenes (runtime data). Baking is a non-reversible process that turns a performance-intensive but flexible set of GameObjects into a set of entities and components optimized for performance.

## Authoring and runtime data

In Unity, GameObject data is made up of both runtime and authoring data. This data model gives you a lot of flexibility, which is essential during editing, but not needed at runtime. Because Unity processes both the GameObject's runtime and authoring data at once, it means that runtime performance suffers.

However, Unity's entity component system (ECS) is designed so that it represents the data in your application in the most efficient way, to avoid processing any data that isn't required. Because of this design, it separates authoring and runtime data:

* **Authoring data** is any data that you create during the editing of your application, such a scripts, assets, or any other game-related data. This data type is flexible and readable: designed for humans to interact with.
* **Runtime data** is the data that ECS processes at runtime, such as the data it processes when you enter Play mode. This data type is optimized for performance and storage efficiency: designed for computers to process.

In the Editor, you can always check what kind of data type Unity is processing, through the data mode circles at the top of the Inspector and Hierarchy. For more information, refer to the documentation on [Working with authoring and runtime data](editor-authoring-runtime.md).

## Baking process

GameObjects called **authoring GameObjects** represent the authoring data in ECS. Any [components](https://docs.unity3d.com/Manual/Components.html) that are part of an authoring GameObject are called **authoring components** to distinguish them from [ECS components](concepts-components.md). 

Authoring GameObjects are contained in **authoring scenes**, which Unity uses to convert the GameObject data within that scene to ECS data. The process of converting the data from authoring GameObjects into ECS data is called **baking**. For more information about authoring scenes, refer to [Scenes overview](conversion-scene-overview.md) documentation.

Baking only happens in the Editor and never in-game, just like asset importing. Baking only happens in the Editor because it takes a lot of time and processing power to process both the GameObject representation of the data, together with its baking code every time it's needed. This process means that your application's performance would be reduced if Unity performed baking in-game.

Whenever the authoring data in an authoring scene changes, it triggers the baking process. How Unity then bakes your data depends on whether you have the authoring scene open as a [subscene](conversion-subscenes.md).

### Open subscene

If the corresponding authoring scene's subscene is open then it triggers **live baking**. Live baking is when Unity bakes authoring data into ECS data while you work on it. Depending on how much authoring data Unity needs to process, it either carries out the baking process [incrementally](#incremental-baking), or performs a [full bake](#full-baking) of the data:

* **Full baking:** Unity processes an entire scene and bakes it.
* **Incremental baking:** Unity only bakes the data that has been modified.

### Closed subscene

If the If the corresponding authoring scene's subscene is closed, then Unity performs asynchronous baking in the background. It performs a full bake of the data in the authoring scene.

### Full baking

When an entire scene is imported, Unity carries out a full baking process. Importing a whole scene happens in a background asset importer process when the entity scene is requested to load and the subscene referencing it is closed. 

The background asset importer is an Editor process that runs without a GUI. The Editor starts it on demand when a scene requires baking, and it runs asynchronously. The advantage of this approach is that the main Editor remains reactive and usable while baking is in progress. However, it means that when you initially load an entity scene, it might take a few seconds to appear in the Editor. Subsequent loads of the main scene reuse the already baked entity scenes and are much faster.

Full baking only happens when the ECS data needs to load, and not the GameObject authoring data. 

This happens in the following situations:

* The entity scene is missing (not present on disk).
* The authoring scene has been modified and the entity scene is outdated.
* The baking code is in an assembly that doesn’t contain a single `[BakingVersion]` attribute. This means that the assembly has been modified and the entity scene is outdated.
* A `[BakingVersion]` attribute on baking code has been modified.
* The [Entities Project Settings](editor-project-settings.md) have been modified.
* You request a reimport from the subscene Inspector. This feature is intended for troubleshooting.
* You clear the baking cache in the [Editor Preferences](editor-preferences.md).

The output of a full baking pass is a set of files on the disk. The Editor or your application subsequently loads these files. As such, baking happens only when needed.

### Incremental baking

When a subscene loads an authoring scene, it also initializes incremental baking. Performing an incremental baking pass on the scene means that you can directly access the results of baking while you edit an authoring scene.

During incremental baking, baking happens in memory instead of doing a round-trip to the disk. When you change the contents of authoring GameObjects, Unity re-bakes only the entities and components affected. Baking only a small subset of the data is much faster, and means ECS data can update in real time. This effectively gives the impression of directly editing ECS data, even though baking is continuously happening.

Incremental baking comes with some additional complexity. While full baking always starts from a blank slate and systematically bakes everything, incremental baking always runs on top of the earlier baking pass and only bakes the entities that depend on the authoring GameObjects that have changed.

This means that there might be discrepancies between baking a full scene, and incrementally baking it when changes are made to it. When writing baking code, you must make sure that the results remain consistent.

> [!NOTE]
> There is variance in the output between incremental baking and full baking. The ordering of entities is different, as is the size of the entities. Because of this variance, the chunk layout isn't the same. You must make sure that this doesn't impact the user experience of your application.

## Additional resources

* [Baker overview](baking-baker-overview.md)
* [Scenes overview](conversion-scene-overview.md)