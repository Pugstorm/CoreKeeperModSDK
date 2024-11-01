using System;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Tests
{
    class DisableComponentTests : ECSTestsFixture
    {
        [Test]
        public void DIS_DontFindDisabledInEntityQuery()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(Disabled));

            var group = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            var entity0 = m_Manager.CreateEntity(archetype0);
            var entity1 = m_Manager.CreateEntity(archetype1);

            Assert.AreEqual(1, group.CalculateEntityCount());
            group.Dispose();

            m_Manager.DestroyEntity(entity0);
            m_Manager.DestroyEntity(entity1);
        }

        [Test]
        public void DIS_DontFindDisabledInChunkIterator()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(Disabled));

            var entity0 = m_Manager.CreateEntity(archetype0);
            var entity1 = m_Manager.CreateEntity(archetype1);

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();
            var count = ArchetypeChunkArray.TotalEntityCountInChunksIgnoreFiltering(chunks);

            Assert.AreEqual(1, count);

            m_Manager.DestroyEntity(entity0);
            m_Manager.DestroyEntity(entity1);
        }

        [Test]
        public void DIS_FindDisabledIfRequestedInEntityQuery()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(Disabled));

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<Disabled>());

            var entity0 = m_Manager.CreateEntity(archetype0);
            var entity1 = m_Manager.CreateEntity(archetype1);
            var entity2 = m_Manager.CreateEntity(archetype1);

            Assert.AreEqual(2, group.CalculateEntityCount());

            group.Dispose();
            m_Manager.DestroyEntity(entity0);
            m_Manager.DestroyEntity(entity1);
            m_Manager.DestroyEntity(entity2);
        }

        [Test]
        public void DIS_FindDisabledIfRequestedInChunkIterator()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(Disabled));

            var entity0 = m_Manager.CreateEntity(archetype0);
            var entity1 = m_Manager.CreateEntity(archetype1);
            var entity2 = m_Manager.CreateEntity(archetype1);

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<Disabled>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();
            var count = ArchetypeChunkArray.TotalEntityCountInChunksIgnoreFiltering(chunks);

            Assert.AreEqual(2, count);

            m_Manager.DestroyEntity(entity0);
            m_Manager.DestroyEntity(entity1);
            m_Manager.DestroyEntity(entity2);
        }

        [Test]
        public void DIS_GetAllIncludesDisabled([Values] bool immediate)
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(Disabled));

            var entity0 = m_Manager.CreateEntity(archetype0);
            var entity1 = m_Manager.CreateEntity(archetype1);
            var entity2 = m_Manager.CreateEntity(archetype1);

            var entities = immediate ? m_Manager.GetAllEntities() : m_Manager.GetAllEntities();
            Assert.AreEqual(3, entities.Length);
            entities.Dispose();

            m_Manager.DestroyEntity(entity0);
            m_Manager.DestroyEntity(entity1);
            m_Manager.DestroyEntity(entity2);
        }

        [Test]
        public void PrefabAndDisabledQueryOptions()
        {
            m_Manager.CreateEntity();
            m_Manager.CreateEntity(typeof(EcsTestData), typeof(Prefab));
            m_Manager.CreateEntity(typeof(EcsTestData), typeof(Disabled));
            m_Manager.CreateEntity(typeof(EcsTestData), typeof(Disabled), typeof(Prefab));

            CheckPrefabAndDisabledQueryOptions(EntityQueryOptions.Default, 0);
            CheckPrefabAndDisabledQueryOptions(EntityQueryOptions.IncludePrefab, 1);
            CheckPrefabAndDisabledQueryOptions(EntityQueryOptions.IncludeDisabledEntities, 1);
            CheckPrefabAndDisabledQueryOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab, 3);
        }

        void CheckPrefabAndDisabledQueryOptions(EntityQueryOptions options, int expected)
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestData>()
                .WithOptions(options)
                .Build(m_Manager);
            Assert.AreEqual(expected, query.CalculateEntityCount());
            query.Dispose();
        }
    }
}
