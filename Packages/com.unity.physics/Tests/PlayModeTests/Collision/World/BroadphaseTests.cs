using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Tests.Utils;
using Unity.Transforms;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.Physics.Tests.Collision.PhysicsWorld
{
    class BroadPhaseTests
    {
        /// Util functions
        //Creates a world
        static public Physics.PhysicsWorld createTestWorld(int staticBodies = 0, int dynamicBodies = 0, int joints = 0)
        {
            return new Physics.PhysicsWorld(staticBodies, dynamicBodies, joints);
        }

        //Adds a static box to the world
        static public unsafe void addStaticBoxToWorld(Physics.PhysicsWorld world, int index, Vector3 pos, Quaternion orientation, Vector3 size)
        {
            Assert.IsTrue(index < world.NumStaticBodies, "Static body index is out of range in addStaticBoxToWorld");
            NativeArray<Physics.RigidBody> staticBodies = world.StaticBodies;
            Physics.RigidBody rb = staticBodies[index];
            BlobAssetReference<Collider> collider = BoxCollider.Create(new BoxGeometry
            {
                Center = pos,
                Orientation = orientation,
                Size = size,
                BevelRadius = 0.01f
            });
            rb.Collider = collider;
            staticBodies[index] = rb;
        }

        //Adds a dynamic box to the world
        static public unsafe void addDynamicBoxToWorld(Physics.PhysicsWorld world, int index, Vector3 pos, Quaternion orientation, Vector3 size)
        {
            Assert.IsTrue(index < world.NumDynamicBodies, "Dynamic body index is out of range in addDynamicBoxToWorld");
            NativeArray<Physics.RigidBody> dynamicBodies = world.DynamicBodies;
            Physics.RigidBody rb = dynamicBodies[index];
            BlobAssetReference<Collider> collider = BoxCollider.Create(new BoxGeometry
            {
                Center = pos,
                Orientation = orientation,
                Size = size,
                BevelRadius = 0.01f
            });
            rb.Collider = collider;
            dynamicBodies[index] = rb;
        }

        /// Tests
        //Tests Broadphase Constructor, Init
        [Test]
        public void InitTest()
        {
            Broadphase bf = new Broadphase(0, 0);
            bf.Dispose();
        }

        //Tests ScheduleBuildJobs with one static box in the world
        [Test]
        public void ScheduleBuildJobsOneStaticBoxTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = createTestWorld(1);
                addStaticBoxToWorld(world, 0, new Vector3(0, 0, 0), Quaternion.identity, new Vector3(10, 0.1f, 10));
                var handle = new JobHandle();
                var buildStaticTree = new NativeReference<int>(1, Allocator.TempJob);
                JobHandle result = world.CollisionWorld.Broadphase.ScheduleBuildJobs(ref world, 1 / 60, -9.81f * math.up(), buildStaticTree, handle, numThreads == 1);
                result.Complete();
                Assert.IsTrue(result.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
                buildStaticTree.Dispose();
            }
        }

        //Tests ScheduleBuildJobs with 10 static boxes
        [Test]
        public void ScheduleBuildJobsTenStaticBoxesTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = createTestWorld(10);
                for (int i = 0; i < 10; ++i)
                {
                    addStaticBoxToWorld(world, i, new Vector3(i * 11, 0, 0), Quaternion.identity, new Vector3(10, 0.1f, 10));
                }
                var handle = new JobHandle();
                var buildStaticTree = new NativeReference<int>(1, Allocator.TempJob);
                JobHandle result = world.CollisionWorld.Broadphase.ScheduleBuildJobs(ref world, 1 / 60, -9.81f * math.up(), buildStaticTree, handle, numThreads == 1);

                result.Complete();
                Assert.IsTrue(result.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
                buildStaticTree.Dispose();
            }
        }

        //Tests ScheduleBuildJobs with 100 static boxes
        [Test]
        public void ScheduleBuildJobsOneHundredStaticBoxesTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = createTestWorld(100);
                for (int i = 0; i < 100; ++i)
                {
                    addStaticBoxToWorld(world, i, new Vector3(i * 11, 0, 0), Quaternion.identity, new Vector3(10, 0.1f, 10));
                }
                var handle = new JobHandle();
                var buildStaticTree = new NativeReference<int>(1, Allocator.TempJob);
                JobHandle result = world.CollisionWorld.Broadphase.ScheduleBuildJobs(ref world, 1 / 60, -9.81f * math.up(), buildStaticTree, handle, numThreads == 1);
                result.Complete();
                Assert.IsTrue(result.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
                buildStaticTree.Dispose();
            }
        }

        //Tests Build with one static box in the world
        [Test]
        public void BuildBPOneStaticBoxTest()
        {
            Physics.PhysicsWorld world = createTestWorld(1);
            addStaticBoxToWorld(world, 0, new Vector3(0, 0, 0), Quaternion.identity, new Vector3(10, 0.1f, 10));
            world.CollisionWorld.Broadphase.Build(world.StaticBodies, world.DynamicBodies, world.MotionVelocities,
                world.CollisionWorld.CollisionTolerance, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests Build with 10 static boxes
        [Test]
        public void BuildBPTenStaticBoxesTest()
        {
            Physics.PhysicsWorld world = createTestWorld(10);
            for (int i = 0; i < 10; ++i)
            {
                addStaticBoxToWorld(world, i, new Vector3(i * 11, 0, 0), Quaternion.identity, new Vector3(10, 0.1f, 10));
            }
            world.CollisionWorld.Broadphase.Build(world.StaticBodies, world.DynamicBodies, world.MotionVelocities,
                world.CollisionWorld.CollisionTolerance, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests Build with 100 static boxes
        [Test]
        public void BuildBPOneHundredStaticBoxesTest()
        {
            Physics.PhysicsWorld world = createTestWorld(100);
            for (int i = 0; i < 100; ++i)
            {
                addStaticBoxToWorld(world, i, new Vector3(i * 11, 0, 0), Quaternion.identity, new Vector3(10, 0.1f, 10));
            }
            world.CollisionWorld.Broadphase.Build(world.StaticBodies, world.DynamicBodies, world.MotionVelocities,
                world.CollisionWorld.CollisionTolerance, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests ScheduleBuildJobs with one Dynamic box in the world
        [Test]
        public void ScheduleBuildJobsOneDynamicBoxTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = createTestWorld(0, 1);
                addDynamicBoxToWorld(world, 0, new Vector3(0, 0, 0), Quaternion.identity, new Vector3(10, 10, 10));
                var handle = new JobHandle();
                var buildStaticTree = new NativeReference<int>(1, Allocator.TempJob);
                JobHandle result = world.CollisionWorld.Broadphase.ScheduleBuildJobs(ref world, 1 / 60, -9.81f * math.up(), buildStaticTree, handle, numThreads == 1);
                result.Complete();
                Assert.IsTrue(result.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
                buildStaticTree.Dispose();
            }
        }

        //Tests ScheduleBuildJobs with 10 Dynamic boxes
        [Test]
        public void ScheduleBuildJobsTenDynamicBoxesTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = createTestWorld(0, 10);
                for (int i = 0; i < 10; ++i)
                {
                    addDynamicBoxToWorld(world, i, new Vector3(i * 11, 0, 0), Quaternion.identity, new Vector3(10, 10, 10));
                }
                var handle = new JobHandle();
                var buildStaticTree = new NativeReference<int>(1, Allocator.TempJob);
                JobHandle result = world.CollisionWorld.Broadphase.ScheduleBuildJobs(ref world, 1 / 60, -9.81f * math.up(), buildStaticTree, handle, numThreads == 1);
                result.Complete();
                Assert.IsTrue(result.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
                buildStaticTree.Dispose();
            }
        }

        //Tests ScheduleBuildJobs with 100 Dynamic boxes
        [Test]
        public void ScheduleBuildJobsOneHundredDynamicBoxesTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = createTestWorld(0, 100);
                for (int i = 0; i < 100; ++i)
                {
                    addDynamicBoxToWorld(world, i, new Vector3(i * 11, 0, 0), Quaternion.identity, new Vector3(10, 10, 10));
                }
                var handle = new JobHandle();
                var buildStaticTree = new NativeReference<int>(1, Allocator.TempJob);
                JobHandle result = world.CollisionWorld.Broadphase.ScheduleBuildJobs(ref world, 1 / 60, -9.81f * math.up(), buildStaticTree, handle, numThreads == 1);
                result.Complete();
                Assert.IsTrue(result.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
                buildStaticTree.Dispose();
            }
        }

        //Tests Build with one dynamic box in the world
        [Test]
        public void BuildBPOneDynamicBoxTest()
        {
            Physics.PhysicsWorld world = createTestWorld(0, 1);
            addDynamicBoxToWorld(world, 0, new Vector3(0, 0, 0), Quaternion.identity, new Vector3(10, 10, 10));
            world.CollisionWorld.Broadphase.Build(world.StaticBodies, world.DynamicBodies, world.MotionVelocities,
                world.CollisionWorld.CollisionTolerance, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests Build with 10 dynamic boxes
        [Test]
        public void BuildBPTenDynamicBoxesTest()
        {
            Physics.PhysicsWorld world = createTestWorld(0, 10);
            for (int i = 0; i < 10; ++i)
            {
                addDynamicBoxToWorld(world, i, new Vector3(i * 11, 0, 0), Quaternion.identity, new Vector3(10, 10, 10));
            }
            world.CollisionWorld.Broadphase.Build(world.StaticBodies, world.DynamicBodies, world.MotionVelocities,
                world.CollisionWorld.CollisionTolerance, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests Build with 100 dynamic boxes
        [Test]
        public void BuildBPOneHundredDynamicBoxesTest()
        {
            Physics.PhysicsWorld world = createTestWorld(0, 100);
            for (int i = 0; i < 100; ++i)
            {
                addDynamicBoxToWorld(world, i, new Vector3(i * 11, 0, 0), Quaternion.identity, new Vector3(10, 10, 10));
            }
            world.CollisionWorld.Broadphase.Build(world.StaticBodies, world.DynamicBodies, world.MotionVelocities,
                world.CollisionWorld.CollisionTolerance, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        [Test]
        public void ScheduleBuildJobsStaticAndDynamicBoxesTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = createTestWorld(100, 100);
                for (int i = 0; i < 100; ++i)
                {
                    addStaticBoxToWorld(world, i, new Vector3(i * 11, 0, 0), Quaternion.identity, new Vector3(10, 0.1f, 10));
                    addDynamicBoxToWorld(world, i, new Vector3(i * 11, 5, 0), Quaternion.identity, new Vector3(1, 1, 1));
                }
                var handle = new JobHandle();
                var buildStaticTree = new NativeReference<int>(1, Allocator.TempJob);
                JobHandle result = world.CollisionWorld.Broadphase.ScheduleBuildJobs(ref world, 1 / 60, -9.81f * math.up(), buildStaticTree, handle, numThreads == 1);
                result.Complete();
                Assert.IsTrue(result.IsCompleted);
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
                buildStaticTree.Dispose();
            }
        }

        [Test]
        public void BuildBPStaticAndDynamicBoxesTest()
        {
            Physics.PhysicsWorld world = createTestWorld(100, 100);
            for (int i = 0; i < 100; ++i)
            {
                addStaticBoxToWorld(world, i, new Vector3(i * 11, 0, 0), Quaternion.identity, new Vector3(10, 0.1f, 10));
                addDynamicBoxToWorld(world, i, new Vector3(i * 11, 5, 0), Quaternion.identity, new Vector3(1, 1, 1));
            }
            world.CollisionWorld.Broadphase.Build(world.StaticBodies, world.DynamicBodies, world.MotionVelocities,
                world.CollisionWorld.CollisionTolerance, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }

        //Tests that ScheduleBuildJobs on an empty world returns a completable JobHandle
        [Test]
        public void ScheduleBuildJobsEmptyWorldTest()
        {
            for (int numThreads = 0; numThreads <= 1; numThreads++)
            {
                Physics.PhysicsWorld world = createTestWorld();
                var handle = new JobHandle();
                var buildStaticTree = new NativeReference<int>(1, Allocator.TempJob);
                JobHandle result = world.CollisionWorld.Broadphase.ScheduleBuildJobs(ref world, 1 / 60, -9.81f * math.up(), buildStaticTree, handle, numThreads == 1);
                result.Complete();
                TestUtils.DisposeAllColliderBlobs(ref world);
                world.Dispose();
                buildStaticTree.Dispose();
            }
        }

        //Tests that Build on an empty world doesn't fail
        [Test]
        public void BuildBPEmptyWorldTest()
        {
            Physics.PhysicsWorld world = createTestWorld();
            world.CollisionWorld.Broadphase.Build(world.StaticBodies, world.DynamicBodies, world.MotionVelocities,
                world.CollisionWorld.CollisionTolerance, 1 / 60, -9.81f * math.up());
            TestUtils.DisposeAllColliderBlobs(ref world);
            world.Dispose();
        }
    }
}
