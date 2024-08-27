using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.NetCode
{
    /// <summary>
    /// Present for both client and server worlds (and Local, for singleplayer input support).
    /// This is the core group, and contains the majority of the netcode systems.
    /// Its responsibilities are varied, and can be roughly sub-divided in the following categories:
    /// <para>-input gathering: <see cref="GhostInputSystemGroup"/></para>
    /// <para>-command handling: <see cref="CommandSendSystemGroup"/></para>
    /// <para>-ghost prediction/simulation: <see cref="PredictedSimulationSystemGroup"/></para>
    /// <para>-ghost spawning: see <see cref="GhostSpawnClassificationSystem"/>, <see cref="GhostSpawnSystemGroup"/>, <see cref="GhostSpawnSystem"/>, <see cref="GhostDespawnSystem"/></para>
    /// <para>-ghost replication: <see cref="GhostCollection"/>, <see cref="GhostSendSystem"/>, <see cref="GhostReceiveSystem"/> and <see cref="GhostUpdateSystem"/>.</para>
    /// <para>
    /// In general, all systems that need to simulate/manipulate ghost entities should be added to this group.
    /// </para>
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.LocalSimulation,
        WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    public partial class GhostSimulationSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// Parent group of all systems that need to process ghost entities after they are spawned.
    /// The group execute before <see cref="NetworkReceiveSystemGroup"/> to guarantee that when a new snasphot is received
    /// from server, all new ghosts has been spawned and ready to receive new data.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation|WorldSystemFilterFlags.ThinClientSimulation, WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst=true)]
    [UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
    [UpdateBefore(typeof(NetworkReceiveSystemGroup))]
    public partial class GhostSpawnSystemGroup : ComponentSystemGroup
    {
    }
}
