using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using Unity.Physics.Tests.Utils;
using Random = Unity.Mathematics.Random;

namespace Unity.Physics.Tests.Collision.Colliders
{
    /// <summary>
    /// Contains all <see cref="ConvexCollider"/> unit tests
    /// </summary>
    class ConvexColliderTests
    {
        #region Construction

        [BurstCompile(CompileSynchronously = true)]
        struct CreateFromBurstJob : IJob
        {
            [GenerateTestsForBurstCompatibility]
            public void Execute()
            {
                var points = new NativeArray<float3>(1024, Allocator.Temp);
                var random = new Random(1234);
                for (var i = 0; i < points.Length; ++i)
                    points[i] = random.NextFloat3(new float3(-1f), new float3(1f));
                ConvexCollider.Create(points, ConvexHullGenerationParameters.Default).Dispose();
            }
        }

        [Test]
        public void ConvexCollider_Create_WhenCalledFromBurstJob_DoesNotThrow() => new CreateFromBurstJob().Run();

        static readonly float3[] k_TestPoints =
        {
            new float3(1.45f, 8.67f, 3.45f),
            new float3(8.75f, 1.23f, 6.44f),
            new float3(100.34f, 5.33f, -2.55f),
            new float3(8.76f, 4.56f, -4.54f),
            new float3(9.75f, -0.45f, -8.99f),
            new float3(7.66f, 3.44f, 0.0f)
        };

        void ValidateConvexCollider(Entities.BlobAssetReference<Collider> collider)
        {
            // manually created colliders are unique by design
            Assert.IsTrue(collider.Value.IsUnique);

            Assert.AreEqual(ColliderType.Convex, collider.Value.Type);
            Assert.AreEqual(CollisionType.Convex, collider.Value.CollisionType);
        }

        /// <summary>
        /// Test that a <see cref="ConvexCollider"/> created with a point cloud has its attributes filled correctly
        /// </summary>
        [Test]
        public void TestConvexColliderCreate()
        {
            var points = new NativeArray<float3>(k_TestPoints, Allocator.Temp);
            using var collider = ConvexCollider.Create(
                points, new ConvexHullGenerationParameters { BevelRadius = 0.15f }, CollisionFilter.Default
            );

            ValidateConvexCollider(collider);
            using var colliderClone = collider.Value.Clone();
            ValidateConvexCollider(colliderClone);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void ConvexCollider_Create_WhenPointsInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN)] float errantValue
        )
        {
            var points = new NativeArray<float3>(k_TestPoints, Allocator.Temp) { [2] = new float3(errantValue) };

            var ex = Assert.Throws<ArgumentException>(() => ConvexCollider.Create(points, ConvexHullGenerationParameters.Default));
            Assert.That(ex.ParamName, Is.EqualTo("points"));
        }

        [Test]
        public void ConvexCollider_Create_WhenGenerationParametersBevelRadiusInvalid_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN, -1f)] float errantValue
        )
        {
            var points = new NativeArray<float3>(k_TestPoints, Allocator.Temp);
            var generationParameters = ConvexHullGenerationParameters.Default;
            generationParameters.BevelRadius = errantValue;

            var ex = Assert.Throws<ArgumentException>(() => ConvexCollider.Create(points, generationParameters));
            Assert.That(ex.ParamName, Is.EqualTo("generationParameters"));
        }

