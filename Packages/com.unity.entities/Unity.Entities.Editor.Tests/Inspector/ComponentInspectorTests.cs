using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.UI;
using Unity.Transforms;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    partial class ComponentInspectorTests
    {
        World m_World;
        ComponentInspectorTestSystem m_ComponentInspectorTestSystem;

        partial class ComponentInspectorTestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .ForEach((ref SystemScheduleTestData1 data1, in SystemScheduleTestData2 data2) =>
                    {
                    }).Run();

                Entities
                    .WithNone<SystemScheduleTestData2>().ForEach((in SystemScheduleTestData1 data1) =>
                    {
                    }).Run();
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_World = new World("ComponentInspectorTestWorld");
            m_ComponentInspectorTestSystem = m_World.GetOrCreateSystemManaged<ComponentInspectorTestSystem>();
            m_World.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(m_ComponentInspectorTestSystem);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            m_World.Dispose();
        }

        [Test]
        public void ComponentInspector_RelationshipsTab_MatchingSystems()
        {
            var matchingSystems = new ComponentMatchingSystems(m_World, typeof(SystemScheduleTestData1));
            matchingSystems.Update();

            var results = matchingSystems.Systems.Where(s => s.SystemName == "Component Inspector Tests | Component Inspector Test System").ToList();
            Assert.That(results.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(results[0].Kind, Is.EqualTo(SystemQueriesViewData.SystemKind.Regular));
            Assert.That(results[0].Queries.Count, Is.EqualTo(2));
        }

        [Test]
        public void ComponentInspector_RelationshipsTab_MatchingEntities()
        {
            var archetype = m_World.EntityManager.CreateArchetype(typeof(SystemScheduleTestData1), typeof(SystemScheduleTestData2));
            using var entities = m_World.EntityManager.CreateEntity(archetype, 6, m_World.UpdateAllocator.ToAllocator);
#if !DOTS_DISABLE_DEBUG_NAMES
            m_World.EntityManager.SetName(entities[0], "ComponentInspectorEntity0");
#endif

            var worldViewData = new ComponentRelationshipWorldViewData(m_World, typeof(SystemScheduleTestData1));
            worldViewData.QueryWithEntitiesViewData.Update();
            Assert.That(worldViewData.QueryWithEntitiesViewData.Entities.Count, Is.EqualTo(5));
            Assert.That(worldViewData.QueryWithEntitiesViewData.TotalEntityCount, Is.EqualTo(6));
#if !DOTS_DISABLE_DEBUG_NAMES
            Assert.That(worldViewData.QueryWithEntitiesViewData.Entities[0].EntityName, Is.EqualTo("ComponentInspectorEntity0"));
#endif
        }
    }
}
