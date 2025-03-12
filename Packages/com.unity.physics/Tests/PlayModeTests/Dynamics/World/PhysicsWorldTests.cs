using System;
using NUnit.Framework;

namespace Unity.Physics.Tests.Dynamics.PhysicsWorld
{
    class PhysicsWorldTests
    {
        [Test]
        public void WorldTest()
        {
            var world = new Physics.PhysicsWorld(10, 5, 10);
            Assert.IsTrue((world.NumDynamicBodies == 5) && (world.NumStaticBodies == 10) && (world.NumBodies == 15) && (world.NumJoints == 10));

            world.Reset(0, 0, 0);
            Assert.IsTrue((world.NumDynamicBodies == 0) && (world.NumStaticBodies == 0) && (world.NumBodies == 0) && (world.NumJoints == 0));

            world.Reset(5, 1, 7);
            Assert.IsTrue((world.NumDynamicBodies == 1) && (world.NumStaticBodies == 5) && (world.NumBodies == 6) && (world.NumJoints == 7));

            // clone world
            var worldClone = new Physics.PhysicsWorld(0, 0, 0);
            Assert.IsTrue((worldClone.NumDynamicBodies == 0) && (worldClone.NumStaticBodies == 0) && (worldClone.NumBodies == 0) && (worldClone.NumJoints == 0));

            worldClone.Dispose();
            worldClone = (Physics.PhysicsWorld)world.Clone();
            Assert.IsTrue((worldClone.NumDynamicBodies == 1) && (worldClone.NumStaticBodies == 5) && (worldClone.NumBodies == 6) && (worldClone.NumJoints == 7));

            // dispose cloned world
            worldClone.Dispose();
            world.Dispose();
        }
    }
}
