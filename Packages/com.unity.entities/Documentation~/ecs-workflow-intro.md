# Understand the ECS workflow

The workflow to create applications with Unity's entity component system (ECS) framework differs from the one you would use to create object-oriented Unity applications in both principle and implementation. It's useful to understand the ECS workflow before you begin to create a project with this framework.

## Create a subscene

ECS uses [subscenes](conversion-subscenes.md) to contain the content of your application. You add GameObjects and MonoBehaviour components to a subscene, and [bakers](baking-baker-overview.md) convert the GameObjects and MonoBehaviour components into entities and ECS components.

## Create ECS components

[Components](concepts-components.md) store data for your application. To create behavior in your application, [systems](concepts-systems.md) provide logic that reads from and writes to ECS component data. The ECS workflow is data-oriented, so it's good practice to plan out your data layout and create ECS components before you work on systems or create any entities.

There are different kinds of ECS components that serve different purposes. For more information, refer to [Component types](components-type.md). 

## Create entities

Entities represent distinct things that exist in an application. To create entities in the Editor, you add GameObjects to a subscene. The [baking](baking-overview.md) process converts these GameObjects into entities. Optionally, to attach ECS components to the converted entities, you create bakers. When you create a baker, you define which MonoBehaviour component it's for and then write code that uses the MonoBehaviour component data to create and attach ECS components to the converted entity. You can also create additional entities from the baker and attach ECS components to them too. In this workflow, the MonoBehaviour component is called an authoring component.

>[!TIP]
>It's a good organizational practice to append `Authoring` to the class name of any authoring components you create.

You can also create entities at runtime. The spawner code examples in the [ECS workflow](ecs-workflow-tutorial.md) section of the documentation show how to set up a spawner system that instantiates entities at runtime.

## Create systems

[Systems](concepts-systems.md) create the behavior in your application. To do this they can query and transform ECS [component](concepts-components.md) data, create and destroy entities, and add and remove ECS components from entities. By default, when you create a system, Unity instantiates it and adds it to the default [world](concepts-worlds.md).

There are different types of systems that serve different purposes. For more information, refer to [System types](concepts-systems.md#system-types).

## Optimize systems

By default, any code that you write in a [system](concepts-systems.md) runs synchronously on the main thread. If the system affects data on many entities and would benefit from multi-threading, it's best practice to create [Burst](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html)-compatible [jobs](xref:JobSystem), and schedule them to run in parallel when possible. Burst compiles your C# code into optimized native CPU code and jobs enable you to distribute work across multiple threads and take advantage of multiple processors.

If a system doesn't do much work, for example if it only processes the component data for a low number of entities, the overhead of scheduling jobs in parallel can exceed the performance gains from multi-threading. To find out if this is the case for one of your jobs, use [the CPU profiler](xref:Profiler) to measure how long Unity takes run your job code with and without multi-threading. If the scheduling overhead makes Unity take longer to run your job code using multi-threading, try the following to optimize the job:

* Run the job on the main thread. For more information, refer to [Run](xref:Unity.Entities.IJobEntityExtensions.Run*).
* If the system is an unmanaged [ISystem](systems-isystem.md), replace the job with a [SystemAPI.Query](xref:Unity.Entities.SystemAPI.Query*) and normal `foreach`. You can then apply the [BurstCompile](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html?subfolder=/manual/compilation-burstcompile.html) attribute to the function that contains the `SystemAPI.Query` to Burst compile the query and your code.

## Additional resources

* [Spawner example](ecs-workflow-example.md)