#endif

        #endregion

        #region IConvexCollider

        /// <summary>
        /// Test that the local AABB is computed correctly for a <see cref="ConvexCollider"/> created with a point cloud
        /// </summary>
        [Test]
        public unsafe void TestConvexColliderCalculateAabbLocal([Values(0, 0.01f, 1.25f)] float maxShrinkMovement)
        {
            var points = new NativeArray<float3>(6, Allocator.TempJob)
            {
                [0] = new float3(1.45f, 8.67f, 3.45f),
                [1] = new float3(8.75f, 1.23f, 6.44f),
                [2] = new float3(100.34f, 5.33f, -2.55f),
                [3] = new float3(8.76f, 4.56f, -4.54f),
                [4] = new float3(9.75f, -0.45f, -8.99f),
                [5] = new float3(7.66f, 3.44f, 0.0f)
            };

            Aabb expectedAabb = Aabb.CreateFromPoints(new float3x4(points[0], points[1], points[2], points[3]));
            expectedAabb.Include(points[4]);
            expectedAabb.Include(points[5]);

            using var collider = ConvexCollider.Create(
                points, new ConvexHullGenerationParameters { BevelRadius = maxShrinkMovement }, CollisionFilter.Default
            );
            points.Dispose();

            Aabb actualAabb = collider.Value.CalculateAabb();
            float convexRadius = ((ConvexCollider*)collider.GetUnsafePtr())->ConvexHull.ConvexRadius;
            float maxError = 1e-3f + maxShrinkMovement - convexRadius;
            TestUtils.AreEqual(expectedAabb.Min, actualAabb.Min, maxError);
            TestUtils.AreEqual(expectedAabb.Max, actualAabb.Max, maxError);
        }

        /// <summary>
        /// Test that the transformed AABB is computed correctly for a <see cref="ConvexCollider"/> created with a point cloud
        /// </summary>
        [Test]
        public unsafe void TestConvexColliderCalculateAabbTransformed([Values(0, 0.01f, 1.25f)] float maxShrinkMovement)
        {
            var points = new NativeArray<float3>(6, Allocator.TempJob)
            {
                [0] = new float3(1.45f, 8.67f, 3.45f),
                [1] = new float3(8.75f, 1.23f, 6.44f),
                [2] = new float3(100.34f, 5.33f, -2.55f),
                [3] = new float3(8.76f, 4.56f, -4.54f),
                [4] = new float3(9.75f, -0.45f, -8.99f),
                [5] = new float3(7.66f, 3.44f, 0.0f)
            };

            float3 translation = new float3(43.56f, -87.32f, -0.02f);
            quaternion rotation = quaternion.AxisAngle(math.normalize(new float3(8.45f, -2.34f, 0.82f)), 43.21f);

            float3[] transformedPoints = new float3[points.Length];
            for (int i = 0; i < points.Length; ++i)
            {
                transformedPoints[i] = translation + math.mul(rotation, points[i]);
            }

            Aabb expectedAabb = Aabb.CreateFromPoints(new float3x4(transformedPoints[0], transformedPoints[1], transformedPoints[2], transformedPoints[3]));
            expectedAabb.Include(transformedPoints[4]);
            expectedAabb.Include(transformedPoints[5]);

            using var collider = ConvexCollider.Create(
                points, new ConvexHullGenerationParameters { BevelRadius = maxShrinkMovement }, CollisionFilter.Default
            );
            points.Dispose();

            Aabb actualAabb = collider.Value.CalculateAabb(new RigidTransform(rotation, translation));
            float convexRadius = ((ConvexCollider*)collider.GetUnsafePtr())->ConvexHull.ConvexRadius;
            float maxError = 1e-3f + maxShrinkMovement - convexRadius;

            TestUtils.AreEqual(expectedAabb.Min, actualAabb.Min, maxError);
            TestUtils.AreEqual(expectedAabb.Max, actualAabb.Max, maxError);
        }

        #endregion

        #region Utilities

        [Test]
        public void TestConvexColliderToMesh()
        {
            using var points = new NativeArray<float3>(k_TestPoints, Allocator.Temp);
            var parameters = ConvexHullGenerationParameters.Default;
            parameters.BevelRadius = 0;
            using var convexCollider = ConvexCollider.Create(points, parameters);

            var aabb = convexCollider.Value.CalculateAabb(RigidTransform.identity);
            var mesh = convexCollider.Value.ToMesh();
            TestUtils.AreEqual(aabb.Center, mesh.bounds.center, math.EPSILON);
            TestUtils.AreEqual(aabb.Extents, mesh.bounds.size, math.EPSILON);

            UnityEngine.Object.DestroyImmediate(mesh);
        }

        #endregion
    }
}
