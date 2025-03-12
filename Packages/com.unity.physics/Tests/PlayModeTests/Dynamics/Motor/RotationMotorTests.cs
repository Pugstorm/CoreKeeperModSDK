using NUnit.Framework;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Unity.Physics.Tests.Motors
{
    [TestFixture]
    public class RotationMotorTests
    {
        // Called by Orientation Tests, Max Impulse Tests
        // Constants: the number of steps, solver iterations, initial motion of bodyB
        // Variable parameters: gravity enabled/disabled, if initial velocity of bodyA starts at 0 or at
        // the target velocity
        void TestSimulateRotationalMotor(string testName, Joint jointData,
            MotionVelocity velocityA, MotionVelocity velocityB, MotionData motionA, MotionData motionB,
            bool useGravity, float3 targetOrientation, float maxImpulse)
        {
            string failureMessage;

            int numIterations = 4;
            int numSteps = 15; // duration = 0.25s
            int numStabilizingSteps = 0;

            var rotation0 = motionA.WorldFromMotion.rot;
            var motorOrientation =
                math.normalizesafe(targetOrientation); //to only consider direction the motor is acting on

            MotorTestRunner.TestSimulateMotor(testName, ref jointData,
                ref velocityA, ref velocityB, ref motionA, ref motionB,
                useGravity, maxImpulse, motorOrientation, numIterations, numSteps, numStabilizingSteps,
                out float3 accumulateAngularVelocity, out float3 accumulateLinearVelocity);

            if (maxImpulse < math.EPSILON)
                targetOrientation = rotation0.ToEulerAngles(); //if maxImpulse=0, then motor shouldn't move

            // Verify that the rotation of bodyA is at expected orientation at end of simulation:
            var testThreshold = 0.2f;

            var newRotation = math.mul(motionA.WorldFromMotion.rot, rotation0).ToEulerAngles();
            var expectedRotation = math.mul(rotation0, targetOrientation);
            var targetInDeg = math.degrees(targetOrientation);
            var finalInDeg = math.degrees(newRotation);

            var change = math.abs(newRotation - expectedRotation);

            failureMessage = $"{testName}: Rotation after simulation {finalInDeg} doesn't match expected rotation {targetInDeg}";
            Assert.Less(change.x, testThreshold, failureMessage);
            Assert.Less(change.y, testThreshold, failureMessage);
            Assert.Less(change.z, testThreshold, failureMessage);
        }

        // Goal of the test cases are to check for positive and negative targets that are aligned with only the x-axis,
        // y-axis, or z-axis. Then to test for positive & negative targets that are not perfectly aligned with any axis.
        // For each case, we test with gravity enabled and disabled
        static readonly TestCaseData[] k_RM_AxisAlignedOrientationTestCases =
        {
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0f), false).SetName("Axis Aligned -z, without gravity"),
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0f), true).SetName("Axis Aligned -z, with gravity"),
            new TestCaseData(new float3(0f, 0f, 1f), new float3(0.5f, 0.5f, 0f), false).SetName("Axis Aligned +z, without gravity"),
            new TestCaseData(new float3(0f, 0f, 1f), new float3(0.5f, 0.5f, 0f), true).SetName("Axis Aligned +z, with gravity"),

            new TestCaseData(new float3(0f, -1f, 0f), new float3(0.5f, 0f, 0.5f), false).SetName("Axis Aligned -y, without gravity"),
            new TestCaseData(new float3(0f, -1f, 0f), new float3(0.5f, 0f, 0.5f), true).SetName("Axis Aligned -y, with gravity"),
            new TestCaseData(new float3(0f, 1f, 0f), new float3(0.5f, 0f, 0.5f), false).SetName("Axis Aligned +y, without gravity"),
            new TestCaseData(new float3(0f, 1f, 0f), new float3(0.5f, 0f, 0.5f), true).SetName("Axis Aligned +y, with gravity"),

            new TestCaseData(new float3(-1f, 0f, 0f), new float3(0f, 0.5f, 0.5f), false).SetName("Axis Aligned -x, without gravity"),
            new TestCaseData(new float3(-1f, 0f, 0f), new float3(0f, 0.5f, 0.5f), true).SetName("Axis Aligned -x, with gravity"),
            new TestCaseData(new float3(1f, 0f, 0f), new float3(0f, 0.5f, 0.5f), false).SetName("Axis Aligned +x, without gravity"),
            new TestCaseData(new float3(1f, 0f, 0f), new float3(0f, 0.5f, 0.5f), true).SetName("Axis Aligned +x, with gravity"),
        };

        // Tests that for a given axis of rotation, pivot position, gravity setting and initial velocity, that the angular
        // velocity of bodyA reaches the target and that the orientation of movement is correct
        // Constants: target speed, the max impulse of the motor
        [TestCaseSource(nameof(k_RM_AxisAlignedOrientationTestCases))]
        public void OrientationTests_RM(float3 axisOfRotation, float3 pivotPosition, bool useGravity)
        {
            // Constants: BodyB is resting above BodyA
            RigidTransform worldFromA = new RigidTransform(quaternion.identity, new float3(-0.5f, 5f, -4f));
            RigidTransform worldFromB = new RigidTransform(quaternion.identity, new float3(-0.5f, 6f, -4f));
            var targetRotationInDegrees = 75.0f;
            var maxImpulse = math.INFINITY;

            // Calculated variables
            var axis = math.normalize(axisOfRotation);
            var targetRotation = math.radians(targetRotationInDegrees);
            var targetRotation_vector = targetRotation * axis;

            MotorTestUtility.SetupMotionVelocity(out MotionVelocity velocityA, out MotionVelocity velocityB);
            MotorTestUtility.SetupMotionData(worldFromA, worldFromB, out MotionData motionA, out MotionData motionB);

            var jointFrameA = JacobianUtilities.CalculateDefaultBodyFramesForConnectedBody(
                worldFromA, worldFromB, pivotPosition, axis, out BodyFrame jointFrameB, true);

            Joint joint = MotorTestRunner.CreateTestMotor(
                PhysicsJoint.CreateRotationalMotor(jointFrameA, jointFrameB, targetRotation, maxImpulse));

            TestSimulateRotationalMotor("Orientation Tests (RM)", joint,
                velocityA, velocityB, motionA, motionB, useGravity, targetRotation_vector, maxImpulse);
        }

        private static readonly TestCaseData[] k_RM_maxImpulseTestCases =
        {
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0), 5.0f, false).SetName("On-Axis maxImpulse=5, gravity off"),
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0), 0.0f, false).SetName("On-Axis maxImpulse=0, gravity off"),
            new TestCaseData(new float3(1f, 1f, 1f), new float3(0.5f, 0.5f, -0.5f), 5.0f, false).SetName("Off-Axis maxImpulse=5, gravity off"),
            new TestCaseData(new float3(1f, 1f, 1f), new float3(0.5f, 0.5f, -0.5f), 0.0f, false).SetName("Off-Axis maxImpulse=0, gravity off"),

            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0), 5.0f, true).SetName("On-Axis maxImpulse=5, gravity on"),
            new TestCaseData(new float3(1f, 1f, 1f), new float3(0.5f, 0.5f, 0.5f), 5.0f, true).SetName("Off-Axis maxImpulse=5, gravity on"),
            //new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0), 0.0f, true).SetName("On-Axis maxImpulse=0, gravity on"), //Test doesn't pass b/c of other constraints
            //new TestCaseData(new float3(1f, 1f, 1f), new float3(0.5f, 0.5f, 0.5f), 0.0f, true).SetName("Off-Axis maxImpulse=0, gravity on"),//Test doesn't pass b/c of other constraints
        };

        // Purpose: to verify that
        // 1) the maxImpulse for the motor is not exceeded,
        // 2) if a maxImpulse is not infinity, that a position motor will arrive at the target
        // 3) if the maxImpulse is zero, the motor shouldn't move
        // Constants: the target rotation, initial position of bodyA and bodyB
        [TestCaseSource(nameof(k_RM_maxImpulseTestCases))]
        public void MaxImpulseTest_RM(float3 axisOfRotation, float3 pivotPosition, float maxImpulse, bool useGravity)
        {
            // Constants: BodyB is resting above BodyA
            RigidTransform worldFromA = new RigidTransform(quaternion.identity, new float3(-0.5f, 5f, -4f));
            RigidTransform worldFromB = new RigidTransform(quaternion.identity, new float3(-0.5f, 6f, -4f));
            var targetAngleInDegrees = 45.0f;  //target is an angular velocity in deg/s

            // Calculated variables
            var axis = math.normalize(axisOfRotation);
            var targetAngleInRadians = math.radians(targetAngleInDegrees);
            var targetAngleInRadians_vector = targetAngleInRadians * axis;

            MotorTestUtility.SetupMotionVelocity(out MotionVelocity velocityA, out MotionVelocity velocityB);
            MotorTestUtility.SetupMotionData(worldFromA, worldFromB, out MotionData motionA, out MotionData motionB);

            var jointFrameA = JacobianUtilities.CalculateDefaultBodyFramesForConnectedBody(
                worldFromA, worldFromB, pivotPosition, axis, out BodyFrame jointFrameB, true);

            Joint joint = MotorTestRunner.CreateTestMotor(
                PhysicsJoint.CreateRotationalMotor(jointFrameA, jointFrameB, targetAngleInRadians, maxImpulse));

            TestSimulateRotationalMotor("Max Impulse Tests (RM)", joint,
                velocityA, velocityB, motionA, motionB, useGravity, targetAngleInRadians_vector, maxImpulse);
        }

        // Runs a simulation with random pivots, random axes, and random velocities for both bodyA and bodyB,
        // for numSteps. On the last step, test checks if InitialError is less than a threshold. This configuration is
        // not going to generate realistic motion. Intention is to verify that the target can be reached by the motor
        [Test]
        public void RandomConfigurationTest_RM()
        {
            MotorTestRunner.RunRandomConfigurationMotorTest("Random Configuration Tests (RM)", (ref Random rnd) =>
            {
                var jointFrameA = new BodyFrame();
                var jointFrameB = new BodyFrame();
                MotorTestUtility.GenerateRandomPivots(ref rnd, out jointFrameA.Position, out jointFrameB.Position);
                MotorTestUtility.GenerateRandomAxes(ref rnd, out jointFrameA.Axis, out jointFrameB.Axis);

                Math.CalculatePerpendicularNormalized(jointFrameA.Axis, out jointFrameA.PerpendicularAxis, out _);
                Math.CalculatePerpendicularNormalized(jointFrameB.Axis, out jointFrameB.PerpendicularAxis, out _);

                var maxImpulseForMotor = math.INFINITY;

                return MotorTestRunner.CreateTestMotor(
                    PhysicsJoint.CreateRotationalMotor(jointFrameA, jointFrameB,
                        math.radians(rnd.NextFloat(-180f, 180f)), maxImpulseForMotor));
            });
        }
    }
}
