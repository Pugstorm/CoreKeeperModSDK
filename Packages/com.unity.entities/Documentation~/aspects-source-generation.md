# Aspect source generation

Source generators generate code during compilation by analyzing your existing code. The Entities package generates methods and types that allow you to use your aspects with other parts of the Unity API. For more information about source generators in Unity, see the user manual documentation on [Roslyn analyzers and source generators](xref:roslyn-analyzers).

## Aspect-generated methods

The aspect source generator generates additional methods into your aspect struct to make it usable with other APIs such as [`IJobEntity`](xref:Unity.Entities.IJobEntity) and [`SystemAPI.Query<MyAspect>()`](xref:Unity.Entities.SystemAPI.Query*).

### AddComponentRequirementsTo

Add component requirements from this aspect into archetype lists. If a component already exists in the lists, it doesn't add a duplicate component. However, it overwrites read-only requirements with read-write if this aspect requires it. 

#### Declaration

```c#
public void AddComponentRequirementsTo(
    ref UnsafeList<ComponentType> all, 
    ref UnsafeList<ComponentType> any, 
    ref UnsafeList<ComponentType> none, 
    bool isReadOnly)
```

#### Parameters

* `all`: Archetype must match all the component requirements.
* `any`: Archetype must match any of the component requirements.
* `none`: Archetype must match none of the component requirements.
* `isReadOnly`: Set to `true` to make all components read-only.

### CreateAspect

Create an instance of the aspect struct for a specific entity's components' data.

#### Declaration

```c#
public AspectT CreateAspect(
    Entity entity, 
    ref SystemState systemState, 
    bool isReadOnly)
```

#### Parameters

* `entity`: The entity from which to create the aspect struct.
* `systemState`: The system state from which to extract the data.
* `isReadOnly`: Set to `true` to make all references to data read-only.

#### Returns

An aspect struct that points at the component data of the `Entity`.

### Query

Create an `IEnumerable<AspectT>` which you can use to iterate through the query entity aspect.

#### Declaration

```c#
public static Enumerator Query(EntityQuery query, TypeHandle typeHandle)
```

#### Parameters

* `query`: The `EntityQuery` to enumerate.
* `typeHandle`: The aspect's type handle.

### CompleteDependencyBeforeRO

Completes the dependency chain required for this aspect to have read access. This completes all write dependencies of the components, buffers, etc. to allow for reading.

#### Declaration

```c#
public static void CompleteDependencyBeforeRO(ref SystemState systemState)
```

#### Parameters

* `state`: The `SystemState` containing an `EntityManager` that stores all dependencies.

### CompleteDependencyBeforeRW

Complete the dependency chain required for this component to have read/write access. This completes all write dependencies of the components, buffers, etc. to allow for reading, and it completes all read dependencies so that you can write to it.

#### Declaration

```c#
public static void CompleteDependencyBeforeRW(ref SystemState state)
```

#### Parameters

* `state`: The `SystemState` containing an `EntityManager` that stores all dependencies.

## Aspect-generated types

The aspect source generator declares new types nested inside all aspect structs that implement `IAspect`. 

### MyAspect.Lookup

Struct that accesses an aspect on any given entity. It's made up of the required structs such as `ComponentLookup` and `BufferLookup` to provide access to all the aspect’s field data.

#### Generated methods

* `Lookup(ref SystemState state)`: Constructs the lookup from a `SystemState`.
* `Update(ref SystemState state)`: Updates the aspect before using this `Entity`.
* `MyAspect this[Entity entity]`: Lookup `MyAspect` from this `Entity`.

### MyAspect.TypeHandle

Struct that accesses an aspect from an `ArchetypeChunk`. It's made up of the required structs such as `ComponentTypeHandle` and `BufferTypeHandle` to provide access to all the aspect’s field data in a chunk.

#### Generated methods: 

* `TypeHandle(ref SystemState state)`:	Constructs the `TypeHandle` from a `SystemState`.
* `Update(ref SystemState state)`: Updates the aspect before using Resolve.
* `ResolvedChunk Resolve(ArchetypeChunk chunk)`: Gets the aspect's chunk data.

### MyAspect.ResolvedChunk
Struct that represents all instances of an aspect in a chunk. It's made up of the required chunk structs such as `NativeArray` and `BufferAccessor` and provides an indexer and a length.

#### Generated fields:

* `NativeArray<Entity> #AspectPath#_#FieldName#NaE;`: Represents an `Entity` aspect field.
* `NativeArray<ComponentT> #AspectPath#_#FieldName#NaC;`: Represents each `RefRO/RW<ComponentT>` aspect field.
* `BufferAccessor<BufferElementT> #AspectPath#_#FieldName#Ba;`: Represents each `DynamicBuffer<BufferElementT>` aspect field.
* `SharedComponentT #AspectPath#_#FieldName#Sc;`: Represents each `SharedComponent` aspect field. 
* `int Length;`: Length of all `NativeArray` and `BufferAccessor` in this chunk.

#### Field names

* `#AspectPath#`: The name of the enclosing aspect. For nested aspects, this is the full path from the root aspect separated by underscores. For example, `MyRootAspect_MyNestedAspect`.
* `#FieldName#`: The name of the field with the same capitalization. 

### MyAspect.Enumerator

Struct that iterates over all instances of an aspect in an EntityQuery.

### Implementation

```c#
System.Collections.Generic.IEnumerator<AspectT>
System.Collections.Generic.IEnumerable<AspectT>
```
