# SystemAPI overview

[SystemAPI](xref:Unity.Entities.SystemAPI) is a class that provides caching and utility methods for accessing data in an entity's [world](concepts-worlds.md). It works in non-static methods in [SystemBase](systems-systembase.md) and non-static methods in [ISystem](systems-isystem.md) that take `ref SystemState` as a parameter. 

You can use it to perform the following actions:

* **Iterate through data**: Retrieve data per entity that matches a query.
* **Query building**: Get a cached [EntityQuery](systems-entityquery.md), which you can use to  schedule jobs, or retrieve information about that query.
* **Access data**: Get component data, buffers, and [EntityStorageInfo](xref:Unity.Entities.SystemAPI.GetEntityStorageInfoLookup).
* **Access singletons**: Find single instances of data, also known as [singletons](components-singleton.md).

All `SystemAPI` methods directly map to the system you put them in. This means a call like `SystemAPI.GetSingleton<T>()` checks whether the world that's contained on the system can perform the action.

`SystemAPI` uses stub methods, which mean that they all directly call `ThrowCodeGenException`. This is because `SystemAPI` uses [Roslyn source generators](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) which replaces the methods with the correct lookup. This means that you can't call `SystemAPI` outside a supported context.

To inspect your systems, use an IDE that supports source generation, such as Visual Studio 2022+ or Rider 2021.3.3+. You can then select **Go To Definition** to inspect the generated code on the system you're using SystemAPI within. This illustrates why you need to mark systems as `partial`.

## Iterate through data

To iterate through a collection of data on the main thread, you can use the [Query](xref:Unity.Entities.SystemAPI.Query*) method in both [ISystem](systems-isystem.md) and [SystemBase](systems-systembase.md) system types. It uses C#’s idiomatic `foreach` syntax. For more information, see the documentation on [SystemAPI.Query overview](systems-systemapi-query.md)

## Query building

The [QueryBuilder](xref:Unity.Entities.SystemAPI.QueryBuilder) method gets an `EntityQuery`, which you can then use to schedule jobs or retrieve information about the query. It follows the same syntax as [EntityQueryBuilder](systems-entityquery-create.md).

The benefit of using `SystemAPI.QueryBuilder` is that the method caches the data. The following example shows how the `SystemAPI` call is fully compiled:

```cs
/// SystemAPI call
SystemAPI.QueryBuilder().WithAll<HealthData>().Build();

/// ECS compiles it like so:
EntityQuery query;
public void OnCreate(ref SystemState state){
    query = new EntityQueryBuilder(state.WorldUpdateAllocator).WithAll<HealthData>().Build(ref state);
}

public void OnUpdate(ref SystemState state){
    query;
}
```

## Access data

`SystemAPI` contains the following utility methods that you can use to access data in a system's world:

|**Data type**|**API**|
|---|---|
|Component data|[GetComponentLookup](xref:Unity.Entities.SystemAPI.GetComponentLookup*)<br/>[GetComponent](xref:Unity.Entities.SystemAPI.GetComponent*)<br/>[SetComponent](xref:Unity.Entities.SystemAPI.SetComponent*)<br/>[HasComponent](xref:Unity.Entities.SystemAPI.HasComponent*)<br/>[IsComponentEnabled](xref:Unity.Entities.SystemAPI.IsComponentEnabled*)<br/>[SetComponentEnabled](xref:Unity.Entities.SystemAPI.SetComponentEnabled*)|
|Buffers|[GetBufferLookup](xref:Unity.Entities.SystemAPI.GetBufferLookup*)<br/>[GetBuffer](xref:Unity.Entities.SystemAPI.GetBuffer*)<br/>[HasBuffer](xref:Unity.Entities.SystemAPI.HasBuffer*)<br/>[IsBufferEnabled](xref:Unity.Entities.SystemAPI.IsBufferEnabled*)<br/>[SetBufferEnabled](xref:Unity.Entities.SystemAPI.SetBufferEnabled*)|
|EntityInfo|[GetEntityStorageInfoLookup](xref:Unity.Entities.SystemAPI.GetEntityStorageInfoLookup)<br/>[Exists](xref:Unity.Entities.SystemAPI.Exists*)|
|Aspects|[GetAspect](xref:Unity.Entities.SystemAPI.GetAspect*)|
|Handles|[GetEntityTypeHandle](xref:Unity.Entities.SystemAPI.GetEntityTypeHandle)<br/>[GetComponentTypeHandle](xref:Unity.Entities.SystemAPI.GetComponentTypeHandle*)<br/>[GetBufferTypeHandle](xref:Unity.Entities.SystemAPI.GetBufferTypeHandle*)<br/>[GetSharedComponentTypeHandle](xref:Unity.Entities.SystemAPI.GetSharedComponentTypeHandle*)|

