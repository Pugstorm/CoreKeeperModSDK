#region allocator-custom-example
using System;
using AOT;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

// This is the example code used in
// Packages/com.unity.collections/Documentation~/allocator/allocator-custom.md
// Example custom allocator.  The allocator is able to allocate memory from Allocator.Persistant,
// if successful, initialize the allocated memory with a user configured value and increment an
// allocation count.  The allocator is able to deallocate the memory, if successful, decrement
// the allocation count.
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

    #region allocator-custom-try
    // Value to initialize the allocated memory
    byte m_initialValue;

    // Allocation count
    int m_allocationCount;

    // Implement the Try method required by IAllocator interface
    public unsafe int Try(ref AllocatorManager.Block block)
    {
        // Error status
        int error = 0;

        // Allocate
        if (block.Range.Pointer == IntPtr.Zero)
        {
            // Allocate memory from Allocator.Persistant and restore the original allocator 
            AllocatorManager.AllocatorHandle tempAllocator = block.Range.Allocator;
            block.Range.Allocator = Allocator.Persistent;
            error = AllocatorManager.Try(ref block);
            block.Range.Allocator = tempAllocator;

            // return if error occurs
            if (error != 0)
                return error;

            // if allocation succeeds, intialize the memory with the initial value and increment the allocation count
            if (block.Range.Pointer != IntPtr.Zero)
            {
                UnsafeUtility.MemSet((void*)block.Range.Pointer, m_initialValue, block.Bytes);
                m_allocationCount++;

            }
            return 0;
        }
        // Deallocate
        else
        {
            // Deallocate memory from Allocator.Persistant and restore the original allocator 
            AllocatorManager.AllocatorHandle tempAllocator = block.Range.Allocator;
            block.Range.Allocator = Allocator.Persistent;
            error = AllocatorManager.Try(ref block);
            block.Range.Allocator = tempAllocator;

            // return if error occurs
            if (error != 0)
                return error;

            // if deallocation succeeds, decrement the allocation count
            if (block.Range.Pointer == IntPtr.Zero)
            {
                m_allocationCount--;
            }

            return 0;
        }
    }

    #endregion // allocator-custom-try

    #region allocator-custom-allocator-function
    // Implement the allocator function of delegate AllocatorManager.TryFunction that is
    // required when register the allocator on the global allocator table
    [BurstCompile(CompileSynchronously = true)]
    [MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
    public static unsafe int AllocatorFunction(IntPtr customAllocatorPtr, ref AllocatorManager.Block block)
    {
        return ((ExampleCustomAllocator*)customAllocatorPtr)->Try(ref block);
    }

    #endregion // allocator-custom-allocator-function

    // Property to get the initial value
    public byte InitialValue => m_initialValue;

    // Property to get the allocation count
    public int AllocationCount => m_allocationCount;

    // Initialize the allocator
    public void Initialize(byte initialValue)
    {
        m_initialValue = initialValue;
        m_allocationCount = 0;
    }
}

#endregion // allocator-custom-example

