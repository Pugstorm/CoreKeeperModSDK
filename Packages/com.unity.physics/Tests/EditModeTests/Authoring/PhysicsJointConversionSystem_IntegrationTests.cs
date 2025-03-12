using System;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Physics.Tests.Authoring
{
    class PhysicsJointConversionSystem_IntegrationTests : BaseHierarchyConversionTest
    {
        static object[] JointTestCases =
        {
            new object[] { typeof(UnityEngine.HingeJoint), 1},
            new object[] { typeof(UnityEngine.FixedJoint), 1 },
            new object[] { typeof(UnityEngine.SpringJoint), 1 },
            new object[] { typeof(UnityEngine.CharacterJoint), 2 },
            new object[] { typeof(UnityEngine.ConfigurableJoint), 2 },
        };

        static object[] MultiJointTestCases =
        {
            new object[] { typeof(UnityEngine.HingeJoint), 2},
            new object[] { typeof(UnityEngine.FixedJoint), 2 },
            new object[] { typeof(UnityEngine.SpringJoint), 2 },
            new object[] { typeof(UnityEngine.CharacterJoint), 4 },
            new object[] { typeof(UnityEngine.ConfigurableJoint), 4 },
        };

        [TestCaseSource(nameof(JointTestCases))]
        public void ConversionSystems_WhenGOHasJoint_IsEnableCollisionFlagSet(Type jointType, int count)
        {
            CreateHierarchy(new[] { jointType }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<UnityEngine.Joint>().enableCollision = true;

            TestConvertedData<PhysicsConstrainedBodyPair>(pair =>
            {
                for (int i = 0; i < count; ++i)
                    Assert.That(pair[i].EnableCollision, Is.Not.EqualTo(0));
            }, count);
        }

        [TestCaseSource(nameof(JointTestCases))]
        public void ConversionSystems_WhenGOHasJoint_AndUsesBreakForce_ConvertsToMaxImpulse(Type jointType, int count)
        {
            CreateHierarchy(new[] { jointType }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.Joint>();
            joint.breakForce = 1.0f;

            TestConvertedData<PhysicsJoint>(joints =>
            {
                for (int i = 0; i < joints.Length; ++i)
                {
                    var constraints = joints[i].GetConstraints();
                    for (int j = 0; j < constraints.Length; ++j)
                    {
                        if (constraints[j].Type == ConstraintType.Linear)
                        {
                            Assume.That(constraints[j].MaxImpulse, Is.EqualTo(new float3(joint.breakForce * Time.fixedDeltaTime)));
                            Assume.That(constraints[j].ShouldRaiseImpulseEvents, Is.EqualTo(true));
                        }
                    }
                }
            }, count);
        }

        [TestCaseSource(nameof(JointTestCases))]
        public void ConversionSystems_WhenGOHasJoint_AndUsesBreakTorque_ConvertsToMaxImpulse(Type jointType, int count)
        {
            CreateHierarchy(new[] { jointType }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.Joint>();
            joint.breakTorque = 1.0f;

            TestConvertedData<PhysicsJoint>(joints =>
            {
                for (int i = 0; i < joints.Length; ++i)
                {
                    var constraints = joints[i].GetConstraints();
                    for (int j = 0; j < constraints.Length; ++j)
                    {
                        if (constraints[j].Type == ConstraintType.Angular)
                        {
                            Assume.That(constraints[j].MaxImpulse, Is.EqualTo(new float3(joint.breakTorque * Time.fixedDeltaTime)));
                            Assume.That(constraints[j].ShouldRaiseImpulseEvents, Is.EqualTo(true));
                        }
                    }
                }
            }, count);
        }

        [TestCaseSource(nameof(JointTestCases))]
        public void ConversionSystems_WhenGOHasJoint_AndHasInfinityBreakValues_ImpulseEventFlagIsDisabled(Type jointType, int count)
        {
            CreateHierarchy(new[] { jointType }, Array.Empty<Type>(), Array.Empty<Type>());
            TestConvertedData<PhysicsJoint>(joints =>
            {
                for (int i = 0; i < joints.Length; ++i)
                {
                    var constraints = joints[i].GetConstraints();
                    for (int j = 0; j < constraints.Length; ++j)
                        Assume.That(constraints[j].ShouldRaiseImpulseEvents, Is.EqualTo(false));
                }
            }, count);
        }

        [Test]
        public void ConversionSystems_WhenGOHasSpringJoint_IsMinDistanceValueSet()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.SpringJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<UnityEngine.SpringJoint>().minDistance = 100f;
            var expectedMin =
                math.min(Root.GetComponent<UnityEngine.SpringJoint>().minDistance, Root.GetComponent<UnityEngine.SpringJoint>().maxDistance);

            TestConvertedData<PhysicsJoint>(j =>
            {
                Assume.That(j.GetConstraints().Length, Is.EqualTo(1)); // TODO: is this a correct assumption, or should it be two separate constraints for min/max with different stiffness?
                Assert.That(j[0].Min, Is.EqualTo(expectedMin));
            });
        }

        [Test]
        public void ConversionSystems_WhenGOHasSpringJoint_IsMaxDistanceValueSet()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.SpringJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<UnityEngine.SpringJoint>().maxDistance = 100f;
            var expectedMax =
                math.max(Root.GetComponent<UnityEngine.SpringJoint>().minDistance, Root.GetComponent<UnityEngine.SpringJoint>().maxDistance);

            TestConvertedData<PhysicsJoint>(j =>
            {
                Assume.That(j.GetConstraints().Length, Is.EqualTo(1)); // TODO: is this a correct assumption, or should it be two separate constraints for min/max with different stiffness?
                Assert.That(j[0].Max, Is.EqualTo(expectedMax));
            });
        }

        [Test]
        public void ConversionSystems_WhenGOHasHingeJoint_WhenUseLimitsHasValue_FinalConstraintIsAllLinearAxesLocked([Values] bool useLimits)
        {
            CreateHierarchy(new[] { typeof(UnityEngine.HingeJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.HingeJoint>();
            joint.useLimits = useLimits;

            TestConvertedData<PhysicsJoint>(j =>
            {
                Assume.That(j.GetConstraints().Length, Is.GreaterThanOrEqualTo(2), "HingeJoint should always produce at least 2 constraints");
                var linearConstraint = j[j.GetConstraints().Length - 1];
                linearConstraint.DampingRatio = linearConstraint.SpringFrequency = 0f; // ignore spring settings
                linearConstraint.MaxImpulse = 0f; // ignore impulse settings
                Assert.That(
                    linearConstraint,
                    Is.EqualTo(new Constraint
                    {
                        Type = ConstraintType.Linear,
                        ConstrainedAxes = new bool3(true),
                        Min = 0f,
                        Max = 0f
                    })
                );
            });
        }

        [Test]
        public void ConversionSystems_WhenGOHasHingeJoint_AndDoesNotUseLimits_FirstConstraintIsOtherAngularAxesLocked()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.HingeJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.HingeJoint>();
            joint.useLimits = false;

            TestConvertedData<PhysicsJoint>(j =>
            {
                Assume.That(j.GetConstraints().Length, Is.EqualTo(2), "Unlimited HingeJoint should always produce exactly 2 constraints");
                var angularConstraint = j[0];
                angularConstraint.DampingRatio = angularConstraint.SpringFrequency = 0f; // ignore spring settings
                angularConstraint.MaxImpulse = 0f; // ignore impulse settings
                Assert.That(
                    angularConstraint,
                    Is.EqualTo(new Constraint
                    {
                        Type = ConstraintType.Angular,
                        ConstrainedAxes = new bool3 { y = true, z = true },
                        Min = 0f,
                        Max = 0f
                    })
                );
            });
        }

        [Test]
        public void ConversionSystems_WhenGOHasHingeJoint_AndUsesLimits_FirstConstraintIsLimitedAxisWithProperHandedness()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.HingeJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.HingeJoint>();
            joint.useLimits = true;
            var limits = new float2(-15f, 35f);
            joint.limits = new JointLimits { min = limits.x, max = limits.y };

            TestConvertedData<PhysicsJoint>(j =>
            {
                Assume.That(j.GetConstraints().Length, Is.EqualTo(3), "Limited HingeJoint should always produce exactly 3 constraints");
                var angularConstraint = j[0];
                angularConstraint.DampingRatio = angularConstraint.SpringFrequency = 0f; // ignore spring settings
                angularConstraint.MaxImpulse = 0f; // ignore impulse settings
                var expectedLimits = math.radians(limits);
                Assert.That(
                    angularConstraint,
                    Is.EqualTo(new Constraint
                    {
                        Type = ConstraintType.Angular,
                        ConstrainedAxes = new bool3 { x = true },
                        Min = expectedLimits.x,
                        Max = expectedLimits.y
                    })
                );
            });
        }

        [Test]
        public void ConversionSystems_WhenGOHasCharacterJoint_FinalConstraintIsAllLinearAxesLocked()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.CharacterJoint) }, Array.Empty<Type>(), Array.Empty<Type>());

            TestConvertedData<PhysicsJoint>(joints =>
            {
                // Character joint will combined always have a total of 4 constraints across 2 joints
                Assume.That(joints[0].GetConstraints().Length, Is.EqualTo(1), "CharacterJoint should always produce exactly 1 linear constraint");
                Assume.That(joints[1].GetConstraints().Length, Is.EqualTo(3), "CharacterJoint should always produce exactly 3 angular constraints");
                var linearConstraint = joints[0][0];
                linearConstraint.DampingRatio = linearConstraint.SpringFrequency = 0f; // ignore spring settings
                linearConstraint.MaxImpulse = 0f; // ignore impulse settings
                Assert.That(
                    linearConstraint,
                    Is.EqualTo(new Constraint
                    {
                        Type = ConstraintType.Linear,
                        ConstrainedAxes = new bool3(true),
                        Min = 0f,
                        Max = 0f
                    })
                );
            }, 2);
        }

        // TODO: verify proper ordering of angular constraints for optimal stability
        [Test]
        public void ConversionSystems_WhenGOHasCharacterJoint_FirstConstraintIsAngularXLimitsWithProperHandedness()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.CharacterJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.CharacterJoint>();
            var limits = new float2(-15f, 35f);
            joint.lowTwistLimit = new SoftJointLimit { limit = limits.x };
            joint.highTwistLimit = new SoftJointLimit { limit = limits.y };
            joint.swing1Limit = new SoftJointLimit { limit = 0f };
            joint.swing2Limit = new SoftJointLimit { limit = 0f };

            TestConvertedData<PhysicsJoint>(joints =>
            {
                // Character joint will combined always have a total of 4 constraints across 2 joints
                Assume.That(joints[0].GetConstraints().Length, Is.EqualTo(1), "CharacterJoint should always produce exactly 1 linear constraint");
                Assume.That(joints[1].GetConstraints().Length, Is.EqualTo(3), "CharacterJoint should always produce exactly 3 angular constraints");
                var angularConstraint = joints[1][0];
                angularConstraint.DampingRatio = angularConstraint.SpringFrequency = 0f; // ignore spring settings
                angularConstraint.MaxImpulse = 0f; // ignore impulse settings
                var expectedLimits = -math.radians(limits).yx;
                Assert.That(
                    angularConstraint,
                    Is.EqualTo(new Constraint
                    {
                        Type = ConstraintType.Angular,
                        ConstrainedAxes = new bool3 { x = true },
                        Min = expectedLimits.x,
                        Max = expectedLimits.y
                    })
                );
            }, 2);
        }

        [Test]
        public void ConversionSystems_WhenGOHasCharacterJoint_SecondConstraintIsAngularYLimitWithProperHandedness()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.CharacterJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.CharacterJoint>();
            const float limit = 45f;
            joint.lowTwistLimit = new SoftJointLimit { limit = 0f };
            joint.highTwistLimit = new SoftJointLimit { limit = 0f };
            joint.swing1Limit = new SoftJointLimit { limit = limit };
            joint.swing2Limit = new SoftJointLimit { limit = 0f };

            TestConvertedData<PhysicsJoint>(joints =>
            {
                // Character joint will combined always have a total of 4 constraints across 2 joints
                Assume.That(joints[0].GetConstraints().Length, Is.EqualTo(1), "CharacterJoint should always produce exactly 1 linear constraint");
                Assume.That(joints[1].GetConstraints().Length, Is.EqualTo(3), "CharacterJoint should always produce exactly 3 angular constraints");
                var angularConstraint = joints[1][1];
                angularConstraint.DampingRatio = angularConstraint.SpringFrequency = 0f; // ignore spring settings
                angularConstraint.MaxImpulse = 0f; // ignore impulse settings
                var expectedLimits = new float2(-math.radians(limit), math.radians(limit));
                Assert.That(
                    angularConstraint,
                    Is.EqualTo(new Constraint
                    {
                        Type = ConstraintType.Angular,
                        ConstrainedAxes = new bool3 { y = true },
                        Min = expectedLimits.x,
                        Max = expectedLimits.y
                    })
                );
            }, 2);
        }

        [Test]
        public void ConversionSystems_WhenGOHasCharacterJoint_FinalConstraintAngularZLimitWithProperHandedness()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.CharacterJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.CharacterJoint>();
            const float limit = 45f;
            joint.lowTwistLimit = new SoftJointLimit { limit = 0f };
            joint.highTwistLimit = new SoftJointLimit { limit = 0f };
            joint.swing1Limit = new SoftJointLimit { limit = 0f };
            joint.swing2Limit = new SoftJointLimit { limit = 45f };

            TestConvertedData<PhysicsJoint>(joints =>
            {
                // Character joint will combined always have a total of 4 constraints across 2 joints
                Assume.That(joints[0].GetConstraints().Length, Is.EqualTo(1), "CharacterJoint should always produce exactly 1 linear constraint");
                Assume.That(joints[1].GetConstraints().Length, Is.EqualTo(3), "CharacterJoint should always produce exactly 3 angular constraints");
                var angularConstraint = joints[1][2];
                angularConstraint.DampingRatio = angularConstraint.SpringFrequency = 0f; // ignore spring settings
                angularConstraint.MaxImpulse = 0f; // ignore impulse settings
                var expectedLimits = new float2(-math.radians(limit), math.radians(limit));
                Assert.That(
                    angularConstraint,
                    Is.EqualTo(new Constraint
                    {
                        Type = ConstraintType.Angular,
                        ConstrainedAxes = new bool3 { z = true },
                        Min = expectedLimits.x,
                        Max = expectedLimits.y
                    })
                );
            }, 2);
        }

        [Test]
        public void ConversionSystems_WhenGOHasConfigurableJoint_LinearMotionAndAngularMotionFree_JointHasNoConstraints()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.ConfigurableJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.ConfigurableJoint>();
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            TestConvertedData<PhysicsJoint>(j => Assert.That(j[0].GetConstraints().Length, Is.EqualTo(0), "Unlimited ConfigurableJoint should produce no constraints"), 2);
        }

        [Test]
        public void ConversionSystems_WhenGOHasConfigurableJoint_WithMixedLinearAndAngularMotionLockedOrLimited_HasMaxConstraints()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.ConfigurableJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.ConfigurableJoint>();
            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Limited;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Locked;

            TestConvertedData<PhysicsJoint>(j =>
            {
                Assume.That(j[0].GetConstraints().Length, Is.EqualTo(2), "Limited & Locked ConfigurableJoint should produce 2 linear constraints");
                Assume.That(j[1].GetConstraints().Length, Is.EqualTo(3), "Limited & Locked ConfigurableJoint should produce 3 angular constraints");
            }, 2);
        }

        [TestCase(ConfigurableJointMotion.Locked, ConfigurableJointMotion.Free, ConfigurableJointMotion.Free, true, false, false, TestName = "Linear locked (x)")]
        [TestCase(ConfigurableJointMotion.Free, ConfigurableJointMotion.Locked, ConfigurableJointMotion.Free, false, true, false, TestName = "Linear locked (y)")]
        [TestCase(ConfigurableJointMotion.Free, ConfigurableJointMotion.Free, ConfigurableJointMotion.Locked, false, false, true, TestName = "Linear locked (z)")]
        [TestCase(ConfigurableJointMotion.Locked, ConfigurableJointMotion.Locked, ConfigurableJointMotion.Locked, true, true, true, TestName = "Linear locked (all)")]
        [TestCase(ConfigurableJointMotion.Limited, ConfigurableJointMotion.Free, ConfigurableJointMotion.Free, true, false, false, TestName = "Linear limited (x)")]
        [TestCase(ConfigurableJointMotion.Free, ConfigurableJointMotion.Limited, ConfigurableJointMotion.Free, false, true, false, TestName = "Linear limited (y)")]
        [TestCase(ConfigurableJointMotion.Free, ConfigurableJointMotion.Free, ConfigurableJointMotion.Limited, false, false, true, TestName = "Linear limited (z)")]
        [TestCase(ConfigurableJointMotion.Limited, ConfigurableJointMotion.Limited, ConfigurableJointMotion.Limited, true, true, true, TestName = "Linear limited (all)")]
        public void ConversionSystems_WhenGOHasConfigurableJoint_LinearMotionLockedOrLimited_AxesAreConstrained(
            ConfigurableJointMotion linearXMotion, ConfigurableJointMotion linearYMotion, ConfigurableJointMotion linearZMotion,
            bool expectedConstrainedX, bool expectedConstrainedY, bool expectedConstrainedZ
        )
        {
            CreateHierarchy(new[] { typeof(UnityEngine.ConfigurableJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.ConfigurableJoint>();
            joint.xMotion = linearXMotion;
            joint.yMotion = linearYMotion;
            joint.zMotion = linearZMotion;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            TestConvertedData<PhysicsJoint>(j =>
            {
                Assume.That(j[0].GetConstraints().Length, Is.EqualTo(1), "ConfigurableJoint with uniform limits on a single axis should produce exactly 1 constraint");
                var expectedConstrainedAxes = new bool3(expectedConstrainedX, expectedConstrainedY, expectedConstrainedZ);
                Assert.That(j[0][0].ConstrainedAxes, Is.EqualTo(expectedConstrainedAxes));
            }, 2);
        }

        [TestCase(ConfigurableJointMotion.Locked, ConfigurableJointMotion.Free, ConfigurableJointMotion.Free, true, false, false, TestName = "Angular locked (x)")]
        [TestCase(ConfigurableJointMotion.Free, ConfigurableJointMotion.Locked, ConfigurableJointMotion.Free, false, true, false, TestName = "Angular locked (y)")]
        [TestCase(ConfigurableJointMotion.Free, ConfigurableJointMotion.Free, ConfigurableJointMotion.Locked, false, false, true, TestName = "Angular locked (z)")]
        [TestCase(ConfigurableJointMotion.Locked, ConfigurableJointMotion.Locked, ConfigurableJointMotion.Locked, true, true, true, TestName = "Angular locked (all)")]
        [TestCase(ConfigurableJointMotion.Limited, ConfigurableJointMotion.Free, ConfigurableJointMotion.Free, true, false, false, TestName = "Angular limited (x)")]
        [TestCase(ConfigurableJointMotion.Free, ConfigurableJointMotion.Limited, ConfigurableJointMotion.Free, false, true, false, TestName = "Angular limited (y)")]
        [TestCase(ConfigurableJointMotion.Free, ConfigurableJointMotion.Free, ConfigurableJointMotion.Limited, false, false, true, TestName = "Angular limited (z)")]
        public void ConversionSystems_WhenGOHasConfigurableJoint_AngularMotionLockOrLimited_AxesAreConstrained(
            ConfigurableJointMotion angularXMotion, ConfigurableJointMotion angularYMotion, ConfigurableJointMotion angularZMotion,
            bool expectedConstrainedX, bool expectedConstrainedY, bool expectedConstrainedZ
        )
        {
            CreateHierarchy(new[] { typeof(UnityEngine.ConfigurableJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.ConfigurableJoint>();
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = angularXMotion;
            joint.angularYMotion = angularYMotion;
            joint.angularZMotion = angularZMotion;

            TestConvertedData<PhysicsJoint>(j =>
            {
                Assume.That(j[1].GetConstraints().Length, Is.EqualTo(1), "ConfigurableJoint with limits on only one axis should produce exactly 1 constraint");
                var expectedConstrainedAxes = new bool3(expectedConstrainedX, expectedConstrainedY, expectedConstrainedZ);
                Assert.That(j[1][0].ConstrainedAxes, Is.EqualTo(expectedConstrainedAxes));
            }, 2);
        }

        [Test]
        public void ConversionSystems_WhenGOHasConfigurableJoint_AngularMotion__AllAxesAreLimited()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.ConfigurableJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.ConfigurableJoint>();
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Limited;

            TestConvertedData<PhysicsJoint>(joints =>
            {
                Assume.That(joints[0].GetConstraints().Length, Is.EqualTo(0), "ConfigurableJoint with free linear on all axes should produce exactly 0 constraints");
                Assume.That(joints[1].GetConstraints().Length, Is.EqualTo(3), "ConfigurableJoint with angular limits on all axes should produce exactly 3 constraints");
                Assert.That(joints[1][0].ConstrainedAxes, Is.EqualTo(new bool3(true, false, false)));
                Assert.That(joints[1][1].ConstrainedAxes, Is.EqualTo(new bool3(false, true, false)));
                Assert.That(joints[1][2].ConstrainedAxes, Is.EqualTo(new bool3(false, false, true)));
            }, 2);
        }

        // TODO: test to verify ConfigurableJoint linear limit converts properly

        // TODO: verify proper ordering of angular constraints for optimal stability
        [Test]
        public void ConversionSystems_WhenGOHasConfigurableJoint_AngularXLimitsConvertToProperHandedness()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.ConfigurableJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.ConfigurableJoint>();
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = ConfigurableJointMotion.Limited;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;
            var limits = new float2(-15f, 35f);
            joint.lowAngularXLimit = new SoftJointLimit { limit = limits.x };
            joint.highAngularXLimit = new SoftJointLimit { limit = limits.y };
            joint.angularYLimit = new SoftJointLimit { limit = 0f };
            joint.angularZLimit = new SoftJointLimit { limit = 0f };

            TestConvertedData<PhysicsJoint>(joints =>
            {
                Assume.That(joints[0].GetConstraints().Length, Is.EqualTo(0), "ConfigurableJoint with no linear limits on any axis should produce exactly 0 constraints");
                Assume.That(joints[1].GetConstraints().Length, Is.EqualTo(1), "ConfigurableJoint with limits on only x angular axis should produce exactly 1 constraint");
                var angularConstraint = joints[1][0];
                angularConstraint.DampingRatio = angularConstraint.SpringFrequency = 0f; // ignore spring settings
                angularConstraint.MaxImpulse = 0f; // ignore impulse settings
                var expectedLimits = -math.radians(limits).yx;
                Assert.That(
                    angularConstraint,
                    Is.EqualTo(new Constraint
                    {
                        Type = ConstraintType.Angular,
                        ConstrainedAxes = new bool3 { x = true },
                        Min = expectedLimits.x,
                        Max = expectedLimits.y
                    })
                );
            }, 2);
        }

        [Test]
        public void ConversionSystems_WhenGOHasConfigurableJoint_AngularYLimitConvertsToProperHandedness()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.ConfigurableJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.ConfigurableJoint>();
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Limited;
            joint.angularZMotion = ConfigurableJointMotion.Free;
            const float limit = 45f;
            joint.lowAngularXLimit = new SoftJointLimit { limit = 0f };
            joint.highAngularXLimit = new SoftJointLimit { limit = 0f };
            joint.angularYLimit = new SoftJointLimit { limit = limit };
            joint.angularZLimit = new SoftJointLimit { limit = 0f };

            TestConvertedData<PhysicsJoint>(joints =>
            {
                Assume.That(joints[0].GetConstraints().Length, Is.EqualTo(0), "ConfigurableJoint with no linear limits on any axis should produce exactly 0 constraints");
                Assume.That(joints[1].GetConstraints().Length, Is.EqualTo(1), "ConfigurableJoint with limits on only y angular axis should produce exactly 1 constraint");
                var angularConstraint = joints[1][0];
                angularConstraint.DampingRatio = angularConstraint.SpringFrequency = 0f; // ignore spring settings
                angularConstraint.MaxImpulse = 0f; // ignore impulse settings
                var expectedLimits = new float2(-math.radians(limit), math.radians(limit));
                Assert.That(
                    angularConstraint,
                    Is.EqualTo(new Constraint
                    {
                        Type = ConstraintType.Angular,
                        ConstrainedAxes = new bool3 { y = true },
                        Min = expectedLimits.x,
                        Max = expectedLimits.y
                    })
                );
            }, 2);
        }

        [Test]
        public void ConversionSystems_WhenGOHasConfigurableJoint_AngularZLimitConvertsToProperHandedness()
        {
            CreateHierarchy(new[] { typeof(UnityEngine.ConfigurableJoint) }, Array.Empty<Type>(), Array.Empty<Type>());
            var joint = Root.GetComponent<UnityEngine.ConfigurableJoint>();
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Limited;
            const float limit = 45f;
            joint.lowAngularXLimit = new SoftJointLimit { limit = 0f };
            joint.highAngularXLimit = new SoftJointLimit { limit = 0f };
            joint.angularYLimit = new SoftJointLimit { limit = 0f };
            joint.angularZLimit = new SoftJointLimit { limit = limit };

            TestConvertedData<PhysicsJoint>(joints =>
            {
                Assume.That(joints[0].GetConstraints().Length, Is.EqualTo(0), "ConfigurableJoint with no linear limits on any axis should produce exactly 0 constraints");
                Assume.That(joints[1].GetConstraints().Length, Is.EqualTo(1), "ConfigurableJoint with limits on only z angular axis should produce exactly 1 constraint");
                var angularConstraint = joints[1][0];
                angularConstraint.DampingRatio = angularConstraint.SpringFrequency = 0f; // ignore spring settings
                angularConstraint.MaxImpulse = 0f; // ignore impulse settings
                var expectedLimits = new float2(-math.radians(limit), math.radians(limit));
                Assert.That(
                    angularConstraint,
                    Is.EqualTo(new Constraint
                    {
                        Type = ConstraintType.Angular,
                        ConstrainedAxes = new bool3 { z = true },
                        Min = expectedLimits.x,
                        Max = expectedLimits.y
                    })
                );
            }, 2);
        }

        [TestCaseSource(nameof(MultiJointTestCases))]
        public void ConversionSystems_WhenGameObjectHasMultipleJointComponents_CreatesMultipleJoints(Type jointType, int assumeCount)
        {
            CreateHierarchy(new[] { jointType, jointType }, Array.Empty<Type>(), Array.Empty<Type>());

            TestConvertedData<PhysicsJoint>(joints => Assert.That(joints, Has.Length.EqualTo(assumeCount).And.All.Not.EqualTo(default(PhysicsJoint))), assumeCount);
        }

        // Tests that when game objects are scaled, the local anchor positions in joints are affected by the scale
        [Test]
        public void ConversionSystems_WhenGameObjectIsScaled_JointAnchorsAreScaled()
        {
            CreateHierarchy(Array.Empty<Type>(),
                new[] { typeof(Rigidbody) },
                new[] {typeof(Rigidbody), typeof(SpringJoint)});

            float3 parentScale = new float3(2f, 3f, 4f);
            Parent.transform.localScale = parentScale;

            float3 localAnchorOffset = 1;
            float3 expectedScaledAnchorOffset = localAnchorOffset * parentScale;

            var springJoint = Child.GetComponent<SpringJoint>();
            springJoint.anchor = localAnchorOffset;
            springJoint.connectedAnchor = localAnchorOffset;
            springJoint.connectedBody = Parent.GetComponent<Rigidbody>();

            TestConvertedData<PhysicsJoint>(joints =>
            {
                var joint = joints[0];
                Assume.That(joint.BodyAFromJoint.Position, Is.PrettyCloseTo(expectedScaledAnchorOffset));
                Assume.That(joint.BodyBFromJoint.Position, Is.PrettyCloseTo(expectedScaledAnchorOffset));
            }, 1);
        }

        /*
        // following are slow tests used for local regression testing only
        [Test]
        public void TestAllConfigurableJointMotionCombinations(
            [Values(ConfigurableJointMotion.Free, ConfigurableJointMotion.Limited, ConfigurableJointMotion.Locked)] ConfigurableJointMotion motionX,
            [Values(ConfigurableJointMotion.Free, ConfigurableJointMotion.Limited, ConfigurableJointMotion.Locked)] ConfigurableJointMotion motionY,
            [Values(ConfigurableJointMotion.Free, ConfigurableJointMotion.Limited, ConfigurableJointMotion.Locked)] ConfigurableJointMotion motionZ
        )
        {
            CreateHierarchy(new[] { typeof(LegacyConfigurable) }, Array.Empty<Type>(), Array.Empty<Type>());
            LegacyConfigurable joint = Root.GetComponent<LegacyConfigurable>();
            joint.xMotion = motionX;
            joint.yMotion = motionY;
            joint.zMotion = motionZ;

            joint.angularXMotion = motionX;
            joint.angularYMotion = motionY;
            joint.angularZMotion = motionZ;

            TestConvertedData<PhysicsJoint>(j => Assert.Pass(), 2);
        }
        */
    }
}
