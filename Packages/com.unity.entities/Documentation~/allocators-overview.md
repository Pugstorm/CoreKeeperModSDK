# Allocators overview

Entities and the [Collections package](https://docs.unity3d.com/Packages/com.unity.collections@latest) has different allocators that you can use to manage memory allocations. The different allocators organize and track their memory in different ways. These are the allocators available:

* [Allocator.Temp](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocation.html): A fast allocator for short-lived allocations, which is created on every thread.
* [Allocator.TempJob](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocation.html): A short-lived allocator, which must be deallocated within 4 frames of their creation.
* [Allocator.Persistent](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocation.html): The slowest allocator for indefinite lifetime allocations.
* [Rewindable allocator](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-rewindable.html): A custom allocator that is fast and thread safe, and can rewind and free all your allocations at one point.
* [World update allocator](allocators-world-update.md): A double rewindable allocator that a world owns, which is fast and thread safe.
* [Entity command buffer allocator](allocators-entity-command-buffer.md): A rewindable allocator that an entity command buffer system owns and uses to create entity command buffers.
* [System group allocator](allocators-system-group.md): An optional double rewindable allocator that a component system group creates when setting its rate manager. It's for allocations in a system of fixed or variable rate system group that ticks at different rate from the world update. 

## Allocator feature comparison

The different allocators have the following different features:

|**Allocator type**|**Custom Allocator**|**Need to create before use**|**Lifetime**|**Automatically freed allocations**|**Can pass allocations to jobs**|
|---|---|---|---|---|---|
|[`Allocator.Temp`](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocation.html)|No|No|A frame or a job|Yes|No|
|[`Allocator.TempJob`](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocation.html)|No|No|Within 4 frames of creation|No|Yes|
|[`Allocator.Persistent`](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocation.html)|No|No|Indefinite|No|Yes|
|[Rewindable allocator](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-rewindable.html)|Yes|Yes|Indefinite|No|Yes|
|[World update allocator](allocators-world-update.md)|Yes - a double rewindable allocator|No|Every 2 frames|Yes|Yes|
|[Entity command buffer allocator](allocators-entity-command-buffer.md)|Yes - a rewindable allocator|No|Same as the entity command buffer|Yes|Yes|
|[System group allocator](allocators-system-group.md)|Yes - a double rewindable allocator|Yes|2 fixed rate system group updates|Yes|Yes|

## Additional resources

* [Custom prebuilt allocators overview](allocators-custom-prebuilt-intro.md)
* [Rewindable allocators](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-rewindable.html)
* [Allocator benchmarks](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-benchmarks.html)