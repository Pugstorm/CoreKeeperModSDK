using Unity.Collections;
using Unity.Entities.Tests;
using UnityEngine;

namespace Unity.Entities.TestComponents
{
    public struct MockBody : IComponentData {}

    public struct RequiredComponent : IComponentData
    {
        public FixedString64Bytes AddedBy;
    }

    public class MockBodyComponent : MonoBehaviour
    {
        void OnEnable() { }

        class MockBodyComponentBaker : Baker<MockBodyComponent>
        {
            public override void Bake(MockBodyComponent authoring)
            {
                // A body test might use transform components
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RequiredComponent {AddedBy = nameof(MockBodyComponent)});
                AddComponent<MockBody>(entity);
            }
        }
    }
}
