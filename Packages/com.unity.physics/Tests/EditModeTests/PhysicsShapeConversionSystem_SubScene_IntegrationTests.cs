using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Unity.Physics.Tests.Authoring
{
    // Conversion system integration tests for physics shapes, including legacy physics
    class ColliderConversionSystem_SubScene_IntegrationTests
        : ConversionSystem_SubScene_IntegrationTestsFixture
    {
        UnityEngine.Mesh NonReadableMesh { get; set; }

        [OneTimeSetUp]
        protected new void OneTimeSetUp()
        {
            base.OneTimeSetUp();

            // create non-readable mesh asset
            NonReadableMesh = UnityEngine.Mesh.Instantiate(Resources.GetBuiltinResource<UnityEngine.Mesh>("New-Cylinder.fbx"));
            NonReadableMesh.UploadMeshData(true);
            Assume.That(NonReadableMesh.isReadable, Is.False, $"{NonReadableMesh} was readable.");
            AssetDatabase.CreateAsset(NonReadableMesh, $"{TemporaryAssetsPath}/NonReadableMesh.asset");
        }

        void CreateSubSceneAndValidate<T>(Action<T> configureSubSceneObject, ColliderType expectedColliderType)
            where T : Component
        {
            CreateAndLoadSubScene(configureSubSceneObject);

            // check result
            var world = World.DefaultGameObjectInjectionWorld;
            using (var group = world.EntityManager.CreateEntityQuery(typeof(PhysicsCollider)))
            using (var bodies = group.ToComponentDataArray<PhysicsCollider>(Allocator.Persistent))
            {
                Assume.That(bodies, Has.Length.EqualTo(1));
                Assume.That(bodies[0].IsValid, Is.True);
                Assert.That(bodies[0].Value.Value.Type, Is.EqualTo(expectedColliderType));
            }
        }

        [Test]
        public void ColliderConversionSystem_WhenShapeIsConvexWithNonReadableMesh_IsInSubScene_DoesNotThrow() =>
            CreateSubSceneAndValidate<UnityEngine.MeshCollider>(
                shape =>
                {
                    shape.sharedMesh = NonReadableMesh;
                    shape.convex = true;
                },
                ColliderType.Convex
            );

        [Test]
        public void ColliderConversionSystem_WhenShapeIsMeshWithNonReadableMesh_IsInSubScene_DoesNotThrow() =>
            CreateSubSceneAndValidate<UnityEngine.MeshCollider>(
                shape =>
                {
                    shape.sharedMesh = NonReadableMesh;
                    shape.convex = false;
                },
                ColliderType.Mesh
            );
    }
}
