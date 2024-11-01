using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    class QueryWithEntitiesViewTests
    {
        World m_World;
        EntityQuery m_Query;
        TestSystemsForControls.SystemA m_SystemA;
        WorldProxyManager m_WorldProxyManager;
        WorldProxy m_WorldProxy;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_World = new World("QueryWithEntitiesTestWorld");
            var group = m_World.GetOrCreateSystemManaged<SimulationSystemGroup>();
            m_SystemA = m_World.GetOrCreateSystemManaged<TestSystemsForControls.SystemA>();
            group.AddSystemToUpdateList(m_SystemA);
            m_WorldProxyManager = new WorldProxyManager();
            m_WorldProxyManager.CreateWorldProxiesForAllWorlds();
            m_WorldProxy = m_WorldProxyManager.GetWorldProxyForGivenWorld(m_World);

            var archetype = m_World.EntityManager.CreateArchetype(typeof(EntityGuid), typeof(EcsTestSharedComp));
            using var entities = m_World.EntityManager.CreateEntity(archetype, 2, m_World.UpdateAllocator.ToAllocator);
            for (var i = 0; i < entities.Length; i++)
            {
                m_World.EntityManager.SetSharedComponentManaged(entities[i], new EcsTestSharedComp{ value = i == 0 ? 123 : 345});
#if !DOTS_DISABLE_DEBUG_NAMES
                m_World.EntityManager.SetName(entities[i], $"QueryWithEntitiesView_Entity{i}");
#endif
            }

            m_Query = m_World.EntityManager.CreateEntityQuery(typeof(EntityGuid), typeof(EcsTestSharedComp));
            m_Query.SetSharedComponentFilterManaged(new EcsTestSharedComp{value = 123});
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            m_World.Dispose();
            m_WorldProxyManager.Dispose();
        }

        [Test]
        public void QueryWithEntitiesView_GeneratesCorrectVisualHierarchy()
        {
            var data = new QueryWithEntitiesViewData(m_World, m_Query, new SystemProxy(m_SystemA, m_WorldProxy), 2);
            var el = new QueryWithEntitiesView(data);

            var headerTitleLabel = el.HeaderName;
            Assert.That(headerTitleLabel, Is.Not.Null);
            Assert.That(headerTitleLabel.text, Is.EqualTo("Query #2"));
            Assert.That(el.Q(className: UssClasses.QueryWithEntities.OpenQueryWindowButton), Is.Not.Null);
            Assert.That(el.Q(className: UssClasses.QueryWithEntities.SeeAllContainer), Is.Not.Null);
        }

        [Test]
        public void QueryWithEntitiesView_UpdatesCorrectly()
        {
            var data = new QueryWithEntitiesViewData(m_World, m_Query);
            var el = new QueryWithEntitiesView(data);
            el.Update();

            Assert.That(el.HeaderName.text, Is.EqualTo("Query #0"));
            Assert.That(el.Q(className: UssClasses.QueryWithEntities.SeeAllContainer).style.display.value, Is.EqualTo(DisplayStyle.None));

            var entityViews = el.Query<EntityView>().ToList();
            Assert.That(entityViews.Count, Is.EqualTo(1));
            var entityView = entityViews.FirstOrDefault();
            Assert.That(entityView, Is.Not.Null);
#if !DOTS_DISABLE_DEBUG_NAMES
            Assert.That(entityView.Q<Label>(className: UssClasses.EntityView.EntityName).text, Is.EqualTo("QueryWithEntitiesView_Entity0"));
#endif
        }
    }
}
