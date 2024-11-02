using System;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [DisableAutoCreation]
    public class MockDataAuthoringBaker_AddComponentWithManualOverride : Baker<MockDataAuthoring>
    {
        public override void Bake(MockDataAuthoring authoring)
        {
            GetEntity(TransformUsageFlags.ManualOverride);
        }
    }
}
