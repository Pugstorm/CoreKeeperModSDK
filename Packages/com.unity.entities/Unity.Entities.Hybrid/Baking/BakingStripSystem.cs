using System.Linq;
using Unity.Collections;
using Unity.Profiling;

namespace Unity.Entities
{
    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    internal partial class BakingStripSystem : SystemBase
    {
        private NativeArray<(ComponentType, EntityQuery)> m_BakingComponentQueries;
        private static readonly ProfilerMarker s_stripping = new ProfilerMarker("stripping baking types");

        protected override void OnCreate()
        {
            var allTypes = TypeManager.AllTypes.Where(t => t.TemporaryBakingType).ToArray();
            m_BakingComponentQueries = new NativeArray<(ComponentType, EntityQuery)>(allTypes.Length, Allocator.Persistent);

            for(int i = 0; i < allTypes.Length; i++)
            {
                var componentType = ComponentType.FromTypeIndex(allTypes[i].TypeIndex);
                EntityQueryDesc desc = new EntityQueryDesc()
                {
                    All = new ComponentType[] {componentType},
                    Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
                };
                m_BakingComponentQueries[i] = (componentType, GetEntityQuery(desc));
            }
        }

        protected override void OnDestroy()
        {
            m_BakingComponentQueries.Dispose();
        }

        protected override void OnUpdate()
        {
            using (s_stripping.Auto())
            {
                foreach(var (componentType, query) in m_BakingComponentQueries)
                {
                    EntityManager.RemoveComponent(query, componentType);
                }
            }
        }

    }
}
