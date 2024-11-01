# Modify dynamic buffers with an entity command buffer

An [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer) (ECB) records commands to add, remove, or set buffer components for entities. There are dynamic buffer-specific APIs that are different than the regular component APIs.

An ECB can only record commands to happen in the future, so it can only manipulate dynamic buffer components in the following ways:

* [`SetBuffer<T>`](xref:Unity.Entities.EntityCommandBuffer.SetBuffer*): Returns a `DynamicBuffer<T>` that the recording thread can populate with data. At playback, these buffer contents overwrite any existing buffer contents. `SetBuffer` doesn’t fail if the target entity already contains the buffer component. If more than one thread records a `SetBuffer` command on the same entity, after playback only the contents added by the last command according to `sortKey` order are visible. `SetBuffer` has the same functionality as [`AddBuffer<T>`](xref:Unity.Entities.EntityCommandBuffer.AddBuffer*), except `AddBuffer` adds the buffer to the component first, if it doesn't exist.
* [`AppendToBuffer<T>`](xref:Unity.Entities.EntityCommandBuffer.AppendToBuffer*): Appends a single buffer element to an existing buffer component on an entity, and preserves any existing buffer contents. Multiple threads can safely append to the same buffer component on the same entity and the `sortKey` of the recorded commands determines the order of the resulting elements. `AppendToBuffer<T>` fails at playback if the target entity doesn’t contain a buffer component of type `T`. Therefore, it's best practice to precede every `AppendToBuffer` command with `AddComponent<T>`, to ensure that the target buffer component is present.
* You can safely use the [`AddComponent<T>`](xref:Unity.Entities.EntityCommandBuffer.AddComponent*) and [`RemoveComponent<T>`](xref:Unity.Entities.EntityCommandBuffer.RemoveComponent*) methods if `T` is an `IBufferElementData` to add an empty buffer or remove an existing buffer. These methods are safe to use from multiple threads and adding an existing component or removing a non-existent component doesn't cause an error.

The following code example walks through some general dynamic buffer-specific `EntityCommandBuffer` APIs. It assumes a dynamic buffer called `MyElement` exists.

[!code-cs[Dynamic buffer in ECB](../DocCodeSamples.Tests/DynamicBufferExamples.cs#dynamicbuffer.ecb)]

When you set the `Length`, `Capacity`, and content of the `DynamicBuffer`, ECS records those changes into the `EntityCommandBuffer`. When you play back the `EntityCommandBuffer`, ECS makes the changes to the dynamic buffer.

## Additional resources

* [Access dynamic buffers from jobs](components-buffer-jobs.md)
* [Entity command buffer overview](systems-entity-command-buffers.md)