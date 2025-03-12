using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.DebugDisplay
{
    internal struct TriangleBuffer : IDisposable
    {
        internal UnsafeList<Instance> m_Instance;

        private bool m_ResizeRequested;

        internal struct Instance
        {
            internal float4 m_vertex0;
            internal float4 m_vertex1;
            internal float4 m_vertex2;
            internal float3 normal;
        }

        internal void Initialize(int size)
        {
            m_Instance = new UnsafeList<Instance>(size, Allocator.Persistent);
            m_Instance.Resize(size);
            m_ResizeRequested = false;
        }

        internal void SetTriangle(float3 vertex0, float3 vertex1, float3 vertex2, float3 normal, Unity.DebugDisplay.ColorIndex colorIndex, int index)
        {
            m_Instance[index] = new Instance
            {
                m_vertex0 = new float4(vertex0, colorIndex.value),
                m_vertex1 = new float4(vertex1, colorIndex.value),
                m_vertex2 = new float4(vertex2, colorIndex.value),
                normal = new float3(normal.x, normal.y, normal.z)
            };
        }

        internal int Size => m_Instance.Length;

        internal void RequestResize()
        {
            m_ResizeRequested = true;
        }

        internal bool ResizeRequested => m_ResizeRequested;

        internal void ClearTriangle(int index)
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
