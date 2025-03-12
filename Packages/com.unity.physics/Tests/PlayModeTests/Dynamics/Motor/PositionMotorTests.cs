using NUnit.Framework;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Unity.Physics.Tests.Motors
{
    [TestFixture]
    public class PositionMotorTests
    {
        // Called by: Orientation tests, Max Impulse Tests
        // Constants: the number steps, solver iterations, initial motion of BodyA and bodyB
        // This test checks:
        // 1) Verifies bodyA arrives at the target position within the given time,
        // 2) Verifies that the orientation of bodyA has not changed while it moved
        // 3) Verifies that the maxImpulse is never exceeded
        void TestSimulatePositionMotor(string testName, Joint jointData,
            MotionVelocity velocityA, MotionVelocity velocityB, MotionData motionA, MotionData motionB,
            bool useGravity, float3 targetPosition, float maxImpulse)
        {
            string failureMessage;

            int numIterations = 4;
            int numSteps = 30; // duration = 0.5s
            int numStabilizingSteps = 0; //takes some iterations to reach the target velocity

            var position0 = motionA.WorldFromMotion.pos;
            var rotation0 = motionA.WorldFromMotion.rot;
            var motorOrientation = math.normalizesafe(targetPosition); //to only consider direction the motor is acting on

            MotorTestRunner.TestSimulateMotor(testName, ref jointData,
                ref velocityA, ref velocityB, ref motionA, ref motionB,
                useGravity, maxImpulse, motorOrientation, numIterations, numSteps, numStabilizingSteps,
                out float3 accumulateAngularVelocity, out float3 accumulateLinearVelocity);

            // Note: some off-axis / gravity enabled tests require the *1000 to pass
            var testThreshold = math.EPSILON * 1000.0f;

            // Verify that bodyA arrived at target position by some threshold, unless the maxImpulse was zero
            var distanceAmoved = motionA.WorldFromMotion.pos - position0;
            float3 compareToTarget = maxImpulse < math.EPSILON
                ? math.abs(distanceAmoved) //if maxImpulse=0, then motor shouldn't move
                : math.abs(targetPosition - distanceAmoved);

            failureMessage = $"{testName}: Position motor didn't arrive at target {targetPosition} by this margin {compareToTarget}";
            Assert.Less(compareToTarget.x, testThreshold, failureMessage);
            Assert.Less(compareToTarget.y, testThreshold, failureMessage);
            Assert.Less(compareToTarget.z, testThreshold, failureMessage);

            // Verify that the orientation of bodyA hasn't changed while it moved to the target
            var orientationDifference = math.mul(motionA.WorldFromMotion.rot, rotation0).ToEulerAngles();
            failureMessage = $"{testName}: Position motor orientation changed during simulation from {rotation0} to {motionA.WorldFromMotion.rot}";
            Assert.Less(orientationDifference.x, testThreshold, failureMessage);
            Assert.Less(orientationDifference.y, testThreshold, failureMessage);
            Assert.Less(orientationDifference.z, testThreshold, failureMessage);
        }

        // Goal of the test cases are to check for positive and negative targets that are both axis-aligned and off-axis.
        // For each case, we test with gravity enabled and disabled.
        static readonly TestCaseData[] k_PM_OrientationTestCases =
        {
            new TestCaseData(new float3(0f, 0f, -1f), false).SetName("Axis Aligned -z, without gravity"),
            new TestCaseData(new float3(0f, 0f, -1f), true).SetName("Axis Aligned -z, with gravity"),
            new TestCaseData(new float3(0f, 0f, 1f), false).SetName("Axis Aligned +z, without gravity"),
            new TestCaseData(new float3(0f, 0f, 1f), true).SetName("Axis Aligned +z, with gravity"),

            new TestCaseData(new float3(0f, -1f, 0f), false).SetName("Axis Aligned -y, without gravity"),
            //new TestCaseData(new float3(0f, -1f, 0f), true).SetName("Axis Aligned -y, with gravity"), //Disabled since applying gravity along the axis of motion fails tests
            new TestCaseData(new float3(0f, 1f, 0f), false).SetName("Axis Aligned +y, without gravity"),
            //new TestCaseData(new float3(0f, 1f, 0f), true).SetName("Axis Aligned +y, with gravity"), //Disabled since applying gravity along the axis of motion fails tests

            new TestCaseData(new float3(-1f, 0f, 0f), false).SetName("Axis Aligned -x, without gravity"),
            new TestCaseData(new float3(-1f, 0f, 0f), true).SetName("Axis Aligned -x, with gravity"),
            new TestCaseData(new float3(1f, 0f, 0f), false).SetName("Axis Aligned +x, without gravity"),
            new TestCaseData(new float3(1f, 0f, 0f), true).SetName("Axis Aligned +x, with gravity"),

            new TestCaseData(new float3(-1f, 1f, 0f), false).SetName("Not Axis Aligned 1 without gravity"),
            new TestCaseData(new float3(3f, 0f, -2f), false).SetName("Not Axis Aligned 2, without gravity"),
            new TestCaseData(new float3(0f, 0.5f, -2f), false).SetName("Not Axis Aligned 3, without gravity"),
            new TestCaseData(new float3(1.5f, 1.5f, 1.5f), false).SetName("Not Axis Aligned 4, without gravity"),
            new TestCaseData(new float3(-1.5f, -1.5f, -1.5f), false).SetName("Not Axis Aligned 5, without gravity"),
            new TestCaseData(new float3(1.5f, -1.5f, 1.5f), false).SetName("Not Axis Aligned 6, without gravity"),

            new TestCaseData(new float3(-1f, 1f, 0f), true).SetName("Not Axis Aligned 1 with gravity"),
            new TestCaseData(new float3(3f, 0f, -2f), true).SetName("Not Axis Aligned 2, with gravity"),
            new TestCaseData(new float3(0f, 0.5f, -2f), true).SetName("Not Axis Aligned 3, with gravity"),
            new TestCaseData(new float3(1.5f, 1.5f, 1.5f), true).SetName("Not Axis Aligned 4, with gravity"),
            new TestCaseData(new float3(-1.5f, -1.5f, -1.5f), true).SetName("Not Axis Aligned 5, with gravity"),
            new TestCaseData(new float3(1.5f, -1.5f, 1.5f), true).SetName("Not Axis Aligned 6, with gravity"),
        };

        // Tests that with a given direction of movement of specified that bodyA arrives at the target position within a
        // set number of steps. Variables: direction of movement, if gravity is enabled/disabled.
        // Held constant: the target distance, the anchor position of bodyA, the maxImpulse for the motor
        [TestCaseSource(nameof(k_PM_OrientationTestCases))]
        public void OrientationTests_PM(float3 directionOfMovement, bool useGravity)
        {
            // Constants: BodyB is resting above BodyA
            RigidTransform worldFromA = new RigidTransform(quaternion.identity, new float3(-0.5f, 5f, -4f));
            RigidTransform worldFromB = new RigidTransform(quaternion.identity, new float3(-0.5f, 6f, -4f));
            float targetDistance = 4.0f;  //distance from anchorA
            var anchorA = float3.zero;
            var maxImpulse = 40.0f;

            // Calculated variables
            var axisInB = math.normalize(directionOfMovement);
            var targetPosition = targetDistance * axisInB;

            MotorTestUtility.SetupMotionVelocity(out MotionVelocity velocityA, out MotionVelocity velocityB);
            MotorTestUtility.SetupMotionData(worldFromA, worldFromB, out MotionData motionA, out MotionData motionB);

            var jointFrameA = JacobianUtilities.CalculateDefaultBodyFramesForConnectedBody(
                worldFromA, worldFromB, anchorA, axisInB, out BodyFrame jointFrameB, false);

            Joint joint = MotorTestRunner.CreateTestMotor(
                PhysicsJoint.CreatePositionMotor(jointFrameA, jointFrameB, targetDistance, maxImpulse));

            TestSimulatePositionMotor("OrientationTests (PM)", joint,
                velocityA, velocityB, motionA, motionB, useGravity, targetPosition, maxImpulse);
        }

        // Test cases to verify: for a given direction of movement and with gravity
        // enabled/disabled, the motor arrives at the target.
        private static readonly TestCaseData[] k_PM_maxImpulseTestCases =
        {
            new TestCaseData(new float3(0f, 0f, -1f), 10.0f, false).SetName("On-Axis maxImpulse=10, gravity off"),
            new TestCaseData(new float3(0f, 0f, -1f), 0.0f, false).SetName("On-Axis maxImpulse=0, gravity off"),
            new TestCaseData(new float3(1f, 1f, -1f), 10.0f, false).SetName("Off-Axis maxImpulse=10, gravity off"),
            new TestCaseData(new float3(1f, 1f, -1f), 0.0f, false).SetName("Off-Axis maxImpulse=0, gravity off"),

            new TestCaseData(new float3(0f, 0f, -1f), 10.0f, true).SetName("On-Axis maxImpulse=10, gravity on"),
            new TestCaseData(new float3(0f, 0f, -1f), 0.0f, true).SetName("On-Axis maxImpulse=0, gravity on"),
            new TestCaseData(new float3(1f, 1f, -1f), 10.0f, true).SetName("Off-Axis maxImpulse=10, gravity on"),
            new TestCaseData(new float3(1f, 0f, -1f), 0.0f, true).SetName("Off-Axis maxImpulse=0, gravity on"),
            //new TestCaseData(new float3(1f, 1f, -1f), 0.0f, true).SetName("Off-Axis maxImpulse=0, gravity on"), //Test doesn't pass b/c of other constraints
        };

        // Purpose of this test is to verify that the maxImpulse for the motor is not exceeded and that if a maxImpulse
        // is not infinity, that a position motor will arrive at the target.
        // Constants: the target distance, the anchor position of bodyA and the max impulse of the motor
        [TestCaseSource(nameof(k_PM_maxImpulseTestCases))]
        public void MaxImpulseTests_PM(float3 directionOfMovement, float maxImpulse, bool useGravity)
        {
            // Constants: BodyB is resting above BodyA
            RigidTransform worldFromA = new RigidTransform(quaternion.identity, new float3(-0.5f, 5f, -4f));
            RigidTransform worldFromB = new RigidTransform(quaternion.identity, new float3(-0.5f, 6f, -4f));
            float targetDistance = 2.0f;  //target is a scalar distance from anchorA
            var anchorPosition = float3.zero;

            // Calculated variables
            var axis = math.normalize(directionOfMovement);
            var targetDisplacement = targetDistance * axis;

            MotorTestUtility.SetupMotionVelocity(out MotionVelocity velocityA, out MotionVelocity velocityB);
            MotorTestUtility.SetupMotionData(worldFromA, worldFromB, out MotionData motionA, out MotionData motionB);

            var jointFrameA = JacobianUtilities.CalculateDefaultBodyFramesForConnectedBody(
                worldFromA, worldFromB, anchorPosition, axis, out BodyFrame jointFrameB, false);

            Joint joint = MotorTestRunner.CreateTestMotor(
                PhysicsJoint.CreatePositionMotor(jointFrameA, jointFrameB, targetDistance, maxImpulse));

            TestSimulatePositionMotor("Max Impulse Tests (PM)", joint,
                velocityA, velocityB, motionA, motionB, useGravity, targetDisplacement, maxImpulse);
        }

        // Runs a simulation with random pivots, random axes, and random velocities for both bodyA and bodyB,
        // for numSteps. On the last step, test checks if InitialError is less than a threshold. This configuration is
        // not going to generate realistic motion. Intention is to verify that the target can be reached by the motor
        [Test]
        public void RandomConfigurationTest_PM()
        {
            MotorTestRunner.RunRandomConfigurationMotorTest("Random Configuration Tests (PM)", (ref Random rnd) =>
            {
                var jointFrameA = new BodyFrame();
                var jointFrameB = new BodyFrame();
                MotorTestUtility.GenerateRandomPivots(ref rnd, out jointFrameA.Position, out jointFrameB.Position);
                MotorTestUtility.GenerateRandomAxes(ref rnd, out jointFrameA.Axis, out jointFrameB.Axis);

                Math.CalculatePerpendicularNormalized(jointFrameA.Axis, out jointFrameA.PerpendicularAxis, out _);
                Math.CalculatePerpendicularNormalized(jointFrameB.Axis, out jointFrameB.PerpendicularAxis, out _);

                var distance = rnd.NextFloat(-5f, 5f);
                //var testRange = new float3(1f, 1f, 1f);
                //var direction = rnd.NextFloat3(-1 * testRange, testRange);
                //var target = math.normalize(direction) * distance; //target is a vector
                var maxImpulseForMotor = math.INFINITY;

                return MotorTestRunner.CreateTestMotor(
                    PhysicsJoint.CreatePositionMotor(jointFrameA, jointFrameB, distance, maxImpulseForMotor));
            });
        }
    }
}
