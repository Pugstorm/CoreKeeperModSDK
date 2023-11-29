# Aliasing allocators

An **alias** is a collection which doesn't have its own allocation but instead shares the allocation of another collection, in whole or in part. For example, you can create an `UnsafeList` that doesn't allocate its own memory but instead uses a `NativeList`'s allocation. Writing to this shared memory via the `UnsafeList` affects the content of the `NativeList`, and vice versa.

You don't need to dispose aliases, and calling `Dispose` on an alias does nothing. Once an original is disposed, you can no longer use the aliases of the original:

[!code-cs[allocation_aliasing](../DocCodeSamples.Tests/CollectionsAllocationExamples.cs#allocation_aliasing)]

Aliasing is useful for the following situations:

* Getting a collection's data in the form of another collection type without copying the data. For example, you can create an `UnsafeList` that aliases a `NativeArray`.
* Getting a subrange of a collection's data without copying the data. For example, you can create an UnsafeList that aliases a subrange of another list or array.   
* [Array reinterpretation](#array-reinterpretation).

An `Unsafe-` collection can alias a `Native-` collection even though such cases undermine the safety checks. For example, if an `UnsafeList` aliases a `NativeList`, it's not safe to schedule a job that accesses one while also another job is scheduled that accesses the other, but the safety checks don't catch these cases.

## Array reinterpretation

A **reinterpretation** of an array is an alias of the array that reads and writes the content as a different element type. For example, a `NativeArray<int>` which reinterprets a `NativeArray<ushort>` shares the same bytes, but it reads and writes the bytes as an int instead of a ushort. This is because each int is 4 bytes while each ushort is 2 bytes. Each int corresponds to two ushorts, and the reinterpretation has half the length of the original.

[!code-cs[allocation_reinterpretation](../DocCodeSamples.Tests/CollectionsAllocationExamples.cs#allocation_reinterpretation)]

## Further information

* [Define a custom allocator](allocator-custom-define.md)
* [Rewindable allocators](allocator-rewindable.md)