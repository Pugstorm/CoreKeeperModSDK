---
uid: systems-systembase
---

# SystemBase overview

To create a managed system, implement the abstract class [`SystemBase`](xref:Unity.Entities.SystemBase).

You must use the [`OnUpdate`](xref:Unity.Entities.SystemBase.OnUpdate) system event callback to add the work that your system must perform every frame. All the other callback methods in the `ComponentSystemBase` namespace are optional. 

All system events run on the main thread. It's best practice to use the `OnUpdate` method to schedule jobs to perform most of the work. To schedule a job from a system, you can use one of the following mechanisms:

* [`Entities.ForEach`](xref:Unity.Entities.SystemBase.Entities): Iterates over component data.
* [`Job.WithCode`](xref:Unity.Entities.SystemBase.Job): Execute a lambda expression as a single, background job.
* [`IJobEntity`](xref:Unity.Entities.IJobEntity): Iterates over component data in multiple systems.
* [`IJobChunk`](xref:Unity.Entities.IJobChunk): Iterates over data by [archetype chunk](concepts-archetypes.md#archetype-chunks).

The following example illustrates using `Entities.ForEach` to implement a system that updates one component based on the value of another:
 
[!code-cs[basic-system](../DocCodeSamples.Tests/SystemBaseExamples.cs#basic-system)]

## Callback method order

There are several callback methods within `SystemBase` that Unity invokes at various points during the system creation process, which you can use to schedule the work your system must do every frame:

* [`OnCreate`](xref:Unity.Entities.ComponentSystemBase.OnCreate): Called when a system is created.
* [`OnStartRunning`](xref:Unity.Entities.ComponentSystemBase.OnStartRunning): Called before the first call to `OnUpdate` and whenever a system resumes running.
* [`OnUpdate`](xref:Unity.Entities.SystemBase.OnUpdate): Called every frame as long as the system has work to do. For more information on what determines when a system has work to do, see [`ShouldRunSystem`](xref:Unity.Entities.ComponentSystemBase.ShouldRunSystem).
* [`OnStopRunning`](xref:Unity.Entities.ComponentSystemBase.OnStopRunning): Called before `OnDestroy`. Also called whenever the system stops running, which happens if no entities match the system's [`RequireForUpdate`](xref:Unity.Entities.ComponentSystemBase.RequireForUpdate*), or if the system's [`Enabled`](xref:Unity.Entities.ComponentSystemBase.Enabled)property is set to `false`. Note if no `RequireForUpdate` is specified, the system will run continuously unless disabled or destroyed.
* [`OnDestroy`](xref:Unity.Entities.ComponentSystemBase.OnDestroy): Called when a system is destroyed.

The following diagram illustrates a system's event order:

![](images/SystemEventOrder.png)

A parent [system group's](concepts-systems.md#system-groups) `OnUpdate` method triggers the `OnUpdate` methods of all the systems in its group. For more information about how systems update, see [Update order of systems](systems-update-order.md). 
