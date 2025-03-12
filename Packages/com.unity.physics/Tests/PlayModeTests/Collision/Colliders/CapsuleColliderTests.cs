using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using TestUtils = Unity.Physics.Tests.Utils.TestUtils;

namespace Unity.Physics.Tests.Collision.Colliders
{
    /// <summary>
    /// Class collecting all tests for the <see cref="CapsuleCollider"/>
    /// </summary>
    class CapsuleColliderTests
    {
        #region Construction

        [BurstCompile(CompileSynchronously = true)]
        struct CreateFromBurstJob : IJob
        {
            [GenerateTestsForBurstCompatibility]
            public void Execute() =>
                CapsuleCollider.Create(new CapsuleGeometry { Vertex0 = math.up(), Vertex1 = -math.up(), Radius = 0.5f }).Dispose();
        }

        [Test]
        public void Capsule_Create_WhenCalledFromBurstJob_DoesNotThrow() => new CreateFromBurstJob().Run();

        unsafe void ValidateCapsuleCollider(Entities.BlobAssetReference<Collider> collider, in CapsuleGeometry geometry)
        {
            // manually created colliders are unique by design
            Assert.IsTrue(collider.Value.IsUnique);

            Assert.AreEqual(ColliderType.Capsule, collider.Value.Type);
            Assert.AreEqual(CollisionType.Convex, collider.Value.CollisionType);

            ref var capsuleCollider = ref UnsafeUtility.AsRef<CapsuleCollider>(collider.GetUnsafePtr());
            Assert.AreEqual(ColliderType.Capsule, capsuleCollider.Type);
            Assert.AreEqual(CollisionType.Convex, capsuleCollider.CollisionType);
            TestUtils.AreEqual(geometry.Vertex0, capsuleCollider.Vertex0);
            TestUtils.AreEqual(geometry.Vertex0, capsuleCollider.Geometry.Vertex0);
            TestUtils.AreEqual(geometry.Vertex1, capsuleCollider.Vertex1);
            TestUtils.AreEqual(geometry.Vertex1, capsuleCollider.Geometry.Vertex1);
            TestUtils.AreEqual(geometry.Radius, capsuleCollider.Radius);
            TestUtils.AreEqual(geometry.Radius, capsuleCollider.Geometry.Radius);
        }

        /// <summary>
        /// Test if all attributes are set as expected when creating a new <see cref="CapsuleCollider"/>.
        /// </summary>
        [Test]
        public void TestCapsuleColliderCreate()
        {
            var geometry = new CapsuleGeometry
            {
                Vertex0 = new float3(1.45f, 0.34f, -8.65f),
                Vertex1 = new float3(100.45f, -80.34f, -8.65f),
                Radius = 1.45f
            };
            using var collider = CapsuleCollider.Create(geometry);
            ValidateCapsuleCollider(collider, geometry);

            using var colliderClone = collider.Value.Clone();
            ValidateCapsuleCollider(colliderClone, geometry);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void CapsuleCollider_Create_WhenVertexInvalid_Throws(
            [Values(0, 1)] int errantArg,
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN)] float errantValue
        )
        {
            var v0 = math.select(default, new float3(errantValue), errantArg == 0);
            var v1 = math.select(default, new float3(errantValue), errantArg == 1);
            var geometry = new CapsuleGeometry { Vertex0 = v0, Vertex1 = v1 };

            var ex = Assert.Throws<ArgumentException>(() => CapsuleCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match($"Vertex{errantArg}"));
        }

        [Test]
        public void CapsuleCollider_Create_WhenRadiusInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN, -1f)] float errantValue
        )
        {
            var geometry = new CapsuleGeometry { Radius = errantValue };

            var ex = Assert.Throws<ArgumentException>(() => CapsuleCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match(nameof(CapsuleGeometry.Radius)));
        }