#region allocator-custom-user-struct
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

    #region allocator-custom-dispose
    // Dispose the custom allocator
    void DisposeCustomAllocator()
    {
        // Dispose the custom allocator
        customAllocator.Dispose();

        // Unregister the custom allocator and dispose it
        customAllocatorHelper.Dispose();
    }
    #endregion // allocator-custom-dispose

    // Constructor of user structure
    public ExampleCustomAllocatorStruct(byte initialValue)
    {
        this = default;
        CreateCustomAllocator(Allocator.Persistent, initialValue);
    }

    // Dispose the user structure
    public void Dispose()
    {
        DisposeCustomAllocator();
    }

    #region allocator-custom-use
    // Sample code to use the custom allocator to allocate containers
    public void UseCustomAllocator(out NativeArray<int> nativeArray, out NativeList<int> nativeList)
    {
        // Use custom allocator to allocate a native array and check initial value.
        nativeArray = CollectionHelper.CreateNativeArray<int, ExampleCustomAllocator>(100, ref customAllocator, NativeArrayOptions.UninitializedMemory);
        Assert.AreEqual(customAllocator.InitialValue, (byte)nativeArray[0] & 0xFF);
        nativeArray[0] = 0xFE;

        // Use custom allocator to allocate a native list and check initial value.
        nativeList = new NativeList<int>(customAllocator.Handle);
        for (int i = 0; i < 50; i++)
        {
            nativeList.Add(i);
        }

        unsafe
        {
            // Use custom allocator to allocate a byte buffer.
            byte* bytePtr = (byte*)AllocatorManager.Allocate(ref customAllocator, sizeof(byte), sizeof(byte), 10);
            Assert.AreEqual(customAllocator.InitialValue, bytePtr[0]);

            // Free the byte buffer.
            AllocatorManager.Free(customAllocator.ToAllocator, bytePtr, 10);
        }
    }
    #endregion // allocator-custom-use

    // Get allocation count from the custom allocator
    public int AllocationCount => customAllocator.AllocationCount;

    public void UseCustomAllocatorHandle(out NativeArray<int> nativeArray, out NativeList<int> nativeList)
    {
        // Use custom allocator to allocate a native array and check initial value.
        nativeArray = CollectionHelper.CreateNativeArray<int>(100, customAllocator.ToAllocator, NativeArrayOptions.UninitializedMemory);
        Assert.AreEqual(customAllocator.InitialValue, (byte)nativeArray[0] & 0xFF);
        nativeArray[0] = 0xFE;

        // Use custom allocator to allocate a native list and check initial value.
        nativeList = new NativeList<int>(customAllocator.Handle);
        for (int i = 0; i < 50; i++)
        {
            nativeList.Add(i);
        }

        unsafe
        {
            // Use custom allocator to allocate a byte buffer.
            byte* bytePtr = (byte*)AllocatorManager.Allocate(ref customAllocator, sizeof(byte), sizeof(byte), 10);
            Assert.AreEqual(customAllocator.InitialValue, bytePtr[0]);

            // Free the byte buffer.
            AllocatorManager.Free(customAllocator.ToAllocator, bytePtr, 10);
        }
    }
}

internal class ExampleCustomAllocatorStructUsage
{
    // Initial value for the custom allocator.
    const int IntialValue = 0xAB;

    // Test code.
    [Test]
    public void UseCustomAllocator_Works()
    {
        ExampleCustomAllocatorStruct exampleStruct = new ExampleCustomAllocatorStruct(IntialValue);

        // Allocate native array and native list from the custom allocator
        exampleStruct.UseCustomAllocator(out NativeArray<int> nativeArray, out NativeList<int> nativeList);

        // Able to access the native array and native list
        Assert.AreEqual(nativeArray[0], 0xFE);
        Assert.AreEqual(nativeList[10], 10);

        // Need to use CollectionHelper.DisposeNativeArray to dispose the native array from a custom allocator
        CollectionHelper.Dispose(nativeArray) ;
        // Dispose the native list
        nativeList.Dispose();

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

        // Check allocation count after dispose the native array and native list
        Assert.AreEqual(0, exampleStruct.AllocationCount);

        // Dispose the user structure
        exampleStruct.Dispose();
    }

    [Test]
    public void UseCustomAllocatorHandle_Works()
    {
        ExampleCustomAllocatorStruct exampleStruct = new ExampleCustomAllocatorStruct(IntialValue);

        // Allocate native array and native list from the custom allocator handle
        exampleStruct.UseCustomAllocatorHandle(out NativeArray<int> nativeArray, out NativeList<int> nativeList);

        // Able to access the native array and native list
        Assert.AreEqual(nativeArray[0], 0xFE);
        Assert.AreEqual(nativeList[10], 10);

        // Need to use CollectionHelper.DisposeNativeArray to dispose the native array from a custom allocator
        CollectionHelper.Dispose(nativeArray);
        // Dispose the native list
        nativeList.Dispose();

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

        // Check allocation count after dispose the native array and native list
        Assert.AreEqual(0, exampleStruct.AllocationCount);

        // Dispose the user structure
        exampleStruct.Dispose();
    }
}
#endregion // allocator-custom-user-struct


