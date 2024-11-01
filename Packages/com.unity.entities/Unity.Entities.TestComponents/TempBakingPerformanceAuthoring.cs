using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class TempBakingPerformanceAuthoring : MonoBehaviour { public int Field; }

    [TemporaryBakingType]
    public struct TempBakingPerformanceComponent : IComponentData { public UnityObjectRef<TempBakingPerformanceAuthoring> component; }

    public struct TempBakingPerformanceSharedComp : ISharedComponentData
    {
        public int value;

        public TempBakingPerformanceSharedComp(int inValue)
        {
            value = inValue;
        }
    }

    class TempBakingPerformanceComponentSystem : Baker<TempBakingPerformanceAuthoring>
    {
        public override void Bake(TempBakingPerformanceAuthoring authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);

            AddSharedComponent(entity, new TempBakingPerformanceSharedComp() { value = authoring.Field });
            AddComponent(entity, new TempBakingPerformanceComponent() { component = authoring });
        }
    }
}
