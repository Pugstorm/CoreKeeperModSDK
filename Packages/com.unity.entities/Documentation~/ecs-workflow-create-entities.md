# Create the spawner entity for the spawner example

This task demonstrates how the [baking](baking-overview.md) process creates a primary entity for each authoring GameObject. It then shows you how to create an authoring GameObject and use a baker to attach an entity component system (ECS) [component](concepts-components.md) to the resulting primary entity. This creates an instance of the ECS component that systems can query, transform, and write to.

To create an entity and attach ECS components to it, you need to create the following:

- An authoring component which is a MonoBehaviour component that holds values that you can pass from the Editor to the ECS component.
- A baker to attach the ECS component to the entity, and populate the ECS component with values from the authoring component.

## ECS workflow overview

This task is the third task in a series of five tasks that show you how to create and optimize behavior in an ECS system. At the end of the tasks, you will have a spawner system that reads and writes component data, and instantiates entities. This workflow contains the following tasks:

1. [Create the subscene for the spawner example](ecs-workflow-scene.md)
2. [Create a component for the spawner example](ecs-workflow-create-components.md)
3. [Create the spawner entity for the spawner example](ecs-workflow-create-entities.md)
4. [Create the system for the spawner example](ecs-workflow-create-systems.md)
5. [Optimize the system for the spawner example](ecs-workflow-optimize-systems.md)

Each task is a prerequisite for the subsequent tasks.

## Create the spawner entity

1. Create a new C# script called `SpawnerAuthoring` and replace the contents of the file with the below code example. This code example contains both the authoring component and the baker.
2. Create an empty GameObject called **Spawner** in your [subscene](conversion-subscenes.md) and attach the SpawnerAuthoring component to it.
3. Create or source a [Prefab](xref:Prefabs) to spawn.
4. Select the **Spawner** GameObject and, in the Inspector, assign the Prefab to the **Prefab** property and set **Spawn Rate** to **2**.
5. Open the [Entities Hierarchy window](editor-hierarchy-window.md) and set the [data mode](editor-hierarchy-window.md#data-modes) to either runtime or mixed. These data modes both display the entities that the baking system generates from the authoring GameObjects.
6. In the Entities Hierarchy window, select the Spawner entity. The Entities Hierarchy window displays both GameObjects and entities. To distinguish between the two, entities are indicated by a hexagon icon (![](images/entity-icon.png)).
7. In the [Inspector](editor-entity-inspector.md) for the Spawner entity, open the Entity Baking Preview. This displays the attached Spawner component and the component values that the baker set.

[!code-cs[The spawner Baker](../DocCodeSamples.Tests/SpawnerBakerExample.cs#example)]

## Next steps

To continue to create and optimize the spawner system, follow the next task in this workflow:

- [Create the system for the spawner example](ecs-workflow-create-systems.md)

## Additional resources

- [Understand the ECS workflow](ecs-workflow-intro.md)
- [Entity concepts](concepts-entities.md)
- [Baking overview](baking-overview.md)
