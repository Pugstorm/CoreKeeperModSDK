# World update allocator

The world update allocator is a rewindable allocator that ECS rewinds during every world update. Every [world](concepts-worlds.md) contains double rewindable allocators that are created when the world is initiated. 

The [`WorldUpdateAllocatorResetSystem`](xref:Unity.Entities.WorldUpdateAllocatorResetSystem) system switches the double rewindable allocators in every world update. After an allocator swaps in, it rewinds the allocator. Because of this, the lifetime of an allocation from a world update allocator spans two frames. You don't need to manually free the allocations, so there isn't any memory leakage.

You can pass allocations from the world update allocator into a job. You can access the world update allocator through:

* [`World.UpdateAllocator`](xref:Unity.Entities.World.UpdateAllocator)
* [`ComponentSystemBase.WorldUpdateAllocator`](xref:Unity.Entities.ComponentSystemBase.WorldUpdateAllocator)
* [`SystemState.WorldUpdateAllocator`](xref:Unity.Entities.SystemState.WorldUpdateAllocator)

The following is an example of accessing the world update allocator through a world:

[!code-cs[Access world update allocator through World.UpdateAllocator](../Unity.Entities.Tests/AllocatorsCustomPrebuiltTests.cs#world-update-allocator-world)]

The following is an example of accessing the world update allocator through SystemBase:

[!code-cs[Access world update allocator through SystemBase.WorldUpdateAllocator](../Unity.Entities.Tests/AllocatorsCustomPrebuiltTests.cs#world-update-allocator-system-base)]

The following is an example of accessing the world update allocator through SystemState:

[!code-cs[Access world update allocator through SystemState.WorldUpdateAllocator](../Unity.Entities.Tests/AllocatorsCustomPrebuiltTests.cs#world-update-allocator-system-state)]

## Additional resources

* [Allocators overview](allocators-overview.md)
* [Rewindable allocators](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-rewindable.html)
* [Allocator benchmarks](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-benchmarks.html)