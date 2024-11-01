# Manage systems in multiple worlds

You can create multiple [worlds](concepts-worlds.md), and you can instantiate the same system type(s) in more than one world. You can also update each system at different rates from different points in the update order. The [Netcode package](https://docs.unity3d.com/Packages/com.unity.netcode@latest) uses this to create separate worlds for client and server running in the same process.
Doing this manually in user code is uncommon, and an advanced use case. 

To do this, you can use the [`ICustomBootstrap`](xref:Unity.Entities.ICustomBootstrap) interface to manage systems in multiple worlds. The Netcode package contains an implementation example which you can refer to.

When you implement this interface, Unity calls it before the default world initialization and uses the return value to determine if the default world initialization should run:

``` c#
public interface ICustomBootstrap
{
    // Create your own set of worlds or your own custom default world in this method.
    // If true is returned, the default world bootstrap doesn't run at all and no additional worlds are created.
    bool Initialize(string defaultWorldName);
}
```

You can use a custom bootstrapper to create worlds, get a filtered list of systems from
`DefaultWorldInitialization.GetAllSystems`, and add a set of systems to a world with
[`DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups`](xref:Unity.Entities.DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups*). You don't need to add the same list of systems that `DefaultWorldInitialization.GetAllSystems` returns and you can add
or remove systems to modify the list. You can also create your own list of systems without using `DefaultWorldInitialization`.

The following is a typical procedure of a custom `MyCustomBootstrap.Initialize` implementation:

1. Create the set of worlds you want your game or application to have.
1. For each created world:
    1. Generate a list of systems you want in that world. You can use [`DefaultWorldInitialization.GetAllSystems`](xref:Unity.Entities.DefaultWorldInitialization.GetAllSystems*) but it isn't required.
    1. Call `DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups` to add the list of systems to the world. 
This also creates the systems in an order that respects [`CreateAfter`](xref:Unity.Entities.CreateAfterAttribute)/[`CreateBefore`](xref:Unity.Entities.CreateBeforeAttribute). 
    1. If you don't want to manually update the world, call `ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop` to add the world to the player loop.
1. If you created the default world, set `World.DefaultGameObjectInjectionWorld` to the default world and return `true`. If you didn't create the default world and want the default bootstrap to do that for you, return `false`.
