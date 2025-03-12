using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

//
// Tools to create and run a tiny simulation for a single body pair and joint, used by all of the tests
//
namespace Unity.Physics.Tests.Motors
{
    // Test runner tools and utilities
    public class MotorTestUtility
    {
        internal const float TestFrequency = 60.0f;
        internal const float TestTimestep = 1.0f / TestFrequency;
        internal const int numTestIterations = 4;

        internal static void ApplyGravity(ref MotionVelocity velocity, float3 gravity, float timestep)
        {
            if (velocity.InverseMass > 0.0f)
            {
                velocity.LinearVelocity += gravity * timestep;
            }
        }

        internal static void Integrate(ref MotionVelocity velocity, ref MotionData motion, float timestep)
        {
            Integrator.Integrate(ref motion.WorldFromMotion, velocity, timestep);
        }

        //
        // Random test data generation
        //
        internal static float3 GenerateRandomCardinalAxis(ref Random rnd)
        {
            float3 axis = float3.zero;
            axis[rnd.NextInt(3)] = rnd.NextBool() ? 1 : -1;
            return axis;
        }

        internal static void GenerateRandomPivots(ref Random rnd, out float3 pivotA, out float3 pivotB)
        {
            pivotA = rnd.NextBool() ? float3.zero : rnd.NextFloat3(-1.0f, 1.0f);
            pivotB = rnd.NextBool() ? float3.zero : rnd.NextFloat3(-1.0f, 1.0f);
        }

        internal static void GenerateRandomAxes(ref Random rnd, out float3 axisA, out float3 axisB)
        {
            axisA = rnd.NextInt(4) == 0 ? GenerateRandomCardinalAxis(ref rnd) : rnd.NextFloat3Direction();
            axisB = rnd.NextInt(4) == 0 ? GenerateRandomCardinalAxis(ref rnd) : rnd.NextFloat3Direction();
        }

        internal static RigidTransform GenerateRandomTransform(ref Random rnd)
        {
            // Random rotation: 1 in 4 are identity, 3 in 16 are 90 or 180 degrees about i j or k, the rest are uniform random
            quaternion rot = quaternion.identity;
            if (rnd.NextInt(4) > 0)
            {
                if (rnd.NextInt(4) > 0)
                {
                    rot = rnd.NextQuaternionRotation();
                }
                else
                {
                    float angle = rnd.NextBool() ? 90 : 180;
                    rot = quaternion.AxisAngle(GenerateRandomCardinalAxis(ref rnd), angle);
                }
            }

            return new RigidTransform()
            {
                pos = rnd.NextInt(4) == 0 ? float3.zero : rnd.NextFloat3(-1.0f, 1.0f),
                rot = rot
            };
        }

        internal static void GenerateRandomMotion(ref Random rnd, out MotionVelocity velocity, out MotionData motion, bool allowInfiniteMass)
        {
            motion = new MotionData
            {
                WorldFromMotion = GenerateRandomTransform(ref rnd),
                BodyFromMotion = GenerateRandomTransform(ref rnd)
            };

            float3 inertia = rnd.NextFloat3(1e-3f, 100.0f);
            switch (rnd.NextInt(3))
            {
                case 0: // all values random
                    break;
                case 1: // two values the same
                    int index = rnd.NextInt(3);
                    inertia[(index + 1) % 2] = inertia[index];
                    break;
                case 2: // all values the same
                    inertia = inertia.zzz;
                    break;
            }

            float3 nextLinVel;
            if (rnd.NextBool())
            {
                nextLinVel = float3.zero;
            }
            else
            {
                nextLinVel = rnd.NextFloat3(-50.0f, 50.0f);
            }

            float3 nextAngVel;
            if (rnd.NextBool())
            {
                nextAngVel = float3.zero;
            }
            else
            {
                nextAngVel = rnd.NextFloat3(-50.0f, 50.0f);
            }

            float3 nextInertia;
            float nextMass;
            if (allowInfiniteMass && rnd.NextBool())
            {
                nextInertia = float3.zero;
                nextMass = 0.0f;
            }
            else
            {
                nextMass = rnd.NextFloat(1e-3f, 100.0f);
                nextInertia = 1.0f / inertia;
            }

            velocity = new MotionVelocity
            {
                LinearVelocity = nextLinVel,
                AngularVelocity = nextAngVel,
                InverseInertia = nextInertia,
                InverseMass = nextMass
            };
        }

