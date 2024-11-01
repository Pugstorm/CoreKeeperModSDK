# Entity command buffer allocator

The entity command buffer allocator is a custom rewindable allocator. Each [entity command buffer](systems-entity-command-buffers.md) system creates an entity command buffer allocator when the system is created. The life span of an allocation from an entity command buffer allocator is the same as the entity command buffer.  

If you use [`EntityCommandBufferSystem.CreateCommandBuffer()`](xref:Unity.Entities.EntityCommandBufferSystem.CreateCommandBuffer*) to create an entity command buffer, the entity command buffer allocator allocates memory during the recording of the entity command buffer and deallocates the memory after the buffer is played back.

You register a unmanaged singleton that implements [`IECBSingleton`](xref:Unity.Entities.IECBSingleton) through [`ECBExtensionMethods.RegisterSingleton`](xref:Unity.Entities.ECBExtensionMethods.RegisterSingleton*). 

During the registration, the entity command buffer allocator of the parent entity command buffer system is set to the singleton's allocator. Therefore, the singleton's entity command buffers are all allocated from this allocator and they're deallocated after the buffers are played back.

The entity command buffer allocator works in the background, and you don't need to make specific code changes to use it.

## Additional resources

* [Allocators overview](allocators-overview.md)
* [Rewindable allocators](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-rewindable.html)
* [Allocator benchmarks](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-benchmarks.html)