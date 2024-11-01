# Create the system for the spawner example

This task shows you how to create a [Burst](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html)-compatible system that queries for a component, reads and writes component values, and instantiates entities at runtime.

## ECS workflow overview

This task is the fourth task in a series of five tasks that show you how to create and optimize behavior in an ECS system. At the end of the tasks, you will have a spawner system that reads and writes component data, and instantiates entities. This workflow contains the following tasks:

1. [Create the subscene for the spawner example](ecs-workflow-scene.md)
2. [Create a component for the spawner example](ecs-workflow-create-components.md)
3. [Create the spawner entity for the spawner example](ecs-workflow-create-entities.md)
4. [Create the system for the spawner example](ecs-workflow-create-systems.md)
5. [Optimize the system for the spawner example](ecs-workflow-optimize-systems.md)

Each task is a prerequisite for the subsequent tasks.

## Create the spawner system

1. Create a new C# script called `SpawnerSystem` and replace the contents of the file with the below code example. When you enter Play mode, Unity creates a [world](concepts-worlds.md) instance and adds every system to this default world. For more information, see [Initialization](concepts-worlds.md#initialization).
2. Enter Play mode. You should see that the system instantiates the Prefab you assigned at the rate that you set. If you open the [Entities Hierarchy window](editor-hierarchy-window.md), you can see the entities appear as the system instantiates them. **Note**: If you can't see the entities in the [Scene view](xref:UsingTheSceneView), make sure to install and setup [Entities Graphics](https://docs.unity3d.com/Packages/com.unity.entities.graphics@latest/index.html) and either the [Universal Render Pipeline](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html) or the [High Definition Render Pipeline](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@latest/index.html).

[!code-cs[The spawner system](../DocCodeSamples.Tests/SpawnerSystemExample.cs#example)]

## Next steps

To continue to create and optimize the spawner system, follow the next task in this workflow:

- [Optimize the system for the spawner example](ecs-workflow-optimize-systems.md)

## Additional resources

- [Understand the ECS workflow](ecs-workflow-intro.md)
- [System concepts](concepts-systems.md)
- [Implementing systems](systems-intro.md)
