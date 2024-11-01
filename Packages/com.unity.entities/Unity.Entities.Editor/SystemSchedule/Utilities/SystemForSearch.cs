using System;
using System.Linq;
using Unity.Properties;

namespace Unity.Entities.Editor
{
    class SystemForSearch
    {
        public SystemForSearch(SystemProxy systemProxy)
        {
            SystemProxy = systemProxy;
            SystemName = systemProxy.TypeName;
            m_ComponentNamesInQueryCache = EntityQueryUtility.CollectComponentTypesFromSystemQuery(SystemProxy).ToArray();
        }

        public readonly SystemProxy SystemProxy;
        public readonly string SystemName;
        public IPlayerLoopNode Node;
        public int SystemItemId;

        string[] m_ComponentNamesInQueryCache;
        public string[] SystemDependencyCache;

        [CreateProperty] public string[] ComponentNamesInQuery => m_ComponentNamesInQueryCache;
        [CreateProperty] public string[] SystemDependency => SystemDependencyCache;
    }
}
