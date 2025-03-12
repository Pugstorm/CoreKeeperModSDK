using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using TestUtils = Unity.Physics.Tests.Utils.TestUtils;

namespace Unity.Physics.Tests.Collision.Colliders
{
    /// <summary>
    /// Contains all test for the <see cref=" PolygonCollider"/>
    /// </summary>
    class PolygonColliderTests
    {
        #region Construction

        [BurstCompile(CompileSynchronously = true)]
        struct CreateQuadFromBurstJob : IJob
        {
            [GenerateTestsForBurstCompatibility]
            public void Execute() =>
                PolygonCollider.CreateQuad(new float3(-1f, 1f, 0f), new float3(1f, 1f, 0f), new float3(1f, -1f, 0f), new float3(-1f, -1f, 0f)).Dispose();
        }

        [Test]
        public void PolygonCollider_CreateQuad_WhenCalledFromBurstJob_DoesNotThrow() => new CreateQuadFromBurstJob().Run();

        [BurstCompile(CompileSynchronously = true)]
        struct CreateTriangleFromBurstJob : IJob
        {
            [GenerateTestsForBurstCompatibility]
            public void Execute() =>
                PolygonCollider.CreateTriangle(new float3(-1f, 1f, 0f), new float3(1f, 1f, 0f), new float3(1f, -1f, 0f)).Dispose();
        }

        [Test]
        public void PolygonCollider_CreateTriangle_WhenCalledFromBurstJob_DoesNotThrow() => new CreateTriangleFromBurstJob().Run();


        unsafe void ValidateTriangleCollider(BlobAssetReference<Collider> collider, float3[] vertices, float3 normal)
        {
            // manually created colliders are unique by design
            Assert.IsTrue(collider.Value.IsUnique);

            var triangleCollider = UnsafeUtility.AsRef<PolygonCollider>(collider.GetUnsafePtr());
            Assert.IsTrue(triangleCollider.IsTriangle);
            Assert.IsFalse(triangleCollider.IsQuad);

            TestUtils.AreEqual(triangleCollider.Vertices[0], vertices[0], 1e-3f);
            TestUtils.AreEqual(triangleCollider.Vertices[1], vertices[1], 1e-3f);
            TestUtils.AreEqual(triangleCollider.Vertices[2], vertices[2], 1e-3f);
            Assert.AreEqual(2, triangleCollider.Planes.Length);
            TestUtils.AreEqual(normal, triangleCollider.Planes[0].Normal, 1e-3f);
            TestUtils.AreEqual(-normal, triangleCollider.Planes[1].Normal, 1e-3f);
            Assert.AreEqual(ColliderType.Triangle, triangleCollider.Type);
            Assert.AreEqual(CollisionType.Convex, triangleCollider.CollisionType);
        }

        /// <summary>
        /// Test whether a triangle collider's attributes are set to the expected values after creation.
        /// </summary>
        [Test]
        public void TestCreateTriangle()
        {
            float3[] vertices =
            {
                new float3(-1.4f, 1.4f, 5.6f),
                new float3(1.4f, 1.4f, 3.6f),
                new float3(0.2f, 1.2f, 5.6f)
            };
            float3 normal = math.normalize(math.cross(vertices[1] - vertices[0], vertices[2] - vertices[0]));

            using var collider = PolygonCollider.CreateTriangle(vertices[0], vertices[1], vertices[2]);
            ValidateTriangleCollider(collider, vertices, normal);

            using var colliderClone = collider.Value.Clone();
            ValidateTriangleCollider(colliderClone, vertices, normal);
        }

        unsafe void ValidateQuadCollider(BlobAssetReference<Collider> collider, float3[] expectedVertices, float3 expectedNormal)
        {
            // manually created colliders are unique by design
            Assert.IsTrue(collider.Value.IsUnique);

            ref var quadCollider = ref UnsafeUtility.AsRef<PolygonCollider>(collider.GetUnsafePtr());
            Assert.IsFalse(quadCollider.IsTriangle);
            Assert.IsTrue(quadCollider.IsQuad);

            TestUtils.AreEqual(quadCollider.Vertices[0], expectedVertices[0], 1e-3f);
            TestUtils.AreEqual(quadCollider.Vertices[1], expectedVertices[1], 1e-3f);
            TestUtils.AreEqual(quadCollider.Vertices[2], expectedVertices[2], 1e-3f);
            TestUtils.AreEqual(quadCollider.Vertices[3], expectedVertices[3], 1e-3f);
            Assert.AreEqual(2, quadCollider.Planes.Length);
            TestUtils.AreEqual(expectedNormal, quadCollider.Planes[0].Normal, 1e-3f);
            TestUtils.AreEqual(-expectedNormal, quadCollider.Planes[1].Normal, 1e-3f);
            Assert.AreEqual(ColliderType.Quad, quadCollider.Type);
            Assert.AreEqual(CollisionType.Convex, quadCollider.CollisionType);
        }

