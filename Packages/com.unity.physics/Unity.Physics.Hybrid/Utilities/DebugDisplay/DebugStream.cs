using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.DebugDisplay;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// A component system group that contains the physics debug display systems.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class PhysicsDebugDisplayGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// A component system group that contains the physics debug display systems while in edit mode.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial class PhysicsDebugDisplayGroup_Editor : ComponentSystemGroup
    {
    }

    /// <summary>
    /// <para> Deprecated. Use PhysicsDebugDisplayGroup instead. </para>
    /// A component system group that contains the physics debug display systems.
    /// </summary>
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [Obsolete("PhysicsDisplayDebugGroup has been deprecated (RemovedAfter 2023-05-04). Use PhysicsDebugDisplayGroup instead. (UnityUpgradable) -> PhysicsDebugDisplayGroup", true)]
    public partial class PhysicsDisplayDebugGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// A system which is responsible for drawing physics debug display data.
    /// Create a singleton entity with <see cref="PhysicsDebugDisplayData"/> and select what you want to be drawn.<para/>
    ///
    /// If you want custom debug draw, you need to:<para/>
    /// 1) Create a system which updates before PhysicsDebugDisplaySystem.<br/>
    /// 2) In OnUpdate() of that system, call GetSingleton <see cref="PhysicsDebugDisplayData"/> (even if you are not using it, it is important to do so to properly chain dependencies).<br/>
    /// 3) Afterwards, in OnUpdate() or in scheduled jobs, call one of the exposed draw methods (Line, Arrow, Plane, Triangle, Cone, Box, ...).<br/>
    /// IMPORTANT: Drawing works only in the Editor.
    /// </summary>
    public abstract partial class PhysicsDebugDisplaySystem : SystemBase
    {
#if UNITY_EDITOR
        GameObject m_DrawComponentGameObject;
#endif
        class DrawComponent : MonoBehaviour
        {
            public PhysicsDebugDisplaySystem System;
            public void OnDrawGizmos()
            {
#if UNITY_EDITOR
                System?.CompleteDisplayDataDependencies();
                Unity.DebugDisplay.DebugDisplay.Render();
#endif
            }
        }

        protected override void OnCreate()
        {
            RequireForUpdate<PhysicsDebugDisplayData>();
#if UNITY_EDITOR
            Unity.DebugDisplay.DebugDisplay.Instantiate();
#endif
        }

        protected override void OnStartRunning()
        {
#if UNITY_EDITOR
            if (m_DrawComponentGameObject == null)
            {
                m_DrawComponentGameObject = new GameObject("PhysicsDebugDisplaySystem")
                {
                    hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy
                };

                // Note: we are adding an additional child here so that we can hide the parent in the hierarchy
                // without hiding the child, which would prevent the OnDrawGizmos method on the DrawComponent in the child
                // from being called.
                var childGameObject = new GameObject("DrawComponent")
                {
                    hideFlags = HideFlags.DontSave
                };
                childGameObject.transform.parent = m_DrawComponentGameObject.transform;

                var drawComponent = childGameObject.AddComponent<DrawComponent>();

                drawComponent.System = this;
            }
#endif
        }

        static void DestroyGameObject(GameObject gameObject)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(gameObject);
            else
                UnityEngine.Object.DestroyImmediate(gameObject);
        }

        protected override void OnStopRunning()
        {
#if UNITY_EDITOR
            if (m_DrawComponentGameObject != null)
            {
                while (m_DrawComponentGameObject.transform.childCount > 0)
                {
                    var child = m_DrawComponentGameObject.transform.GetChild(0);
                    child.parent = null;
                    DestroyGameObject(child.gameObject);
                }

                DestroyGameObject(m_DrawComponentGameObject);

                m_DrawComponentGameObject = null;
            }
#endif
        }

        /// <summary>
        /// Draws a point.
        /// </summary>
        /// <param name="x"> World space position. </param>
        /// <param name="size"> Extents. </param>
        /// <param name="color"> Color. </param>
        public static void Point(float3 x, float size, ColorIndex color, float3 offset)
        {
            x += offset;

            var lines = new Lines(3);

            lines.Draw(x - new float3(size, 0, 0), x + new float3(size, 0, 0), color);
            lines.Draw(x - new float3(0, size, 0), x + new float3(0, size, 0), color);
            lines.Draw(x - new float3(0, 0, size), x + new float3(0, 0, size), color);
        }

        /// <summary>
        /// Draws a line between 2 points.
        /// </summary>
        /// <param name="x0"> Point 0 in world space. </param>
        /// <param name="x1"> Point 1 in world space. </param>
        /// <param name="color"> Color. </param>
        public static void Line(float3 x0, float3 x1, Unity.DebugDisplay.ColorIndex color, float3 offset)
        {
            x0 += offset;
            x1 += offset;

            new Lines(1).Draw(x0, x1, color);
        }

        /// <summary>
        /// Draws multiple lines between the provided pairs of points.
        /// </summary>
        /// <param name="lineVertices"> A pointer to a vertices array containing a sequence of point pairs. A line is drawn between every pair of points. </param>
        /// <param name="numVertices"> Number of vertices. </param>
        /// <param name="color"> Color. </param>
        public static unsafe void Lines(float3* lineVertices, int numVertices, ColorIndex color, float3 offset)
        {
            var lines = new Lines(numVertices / 2);
            for (int i = 0; i < numVertices; i += 2)
            {
                lines.Draw(lineVertices[i] + offset, lineVertices[i + 1] + offset, color);
            }
        }

        /// <summary>
        /// Draws multiple lines between the provided pairs of points.
        /// </summary>
        /// <param name="lineVertices"> A list of vertices containing a sequence of point pairs. A line is drawn between every pair of points. </param>
        /// <param name="color"> Color. </param>
        public static void Lines(in NativeList<float3> lineVertices, ColorIndex color, float3 offset)
        {
            unsafe
            {
                Lines(lineVertices.GetUnsafeReadOnlyPtr(), lineVertices.Length, color, offset);
            }
        }

        /// <summary>
        /// Draws multiple lines between the provided pairs of points.
        /// </summary>
        /// <param name="lineVertices"> An array of vertices containing a sequence of point pairs. A line is drawn between every pair of points. </param>
        /// <param name="color"> Color. </param>
        public static void Lines(in NativeArray<float3> lineVertices, ColorIndex color, float3 offset)
        {
            unsafe
            {
                Lines((float3*)lineVertices.GetUnsafeReadOnlyPtr(), lineVertices.Length, color, offset);
            }
        }

        /// <summary>
        /// Draws multiple triangles from the provided data arrays.
        /// </summary>
        /// <param name="vertices"> An array of vertices. </param>
        /// <param name="triangleIndices"> An array of triangle indices pointing into the vertices array. A triangle is drawn from every triplet of triangle indices. </param>
        /// <param name="color"> Color. </param>
        public static void TriangleEdges(in NativeArray<float3> vertices, in NativeArray<int> triangleIndices,
            ColorIndex color, float3 offset)
        {
            var lines = new Lines(triangleIndices.Length);
            for (int i = 0; i < triangleIndices.Length; i += 3)
            {
                lines.Draw(vertices[triangleIndices[i]] + offset, vertices[triangleIndices[i + 1]] + offset, color);
                lines.Draw(vertices[triangleIndices[i + 1]] + offset, vertices[triangleIndices[i + 2]] + offset, color);
                lines.Draw(vertices[triangleIndices[i + 2]] + offset, vertices[triangleIndices[i]] + offset, color);
            }
        }

        /// <summary>
        /// Draws an arrow.
        /// </summary>
        /// <param name="x"> World space position of the arrow base. </param>
        /// <param name="v"> Arrow direction with length. </param>
        /// <param name="color"> Color. </param>
        public static void Arrow(float3 x, float3 v, ColorIndex color, float3 offset)
        {
            x += offset;

            Unity.DebugDisplay.Arrow.Draw(x, v, color);
        }

        /// <summary>
        /// Draws a plane.
        /// </summary>
        /// <param name="x"> Point in world space. </param>
        /// <param name="v"> Normal. </param>
        /// <param name="color"> Color. </param>
        public static void Plane(float3 x, float3 v, ColorIndex color, float3 offset)
        {
            x += offset;

            Unity.DebugDisplay.Plane.Draw(x, v, color);
        }

        /// <summary>
        /// Draws an arc.
        /// </summary>
        /// <param name="center"> World space position of the arc center. </param>
        /// <param name="normal"> Arc normal. </param>
        /// <param name="arm"> Arc arm. </param>
        /// <param name="angle"> Arc angle. </param>
        /// <param name="color"> Color. </param>
        public static void Arc(float3 center, float3 normal, float3 arm, float angle,
            Unity.DebugDisplay.ColorIndex color, float3 offset)
        {
            center += offset;

            Unity.DebugDisplay.Arc.Draw(center, normal, arm, angle, color);
        }

        /// <summary>
        /// Draws a cone.
        /// </summary>
        /// <param name="point"> Point in world space. </param>
        /// <param name="axis"> Cone axis. </param>
        /// <param name="angle"> Cone angle. </param>
        /// <param name="color"> Color. </param>
        public static void Cone(float3 point, float3 axis, float angle, ColorIndex color, float3 offset)
        {
            point += offset;

            Unity.DebugDisplay.Cone.Draw(point, axis, angle, color);
        }

        /// <summary>
        /// Draws a box.
        /// </summary>
        /// <param name="Size"> Size of the box. </param>
        /// <param name="Center"> Center of the box in world space. </param>
        /// <param name="Orientation"> Orientation of the box in world space. </param>
        /// <param name="color"> Color. </param>
        public static void Box(float3 Size, float3 Center, quaternion Orientation, ColorIndex color, float3 offset)
        {
            Center += offset;

            Unity.DebugDisplay.Box.Draw(Size, Center, Orientation, color);
        }

        /// <summary>
        /// Draws multiple triangles from the provided data arrays.
        /// </summary>
        /// <param name="vertices"> An array of vertices. </param>
        /// <param name="triangleIndices"> An array of triangle indices pointing into the vertices array. A triangle is drawn from every triplet of triangle indices. </param>
        /// <param name="color"> Color. </param>
        public static void Triangles(in NativeArray<float3> vertices, in NativeArray<int> triangleIndices, ColorIndex color, float3 offset)
        {
            var triangles = new Triangles(triangleIndices.Length / 3);
            for (int i = 0; i < triangleIndices.Length; i += 3)
            {
                var v0 = vertices[triangleIndices[i]];
                var v1 = vertices[triangleIndices[i + 1]];
                var v2 = vertices[triangleIndices[i + 2]];

                v0 += offset;
                v1 += offset;
                v2 += offset;

                float3 normal = math.normalize(math.cross(v1 - v0, v2 - v0));
                triangles.Draw(v0, v1, v2, normal, color);
            }
        }

        /// <summary>
        /// Draws multiple triangles from the provided array of triplets of vertices.
        /// </summary>
        /// <param name="vertices"> An array containing a sequence of vertex triplets. A triangle is drawn from every triplet of vertices. </param>
        /// <param name="numVertices"> Number of vertices. </param>
        /// <param name="color"> Color. </param>
        public static unsafe void Triangles(float3* vertices, int numVertices, ColorIndex color, float3 offset)
        {
            var triangles = new Triangles(numVertices / 3);
            for (int i = 0; i < numVertices; i += 3)
            {
                var v0 = vertices[i];
                var v1 = vertices[i + 1];
                var v2 = vertices[i + 2];

                v0 += offset;
                v1 += offset;
                v2 += offset;

                float3 normal = math.normalize(math.cross(v1 - v0, v2 - v0));
                triangles.Draw(v0, v1, v2, normal, color);
            }
        }

        /// <summary>
        /// Draws multiple triangles from the provided list of triplets of vertices.
        /// </summary>
        /// <param name="vertices"> A list containing a sequence of vertex triplets. A triangle is drawn from every triplet of vertices. </param>
        /// <param name="color"> Color. </param>
        public static void Triangles(in NativeList<float3> vertices, ColorIndex color, float3 offset)
        {
            unsafe
            {
                Triangles(vertices.GetUnsafePtr(), vertices.Length, color, offset);
            }
        }

        /// <summary>
        /// Draws multiple triangles from the provided array of triplets of vertices.
        /// </summary>
        /// <param name="vertices"> An array containing a sequence of vertex triplets. A triangle is drawn from every triplet of vertices. </param>
        /// <param name="color"> Color. </param>
        public static void Triangles(in NativeArray<float3> vertices, ColorIndex color, float3 offset)
        {
            unsafe
            {
                Triangles((float3*)vertices.GetUnsafePtr(), vertices.Length, color, offset);
            }
        }

        protected override void OnUpdate()
        {
        }

        /// <summary>
        ///  Completes dependencies for all systems in the physics debug display groups, which ensures that the debug display
        ///  data is fully produced by the corresponding debug display data systems before it is being prepared for rendering by this system.
        /// </summary>
        void CompleteDisplayDataDependencies()
        {
            var displayGroup = World.GetExistingSystemManaged<PhysicsDebugDisplayGroup>();
            if (displayGroup != null)
            {
                using var systemHandles = displayGroup.GetAllSystems();
                foreach (var handle in systemHandles)
                {
                    World.Unmanaged.ResolveSystemStateRef(handle).CompleteDependency();
                }
            }

            var editorDisplayGroup = World.GetExistingSystemManaged<PhysicsDebugDisplayGroup_Editor>();
            if (editorDisplayGroup != null)
            {
                using var systemHandles = editorDisplayGroup.GetAllSystems();
                foreach (var handle in systemHandles)
                {
                    World.Unmanaged.ResolveSystemStateRef(handle).CompleteDependency();
                }
            }
        }
    }

    /// <summary>
    /// Draws physics debug display data.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PhysicsDebugDisplayGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial class PhysicsDebugDisplaySystem_Default : PhysicsDebugDisplaySystem
    {}

    /// <summary>
    /// Draws physics debug display data while in edit mode.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PhysicsDebugDisplayGroup_Editor), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial class PhysicsDebugDisplaySystem_Editor : PhysicsDebugDisplaySystem
    {}
}
