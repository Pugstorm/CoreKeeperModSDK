---
uid: systems-isystem
---

# ISystem overview

To create an unmanaged system, implement the interface type [`ISystem`](xref:Unity.Entities.ISystem).

## Implement abstract methods

You must implement the following abstract methods, which you can [Burst compile](https://docs.unity3d.com/Packages/com.unity.burst@latest):

|**Method**|**Description**|
|---|---|
|[`OnCreate`](xref:Unity.Entities.ISystem.OnCreate*)|System event callback to initialize the system and its data before usage.|
|[`OnUpdate`](xref:Unity.Entities.ISystem.OnUpdate*)|System event callback to add the work that your system must perform every frame.|
|[`OnDestroy`](xref:Unity.Entities.ISystem.OnDestroy*)|System event callback to clean up resources before destruction.|

`ISystem` systems aren't inherited through a base class, like [`SystemBase`](systems-systembase.md) systems. Instead, each of the `OnCreate`, `OnUpdate`, and `OnDestroy` methods have a [`ref SystemState`](xref:Unity.Entities.SystemState) argument that you can use to access [`World`](xref:Unity.Entities.World), [`WorldUnmanaged`](xref:Unity.Entities.WorldUnmanaged), or contextual `World` data and APIs such as [`EntityManager`](xref:Unity.Entities.EntityManager).

## Optionally implement ISystemStartStop

You can also optionally implement the interface [`ISystemStartStop`](xref:Unity.Entities.ISystemStartStop), which provides the following callbacks:

|**Method**|**Description**|
|---|---|
|[`OnStartRunning`](xref:Unity.Entities.ISystemStartStop.OnStartRunning*)|System event callback before the first call to `OnUpdate`, and when a system resumes after it's stopped or disabled.|
|[`OnStopRunning`](xref:Unity.Entities.ISystemStartStop.OnStopRunning*)|System event callback when a system is disabled or doesn't match any of the system's required components for update.|

## Schedule jobs

All system events run on the main thread. It's best practice to use the `OnUpdate` method to schedule jobs to perform most of the work. To schedule a job from a system, use one of the following:

* [`IJobEntity`](iterating-data-ijobentity.md): Iterates over component data in multiple entities, which you can reuse across systems.
* [`IJobChunk`](xref:Unity.Entities.IJobChunk): Iterates over data by [archetype chunk](concepts-archetypes.md#archetype-chunks).

## Callback method order

There are several callback methods within `ISystem` that Unity invokes at various points during the system creation process, which you can use to schedule the work your system must do every frame:

* [`OnCreate`](xref:Unity.Entities.ISystem.OnCreate*): Called when ECS creates a system.
* [`OnStartRunning`](xref:Unity.Entities.ISystemStartStop.OnStartRunning*): Called before the first call to `OnUpdate` and whenever a system resumes running.
* [`OnUpdate`](xref:Unity.Entities.ISystem.OnUpdate*): Called every frame as long as the system has work to do. For more information on what determines when a system has work to do, see [`ShouldRunSystem`](xref:Unity.Entities.SystemState.ShouldRunSystem).
* [`OnStopRunning`](xref:Unity.Entities.ISystemStartStop.OnStopRunning*): Called before `OnDestroy`. Also called whenever the system stops running, which happens if no entities match the system's [`RequireForUpdate`](xref:Unity.Entities.SystemState.RequireForUpdate*), or if you've set the system's [`Enabled`](xref:Unity.Entities.SystemState.Enabled)property to `false`. If you've not specified a  `RequireForUpdate`, the system runs continuously unless disabled or destroyed.
* [`OnDestroy`](xref:Unity.Entities.ISystem.OnDestroy*): Called when ECS destroys a system.

The following diagram illustrates a system's event order:

![](images/SystemEventOrder.png)

A parent [system group's](systems-update-order.md) `OnUpdate` method triggers the `OnUpdate` methods of all the systems in its group. For more information about how systems update, see [Update order of systems](systems-update-order.md#update-order-of-systems). 
