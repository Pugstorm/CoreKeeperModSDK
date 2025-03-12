using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Collections;
using Unity.DebugDisplay;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine.SocialPlatforms;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace Unity.Physics.Authoring
{
    internal readonly struct ColliderGeometry : IDisposable
    {
        internal readonly NativeArray<Vector3> VerticesArray;
        internal readonly NativeArray<int> IndicesArray;
        internal readonly NativeArray<Vector3> EdgesArray;

        internal ColliderGeometry(NativeArray<Vector3> vertices, NativeArray<int> indices, NativeArray<Vector3> edgesArray)
        {
            VerticesArray = vertices;
            IndicesArray = indices;
            EdgesArray = edgesArray;
        }

        public void Dispose()
        {
            VerticesArray.Dispose();
            IndicesArray.Dispose();
            EdgesArray.Dispose();
        }
    }

    internal struct PrimitiveColliderGeometries : IDisposable
    {
        internal ColliderGeometry CapsuleGeometry;
        internal ColliderGeometry BoxGeometry;
        internal ColliderGeometry CylinderGeometry;
        internal ColliderGeometry SphereGeometry;

        public void Dispose()
        {
            CapsuleGeometry.Dispose();
            BoxGeometry.Dispose();
            CylinderGeometry.Dispose();
            SphereGeometry.Dispose();
        }
    }

    static class DrawColliderUtility
    {
        private static readonly ColorIndex DebugDynamicColor = ColorIndex.DynamicMesh;
        private static readonly ColorIndex DebugStaticColor = ColorIndex.StaticMesh;
        private static readonly ColorIndex DebugKinematicColor = ColorIndex.KinematicMesh;

#if UNITY_EDITOR
        private static void CreateGeometryArray(MeshType meshType, out ColliderGeometry outGeometry)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            DebugMeshCache.GetMesh(meshType).GetVertices(vertices);
            DebugMeshCache.GetMesh(meshType).GetIndices(indices, 0);

            // We want to simplify the capsule wireframe
            if (meshType == MeshType.Capsule)
            {
                outGeometry = new ColliderGeometry(
                    vertices.ToNativeArray(Allocator.Persistent),
                    indices.ToNativeArray(Allocator.Persistent),
                    DrawColliderUtility.CreateCapsuleWireFrame(Allocator.Persistent));
            }
            else
            {
                outGeometry = new ColliderGeometry(
                    vertices.ToNativeArray(Allocator.Persistent),
                    indices.ToNativeArray(Allocator.Persistent),
                    new NativeArray<Vector3>(0, Allocator.Persistent));
            }
        }

        public static void GetRigidBodiesFromQuery(ref SystemState state, ref EntityQuery query, ref NativeList<RigidBody> rigidBodiesList)
        {
            using var entities = query.ToEntityArray(Allocator.Temp);
            var manager = state.EntityManager;
            foreach (var entity in entities)
            {
                var collider = state.EntityManager.GetComponentData<PhysicsCollider>(entity);
                GetRigidBodyTransform(ref manager, entity, out var transform, out var scale);
                rigidBodiesList.Add(CreateRigidBody(transform, scale, collider.Value));
            }
        }

        public static void GetBodiesByMotionsFromQuery(ref SystemState state, ref EntityQuery rigidBodiesQuery, ref NativeList<RigidBody> rigidBodies,
            ref NativeList<BodyMotionType> bodyMotionTypes)
        {
            using var dynamicEntities = rigidBodiesQuery.ToEntityArray(Allocator.Temp);
            var manager = state.EntityManager;
            foreach (var entity in dynamicEntities)
            {
                var collider = state.EntityManager.GetComponentData<PhysicsCollider>(entity);

                BodyMotionType motionType = BodyMotionType.Static;
                if (state.EntityManager.HasComponent<PhysicsMass>(entity))
                {
                    var physicsMass = state.EntityManager.GetComponentData<PhysicsMass>(entity);
                    motionType = physicsMass.IsKinematic ? BodyMotionType.Kinematic : BodyMotionType.Dynamic;
                }

                GetRigidBodyTransform(ref manager, entity, out var transform, out var scale);
                rigidBodies.Add(CreateRigidBody(transform, scale, collider.Value));
                bodyMotionTypes.Add(motionType);
            }
        }

        public static void GetBodiesMotionTypesFromWorld(ref PhysicsWorld physicsWorld, ref NativeArray<BodyMotionType> bodyMotionTypes)
        {
            for (int index = 0; index < physicsWorld.NumBodies; ++index)
            {
                if (index < physicsWorld.NumDynamicBodies)
                {
                    bodyMotionTypes[index] = physicsWorld.MotionVelocities[index].IsKinematic
                        ? BodyMotionType.Kinematic
                        : BodyMotionType.Dynamic;
                }
                else
                {
                    bodyMotionTypes[index] = BodyMotionType.Static;
                }
            }
        }

        static void GetRigidBodyTransform(ref EntityManager inManager, in Entity inEntity, out RigidTransform outTransform, out float outScale)
        {
            bool hasParent = inManager.HasComponent<Parent>(inEntity);
            bool hasLocalTransform = inManager.HasComponent<LocalTransform>(inEntity);

            if (hasParent || !hasLocalTransform)
            {
                var localToWorld = inManager.GetComponentData<LocalToWorld>(inEntity);
                outTransform = Math.DecomposeRigidBodyTransform(localToWorld.Value);
                outScale = 1;
            }
            else
            {
                var localTransform = inManager.GetComponentData<LocalTransform>(inEntity);
                outTransform = new RigidTransform(localTransform.Rotation, localTransform.Position);
                outScale = localTransform.Scale;
            }
        }

        static RigidBody CreateRigidBody(in RigidTransform worldTransform, in float scale, BlobAssetReference<Collider> collider)
        {
            return new RigidBody
            {
                Collider = collider,
                WorldFromBody = worldTransform,
                Scale = scale
            };
        }

        internal static void CreateGeometries(out PrimitiveColliderGeometries primitiveColliderGeometries)
        {
            CreateGeometryArray(MeshType.Capsule, out var capsuleGeometry);
            CreateGeometryArray(MeshType.Cube, out var boxGeometry);
            CreateGeometryArray(MeshType.Cylinder, out var cylinderGeometry);
            CreateGeometryArray(MeshType.Sphere, out var sphereGeometry);

            primitiveColliderGeometries = new PrimitiveColliderGeometries()
            {
                CapsuleGeometry = capsuleGeometry,
                BoxGeometry = boxGeometry,
                CylinderGeometry = cylinderGeometry,
                SphereGeometry = sphereGeometry
            };
        }

#endif

        public static ColorIndex GetColorIndex(BodyMotionType motionType)
        {
            if (motionType == BodyMotionType.Dynamic)
            {
                return DebugDynamicColor;
            }

            if (motionType == BodyMotionType.Kinematic)
            {
                return DebugKinematicColor;
            }

            return DebugStaticColor;
        }

        public static void DrawPrimitiveSphereEdges(float radius, float3 center, RigidTransform wfc, ref ColliderGeometry sphereGeometry, float uniformScale, float3 offset)
        {
            var edgesColor = DebugDisplay.ColorIndex.Green;
            var shapeScale = new float3(radius * 2.0f); // Radius to scale : Multiple by 2

            var sphereTransform = float4x4.TRS(center * uniformScale, Quaternion.identity, shapeScale * uniformScale);
            var worldTransform = math.mul(new float4x4(wfc), sphereTransform);

            var sphereVerticesWorld = new NativeArray<float3>(sphereGeometry.VerticesArray.Length, Allocator.Temp);
            try
            {
                for (int i = 0; i < sphereVerticesWorld.Length; ++i)
                {
                    sphereVerticesWorld[i] = math.transform(worldTransform, sphereGeometry.VerticesArray[i]);
                }

                PhysicsDebugDisplaySystem.TriangleEdges(sphereVerticesWorld, sphereGeometry.IndicesArray,
                    edgesColor, offset);
            }
            finally
            {
                sphereVerticesWorld.Dispose();
            }
        }

        public static void DrawPrimitiveSphereFaces(float radius, float3 center, RigidTransform wfc, ref ColliderGeometry sphereGeometry, ColorIndex color, float uniformScale, float3 offset)
        {
            var shapeScale = new float3(radius * 2.0f) * uniformScale;
            var sphereTransform = float4x4.TRS(center, Quaternion.identity, shapeScale);
            var worldTransform = math.mul(new float4x4(wfc), sphereTransform);

            var sphereVerticesWorld = new NativeArray<float3>(sphereGeometry.VerticesArray.Length, Allocator.Temp);
            try
            {
                for (int i = 0; i < sphereVerticesWorld.Length; ++i)
                {
                    sphereVerticesWorld[i] = math.transform(worldTransform, sphereGeometry.VerticesArray[i]);
                }

                PhysicsDebugDisplaySystem.Triangles(sphereVerticesWorld, sphereGeometry.IndicesArray, color, offset);
            }
            finally
            {
                sphereVerticesWorld.Dispose();
            }
        }

        public static void DrawPrimitiveCapsuleEdges(float radius, float height, float3 center, Quaternion orientation, RigidTransform wfc, ref ColliderGeometry capsuleGeometry, float uniformScale, float3 offset)
        {
            var edgesColor = ColorIndex.Green;
            var shapeScale = new float3(2.0f * radius, height, 2.0f * radius);
            var capsuleTransform = float4x4.TRS(center * uniformScale, orientation, shapeScale * uniformScale);
            var worldTransform = math.mul(new float4x4(wfc), capsuleTransform);

            var capsuleEdges = capsuleGeometry.EdgesArray;
            var lineVertices = new NativeArray<float3>(capsuleEdges.Length, Allocator.Temp);
            try
            {
                for (int i = 0; i < capsuleEdges.Length; i += 2)
                {
                    lineVertices[i] = math.transform(worldTransform, capsuleEdges[i]);
                    lineVertices[i + 1] = math.transform(worldTransform, capsuleEdges[i + 1]);
                }

                PhysicsDebugDisplaySystem.Lines(lineVertices, edgesColor, offset);
            }
            finally
            {
                lineVertices.Dispose();
            }
        }

        public static void DrawPrimitiveCapsuleFaces(float radius, float height, float3 center, Quaternion orientation, RigidTransform wfc, ref ColliderGeometry capsuleGeometry, ColorIndex color, float uniformScale, float3 offset)
        {
            var shapeScale = new float3(2.0f * radius, height, 2.0f * radius);
            var capsuleTransform = float4x4.TRS(center, orientation, uniformScale * shapeScale);
            var worldTransform = math.mul(new float4x4(wfc), capsuleTransform);

            var capsuleVerticesWorld = new NativeArray<float3>(capsuleGeometry.VerticesArray.Length, Allocator.Temp);
            try
            {
                for (int i = 0; i < capsuleVerticesWorld.Length; ++i)
                {
                    capsuleVerticesWorld[i] = math.transform(worldTransform, capsuleGeometry.VerticesArray[i]);
                }

                PhysicsDebugDisplaySystem.Triangles(capsuleVerticesWorld, capsuleGeometry.IndicesArray, color, offset);
            }
            finally
            {
                capsuleVerticesWorld.Dispose();
            }
        }

        public static void DrawPrimitiveBoxFaces(float3 size, float3 center, Quaternion orientation, RigidTransform wfc, ref ColliderGeometry boxGeometry, ColorIndex color, float uniformScale, float3 offset)
        {
            var boxVerticesWorld = new NativeArray<float3>(boxGeometry.VerticesArray.Length, Allocator.Temp);
            var boxTransform = float4x4.TRS(center, orientation, uniformScale * size);
            var worldTransform = math.mul(new float4x4(wfc), boxTransform);

            try
            {
                for (int i = 0; i < boxVerticesWorld.Length; ++i)
                {
                    boxVerticesWorld[i] = math.transform(worldTransform,  boxGeometry.VerticesArray[i]);
                }

                PhysicsDebugDisplaySystem.Triangles(boxVerticesWorld, boxGeometry.IndicesArray, color, offset);
            }
            finally
            {
                boxVerticesWorld.Dispose();
            }
        }

        public static void DrawPrimitiveCylinderEdges(float radius, float height, float3 center, Quaternion orientation, RigidTransform wfc, ref ColliderGeometry cylinderGeometry, float uniformScale, float3 offset)
        {
            var edgesColor = ColorIndex.Green;
            var shapeScale = new float3(2.0f * radius, height * 0.5f, 2.0f * radius) * uniformScale;
            var cylinderTransform = float4x4.TRS(center, orientation, shapeScale);
            var worldTransform = math.mul(new float4x4(wfc), cylinderTransform);
            var cylinderVerticesWorld = new NativeArray<float3>(cylinderGeometry.VerticesArray.Length, Allocator.Temp);

            try
            {
                for (int i = 0; i < cylinderVerticesWorld.Length; ++i)
                {
                    cylinderVerticesWorld[i] = math.transform(worldTransform, cylinderGeometry.VerticesArray[i]);
                }

                PhysicsDebugDisplaySystem.TriangleEdges(cylinderVerticesWorld, cylinderGeometry.IndicesArray, edgesColor, offset);
            }
            finally
            {
                cylinderVerticesWorld.Dispose();
            }
        }

        public static void DrawPrimitiveCylinderFaces(float radius, float height, float3 center, Quaternion orientation, RigidTransform wfc, ref ColliderGeometry cylinderGeometry, ColorIndex color, float uniformScale, float3 offset)
        {
            var shapeScale = new float3(2.0f * radius, height * 0.5f, 2.0f * radius);
            var cylinderTransform = float4x4.TRS(center, orientation, uniformScale * shapeScale);
            var worldTransform = math.mul(new float4x4(wfc), cylinderTransform);

            var cylinderVerticesWorld = new NativeArray<float3>(cylinderGeometry.VerticesArray.Length, Allocator.Temp);

            try
            {
                for (int i = 0; i < cylinderVerticesWorld.Length; ++i)
                {
                    cylinderVerticesWorld[i] = math.transform(worldTransform, cylinderGeometry.VerticesArray[i]);
                }

                PhysicsDebugDisplaySystem.Triangles(cylinderVerticesWorld, cylinderGeometry.IndicesArray, color, offset);
            }
            finally
            {
                cylinderVerticesWorld.Dispose();
            }
        }

        private static void SetDiscSectionPoints(NativeArray<Vector3> dest, int count, Vector3 normalAxis, Vector3 from, float angle, float radius)
        {
            Vector3 startingPoint = math.normalize(from) * radius;
            var step = angle * math.PI / 180f;
            var r = quaternion.AxisAngle(normalAxis, step / (count - 1));
            Vector3 tangent = startingPoint;
            for (int i = 0; i < count; i++)
            {
                dest[i] = tangent;
                tangent = math.rotate(r, tangent);
            }
        }

        private static void GetWireArcSegments(ref NativeArray<Vector3> segmentArray, int segmentIndex, Vector3 center,
            Vector3 normal, Vector3 from, float angle, float radius)
        {
            const int kMaxArcPoints = 15;
            NativeArray<Vector3> sPoints = new NativeArray<Vector3>(kMaxArcPoints, Allocator.Temp);

            SetDiscSectionPoints(sPoints, kMaxArcPoints, normal, from, angle, radius); //scaled by radius

            for (var i = 0; i < kMaxArcPoints; ++i)
                sPoints[i] = center + sPoints[i];

            var j = segmentIndex;
            for (var i = 0; i < kMaxArcPoints - 1; ++i)
            {
                segmentArray[j++] = sPoints[i];
                segmentArray[j++] = sPoints[i + 1];
            }
            sPoints.Dispose();
        }

        private static readonly float3[] s_HeightAxes = new float3[3]
        {
            Vector3.right,
            Vector3.up,
            Vector3.forward
        };

        private static readonly int[] s_NextAxis = new int[3] {1, 2, 0};

        // Create a wireframe capsule with a default orientation along the y-axis. Use the general method of DrawWireArc
        // declared in GizmoUtil.cpp and with CapsuleBoundsHandle.DrawWireframe(). Output is an array that comprises
        // pairs of vertices to be used in the drawing of Capsule Collider Edges.
        static NativeArray<Vector3> CreateCapsuleWireFrame(Allocator allocator)
        {
            const float radius = 0.5f;
            const float height = 2.0f;
            const int mHeightAxis = 1; //corresponds to the y-axis
            var center = new float3(0, 0, 0);

            var heightAx1 = s_HeightAxes[mHeightAxis];
            var heightAx2 = s_HeightAxes[s_NextAxis[mHeightAxis]];
            var heightAx3 = s_HeightAxes[s_NextAxis[s_NextAxis[mHeightAxis]]];

            var center1 = center + heightAx1 * (height * 0.5f - radius);
            var center2 = center - heightAx1 * (height * 0.5f - radius);

            // 15 segments * 2 float3s * 4 arcs + 4 lines * 2 float3s = 128 (+2 circles = 188)
            NativeArray<Vector3> capsuleEdges = new NativeArray<Vector3>(128, allocator); //or 188 if want extra circles

            GetWireArcSegments(ref capsuleEdges, 0, center1, heightAx2, heightAx3, 180f, radius);
            GetWireArcSegments(ref capsuleEdges, 30, center2, heightAx2, heightAx3, -180f, radius);

            GetWireArcSegments(ref capsuleEdges, 60, center1, heightAx3, heightAx2, -180f, radius);
            GetWireArcSegments(ref capsuleEdges, 90, center2, heightAx3, heightAx2, 180f, radius);

            // Lines to connect the two hemispheres:
            capsuleEdges[120] = center1 + heightAx3 * radius;
            capsuleEdges[121] = center2 + heightAx3 * radius;

            capsuleEdges[122] = center1 - heightAx3 * radius;
            capsuleEdges[123] = center2 - heightAx3 * radius;


            capsuleEdges[124] = center1 + heightAx2 * radius;
            capsuleEdges[125] = center2 + heightAx2 * radius;

            capsuleEdges[126] = center1 - heightAx2 * radius;
            capsuleEdges[127] = center2 - heightAx2 * radius;

            // If want to add the [xz] circles along the y-axis edges (adds 30 lines per use)
            //GetWireArcSegments(ref capsuleEdges, 128, center1, heightAx1, heightAx2, -360f, radius);
            //GetWireArcSegments(ref capsuleEdges, 158, center2, heightAx1, heightAx2, 360f, radius);

            return capsuleEdges;
        }
    }
}
