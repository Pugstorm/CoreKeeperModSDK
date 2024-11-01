# Upgrading from Entities 0.51 to 1.0

To upgrade from Entities 0.51 to 1.0, you need to do the following:

* [Update ISystem](#update-isystem)
* [Update Transforms in your project](#update-transforms-in-your-project)
* [Update conversions in your project](#update-conversions-in-your-project)
* [Remove GenerateAuthoringComponent](#remove-generateauthoringcomponent)
* [Update code that gets and creates a world's systems](#update-code-that-gets-and-creates-a-worlds-systems)
* [Remove Entities.ForEach in ISystem-based systems](#remove-entitiesforeach-in-isystem-based-systems)
* [Update IJobChunk to handle enableable components](#update-ijobchunk-to-handle-enableable-components)
* [Convert IJobEntityBatch and IJobEntityBatchWithIndex to IJobChunk](#convert-ijobentitybatch-and-ijobentitybatchwithindex-to-ijobchunk)
* [Rename asynchronous EntityQuery gather-scatter methods](#rename-asynchronous-entityquery-gather-scatter-methods)
* [Convert IJobEntityBatch and IJobEntityBatchWithIndex to IJobChunk](#convert-ijobentitybatch-and-ijobentitybatchwithindex-to-ijobchunk)
* [Update system state components to cleanup components](#update-system-state-components-to-cleanup-components)
* [Update TypeManager code to use TypeIndex instead of int](#update-typemanager-code-to-use-typeindex-instead-of-int)
* [Update EntityCommandBufferSystem that have direct access](#update-entitycommandbuffersystem-that-have-direct-access)
* [Change system update code](#change-system-update-code)
* [Rename EntityQueryDescBuilder](#rename-entityquerydescbuilder)
* [Update SystemBase.Time and SystemState.Time](#update-systembasetime-and-systemstatetime)
* [Update SystemBase helper methods to SystemAPI](#update-systembase-helper-methods-to-systemapi)
* [Add the Entities Graphics package to your project](#add-the-entities-graphics-package-to-your-project)
* [Modify blob assets that use new or default](#modify-blob-assets-that-use-new-or-default)
* [Update partials in your project](#update-partials-in-your-project)
* [Update SceneSystem](#update-scenesystem)

## Update ISystem

ISystem now uses C#'s default implemented methods, so you don't have to implement every function:

```c#
// Before
partial struct MySystem : ISystem {
    public void OnCreate(ref SystemState state){}
    public void OnUpdate(ref SystemState state){
        // code goes here...
    }
    public void OnDestroy(ref SystemState state){}
}

// After
partial struct MySystem : ISystem {
    public void OnUpdate(ref SystemState state){
        // code goes here...
    }
}
```

You now no longer need to put `[BurstCompile]` on the struct of an `ISystem`, but it's still needed on `OnCreate`, `OnStartRunning`, `OnUpdate`, `OnStopRunning` and `OnDestroy`. For example:

```c#
// Before
[BurstCompile]
partial struct MySystem : ISystem {
    [BurstCompile]
    public void OnUpdate(ref SystemState state){
        // code goes here...
    }
}

// After
partial struct MySystem : ISystem {
    [BurstCompile]
    public void OnUpdate(ref SystemState state){
        // code goes here...
    }
}
```

## Update Transforms in your project

The way that Transforms work in Entities 1.0 has changed. This section contains information on how to upgrade your project to work with the new Transforms. For further information on how Transforms work in Entities, see the [Transforms in Entities](transforms-intro.md) documentation. 

Note: The Transform system is under active development and subject to change up until the 1.0 release.

### LocalTransform
The three components `Translation`, `Rotation`, and `Scale` have been combined into one component, named `LocalTransform`.

```c#
public struct LocalTransform : IComponentData
{
    public float3     Position;
    public float      Scale;
    public quaternion Rotation;
}
```

The following is an example of how to convert your code to use the `LocalTransform` component:

```c#
// BEFORE
void Execute(ref Translation translation)
{
    translation.Value += math.up();
}

// AFTER
void Execute(ref LocalTransform transform)
{
    transform.Position += math.up();
}
```
Other transform components (`CompositeRotation`, `RotationPivotTranslation`, `RotationPivot`, `PostRotation`, `CompositeRotation`, `RotationEulerXYZ` (etc), `PostRotationEulerXYZ` (etc), `NonUniformScale`, `ScalePivot`, `ScalePivotTranslation`, `CompositeScale`, `ParentScaleInverse`) have been removed.

### Scale

`LocalTransform` has a single uniform scale property. The `NonUniformScale` component has been removed. To non-uniformly scale the geometry, use a [`PostTransformMatrix`](xref:Unity.Transforms.PostTransformMatrix). For example:

```c#
EntityManager.AddComponent(myEntity, new PostTransformMatrix { Value = float4x4.Scale(1, 2, 3) });
```

### Relativity

The `LocalTransform` component is relative to its parent. If there is no [`Parent`](xref:Unity.Transforms.Parent) component, `LocalTransform` is relative to the origin of the world.

### Initialization

The following is an example of how to initialize the `LocalTransform` component:

```c#
// BEFORE
var t = new Translation { Value = new float3(1, 2, 3) };
var r = new Rotation { Value = quaternion.RotateZ(1) };
var s = new Scale { Value = 2 }

// AFTER
var transform = LocalTransform.FromPositionRotationScale(new float3(1, 2, 3), quaternion.RotateZ(1), 2);
```

To see the full list of initializer variations available, see the API documentation for [`LocalTransform`](xref:Unity.Transforms.LocalTransform). All initializers begin with `From`.

You must initialize all transforms. The C# default behavior is to initialize a struct with all zeroes, and that's an invalid transform. Where necessary, use [`LocalTransform.Identity`](xref:Unity.Transforms.LocalTransform.Identity) as a default value, like so:

```c#
var t = LocalTransform.Identity;
```
### Changing an individual transform property

You may set the Position, Rotation, and Scale components of `LocalTransform` directly. The `LocalTransform` API also includes helper functions to create a new transform with one or more properties changed. For example, to create a new transform with a new position, leaving rotation and scale at their original value, you could:

```c#
SomeFunction(myTransform.WithPosition(1, 2, 3));
```

To see the full list of modifier variations available, see the API documentation for [`LocalTransform`](xref:Unity.Transforms.LocalTransform).

## Update conversions in your project

The previous conversion system has been replaced with Baking. For more information, see the documentation on [Baking](baking.md)

Bakers are close to `IConvertGameObjectToEntity` and are where you directly interface with the Unity objects, such as your authoring component. Bakers are also where dependencies are implicitly or explicitly captured, and all components added are capable of automatically being reverted if a Baker re-runs. A Baker can only add components to the primary entity that it's baking and the additional entities created by itself.

### Basic Baker
This example shows how to change the usage of `IConvertGameObjectToEntity` to a Baker:

```c#
public struct MyComponent : IComponentData
{
   public float Value;
}

// BEFORE
public class MyMonoBehaviour : MonoBehaviour, IConvertGameObjectToEntity
{
    public float value;

    public void Convert(Entity entity, EntityManager dstManager,
        GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new MyComponent {Value = value});
    }
}


// AFTER
public class MyMonoBehaviour : MonoBehaviour
{
   public float value;
}

public class MyBaker : Baker<MyMonoBehaviour>
{
   public override void Bake(MyMonoBehaviour authoring)
   {
       AddComponent(new MyComponent {Value = authoring.value} );
   }
}
```

The code inside the Baker is almost the same as the one in Convert with the following exceptions:

* You access the data via the authoring parameter.
* You don’t need to specify the `EntityManager`.
* Because you're adding a component to the primary entity, you don’t need to specify it.

A Baker can also be inside a `MyMonoBehaviour` class or in a completely separate file.

### Prefabs in Bakers
Previously, to declare and convert Prefabs you had to implement the `IDeclareReferencedPrefabs` interface. Now you just need to call `GetEntity` in the Baker:

```c#
public struct MyComponent : IComponentData
{
   public Entity Prefab;
}

public class MyMonoBehaviour : MonoBehaviour
{
   public GameObject prefab;
}

public class MyBaker : Baker<MyMonoBehaviour>
{
   public override void Bake(MyMonoBehaviour authoring)
   {
       AddComponent(new MyComponent { Prefab = GetEntity(authoring.prefab) } );
   }
}
```

### Runtime conversion
Runtime Conversion will be fully removed for 1.0 and the only way to use Baking is through Sub Scenes. This means that `ConvertToEntity` and `GameObjectConversionUtility.ConvertGameObjectHierarchy` don’t work with Baking.

## Remove GenerateAuthoringComponent

`GenerateAuthoringComponent` has been removed and you need to use a regular MonoBehaviour and Baker instead. 

To migrate without losing the data in the scenes, you need to write the MonoBehaviours before upgrading to 1.0. These are the steps to preserve the data. Make sure the scripts aren't compiled until you complete step 2:

1. Remove `[GenerateAuthoringComponent]` from your component.
    ```c#
    //[GenerateAuthoringComponent]
    public struct MyComponent : IComponentData
    {
        public float Value;
    }
    ```
1. In the same file where you got your `IComponentData`, write your `MonoBehaviour`. This can be done manually or by copy and pasting the generated code.
    ```c#
    //[GenerateAuthoringComponent]
    public struct MyComponent : IComponentData
    {
        public float Value;
    }

    public class MyComponentAuthoring : MonoBehaviour
    {
        public float Value;
    }
    ```
1. At this point, errors and warnings appear in the Unity Editor. Ignore them and carry on to step 4. The errors look like:
    ```
    'MyComponent' is missing the class attribute 'ExtensionOfNativeClass'!
    GameObject (named 'GameObject') references runtime script in scene file. Fixing!
    ```
1. In the Unity Editor, rename the file to match the name of your new `MonoBehaviour`. This preserves the information in the .meta file. In the case of the sample, the file needs to be renamed to `MyComponentAuthoring.cs`. The previous errors should be gone now and your data should have been preserved.
1. After upgrading to 1.0, you need to write the Baker for that new `MonoBehaviour`. The final file in the example will look like this (`MyComponentAuthoring.cs`):
    ```c#
    public struct MyComponent : IComponentData
    {
        public float Value;
    }

    public class MyComponentAuthoring : MonoBehaviour
    {
        public float Value;
    }

    public class MyComponentAuthoringBaker : Baker<MyComponentAuthoring>
    {
    public override void Bake(MyComponentAuthoring authoring)
    {
        AddComponent(new MyComponent
        {
            Value = authoring.Value
        });       
    }
    }
    ```

## Update code that gets and creates a world's systems

Previously, World methods for accessing particular systems would return a direct reference to the system instance. This includes `GetExistingSystem`, `GetOrCreateSystem`, and `CreateSystem`.

They now return a `SystemHandle` rather than a direct reference. 

If you have code that accesses system data directly, you should move system-associated data into a component. These components can exist in either a singleton entity, or they can belong to a system-associated entity through `EntityManager.GetComponentData<T>(SystemHandle)` and similar methods. The latter is recommended when data lifetime should be tied to the system lifetime. 

This change enables Burst compiled `ISystem` systems to do all the things that `SystemBase` systems that aren't Burst compiled can do. 

To access managed system instances directly, replace the following calls:
* `GetExistingSystem` to `GetExistingSystemManaged`
* `GetOrCreateSystem` to `GetOrCreateSystemManaged`
* `CreateSystem` to `CreateSystemManaged`

## Remove Entities.ForEach in ISystem-based systems

`Entities.ForEach` is deprecated in ISystem-based systems. There are now two APIs you can use to iterate over entities: [`IJobEntity`](xref:Unity.Entities.IJobEntity) and [`Query<T>`](xref:Unity.Entities.SystemAPI.Query*).

The `IJobEntity` job interface was introduced in 0.50. For information on how to use this interface, see the documentation on [Iterating over data with IJobEntity](iterating-data-ijobentity.md).

To iterate directly over an enumerable that gives access to component data or aspects with [`Query<T>`](xref:Unity.Entities.SystemAPI.Query*) you can use an idiomatic C# foreach statement:

```c#
foreach (var (myAspect, myWriteData, myReadData) in Query<MyAspect,RefRW<WriteComponent>, RefRO<ReadComponent>>())
    { 
    // Do stuff; 
    }
 ```

The Query method is part of the `SystemAPI` helper class. Use a static statement at the top of the source file to implicitly reference this:

```c#
using static Unity.Entities.SystemAPI;
```

### Limitations

* Code in the `foreach` body always runs on the main thread (until `foreach` is supported inside `IJobEntity`).
* You must use the `RefRO` and `RefRW` generic types to iterate through components. `RefRO` indicates read-only access and `RefRW` indicates read-write.
* You can iterate up to 8 aspects or components in the same `foreach` statement.
* You can't query dynamic buffers in a `foreach` statement.
* You can't save a query in a variable, field, or property and then reuse it multiple times.
* You can only perform a `foreach` iteration over a `Query<T>()` inside a method that has a `ref SystemState` parameter.

## Update IJobChunk to handle enableable components

[IJobChunk](xref:Unity.Entities.IJobChunk) now supports [enableable components](components-enableable.md). When an `IJobChunk` implementation iterates over the entities in a chunk, you must now identify and skip over individual entities with disabled components that cause them not to match the job’s `EntityQuery`. As such, you must update all existing `IJobChunk` implementations to handle enableable components.

The parameters passed to [`IJobChunk.Execute()`](xref:Unity.Entities.IJobChunk.Execute*) have changed:

* The `chunkIndex` parameter is now called `unfilteredChunkIndex`, to emphasize that it's the index of the chunk within the list of all chunks that match the job’s query.
* A new `chunkEnabledMask` parameter is now provided, which contains a 128-bit mask that describes which of the chunk’s entities match the job’s query, and should be processed.
* A new `useEnabledMask` parameter is now provided, which indicates whether the `chunkEnabledMask` parameter contains valid data.
* The `firstEntityIndex` parameter is removed. This value was expensive to calculate, and most `IJobChunk` implementations don't use this parameter. If you have any jobs that use this value as a sort key when recording an `EntityCommandBuffer`, you should use `unfilteredChunkIndex` instead. If you need this value for example, to read or write per-entity data to or from a tightly packed array, see the [upgrade information below](#jobs-that-need-firstentityindex).

For more information on these parameters, see the [`IJobChunk.Execute()`](xref:Unity.Entities.IJobChunk.Execute*) API documentation.

### Required updates
To upgrade an existing IJobChunk implementation:

1. Change the signature of the `Execute()` method:
    ```c#
    // Old signature
    void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
    // New signature
    void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    ```
1. If the job contains a loop over the chunk’s entities, update the loop to skip entities that don’t match the job’s query. The new [`ChunkEntityEnumerator`](xref:Unity.Entities.ChunkEntityEnumerator) helper encapsulates all the necessary logic:
    ```c#
    // Old loop
    for(int i=0, chunkEntityCount=chunk.Count; i < chunkEntityCount; ++i)
    {
    }
    // New loop
    var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
    while(enumerator.NextEntityIndex(out int i))
    {
    }
    ```
1. The overhead of `ChunkEntityEnumerator` is as low as possible, so you can leave in the original for loop in cases where you are certain that the `EntityQuery` used to schedule the job doesn't contain any enableable components. However, it's best practice to add an assert that fires if this precondition ever changes:
    ```c#
    // Loop where enableable components are not needed
    Assert.IsFalse(useEnabledMask); 
    // This job is not written to handle enableable components
    for(int i=0, chunkEntityCount=chunk.Count; i < chunkEntityCount; ++i)
    {
    }
    ```
1. At all call sites where the job is scheduled, change `job.Schedule(query,dependsOn)` to `job.ScheduleParallel(query, dependsOn)`. Without this change, the job compiles and runs, but its execution happens on a single worker thread instead of “going wide” across multiple worker threads in parallel. **Important:** The `dependsOn` parameter is no longer optional: most jobs should already have an explicit input dependency, but if they don't, pass `default`.

### Jobs that need firstEntityIndex
By default, `IJobChunk` no longer passes the `firstEntityIndex` parameter to its `Execute()` method. This is because most `IJobChunk` operations don't use this parameter, and it requires a prefix sum operation to compute which isn't performant. The two most common use cases for `firstEntityIndex` were as a `sortKey` parameter when recording an `EntityCommandBuffer`, and as an index into an array of per-entity data that the job is reading or writing.

You can now use `chunkIndex` or `unfilteredChunkIndex` as an `EntityCommandBuffer` sort key. For more information, see the [`IJobChunk.Execute()`](xref:Unity.Entities.IJobChunk.Execute*) API documentation.

For jobs that need the `firstEntityIndex` value for any other reason, use the optional helper function [`EntityQuery.CalculateBaseEntityIndexArrayAsync()`](xref:Unity.Entities.EntityQuery.CalculateBaseEntityIndexArrayAsync*) to compute the index for each chunk. You should call this method directly  before scheduling the job that needs `firstEntityIndex`, and use the same `EntityQuery` that schedules the job, plus whatever input dependency the job requires. You can then pass the resulting array of per-chunk indices into the user job:

```c#
NativeArray<int> chunkBaseEntityIndices = query.CalculateBaseEntityIndexArrayAsync(
	Allocator.TempJob, myJobInputDependency, out JobHandle baseIndexJobHandle);
JobHandle myJobHandle = new MyJob
    {
        ChunkBaseEntityIndices = chunkBaseEntityIndices,
        // other job fields here
    }.ScheduleParallel(query, baseIndexJobHandle);
```

In the user job, you can use a chunk's unfilteredChunkIndex to look up the baseEntityIndex for each chunk. You must mark the array as `[DeallocateOnJobCompletion]` to prevent a memory leak. You can compute the index of a given entity in the query like so:

```c#
struct MyJob : IJobChunk
{
	[ReadOnly] [DeallocateOnJobCompletion] public NativeArray<int> ChunkBaseEntityIndices;
	// other job fields here
	public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
		bool useEnabledMask, in v128 chunkEnabledMask)
	{
		int baseEntityIndex = ChunkBaseEntityIndices[unfilteredChunkIndex];
		int validEntitiesInChunk = 0;
		var enumerator = new ChunkEntityEnumerator(useEnabledMask,
			chunkEnabledMask, chunk.Count);
		while(enumerator.NextEntityIndex(out int i))
		{
			// INCORRECT: may count entities that do not match the query
			int incorrectEntityIndexInQuery = baseEntityIndex + i;
			// CORRECT: gives the index of the entity relative to all matching entities
			int entityIndexInQuery = baseEntityIndex + validEntitiesInChunk;
			++validEntitiesInChunk;
		}
	}
}
```

## Convert IJobEntityBatch and IJobEntityBatchWithIndex to IJobChunk

The `IJobEntityBatch` and `IJobEntityBatchWithIndex` job types have been removed. You must convert existing implementations of these job types to [`IJobChunk`](xref:Unity.Entities.IJobChunk), which has been rewritten to efficiently support [enableable components](components-enableable.md). This migration is similar to the information in the [Update IJobChunk](#required-updates) section of this upgrade guide:

1. Change the job struct to implement `IJobChunk` instead of `IJobEntityBatch` or `IJobEntityBatchWithIndex`
1. Update the signature of `Execute()`. The old parameter names are slightly different, but they map to the same `chunkIndex` and f`irstEntityIndex` parameters of the old `IJobChunk` API.
1. Update any loops over entities to use `ChunkEntityEnumerator` (if enableable components might be present), or a raw for loop with an assert (if not).

You don't need to change `Schedule(query,dependsOn)` to `ScheduleParallel(query,dependsOn)`. `IJobEntityBatch.Schedule()` and `IJobEntityBatchWithIndex.Schedule()` already have the same semantics as the new `IJobChunk` API.

For `IJobEntityBatchWithIndex`, see the information in the [Jobs that need firstEntityIndex](#jobs-that-need-firstentityindex) section of this upgrade guide.

## Rename asynchronous EntityQuery gather-scatter methods

The following methods in the [`EntityQuery`](xref:Unity.Entities.EntityQuery) namespace have been renamed:

* `ToEntityArrayAsync`: Renamed to [`ToEntityListAsync`](xref:Unity.Entities.EntityQuery.ToEntityListAsync*).
* `ToComponentDataArrayAsync`: Renamed to [`ToComponentDataListAsync`](xref:Unity.Entities.EntityQuery.ToComponentDataListAsync*).
* `CopyFromComponentDataArrayAsync`: Renamed to [`CopyFromComponentDataListAsync`](xref:Unity.Entities.EntityQuery.CopyFromComponentDataListAsync*).

This is a result of the new [enableable components](components-enableable.md) feature. The set of entities that match an EntityQuery might depend on the results of a previously-scheduled job if the query references any enableable components. The asynchronous gather operations (previously `EntityQuery.ToEntityArrayAsync` and `EntityQuery.ToComponentArrayAsync`) now return a `NativeList` rather than a `NativeArray`. This is because the final length of a list that has enableable components can't be known until the gather job completes. A `NativeList` can be returned to the caller before its final size is known, unlike a `NativeArray`, whose size needs to be set at array creation time. So, the asynchronous methods have been renamed to reflect this.

To upgrade existing calls to these methods:

1. Change the method name to its new one.
1. Change the return type from `NativeArray<T>` to `NativeList<T>`
1. Optionally, pass an additional input dependency `JobHandle`. The scheduled gather-scatter job automatically depends on any previously registered jobs which have write access to the query's enableable components. If you need additional input dependencies, you can pass them as an optional `additionalInputDep` parameter.
1. If you want to access the output `NativeList` (including reading any of its fields), you need to either wait for the gather job to complete, or pass the list to a follow up job. This is because attempting to access the list triggers a safety error. If you create a follow up job, make sure it expects a `NativeList` rather than a `NativeArray`. If the follow up job is an `IJobParallelFor`, you should convert it to `IJobParallelForDefer`, which supports scheduling with a `NativeList`. 
1. Change `[DeallocateOnJobCompletion]` attributes to an allocator that doesn't require allocations to be explicitly disposed, such as`World.UpdateAllocator.ToAllocator`. If you can't do this, then schedule a follow up job with `list.Dispose(JobHandle)` to asynchronously dispose the list once it's no longer required. For example:
    ```c#
    // old code
    struct ProcessHealthArrayJob : IJobParallelFor
    {
        [DeallocateOnJobCompletion] public NativeArray<Health> HealthArray;
        void Execute(int i)
        {
            // process HealthArray[i] here
        }
    }
    // inside system.OnUpdate()
    NativeArray<Health> healthArray = query.ToComponentDataArrayAsync(Allocator.TempJob,
        out JobHandle gatherJob);
    var processJob = new ProcessHealthArrayJob{ HealthArray = healthArray }
        .Schedule(healthArray.Count, 64, gatherJob);

    // new code
    struct ProcessHealthJob : IJobParallelForDefer
    {
        public NativeList<Health> HealthList;
        void Execute(int i)
        {
            // process HealthList[i] here
        }
    }
    // inside system.OnUpdate()
    NativeList<Health> healthList =
        query.ToComponentDataListAsync(World.UpdateAllocator.ToAllocator, out JobHandle gatherJob);
    var processJob = new ProcessHealthListJob{ HealthList = healthList }
        .Schedule(healthList, gatherJob);
    ```

## Update system state components to cleanup components

The following APIs have been renamed:

* `ISystemStateComponentData` renamed to [`ICleanupComponentData`](xref:Unity.Entities.ICleanupComponentData)
* `ISystemStateSharedComponentData` renamed to [`ICleanupSharedComponentData`](xref:Unity.Entities.ICleanupSharedComponentData)
* `ISystemStateBufferElementData` renamed to [`ICleanupBufferElementData`](xref:Unity.Entities.ICleanupBufferElementData)

You should replace all occurrences of these if the script updater doesn't automatically handle them following. Using the old type names won’t function properly and should trigger warnings.

## Update TypeManager code to use TypeIndex instead of int

`TypeManager` now uses `TypeIndex` to represent a type index, rather than an int. This provides better type-safety, and less confusion on how the int should be used in code paths. `TypeIndex` implicitly converts the int back and forth, so this has little impact on existing code, however this conversion has a performance cost. You should therefore upgrade any code that uses into to prefer `TypeIndex` instead.

You must manually update any code that uses an `int*` for an indirection to a type index, or array of type indices to `TypeIndex*`.

## Update EntityCommandBufferSystem that have direct access

`EntityCommandBufferSystem` has been updated so that if you want to play back a particular `EntityCommandBufferSystem`, you can now Burst compile it.

Previously, you couldn't Burst compile an entity command buffer that would get played back by a particular `EntityCommandBufferSystem` because it directly accessed the `EntityCommandBufferSystem`. For example:

```c#
public void OnUpdate(ref SystemState state)  // ‘state’ unused in this example
{
    var ecb = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>()
        .CreateCommandBuffer();
    var deferredEntity = ecb.CreateEntity();
    ecb.AddComponentData(deferredEntity, new myData(42, 255));
}
```

To do the same thing and Burst compile it, you can use `SystemAPI` do the following: 

```c#
[BurstCompile]
public void OnUpdate(ref SystemState state)
{
    var ecb = SystemAPI.GetSingleton
        <EndSimulationEntityCommandBufferSystem.Singleton>()
        .CreateCommandBuffer(state.WorldUnmanaged);
    var deferredEntity = ecb.CreateEntity();
    ecb.AddComponentData(deferredEntity, new myData(42, 255));
}
```
While the nested singleton type has been implemented for built in top-level entities `ComponentSystemGroup`s, you need to define the singleton type and implementation in any `EntityCommandBufferSystems` you've defined to follow the same design.

## Change system update code 

Systems have been changed so that they update by default.

Previously, systems would early-out before calling `OnUpdate` if none of the entity queries created by the system matched any entities (as determined by calling `EntityQuery.IsEmptyNoFilter`), unless you used the `AlwaysUpdateSystem` attribute.

If you don't make any changes to your code, some systems might call `OnUpdate` when they wouldn't before. This causes a decrease in performance, because the early-out is used to avoid overhead of updating a system when it hasn't any work to do. It might also cause exceptions if the implementation of these systems assumes that certain components exist, based on the previous behavior.

To maintain the previous behavior, review each system:

1. If the system has any calls to `RequireForUpdate` in `OnCreate`, you don't need to make any changes.
1. If the system doesn't make any calls to `GetEntityQuery`, then you don't need to make any changes. 
    * Calls to `GetSingleton`, `SetSingleton`, `HasSingleton`, and similar, make calls to `GetEntityQuery` internally.
    * Source generation from `Entities.ForEach` or `IJobEntity` can generate calls to `GetEntityQuery`.
1. If the system uses the obsolete `[AlwaysUpdateSystem]` attribute, remove it.
1. If none of the above are true, add the `[RequireMatchingQueriesForUpdate]` attribute.

For the best performance, you should specify the system update requirements explicitly with `RequireForUpdate` and `RequireAnyForUpdate`, and provide the minimal number of required queries or components.

## Rename EntityQueryDescBuilder

`EntityQueryDescBuilder` has been renamed to `EntityQueryBuilder`, and its methods have been changed to match the syntax. `EntityQueryBuilder` is now the recommended method to create an `EntityQuery`, because it's Burst-compatible and more performant than other options, even if it isn't Burst compiled. 

Previous methods such as `EntityQueryDesc` and `GetEntityQuery(ComponentType[])` will be removed in a future version of Entities.

For example, you can rewrite a query in a system inheriting from `SystemBase` like so:

```c#
/// Old way
var query = GetEntityQuery(typeof(Translation), typeof(Rotation));

/// New way
var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<Translation, Rotation>().Build(this);
```

This is an example of a more complex query:

```c#
/// Old way
var query2 = GetEntityQuery(
    new EntityQueryDesc
    {
        All = new[]
        {
            ComponentType.ReadWrite<LocalToWorld>(),
            ComponentType.ReadOnly<Translation>(),
            ComponentType.ReadOnly<Rotation>(),
        },
        None = new[]
        {
            ComponentType.ReadOnly<Parent>(),
            ComponentType.ReadOnly<Child>(),
        }
    });


/// New way
var query2 = new EntityQueryBuilder(Allocator.Temp)
    .WithAllRW<LocalToWorld>()
    .WithAll<Translation, Rotation>()
    .WithNone<Parent, Child>()
    .Build(this);
```

## Update SystemBase.Time and SystemState.Time

`SystemBase.Time` and `SystemState.Time` are now deprecated and you should use `SystemAPI.Time `instead. Previously `SystemBase.Time` and `SystemState.Time` acted as aliases for `World.Time`. You should now use`SystemAPI.Time` which works in both `ISystem` and `SystemBase`. In cases where you can’t do that, because you’re outside a system, get hold of the world instead either as `World` or `WorldUnmanged`, which both have a `Time` property.

## Update SystemBase helper methods to SystemAPI

SystemBase helpers like `GetComponent`, `SetComponent`, `GetSingleton` have been marked for deprecation. Instead use [`SystemAPI`](xref:Unity.Entities.SystemAPI) inside systems. This also works for `Entities.ForEach` 

In places where `SystemAPI` doesn't work, you can do the following things:

For singleton APIs you can get an `EntityQuery` and use their equating method:

```c#
// Before
void MyMethod(SystemBase mySystem) {
    var mySingleton = mySystem.GetSingleton<MySingleton>();
}

// After
void MyMethod(SystemBase mySystem) {
    var myEntityQueryBuilder = new EntityQueryBuilder(mySystem.WorldUpdateAllocator).WithAll<MySingleton>();
    var myQuery = myEntityQueryBuilder.Build(mySystem);
    var mySingleton = myQuery.GetSingleton<MySingleton>();
}
```

For `GetComponent`, `SetComponent`, `HasComponent`, `GetBuffer` and `Exists`, you can either use the equating `EntityManager` methods, or cache your own lookups:

```c#
// Before
void MyMethod(SystemBase mySystem, Entity e) {
    var myComponent = mySystem.GetComponent<MyComponent>(e);
}

// After (Option A)
void MyMethod(SystemBase mySystem, Entity e) {
    var myComponent = mySystem.EntityManager.GetComponentData<MyComponent>(e);
}

// After (Option B)
void MyMethod(SystemBase mySystem, Entity e, ComponentLookup<MyComponent> alreadyUpdatedLookupOfMyComponent) {
    var myComponent = alreadyUpdatedLookupOfMyComponent[e];
}
```

`GetBuffer` in `SystemAPI` doesn't take in a bool of whether or not it's read only. Therefore it assumes it's always ReadWrite. To convert to read only, do the following:

```c#
// Before
var readonlyBuffer = this.GetBuffer<MyElement>(e, true);

// After
EntityManager.CompleteDependencyBeforeRO<MyElement>(); // Lookups don't cause syncing so this is needed for giving the exact same effect as before.
var readonlyBuffer = SystemAPI.GetBufferLookup<MyElement>(true)[e];
```

## Add the Entities Graphics package to your project

The Hybrid Rendering package (com.unity.rendering.hybrid) has been renamed to Entities Graphics (com.unity.entities.graphics) for consistency. The Hybrid Renderer still exists as an empty utility package with a dependency on Entities Graphics so you won't encounter any problem. However, you should add the Entities Graphics package to your project directly to avoid any errors in the future when the Hybrid Renderer package is removed completely.

If your project uses stock hybrid rendering you don't need to change your code. If you're using hybrid rendering classes or structs in your custom code, you might need to rename some classes. As a rule of thumb, any `Hybrid` in a class, struct, or enum has been replaced with `EntitiesGraphics`. For example, `UpdateOldHybridChunksJob` is now `UpdateOldEntitiesGraphicsChunksJob`.

For further information upgrading to Entities Graphics, see the [Entities Graphics upgrade guide](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/upgrade-guide.html). 

## Modify blob assets that use new or default

Blob assets created with `new` or `default` now produce an error. To fix this, do the following:


```c#
/// Before
var myBlob = new MyBlob(...);

struct MyBlob {
    public MyBlob(...){...}
}

/// After
ref var myBlob = ref builder.ConstructRoot<MyBlob>();
myBlob.Setup(...);

struct MyBlob {
    public void Setup(...){...}
}
```

You can't use `fixed type varName[n]` syntax inside a field on a blob because it creates a pointer, which you can't use in blob assets. To fix this, do the following:

```c#

/// Before
unsafe struct MyBlob {
    public fixed bool Values[128];
}
ref var blob = ...; // construct it
blob.Values[0] = true;

/// After
using Unity.Collections.LowLevel.Unsafe;

unsafe struct MyBlob {
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct Values128
    {
        byte @byte;
        public bool this[int i] {
            get => UnsafeUtility.ReadArrayElement<bool>(UnsafeUtility.AddressOf(ref @byte), i);
            set => UnsafeUtility.WriteArrayElement(UnsafeUtility.AddressOf(ref @byte), i, value);
        }
    }

    public Values128 Values;
}
ref var blob = ...; // construct it
blob.Values[0] = true;
```

## Update partials in your project

Unity now generates a backing partial for `ISystem`, and `SystemBase` in most cases, and for `IAspect` and `IJobEntity` in all cases. Previously, every generator had their own `Missing Partial` error message implementation. This error message has been replaced with an analyzer that throws `EA0007`. This makes it easier to maintain, and means that you can disable specific sections. However, it also means that it's more conservative because it can't always detect when a generator does actually need the partial. As a result, `ISystem` and `SystemBase` now always has to have a partial. 

To fix this, you can use Roslyn. In Rider and Visual Studio you can hover over a snippet that needs fixing, then select **Add partial**. You should see the replaced snippet along with options to apply the fix for your entire document, project, solution or containing type.

If you want to disallow sourcegen in a specific system, you can use the following syntax:

```c#
using Unity.Entities;
#pragma warning disable EA0007 // Force no sourcegen to take place for this system. E.g. SystemAPI, and IJobEntity scheduling will not be avaiable in this system.
struct ManualSystem : ISystem {}
#pragma warning enable EA0007
```

## Update SceneSystem

SceneSystem is used to manage entity scenes loading. In Entities 0.51 you had to access the SceneSystem instance first and then call its methods to manage a scene. In 1.0 all those methods in SceneSystem are static, so you don't use the system instance. For example in 0.51 the code to load a scene would be:

```c#
var sceneSystem = World.GetOrCreateSystem<SceneSystem>();
sceneSystem.LoadSceneAsync(sceneGUID);
```

For the same example in 1.0 you can call the static method directly, but pass the unmanaged world as the first parameter:

```c#
SceneSystem.LoadSceneAsync(state.WorldUnmanaged, sceneGUID);
```