using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Entities;

using Assert = UnityEngine.Assertions.Assert;

namespace Unity.Physics.Tests.Collision.RigidBody
{
    class RigidBodyTest
    {
        [Test]
        public unsafe void RigidBodyCalculateAabb_BoxColliderTest()
        {
            var geometry = new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = 1.0f,
                BevelRadius = 0.2f
            };

            Physics.RigidBody rigidbodyBox = Unity.Physics.RigidBody.Zero;
            using var collider = BoxCollider.Create(geometry);
            rigidbodyBox.Collider = collider;

            var boxAabb = rigidbodyBox.CalculateAabb();
            using var collider2 = BoxCollider.Create(geometry);
            var boxCollider = (BoxCollider*)collider2.GetUnsafePtr();
            Assert.IsTrue(boxAabb.Equals(boxCollider->CalculateAabb()));
        }

        [Test]
        public unsafe void RigidBodyCalculateAabb_SphereColliderTest()
        {
            var geometry = new SphereGeometry
            {
                Center = float3.zero,
                Radius = 1.0f
            };

            Physics.RigidBody rigidbodySphere = Unity.Physics.RigidBody.Zero;
            using var collider = SphereCollider.Create(geometry);
            rigidbodySphere.Collider = collider;

            var sphereAabb = rigidbodySphere.CalculateAabb();
            using var collider2 = SphereCollider.Create(geometry);
            var sphere = (Collider*)collider2.GetUnsafePtr();
            Assert.IsTrue(sphereAabb.Equals(sphere->CalculateAabb()));
        }

