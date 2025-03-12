using System;
using Unity.Burst;
using Unity.Mathematics;

namespace Unity.DebugDisplay
{
    internal struct Arrows : IDisposable
    {
        private Lines m_Lines;
        internal Arrows(int count)
        {
            m_Lines = new Lines(count * 5);
        }

        internal void Draw(float3 x, float3 v, Unity.DebugDisplay.ColorIndex color)
        {
            var X0 = x;
            var X1 = x + v;

            m_Lines.Draw(X0, X1, color);

            float3 dir;
            float length = Physics.Math.NormalizeWithLength(v, out dir);
            float3 perp, perp2;
            Physics.Math.CalculatePerpendicularNormalized(dir, out perp, out perp2);
            float3 scale = length * 0.2f;

            m_Lines.Draw(X1, X1 + (perp - dir) * scale, color);
            m_Lines.Draw(X1, X1 - (perp + dir) * scale, color);
            m_Lines.Draw(X1, X1 + (perp2 - dir) * scale, color);
            m_Lines.Draw(X1, X1 - (perp2 + dir) * scale, color);
        }

        public void Dispose()
        {
            m_Lines.Dispose();
        }
    }
    internal struct Arrow
    {
        internal static void Draw(float3 x, float3 v, Unity.DebugDisplay.ColorIndex color)
        {
            new Arrows(1).Draw(x, v, color);
        }
    }

    internal struct Planes : IDisposable
    {
        private Lines m_Lines;
        internal Planes(int count)
        {
            m_Lines = new Lines(count * 9);
        }

        internal void Draw(float3 x, float3 v, Unity.DebugDisplay.ColorIndex color)
        {
            var X0 = x;
            var X1 = x + v;

            m_Lines.Draw(X0, X1, color);

            float3 dir;
            float length = Physics.Math.NormalizeWithLength(v, out dir);
            float3 perp, perp2;
            Physics.Math.CalculatePerpendicularNormalized(dir, out perp, out perp2);
            float3 scale = length * 0.2f;

            m_Lines.Draw(X1, X1 + (perp - dir) * scale, color);
            m_Lines.Draw(X1, X1 - (perp + dir) * scale, color);
            m_Lines.Draw(X1, X1 + (perp2 - dir) * scale, color);
            m_Lines.Draw(X1, X1 - (perp2 + dir) * scale, color);

            perp *= length;
            perp2 *= length;

            m_Lines.Draw(X0 + perp + perp2, X0 + perp - perp2, color);
            m_Lines.Draw(X0 + perp - perp2, X0 - perp - perp2, color);
            m_Lines.Draw(X0 - perp - perp2, X0 - perp + perp2, color);
            m_Lines.Draw(X0 - perp + perp2, X0 + perp + perp2, color);
        }

        public void Dispose()
        {
            m_Lines.Dispose();
        }
    }
    internal struct Plane
    {
        internal static void Draw(float3 x, float3 v, Unity.DebugDisplay.ColorIndex color)
        {
            new Planes(1).Draw(x, v, color);
        }
    }

    internal struct Arcs : IDisposable
    {
        private Lines m_Lines;
        const int res = 16;

        internal Arcs(int count)
        {
            m_Lines = new Lines(count * (2 + res));
        }

        internal void Draw(float3 center, float3 normal, float3 arm, float angle, Unity.DebugDisplay.ColorIndex color)
        {
            quaternion q = quaternion.AxisAngle(normal, angle / res);
            float3 currentArm = arm;
            m_Lines.Draw(center, center + currentArm, color);
            for (int i = 0; i < res; i++)
            {
                float3 nextArm = math.mul(q, currentArm);
                m_Lines.Draw(center + currentArm, center + nextArm, color);
                currentArm = nextArm;
            }
            m_Lines.Draw(center, center + currentArm, color);
        }

        public void Dispose()
        {
            m_Lines.Dispose();
        }
    }
    internal struct Arc
    {
        internal static void Draw(float3 center, float3 normal, float3 arm, float angle,
            Unity.DebugDisplay.ColorIndex color)
        {
            new Arcs(1).Draw(center, normal, arm, angle, color);
        }
    }

    internal struct Boxes : IDisposable
    {
        private Lines m_Lines;

        internal Boxes(int count)
        {
            m_Lines = new Lines(count * 12);
        }

