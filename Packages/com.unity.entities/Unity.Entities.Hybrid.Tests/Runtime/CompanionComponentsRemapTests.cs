using System;
using NUnit.Framework;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public class CompanionComponentsRemapTests : ECSTestsFixture
    {
        class TestCompanionComponent : MonoBehaviour
        {
        }

        [Test]
        public unsafe void CompanionComponents_WillThrow_IfRemapped()
        {
            var gameObject = new GameObject("TestCompanionComponent", typeof(TestCompanionComponent));

            var managedObjectRemap = new ManagedObjectRemap();
            Assert.Throws<ArgumentException>(() =>
            {
                var local = (object) gameObject;
                managedObjectRemap.RemapEntityReferences(ref local, null);
            });
        }

        [Test]
        public unsafe void CompanionComponents_WillThrow_IfRemappedPrefab()
        {
            var gameObject = new GameObject("TestCompanionComponent", typeof(TestCompanionComponent));

            var managedObjectRemap = new ManagedObjectRemap();
            Assert.Throws<ArgumentException>(() =>
            {
                var local = (object) gameObject;
                managedObjectRemap.RemapEntityReferencesForPrefab(ref local, null, null, 0);
            });
        }
    }
}
