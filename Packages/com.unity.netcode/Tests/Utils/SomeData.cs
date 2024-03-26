using Unity.Entities;

namespace Unity.NetCode.Tests
{
    public struct SomeData : IComponentData
    {
        [GhostField] public int Value;
    }

    public struct SomeDataElement : IBufferElementData
    {
        [GhostField] public int Value;
    }
}

