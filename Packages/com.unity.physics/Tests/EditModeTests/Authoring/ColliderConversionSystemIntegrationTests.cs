using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Physics.Tests.Authoring
{
    class ColliderConversionSystemIntegrationTests : BaseHierarchyConversionTest
    {
        private UnityEngine.Mesh NonReadableMesh { get; set; }
        private UnityEngine.Mesh ReadableMesh { get; set; }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            ReadableMesh = Resources.GetBuiltinResource<UnityEngine.Mesh>("New-Cylinder.fbx");
            Assume.That(ReadableMesh.isReadable, Is.True, $"{ReadableMesh} was not readable.");

            NonReadableMesh = UnityEngine.Mesh.Instantiate(ReadableMesh);
            NonReadableMesh.UploadMeshData(true);
            Assume.That(NonReadableMesh.isReadable, Is.False, $"{NonReadableMesh} was readable.");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
#if !UNITY_EDITOR
            UnityEngine.Mesh.Destroy(NonReadableMesh);
#else
            UnityEngine.Mesh.DestroyImmediate(NonReadableMesh);
#endif
        }

        [Test]
        public void MeshColliderConversionSystem_WhenMeshColliderHasNonReadableMesh_ThrowsException(
            [Values] bool convex
        )
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { typeof(UnityEngine.MeshCollider) });
#if !UNITY_EDITOR
            // legacy components log error messages in the player for non-readable meshes once for each property access
            LogAssert.Expect(LogType.Error, k_NonReadableMeshPattern);
            LogAssert.Expect(LogType.Error, k_NonReadableMeshPattern);
