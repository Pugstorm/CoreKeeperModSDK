---
uid: components-buffer
---

# Dynamic buffer components

A dynamic buffer component is a component that acts as a resizable array.

| **Topic**                                                    | **Description**                                              |
| ------------------------------------------------------------ | ------------------------------------------------------------ |
| [Introducing dynamic buffer components](components-buffer-introducing.md) | Understand dynamic buffer components and their use cases.    |
| [Create a dynamic buffer component](components-buffer-create.md) | Create a new dynamic buffer component to use in your application. |
| [Access all dynamic buffers in a chunk](components-buffer-get-all-in-chunk.md) | Use a [`BufferAccessor<T>`](xref:Unity.Entities.BufferAccessor`1) to get all dynamic buffers of a particular type in a chunk. |
| [Reuse a dynamic buffer for multiple entities](components-buffer-reuse.md) | Access a dynamic buffer on the main thread and use it's data for multiple entities. |
| [Access dynamic buffers from jobs](components-buffer-jobs.md) | Create a [`BufferLookup`](xref:Unity.Entities.BufferLookup`1) lookup to access dynamic buffers when not on the main thread. |
| [Modify dynamic buffers with an EntityCommandBuffer](components-buffer-command-buffer.md) | Use an [`EntityCommandBuffer`](xref:Unity.Entities.EntityCommandBuffer) to defer dynamic buffer modifications. |
| [Reinterpret a dynamic buffer](components-buffer-reinterpret.md) | Reinterpret the contents of a dynamic buffer as another type. |
