using NUnit.Framework;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using Unity.Physics.Aspects;
using Unity.Physics.Tests.Utils;
using UnityEngine;
using UnityEngine.Assertions.Must;
using static Unity.Entities.SystemAPI;
using ForceMode = Unity.Physics.Extensions.ForceMode;

namespace Unity.Physics.Tests.Aspects
{
    public static class AspectTestUtils
    {
        internal static float3 DefaultPos => new float3(1.0f, 0.0f, 0.0f);
        internal static quaternion DefaultRot => quaternion.AxisAngle(new float3(0.0f, 1.0f, 0.0f), math.radians(45.0f));
        internal static PhysicsVelocity DefaultVelocity => new PhysicsVelocity { Angular = new float3(0.2f, 0.5f, 0.1f), Linear = new float3(0.1f, 0.2f, 0.3f) };
        internal static PhysicsDamping DefaultDamping => new PhysicsDamping { Angular = 0.75f, Linear = 0.5f };
        internal static PhysicsMass DefaultMass => PhysicsMass.CreateDynamic(MassProperties.UnitSphere, 3.0f);
        internal static PhysicsGravityFactor DefaultGravityFactor => new PhysicsGravityFactor { Value = 0.50f };
        internal static float NonIdentityScale => 2.0f;
        internal static Material Material1 => new Material { CollisionResponse = CollisionResponsePolicy.Collide, CustomTags = 2, Friction = 0.197f, Restitution = 0.732f, FrictionCombinePolicy = Material.CombinePolicy.Minimum, RestitutionCombinePolicy = Material.CombinePolicy.ArithmeticMean };
        internal static Material Material2 => new Material { CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents, CustomTags = 4, Friction = 0.127f, Restitution = 0.332f, FrictionCombinePolicy = Material.CombinePolicy.Maximum, RestitutionCombinePolicy = Material.CombinePolicy.GeometricMean};
        internal static CollisionFilter NonDefaultFilter => new CollisionFilter { BelongsTo = 123, CollidesWith = 567, GroupIndex = 442 };
        internal static CollisionFilter DefaultFilter => CollisionFilter.Default;
        internal static CollisionFilter ModificationFilter => new CollisionFilter { BelongsTo = 234, CollidesWith = 123, GroupIndex = 221 };
    }

    partial class RigidBodyAspect_UnitTests
    {
        internal enum BodyType
        {
            STATIC,
            DYNAMIC,
            INFINITE_MASS,
            INFINITE_INERTIA,
            KINEMATIC_NO_MASS_OVERRIDE,
            KINEMATIC_MASS_OVERRIDE,
            SET_VELOCITY_TO_ZERO,
            SCALED_DYNAMIC
        }

