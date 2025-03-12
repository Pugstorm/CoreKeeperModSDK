using Unity.Core;

namespace Unity.Entities
{
    [AssumeReadOnly]
    public struct WorldTime : IComponentData
    {
        public TimeData Time;
    }

    [AssumeReadOnly]
    internal struct WorldTimeQueue : IBufferElementData
    {
        public TimeData Time;
    }
}
