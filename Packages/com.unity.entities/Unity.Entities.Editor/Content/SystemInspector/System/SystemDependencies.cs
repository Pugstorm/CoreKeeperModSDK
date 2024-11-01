using System.Collections.Generic;

namespace Unity.Entities.Editor
{
    class SystemDependencies
    {
        readonly World m_World;
        SystemProxy m_SystemProxy;
        readonly List<SystemDependencyViewData> m_UpdateBeforeSystemViewDataList;
        readonly List<SystemDependencyViewData> m_UpdateAfterSystemViewDataList;

        public SystemDependencies(World world, SystemProxy systemProxy)
        {
            m_World = world;
            m_SystemProxy = systemProxy;
            m_UpdateBeforeSystemViewDataList = new List<SystemDependencyViewData>();
            m_UpdateAfterSystemViewDataList = new List<SystemDependencyViewData>();
        }

        public string CurrentSystemName => m_SystemProxy.TypeName;

        public List<SystemDependencyViewData> GetUpdateBeforeSystemViewDataList()
        {
            m_UpdateBeforeSystemViewDataList.Clear();

            if (m_World == null || !m_World.IsCreated || !m_SystemProxy.Valid)
                return m_UpdateBeforeSystemViewDataList;

            foreach (var after in m_SystemProxy.UpdateAfterSet)
                m_UpdateBeforeSystemViewDataList.Add(new SystemDependencyViewData(after, after.NicifiedDisplayName));

            return m_UpdateBeforeSystemViewDataList;
        }

        public List<SystemDependencyViewData> GetUpdateAfterSystemViewDataList()
        {
            m_UpdateAfterSystemViewDataList.Clear();

            if (m_World == null || !m_World.IsCreated || !m_SystemProxy.Valid)
                return m_UpdateAfterSystemViewDataList;

            foreach (var before in m_SystemProxy.UpdateBeforeSet)
                m_UpdateAfterSystemViewDataList.Add(new SystemDependencyViewData(before, before.NicifiedDisplayName));

            return m_UpdateAfterSystemViewDataList;
        }
    }
}
