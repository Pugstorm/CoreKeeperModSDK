using System;
using Unity.Collections;

namespace Unity.Entities.Editor
{
    class HierarchyNodeImmutableStore : IDisposable
    {
        const int k_ReadBuffer = 0;
        const int k_WriteBuffer = 1;
            
        int m_ImmutableBufferIndex;
            
        HierarchyNodeStore.Immutable m_Buffer0;
        HierarchyNodeStore.Immutable m_Buffer1;

        public HierarchyNodeImmutableStore(Allocator allocator)
        {
            m_Buffer0 = new HierarchyNodeStore.Immutable(allocator);
            m_Buffer1 = new HierarchyNodeStore.Immutable(allocator);
        }

        public void Dispose()
        {
            m_Buffer0.Dispose();
            m_Buffer1.Dispose();
        }

        public HierarchyNodeStore.Immutable GetReadBuffer()
            => GetBuffer(k_ReadBuffer);
            
        public HierarchyNodeStore.Immutable GetWriteBuffer()
            => GetBuffer(k_WriteBuffer);

        HierarchyNodeStore.Immutable GetBuffer(int offset)
            => (m_ImmutableBufferIndex + offset) % 2 == 0 ? m_Buffer0 : m_Buffer1;

        public void SwapBuffers()
            => ++m_ImmutableBufferIndex;

        public void Clear()
        {
            m_ImmutableBufferIndex = 0;
            m_Buffer0.Clear();
            m_Buffer1.Clear();
        }
    }
}