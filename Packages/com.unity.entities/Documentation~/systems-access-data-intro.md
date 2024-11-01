# Ways to access data

How you access data in a system depends on whether you’ve used a managed [`SystemBase`](systems-systembase.md) system, or an unmanaged [`ISystem`](systems-isystem.md) system. The following are ways of accessing the data in a system:

* **SystemState**: Use the properties and methods in [`SystemState`](xref:Unity.Entities.SystemState) to access raw entity state data in `ISystem` systems. `SystemBase` and `SystemAPI` natively use the data in `SystemState`.
* **SystemBase**: Contains the same methods as `SystemState`, but you can call them from a [`SystemBase`](xref:Unity.Entities.SystemBase) system. 
* **SystemAPI**: Calls the data you can get from SystemState, but caches and updates the data for you, meaning that you can use [`SystemAPI`](xref:Unity.Entities.SystemAPI) methods directly in an `Update` loop. Because you can directly use these methods in `Update`, there's no runtime cost to using `SystemAPI`, so use `SystemAPI` to access data, wherever possible.

## SystemState

You can use `SystemState` to get data in the following ways:

* Find out information about worlds
* Query a system to get information about it
* Get data which to add as a dependency of the system. This is similar to the way you get data with EntityManager, but with added dependencies.

### World information

To find out information about worlds, you can use the following properties: 

* [`state.World`](xref:Unity.Entities.SystemState.World*)
* [`state.WorldUnmanaged`](xref:Unity.Entities.SystemState.WorldUnmanaged)
* [`state.WorldUpdateAllocator`](xref:Unity.Entities.SystemState.WorldUpdateAllocator)
* [`state.GlobalSystemVersion`](xref:Unity.Entities.SystemState.GlobalSystemVersion)
* [`state.EntityManager`](xref:Unity.Entities.SystemState.EntityManager)

### System information

There are several ways in which you can query the status of systems:

|**API**|**Description**|
|---|---|
|[`state.Dependency`](xref:Unity.Entities.SystemState.Dependency)<br/>[`state.CompleteDependency`](xref:Unity.Entities.SystemState.CompleteDependency)|Get or complete dependencies of the system.|
|[`RequireForUpdate`](xref:Unity.Entities.SystemState.RequireForUpdate*)<br/>[`RequireAnyForUpdate`](xref:Unity.Entities.SystemState.RequireAnyForUpdate*)<br/> [`ShouldRunSystem`](xref:Unity.Entities.SystemState.ShouldRunSystem)<br/>[`Enabled`](xref:Unity.Entities.SystemState.Enabled)| Determine when a system needs to run.|
|[`state.SystemHandle`](xref:Unity.Entities.SystemState.SystemHandle)| Get a system’s handle.|
|[`state.LastSystemVersion`](xref:Unity.Entities.SystemState.LastSystemVersion)| Get a system’s [version number](systems-version-numbers.md).|
|[`state.DebugName`](xref:Unity.Entities.SystemState.DebugName)| Get a system’s debug name.|

### Dependency data

To get methods which you can add as a dependency of the system, you can use:

|**API**|**Description**|
|---|---|
|[`GetEntityQuery`](xref:Unity.Entities.SystemState.GetEntityQuery*)| Gets a query.<br/><br>**Note:** [`EntityQueryBuilder.Build`](xref:Unity.Entities.EntityQueryBuilder.Build*) is the preferred method to get queries and you should use this method wherever possible.|
|[`GetBufferLookup`](xref:Unity.Entities.SystemState.GetBufferLookup*)<br/>[`GetComponentLookup`](xref:Unity.Entities.SystemState.GetComponentLookup*)<br/>[`GetEntityStorageInfoLookup`](xref:Unity.Entities.SystemState.GetEntityStorageInfoLookup*)| Get lookups.|
|[`GetComponentTypeHandle`](xref:Unity.Entities.SystemState.GetComponentTypeHandle*)<br/> [`GetBufferTypeHandle`](xref:Unity.Entities.SystemState.GetBufferTypeHandle*)<br/> [`GetEntityTypeHandle`](xref:Unity.Entities.SystemState.GetEntityTypeHandle*)<br/> [`GetSharedComponentTypeHandle`](xref:Unity.Entities.SystemState.GetSharedComponentTypeHandle*)<br/>[`GetDynamicComponentTypeHandle`](xref:Unity.Entities.SystemState.GetDynamicComponentTypeHandle*)<br/> [`GetDynamicSharedComponentTypeHandle`](xref:Unity.Entities.SystemState.GetDynamicSharedComponentTypeHandle*)| Get type handles.| 

These methods all add dependencies to the given type. For example, if you call `state.GetComponentTypeHandle<MyComp>(isReadOnly: true)`, it adds a dependency of `MyComp` to be readable. This means that `state.Dependency` includes all earlier systems' `state.Dependency` for the systems which write to `MyComp`. `GetEntityQuery` has the same functionality for every component in the query, while the lookup methods add a dependency of the type.

## SystemBase

All the methods in `SystemState` are available in `SystemBase`, and they're prefixed with `this. `rather than `state.`

## SystemAPI

SystemAPI is a class that provides caching and utility methods for accessing data in an entity's world. It works in non-static methods in `SystemBase` and non-static methods in `ISystem` that take `ref SystemState` as a parameter. Because you can directly use these methods in `Update`, there's no runtime cost to using `SystemAPI`, so use `SystemAPI` to access data, wherever possible.

For more information, refer to the [SystemAPI overview documentation](systems-systemapi.md).

## Additional resources

* [`SystemState` API documentation](xref:Unity.Entities.SystemState)
* [Systems comparison](systems-comparison.md)
* [SystemAPI overview](systems-systemapi.md)