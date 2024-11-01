using System.Collections.Generic;

namespace Unity.Entities.Analyzer
{
    public static class SystemAPIMethods
    {
        // Represents all methods that are allowed usage inside of Entities.ForEach
        public static IEnumerable<string[]> EFEAllowedAPIMethods
        {
            get
            {
                return new[]
                {
                    new[] {"GetComponentLookup", "SystemAPI.GetComponentLookup<EcsTestData>()"},
                    new[] {"GetComponent", "SystemAPI.GetComponent<EcsTestData>(entity)"},
                    new[] {"SetComponent", "SystemAPI.SetComponent<EcsTestData>(entity, new EcsTestData())"},
                    new[] {"HasComponent", "SystemAPI.HasComponent<EcsTestData>(entity)"},
                    new[] {"GetBufferLookup", "SystemAPI.GetBufferLookup<EcsIntElement>(true)"},
                    new[] {"GetBuffer", "SystemAPI.GetBuffer<EcsIntElement>(entity)"},
                    new[] {"HasBuffer", "SystemAPI.HasBuffer<EcsIntElement>(entity)"},
                    new[] {"GetEntityStorageInfoLookup", "SystemAPI.GetEntityStorageInfoLookup()"},
                    new[] {"Exists", "SystemAPI.Exists(entity)"},
                    new[] {"GetAspect", "SystemAPI.GetAspect<EcsTestAspect>(entity)"}
                };
            }
        }
    }
}
