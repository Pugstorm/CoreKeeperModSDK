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
    /// Test class containing tests for the <see cref="BoxCollider"/>
    /// </summary>
    class BoxColliderTests
    {
        #region Construction

        [BurstCompile(CompileSynchronously = true)]
        struct CreateFromBurstJob : IJob
        {
            [GenerateTestsForBurstCompatibility]
            public void Execute() =>
                BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = new float3(1f) }).Dispose();
        }

        [Test]
        public void BoxCollider_Create_WhenCalledFromBurstJob_DoesNotThrow() => new CreateFromBurstJob().Run();

        unsafe void ValidateBoxCollider(Entities.BlobAssetReference<Collider> collider, in BoxGeometry geometry)
        {
            // Note: manually created colliders are unique by design
            Assert.IsTrue(collider.Value.IsUnique);

            Assert.AreEqual(ColliderType.Box, collider.Value.Type);
            Assert.AreEqual(CollisionType.Convex, collider.Value.CollisionType);

            ref var boxCollider = ref UnsafeUtility.AsRef<BoxCollider>(collider.GetUnsafePtr());
            Assert.AreEqual(geometry.Center, boxCollider.Center);
            Assert.AreEqual(geometry.Center, boxCollider.Geometry.Center);
            Assert.AreEqual(geometry.Orientation, boxCollider.Orientation);
            Assert.AreEqual(geometry.Orientation, boxCollider.Geometry.Orientation);
            Assert.AreEqual(geometry.Size, boxCollider.Size);
            Assert.AreEqual(geometry.Size, boxCollider.Geometry.Size);
            Assert.AreEqual(geometry.BevelRadius, boxCollider.BevelRadius);
            Assert.AreEqual(geometry.BevelRadius, boxCollider.Geometry.BevelRadius);
            Assert.AreEqual(CollisionType.Convex, boxCollider.CollisionType);
            Assert.AreEqual(ColliderType.Box, boxCollider.Type);
        }

        /// <summary>
        /// Create a <see cref="BoxCollider"/> and check that all attributes are set as expected
        /// </summary>
        [Test]
        public void TestBoxColliderCreate()
        {
            var geometry = new BoxGeometry
            {
                Center = new float3(-10.10f, 10.12f, 0.01f),
                Orientation = quaternion.AxisAngle(math.normalize(new float3(1.4f, 0.2f, 1.1f)), 38.50f),
                Size = new float3(0.01f, 120.40f, 5.4f),
                BevelRadius = 0.0f
            };

            // create and validate
            using var collider = BoxCollider.Create(geometry);
            ValidateBoxCollider(collider, geometry);

            // clone and validate again
            using var colliderClone = collider.Value.Clone();
            ValidateBoxCollider(colliderClone, geometry);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void BoxCollider_Create_WhenCenterInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN)] float errantValue
        )
        {
            var geometry = new BoxGeometry { Center = new float3(errantValue), Size = new float3(1f), Orientation = quaternion.identity };

            var ex = Assert.Throws<ArgumentException>(() => BoxCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match(nameof(BoxGeometry.Center)));
        }

        [Test]
        public void BoxCollider_Create_WhenOrientationInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN, 0f)] float errantValue
        )
        {
            var geometry = new BoxGeometry { Size = new float3(1f), Orientation = new quaternion(0f, 0f, 0f, errantValue) };

            var ex = Assert.Throws<ArgumentException>(() => BoxCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match(nameof(BoxGeometry.Orientation)));
        }

        [Test]
        public void BoxCollider_Create_WhenSizeInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN, -1f)] float errantValue
        )
        {
            var geometry = new BoxGeometry { Size = new float3(errantValue), Orientation = quaternion.identity };

            var ex = Assert.Throws<ArgumentException>(() => BoxCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match(nameof(BoxGeometry.Size)));
        }

        [Test]
        public void BoxCollider_Create_WhenBevelRadiusInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN, -1f, 0.55f)] float errantValue
        )
        {
            var geometry = new BoxGeometry { Size = new float3(1f), Orientation = quaternion.identity, BevelRadius = errantValue};

            var ex = Assert.Throws<ArgumentException>(() => BoxCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match(nameof(BoxGeometry.BevelRadius)));
        }

