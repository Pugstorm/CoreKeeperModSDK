using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Entities.Editor
{
    class ComponentMatchingSystems
    {
        readonly World m_World;
        readonly Type m_ComponentType;
        public readonly List<SystemQueriesViewData> Systems;
        WorldProxyManager m_WorldProxyManager;
        int m_LastWorldVersion;
        List<SystemProxy> m_AllSystemProxies;

        public ComponentMatchingSystems(World world, Type type)
        {
            m_World = world;
            m_ComponentType = type;
            Systems = new List<SystemQueriesViewData>();
            m_WorldProxyManager = new WorldProxyManager();
        }

        ~ComponentMatchingSystems()
        {
            m_WorldProxyManager.Dispose();
        }

        public void Update()
        {
            if (m_World == null || !m_World.IsCreated)
                return;

            var worldVersion = m_World.Version;
            if (m_LastWorldVersion == worldVersion)
                return;

            m_LastWorldVersion = worldVersion;

            Systems.Clear();
            m_AllSystemProxies = GetAllSystemProxies(m_World);
            GetMatchingSystems(Systems);
        }

        List<SystemProxy> GetAllSystemProxies(World world)
        {
            m_WorldProxyManager.RebuildWorldProxyForGivenWorld(world);
            var worldProxy = m_WorldProxyManager.GetWorldProxyForGivenWorld(world);
            return worldProxy?.AllSystemsInOrder.ToList();
        }

        unsafe void GetMatchingSystems(List<SystemQueriesViewData> systems)
        {
            foreach (var system in m_AllSystemProxies)
            {
                var systemStatePtr = system.StatePointerForQueryResults;
                if (systemStatePtr == null)
                    continue;

                using var queryViewDataList = Pooling.GetList<QueryViewData>();
                using var pooled = Pooling.GetList<EntityQuery>();
                var i = 0;
                foreach (var query in systemStatePtr->EntityQueries)
                {
                    i++;
                    if (query.GetQueryTypes().Any(componentType => componentType.GetManagedType() == m_ComponentType))
                    {
                        pooled.List.Add(query);
                        queryViewDataList.List.Add(new QueryViewData(i, query, system, m_World));
                    }
                }

                if (pooled.List.Count > 0)
                {
                    systems.Add(new SystemQueriesViewData(system, RelationshipsTab.GetSystemKind(system), queryViewDataList.List.ToArray()));
                }
            }
        }
    }
}
