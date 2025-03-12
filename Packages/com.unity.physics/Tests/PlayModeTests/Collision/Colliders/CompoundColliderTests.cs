using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using Unity.Physics.Tests.Utils;

namespace Unity.Physics.Tests.Collision.Colliders
{
    /// <summary>
    /// Test class containing tests for the <see cref="CompoundCollider"/>
    /// </summary>
    class CompoundColliderTests
    {
        [BurstCompile(CompileSynchronously = true)]
        struct CreateFromBurstJob : IJob
        {
            public BlobAssetReference<Collider> BoxCollider;

            [GenerateTestsForBurstCompatibility]
            public void Execute()
            {
                using var children = new NativeArray<CompoundCollider.ColliderBlobInstance>(3, Allocator.Temp)
                    {
                        [0] = new CompoundCollider.ColliderBlobInstance { Collider = BoxCollider, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(0)) },
                        [1] = new CompoundCollider.ColliderBlobInstance { Collider = BoxCollider, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(1)) },
                        [2] = new CompoundCollider.ColliderBlobInstance { Collider = BoxCollider, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(2)) },
                    };
                CompoundCollider.Create(children).Dispose();
            }
        }

        [Test]
        public void CreateCompound_WhenCalledFromBurstJob_DoesNotThrow()
        {
            // Create a unit box as child collider in the compound
            using var box = BoxCollider.Create(new BoxGeometry {Orientation = quaternion.identity});

            // create a compound collider from a job
            new CreateFromBurstJob
            {
                BoxCollider = box
            }.Run();
        }

        [Test]
        public void MassProperties_BuiltFromChildren_MatchesExpected()
        {
            void TestCompoundBox(RigidTransform transform)
            {
                // Create a unit box
                using var box = BoxCollider.Create(new BoxGeometry
                {
                    Center = transform.pos,
                    Orientation = transform.rot,
                    Size = new float3(1),
                    BevelRadius = 0.0f
                });

                // Create a compound of mini boxes, matching the volume of the single box
                using var miniBox = BoxCollider.Create(new BoxGeometry
                {
                    Center = float3.zero,
                    Orientation = quaternion.identity,
                    Size = new float3(0.5f, 0.5f, 0.5f),
                    BevelRadius = 0.0f
                });
                var children = new NativeArray<CompoundCollider.ColliderBlobInstance>(8, Allocator.Temp)
                {
                    [0] = new CompoundCollider.ColliderBlobInstance { Collider = miniBox, CompoundFromChild = math.mul(transform, new RigidTransform(quaternion.identity, new float3(+0.25f, +0.25f, +0.25f))) },
                    [1] = new CompoundCollider.ColliderBlobInstance { Collider = miniBox, CompoundFromChild = math.mul(transform, new RigidTransform(quaternion.identity, new float3(-0.25f, +0.25f, +0.25f))) },
                    [2] = new CompoundCollider.ColliderBlobInstance { Collider = miniBox, CompoundFromChild = math.mul(transform, new RigidTransform(quaternion.identity, new float3(+0.25f, -0.25f, +0.25f))) },
                    [3] = new CompoundCollider.ColliderBlobInstance { Collider = miniBox, CompoundFromChild = math.mul(transform, new RigidTransform(quaternion.identity, new float3(+0.25f, +0.25f, -0.25f))) },
                    [4] = new CompoundCollider.ColliderBlobInstance { Collider = miniBox, CompoundFromChild = math.mul(transform, new RigidTransform(quaternion.identity, new float3(-0.25f, -0.25f, +0.25f))) },
                    [5] = new CompoundCollider.ColliderBlobInstance { Collider = miniBox, CompoundFromChild = math.mul(transform, new RigidTransform(quaternion.identity, new float3(+0.25f, -0.25f, -0.25f))) },
                    [6] = new CompoundCollider.ColliderBlobInstance { Collider = miniBox, CompoundFromChild = math.mul(transform, new RigidTransform(quaternion.identity, new float3(-0.25f, +0.25f, -0.25f))) },
                    [7] = new CompoundCollider.ColliderBlobInstance { Collider = miniBox, CompoundFromChild = math.mul(transform, new RigidTransform(quaternion.identity, new float3(-0.25f, -0.25f, -0.25f))) }
                };
                using var compound = CompoundCollider.Create(children);
                children.Dispose();

                var boxMassProperties = box.Value.MassProperties;
                var compoundMassProperties = compound.Value.MassProperties;

                TestUtils.AreEqual(compoundMassProperties.Volume, boxMassProperties.Volume, 1e-3f);
                TestUtils.AreEqual(compoundMassProperties.AngularExpansionFactor, boxMassProperties.AngularExpansionFactor, 1e-3f);
                TestUtils.AreEqual(compoundMassProperties.MassDistribution.Transform.pos, boxMassProperties.MassDistribution.Transform.pos, 1e-3f);
                //TestUtils.AreEqual(compoundMassProperties.MassDistribution.Orientation, boxMassProperties.MassDistribution.Orientation, 1e-3f);   // TODO: Figure out why this differs, and if that is a problem
                TestUtils.AreEqual(compoundMassProperties.MassDistribution.InertiaTensor, boxMassProperties.MassDistribution.InertiaTensor, 1e-3f);
            }

            // Compare box with compound at various transforms
            TestCompoundBox(RigidTransform.identity);
            TestCompoundBox(new RigidTransform(quaternion.identity, new float3(1.0f, 2.0f, 3.0f)));
            TestCompoundBox(new RigidTransform(quaternion.EulerXYZ(0.5f, 1.0f, 1.5f), float3.zero));
            TestCompoundBox(new RigidTransform(quaternion.EulerXYZ(0.5f, 1.0f, 1.5f), new float3(1.0f, 2.0f, 3.0f)));
        }

        unsafe void ValidateCompoundCollider(BlobAssetReference<Collider> collider, int expectedChildCount)
        {
            // manually created colliders are unique by design
            Assert.IsTrue(collider.Value.IsUnique);

            Assert.AreEqual(ColliderType.Compound, collider.Value.Type);
            Assert.AreEqual(CollisionType.Composite, collider.Value.CollisionType);

            var compound = (CompoundCollider*)collider.GetUnsafePtr();
            var uniqueChildren = new HashSet<long>();
            for (var i = 0; i < compound->Children.Length; i++)
                uniqueChildren.Add((long)compound->Children[i].Collider);
            Assert.That(uniqueChildren.Count, Is.EqualTo(expectedChildCount));
        }

        [Test]
        public void CreateCompound_WithRepeatedInputs_ChildrenAreInstances()
        {
            BlobAssetReference<Collider> boxBlob = default;
            BlobAssetReference<Collider> capsuleBlob = default;
            BlobAssetReference<Collider> sphereBlob = default;
            BlobAssetReference<Collider> compoundBlob = default;
            BlobAssetReference<Collider> cloneBlob = default;
            try
            {
                // 3 unique instance inputs
                boxBlob = BoxCollider.Create(new BoxGeometry { Orientation = quaternion.identity, Size = new float3(1) });
                capsuleBlob = CapsuleCollider.Create(new CapsuleGeometry { Radius = 0.5f, Vertex0 = new float3(1f), Vertex1 = new float3(-1f) });
                sphereBlob = SphereCollider.Create(new SphereGeometry { Radius = 0.5f });
                var children = new NativeArray<CompoundCollider.ColliderBlobInstance>(8, Allocator.Temp)
                {
                    [0] = new CompoundCollider.ColliderBlobInstance { Collider = boxBlob, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(0f)) },
                    [1] = new CompoundCollider.ColliderBlobInstance { Collider = capsuleBlob, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(1f)) },
                    [2] = new CompoundCollider.ColliderBlobInstance { Collider = boxBlob, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(2f)) },
                    [3] = new CompoundCollider.ColliderBlobInstance { Collider = sphereBlob, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(3f)) },
                    [4] = new CompoundCollider.ColliderBlobInstance { Collider = boxBlob, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(4f)) },
                    [5] = new CompoundCollider.ColliderBlobInstance { Collider = capsuleBlob, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(5f)) },
                    [6] = new CompoundCollider.ColliderBlobInstance { Collider = boxBlob, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(6f)) },
                    [7] = new CompoundCollider.ColliderBlobInstance { Collider = sphereBlob, CompoundFromChild = new RigidTransform(quaternion.identity, new float3(7f)) }
                };

                const int kExpectedChildCount = 3;
                compoundBlob = CompoundCollider.Create(children);
                ValidateCompoundCollider(compoundBlob, kExpectedChildCount);

                cloneBlob = compoundBlob.Value.Clone();
                ValidateCompoundCollider(cloneBlob, kExpectedChildCount);
            }
            finally
            {
                boxBlob.Dispose();
                capsuleBlob.Dispose();
                sphereBlob.Dispose();
                if (compoundBlob.IsCreated)
                    compoundBlob.Dispose();

                if (cloneBlob.IsCreated)
                    cloneBlob.Dispose();
            }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void CompoundCollider_Create_WhenChildrenNotCreated_Throws() =>
            Assert.Throws<ArgumentException>(() => CompoundCollider.Create(default));

        [Test]
        public void CompoundCollider_Create_WhenChildrenEmpty_Throws() =>
            Assert.Throws<ArgumentException>(() => CompoundCollider.Create(new NativeArray<CompoundCollider.ColliderBlobInstance>(0, Allocator.Temp)));

        [Test]
        public void CompoundCollider_Create_WhenNestingLevelBreached_Throws()
        {
            var CreatedColliders = new NativeList<BlobAssetReference<Collider>>(Allocator.Temp);
            Assert.Throws<ArgumentException>(
                () =>
                {
                    int numSpheres = 17;
                    float sphereRadius = 0.5f;
                    float ringRadius = 2f;
                    BlobAssetReference<Collider> compound = default;
                    for (int i = 0; i < numSpheres; ++i)
                    {
                        var t = i / (float)numSpheres * 2f * math.PI;
                        var p = ringRadius * new float3(math.cos(t), 0f, math.sin(t));
                        var sphere = SphereCollider.Create(new SphereGeometry { Center = p, Radius = sphereRadius });
                        CreatedColliders.Add(sphere);
                        if (compound.IsCreated)
                        {
                            compound = CompoundCollider.Create(
                                new NativeArray<CompoundCollider.ColliderBlobInstance>(2, Allocator.Temp)
                                {
                                    [0] = new CompoundCollider.ColliderBlobInstance { Collider = sphere, CompoundFromChild = RigidTransform.identity },
                                    [1] = new CompoundCollider.ColliderBlobInstance { Collider = compound, CompoundFromChild = RigidTransform.identity }
                                });
                        }
                        else
                        {
                            compound = CompoundCollider.Create(
                                new NativeArray<CompoundCollider.ColliderBlobInstance>(1, Allocator.Temp)
                                {
                                    [0] = new CompoundCollider.ColliderBlobInstance { Collider = sphere, CompoundFromChild = RigidTransform.identity }
                                });
                        }
                        CreatedColliders.Add(compound);
                    }
                });

            for (int i = CreatedColliders.Length - 1; i >= 0; i--)
            {
                CreatedColliders[i].Dispose();
            }

            CreatedColliders.Dispose();
        }

