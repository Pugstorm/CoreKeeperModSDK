using NUnit.Framework;
using Unity.Entities;
using Unity.Physics.Systems;

namespace Unity.Physics.Tests.Systems
{
    class ColliderBlobCleanup_UnitTests
    {
        // Tests that the ColliderBlobCleanupSystem does not crash when it is run twice on the same set of entities to cleanup.
        //
        // This can frequently occur within a Server World in a netcode environment when deleting entities with PhysicsCollider components
        // that were made unique via the PhysicsCollider.MakeUnique function. In this case, the cleanup system will be triggered more than once
        // when the server performs catch-up physics simulations. In this situation, the entire PhysicsSystemGroup is updated multiple times, including the
        // cleanup system, but the ECB that the cleanup system uses to remove the cleanup data component is not performing its removal between the cleanup
        // system runs. This leads to the cleanup system obtaining the same set of entities to cleanup on each run. Without checking whether the
        // collider blob has already been disposed, the cleanup system will attempt to dispose the same blob multiple times, which will crash.
        [Test]
        public void OnUpdate_DoubleCleanupDoesNotCrash()
        {
            using (var world = new World("Test world"))
            {
                // Create the system and its dependency
                var blobCleanupSystem = world.GetOrCreateSystem<ColliderBlobCleanupSystem>();
                world.GetOrCreateSystemManaged(typeof(EndFixedStepSimulationEntityCommandBufferSystem));

                // Create some collider blob we want the cleanup system to dispose ONCE!
                var colliderBlob = SphereCollider.Create(new SphereGeometry { Radius = 1.0f });

                // Create an entity with a ColliderBlobCleanupData component
                Entity entity = world.EntityManager.CreateEntity();
                world.EntityManager.AddComponentData(entity, new ColliderBlobCleanupData { Value = colliderBlob });
                // Baseline test: Make sure the blob in the cleanup component is created (we don't expect otherwise).
                var cleanupData = world.EntityManager.GetComponentData<ColliderBlobCleanupData>(entity);
                Assert.IsTrue(cleanupData.Value.IsCreated);

                // Trigger system update twice. We expect the cleanup to happen only once and therefore no crash to occur.
                blobCleanupSystem.Update(world.Unmanaged);
                blobCleanupSystem.Update(world.Unmanaged);

                // Check that the blob has been disposed now
                cleanupData = world.EntityManager.GetComponentData<ColliderBlobCleanupData>(entity);
                Assert.IsFalse(cleanupData.Value.IsCreated);
            }
        }
    }
}
