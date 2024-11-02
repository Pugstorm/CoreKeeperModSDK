using System;
using NUnit.Framework;

namespace Unity.Entities.Tests
{
    // tests for internal assertion methods
    public class InternalAssertsTests : ECSTestsFixture
    {
        [Test]
        unsafe public void AssertCanAddComponent_SingleComponent()
        {
            var access = m_Manager.GetCheckedEntityDataAccess();
            var entity = m_Manager.CreateEntity();

            // Add component should be fine for existing entity.
            Assert.DoesNotThrow(() =>
            {
                access->EntityComponentStore->AssertCanAddComponent(entity, typeof(EcsTestData));
            });

            // Add component valid if entity already has that component.
            Assert.DoesNotThrow(() =>
            {
                access->EntityComponentStore->AssertCanAddComponent(entity, typeof(EcsTestData));
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        unsafe public void AssertCanAddComponent_SingleComponent_InvalidCases()
        {
            var access = m_Manager.GetCheckedEntityDataAccess();
            var entity = m_Manager.CreateEntity();

            // Add component should be invalid for Entity.Null.
            Assert.Throws<InvalidOperationException>(() =>
            {
                access->EntityComponentStore->AssertCanAddComponent(Entity.Null, typeof(EcsTestData));
            });

            // Add component should be invalid for Entity type.
            Assert.Throws<ArgumentException>(() =>
            {
                access->EntityComponentStore->AssertCanAddComponent(entity, ComponentType.ReadWrite<Entity>());
            });

            // Add component should be invalid for adding one too many shared component.
            entity = m_Manager.CreateEntity(
                typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3), typeof(EcsTestSharedComp4),
                typeof(EcsTestSharedComp5), typeof(EcsTestSharedComp6), typeof(EcsTestSharedComp7), typeof(EcsTestSharedComp8),
                typeof(EcsTestSharedComp9), typeof(EcsTestSharedComp10), typeof(EcsTestSharedComp11), typeof(EcsTestSharedComp12),
                typeof(EcsTestSharedComp13), typeof(EcsTestSharedComp14), typeof(EcsTestSharedComp15), typeof(EcsTestSharedComp16));
            Assert.AreEqual(16, EntityComponentStore.kMaxSharedComponentCount, "Update test if this constant changes.");
            Assert.Throws<InvalidOperationException>(() =>
            {
                access->EntityComponentStore->AssertCanAddComponent(entity, typeof(EcsTestSharedComp17));
            });

            // Add component should be invalid for destroyed entity.
            m_Manager.DestroyEntity(entity);
            Assert.Throws<InvalidOperationException>(() =>
            {
                access->EntityComponentStore->AssertCanAddComponent(entity, typeof(EcsTestData));
            });
        }

        [Test]
        unsafe public void AssertCanAddComponent_MultipleComponents()
        {
            var access = m_Manager.GetCheckedEntityDataAccess();

            var entity = m_Manager.CreateEntity();
            var types = new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2));

            // Add component should be fine for existing entity.
            Assert.DoesNotThrow(() =>
            {
                access->EntityComponentStore->AssertCanAddComponents(entity, types);
            });

            // Add component valid if entity already has the components.
            Assert.DoesNotThrow(() =>
            {
                access->EntityComponentStore->AssertCanAddComponents(entity, types);
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        unsafe public void AssertCanAddComponent_MultipleComponents_InvalidCases()
        {
            var access = m_Manager.GetCheckedEntityDataAccess();

            var entity = m_Manager.CreateEntity();
            var types = new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2));

            // Add component should be invalid for Entity.Null.
            Assert.Throws<InvalidOperationException>(() =>
            {
                access->EntityComponentStore->AssertCanAddComponents(Entity.Null, types);
            });

            // Add component should be invalid for Entity type.
            types = new ComponentTypeSet(typeof(EcsTestData), ComponentType.ReadWrite<Entity>());
            Assert.Throws<ArgumentException>(() =>
            {
                access->EntityComponentStore->AssertCanAddComponents(entity, types);
            });

            // Add component should be invalid for adding one too many shared component.
            types = new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestSharedComp17));
            entity = m_Manager.CreateEntity(
                typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3), typeof(EcsTestSharedComp4),
                typeof(EcsTestSharedComp5), typeof(EcsTestSharedComp6), typeof(EcsTestSharedComp7), typeof(EcsTestSharedComp8),
                typeof(EcsTestSharedComp9), typeof(EcsTestSharedComp10), typeof(EcsTestSharedComp11), typeof(EcsTestSharedComp12),
                typeof(EcsTestSharedComp13), typeof(EcsTestSharedComp14), typeof(EcsTestSharedComp15), typeof(EcsTestSharedComp16));
            Assert.AreEqual(16, EntityComponentStore.kMaxSharedComponentCount, "Update test if this constant changes.");
            Assert.Throws<InvalidOperationException>(() =>
            {
                access->EntityComponentStore->AssertCanAddComponents(entity, types);
            });

            // Add component should be invalid for destroyed entity.
            m_Manager.DestroyEntity(entity);
            Assert.Throws<InvalidOperationException>(() =>
            {
                access->EntityComponentStore->AssertCanAddComponents(entity, types);
            });
        }
    }
}
