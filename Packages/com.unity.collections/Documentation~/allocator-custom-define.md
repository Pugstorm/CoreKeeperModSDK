# Custom allocator overview

You can use a custom allocator for specific memory allocation needs. To create a custom allocator, it must contain an allocator handle of type [`AllocatorManager.AllocatorHandle`](xref:Unity.Collections.AllocatorManager.AllocatorHandle) and implement the interface, [`AllocatorManager.IAllocator`](xref:Unity.Collections.AllocatorManager.IAllocator). After you create a custom allocator, you need to register it in a global allocator table in [`AllocatorManager`](xref:Unity.Collections.AllocatorManager).

## Add the AllocatorManager.AllocatorHandle type to the custom allocator

A custom allocator must contain an allocator handle of type [`AllocatorManager.AllocatorHandle`](xref:Unity.Collections.AllocatorManager.AllocatorHandle). An allocator handle includes the following:

* `Version`: A 2 byte unsigned version number. Only the lower 15 bits are valid.
* `Index`: A 2 byte unsigned index of the global allocator table obtained during registration.
* Method to add a safety handle to the list of child safety handles of the allocator handle.
* Method to add a child allocator to the list of child allocators of the allocator handle.
* A rewind method to invalidate and unregister all the child allocators, invalidate all the child safety handles of the allocator handle, and increment the allocator handle' `Version` and `OfficialVersion`.

## Implement AllocatorManager.IAllocator interface

To define a custom allocator, you must implement the interface [`AllocatorManager.IAllocator`](xref:Unity.Collections.AllocatorManager.IAllocator) which includes: 

* [`Function`](xref:Unity.Collections.AllocatorManager.IAllocator.Function): A property that gets the allocator function of delegate [`TryFunction`](xref:Unity.Collections.AllocatorManager.TryFunction). The allocator function can allocate, deallocate, and reallocate memory.
* [`Try`](xref:Unity.Collections.AllocatorManager.IAllocator.Try(Unity.Collections.AllocatorManager.Block@)): A method that the allocator function invokes to allocate, deallocate, or reallocate memory.
* [`Handle`](xref:Unity.Collections.AllocatorManager.IAllocator.Handle): A property that gets and sets the allocator handle which is of type [`AllocatorManager.AllocatorHandle`](xref:Unity.Collections.AllocatorManager.AllocatorHandle).
* [`ToAllocator`](xref:Unity.Collections.AllocatorManager.IAllocator.ToAllocator): A property that casts the allocator handle index to the enum `Allocator`.
* [`IsCustomAllocator`](xref:Unity.Collections.AllocatorManager.IAllocator.IsCustomAllocator): A property that checks whether the allocator is a custom allocator. An allocator is a custom allocator if its handle `Index` is larger or equal to [`AllocatorManager.FirstUserIndex`](xref:Unity.Collections.AllocatorManager.FirstUserIndex).
* [`IsAutoDispose`](xref:Unity.Collections.AllocatorManager.AllocatorHandle.IsAutoDispose): A property that checks whether the allocator is able to dispose individual allocations. False if disposing an individual allocation is a no-op.

Because `AllocatorManager.IAllocator` implements `IDisposable`, your custom allocator must implement the `Dispose` method.

The following is an example of how to set up the `IAllocator` interface and its required properties except the `Try` and `AllocatorFunction` method:

```c#
// A custom allocator must implement AllocatorManager.IAllocator interface
[BurstCompile(CompileSynchronously = true)]
internal struct ExampleCustomAllocator : AllocatorManager.IAllocator
{
    // A custom allocator must contain AllocatorManager.AllocatorHandle
    AllocatorManager.AllocatorHandle m_handle;

    // Implement the Function property required by IAllocator interface
    public AllocatorManager.TryFunction Function => AllocatorFunction;

    // Implement the Handle property required by IAllocator interface
    public AllocatorManager.AllocatorHandle Handle { get { return m_handle; } set { m_handle = value; } }

    // Implement the ToAllocator property required by IAllocator interface
    public Allocator ToAllocator { get { return m_handle.ToAllocator; } }

    // Implement the IsCustomAllocator property required by IAllocator interface
    public bool IsCustomAllocator { get { return m_handle.IsCustomAllocator; } }

    // Implement the IsAutoDispose property required by IAllocator interface
    // Allocations made by this example allocator are not automatically disposed.
    // This implementation can be skipped because the default implementation of
    // this property is false.
    public bool IsAutoDispose { get { return false; } }

    // Implement the Dispose method required by IDisposable interface because
    // AllocatorManager.IAllocator implements IDisposable
    public void Dispose()
    {
        // Make sure no memory leaks
        Assert.AreEqual(0, m_allocationCount);

        m_handle.Dispose();
    }
}
 ```

The `Try` method tells a custom allocator how to allocate or deallocate memory. The following is an example of the`Try` method where a custom allocator allocates memory from `Allocator.Persistant`, initializes the allocated memory with a user configured value, and increments an allocation count. The custom allocator also decrements the allocation count when deallocating the allocated memory.

[!code-cs[Try method of allocate/deallocate memory](../Unity.Collections.Tests/AllocatorCustomTests.cs#allocator-custom-try)]

Example method `AllocatorFunction` below shows an allocator function of the custom allocator.

[!code-cs[Allocator function](../Unity.Collections.Tests/AllocatorCustomTests.cs#allocator-custom-allocator-function)]

## Global allocator table

The global allocator table in [`AllocatorManager`](xref:Unity.Collections.AllocatorManager) stores all the necessary information for custom allocators to work. When you instantiate a custom allocator, you must register the allocator in the global allocator table. The table stores the following information:

* A pointer to the custom allocator instance
* A pointer to the allocator function of the custom allocator instance
* The current official version of the custom allocator instance, lower 15 bits of a 2 byte unsigned integer value
* A list of child safety handles of native containers that are created using the custom allocator instance
* A list of child allocators that are allocated using the custom allocator instance
* A bit flag indicating whether the custom allocator is able to dispose individual allocations

## Custom allocator example

The following is an example of a custom allocator that has an `AllocatorManager.AllocatorHandle` and initializes the allocated memory with a user configured value and increments the allocation count. It also uses `AllocatorManager.TryFunction` to register the allocator on the global allocator table:

[!code-cs[Custom allocator example](../Unity.Collections.Tests/AllocatorCustomTests.cs#allocator-custom-example)]

## Further information

* [Use a custom allocator](allocator-custom-use.md)