        /// <summary>
        /// Test whether a quad collider's attributes are set correctly after creation.
        /// </summary>
        [Test]
        public void TestCreateQuad()
        {
            float3[] vertices =
            {
                new float3(-4.5f, 0.0f, 1.0f),
                new float3(3.4f, 0.7f, 1.0f),
                new float3(3.4f, 2.7f, 1.0f),
                new float3(-3.4f, 1.2f, 1.0f)
            };
            float3 normal = math.normalize(math.cross(vertices[2] - vertices[1], vertices[0] - vertices[1]));

            using var collider = PolygonCollider.CreateQuad(vertices[0], vertices[1], vertices[2], vertices[3]);
            ValidateQuadCollider(collider, vertices, normal);

            using var colliderClone = collider.Value.Clone();
            ValidateQuadCollider(colliderClone, vertices, normal);
        }

        /// <summary>
        /// Test that 'unsorted', i.e. neither clockwise nor counter-clockwise winding, quads are created correctly
        /// </summary>
        [Test]
        unsafe public void TestCreateQuadUnsorted()
        {
            float3[] vertices =
            {
                new float3(-4.5f, 0.0f, 1.0f),
                new float3(3.4f, 2.7f, 1.0f),
                new float3(3.4f, 0.7f, 1.0f),
                new float3(-3.4f, 1.2f, 1.0f)
            };
            float3 normal = math.normalize(math.cross(vertices[2] - vertices[1], vertices[0] - vertices[1]));

            using var collider = PolygonCollider.CreateQuad(vertices[0], vertices[1], vertices[2], vertices[3]);
            var quadCollider = UnsafeUtility.AsRef<PolygonCollider>(collider.GetUnsafePtr());
            Assert.IsFalse(quadCollider.IsTriangle);
            Assert.IsTrue(quadCollider.IsQuad);

            TestUtils.AreEqual(quadCollider.Vertices[0], vertices[0], 1e-3f);
            TestUtils.AreEqual(quadCollider.Vertices[1], vertices[1], 1e-3f);
            TestUtils.AreEqual(quadCollider.Vertices[2], vertices[2], 1e-3f);
            TestUtils.AreEqual(quadCollider.Vertices[3], vertices[3], 1e-3f);
            Assert.AreEqual(2, quadCollider.Planes.Length);
            TestUtils.AreEqual(normal, quadCollider.Planes[0].Normal, 1e-3f);
            TestUtils.AreEqual(-normal, quadCollider.Planes[1].Normal, 1e-3f);
            Assert.AreEqual(ColliderType.Quad, quadCollider.Type);
            Assert.AreEqual(CollisionType.Convex, quadCollider.CollisionType);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void PolygonCollider_CreateTriangle_WhenArgOutOfRange_Throws(
            [Values(0, 1, 2)] int errantArg,
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN)] float errantValue
        )
        {
            var vertices = new[]
            {
                math.select(default, new float3(errantValue), errantArg == 0),
                math.select(default, new float3(errantValue), errantArg == 1),
                math.select(default, new float3(errantValue), errantArg == 2)
            };

            var ex = Assert.Throws<ArgumentException>(() => PolygonCollider.CreateTriangle(vertices[0], vertices[1], vertices[2]));
            Assert.That(ex.ParamName, Is.EqualTo($"vertex{errantArg}"));
        }