        [Test]
        public unsafe void RigidBodyCastRayTest()
        {
            Physics.RigidBody rigidbody = Unity.Physics.RigidBody.Zero;

            const float size = 1.0f;
            const float convexRadius = 0.0f;

            var rayStartOK = new float3(-10, -10, -10);
            var rayEndOK = new float3(10, 10, 10);

            var rayStartFail = new float3(-10, 10, -10);
            var rayEndFail = new float3(10, 10, 10);

            using var collider = BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = size,
                BevelRadius = convexRadius
            });
            rigidbody.Collider = collider;

            var raycastInput = new RaycastInput();
            var closestHit = new RaycastHit();
            var allHits = new NativeList<RaycastHit>(Allocator.Temp);

            // OK case : Ray hits the box collider
            raycastInput.Start = rayStartOK;
            raycastInput.End = rayEndOK;
            raycastInput.Filter = CollisionFilter.Default;

            Assert.IsTrue(rigidbody.CastRay(raycastInput));
            Assert.IsTrue(rigidbody.CastRay(raycastInput, out closestHit));
            Assert.IsTrue(rigidbody.CastRay(raycastInput, ref allHits));

            // Fail Case : wrong direction
            raycastInput.Start = rayStartFail;
            raycastInput.End = rayEndFail;

            Assert.IsFalse(rigidbody.CastRay(raycastInput));
            Assert.IsFalse(rigidbody.CastRay(raycastInput, out closestHit));
            Assert.IsFalse(rigidbody.CastRay(raycastInput, ref allHits));
        }

        [Test]
        public unsafe void RigidBodyCastColliderTest()
        {
            Physics.RigidBody rigidbody = Unity.Physics.RigidBody.Zero;

            const float size = 1.0f;
            const float convexRadius = 0.0f;
            const float sphereRadius = 1.0f;

            var rayStartOK = new float3(-10, -10, -10);
            var rayEndOK = new float3(10, 10, 10);

            var rayStartFail = new float3(-10, 10, -10);
            var rayEndFail = new float3(10, 10, 10);

            using var collider = BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = size,
                BevelRadius = convexRadius
            });
            rigidbody.Collider = collider;

            var colliderCastInput = new ColliderCastInput();
            var closestHit = new ColliderCastHit();
            var allHits = new NativeList<ColliderCastHit>(Allocator.Temp);

            // OK case : Sphere hits the box collider
            colliderCastInput.Start = rayStartOK;
            colliderCastInput.End = rayEndOK;
            using var collider2 = SphereCollider.Create(
                new SphereGeometry { Center = float3.zero, Radius = sphereRadius }
            );
            colliderCastInput.Collider = (Collider*)collider2.GetUnsafePtr();

            Assert.IsTrue(rigidbody.CastCollider(colliderCastInput));
            Assert.IsTrue(rigidbody.CastCollider(colliderCastInput, out closestHit));
            Assert.IsTrue(rigidbody.CastCollider(colliderCastInput, ref allHits));

            // Fail case : wrong direction
            colliderCastInput.Start = rayStartFail;
            colliderCastInput.End = rayEndFail;

            Assert.IsFalse(rigidbody.CastCollider(colliderCastInput));
            Assert.IsFalse(rigidbody.CastCollider(colliderCastInput, out closestHit));
            Assert.IsFalse(rigidbody.CastCollider(colliderCastInput, ref allHits));
        }

        [Test]
        public unsafe void RigidBodyCalculateDistancePointTest()
        {
            Physics.RigidBody rigidbody = Unity.Physics.RigidBody.Zero;

            const float size = 1.0f;
            const float convexRadius = 0.0f;

            var queryPos = new float3(-10, -10, -10);

            using var collider = BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = size,
                BevelRadius = convexRadius
            });
            rigidbody.Collider = collider;

            var pointDistanceInput = new PointDistanceInput();

            pointDistanceInput.Position = queryPos;
            pointDistanceInput.Filter = CollisionFilter.Default;

            var closestHit = new DistanceHit();
            var allHits = new NativeList<DistanceHit>(Allocator.Temp);

            // OK case : with enough max distance
            pointDistanceInput.MaxDistance = 10000.0f;
            Assert.IsTrue(rigidbody.CalculateDistance(pointDistanceInput));
            Assert.IsTrue(rigidbody.CalculateDistance(pointDistanceInput, out closestHit));
            Assert.IsTrue(rigidbody.CalculateDistance(pointDistanceInput, ref allHits));

            // Fail case : not enough max distance
            pointDistanceInput.MaxDistance = 1;
            Assert.IsFalse(rigidbody.CalculateDistance(pointDistanceInput));
            Assert.IsFalse(rigidbody.CalculateDistance(pointDistanceInput, out closestHit));
            Assert.IsFalse(rigidbody.CalculateDistance(pointDistanceInput, ref allHits));
        }

        [Test]
        public unsafe void RigidBodyCalculateDistanceTest()
        {
            const float size = 1.0f;
            const float convexRadius = 0.0f;
            const float sphereRadius = 1.0f;

            var queryPos = new float3(-10, -10, -10);

            using BlobAssetReference<Collider> boxCollider = BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = size,
                BevelRadius = convexRadius
            });
            using BlobAssetReference<Collider> sphereCollider = SphereCollider.Create(new SphereGeometry
            {
                Center = float3.zero,
                Radius = sphereRadius
            });

            var rigidBody = new Physics.RigidBody
            {
                WorldFromBody = RigidTransform.identity,
                Scale = 1.0f,
                Collider = boxCollider
            };

            var colliderDistanceInput = new ColliderDistanceInput
            {
                Collider = (Collider*)sphereCollider.GetUnsafePtr(),
                Transform = new RigidTransform(quaternion.identity, queryPos)
            };

            var closestHit = new DistanceHit();
            var allHits = new NativeList<DistanceHit>(Allocator.Temp);

            // OK case : with enough max distance
            colliderDistanceInput.MaxDistance = 10000.0f;
            Assert.IsTrue(rigidBody.CalculateDistance(colliderDistanceInput));
            Assert.IsTrue(rigidBody.CalculateDistance(colliderDistanceInput, out closestHit));
            Assert.IsTrue(rigidBody.CalculateDistance(colliderDistanceInput, ref allHits));

            // Fail case : not enough max distance
            colliderDistanceInput.MaxDistance = 1;
            Assert.IsFalse(rigidBody.CalculateDistance(colliderDistanceInput));
            Assert.IsFalse(rigidBody.CalculateDistance(colliderDistanceInput, out closestHit));
            Assert.IsFalse(rigidBody.CalculateDistance(colliderDistanceInput, ref allHits));
        }
    }
}
