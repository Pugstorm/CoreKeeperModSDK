---
uid: components-buffer-introducing
---

# Introducing dynamic buffer components

A dynamic buffer component is a component that acts as a resizable array of unmanaged structs. You can use it to store array data for an entity, such as waypoint positions for the entity to navigate between.

Alongside the data, each buffer stores a `Length`, a `Capacity`, and an internal pointer:

* The `Length` is the number of elements in the buffer. It starts at `0` and increments when you append a value to the buffer.
* The `Capacity` is the amount of storage in the buffer. It starts out matching the internal buffer capacity. Setting `Capacity` resizes the buffer.
* The pointer indicates where the dynamic buffer data is. Initially it is `null` to signify that the data is in the chunk with the entity, and if Unity moves the data outside the chunk, the pointer is set to point to the new array. For more information on how Unity stores dynamic buffer components, see [Capacity](#capacity).

## Capacity

The initial capacity of a dynamic buffer is defined by the type that the buffer stores. By default, the capacity defaults to the number of elements that fit within 128 bytes. For more information, see [`DefaultBufferCapacityNumerator`](xref:Unity.Entities.TypeManager.DefaultBufferCapacityNumerator). You can specify a custom capacity using the [`InternalBufferCapacity`](xref:Unity.Entities.InternalBufferCapacityAttribute) attribute. For information on how to create a dynamic buffer component type, see [Create a dynamic buffer component type](components-buffer-create.md).

Initially, Unity stores the dynamic buffer data directly in the chunk of the entity that the component belongs to. If the length of a dynamic buffer is ever greater than the capacity, Unity copies the dynamic buffer data to an array outside of the chunk. If the length of the dynamic buffer later shrinks to less than the capacity, Unity still stores the data outside of the chunk; if Unity moves dynamic buffer data outside of a chunk, it never moves the data back into the chunk.

The original internal buffer capacity is part of the chunk and Unity only deallocates it when Unity deallocates the chunk itself. This means if the dynamic buffer length exceeds the internal capacity and Unity copies the data outside of the chunk, there is wasted space within the chunk. It's best practice to use the data in the chunk when possible. To do this, make sure most of your entities don't exceed the buffer capacities, but also don't set the capacity too high if the entities don't use it. If the size of a dynamic buffer changes too much, it's best practice to store its data outside of the chunk. To do this, set the `InternalBufferCapacity` to `0`.

There are other options available to store array data:

* [Blob assets](xref:Unity.Entities.BlobBuilder): Stores tightly-packed read-only structured data, including arrays, and multiple entities can share a single blob asset. Because they're read-only, you can access them from multiple threads simultaneously.
* [Managed components](components-managed.md): Stores arrays of native or managed objects. However, accessing managed component data is more restrictive and less performant than dynamic buffer component data. Also you need to manually clone and dispose of the array data.
* [Shared components](components-shared.md): Similar to managed components, they store arrays of native or managed objects and your entities can store indices into these larger arrays. They have the same restrictions and performance considerations as managed components.

## Structural changes

[Structural changes](concepts-structural-changes.md) might destroy or move the array referenced by a dynamic buffer which means that any handle to a dynamic buffer becomes invalid after a structural change. You must reacquire dynamic buffers after any structural changes. For example:

```c#
public void DynamicBufferExample(Entity e)
{
    // Acquires a dynamic buffer of type MyElement.
    DynamicBuffer<MyElement> myBuff = EntityManager.GetBuffer<MyElement>(e);

    // This structural change invalidates the previously acquired DynamicBuffer.
    EntityManager.CreateEntity();

    // A safety check will throw an exception on any read or write actions on the buffer.
    var x = myBuff[0];

    // Reacquires the dynamic buffer after the above structural changes.
    myBuff = EntityManager.GetBuffer<MyElement>(e);
    var y = myBuff[0];
}
```
## Comparison to native containers in components

Dynamic buffers are often preferable to using [nativecontainers on components](components-nativecontainers.md) because they are free
of the job scheduling restrictions on the latter, and because they can optionally be stored inline inside a chunk, which helps reduce
memory bandwidth usage.

In general, when there is more than one entity that needs a collection on it, it probably merits a dynamic buffer. If there's only one,
it might work well as a singleton with a native container on it. 

## Additional resources

* [Create a dynamic buffer component](components-buffer-create.md)