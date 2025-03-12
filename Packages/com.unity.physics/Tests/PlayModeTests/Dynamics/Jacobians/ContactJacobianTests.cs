using System;
using NUnit.Framework;
using Unity.Collections;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.Physics.Tests.Dynamics.Jacobians
{
    class ContactJacobiansTests
    {
        [Test]
        public void SolveTest()
        {
            var jacobian = new ContactJacobian();

            var jacHeader = new JacobianHeader();
            var velocityA = MotionVelocity.Zero;
            var velocityB = MotionVelocity.Zero;
            var stepInput = new Solver.StepInput();
            var collisionEventsWriter = new NativeStream.Writer();

            jacobian.SolveContact(ref jacHeader, ref velocityA, ref velocityB, stepInput, ref collisionEventsWriter,
                false, Solver.MotionStabilizationInput.Default, Solver.MotionStabilizationInput.Default);

            Assert.AreEqual(new JacobianHeader(), jacHeader);
            Assert.AreEqual(MotionVelocity.Zero, velocityA);
            Assert.AreEqual(MotionVelocity.Zero, velocityB);
            Assert.AreEqual(new NativeStream.Writer(), collisionEventsWriter);
        }
    }
}