        internal static void SetupMotionVelocity(out MotionVelocity velocityA, out MotionVelocity velocityB)
        {
            velocityA = new MotionVelocity
            {
                LinearVelocity = float3.zero,
                AngularVelocity = float3.zero,
                InverseInertia = new float3(6f, 6f, 6f),
                InverseMass = 1.0f,
                AngularExpansionFactor = 0.692820311f
            };

            velocityB = new MotionVelocity();
        }

        internal static void SetupMotionData(RigidTransform worldFromA, RigidTransform worldFromB,
            out MotionData motionA, out MotionData motionB)
        {
            motionA = new MotionData
            {
                WorldFromMotion = worldFromA,
                BodyFromMotion = RigidTransform.identity,
                LinearDamping = 0.0099f,
                AngularDamping = 0.05f
            };

            motionB = new MotionData //use default other than this position
            {
                WorldFromMotion = worldFromB
            };
        }
    }

    //
    // Test runner methods
    //
    public class MotorTestRunner
    {
        internal const float Frequency = 60.0f;
        internal const float Timestep = 1.0f / Frequency;

        internal delegate Joint GenerateMotor(ref Random rnd);

        // Solves a joint, using a set number of iterations, and integrates the motions
        // Input timestep needs to be an inverse timestep
        internal unsafe static void SolveSingleJoint(Joint jointData, int numIterations, float timestep,
            ref MotionVelocity velocityA, ref MotionVelocity velocityB, ref MotionData motionA, ref MotionData motionB,
            out NativeStream jacobiansOut)
        {
            var stepInput = new Solver.StepInput
            {
                IsLastIteration = false,
                InvNumSolverIterations = 1.0f / numIterations,
                Timestep = timestep,
                InvTimestep = timestep > 0.0f ? 1.0f / timestep : 0.0f //calculate the frequency
            };

            // Build jacobians
            jacobiansOut = new NativeStream(1, Allocator.Temp);
            {
                NativeStream.Writer jacobianWriter = jacobiansOut.AsWriter();
                jacobianWriter.BeginForEachIndex(0);
                Solver.BuildJointJacobian(jointData, velocityA, velocityB, motionA, motionB, stepInput.Timestep,
                    numIterations, ref jacobianWriter);
                jacobianWriter.EndForEachIndex();
            }

            var eventWriter = new NativeStream.Writer(); // no events expected

            // Solve the joint, using numIterations
            for (int iIteration = 0; iIteration < numIterations; iIteration++)
            {
                stepInput.IsLastIteration = (iIteration == numIterations - 1);
                NativeStream.Reader jacobianReader = jacobiansOut.AsReader();
                var jacIterator = new JacobianIterator(jacobianReader, 0);
                while (jacIterator.HasJacobiansLeft())
                {
                    ref JacobianHeader header = ref jacIterator.ReadJacobianHeader();
                    header.Solve(ref velocityA, ref velocityB, stepInput,
                        ref eventWriter, ref eventWriter,
                        ref eventWriter, false,
                        Solver.MotionStabilizationInput.Default,
                        Solver.MotionStabilizationInput.Default);
                }
            }

            // After solving, integrate motions
            MotorTestUtility.Integrate(ref velocityA, ref motionA, stepInput.Timestep);
            MotorTestUtility.Integrate(ref velocityB, ref motionB, stepInput.Timestep);
        }

        // Configures a new Joint based on input constraints between two Bodies
        internal static Joint CreateTestMotor(PhysicsJoint joint) => new Joint
        {
            AFromJoint = joint.BodyAFromJoint.AsMTransform(),
            BFromJoint = joint.BodyBFromJoint.AsMTransform(),
            Constraints = joint.m_Constraints
        };

