using System;
using Unity.Entities.Tests;
using UnityEngine;

namespace Unity.Entities.TestComponents
{
    public struct MockCollider : IComponentData {}
    public class MockColliderComponent : MonoBehaviour
    {
        void OnEnable() { }

        class MockColliderComponentBaker : Baker<MockColliderComponent>
        {
            public override void Bake(MockColliderComponent authoring)
            {
                // A collider test might use transform components
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var body = GetComponent<MockBodyComponent>();
                if (body == null || !body.enabled)
                {
                    AddComponent(entity, new RequiredComponent {AddedBy = nameof(MockColliderComponent)});
                }
                AddComponent<MockCollider>(entity);
            }
        }
    }
}
