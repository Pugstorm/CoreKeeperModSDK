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
    /// Contains all tests for the <see cref="SphereCollider"/>
    /// </summary>
    class SphereColliderTests
    {
        #region Construction

        [BurstCompile(CompileSynchronously = true)]
        struct CreateFromBurstJob : IJob
        {
            [GenerateTestsForBurstCompatibility]
            public void Execute() => SphereCollider.Create(new SphereGeometry { Radius = 1f }).Dispose();
        }

        [Test]
        public void SphereCollider_Create_WhenCalledFromBurstJob_DoesNotThrow() => new CreateFromBurstJob().Run();


        unsafe void ValidateSphereCollider(BlobAssetReference<Collider> collider, in SphereGeometry sphere)
        {
            // manually created colliders are unique by design
            Assert.IsTrue(collider.Value.IsUnique);

            Assert.AreEqual(ColliderType.Sphere, collider.Value.Type);
            Assert.AreEqual(CollisionType.Convex, collider.Value.CollisionType);

            ref var sphereCollider = ref UnsafeUtility.AsRef<SphereCollider>(collider.GetUnsafePtr());

            TestUtils.AreEqual(sphere.Center, sphereCollider.Center, 1e-3f);
            TestUtils.AreEqual(sphere.Center, sphereCollider.Geometry.Center, 1e-3f);
            TestUtils.AreEqual(sphere.Radius, sphereCollider.Radius, 1e-3f);
            TestUtils.AreEqual(sphere.Radius, sphereCollider.Geometry.Radius, 1e-3f);
        }

        /// <summary>
        /// Tests if a created <see cref="SphereCollider"/> has its attributes set correctly
        /// </summary>
        [Test]
        public void TestSphereColliderCreate()
        {
            var sphere = new SphereGeometry
            {
                Center = new float3(-8.45f, 9.65f, -0.10f),
                Radius = 0.98f
            };

            using var collider = SphereCollider.Create(sphere);
            ValidateSphereCollider(collider, sphere);

            using var colliderClone = collider.Value.Clone();
            ValidateSphereCollider(colliderClone, sphere);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void SphereCollider_Create_WhenCenterInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN)] float value
        )
        {
            var geometry = new SphereGeometry { Center = new float3(value) };

            var ex = Assert.Throws<ArgumentException>(() => SphereCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match(nameof(SphereGeometry.Center)));
        }

        [Test]
        public void SphereCollider_Create_WhenRadiusInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN, -1f)] float value
        )
        {
            var geometry = new SphereGeometry { Radius = value };

            var ex = Assert.Throws<ArgumentException>(() => SphereCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match(nameof(SphereGeometry.Radius)));
        }

#endif

        #endregion

        #region IConvexCollider

        /// <summary>
        /// Test that the local AABB of a <see cref="SphereCollider"/> are calculated correctly
        /// </summary>
        [Test]
        public void TestSphereColliderCalculateAabbLocal()
        {
            float3 center = new float3(-8.4f, 5.63f, -7.2f);
            float radius = 2.3f;
            using var sphereCollider = SphereCollider.Create(new SphereGeometry { Center = center, Radius = radius });

            Aabb expected = new Aabb();
            expected.Min = center - new float3(radius, radius, radius);
            expected.Max = center + new float3(radius, radius, radius);

            Aabb actual = sphereCollider.Value.CalculateAabb();
            TestUtils.AreEqual(expected.Min, actual.Min, 1e-3f);
            TestUtils.AreEqual(expected.Max, actual.Max, 1e-3f);
        }

        /// <summary>
        /// Test that the AABB of a transformed <see cref="SphereCollider"/> is calculated correctly
        /// </summary>
        [Test]
        public void TestSphereColliderCalculateAabbTransformed()
        {
            float3 center = new float3(-3.4f, 0.63f, -17.2f);
            float radius = 5.3f;
            using var sphereCollider = SphereCollider.Create(new SphereGeometry { Center = center, Radius = radius });

            float3 translation = new float3(8.3f, -0.5f, 170.0f);
            quaternion rotation = quaternion.AxisAngle(math.normalize(new float3(1.1f, 4.5f, 0.0f)), 146.0f);

            Aabb expected = new Aabb();
            expected.Min = math.mul(rotation, center) + translation - new float3(radius, radius, radius);
            expected.Max = math.mul(rotation, center) + translation + new float3(radius, radius, radius);

            Aabb actual = sphereCollider.Value.CalculateAabb(new RigidTransform(rotation, translation));
            TestUtils.AreEqual(expected.Min, actual.Min, 1e-3f);
            TestUtils.AreEqual(expected.Max, actual.Max, 1e-3f);
        }

        /// <summary>
        /// Test that the inertia tensor of a <see cref="SphereCollider"/> is calculated correctly
        /// </summary>
        /// <remarks>
        /// Inertia tensor formula taken from here: https://en.wikipedia.org/wiki/List_of_moments_of_inertia
        /// </remarks>
        [Test]
        public void TestSphereColliderMassProperties()
        {
            float3 center = new float3(-8.4f, 5.63f, 77.2f);
            float radius = 2.3f;
            using var sphereCollider = SphereCollider.Create(new SphereGeometry { Center = center, Radius = radius });

            float inertia = 2.0f / 5.0f * radius * radius;
            float3 expectedInertiaTensor = new float3(inertia, inertia, inertia);
            float3 inertiaTensor = sphereCollider.Value.MassProperties.MassDistribution.InertiaTensor;
            TestUtils.AreEqual(expectedInertiaTensor, inertiaTensor, 1e-3f);
        }

        #endregion

        #region Utilities

        [Test]
        public void TestSphereColliderToMesh()
        {
            var geometry = new SphereGeometry()
            {
                Center = new float3(1, 2, 3),
                Radius = 1
            };

            using var sphereCollider = SphereCollider.Create(geometry);

            var expectedSize = new float3(geometry.Radius * 2);
            var aabb = sphereCollider.Value.CalculateAabb(RigidTransform.identity);
            TestUtils.AreEqual(geometry.Center, aabb.Center, math.EPSILON);
            TestUtils.AreEqual(expectedSize, aabb.Extents, math.EPSILON);

            var mesh = sphereCollider.Value.ToMesh();
            TestUtils.AreEqual(geometry.Center, mesh.bounds.center, math.EPSILON);
            TestUtils.AreEqual(expectedSize, mesh.bounds.size, math.EPSILON);

            UnityEngine.Object.DestroyImmediate(mesh);
        }

        #endregion
    }
}