        // For the given test configuration and motor types, simulate the motor
        // Returns: accumulated angular velocity, accumulated linear velocity, velocityA, velocityB, motionA, motionB
        // Verifies 2 values: the impulse applied to the linear velocity, the impulse applied to the angular velocity
        internal static void TestSimulateMotor(string testName, ref Joint jointData,
            ref MotionVelocity velocityA, ref MotionVelocity velocityB, ref MotionData motionA, ref MotionData motionB,
            bool useGravity, float maxImpulse, float3 motorOrientation,
            in int numIterations, in int numSteps, in int numStabilizingSteps,
            out float3 accumulateAngularVelocity, out float3 accumulateLinearVelocity)
        {
            string failureMessage;
            var impulseThreshold = math.EPSILON;

            float3 gravity = float3.zero;
            if (useGravity) gravity = new float3(0.0f, -9.81f, 0.0f);

            accumulateAngularVelocity = float3.zero;
            accumulateLinearVelocity = float3.zero;

            var rotation0 = motionA.WorldFromMotion.rot;

            // Simulate
            for (int iStep = 0; iStep < numSteps + numStabilizingSteps; iStep++)
            {
                if (iStep == numStabilizingSteps - 1)
                    rotation0 = motionA.WorldFromMotion.rot; //save rotation once motion stabilizes a bit

                var v0Ang = velocityA.AngularVelocity;
                var v0Lin = velocityA.LinearVelocity * motorOrientation;

                // Before solving, apply gravity
                MotorTestUtility.ApplyGravity(ref velocityA, gravity, Timestep);
                MotorTestUtility.ApplyGravity(ref velocityB, gravity, Timestep);

                //NOTE: Position motor tests for y-aligned motors require v0Lin be measured here to pass if not using motorOrientation

                // Solve and integrate
                SolveSingleJoint(jointData, numIterations, Timestep,
                    ref velocityA, ref velocityB, ref motionA, ref motionB, out NativeStream jacobians);

                // Verify that the angular maxImpulse for the motor is never exceeded
                var v1Ang = velocityA.AngularVelocity;
                var motorImpulseAng = math.length((v1Ang - v0Ang) / velocityA.InverseInertia);
                var impulseMarginAng = math.abs(motorImpulseAng) - maxImpulse;
                failureMessage = $"{testName}: Angular Motor impulse {motorImpulseAng} exceeded maximum ({maxImpulse})";
                Assert.LessOrEqual(impulseMarginAng, impulseThreshold, failureMessage);

                // Verify that the linear maxImpulse for the motor is never exceeded, but only consider directions that the motor is acting on
                var v1Lin = velocityA.LinearVelocity * motorOrientation;
                var motorImpulseLin = math.length((v1Lin - v0Lin) / velocityA.InverseMass);
                var impulseMarginLin = math.abs(motorImpulseLin) - maxImpulse;
                failureMessage = $"{testName}: Linear Motor impulse {motorImpulseLin} exceeded maximum ({maxImpulse})";
                Assert.LessOrEqual(impulseMarginLin, impulseThreshold, failureMessage);

                // Only start to accumulate after a stabilizing velocity has been achieved
                if (iStep > numStabilizingSteps - 1)
                {
                    accumulateAngularVelocity += velocityA.AngularVelocity;
                    accumulateLinearVelocity += velocityA.LinearVelocity;
                }

                // Cleanup
                jacobians.Dispose();
            }
        }

