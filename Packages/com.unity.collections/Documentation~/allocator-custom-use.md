# Use a custom allocator

Once you've [defined a custom allocator](allocator-custom-define.md), you can add it to your structure or class. 

## Declare and create a custom allocator

The first step is to declare and create the custom allocator. You must do the following:

* Allocate memory to hold the custom allocator 
* Register the allocator by adding an entry in a global allocator table
* Initialize the allocator if necessary.

The wrapper [`AllocatorHelper`](xref:Unity.Collections.AllocatorHelper`1) helps the process in creating a custom allocator. Examples are given below as how to declare and create a custom allocator defined in the [Example custom allocator](allocator-custom-define.md).  

```c#
// Example user structure that contains the custom allocator
internal struct ExampleCustomAllocatorStruct
{
    // Use AllocatorHelper to help creating the example custom alloctor
    AllocatorHelper<ExampleCustomAllocator> customAllocatorHelper;

    // Custom allocator property for accessibility
    public ref ExampleCustomAllocator customAllocator => ref customAllocatorHelper.Allocator;

    // Create the example custom allocator
    void CreateCustomAllocator(AllocatorManager.AllocatorHandle backgroundAllocator, byte initialValue)
    {
        // Allocate the custom allocator from backgroundAllocator and register the allocator
        customAllocatorHelper = new AllocatorHelper<ExampleCustomAllocator>(backgroundAllocator);

        // Set the initial value to initialize the memory
        customAllocator.Initialize(initialValue);
    }
}
```

## Use a custom allocator to allocate memory

For `Native-` collection types, allocation from a custom allocator is similar to a classic allocator, except you must use [`CollectionHelper.CreateNativeArray`](xref:Unity.Collections.CollectionHelper.CreateNativeArray*) to create a `NativeArray` from a custom allocator and [`CollectionHelper.Dispose`](xref:Unity.Collections.CollectionHelper.Dispose*) to deallocate a `NativeArray` from a custom allocator.

For `Unsafe-` collection types, you must use [`AllocatorManager.Allocate`](xref:Unity.Collections.AllocatorManager.Allocate*) to allocate memory from a custom allocator and [`AllocatorManager.Free`](xref:Unity.Collections.AllocatorManager.Free*) to deallocate the memory.

When you use a custom allocator to create a `Native-` collection type, its safety handle is added to the list of child safety handles of the custom allocator. When you rewind the allocator handle of a custom allocator, it invalidates and unregisters all its child allocators, and invalidates all its child safety handles. For `Native-` collection types, the disposal safety checks throw an exception if the allocator handle has rewound.

The following example method `UseCustomAllocator` shows how to use a custom allocator to create and allocate native containers:

[!code-cs[Use custom allocator to allocate memory](../Unity.Collections.Tests/AllocatorCustomTests.cs#allocator-custom-use)]

## Dispose a custom allocator

To dispose a custom allocator, the following must happen:

* The custom allocator must rewind its allocator handle which invalidates and unregisters all the allocator handle's child allocators, and invalidates all its child safety handles.
* You must unregister the allocator
* You must dispose the memory used to store the allocator.

Example method `DisposeCustomAllocator` in the user structure shows how to dispose a custom allocator.

[!code-cs[Dispose a custom allocator](../Unity.Collections.Tests/AllocatorCustomTests.cs#allocator-custom-dispose)]

## Full example of a custom allocator
The following is a full example of how to use a custom allocator:

[!code-cs[Add a custom allocator in user structure](../Unity.Collections.Tests/AllocatorCustomTests.cs#allocator-custom-user-struct)]

