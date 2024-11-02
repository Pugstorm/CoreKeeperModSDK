using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Tests.Types
{
    [TestFixture]
    class ComponentTypeTests : ECSTestsFixture
    {
        struct MockComponentData : IComponentData {}

        [Test]
        public void SameComponent_SameAccessMode_AreEqual()
        {
            var t1 = new ComponentType(typeof(Entity), ComponentType.AccessMode.ReadOnly);
            var t2 = new ComponentType(typeof(Entity), ComponentType.AccessMode.ReadOnly);

            Assert.IsTrue(t1 == t2);
            Assert.IsFalse(t1 != t2);

            Assert.IsTrue(t1.Equals(t2));
            Assert.AreEqual(t1.GetHashCode(), t2.GetHashCode());
        }

        [Test]
        public void DifferentComponents_SameAccessMode_AreNotEqual()
        {
            var t1 = new ComponentType(typeof(MockComponentData), ComponentType.AccessMode.ReadOnly);
            var t2 = new ComponentType(typeof(Entity), ComponentType.AccessMode.ReadOnly);

            Assert.IsTrue(t1 != t2);
            Assert.IsFalse(t1 == t2);

            Assert.IsFalse(t1.Equals(t2));
            Assert.AreNotEqual(t1.GetHashCode(), t2.GetHashCode());
        }

        [Test]
        public void SameComponent_DifferentAccessModes_AreNotEqual()
        {
            var t1 = new ComponentType(typeof(Entity));
            var t2 = new ComponentType(typeof(Entity), ComponentType.AccessMode.ReadOnly);

            Assert.IsTrue(t1 != t2);
            Assert.IsFalse(t1 == t2);

            Assert.IsFalse(t1.Equals(t2));
            Assert.AreNotEqual(t1.GetHashCode(), t2.GetHashCode());
        }

        [Test]
        public void ChunkComponentCount()
        {
            var types = new ComponentTypeSet();
            Assert.AreEqual(0, types.ChunkComponentCount);
            types = new ComponentTypeSet(ComponentType.ChunkComponent<EcsTestData>(), typeof(EcsTestData2));
            Assert.AreEqual(1, types.ChunkComponentCount);
            types = new ComponentTypeSet(ComponentType.ChunkComponent<EcsTestData>(), ComponentType.ChunkComponent<EcsTestData2>());
            Assert.AreEqual(2, types.ChunkComponentCount);
        }

        struct EcsTestTag1 : IComponentData {};
        struct EcsTestTag2 : IComponentData {};
        struct EcsTestTag3 : IComponentData {};
        struct EcsTestTag4 : IComponentData {};
        struct EcsTestTag5 : IComponentData {};
        struct EcsTestTag6 : IComponentData {};
        struct EcsTestTag7 : IComponentData {};
        struct EcsTestTag8 : IComponentData {};
        struct EcsTestTag9 : IComponentData {};
        struct EcsTestTag10 : IComponentData {};
        struct EcsTestTag11 : IComponentData {};
        struct EcsTestTag12 : IComponentData {};
        struct EcsTestTag13 : IComponentData {};
        struct EcsTestTag14 : IComponentData {};
        struct EcsTestTag15 : IComponentData {};
        struct EcsTestTag16 : IComponentData {};

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires component safety checks")]
        public unsafe void ComponentTypeSet_TooManyTypes_Throws()
        {
            var typesArray = new ComponentType[]{
                typeof(EcsTestTag1), typeof(EcsTestTag2), typeof(EcsTestTag3), typeof(EcsTestTag4),
                typeof(EcsTestTag5), typeof(EcsTestTag6), typeof(EcsTestTag7), typeof(EcsTestTag8),
                typeof(EcsTestTag9), typeof(EcsTestTag10), typeof(EcsTestTag11), typeof(EcsTestTag12),
                typeof(EcsTestTag13), typeof(EcsTestTag14), typeof(EcsTestTag15), typeof(EcsTestTag16),
            };
            Assert.Throws<ArgumentException>(() => { var componentTypes = new ComponentTypeSet(typesArray); });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires component safety checks")]
        public void ComponentTypeSet_DuplicateTypes_Throws()
        {
            Assert.That(() => new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData)), Throws.ArgumentException
                .With.Message.Contains($"ComponentTypes cannot contain duplicate types. Remove all but one occurrence of \"Unity.Entities.Tests.EcsTestData\""));
            Assert.That(() => new ComponentTypeSet(typeof(EcsTestData2), typeof(EcsTestData), typeof(EcsTestData)), Throws.ArgumentException
                .With.Message.Contains($"ComponentTypes cannot contain duplicate types. Remove all but one occurrence of \"Unity.Entities.Tests.EcsTestData\""));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires component safety checks")]
        public void ComponentTypeSet_EmptyList_Works()
        {
            Assert.DoesNotThrow(() => { var _ = new ComponentTypeSet(Array.Empty<ComponentType>()); });
        }
    }
}
