# Spawner example

Implement an entity component system (ECS) example that creates a spawner which reads and writes component data, and instantiates entities at runtime.

The topics in this section of the documentation are workflow steps and you should read them in order.

| **Topic**                                                    | **Description**                                              |
| ------------------------------------------------------------ | ------------------------------------------------------------ |
| [Create the subscene for the spawner example](ecs-workflow-scene.md) | Create a [subscene](conversion-subscenes.md) to contain the entities for the spawner example. |
| [Create a component for the spawner example](ecs-workflow-create-components.md) | Create the component to store data for the spawner example.  |
| [Create the spawner entity for the spawner example](ecs-workflow-create-entities.md) | Convert GameObjects into entities and attach components to the entities. |
| [Create the system for the spawner example](ecs-workflow-create-systems.md) | Create a system to provide logic for the spawner example.    |
| [Optimize the system for the spawner example](ecs-workflow-optimize-systems.md) | Convert the synchronous single-threaded spawner system code into a [Burst](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html)-compatible [job](xref:JobSystem). |

## Additional resources

* [Understand the ECS workflow](ecs-workflow-intro.md)