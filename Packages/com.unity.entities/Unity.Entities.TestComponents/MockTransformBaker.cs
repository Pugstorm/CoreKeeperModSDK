using System;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [DisableAutoCreation]
    public class MockDataAuthoringBaker_TransformBaker : Baker<Transform>
    {
        public override void Bake(Transform authoring)
        {
            GetEntity(TransformUsageFlags.Dynamic);
        }
    }
}
