# Baking worlds overview

Unity bakes each [entity scene](conversion-scene-overview.md) in isolation. Background importers only process one scene at a time, but when the Unity Editor live bakes open [subscenes](conversion-subscenes.md) it relies on separate [worlds](concepts-worlds.md) to isolate one from the other. In particular, each live baked subscene uses two worlds:

* **Conversion world**: A world where baking happens. [Bakers](baking-baker-overview.md) and [baking systems](baking-baking-systems-overview.md) run in this world.
* **Shadow world**: A world that contains a copy of the previous baking output. Unity uses a shadow world to compare what's changed since the last baking pass.

Unity creates entities for each authoring GameObject in a scene in the conversion world, and runs the bakers and baking systems in that same world. Because [live baking](baking-overview.md) tries to limit the amount of work it does, the results of baking a scene remain in the conversion world for as long as you keep the subscene open.

At the end of baking, Unity has to copy any data that has changed since the last baking pass to the main world. For example, if the transform of the authoring GameObject for a treasure chest has changed, Unity only needs to copy the ECS components that the transform affected to the main world. If the project was in Play mode, and the player already emptied the chest, moving the chest shouldn’t reset its contents.

This is the purpose of the shadow world. It contains a copy of the earlier baking output. At the end of a new baking pass, Unity checks the contents of the conversion world against the contents of the shadow world. It copies the entities and components that are different to the main world, and updates the shadow world to match the current conversion world.


## Additional resources

* [Bakers overview](baking-baker-overview.md)
* [Baking systems overview](baking-baking-systems-overview.md)