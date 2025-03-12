using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Unity.Physics.Tests.Motors
{
    [TestFixture]
    public class AngularVelocityMotorTests
    {
        // Called by Constant Velocity Tests. These tests require a larger test threshold. A smaller threshold is possible if
        // more frequency and numSteps are increased, don't want tests to take too long.
        // This test checks:
        // 1) Verifies bodyA target velocity has been achieved within the given time
        // 2) Verifies that the orientation of bodyA after one rotation matches the initial orientation
        // 3) Verifies that the position of bodyA after one rotation matches the initial position
        // 4) Verifies that the maxImpulse is never exceeded
        // 5) Verifies that the variance in the average angular velocity is within a threshold
        void TestSimulateAngularVelocityMotor_ConstantSpeed(string testName, Joint jointData,
            MotionVelocity velocityA, MotionVelocity velocityB, MotionData motionA, MotionData motionB,
            bool useGravity, float3 targetVelocity, float maxImpulse)
        {
            string failureMessage;

            int numIterations = 4;
            int numSteps = 61; //duration 1.0s to get full rotation
            int numStabilizingSteps = 0; //needs to be zero to get initial position and rotation

            var motorOrientation = math.normalizesafe(targetVelocity); //to only consider direction the motor is acting on

            var position0 = motionA.WorldFromMotion.pos;
            var rotation0 = motionA.WorldFromMotion.rot;

            MotorTestRunner.TestSimulateMotor(testName, ref jointData,
                ref velocityA, ref velocityB, ref motionA, ref motionB,
                useGravity, maxImpulse, motorOrientation, numIterations, numSteps, numStabilizingSteps,
                out float3 accumulateAngularVelocity, out float3 accumulateLinearVelocity);

            // Testing thresholds:
            float thresholdFinalAngularVelocity = 0.05f;      // Angular velocity after simulation, in radians
            if (useGravity) thresholdFinalAngularVelocity = 0.12f; // if gravity enabled, some orientations are less precise
            const float thresholdPositionAfterFullRotation = 0.01f;   // Position threshold after one full rotation
            const float thresholdRotationAfterFullRotation = 0.0001f; // Rotation threshold after one full rotation
            const float thresholdConstantAngularVelocity = 0.1f;      // Averaged angular velocity over numSteps, in rad/s
            const float thresholdConstantLinearVelocity = 0.01f;      // Averaged linear velocity over numSteps, in m/s

            // Angular speed after simulation should be within testThreshold of the target velocity:
            var compareToTarget = math.abs(velocityA.AngularVelocity - targetVelocity);
            failureMessage = $"{testName}: Final angular velocity failed test with angular velocity {velocityA.AngularVelocity}. Target: {targetVelocity}";
            Assert.Less(compareToTarget.x, thresholdFinalAngularVelocity, failureMessage);
            Assert.Less(compareToTarget.y, thresholdFinalAngularVelocity, failureMessage);
            Assert.Less(compareToTarget.z, thresholdFinalAngularVelocity, failureMessage);

            // After one full rotation, the position should match the initial position:
            var positionChange = math.abs(motionA.WorldFromMotion.pos - position0);
            failureMessage = $"{testName}: Position after one rotation {motionA.WorldFromMotion.pos} doesn't match initial position: {position0}";
            Assert.Less(positionChange.x, thresholdPositionAfterFullRotation, failureMessage);
            Assert.Less(positionChange.y, thresholdPositionAfterFullRotation, failureMessage);
            Assert.Less(positionChange.z, thresholdPositionAfterFullRotation, failureMessage);

            // After one full rotation, the rotation should match the initial rotation:
            var orientationDifference = math.mul(motionA.WorldFromMotion.rot, rotation0).ToEulerAngles();
            failureMessage = $"{testName}: Rotation after one rotation {motionA.WorldFromMotion.rot.ToEulerAngles()} doesn't match initial rotation: {rotation0.ToEulerAngles()}";
            Assert.Less(orientationDifference.x, thresholdRotationAfterFullRotation, failureMessage);
            Assert.Less(orientationDifference.y, thresholdRotationAfterFullRotation, failureMessage);
            Assert.Less(orientationDifference.z, thresholdRotationAfterFullRotation, failureMessage);

            // [Units rad/s] The motor should maintain a constant velocity over many iterations:
            var meanAngularVelocity = accumulateAngularVelocity / numSteps;
            compareToTarget = math.abs(meanAngularVelocity - targetVelocity);
            failureMessage = $"{testName}: Averaged angular velocity failed test with mean {meanAngularVelocity}. Target: {targetVelocity}";
            Assert.Less(compareToTarget.x, thresholdConstantAngularVelocity, failureMessage);
            Assert.Less(compareToTarget.y, thresholdConstantAngularVelocity, failureMessage);
            Assert.Less(compareToTarget.z, thresholdConstantAngularVelocity, failureMessage);

            // After one full rotation, averaged linear velocity should be zero
            var meanLinearVelocity = accumulateLinearVelocity / numSteps;
            compareToTarget = math.abs(meanLinearVelocity);
            failureMessage = $"{testName}: Averaged linear velocity failed test with mean {meanLinearVelocity}. Target: 0";
            Assert.Less(compareToTarget.x, thresholdConstantLinearVelocity, failureMessage);
            Assert.Less(compareToTarget.y, thresholdConstantLinearVelocity, failureMessage);
            Assert.Less(compareToTarget.z, thresholdConstantLinearVelocity, failureMessage);
        }

        // For a rotational axis, we chose pivot points that lie along each 4 (x,y) corners of a cube that rotates about
        // the z-axis and 1 at the center. All simulations start with the initial velocity at target velocity
        private static readonly TestCaseData[] k_AVM_ConstantVelocityTestCases =
        {
            // Arguments: axis of rotation, pivotPosition, using gravity
            new TestCaseData(new float3(0f, 0f, -1f), new float3(-0.5f, 0.5f, 0), false).SetName("Axis Aligned -z, -x/+y pivot, gravity off"), // v = -358.163, off by 1.836
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0), false).SetName("Axis Aligned -z, +x/+y pivot, gravity off"),  // v = -358.163, off by 1.836
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0f, 0f, 0),  false).SetName("Axis Aligned -z, 0/0 pivot, gravity off"),
            new TestCaseData(new float3(0f, 0f, -1f), new float3(-0.5f, -0.5f, 0), false).SetName("Axis Aligned -z, -x/-y pivot, gravity off"), // v = -358.163, off by 1.836
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, -0.5f, 0), false).SetName("Axis Aligned -z, +x/-y pivot, gravity off"),  // v = -358.164, off by 1.835

            new TestCaseData(new float3(0f, 0f, -1f), new float3(-0.5f, 0.5f, 0), true).SetName("Axis Aligned -z, -x/+y pivot, gravity on"), // v = -362.664, off by 2.665
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0), true).SetName("Axis Aligned -z, +x/+y pivot, gravity on"),  // v = -353.918, off by 6.082
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0f, 0f, 0), true).SetName("Axis Aligned -z, 0/0 pivot, gravity on"),
            new TestCaseData(new float3(0f, 0f, -1f), new float3(-0.5f, -0.5f, 0), true).SetName("Axis Aligned -z, -x/-y pivot, gravity on"), // v= = -362.394, off by 2.394
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, -0.5f, 0), true).SetName("Axis Aligned -z, +x/-y pivot, gravity on"),  // v = -353.670, off by 6.329
        };

        // Purpose of this test is for bodyA to make a full rotation in the expected amount of time and arrive back at its
        // starting point after one full rotation.
        [TestCaseSource(nameof(k_AVM_ConstantVelocityTestCases))]
        public void ConstantVelocityTest_AVM(float3 axisOfRotation, float3 pivotPosition, bool useGravity)
        {
            // Constants: BodyB is resting above BodyA
            RigidTransform worldFromA = new RigidTransform(quaternion.identity, new float3(-0.5f, 5f, -4f));
            RigidTransform worldFromB = new RigidTransform(quaternion.identity, new float3(-0.5f, 6f, -4f));
            float targetSpeed = 360.0f;
            float maxImpulseForMotor = math.INFINITY;

            // Calculated variables
            var axis = math.normalize(axisOfRotation);
            var targetSpeedInRadians = math.radians(targetSpeed);
            var targetVelocityInRadians = targetSpeedInRadians * axis;

            MotorTestUtility.SetupMotionVelocity(out MotionVelocity velocityA, out MotionVelocity velocityB);
            velocityA.AngularVelocity = targetVelocityInRadians; //initialize at target velocity
            MotorTestUtility.SetupMotionData(worldFromA, worldFromB, out MotionData motionA, out MotionData motionB);

            var jointFrameA = JacobianUtilities.CalculateDefaultBodyFramesForConnectedBody(
                worldFromA, worldFromB, pivotPosition, axis, out BodyFrame jointFrameB, true);

            Joint joint = MotorTestRunner.CreateTestMotor(
                PhysicsJoint.CreateAngularVelocityMotor(jointFrameA, jointFrameB, targetSpeedInRadians, maxImpulseForMotor));

            TestSimulateAngularVelocityMotor_ConstantSpeed("Constant Velocity Tests (AVM)", joint,
                velocityA, velocityB, motionA, motionB, useGravity, targetVelocityInRadians, maxImpulseForMotor);
        }

        // Called by Orientation Tests, Max Impulse Tests
        // Constants: the number of steps, solver iterations, initial motion of bodyB
        // Variable parameters: gravity enabled/disabled, if initial velocity of bodyA starts at 0 or at
        // the target velocity
        // Since it can take some number of steps for the target to be reached, we do not accumulate the velocity
        // of bodyA for numStabilizingSteps, so that the average velocity is not skewed
        // checks: if the constant velocity is maintained after several steps, checks if the target
        // velocity is reached
        void TestSimulateAngularVelocityMotor(string testName, Joint jointData,
            MotionVelocity velocityA, MotionVelocity velocityB, MotionData motionA, MotionData motionB,
            bool useGravity, float3 targetVelocity, float maxImpulse)
        {
            string failureMessage;

            int numIterations = 4;
            int numSteps = 15; // duration = 0.25s
            int numStabilizingSteps = 5; //takes some iterations to reach the target velocity

            var rotation0 = motionA.WorldFromMotion.rot;
            var motorOrientation = math.normalizesafe(targetVelocity); //to only consider direction the motor is acting on

            MotorTestRunner.TestSimulateMotor(testName, ref jointData,
                ref velocityA, ref velocityB, ref motionA, ref motionB,
                useGravity, maxImpulse, motorOrientation, numIterations, numSteps, numStabilizingSteps,
                out float3 accumulateAngularVelocity, out float3 accumulateLinearVelocity);

            // Testing thresholds:
            float thresholdFinalAngularVelocity = 0.05f;  // Angular velocity after simulation, in radians
            if (useGravity) thresholdFinalAngularVelocity = 0.12f; // if gravity enabled, some orientations are less precise
            const float thresholdRotationAfterRotation = 0.1f;    // Rotation threshold after rotation
            const float thresholdConstantAngularVelocity = 0.1f;  // Averaged angular velocity over numSteps, in rad/s

            // Angular speed after simulation should be within testThreshold of the target velocity:
            if (maxImpulse < math.EPSILON) targetVelocity = float3.zero; //if maxImpulse=0, then motor shouldn't move
            var compareToTarget = math.abs(velocityA.AngularVelocity - targetVelocity);
            failureMessage = $"{testName}: Final angular velocity failed test with angular velocity {velocityA.AngularVelocity}. Target: {targetVelocity}";
            Assert.Less(compareToTarget.x, thresholdFinalAngularVelocity, failureMessage);
            Assert.Less(compareToTarget.y, thresholdFinalAngularVelocity, failureMessage);
            Assert.Less(compareToTarget.z, thresholdFinalAngularVelocity, failureMessage);

            // The motor should maintain a constant velocity over many iterations, in rad/s:
            var meanAngularVelocity = accumulateAngularVelocity / numSteps;
            compareToTarget = math.abs(meanAngularVelocity - targetVelocity);
            failureMessage = $"{testName}: Averaged angular velocity failed test with mean {meanAngularVelocity}. Target: {targetVelocity}";
            Assert.Less(compareToTarget.x, thresholdConstantAngularVelocity, failureMessage);
            Assert.Less(compareToTarget.y, thresholdConstantAngularVelocity, failureMessage);
            Assert.Less(compareToTarget.z, thresholdConstantAngularVelocity, failureMessage);

            // Verify that the rotation of bodyA arrived at expected rotation after simulation:
            var targetAngle = targetVelocity * MotorTestRunner.Timestep * numSteps;
            var simulatedAngle = math.mul(motionA.WorldFromMotion.rot, rotation0).ToEulerAngles();
            compareToTarget = math.abs(simulatedAngle - targetAngle);
            failureMessage = $"{testName}: Rotation after simulation {motionA.WorldFromMotion.rot.ToEulerAngles()} doesn't match expected rotation: {targetAngle}";
            Assert.Less(compareToTarget.x, thresholdRotationAfterRotation, failureMessage);
            Assert.Less(compareToTarget.y, thresholdRotationAfterRotation, failureMessage);
            Assert.Less(compareToTarget.z, thresholdRotationAfterRotation, failureMessage);
        }

        // Goal of the test cases are to check for positive and negative targets that are aligned with only the x-axis,
        // y-axis, or z-axis. Then to test for positive & negative targets that are not perfectly aligned with any axis.
        // For each case, we test with gravity enabled and disabled and with the initial velocity of BodyA either 0 or at the target
        static readonly TestCaseData[] k_AVM_AxisAlignedOrientationTestCases =
        {
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0f), false, false).SetName("Axis Aligned -z, without gravity, v0=0"),
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0f), true, false).SetName("Axis Aligned -z, with gravity, v0=0"),
            new TestCaseData(new float3(0f, 0f, 1f), new float3(0.5f, 0.5f, 0f), false, false).SetName("Axis Aligned +z, without gravity, v0=0"),
            new TestCaseData(new float3(0f, 0f, 1f), new float3(0.5f, 0.5f, 0f), true, false).SetName("Axis Aligned +z, with gravity, v0=0"),

            new TestCaseData(new float3(0f, -1f, 0f), new float3(0.5f, 0f, 0.5f), false, false).SetName("Axis Aligned -y, without gravity, v0=0"),
            new TestCaseData(new float3(0f, -1f, 0f), new float3(0.5f, 0f, 0.5f), true, false).SetName("Axis Aligned -y, with gravity, v0=0"),
            new TestCaseData(new float3(0f, 1f, 0f), new float3(0.5f, 0f, 0.5f), false, false).SetName("Axis Aligned +y, without gravity, v0=0"),
            new TestCaseData(new float3(0f, 1f, 0f), new float3(0.5f, 0f, 0.5f), true, false).SetName("Axis Aligned +y, with gravity, v0=0"),

            new TestCaseData(new float3(-1f, 0f, 0f), new float3(0f, 0.5f, 0.5f), false, false).SetName("Axis Aligned -x, without gravity, v0=0"),
            new TestCaseData(new float3(-1f, 0f, 0f), new float3(0f, 0.5f, 0.5f), true, false).SetName("Axis Aligned -x, with gravity, v0=0"),
            new TestCaseData(new float3(1f, 0f, 0f), new float3(0f, 0.5f, 0.5f), false, false).SetName("Axis Aligned +x, without gravity, v0=0"),
            new TestCaseData(new float3(1f, 0f, 0f), new float3(0f, 0.5f, 0.5f), true, false).SetName("Axis Aligned +x, with gravity, v0=0"),

            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0f), false, true).SetName("Axis Aligned -z, without gravity, v0=target"),
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0f), true, true).SetName("Axis Aligned -z, with gravity, v0=target"),
            new TestCaseData(new float3(0f, 0f, 1f), new float3(0.5f, 0.5f, 0f), false, true).SetName("Axis Aligned +z, without gravity, v0=target"),
            new TestCaseData(new float3(0f, 0f, 1f), new float3(0.5f, 0.5f, 0f), true, true).SetName("Axis Aligned +z, with gravity, v0=target"),

            new TestCaseData(new float3(0f, -1f, 0f), new float3(0.5f, 0f, 0.5f), false, true).SetName("Axis Aligned -y, without gravity, v0=target"),
            new TestCaseData(new float3(0f, -1f, 0f), new float3(0.5f, 0f, 0.5f), true, true).SetName("Axis Aligned -y, with gravity, v0=target"),
            new TestCaseData(new float3(0f, 1f, 0f), new float3(0.5f, 0f, 0.5f), false, true).SetName("Axis Aligned +y, without gravity, v0=target"),
            new TestCaseData(new float3(0f, 1f, 0f), new float3(0.5f, 0f, 0.5f), true, true).SetName("Axis Aligned +y, with gravity, v0=target"),

            new TestCaseData(new float3(-1f, 0f, 0f), new float3(0f, 0.5f, 0.5f), false, true).SetName("Axis Aligned -x, without gravity, v0=target"),
            new TestCaseData(new float3(-1f, 0f, 0f), new float3(0f, 0.5f, 0.5f), true, true).SetName("Axis Aligned -x, with gravity, v0=target"),
            new TestCaseData(new float3(1f, 0f, 0f), new float3(0f, 0.5f, 0.5f), false, true).SetName("Axis Aligned +x, without gravity, v0=target"),
            new TestCaseData(new float3(1f, 0f, 0f), new float3(0f, 0.5f, 0.5f), true, true).SetName("Axis Aligned +x, with gravity, v0=target")
        };

        // Tests that for a given axis of rotation, pivot position, gravity setting and initial velocity, that the angular
        // velocity of bodyA reaches the target and that the orientation of movement is correct
        // Constants: target speed, the max impulse of the motor
        [TestCaseSource(nameof(k_AVM_AxisAlignedOrientationTestCases))]
        public void OrientationTests_AVM(float3 axisOfRotation, float3 pivotPosition, bool useGravity, bool initAtTargetVelocity)
        {
            // Input variables
            var targetSpeed = 45.0f;  //target is an angular velocity in deg/s
            var maxImpulse = math.INFINITY;

            // Calculated variables
            var axis = math.normalize(axisOfRotation);
            var targetSpeedInRadians = math.radians(targetSpeed);
            var targetVelocityInRadians = targetSpeedInRadians * axis;

            // BodyB is resting above BodyA
            RigidTransform worldFromA = new RigidTransform(quaternion.identity, new float3(-0.5f, 5f, -4f));
            RigidTransform worldFromB = new RigidTransform(quaternion.identity, new float3(-0.5f, 6f, -4f));

            MotorTestUtility.SetupMotionVelocity(out MotionVelocity velocityA, out MotionVelocity velocityB);
            if (initAtTargetVelocity) velocityA.AngularVelocity = targetVelocityInRadians;
            MotorTestUtility.SetupMotionData(worldFromA, worldFromB, out MotionData motionA, out MotionData motionB);

            var jointFrameA = JacobianUtilities.CalculateDefaultBodyFramesForConnectedBody(
                worldFromA, worldFromB, pivotPosition, axis, out BodyFrame jointFrameB, true);

            Joint joint = MotorTestRunner.CreateTestMotor(
                PhysicsJoint.CreateAngularVelocityMotor(jointFrameA, jointFrameB, targetSpeedInRadians, maxImpulse));

            TestSimulateAngularVelocityMotor("Orientation Tests (AVM)", joint,
                velocityA, velocityB, motionA, motionB, useGravity, targetVelocityInRadians, maxImpulse);
        }

        private static readonly TestCaseData[] k_AVM_maxImpulseTestCases =
        {
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0), 1.0f, false).SetName("On-Axis maxImpulse=1, gravity off"),
            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0), 0.0f, false).SetName("On-Axis maxImpulse=0, gravity off"),
            new TestCaseData(new float3(1f, 1f, 1f), new float3(0.5f, 0.5f, 0.5f), 1.0f, false).SetName("Off-Axis maxImpulse=1, gravity off"),
            new TestCaseData(new float3(1f, 1f, 1f), new float3(0.5f, 0.5f, 0.5f), 0.0f, false).SetName("Off-Axis maxImpulse=0, gravity off"),

            new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0), 1.0f, true).SetName("On-Axis maxImpulse=1, gravity on"),
            //new TestCaseData(new float3(0f, 0f, -1f), new float3(0.5f, 0.5f, 0), 0.0f, true).SetName("On-Axis maxImpulse=0, gravity on"), //Test doesn't pass b/c of other constraints
            new TestCaseData(new float3(1f, 1f, 1f), new float3(0.5f, 0.5f, 0.5f), 1.0f, true).SetName("Off-Axis maxImpulse=1, gravity on"),
            //new TestCaseData(new float3(1f, 1f, 1f), new float3(0.5f, 0.5f, 0.5f), 0.0f, true).SetName("Off-Axis maxImpulse=0, gravity on"),//Test doesn't pass b/c of other constraints
        };

        [TestCaseSource(nameof(k_AVM_maxImpulseTestCases))]
        public void MaxImpulseTest_AVM(float3 axisOfRotation, float3 pivotPosition, float maxImpulse, bool useGravity)
        {
            // Input variables
            var targetSpeed = 45.0f;  //target is an angular velocity in deg/s

            // Calculated variables
            var axis = math.normalize(axisOfRotation);
            var targetSpeedInRadians = math.radians(targetSpeed);
            var targetVelocityInRadians = targetSpeedInRadians * axis;

            // BodyB is resting above BodyA
            RigidTransform worldFromA = new RigidTransform(quaternion.identity, new float3(-0.5f, 5f, -4f));
            RigidTransform worldFromB = new RigidTransform(quaternion.identity, new float3(-0.5f, 6f, -4f));

            MotorTestUtility.SetupMotionVelocity(out MotionVelocity velocityA, out MotionVelocity velocityB);
            MotorTestUtility.SetupMotionData(worldFromA, worldFromB, out MotionData motionA, out MotionData motionB);

            var jointFrameA = JacobianUtilities.CalculateDefaultBodyFramesForConnectedBody(
                worldFromA, worldFromB, pivotPosition, axis, out BodyFrame jointFrameB, true);

            Joint joint = MotorTestRunner.CreateTestMotor(
                PhysicsJoint.CreateAngularVelocityMotor(jointFrameA, jointFrameB, targetSpeedInRadians, maxImpulse));

            TestSimulateAngularVelocityMotor("Max Impulse Tests (AVM)", joint,
                velocityA, velocityB, motionA, motionB, useGravity, targetVelocityInRadians, maxImpulse);
        }
    }
}
