using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.DebugDisplay
{
    internal struct LineBuffer : IDisposable
    {
        internal UnsafeList<Instance> m_Instance;

        private bool m_ResizeRequested;

        internal struct Instance
        {
            internal float4 m_Begin;
            internal float4 m_End;
        }

        internal void Initialize(int size)
        {
            m_Instance = new UnsafeList<Instance>(size, Allocator.Persistent);
            m_Instance.Resize(size);
            m_ResizeRequested = false;
        }

        internal void SetLine(float3 begin, float3 end, ColorIndex colorIndex, int index)
        {
            m_Instance[index] = new Instance
            {
                m_Begin = new float4(begin.x, begin.y, begin.z, colorIndex.value),
                m_End = new float4(end.x, end.y, end.z, colorIndex.value)
            };
        }

        internal int Size => m_Instance.Length;

        internal void RequestResize()
        {
            m_ResizeRequested = true;
        }

        internal bool ResizeRequested => m_ResizeRequested;

        internal void ClearLine(int index)
        {
            m_Instance[index] = new Instance {};
        }

        public void Dispose()
        {
            m_Instance.Dispose();
        }

        internal Unit AllocateAll()
        {
            return new Unit(m_Instance.Length);
        }
    }
}
