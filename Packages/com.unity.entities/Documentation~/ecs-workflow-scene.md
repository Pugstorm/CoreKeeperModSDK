# Create the subscene for the spawner example

The first step in the entity component system (ECS) workflow is to create a [subscene](conversion-subscenes.md) to contain the content for your application. This task shows you how to create a new subscene which you'll use in subsequent tasks in this workflow to instantiate entities within.

## ECS workflow overview

This task is the first task in a series of five tasks that show you how to create and optimize behavior in an ECS system. At the end of the tasks, you will have a spawner system that reads and writes component data, and instantiates entities. This workflow contains the following tasks:

1. [Create the subscene for the spawner example](ecs-workflow-scene.md)
2. [Create a component for the spawner example](ecs-workflow-create-components.md)
3. [Create the spawner entity for the spawner example](ecs-workflow-create-entities.md)
4. [Create the system for the spawner example](ecs-workflow-create-systems.md)
5. [Optimize the system for the spawner example](ecs-workflow-optimize-systems.md)

Each task is a prerequisite for the subsequent tasks.

## Create a subscene for the spawner

You create subscenes in the Unity Editor.

1. In the Editor, open a [scene](xref:CreatingScenes) that has been saved as a file.
2. In the Hierarchy, right-click and select **New Sub Scene** > **Empty Scene**.
3. In the prompt that appears, save the new subscene. Unity adds the subscene to the open scene and you can now use it.

## Next steps

To continue to create and optimize the spawner system, follow the next task in this workflow:

- [Create a component for the spawner example](ecs-workflow-create-components.md)

## Additional resources

- [Understand the ECS workflow](ecs-workflow-intro.md)
- [Subscene overview](conversion-subscenes.md)
