using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;

namespace Unity.Physics.Tests.Systems
{
    class BuildPhysicsWorld_UnitTests
    {
        [Test]
        public void OnUpdate_NoBodiesSingleJoint()
        {
            using (var world = new World("Test world"))
            using (var blobAssetStore = new BlobAssetStore(128))
            {
                // Create the system
                var buildPhysicsWorld = world.GetOrCreateSystem<BuildPhysicsWorld>();
                ref var bpwData = ref world.EntityManager.GetComponentDataRW<BuildPhysicsWorldData>(buildPhysicsWorld).ValueRW;

                // Setup PhysicsWorld like it was already built in first system update.
                // Not calling buildPhysicsWorldSystem.Update() here, although it would be the proper way, because there's no way to wait for completion of spawned jobs.
                bpwData.PhysicsData.PhysicsWorld.Reset(1, 0, 0);

                // Create joint entity
                Entity jointEntity = world.EntityManager.CreateEntity(typeof(PhysicsJoint), typeof(PhysicsConstrainedBodyPair));
                world.EntityManager.AddComponentData(jointEntity, PhysicsJoint.CreateFixed(new RigidTransform(quaternion.identity, float3.zero), new RigidTransform(quaternion.identity, float3.zero)));
                world.EntityManager.AddComponentData(jointEntity, new PhysicsConstrainedBodyPair(Entity.Null, Entity.Null, false));

                // Trigger system update
                buildPhysicsWorld.Update(world.Unmanaged);

                // Verify results
                VerifySystemData(ref bpwData, 1, 0, 0);
            }
        }

        [Test]
        public void OnUpdate_NoBodiesNoJoints()
        {
            using (var world = new World("Test world"))
            using (var blobAssetStore = new BlobAssetStore(128))
            {
                // Create the system
                var buildPhysicsWorld = world.GetOrCreateSystem<BuildPhysicsWorld>();
                ref var bpwData = ref world.EntityManager.GetComponentDataRW<BuildPhysicsWorldData>(buildPhysicsWorld).ValueRW;

                // Setup PhysicsWorld like it was already built in first system update.
                // Not calling buildPhysicsWorldSystem.Update() here, although it would be the proper way, because there's no way to wait for completion of spawned jobs.
                bpwData.PhysicsData.PhysicsWorld.Reset(1, 0, 0);

                // Trigger system update
                buildPhysicsWorld.Update(world.Unmanaged);

                // Verify results
                VerifySystemData(ref bpwData, 1, 0, 0);
            }
        }

        public static void VerifySystemData(ref BuildPhysicsWorldData bpwData, int expectedNumBodies, int expectedNumJoints, int expectedStaticBodiesChanged)
        {
            Assert.AreEqual(expectedNumBodies, bpwData.PhysicsData.PhysicsWorld.NumBodies);
            Assert.AreEqual(expectedNumJoints, bpwData.PhysicsData.PhysicsWorld.NumJoints);
            Assert.AreEqual(expectedStaticBodiesChanged, bpwData.PhysicsData.HaveStaticBodiesChanged.Value);
        }
    }
}
