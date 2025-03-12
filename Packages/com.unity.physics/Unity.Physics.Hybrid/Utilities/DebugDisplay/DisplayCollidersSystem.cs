using System;
using Unity.Physics.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.DebugDisplay;
using Unity.Transforms;
using static Unity.Physics.Math;

namespace Unity.Physics.Authoring
{
#if UNITY_EDITOR

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct DisplayColliderFacesJob : IJobParallelFor
    {
        [ReadOnly] private NativeArray<RigidBody> RigidBodies;
        [ReadOnly] private NativeArray<BodyMotionType> BodiesMotionTypes;
        [ReadOnly] private PrimitiveColliderGeometries Geometries;
        [ReadOnly] private float CollidersFacesScale;

        public float3 Offset;

        internal static JobHandle ScheduleJob(in NativeArray<RigidBody> rigidBodies, in NativeArray<BodyMotionType> bodiesMotionTypes, float collidersFacesScale, in PrimitiveColliderGeometries geometries, JobHandle inputDeps, float3 offset)
        {
            return new DisplayColliderFacesJob
            {
                RigidBodies = rigidBodies,
                BodiesMotionTypes = bodiesMotionTypes,
                Geometries = geometries,
                CollidersFacesScale = collidersFacesScale,
		Offset = offset,
            }.Schedule(rigidBodies.Length, 1, inputDeps);
        }

        public void Execute(int i)
        {
            var rigidBody = RigidBodies[i];
            var bodyMotionType = BodiesMotionTypes[i];
            var collider = rigidBody.Collider;

            if (collider.IsCreated)
            {
                DrawColliderFaces(collider, rigidBody.WorldFromBody, bodyMotionType, rigidBody.Scale * CollidersFacesScale, Offset);
            }
        }

        private unsafe void DrawColliderFaces(BlobAssetReference<Collider> collider, RigidTransform worldFromCollider,
            BodyMotionType bodyMotionType, float uniformScale = 1.0f, float3 offset = default)
        {
            DrawColliderFaces((Collider*)collider.GetUnsafePtr(), worldFromCollider, bodyMotionType, uniformScale, offset: offset);
        }

        private unsafe void DrawColliderFaces(Collider* collider, RigidTransform worldFromCollider,
            BodyMotionType bodyMotionType, float uniformScale = 1.0f, float3 offset = default)
        {
            Quaternion colliderOrientation;
            float3 colliderPosition;
            float radius;

            ColorIndex color = DrawColliderUtility.GetColorIndex(bodyMotionType);
            switch (collider->Type)
            {
                case ColliderType.Cylinder:
                    radius = ((CylinderCollider*)collider)->Radius;
                    var height = ((CylinderCollider*)collider)->Height;
                    colliderPosition = ((CylinderCollider*)collider)->Center;
                    colliderOrientation = ((CylinderCollider*)collider)->Orientation * Quaternion.FromToRotation(Vector3.up, Vector3.back);

                    DrawColliderUtility.DrawPrimitiveCylinderFaces(radius, height, colliderPosition, colliderOrientation, worldFromCollider, ref Geometries.CylinderGeometry, color, uniformScale, offset);
                    break;

                case ColliderType.Box:
                    colliderPosition = ((BoxCollider*)collider)->Center;
                    var size = ((BoxCollider*)collider)->Size;
                    colliderOrientation = ((BoxCollider*)collider)->Orientation;

                    DrawColliderUtility.DrawPrimitiveBoxFaces(size, colliderPosition, colliderOrientation, worldFromCollider, ref Geometries.BoxGeometry, color, uniformScale, offset);
                    break;

                case ColliderType.Triangle:
                case ColliderType.Quad:
                case ColliderType.Convex:
                    DrawConvexFaces(ref ((ConvexCollider*)collider)->ConvexHull, worldFromCollider, color, uniformScale, offset);
                    break;

                case ColliderType.Sphere:
                    radius = ((SphereCollider*)collider)->Radius;
                    colliderPosition = ((SphereCollider*)collider)->Center;

                    DrawColliderUtility.DrawPrimitiveSphereFaces(radius, colliderPosition, worldFromCollider, ref Geometries.SphereGeometry, color, uniformScale, offset);
                    break;

                case ColliderType.Capsule:
                    radius = ((CapsuleCollider*)collider)->Radius;

                    var vertex0 = ((CapsuleCollider*)collider)->Vertex0;
                    var vertex1 = ((CapsuleCollider*)collider)->Vertex1;
                    var axis = vertex1 - vertex0; //axis in wfc-space

                    height = 0.5f * math.length(axis) + radius;

                    colliderPosition = (vertex1 + vertex0) / 2.0f; //axis in wfc-space
                    colliderOrientation = Quaternion.FromToRotation(Vector3.up, -axis);

                    DrawColliderUtility.DrawPrimitiveCapsuleFaces(radius, height, colliderPosition, colliderOrientation, worldFromCollider, ref Geometries.CapsuleGeometry, color, uniformScale, offset: offset);

                    break;

                case ColliderType.Mesh:
                    DrawMeshColliderFaces((MeshCollider*)collider, worldFromCollider, color, uniformScale, offset);
                    break;

                case ColliderType.Compound:
                    DrawCompoundColliderFaces((CompoundCollider*)collider, worldFromCollider, bodyMotionType,
                        uniformScale, offset);
                    break;

                case ColliderType.Terrain:
                    DrawTerrainColliderFaces((TerrainCollider*)collider, worldFromCollider, color, uniformScale, offset);
                    break;
            }
        }

        private static void DrawConvexFaces(ref ConvexHull hull, RigidTransform worldFromCollider,
            ColorIndex ci, float uniformScale = 1.0f, float3 offset = default)
        {
            var triangleVertices = new NativeList<float3>(Allocator.Temp);
            try
            {
                // set some best guess capacity, assuming we have on average of 3 triangles per face,
                // and given the fact that we need 3 vertices to define a triangle.
                const int kAvgTrianglesPerFace = 3;
                const int kNumVerticesPerTriangle = 3;
                triangleVertices.Capacity = hull.NumFaces * kAvgTrianglesPerFace * kNumVerticesPerTriangle;

                unsafe
                {
                    var vertexBuffer = stackalloc float3[ConvexCollider.k_MaxFaceVertices];
                    for (var f = 0; f < hull.NumFaces; f++)
                    {
                        var countVert = hull.Faces[f].NumVertices;

                        if (countVert == 3) // A triangle
                        {
                            for (var fv = 0; fv < countVert; fv++)
                            {
                                var origVertexIndex = hull.FaceVertexIndices[hull.Faces[f].FirstIndex + fv];
                                triangleVertices.Add(math.transform(worldFromCollider,
                                    uniformScale * hull.Vertices[origVertexIndex]));
                            }
                        }
                        else if (countVert == 4) // A quad: break into two triangles
                        {
                            for (var fv = 0; fv < countVert; fv++)
                            {
                                var origVertexIndex = hull.FaceVertexIndices[hull.Faces[f].FirstIndex + fv];
                                vertexBuffer[fv] = math.transform(worldFromCollider,
                                    uniformScale * hull.Vertices[origVertexIndex]);
                            }

                            // triangle 0, 1, 2
                            triangleVertices.Add(vertexBuffer[0]);
                            triangleVertices.Add(vertexBuffer[1]);
                            triangleVertices.Add(vertexBuffer[2]);
                            // triangle 2, 3, 0
                            triangleVertices.Add(vertexBuffer[2]);
                            triangleVertices.Add(vertexBuffer[3]);
                            triangleVertices.Add(vertexBuffer[0]);
                        }
                        else // find the average vertex and then use to break into triangles
                        {
                            // Todo: we can avoid using the centroid as an extra vertex by simply walking around the face
                            // and producing triangles with the first vertex and every next pair of vertices.

                            var faceCentroid = float3.zero;
                            for (var i = 0; i < countVert; i++)
                            {
                                var origVertexIndex = hull.FaceVertexIndices[hull.Faces[f].FirstIndex + i];
                                var scaledVertex = math.transform(worldFromCollider, uniformScale * hull.Vertices[origVertexIndex]);
                                faceCentroid += scaledVertex;

                                vertexBuffer[i] = scaledVertex;
                            }

                            faceCentroid /= countVert;

                            for (var j = 0; j < countVert; j++)
                            {
                                var vertices = new float3x3();
                                if (j < countVert - 1)
                                {
                                    vertices[0] = vertexBuffer[j];
                                    vertices[1] = vertexBuffer[j + 1];
                                }
                                else //close the circle of triangles
                                {
                                    vertices[0] = vertexBuffer[j];
                                    vertices[1] = vertexBuffer[0];
                                }

                                vertices[2] = faceCentroid;

                                triangleVertices.Add(vertices[0]);
                                triangleVertices.Add(vertices[1]);
                                triangleVertices.Add(vertices[2]);
                            }
                        }
                    }
                }

                PhysicsDebugDisplaySystem.Triangles(triangleVertices, ci, offset);
            }
            finally
            {
                triangleVertices.Dispose();
            }
        }

        private unsafe void DrawCompoundColliderFaces(CompoundCollider* compoundCollider, RigidTransform worldFromCollider,
            BodyMotionType bodyMotionType, float uniformScale = 1.0f, float3 offset = default)
        {
            for (var i = 0; i < compoundCollider->Children.Length; i++)
            {
                ref CompoundCollider.Child child = ref compoundCollider->Children[i];

                ScaledMTransform mWorldFromCompound = new ScaledMTransform(worldFromCollider, uniformScale);
                ScaledMTransform mWorldFromChild = ScaledMTransform.Mul(mWorldFromCompound, new MTransform(child.CompoundFromChild));
                RigidTransform worldFromChild = new RigidTransform(mWorldFromChild.Rotation, mWorldFromChild.Translation);

                DrawColliderFaces(child.Collider, worldFromChild, bodyMotionType, uniformScale, offset: offset);
            }
        }

        private static unsafe void DrawMeshColliderFaces(MeshCollider* meshCollider, RigidTransform worldFromCollider,
            DebugDisplay.ColorIndex ci, float uniformScale = 1.0f, float3 offset = default)
        {
            ref Mesh mesh = ref meshCollider->Mesh;

            float4x4 worldMatrix = new float4x4(worldFromCollider);
            worldMatrix.c0 *= uniformScale;
            worldMatrix.c1 *= uniformScale;
            worldMatrix.c2 *= uniformScale;

            var triangleVertices = new NativeList<float3>(Allocator.Temp);
            try
            {
                // calculate upper bound triangle count with max 2 triangles per primitive
                int maxTriangleCount = 0;
                const int kMaxTrianglesPerPrimitive = 2;
                for (int sectionIndex = 0; sectionIndex < mesh.Sections.Length; sectionIndex++)
                {
                    ref Mesh.Section section = ref mesh.Sections[sectionIndex];
                    maxTriangleCount += section.PrimitiveVertexIndices.Length * kMaxTrianglesPerPrimitive;
                }

                // set max capacity for vertex list, given that we need three vertices per triangle
                const int kNumVerticesPerTriangle = 3;
                triangleVertices.Capacity = maxTriangleCount * kNumVerticesPerTriangle;

                for (int sectionIndex = 0; sectionIndex < mesh.Sections.Length; sectionIndex++)
                {
                    ref Mesh.Section section = ref mesh.Sections[sectionIndex];
                    for (int primitiveIndex = 0; primitiveIndex < section.PrimitiveVertexIndices.Length; primitiveIndex++)
                    {
                        Mesh.PrimitiveVertexIndices vertexIndices = section.PrimitiveVertexIndices[primitiveIndex];
                        Mesh.PrimitiveFlags flags = section.PrimitiveFlags[primitiveIndex];
                        var numTriangles = 1;
                        if ((flags & Mesh.PrimitiveFlags.IsTrianglePair) != 0)
                        {
                            numTriangles = 2;
                        }

                        float3x4 v = new float3x4(
                            math.transform(worldMatrix, section.Vertices[vertexIndices.A]),
                            math.transform(worldMatrix, section.Vertices[vertexIndices.B]),
                            math.transform(worldMatrix, section.Vertices[vertexIndices.C]),
                            math.transform(worldMatrix, section.Vertices[vertexIndices.D]));

                        for (int triangleIndex = 0; triangleIndex < numTriangles; triangleIndex++)
                        {
                            triangleVertices.Add(v[0]);
                            triangleVertices.Add(v[1 + triangleIndex]);
                            triangleVertices.Add(v[2 + triangleIndex]);
                        }
                    }
                }

                PhysicsDebugDisplaySystem.Triangles(triangleVertices, ci, offset);
            }
            finally
            {
                triangleVertices.Dispose();
            }
        }

        private static unsafe void DrawTerrainColliderFaces(TerrainCollider* terrainCollider, RigidTransform worldFromCollider,
            DebugDisplay.ColorIndex ci, float uniformScale = 1.0f, float3 offset = default)
        {
            ref Terrain terrain = ref terrainCollider->Terrain;

            float4x4 worldMatrix = new float4x4(worldFromCollider);
            worldMatrix.c0 *= uniformScale;
            worldMatrix.c1 *= uniformScale;
            worldMatrix.c2 *= uniformScale;

            // calculate the number of triangles in the terrain
            int numCellsX = terrain.Size.x - 1;
            int numCellsY = terrain.Size.y - 1;
            int numTriangles = numCellsX * numCellsY * 2;

            // allocate triangle vertex array for the required number of triangles
            const int kNumVerticesPerTriangle = 3;
            var triangleVertices = new NativeArray<float3>(numTriangles * kNumVerticesPerTriangle, Allocator.Temp);
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
                        float3 v3 = math.transform(worldMatrix,
                            new float3(i1, terrain.Heights[i1 + terrain.Size.x * j1], j1) * terrain.Scale);

                        // add two triangles for each cell

                        // triangle 1
                        triangleVertices[vertexIndex++] = v2;
                        triangleVertices[vertexIndex++] = v1;
                        triangleVertices[vertexIndex++] = v0;

                        // triangle 2
                        triangleVertices[vertexIndex++] = v2;
                        triangleVertices[vertexIndex++] = v3;
                        triangleVertices[vertexIndex++] = v1;
                    }
                }

