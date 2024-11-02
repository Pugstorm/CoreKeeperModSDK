using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class TestConditionalComponentAuthoring : MonoBehaviour
    {
        public bool condition;

        public struct TestComponent : IComponentData
        {
        }

        class Baker : Baker<TestConditionalComponentAuthoring>
        {
            public override void Bake(TestConditionalComponentAuthoring authoring)
            {
                if (authoring.condition)
                {
                    // This test shouldn't require transform components
                    var entity = GetEntity(TransformUsageFlags.None);
                    AddComponent(entity, default(TestComponent));
                }
            }
        }
    }
}
