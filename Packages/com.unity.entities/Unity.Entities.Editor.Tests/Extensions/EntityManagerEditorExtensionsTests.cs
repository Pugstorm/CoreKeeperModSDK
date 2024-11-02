using System;
using NUnit.Framework;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    class EntityManagerEditorExtensionsTests
    {
        World m_World;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_World = new World("SafeExists");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            m_World.Dispose();
        }

        [Test]
        public void EntityManager_WhenAskingIfItExists_WillNotThrowExceptions()
        {
            Assert.Throws<ArgumentException>(() => m_World.EntityManager.Exists(new Entity {Index = -1, Version = 0}));
            Assert.DoesNotThrow(() => m_World.EntityManager.SafeExists(new Entity {Index = -1, Version = 0}));

#if !ENTITY_STORE_V1
            Assert.Throws<ArgumentException>(() => m_World.EntityManager.Exists(new Entity { Index = int.MaxValue, Version = 0 }));
            Assert.DoesNotThrow(() => m_World.EntityManager.SafeExists(new Entity { Index = int.MaxValue, Version = 0 }));
#else
            Assert.Throws<ArgumentException>(() => m_World.EntityManager.Exists(new Entity {Index = m_World.EntityManager.EntityCapacity + 1, Version = 0}));
            Assert.DoesNotThrow(() => m_World.EntityManager.SafeExists(new Entity {Index = m_World.EntityManager.EntityCapacity + 1, Version = 0}));
#endif
        }
    }
}
