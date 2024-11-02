# Create a component for the spawner example

This task shows you how to create an entity component system (ECS) component to store data for the spawner example. Subsequent tasks in this workflow use the data in this component to determine which entity the spawner should instantiate, how often to instantiate the entity, and where to instantiate the entity at.

Before you create a component, you should think about the kind of data that the component will store and in what context you will use it. You can then decide which [component type](components-type.md) to use to implement the component. The component for the spawner example will store:

* A Prefab to instantiate.
* A position to instantiate the Prefab at.
* The rate at which to instantiate the Prefab.
* The last time that the spawner instantiated the Prefab.

The most appropriate component type to store this kind of data is an [unmanaged component](components-unmanaged.md).

## ECS workflow overview

This task is the second task in a series of five tasks that show you how to create and optimize behavior in an ECS system. At the end of the tasks, you will have a spawner system that reads and writes component data, and instantiates entities. This workflow contains the following tasks: 

1. [Create the subscene for the spawner example](ecs-workflow-scene.md)
2. [Create a component for the spawner example](ecs-workflow-create-components.md)
3. [Create the spawner entity for the spawner example](ecs-workflow-create-entities.md)
4. [Create the system for the spawner example](ecs-workflow-create-systems.md)
5. [Optimize the system for the spawner example](ecs-workflow-optimize-systems.md)

Each task is a prerequisite for the subsequent tasks.

## Create the component

1. Create a new C# script called `Spawner`.
2. Replace the contents of the file with the following code example.

[!code-cs[The spawner component](../DocCodeSamples.Tests/SpawnerComponentExample.cs#example)]

## Next steps

To continue to create and optimize the spawner system, follow the next task in this workflow:

* [Create the spawner entity for the spawner example](ecs-workflow-create-entities.md)

## Additional resources

* [Understand the ECS workflow](ecs-workflow-intro.md)
* [Component concepts](concepts-components.md)
* [Working with components](components-intro.md)