        internal void Draw(float3 Size, float3 Center, quaternion Orientation, Unity.DebugDisplay.ColorIndex color)
        {
            float3x3 mat = math.float3x3(Orientation);
            float3 x = mat.c0 * Size.x * 0.5f;
            float3 y = mat.c1 * Size.y * 0.5f;
            float3 z = mat.c2 * Size.z * 0.5f;
            float3 c0 = Center - x - y - z;
            float3 c1 = Center - x - y + z;
            float3 c2 = Center - x + y - z;
            float3 c3 = Center - x + y + z;
            float3 c4 = Center + x - y - z;
            float3 c5 = Center + x - y + z;
            float3 c6 = Center + x + y - z;
            float3 c7 = Center + x + y + z;

            m_Lines.Draw(c0, c1, color); // ring 0
            m_Lines.Draw(c1, c3, color);
            m_Lines.Draw(c3, c2, color);
            m_Lines.Draw(c2, c0, color);

            m_Lines.Draw(c4, c5, color); // ring 1
            m_Lines.Draw(c5, c7, color);
            m_Lines.Draw(c7, c6, color);
            m_Lines.Draw(c6, c4, color);

            m_Lines.Draw(c0, c4, color); // between rings
            m_Lines.Draw(c1, c5, color);
            m_Lines.Draw(c2, c6, color);
            m_Lines.Draw(c3, c7, color);
        }

        public void Dispose()
        {
            m_Lines.Dispose();
        }
    }
    internal struct Box
    {
        internal static void Draw(float3 Size, float3 Center, quaternion Orientation, Unity.DebugDisplay.ColorIndex color)
        {
            new Boxes(1).Draw(Size, Center, Orientation, color);
        }
    }

    internal struct Cones : IDisposable
    {
        private Lines m_Lines;
        const int res = 16;

        internal Cones(int count)
        {
            m_Lines = new Lines(count * res * 2);
        }

        internal void Draw(float3 point, float3 axis, float angle, Unity.DebugDisplay.ColorIndex color)
        {
            float3 dir;
            float scale = Physics.Math.NormalizeWithLength(axis, out dir);
            float3 arm;
            {
                float3 perp1, perp2;
                Physics.Math.CalculatePerpendicularNormalized(dir, out perp1, out perp2);
                arm = math.mul(quaternion.AxisAngle(perp1, angle), dir) * scale;
            }
            quaternion q = quaternion.AxisAngle(dir, 2.0f * (float)math.PI / res);

            for (int i = 0; i < res; i++)
            {
                float3 nextArm = math.mul(q, arm);
                m_Lines.Draw(point, point + arm, color);
                m_Lines.Draw(point + arm, point + nextArm, color);
                arm = nextArm;
            }
        }

        public void Dispose()
        {
            m_Lines.Dispose();
        }
    }
    internal struct Cone
    {
        internal static void Draw(float3 point, float3 axis, float angle, Unity.DebugDisplay.ColorIndex color)
        {
            new Cones(1).Draw(point, axis, angle, color);
        }
    }

    internal struct Lines : IDisposable
    {
        Unit m_Unit;
        internal Lines(int count)
        {
            DebugDisplay.Instantiate();
            m_Unit = Unmanaged.Instance.Data.m_LineBufferAllocations.AllocateAtomic(count);
        }

        internal void Draw(float3 begin, float3 end, ColorIndex color)
        {
            if (m_Unit.m_Next < m_Unit.m_End)
            {
                Unmanaged.Instance.Data.m_LineBuffer.SetLine(begin, end, color, m_Unit.m_Next++);
            }
            else
            {
                Unmanaged.Instance.Data.m_LineBuffer.RequestResize();
            }
        }

        public void Dispose()
        {
            while (m_Unit.m_Next < m_Unit.m_End)
                Unmanaged.Instance.Data.m_LineBuffer.ClearLine(m_Unit.m_Next++);
        }
    }

    //------
    internal struct Triangles : IDisposable
    {
        Unit m_Unit;
        internal Triangles(int count)
        {
            DebugDisplay.Instantiate();
            m_Unit = Unmanaged.Instance.Data.m_TriangleBufferAllocations.AllocateAtomic(count);
        }

        internal void Draw(float3 vertex0, float3 vertex1, float3 vertex2, float3 normal, Unity.DebugDisplay.ColorIndex color)
        {
            if (m_Unit.m_Next < m_Unit.m_End)
            {
                Unmanaged.Instance.Data.m_TriangleBuffer.SetTriangle(vertex0, vertex1, vertex2, normal, color,
                    m_Unit.m_Next++);
            }
            else
            {
                Unmanaged.Instance.Data.m_TriangleBuffer.RequestResize();
            }
        }

        public void Dispose()
        {
            while (m_Unit.m_Next < m_Unit.m_End)
                Unmanaged.Instance.Data.m_TriangleBuffer.ClearTriangle(m_Unit.m_Next++);
        }
    }

    internal class DebugDisplay
    {
        internal static void Render()
        {
            Managed.Instance.CopyFromCpuToGpu();
            Managed.Instance.Render();
        }

        internal static void Clear()
        {
            Managed.Instance.Clear();
        }

        [BurstDiscard]
        internal static void Instantiate()
        {
            if (Managed.Instance == null)
                Managed.Instance = new Managed();
        }
    }
}
