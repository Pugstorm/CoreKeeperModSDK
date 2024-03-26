using System;
using Unity.Collections;
using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Singleton component with APIs and collections required for Ghost spawning and despawning.
    /// <see cref="GhostSpawnSystem"/> and <see cref="GhostDespawnSystem"/>.
    /// </summary>
    internal struct GhostDespawnQueues : IComponentData
    {
        internal NativeQueue<GhostDespawnSystem.DelayedDespawnGhost> InterpolatedDespawnQueue;
        internal NativeQueue<GhostDespawnSystem.DelayedDespawnGhost> PredictedDespawnQueue;
    }
}
