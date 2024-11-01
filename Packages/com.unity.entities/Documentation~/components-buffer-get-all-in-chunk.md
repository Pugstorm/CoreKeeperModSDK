# Access dynamic buffers in a chunk

To access all dynamic buffers in a chunk, use the [`ArchetypeChunk.GetBufferAccessor`](xref:Unity.Entities.ArchetypeChunk.GetBufferAccessor*) method. This takes a [`BufferTypeHandle<T>`](xref:Unity.Entities.BufferTypeHandle`1) and returns a [`BufferAccessor<T>`](xref:Unity.Entities.BufferAccessor`1).  If you index the `BufferAccessor<T>`, it returns the chunk's buffers of type `T`:

The following code sample shows how to access every dynamic buffer of a type in a chunk.

[!code-cs[Dynamic buffer component](../DocCodeSamples.Tests/CreateComponentExamples.cs#buffer)]

[!code-cs[Get dynamic buffers](../DocCodeSamples.Tests/DynamicBufferExamples.cs#access-buffers-in-chunk)]

## Additional resources

* [Reuse a dynamic buffer for multiple entities](components-buffer-reuse.md)
* [Modify dynamic buffers with an `EntityCommandBuffer`](components-buffer-command-buffer.md)