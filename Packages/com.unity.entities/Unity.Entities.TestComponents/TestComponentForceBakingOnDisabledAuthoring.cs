using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Entities.TestComponents
{
    [AddComponentMenu("")]
    public class TestComponentForceBakingOnDisabledAuthoring : MonoBehaviour
    {
        public struct ForcedBaking : IComponentData { }
        public struct NotForcedBaking : IComponentData { }

        public void Update()
        {

        }

        [ForceBakingOnDisabledComponents]
        class ForcedBaker : Baker<TestComponentForceBakingOnDisabledAuthoring>
        {
            public override void Bake(TestComponentForceBakingOnDisabledAuthoring authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<ForcedBaking>(entity);
            }
        }

        class NotForcedBaker : Baker<TestComponentForceBakingOnDisabledAuthoring>
        {
            public override void Bake(TestComponentForceBakingOnDisabledAuthoring authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<NotForcedBaking>(entity);
            }
        }
    }
}
