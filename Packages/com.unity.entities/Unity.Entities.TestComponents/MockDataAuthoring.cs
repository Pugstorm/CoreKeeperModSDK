using System;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class MockDataAuthoring : MonoBehaviour
    {
        public int Value;
    }

    public class MockDataAuthoringBaker : Baker<MockDataAuthoring>
    {
        public override void Bake(MockDataAuthoring authoring)
        {
            // This test might require transform components
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new MockData{Value = authoring.Value});
        }
    }

    [DisableAutoCreation]
    public class MockDataAuthoringBaker_WithAdditionalEntities : Baker<MockDataAuthoring>
    {
        public override void Bake(MockDataAuthoring authoring)
        {
            // This test might require transform components
            var mainEntity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(mainEntity, new MockData{Value = authoring.Value});
            for (int i = 0; i < authoring.Value; i++)
            {
                var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new MockData{Value = i+1});
            }
        }
    }

    [Serializable]
    public struct MockData : IComponentData
    {
        public int Value;

        public MockData(int value) => Value = value;

        public override string ToString() => Value.ToString();
    }
}
