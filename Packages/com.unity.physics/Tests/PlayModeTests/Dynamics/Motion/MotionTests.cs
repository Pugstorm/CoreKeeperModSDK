using System;
using NUnit.Framework;
using Unity.Mathematics;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.Physics.Tests.Dynamics.Motion
{
    class MotionTests
    {
        [Test]
        public void MassPropertiesUnitSphereTest()
        {
            var unitSphere = MassProperties.UnitSphere;

            Assert.AreEqual(float3.zero, unitSphere.MassDistribution.Transform.pos);
            Assert.AreEqual(quaternion.identity, unitSphere.MassDistribution.Transform.rot);
            Assert.AreEqual(new float3(0.4f), unitSphere.MassDistribution.InertiaTensor);
            Assert.AreEqual(0.0f, unitSphere.AngularExpansionFactor);
        }

        [Test]
        public void MotionVelocityApplyLinearImpulseTest()
        {
            var motionVelocity = new MotionVelocity()
            {
                LinearVelocity = new float3(3.0f, 4.0f, 5.0f),
                InverseInertia = float3.zero,
                InverseMass = 2.0f
            };
            motionVelocity.ApplyLinearImpulse(new float3(1.0f, 2.0f, 3.0f));

            Assert.AreEqual(new float3(5.0f, 8.0f, 11.0f), motionVelocity.LinearVelocity);
        }

        [Test]
        public void MotionVelocityApplyAngularImpulseTest()
        {
            var motionVelocity = new MotionVelocity()
            {
                AngularVelocity = new float3(3.0f, 4.0f, 5.0f),
                InverseInertia = new float3(2.0f, 3.0f, 4.0f),
                InverseMass = 2.0f
            };
            motionVelocity.ApplyAngularImpulse(new float3(1.0f, 2.0f, 3.0f));

            Assert.AreEqual(new float3(5.0f, 10.0f, 17.0f), motionVelocity.AngularVelocity);
        }

        [Test]
        public void MotionVelocityCalculateExpansionTest()
        {
            var motionVelocity = new MotionVelocity()
            {
                LinearVelocity = new float3(2.0f, 1.0f, 5.0f),
                AngularVelocity = new float3(3.0f, 4.0f, 5.0f),
                InverseInertia = new float3(2.0f, 3.0f, 4.0f),
                InverseMass = 2.0f,
                AngularExpansionFactor = 1.2f
            };
            var motionExpansion = motionVelocity.CalculateExpansion(1.0f / 60.0f);

            Assert.AreEqual(new float3(1.0f / 30.0f, 1.0f / 60.0f, 1.0f / 12.0f), motionExpansion.Linear);
            Assert.AreApproximatelyEqual((float)math.SQRT2 / 10.0f, motionExpansion.Uniform);
        }

        [Test]
        public void MotionExpansionMaxDistanceTest()
        {
            var motionExpansion = new MotionExpansion()
            {
                Linear = new float3(2.0f, 3.0f, 4.0f),
                Uniform = 5.0f
            };

            Assert.AreEqual(math.sqrt(29.0f) + 5.0f, motionExpansion.MaxDistance);
        }

        [Test]
        public void MotionExpansionSweepAabbTest()
        {
            var motionExpansion = new MotionExpansion()
            {
                Linear = new float3(2.0f, 3.0f, 4.0f),
                Uniform = 5.0f
            };
            var aabb = motionExpansion.ExpandAabb(new Aabb() { Min = new float3(-10.0f, -10.0f, -10.0f), Max = new float3(10.0f, 10.0f, 10.0f) });

            Assert.AreEqual(new float3(-15.0f, -15.0f, -15.0f), aabb.Min);
            Assert.AreEqual(new float3(17.0f, 18.0f, 19.0f), aabb.Max);
        }
    }
}
