using System;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics.Tests.Utils;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.Physics.Tests.Collision.PhysicsWorld
{
    class CollisionWorldTests
    {
        //Tests creating a Zero body world
        [Test]
        public void ZeroBodyInitTest()
        {
            CollisionWorld world = new CollisionWorld(0, 0);
            Assert.IsTrue(world.NumBodies == 0);
            world.Dispose();
        }

        //Tests creating a 10 body world
        [Test]
        public void TenBodyInitTest()
        {
            CollisionWorld world = new CollisionWorld(10, 0);
            Assert.IsTrue(world.NumBodies == 10);
            // The bodies/colliders in this world not not initialized, so they do not need to be disposed.
            world.Dispose();
        }

        //Tests updating an empty world
        [Test]
        public void ScheduleUpdateJobsEmptyWorldTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld();
                Unity.Jobs.JobHandle handle = new Unity.Jobs.JobHandle();
                Unity.Jobs.JobHandle worldJobHandle = world.CollisionWorld.ScheduleUpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up(), handle, numThreads == 1);
                worldJobHandle.Complete();
                Assert.IsTrue(worldJobHandle.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
            }
        }

        //Tests updating an empty world
        [Test]
        public void UpdateEmptyWorldTest()
        {
            Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld();
            world.CollisionWorld.UpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests updating a static box
        [Test]
        public void ScheduleUpdateJobsOneStaticBoxTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Unity.Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(1);
                BroadPhaseTests.addStaticBoxToWorld(world, 0, Vector3.zero, quaternion.identity, new Vector3(10, .1f, 10));
                Unity.Jobs.JobHandle handle = new Unity.Jobs.JobHandle();
                Unity.Jobs.JobHandle worldJobHandle = world.CollisionWorld.ScheduleUpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up(), handle, numThreads == 1);
                worldJobHandle.Complete();
                Assert.IsTrue(worldJobHandle.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
            }
        }

        //Tests updating a static box
        [Test]
        public void UpdateWorldOneStaticBoxTest()
        {
            Unity.Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(1);
            BroadPhaseTests.addStaticBoxToWorld(world, 0, Vector3.zero, quaternion.identity, new Vector3(10, .1f, 10));
            world.CollisionWorld.UpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests updating 10 static boxes
        [Test]
        public void ScheduleUpdateJobsTenStaticBoxesTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(10);
                for (int i = 0; i < 10; ++i)
                    BroadPhaseTests.addStaticBoxToWorld(world, i, new Vector3(11 * i, 0, 0), quaternion.identity, new Vector3(10, .1f, 10));
                Unity.Jobs.JobHandle handle = new Unity.Jobs.JobHandle();
                Unity.Jobs.JobHandle worldJobHandle = world.CollisionWorld.ScheduleUpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up(), handle, numThreads == 1);
                worldJobHandle.Complete();
                Assert.IsTrue(worldJobHandle.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
            }
        }

        //Tests updating 10 static boxes
        [Test]
        public void UpdateWorldTenStaticBoxesTest()
        {
            Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(10);
            for (int i = 0; i < 10; ++i)
                BroadPhaseTests.addStaticBoxToWorld(world, i, new Vector3(11 * i, 0, 0), quaternion.identity, new Vector3(10, .1f, 10));
            world.CollisionWorld.UpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests updating 100 static boxes
        [Test]
        public void ScheduleUpdateJobsOneHundredStaticBoxesTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(100);
                for (int i = 0; i < 100; ++i)
                    BroadPhaseTests.addStaticBoxToWorld(world, i, new Vector3(11 * i, 0, 0), quaternion.identity, new Vector3(10, .1f, 10));
                Unity.Jobs.JobHandle handle = new Unity.Jobs.JobHandle();
                Unity.Jobs.JobHandle worldJobHandle = world.CollisionWorld.ScheduleUpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up(), handle, numThreads == 1);
                worldJobHandle.Complete();
                Assert.IsTrue(worldJobHandle.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
            }
        }

        //Tests updating 100 static boxes
        [Test]
        public void UpdateWorldOneHundredStaticBoxesTest()
        {
            Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(100);
            for (int i = 0; i < 100; ++i)
                BroadPhaseTests.addStaticBoxToWorld(world, i, new Vector3(11 * i, 0, 0), quaternion.identity, new Vector3(10, .1f, 10));
            world.CollisionWorld.UpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests updating a Dynamic box
        [Test]
        public void ScheduleUpdateJobsOneDynamicBoxTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(0, 1);
                BroadPhaseTests.addDynamicBoxToWorld(world, 0, Vector3.zero, quaternion.identity, new Vector3(10, .1f, 10));
                Unity.Jobs.JobHandle handle = new Unity.Jobs.JobHandle();
                Unity.Jobs.JobHandle worldJobHandle = world.CollisionWorld.ScheduleUpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up(), handle, numThreads == 1);
                worldJobHandle.Complete();
                Assert.IsTrue(worldJobHandle.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
            }
        }

        //Tests updating a Dynamic box
        [Test]
        public void UpdateWorldOneDynamicBoxTest()
        {
            Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(0, 1);
            BroadPhaseTests.addDynamicBoxToWorld(world, 0, Vector3.zero, quaternion.identity, new Vector3(10, .1f, 10));
            world.CollisionWorld.UpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests updating 10 dynamic boxes
        [Test]
        public void ScheduleUpdateJobsTenDynamicBoxesTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(0, 10);
                for (int i = 0; i < 10; ++i)
                    BroadPhaseTests.addDynamicBoxToWorld(world, i, new Vector3(11 * i, 0, 0), quaternion.identity, new Vector3(10, .1f, 10));
                Unity.Jobs.JobHandle handle = new Unity.Jobs.JobHandle();
                Unity.Jobs.JobHandle worldJobHandle = world.CollisionWorld.ScheduleUpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up(), handle, numThreads == 1);
                worldJobHandle.Complete();
                Assert.IsTrue(worldJobHandle.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
            }
        }

        //Tests updating 10 dynamic boxes
        [Test]
        public void UpdateWorldTenDynamicBoxesTest()
        {
            Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(0, 10);
            for (int i = 0; i < 10; ++i)
                BroadPhaseTests.addDynamicBoxToWorld(world, i, new Vector3(11 * i, 0, 0), quaternion.identity, new Vector3(10, .1f, 10));
            world.CollisionWorld.UpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests updating 100 dynamic boxes
        [Test]
        public void ScheduleUpdateJobsOneHundredDynamicBoxesTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(0, 100);
                for (int i = 0; i < 100; ++i)
                    BroadPhaseTests.addDynamicBoxToWorld(world, i, new Vector3(11 * i, 0, 0), quaternion.identity, new Vector3(10, .1f, 10));
                Unity.Jobs.JobHandle handle = new Unity.Jobs.JobHandle();
                Unity.Jobs.JobHandle worldJobHandle = world.CollisionWorld.ScheduleUpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up(), handle, numThreads == 1);
                worldJobHandle.Complete();
                Assert.IsTrue(worldJobHandle.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
            }
        }

        //Tests updating 100 dynamic boxes
        [Test]
        public void UpdateWorldOneHundredDynamicBoxesTest()
        {
            Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(0, 100);
            for (int i = 0; i < 100; ++i)
                BroadPhaseTests.addDynamicBoxToWorld(world, i, new Vector3(11 * i, 0, 0), quaternion.identity, new Vector3(10, .1f, 10));
            world.CollisionWorld.UpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests updating 100 static and dynamic boxes
        [Test]
        public void ScheduleUpdateJobsStaticAndDynamicBoxesTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(100, 100);
                for (int i = 0; i < 100; ++i)
                {
                    BroadPhaseTests.addDynamicBoxToWorld(world, i, new Vector3(11 * i, 0, 0), quaternion.identity, new Vector3(10, .1f, 10));
                    BroadPhaseTests.addStaticBoxToWorld(world, i, new Vector3(11 * i, 0, 0), quaternion.identity, new Vector3(10, .1f, 10));
                }

                Unity.Jobs.JobHandle handle = new Unity.Jobs.JobHandle();
                Unity.Jobs.JobHandle worldJobHandle = world.CollisionWorld.ScheduleUpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up(), handle, numThreads == 1);
                worldJobHandle.Complete();
                Assert.IsTrue(worldJobHandle.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
            }
        }

        //Tests updating 100 static and dynamic boxes
        [Test]
        public void UpdateWorldStaticAndDynamicBoxesTest()
        {
            Physics.PhysicsWorld world = BroadPhaseTests.createTestWorld(100, 100);
            for (int i = 0; i < 100; ++i)
            {
                BroadPhaseTests.addDynamicBoxToWorld(world, i, new Vector3(11 * i, 0, 0), quaternion.identity, new Vector3(10, .1f, 10));
                BroadPhaseTests.addStaticBoxToWorld(world, i, new Vector3(11 * i, 0, 0), quaternion.identity, new Vector3(10, .1f, 10));
            }

            world.CollisionWorld.UpdateDynamicTree(ref world, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }
    }
}
