using System.Collections.Generic;
using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Update the <see cref="InitializationSystemGroup"/> of a client world from another world (usually the default world)
    /// Used only for DOTSRuntime and tests or other specific use cases.
    /// </summary>
#if !UNITY_SERVER || UNITY_EDITOR
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    internal partial class TickClientInitializationSystem : TickComponentSystemGroup
    {
    }
#endif
}
