#if !DOTS_DISABLE_DEBUG_NAMES
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    class EntityFieldTests
    {
        World m_World;
        Entity m_Entity;
        Inspectors.EntityField m_Field;
        Label m_EntityLabel;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_World = new World("Entity Inspector tests");
            m_Entity = m_World.EntityManager.CreateEntity();
            m_Field = new Inspectors.EntityField();
            m_EntityLabel = m_Field.Q<Label>(className: "unity-entity-field__name");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            m_World.EntityManager.DestroyEntity(m_Entity);
            m_World.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            m_Field.World = null;
            m_Field.value = Entity.Null;
            m_World.EntityManager.SetName(m_Entity, "SomeName");
        }

        [Test]
        public void EntityField_WithNoWorld_HasNoName()
        {
            m_Field.value = m_Entity;
            Assert.That(m_EntityLabel.text, Is.EqualTo($"Entity {{{m_Entity.Index}:{m_Entity.Version}}}"));
        }

        [Test]
        public void EntityField_WithWorld_HasEntityName()
        {
            m_Field.World = m_World;
            m_Field.value = m_Entity;
            Assert.That(m_EntityLabel.text, Is.EqualTo($"SomeName {{{m_Entity.Index}:{m_Entity.Version}}}"));
            m_Field.World = null;
            Assert.That(m_EntityLabel.text, Is.EqualTo($"Entity {{{m_Entity.Index}:{m_Entity.Version}}}"));
        }

        [Test]
        public void EntityField_WithNoName_DisplaysDebugName()
        {
            m_Field.World = m_World;
            var entity = m_World.EntityManager.CreateEntity();
            m_Field.value = entity;
            Assert.That(m_EntityLabel.text, Is.EqualTo($"Entity {{{entity.Index}:{entity.Version}}}"));
            m_World.EntityManager.SetName(entity, "SomeName");
            m_Field.ForceUpdateBindings();
            Assert.That(m_EntityLabel.text, Is.EqualTo($"SomeName {{{entity.Index}:{entity.Version}}}"));
            m_World.EntityManager.DestroyEntity(entity);
        }

        [Test]
        public void EntityField_WithInvalidEntity_ShowInvalidEntity()
        {
            var invalidEntity = new Entity { Index = 234, Version = 123 };
            m_Field.value = invalidEntity;
            Assert.That(m_EntityLabel.text, Is.EqualTo($"Entity {{{invalidEntity.Index}:{invalidEntity.Version}}}"));
            m_Field.World = m_World;
            Assert.That(m_EntityLabel.text, Is.EqualTo($"Invalid (Entity {{{invalidEntity.Index}:{invalidEntity.Version}}})"));
            m_Field.value = Entity.Null;
            Assert.That(m_EntityLabel.text, Is.EqualTo($"None (Entity)"));
        }

        [Test]
        public void EntityField_WhenNameIsUpdated_UpdatesCorrectly()
        {
            m_Field.World = m_World;
            m_Field.value = m_Entity;
            Assert.That(m_EntityLabel.text, Is.EqualTo($"SomeName {{{m_Entity.Index}:{m_Entity.Version}}}"));
            m_World.EntityManager.SetName(m_Entity, "HeyOh!");
            m_Field.ForceUpdateBindings();
            Assert.That(m_EntityLabel.text, Is.EqualTo($"HeyOh! {{{m_Entity.Index}:{m_Entity.Version}}}"));
        }
    }
}
#endif
