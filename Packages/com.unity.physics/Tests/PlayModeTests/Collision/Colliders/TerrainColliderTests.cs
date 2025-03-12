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
    class TerrainColliderTests
    {
        #region Construction

        [BurstCompile(CompileSynchronously = true)]
        struct CreateFromBurstJob : IJob
        {
            public TerrainCollider.CollisionMethod CollisionMethod;

            [GenerateTestsForBurstCompatibility]
            public void Execute()
            {
                using var heights = new NativeArray<float>(16, Allocator.Temp);
                TerrainCollider.Create(heights, new int2(4, 4), new float3(1f), CollisionMethod).Dispose();
            }
        }

        [Test]
        public void TerrainCollider_Create_WhenCalledFromBurstJob_DoesNotThrow(
            [Values] TerrainCollider.CollisionMethod collisionMethod
        ) =>
            new CreateFromBurstJob { CollisionMethod = collisionMethod }
            .Run();

        unsafe void ValidateTerrainCollider(BlobAssetReference<Collider> collider, NativeArray<float> heights, int2 size, TerrainCollider.CollisionMethod collisionMethod)
        {
            // manually created colliders are unique by design
            Assert.IsTrue(collider.Value.IsUnique);

            // validate terrain collider
            Assert.AreEqual(ColliderType.Terrain, collider.Value.Type);
            var exepectedCollisionType = collisionMethod == TerrainCollider.CollisionMethod.Triangles
                ? CollisionType.Composite
                : CollisionType.Terrain;
            Assert.AreEqual(exepectedCollisionType, collider.Value.CollisionType);

            ref var terrainCollider = ref UnsafeUtility.AsRef<TerrainCollider>(collider.GetUnsafePtr());

            Assert.AreEqual(heights.Length, terrainCollider.Terrain.Heights.Length);
            Assert.AreEqual(size, terrainCollider.Terrain.Size);
        }

        [Test]
        public void TerrainCollider_Create_ResultHasExpectedValues([Values] TerrainCollider.CollisionMethod collisionMethod)
        {
            var heights = new NativeArray<float>(16, Allocator.Temp);
            var size = new int2(4, 4);
            try
            {
                for (int i = 0; i < heights.Length; ++i)
                {
                    heights[i] = i;
                }

                using var collider = TerrainCollider.Create(heights, size, 1, collisionMethod);
                ValidateTerrainCollider(collider, heights, size, collisionMethod);

                using var colliderClone = collider.Value.Clone();
                ValidateTerrainCollider(colliderClone, heights, size, collisionMethod);
            }
            finally
            {
                heights.Dispose();
            }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void TerrainCollider_Create_WhenSizeOutOfRange_Throws(
            [Values(0, 1)] int errantDimension
        )
        {
            var size = new int2(2) { [errantDimension] = 1 };
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => TerrainCollider.Create(default, size, default, default));
            Assert.That(ex.ParamName, Is.EqualTo("size"));
        }

        [Test]
        public void TerrainCollider_Create_WhenScaleOutOfRange_Throws(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.NaN)] float errantValue
        )
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => TerrainCollider.Create(default, new int2(2), new float3(errantValue), default));
            Assert.That(ex.ParamName, Is.EqualTo("scale"));
        }

#endif

        #endregion

        #region Utilities

        [Test]
        public void TestTerrainColliderToMesh([Values] TerrainCollider.CollisionMethod collisionMethod)
        {
            var heights = new NativeArray<float>(16, Allocator.Temp);
            var size = new int2(4, 4);
            try
            {
                for (int i = 0; i < heights.Length; ++i)
                {
                    heights[i] = i;
                }

                using var collider = TerrainCollider.Create(heights, size, 1, collisionMethod);
                var aabb = collider.Value.CalculateAabb(RigidTransform.identity);
                var mesh = collider.Value.ToMesh();
                TestUtils.AreEqual(aabb.Center, mesh.bounds.center, math.EPSILON);
                TestUtils.AreEqual(aabb.Extents, mesh.bounds.size, math.EPSILON);

                UnityEngine.Object.DestroyImmediate(mesh);
            }
            finally
            {
                heights.Dispose();
            }
        }

        #endregion
    }
}
