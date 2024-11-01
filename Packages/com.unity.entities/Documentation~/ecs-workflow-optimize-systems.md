# Optimize the system for the spawner example

This task shows you how to modify a system so that it uses [Burst](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html)-compatible [jobs](xref:JobSystem) that run in parallel on multiple threads.

> [!NOTE]
> Before you modify a system to run in parallel on multiple threads, consider whether your system affects data on enough entities to make the benefits of multi-threading exceed the overhead of scheduling the jobs. For more information, refer to [Optimize systems](ecs-workflow-intro.md#optimize-systems).

This task recreates `SpawnerSystem` using [IJobEntity](xref:Unity.Entities.IJobEntity) and schedules the job to run in parallel across multiple threads. Using an `IJobEntity` changes how you query and iterate over component data, and changes how you instantiate new entities. For information on component data query and iteration changes due to `IJobEntity`, refer to [Specify a query](iterating-data-ijobentity.md#specify-a-query). 

Unity can only create entities on the main thread which means parallel jobs must use an [entity command buffer](systems-entity-command-buffers.md) to record commands to create and configure new entities. After the parallel job runs, Unity plays back the entity command buffer on the main thread to actually create and configure the entities. For more information, refer to [Use EntityCommandBuffer in a parallel job](systems-entity-command-buffer-use.md#parallel-jobs) and [Deterministic playback](systems-entity-command-buffer-playback.md#deterministic-playback-in-parallel-jobs).

## ECS workflow overview

This task is the fifth task in a series of five tasks that show you how to create and optimize behavior in an ECS system. At the end of the tasks, you will have a spawner system that reads and writes component data, and instantiates entities. This workflow contains the following tasks:

1. [Create the subscene for the spawner example](ecs-workflow-scene.md)
2. [Create a component for the spawner example](ecs-workflow-create-components.md)
3. [Create the spawner entity for the spawner example](ecs-workflow-create-entities.md)
4. [Create the system for the spawner example](ecs-workflow-create-systems.md)
5. [Optimize the system for the spawner example](ecs-workflow-optimize-systems.md)

Each task is a prerequisite for the subsequent tasks.

## Optimize the spawner system

1. Open `SpawnerSystem`.
2. Replace the contents of the file with the below code example.
3. Enter Play mode. You should see that the system behaves as it did previously. However, if you open the [Profiler](xref:Profiler) window, you should see that the work runs on multiple threads. **Note**: To see the result of multi-threading more clearly, duplicate the Spawner in the [subscene](conversion-subscenes.md) so that there are multiple spawner components for the system to process.

[!code-cs[The optimized spawner system](../DocCodeSamples.Tests/SpawnerSystemOptimizedExample.cs#example)]

## Additional resources

- [Understand the ECS workflow](ecs-workflow-intro.md)
- [C# Job System](xref:JobSystem)
- [Iterate over component data with IJobEntity](iterating-data-ijobentity.md)
- [Iterate over component data with IJobChunk](iterating-data-ijobchunk.md)
