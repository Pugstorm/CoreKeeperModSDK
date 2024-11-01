using System;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class MockSharedDataAuthoring : MonoBehaviour
    {
        public int Value;

        class Baker : Baker<MockSharedDataAuthoring>
        {
            public override void Bake(MockSharedDataAuthoring authoring)
            {
                // This test might require transform components
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddSharedComponent(entity, new MockSharedData
                {
                    Value = authoring.Value
                });
            }
        }
    }

    [Serializable]
    public struct MockSharedData : ISharedComponentData
    {
        public int Value;
    }
}
