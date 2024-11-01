# Scenes overview

In the entity component system (ECS), scenes work differently. This is because Unity's core [scene system](xref:CreatingScenes) is incompatible with ECS. There are the following types of scene concept to understand:

* **Authoring scenes:** An authoring scene is a scene that you can open and edit like any other scene, but is designed for [baking](baking-overview.md) to process. It contains GameObjects and MonoBehaviour components that Unity converts to ECS data at runtime.
* **Entity scenes:** An entity scene contains the ECS data that the baking process produces.
* **Subscenes:** A [subscene](conversion-subscenes.md) is a reference to an authoring or entity scene. In the Unity Editor, you create a subscene to add authoring elements to. When the subscene is closed, it triggers the baking process for related entity scenes.

Because some projects have large amounts of data in them, it can be difficult for the Unity Editor to process all the data if it's contained within one authoring scene. ECS can handle processing millions of entities efficiently, but in their GameObject representation they might cause the Editor to stall. Therefore, it's more efficient to place authoring data into several smaller authoring scenes. 

For example, in the [MegaCity example project](https://github.com/Unity-Technologies/Megacity-Sample), each building is in a separate subscene to efficiently manage the buildings' GameObject representations. However, you can load the whole city in the Editor as ECS data to view the project in context.

Not all projects need large environments, so in some situations a single authoring scene is enough. But in general, multiple authoring scenes can be baked independently and loaded together.

This is the purpose of a subscene. It's a GameObject component that allows loading a scene as either its GameObject authoring representation (to work on it) or as its ECS representation (read only, but performant).

Subscenes and entity scenes are often confused with each other. But a subscene is nothing more than an attachment point, to conveniently load entity scenes.

## Additional resources

* [Subscenes overview](conversion-subscenes.md)
* [Scene streaming](streaming-scenes.md)