        [Test]
        public void PolygonCollider_CreateQuad_WhenArgOutOfRange_Throws(
            [Values(0, 1, 2, 3)] int errantArg,
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN)] float errantValue
        )
        {
            var vertices = new[]
            {
                math.select(default, new float3(errantValue), errantArg == 0),
                math.select(default, new float3(errantValue), errantArg == 1),
                math.select(default, new float3(errantValue), errantArg == 2),
                math.select(default, new float3(errantValue), errantArg == 3)
            };

            var ex = Assert.Throws<ArgumentException>(() => PolygonCollider.CreateQuad(vertices[0], vertices[1], vertices[2],
                vertices[3]));
            Assert.That(ex.ParamName, Is.EqualTo($"vertex{errantArg}"));
        }

        [TestCase(
            -4.5f, 0.0f, 1.0f,
            3.4f, 0.7f, 1.0f,
            3.4f, 2.7f, 1.0f,
            -3.4f, 1.2f, 1.1f,
            TestName = "Sorted quad vertices"
         )]
        [TestCase(
            -4.5f, -0.30f, 1.0f,
            3.4f, 2.7f, 1.0f,
            3.4f, -0.7f, 1.0f,
            -3.4f, 1.2f, 1.1f,
            TestName = "Unsorted quad vertices"
         )]
        public void PolygonCollider_CreateQuad_WhenNotCoplanar_Throws(
            float v00, float v01, float v02,
            float v10, float v11, float v12,
            float v20, float v21, float v22,
            float v30, float v31, float v32
        )
        {
            Assert.Throws<ArgumentException>(() =>
            {
                PolygonCollider.CreateQuad(
                    new float3(v00, v01, v02),
                    new float3(v10, v11, v12),
                    new float3(v20, v21, v22),
                    new float3(v30, v31, v32)
                );
            });
        }

