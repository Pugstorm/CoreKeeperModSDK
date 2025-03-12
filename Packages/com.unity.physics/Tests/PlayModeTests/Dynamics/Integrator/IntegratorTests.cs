using System;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.Physics.Tests.Dynamics.Integrator
{
    class IntegratorTests
    {
        [Test]
        public void IntegrateOrientationTest()
        {
            var orientation = quaternion.identity;
            var angularVelocity = new float3(0.0f, 0.0f, 0.0f);
            float timestep = 1.0f;

            Physics.Integrator.IntegrateOrientation(ref orientation, angularVelocity, timestep);

            Assert.AreEqual(new quaternion(0.0f, 0.0f, 0.0f, 1.0f), orientation);
        }

        [Test]
        public void IntegrateAngularVelocityTest()
        {
            var angularVelocity = new float3(1.0f, 2.0f, 3.0f);
            float timestep = 4.0f;

            var orientation = Unity.Physics.Integrator.IntegrateAngularVelocity(angularVelocity, timestep);

            Assert.AreEqual(new quaternion(2.0f, 4.0f, 6.0f, 1.0f), orientation);
        }
    }
}
