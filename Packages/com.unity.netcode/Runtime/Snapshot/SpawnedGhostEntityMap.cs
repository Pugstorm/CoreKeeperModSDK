using Unity.Entities;
using Unity.Collections;

namespace Unity.NetCode
{
    internal struct GhostUpdateVersion : IComponentData
    {
        public uint LastSystemVersion;
    }

    /// <summary>
    /// Singleton entity used store the entities references for all the spawned ghost.
    /// </summary>
    public struct SpawnedGhostEntityMap : IComponentData
    {
        /// <summary>
        /// Updated by the <see cref="GhostReceiveSystem"/> and the <see cref="GhostSendSystem"/> when a ghost is spawned/despawned,
        /// let you retrieve the spawned ghost entity reference from the ghost <see cref="SpawnedGhost"/> identity.
        /// </summary>
        public NativeParallelHashMap<SpawnedGhost, Entity>.ReadOnly Value;
        internal NativeParallelHashMap<SpawnedGhost, Entity> SpawnedGhostMapRW;

        // Server data
        internal NativeList<int> ServerDestroyedPrespawns;
        internal NativeArray<int> m_ServerAllocatedGhostIds;

        internal void SetServerAllocatedPrespawnGhostId(int prespawnCount)
        {
            m_ServerAllocatedGhostIds[1] = prespawnCount;
        }

        // Client data
        internal NativeParallelHashMap<int, Entity> ClientGhostEntityMap;

        internal void AddClientNonSpawnedGhosts(NativeArray<NonSpawnedGhostMapping> ghosts, NetDebug netDebug)
        {
            for (int i = 0; i < ghosts.Length; ++i)
            {
                var ghostId = ghosts[i].ghostId;
                var ent = ghosts[i].entity;
                if (!ClientGhostEntityMap.TryAdd(ghostId, ent))
                {
                    netDebug.LogError($"Ghost ID {ghostId} has already been added");
                    ClientGhostEntityMap[ghostId] = ent;
                }
            }
        }

        internal void AddClientSpawnedGhosts(NativeArray<SpawnedGhostMapping> ghosts, NetDebug netDebug)
        {
            for (int i = 0; i < ghosts.Length; ++i)
            {
                var ghost = ghosts[i].ghost;
                var ent = ghosts[i].entity;
                if (!ClientGhostEntityMap.TryAdd(ghost.ghostId, ent))
                {
                    netDebug.LogError($"Ghost ID {ghost.ghostId} has already been added");
                    ClientGhostEntityMap[ghost.ghostId] = ent;
                }

                if (!SpawnedGhostMapRW.TryAdd(ghost, ent))
                {
                    netDebug.LogError($"Ghost ID {ghost.ghostId} has already been added to the spawned ghost map");
                    SpawnedGhostMapRW[ghost] = ent;
                }
            }
        }
        internal void UpdateClientSpawnedGhosts(NativeArray<SpawnedGhostMapping> ghosts, NetDebug netDebug)
        {
            for (int i = 0; i < ghosts.Length; ++i)
            {
                var ghost = ghosts[i].ghost;
                var ent = ghosts[i].entity;
                var prevEnt = ghosts[i].previousEntity;
                // If the ghost is also in the desapwn queue it will not be in the ghost map
                // If a ghost id previously used for an interpolated ghost is not used for a predicted ghost
                // a different ghost might be in the ghost map
                if (ClientGhostEntityMap.TryGetValue(ghost.ghostId, out var existing) && existing == prevEnt)
                {
                    ClientGhostEntityMap[ghost.ghostId] =  ent;
                }
                if (!SpawnedGhostMapRW.TryAdd(ghost, ent))
                {
                    netDebug.LogError($"Ghost ID {ghost.ghostId} has already been added to the spawned ghost map");
                    SpawnedGhostMapRW[ghost] = ent;
                }
            }
        }
    }
}
