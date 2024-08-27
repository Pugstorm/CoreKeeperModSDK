using System;
using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.NetCode.LowLevel;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.NetCode
{
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("GhostSpawnQueueComponent has been deprecated. Use GhostSpawnQueueComponent instead (UnityUpgradable) -> GhostSpawnQueue", true)]
    public struct GhostSpawnQueueComponent : IComponentData
    {}

    /// <summary>
    /// GhostSPawnQueue is used to identify the singleton component which contains the GhostSpawnBuffer.
    /// </summary>
    public struct GhostSpawnQueue : IComponentData
    {

    }

    /// <summary>
    /// The GhostSpawnBuffer is the data for a GhostSpawnQueue singleton. It contains a list of ghosts which
    /// will be spawned by the GhostSpawnSystem at the beginning of next frame. It is populated by the
    /// GhostReceiveSystem and there needs to be a classification system updating after the GhostReceiveSystem which
    /// sets the SpawnType so the spawn system knows how to spawn the ghost.
    /// A classification system should only modify the SpawnType and PredictedSpawnEntity fields of this struct.
    /// InternalBufferCapacity allocated to almost max out chunk memory.
    /// </summary>
    [InternalBufferCapacity(240)]
    public struct GhostSpawnBuffer : IBufferElementData
    {
        /// <summary>
        /// The ghost mode to use to spawn th entity
        /// </summary>
        public enum Type
        {
            /// <summary>
            /// The ghost has not be classified yet and it is expected that a classification system will
            /// change this value to the proper ghost mode (see also <seealso cref="GhostSpawnClassificationSystem"/>).
            /// </summary>
            Unknown,
            /// <summary>
            /// The new ghost must be spawned as interpolated. The ghost creation is delayed
            /// until the <see cref="NetworkTime.InterpolationTick"/> match (or is greater) the actual spawn tick on the server.
            /// See <see cref="GhostSpawnSystem"/> and also <seealso cref="PendingSpawnPlaceholder"/>.
            /// </summary>
            Interpolated,
            /// <summary>
            /// The ghost is a predicted ghost. A new ghost instance is immediately created, unless the
            /// <see cref="PredictedSpawnEntity"/> is set to a valid entity reference, in which case the
            /// referenced entity is used instead as destination where to copy the received ghost snapshot.
            /// </summary>
            Predicted
        }
        /// <summary>
        /// The type of ghost to spawn. Based on the spawn type, some components may be enable/disabled or
        /// removed from the instantiated ghost.
        /// </summary>
        public Type SpawnType;
        /// <summary>
        /// The index of the ghost type in the <seealso cref="GhostCollectionPrefab"/> collection. Used to classify the ghost (<see cref="GhostSpawnClassificationSystem"/>).
        /// </summary>
        public int GhostType;
        /// <summary>
        /// The ghost id that will be assigned to the new ghost instance.
        /// </summary>
        public int GhostID;
        /// <summary>
        /// Offset im bytes used to retrieve from the temporary <see cref="SnapshotDataBuffer"/>, present on the
        /// <see cref="GhostSpawnQueue"/> singleton, the first received snapshot from the server.
        /// </summary>
        public int DataOffset;
        /// <summary>
        /// The size of the initial dynamic buffers data associated with the entity.
        /// </summary>
        public uint DynamicDataSize;
        /// <summary>
        /// The tick this ghost was spawned on the client. This is mainly used to determine the first tick we have data
        /// for so we can avoid spawning it before we have any data for the ghost.
        /// </summary>
        internal NetworkTick ClientSpawnTick;
        /// <summary>
        /// The tick this ghost was spawned on the server. For any predicted spawning this is the tick that should
        /// match since you are interested in when the server spawned the ghost, not when the server first sent the
        /// ghost to the client. Using this also means you are not considering ghosts becoming relevant as spawning.
        /// </summary>
        public NetworkTick ServerSpawnTick;
        /// <summary>
        /// Entity reference assigned by a classification system, when a predicted spawned entity for a newly received
        /// ghost is found. When assigning this <see cref="HasClassifiedPredictedSpawn"/> should also be set to true.
        /// If the referenced entity is different than <see cref="Entity.Null"/> the ghost type must be set to <see cref="Type.Predicted"/>.
        /// </summary>
        public Entity PredictedSpawnEntity;
        /// <summary>
        /// Should be set to true when a ghost classification system has processed this particular ghost spawn instance. It
        /// will then not be processed again in a system running later in the frame (like the default classification
        /// system).
        /// </summary>
        public bool HasClassifiedPredictedSpawn
        {
            get => m_HasClassifiedPredictedSpawn == 1;
            set => m_HasClassifiedPredictedSpawn = (byte)(value ? 1 : 0);
        }
        byte m_HasClassifiedPredictedSpawn;
        /// <summary>
        /// Only valid for pre-spawned ghost. Mainly used by the spawning system to re-assign
        /// the PrespawnGhostIndex component to pre-spawned ghosts that has re-instantiated because of relevancy changes.
        /// </summary>
        internal int PrespawnIndex;
        /// <summary>
        /// Only valid for pre-spawned ghost. The scene section that ghost belong to.
        /// </summary>
        internal  Hash128 SceneGUID;
        /// <summary>
        /// Only valid for pre-spawned ghost, used to the re-assign the correct index to the <see cref="SceneSection"/> shared
        /// component when an pre-spawned ghost is re-spawned (i.e, because of relevancy changes).
        /// The section index is necessary to ensure that, if the sub-scene from which the ghost were created
        /// is requested to be unloaded by destroying all entities that were part of the scene (the default),
        /// the pre-spawned ghost instances are also destroyed.
        /// </summary>
        internal  int SectionIndex;
    }

    /// <summary>
    /// Contains all the system that classify spawned ghost. Runs after the <see cref="GhostReceiveSystem"/> system.
    /// Your custom classification system should be updated into this group.
    /// <code>
    /// [UpdateInGroup(typeof(GhostSpawnClassificationSystemGroup))]
    /// public partial struct MyCustomClassificationSystemGroup
    /// {
    ///    ...
    /// }
    /// </code>
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation, WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(GhostInputSystemGroup))]
    public partial class GhostSpawnClassificationSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// The default GhostSpawnClassificationSystem will set the SpawnType to the default specified in the
    /// GhostAuthoringComponent, unless some other classification has already set the SpawnType. This system
    /// will also check ghost owner to set the spawn type correctly for owner predicted ghosts.
    /// For predictive spawning you usually add a system after GhostSpawnClassificationSystem which only looks at
    /// items with SpawnType set to Predicted and set the PredictedSpawnEntity if you find a matching entity.
    /// The reason to put predictive spawn systems after the default is so the owner predicted logic has run.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSpawnClassificationSystemGroup))]
    [CreateAfter(typeof(GhostCollectionSystem))]
    [CreateAfter(typeof(GhostReceiveSystem))]
    [BurstCompile]
    public partial struct GhostSpawnClassificationSystem : ISystem
    {
        private SnapshotDataLookupHelper m_spawnBufferHelper;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_spawnBufferHelper = new SnapshotDataLookupHelper(ref state,
                SystemAPI.GetSingletonEntity<GhostCollection>(),
                SystemAPI.GetSingletonEntity<SpawnedGhostEntityMap>());
            state.RequireForUpdate<NetworkId>();
            state.RequireForUpdate<GhostCollection>();
            state.RequireForUpdate<GhostSpawnQueue>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_spawnBufferHelper.Update(ref state);
            var classificationJob = new GhostSpawnClassification
            {
                SpawnBufferLookupHelper = m_spawnBufferHelper,
                networkId = SystemAPI.GetSingleton<NetworkId>().Value
            };
            state.Dependency = classificationJob.Schedule(state.Dependency);
        }
        [WithAll(typeof(GhostSpawnQueue))]
        [BurstCompile]
        partial struct GhostSpawnClassification : IJobEntity
        {
            public SnapshotDataLookupHelper SpawnBufferLookupHelper;
            public int networkId;
            public void Execute(DynamicBuffer<GhostSpawnBuffer> ghosts, in DynamicBuffer<SnapshotDataBuffer> data)
            {
                var spawnBufferLookup = SpawnBufferLookupHelper.CreateSnapshotBufferLookup();
                for (int i = 0; i < ghosts.Length; ++i)
                {
                    var ghost = ghosts[i];
                    if (ghost.SpawnType == GhostSpawnBuffer.Type.Unknown)
                    {
                        ghost.SpawnType = spawnBufferLookup.GetFallbackPredictionMode(ghost);
                        if(spawnBufferLookup.IsOwnerPredicted(ghost) && spawnBufferLookup.HasGhostOwner(ghost))
                        {
                            // Prediction mode is where the owner i is stored in the snapshot data
                            var ghostOwner = spawnBufferLookup.GetGhostOwner(ghost, data);
                            if(ghostOwner == networkId)
                                ghost.SpawnType = GhostSpawnBuffer.Type.Predicted;
                        }
                        ghosts[i] = ghost;
                    }
                }
            }
        }
    }

    /// <summary>
    /// The default ghost spawn classification system will match predict spawned entities
    /// with new spawns of the same ghost type from server snapshots when their spawn ticks
    /// is within a certain bound (by default 5 ticks).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSpawnClassificationSystemGroup), OrderLast = true)]
    [BurstCompile]
    internal partial struct DefaultGhostSpawnClassificationSystem : ISystem
    {
        /// <summary>
        /// The amount of ticks where the ghost type of the new spawned ghost will be matched within.
        /// </summary>
        const uint k_TickPeriod = 5;

        BufferLookup<PredictedGhostSpawn> m_PredictedGhostSpawnLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_PredictedGhostSpawnLookup = state.GetBufferLookup<PredictedGhostSpawn>();
            state.RequireForUpdate<GhostSpawnQueue>();
            state.RequireForUpdate<PredictedGhostSpawnList>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_PredictedGhostSpawnLookup.Update(ref state);
            var classificationJob = new DefaultGhostSpawnClassificationJob
            {
                spawnListEntity = SystemAPI.GetSingletonEntity<PredictedGhostSpawnList>(),
                spawnListLookup = m_PredictedGhostSpawnLookup
            };
            state.Dependency = classificationJob.Schedule(state.Dependency);
        }

        [WithAll(typeof(GhostSpawnQueue))]
        [BurstCompile]
        partial struct DefaultGhostSpawnClassificationJob : IJobEntity
        {
            public Entity spawnListEntity;
            public BufferLookup<PredictedGhostSpawn> spawnListLookup;

            public void Execute(DynamicBuffer<GhostSpawnBuffer> ghosts)
            {
                var spawnList = spawnListLookup[spawnListEntity];
                for (int i = 0; i < ghosts.Length; ++i)
                {
                    var ghost = ghosts[i];
                    if (ghost.SpawnType != GhostSpawnBuffer.Type.Predicted || ghost.HasClassifiedPredictedSpawn || ghost.PredictedSpawnEntity != Entity.Null)
                        continue;
                    for (int j = 0; j < spawnList.Length; ++j)
                    {
                        if (ghost.GhostType == spawnList[j].ghostType &&
                            math.abs(ghost.ServerSpawnTick.TicksSince(spawnList[j].spawnTick)) < k_TickPeriod)
                        {
                            ghost.PredictedSpawnEntity = spawnList[j].entity;
                            ghost.HasClassifiedPredictedSpawn = true;
                            spawnList[j] = spawnList[spawnList.Length - 1];
                            spawnList.RemoveAt(spawnList.Length - 1);
                            break;
                        }
                    }

                    ghosts[i] = ghost;
                }
            }
        }
    }
}
