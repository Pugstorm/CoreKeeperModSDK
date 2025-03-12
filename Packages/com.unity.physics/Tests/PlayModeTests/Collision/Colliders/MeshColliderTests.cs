using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using Unity.Physics.Extensions;
using Unity.Physics.Tests.Utils;
using Random = Unity.Mathematics.Random;

namespace Unity.Physics.Tests.Collision.Colliders
{
    /// <summary>
    /// Test class containing tests for the <see cref="MeshCollider"/>
    /// </summary>
    class MeshColliderTests
    {
        static void GenerateMeshData(in int numTriangles, out NativeArray<float3> vertices, out NativeArray<int3> triangles)
        {
            vertices = new NativeArray<float3>(numTriangles * 3, Allocator.Temp);
            triangles = new NativeArray<int3>(numTriangles, Allocator.Temp);

            for (int i = 0; i < numTriangles; i++)
            {
                int firstVertexIndex = i * 3;

                vertices[firstVertexIndex]     = new float3(firstVertexIndex    , 1f * (firstVertexIndex % 2)      , firstVertexIndex + 1);
                vertices[firstVertexIndex + 1] = new float3(firstVertexIndex + 1, 1f * ((firstVertexIndex + 1) % 2), firstVertexIndex + 2);
                vertices[firstVertexIndex + 2] = new float3(firstVertexIndex + 2, 1f * ((firstVertexIndex + 2) % 2), firstVertexIndex + 3);
                triangles[i] = new int3(firstVertexIndex, firstVertexIndex + 1, firstVertexIndex + 2);
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct CreateFromBurstJob : IJob
        {
            [GenerateTestsForBurstCompatibility]
            public void Execute()
            {
                GenerateMeshData(100, out var vertices, out var triangles);
                try
                {
                    using var collider = MeshCollider.Create(vertices, triangles);
                }
                finally
                {
                    vertices.Dispose();
                    triangles.Dispose();
                }
            }
        }

        [Test]
        public void MeshCollider_Create_WhenCalledFromBurstJob_DoesNotThrow() => new CreateFromBurstJob().Run();

        [BurstCompile(CompileSynchronously = true)]
        struct CreateFromMeshDataJob : IJobParallelFor
        {
            public NativeArray<UnityEngine.Mesh.MeshData> MeshData;

            [GenerateTestsForBurstCompatibility]
            public void Execute(int i)
            {
                using var colliderBlob = MeshCollider.Create(MeshData[i], CollisionFilter.Default, Material.Default);
                var physicsCollider = new PhysicsCollider() { Value = colliderBlob};
            }
        }

        [Test]
        public void MeshCollider_CreateFromMeshData_WhenCalledFromBurstJob_DoesNotThrow()
        {
            var meshData = new NativeArray<UnityEngine.Mesh.MeshData>(3, Allocator.TempJob);
            var engineMesh = DebugMeshCache.GetMesh(MeshType.Cube);
            using var meshDataArray = UnityEngine.Mesh.AcquireReadOnlyMeshData(engineMesh);
            for (int i = 0; i < 3; i++)
            {
                meshData[i] = meshDataArray[0];
            }

            new CreateFromMeshDataJob()
            {
                MeshData = meshData
            }.Run(3);
            meshData.Dispose();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct CreateFromMeshDataArrayJob : IJob
        {
            [ReadOnly] public UnityEngine.Mesh.MeshDataArray MeshDataArray;

            [GenerateTestsForBurstCompatibility]
            public void Execute()
            {
                using var colliderBlob = MeshCollider.Create(MeshDataArray, CollisionFilter.Default, Material.Default);
                var physicsCollider = new PhysicsCollider() { Value = colliderBlob};
            }
        }

        [Test]
        public void MeshCollider_CreateFromMeshDataArray_WhenCalledFromBurstJob_DoesNotThrow()
        {
            var engineMesh = DebugMeshCache.GetMesh(MeshType.Cube);
            using var meshDataArray = UnityEngine.Mesh.AcquireReadOnlyMeshData(engineMesh);

            new CreateFromMeshDataArrayJob()
            {
                MeshDataArray = meshDataArray,
            }.Run();
        }

        struct PolygonCounter : ILeafColliderCollector
        {
            public int NumTriangles;
            public int NumQuads;

            public unsafe void AddLeaf(ColliderKey key, ref ChildCollider leaf)
            {
                var collider = leaf.Collider;
                if (collider->Type == ColliderType.Quad)
                {
                    ++NumQuads;
                }
                else if (collider->Type == ColliderType.Triangle)
                {
                    ++NumTriangles;
                }
            }

            public void PushCompositeCollider(ColliderKeyPath compositeKey, Math.MTransform parentFromComposite, out Math.MTransform worldFromParent)
            {
                worldFromParent = new Math.MTransform();

                // does nothing
            }

            public void PopCompositeCollider(uint numCompositeKeyBits, Math.MTransform worldFromParent)
            {
                // does nothing
            }
        }

        unsafe void ValidateMeshCollider(BlobAssetReference<Collider> collider, int expectedTriangleCount, int expectedQuadCount, bool checkQuads = false)
        {
            // manually created colliders are unique by design
            Assert.IsTrue(collider.Value.IsUnique);

            Assert.AreEqual(ColliderType.Mesh, collider.Value.Type);
            Assert.AreEqual(CollisionType.Composite, collider.Value.CollisionType);

            // make sure the mesh collider contains the correct number of triangles
            ref var meshCollider = ref UnsafeUtility.AsRef<MeshCollider>(collider.GetUnsafePtr());
            int quadCount = 0;
            int triangleCount = 0;
            ref Mesh mesh = ref meshCollider.Mesh;
            for (int sectionIndex = 0; sectionIndex < mesh.Sections.Length; sectionIndex++)
            {
                ref Mesh.Section section = ref mesh.Sections[sectionIndex];
                for (int primitiveIndex = 0; primitiveIndex < section.PrimitiveVertexIndices.Length; primitiveIndex++)
                {
                    Mesh.PrimitiveVertexIndices vertexIndices = section.PrimitiveVertexIndices[primitiveIndex];
                    Mesh.PrimitiveFlags flags = section.PrimitiveFlags[primitiveIndex];
                    bool isTrianglePair = (flags & Mesh.PrimitiveFlags.IsTrianglePair) != 0;
                    bool isQuad = (flags & Mesh.PrimitiveFlags.IsQuad) != 0;
                    if (isQuad)
                    {
                        ++quadCount;
                    }
                    else if (isTrianglePair)
                    {
                        triangleCount += 2;
                    }
                    else
                    {
                        ++triangleCount;
                    }
                }
            }
            Assert.AreEqual(expectedTriangleCount, triangleCount);
            Assert.AreEqual(expectedQuadCount, quadCount);

            // check the same with a leaf collector
            var counter = new PolygonCounter();
            meshCollider.GetLeaves(ref counter);
            Assert.AreEqual(expectedTriangleCount, counter.NumTriangles);
            if (checkQuads) // Remove when ECSB-582 is resolved
            {
                Assert.AreEqual(expectedQuadCount, counter.NumQuads);
            }
        }

        /// <summary>
        /// Create a <see cref="MeshCollider"/> and check that all attributes are set as expected
        /// </summary>
        [Test]
        public void MeshCollider_Create_ResultHasExpectedValues()
        {
            const int kNumTriangles = 10;
            GenerateMeshData(kNumTriangles, out var vertices, out var triangles);
            try
            {
                using var collider = MeshCollider.Create(vertices, triangles);
                const int kExpectedQuadCount = 0;
                ValidateMeshCollider(collider, kNumTriangles, kExpectedQuadCount);

                using var colliderClone = collider.Value.Clone();
                ValidateMeshCollider(colliderClone, kNumTriangles, kExpectedQuadCount);
            }
            finally
            {
                vertices.Dispose();
                triangles.Dispose();
            }
        }

        static readonly TestCaseData[] k_MeshTypeTestCases =
        {
            new TestCaseData(MeshType.Cube, 0, 6).SetName("CubeMesh"),  // a mesh with just quads
            new TestCaseData(MeshType.Capsule, 768, 32).SetName("CapsuleMesh"),  // a mesh with triangles and quads
            new TestCaseData(MeshType.Icosahedron, 20, 0).SetName("IcosahedronMesh")  // a mesh with just triangles
        };

        [TestCaseSource(nameof(k_MeshTypeTestCases))]
        public void MeshCollider_CreateFromEngineMesh_ResultHasExpectedValues(MeshType meshType, int numTrianglesExpected, int numQuadsExpected)
        {
            UnityEngine.Mesh mesh = DebugMeshCache.GetMesh(meshType);
            var filter = CollisionFilter.Default;
            using var collider = MeshCollider.Create(mesh, filter, Material.Default);
            ValidateMeshCollider(collider, numTrianglesExpected, numQuadsExpected);

            using var colliderClone = collider.Value.Clone();
            ValidateMeshCollider(colliderClone, numTrianglesExpected, numQuadsExpected);
        }

        [TestCaseSource(nameof(k_MeshTypeTestCases))]
        public void MeshCollider_CreateFromEngineMeshDataArray_ResultHasExpectedValues(MeshType meshType, int numTrianglesExpected, int numQuadsExpected)
        {
            UnityEngine.Mesh mesh = DebugMeshCache.GetMesh(meshType);
            using var engineMeshDataArray = UnityEngine.Mesh.AcquireReadOnlyMeshData(mesh);
            var filter = CollisionFilter.Default;
            using var collider = MeshCollider.Create(engineMeshDataArray, filter, Material.Default);
            ValidateMeshCollider(collider, numTrianglesExpected, numQuadsExpected);

            using var colliderClone = collider.Value.Clone();
            ValidateMeshCollider(colliderClone, numTrianglesExpected, numQuadsExpected);
        }

        [TestCaseSource(nameof(k_MeshTypeTestCases))]
        public void MeshCollider_CreateFromEngineMeshData_ResultHasExpectedValues(MeshType meshType, int numTrianglesExpected, int numQuadsExpected)
        {
            UnityEngine.Mesh mesh = DebugMeshCache.GetMesh(meshType);
            using var engineMeshDataArray = UnityEngine.Mesh.AcquireReadOnlyMeshData(mesh);
            var filter = CollisionFilter.Default;
            using var collider = MeshCollider.Create(engineMeshDataArray[0], filter, Material.Default);
            ValidateMeshCollider(collider, numTrianglesExpected, numQuadsExpected);

            using var colliderClone = collider.Value.Clone();
            ValidateMeshCollider(colliderClone, numTrianglesExpected, numQuadsExpected);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary>
        /// Create <see cref="MeshCollider"/> with invalid triangle indices
        /// and ensure that the invalid index is detected
        /// </summary>
        [Test]
        public void MeshCollider_Create_WhenTriangleIndexOutOfRange_Throws()
        {
            GenerateMeshData(10, out var vertices, out var triangles);

            try
            {
                Random rnd = new Random(0x12345678);

                for (int i = 0; i < 100; i++)
                {
                    int indexToChange = rnd.NextInt(0, triangles.Length * 3 - 1);

                    int triangleIndex = indexToChange / 3;
                    int vertexInTriangle = indexToChange % 3;
                    int invalidValue = rnd.NextInt() * (rnd.NextBool() ? -1 : 1);

                    var triangle = triangles[triangleIndex];
                    triangle[vertexInTriangle] = invalidValue;
                    triangles[triangleIndex] = triangle;

                    Assert.Throws<ArgumentException>(() => MeshCollider.Create(vertices, triangles));

                    triangle[vertexInTriangle] = indexToChange;
                    triangles[triangleIndex] = triangle;
                }
            }
            finally
            {
                triangles.Dispose();
                vertices.Dispose();
            }
        }

#endif

        [Test]
        public void TestMeshColliderToMesh()
        {
            const int kNumTriangles = 100;
            GenerateMeshData(kNumTriangles, out var vertices, out var triangles);
            try
            {
                using var meshCollider = MeshCollider.Create(vertices, triangles);
                var aabb = meshCollider.Value.CalculateAabb(RigidTransform.identity);

                var mesh = meshCollider.Value.ToMesh();
                TestUtils.AreEqual(aabb.Center, mesh.bounds.center, math.EPSILON);
                TestUtils.AreEqual(aabb.Extents, mesh.bounds.size, math.EPSILON);
                TestUtils.AreEqual(kNumTriangles, mesh.triangles.Length / 3);

                UnityEngine.Object.DestroyImmediate(mesh);
            }
            finally
            {
                vertices.Dispose();
                triangles.Dispose();
            }
        }
    }
}