#endif

        #endregion

        #region IConvexCollider

        /// <summary>
        /// Test if the local AABB of the <see cref="CapsuleCollider"/> is calculated correctly.
        /// </summary>
        [Test]
        public void TestCapsuleColliderCalculateAabbLocal()
        {
            float radius = 2.3f;
            float length = 5.5f;
            float3 p0 = new float3(1.1f, 2.2f, 3.4f);
            float3 p1 = p0 + length * math.normalize(new float3(1, 1, 1));
            using var capsuleCollider = CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = p0,
                Vertex1 = p1,
                Radius = radius
            });

            Aabb expectedAabb = new Aabb();
            expectedAabb.Min = math.min(p0, p1) - new float3(radius);
            expectedAabb.Max = math.max(p0, p1) + new float3(radius);

            Aabb aabb = capsuleCollider.Value.CalculateAabb();
            TestUtils.AreEqual(expectedAabb.Min, aabb.Min, 1e-3f);
            TestUtils.AreEqual(expectedAabb.Max, aabb.Max, 1e-3f);
        }

        /// <summary>
        /// Test whether the AABB of the transformed <see cref="CapsuleCollider"/> is calculated correctly.
        /// </summary>
        [Test]
        public void TestCapsuleColliderCalculateAabbTransformed()
        {
            float radius = 2.3f;
            float length = 5.5f;
            float3 p0 = new float3(1.1f, 2.2f, 3.4f);
            float3 p1 = p0 + length * math.normalize(new float3(1, 1, 1));
            using var capsuleCollider = CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = p0,
                Vertex1 = p1,
                Radius = radius
            });

            float3 translation = new float3(-3.4f, 0.5f, 0.0f);
            quaternion rotation = quaternion.AxisAngle(math.normalize(new float3(0.4f, 0.0f, 150.0f)), 123.0f);

            Aabb expectedAabb = new Aabb();
            float3 p0Transformed = math.mul(rotation, p0) + translation;
            float3 p1Transformed = math.mul(rotation, p1) + translation;
            expectedAabb.Min = math.min(p0Transformed, p1Transformed) - new float3(radius);
            expectedAabb.Max = math.max(p0Transformed, p1Transformed) + new float3(radius);

            Aabb aabb = capsuleCollider.Value.CalculateAabb(new RigidTransform(rotation, translation));
            TestUtils.AreEqual(expectedAabb.Min, aabb.Min, 1e-3f);
            TestUtils.AreEqual(expectedAabb.Max, aabb.Max, 1e-3f);
        }

        /// <summary>
        /// Test whether the inertia tensor of the <see cref="CapsuleCollider"/> is calculated correctly.
        /// </summary>
        /// <remarks>
        /// Used the formula from the following article as reference: https://www.gamedev.net/articles/programming/math-and-physics/capsule-inertia-tensor-r3856/
        /// NOTE: There is an error in eq. 14 of the article: it should be H^2 / 4 instead of H^2 / 2 in Ixx and Izz.
        /// </remarks>
        [Test]
        public void TestCapsuleColliderMassProperties()
        {
            float radius = 2.3f;
            float length = 5.5f;
            float3 p0 = new float3(1.1f, 2.2f, 3.4f);
            float3 p1 = p0 + length * math.normalize(new float3(1, 1, 1));

            float hemisphereMass = 0.5f * 4.0f / 3.0f * (float)math.PI * radius * radius * radius;
            float cylinderMass = (float)math.PI * radius * radius * length;
            float totalMass = 2.0f * hemisphereMass + cylinderMass;
            hemisphereMass /= totalMass;
            cylinderMass /= totalMass;

            float itX = cylinderMass * (length * length / 12.0f + radius * radius / 4.0f) + 2.0f * hemisphereMass * (2.0f * radius * radius / 5.0f + length * length / 4.0f + 3.0f * length * radius / 8.0f);
            float itY = cylinderMass * radius * radius / 2.0f + 4.0f * hemisphereMass * radius * radius / 5.0f;
            float itZ = itX;
            float3 expectedInertiaTensor = new float3(itX, itY, itZ);

            using var capsuleCollider = CapsuleCollider.Create(new CapsuleGeometry
            {
                Vertex0 = p0,
                Vertex1 = p1,
                Radius = radius
            });
            float3 inertiaTensor = capsuleCollider.Value.MassProperties.MassDistribution.InertiaTensor;
            TestUtils.AreEqual(expectedInertiaTensor, inertiaTensor, 1e-3f);
        }

        #endregion

        #region Utilities

        [Test]
        public void TestCapsuleColliderToMesh()
        {
            var capsuleHeightY = 2f;
            var geometry = new CapsuleGeometry()
            {
                Radius = 1f,
                Vertex0 = new float3(0, -capsuleHeightY / 2, 0),
                Vertex1 = new float3(0, capsuleHeightY / 2, 0)
            };

            using var capsuleCollider = CapsuleCollider.Create(geometry);

            var center = 0.5f * (geometry.Vertex0 + geometry.Vertex1);
            var size = new float3(2 * geometry.Radius) + new float3(0, capsuleHeightY, 0);

            var aabb = capsuleCollider.Value.CalculateAabb(RigidTransform.identity);
            TestUtils.AreEqual(center, aabb.Center, math.EPSILON);
            TestUtils.AreEqual(size, aabb.Extents, math.EPSILON);

            var mesh = capsuleCollider.Value.ToMesh();
            const float kEps = 1e-6f;
            TestUtils.AreEqual(center, mesh.bounds.center, kEps);
            TestUtils.AreEqual(size, mesh.bounds.size, kEps);

            UnityEngine.Object.DestroyImmediate(mesh);
        }

        #endregion
    }
}
