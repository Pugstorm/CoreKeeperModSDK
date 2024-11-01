---
uid: iterating-data-entities-foreach
---

# Iterate over data with Entities.ForEach

If you use the [`SystemBase`](xref:Unity.Entities.SystemBase) class to create your systems, you can use the [`Entities.ForEach`](xref:Unity.Entities.SystemBase.Entities) construction to define and execute algorithms over entities and their components. At compile time, Unity translates each `ForEach` call into a generated job.

You pass `Entities.ForEach` a lambda expression, and Unity generates an [entity query](systems-entityquery.md) based on the lambda parameter types. When the generated job runs, Unity calls the lambda expression once for each entity that matches the query. `ForEachLambdaJobDescription` represents this generated job.

If you use [`ISystem`](systems-isystem.md) to create your systems, use [`SystemAPI.Query`](systems-systemapi-query.md) to iterate over system data. `Entities.ForEach` has four times slower compilation time than `SystemAPI.Query` and [`IJobEntity`](iterating-data-ijobentity.md), so you should consider using those methods to iterate over data instead. 

## Supported features

Use `Run()` to execute the lambda expression on the main thread. You can also use `Schedule` to execute it as a single job, or `ScheduleParallel` to execute it as a parallel job. These different execution methods have different constraints on how you access data. Also, the [Burst compiler](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html) uses a restricted subset of the C# language, so you need to specify `WithoutBurst` if you want to use C# features outside this subset. This includes accessing managed types. 

The following table shows which features are supported in `Entities.ForEach` for the different methods of scheduling available in `SystemBase`:

| **Supported feature**| **Run method**| **Schedule method** | **ScheduleParallel method**|
|---|---|---|---|
| Capture local value type      | Supported| Supported |Supported|
| Capture local reference type  | Supported only `WithoutBurst` and not in `ISystem`| Unsupported|Unsupported|
| Writing to captured variables |Supported|Unsupported|Unsupported|
| Use field on the system class | Supported only `WithoutBurst`|Unsupported|Unsupported|
| Methods on reference types    | Supported only `WithoutBurst` and not in `ISystem`|Unsupported|Unsupported|
| Shared components             | Supported only `WithoutBurst` and not in `ISystem`|Unsupported|Unsupported|
| Managed components            | Supported only `WithoutBurst` and not in `ISystem`|Unsupported|Unsupported|
| Structural changes            | Supported only `WithStructuralChanges` and not in `ISystem`|Unsupported|Unsupported|
| `SystemBase.GetComponent`     | Supported| Supported |Supported|
| `SystemBase.SetComponent`     | Supported| Supported |Unsupported|
| `GetComponentDataFromEntity`  | Supported| Supported | Supported only as `ReadOnly`|
| `HasComponent`                | Supported| Supported |Supported|
| `WithDisposeOnCompletion`     | Supported| Supported |Supported|
| `WithScheduleGranularity `    | Unsupported|Unsupported|Supported|
| `WithDeferredPlaybackSystem ` | Supported| Supported |Supported|
| `WithImmediatePlayback`       | Supported|Unsupported|Unsupported|
| `HasBuffer `                  |  Supported| Supported |Supported|
| `SystemBase.GetStorageInfoFromEntity`|  Supported| Supported |Supported|
| `SystemBase.Exists  `                |  Supported| Supported |Supported|

>[!IMPORTANT]
> `WithStructuralChanges` disables Burst. Don't use this option if you want to achieve high levels of performance with `Entities.ForEach`. If you want to use this option, use an [entity command buffer](systems-entity-command-buffers.md) instead.

An `Entities.ForEach` construction uses Roslyn source generators to translate the code you write for the construction into correct ECS code. This translation means you can express the intent of your algorithm without having to include complex, boilerplate code. However, it means that some common ways of writing code aren't allowed.

The following features aren't supported:

* Dynamic code in `.With` invocations
* `SharedComponent` parameters `by ref` 
* Nested `Entities.ForEach` lambda expressions
* Calling with a delegate stored in a variable, field, or by method
* `SetComponent` with lambda parameter type
* `GetComponent` with writable lambda parameter
* Generic parameters in lambdas
* In systems with generic parameters

## Dependencies

By default, a system uses its [`Dependency`](xref:Unity.Entities.SystemBase.Dependency) property to manage its ECS-related dependencies. The system adds each job created with `Entities.ForEach` and `Job.WithCode` to the `Dependency` job handle in the order that they appear in the [`OnUpdate`](xref:Unity.Entities.SystemBase.OnUpdate*) method. You can also pass a `JobHandle` to your `Schedule` methods to manage job dependencies manually, which then returns the resulting dependency. For more information, refer to the [`Dependency`](xref:Unity.Entities.SystemBase.Dependency) documentation.
 
Refer to [Job dependencies](scheduling-jobs-dependencies.md) for more general information about job dependencies.
