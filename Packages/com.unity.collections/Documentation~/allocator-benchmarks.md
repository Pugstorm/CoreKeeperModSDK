# Allocator benchmarks

The Collections package has different allocators that you can use to manage memory allocations. The different allocators organize and track their memory in different ways. These are the allocators available:

* [Allocator.Temp](allocator-overview.md#allocatortemp): A fast allocator for short-lived allocations, which is created on every thread.
* [Allocator.TempJob](allocator-overview.md#allocatortempjob): A short-lived allocator, which must be deallocated within 4 frames of their creation.
* [Allocator.Persistent](allocator-overview.md#allocatorpersistent): The slowest allocator for indefinite lifetime allocations.
* [Rewindable allocator](allocator-rewindable.md): A custom allocator that's fast and thread safe, and can rewind and free all your allocations at one point.

The [Entities package](https://docs.unity3d.com/Packages/com.unity.entities@latest) has its own set of custom prebuilt allocators:

* [World update allocator](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/manual/allocators-world-update.html): A double rewindable allocator that a world owns, which is fast and thread safe.
* [Entity command buffer allocator](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/manual/allocators-entity-command-buffer.html): A rewindable allocator that an entity command buffer system owns and uses to create entity command buffers.
* [System group allocator](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/manual/allocators-system-group.html): An optional double rewindable allocator that a component system group creates when setting its rate manager. It's for allocations in a system of fixed or variable rate system group that ticks at different rate from the world update. 

For more information, see the Entities documentation on [Custom prebuilt allocators](https://docs.unity3d.com/Packages/com.unity.entities@latest/index.html?subfolder=/manual/allocators-custom-prebuilt.html).

## Allocator feature comparison

The different allocators have the following different features:

|**Allocator type**|**Custom Allocator**|**Need to create before use**|**Lifetime**|**Automatically freed allocations**|**Can pass to jobs**|**Min Allocation Alignment (bytes)**|
|---|---|---|---|---|---|---|
|[Allocator.Temp](allocator-overview.md#allocatortemp)|No|No|A frame or a job|Yes|No|64|
|[Allocator.TempJob](allocator-overview.md#allocatortempjob)|No|No|Within 4 frames of creation|No|Yes|16|
|[Allocator.Persistent](allocator-overview.md#allocatorpersistent)|No|No|Indefinite|No|Yes|16|
|[Rewindable allocator](allocator-rewindable.md)|Yes|Yes|Indefinite|No|Yes|64|

## Performance test results

The following performance tests compare Temp, TempJob, Persistent and rewindable allocators. Because the world update allocator, entity command buffer allocator, and system group allocator are rewindable allocators, their performance is reflected in the rewindable allocator test results. The allocators are tested in single thread cases and in multithread cases by scheduling allocations in jobs across all the cores.  

For results, refer to the [Performance comparison of allocators](performance-comparison-allocators.md) documentation.