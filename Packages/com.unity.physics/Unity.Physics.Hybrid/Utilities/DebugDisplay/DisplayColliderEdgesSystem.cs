using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.DebugDisplay;
using Unity.Jobs;
using Unity.Physics.Systems;
using Unity.Transforms;
using static Unity.Physics.Math;
using UnityEngine;

namespace Unity.Physics.Authoring
{
#if UNITY_EDITOR
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct DisplayColliderEdgesJob : IJobParallelFor
    {
        [ReadOnly] private NativeArray<RigidBody> RigidBodies;
        [ReadOnly] private PrimitiveColliderGeometries Geometries;
        [ReadOnly] private float CollidersEdgesScale;

        public float3 Offset;

        internal static JobHandle ScheduleJob(in NativeArray<RigidBody> rigidBodies, float collidersEdgesScales, in PrimitiveColliderGeometries geometries, JobHandle inputDeps, float3 offset)
        {
            return new DisplayColliderEdgesJob
            {
                RigidBodies = rigidBodies,
                Geometries = geometries,
                CollidersEdgesScale = collidersEdgesScales,
		Offset = offset,
            }.Schedule(rigidBodies.Length, 1, inputDeps);
        }

        public void Execute(int i)
        {
            var rigidBody = RigidBodies[i];
            if (rigidBody.Collider.IsCreated)
            {
                DrawColliderEdges(rigidBody.Collider, rigidBody.WorldFromBody, rigidBody.Scale * CollidersEdgesScale, ref Geometries, offset: Offset);
            }
        }

        private static unsafe void DrawColliderEdges(BlobAssetReference<Collider> collider, RigidTransform worldFromCollider, float uniformScale, ref PrimitiveColliderGeometries geometries,
            bool drawVertices = false, float3 offset = default)
        {
            DrawColliderEdges((Collider*)collider.GetUnsafePtr(), worldFromCollider, uniformScale, ref geometries, drawVertices, offset: offset);
        }

        static unsafe void DrawColliderEdges(Collider* collider, RigidTransform worldFromCollider, float uniformScale, ref PrimitiveColliderGeometries geometries, bool drawVertices = false, float3 offset = default)
        {
            switch (collider->CollisionType)
            {
                case CollisionType.Convex:
                    DrawConvexColliderEdges((ConvexCollider*)collider, worldFromCollider, uniformScale, ref geometries, drawVertices, offset);
                    break;
                case CollisionType.Composite:
                    switch (collider->Type)
                    {
                        case ColliderType.Compound:
                            DrawCompoundColliderEdges((CompoundCollider*)collider, worldFromCollider, uniformScale, ref geometries, drawVertices, offset: offset);
                            break;
                        case ColliderType.Mesh:
                            DrawMeshColliderEdges((MeshCollider*)collider, worldFromCollider, uniformScale, offset);
                            break;
                        case ColliderType.Terrain:
                            DrawTerrainColliderEdges((TerrainCollider*)collider, worldFromCollider, uniformScale, offset);
                            break;
                    }

                    break;
                case CollisionType.Terrain:
                    DrawTerrainColliderEdges((TerrainCollider*)collider, worldFromCollider, uniformScale, offset);
                    break;
            }
        }

        static void GetDebugDrawEdge(ref ConvexHull hullIn, ConvexHull.Face faceIn, int edgeIndex, out float3 from,
            out float3 to)
        {
            byte fromIndex = hullIn.FaceVertexIndices[faceIn.FirstIndex + edgeIndex];
            byte toIndex = hullIn.FaceVertexIndices[faceIn.FirstIndex + (edgeIndex + 1) % faceIn.NumVertices];
            from = hullIn.Vertices[fromIndex];
            to = hullIn.Vertices[toIndex];
        }

        static unsafe void DrawConvexColliderEdges(ConvexCollider* collider, RigidTransform worldFromConvex, float uniformScale, ref PrimitiveColliderGeometries geometries, bool drawVertices = false, float3 offset = default)
        {
            var worldMatrix = math.mul(new float4x4(worldFromConvex), float4x4.Scale(uniformScale));

            ref ConvexHull hull = ref collider->ConvexHull;
            float3 centroid = float3.zero;

            void ExpandHullVertices(ref float3x2 vertices, ref ConvexHull convexHull)
            {
                for (int i = 0; i < 2; i++)
                {
                    float3 direction = vertices[i] - centroid;
                    float3 directionNormalized = math.normalize(direction);

                    vertices[i] += directionNormalized * convexHull.ConvexRadius;
                }
            }

            // centroid is only needed in those cases
            if (hull.FaceLinks.Length > 0 || (drawVertices && hull.VertexEdges.Length > 0))
            {
                centroid = MeshUtilities.ComputeHullCentroid(ref hull);
            }

            if (hull.FaceLinks.Length > 0)
            {
                var lineVertices = new NativeList<float3>(Allocator.Temp);
                try
                {
                    // set some best guess capacity, assuming we have on average of 5 edges per face,
                    // and given the fact that we need 2 vertices to define an edge.
                    const int kAvgEdgesPerFace = 5;
                    const int kNumVerticesPerEdge = 2;
                    lineVertices.Capacity = hull.Faces.Length * kAvgEdgesPerFace * kNumVerticesPerEdge;

                    foreach (ConvexHull.Face face in hull.Faces)
                    {
                        for (int edgeIndex = 0; edgeIndex < face.NumVertices; edgeIndex++)
                        {
                            var verts = new float3x2();
                            GetDebugDrawEdge(ref hull, face, edgeIndex, out verts[0], out verts[1]);
                            ExpandHullVertices(ref verts, ref hull);

                            lineVertices.Add(math.transform(worldMatrix, verts[0]));
                            lineVertices.Add(math.transform(worldMatrix, verts[1]));
                        }
                    }

                    PhysicsDebugDisplaySystem.Lines(lineVertices, ColorIndex.Green, offset);
                }
                finally
                {
                    lineVertices.Dispose();
                }
            }
            else
            {
                float radius;
                float3 center;
                float height;
                switch (collider->Type)
                {
                    case ColliderType.Capsule:
                        radius = ((CapsuleCollider*)collider)->Radius;
                        var vertex0 = ((CapsuleCollider*)collider)->Vertex0;
                        var vertex1 = ((CapsuleCollider*)collider)->Vertex1;
                        center = -0.5f * (vertex1 - vertex0) + vertex1;
                        var axis = vertex1 - vertex0; //axis in wfc-space
                        var colliderOrientation = Quaternion.FromToRotation(Vector3.up, -axis);
                        height = 0.5f * math.length(axis) + radius;
                        DrawColliderUtility.DrawPrimitiveCapsuleEdges(radius, height, center, colliderOrientation, worldFromConvex, ref geometries.CapsuleGeometry, uniformScale, offset);
                        break;

                    case ColliderType.Cylinder:
                        radius = ((CylinderCollider*)collider)->Radius;
                        height = ((CylinderCollider*)collider)->Height;
                        var colliderPosition = ((CylinderCollider*)collider)->Center;
                        colliderOrientation = ((CylinderCollider*)collider)->Orientation * Quaternion.FromToRotation(Vector3.up, Vector3.back);
                        DrawColliderUtility.DrawPrimitiveCylinderEdges(radius, height, colliderPosition, colliderOrientation, worldFromConvex, ref geometries.CylinderGeometry, uniformScale, offset);
                        break;

                    case ColliderType.Sphere:
                        radius = ((SphereCollider*)collider)->Radius;
                        center = ((SphereCollider*)collider)->Center;
                        DrawColliderUtility.DrawPrimitiveSphereEdges(radius, center, worldFromConvex, ref geometries.SphereGeometry, uniformScale, offset);
                        break;
                }
            }

            // This section is used to highlight the edges of the corners in red DebugDraw lines. These are drawn on top
            // of the green DebugDraw edges. It can be a useful little tool to highlight where your corners are.
            // drawVertices=false everywhere as default.
            if (drawVertices && hull.VertexEdges.Length > 0)
            {
                const int kNumLinesPerVertex = 8;
                var lineVertices = new NativeArray<float3>(hull.VertexEdges.Length * kNumLinesPerVertex, Allocator.Temp);
                try
                {
                    float3* vertexBuffer = stackalloc float3[kNumLinesPerVertex];

                    int i = 0;
                    foreach (ConvexHull.Edge vertexEdge in hull.VertexEdges)
                    {
                        ConvexHull.Face face = hull.Faces[vertexEdge.FaceIndex];
                        var verts = new float3x2();
                        GetDebugDrawEdge(ref hull, face, vertexEdge.EdgeIndex, out verts[0], out verts[1]);
                        ExpandHullVertices(ref verts, ref hull);

                        float3 r3 = new float3(0.01f, 0f, 0f);

                        // line 1
                        vertexBuffer[0] = verts[0] - r3;
                        vertexBuffer[1] = verts[0] + r3;

                        // line 2
                        vertexBuffer[2] = verts[0] - r3.yzx;
                        vertexBuffer[3] = verts[0] + r3.yzx;

                        // line 3
                        vertexBuffer[4] = verts[0] - r3.zxy;
                        vertexBuffer[5] = verts[0] + r3.zxy;

                        // line 4
                        float3 direction = (verts[1] - verts[0]) * 0.25f;
                        vertexBuffer[6] = verts[0];
                        vertexBuffer[7] = verts[0] + direction;

                        // transform line vertices into world space
                        for (int j = 0; j < kNumLinesPerVertex; j++)
                        {
                            lineVertices[i++] = math.transform(worldMatrix, vertexBuffer[j]);
                        }
                    }

                    PhysicsDebugDisplaySystem.Lines(lineVertices, ColorIndex.Red, offset);
                }
                finally
                {
                    lineVertices.Dispose();
                }
            }
        }

        static unsafe void DrawMeshColliderEdges(MeshCollider* meshCollider, RigidTransform worldFromCollider, float uniformScale, float3 offset)
        {
            ref Mesh mesh = ref meshCollider->Mesh;

            float4x4 worldMatrix = new float4x4(worldFromCollider);
            worldMatrix.c0 *= uniformScale;
            worldMatrix.c1 *= uniformScale;
            worldMatrix.c2 *= uniformScale;

            var lineVertices = new NativeList<float3>(Allocator.Temp);
            try
            {
                // calculate approximate edge count, assuming on average of 4 edges per primitive
                int approximateEdgeCount = 0;
                const int kAvgEdgesPerPrimitive = 4;
                for (int sectionIndex = 0; sectionIndex < mesh.Sections.Length; sectionIndex++)
                {
                    ref Mesh.Section section = ref mesh.Sections[sectionIndex];
                    approximateEdgeCount += section.PrimitiveVertexIndices.Length * kAvgEdgesPerPrimitive;
                }

                // set best guess capacity for line vertices
                const int kNumVerticesPerLine = 2;
                lineVertices.Capacity = approximateEdgeCount * kNumVerticesPerLine;

                for (int sectionIndex = 0; sectionIndex < mesh.Sections.Length; sectionIndex++)
                {
                    ref Mesh.Section section = ref mesh.Sections[sectionIndex];
                    for (int primitiveIndex = 0;
                         primitiveIndex < section.PrimitiveVertexIndices.Length;
                         primitiveIndex++)
                    {
                        Mesh.PrimitiveVertexIndices vertexIndices = section.PrimitiveVertexIndices[primitiveIndex];
                        Mesh.PrimitiveFlags flags = section.PrimitiveFlags[primitiveIndex];
                        bool isTrianglePair = (flags & Mesh.PrimitiveFlags.IsTrianglePair) != 0;
                        bool isQuad = (flags & Mesh.PrimitiveFlags.IsQuad) != 0;

                        var v0 = math.transform(worldMatrix, section.Vertices[vertexIndices.A]);
                        var v1 = math.transform(worldMatrix, section.Vertices[vertexIndices.B]);
                        var v2 = math.transform(worldMatrix, section.Vertices[vertexIndices.C]);
                        var v3 = math.transform(worldMatrix, section.Vertices[vertexIndices.D]);

                        if (isQuad)
                        {
                            // edge 1
                            lineVertices.Add(v0);
                            lineVertices.Add(v1);

                            // edge 2
                            lineVertices.Add(v1);
                            lineVertices.Add(v2);

                            // edge 3
                            lineVertices.Add(v2);
                            lineVertices.Add(v3);

                            // edge 4
                            lineVertices.Add(v3);
                            lineVertices.Add(v0);
                        }
                        else if (isTrianglePair)
                        {
                            // edge 1
                            lineVertices.Add(v0);
                            lineVertices.Add(v1);

                            // edge 2
                            lineVertices.Add(v1);
                            lineVertices.Add(v2);

                            // edge 3
                            lineVertices.Add(v2);
                            lineVertices.Add(v3);

                            // edge 4
                            lineVertices.Add(v3);
                            lineVertices.Add(v0);

                            // edge 5
                            lineVertices.Add(v0);
                            lineVertices.Add(v2);
                        }
                        else
                        {
                            // edge 1
                            lineVertices.Add(v0);
                            lineVertices.Add(v1);

                            // edge 2
                            lineVertices.Add(v1);
                            lineVertices.Add(v2);

                            // edge 3
                            lineVertices.Add(v2);
                            lineVertices.Add(v0);
                        }
                    }
                }

                PhysicsDebugDisplaySystem.Lines(lineVertices, ColorIndex.Green, offset);
            }
            finally
            {
                lineVertices.Dispose();
            }
        }

        static unsafe void DrawTerrainColliderEdges(TerrainCollider* terrainCollider, RigidTransform worldFromCollider, float uniformScale, float3 offset)
        {
            ref Terrain terrain = ref terrainCollider->Terrain;

            float4x4 worldMatrix = new float4x4(worldFromCollider);
            worldMatrix.c0 *= uniformScale;
            worldMatrix.c1 *= uniformScale;
            worldMatrix.c2 *= uniformScale;

            // calculate number of edges in the terrain and allocate line vertex array with correct size
            int numCellsX = terrain.Size.x - 1;
            int numCellsY = terrain.Size.y - 1;
            int numEdges = (numCellsX * numCellsY) * 3 + (numCellsX + numCellsY);
            int numLineVertices = numEdges * 2;
            var lineVertices = new NativeArray<float3>(numLineVertices, Allocator.Temp);

            int vertexIndex = 0;
            try
            {
                for (int j = 0; j < numCellsY; ++j)
                {
                    for (int i = 0; i < numCellsX; ++i)
                    {
                        int i0 = i;
                        int i1 = i + 1;
                        int j0 = j;
                        int j1 = j + 1;
                        float3 v0 = math.transform(worldMatrix,
                            new float3(i0, terrain.Heights[i0 + terrain.Size.x * j0], j0) * terrain.Scale);
                        float3 v1 = math.transform(worldMatrix,
                            new float3(i1, terrain.Heights[i1 + terrain.Size.x * j0], j0) * terrain.Scale);
                        float3 v2 = math.transform(worldMatrix,
                            new float3(i0, terrain.Heights[i0 + terrain.Size.x * j1], j1) * terrain.Scale);

                        // edges originating at the lower corner of the cell:
                        // edge 1
                        lineVertices[vertexIndex++] = v0;
                        lineVertices[vertexIndex++] = v2;

                        // edge 2
                        lineVertices[vertexIndex++] = v2;
                        lineVertices[vertexIndex++] = v1;

                        // edge 3
                        lineVertices[vertexIndex++] = v1;
                        lineVertices[vertexIndex++] = v0;

                        // edges at the border of the terrain:
                        bool maxX = i == numCellsX - 1;
                        bool maxY = j == numCellsY - 1;
                        if (maxX || maxY)
                        {
                            float3 v3 = math.transform(worldMatrix,
                                new float3(i1, terrain.Heights[i1 + terrain.Size.x * j1], j1) * terrain.Scale);

                            if (maxY)
                            {
                                // edge along x, at y border
                                lineVertices[vertexIndex++] = v2;
                                lineVertices[vertexIndex++] = v3;
                            }

                            if (maxX)
                            {
                                // edge along y, at x border
                                lineVertices[vertexIndex++] = v3;
                                lineVertices[vertexIndex++] = v1;
                            }
                        }
                    }
                }

                PhysicsDebugDisplaySystem.Lines(lineVertices, ColorIndex.Green, offset);
            }
            finally
            {
                lineVertices.Dispose();
            }
        }

        static unsafe void DrawCompoundColliderEdges(CompoundCollider* compoundCollider, RigidTransform worldFromCompound, float uniformScale, ref PrimitiveColliderGeometries geometries,
            bool drawVertices = false, float3 offset = default)
        {
            for (int i = 0; i < compoundCollider->NumChildren; i++)
            {
                ref CompoundCollider.Child child = ref compoundCollider->Children[i];

                ScaledMTransform mWorldFromCompound = new ScaledMTransform(worldFromCompound, uniformScale);
                ScaledMTransform mWorldFromChild = ScaledMTransform.Mul(mWorldFromCompound, new MTransform(child.CompoundFromChild));
                RigidTransform worldFromChild = new RigidTransform(mWorldFromChild.Rotation, mWorldFromChild.Translation);

                var childCollider = child.Collider;
                DrawColliderEdges(childCollider, worldFromChild, uniformScale, ref geometries, drawVertices, offset);
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PhysicsDebugDisplayGroup))]
    [BurstCompile]
    internal partial struct DisplayBodyColliderEdges_Default : ISystem
    {
        private PrimitiveColliderGeometries DefaultGeometries;
        private EntityQuery ColliderQuery;