These `SystemAPI` methods cache in your systems' `OnCreate` and call `.Update` before any call. Also, when you call these methods, ECS makes sure that the calls are synced before they get lookup access. This means that a call like `SystemAPI.SetBuffer<MyElement>`, which uses a lookup, in this case `BufferLookup<MyElement>`, causes all jobs that are currently writing to `MyElement` to complete. Calls such as `GetEntityTypeHandle` and `GetBufferLookup` don't cause syncs.

This is a useful way to pass data like `IJobEntity` and `IJobChunk` into jobs without causing a sync on the main thread. For example:

```cs
new MyJob{healthLookup=SystemAPI.GetComponentLookup<HealthData>(isReadOnly:true)};
```

Because ECS caches this data, you can directly call it in `OnUpdate`. You don't need to write the whole thing, because it's equal to:

```cs
ComponentLookup<HealthData> lookup_HealthData_RO;
public void OnCreate(ref SystemState state){
    lookup_HealthData_RO = state.GetComponentLookup<HealthData>(isReadOnly:true);
}

public void OnUpdate(ref SystemState state){
    lookup_HealthData_RO.Update(ref state);
    new MyJob{healthLookup=lookup_HealthData_RO};
}
```

### Entities.ForEach compatibility

Only a selection of SystemAPI methods work in [Entities.ForEach](iterating-data-entities-foreach.md). These are as follows:

|**Data type**|**API**|
|---|---|
|Component data|[GetComponentLookup](xref:Unity.Entities.SystemAPI.GetComponentLookup*)<br/>[GetComponent](xref:Unity.Entities.SystemAPI.GetComponent*)<br/>[SetComponent](xref:Unity.Entities.SystemAPI.SetComponent*)<br/>[HasComponent](xref:Unity.Entities.SystemAPI.HasComponent*)|
|Buffers|[GetBufferLookup](xref:Unity.Entities.SystemAPI.GetBufferLookup*)<br/>[GetBuffer](xref:Unity.Entities.SystemAPI.GetBuffer*)<br/>[HasBuffer](xref:Unity.Entities.SystemAPI.HasBuffer*)|
|EntityInfo|[GetEntityStorageInfoLookup](xref:Unity.Entities.SystemAPI.GetEntityStorageInfoLookup)<br/>[Exists](xref:Unity.Entities.SystemAPI.Exists*)|
|Aspects|[GetAspect](xref:Unity.Entities.SystemAPI.GetAspect*)|

## Access singletons

`SystemAPI` has [singleton](components-singleton.md) methods that check to make sure that there is only a single instance of the data it retrieves when invoked. These methods don't sync, which gives them a performance boost.

For example, a call like `SystemAPI.GetSingleton<MyComponent>()` queries whether there is only one entity that matches the given criteria, and if so, gets the component `MyComponent`. It does this without asking the job system to complete all jobs that use `MyComponent`.

This is a useful alternative to [EntityManager.GetComponentData](xref:Unity.Entities.EntityManager.GetComponentData*), which syncs data. For example, when you call `EntityManager.GetComponentData<MyComponent>`, all jobs that write to `MyComponent` complete. 

