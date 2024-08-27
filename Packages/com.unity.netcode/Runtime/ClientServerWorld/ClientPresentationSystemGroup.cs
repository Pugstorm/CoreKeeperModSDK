using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;

namespace Unity.NetCode
{
    /// <summary>
    /// Update the <see cref="PresentationSystemGroup"/> of a client world from another world (usually the default world)
    /// Used only for DOTSRuntime and tests or other specific use cases.
    /// </summary>
#if !UNITY_SERVER || UNITY_EDITOR
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    internal partial class TickClientPresentationSystem : TickComponentSystemGroup
    {
    }
#endif
}
