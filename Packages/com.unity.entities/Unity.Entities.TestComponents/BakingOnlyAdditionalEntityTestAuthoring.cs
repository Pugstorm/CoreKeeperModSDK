using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class BakingOnlyAdditionalEntityTestAuthoring : MonoBehaviour
    {
        public int SelfValue;

        public struct BakingOnlyEntityTestComponent : IComponentData
        {
            public int Value;
        }

        class Baker : Baker<BakingOnlyAdditionalEntityTestAuthoring>
        {
            public override void Bake(BakingOnlyAdditionalEntityTestAuthoring authoring)
            {
                var component = new BakingOnlyEntityTestComponent
                {
                    Value = authoring.SelfValue
                };
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, component);

                var bakeOnlyEntity = CreateAdditionalEntity(TransformUsageFlags.None, true);
                AddComponent(bakeOnlyEntity, component);

                var noBakeOnlyEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(noBakeOnlyEntity, component);
            }
        }
    }
}

