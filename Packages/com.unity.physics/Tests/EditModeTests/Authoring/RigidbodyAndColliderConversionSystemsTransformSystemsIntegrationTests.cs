using System;
using Unity.Entities;
using NUnit.Framework;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityAssert = UnityEngine.Assertions.Assert;

namespace Unity.Physics.Tests.Authoring
{
    class RigidbodyAndColliderConversionSystemsTransformSystemsIntegrationTests : BaseHierarchyConversionTest
    {
        [TestCaseSource(nameof(k_ExplicitRigidbodyHierarchyTestCases))]
        public void ConversionSystems_WhenChildGOHasExplicitRigidbody_EntityIsInExpectedHierarchyLocation(
            BodyMotionType motionType, EntityQueryDesc expectedQuery
        )
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] { typeof(UnityEngine.Rigidbody), typeof(UnityEngine.BoxCollider) }
            );
            Child.GetComponent<UnityEngine.Rigidbody>().isKinematic = motionType != BodyMotionType.Dynamic;
            Child.gameObject.isStatic = motionType == BodyMotionType.Static;

            TransformConversionUtils.ConvertHierarchyAndUpdateTransformSystemsVerifyEntityExists(Root, expectedQuery);
        }

        // Test that the mass properties are unscaled and as expected when the game object is not scaled.
        [Test]
        public void ConversionSystems_WhenChildGOIsNotScaled_MassPropertiesAreNotScaled()
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] {typeof(UnityEngine.Rigidbody), typeof(UnityEngine.SphereCollider) }
            );

            var rigidBody = Child.GetComponent<Rigidbody>();
            rigidBody.mass = 42;
            var sphereCollider = Child.GetComponent<UnityEngine.SphereCollider>();
            sphereCollider.radius = 1;

            var expectedMassProperties = PhysicsMass.CreateDynamic(MassProperties.UnitSphere, rigidBody.mass);
            var actualMassProperties = TransformConversionUtils.ConvertHierarchyAndUpdateTransformSystems<PhysicsMass>(Root);

            // we expect the mass properties to correspond to the mass properties of a unit sphere
            UnityAssert.AreApproximatelyEqual(actualMassProperties.InverseMass, expectedMassProperties.InverseMass);
            Assert.That(actualMassProperties.InverseInertia, Is.PrettyCloseTo(expectedMassProperties.InverseInertia));
        }

        // Test that the mass properties are scaled when the game object is scaled.
        [Test]
        public void ConversionSystems_WhenChildGOIsScaled_MassPropertiesAreScaled()
        {
            CreateHierarchy(
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                new[] {typeof(UnityEngine.Rigidbody), typeof(UnityEngine.SphereCollider) }
            );

            // scale the game objects
            TransformHierarchyNodes();

            var rigidBody = Child.GetComponent<Rigidbody>();
            rigidBody.mass = 42;
            var sphereCollider = Child.GetComponent<UnityEngine.SphereCollider>();
            sphereCollider.radius = 1;

            var unscaledMassProperties = PhysicsMass.CreateDynamic(MassProperties.UnitSphere, rigidBody.mass);
            var actualMassProperties = TransformConversionUtils.ConvertHierarchyAndUpdateTransformSystems<PhysicsMass>(Root);

            // we expect the mass to be unaffected by the scale
            UnityAssert.AreApproximatelyEqual(actualMassProperties.InverseMass, unscaledMassProperties.InverseMass);
            // we expect the inertia tensor to be affected by the scale, and not to match the unit sphere inertia tensor despite
            // the (unscaled) sphere collider component being set up as a unit sphere.
            Assert.That(actualMassProperties.InverseInertia, Is.Not.PrettyCloseTo(unscaledMassProperties.InverseInertia));
        }
    }
}