        internal static Entity CreateBodyComponents(BodyType type, EntityManager manager)
        {
            // Create default components - index, transform, body, scale
            PhysicsWorldIndex worldIndex = new PhysicsWorldIndex { Value = 0 };

            PhysicsCollider pc = new PhysicsCollider();
            {
                BoxGeometry geometry = new BoxGeometry
                {
                    BevelRadius = 0.0015f,
                    Center = float3.zero,
                    Orientation = quaternion.identity,
                    Size = new float3(1.0f, 2.0f, 3.0f)
                };

                pc.Value = BoxCollider.Create(geometry);
            }

            LocalTransform tl = LocalTransform.FromPositionRotationScale(AspectTestUtils.DefaultPos, AspectTestUtils.DefaultRot, 1.0f);
            LocalToWorld ltw = new LocalToWorld { Value = tl.ToMatrix() };

            PhysicsVelocity pv = AspectTestUtils.DefaultVelocity;
            PhysicsMass pm = AspectTestUtils.DefaultMass;
            PhysicsGravityFactor pgf = AspectTestUtils.DefaultGravityFactor;
            PhysicsDamping pd = AspectTestUtils.DefaultDamping;
            PhysicsMassOverride pmo = new PhysicsMassOverride { IsKinematic = 0, SetVelocityToZero = 0 };

            Entity body = manager.CreateEntity();

            // Add index, transform, scale, localToWorld, collider
            manager.AddSharedComponent<PhysicsWorldIndex>(body, worldIndex);
            manager.AddComponentData<LocalTransform>(body, tl);
            manager.AddComponentData<LocalToWorld>(body, ltw);
            manager.AddComponentData<PhysicsCollider>(body, pc);
            manager.AddComponentData<PhysicsDamping>(body, pd);
            manager.AddComponentData<PhysicsGravityFactor>(body, pgf);

            switch (type)
            {
                case BodyType.INFINITE_MASS:
                    pm.InverseMass = 0.0f;
                    goto case BodyType.DYNAMIC;

                case BodyType.INFINITE_INERTIA:
                    pm.InverseInertia = float3.zero;
                    goto case BodyType.DYNAMIC;

                case BodyType.KINEMATIC_NO_MASS_OVERRIDE:
                    pm.InverseInertia = float3.zero;
                    pm.InverseMass = 0.0f;
                    goto case BodyType.DYNAMIC;

                case BodyType.SET_VELOCITY_TO_ZERO:
                    pmo.SetVelocityToZero = 1;
                    goto case BodyType.KINEMATIC_MASS_OVERRIDE;

                case BodyType.SCALED_DYNAMIC:

                    var localTransform = manager.GetComponentData<LocalTransform>(body);
                    localTransform.Scale = AspectTestUtils.NonIdentityScale;
                    manager.SetComponentData<LocalTransform>(body, localTransform);
                    goto case BodyType.DYNAMIC;

                case BodyType.KINEMATIC_MASS_OVERRIDE:
                    pmo.IsKinematic = 1;
                    manager.AddComponentData<PhysicsMassOverride>(body, pmo);
                    goto case BodyType.DYNAMIC;

                case BodyType.DYNAMIC:
                    manager.AddComponentData<PhysicsVelocity>(body, pv);
                    manager.AddComponentData<PhysicsMass>(body, pm);
                    break;

                case BodyType.STATIC:
                default:
                    break;
            }
            return body;
        }

        internal static void RunTest<S>(BodyType type)
            where S : SystemBase
        {
            using (var world = new World("Test World"))
            {
                var bodyEntity = CreateBodyComponents(type, world.EntityManager);
                var system = world.GetOrCreateSystemManaged<S>();
                system.Update();
                world.EntityManager.GetComponentData<PhysicsCollider>(bodyEntity).Value.Dispose();
            }
        }

        internal const float k_Epsilon = 1e-3f;

        // true if equal
        internal static bool Compare2float3WithEpsilon(float3 a, float3 b, float epsilon = k_Epsilon)
        {
            float3 diff = a - b;
            for (int i = 0; i < 3; i++)
            {
                if (math.abs(diff[i]) > epsilon)
                {
                    return false;
                }
            }
            return true;
        }

        // true if equal
        internal static bool Compare2QuaternionWithEpsilon(quaternion a, quaternion b, float epsilon = k_Epsilon)
        {
            float4 diff = a.value - b.value;
            for (int i = 0; i < 4; i++)
            {
                if (math.abs(diff[i]) > epsilon)
                {
                    return false;
                }
            }
            return true;
        }

        [Test]
        public void StaticBody_Has_No_RigidBodyAspect()
        {
            RunTest<StaticBodyHasNoRigidBodyAspect>(BodyType.STATIC);
        }

        [Test]
        public void DynamicBody_Has_RigidBodyAspect()
        {
            RunTest<DynamicBodyHasRigidBodyAspect>(BodyType.DYNAMIC);
        }

        [Test]
        public void DynamicBody_Aspect_PropertyTest()
        {
            RunTest<DynamicBodyAspectPropertyTest>(BodyType.DYNAMIC);
        }