        public void OnCreate(ref SystemState state)
        {
            ColliderQuery = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsCollider>(),
                ComponentType.ReadOnly<LocalToWorld>());

            state.RequireForUpdate(ColliderQuery);
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<PhysicsDebugDisplayData>();

            DrawColliderUtility.CreateGeometries(out DefaultGeometries);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out PhysicsDebugDisplayData debugDisplay) || debugDisplay.DrawColliderEdges == 0)
                return;

            switch (debugDisplay.ColliderEdgesDisplayMode)
            {
                case PhysicsDebugDisplayMode.PreIntegration:
                {
                    if (SystemAPI.TryGetSingleton(out PhysicsWorldSingleton physicsWorldSingleton))
                    {
                        state.Dependency = DisplayColliderEdgesJob.ScheduleJob(
                            physicsWorldSingleton.PhysicsWorld.Bodies, 1.0f, DefaultGeometries, state.Dependency, debugDisplay.Offset);
                    }
                    break;
                }
                case PhysicsDebugDisplayMode.PostIntegration:
                {
                    var rigidBodiesList = new NativeList<RigidBody>(Allocator.TempJob);
                    DrawColliderUtility.GetRigidBodiesFromQuery(ref state, ref ColliderQuery, ref rigidBodiesList);

                    if (rigidBodiesList.IsEmpty)
                    {
                        rigidBodiesList.Dispose();
                        return;
                    }

                    var displayHandle = DisplayColliderEdgesJob.ScheduleJob(rigidBodiesList.AsArray(), 1.0f, DefaultGeometries, state.Dependency, debugDisplay.Offset);
                    var disposeHandle = rigidBodiesList.Dispose(displayHandle);

                    state.Dependency = disposeHandle;

                    break;
                }
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            DefaultGeometries.Dispose();
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PhysicsDebugDisplayGroup_Editor))]
    internal partial struct DisplayBodyColliderEdges_Editor : ISystem
    {
        private PrimitiveColliderGeometries DefaultGeometries;
        private EntityQuery ColliderQuery;

        public void OnCreate(ref SystemState state)
        {
            ColliderQuery = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsCollider>(),
                ComponentType.ReadOnly<LocalToWorld>());
            state.RequireForUpdate<PhysicsDebugDisplayData>();
            state.RequireForUpdate(ColliderQuery);

            DrawColliderUtility.CreateGeometries(out DefaultGeometries);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out PhysicsDebugDisplayData debugDisplay) || debugDisplay.DrawColliderEdges == 0)
                return;

            var rigidBodiesList = new NativeList<RigidBody>(Allocator.TempJob);
            DrawColliderUtility.GetRigidBodiesFromQuery(ref state, ref ColliderQuery, ref rigidBodiesList);

            if (rigidBodiesList.IsEmpty)
            {
                rigidBodiesList.Dispose();
                return;
            }

            var displayHandle = DisplayColliderEdgesJob.ScheduleJob(rigidBodiesList.AsArray(), 1.0f, DefaultGeometries, state.Dependency, debugDisplay.Offset);
            var disposeHandle = rigidBodiesList.Dispose(displayHandle);

            state.Dependency = disposeHandle;
        }

        public void OnDestroy(ref SystemState state)
        {
            DefaultGeometries.Dispose();
        }
    }
#endif
}
