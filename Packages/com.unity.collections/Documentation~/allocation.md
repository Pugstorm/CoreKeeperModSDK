---
uid: collections-allocation
---

# Use allocators to control unmanaged memory

The Collections package allocates `Native-` and `Unsafe-` collections from unmanaged memory, which means that their existence is unknown to the garbage collector.

You are responsible for deallocating any unmanaged memory that you don't need. If you fail to deallocate large or multiple allocations, it can lead to wasting a lot of memory, which might slow down or crash your program.

|**Topic**|**Description**|
|---|---|
|[Allocator overview](allocator-overview.md)| Understand how to use an allocator to manage unmanaged memory.|
|[Aliasing allocators](allocator-aliasing.md)| Create aliases, which share memory allocations with another collection.|
| [Rewindable allocator overview](allocator-rewindable.md)| Understand rewindable allocators, which can pre-allocate memory.|
|[Custom allocator](allocator-custom-define.md)| Understand custom allocators, which you can create for specific memory allocation needs.|
|[Allocator benchmarks](allocator-benchmarks.md)| Compare the various allocators and inspect their performance benchmarks.|