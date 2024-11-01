# Create a dynamic buffer component

To create a dynamic buffer component, create a struct that inherits from `IBufferElementData`. This struct defines the element of the dynamic buffer type and also represents the dynamic buffer Component itself.

To specify the initial capacity of the buffer, use the `InternalBufferCapacity` attribute. For information on how Unity manages the capacity of the buffer, see [Capacity](components-buffer-introducing.md).

The following code sample shows a buffer component:

[!code-cs[Create a dynamic buffer component](../DocCodeSamples.Tests/CreateComponentExamples.cs#buffer)]

Like other components, you can add a dynamic buffer component to an entity. However, you represent a dynamic buffer component with a [`DynamicBuffer`](xref:Unity.Entities.DynamicBuffer`1)`<ExampleBufferComponent>` and use dynamic buffer component-specific `EntityManager` APIs, such as [`EntityManager.GetBuffer<T>`](xref:Unity.Entities.EntityManager.GetBuffer*), to interact with them. For example:

```c#
public void GetDynamicBufferComponentExample(Entity e)
{
	DynamicBuffer<ExampleBufferComponent> myDynamicBuffer = EntityManager.GetBuffer<ExampleBufferComponent>(e);
} 
```

## Additional resources

* [Access dynamic buffers from jobs](components-buffer-jobs.md)
* [Reinterpret a dynamic buffer](components-buffer-reinterpret.md)