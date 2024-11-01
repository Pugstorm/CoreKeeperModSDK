using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEngine;

namespace Unity.Entities.TestComponents
{

    public struct EmptyEnableableComponent : IComponentData, IEnableableComponent
    {
    }

    public struct EnableableComponent : IComponentData, IEnableableComponent
    {
        public int Value;
    }

    public struct EnableableBuffer : IBufferElementData, IEnableableComponent
    {
        public int Value;
    }

    public class EnableableTestAuthoring : MonoBehaviour
    {
        public bool emptyEnableComponent;
        public bool enableComponent;
        public bool enableBuffer;
        public int value;

        class Baker : Baker<EnableableTestAuthoring>
        {
            public override void Bake(EnableableTestAuthoring authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new EmptyEnableableComponent());
                AddComponent(entity, new EnableableComponent {Value = authoring.value});

                var bufferData = new NativeArray<EnableableBuffer>(authoring.value, Allocator.Temp);
                for (int i = 0; i < authoring.value; i++)
                {
                    bufferData[i] = new EnableableBuffer {Value = i};
                }

                var buffer = AddBuffer<EnableableBuffer>(entity);
                buffer.AddRange(bufferData);

                SetComponentEnabled<EmptyEnableableComponent>(entity, authoring.emptyEnableComponent);
                SetComponentEnabled<EnableableComponent>(entity, authoring.enableComponent);
                SetComponentEnabled<EnableableBuffer>(entity, authoring.enableBuffer);
            }
        }
    }
}
