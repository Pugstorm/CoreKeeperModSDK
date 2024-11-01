using System;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public struct AdditionalEntity : IComponentData {}

    [AddComponentMenu("")]
    public class TestAdditionalEntityComponentAuthoring : MonoBehaviour
    {
        public int value;

        public class Baker : Baker<TestAdditionalEntityComponentAuthoring>
        {
            public override void Bake(TestAdditionalEntityComponentAuthoring authoring)
            {
                for (int index = 0; index < authoring.value; ++index)
                {
                    var entity = CreateAdditionalEntity(TransformUsageFlags.None);
                    AddComponent<AdditionalEntity>(entity);
                }
            }
        }
    }
}