        [Test]
        public void DynamicBody_Aspect_InfiniteInertia_Property_Test()
        {
            RunTest<DynamicBodyInfiniteInertiaPropertyTest>(BodyType.INFINITE_INERTIA);
        }

        [Test]
        public void DynamicBody_Aspect_InfiniteMass_Property_Test()
        {
            RunTest<DynamicBodyInfiniteMassPropertyTest>(BodyType.INFINITE_MASS);
        }

        [Test]
        public void DynamicBody_GetSetDamping_RigidBodyAspect()
        {
            RunTest<DynamicBodyGetSetDampingRigidBodyAspect>(BodyType.DYNAMIC);
        }

        [Test]
        public void KinematicBody_Aspect_No_Mass_Override_PropertyTest()
        {
            RunTest<KinematicNoMassOverridePropertyTest>(BodyType.KINEMATIC_NO_MASS_OVERRIDE);
        }

        [Test]
        public void KinematicBody_Aspect_Mass_Override_PropertyTest()
        {
            RunTest<KinematicMassOverridePropertyTest>(BodyType.KINEMATIC_MASS_OVERRIDE);
        }

        [Test]
        public void KinematicBody_Aspect_Set_Velocity_To_Zero()
        {
            RunTest<KinematicSetVelocityToZeroPropertyTest>(BodyType.SET_VELOCITY_TO_ZERO);
        }

        [Test]
        public void DynamicBody_Aspect_Modifier_Test()
        {
            RunTest<DynamicBodyModifierTest>(BodyType.DYNAMIC);
        }

        [Test]
        public void DynamicBody_Scaled_Aspect_Modifier_Test()
        {
            RunTest<DynamicScaledBodyModifierTest>(BodyType.SCALED_DYNAMIC);
        }

        [Test]
        public void DynamicBody_Scaled_Aspect_Impulse_Test()
        {
            RunTest<DynamicScaledBodyImpulseTest>(BodyType.SCALED_DYNAMIC);
        }

        #region Systems

        [DisableAutoCreation]
        public partial class StaticBodyHasNoRigidBodyAspect : SystemBase
        {
            protected override void OnUpdate()
            {
                int count = 0;
                foreach (var aspect in SystemAPI.Query<RigidBodyAspect>())
                {
                    count++;
                }

                Assert.AreEqual(0, count);
            }
        }

        [DisableAutoCreation]
        public partial class DynamicBodyHasRigidBodyAspect : SystemBase
        {
            protected override void OnUpdate()
            {
                int count = 0;
                foreach (var aspect in SystemAPI.Query<RigidBodyAspect>())
                {
                    count++;
                }

                Assert.AreEqual(1, count);
            }
        }

        [DisableAutoCreation]
        public partial class DynamicBodyAspectPropertyTest : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                RigidBodyAspect aspect = GetAspect<RigidBodyAspect>(aspectEntity);

                // Transforms
                Assert.AreEqual(math.all(aspect.Position == AspectTestUtils.DefaultPos), true);
                Assert.AreEqual(aspect.Rotation, AspectTestUtils.DefaultRot);
                Assert.AreEqual(aspect.Scale, 1.0f);

                // Velocity
                Assert.AreEqual(math.all(aspect.LinearVelocity == AspectTestUtils.DefaultVelocity.Linear), true);
                Assert.AreEqual(math.all(aspect.AngularVelocityLocalSpace == AspectTestUtils.DefaultVelocity.Angular), true);
                Assert.AreEqual(math.all(math.rotate(math.mul(aspect.Rotation, aspect.BodyFromMotion_Rot), aspect.AngularVelocityLocalSpace) == aspect.AngularVelocityWorldSpace), true);

                // Damping
                Assert.AreEqual(aspect.AngularDamping, AspectTestUtils.DefaultDamping.Angular);
                Assert.AreEqual(aspect.LinearDamping, AspectTestUtils.DefaultDamping.Linear);

                // Gravity
                Assert.AreEqual(aspect.GravityFactor, AspectTestUtils.DefaultGravityFactor.Value);

