using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Unity.Entities.Editor
{
    struct SimpleDiffer<T> : IDisposable where T : unmanaged, IEquatable<T>
    {
        NativeParallelHashSet<T> m_Current;
        NativeParallelHashSet<T> m_Existing;

        public SimpleDiffer(int initialCapacity, Allocator allocator)
        {
            m_Current = new(initialCapacity, allocator);
            m_Existing = new(initialCapacity, allocator);
        }

        public void GetCreatedAndRemovedItems(NativeArray<T> source, NativeList<T> createdItems, NativeList<T> removedItems)
        {
            createdItems.Clear();
            removedItems.Clear();
            m_Existing.Clear();

            for (var i = 0; i < source.Length; i++)
            {
                var item = source[i];

                m_Existing.Add(item);
                if (m_Current.Add(item))
                    createdItems.Add(item);
            }

            foreach (var item in m_Current)
            {
                if (!m_Existing.Contains(item))
                    removedItems.Add(item);
            }

            foreach (var item in removedItems)
            {
                m_Current.Remove(item);
            }
        }

        public void Clear()
        {
            m_Current.Clear();
            m_Existing.Clear();
        }

        public void Dispose()
        {
            m_Current.Dispose();
            m_Existing.Dispose();
        }
    }

}
