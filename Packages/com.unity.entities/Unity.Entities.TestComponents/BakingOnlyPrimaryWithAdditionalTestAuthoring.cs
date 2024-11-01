using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class BakingOnlyPrimaryWithAdditionalEntitiesTestAuthoring : MonoBehaviour
    {
        public int SelfValue;

        public struct PrimaryBakeOnlyAdditionalEntityTestComponent: IComponentData
        {
            public int Value;
        }

        class Baker : Baker<BakingOnlyPrimaryWithAdditionalEntitiesTestAuthoring>
        {
            public override void Bake(BakingOnlyPrimaryWithAdditionalEntitiesTestAuthoring authoring)
            {
                var component = new PrimaryBakeOnlyAdditionalEntityTestComponent
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


