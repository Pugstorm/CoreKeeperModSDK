#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using Unity.Burst;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true), BurstCompile]
        partial struct FrameCountSystem : ISystem
        {
            sealed class SharedFrameCount { internal static readonly SharedStatic<int> Ref = SharedStatic<int>.GetOrCreate<SharedFrameCount>(); }

            public static int FrameCount
            {
                get => SharedFrameCount.Ref.Data;
                private set => SharedFrameCount.Ref.Data = value;
            }

            public void OnCreate(ref SystemState state) => FrameCount = 0;
            public void OnDestroy(ref SystemState state) => FrameCount = 0;
            [BurstCompile] public void OnUpdate(ref SystemState state) => FrameCount++;
        }
    }
}
#endif
