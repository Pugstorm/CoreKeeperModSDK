using Unity.Entities.Build;
using Unity.Entities.Hybrid.Tests.Baking.SeparateAssembly;

namespace Unity.Entities.Hybrid.Tests.Baking.ExcludedAssemblyTest
{
    public struct AlwaysIncludeBakingSystemComponent : IComponentData { }

    [AlwaysIncludeBakingSystem]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class AlwaysIncludedBakingSystems : SystemBase
    {
        private EntityQuery query;

        protected override void OnCreate()
        {
            query = GetEntityQuery(
                typeof(ComponentInAssemblyBakerC));
        }

        protected override void OnUpdate()
        {
            EntityManager.AddComponent<AlwaysIncludeBakingSystemComponent>(query);
        }
    }
}
