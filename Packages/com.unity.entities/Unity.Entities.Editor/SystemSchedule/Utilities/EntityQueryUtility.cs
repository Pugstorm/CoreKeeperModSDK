using System.Collections.Generic;

namespace Unity.Entities.Editor
{
    static class EntityQueryUtility
    {
        public static IEnumerable<string> CollectComponentTypesFromSystemQuery(SystemProxy systemProxy)
        {
            return systemProxy.GetComponentTypesUsedByQueries();
        }
    }
}
