using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using Unity.Physics.Tests.Utils;

namespace Unity.Physics.Tests.Collision.Colliders
{
    class CylinderColliderTests
    {
        #region Construction

        [BurstCompile(CompileSynchronously = true)]
        struct CreateFromBurstJob : IJob
        {
            [GenerateTestsForBurstCompatibility]
            public void Execute() => CylinderCollider.Create(new CylinderGeometry
            {
                Orientation = quaternion.identity,
                Height = 1f,
                Radius = 1f,
                SideCount = CylinderGeometry.MaxSideCount
            }).Dispose();
        }

        [Test]
        public void CylinderCollider_Create_WhenCalledFromBurstJob_DoesNotThrow() => new CreateFromBurstJob().Run();

        unsafe void ValidateCylinderCollider(BlobAssetReference<Collider> collider, in CylinderGeometry geometry)
        {
            // manually created colliders are unique by design
            Assert.IsTrue(collider.Value.IsUnique);

            Assert.AreEqual(ColliderType.Cylinder, collider.Value.Type);
            Assert.AreEqual(CollisionType.Convex, collider.Value.CollisionType);

            ref var cylinderCollider = ref UnsafeUtility.AsRef<CylinderCollider>(collider.GetUnsafePtr());

            Assert.AreEqual(geometry.Center, cylinderCollider.Center);
            Assert.AreEqual(geometry.Center, cylinderCollider.Geometry.Center);
            Assert.AreEqual(geometry.Orientation, cylinderCollider.Orientation);
            Assert.AreEqual(geometry.Orientation, cylinderCollider.Geometry.Orientation);
            Assert.AreEqual(geometry.Height, cylinderCollider.Height);
            Assert.AreEqual(geometry.Height, cylinderCollider.Geometry.Height);
            Assert.AreEqual(geometry.Radius, cylinderCollider.Radius);
            Assert.AreEqual(geometry.Radius, cylinderCollider.Geometry.Radius);
            Assert.AreEqual(geometry.BevelRadius, cylinderCollider.BevelRadius);
            Assert.AreEqual(geometry.BevelRadius, cylinderCollider.Geometry.BevelRadius);
            Assert.AreEqual(CollisionType.Convex, cylinderCollider.CollisionType);
            Assert.AreEqual(ColliderType.Cylinder, cylinderCollider.Type);
        }

        [Test]
        public void CylinderCollider_Create_ResultHasExpectedValues()
        {
            var geometry = new CylinderGeometry
            {
                Center = new float3(-10.10f, 10.12f, 0.01f),
                Orientation = quaternion.AxisAngle(math.normalize(new float3(1.4f, 0.2f, 1.1f)), 38.50f),
                Height = 2f,
                Radius = 0.25f,
                BevelRadius = 0.05f,
                SideCount = 10
            };

            using var collider = CylinderCollider.Create(geometry);
            ValidateCylinderCollider(collider, geometry);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void CylinderCollider_Create_WhenCenterInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN)] float errantValue
        )
        {
            var geometry = new CylinderGeometry
            {
                Center = new float3(errantValue),
                Orientation = quaternion.identity,
                SideCount = CylinderGeometry.MaxSideCount
            };

            var ex = Assert.Throws<ArgumentException>(() => CylinderCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match(nameof(CylinderGeometry.Center)));
        }

        [Test]
        public void CylinderCollider_Create_WhenOrientationInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN, 0f)] float errantValue
        )
        {
            var geometry = new CylinderGeometry
            {
                Orientation = new quaternion(0f, 0f, 0f, errantValue),
                SideCount = CylinderGeometry.MaxSideCount
            };

            var ex = Assert.Throws<ArgumentException>(() => CylinderCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match(nameof(CylinderGeometry.Orientation)));
        }

        [Test]
        public void CylinderCollider_Create_WhenHeightInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN, -1f)] float errantValue
        )
        {
            var geometry = new CylinderGeometry
            {
                Height = errantValue,
                Orientation = quaternion.identity,
                SideCount = CylinderGeometry.MaxSideCount
            };

            var ex = Assert.Throws<ArgumentException>(() => CylinderCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match(nameof(CylinderGeometry.Height)));
        }

        [Test]
        public void CylinderCollider_Create_WhenRadiusInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN, -1f)] float errantValue
        )
        {
            var geometry = new CylinderGeometry
            {
                Radius = errantValue,
                Orientation = quaternion.identity,
                SideCount = CylinderGeometry.MaxSideCount
            };

            var ex = Assert.Throws<ArgumentException>(() => CylinderCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match(nameof(CylinderGeometry.Radius)));
        }

        [Test]
        public void CylinderCollider_Create_WhenBevelRadiusInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN, -1f, 0.55f)] float errantValue
        )
        {
            var geometry = new CylinderGeometry
            {
                Height = 1f,
                Radius = 0.5f,
                Orientation = quaternion.identity,
                BevelRadius = errantValue,
                SideCount = CylinderGeometry.MaxSideCount
            };

            var ex = Assert.Throws<ArgumentException>(() => CylinderCollider.Create(geometry));
            Assert.That(ex.Message, Does.Match(nameof(CylinderGeometry.BevelRadius)));
        }

#endif

        #endregion

        #region Utilities

        [Test]
        public void TestCylinderColliderToMesh()
        {
            var geometry = new CylinderGeometry()
            {
                Center = new float3(1, 2, 3),
                Orientation = quaternion.identity,
                Height = 4,
                Radius = 1,
                SideCount = CylinderGeometry.MaxSideCount
            };

            using var cylinderCollider = CylinderCollider.Create(geometry);

            var expectedSize = new float3(geometry.Radius * 2, geometry.Radius * 2, geometry.Height);
            var aabb = cylinderCollider.Value.CalculateAabb(RigidTransform.identity);
            TestUtils.AreEqual(geometry.Center, aabb.Center, math.EPSILON);
            TestUtils.AreEqual(expectedSize, aabb.Extents, math.EPSILON);

            var mesh = cylinderCollider.Value.ToMesh();
            TestUtils.AreEqual(geometry.Center, mesh.bounds.center, math.EPSILON);
            TestUtils.AreEqual(expectedSize, mesh.bounds.size, math.EPSILON);

            UnityEngine.Object.DestroyImmediate(mesh);
        }

        #endregion
    }
}