                // Mass
                Assert.AreEqual(aspect.CenterOfMassLocalSpace, AspectTestUtils.DefaultMass.CenterOfMass);
                Assert.AreEqual(math.transform(new RigidTransform(AspectTestUtils.DefaultRot, AspectTestUtils.DefaultPos), aspect.CenterOfMassLocalSpace), aspect.CenterOfMassWorldSpace);
                Assert.AreEqual(aspect.BodyFromMotion_Rot, AspectTestUtils.DefaultMass.InertiaOrientation);
                Assert.AreEqual(aspect.Mass, 1.0f / AspectTestUtils.DefaultMass.InverseMass);
                Assert.AreEqual(math.all(1.0f / aspect.Inertia == AspectTestUtils.DefaultMass.InverseInertia), true);

                // Properties
                Assert.AreEqual(aspect.HasInfiniteInertia, false);
                Assert.AreEqual(aspect.HasInfiniteMass, false);
                Assert.AreEqual(aspect.IsKinematic, false);
            }
        }

        [DisableAutoCreation]
        public partial class DynamicBodyInfiniteInertiaPropertyTest : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                RigidBodyAspect aspect = GetAspect<RigidBodyAspect>(aspectEntity);

                Assert.AreEqual(aspect.HasInfiniteInertia, true);
                Assert.AreEqual(aspect.HasInfiniteMass, false);
                Assert.AreEqual(aspect.IsKinematic, false);
                Assert.AreEqual(math.all(aspect.Inertia == new float3(float.PositiveInfinity)), true);
            }
        }

        [DisableAutoCreation]
        public partial class DynamicBodyInfiniteMassPropertyTest : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                RigidBodyAspect aspect = GetAspect<RigidBodyAspect>(aspectEntity);

                Assert.AreEqual(aspect.HasInfiniteInertia, false);
                Assert.AreEqual(aspect.HasInfiniteMass, true);
                Assert.AreEqual(aspect.IsKinematic, false);
                Assert.AreEqual(aspect.Mass, float.PositiveInfinity);
            }
        }

        [DisableAutoCreation]
        public partial class DynamicBodyGetSetDampingRigidBodyAspect : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity entity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                var rba = GetAspect<RigidBodyAspect>(entity);

                // Overwrite the damping values to access set methods
                rba.LinearDamping = 0.1f;
                rba.AngularDamping = 0.2f;

                Assert.AreEqual(rba.LinearDamping, 0.1f);
                Assert.AreEqual(rba.AngularDamping, 0.2f);
            }
        }

        [DisableAutoCreation]
        public partial class KinematicNoMassOverridePropertyTest : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                RigidBodyAspect aspect = GetAspect<RigidBodyAspect>(aspectEntity);

                Assert.AreEqual(aspect.HasInfiniteInertia, true);
                Assert.AreEqual(aspect.HasInfiniteMass, true);
                Assert.AreEqual(aspect.IsKinematic, true);
                Assert.AreEqual(aspect.Mass, float.PositiveInfinity);
                Assert.AreEqual(math.all(aspect.Inertia == new float3(float.PositiveInfinity)), true);
            }
        }

        [DisableAutoCreation]
        public partial class KinematicMassOverridePropertyTest : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                RigidBodyAspect aspect = GetAspect<RigidBodyAspect>(aspectEntity);

                Assert.AreEqual(aspect.HasInfiniteInertia, true);
                Assert.AreEqual(aspect.HasInfiniteMass, true);
                Assert.AreEqual(aspect.IsKinematic, true);
                Assert.AreNotEqual(aspect.Mass, float.PositiveInfinity);
                Assert.AreNotEqual(math.all(aspect.Inertia == float3.zero), true);
                Assert.AreEqual(aspect.m_MassOveride.IsValid, true);

                // We can change IsKinematic in this case
                aspect.IsKinematic = false;

                Assert.AreEqual(aspect.HasInfiniteInertia, false);
                Assert.AreEqual(aspect.HasInfiniteMass, false);
                Assert.AreEqual(aspect.IsKinematic, false);

                // revert
                aspect.IsKinematic = true;

                // impulses should not affect kinematic bodies
                {
                    aspect.ApplyImpulseAtPointWorldSpace(new float3(1.0f, 2.0f, 3.0f), new float3(2.0f, 3.0f, 4.0f));

                    Assert.AreEqual(math.all(AspectTestUtils.DefaultVelocity.Linear == aspect.LinearVelocity), true);
                    Assert.AreEqual(math.all(AspectTestUtils.DefaultVelocity.Angular == aspect.AngularVelocityLocalSpace), true);
                }
            }
        }

        [DisableAutoCreation]
        public partial class KinematicSetVelocityToZeroPropertyTest : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                RigidBodyAspect aspect = GetAspect<RigidBodyAspect>(aspectEntity);

                Assert.AreEqual(aspect.HasInfiniteInertia, true);
                Assert.AreEqual(aspect.HasInfiniteMass, true);
                Assert.AreEqual(aspect.IsKinematic, true);
                Assert.AreNotEqual(aspect.Mass, float.PositiveInfinity);
                Assert.AreNotEqual(math.all(aspect.Inertia == new float3(float.PositiveInfinity)), true);
                Assert.AreEqual(aspect.m_MassOveride.IsValid, true);
                Assert.AreEqual(math.all(aspect.LinearVelocity == float3.zero), false);
            }
        }

        [DisableAutoCreation]
        public partial class DynamicBodyModifierTest : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                RigidBodyAspect aspect = GetAspect<RigidBodyAspect>(aspectEntity);

                // pos, rot
                {
                    // pos
                    {
                        float3 newPos = new float3(5.0f, 5.0f, 5.0f);
                        aspect.Position = newPos;
                        Assert.AreEqual(math.all(aspect.Position == newPos), true);

                        // revert
                        aspect.Position = AspectTestUtils.DefaultPos;
                        Assert.AreEqual(math.all(aspect.Position == AspectTestUtils.DefaultPos), true);
                    }

                    // rot
                    {
                        quaternion newRot = quaternion.AxisAngle(new float3(1.0f, 0.0f, 0.0f), math.radians(60));
                        aspect.Rotation = newRot;
                        Assert.AreEqual(aspect.Rotation, newRot);

                        // revert
                        aspect.Rotation = AspectTestUtils.DefaultRot;
                        Assert.AreEqual(aspect.Rotation, AspectTestUtils.DefaultRot);
                    }
                }

                // center of mass
                {
                    // center of mass
                    {
                        // change COM local space
                        {
                            float3 newCOMLocal = new float3(15.0f, 32.0f, 22.0f);
                            aspect.CenterOfMassLocalSpace = newCOMLocal;
                            Assert.AreEqual(math.all(newCOMLocal == aspect.CenterOfMassLocalSpace), true);
                            float3 newCOMWorldSpace = math.transform(new RigidTransform(aspect.Rotation, aspect.Position), newCOMLocal);
                            Assert.AreEqual(math.all(newCOMWorldSpace == aspect.CenterOfMassWorldSpace), true);

                            // revert
                            aspect.CenterOfMassLocalSpace = AspectTestUtils.DefaultMass.CenterOfMass;
                            Assert.AreEqual(math.all(aspect.CenterOfMassLocalSpace == AspectTestUtils.DefaultMass.CenterOfMass), true);
                        }

                        // change COM world space
                        {
                            float3 newCOMWorld = new float3(12.0f, 9.0f, 3.0f);
                            aspect.CenterOfMassWorldSpace = newCOMWorld;
                            Assert.AreEqual(Compare2float3WithEpsilon(newCOMWorld, aspect.CenterOfMassWorldSpace), true);

                            float3 newCOMLocal = math.transform(math.inverse(new RigidTransform(aspect.Rotation, aspect.Position)), newCOMWorld);
                            Assert.AreEqual(Compare2float3WithEpsilon(newCOMLocal, aspect.CenterOfMassLocalSpace), true);

                            // revert
                            aspect.CenterOfMassLocalSpace = AspectTestUtils.DefaultMass.CenterOfMass;
                            Assert.AreEqual(math.all(aspect.CenterOfMassLocalSpace == AspectTestUtils.DefaultMass.CenterOfMass), true);
                        }
                    }
                }

                // Mass and Inertia
                {
                    //Mass
                    {
                        aspect.Mass = float.PositiveInfinity;
                        Assert.IsTrue(aspect.HasInfiniteMass);
                        Assert.IsFalse(aspect.HasInfiniteInertia);
                        Assert.IsFalse(aspect.IsKinematic);

                        aspect.Mass = 0.0f;
                        Assert.IsTrue(aspect.Mass == AspectConstants.k_MinMass);
                        Assert.IsFalse(aspect.HasInfiniteMass);
                        Assert.IsFalse(aspect.IsKinematic);

                        // revert
                        aspect.Mass = 1.0f / AspectTestUtils.DefaultMass.InverseMass;
                    }

                    // Inertia
                    {
                        aspect.Inertia = new float3(float.PositiveInfinity);
                        Assert.IsTrue(math.all(aspect.Inertia == new float3(float.PositiveInfinity)));
                        Assert.IsTrue(aspect.HasInfiniteInertia);
                        Assert.IsFalse(aspect.HasInfiniteMass);
                        Assert.IsFalse(aspect.IsKinematic);

                        aspect.Inertia = float3.zero;
                        Assert.IsTrue(math.all(aspect.Inertia == new float3(AspectConstants.k_MinInertiaComponentValue)));
                        Assert.IsFalse(aspect.HasInfiniteInertia);
                        Assert.IsFalse(aspect.HasInfiniteMass);
                        Assert.IsFalse(aspect.IsKinematic);

                        aspect.Inertia = new float3(1.0f, float.PositiveInfinity, 1.0f);
                        Assert.IsTrue(math.all(aspect.ScaledInverseInertia == new float3(1.0f, 0.0f, 1.0f)));
                        Assert.IsTrue(math.all(aspect.Inertia == new float3(1.0f, float.PositiveInfinity, 1.0f)));
                        Assert.IsFalse(aspect.HasInfiniteInertia);
                        Assert.IsFalse(aspect.HasInfiniteMass);
                        Assert.IsFalse(aspect.IsKinematic);

                        // revert
                        aspect.Inertia = 1.0f / AspectTestUtils.DefaultMass.InverseInertia;
                    }

                    // Mass and inertia (kinematic)
                    {
                        aspect.Mass = float.PositiveInfinity;
                        aspect.Inertia = new float3(float.PositiveInfinity);
                        Assert.IsTrue(aspect.HasInfiniteInertia);
                        Assert.IsTrue(aspect.HasInfiniteMass);
                        Assert.IsTrue(aspect.IsKinematic);
                    }

                    // switching a body to kinematic is not allowed, since there is no PhysicsMassOverride component
#if UNITY_EDITOR
                    // Tests possible only in Editor, since SafetyChecks do nothing in standalone.
                    {
                        Assert.Throws<InvalidOperationException>(() =>
                        {
                            aspect.IsKinematic = true;
                        });
                    }
#endif
                }
            }
        }

        [DisableAutoCreation]
        public partial class DynamicScaledBodyModifierTest : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                RigidBodyAspect aspect = GetAspect<RigidBodyAspect>(aspectEntity);

                // scale modification
                {
                    float newScale = 3.0f;
                    aspect.Scale = newScale;
                    Assert.AreEqual(newScale, aspect.Scale);

                    // revert
                    aspect.Scale = AspectTestUtils.NonIdentityScale;
                }

                // mass implications
                {
                    Assert.AreEqual(math.all(aspect.CenterOfMassWorldSpace == aspect.WorldFromBody.TransformPoint(aspect.CenterOfMassLocalSpace)), true);

                    // local center of mass should not change depending on scale
                    var oldCOMLocal = aspect.CenterOfMassLocalSpace;
                    aspect.Scale = 7.0f;

                    Assert.AreEqual(math.all(oldCOMLocal == aspect.CenterOfMassLocalSpace), true);

                    // revert
                    aspect.Scale = AspectTestUtils.NonIdentityScale;
                }
            }
        }

        [DisableAutoCreation]
        public partial class DynamicScaledBodyImpulseTest : SystemBase
        {
            protected override void OnUpdate()
            {
                Entity aspectEntity = SystemAPI.GetSingletonEntity<PhysicsCollider>();
                RigidBodyAspect aspect = GetAspect<RigidBodyAspect>(aspectEntity);

                // GetVelocityAtPoint
                {
                    float3 point = new float3(1.0f, 2.0f, 3.0f);
                    PhysicsVelocity pv = AspectTestUtils.DefaultVelocity;
                    float3 linVel1 = pv.GetLinearVelocity(aspect.m_Mass.ValueRO, aspect.Position, aspect.Rotation, point);
                    float3 linVel2 = aspect.GetLinearVelocityAtPointWorldSpace(point);

                    Assert.AreEqual(math.all(linVel1 == linVel2), true);
                }

                // Linear Impulse
                {
                    float3 impulse = new float3(1.0f, 2.0f, 3.0f);
                    PhysicsVelocity pv = AspectTestUtils.DefaultVelocity;

                    pv.ApplyLinearImpulse(aspect.m_Mass.ValueRO, aspect.m_Transform.ValueRO.Scale, impulse);

                    aspect.ApplyLinearImpulseWorldSpace(impulse);

                    Console.WriteLine($"~~~ 1 {pv.Linear}");
                    Console.WriteLine($"~~~ 2 {aspect.LinearVelocity}");
                    Assert.AreEqual(math.all(pv.Linear == aspect.LinearVelocity), true);
                    Assert.AreEqual(math.all(pv.Angular == aspect.AngularVelocityLocalSpace), true);

                    // revert
                    aspect.LinearVelocity = AspectTestUtils.DefaultVelocity.Linear;
                    aspect.AngularVelocityLocalSpace = AspectTestUtils.DefaultVelocity.Angular;
                }

                // Angular impulse
                {
                    float3 impulse = new float3(1.0f, 2.0f, 3.0f);
                    PhysicsVelocity pv = AspectTestUtils.DefaultVelocity;
                    float3 impulseBodySpace = math.rotate(math.inverse(aspect.m_Transform.ValueRO.Rotation), impulse);
                    float3 impulseMotionSpace = math.rotate(math.inverse(aspect.BodyFromMotion_Rot), impulseBodySpace);

                    pv.ApplyAngularImpulse(aspect.m_Mass.ValueRO, aspect.m_Transform.ValueRO.Scale, impulseMotionSpace);

                    aspect.ApplyAngularImpulseWorldSpace(impulse);

                    Assert.AreEqual(math.all(pv.Linear == aspect.LinearVelocity), true);
                    Assert.AreEqual(math.all(pv.Angular == aspect.AngularVelocityLocalSpace), true);

                    // revert
                    aspect.LinearVelocity = AspectTestUtils.DefaultVelocity.Linear;
                    aspect.AngularVelocityLocalSpace = AspectTestUtils.DefaultVelocity.Angular;
                }

                // Angular and linear
                {
                    float3 impulse = new float3(1.0f, 2.0f, 3.0f);
                    PhysicsVelocity pv = AspectTestUtils.DefaultVelocity;

                    pv.ApplyLinearImpulse(aspect.m_Mass.ValueRO, aspect.m_Transform.ValueRO.Scale, impulse);
                    float3 impulseBodySpace = math.rotate(math.inverse(aspect.m_Transform.ValueRO.Rotation), impulse);
                    float3 impulseMotionSpace = math.rotate(math.inverse(aspect.BodyFromMotion_Rot), impulseBodySpace);

                    pv.ApplyAngularImpulse(aspect.m_Mass.ValueRO, aspect.m_Transform.ValueRO.Scale, impulseMotionSpace);

                    aspect.ApplyLinearImpulseWorldSpace(impulse);
                    aspect.ApplyAngularImpulseWorldSpace(impulse);

                    Assert.AreEqual(math.all(pv.Linear == aspect.LinearVelocity), true);
                    Assert.AreEqual(math.all(pv.Angular == aspect.AngularVelocityLocalSpace), true);

                    // revert
                    aspect.LinearVelocity = AspectTestUtils.DefaultVelocity.Linear;
                    aspect.AngularVelocityLocalSpace = AspectTestUtils.DefaultVelocity.Angular;
                }

                // Impulse at point
                {
                    float3 impulse = new float3(1.0f, 2.0f, 3.0f);
                    float3 point = new float3(1.0f, 2.0f, 3.0f);
                    PhysicsVelocity pv = AspectTestUtils.DefaultVelocity;

                    pv.ApplyImpulse(aspect.m_Mass.ValueRO, aspect.Position, aspect.Rotation, aspect.m_Transform.ValueRO.Scale, impulse, point);

                    aspect.ApplyImpulseAtPointWorldSpace(impulse, point);

                    Assert.AreEqual(math.all(pv.Linear == aspect.LinearVelocity), true);
                    Assert.AreEqual(math.all(pv.Angular == aspect.AngularVelocityLocalSpace), true);

                    // revert
                    aspect.LinearVelocity = AspectTestUtils.DefaultVelocity.Linear;
                    aspect.AngularVelocityLocalSpace = AspectTestUtils.DefaultVelocity.Angular;
                }

                // Explosive force
                {
                    float impulse = 1.14f;
                    float3 explosionPosition = new float3(-1.0f, 0.0f, 0.0f);
                    float explosionRadius = 10.0f;

                    PhysicsVelocity pv = AspectTestUtils.DefaultVelocity;

                    pv.ApplyExplosionForce(AspectTestUtils.DefaultMass, aspect.m_Collider.ValueRO,
                        AspectTestUtils.DefaultPos, AspectTestUtils.DefaultRot, AspectTestUtils.NonIdentityScale,
                        impulse, explosionPosition, explosionRadius, 1.0f, math.up(), CollisionFilter.Default,
                        0.0f, ForceMode.Impulse);
                    aspect.ApplyExplosiveImpulse(impulse, explosionPosition, explosionRadius, math.up(), CollisionFilter.Default, 0.0f);

                    // First, check that the impulse got applied
                    Assert.AreNotEqual(AspectTestUtils.DefaultVelocity.Linear, pv.Linear, "linear impulse was not applied");
                    Assert.AreNotEqual(AspectTestUtils.DefaultVelocity.Angular, pv.Angular, "angular impulse was not applied");

                    // Now, check the outputs of 2 ways of applying explosion force
                    Assert.AreEqual(aspect.LinearVelocity, pv.Linear, "linear velocities do not match");
                    Assert.AreEqual(aspect.m_Velocity.ValueRO.Angular, pv.Angular, "angular velocities do not match");

                    // Revert
                    aspect.LinearVelocity = AspectTestUtils.DefaultVelocity.Linear;
                    aspect.AngularVelocityLocalSpace = AspectTestUtils.DefaultVelocity.Angular;
                }

                // Center of mass consistency
                {
                    PhysicsMass pm = aspect.m_Mass.ValueRO;
                    var cOmWS = pm.GetCenterOfMassWorldSpace(aspect.Scale, aspect.Position, aspect.Rotation);
                    Assert.AreEqual(math.all(cOmWS == aspect.CenterOfMassWorldSpace), true);
                }
            }
        }

        #endregion
    }
}
