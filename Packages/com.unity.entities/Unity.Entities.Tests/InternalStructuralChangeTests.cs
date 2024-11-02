using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Tests
{
    public class InternalStructuralChangeTests : ECSTestsFixture
    {
        [Test]
        public unsafe void AddComponentDuringStructuralChange_NativeArray()
        {
            var access = m_Manager.GetCheckedEntityDataAccess();
            var archetype = m_Manager.CreateArchetype();

            var archetypeChanges = access->BeginStructuralChanges();
            // Test create & add with zero entities
            var entities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(0, ref World.UpdateAllocator);
            access->CreateEntityDuringStructuralChange(archetype, (Entity*)entities.GetUnsafePtr(), 0);
            Assert.DoesNotThrow(() =>
            {
                access->AddComponentDuringStructuralChange(entities, typeof(EcsTestData));
            });

            // Test create & add with 3 entities
            entities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(3, ref World.UpdateAllocator);
            access->CreateEntityDuringStructuralChange(archetype, (Entity*)entities.GetUnsafePtr(), 3);
            Assert.DoesNotThrow(() =>
            {
                access->AddComponentDuringStructuralChange(entities, typeof(EcsTestData));
            });
            access->EndStructuralChanges(ref archetypeChanges);

            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(m_Manager.HasComponent<Simulate>(entities[i]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entities[i]));
                Assert.AreEqual(2, m_Manager.GetComponentCount(entities[i])); // +1 for Simulate
            }
        }

        [Test]
        public unsafe void AddComponentDuringStructuralChange_NativeArray_AlreadyHasComponents()
        {
            var access = m_Manager.GetCheckedEntityDataAccess();
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));

            var archetypeChanges = access->BeginStructuralChanges();
            var entities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(3, ref World.UpdateAllocator);
            access->CreateEntityDuringStructuralChange(archetype, (Entity*)entities.GetUnsafePtr(), 3);
            // add component which the entities already have
            access->AddComponentDuringStructuralChange(entities, typeof(EcsTestData2));

            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entities[i]));
                Assert.AreEqual(4, m_Manager.GetComponentCount(entities[i])); // +1 for Simulate
            }

            // add component which the entities do NOT already have
            access->AddComponentDuringStructuralChange(entities, typeof(EcsTestData4));
            access->EndStructuralChanges(ref archetypeChanges);

            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData4>(entities[i]));
                Assert.AreEqual(5, m_Manager.GetComponentCount(entities[i])); // +1 for Simulate
            }
        }

        [Test]
        public unsafe void AddMultipleComponentsDuringStructuralChange_NativeArray()
        {
            var access = m_Manager.GetCheckedEntityDataAccess();
            var archetype = m_Manager.CreateArchetype();
            var types = new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2));

            var archetypeChanges = access->BeginStructuralChanges();
            var entities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(0, ref World.UpdateAllocator);
            access->CreateEntityDuringStructuralChange(archetype, (Entity*)entities.GetUnsafePtr(), 0);

            // array of zero entities
            Assert.DoesNotThrow(() =>
            {
                access->AddMultipleComponentsDuringStructuralChange(entities, types);
            });

            entities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(3, ref World.UpdateAllocator);
            access->CreateEntityDuringStructuralChange(archetype, (Entity*)entities.GetUnsafePtr(), 3);
            // entities start with zero components
            access->AddMultipleComponentsDuringStructuralChange(entities, types);
            access->EndStructuralChanges(ref archetypeChanges);

            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entities[i]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entities[i]));
                Assert.AreEqual(3, m_Manager.GetComponentCount(entities[i])); // +1 for Simulate
            }
        }

        [Test]
        public unsafe void AddMultipleComponentsDuringStructuralChange_NativeArray_AlreadyHasComponents()
        {
            var access = m_Manager.GetCheckedEntityDataAccess();
            var types = new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2));
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));

            // call after CreateArchetype
            access->BeforeStructuralChange();
            var archetypeChanges = access->BeginStructuralChanges();
            var entities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(3, ref World.UpdateAllocator);
            access->CreateEntityDuringStructuralChange(archetype, (Entity*)entities.GetUnsafePtr(), 3);

            // add components which the entities already have
            access->AddMultipleComponentsDuringStructuralChange(entities, types);

            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entities[i]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entities[i]));
                Assert.AreEqual(4, m_Manager.GetComponentCount(entities[i])); // +1 for Simulate
            }

            // add a component which the entities do NOT already have
            types = new ComponentTypeSet(typeof(EcsTestData3), typeof(EcsTestData4));
            access->AddMultipleComponentsDuringStructuralChange(entities, types);
            access->EndStructuralChanges(ref archetypeChanges);

            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData3>(entities[i]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData4>(entities[i]));
                Assert.AreEqual(5, m_Manager.GetComponentCount(entities[i])); // +1 for Simulate
            }
        }
    }
}
