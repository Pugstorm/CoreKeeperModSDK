---
uid: systems-update-order
---

# System groups

A system group can have systems and other system groups as its children. A system group updates its children in a sorted order on the main thread. 

To create your own system group, create a class that inherits from [`ComponentSystemGroup`](xref:Unity.Entities.ComponentSystemGroup). Use the [`UpdateInGroup`](xref:Unity.Entities.UpdateInGroupAttribute) attribute on the member systems to specify which systems need to be updated in a given group. Using the `UpdateInGroup` attribute ensures that Unity creates all systems in an order that respects the [`CreateAfter`](xref:Unity.Entities.CreateAfterAttribute) and [`CreateBefore`](xref:Unity.Entities.CreateBeforeAttribute) attributes. 

## Creation order of systems

By default, Unity creates systems in an order that doesn't respect system groups, but does respect `CreateAfter` and `CreateBefore` attributes. 

`OnCreate` methods are called in the same order as systems are created. 

It's best practice to use the [`CreateAfter`](xref:Unity.Entities.CreateAfterAttribute) and [`CreateBefore`](xref:Unity.Entities.CreateBeforeAttribute) attributes, and either the default world creation or [`ICustomBootstrap`](systems-icustombootstrap.md) for advanced use cases. This is the recommended way of creating systems over calling [`World.GetOrCreateSystem`](xref:Unity.Entities.World.CreateSystem*). 

This reduces the likelihood of errors such as if the system creation order changes to another one that also satisfies the attribute constraints. If you must refer to another system in your `OnCreate` method, you should use `CreateAfter` to ensure that your system is created after the other system you're referring to, and then use [`World.GetExistingSystem`](xref:Unity.Entities.World.GetExistingSystem*) to retrieve it.

More commonly, you might want to access a [singleton component](components-singleton.md) or other resource that another system creates in that system's `OnCreate` method. You can add `[CreateAfter(typeof(OtherSystem))]` to the system type to ensure that your `OnCreate` method runs after `OtherSystem.OnCreate`. 

## Destruction order of systems

When you call [`World.Dispose`](xref:Unity.Entities.World.Dispose), Unity destroys the systems in reverse order of their actual creation. Unity destroys them in this order even if their creation broke the `CreateAfter` or `CreateBefore` constraints (for example if you manually called `CreateSystem` out of order).

## Update order of systems

Every time you add a group to a system group, the group re-sorts the system update order for that group before updating again. To control the update order of a system group, add the [`UpdateBefore`](xref:Unity.Entities.UpdateBeforeAttribute) or [`UpdateAfter`](xref:Unity.Entities.UpdateAfterAttribute) attribute to a system to specify which systems it should update before or after. These attributes only apply relative to direct children of the same system group. 

You can also use the [`OrderFirst`](xref:Unity.Entities.UpdateInGroupAttribute.OrderFirst) or [`OrderLast`](xref:Unity.Entities.UpdateInGroupAttribute.OrderLast) parameters to customize the update order, and these take precedence over `UpdateBefore` and `UpdateAfter`. For example:

```c#
// If PeachSystem and DaisySystem are children of the same group, then the
// entity component system puts PeachSystem somewhere before DaisySystem in 
// the sorted order.
[UpdateBefore(typeof(DaisySystem))]
public partial class PeachSystem : SystemBase { }
```

Because everything in a group's update list updates together, the global order of system updates represents a hierarchical ordering, where all direct children of a group are ordered first by `OrderFirst` and `OrderLast`, and then by `UpdateBefore` and `UpdateAfter` constraints. Because the children can also be system groups, these children groups might update many grandchild systems before returning to the current group's update list. 

In the Editor, you can use the Systems window (**Window &gt; Entities &gt; Systems**) to see the full list of system groups in your application, and their ordering:

![](images/editor-system-window.png)<br/>_Systems window showing hierarchical ordering of system updates_

For more information, see the [Systems window](editor-systems-window.md) documentation.

## Default system groups

There is a set of default system groups that you can use to update systems in the correct 
phase of a frame. You can nest one system group inside another so that all systems in a group update in the correct phase and update according to the order within their group.

The default world contains a hierarchy of `ComponentSystemGroup` instances. There are three root-level system groups in the Unity player loop:

* **InitializationSystemGroup**: Updates at the end of the `Initialization` phase of the player loop.
* **SimulationSystemGroup**: Updates at the end of the `Update` phase of the player loop.
* **PresentationSystemGroup**: Updates at the end of the `PreLateUpdate` phase of the player loop.

## System ordering attributes

You can use the following attributes on a system to determine its update order:

|**Attribute**| **Description**|
|---|---|
|`UpdateInGroup`| Specify a `ComponentSystemGroup` that this system should be a member of. If you don't set this attribute, it's automatically be added to the default world's `SimulationSystemGroup`. You can use the optional `OrderFirst` and `OrderLast` parameters to sort systems before or after all other systems in the group that don't have the same parameter set.|
|`UpdateBefore`<br/>`UpdateAfter`| Order systems relative to other systems within the same group. When you apply this attribute to groups, they implicitly constrain all member systems inside them.<br/><br/> For example, if `CarSystem` is in `CarGroup`, and `TruckSystem` is in `TruckGroup`, and `CarGroup` and `TruckGroup` are both members of `VehicleGroup`, then the ordering of `CarGroup` and `TruckGroup` implicitly determines the relative ordering of `CarSystem` and `TruckSystem`. |
|`CreateBefore`<br/>`CreateAfter`| Order system creation relative to other systems. The same ordering rules for `UpdateBefore` and `UpdateAfter` apply here, except that there are no groups, and no `OrderFirst`/`OrderLast`. These attributes override the default behavior. System destruction order is defined as the reverse of creation order. |
|`DisableAutoCreation`| Prevents Unity from creating the system during the default world initialization. See [Manual system creation](#manual-system-creation) below.|

## Manual system creation

If you need to create a system manually, mark the system in question with the [`DisableAutoCreation`](xref:Unity.Entities.DisableAutoCreationAttribute), and then you can use [`World.CreateSystem`](xref:Unity.Entities.World.CreateSystem*) to create a system. To add a system to a group, use the [`AddSystemToUpdateList`](xref:Unity.Entities.ComponentSystemGroup.AddSystemToUpdateList*) method. You can add also other system groups to existing system groups. 

One problem with this approach is that it doesn't guarantee that systems get created in an order that respects the `CreateAfter` and `CreateBefore` attributes on each system.

## Multiple worlds

You can create multiple [worlds](concepts-worlds.md) and you can instantiate the same component system class in more than one world. You can also update each instance at different rates from different points in the update order. The [Netcode package](https://docs.unity3d.com/Packages/com.unity.netcode@latest) uses this to create separate worlds for client and server running in the same process.

To do this, use the `ICustomBootstrap` interface to manage systems in multiple worlds. For more information, see the documentation on [ICustomBootstrap](systems-icustombootstrap.md).

