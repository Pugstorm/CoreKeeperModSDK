using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

internal interface IFirstKeyJobCallback<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged
{
    public void ExecuteFirst(int threadIndex, in UnsafeParallelMultiHashMap<TKey, TValue> hashMap, in TKey key, in TValue firstValue, ref NativeParallelMultiHashMapIterator<TKey> it );
}

internal interface IKeyValueJobCallback<TKey, TValue>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged
{
    public void ProcessEntry(int threadIndex, in UnsafeParallelMultiHashMap<TKey, TValue> hashMap, in TKey key, in TValue value);
}

internal struct DependenciesHashMapHelper
{
    public static void ExecuteOnEntries<THandler, TKey, TValue>(THandler handler, UnsafeParallelMultiHashMap<TKey, TValue> hashmap, int threadIndex, int i)
        where THandler : unmanaged, IKeyValueJobCallback<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        unsafe
        {
            var bucketData = hashmap.m_Buffer->GetBucketData();
            var buckets = (int*)bucketData.buckets;
            var nextPtrs = (int*)bucketData.next;
            var keys = bucketData.keys;
            var values = bucketData.values;

            int entryIndex = buckets[i];

            while (entryIndex != -1)
            {
                var key = UnsafeUtility.ReadArrayElement<TKey>(keys, entryIndex);
                var value = UnsafeUtility.ReadArrayElement<TValue>(values, entryIndex);

                handler.ProcessEntry(threadIndex, in hashmap, in key, in value);
                entryIndex = nextPtrs[entryIndex];
            }
        }
    }

    public static void ExecuteOnFirstKey<THandler, TKey, TValue>(THandler handler, UnsafeParallelMultiHashMap<TKey, TValue> hashmap, int threadIndex, int i)
            where THandler : unmanaged, IFirstKeyJobCallback<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        unsafe
        {
            var bucketData = hashmap.m_Buffer->GetBucketData();
            var buckets = (int*)bucketData.buckets;
            var nextPtrs = (int*)bucketData.next;
            var keys = bucketData.keys;

            int entryIndex = buckets[i];

            while (entryIndex != -1)
            {
                var key = UnsafeUtility.ReadArrayElement<TKey>(keys, entryIndex);

                hashmap.TryGetFirstValue(key, out var firstValue, out var it);

                if (entryIndex == it.GetEntryIndex())
                {
                    handler.ExecuteFirst(threadIndex, in hashmap, in key, in firstValue, ref it);
                }
                entryIndex = nextPtrs[entryIndex];
            }
        }
    }

    public static int GetBucketSize<TKey, TValue>(UnsafeParallelMultiHashMap<TKey, TValue> hashmap)
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        unsafe
        {
            return hashmap.m_Buffer->GetBucketData().bucketCapacityMask + 1;
        }
    }
}
