# Prebuilt custom allocators overview

You can use prebuilt custom allocators to manage allocations in [worlds](concepts-worlds.md), [entity command buffers](systems-entity-command-buffers.md), and [system groups](systems-write-groups.md). All the following are [rewindable allocators](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-rewindable.html):

|**Allocator**|**Description**|
|---|---|
|[World update allocator](allocators-world-update.md)| A double rewindable allocator that a [world](concepts-worlds.md) owns, which is fast and thread safe.<br/><br/> The allocations that the world update allocator makes are automatically freed every 2 frames, which makes it useful for allocations within a world that last for 2 frames. This allocator has no memory leakage.|
|[Entity command buffer allocator](allocators-entity-command-buffer.md)| A rewindable allocator that an [entity command buffer](systems-entity-command-buffers.md) system owns, which is fast and thread safe.<br/><br/> An entity command buffer system uses this allocator to create entity command buffers. Entity command buffer system automatically frees the allocations after it plays back an entity command buffer. This allocator has no memory leakage.|
|[System group allocator for rate manager](allocators-system-group.md)| An optional double rewindable allocator that a component system group creates when setting its rate manager.<br/><br/> It's useful for allocations in a system of fixed or variable rate system groups that tick at different rate from the world update. Allocations last for 2 system group updates and you don't need to manually free the allocations.|

These prebuilt allocators are custom allocators. To allocate and deallocate `Native-` collection types and `Unsafe-` collection types see the Collections package documentation on [How to use a custom allocator](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-custom-use.html).

## Additional resources

* [Allocators overview](allocators-overview.md)
* [Rewindable allocators](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-rewindable.html)
* [Allocator benchmarks](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-benchmarks.html)
