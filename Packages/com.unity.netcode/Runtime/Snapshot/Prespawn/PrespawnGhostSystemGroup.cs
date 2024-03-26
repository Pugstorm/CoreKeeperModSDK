using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// The PrespawnGhostSystemGroup contains all the systems related to pre-spawned ghost.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateAfter(typeof(GhostCollectionSystem))]
    public partial class PrespawnGhostSystemGroup : ComponentSystemGroup
    {
    }
}