        // Runs a random simulation. For an input motor, this test sets both bodies to have random motions,
        // then runs the simulation for numSteps. On the last step, checks if InitialError for the Jacobian has an
        // InitialError less than a specified threshold
        internal static unsafe void RunRandomConfigurationMotorTest(string testName, GenerateMotor generateMotor)
        {
            uint numTests = 1000;
            uint dbgTest = 2472156941;
            if (dbgTest > 0)
            {
                numTests = 1;
            }

            Random rnd = new Random(58297436);
            for (int iTest = 0; iTest < numTests; iTest++)
            {
                if (dbgTest > 0)
                {
                    rnd.state = dbgTest;
                }

                // Generate a random ball and socket joint
                Joint jointData = generateMotor(ref rnd);

                // Generate random motions
                MotionVelocity velocityA, velocityB;
                MotionData motionA, motionB;
                MotorTestUtility.GenerateRandomMotion(ref rnd, out velocityA, out motionA, true);
                MotorTestUtility.GenerateRandomMotion(ref rnd, out velocityB, out motionB, !velocityA.IsKinematic);

                // Simulate the joint
                {
                    // Build input
                    const float frequency = 50.0f;
                    float timestep = 1 / frequency;
                    const int numIterations = 4;
                    const int numSteps = 15;
                    float3 gravity = new float3(0.0f, -9.81f, 0.0f);

                    // Simulate
                    for (int iStep = 0; iStep < numSteps; iStep++)
                    {
                        // Before solving, apply gravity
                        MotorTestUtility.ApplyGravity(ref velocityA, gravity, timestep);
                        MotorTestUtility.ApplyGravity(ref velocityB, gravity, timestep);

                        // Solve and integrate
                        SolveSingleJoint(jointData, numIterations, timestep,
                            ref velocityA, ref velocityB, ref motionA, ref motionB, out NativeStream jacobians);

                        // Last step, check the joint error
                        if (iStep == numSteps - 1)
                        {
                            NativeStream.Reader jacobianReader = jacobians.AsReader();
                            var jacIterator = new JacobianIterator(jacobianReader, 0);
                            string failureMessage = $"{testName} failed {iTest}";
                            while (jacIterator.HasJacobiansLeft())
                            {
                                ref JacobianHeader header = ref jacIterator.ReadJacobianHeader();
                                float3 target;
                                float3 pivotA;
                                switch (header.Type)
                                {
                                    case JacobianType.LinearLimit:
                                        Assert.Less(header.AccessBaseJacobian<LinearLimitJacobian>().InitialError, 1e-3f, failureMessage + ": LinearLimitJacobian");
                                        break;
                                    case JacobianType.AngularLimit1D:
                                        Assert.Less(header.AccessBaseJacobian<AngularLimit1DJacobian>().InitialError, 1e-2f, failureMessage + ": AngularLimit1DJacobian");
                                        break;
                                    case JacobianType.AngularLimit2D:
                                        Assert.Less(header.AccessBaseJacobian<AngularLimit2DJacobian>().InitialError, 1e-2f, failureMessage + ": AngularLimit2DJacobian");
                                        break;
                                    case JacobianType.AngularLimit3D:
                                        Assert.Less(header.AccessBaseJacobian<AngularLimit3DJacobian>().InitialError, 1e-2f, failureMessage + ": AngularLimit3DJacobian");
                                        break;
                                    case JacobianType.PositionMotor:
                                        target = header.AccessBaseJacobian<PositionMotorJacobian>().TargetInB;
                                        pivotA = header.AccessBaseJacobian<PositionMotorJacobian>().PivotAinA;
                                        var pivotB = header.AccessBaseJacobian<PositionMotorJacobian>().PivotBinB;
                                        Assert.Less(header.AccessBaseJacobian<PositionMotorJacobian>().InitialError, 1e-3f,
                                            $"{failureMessage}: PositionMotorJacobian for target: {target} with pivotA: {pivotA} and pivotB: {pivotB}");
                                        break;
                                    case JacobianType.RotationMotor:
                                        target = header.AccessBaseJacobian<RotationMotorJacobian>().Target;
                                        pivotA = header.AccessBaseJacobian<RotationMotorJacobian>().AxisInMotionA;
                                        Assert.Less(header.AccessBaseJacobian<RotationMotorJacobian>().InitialError, 1e-2f,
                                            $"{failureMessage}: RotationMotorJacobian for target: {target} with axisInMotionA: {pivotA}");
                                        break;
                                    case JacobianType.LinearVelocityMotor:  // InitialError unused
                                    case JacobianType.AngularVelocityMotor: // InitialError unused
                                    default:
                                        Assert.Fail(failureMessage + ": unexpected jacobian type");
                                        break;
                                }
                            }
                        }

                        // Cleanup
                        jacobians.Dispose();
                    }
                }
            }
        }
    }
}
