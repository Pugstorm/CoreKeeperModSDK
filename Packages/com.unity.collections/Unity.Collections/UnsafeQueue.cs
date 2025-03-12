using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Collections.Generic;

namespace Unity.Collections
{
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct UnsafeQueueBlockHeader
    {
        public UnsafeQueueBlockHeader* m_NextBlock;
        public int m_NumItems;
    }

    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility]
    internal unsafe struct UnsafeQueueBlockPoolData
    {
        internal IntPtr m_FirstBlock;
        internal int m_NumBlocks;
        internal int m_MaxBlocks;
        internal const int m_BlockSize = 16 * 1024;
        internal int m_AllocLock;

        public UnsafeQueueBlockHeader* AllocateBlock()
        {
            // There can only ever be a single thread allocating an entry from the free list since it needs to
            // access the content of the block (the next pointer) before doing the CAS.
            // If there was no lock thread A could read the next pointer, thread B could quickly allocate
            // the same block then free it with another next pointer before thread A performs the CAS which
            // leads to an invalid free list potentially causing memory corruption.
            // Having multiple threads freeing data concurrently to each other while another thread is allocating
            // is no problems since there is only ever a single thread modifying global data in that case.
            while (Interlocked.CompareExchange(ref m_AllocLock, 1, 0) != 0)
            {
            }

            UnsafeQueueBlockHeader* checkBlock = (UnsafeQueueBlockHeader*)m_FirstBlock;
            UnsafeQueueBlockHeader* block;

            do
            {
                block = checkBlock;
                if (block == null)
                {
                    Interlocked.Exchange(ref m_AllocLock, 0);
                    Interlocked.Increment(ref m_NumBlocks);
                    block = (UnsafeQueueBlockHeader*)Memory.Unmanaged.Allocate(m_BlockSize, 16, Allocator.Persistent);
                    return block;
                }

                checkBlock = (UnsafeQueueBlockHeader*)Interlocked.CompareExchange(ref m_FirstBlock, (IntPtr)block->m_NextBlock, (IntPtr)block);
            }
            while (checkBlock != block);

            Interlocked.Exchange(ref m_AllocLock, 0);

            return block;
        }

        public void FreeBlock(UnsafeQueueBlockHeader* block)
        {
            if (m_NumBlocks > m_MaxBlocks)
            {
                if (Interlocked.Decrement(ref m_NumBlocks) + 1 > m_MaxBlocks)
                {
                    Memory.Unmanaged.Free(block, Allocator.Persistent);
                    return;
                }

                Interlocked.Increment(ref m_NumBlocks);
            }

            UnsafeQueueBlockHeader* checkBlock = (UnsafeQueueBlockHeader*)m_FirstBlock;
            UnsafeQueueBlockHeader* nextPtr;

            do
            {
                nextPtr = checkBlock;
                block->m_NextBlock = checkBlock;
                checkBlock = (UnsafeQueueBlockHeader*)Interlocked.CompareExchange(ref m_FirstBlock, (IntPtr)block, (IntPtr)checkBlock);
            }
            while (checkBlock != nextPtr);
        }
    }

    internal unsafe class UnsafeQueueBlockPool
    {
        static readonly SharedStatic<IntPtr> Data = SharedStatic<IntPtr>.GetOrCreate<UnsafeQueueBlockPool>();

        internal static UnsafeQueueBlockPoolData* GetQueueBlockPool()
        {
            var pData = (UnsafeQueueBlockPoolData**)Data.UnsafeDataPointer;
            var data = *pData;

            if (data == null)
            {
                data = (UnsafeQueueBlockPoolData*)Memory.Unmanaged.Allocate(UnsafeUtility.SizeOf<UnsafeQueueBlockPoolData>(), 8, Allocator.Persistent);
                *pData = data;
                data->m_NumBlocks = data->m_MaxBlocks = 256;
                data->m_AllocLock = 0;
                // Allocate MaxBlocks items
                UnsafeQueueBlockHeader* prev = null;

                for (int i = 0; i < data->m_MaxBlocks; ++i)
                {
                    UnsafeQueueBlockHeader* block = (UnsafeQueueBlockHeader*)Memory.Unmanaged.Allocate(UnsafeQueueBlockPoolData.m_BlockSize, 16, Allocator.Persistent);
                    block->m_NextBlock = prev;
                    prev = block;
                }
                data->m_FirstBlock = (IntPtr)prev;

                AppDomainOnDomainUnload();
            }
            return data;
        }

        [BurstDiscard]
        static void AppDomainOnDomainUnload()
        {
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
        }

        static void OnDomainUnload(object sender, EventArgs e)
        {
            var pData = (UnsafeQueueBlockPoolData**)Data.UnsafeDataPointer;
            var data = *pData;

            while (data->m_FirstBlock != IntPtr.Zero)
            {
                UnsafeQueueBlockHeader* block = (UnsafeQueueBlockHeader*)data->m_FirstBlock;
                data->m_FirstBlock = (IntPtr)block->m_NextBlock;
                Memory.Unmanaged.Free(block, Allocator.Persistent);
                --data->m_NumBlocks;
            }
            Memory.Unmanaged.Free(data, Allocator.Persistent);
            *pData = null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility]
    internal unsafe struct UnsafeQueueData
    {
        public IntPtr m_FirstBlock;
        public IntPtr m_LastBlock;
        public int m_MaxItems;
        public int m_CurrentRead;
        public byte* m_CurrentWriteBlockTLS;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal UnsafeQueueBlockHeader* GetCurrentWriteBlockTLS(int threadIndex)
        {
            var data = (UnsafeQueueBlockHeader**)&m_CurrentWriteBlockTLS[threadIndex * JobsUtility.CacheLineSize];
            return *data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetCurrentWriteBlockTLS(int threadIndex, UnsafeQueueBlockHeader* currentWriteBlock)
        {
            var data = (UnsafeQueueBlockHeader**)&m_CurrentWriteBlockTLS[threadIndex * JobsUtility.CacheLineSize];
            *data = currentWriteBlock;
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new [] { typeof(int) })]
        public static UnsafeQueueBlockHeader* AllocateWriteBlockMT<T>(UnsafeQueueData* data, UnsafeQueueBlockPoolData* pool, int threadIndex) where T : unmanaged
        {
            UnsafeQueueBlockHeader* currentWriteBlock = data->GetCurrentWriteBlockTLS(threadIndex);

            if (currentWriteBlock != null)
            {
                if (currentWriteBlock->m_NumItems != data->m_MaxItems)
                {
                    return currentWriteBlock;
                }
                currentWriteBlock = null;
            }

            currentWriteBlock = pool->AllocateBlock();
            currentWriteBlock->m_NextBlock = null;
            currentWriteBlock->m_NumItems = 0;
            UnsafeQueueBlockHeader* prevLast = (UnsafeQueueBlockHeader*)Interlocked.Exchange(ref data->m_LastBlock, (IntPtr)currentWriteBlock);

            if (prevLast == null)
            {
                data->m_FirstBlock = (IntPtr)currentWriteBlock;
            }
            else
            {
                prevLast->m_NextBlock = currentWriteBlock;
            }

            data->SetCurrentWriteBlockTLS(threadIndex, currentWriteBlock);
            return currentWriteBlock;
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new [] { typeof(int) })]
        public unsafe static void AllocateQueue<T>(AllocatorManager.AllocatorHandle label, out UnsafeQueueData* outBuf) where T : unmanaged
        {
#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif

            var queueDataSize = CollectionHelper.Align(UnsafeUtility.SizeOf<UnsafeQueueData>(), JobsUtility.CacheLineSize);

            var data = (UnsafeQueueData*)Memory.Unmanaged.Allocate(
                queueDataSize
                + JobsUtility.CacheLineSize * maxThreadCount
                , JobsUtility.CacheLineSize
                , label
            );

            data->m_CurrentWriteBlockTLS = (((byte*)data) + queueDataSize);

            data->m_FirstBlock = IntPtr.Zero;
            data->m_LastBlock = IntPtr.Zero;
            data->m_MaxItems = (UnsafeQueueBlockPoolData.m_BlockSize - UnsafeUtility.SizeOf<UnsafeQueueBlockHeader>()) / UnsafeUtility.SizeOf<T>();

            data->m_CurrentRead = 0;
            for (int threadIndex = 0; threadIndex < maxThreadCount; ++threadIndex)
            {
                data->SetCurrentWriteBlockTLS(threadIndex, null);
            }

            outBuf = data;
        }

        public unsafe static void DeallocateQueue(UnsafeQueueData* data, UnsafeQueueBlockPoolData* pool, AllocatorManager.AllocatorHandle allocation)
        {
            UnsafeQueueBlockHeader* firstBlock = (UnsafeQueueBlockHeader*)data->m_FirstBlock;

            while (firstBlock != null)
            {
                UnsafeQueueBlockHeader* next = firstBlock->m_NextBlock;
                pool->FreeBlock(firstBlock);
                firstBlock = next;
            }

            Memory.Unmanaged.Free(data, allocation);
        }
    }

    /// <summary>
    /// An unmanaged queue.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new [] { typeof(int) })]
    public unsafe struct UnsafeQueue<T>
        : INativeDisposable
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeQueueData* m_Buffer;
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeQueueBlockPoolData* m_QueuePool;
        internal AllocatorManager.AllocatorHandle m_AllocatorLabel;

        /// <summary>
        /// Initializes and returns an instance of UnsafeQueue.
        /// </summary>
        /// <param name="allocator">The allocator to use.</param>
        public UnsafeQueue(AllocatorManager.AllocatorHandle allocator)
        {
            m_QueuePool = UnsafeQueueBlockPool.GetQueueBlockPool();
            m_AllocatorLabel = allocator;
            UnsafeQueueData.AllocateQueue<T>(allocator, out m_Buffer);
        }

        internal static UnsafeQueue<T>* Alloc(AllocatorManager.AllocatorHandle allocator)
        {
            UnsafeQueue<T>* data = (UnsafeQueue<T>*)Memory.Unmanaged.Allocate(sizeof(UnsafeQueue<T>), UnsafeUtility.AlignOf<UnsafeQueue<T>>(), allocator);
            return data;
        }

        internal static void Free(UnsafeQueue<T>* data)
        {
            if (data == null)
            {
                throw new InvalidOperationException("UnsafeQueue has yet to be created or has been destroyed!");
            }
            var allocator = data->m_AllocatorLabel;
            data->Dispose();
            Memory.Unmanaged.Free(data, allocator);
        }

        /// <summary>
        /// Returns true if this queue is empty.
        /// </summary>
        /// <returns>True if this queue has no items or if the queue has not been constructed.</returns>
        public readonly bool IsEmpty()
        {
            if (IsCreated)
            {
                int count = 0;
                var currentRead = m_Buffer->m_CurrentRead;

                for (UnsafeQueueBlockHeader* block = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock
                        ; block != null
                        ; block = block->m_NextBlock
                )
                {
                    count += block->m_NumItems;

                    if (count > currentRead)
                    {
                        return false;
                    }
                }

                return count == currentRead;
            }
            return true;
        }

        /// <summary>
        /// Returns the current number of elements in this queue.
        /// </summary>
        /// <remarks>Note that getting the count requires traversing the queue's internal linked list of blocks.
        /// Where possible, cache this value instead of reading the property repeatedly.</remarks>
        /// <returns>The current number of elements in this queue.</returns>
        public readonly int Count
        {
            get
            {
                int count = 0;

                for (UnsafeQueueBlockHeader* block = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock
                     ; block != null
                     ; block = block->m_NextBlock
                )
                {
                    count += block->m_NumItems;
                }

                return count - m_Buffer->m_CurrentRead;
            }
        }

        internal static int PersistentMemoryBlockCount
        {
            get { return UnsafeQueueBlockPool.GetQueueBlockPool()->m_MaxBlocks; }
            set { Interlocked.Exchange(ref UnsafeQueueBlockPool.GetQueueBlockPool()->m_MaxBlocks, value); }
        }

        internal static int MemoryBlockSize
        {
            get { return UnsafeQueueBlockPoolData.m_BlockSize; }
        }

        /// <summary>
        /// Returns the element at the end of this queue without removing it.
        /// </summary>
        /// <returns>The element at the end of this queue.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek()
        {
            CheckNotEmpty();

            UnsafeQueueBlockHeader* firstBlock = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock;
            return UnsafeUtility.ReadArrayElement<T>(firstBlock + 1, m_Buffer->m_CurrentRead);
        }

        /// <summary>
        /// Adds an element at the front of this queue.
        /// </summary>
        /// <param name="value">The value to be enqueued.</param>
        public void Enqueue(T value)
        {
            UnsafeQueueBlockHeader* writeBlock = UnsafeQueueData.AllocateWriteBlockMT<T>(m_Buffer, m_QueuePool, 0);
            UnsafeUtility.WriteArrayElement(writeBlock + 1, writeBlock->m_NumItems, value);
            ++writeBlock->m_NumItems;
        }

        /// <summary>
        /// Removes and returns the element at the end of this queue.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if this queue is empty.</exception>
        /// <returns>The element at the end of this queue.</returns>
        public T Dequeue()
        {
            if (!TryDequeue(out T item))
            {
                ThrowEmpty();
            }

            return item;
        }

        /// <summary>
        /// Removes and outputs the element at the end of this queue.
        /// </summary>
        /// <param name="item">Outputs the removed element.</param>
        /// <returns>True if this queue was not empty.</returns>
        public bool TryDequeue(out T item)
        {
            UnsafeQueueBlockHeader* firstBlock = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock;

            if (firstBlock != null)
            {
                var currentRead = m_Buffer->m_CurrentRead++;
                var numItems = firstBlock->m_NumItems;
                item = UnsafeUtility.ReadArrayElement<T>(firstBlock + 1, currentRead);

                if (currentRead + 1 >= numItems)
                {
                    m_Buffer->m_CurrentRead = 0;
                    m_Buffer->m_FirstBlock = (IntPtr)firstBlock->m_NextBlock;

                    if (m_Buffer->m_FirstBlock == IntPtr.Zero)
                    {
                        m_Buffer->m_LastBlock = IntPtr.Zero;
                    }

#if UNITY_2022_2_14F1_OR_NEWER
                    int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
                    int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
                    for (int threadIndex = 0; threadIndex < maxThreadCount; ++threadIndex)
                    {
                        if (m_Buffer->GetCurrentWriteBlockTLS(threadIndex) == firstBlock)
                        {
                            m_Buffer->SetCurrentWriteBlockTLS(threadIndex, null);
                        }
                    }

                    m_QueuePool->FreeBlock(firstBlock);
                }
                return true;
            }

            item = default(T);
            return false;
        }

        /// <summary>
        /// Returns an array containing a copy of this queue's content.
        /// </summary>
        /// <param name="allocator">The allocator to use.</param>
        /// <returns>An array containing a copy of this queue's content. The elements are ordered in the same order they were
        /// enqueued, *e.g.* the earliest enqueued element is copied to index 0 of the array.</returns>
        public NativeArray<T> ToArray(AllocatorManager.AllocatorHandle allocator)
        {
            UnsafeQueueBlockHeader* firstBlock = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock;
            var outputArray = CollectionHelper.CreateNativeArray<T>(Count, allocator, NativeArrayOptions.UninitializedMemory);

            UnsafeQueueBlockHeader* currentBlock = firstBlock;
            var arrayPtr = (byte*)outputArray.GetUnsafePtr();
            int size = UnsafeUtility.SizeOf<T>();
            int dstOffset = 0;
            int srcOffset = m_Buffer->m_CurrentRead * size;
            int srcOffsetElements = m_Buffer->m_CurrentRead;
            while (currentBlock != null)
            {
                int bytesToCopy = (currentBlock->m_NumItems - srcOffsetElements) * size;
                UnsafeUtility.MemCpy(arrayPtr + dstOffset, (byte*)(currentBlock + 1) + srcOffset, bytesToCopy);
                srcOffset = srcOffsetElements = 0;
                dstOffset += bytesToCopy;
                currentBlock = currentBlock->m_NextBlock;
            }

            return outputArray;
        }

        /// <summary>
        /// Removes all elements of this queue.
        /// </summary>
        public void Clear()
        {
            UnsafeQueueBlockHeader* firstBlock = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock;

            while (firstBlock != null)
            {
                UnsafeQueueBlockHeader* next = firstBlock->m_NextBlock;
                m_QueuePool->FreeBlock(firstBlock);
                firstBlock = next;
            }

            m_Buffer->m_FirstBlock = IntPtr.Zero;
            m_Buffer->m_LastBlock = IntPtr.Zero;
            m_Buffer->m_CurrentRead = 0;

#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            for (int threadIndex = 0; threadIndex < maxThreadCount; ++threadIndex)
            {
                m_Buffer->SetCurrentWriteBlockTLS(threadIndex, null);
            }
        }

        /// <summary>
        /// Whether this queue has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this queue has been allocated (and not yet deallocated).</value>
        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Buffer != null;
        }

        /// <summary>
        /// Releases all resources (memory and safety handles).
        /// </summary>
        public void Dispose()
        {
            if (!IsCreated)
            {
                return;
            }

            UnsafeQueueData.DeallocateQueue(m_Buffer, m_QueuePool, m_AllocatorLabel);
            m_Buffer = null;
            m_QueuePool = null;
        }

        /// <summary>
        /// Creates and schedules a job that releases all resources (memory and safety handles) of this queue.
        /// </summary>
        /// <param name="inputDeps">The dependency for the new job.</param>
        /// <returns>The handle of the new job. The job depends upon `inputDeps` and releases all resources (memory and safety handles) of this queue.</returns>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (!IsCreated)
            {
                return inputDeps;
            }

            var jobHandle = new UnsafeQueueDisposeJob { Data = new UnsafeQueueDispose { m_Buffer = m_Buffer, m_QueuePool = m_QueuePool, m_AllocatorLabel = m_AllocatorLabel }  }.Schedule(inputDeps);
            m_Buffer = null;
            m_QueuePool = null;

            return jobHandle;
        }

        /// <summary>
        /// An enumerator over the values of a container.
        /// </summary>
        /// <remarks>
        /// In an enumerator's initial state, <see cref="Current"/> is invalid.
        /// The first <see cref="MoveNext"/> call advances the enumerator to the first value.
        /// </remarks>
        public struct Enumerator : IEnumerator<T>
        {
            [NativeDisableUnsafePtrRestriction]
            internal UnsafeQueueBlockHeader* m_FirstBlock;

            [NativeDisableUnsafePtrRestriction]
            internal UnsafeQueueBlockHeader* m_Block;

            internal int m_Index;
            T value;

            /// <summary>
            /// Does nothing.
            /// </summary>
            public void Dispose() { }

            /// <summary>
            /// Advances the enumerator to the next value.
            /// </summary>
            /// <returns>True if `Current` is valid to read after the call.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                m_Index++;

                for (; m_Block != null
                     ; m_Block = m_Block->m_NextBlock
                )
                {
                    var numItems = m_Block->m_NumItems;

                    if (m_Index < numItems)
                    {
                        value = UnsafeUtility.ReadArrayElement<T>(m_Block + 1, m_Index);
                        return true;
                    }

                    m_Index -= numItems;
                }

                value = default;
                return false;
            }

            /// <summary>
            /// Resets the enumerator to its initial state.
            /// </summary>
            public void Reset()
            {
                m_Block = m_FirstBlock;
                m_Index = -1;
            }

            /// <summary>
            /// The current value.
            /// </summary>
            /// <value>The current value.</value>
            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => value;
            }

            object IEnumerator.Current => Current;
        }

        /// <summary>
        /// Returns a readonly version of this UnsafeQueue instance.
        /// </summary>
        /// <remarks>ReadOnly containers point to the same underlying data as the UnsafeQueue it is made from.</remarks>
        /// <returns>ReadOnly instance for this.</returns>
        public ReadOnly AsReadOnly()
        {
            return new ReadOnly(ref this);
        }

        /// <summary>
        /// A read-only alias for the value of a UnsafeQueue. Does not have its own allocated storage.
        /// </summary>
        public struct ReadOnly
            : IEnumerable<T>
        {
            [NativeDisableUnsafePtrRestriction]
            UnsafeQueueData* m_Buffer;

            internal ReadOnly(ref UnsafeQueue<T> data)
            {
                m_Buffer = data.m_Buffer;
            }

            /// <summary>
            /// Whether this container been allocated (and not yet deallocated).
            /// </summary>
            /// <value>True if this container has been allocated (and not yet deallocated).</value>
            public readonly bool IsCreated
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return m_Buffer != null;
                }
            }

            /// <summary>
            /// Returns true if this queue is empty.
            /// </summary>
            /// <remarks>Note that getting the count requires traversing the queue's internal linked list of blocks.
            /// Where possible, cache this value instead of reading the property repeatedly.</remarks>
            /// <returns>True if this queue has no items or if the queue has not been constructed.</returns>
            public readonly bool IsEmpty()
            {
                int count = 0;
                var currentRead = m_Buffer->m_CurrentRead;

                for (UnsafeQueueBlockHeader* block = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock
                        ; block != null
                        ; block = block->m_NextBlock
                )
                {
                    count += block->m_NumItems;

                    if (count > currentRead)
                    {
                        return false;
                    }
                }

                return count == currentRead;
            }

            /// <summary>
            /// Returns the current number of elements in this queue.
            /// </summary>
            /// <remarks>Note that getting the count requires traversing the queue's internal linked list of blocks.
            /// Where possible, cache this value instead of reading the property repeatedly.</remarks>
            /// <returns>The current number of elements in this queue.</returns>
            public readonly int Count
            {
                get
                {
                    int count = 0;

                    for (UnsafeQueueBlockHeader* block = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock
                         ; block != null
                         ; block = block->m_NextBlock
                    )
                    {
                        count += block->m_NumItems;
                    }

                    return count - m_Buffer->m_CurrentRead;
                }
            }

            /// <summary>
            /// The element at an index.
            /// </summary>
            /// <param name="index">An index.</param>
            /// <value>The element at the index.</value>
            /// <exception cref="IndexOutOfRangeException">Thrown if the index is out of bounds.</exception>
            public readonly T this[int index]
            {
                get
                {
                    T result;
                    if (!TryGetValue(index, out result))
                    {
                        ThrowIndexOutOfRangeException(index);
                    }

                    return result;
                }
            }

            readonly bool TryGetValue(int index, out T item)
            {
                if (index >= 0)
                {
                    var idx = index;

                    for (UnsafeQueueBlockHeader* block = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock
                         ; block != null
                         ; block = block->m_NextBlock
                    )
                    {
                        var numItems = block->m_NumItems;

                        if (idx < numItems)
                        {
                            item = UnsafeUtility.ReadArrayElement<T>(block + 1, idx);
                            return true;
                        }

                        idx -= numItems;
                    }
                }

                item = default;
                return false;
            }

            /// <summary>
            /// Returns an enumerator over the items of this container.
            /// </summary>
            /// <returns>An enumerator over the items of this container.</returns>
            public readonly Enumerator GetEnumerator()
            {
                return new Enumerator
                {
                    m_FirstBlock = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock,
                    m_Block = (UnsafeQueueBlockHeader*)m_Buffer->m_FirstBlock,
                    m_Index = -1,
                };
            }

            /// <summary>
            /// This method is not implemented. Use <see cref="GetEnumerator"/> instead.
            /// </summary>
            /// <returns>Throws NotImplementedException.</returns>
            /// <exception cref="NotImplementedException">Method is not implemented.</exception>
            IEnumerator<T> IEnumerable<T>.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// This method is not implemented. Use <see cref="GetEnumerator"/> instead.
            /// </summary>
            /// <returns>Throws NotImplementedException.</returns>
            /// <exception cref="NotImplementedException">Method is not implemented.</exception>
            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly void ThrowIndexOutOfRangeException(int index)
            {
                throw new IndexOutOfRangeException($"Index {index} is out of bounds [0-{Count}].");
            }
        }

        /// <summary>
        /// Returns a parallel writer for this queue.
        /// </summary>
        /// <returns>A parallel writer for this queue.</returns>
        public ParallelWriter AsParallelWriter()
        {
            ParallelWriter writer;

            writer.m_Buffer = m_Buffer;
            writer.m_QueuePool = m_QueuePool;
            writer.m_ThreadIndex = 0;

            return writer;
        }

        /// <summary>
        /// A parallel writer for a UnsafeQueue.
        /// </summary>
        /// <remarks>
        /// Use <see cref="AsParallelWriter"/> to create a parallel writer for a UnsafeQueue.
        /// </remarks>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new [] { typeof(int) })]
        public unsafe struct ParallelWriter
        {
            [NativeDisableUnsafePtrRestriction]
            internal UnsafeQueueData* m_Buffer;

            [NativeDisableUnsafePtrRestriction]
            internal UnsafeQueueBlockPoolData* m_QueuePool;

            [NativeSetThreadIndex]
            internal int m_ThreadIndex;

            /// <summary>
            /// Adds an element at the front of the queue.
            /// </summary>
            /// <param name="value">The value to be enqueued.</param>
            public void Enqueue(T value)
            {
                UnsafeQueueBlockHeader* writeBlock = UnsafeQueueData.AllocateWriteBlockMT<T>(m_Buffer, m_QueuePool, m_ThreadIndex);
                UnsafeUtility.WriteArrayElement(writeBlock + 1, writeBlock->m_NumItems, value);
                ++writeBlock->m_NumItems;
            }

            /// <summary>
            /// Adds an element at the front of the queue.
            /// </summary>
            /// <param name="value">The value to be enqueued.</param>
            /// <param name="threadIndexOverride">The thread index which must be set by a field from a job struct with the <see cref="NativeSetThreadIndexAttribute"/> attribute.</param>
            internal void Enqueue(T value, int threadIndexOverride)
            {
                UnsafeQueueBlockHeader* writeBlock = UnsafeQueueData.AllocateWriteBlockMT<T>(m_Buffer, m_QueuePool, threadIndexOverride);
                UnsafeUtility.WriteArrayElement(writeBlock + 1, writeBlock->m_NumItems, value);
                ++writeBlock->m_NumItems;
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CheckNotEmpty()
        {
            if (m_Buffer->m_FirstBlock == (IntPtr)0)
            {
                ThrowEmpty();
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        static void ThrowEmpty()
        {
            throw new InvalidOperationException("Trying to read from an empty queue.");
        }
    }

    [GenerateTestsForBurstCompatibility]
    internal unsafe struct UnsafeQueueDispose
    {
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeQueueData* m_Buffer;

        [NativeDisableUnsafePtrRestriction]
        internal UnsafeQueueBlockPoolData* m_QueuePool;

        internal AllocatorManager.AllocatorHandle m_AllocatorLabel;

        public void Dispose()
        {
            UnsafeQueueData.DeallocateQueue(m_Buffer, m_QueuePool, m_AllocatorLabel);
        }
    }

    [BurstCompile]
    struct UnsafeQueueDisposeJob : IJob
    {
        public UnsafeQueueDispose Data;

        public void Execute()
        {
            Data.Dispose();
        }
    }
}