#endif

        #endregion

        #region IConvexCollider

        /// <summary>
        /// Test that the local AABB of a triangle collider is computed correctly
        /// </summary>
        [Test]
        public void TestCalculateAabbLocalTriangle()
        {
            float3[] vertices =
            {
                new float3(-1.8f, 2.4f, 4.6f),
                new float3(1.4f, 1.6f, 1.6f),
                new float3(0.2f, 1.2f, 3.6f)
            };

            using var collider = PolygonCollider.CreateTriangle(vertices[0], vertices[1], vertices[2]);
            Aabb aabb = collider.Value.CalculateAabb();

            Aabb expected = new Aabb()
            {
                Min = math.min(math.min(vertices[0], vertices[1]), vertices[2]),
                Max = math.max(math.max(vertices[0], vertices[1]), vertices[2])
            };

            TestUtils.AreEqual(expected.Min, aabb.Min, 1e-3f);
            TestUtils.AreEqual(expected.Max, aabb.Max, 1e-3f);
        }

        /// <summary>
        /// Test that the local AABB of a quad collider is calculated correctly
        /// </summary>
        [Test]
        public void TestCalculateAabbLocalQuad()
        {
            float3[] quadVertices =
            {
                new float3(-4.5f, 0.0f, 1.0f),
                new float3(3.4f, 0.7f, 1.0f),
                new float3(3.4f, 2.7f, 1.0f),
                new float3(-3.4f, 1.2f, 1.0f)
            };
            using var collider = PolygonCollider.CreateQuad(quadVertices[0], quadVertices[1], quadVertices[2], quadVertices[3]);
            Aabb aabb = collider.Value.CalculateAabb();
            Aabb expected = Aabb.CreateFromPoints(new float3x4(quadVertices[0], quadVertices[1], quadVertices[2], quadVertices[3]));

            TestUtils.AreEqual(expected.Min, aabb.Min, 1e-3f);
            TestUtils.AreEqual(expected.Max, aabb.Max, 1e-3f);
        }

        /// <summary>
        /// Test that the AABB of a transformed triangle collider is calculated correctly
        /// </summary>
        [Test]
        public void TestCalculateAabbTransformedTriangle()
        {
            float3[] vertices =
            {
                new float3(-1.8f, 2.4f, 4.6f),
                new float3(1.4f, 1.6f, 1.6f),
                new float3(0.2f, 1.2f, 3.6f)
            };

            float3 translation = new float3(3.4f, 2.5f, -1.1f);
            quaternion rotation = quaternion.AxisAngle(math.normalize(new float3(1.1f, 10.1f, -3.4f)), 78.0f);

            using var collider = PolygonCollider.CreateTriangle(vertices[0], vertices[1], vertices[2]);
            Aabb aabb = collider.Value.CalculateAabb(new RigidTransform(rotation, translation));

            for (int i = 0; i < 3; ++i)
            {
                vertices[i] = translation + math.mul(rotation, vertices[i]);
            }

            Aabb expected = new Aabb()
            {
                Min = math.min(math.min(vertices[0], vertices[1]), vertices[2]),
                Max = math.max(math.max(vertices[0], vertices[1]), vertices[2])
            };

            TestUtils.AreEqual(expected.Min, aabb.Min, 1e-3f);
            TestUtils.AreEqual(expected.Max, aabb.Max, 1e-3f);
        }

        /// <summary>
        /// Test that the AABB of a transformed quad collider is calculated correctly
        /// </summary>
        [Test]
        public void TestCalculateAabbTransformedQuad()
        {
            float3[] vertices =
            {
                new float3(-4.5f, 0.0f, 1.0f),
                new float3(3.4f, 0.7f, 1.0f),
                new float3(3.4f, 2.7f, 1.0f),
                new float3(-3.4f, 1.2f, 1.0f)
            };

            float3 translation = new float3(-3.4f, -2.5f, -1.1f);
            quaternion rotation = quaternion.AxisAngle(math.normalize(new float3(11.1f, 10.1f, -3.4f)), 178.0f);

            using var collider = PolygonCollider.CreateQuad(vertices[0], vertices[1], vertices[2], vertices[3]);
            Aabb aabb = collider.Value.CalculateAabb(new RigidTransform(rotation, translation));

            for (int i = 0; i < 4; ++i)
            {
                vertices[i] = translation + math.mul(rotation, vertices[i]);
            }

            Aabb expected = Aabb.CreateFromPoints(new float3x4(vertices[0], vertices[1], vertices[2], vertices[3]));

            TestUtils.AreEqual(expected.Min, aabb.Min, 1e-3f);
            TestUtils.AreEqual(expected.Max, aabb.Max, 1e-3f);
        }

        //[Test] // #TODO: Add test back in once we have implemented this in Physics
        unsafe public void TestMassPropertiesTriangle()
        {
            // constructing the triangle to be parallel to the xy plane so we don't have to figure out the transformations first
            float3[] vertices =
            {
                new float3(-1.1f, -0.4f, 0.0f),
                new float3(0.8f, -0.1f, 0.0f),
                new float3(-0.2f, 1.3f, 0.0f)
            };

            using var collider = PolygonCollider.CreateTriangle(vertices[0], vertices[1], vertices[2]);

            float3 inertiaTensor = collider.Value.MassProperties.MassDistribution.InertiaTensor;
            float3 expectedInertiaTensor = calcTriangleInertiaTensor(vertices[0], vertices[1], vertices[2]);
            TestUtils.AreEqual(expectedInertiaTensor, inertiaTensor, 1e-3f);
        }

        //[Test] // #TODO: Add test back in once we have implemented this in Physics
        public void TestMassPropertiesQuad()
        {
            float3[] vertices =
            {
                new float3(-1.1f, -0.4f, 0.0f),
                new float3(0.8f, -0.1f, 0.0f),
                new float3(1.2f, 1.3f, 0.0f),
                new float3(-0.2f, 1.3f, 0.0f)
            };

            using var collider = PolygonCollider.CreateQuad(vertices[0], vertices[1], vertices[2], vertices[3]);

            float3 inertiaTensor = collider.Value.MassProperties.MassDistribution.InertiaTensor;
            float3 expectedInertiaTensor = calcQuadInertiaTensor(vertices[0], vertices[1], vertices[2], vertices[3]);
            TestUtils.AreEqual(expectedInertiaTensor, inertiaTensor, 1e-3f);
        }

        private float3 calcTriangleInertiaTensor(float3 v0, float3 v1, float3 v2)
        {
            // #TODO: Add function once inertia is properly computed in Physics
            return new float3(0, 0, 0);
        }

        private float3 calcQuadInertiaTensor(float3 v0, float3 v1, float3 v2, float3 v3)
        {
            // #TODO: Add function once inertia is properly computed in Physics
            return new float3(0, 0, 0);
        }

        #endregion

        #region Utilities

        void ValidateMeshCreation(ref Collider collider)
        {
            var aabb = collider.CalculateAabb(RigidTransform.identity);
            var mesh = collider.ToMesh();
            TestUtils.AreEqual(aabb.Center, mesh.bounds.center, math.EPSILON);
            TestUtils.AreEqual(aabb.Extents, mesh.bounds.size, math.EPSILON);

            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void TestTriangleColliderToMesh()
        {
            float3[] vertices =
            {
                new(-1.4f, 1.4f, 5.6f),
                new(1.4f, 1.4f, 3.6f),
                new(0.2f, 1.2f, 5.6f)
            };
            using var triangleCollider = PolygonCollider.CreateTriangle(vertices[0], vertices[1], vertices[2]);
            ValidateMeshCreation(ref triangleCollider.Value);
        }

        [Test]
        public void TestQuadColliderToMesh()
        {
            float3[] vertices =
            {
                new(-4.5f, 0.0f, 1.0f),
                new(3.4f, 0.7f, 1.0f),
                new(3.4f, 2.7f, 1.0f),
                new(-3.4f, 1.2f, 1.0f)
            };
            using var quadCollider = PolygonCollider.CreateQuad(vertices[0], vertices[1], vertices[2], vertices[3]);
            ValidateMeshCreation(ref quadCollider.Value);
        }

        #endregion
    }
}
