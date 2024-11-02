# System group allocator

Each [`ComponentSystemGroup`](xref:Unity.Entities.ComponentSystemGroup) has an option to create a system group allocator when setting its rate manager. To do this, use [`ComponentSystemGroup.SetRateManagerCreateAllocator`](xref:Unity.Entities.ComponentSystemGroup.SetRateManagerCreateAllocator*). If you use the property [`RateManager`](xref:Unity.Entities.ComponentSystemGroup.RateManager*) to set a rate manager in the system group, then component system group doesn't create a system group allocator. 

The following example uses `ComponentSystemGroup.SetRateManagerCreateAllocator` to set a rate manager and create a system group allocator:

[!code-cs[System group creation](../DocCodeSamples.Tests/SystemGroupAllocatorExample.cs#create-allocator)]        

The component system group that creates a system group allocator contains double rewindable allocators. [`World.SetGroupAllocator`](xref:Unity.Entities.World.SetGroupAllocator*) and [`World.RestoreGroupAllocator`](xref:Unity.Entities.World.RestoreGroupAllocator*) are used in [`IRateManager.ShouldGroupUpdate`](xref:Unity.Entities.IRateManager.ShouldGroupUpdate*) to replace the world update allocator with the system group allocator, and later to restore back the world update allocator. 

The example below shows how to use `World.SetGroupAllocator` and `World.RestoreGroupAllocator`:

[!code-cs[System group creation](../DocCodeSamples.Tests/SystemGroupAllocatorExample.cs#group-allocator)]   

The system group allocator contains double rewindable allocators and works in the same way as the world update allocator. Before a system group proceeds to its update, its system group allocator is put in the world update allocator, and allocations from the world update allocator are allocated from the system group allocator. 

If the system group skips its update, it switches the double rewindable allocators of the system group allocator, rewinds the one that swaps in, and then brings back the [world update allocator](allocators-world-update.md). Because this is a double rewindable allocator, the lifetime of an allocation from a system group allocator lasts two system group updates. You don't need to manually free the allocations, so there isn't any memory leakage.

In the example below, the system group allocator is used in `ExampleSystemGroupAllocatorSystem` which is in a fixed rate system group that has a rate manager `FixedRateSimpleManager` as shown above.

[!code-cs[world-update-allocator-worl](../Unity.Entities.Tests/AllocatorsCustomPrebuiltTests.cs#world-update-allocator-system-state)]

## Additional resources

* [Allocators overview](allocators-overview.md)
* [Rewindable allocators](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-rewindable.html)
* [Allocator benchmarks](https://docs.unity3d.com/Packages/com.unity.collections@latest/index.html?subfolder=/manual/allocator-benchmarks.html)