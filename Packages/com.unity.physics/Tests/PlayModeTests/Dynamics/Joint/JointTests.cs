using Unity.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;
using static Unity.Physics.Math;

namespace Unity.Physics.Tests.Joints
{
    /// <summary>
    /// These tests generate random motions and joints, simulate them for several steps, then verifies that the joint error is nearly zero.
    /// Passing test does not necessarily mean good-looking joint behavior or stability for systems of multiple constraints, but the test
    /// will catch a lot of basic mathematical errors in the solver.
    /// </summary>
    class JointTests
    {
        //
        // Tiny simulation for a single body pair and joint, used by all of the tests
        //

        void applyGravity(ref MotionVelocity velocity, ref MotionData motion, float3 gravity, float timestep)
        {
            if (velocity.InverseMass > 0.0f)
            {
                velocity.LinearVelocity += gravity * timestep;
            }
        }

        static void integrate(ref MotionVelocity velocity, ref MotionData motion, float timestep)
        {
            Integrator.Integrate(ref motion.WorldFromMotion, velocity, timestep);
        }

        //
        // Random test data generation
        //

        static float3 generateRandomCardinalAxis(ref Random rnd)
        {
            float3 axis = float3.zero;
            axis[rnd.NextInt(3)] = rnd.NextBool() ? 1 : -1;
            return axis;
        }

