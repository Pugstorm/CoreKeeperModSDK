using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using Unity.Physics.Tests.Utils;
using Random = Unity.Mathematics.Random;

namespace Unity.Physics.Tests.Components
{
    partial class PhysicsColliderTests
    {
        #region TestUtilities
        static Entity CreateConvexColliderComponents(ColliderType colliderType, float desiredBevelRadius, EntityManager entityManager)
        {
            BlobAssetReference<Collider> colliderBlob = default;
            Entity body = entityManager.CreateEntity();
            Random rnd = new Random(42);
            switch (colliderType)
            {
                case ColliderType.Convex:
                {
                    // make sure we have at least three points for at least a single triangle
                    int numPoints = rnd.NextInt(3, 16);
                    var points = new NativeArray<float3>(numPoints, Allocator.TempJob);
                    for (int i = 0; i < numPoints; i++)
                    {
                        points[i] = rnd.NextFloat3(-1.0f, 1.0f);
                    }

                    var generationParameters = ConvexHullGenerationParameters.Default;
                    generationParameters.BevelRadius = desiredBevelRadius;
                    colliderBlob = ConvexCollider.Create(points, generationParameters);
                    points.Dispose();

                    break;
                }
                default:
                {
                    colliderBlob = TestUtils.GenerateRandomConvex(ref rnd, colliderType, 1.0f, 0.0f);

                    break;
                }
            }

            entityManager.AddComponentData(body, new PhysicsCollider
            {
                Value = colliderBlob
            });
            return body;
        }

        internal static void RunTest<S>(ColliderType type, float desiredBevelRadius)
            where S : SystemBase
        {
            using (var world = new World("Test World"))
            {
                var bodyEntity = CreateConvexColliderComponents(type, desiredBevelRadius, world.EntityManager);
                var system = world.GetOrCreateSystemManaged<S>();
                system.Update();
                world.EntityManager.GetComponentData<PhysicsCollider>(bodyEntity).Value.Dispose();
            }
        }

        #endregion

        #region TestFunctions
        // Test that a PhysicsCollider can be successfully converted to a UnityEngine.Mesh
        [Test]
        public void TestPhysicsColliderToMesh([Range((int)ColliderType.Convex, (int)ColliderType.Cylinder)] int colliderType)
        {
            RunTest<ToMeshConversion>((ColliderType)colliderType, 0.0f);
        }

        #endregion

        #region TestSystems

        [DisableAutoCreation]
        public partial class ToMeshConversion : SystemBase
        {
            protected override void OnUpdate()
            {
                int colliderCount = 0;
                foreach (var collider in SystemAPI.Query<RefRO<PhysicsCollider>>())
                {
                    // convert physics collider to a UnityEngine.Mesh
                    var mesh = collider.ValueRO.ToMesh();
                    Assert.That(mesh, Is.Not.Null);

                    // compare mesh bounds to collider aabb
                    var aabb = collider.ValueRO.Value.Value.CalculateAabb(RigidTransform.identity);
                    Assert.That(aabb.Extents, Is.Not.EqualTo(float3.zero));

                    const float kEps = 1e-5f;
                    TestUtils.AreEqual(aabb.Center, mesh.bounds.center, kEps);
                    TestUtils.AreEqual(aabb.Extents, mesh.bounds.size, kEps);

                    UnityEngine.Object.DestroyImmediate(mesh);

                    ++colliderCount;
                }

                Assert.That(colliderCount, Is.EqualTo(1));
            }
        }
        #endregion
    }
}
