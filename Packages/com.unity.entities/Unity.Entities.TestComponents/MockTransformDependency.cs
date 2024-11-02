using System;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [DisableAutoCreation]
    public class MockDataAuthoringBaker_TransformDependencyBaker : Baker<MockDataAuthoring>
    {
        public override void Bake(MockDataAuthoring authoring)
        {
            GetEntity(TransformUsageFlags.Dynamic);
            GetComponent<Transform>();
        }
    }
}
