using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEditor;
using UnityEngine;

namespace Unity.Physics.Tests.Authoring
{
    class PrefabConversionTests : PrefabConversionTestsBase
    {
        [Test]
        public void PrefabConversion_ChildCollider_ForceUnique([Values] bool forceUniqueCollider)
        {
            var rigidBody = new GameObject("Parent", new[] {typeof(Rigidbody), typeof(UnityEngine.BoxCollider)});

            ValidatePrefabChildColliderUniqueStatus(rigidBody, forceUniqueCollider,
                (gameObject, mass) => { gameObject.GetComponent<Rigidbody>().mass = mass; },
                (gameObject) =>
                {
                    if (forceUniqueCollider) rigidBody.AddComponent<ForceUniqueColliderAuthoring>();
                });
        }
    }

    class ConversionTestPrefabReference : MonoBehaviour
    {
        public GameObject Prefab;
    }

    struct ConversionTestPrefabComponent : IComponentData
    {
        public Entity Prefab;
    }
    class ConversionTestPrefabReferenceBaker : Baker<ConversionTestPrefabReference>
    {
        public override void Bake(ConversionTestPrefabReference authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new ConversionTestPrefabComponent { Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic) });
        }
    }

    internal class PrefabConversionTestsBase : BaseHierarchyConversionTest
    {
        private string TempPrefabAssetPath => "Assets/Temp.prefab";
        private GameObject Prefab;

        void CreatePrefab(GameObject gameObject)
            => Prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, TempPrefabAssetPath);

        [TearDown]
        public override void TearDown()
        {
            AssetDatabase.DeleteAsset(TempPrefabAssetPath);

            base.TearDown();
        }

        /// <summary>
        ///     Ensures that a prefab containing two copies of the provided dynamic rigid body with collider game object
        ///     are baked as expected.
        ///
        /// </summary>
        /// <param name="dynamicRigidBodyWithCollider">The game object containing the dynamic rigid body with collider.</param>
        /// <param name="forceUniqueCollider">A flag indicating whether the collider on the provided game object should be forced to be unique.</param>
        /// <param name="setMassFunction">A function required for the validation which sets the mass on the dynamic rigid body to the specified value.</param>
        /// <param name="forceUniqueFunction">A function which ensures that the provided game object's collider is forced to be unique.</param>
        protected void ValidatePrefabChildColliderUniqueStatus(GameObject dynamicRigidBodyWithCollider, bool forceUniqueCollider, Action<GameObject, float> setMassFunction, Action<GameObject> forceUniqueFunction)
        {
            var child1 = dynamicRigidBodyWithCollider;
            var child2 = UnityEngine.Object.Instantiate(child1);

            // create a prefab with a few colliders that use the same collider, to induce collider sharing
            var prefabRoot = new GameObject("Root");
            child1.transform.parent = prefabRoot.transform;
            child2.transform.parent = prefabRoot.transform;

            // move the second child away a little to prevent any sort of simulation issues, since we will be updating the world
            child2.transform.localPosition = new Vector3(0, 10, 0);

            // use mass to uniquely identify the two rigid bodies
            var child1Mass = 1.0f;
            var child2Mass = 2.0f;
            setMassFunction(child1, child1Mass);
            setMassFunction(child2, child2Mass);

            // Force child 1 to be unique if requested (see forceUniqueCollider flag).
            // Child 2 is never unique, i.e., shared, as a baseline.
            if (forceUniqueCollider)
            {
                forceUniqueFunction(child1);
            }

            // Create a prefab from the prefab root game object.
            // Accessible as member Prefab afterwards.
            CreatePrefab(prefabRoot);

            // create a game object that references the prefab, in order to trigger the prefab baking
            var prefabRef = new GameObject("prefab_ref", typeof(ConversionTestPrefabReference));
            var prefabRefComponent = prefabRef.GetComponent<ConversionTestPrefabReference>();
            Assert.That(prefabRefComponent, Is.Not.Null);
            prefabRefComponent.Prefab = Prefab;

            var world = DefaultWorldInitialization.Initialize("Test world");
            try
            {
                using var blobAssetStore = new BlobAssetStore(128);
                ConvertBakeGameObject(prefabRef, world, blobAssetStore);

                using var prefabRootQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
                        .WithOptions(EntityQueryOptions.IncludePrefab)
                        .WithAll<Prefab>()
                            .WithAll<LinkedEntityGroup>();

                using var prefabInstanceRootQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
                        .WithAll<LinkedEntityGroup>();

                using var colliderQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
                        .WithAll<PhysicsCollider>();

                // at this point we expect exactly one prefab roots and no prefab instances or colliders
                using (var prefabRoots = world.EntityManager.CreateEntityQuery(prefabRootQueryBuilder))
                {
                    Assert.That(prefabRoots.CalculateEntityCount(), Is.EqualTo(1));
                }

                using (var prefabInstanceRoots = world.EntityManager.CreateEntityQuery(prefabInstanceRootQueryBuilder))
                {
                    Assert.That(prefabInstanceRoots.CalculateEntityCount(), Is.EqualTo(0));
                }

                using var colliders = world.EntityManager.CreateEntityQuery(colliderQueryBuilder);
                Assert.That(colliders.CalculateEntityCount(), Is.EqualTo(0));

                // instantiate a few instances of the prefab
                using var prefabRefComponentsQuery =
                        world.EntityManager.CreateEntityQuery(typeof(ConversionTestPrefabComponent));
                using var prefabRefComponents =
                        prefabRefComponentsQuery.ToComponentDataArray<ConversionTestPrefabComponent>(Allocator.Temp);
                Assert.That(prefabRefComponents.Length, Is.EqualTo(1));

                var entityPrefab = prefabRefComponents[0].Prefab;
                const int kInstanceCount = 10;
                using var prefabInstances = new NativeArray<Entity>(kInstanceCount, Allocator.Temp);
                world.EntityManager.Instantiate(entityPrefab, prefabInstances);

                // for each entity prefab instance make sure that the contained colliders have the correct collider state
                foreach (var rootEntity in prefabInstances)
                {
                    int prefabColliderCount = 0;

                    // regardless of the forceUniqueCollider flag, we expect the colliders to be shared initially after
                    // prefab instantiation
                    Assert.That(world.EntityManager.HasComponent<LinkedEntityGroup>(rootEntity), Is.True);
                    var linkedEntityGroup = world.EntityManager.GetBuffer<LinkedEntityGroup>(rootEntity);
                    foreach (var element in linkedEntityGroup)
                    {
                        var entity = element.Value;

                        // Note: the LinkedEntityGroup buffer contains all entities in the entity prefab instance, including the root.
                        // We need to filter it out.
                        if (entity != rootEntity)
                        {
                            Assert.That(world.EntityManager.HasComponent<PhysicsCollider>(entity));
                            var collider = world.EntityManager.GetComponentData<PhysicsCollider>(entity);
                            ++prefabColliderCount;
                            Assert.That(collider.IsUnique, Is.False);
                        }
                    }

                    Assert.That(prefabColliderCount, Is.EqualTo(2));
                }

                // update world and expect the colliders to now be unique if requested
                world.Update();

                // validate world content:

                using (var prefabInstanceRoots = world.EntityManager.CreateEntityQuery(prefabInstanceRootQueryBuilder))
                {
                    Assert.That(prefabInstanceRoots.CalculateEntityCount(), Is.EqualTo(kInstanceCount));

                    using var rootEntities = prefabInstanceRoots.ToEntityArray(Allocator.Temp);
                    // ensure the colliders have the correct IsUnique state and share their colliders only if requested.
                    foreach (var rootEntity in rootEntities)
                    {
                        BlobAssetReference<Collider> child1Collider = default, child2Collider = default;

                        // Depending on the forceUniqueCollider flag, we expect the collider in child1 to now be unique.
                        // Child2 is still going to be indicated as not unique.
                        Assert.That(world.EntityManager.HasComponent<LinkedEntityGroup>(rootEntity), Is.True);
                        var linkedEntityGroup = world.EntityManager.GetBuffer<LinkedEntityGroup>(rootEntity);
                        foreach (var element in linkedEntityGroup)
                        {
                            var entity = element.Value;
                            if (entity != rootEntity)
                            {
                                Assert.That(world.EntityManager.HasComponent<PhysicsCollider>(entity));
                                var collider = world.EntityManager.GetComponentData<PhysicsCollider>(element.Value);
                                var mass = world.EntityManager.GetComponentData<PhysicsMass>(element.Value);
                                bool isChild1 = math.abs(mass.InverseMass - 1 / child1Mass) < 1e-4;

                                if (!isChild1)
                                {
                                    // ensure that the mass is as expected
                                    Assert.That(math.abs(mass.InverseMass - 1 / child2Mass) < 1e-4, Is.True);
                                    child2Collider = collider.Value;
                                }
                                else
                                {
                                    child1Collider = collider.Value;
                                }

                                bool expectUnique = isChild1 && forceUniqueCollider;
                                Assert.That(collider.IsUnique, Is.EqualTo(expectUnique));
                            }
                        }

                        Assert.That(child1Collider.IsCreated && child2Collider.IsCreated, Is.True);
                        // ensure that the colliders are not the same if they should not be shared (force unique requested on child 1)
                        unsafe
                        {
                            bool collidersShared = child1Collider.GetUnsafePtr() == child2Collider.GetUnsafePtr();
                            Assert.That(collidersShared, Is.Not.EqualTo(forceUniqueCollider));
                        }
                    }
                }

                world.Update();

                // If we are forcing the colliders to be unique, we are expecting some entities with the ColliderBlobCleanupData component.
                // This component is required as part of the PhysicsCollider.MakeUnique function. Otherwise we don't expect any such components.
                using (var colliderBlobCleanupQuery = world.EntityManager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<ColliderBlobCleanupData>()))
                {
                    Assert.That(colliderBlobCleanupQuery.IsEmpty, Is.Not.EqualTo(forceUniqueCollider));
                }

                // destroy instantiated entities and prefab and expect them to be cleaned up properly
                using (var allEntities = world.EntityManager.CreateEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                    .WithAny<LinkedEntityGroup, PhysicsCollider>()
                    .WithOptions(EntityQueryOptions.IncludePrefab)))
                {
                    world.EntityManager.DestroyEntity(allEntities);
                }

                world.Update();

                // After we have destroyed the entities we created and updated the world, we don't expect any more ColliderBlobCleanupData components.
                // They should have been cleared out by now.
                using (var colliderBlobCleanupQuery = world.EntityManager.CreateEntityQuery(
                    new EntityQueryBuilder(Allocator.Temp)
                        .WithAll<ColliderBlobCleanupData>()))
                {
                    Assert.That(colliderBlobCleanupQuery.IsEmpty, Is.True);
                }
            }
            finally
            {
                World.DisposeAllWorlds();
            }
        }
    }
}
