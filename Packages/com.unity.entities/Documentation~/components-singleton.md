# Singleton components

A singleton component is a component that has only one instance in a given [world](concepts-worlds.md). For example, if only one entity in a world has a component of type `T`, then `T` is a singleton component. 

If a singleton component is added to another entity, then it's no longer a singleton component. Additionally, a singleton component can exist in another world, without affecting its singleton state.

## Singleton component APIs

The Entities package contains several APIs you can use to work with singleton components:

|**Namespace**|**Method**|
|---|---|
|**[EntityManager](xref:Unity.Entities.EntityManager)**|[CreateSingleton](xref:Unity.Entities.EntityManager.CreateSingleton*)|
|**[EntityQuery](xref:Unity.Entities.EntityQuery)**|[GetSingletonEntity](xref:Unity.Entities.EntityQuery.GetSingletonEntity)|
||[GetSingleton](xref:Unity.Entities.EntityQuery.GetSingleton*)|
||[GetSingletonRW](xref:Unity.Entities.EntityQuery.GetSingletonRW*)|
||[TryGetSingleton](xref:Unity.Entities.EntityQuery.TryGetSingleton*)|
||[HasSingleton](xref:Unity.Entities.EntityQuery.HasSingleton*)|
||[TryGetSingletonBuffer](xref:Unity.Entities.EntityQuery.TryGetSingletonBuffer*)|
||[TryGetSingletonEntity](xref:Unity.Entities.EntityQuery.TryGetSingletonEntity*)|
||[GetSingletonBuffer](xref:Unity.Entities.EntityQuery.GetSingletonBuffer*)| 
||[SetSingleton](xref:Unity.Entities.EntityQuery.SetSingleton*)|
|**[SystemAPI](xref:Unity.Entities.SystemAPI)**|[GetSingletonEntity](xref:Unity.Entities.SystemAPI.GetSingletonEntity*)|
||[GetSingleton](xref:Unity.Entities.SystemAPI.GetSingleton*)|
||[GetSingletonRW](xref:Unity.Entities.SystemAPI.GetSingletonRW*)|
||[TryGetSingleton](xref:Unity.Entities.SystemAPI.TryGetSingleton*)|
||[HasSingleton](xref:Unity.Entities.SystemAPI.HasSingleton*)|
||[TryGetSingletonBuffer](xref:Unity.Entities.SystemAPI.TryGetSingletonBuffer*)|
||[TryGetSingletonEntity](xref:Unity.Entities.SystemAPI.TryGetSingletonEntity*)|
||[GetSingletonBuffer](xref:Unity.Entities.SystemAPI.GetSingletonBuffer*)|
||[SetSingleton](xref:Unity.Entities.SystemAPI.SetSingleton*)|

It's useful to use the singleton component APIs in situations where you know that there's only one instance of a component. For example, if you have a single-player application and only need one instance of a `PlayerController` component, you can use the singleton APIs to simplify your code. Additionally, in server-based architecture, client-side implementations typically track timestamps for their instance only, so the singleton APIs are convenient and simplify a lot of hand written code.

## Dependency completion

Singleton components have special-case behavior in dependency completion in systems code. With normal component access, APIs such as [EntityManager.GetComponentData](xref:Unity.Entities.EntityManager.GetComponentData*) or [SystemAPI.GetComponent](xref:Unity.Entities.SystemAPI.GetComponent*) ensure that any running jobs that might write to the same component data on a worker thread are completed before returning the requested data.

However, singleton API calls don't ensure that running jobs are completed first. The Jobs Debugger logs an error on invalid access, and you either need to manually complete dependencies with [EntityManager.CompleteDependencyBeforeRO](xref:Unity.Entities.EntityManager.CompleteDependencyBeforeRO*) or [EntityManager.CompleteDependencyBeforeRW](xref:Unity.Entities.EntityManager.CompleteDependencyBeforeRW*), or you need to restructure the data dependencies.

You should also be careful if you use [GetSingletonRW](xref:Unity.Entities.EntityQuery.GetSingletonRW*) to get read/write access to components. Because a reference to component data is returned, it's possible to modify data while jobs are also reading or writing it. The best practices for [GetSingletonRW](xref:Unity.Entities.EntityQuery.GetSingletonRW*) are:

* Only use to access a `NativeContainer` in a component. This is because native containers have their own safety mechanisms compatible with Jobs Debugger, separate from ECS component safety mechanisms.
* Check the Jobs Debugger for errors. Any errors indicate a dependency issue that you need to either restructure or manually complete.
