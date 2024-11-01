using System;
using Unity.Collections;
using Unity.Entities.Serialization;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Entities.LowLevel;
using SpinLock = Unity.Entities.LowLevel.SpinLock;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Loading status for requests.
    /// </summary>
    public enum ObjectLoadingStatus
    {
        /// <summary>
        /// The requested runtime id was not found and has not started loading.
        /// </summary>
        None,
        /// <summary>
        /// The requested id has enterd the queue to be loaded.
        /// </summary>
        Queued,
        /// <summary>
        /// The requested runtime id has started loading, but is still active.
        /// </summary>
        Loading,
        /// <summary>
        /// The requested runtime id has completed loading successfully.
        /// </summary>
        Completed,
        /// <summary>
        /// There was an error encountered when attempting to load.
        /// </summary>
        Error
    }

    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile]
    unsafe struct ObjectValueCache : IDisposable
    {
        struct ObjectCacheEntry : IDisposable
        {
            public ObjectLoadingStatus LoadingStatus;
            public GCHandle ObjectHandle;

            public void Dispose()
            {
                if (ObjectHandle.IsAllocated)
                    ObjectHandle.Free();
            }
        }
        SpinLock spinLock;
        UnsafeHashMap<UntypedWeakReferenceId, ObjectCacheEntry> Values;

        public bool IsCreated => Values.IsCreated;

        public ObjectValueCache(int initialCapacity)
        {
            spinLock = new SpinLock();
            Values = new UnsafeHashMap<UntypedWeakReferenceId, ObjectCacheEntry>(initialCapacity, Allocator.Persistent);
        }

        public ObjectLoadingStatus GetLoadingStatus(in UntypedWeakReferenceId id)
        {
            ObjectLoadingStatus val = ObjectLoadingStatus.None;
            spinLock.Acquire();
            if (Values.TryGetValue(id, out var entry))
                val = entry.LoadingStatus;
            spinLock.Release();
            return val;
        }

        public bool GetObjectHandle(in UntypedWeakReferenceId id, ref GCHandle objHandle)
        {
            spinLock.Acquire();
            if (Values.TryGetValue(id, out var entry))
                objHandle = entry.ObjectHandle;
            spinLock.Release();
            return objHandle.IsAllocated;
        }

        public void Dispose()
        {
            spinLock.Acquire();
            foreach (var e in Values)
                e.Value.Dispose();
            Values.Dispose();
            spinLock.Release();
        }

        internal void AddEntry(in UntypedWeakReferenceId objectId)
        {
            spinLock.Acquire();
            Values.TryAdd(objectId, new ObjectCacheEntry { LoadingStatus = ObjectLoadingStatus.Queued });
            spinLock.Release();
        }

        unsafe internal void AddEntries(UntypedWeakReferenceId *objectIds, int count)
        {
            spinLock.Acquire();
            if (Values.Capacity - Values.Count < count)
                Values.Capacity = Values.Count + count;
            for(int i = 0; i < count; i++)
                Values.TryAdd(objectIds[i], new ObjectCacheEntry { LoadingStatus = ObjectLoadingStatus.Queued });
            spinLock.Release();
        }

        internal void SetObjectStatus(in UntypedWeakReferenceId objectId, ObjectLoadingStatus status, in GCHandle objHandle)
        {
            spinLock.Acquire();
            Values[objectId] = new ObjectCacheEntry { LoadingStatus = status, ObjectHandle = objHandle };
            spinLock.Release();
        }

        internal bool RemoveEntry(in UntypedWeakReferenceId objectId, out ObjectLoadingStatus status)
        {
            bool hasEntry = false;
            status = default;
            spinLock.Acquire();
            if (Values.TryGetValue(objectId, out var entry))
            {
                status = entry.LoadingStatus;
                Values.Remove(objectId);
                entry.Dispose();
                hasEntry = true;
            }
            spinLock.Release();
            return hasEntry;
        }

        internal int Count()
        {
            var count = 0;
            spinLock.Acquire();
            count = Values.Count;
            spinLock.Release();
            return count;
        }
    }



    [BurstCompile]
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct MultiProducerSingleBulkConsumerQueue<T> : IDisposable where T : unmanaged
    {
        SpinLock spinLock;
        UnsafeRingQueue<T> Values;

        public int Length
        {
            get
            {
                spinLock.Acquire();
                var length = Values.Length;
                spinLock.Release();
                return length;
            }
        }
        public bool IsCreated => Values.IsCreated;
        public MultiProducerSingleBulkConsumerQueue(int initialQueueSize)
        {
            spinLock = new SpinLock();
            Values = new UnsafeRingQueue<T>(initialQueueSize, Allocator.Persistent);
        }

        unsafe public void Produce(T *vals, int count)
        {
            spinLock.Acquire();
            if (Values.Capacity - Values.Length < count)
            {
                var resized = new UnsafeRingQueue<T>(Values.Length + count, Allocator.Persistent);
                while (Values.TryDequeue(out var id))
                    resized.Enqueue(id);
                Values.Dispose();
                Values = resized;
            }
            for (int i = 0; i < count; i++)
                Values.TryEnqueue(vals[i]);
            spinLock.Release();
        }


        public void Produce(in T val)
        {
            spinLock.Acquire();
            if (!Values.TryEnqueue(val))
            {
                var resized = new UnsafeRingQueue<T>(Values.Capacity * 2, Allocator.Persistent);
                while (Values.TryDequeue(out var id))
                    resized.Enqueue(id);
                Values.Dispose();
                Values = resized;
                Values.TryEnqueue(val);
            }
            spinLock.Release();
        }

        public unsafe bool ConsumeAll(out NativeArray<T> newIdContainer, AllocatorManager.AllocatorHandle allocator)
        {
            newIdContainer = default;
            spinLock.Acquire();
            if (!Values.IsEmpty)
            {
                // Todo: When NativeArray supports custom allocators, remove these .ToAllocator callsites DOTS-7695
                newIdContainer = new NativeArray<T>(Values.Length, allocator.ToAllocator);
                UnsafeUtility.MemCpy(newIdContainer.GetUnsafePtr(), Values.Ptr, sizeof(T) * Values.Length);
                var size = Values.Capacity;
                Values.Dispose();
                Values = new UnsafeRingQueue<T>(size, Allocator.Persistent);
            }
            spinLock.Release();
            return newIdContainer.IsCreated;
        }

        public void Dispose()
        {
            spinLock.Acquire();
            Values.Dispose();
            spinLock.Release();
        }
    }
}
