using System;
using NUnit.Framework;

namespace Unity.Entities.Tests
{
    public class DebugEntityTests : ECSTestsFixture
    {
        [Test]
        public void GetAllEntities_WithEmptyEcs()
        {
            var debugEntities = DebugEntity.GetAllEntitiesWithSystems(m_Manager);

            CollectionAssert.IsEmpty(debugEntities);
        }

        [Test]
        public void GetAllEntities_WithEmptyEntity()
        {
            var entity = m_Manager.CreateEntity();

            var debugEntities = DebugEntity.GetAllEntitiesWithSystems(m_Manager);

            EntitiesAssert.AreEqual(
                new[] { new DebugEntity(entity, new DebugComponent {Type = typeof(Simulate), Data = new Simulate()}) },
                debugEntities);
        }

        [Test]
        public void GetAllEntities_WithTaggedEntity()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestTag));

            var debugEntities = DebugEntity.GetAllEntitiesWithSystems(m_Manager);

            EntitiesAssert.AreEqual(
                new[] { new DebugEntity(entity,
                    new DebugComponent { Type = typeof(EcsTestTag), Data = new EcsTestTag() },
                    new DebugComponent {Type = typeof(Simulate), Data = new Simulate()}) },
                debugEntities);
        }

        [Test]
        public void GetAllEntities_WithSharedTagEntity()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestSharedTag));

            var debugEntities = DebugEntity.GetAllEntitiesWithSystems(m_Manager);

            EntitiesAssert.AreEqual(
                new[] { new DebugEntity(entity,
                    new DebugComponent {Type = typeof(Simulate), Data = new Simulate()},
                    new DebugComponent { Type = typeof(EcsTestSharedTag), Data = new EcsTestSharedTag() }) },
                debugEntities);
        }

        [Test]
        public void GetAllEntities_WithComponentData()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddComponentData(entity, new EcsTestData(5));

            var debugEntities = DebugEntity.GetAllEntitiesWithSystems(m_Manager);

            EntitiesAssert.AreEqual(
                new[] { new DebugEntity(entity,
                    new DebugComponent { Type = typeof(EcsTestData), Data = new EcsTestData(5)},
                    new DebugComponent {Type = typeof(Simulate), Data = new Simulate()}) },
                debugEntities);

            EntitiesAssert.AreNotEqual(
                new[] { new DebugEntity(entity,
                    new DebugComponent { Type = typeof(EcsTestData), Data = new EcsTestData(6)},
                    new DebugComponent {Type = typeof(Simulate), Data = new Simulate()}) },
                debugEntities);
        }

        [Test]
        public void GetAllEntities_WithSharedComponentData()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp(5));

            var debugEntities = DebugEntity.GetAllEntitiesWithSystems(m_Manager);

            EntitiesAssert.AreEqual(
                new[] { new DebugEntity(entity,
                    new DebugComponent {Type = typeof(Simulate), Data = new Simulate()},
                    new DebugComponent { Type = typeof(EcsTestSharedComp), Data = new EcsTestSharedComp(5)}) },
                debugEntities);

            EntitiesAssert.AreNotEqual(
                new[] { new DebugEntity(entity,
                    new DebugComponent {Type = typeof(Simulate), Data = new Simulate()},
                    new DebugComponent { Type = typeof(EcsTestSharedComp), Data = new EcsTestSharedComp(6)}) },
                debugEntities);
        }

        [Test]
        public void GetAllEntities_WithBufferElementData()
        {
            var entity = m_Manager.CreateEntity();
            var buffer = m_Manager.AddBuffer<EcsIntElement>(entity);
            buffer.Add(1);
            buffer.Add(5);
            buffer.Add(9);

            var debugEntities = DebugEntity.GetAllEntitiesWithSystems(m_Manager);

            EntitiesAssert.AreEqual(
                new[] { new DebugEntity(entity,
                    new DebugComponent { Type = typeof(EcsIntElement), Data = new EcsIntElement[] { 1, 5, 9 } },
                    new DebugComponent {Type = typeof(Simulate), Data = new Simulate()}) },
                debugEntities);
        }

        class TestClassComponent : UnityEngine.Object
        {
            public int Value;

            public override bool Equals(object obj) => obj is TestClassComponent other && other.Value == Value;
            public override int GetHashCode() => throw new InvalidOperationException();
        }

        [Test]
        public void GetAllEntities_WithComponentObject()
        {
            var entity = m_Manager.CreateEntity();
            var component = new TestClassComponent { Value = 5 };
            m_Manager.AddComponentObject(entity, component);

            var debugEntities = DebugEntity.GetAllEntitiesWithSystems(m_Manager);

            EntitiesAssert.AreEqual(
                new[] { new DebugEntity(entity,
                    new DebugComponent { Type = typeof(TestClassComponent), Data = component },
                    new DebugComponent {Type = typeof(Simulate), Data = new Simulate()}) },
                debugEntities);

            // currently we are doing Equals comparisons, so validate it
            EntitiesAssert.AreEqual(
                new[] { new DebugEntity(entity,
                    new DebugComponent { Type = typeof(TestClassComponent), Data = new TestClassComponent { Value = 5 } },
                    new DebugComponent {Type = typeof(Simulate), Data = new Simulate()}) },
                debugEntities);
            EntitiesAssert.AreNotEqual(
                new[] { new DebugEntity(entity,
                    new DebugComponent { Type = typeof(TestClassComponent), Data = new TestClassComponent { Value = 6 } },
                    new DebugComponent {Type = typeof(Simulate), Data = new Simulate()}) },
                debugEntities);
        }
    }

    public class DebugComponentTests
    {
        [Test]
        public void ToString_WithSmallMaxLen_TruncatesWithoutEllipsis()
        {
            Assert.AreEqual("String=",       new DebugComponent { Data = ""        }.ToString(0));

            Assert.AreEqual("String=",       new DebugComponent { Data = "abc"     }.ToString(0));
            Assert.AreEqual("String=a",      new DebugComponent { Data = "abc"     }.ToString(1));
            Assert.AreEqual("String=ab",     new DebugComponent { Data = "abc"     }.ToString(2));

            Assert.AreEqual("String=",       new DebugComponent { Data = "abcdefg" }.ToString(0));
            Assert.AreEqual("String=a",      new DebugComponent { Data = "abcdefg" }.ToString(1));
            Assert.AreEqual("String=ab",     new DebugComponent { Data = "abcdefg" }.ToString(2));
            Assert.AreEqual("String=abc",    new DebugComponent { Data = "abcdefg" }.ToString(3));
        }

        [Test]
        public void ToString_WithNormalMaxLen_TruncatesWithEllipsis()
        {
            Assert.AreEqual("String=a...",   new DebugComponent { Data = "abcdefg" }.ToString(4));
            Assert.AreEqual("String=ab...",  new DebugComponent { Data = "abcdefg" }.ToString(5));
            Assert.AreEqual("String=abc...", new DebugComponent { Data = "abcdefg" }.ToString(6));
        }

        [Test]
        public void ToString_WithGreaterOrEqualOrDefaultMaxLen_DoesNotTruncate()
        {
            Assert.AreEqual("String=",        new DebugComponent { Data = ""        }.ToString());
            Assert.AreEqual("String=",        new DebugComponent { Data = ""        }.ToString(1));

            Assert.AreEqual("String=abc",     new DebugComponent { Data = "abc"     }.ToString());
            Assert.AreEqual("String=abc",     new DebugComponent { Data = "abc"     }.ToString(3));
            Assert.AreEqual("String=abc",     new DebugComponent { Data = "abc"     }.ToString(4));

            Assert.AreEqual("String=abcdefg", new DebugComponent { Data = "abcdefg" }.ToString());
            Assert.AreEqual("String=abcdefg", new DebugComponent { Data = "abcdefg" }.ToString(7));
            Assert.AreEqual("String=abcdefg", new DebugComponent { Data = "abcdefg" }.ToString(8));
        }
    }
}
