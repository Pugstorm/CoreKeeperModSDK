using UnityEngine;

namespace Unity.Entities.Hybrid.Tests.Baking.SeparateAssembly
{
    public class ComponentInAssemblyAuthoring : MonoBehaviour
    {
        public int value;
    }

    public struct ComponentInAssemblyBakerC : IComponentData
    {
        public int value;
    }

    public struct ComponentInAssemblyBakingSystemC : IComponentData
    {
        public int value;
    }

    public class ComponentInAssemblyAuthoringBaker : Baker<ComponentInAssemblyAuthoring>
    {
        public override void Bake(ComponentInAssemblyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ComponentInAssemblyBakerC
            {
                value = authoring.value
            });
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial class ComponentInAssemblyBakingSystem : SystemBase
    {
        private EntityQuery query;

        protected override void OnCreate()
        {
            query = GetEntityQuery(
                typeof(ComponentInAssemblyBakerC));
        }

        protected override void OnUpdate()
        {
            EntityManager.AddComponent<ComponentInAssemblyBakingSystemC>(query);
        }
    }
}