        static RigidTransform generateRandomTransform(ref Random rnd)
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
                    rot = quaternion.AxisAngle(generateRandomCardinalAxis(ref rnd), angle);
                }
            }

            return new RigidTransform()
            {
                pos = rnd.NextInt(4) == 0 ? float3.zero : rnd.NextFloat3(-1.0f, 1.0f),
                rot = rot
            };
        }

        void generateRandomMotion(ref Random rnd, out MotionVelocity velocity, out MotionData motion, bool allowInfiniteMass)
        {
            motion = new MotionData
            {
                WorldFromMotion = generateRandomTransform(ref rnd),
                BodyFromMotion = generateRandomTransform(ref rnd)
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

        //
        // Helpers
        //

        static RigidTransform getWorldFromBody(MotionData motion)
        {
            return math.mul(motion.WorldFromMotion, math.inverse(motion.BodyFromMotion));
        }

        static float3 getBodyPointVelocity(MotionVelocity velocity, MotionData motion, float3 positionInBodySpace, out float angularLength)
        {
            float3 positionInMotion = math.transform(math.inverse(motion.BodyFromMotion), positionInBodySpace);
            float3 angularInMotion = math.cross(velocity.AngularVelocity, positionInMotion);
            angularLength = math.length(angularInMotion);
            float3 angularInWorld = math.rotate(motion.WorldFromMotion, angularInMotion);
            return angularInWorld + velocity.LinearVelocity;
        }

        //
        // Test runner
        //

        delegate Joint GenerateJoint(ref Random rnd);

        static void SolveSingleJoint(Joint jointData, int numIterations, float timestep,
            ref MotionVelocity velocityA, ref MotionVelocity velocityB, ref MotionData motionA,
            ref MotionData motionB, out NativeStream jacobiansOut, NativeStream impulseEventStream = default)
        {
            var stepInput = new Solver.StepInput
            {
                IsLastIteration = false,
                InvNumSolverIterations = 1.0f / numIterations,
                Timestep = timestep,
                InvTimestep = timestep > 0.0f ? 1.0f / timestep : 0.0f
            };

            // Build jacobians
            jacobiansOut = new NativeStream(1, Allocator.Temp);
            {
                NativeStream.Writer jacobianWriter = jacobiansOut.AsWriter();
                jacobianWriter.BeginForEachIndex(0);
                Solver.BuildJointJacobian(jointData, velocityA, velocityB, motionA, motionB, timestep, numIterations, ref jacobianWriter);
                jacobianWriter.EndForEachIndex();
            }

            var eventWriter = new NativeStream.Writer(); // no events expected
            NativeStream.Writer impulseEventWriter = impulseEventStream.IsCreated ? impulseEventStream.AsWriter() : default;

            // Solve the joint
            for (int iIteration = 0; iIteration < numIterations; iIteration++)
            {
                stepInput.IsLastIteration = (iIteration == numIterations - 1);
                NativeStream.Reader jacobianReader = jacobiansOut.AsReader();
                var jacIterator = new JacobianIterator(jacobianReader, 0);

                if (impulseEventStream.IsCreated && iIteration == (numIterations - 1))
                    impulseEventWriter.BeginForEachIndex(0);

                while (jacIterator.HasJacobiansLeft())
                {
                    ref JacobianHeader header = ref jacIterator.ReadJacobianHeader();
                    header.Solve(ref velocityA, ref velocityB, stepInput, ref eventWriter, ref eventWriter, ref impulseEventWriter,
                        false, Solver.MotionStabilizationInput.Default, Solver.MotionStabilizationInput.Default);
                }

                if (impulseEventStream.IsCreated && iIteration == (numIterations - 1))
                    impulseEventWriter.EndForEachIndex();
            }

            // After solving, integrate motions
            integrate(ref velocityA, ref motionA, timestep);
            integrate(ref velocityB, ref motionB, timestep);
        }

        void RunJointTest(string testName, GenerateJoint generateJoint)
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
                uint state = rnd.state;

                // Generate a random ball and socket joint
                Joint jointData = generateJoint(ref rnd);

                // Generate random motions
                MotionVelocity velocityA, velocityB;
                MotionData motionA, motionB;
                generateRandomMotion(ref rnd, out velocityA, out motionA, true);
                generateRandomMotion(ref rnd, out velocityB, out motionB, !velocityA.IsKinematic);

                // Simulate the joint
                {
                    // Build input
                    const float timestep = 1.0f / 50.0f;
                    const int numIterations = 4;
                    const int numSteps = 15;
                    float3 gravity = new float3(0.0f, -9.81f, 0.0f);

                    // Simulate
                    for (int iStep = 0; iStep < numSteps; iStep++)
                    {
                        // Before solving, apply gravity
                        applyGravity(ref velocityA, ref motionA, gravity, timestep);
                        applyGravity(ref velocityB, ref motionB, gravity, timestep);

                        // Solve and integrate
                        SolveSingleJoint(jointData, numIterations, timestep, ref velocityA, ref velocityB, ref motionA, ref motionB, out NativeStream jacobians);

                        // Last step, check the joint error
                        if (iStep == numSteps - 1)
                        {
                            NativeStream.Reader jacobianReader = jacobians.AsReader();
                            var jacIterator = new JacobianIterator(jacobianReader, 0);
                            string failureMessage = testName + " failed " + iTest + " (" + state + ")";
                            while (jacIterator.HasJacobiansLeft())
                            {
                                ref JacobianHeader header = ref jacIterator.ReadJacobianHeader();
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

        //
        // Tests
        //

        static void generateRandomPivots(ref Random rnd, out float3 pivotA, out float3 pivotB)
        {
            pivotA = rnd.NextBool() ? float3.zero : rnd.NextFloat3(-1.0f, 1.0f);
            pivotB = rnd.NextBool() ? float3.zero : rnd.NextFloat3(-1.0f, 1.0f);
        }

        static void generateRandomAxes(ref Random rnd, out float3 axisA, out float3 axisB)
        {
            axisA = rnd.NextInt(4) == 0 ? generateRandomCardinalAxis(ref rnd) : rnd.NextFloat3Direction();
            axisB = rnd.NextInt(4) == 0 ? generateRandomCardinalAxis(ref rnd) : rnd.NextFloat3Direction();
        }

        static void generateRandomLimits(ref Random rnd, float minClosed, float maxClosed, out float min, out float max)
        {
            min = rnd.NextBool() ? float.MinValue : rnd.NextFloat(minClosed, maxClosed);
            max = rnd.NextBool() ? rnd.NextBool() ? float.MaxValue : min : rnd.NextFloat(min, maxClosed);
        }

        Joint CreateTestJoint(PhysicsJoint joint) => new Joint
        {
            AFromJoint = joint.BodyAFromJoint.AsMTransform(),
            BFromJoint = joint.BodyBFromJoint.AsMTransform(),
            Constraints = joint.m_Constraints
        };

        [Test]
        public void FireImpulseEventTest()
        {
            var bodyAFromJoint = BodyFrame.Identity;
            bodyAFromJoint.Position = new float3(0, 0.5f, 0);
            var bodyBFromJoint = BodyFrame.Identity;
            bodyBFromJoint.Position = new float3(0, 0.5f, 0);

            var constraint = new Constraint
            {
                ConstrainedAxes = new bool3(true),
                Type = ConstraintType.Linear,
                Min = 0.5f,
                Max = 2.0f,
                SpringFrequency = Constraint.DefaultSpringFrequency,
                DampingRatio = Constraint.DefaultDampingRatio,
                MaxImpulse = float3.zero,
            };

            ConstraintBlock3 constraintblock = new ConstraintBlock3
            {
                A = constraint,
                Length = 1
            };

            var jointData = new Joint
            {
                AFromJoint = bodyAFromJoint.AsMTransform(),
                BFromJoint = bodyBFromJoint.AsMTransform(),
                Constraints = constraintblock
            };

            MotionVelocity velocityA = new MotionVelocity
            {
                LinearVelocity = new float3(10),
                AngularVelocity = new float3(10),
                InverseInertia = new float3(1),
                InverseMass = 1
            };
            MotionVelocity velocityB = new MotionVelocity
            {
                LinearVelocity = float3.zero,
                AngularVelocity = float3.zero,
                InverseInertia = float3.zero,
                InverseMass = 0.0f
            };

            MotionData motionData = new MotionData
            {
                WorldFromMotion = RigidTransform.identity,
                BodyFromMotion = RigidTransform.identity
            };

            // Build input
            const float timestep = 1.0f / 50.0f;
            const int numFrames = 15;
            float3 gravity = new float3(0.0f, -9.81f, 0.0f);

            // Simulate N frames
            for (int frame = 0; frame < numFrames; frame++)
            {
                using (var impulseEventStream = new NativeStream(1, Allocator.Temp))
                {
                    // Before solving, apply gravity
                    applyGravity(ref velocityA, ref motionData, gravity, timestep);
                    applyGravity(ref velocityB, ref motionData, gravity, timestep);

                    // Solve and integrate
                    SolveSingleJoint(jointData, 4, timestep, ref velocityA, ref velocityB, ref motionData, ref motionData, out NativeStream jacobians, impulseEventStream);

                    // We expect 1 event to be in the stream
                    Assert.AreEqual(1, impulseEventStream.Count());

                    // Cleanup
                    jacobians.Dispose();
                }
            }
        }

        [Test]
        public void BallAndSocketTest()
        {
            RunJointTest("BallAndSocketTest", (ref Random rnd) =>
            {
                generateRandomPivots(ref rnd, out float3 pivotA, out float3 pivotB);
                return CreateTestJoint(PhysicsJoint.CreateBallAndSocket(pivotA, pivotB));
            });
        }

        [Test]
        public void StiffSpringTest()
        {
            RunJointTest("StiffSpringTest", (ref Random rnd) =>
            {
                generateRandomPivots(ref rnd, out float3 pivotA, out float3 pivotB);
                generateRandomLimits(ref rnd, 0.0f, 0.5f, out float minDistance, out float maxDistance);
                return CreateTestJoint(PhysicsJoint.CreateLimitedDistance(pivotA, pivotB, new FloatRange(minDistance, maxDistance)));
            });
        }

        [Test]
        public void PrismaticTest()
        {
            RunJointTest("PrismaticTest", (ref Random rnd) =>
            {
                var jointFrameA = new BodyFrame();
                var jointFrameB = new BodyFrame();
                generateRandomPivots(ref rnd, out jointFrameA.Position, out jointFrameB.Position);
                generateRandomAxes(ref rnd, out jointFrameA.Axis, out jointFrameB.Axis);
                Math.CalculatePerpendicularNormalized(jointFrameA.Axis, out jointFrameA.PerpendicularAxis, out _);
                Math.CalculatePerpendicularNormalized(jointFrameB.Axis, out jointFrameB.PerpendicularAxis, out _);
                var distance = new FloatRange { Min = rnd.NextFloat(-0.5f, 0.5f) };
                distance.Max = rnd.NextBool() ? distance.Min : rnd.NextFloat(distance.Min, 0.5f); // note, can't use open limits because the accuracy can get too low as the pivots separate
                return CreateTestJoint(PhysicsJoint.CreatePrismatic(jointFrameA, jointFrameB, distance));
            });
        }

        [Test]
        public void HingeTest()
        {
            RunJointTest("HingeTest", (ref Random rnd) =>
            {
                var jointFrameA = new BodyFrame();
                var jointFrameB = new BodyFrame();
                generateRandomPivots(ref rnd, out jointFrameA.Position, out jointFrameB.Position);
                generateRandomAxes(ref rnd, out jointFrameA.Axis, out jointFrameB.Axis);
                Math.CalculatePerpendicularNormalized(jointFrameA.Axis, out jointFrameA.PerpendicularAxis, out _);
                Math.CalculatePerpendicularNormalized(jointFrameB.Axis, out jointFrameB.PerpendicularAxis, out _);
                return CreateTestJoint(PhysicsJoint.CreateHinge(jointFrameA, jointFrameB));
            });
        }

        [Test]
        public void LimitedHingeTest()
        {
            RunJointTest("LimitedHingeTest", (ref Random rnd) =>
            {
                var jointFrameA = new BodyFrame();
                var jointFrameB = new BodyFrame();
                generateRandomPivots(ref rnd, out jointFrameA.Position, out jointFrameB.Position);
                generateRandomAxes(ref rnd, out jointFrameA.Axis, out jointFrameB.Axis);
                Math.CalculatePerpendicularNormalized(jointFrameA.Axis, out jointFrameA.PerpendicularAxis, out _);
                Math.CalculatePerpendicularNormalized(jointFrameB.Axis, out jointFrameB.PerpendicularAxis, out _);
                FloatRange limits;
                generateRandomLimits(ref rnd, -(float)math.PI, (float)math.PI, out limits.Min, out limits.Max);
                return CreateTestJoint(PhysicsJoint.CreateLimitedHinge(jointFrameA, jointFrameB, limits));
            });
        }

        // TODO - test CreateRagdoll(), if it stays.  Doesn't fit nicely because it produces two JointDatas.

        [Test]
        public  void FixedTest()
        {
            RunJointTest("FixedTest", (ref Random rnd) =>
            {
                var jointFrameA = new RigidTransform();
                var jointFrameB = new RigidTransform();
                generateRandomPivots(ref rnd, out jointFrameA.pos, out jointFrameB.pos);
                jointFrameA.rot = generateRandomTransform(ref rnd).rot;
                jointFrameB.rot = generateRandomTransform(ref rnd).rot;
                return CreateTestJoint(PhysicsJoint.CreateFixed(jointFrameA, jointFrameB));
            });
        }

        [Test]
        public void LimitedDOFTest()
        {
            RunJointTest("LimitedDOFTest", (ref Random rnd) =>
            {
                var linearAxes = new bool3(false);
                var angularAxes = new bool3(false);
                for (int i = 0; i < 3; i++) linearAxes[rnd.NextInt(0, 2)] = !linearAxes[rnd.NextInt(0, 2)];
                for (int i = 0; i < 3; i++) angularAxes[rnd.NextInt(0, 2)] = !angularAxes[rnd.NextInt(0, 2)];
                return CreateTestJoint(PhysicsJoint.CreateLimitedDOF(generateRandomTransform(ref rnd), linearAxes, angularAxes));
            });
        }

        [Test]
        public void TwistTest()
        {
            // Check that the twist constraint works in each axis.
            // Set up a constraint between a fixed and dynamic body, give the dynamic body
            // angular velocity about the limited axis, and verify that it stops at the limit
            for (int i = 0; i < 3; i++) // For each axis
            {
                for (int j = 0; j < 2; j++) // Negative / positive limit
                {
                    float3 axis = float3.zero;
                    axis[i] = 1.0f;

                    MotionVelocity velocityA = new MotionVelocity
                    {
                        LinearVelocity = float3.zero,
                        AngularVelocity = (j + j - 1) * axis,
                        InverseInertia = new float3(1),
                        InverseMass = 1
                    };

                    MotionVelocity velocityB = new MotionVelocity
                    {
                        LinearVelocity = float3.zero,
                        AngularVelocity = float3.zero,
                        InverseInertia = float3.zero,
                        InverseMass = 0.0f
                    };

                    MotionData motionA = new MotionData
                    {
                        WorldFromMotion = RigidTransform.identity,
                        BodyFromMotion = RigidTransform.identity
                    };

                    MotionData motionB = motionA;

                    const float angle = 0.5f;
                    float minLimit = (j - 1) * angle;
                    float maxLimit = j * angle;

                    var jointData = new Joint
                    {
                        AFromJoint = MTransform.Identity,
                        BFromJoint = MTransform.Identity
                    };
                    jointData.Constraints.A = Constraint.Twist(i, new FloatRange(minLimit, maxLimit));
                    jointData.Constraints.Length = 1;
                    SolveSingleJoint(jointData, 4, 1.0f, ref velocityA, ref velocityB, ref motionA, ref motionB, out NativeStream jacobians);

                    quaternion expectedOrientation = quaternion.AxisAngle(axis, minLimit + maxLimit);
                    Utils.TestUtils.AreEqual(expectedOrientation, motionA.WorldFromMotion.rot, 1e-3f);
                    jacobians.Dispose();
                }
            }
        }

        [Test]
        public void ZeroDimensionTest()
        {
            RunJointTest("LimitedDOFTestZeroDimension", (ref Random rnd) =>
            {
                // Create a joint with 2 constraints that have 0 dimensions
                var noAxes = new bool3(false);
                return CreateTestJoint(PhysicsJoint.CreateLimitedDOF(generateRandomTransform(ref rnd), noAxes, noAxes));
            });
        }

        [Test]
        public void RaiseImpulseEventsFlagTest()
        {
            const int numCases = 9;

            NativeArray<float3> impulses = new NativeArray<float3>(numCases, Allocator.Temp);
            NativeArray<bool> shouldRaiseImpulseEvents = new NativeArray<bool>(numCases, Allocator.Temp);

            impulses[0] = new float3(math.INFINITY);                                                                shouldRaiseImpulseEvents[0] = false;
            impulses[1] = new float3(1.0f, math.INFINITY, math.INFINITY);                                           shouldRaiseImpulseEvents[1] = true;
            impulses[2] = new float3(1.0f, 2.0f, 3.0f);                                                             shouldRaiseImpulseEvents[2] = true;
            impulses[3] = new float3(System.Single.NegativeInfinity);                                               shouldRaiseImpulseEvents[3] = false;
            impulses[4] = new float3(1.0f, 2.0f, System.Single.NegativeInfinity);                                   shouldRaiseImpulseEvents[4] = true;
            impulses[5] = new float3(-1.0f, -2.0f, -3.0f);                                                          shouldRaiseImpulseEvents[5] = true;
            impulses[6] = new float3(-1.0f, System.Single.NegativeInfinity, System.Single.NegativeInfinity);        shouldRaiseImpulseEvents[6] = true;
            impulses[7] = new float3(math.INFINITY, math.INFINITY, System.Single.NegativeInfinity);                 shouldRaiseImpulseEvents[7] = false;
            impulses[8] = new float3(1.0f, -2.0f, 3.0f);                                                            shouldRaiseImpulseEvents[8] = true;

            Constraint constraint = default(Constraint);
            for (int i = 0; i < numCases; i++)
            {
                constraint = Constraint.BallAndSocket(impulses[i]);
                Assert.AreEqual(constraint.ShouldRaiseImpulseEvents, shouldRaiseImpulseEvents[i], $"Constraint: {constraint.Type} test failed. Expected value for raising impulse events is: {shouldRaiseImpulseEvents[i]} but the actual value is: {constraint.ShouldRaiseImpulseEvents}");

                constraint = Constraint.Twist(0, new FloatRange(-1.0f, 1.0f), impulses[i]);
                Assert.AreEqual(constraint.ShouldRaiseImpulseEvents, shouldRaiseImpulseEvents[i], $"Constraint: {constraint.Type} test failed. Expected value for raising impulse events is: {shouldRaiseImpulseEvents[i]} but the actual value is: {constraint.ShouldRaiseImpulseEvents}");

                constraint = Constraint.Cone(0, new FloatRange(-1.0f, 1.0f), impulses[i]);
                Assert.AreEqual(constraint.ShouldRaiseImpulseEvents, shouldRaiseImpulseEvents[i], $"Constraint: {constraint.Type} test failed. Expected value for raising impulse events is: {shouldRaiseImpulseEvents[i]} but the actual value is: {constraint.ShouldRaiseImpulseEvents}");

                constraint = Constraint.Cylindrical(0, new FloatRange(-1.0f, 1.0f), impulses[i]);
                Assert.AreEqual(constraint.ShouldRaiseImpulseEvents, shouldRaiseImpulseEvents[i], $"Constraint: {constraint.Type} test failed. Expected value for raising impulse events is: {shouldRaiseImpulseEvents[i]} but the actual value is: {constraint.ShouldRaiseImpulseEvents}");

                constraint = Constraint.FixedAngle(impulses[i]);
                Assert.AreEqual(constraint.ShouldRaiseImpulseEvents, shouldRaiseImpulseEvents[i], $"Constraint: {constraint.Type} test failed. Expected value for raising impulse events is: {shouldRaiseImpulseEvents[i]} but the actual value is: {constraint.ShouldRaiseImpulseEvents}");

                constraint = Constraint.Hinge(0, impulses[i]);
                Assert.AreEqual(constraint.ShouldRaiseImpulseEvents, shouldRaiseImpulseEvents[i], $"Constraint: {constraint.Type} test failed. Expected value for raising impulse events is: {shouldRaiseImpulseEvents[i]} but the actual value is: {constraint.ShouldRaiseImpulseEvents}");

                constraint = Constraint.LimitedDistance(new FloatRange(-1.0f, 1.0f), impulses[i]);
                Assert.AreEqual(constraint.ShouldRaiseImpulseEvents, shouldRaiseImpulseEvents[i], $"Constraint: {constraint.Type} test failed. Expected value for raising impulse events is: {shouldRaiseImpulseEvents[i]} but the actual value is: {constraint.ShouldRaiseImpulseEvents}");

                constraint = Constraint.Planar(0, new FloatRange(-1.0f, 1.0f), impulses[i]);
                Assert.AreEqual(constraint.ShouldRaiseImpulseEvents, shouldRaiseImpulseEvents[i], $"Constraint: {constraint.Type} test failed. Expected value for raising impulse events is: {shouldRaiseImpulseEvents[i]} but the actual value is: {constraint.ShouldRaiseImpulseEvents}");

                // Motorized constraints should never raise impulse events

                for (int j = 0; j < 3; j++)
                {
                    constraint = Constraint.MotorPlanar(impulses[i][j], 1.0f);
                    Assert.IsFalse(constraint.ShouldRaiseImpulseEvents, $"Motorized constraints should not raise impulse events. Test failed for: {constraint.Type}");

                    constraint = Constraint.MotorTwist(impulses[i][j], 1.0f);
                    Assert.IsFalse(constraint.ShouldRaiseImpulseEvents, $"Motorized constraints should not raise impulse events. Test failed for: {constraint.Type}");

                    constraint = Constraint.LinearVelocityMotor(impulses[i][j], 1.0f);
                    Assert.IsFalse(constraint.ShouldRaiseImpulseEvents, $"Motorized constraints should not raise impulse events. Test failed for: {constraint.Type}");

                    constraint = Constraint.AngularVelocityMotor(impulses[i][j], 1.0f);
                    Assert.IsFalse(constraint.ShouldRaiseImpulseEvents, $"Motorized constraints should not raise impulse events. Test failed for: {constraint.Type}");
                }
            }
        }
    }
}
