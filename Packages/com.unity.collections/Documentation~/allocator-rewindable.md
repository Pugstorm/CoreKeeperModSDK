# Rewindable allocator overview

A rewindable allocator is a [custom allocator](allocator-custom-define.md) that works in a similar way to a linear allocator. It's fast and thread safe. A rewindable allocator pre-allocates blocks of memory in advance. 

When you request memory from a rewindable allocator, it selects a range of memory from its pre-allocated block and assigns it to use. The minimum alignment of rewindable allocations is 64 bytes. After it uses all the existing blocks of memory, the rewindable allocator allocates another block of memory. 

It doubles the size of the new block until it reaches a maximum block size. When it reaches this point, the rewindable allocator adds the maximum block size to its previous block size to increase its block size linearly.

One advantage of rewindable allocator is that you don't need to free individual allocations. As its name implies, a rewindable allocator can rewind and free all your allocations at one point. 

When you rewind an allocator, the allocator keeps the memory blocks that it used before to improve performance and disposes the rest of the blocks. When you request to free or dispose a memory allocation from a rewindable allocator, it's a no-op unless you set the enable block free flag of the rewindable allocator. When you set the flag to enable block free, the rewindable allocator rewinds a memory block when it frees the last allocation from the block.     

## Declare and create a rewindable allocator

To create a rewindable allocator, you must do the following:

* Allocate memory to hold the rewindable allocator 
* Add an entry in the global allocator table to register the allocator
* Pre-allocate the allocator's first memory block to initialize it.  

You can use the wrapper [`AllocatorHelper`](xref:Unity.Collections.AllocatorHelper`1) to create a rewindable allocator.

The following example declares and creates a rewindable allocator:

```c#
// Example user structure
internal struct ExampleStruct
{
    // Use AllocatorHelper to help creating a rewindable alloctor
    AllocatorHelper<RewindableAllocator> rwdAllocatorHelper;

    // Rewindable allocator property for accessibility
    public ref RewindableAllocator RwdAllocator => ref rwdAllocatorHelper.Allocator;

    // Create the rewindable allocator
    void CreateRewindableAllocator(AllocatorManager.AllocatorHandle backgroundAllocator, int initialBlockSize, bool enableBlockFree = false)
    {
        // Allocate the rewindable allocator from backgroundAllocator and register the allocator
        rwdAllocatorHelper = new AllocatorHelper<RewindableAllocator>(backgroundAllocator);

        // Allocate the first memory block with initialBlockSize in bytes, and indicate whether
        // to enable the rewindable allocator with individual block free through enableBlockFree
        RwdAllocator.Initialize(initialBlockSize, enableBlockFree);
    }
}
```
## Use a rewindable allocator to allocate memory

For `Native-` collection types, allocation from a rewindable allocator is similar to a classic allocator, except you must use [`CollectionHelper.CreateNativeArray`](xref:Unity.Collections.CollectionHelper.CreateNativeArray*) to create a `NativeArray` from a rewindable allocator. When you use a rewindable allocator to create a `Native-` collection type, its safety handle is added to the list of child safety handles of the rewindable allocator.

For `Unsafe-` collection types, you must use [`AllocatorManager.Allocate`](xref:Unity.Collections.AllocatorManager.Allocate*) to allocate memory from a rewindable allocator.

You don't need to dispose individual allocations. When all allocations aren't needed anymore, call the `Rewind` method of a rewindable allocator to free all its allocations. When you rewind the rewindable allocator, it invalidates and unregisters its child allocators, and invalidates all its child safety handles. For `Native-` collection types, the disposal safety checks throw an exception if the rewindable allocator has rewound. 

This example method `UseRewindableAllocator` shows how to use a rewindable allocator to create and allocate native containers:

[!code-cs[Use rewindable allocator to allocate memory](../Unity.Collections.Tests/AllocatorRewindableTests.cs#allocator-rewindable-use)]

## Free all allocated memory of a rewindable allocator

When you `Rewind` the rewindable allocator, it performs the following operations:

* Invalidates and unregisters all the allocator handle's child allocators
* Invalidates all its child safety handles

The example method `FreeRewindableAllocator` shows how to Free all allocations from the rewindable allocator, with [`Rewind`](xref:Unity.Collections.RewindableAllocator.Rewind).

[!code-cs[Free all allocations from the rewindable allocator](../Unity.Collections.Tests/AllocatorRewindableTests.cs#allocator-rewindable-free)]

## Dispose a rewindable allocator

To dispose a rewindable allocator, you must do the following:

* Dispose all the memory blocks of the rewindable allocator from `Allocator.Persistant`. 
* Unregister the allocator
* Dispose the memory used to store the allocator

The following example adds a method `DisposeRewindableAllocator`that disposes a rewindable allocator using [`Dispose`](xref:Unity.Collections.AllocatorHelper`1.Dispose):

[!code-cs[Dispose a rewindable allocator](../Unity.Collections.Tests/AllocatorRewindableTests.cs#allocator-rewindable-dispose)]

## Full example of a rewindable allocator

The following is a full example of how to use a rewindable allocator:

[!code-cs[Full example of a rewindable allocator](../Unity.Collections.Tests/AllocatorRewindableTests.cs#allocator-rewindable-example)]
