#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Unity.Entities.Tests
{
    class WorldDebuggingToolsTests : ECSTestsFixture
    {
        partial class RegularSystem : SystemBase
        {
            public EntityQuery entities;

            protected override void OnUpdate()
            {
                throw new NotImplementedException();
            }

            protected override void OnCreate()
            {
                entities = GetEntityQuery(ComponentType.ReadWrite<EcsTestData>());
            }
        }

        partial class ExcludeSystem : SystemBase
        {
            public EntityQuery entities;

            protected override void OnUpdate()
            {
                throw new NotImplementedException();
            }

            protected override void OnCreate()
            {
                entities = GetEntityQuery(
                    ComponentType.ReadWrite<EcsTestData>(),
                    ComponentType.Exclude<EcsTestData2>());
            }
        }

        [Test]
        public void SystemInclusionList_MatchesComponents()
        {
            var system = World.GetOrCreateSystemManaged<RegularSystem>();

            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            var matchList = new List<Tuple<ComponentSystemBase, List<EntityQuery>>>();

            WorldDebuggingTools.MatchEntityInEntityQueries(World.DefaultGameObjectInjectionWorld, entity, matchList);

            Assert.AreEqual(1, matchList.Count);
            Assert.AreEqual(system, matchList[0].Item1);
            Assert.AreEqual(system.EntityQueries[0], matchList[0].Item2[0]);
        }

        [Test]
        public void SystemInclusionList_IgnoresSubtractedComponents()
        {
            World.GetOrCreateSystemManaged<ExcludeSystem>();

            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            var matchList = new List<Tuple<ComponentSystemBase, List<EntityQuery>>>();

            WorldDebuggingTools.MatchEntityInEntityQueries(World, entity, matchList);

            Assert.AreEqual(0, matchList.Count);
        }
    }
}
#endif