The following are a list of methods that you can use to access singleton data in `SystemAPI`: 

|**Data type**|**API name**|
|---|---|
|Singleton component data| [GetSingleton](xref:Unity.Entities.SystemAPI.GetSingleton*)<br/>[TryGetSingleton](xref:Unity.Entities.SystemAPI.TryGetSingleton*)<br/>[GetSingletonRW](xref:Unity.Entities.SystemAPI.GetSingletonRW*)<br/>[TryGetSingletonRW](xref:Unity.Entities.SystemAPI.TryGetSingletonRW*)<br/>[SetSingleton](xref:Unity.Entities.SystemAPI.SetSingleton*)|
|Singleton entity data| [GetSingletonEntity](xref:Unity.Entities.SystemAPI.GetSingletonEntity*)<br/>[TryGetSingletonEntity](xref:Unity.Entities.SystemAPI.TryGetSingletonEntity*)|
|Singleton buffers| [GetSingletonBuffer](xref:Unity.Entities.SystemAPI.GetSingletonBuffer*)<br/>[TryGetSingletonBuffer](xref:Unity.Entities.SystemAPI.TryGetSingletonBuffer*)|
|All singletons| [HasSingleton](xref:Unity.Entities.SystemAPI.HasSingleton*)|

## Managed versions of SystemAPI

The `SystemAPI.ManagedAPI` namespace exposes managed versions of the methods in SystemAPI, which you can use to access [managed components](components-managed.md).

|**Data type**|**API name**|
|---|---|
|Component data| [ManagedAPI.GetComponent](xref:Unity.Entities.SystemAPI.ManagedAPI.GetComponent*)<br/>[ManagedAPI.HasComponent](xref:Unity.Entities.SystemAPI.ManagedAPI.HasComponent*)<br/> [ManagedAPI.IsComponentEnabled](xref:Unity.Entities.SystemAPI.ManagedAPI.IsComponentEnabled*)<br/> [ManagedAPI.SetComponentEnabled](xref:Unity.Entities.SystemAPI.ManagedAPI.SetComponentEnabled*)|
|Handles| [ManagedAPI.GetSharedComponentTypeHandle](xref:Unity.Entities.SystemAPI.ManagedAPI.GetSharedComponentTypeHandle*)|

It also contains the following managed versions of the singleton APIs: 

|**Data type**|**API name**|
|---|---|
|Singleton component data| [ManagedAPI.GetSingleton](xref:Unity.Entities.SystemAPI.ManagedAPI.GetSingleton*)<br/>[ManagedAPI.TryGetSingleton](xref:Unity.Entities.SystemAPI.ManagedAPI.TryGetSingleton*)|
|Singleton entity data| [ManagedAPI.GetSingletonEntity](xref:Unity.Entities.SystemAPI.ManagedAPI.GetSingletonEntity*)<br/>[ManagedAPI.TryGetSingletonEntity](xref:Unity.Entities.SystemAPI.ManagedAPI.TryGetSingletonEntity*)|
|All singletons| [ManagedAPI.HasSingleton](xref:Unity.Entities.SystemAPI.ManagedAPI.HasSingleton*)|

You can also use the [ManagedAPI.UnityEngineComponent](xref:Unity.Entities.SystemAPI.ManagedAPI.UnityEngineComponent`1) method, which extends [SystemAPI.Query](systems-systemapi-query.md) so you can query over MonoBehaviours, scriptable objects, and UnityEngine components like `UnityEngine.Transform`. For example:

```cs
foreach (var transformRef in SystemAPI.Query<SystemAPI.ManagedAPI.UnityEngineComponent<Transform>>())
    transformRef.Value.Translate(0,1,0);
```

## Additional resources

* [`SystemAPI` API documentation](xref:Unity.Entities.SystemAPI)