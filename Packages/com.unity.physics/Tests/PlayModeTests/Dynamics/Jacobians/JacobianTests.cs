//#define PLOT_SPRING_DAMPER

using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.Physics.Tests.Dynamics.Jacobians
{
    class JacobiansTests
    {
        [Test]
        public void JacobianUtilitiesCalculateTauAndDampingTest()
        {
            float springFrequency = 1.0f;
            float springDampingRatio = 1.0f;
            float timestep = 1.0f;
            int iterations = 4;

            JacobianUtilities.CalculateConstraintTauAndDamping(springFrequency, springDampingRatio, timestep, iterations, out float tau, out float damping);

            Assert.AreApproximatelyEqual(0.4774722f, tau);
            Assert.AreApproximatelyEqual(0.6294564f, damping);
        }

        [Test]
        public void JacobianUtilitiesCalculateTauAndDampingFromConstraintTest()
        {
            var constraint = new Constraint { SpringFrequency = 1.0f, DampingRatio = 1.0f };
            float timestep = 1.0f;
            int iterations = 4;

            float tau;
            float damping;
            JacobianUtilities.CalculateConstraintTauAndDamping(constraint.SpringFrequency, constraint.DampingRatio, timestep, iterations, out tau, out damping);

            Assert.AreApproximatelyEqual(0.4774722f, tau);
            Assert.AreApproximatelyEqual(0.6294564f, damping);
        }

        private static readonly TestCaseData[] k_JacobianCalculateErrorCases =
        {
            new TestCaseData(5.0f, 0.0f, 10.0f, 0.0f).SetName("Case 1: Within threshold"),
            new TestCaseData(-5.0f, 0.0f, 10.0f, -5.0f).SetName("Case 2: Below threshold"),
            new TestCaseData(15.0f, 0.0f, 10.0f, 5.0f).SetName("Case 3: Above threshold"),
        };

        [TestCaseSource(nameof(k_JacobianCalculateErrorCases))]
        public void JacobianUtilitiesCalculateErrorTest(float x, float min, float max, float expected)
        {
            Assert.AreApproximatelyEqual(expected, JacobianUtilities.CalculateError(x, min, max));
        }

        [Test]
        public void JacobianUtilitiesCalculateCorrectionTest()
        {
            float predictedError = 0.2f;
            float initialError = 0.1f;
            float tau = 0.6f;
            float damping = 1.0f;

            Assert.AreApproximatelyEqual(0.16f, JacobianUtilities.CalculateCorrection(predictedError, initialError, tau, damping));
        }

        [Test]
        public void JacobianUtilitiesIntegrateOrientationBFromATest()
        {
            var bFromA = quaternion.identity;
            var angularVelocityA = float3.zero;
            var angularVelocityB = float3.zero;
            var timestep = 1.0f;

            Assert.AreEqual(quaternion.identity, JacobianUtilities.IntegrateOrientationBFromA(bFromA, angularVelocityA, angularVelocityB, timestep));
        }

        [Test]
        public void JacobianIteratorHasJacobiansLeftTest()
        {
            var jacobianStream = new NativeStream(1, Allocator.Temp);
            NativeStream.Reader jacobianStreamReader = jacobianStream.AsReader();
            int workItemIndex = 0;
            var jacIterator = new JacobianIterator(jacobianStreamReader, workItemIndex);

            Assert.IsFalse(jacIterator.HasJacobiansLeft());

            jacobianStream.Dispose();
        }

        float CalculateSpringStiffnessFromSpringFrequency(float springFrequency, float mass)
        {
            var factor = springFrequency * Math.Constants.Tau;
            return factor * factor * mass;
        }

        float CalculateDampingCoefficientFromDampingRatio(float dampingRatio, float springStiffness, float mass)
        {
            return dampingRatio * 2 * math.sqrt(springStiffness * mass);
        }

        [Test]
        public void JacobianUtilitiesTestConstraintRegularization([NUnit.Framework.Values(30, 60, 120)] float frameRate,
            [NUnit.Framework.Values(1, 4, 20)] int iterations,
            [NUnit.Framework.Values(1f, 10f)] float mass,
            [NUnit.Framework.Values(100, 1000)] float springFrequency,
            [NUnit.Framework.Values(0.0f, 0.8f, 100.0f)] float dampingRatio)
        {
            // Compare implicit euler simulation of a spring-damper with the regularized gauss-seidel solver used in
            // the solver. The implicit euler simulation is the ground truth which we want to match.
            // This test simulates a few steps of a spring-damper system with a given spring frequency and damping ratio under
            // with time steps and solver iterations.

            var kSimulationTime = 2.0f;
            var kNumSteps = (int)(kSimulationTime * frameRate);  // simulate for 2 seconds
            var timeStep = 1 / frameRate;

            var kInitialPosError = 0.1f;
            var kInitialVelError = 0.2f;
            var expectedPositions = new NativeArray<float>(kNumSteps, Allocator.Temp);
            var expectedVelocities = new NativeArray<float>(kNumSteps, Allocator.Temp);

            /*
             Implicit Euler integration of a spring-damper:

                Constitutive equation of a spring-damper: F = -kx - cx'
                Backwards euler of the equation of motion a = x'' with a = F/m:
                where h = step length:

                    x2 = x1 + hv2
                    v2 = v1 + hx''
                       = v1 + hF/m
                       = v1 + h(-kx2 - cv2)/m
                       = v1 + h(-kx1 - hkv2 - cv2)/m
                       = 1 / (1 + h^2k/m + hc/m) * v1 - hk / (m + h^2k + hc) * x1
             */
            var x = kInitialPosError;
            var v = kInitialVelError;
            var springCoefficient = CalculateSpringStiffnessFromSpringFrequency(springFrequency, mass);
            var dampingCoefficient = CalculateDampingCoefficientFromDampingRatio(dampingRatio, springCoefficient, mass);

#if PLOT_SPRING_DAMPER
            Debug.Log("Implicit Euler:");
#endif
            for (int i = 0; i < kNumSteps; ++i)
            {
#if PLOT_SPRING_DAMPER
                Debug.Log($"(pos, vel) = ({x}, {v})");
#endif

                expectedPositions[i] = x;
                expectedVelocities[i] = v;

                // integrate velocity
                var vFactor = 1 / (1 + timeStep * timeStep * springCoefficient / mass + timeStep * dampingCoefficient / mass);
                var xFactor = (timeStep * springCoefficient) /
                    (mass + timeStep * timeStep * springCoefficient + timeStep * dampingCoefficient);
                v = vFactor * v - xFactor * x;

                // integrate position
                x += timeStep * v;
            }

            /*
              Gauss-Seidel iterations of a stiff constraint with Baumgarte stabilization parameters t and a, where
              t = tau, d = damping, and a = 1 - d:

                Example for four iterations:

                    v2 = av1 - (t / h)x1
                    v3 = av2 - (t / h)x1
                    v4 = av3 - (t / h)x1
                    v5 = av4 - (t / h)x1
                       = a^4v1 - (a^3 + a^2 + a + 1)(t / h)x1

                Given the recursive nature of the relationship above we can derive a closed-form expression for the new velocity with n iterations:
                    v_n = a * v_n-1 - (t / h) * x1
                        = a^n * v1 - (a^(n-1) + a^(n-2) + ... + a + 1)(t / h) * x1
                        = a^n * v1 - (\sum_{i=0}^{n-1} a^i)(t / h) * x1
                        = a^n * v1 - ((1 - a^n) / (1 - a))(t / h) * x1

                Position integration is identical to the implicit euler integration above.
             */

            x = kInitialPosError;
            v = kInitialVelError;
            var actualPositions = new NativeArray<float>(kNumSteps, Allocator.Temp);
            var actualVelocities = new NativeArray<float>(kNumSteps, Allocator.Temp);

            JacobianUtilities.CalculateConstraintTauAndDamping(springFrequency, dampingRatio, timeStep, iterations, out var tau, out var damping);

#if PLOT_SPRING_DAMPER
            Debug.Log("Gauss-Seidel:");
#endif
            for (int i = 0; i < kNumSteps; ++i)
            {
#if PLOT_SPRING_DAMPER
                Debug.Log($"(pos, vel) = ({x}, {v})");
#endif

                actualPositions[i] = x;
                actualVelocities[i] = v;

                // integrate velocity using Gauss-Seidel style iterations with constraint regularization
                var a = 1 - damping;
                var vNew = v;
                for (int j = 0; j < iterations; ++j)
                {
                    vNew = a * vNew - (tau / timeStep) * x;
                }
                // check if this is identical to the closed-form expression for the new velocity:
                var aPow = math.pow(a, iterations);
                var vClosedForm = aPow * v - ((1.0f - aPow) / (1 - a)) * (tau / timeStep) * x;
                Assert.AreApproximatelyEqual(vClosedForm, vNew);

                // set new velocity
                v = vNew;

                // integrate position
                x += timeStep * v;
            }

            // compare results:
            for (int i = 0; i < kNumSteps; ++i)
            {
                Assert.AreApproximatelyEqual(expectedPositions[i], actualPositions[i]);
                Assert.AreApproximatelyEqual(expectedVelocities[i], actualVelocities[i]);
            }
        }
    }
}