#endif

        [Test]
        public void TestCompoundColliderToMesh()
        {
            // create a compound made of 2 boxes placed diagonally from each other and make sure we get the
            // expected mesh center and bounds

            var size = new float3(1, 2, 3);
            var geometry = new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = size
            };
            using var boxCollider = BoxCollider.Create(geometry);

            var children = new NativeArray<CompoundCollider.ColliderBlobInstance>(2, Allocator.Temp);
            try
            {
                for (int i = 0; i < 2; ++i)
                {
                    children[i] = new CompoundCollider.ColliderBlobInstance {Collider = boxCollider, CompoundFromChild = RigidTransform.Translate(i * size)};
                }

                var expectedBoundsSize = 2 * size;
                var expectedBoundsCenter = size / 2;

                using var compoundCollider = CompoundCollider.Create(children);
                var aabb = compoundCollider.Value.CalculateAabb(RigidTransform.identity);
                TestUtils.AreEqual(expectedBoundsCenter, aabb.Center, math.EPSILON);
                TestUtils.AreEqual(expectedBoundsSize, aabb.Extents, math.EPSILON);

                var mesh = compoundCollider.Value.ToMesh();
                TestUtils.AreEqual(expectedBoundsCenter, mesh.bounds.center, math.EPSILON);
                TestUtils.AreEqual(expectedBoundsSize, mesh.bounds.size, math.EPSILON);

                UnityEngine.Object.DestroyImmediate(mesh);
            }
            finally
            {
                children.Dispose();
            }
        }
    }
}
