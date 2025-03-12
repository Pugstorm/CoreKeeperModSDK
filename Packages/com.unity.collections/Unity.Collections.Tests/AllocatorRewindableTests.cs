#region allocator-rewindable-example
using System;
using NUnit.Framework;
using Unity.Collections;

// This is the example code used in
// Packages/com.unity.collections/Documentation~/allocator/allocator-rewindable.md
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

    // Constructor of user structure
    public ExampleStruct(int initialBlockSize)
    {
        this = default;
        CreateRewindableAllocator(Allocator.Persistent, initialBlockSize, false);
    }

    // Dispose the user structure
    public void Dispose()
    {
        DisposeRewindableAllocator();
    }

    #region allocator-rewindable-use
    // Sample code to use rewindable allocator to allocate containers
    public unsafe void UseRewindableAllocator(out NativeArray<int> nativeArray, out NativeList<int> nativeList, out byte* bytePtr)
    {
        // Use rewindable allocator to allocate a native array, no need to dispose the array manually
        // CollectionHelper is required to create/allocate native array from a custom allocator.
        nativeArray = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(100, ref RwdAllocator);
        nativeArray[0] = 0xFE;

        // Use rewindable allocator to allocate a native list, do not need to dispose the list manually
        nativeList = new NativeList<int>(RwdAllocator.Handle);
        for (int i = 0; i < 50; i++)
        {
            nativeList.Add(i);
        }

        // Use custom allocator to allocate a byte buffer.
        bytePtr = (byte*)AllocatorManager.Allocate(ref RwdAllocator, sizeof(byte), sizeof(byte), 10);
        bytePtr[0] = 0xAB;
    }
    #endregion // allocator-rewindable-use

    #region allocator-rewindable-free
    // Free all allocations from the rewindable allocator
    public void FreeRewindableAllocator()
    {
        RwdAllocator.Rewind();
    }
    #endregion // allocator-rewindable-free

    #region allocator-rewindable-dispose
    // Dispose the rewindable allocator
    void DisposeRewindableAllocator()
    {
        // Dispose all the memory blocks in the rewindable allocator
        RwdAllocator.Dispose();
        // Unregister the rewindable allocator and dispose it
        rwdAllocatorHelper.Dispose();
    }
    #endregion // allocator-rewindable-dispose
}
internal class ExampleStructSampleUsage
{
    // Initial block size of the rewindable allocator.
    const int IntialBlockSize = 128 * 1024;

    [Test]
    public unsafe void UseRewindableAllocator_Works()
    {
        ExampleStruct exampleStruct = new ExampleStruct(IntialBlockSize);

        // Allocate native array and native list from rewindable allocator
        exampleStruct.UseRewindableAllocator(out NativeArray<int> nativeArray, out NativeList<int> nativeList, out byte* bytePtr);

        // Still able to access the native array, native list and byte buffer
        Assert.AreEqual(nativeArray[0], 0xFE);
        Assert.AreEqual(nativeList[10], 10);
        Assert.AreEqual(bytePtr[0], 0xAB);


        // Free all memories allocated from the rewindable allocator
        // No need to dispose the native array and native list
        exampleStruct.FreeRewindableAllocator();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // Object disposed exception throws because nativeArray is already disposed
        Assert.Throws<ObjectDisposedException>(() =>
        {
            nativeArray[0] = 0xEF;
        });

        // Object disposed exception throws because nativeList is already disposed
        Assert.Throws<ObjectDisposedException>(() =>
        {
            nativeList[10] = 0x10;
        });
#endif

        // Dispose the user structure
        exampleStruct.Dispose();
    }
}
#endregion // allocator-rewindable-example
