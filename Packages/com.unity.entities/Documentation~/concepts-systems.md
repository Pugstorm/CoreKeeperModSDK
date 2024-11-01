# System concepts

A **system** provides the logic that transforms [component](concepts-components.md) data from its current state to its next state. For example, a system might update the positions of all moving entities by their velocity multiplied by the time interval since the previous update. 

A system runs on the main thread once per frame. Systems are organized into a hierarchy of system groups that you can use to organize the order that systems should update in.

You can create either an unmanaged, or a managed system in Entities. To define a managed system, create a class that inherits from [`SystemBase`](xref:Unity.Entities.SystemBase). To define an unmanaged system, create a struct that inherits from [`ISystem`](xref:Unity.Entities.ISystem). For more information, see [Systems overview](systems-intro.md).

Both `ISystem` and `SystemBase` have three methods you can override: `OnUpdate`, `OnCreate` and `OnDestroy`. A system's `OnUpdate` method is executed once per frame.

A system can only process entities in one [world](concepts-worlds.md), so a system is associated with a particular world. You can use the [`World`](xref:Unity.Entities.ComponentSystemBase.World) property to return the world that the system is attached to.

By default, an automatic bootstrapping process creates an instance of each system and system group. The bootstrapping creates a default world with three system groups: `InitializationSystemGroup`, `SimulationSystemGroup`, and `PresentationSystemGroup`. By default an instance of a system is added to the `SimulationSystemGroup`. You can use the `[UpdateInGroup]` attribute to override this behavior.

To disable the automatic bootstrapping process, use the scripting define `#UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP`.

## System types

There are several types of systems you can use:

* [`SystemBase`](systems-systembase.md): Provides a base class for managed systems. 
* [`ISystem`](systems-isystem.md): Provides an interface for unmanaged systems.
* [`EntityCommandBufferSystem`](systems-entity-command-buffers.md): Provides entity command bufferinstances for other systems. This allows you to group [structural changes](concepts-structural-changes.md) together to improve the performance of your application.
* [`ComponentSystemGroup`](xref:Unity.Entities.ComponentSystemGroup): Provides a nested organization and update order for systems.

## System groups

A system group can have systems and other system groups as its children. A system group has an update method that you can override, and the base method updates the group's children in a sorted order. 

For more information, see the documentation on [System groups](systems-update-order.md).

## Inspecting systems

You can use the Systems window to inspect the update order of the systems in each world, and to view the full hierarchy of the system groups. For more information, see the documentation on [Systems window reference](editor-systems-window.md).

### Systems in the Editor

In the Editor, the following icons represent the different types of Systems. You’ll see this when you use the specific [Entities windows and Inspectors](editor-workflows.md).

|**Icon**|**Represents**|
|---|---|
|![](images/editor-system-group.png)| A system group|
|![](images/editor-system.png)| A system|
|![](images/editor-system-start-step.png)| An entity command buffer system set to execute at the beginning of a system group with the [OrderFirst](xref:Unity.Entities.UpdateInGroupAttribute.OrderFirst) argument.|
|![](images/editor-system-end-step.png)| An entity command buffer system set to execute at the end of a system group with the [OrderLast](xref:Unity.Entities.UpdateInGroupAttribute.OrderLast) argument.|

## Additional resources

* [Accessing data with systems](systems-overview.md)
* [Systems window reference](editor-systems-window.md)
* [System update order](systems-update-order.md)