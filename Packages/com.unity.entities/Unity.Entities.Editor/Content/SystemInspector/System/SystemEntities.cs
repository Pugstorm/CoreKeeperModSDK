using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Editor
{
    class SystemEntities
    {
        readonly World m_World;
        SystemProxy m_SystemProxy;
        UnsafeList<EntityQuery> m_LastEntityQueries;

        public SystemEntities(World world, SystemProxy systemProxy)
        {
            m_World = world;
            m_SystemProxy = systemProxy;
            m_EntitiesFromQueries = new List<QueryWithEntitiesViewData>();
        }

        readonly List<QueryWithEntitiesViewData> m_EntitiesFromQueries;

        public unsafe List<QueryWithEntitiesViewData> EntitiesFromQueries
        {
            get
            {
                if (!m_World.IsCreated || !m_SystemProxy.Valid)
                {
                    m_EntitiesFromQueries.Clear();
                    return m_EntitiesFromQueries;
                }

                var ptr = m_SystemProxy.StatePointerForQueryResults;
                if (ptr == null)
                {
                    m_EntitiesFromQueries.Clear();
                    return m_EntitiesFromQueries;
                }

                var currentQueries = ptr->EntityQueries;
                if (m_LastEntityQueries.Equals(currentQueries))
                    return m_EntitiesFromQueries;

                m_LastEntityQueries = currentQueries;
                m_EntitiesFromQueries.Clear();

                for (var i = 0; i < m_LastEntityQueries.Length; i++)
                {
                    m_EntitiesFromQueries.Add(new QueryWithEntitiesViewData(m_World, m_LastEntityQueries[i], m_SystemProxy, i + 1));
                }

                return m_EntitiesFromQueries;
            }
        }

    }
}
