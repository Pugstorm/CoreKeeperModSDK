using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    sealed class HierarchyNameStoreTests
    {
        HierarchyNameStore m_Store;

        [SetUp]
        public void Setup()
        {
            m_Store = new HierarchyNameStore(Allocator.Persistent);
        }

        [TearDown]
        public void Teardown()
        {
            m_Store.Dispose();
        }

        [Test]
        public void SetName_WhenNameDoesNotExist_AddNewName()
        {
            var handle = new HierarchyNodeHandle(NodeKind.GameObject, 1, 1);
            m_Store.SetName(handle, "My Entity");

            FixedString64Bytes nodeName = default;
            m_Store.GetName(handle, ref nodeName);
            Assert.That(nodeName.ToString(), Is.EqualTo("My Entity"));
        }

        [Test]
        public void SetName_WhenNameAlreadyExist_UpdatesName()
        {
            var handle = new HierarchyNodeHandle(NodeKind.GameObject, 1, 1);
            m_Store.SetName(handle, "My Entity");
            m_Store.SetName(handle, "My Renamed Entity");

            FixedString64Bytes nodeName = default;
            m_Store.GetName(handle, ref nodeName);
            Assert.That(nodeName.ToString(), Is.EqualTo("My Renamed Entity"));
        }

        [Test]
        public void SetName_WhenNameIsTooLong_DoesNotThrowAndTruncate()
        {
            var handle = new HierarchyNodeHandle(NodeKind.GameObject, 1, 1);
            Assert.DoesNotThrow(() => m_Store.SetName(handle, "My extremely long entity name that is over the FixedString64Bytes capacity"));

            FixedString64Bytes nodeName = default;
            m_Store.GetName(handle, ref nodeName);
            Assert.That(nodeName.ToString(), Is.EqualTo("My extremely long entity name that is over the FixedString64B"));
        }
    }
}
