using System;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [DisableAutoCreation]
    public class MockDataAuthoringBaker_ManualOverrideCopyPosBaker : Baker<MockDataAuthoring>
    {
        public override void Bake(MockDataAuthoring authoring)
        {
            // This test might require transform components
            var mainEntity = GetEntity(TransformUsageFlags.ManualOverride);
            var t = GetComponent<Transform>(authoring);
            AddComponent(mainEntity, new MockData((int)(t.position.x)));
        }
    }
}
