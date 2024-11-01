using NUnit.Framework;
using Unity.Collections;
using Unity.Transforms;
using UnityEditor;

namespace Unity.Entities.Editor.Tests
{
    sealed class HierarchyTests
    {
        World m_World;

        Hierarchy m_Hierarchy;

        [SetUp]
        public void SetUp()
        {
            m_World = new World("Hierarchy World");
            m_Hierarchy = new Hierarchy(m_World, Allocator.Persistent, DataMode.Disabled)
            {
                Configuration = new HierarchyConfiguration
                {
                    UpdateMode = Hierarchy.UpdateModeType.Synchronous
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            m_World.Dispose();
            m_World = null;

            m_Hierarchy.Dispose();
            m_Hierarchy = null;
        }

        [Test]
        public void Update_WhenParentIsNull_DoesNotThrow()
        {
            m_World.EntityManager.CreateEntity(ComponentType.ReadOnly<Parent>());

            Assert.DoesNotThrow(() =>
            {
                m_Hierarchy.Update(true);
            });
        }

        [Test]
        public void SetWorldAndUpdate_InExclusiveTransaction_DoesNotThrow()
        {
            using var world = new World("Transaction World");
            world.EntityManager.BeginExclusiveEntityTransaction();
            Assert.DoesNotThrow(() =>
            {
                m_Hierarchy.SetWorld(world);
                m_Hierarchy.Update(true);
            });
        }
    }
}