#endif

        #endregion

        #region IConvexCollider

        /// <summary>
        /// Test that a translated box collider generates the correct local AABB
        /// </summary>
        /// <remarks>
        /// The following code was used to produce reference data for Aabbs:
        /// <code>
        /// private Aabb CalculateBoxAabbNaive(float3 center, quaternion orientation, float3 size, quaternion bRotation, float3 bTranslation)
        ///{
        ///    float3[] points = {
        ///        0.5f * new float3(-size.x, -size.y, -size.z),
        ///        0.5f * new float3(-size.x, -size.y, size.z),
        ///        0.5f * new float3(-size.x, size.y, -size.z),
        ///        0.5f * new float3(-size.x, size.y, size.z),
        ///        0.5f * new float3(size.x, -size.y, -size.z),
        ///        0.5f * new float3(size.x, -size.y, size.z),
        ///        0.5f * new float3(size.x, size.y, -size.z),
        ///        0.5f * new float3(size.x, size.y, size.z)
        ///    };
        ///
        ///    for (int i = 0; i < 8; ++i)
        ///    {
        ///        points[i] = center + math.mul(orientation, points[i]);
        ///        points[i] = bTranslation + math.mul(bRotation, points[i]);
        ///    }
        ///
        ///    Aabb result = Aabb.CreateFromPoints(new float3x4(points[0], points[1], points[2], points[3]));
        ///    for (int i = 4; i < 8; ++i)
        ///    {
        ///        result.Include(points[i]);
        ///    }
        ///    return result;
        ///}
        /// </code>
        /// </remarks>
        [Test]
        public void TestBoxColliderCalculateAabbLocalTranslation()
        {
            // Expected values in this test were generated using CalculateBoxAabbNaive above
            {
                var geometry = new BoxGeometry
                {
                    Center = new float3(-0.59f, 0.36f, 0.35f),
                    Orientation = quaternion.identity,
                    Size = new float3(2.32f, 10.87f, 16.49f),
                    BevelRadius = 0.25f
                };

                Aabb expectedAabb = new Aabb
                {
                    Min = new float3(-1.75f, -5.075f, -7.895f),
                    Max = new float3(0.57f, 5.795f, 8.595f)
                };

                using var boxCollider = BoxCollider.Create(geometry);
                Aabb aabb = boxCollider.Value.CalculateAabb();
                TestUtils.AreEqual(expectedAabb.Min, aabb.Min, 1e-3f);
                TestUtils.AreEqual(expectedAabb.Max, aabb.Max, 1e-3f);
            }
        }

        /// <summary>
        /// Test that the created inertia tensor of the <see cref="BoxCollider"/> is correct
        /// </summary>
        /// <remarks>
        /// Formula for inertia tensor from here was used: https://en.wikipedia.org/wiki/List_of_moments_of_inertia
        /// </remarks>
        [Test]
        public void TestBoxColliderMassProperties()
        {
            var geometry = new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = new float3(1.0f, 250.0f, 2.0f),
                BevelRadius = 0.25f
            };

            using var boxCollider = BoxCollider.Create(geometry);

            float3 expectedInertiaTensor = 1.0f / 12.0f * new float3(
                geometry.Size.y * geometry.Size.y + geometry.Size.z * geometry.Size.z,
                geometry.Size.x * geometry.Size.x + geometry.Size.z * geometry.Size.z,
                geometry.Size.y * geometry.Size.y + geometry.Size.x * geometry.Size.x);

            MassProperties massProperties = boxCollider.Value.MassProperties;
            float3 inertiaTensor = massProperties.MassDistribution.InertiaTensor;
            TestUtils.AreEqual(expectedInertiaTensor, inertiaTensor, 1e-3f);
        }

        #endregion

        #region Utilities

        [Test]
        public void TestBoxColliderToMesh()
        {
            var geometry = new BoxGeometry
            {
                Center = new float3(1, 2, 3),
                Orientation = quaternion.identity,
                Size = new float3(2, 3, 4),
            };

            using var boxCollider = BoxCollider.Create(geometry);

            var aabb = boxCollider.Value.CalculateAabb(RigidTransform.identity);
            TestUtils.AreEqual(geometry.Center, aabb.Center, math.EPSILON);
            TestUtils.AreEqual(geometry.Size, aabb.Extents, math.EPSILON);

            var mesh = boxCollider.Value.ToMesh();
            TestUtils.AreEqual(geometry.Center, mesh.bounds.center, math.EPSILON);
            TestUtils.AreEqual(geometry.Size, mesh.bounds.size, math.EPSILON);

            UnityEngine.Object.DestroyImmediate(mesh);
        }

        #endregion
    }
}
