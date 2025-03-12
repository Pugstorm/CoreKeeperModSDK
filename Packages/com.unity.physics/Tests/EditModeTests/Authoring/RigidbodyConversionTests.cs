using System;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Unity.Physics.Tests.Authoring
{
    class RigidbodyConversionTests : BaseHierarchyConversionTest
    {
        // Make sure the Rigidbody mass property is converted to a PhysicsMass component, with and without a collider
        [TestCase(true)]
        [TestCase(false)]
        public void RigidbodyConversion_Mass(bool withCollider)
        {
            CreateHierarchy(withCollider ? new[] {typeof(Rigidbody), typeof(BoxCollider)} : new[] {typeof(Rigidbody)},
                Array.Empty<Type>(), Array.Empty<Type>());

            var rb = Root.GetComponent<Rigidbody>();
            var expectedMass = rb.mass + 42f;
            rb.mass = expectedMass;

            TestConvertedData<PhysicsMass>(mass =>
            {
                Assume.That(mass.InverseMass, Is.EqualTo(1 / expectedMass).Using(FloatEqualityComparer.Instance));
                if (!withCollider)
                {
                    // Spot check the inertia tensor for the default case without collider.
                    // In this case we expect an inertia tensor of a unit sphere, scaled by the mass.
                    var expectedInertiaTensor = expectedMass * MassProperties.UnitSphere.MassDistribution.InertiaTensor;
                    Assume.That((Vector3)mass.InverseInertia, Is.EqualTo(new Vector3(1f / expectedInertiaTensor.x, 1f / expectedInertiaTensor.y, 1f / expectedInertiaTensor.z)).Using(Vector3EqualityComparer.Instance));
                }
            });
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void RigidbodyConversion_AutomaticMassProperties(bool automaticCenterOfMass, bool automaticInertiaTensor)
        {
            CreateHierarchy(new[] {typeof(Rigidbody)}, new[] {typeof(BoxCollider)}, Array.Empty<Type>());

            // enable or disable automatic mass properties computation
            var rb = Root.GetComponent<Rigidbody>();
            rb.automaticCenterOfMass = automaticCenterOfMass;
            rb.automaticInertiaTensor = automaticInertiaTensor;

            // set non-default scalar mass (Note: can not be automatically calculated)
            var expectedMass = rb.mass + 42f;
            rb.mass = expectedMass;

            // move and resize the child box collider of the root rigid body to induce a different center of mass and inertia tensor
            var boxGameObject = Parent;
            boxGameObject.transform.localPosition += new Vector3(1, 2, 3);
            boxGameObject.transform.localRotation *= Quaternion.Euler(20, 30, 42);
            var boxCollider = Parent.GetComponent<UnityEngine.BoxCollider>();
            boxCollider.size += new Vector3(3, 1, 2);

            var expectedBoxMassProperties = MassProperties.CreateBox(boxCollider.size);
            var expectedInertiaTensor = expectedMass * (automaticInertiaTensor ? (Vector3)expectedBoxMassProperties.MassDistribution.InertiaTensor : rb.inertiaTensor);
            var expectedCenterOfMass = automaticCenterOfMass ? boxGameObject.transform.localPosition : rb.centerOfMass;
            var expectedInertiaOrientation = automaticInertiaTensor ? boxGameObject.transform.localRotation : rb.inertiaTensorRotation;

            TestConvertedData<PhysicsMass>(mass =>
            {
                Assume.That(mass.InverseMass, Is.EqualTo(1 / expectedMass).Using(FloatEqualityComparer.Instance));
                Assume.That((Vector3)mass.CenterOfMass, Is.EqualTo(expectedCenterOfMass).Using(Vector3EqualityComparer.Instance));
                Assume.That((Quaternion)mass.InertiaOrientation, Is.EqualTo(expectedInertiaOrientation).Using(QuaternionEqualityComparer.Instance));
                Assume.That((Vector3)mass.InverseInertia, Is.EqualTo(new Vector3(1f / expectedInertiaTensor.x, 1f / expectedInertiaTensor.y, 1f / expectedInertiaTensor.z)).Using(Vector3EqualityComparer.Instance));
            });

            TestConvertedData<PhysicsMass>(mass => Assert.That(mass.InverseMass, Is.EqualTo(1 / expectedMass).Using(FloatEqualityComparer.Instance)));
        }

        // Make sure a Rigidbody's overridden center of mass and inertia tensor properties are properly migrated to the
        // PhysicsMass component with and without a collider being present.
        [TestCase(true)]
        [TestCase(false)]
        public void RigidbodyConversion_MassPropertiesOverride(bool withCollider)
        {
            CreateHierarchy(withCollider ? new[] {typeof(Rigidbody), typeof(BoxCollider)} : new[] {typeof(Rigidbody)},
                Array.Empty<Type>(), Array.Empty<Type>());

            var rb = Root.GetComponent<Rigidbody>();
            rb.automaticInertiaTensor = false;
            rb.automaticCenterOfMass = false;
            var expectedCoM = rb.centerOfMass + new Vector3(-1, 2, 42);
            rb.centerOfMass = expectedCoM;
            var expectedInertiaTensor = rb.inertiaTensor + new Vector3(42, 1, 2);
            rb.inertiaTensor = expectedInertiaTensor;
            var expectedInertiaTensorRotation = rb.inertiaTensorRotation * Quaternion.Euler(-1, 2, 42);
            rb.inertiaTensorRotation = expectedInertiaTensorRotation;

            TestConvertedData<PhysicsMass>(mass =>
            {
                Assume.That((Vector3)mass.CenterOfMass, Is.EqualTo(expectedCoM));
                Assume.That((Quaternion)mass.InertiaOrientation, Is.EqualTo(expectedInertiaTensorRotation).Using(QuaternionEqualityComparer.Instance));
                Assume.That((Vector3)mass.InverseInertia, Is.EqualTo(new Vector3(1f / expectedInertiaTensor.x, 1f / expectedInertiaTensor.y, 1f / expectedInertiaTensor.z)).Using(Vector3EqualityComparer.Instance));
            });
        }

        // Make sure the Rigidbody drag property is converted to PhysicsDamping.Linear
        [Test]
        public void RigidbodyConversion_Damping()
        {
            CreateHierarchy(new[] { typeof(Rigidbody) }, Array.Empty<Type>(), Array.Empty<Type>());
#if UNITY_2023_3_OR_NEWER
            Root.GetComponent<Rigidbody>().linearDamping = 0.5f;
#else
            Root.GetComponent<Rigidbody>().drag = 0.5f;
#endif

            TestConvertedData<PhysicsDamping>(damping => Assert.That(damping.Linear, Is.EqualTo(0.5f)));
        }

        // Make sure a kinematic body does contain a PhysicsGravityFactor component with factor zero by default
        [Test]
        public void RigidbodyConversion_KinematicProducesGravityFactor()
        {
            CreateHierarchy(new[] { typeof(Rigidbody) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<Rigidbody>().isKinematic = true;

            TestConvertedData<PhysicsGravityFactor>(gravity => Assert.That(gravity.Value, Is.EqualTo(0.0f)));
        }

        // Make sure a non-kinematic body does not contain a PhysicsGravityFactor component by default
        [Test]
        public void RigidbodyConversion_NotKinematicDoesNotProduceGravityFactor()
        {
            CreateHierarchy(new[] { typeof(Rigidbody) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<Rigidbody>().isKinematic = false;

            VerifyNoDataProduced<PhysicsGravityFactor>();
        }

        // Make sure by default a rigid body does not contain a collider
        [Test]
        public void RigidbodyConversion_NoCollider()
        {
            CreateHierarchy(new[] { typeof(Rigidbody) }, Array.Empty<Type>(), Array.Empty<Type>());

            VerifyNoDataProduced<PhysicsCollider>();
        }

        static readonly TestCaseData[] k_EntityBufferTestCases =
        {
            new TestCaseData(
                new[] { typeof(Rigidbody), typeof(BoxCollider) },   // parent components
                Array.Empty<Type>(),                                // child components
                false,                                              // expect entity buffer
                0                                                   // expected buffer size
            ).SetName("RigidbodyConversion_ExpectEntityBufferOrNot_WithSingleCollider"),
            new TestCaseData(
                new[] { typeof(Rigidbody) },
                new[] { typeof(BoxCollider) },
                true,
                1
            ).SetName("RigidbodyConversion_ExpectEntityBufferOrNot_WithSingleColliderInDescendent"),
            new TestCaseData(
                new[] { typeof(Rigidbody), typeof(BoxCollider), typeof(BoxCollider) },
                Array.Empty<Type>(),
                true,
                2
            ).SetName("RigidbodyConversion_ExpectEntityBufferOrNot_WithMultipleColliders"),
            new TestCaseData(
                new[] { typeof(Rigidbody) },
                new[] { typeof(BoxCollider), typeof(BoxCollider) },
                true,
                2
            ).SetName("RigidbodyConversion_ExpectEntityBufferOrNot_WithMultipleCollidersInDescendent"),
            new TestCaseData(
                new[] { typeof(Rigidbody), typeof(BoxCollider) },
                new[] { typeof(BoxCollider), typeof(BoxCollider) },
                true,
                3
            ).SetName("RigidbodyConversion_ExpectEntityBufferOrNot_WithMultipleCollidersInHierarchy")
        };

        // Make sure there is a PhysicsColliderKeyEntityPair buffer in a rigid body only when needed, and it has the right size if present.
        [TestCaseSource(nameof(k_EntityBufferTestCases))]
        public void RigidbodyConversion_ExpectEntityBuffer(Type[] parentComponentTypes, Type[] childComponentTypes, bool expectEntityBuffer, int expectedBufferSize)
        {
            CreateHierarchy(parentComponentTypes, childComponentTypes, Array.Empty<Type>());
            TestConvertedData<PhysicsCollider>((w, e, c) =>
            {
                Assert.That(e.Length, Is.EqualTo(1));
                var entity = e[0];
                var hasBuffer = w.EntityManager.HasBuffer<PhysicsColliderKeyEntityPair>(entity);
                Assert.AreEqual(expectEntityBuffer, hasBuffer);
                if (hasBuffer)
                {
                    var buffer = w.EntityManager.GetBuffer<PhysicsColliderKeyEntityPair>(entity);
                    Assert.That(buffer.Length, Is.EqualTo(expectedBufferSize));
                }
            }, 1);
        }

        // Make sure we get the correct mass (infinite) with kinematic bodies
        [Test]
        public void RigidbodyConversion_KinematicCausesInfiniteMass()
        {
            CreateHierarchy(new[] { typeof(Rigidbody) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<Rigidbody>().isKinematic = true;
            Root.GetComponent<Rigidbody>().mass = 50f;

            // zero inverse mass corresponds to infinite mass
            TestConvertedData<PhysicsMass>(mass => Assert.That(mass.InverseMass, Is.EqualTo(0.0f)));
        }

        // Make sure we get the correct mass with non-kinematic bodies
        [Test]
        public void RigidbodyConversion_NotKinematicCausesFiniteMass()
        {
            CreateHierarchy(new[] { typeof(Rigidbody) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<Rigidbody>().isKinematic = false;
            Root.GetComponent<Rigidbody>().mass = 50f;

            TestConvertedData<PhysicsMass>(mass => Assert.That(mass.InverseMass, Is.EqualTo(0.02f)));
        }

        static Vector3[] GetDifferentScales()
        {
            return new[]
            {
                new Vector3(1.0f, 1.0f, 1.0f),
                new Vector3(0.542f, 0.542f, 0.542f),
                new Vector3(0.42f, 1.1f, 2.1f),
            };
        }

        // Make sure we obtain the user-specified mass properties after baking and in simulation for a rigid body
        // when uniformly scaling the game object at edit-time.
        [Test]
        public void RigidbodyConversion_WithDifferentScales_EditTimeMassIsPreserved([Values] bool massOverride, [Values] bool withCollider, [ValueSource(nameof(GetDifferentScales))] Vector3 scale)
        {
            CreateHierarchy(new[] { typeof(Rigidbody) }, Array.Empty<Type>(), Array.Empty<Type>());
            var rb = Root.GetComponent<Rigidbody>();

            rb.isKinematic = false;

            const float expectedMass = 42f;
            var expectedCOM = new Vector3(1f, 2f, 3f);
            var expectedInertia = new Vector3(2f, 3f, 4f);
            var expectedInertiaRot = Quaternion.Euler(10f, 20f, 30f);

            rb.mass = expectedMass;
            MassProperties automaticMassProperties;

            if (withCollider)
            {
                var boxCollider = Root.AddComponent<UnityEngine.BoxCollider>();
                var boxColliderSize = new float3(3, 4, 5);
                boxCollider.size = boxColliderSize;

                // We expect the mass properties to correspond to a scaled version of the box based on the provided scale.
                automaticMassProperties = MassProperties.CreateBox(boxColliderSize * scale);
            }
            else
            {
                // We expect the mass properties to correspond to a scaled version of the default unit sphere mass properties.

                // Special case: Without a collider, we use default mass properties. In this case, when a non-uniform scale is
                // present, we don't bake it into the collider and consequently don't scale the mass properties either.
                var radius = 1f;
                if (!float4x4.Scale(scale).HasNonUniformScale())
                {
                    radius *= scale[0];
                }
                automaticMassProperties = MassProperties.CreateSphere(radius);
            }

            if (massOverride)
            {
                rb.automaticCenterOfMass = false;
                rb.automaticInertiaTensor = false;
                rb.centerOfMass = expectedCOM;
                rb.inertiaTensor = expectedInertia;
                rb.inertiaTensorRotation = expectedInertiaRot;
            }
            else
            {
                expectedCOM = automaticMassProperties.MassDistribution.Transform.pos;
                expectedInertia = automaticMassProperties.MassDistribution.InertiaTensor;
                expectedInertiaRot = automaticMassProperties.MassDistribution.Transform.rot;
            }

            // scale the object
            Root.transform.localScale = scale;

            TestExpectedMass(expectedMass, expectedCOM, expectedInertia, expectedInertiaRot);
        }

        // Make sure we get the correct mass with non-kinematic bodies
        [Test]
        public void RigidbodyConversion_GOIsActive_BodyIsConverted()
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { typeof(Rigidbody) });

            // conversion presumed to create PhysicsVelocity under default conditions
            TestConvertedData<PhysicsVelocity>(v => Assert.That(v, Is.EqualTo(default(PhysicsVelocity))));
        }

        [Test]
        public void RigidbodyConversion_GOIsInactive_BodyIsNotConverted([Values] Node inactiveNode)
        {
            CreateHierarchy(Array.Empty<Type>(), Array.Empty<Type>(), new[] { typeof(Rigidbody) });
            GetNode(inactiveNode).SetActive(false);
            var numInactiveNodes = Root.GetComponentsInChildren<Transform>(true).Count(t => t.gameObject.activeSelf);
            Assume.That(numInactiveNodes, Is.EqualTo(2));

            // conversion presumed to create PhysicsVelocity under default conditions
            // covered by corresponding test RigidbodyConversion_GOIsActive_BodyIsConverted
            VerifyNoDataProduced<PhysicsVelocity>();
        }

        // Make sure we get the default physics world index in a default rigid body
        [Test]
        public void RigidbodyConversion_DefaultPhysicsWorldIndex()
        {
            CreateHierarchy(new[] { typeof(Rigidbody) }, Array.Empty<Type>(), Array.Empty<Type>());

            // Note: testing for presence of PhysicsVelocity component which is expected for a default rigid body
            TestConvertedSharedData<PhysicsVelocity, PhysicsWorldIndex>(k_DefaultWorldIndex);
        }

        // Make sure we get the default physics world index when using a default PhysicsWorldIndexAuthoring component
        [Test]
        public void RigidbodyConversion_WithPhysicsWorldIndexAuthoring_DefaultPhysicsWorldIndex()
        {
            CreateHierarchy(new[] { typeof(Rigidbody), typeof(PhysicsWorldIndexAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());

            // Note: testing for presence of PhysicsVelocity component which is expected for a default rigid body
            TestConvertedSharedData<PhysicsVelocity, PhysicsWorldIndex>(k_DefaultWorldIndex);
        }

        // Make sure we get the correct physics world index when using the PhysicsWorldIndexAuthoring component
        [Test]
        public void RigidbodyConversion_WithPhysicsWorldIndexAuthoring_NonDefaultPhysicsWorldIndex()
        {
            CreateHierarchy(new[] { typeof(Rigidbody), typeof(PhysicsWorldIndexAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());
            Root.GetComponent<PhysicsWorldIndexAuthoring>().WorldIndex = 3;

            // Note: testing for presence of PhysicsVelocity component which is expected for a default rigid body
            TestConvertedSharedData<PhysicsVelocity, PhysicsWorldIndex>(new PhysicsWorldIndex(3));
        }

        // Make sure there is no leftover baking data when using the PhysicsWorldIndexAuthoring component
        [Test]
        public void RigidbodyConversion_WithPhysicsWorldIndexAuthoring_NoBakingDataRemainsAfterBaking()
        {
            CreateHierarchy(new[] { typeof(Rigidbody), typeof(PhysicsWorldIndexAuthoring) }, Array.Empty<Type>(), Array.Empty<Type>());
            VerifyNoDataProduced<PhysicsWorldIndexBakingData>();
        }
    }
}
