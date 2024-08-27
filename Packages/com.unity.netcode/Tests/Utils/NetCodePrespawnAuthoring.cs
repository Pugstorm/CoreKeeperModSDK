using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.NetCode.Tests
{
    public class NetCodePrespawnAuthoring : MonoBehaviour
    {}

    [GhostComponent(SendDataForChildEntity = true)]
    [GhostEnabledBit]
    public struct TestComponent1 : IComponentData, IEnableableComponent
    {}

    [GhostComponent(SendDataForChildEntity = true)]
    [GhostEnabledBit]
    public struct TestComponent2 : IComponentData, IEnableableComponent
    {
        [GhostField] public float3 Test1;
        [GhostField] public long Test2;
        [GhostField] public ulong Test3;
        [GhostField] public FixedString128Bytes Test4;
    }

    [GhostComponent(SendDataForChildEntity = true)]
    [InternalBufferCapacity(0)]
    [GhostEnabledBit]
    public struct TestBuffer3 : IBufferElementData, IEnableableComponent
    {
        [GhostField] public float2 Test1;
        [GhostField] public int Test2;
        [GhostField] public byte Test3;
        [GhostField] public sbyte Test4;
    }

    class NetCodePrespawnAuthoringBaker : Baker<NetCodePrespawnAuthoring>
    {
        public override void Bake(NetCodePrespawnAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<NetCodePrespawnTag>(entity);
            AddComponent<TestComponent1>(entity);
            SetComponentEnabled<TestComponent1>(entity, false);
            AddComponent(entity, new TestComponent2
            {
                Test1 = 5,
                Test2 = -6,
                Test3 = 7,
                Test4 = "::LongTextLongTextLongTextLongTextLongTextLongTextLongTextLongTextLongTextLongTextLongTextLongTextLongText::",
            });
            SetComponentEnabled<TestComponent2>(entity, true);

            var buffer = AddBuffer<TestBuffer3>(entity);
            const int bufferLength = 5;
            buffer.Length = bufferLength;
            for (int i = 0; i < bufferLength; i++)
                buffer[i] = new TestBuffer3
                {
                    Test1 = 1,
                    Test2 = 2,
                    Test3 = 3,
                    Test4 = 4
                };
            SetComponentEnabled<TestBuffer3>(entity, false);
        }
    }
}
