# Common Errors
This section lists some of the causes and solutions for common errors coming from [the safety system](concepts-safety.md).

## Errors related to the Dependency property
The [`Dependency`](xref:Unity.Entities.SystemState.Dependency) property is used to record the handles of all jobs a system is scheduling. This is important so that later systems can pass the right dependencies into their jobs to avoid data races. The safety system makes a best effort attempt to detect cases where a job has not been assigned to the `Dependency` property. In these cases, you will see an error such as:
```
The system <SYSTEM NAME> reads <COMPONENT NAME> via <JOB NAME> but that type was not assigned to the Dependency property. To ensure correct behavior of other systems, the job or a dependency must be assigned to the Dependency property before returning from the OnUpdate method.
```

Common causes for this error are:
 - Your code does not assign the `JobHandle`s of the jobs it is scheduling to the `Dependency` property.
 - The safety system could not determine which component types your system is using. This can happen when you use [`EntityQueries`](xref:Unity.Entities.EntityQuery) that were not created using [`GetEntityQuery`](xref:Unity.Entities.SystemState.GetEntityQuery). Your system must not use any query that it did not create itself using `GetEntityQuery`. In particular, do not create entity queries using the `EntityManager`.
 - An exception elsewhere in the system update is causing `OnUpdate` to terminate before the `Dependency` property is assigned (in which case, you should see the real exception logged to the console as well, and this error is a red herring).

## Errors related to parallel writing
You may sometimes encounter errors such as:
```
InvalidOperationException: <JOB FIELD> is not declared [ReadOnly] in a IJobParallelFor job. The container does not support parallel writing. Please use a more suitable container type.
```
This error means that you are using a parallel job and this job is accessing something in parallel in a way that may be unsafe. Parallel jobs in the Entities package include [`IJobChunk`](xref:Unity.Entities.IJobChunk) and [`IJobEntity`](xref:Unity.Entities.IJobEntity).

It is generally unsafe to access data concurrently, unless your job is only reading that data. However, sometimes you may know through domain specific knowledge that your concurrent writes are safe. This is for example the case if you know that no two threads will ever access the same value in parallel.

Common causes for this error are:
 - You are actually only reading from this data. In this case, adding the [`ReadOnly`](https://docs.unity3d.com/ScriptReference/Unity.Collections.ReadOnly.html) attribute is the correct fix. 
 - You are using a native container in a parallel job without the [`ReadOnly`](https://docs.unity3d.com/ScriptReference/Unity.Collections.ReadOnly.html) attribute.
 - You are using a [`ComponentLookup`](xref:Unity.Entities.ComponentLookup) in a parallel job without [`ReadOnly`](https://docs.unity3d.com/ScriptReference/Unity.Collections.ReadOnly.html) attribute.

If you can guarantee that your access is safe, you may use the [`NativeDisableParallelForRestriction`](https://docs.unity3d.com/ScriptReference/Unity.Collections.NativeDisableParallelForRestrictionAttribute.html) attribute to silence the error.

## Errors related to missing dependencies on previously scheduled jobs
When the safety system detects that a job is missing a dependency on another job, you will see an error such as this:
```
InvalidOperationException: The previously scheduled job <JOB NAME> writes to the <OBJECT TYPE> <FIELD IN JOB>. You are trying to schedule a new job <OTHER JOB NAME>, which writes to the same <OBJECT TYPE> (via <FIELD IN OTHER JOB>). To guarantee safety, you must include <JOB NAME> as a dependency of the newly scheduled job.
```
The safety system will flag all cases where the same component type is used in multiple jobs that may run concurrently. In some cases, it may still be safe for these jobs to be executing in parallel as long as you can guarantee that the entities the concurrent jobs operate on do not overlap.

A common cause for this error is that you are scheduling multiple copies of the same job, each on a query with different shared component values. This is safe, as queries with different values for the same shared component type never overlap. You can use the [`NativeDisableContainerSafetyRestriction`](https://docs.unity3d.com/ScriptReference/Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestrictionAttribute.html) attribute on affected fields to disable this error.

## Errors related to safety handles
When the safety system detects that a job is reading a resource that it doesn't have access to, you may see an error such as:
```
InvalidOperationException: The <JOB FIELD> has been declared as [WriteOnly] in the job, but you are reading from it.
```
In practice, `[WriteOnly]` resources are extremely uncommon. A more likely cause for this error is that the read in question is occurring from a job that was launched from within another job. Launching jobs from jobs is not currently supported, and the resulting safety handles won't be set up correctly. This includes less obvious cases like using `.job.Run()` from inside of an `Entities.ForEach().Run()` lambda function, which is itself implemented as a main-thread job. 

## Errors related to system type definitions
The Entities package makes extensive use of source generators to implement core features like `IJobEntity`, `ISystem`,
and `SystemAPI`. One requirement this imposes is that all system types and `IJobEntity` implementations must have the
`partial` keyword, so that the source generators can extend the types with additional generated methods. It's easy to
forget to add the `partial` keyword, which may lead to the following compiler error:
```
error CS0101: The namespace <NAMESPACE> already contains a definition for <SYSTEM/JOB TYPE>
```
Also note that a system deriving from the managed `SystemBase` interface should be a `class`,
while an unmanaged system implementing the `ISystem` interface must be a `struct`.
