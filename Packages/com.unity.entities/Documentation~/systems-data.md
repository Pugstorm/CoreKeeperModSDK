# Define and manage system data

When defining how to structure your system level data, you should organize it as component level data, rather than as fields within the system type. 

Using public data on systems isn't best practice. This is because public data access in systems requires a direct reference or pointer to the system instantiation which has the following consequences:

* It creates dependencies between systems, which conflicts with data oriented approaches
* It can't guarantee thread or lifetime safety around accessing the system instance
* It can't guarantee thread or lifetime safety around accessing the system’s data, even if the system still exists and is accessed in a thread-safe manner

## Store system data in components

You should store publicly accessible data in systems in components rather than as fields in the system type. An example of this is in the [`World`](xref:Unity.Entities.World) namespace’s `Get` and `Create` system APIs such as [`GetExistingSystem<T>`](xref:Unity.Entities.World.GetExistingSystem*). They return an opaque [`SystemHandle`](xref:Unity.Entities.SystemHandle) handle to the system rather than direct access to the system’s instance. This applies for both managed [SystemBase](xref:Unity.Entities.SystemBase) and unmanaged [ISystem](xref:Unity.Entities.ISystem) systems.

As an example, in a typically implemented object-oriented code, a type's data is part of the type's definition:

```c#
/// Object-oriented code example
public partial struct PlayerInputSystem : ISystem
{
    public float AxisX;
    public float AxisY;

    public void OnCreate(ref SystemState state) { }

    public void OnUpdate(ref SystemState state)
    {
        AxisX = [... read controller input];
        AxisY = [... read controller input];
    }

    public void OnDestroy(ref SystemState state) { }
}
```

An alternate, data-oriented version of the `PlayerInputSystem` above might look something like this:

```c#
public struct PlayerInputData : IComponentData
{
    public float AxisX;
    public float AxisY;
}

public partial struct PlayerInputSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.AddComponent<PlayerInputData>(state.SystemHandle);
    }

    public void OnUpdate(ref SystemState state)
    {
        SystemAPI.SetComponent(state.SystemHandle, new PlayerInputData {
            AxisX = [...read controller data],
            AxisY = [...read controller data]
        });
    }

    // Component data is automatically destroyed when the system is destroyed. 
    // If a Native Container existed in the component, however, OnDestroy could be used to
    // ensure memory is disposed.
    public void OnDestroy(ref SystemState state) { }  
}
```

This defines a data protocol for the system which is separate from the system functionality. These components can exist in either a [singleton entity](components-singleton.md), or they can belong to a system-associated entity through [`EntityManager.GetComponentData<T>(SystemHandle)`](xref:Unity.Entities.EntityManager.GetComponentData``1(Unity.Entities.SystemHandle)) and similar methods. You should use the latter when you want the data lifetime to be tied to the system lifetime.

When you use this technique, you can access the system's data in the same way as any other entity component data. A reference or pointer to the system instance is no longer necessary.

## Choosing system or singleton entity components

Using the singleton APIs is similar to using system data in an entity component, but with the following differences:

* Singletons aren't tied to the system's lifetime
* Singletons can only exist per system type, not per [system instance](systems-comparison.md#multiple-system-instances)

For more information, see the documentation on [singleton components](components-singleton.md).

## Direct access APIs

The Entities package contains several APIs you can use to directly access system instances, in exceptional circumstances, as follows:

|**Method name**|**ISystem**|**SystemBase**|
|--|--|--|
|[`World.GetExistingSystemManaged<T>`](xref:Unity.Entities.World.GetExistingSystemManaged``1)|No|Yes|
|[`World.GetOrCreateSystemManaged<T>`](xref:Unity.Entities.World.GetOrCreateSystemManaged``1)|No|Yes|
|[`World.CreateSystemManaged<T>`](xref:Unity.Entities.World.CreateSystemManaged``1)|No|Yes|
|[`World.AddSystemManaged<T>`](xref:Unity.Entities.World.AddSystemManaged*)|No|Yes|
|[`WorldUnmanaged.GetUnsafeSystemRef<T>`](xref:Unity.Entities.WorldUnmanaged.GetUnsafeSystemRef*)|Yes|No|
|[`WorldUnmanaged.ResolveSystemStateRef<T>`](xref:Unity.Entities.WorldUnmanaged.ResolveSystemStateRef*)|Yes|Yes|
