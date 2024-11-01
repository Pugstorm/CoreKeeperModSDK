---
uid: job-with-code
---

# Scheduling background jobs with Job.WithCode

The [`Job.WithCode`](xref:Unity.Entities.SystemBase.Job) construction in the [`SystemBase`](xref:Unity.Entities.SystemBase) class runs a method as a single background job. You can also run `Job.WithCode` on the main thread and take advantage of [Burst](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html) compilation to speed up execution.

## Using Job.WithCode

The following example uses one `Job.WithCode` lambda expression to fill a [native array](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html) with random numbers, and another job to add those numbers together:

[!code-cs[job-with-code-example](../DocCodeSamples.Tests/LambdaJobExamples.cs#job-with-code-example)]


To run a parallel job, implement [`IJobFor`](https://docs.unity3d.com/Manual/JobSystemCreatingJobs.html). You can use using [`ScheduleParallel()`](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobForExtensions.ScheduleParallel.html) to schedule the parallel job in the system's [`OnUpdate()`](xref:Unity.Entities.SystemBase.OnUpdate*) function.

## Capture variables

You can't pass parameters to the `Job.WithCode` lambda expression or return a value. Instead, you must capture local variables in a system's [`OnUpdate()`](xref:Unity.Entities.SystemBase.OnUpdate*) function. 

If you use `Schedule()` to schedule your job to run in [Unity's job system](https://docs.unity3d.com/Manual/JobSystem.html), there are additional restrictions:

* You must declare captured variables as a [`NativeArray`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeArray_1.html), a [native container](https://docs.unity3d.com/Manual/JobSystemNativeContainer.html), or a [blittable](https://docs.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types) type.  
* To return data, you must write the return value to a captured `Native Array`, even if the data is a single value. However, if you use `Run()` to execute the job, you can write to any captured variable.

`Job.WithCode` has a set of methods that apply read-only and safety attributes to a captured native container's variables. For example, you can use `WithReadOnly` to restrict access to the variable as read-only. You can also use `WithDisposeOnCompletion` to automatically dispose of a container after the job finishes. For more information, see the [Capturing variables](xref:Unity.Entities.SystemBase.Job) section of the `Job.WithCode` documentation.
 
## Execute the Job.WithCode lambda expression

To execute the `Job.WithCode` lambda expression you can use the following:

* `Schedule()`: Executes the method as a single, non-parallel job. Scheduling a job runs the code on a background thread and takes better advantage of all available CPU resources. You can either explicitly pass a [`JobHandle`](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html) to `Schedule()` or, if you don't pass any dependency, the system assumes that the current system's [`Dependency`](xref:Unity.Entities.SystemBase.Dependency) property represents the job's dependencies. Alternatively, you can pass in a new `JobHandle` if the job has no dependencies.
* `Run()`: Executes the method on the main thread. You can [Burst](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html) compile `Job.WithCode`, so if you use `Run()` to execute the code, this can be faster even though it runs on the main thread. When you call `Run()`, Unity automatically completes all the dependencies of the `Job.WithCode` construction.

## Dependencies

By default, a system uses its [`Dependency`](xref:Unity.Entities.SystemBase.Dependency) property to manage its dependencies. The system adds each [`Entities.ForEach`](iterating-data-entities-foreach.md) and `Job.WithCode` job you create to the `Dependency` job handle in the order that they appear in the `OnUpdate()` method. 

To manage job dependencies manually, pass a [`JobHandle`](https://docs.unity3d.com/ScriptReference/Unity.Jobs.JobHandle.html) to the `Schedule` methods, which then return the resulting dependency. For more information, see the [`Dependency` API documentation](xref:Unity.Entities.SystemBase.Dependency).
 
For general information about job dependencies, see the documentation on [Job dependencies](scheduling-jobs-dependencies.md).