                PhysicsDebugDisplaySystem.Triangles(triangleVertices, ci, offset);
            }
            finally
            {
                triangleVertices.Dispose();
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PhysicsDebugDisplayGroup))]
    [BurstCompile]
    internal partial struct DisplayBodyCollidersSystem : ISystem
    {
        private PrimitiveColliderGeometries DefaultGeometries;
        private EntityQuery StaticBodyQuery;
        private EntityQuery DynamicBodyQuery;

        public void OnCreate(ref SystemState state)
        {
            var colliderQuery = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsCollider>(),
                ComponentType.ReadOnly<LocalToWorld>());

            DynamicBodyQuery = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsCollider>(),
                ComponentType.ReadOnly<LocalToWorld>(),
                ComponentType.ReadOnly<PhysicsMass>());

            StaticBodyQuery = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsCollider>(),
                ComponentType.ReadOnly<LocalToWorld>(),
                ComponentType.Exclude<PhysicsMass>());

            state.RequireForUpdate(colliderQuery);
            state.RequireForUpdate<PhysicsDebugDisplayData>();

            DrawColliderUtility.CreateGeometries(out DefaultGeometries);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out PhysicsDebugDisplayData debugDisplay) || debugDisplay.DrawColliders == 0)
                return;

            switch (debugDisplay.ColliderDisplayMode)
            {
                case PhysicsDebugDisplayMode.PreIntegration:
                {
                    state.EntityManager.CompleteDependencyBeforeRO<PhysicsWorldSingleton>();
                    if (SystemAPI.TryGetSingleton(out PhysicsWorldSingleton physicsWorldSingleton))
                    {
                        ref var physicsWorld = ref physicsWorldSingleton.PhysicsWorld;
                        var bodyMotionTypes =
                            new NativeArray<BodyMotionType>(physicsWorld.NumBodies, Allocator.TempJob);

                        DrawColliderUtility.GetBodiesMotionTypesFromWorld(ref physicsWorld, ref bodyMotionTypes);

                        var displayHandle = DisplayColliderFacesJob.ScheduleJob(
                            physicsWorldSingleton.PhysicsWorld.Bodies,
                            bodyMotionTypes, 1.0f, DefaultGeometries, state.Dependency, debugDisplay.Offset);
                        var disposeHandle = bodyMotionTypes.Dispose(displayHandle);
                        state.Dependency = disposeHandle;
                    }
                    break;
                }
                case PhysicsDebugDisplayMode.PostIntegration:
                {
                    var rigidBodies = new NativeList<RigidBody>(Allocator.TempJob);
                    var bodyMotionTypes = new NativeList<BodyMotionType>(Allocator.TempJob);

                    DrawColliderUtility.GetBodiesByMotionsFromQuery(ref state, ref DynamicBodyQuery, ref rigidBodies, ref bodyMotionTypes);
                    DrawColliderUtility.GetBodiesByMotionsFromQuery(ref state, ref StaticBodyQuery, ref rigidBodies, ref bodyMotionTypes);

                    var displayHandle = DisplayColliderFacesJob.ScheduleJob(rigidBodies.AsArray(),
                        bodyMotionTypes.AsArray(), 1.0f, DefaultGeometries, state.Dependency, debugDisplay.Offset);
                    var disposeHandle = JobHandle.CombineDependencies(rigidBodies.Dispose(displayHandle),
                        bodyMotionTypes.Dispose(displayHandle));
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
    [BurstCompile]
    internal partial struct DisplayBodyCollidersSystem_Editor : ISystem
    {
        private PrimitiveColliderGeometries DefaultGeometries;
        private EntityQuery ColliderQuery;

        public void OnCreate(ref SystemState state)
        {
            ColliderQuery = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsCollider>(),
                ComponentType.ReadOnly<LocalToWorld>());
            state.RequireForUpdate(ColliderQuery);
            state.RequireForUpdate<PhysicsDebugDisplayData>();

            DrawColliderUtility.CreateGeometries(out DefaultGeometries);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out PhysicsDebugDisplayData debugDisplay))
                return;

            if (debugDisplay.DrawColliders > 0)
            {
                var rigidBodies = new NativeList<RigidBody>(Allocator.TempJob);
                var bodyMotionTypes = new NativeList<BodyMotionType>(Allocator.TempJob);

                DrawColliderUtility.GetBodiesByMotionsFromQuery(ref state, ref ColliderQuery, ref rigidBodies, ref bodyMotionTypes);

                var displayHandle = DisplayColliderFacesJob.ScheduleJob(rigidBodies.AsArray(),
                    bodyMotionTypes.AsArray(), 1.0f, DefaultGeometries, state.Dependency, debugDisplay.Offset);
                var disposeHandle = JobHandle.CombineDependencies(rigidBodies.Dispose(displayHandle),
                    bodyMotionTypes.Dispose(displayHandle));
                state.Dependency = disposeHandle;
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            DefaultGeometries.Dispose();
        }
    }

#endif
}