#endif
            Child.GetComponent<UnityEngine.MeshCollider>().sharedMesh = NonReadableMesh;
            Child.GetComponent<UnityEngine.MeshCollider>().convex = convex;

            VerifyLogsException<InvalidOperationException>(k_NonReadableMeshPattern);
        }

        private unsafe void ValidateUniqueColliderCount(NativeArray<PhysicsCollider> colliders, int expectedCount)
        {
            var uniqueColliders = new HashSet<IntPtr>();
            foreach (var c in colliders)
            {
                uniqueColliders.Add((IntPtr)c.ColliderPtr);
            }

            var numUnique = uniqueColliders.Count;
            Assume.That(numUnique, Is.EqualTo(expectedCount), $"Expected {expectedCount} unique collider(s), but found {numUnique} unique collider(s).");
        }

        [Test]
        public void MeshColliderConversionSystem_WhenMultipleShapesShareInputs_CollidersShareTheSameData(
            [Values] bool convex
        )
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                new[] { typeof(UnityEngine.MeshCollider), typeof(Rigidbody) },
                new[] { typeof(UnityEngine.MeshCollider), typeof(Rigidbody) }
            );
            foreach (var shape in Root.GetComponentsInChildren<UnityEngine.MeshCollider>())
            {
                shape.convex = convex;
                shape.sharedMesh = ReadableMesh;
            }
            Child.transform.localPosition = TransformConversionUtils.k_SharedDataChildTransformation.pos;
            Child.transform.localRotation = TransformConversionUtils.k_SharedDataChildTransformation.rot;

            TestConvertedSharedData<PhysicsCollider, PhysicsWorldIndex>(colliders =>
            {
                // ensure that the colliders indicate that they are shared.
                foreach (var collider in colliders)
                {
                    Assume.That(collider.IsUnique, Is.False);
                }

                // ensure that the collider are effectively shared.
                ValidateUniqueColliderCount(colliders, 1);
            }, 2, k_DefaultWorldIndex);
        }

        public enum MakeUniqueMode
        {
            EntityManager,
            EntityCommandBuffer,
            ParallelWriter
        }

        [Test]
        // Test which ensures that a baked collider is initially shared but can be made unique
        public void ColliderConversionSystem_SharedColliders_MadeUnique([Values] MakeUniqueMode makeUniqueMode)
        {
            // Create two separate rigid bodies with a shared collider.
            // Sharing occurs automatically because the colliders have identical parameters.
            CreateHierarchy(
                Array.Empty<Type>(),
                new[] {typeof(UnityEngine.SphereCollider), typeof(Rigidbody)},
                new[] {typeof(UnityEngine.SphereCollider), typeof(Rigidbody)}
            );

            var colliderBlobsToDispose = new NativeList<BlobAssetReference<Collider>>(Allocator.Temp);

            TestConvertedData<PhysicsCollider>((world, entities, colliders)  =>
            {
                // ensure that the colliders indicate that they are shared.
                foreach (var collider in colliders)
                {
                    Assume.That(collider.IsUnique, Is.False);
                }

                // ensure that the collider are initially shared.
                ValidateUniqueColliderCount(colliders, 1);

                // make one of the two colliders unique
                var c = colliders[0];
                switch (makeUniqueMode)
                {
                    case MakeUniqueMode.EntityManager:
                        {
                            c.MakeUnique(entities[0], world.EntityManager);
                            break;
                        }
                    case MakeUniqueMode.EntityCommandBuffer:
                        {
                            using var ecb = new EntityCommandBuffer(Allocator.Temp);
                            c.MakeUnique(entities[0], ecb);
                            ecb.Playback(world.EntityManager);
                            break;
                        }
                    case MakeUniqueMode.ParallelWriter:
                        {
                            using var ecb = new EntityCommandBuffer(Allocator.TempJob);
                            {
                                c.MakeUnique(entities[0], ecb.AsParallelWriter(), 0);
                                ecb.Playback(world.EntityManager);
                            }
                            break;
                        }
                }
                colliders[0] = c;
                if (world.EntityManager.HasComponent<ColliderBlobCleanupData>(entities[0]))
                {
                    colliderBlobsToDispose.Add(world.EntityManager
                        .GetComponentData<ColliderBlobCleanupData>(entities[0]).Value);
                }

                // ensure that the collider is now unique.
                Assert.That(c.IsUnique, Is.True);
                ValidateUniqueColliderCount(colliders, 2);
            }, 2);

            foreach (var blob in colliderBlobsToDispose)
                blob.Dispose();
        }

        private static readonly TestCaseData[] k_ColliderTypeTestCases =
        {
            new TestCaseData(
                new[] { typeof(UnityEngine.SphereCollider), typeof(Rigidbody), typeof(ForceUniqueColliderAuthoring)}, // parent components
                new[] { typeof(UnityEngine.SphereCollider), typeof(Rigidbody), typeof(ForceUniqueColliderAuthoring)}, // child components
                2, // expect unique count
                false // is convex (unused)
            ).SetName("Unique Sphere Collider"),
            new TestCaseData(
                new[] { typeof(UnityEngine.CapsuleCollider), typeof(Rigidbody), typeof(ForceUniqueColliderAuthoring)}, // parent components
                new[] { typeof(UnityEngine.CapsuleCollider), typeof(Rigidbody), typeof(ForceUniqueColliderAuthoring)}, // child components
                2, // expect unique count
                false // is convex (unused)
            ).SetName("Unique Capsule Collider"),
            new TestCaseData(
                new[] { typeof(UnityEngine.BoxCollider), typeof(Rigidbody), typeof(ForceUniqueColliderAuthoring)}, // parent components
                new[] { typeof(UnityEngine.BoxCollider), typeof(Rigidbody), typeof(ForceUniqueColliderAuthoring)}, // child components
                2, // expect unique count
                false // is convex (unused)
            ).SetName("Unique Box Collider"),
            new TestCaseData(
                new[] { typeof(UnityEngine.MeshCollider), typeof(Rigidbody), typeof(ForceUniqueColliderAuthoring)}, // parent components
                new[] { typeof(UnityEngine.MeshCollider), typeof(Rigidbody), typeof(ForceUniqueColliderAuthoring)}, // child components
                2, // expect unique count
                false // is convex
            ).SetName("Unique Mesh Collider (non-convex)"),
            new TestCaseData(
                new[] { typeof(UnityEngine.MeshCollider), typeof(Rigidbody), typeof(ForceUniqueColliderAuthoring)}, // parent components
                new[] { typeof(UnityEngine.MeshCollider), typeof(Rigidbody), typeof(ForceUniqueColliderAuthoring)}, // child components
                2, // expect unique count
                true // is convex
            ).SetName("Unique Mesh Collider (convex)"),
        };

        // Test which ensures that a baked built-in collider is unique when the ForceUniqueColliderAuthoring component is present
        [TestCaseSource(nameof(k_ColliderTypeTestCases))]
        public void ColliderConversionSystem_BuiltInSharedColliders_MadeUnique(Type[] parentComponentTypes,
            Type[] childComponentTypes, int expectedCount, bool isConvex)
        {
            // Create two separate rigid bodies with the same collider type but with the force unique component
            CreateHierarchy(Array.Empty<Type>(), parentComponentTypes, childComponentTypes);

            // Must ensure that the mesh isn't null:
            foreach (var parent in parentComponentTypes)
            {
                if (parent == typeof(UnityEngine.MeshCollider))
                {
                    Parent.GetComponent<UnityEngine.MeshCollider>().sharedMesh = ReadableMesh;
                    Parent.GetComponent<UnityEngine.MeshCollider>().convex = isConvex;
                }
            }
            foreach (var child in childComponentTypes)
            {
                if (child == typeof(UnityEngine.MeshCollider))
                {
                    Child.GetComponent<UnityEngine.MeshCollider>().sharedMesh = ReadableMesh;
                    Child.GetComponent<UnityEngine.MeshCollider>().convex = isConvex;
                }
            }

            TestConvertedData<PhysicsCollider>((world, entities, colliders)  =>
            {
                int numUnique = 0;
                foreach (var collider in colliders)
                {
                    if (collider.IsUnique) numUnique++;
                    Assume.That(collider.IsUnique, Is.True);
                }

                Assume.That(numUnique, Is.EqualTo(expectedCount), $"Expected {expectedCount} unique collider(s), but found {numUnique} unique collider(s).");
            }, 2);
        }

        static IEnumerable GetColliderTypes()
        {
            return new[] { typeof(UnityEngine.SphereCollider), typeof(UnityEngine.BoxCollider), typeof(UnityEngine.CapsuleCollider), typeof(UnityEngine.MeshCollider) };
        }

        static IEnumerable GetPrimitiveColliderTypes()
        {
            return new[] { typeof(UnityEngine.SphereCollider), typeof(UnityEngine.BoxCollider), typeof(UnityEngine.CapsuleCollider) };
        }

        /// <summary>
        /// Test that when game object contains uniform scale, the resultant entity's local transform has the same scale.
        /// </summary>
        [Test]
        public void ColliderConversionSystem_WhenGOIsUniformlyScaled_LocalTransformHasScale([ValueSource(nameof(GetColliderTypes))] Type colliderType)
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] {typeof(UnityEngine.Rigidbody), colliderType }
            );

            if (colliderType == typeof(UnityEngine.MeshCollider))
            {
                var collider = Child.GetComponent<UnityEngine.MeshCollider>();
                collider.sharedMesh = ReadableMesh;
            }

            // uniformly transform the child collider
            const float k_UniformScale = 2f;
            Child.transform.localScale = new float3(k_UniformScale);

            TestConvertedData<LocalTransform>((world, transform, entity) =>
            {
                Assert.That(transform.Scale, Is.PrettyCloseTo(k_UniformScale));

                TestScaleChange(world, entity);
            });
        }

        /// <summary>
        /// Test that when game object contains uniform scale and a primitive collider, the resultant entity's local transform has the expected scale and the
        /// baked collider geometry is not affected by the scale.
        /// </summary>
        [Test]
        public void ColliderConversionSystems_WhenGOIsUniformlyScaled_LocalTransformHasScale_PrimitiveColliderIsNotScaled([ValueSource(nameof(GetPrimitiveColliderTypes))] Type colliderType)
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] {typeof(UnityEngine.Rigidbody), colliderType }
            );

            var collider = Child.GetComponent<UnityEngine.Collider>();
            Assert.That(collider != null);

            if (collider is UnityEngine.CapsuleCollider capsuleCollider)
            {
                // Since the default capsule properties lead to the two baked capsule collider vertices to be identical,
                // we elongate the capsule to prevent that. This way we can compare the capsule direction with
                // the baked capsule collider's alignment (see below).
                capsuleCollider.height = 2;
                capsuleCollider.radius = 0.5f;
            }

            // uniformly transform the child collider
            const float k_UniformScale = 2f;
            Child.transform.localScale = new float3(k_UniformScale);

            TestConvertedData<LocalTransform>((world, transform, entity) =>
            {
                // make sure the uniform scale was applied to the baked LocalTransform.Scale property
                Assert.That(transform.Scale, Is.PrettyCloseTo(k_UniformScale));

                // make sure baked collider geometry is not affected by the uniform scale

                if (collider is UnityEngine.BoxCollider box)
                {
                    // compare box properties with baked BoxCollider properties and expect them to be identical

                    var physicsCollider = world.EntityManager.GetComponentData<PhysicsCollider>(entity);
                    unsafe
                    {
                        var boxCollider = (BoxCollider*)physicsCollider.ColliderPtr;
                        // make sure the collider type is as expected
                        Assert.That(boxCollider->Type, Is.EqualTo(ColliderType.Box));

                        // compare box properties
                        Assert.That(boxCollider->Size, Is.PrettyCloseTo(box.size));
                        Assert.That(boxCollider->Center, Is.PrettyCloseTo(box.center));
                        Assert.That(boxCollider->Orientation, Is.OrientedEquivalentTo(quaternion.identity));
                    }
                }
                else if (collider is UnityEngine.CapsuleCollider capsule)
                {
                    // compare capsule properties with baked CapsuleCollider properties and expect them to be identical
                    unsafe
                    {
                        var physicsCollider = world.EntityManager.GetComponentData<PhysicsCollider>(entity);
                        // make sure the collider type is as expected
                        Assert.That(physicsCollider.ColliderPtr->Type, Is.EqualTo(ColliderType.Capsule));

                        // compare capsule properties
                        var capsuleCollider = (CapsuleCollider*)physicsCollider.ColliderPtr;
                        var actualCenter = 0.5f * (capsuleCollider->Vertex0 + capsuleCollider->Vertex1);
                        var actualHeight = math.distance(capsuleCollider->Vertex0, capsuleCollider->Vertex1) + 2 * capsuleCollider->Radius;
                        var expectedDirection = new float3 {[capsule.direction] = 1};
                        var actualDirection = math.normalize(capsuleCollider->Vertex0 - capsuleCollider->Vertex1);
                        Assert.That(math.dot(actualDirection, expectedDirection), Is.PrettyCloseTo(1));
                        Assert.That(actualCenter, Is.PrettyCloseTo(capsule.center));
                        Assert.That(capsuleCollider->Radius, Is.PrettyCloseTo(capsule.radius));
                        Assert.That(actualHeight, Is.PrettyCloseTo(capsule.height));
                    }
                }
                else if (collider is UnityEngine.SphereCollider sphere)
                {
                    // compare sphere properties with baked SphereCollider properties and expect them to be identical
                    unsafe
                    {
                        var physicsCollider = world.EntityManager.GetComponentData<PhysicsCollider>(entity);
                        // make sure the collider type is as expected
                        Assert.That(physicsCollider.ColliderPtr->Type, Is.EqualTo(ColliderType.Sphere));

                        // compare sphere properties
                        var sphereCollider = (SphereCollider*)physicsCollider.ColliderPtr;
                        Assert.That(sphereCollider->Radius, Is.PrettyCloseTo(sphere.radius));
                        Assert.That(sphereCollider->Center, Is.PrettyCloseTo(sphere.center));
                    }
                }

                TestScaleChange(world, entity);
            });
        }

        /// <summary>
        /// Test that when game object contains uniform scale and a UnityEngine.MeshCollider, the resultant entity's local transform has the expected scale and the
        /// baked collider geometry is not affected by the scale.
        /// </summary>
        [Test]
        public void ColliderConversionSystems_WhenGOIsUniformlyScaled_LocalTransformHasScale_MeshColliderIsNotScaled([Values] bool convex)
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] {typeof(UnityEngine.Rigidbody), typeof(UnityEngine.MeshCollider)}
            );

            var mesh = Child.GetComponent<UnityEngine.MeshCollider>();
            mesh.convex = convex;
            mesh.sharedMesh = ReadableMesh;

            // uniformly transform the child collider
            const float k_UniformScale = 2f;
            Child.transform.localScale = new float3(k_UniformScale);

            TestConvertedData<LocalTransform>((world, transform, entity) =>
            {
                // make sure the uniform scale was applied to the baked LocalTransform.Scale property
                Assert.That(transform.Scale, Is.PrettyCloseTo(k_UniformScale));

                // make sure baked collider geometry is not affected by the uniform scale

                // compare bounds of original mesh with baked collider and expect them to be unaffected by scale.

                var expectedBounds = ReadableMesh.bounds;
                var physicsCollider = world.EntityManager.GetComponentData<PhysicsCollider>(entity);

                unsafe
                {
                    Aabb actualBounds;
                    var sizeTolerance = 1e-5f;
                    if (convex)
                    {
                        var convexCollider = (ConvexCollider*)physicsCollider.ColliderPtr;
                        // make sure the collider type is as expected
                        Assert.That(convexCollider->Type, Is.EqualTo(ColliderType.Convex));
                        actualBounds = convexCollider->CalculateAabb();

                        // override size tolerance for convex mesh due to expected inaccuracies
                        sizeTolerance = 1e-2f;
                    }
                    else
                    {
                        var meshCollider = (MeshCollider*)physicsCollider.ColliderPtr;
                        // make sure the collider type is as expected
                        Assert.That(meshCollider->Type, Is.EqualTo(ColliderType.Mesh));
                        actualBounds = meshCollider->CalculateAabb();
                    }
                    // compare bounds
                    Assert.That(actualBounds.Center, Is.PrettyCloseTo(expectedBounds.center));
                    Assert.That(actualBounds.Extents, Is.PrettyCloseTo(expectedBounds.size).Within(sizeTolerance));
                }

                TestScaleChange(world, entity);
            });
        }

        /// <summary>
        /// Test that when game object contains non uniform scale, the resultant entity's local transform has identity scale and the
        /// PostTransformMatrix contains the non uniform scale.
        /// </summary>
        [Test]
        public void ColliderConversionSystem_WhenGOIsNonUniformlyScaled_LocalTransformHasNoScale(
            [ValueSource(nameof(GetColliderTypes))] Type colliderType)
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] {typeof(UnityEngine.Rigidbody), colliderType }
            );

            if (colliderType == typeof(UnityEngine.MeshCollider))
            {
                var collider = Child.GetComponent<UnityEngine.MeshCollider>();
                collider.sharedMesh = ReadableMesh;
            }

            // uniformly transform the child collider
            var k_NonUniformScale = new Vector3(1, 2, 3);
            Child.transform.localScale = k_NonUniformScale;

            TestConvertedData<LocalTransform>((world, transform, entity) =>
            {
                // expect the local transform scale to be identity
                Assert.That(transform.Scale, Is.PrettyCloseTo(1));

                // expect there to be a PostTransformMatrix component
                Assert.That(world.EntityManager.HasComponent<PostTransformMatrix>(entity), Is.True);

                // expect the PostTransformMatrix to represent the same scale as the local transform
                var postTransformMatrix = world.EntityManager.GetComponentData<PostTransformMatrix>(entity);
                Assert.That(postTransformMatrix.Value, Is.PrettyCloseTo(float4x4.Scale(k_NonUniformScale)));

                TestScaleChange(world, entity);
            });
        }

        /// <summary>
        /// Test that when a game object contains shear in world space, the resultant entity's local transform has identity scale and the
        /// PostTransformMatrix (containing the shear) and LocalTransform (containing the rigid body transform) components
        /// together represent the same world transform as the game object.
        /// </summary>
        [Test]
        public void ColliderConversionSystem_WhenGOIsSheared_LocalTransformHasNoScale(
            [ValueSource(nameof(GetColliderTypes))] Type colliderType)
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] {typeof(UnityEngine.Rigidbody), colliderType }
            );

            if (colliderType == typeof(UnityEngine.MeshCollider))
            {
                var collider = Child.GetComponent<UnityEngine.MeshCollider>();
                collider.sharedMesh = ReadableMesh;
            }

            // create a hierarchy that leads to shear in the child's world transform
            Root.transform.localPosition = new Vector3(1f, 2f, 3f);
            Root.transform.localRotation = Quaternion.Euler(30f, 60f, 90f);
            Root.transform.localScale = new Vector3(3f, 5f, 7f);
            Parent.transform.localPosition = new Vector3(2f, 4f, 8f);
            Parent.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
            Parent.transform.localScale = new Vector3(2f, 4f, 8f);
            Child.transform.localPosition = new Vector3(3f, 6f, 9f);
            Child.transform.localRotation = Quaternion.Euler(-30f, 20f, -10f);
            Child.transform.localScale = new Vector3(2f, 2f, 2f);

            var expectedColliderWorldTransform = (float4x4)Child.transform.localToWorldMatrix;
            Assert.That(expectedColliderWorldTransform.HasShear());

            TestConvertedData<LocalTransform>((world, transform, entity) =>
            {
                // expect the local transform scale to be identity
                var localTransform = transform;
                Assert.That(localTransform.Scale, Is.PrettyCloseTo(1));

                // expect there to be a PostTransformMatrix component
                Assert.That(world.EntityManager.HasComponent<PostTransformMatrix>(entity), Is.True);

                var postTransformMatrix = world.EntityManager.GetComponentData<PostTransformMatrix>(entity);
                // expect the post transform matrix to have shear
                Assert.That(postTransformMatrix.Value.HasShear());

                // check if world transform of the collider is as expected
                var actualColliderWorldTransform = math.mul(localTransform.ToMatrix(), postTransformMatrix.Value);
                Assert.That(expectedColliderWorldTransform, Is.PrettyCloseTo(actualColliderWorldTransform));

                TestScaleChange(world, entity);
            });
        }

        /// <summary>
        /// Test that when a game object contains non-uniform scale in world space and UnityEngine.BoxCollider,
        /// the resultant baked collider has the non-uniform scale baked in.
        /// </summary>
        [Test]
        public void ColliderConversionSystem_WhenGOIsNonUniformlyScaled_BoxColliderHasBakedScale()
        {
            TestNonUniformScaleOnCollider(new[] {typeof(UnityEngine.Rigidbody), typeof(UnityEngine.BoxCollider)}, gameObjectToConvert  =>
            {
                // create a primitive cube which we will assign to the game object used in the test
                var cubeGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var cubeMeshFilter = cubeGameObject.GetComponent<MeshFilter>();
                var cubeMeshRenderer = cubeGameObject.GetComponent<MeshRenderer>();
                Assert.That(cubeMeshFilter != null && cubeMeshFilter.sharedMesh != null && cubeMeshRenderer != null);

                // Set up the test game object with a mesh filter and renderer of the cube.
                // It is used to calculate the expected bounds of the baked collider geometry.
                var meshFilter = gameObjectToConvert.GetComponent<MeshFilter>();
                meshFilter.mesh = cubeMeshFilter.sharedMesh;
                var meshRenderer = gameObjectToConvert.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = cubeMeshRenderer.sharedMaterial;

                UnityEngine.Object.DestroyImmediate(cubeGameObject);
            });
        }

        /// <summary>
        /// Test that when a game object contains non-uniform scale in world space and a UnityEngine.MeshCollider,
        /// the resultant baked convex collider has the non-uniform scale baked in.
        /// </summary>
        [Test]
        public void ColliderConversionSystem_WhenGOIsNonUniformlyScaled_ConvexColliderHasBakedScale([Values] bool convex)
        {
            TestNonUniformScaleOnCollider(new[] {typeof(UnityEngine.Rigidbody), typeof(UnityEngine.MeshCollider)}, gameObjectToConvert =>
            {
                // create a primitive cube which we will assign to the game object used in the test
                var cubeGameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var cubeMeshFilter = cubeGameObject.GetComponent<MeshFilter>();
                var cubeMeshRenderer = cubeGameObject.GetComponent<MeshRenderer>();
                Assert.That(cubeMeshFilter != null && cubeMeshFilter.sharedMesh != null && cubeMeshRenderer != null);

                // Set up the test game object with a mesh filter and renderer of the cube.
                // It is used to calculate the expected bounds of the baked collider geometry.
                var meshFilter = gameObjectToConvert.GetComponent<MeshFilter>();
                meshFilter.mesh = cubeMeshFilter.sharedMesh;
                var meshRenderer = gameObjectToConvert.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = cubeMeshRenderer.sharedMaterial;

                // assign mesh to mesh collider and make it convex if required
                var collider = Child.GetComponent<UnityEngine.MeshCollider>();
                collider.sharedMesh = cubeMeshFilter.sharedMesh;
                collider.convex = convex;

                UnityEngine.Object.DestroyImmediate(cubeGameObject);
            });
        }

        /// <summary>
        /// Test that when a game object contains non-uniform scale in world space, a sphere collider has the non-uniform
        /// scale baked in.
        /// </summary>
        [Test]
        public void ColliderConversionSystem_WhenGOIsNonUniformlyScaled_SphereColliderHasBakedScale()
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] {typeof(UnityEngine.Rigidbody), typeof(UnityEngine.SphereCollider)}
            );

            // induce non-uniform scale in the child collider
            var nonUniformScale = new Vector3(1, 2, 3);
            Child.transform.localScale = nonUniformScale;

            var sphereCollider = Child.GetComponent<UnityEngine.SphereCollider>();
            var unscaledRadius = 1.23f;
            sphereCollider.radius = unscaledRadius;

            var expectedRadius = unscaledRadius * math.cmax(nonUniformScale);

            TestConvertedData<PhysicsCollider>((world, entities, colliders) =>
            {
                // expect there to be a LocalTransform component with identity scale
                var entity = entities[0];
                Assert.That(world.EntityManager.HasComponent<LocalTransform>(entity), Is.True);
                var localTransform = world.EntityManager.GetComponentData<LocalTransform>(entity);
                Assert.That(localTransform.Scale, Is.PrettyCloseTo(1));

                // expect there to be a PostTransformMatrix component
                Assert.That(world.EntityManager.HasComponent<PostTransformMatrix>(entity), Is.True);

                var postTransformMatrix = world.EntityManager.GetComponentData<PostTransformMatrix>(entity);
                // expect the post transform matrix to have non-uniform scale but no shear
                Assert.That(postTransformMatrix.Value.HasNonUniformScale());
                Assert.That(postTransformMatrix.Value.HasShear(), Is.False);

                // check if the sphere collider geometry is as expected
                unsafe
                {
                    var sphereColliderPtr = (SphereCollider*)colliders[0].ColliderPtr;
                    Assert.That(sphereColliderPtr->Radius, Is.PrettyCloseTo(expectedRadius));
                }

                TestScaleChange(world, entity);
            }, 1);
        }

        /// <summary>
        /// Test that when a game object contains non-uniform scale in world space, a capsule collider has the non-uniform
        /// scale baked in.
        /// </summary>
        [Test]
        public void ColliderConversionSystem_WhenGOIsNonUniformlyScaled_CapsuleColliderHasBakedScale()
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] {typeof(UnityEngine.Rigidbody), typeof(UnityEngine.CapsuleCollider)}
            );

            // induce non-uniform scale in the child collider
            var nonUniformScale = new Vector3(2, 3, 4);
            Child.transform.localScale = nonUniformScale;

            var capsuleCollider = Child.GetComponent<UnityEngine.CapsuleCollider>();
            var unscaledRadius = 1.23f;
            var unscaledHeight = 4.2f;
            capsuleCollider.radius = unscaledRadius;
            capsuleCollider.height = unscaledHeight;
            capsuleCollider.direction = 1; // y axis

            var expectedRadius = unscaledRadius * math.cmax(new float3(nonUniformScale) { [capsuleCollider.direction] = 0f });
            var expectedHeight = unscaledHeight * nonUniformScale[capsuleCollider.direction];

            TestConvertedData<PhysicsCollider>((world, entities, colliders) =>
            {
                // expect there to be a LocalTransform component with identity scale
                var entity = entities[0];
                Assert.That(world.EntityManager.HasComponent<LocalTransform>(entity), Is.True);
                var localTransform = world.EntityManager.GetComponentData<LocalTransform>(entity);
                Assert.That(localTransform.Scale, Is.PrettyCloseTo(1));

                // expect there to be a PostTransformMatrix component
                Assert.That(world.EntityManager.HasComponent<PostTransformMatrix>(entity), Is.True);

                var postTransformMatrix = world.EntityManager.GetComponentData<PostTransformMatrix>(entity);
                // expect the post transform matrix to have non-uniform scale but no shear
                Assert.That(postTransformMatrix.Value.HasNonUniformScale());
                Assert.That(postTransformMatrix.Value.HasShear(), Is.False);

                // check if the sphere collider geometry is as expected
                unsafe
                {
                    var capsuleColliderPtr = (CapsuleCollider*)colliders[0].ColliderPtr;
                    Assert.That(capsuleColliderPtr->Radius, Is.PrettyCloseTo(expectedRadius));

                    var height = math.distance(capsuleColliderPtr->Vertex0, capsuleColliderPtr->Vertex1) + 2 * capsuleColliderPtr->Radius;
                    Assert.That(height, Is.PrettyCloseTo(expectedHeight));
                }

                TestScaleChange(world, entity);
            }, 1);
        }

        private static Vector3[] GetLocalScalesUniform()
        {
            return new[]
            {
                new Vector3(1, 1, 1),
                new Vector3(0.8f, 0.8f, 0.8f)
            };
        }

        [Test]
        public void ColliderConversionSystem_RigidbodyHierarchy_WithDifferentScales_CollidersHaveExpectedSize([Values] bool rigidBodyIsKinematic, [Values] bool gameObjectIsStatic, [ValueSource(nameof(GetPrimitiveColliderTypes))] Type colliderType, [ValueSource(nameof(GetLocalScalesUniform))] Vector3 localScale)
        {
            const bool expectCompound = false;
            TestCorrectColliderSizeInHierarchy(new[] {typeof(UnityEngine.Rigidbody), colliderType},
                () =>
                {
                    Root.transform.localScale = Parent.transform.localScale = Child.transform.localScale = localScale;

                    foreach (var rigidbody in Root.GetComponentsInChildren<UnityEngine.Rigidbody>())
                    {
                        rigidbody.isKinematic = rigidBodyIsKinematic;
                    }
                }, expectCompound
            );
        }

        [Test]
        public void ColliderConversionSystem_RigidbodyHierarchy_WithDifferentScales_MeshColliderHasExpectedSize([Values] bool rigidBodyIsKinematic, [Values] bool meshIsConvex, [Values] bool gameObjectIsStatic, [ValueSource(nameof(GetLocalScalesUniform))] Vector3 localScale)
        {
            const bool expectCompound = false;
            TestCorrectColliderSizeInHierarchy(new[] {typeof(UnityEngine.Rigidbody), typeof(UnityEngine.MeshCollider)},
                () =>
                {
                    Root.transform.localScale = Parent.transform.localScale = Child.transform.localScale = localScale;

                    foreach (var rigidbody in Root.GetComponentsInChildren<UnityEngine.Rigidbody>())
                    {
                        rigidbody.isKinematic = rigidBodyIsKinematic;
                    }

                    foreach (var mesh in Root.GetComponentsInChildren<UnityEngine.MeshCollider>())
                    {
                        mesh.convex = meshIsConvex;
                    }
                }, expectCompound
            );
        }

        [Test]
        public void ColliderConversionSystem_RigidbodyHierarchy_WithNonUniformScale_BoxColliderHasExpectedSize([Values] bool rigidBodyIsKinematic, [Values] bool gameObjectIsStatic)
        {
            const bool expectCompound = false;
            TestCorrectColliderSizeInHierarchy(new[] {typeof(UnityEngine.Rigidbody), typeof(UnityEngine.BoxCollider)},
                () =>
                {
                    Root.transform.localScale = new Vector3(0.75f, 0.5f, 1);
                    Parent.transform.localScale = new Vector3(1, 0.75f, 0.5f);
                    Child.transform.localScale = new Vector3(0.5f, 1, 0.75f);

                    foreach (var rigidbody in Root.GetComponentsInChildren<UnityEngine.Rigidbody>())
                    {
                        rigidbody.isKinematic = rigidBodyIsKinematic;
                    }
                }, expectCompound
            );
        }

        [Test]
        public void ColliderConversionSystem_RigidbodyHierarchy_WithNonUniformScale_MeshColliderHasExpectedSize([Values] bool rigidBodyIsKinematic, [Values] bool meshIsConvex, [Values] bool gameObjectIsStatic)
        {
            const bool expectCompound = false;
            TestCorrectColliderSizeInHierarchy(new[] {typeof(UnityEngine.Rigidbody), typeof(UnityEngine.MeshCollider)},
                () =>
                {
                    Root.transform.localScale = new Vector3(0.75f, 0.5f, 1);
                    Parent.transform.localScale = new Vector3(1, 0.75f, 0.5f);
                    Child.transform.localScale = new Vector3(0.5f, 1, 0.75f);

                    foreach (var rigidbody in Root.GetComponentsInChildren<UnityEngine.Rigidbody>())
                    {
                        rigidbody.isKinematic = rigidBodyIsKinematic;
                    }

                    foreach (var mesh in Root.GetComponentsInChildren<UnityEngine.MeshCollider>())
                    {
                        mesh.convex = meshIsConvex;
                    }
                }, expectCompound);
        }

        [Test]
        public void ColliderConversionSystem_ColliderHierarchy_WithNonUniformScale_MeshColliderHasExpectedSize([Values] bool meshIsConvex, [Values] bool gameObjectIsStatic)
        {
            const bool expectCompound = true;
            TestCorrectColliderSizeInHierarchy(new[] {typeof(UnityEngine.MeshCollider)},
                () =>
                {
                    Root.transform.localScale = new Vector3(0.75f, 0.5f, 1);
                    Parent.transform.localScale = new Vector3(1, 0.75f, 0.5f);
                    Child.transform.localScale = new Vector3(0.5f, 1, 0.75f);

                    Root.isStatic = Parent.isStatic = Child.isStatic = gameObjectIsStatic;

                    foreach (var mesh in Root.GetComponentsInChildren<UnityEngine.MeshCollider>())
                    {
                        mesh.convex = meshIsConvex;
                    }
                }, expectCompound
            );
        }

        [Test]
        public void ColliderConversionSystem_ColliderHierarchy_WithNonUniformScale_BoxColliderHasExpectedSize([Values] bool gameObjectIsStatic)
        {
            const bool expectCompound = true;
            TestCorrectColliderSizeInHierarchy(new[] {typeof(UnityEngine.BoxCollider)},
                () =>
                {
                    Root.transform.localScale = new Vector3(0.75f, 0.5f, 1);
                    Parent.transform.localScale = new Vector3(1, 0.75f, 0.5f);
                    Child.transform.localScale = new Vector3(0.5f, 1, 0.75f);

                    Root.isStatic = Parent.isStatic = Child.isStatic = gameObjectIsStatic;
                }, expectCompound
            );
        }

        [Test]
        public void ColliderConversionSystem_ColliderHierarchy_WithDifferentScales_ColliderHasExpectedSize([ValueSource(nameof(GetPrimitiveColliderTypes))] Type colliderType, [ValueSource(nameof(GetLocalScalesUniform))] Vector3 localScale, [Values] bool gameObjectIsStatic)
        {
            const bool expectCompound = true;
            TestCorrectColliderSizeInHierarchy(new[] {colliderType},
                () =>
                {
                    Root.transform.localScale = Parent.transform.localScale = Child.transform.localScale = localScale;

                    Root.isStatic = Parent.isStatic = Child.isStatic = gameObjectIsStatic;
                }, expectCompound
            );
        }

        [Test]
        public void ColliderConversionSystem_ColliderHierarchy_WithDifferentScales_MeshColliderHasExpectedSize([Values] bool meshIsConvex, [ValueSource(nameof(GetLocalScalesUniform))] Vector3 localScale, [Values] bool gameObjectIsStatic)
        {
            const bool expectCompound = true;
            TestCorrectColliderSizeInHierarchy(new[] {typeof(UnityEngine.MeshCollider)},
                () =>
                {
                    Root.transform.localScale = Parent.transform.localScale = Child.transform.localScale = localScale;

                    Root.isStatic = Parent.isStatic = Child.isStatic = gameObjectIsStatic;

                    foreach (var mesh in Root.GetComponentsInChildren<UnityEngine.MeshCollider>())
                    {
                        mesh.convex = meshIsConvex;
                    }
                }, expectCompound
            );
        }

        [Test]
        public void ColliderConversionSystem_LayerBasedCollision_ColliderOnly([ValueSource(nameof(GetColliderTypes))] Type colliderType)
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { colliderType });

            int layerSelf = 0;
            int layerOther1 = 1;
            int layerOther2 = 2;
            int layerOther3 = 3;
            int layerOther4 = 4;

            if (colliderType == typeof(UnityEngine.MeshCollider))
            {
                var meshCollider = Child.GetComponent<UnityEngine.MeshCollider>();
                meshCollider.sharedMesh = ReadableMesh;
            }

            // self, other1: expect no collision.
            UnityEngine.Physics.IgnoreLayerCollision(layerSelf, layerOther1, true);
            // self, other2: expect no collision. But will be overridden with include layer below.
            UnityEngine.Physics.IgnoreLayerCollision(layerSelf, layerOther2, true);
            // self, other3: expect collision.
            UnityEngine.Physics.IgnoreLayerCollision(layerSelf, layerOther3, false);
            // self, other4: expect collision. But will be overridden with exclude layer below.
            UnityEngine.Physics.IgnoreLayerCollision(layerSelf, layerOther4, false);

            // assign own layer
            Child.layer = layerSelf;
            var collider = Child.GetComponent<UnityEngine.Collider>();

            // assign included and excluded layers
            var includeMask = 1 << layerOther2;
            var excludeMask = 1 << layerOther4;

            collider.includeLayers = includeMask;
            collider.excludeLayers = excludeMask;

            TestConvertedData<PhysicsCollider>(physicsCollider =>
            {
                var filter = physicsCollider.Value.Value.GetCollisionFilter();
                Assert.That(filter.BelongsTo, Is.EqualTo(1u << layerSelf));
                var collisionMask = filter.CollidesWith;

                // expect no collision for (self, other1)
                Assert.That(collisionMask & (1u << layerOther1), Is.EqualTo(0));
                // expect collision for (self, other2)
                Assert.That(collisionMask & (1u << layerOther2), Is.Not.EqualTo(0));
                // expect collision for (self, other3)
                Assert.That(collisionMask & (1u << layerOther3), Is.Not.EqualTo(0));
                // expect no collision for (self, other4)
                Assert.That(collisionMask & (1u << layerOther4), Is.EqualTo(0));
            });
        }

        [Test]
        public void ColliderConversionSystem_LayerBasedCollision_RigidBodyOnly([ValueSource(nameof(GetColliderTypes))] Type colliderType, [Values] bool rigidBodyOnSameLevel)
        {
            if (rigidBodyOnSameLevel)
            {
                CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] {colliderType, typeof(UnityEngine.Rigidbody)});
            }
            else
            {
                CreateHierarchy(Array.Empty<Type>(), new[] {typeof(UnityEngine.Rigidbody)}, new[] {colliderType});
            }

            var body = Root.GetComponentInChildren<UnityEngine.Rigidbody>();

            int layerSelf = 0;
            int layerOther1 = 1;
            int layerOther2 = 2;
            int layerOther3 = 3;
            int layerOther4 = 4;

            if (colliderType == typeof(UnityEngine.MeshCollider))
            {
                var meshCollider = Child.GetComponent<UnityEngine.MeshCollider>();
                meshCollider.sharedMesh = ReadableMesh;
            }

            // self, other1: expect no collision.
            UnityEngine.Physics.IgnoreLayerCollision(layerSelf, layerOther1, true);
            // self, other2: expect no collision. But will be overridden with include layer below.
            UnityEngine.Physics.IgnoreLayerCollision(layerSelf, layerOther2, true);
            // self, other3: expect collision.
            UnityEngine.Physics.IgnoreLayerCollision(layerSelf, layerOther3, false);
            // self, other4: expect collision. But will be overridden with exclude layer below.
            UnityEngine.Physics.IgnoreLayerCollision(layerSelf, layerOther4, false);

            // assign own layer
            Child.layer = layerSelf;

            // assign included and excluded layers
            var includeMask = 1 << layerOther2;
            var excludeMask = 1 << layerOther4;

            body.includeLayers = includeMask;
            body.excludeLayers = excludeMask;

            TestConvertedData<PhysicsCollider>(physicsCollider =>
            {
                var filter = physicsCollider.Value.Value.GetCollisionFilter();
                Assert.That(filter.BelongsTo, Is.EqualTo(1u << layerSelf));
                var collisionMask = filter.CollidesWith;

                // expect no collision for (self, other1)
                Assert.That(collisionMask & (1u << layerOther1), Is.EqualTo(0));
                // expect collision for (self, other2)
                Assert.That(collisionMask & (1u << layerOther2), Is.Not.EqualTo(0));
                // expect collision for (self, other3)
                Assert.That(collisionMask & (1u << layerOther3), Is.Not.EqualTo(0));
                // expect no collision for (self, other4)
                Assert.That(collisionMask & (1u << layerOther4), Is.EqualTo(0));
            });
        }

        [Test]
        public void ColliderConversionSystem_LayerBasedCollision_Mixed([ValueSource(nameof(GetColliderTypes))] Type colliderType)
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] {colliderType, typeof(UnityEngine.Rigidbody)});

            var body = Root.GetComponentInChildren<UnityEngine.Rigidbody>();

            int layerSelf = 0;
            int includeLayer1 = 1;
            int includeLayer2 = 2;
            int excludeLayer1 = 3;
            int excludeLayer2 = 4;

            if (colliderType == typeof(UnityEngine.MeshCollider))
            {
                var meshCollider = Child.GetComponent<UnityEngine.MeshCollider>();
                meshCollider.sharedMesh = ReadableMesh;
            }

            // self, other1: expect no collision. But will be overridden with include layer in collider below.
            UnityEngine.Physics.IgnoreLayerCollision(layerSelf, includeLayer1, true);
            // self, other2: expect no collision. But will be overridden with include layer in rigid body below.
            UnityEngine.Physics.IgnoreLayerCollision(layerSelf, includeLayer2, true);
            // self, other3: expect collision. But will be overridden with exclude layer in collider below.
            UnityEngine.Physics.IgnoreLayerCollision(layerSelf, excludeLayer1, false);
            // self, other4: expect collision. But will be overridden with exclude layer in rigid body below.
            UnityEngine.Physics.IgnoreLayerCollision(layerSelf, excludeLayer2, false);

            // assign own layer
            Child.layer = layerSelf;

            // assign included and excluded layers to rigid body and collider
            var collider = Child.GetComponent<UnityEngine.Collider>();
            collider.includeLayers = 1 << includeLayer1;
            collider.excludeLayers = 1 << excludeLayer1;
            body.includeLayers = 1 << includeLayer2;
            body.excludeLayers = 1 << excludeLayer2;

            TestConvertedData<PhysicsCollider>(physicsCollider =>
            {
                var filter = physicsCollider.Value.Value.GetCollisionFilter();
                Assert.That(filter.BelongsTo, Is.EqualTo(1u << layerSelf));
                var collisionMask = filter.CollidesWith;

                // expect collision for (self, includeLayer1)
                Assert.That(collisionMask & (1u << includeLayer1), Is.Not.EqualTo(0));
                // expect collision for (self, includeLayer2)
                Assert.That(collisionMask & (1u << includeLayer2), Is.Not.EqualTo(0));
                // expect no collision for (self, excludeLayer1)
                Assert.That(collisionMask & (1u << excludeLayer1), Is.EqualTo(0));
                // expect no collision for (self, excludeLayer2)
                Assert.That(collisionMask & (1u << excludeLayer2), Is.EqualTo(0));
            });
        }
    }
}
