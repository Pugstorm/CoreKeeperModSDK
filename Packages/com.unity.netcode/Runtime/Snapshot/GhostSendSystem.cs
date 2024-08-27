#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Networking.Transport;


namespace Unity.NetCode
{
    internal struct GhostCleanup : ICleanupComponentData
    {
        public int ghostId;
        public NetworkTick spawnTick;
        public NetworkTick despawnTick;
    }

    /// <summary>
    /// For internal use only, struct used to pass some data to the code-generate ghost serializer.
    /// </summary>
    public struct GhostSerializerState
    {
        /// <summary>
        /// A readonly accessor to retrieve the <see cref="GhostInstance"/> from an entity reference. Used to
        /// serialize a ghost entity reference.
        /// </summary>
        public ComponentLookup<GhostInstance> GhostFromEntity;
    }

    internal struct GhostSystemConstants
    {
        /// <summary>
        /// The number of ghost snapshots stored internally by the server in
        /// the <see cref="GhostChunkSerializationState"/> and by the client in the <see cref="SnapshotDataBuffer"/>
        /// ring buffer.
        /// Reducing the SnapshotHistorySize would reduce the cost of storage on both server and client but will
        /// affect the server ability to delta compress data. This because, based on the client latency, by the time
        /// the server receive the snapshot acks (inside the client command stream), the slot in which the acked data
        /// was stored could have been overwritten.
        /// The default size is designed to work with a round trip time of about 500ms at 60hz network tick rate.
        /// </summary>
        public const int SnapshotHistorySize = 32;
        public const uint MaxNewPrefabsPerSnapshot = 32u; // At most around half the snapshot can consist of new prefabs to use
        public const int MaxDespawnsPerSnapshot = 100; // At most around one quarter the snapshot can consist of despawns
        /// <summary>
        /// Prepend to all serialized ghosts in the snapshot their compressed size. This can be used by the client
        /// to recover from error condition and to skip ghost data in some situation, for example transitory condition
        /// while streaming in/out scenes.
        /// </summary>
        public const bool SnaphostHasCompressedGhostSize = true;
        /// <summary>
        /// The maximum age of a baseline. If a baseline is older than this limit it will not be used
        /// for delta compression.
        /// </summary>
        /// <remarks>
        /// The index part of a network tick is 31 bits, at most 30 bits can be used without producing negative
        /// values in TicksSince due to wrap around. This adds a margin of 2 bits to that limit.
        /// </remarks>
        public const uint MaxBaselineAge = 1u<<28;
    }

    internal readonly partial struct AspectPacket : IAspect
    {
        public readonly Entity Entity;
        public readonly RefRO<NetworkId> Id;
        public readonly RefRO<EnablePacketLogging> EnablePacketLogging;
        public readonly RefRO<NetworkStreamConnection> Connection;
        public readonly RefRO<NetworkStreamInGame> InGame;
    }

#if UNITY_EDITOR
    internal struct GhostSendSystemAnalyticsData : IComponentData
    {
        public NativeArray<uint> UpdateLenSums;
        public NativeArray<uint> NumberOfUpdates;
    }
#endif

    /// <summary>
    /// Singleton entity that contains all the tweakable settings for the <see cref="GhostSendSystem"/>.
    /// </summary>
    [Serializable]
    public struct GhostSendSystemData : IComponentData
    {
        /// <summary>
        /// Non-zero values for <see cref="MinSendImportance"/> can cause both:
        /// a) 'unchanged chunks that are "new" to a new-joiner' and b) 'newly spawned chunks'
        /// to be ignored by the replication priority system for multiple seconds.
        /// If this behaviour is undesirable, set this to be above <see cref="MinSendImportance"/>.
        /// This multiplies the importance value used on those "new" (to the player or to the world) ghost chunks.
        /// Note: This does not guarantee delivery of all "new" chunks,
        /// it only guarantees that every ghost chunk will get serialized and sent at least once per connection,
        /// as quickly as possible (e.g. assuming you have the bandwidth for it).
        /// </summary>
        public uint FirstSendImportanceMultiplier
        {
            get => m_FirstSendImportanceMultiplier;
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(FirstSendImportanceMultiplier));
#endif
                m_FirstSendImportanceMultiplier = value;
            }
        }

        /// <summary>
        /// If not 0, denotes the desired size of an individual snapshot (unless clobbered by <see cref="NetworkStreamSnapshotTargetSize"/>).
        /// If zero, <see cref="NetworkParameterConstants.MTU"/> is used (minus headers).
        /// </summary>
        public int DefaultSnapshotPacketSize;

        /// <summary>
        /// The minimum importance considered for inclusion in a snapshot. Any ghost importance lower
        /// than this value will not be send every frame even if there is enough space in the packet.
        /// E.g. Value=60, tick-rate=60, ghost.importance=1 implies a ghost will be replicated roughly once per second.
        /// </summary>
        public int MinSendImportance;

        /// <summary>
        /// The minimum importance considered for inclusion in a snapshot after applying distance based
        /// priority scaling. Any ghost importance lower than this value will not be send every frame
        /// even if there is enough space in the packet.
        /// </summary>
        public int MinDistanceScaledSendImportance;

        /// <summary>
        /// The maximum number of chunks the system will try to send to a single connection in a single frame.
        /// A chunk will count as sent even if it does not contain any ghosts which needed to be sent (because
        /// of relevancy or static optimization).
        /// If there are more chunks than this the least important chunks will not be sent even if there is space
        /// in the packet. This can be used to control CPU time on the server.
        /// </summary>
        public int MaxSendChunks;

        /// <summary>
        /// The maximum number of entities the system will try to send to a single connection in a single frame.
        /// An entity will count even if it is not actually sent (because of relevancy or static optimization).
        /// If there are more chunks than this the least important chunks will not be sent even if there is space
        /// in the packet. This can be used to control CPU time on the server.
        /// </summary>
        public int MaxSendEntities;

        /// <summary>
        /// Value used to scale down the importance of chunks where all entities were irrelevant last time it was sent.
        /// The importance is divided by this value. It can be used together with MinSendImportance to make sure
        /// relevancy is not updated every frame for things with low importance.
        /// </summary>
        public int IrrelevantImportanceDownScale
        {
            get => m_IrrelevantImportanceDownScale;
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(IrrelevantImportanceDownScale));
#endif
                m_IrrelevantImportanceDownScale = value;
            }
        }

        /// <summary>
        /// Force all ghosts to use a single baseline. This will reduce CPU usage at the expense of increased
        /// bandwidth usage. This is mostly meant as a way of measuring which ghosts should use static optimization
        /// instead of dynamic. If the bits / ghost does not significantly increase when enabling this the ghost
        /// can use static optimization to save CPU.
        /// </summary>
        public bool ForceSingleBaseline
        {
            get { return m_ForceSingleBaseline == 1; }
            set { m_ForceSingleBaseline = value ? (byte)1 : (byte)0; }
        }
        internal byte m_ForceSingleBaseline;

        /// <summary>
        /// Force all ghosts to use pre serialization. This means part of the serialization will be done once for
        /// all connection instead of once per connection. This can increase CPU time for simple ghosts and ghosts
        /// which are rarely sent. This switch is meant as a way of measuring which ghosts would benefit from using
        /// pre-serialization.
        /// </summary>
        public bool ForcePreSerialize
        {
            get { return m_ForcePreSerialize == 1; }
            set { m_ForcePreSerialize = value ? (byte)1 : (byte)0; }
        }
        internal byte m_ForcePreSerialize;

        /// <summary>
        /// Try to keep the snapshot history buffer for an entity when there is a structucal change.
        /// Doing this will require a lookup and copy of data whenever a ghost has a structucal change
        /// which will add additional CPU cost on the server.
        /// Keeping the snapshot history will not always be possible so this flag does no give a 100% guarantee,
        /// you are expected to measure CPU and bandwidth when changing this.
        /// </summary>
        public bool KeepSnapshotHistoryOnStructuralChange
        {
            get { return m_KeepSnapshotHistoryOnStructuralChange == 1; }
            set { m_KeepSnapshotHistoryOnStructuralChange = value ? (byte)1 : (byte)0; }
        }
        internal byte m_KeepSnapshotHistoryOnStructuralChange;

        /// <summary>
        /// Enable profiling scopes for each component in a ghost. They can help tracking down why a ghost
        /// is expensive to serialize - but they come with a performance cost so they are not enabled by default.
        /// </summary>
        public bool EnablePerComponentProfiling
        {
            get { return m_EnablePerComponentProfiling == 1; }
            set { m_EnablePerComponentProfiling = value ? (byte)1 : (byte)0; }
        }
        internal byte m_EnablePerComponentProfiling;

        /// <summary>
        /// The number of connections to cleanup unused serialization data for in a single tick. Setting this
        /// higher can recover memory faster, but uses more CPU time.
        /// </summary>
        public int CleanupConnectionStatePerTick;

        uint m_FirstSendImportanceMultiplier;
        int m_IrrelevantImportanceDownScale;

        /// <summary>
        /// Value used to set the initial size of the internal temporary stream in which
        /// ghost data is serialized. The default value is 8kb;
        /// <para>
        /// Using a small size will incur in extra serialization costs (because
        /// of multiple round of serialization), while using a larger size provide better performance (overall).
        /// The initial size of the temporary stream is set to be equals to the capacity of the outgoing data
        /// stream (usually an MTU or larger for fragmented payloads).
        /// The suggested default (8kb), while extremely large in respect to the packet size, would allow in general
        /// to be able to to write a large range of mid/small ghost entities type, with varying size (up to hundreds of bytes
        /// each) without incurring in extra serialization overhead.
        /// </para>
        /// </summary>
        public int TempStreamInitialSize
        {
            get => m_TempStreamSize;
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(m_TempStreamSize));
#endif
                m_TempStreamSize = value;
            }
        }

        /// <summary>
        /// When set, enable using any registered <see cref="GhostPrefabCustomSerializer"/> for
        /// serializing ghost chunks.
        /// </summary>
        public int UseCustomSerializer
        {
            get => m_UseCustomSerializer;
            set => m_UseCustomSerializer = value;
        }
        internal int m_TempStreamSize;
        internal int m_UseCustomSerializer;


        internal void Initialize()
        {
            MinSendImportance = 0;
            MinDistanceScaledSendImportance = 0;
            MaxSendChunks = 0;
            MaxSendEntities = 0;
            ForceSingleBaseline = false;
            ForcePreSerialize = false;
            KeepSnapshotHistoryOnStructuralChange = true;
            EnablePerComponentProfiling = false;
            CleanupConnectionStatePerTick = 1;
            m_FirstSendImportanceMultiplier = 1;
            m_IrrelevantImportanceDownScale = 1;
            m_TempStreamSize = 8 * 1024;
        }
    }

    /// <summary>
    /// System present only for servers worlds, and responsible to replicate ghost entities to the clients.
    /// The <see cref="GhostSendSystem"/> is one of the most complex system of the whole package and heavily rely on multi-thread jobs to dispatch ghosts to all connection as much as possible in parallel.
    /// <para>
    /// Ghosts entities are replicated by sending a 'snapshot' of their state to the clients, at <see cref="ClientServerTickRate.NetworkTickRate"/> frequency.
    /// Snaphosts are streamed to the client when their connection is tagged with a <see cref="NetworkStreamInGame"/> component (we usually refere a connection with that tag as "in-game"),
    /// and transmitted using an unrealiable channel. To save bandwith, snapshosts are delta-compressed against the latest reported ones received by the client.
    /// By default, up to 3 baseline are used to delta-compress the data, by using a predictive compression scheme (see <see cref="GhostDeltaPredictor"/>). It is possible
    /// to reduce the number of baseline used (and CPU cycles) using the <see cref="GhostSendSystemData"/> settings.
    /// </para>
    /// The GhostSendSystem is designed to send to each connection <b>one single packet per network update</b>. By default, the system will try to
    /// replicate to the clients all the existing ghost present in the world. When all ghosts cannot be serialized into the same packet,
    /// the enties are prioritized by their importance.
    /// <para>
    /// The base ghost importance can be set at authoring time on the prefab (<see cref="Unity.NetCode.GhostAuthoringComponent"/>);
    /// At runtime the ghost importance is scaled based on:
    /// <para>- age (the last time the entities has been sent)</para>
    /// <para>- scaled by distance, (see <see cref="GhostConnectionPosition"/>, <see cref="GhostDistanceImportance"/></para>
    /// <para>- scaled by custom scaling (see <see cref="GhostImportance"/></para>
    /// </para>
    /// Ghost entities are replicated on "per-chunk" basis; all ghosts for the same chunk, are replicated
    /// together. The importance, as well as the importance scaling, apply to whole chunk.
    /// <para>
    /// The send system can also be configured to send multiple ghost packets per frame and to to use snaphost larger than a single MTU.
    /// In that case, the snapshot packet is sent using another unreliable channel, setup with a <see cref="FragmentationPipelineStage"/>.
    /// </para>
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    [BurstCompile]
    public partial struct GhostSendSystem : ISystem
    {
        NativeParallelHashMap<RelevantGhostForConnection, int> m_GhostRelevancySet;

        EntityQuery ghostQuery;
        EntityQuery ghostSpawnQuery;
        EntityQuery ghostDespawnQuery;
        EntityQuery prespawnSharedComponents;

        EntityQueryMask internalGlobalRelevantQueryMask;
        EntityQueryMask netcodeEmptyQuery;

        EntityQuery connectionQuery;

        NativeQueue<int> m_FreeGhostIds;
        NativeArray<int> m_AllocatedGhostIds;
        NativeList<int> m_DestroyedPrespawns;
        NativeQueue<int> m_DestroyedPrespawnsQueue;
        NativeReference<NetworkTick> m_DespawnAckedByAllTick;
#if UNITY_EDITOR
        NativeArray<uint> m_UpdateLen;
        NativeArray<uint> m_UpdateCounts;
#endif

        NativeList<ConnectionStateData> m_ConnectionStates;
        NativeParallelHashMap<Entity, int> m_ConnectionStateLookup;
        StreamCompressionModel m_CompressionModel;
        NativeParallelHashMap<int, ulong> m_SceneSectionHashLookup;

        NativeList<int> m_ConnectionRelevantCount;
        NativeList<ConnectionStateData> m_ConnectionsToProcess;
#if NETCODE_DEBUG
        EntityQuery m_PacketLogEnableQuery;
        ComponentLookup<PrefabDebugName> m_PrefabDebugNameFromEntity;
        FixedString512Bytes m_LogFolder;
#endif

        NativeParallelHashMap<SpawnedGhost, Entity> m_GhostMap;
        NativeQueue<SpawnedGhost> m_FreeSpawnedGhostQueue;

        Profiling.ProfilerMarker m_PrioritizeChunksMarker;
        Profiling.ProfilerMarker m_GhostGroupMarker;
        static readonly Profiling.ProfilerMarker k_Scheduling = new Profiling.ProfilerMarker("GhostSendSystem_Scheduling");

        GhostPreSerializer m_GhostPreSerializer;
        ComponentLookup<NetworkId> m_NetworkIdFromEntity;
        ComponentLookup<NetworkSnapshotAck> m_SnapshotAckFromEntity;
        ComponentLookup<GhostType> m_GhostTypeFromEntity;
        ComponentLookup<NetworkStreamConnection> m_ConnectionFromEntity;
        ComponentLookup<GhostInstance> m_GhostFromEntity;
        ComponentLookup<NetworkStreamSnapshotTargetSize> m_SnapshotTargetFromEntity;
        ComponentLookup<EnablePacketLogging> m_EnablePacketLoggingFromEntity;

        ComponentTypeHandle<GhostCleanup> m_GhostSystemStateType;
        ComponentTypeHandle<PreSerializedGhost> m_PreSerializedGhostType;
        ComponentTypeHandle<GhostInstance> m_GhostComponentType;
        ComponentTypeHandle<GhostOwner> m_GhostOwnerComponentType;
        ComponentTypeHandle<GhostChildEntity> m_GhostChildEntityComponentType;
        ComponentTypeHandle<PreSpawnedGhostIndex> m_PrespawnedGhostIdType;
        ComponentTypeHandle<GhostType> m_GhostTypeComponentType;

        EntityTypeHandle m_EntityType;
        BufferTypeHandle<GhostGroup> m_GhostGroupType;
        BufferTypeHandle<LinkedEntityGroup> m_LinkedEntityGroupType;
        BufferTypeHandle<PrespawnGhostBaseline> m_PrespawnGhostBaselineType;
        SharedComponentTypeHandle<SubSceneGhostComponentHash> m_SubsceneGhostComponentType;

        BufferLookup<PrespawnGhostIdRange> m_PrespawnGhostIdRangeFromEntity;
        BufferLookup<GhostCollectionPrefabSerializer> m_GhostTypeCollectionFromEntity;
        BufferLookup<GhostCollectionPrefab> m_GhostCollectionFromEntity;
        BufferLookup<GhostComponentSerializer.State> m_GhostComponentCollectionFromEntity;
        BufferLookup<GhostCollectionComponentIndex> m_GhostComponentIndexFromEntity;
        BufferLookup<PrespawnSectionAck> m_PrespawnAckFromEntity;
        BufferLookup<PrespawnSceneLoaded> m_PrespawnSceneLoadedFromEntity;
        ComponentLookup<GhostCollectionCustomSerializers> m_CustomSerializerFromEntity;

        int m_CurrentCleanupConnectionState;
        uint m_SentSnapshots;
        ComponentTypeHandle<GhostImportance> m_GhostImportanceType;

        public void OnCreate(ref SystemState state)
        {
#if NETCODE_DEBUG
            m_LogFolder = NetDebug.LogFolderForPlatform();
            NetDebugInterop.Initialize();
#endif
            ghostQuery = state.GetEntityQuery(ComponentType.ReadOnly<GhostInstance>(), ComponentType.ReadOnly<GhostCleanup>());
            EntityQueryDesc filterSpawn = new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(GhostInstance)},
                None = new ComponentType[] {typeof(GhostCleanup), typeof(PreSpawnedGhostIndex)}
            };
            EntityQueryDesc filterDespawn = new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(GhostCleanup)},
                None = new ComponentType[] {typeof(GhostInstance)}
            };
            ghostSpawnQuery = state.GetEntityQuery(filterSpawn);
            ghostDespawnQuery = state.GetEntityQuery(filterDespawn);
            prespawnSharedComponents = state.GetEntityQuery(ComponentType.ReadOnly<SubSceneGhostComponentHash>());
            internalGlobalRelevantQueryMask = state.GetEntityQuery(ComponentType.ReadOnly<PrespawnSceneLoaded>()).GetEntityQueryMask();
            netcodeEmptyQuery = state.GetEntityQuery(new EntityQueryDesc { None = new ComponentType[] { typeof(GhostInstance) } }).GetEntityQueryMask(); // "default" just matches everything so we need to specify None to have a real "no query is set"

            m_FreeGhostIds = new NativeQueue<int>(Allocator.Persistent);
            m_AllocatedGhostIds = new NativeArray<int>(2, Allocator.Persistent);
            m_AllocatedGhostIds[0] = 1; // To make sure 0 is invalid
            m_AllocatedGhostIds[1] = 1; // To make sure 0 is invalid
            m_DestroyedPrespawns = new NativeList<int>(Allocator.Persistent);
            m_DestroyedPrespawnsQueue = new NativeQueue<int>(Allocator.Persistent);
            m_DespawnAckedByAllTick = new NativeReference<NetworkTick>(Allocator.Persistent);
#if UNITY_EDITOR
#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            m_UpdateLen = new NativeArray<uint>(maxThreadCount, Allocator.Persistent);
            m_UpdateCounts = new NativeArray<uint>(maxThreadCount, Allocator.Persistent);
#endif

            connectionQuery = state.GetEntityQuery(
                ComponentType.ReadWrite<NetworkStreamConnection>(),
                ComponentType.ReadOnly<NetworkStreamInGame>());

            m_ConnectionStates = new NativeList<ConnectionStateData>(256, Allocator.Persistent);
            m_ConnectionStateLookup = new NativeParallelHashMap<Entity, int>(256, Allocator.Persistent);
            m_CompressionModel = StreamCompressionModel.Default;
            m_SceneSectionHashLookup = new NativeParallelHashMap<int, ulong>(256, Allocator.Persistent);

            state.RequireForUpdate<GhostCollection>();

            m_GhostRelevancySet = new NativeParallelHashMap<RelevantGhostForConnection, int>(1024, Allocator.Persistent);
            m_ConnectionRelevantCount = new NativeList<int>(16, Allocator.Persistent);
            m_ConnectionsToProcess = new NativeList<ConnectionStateData>(16, Allocator.Persistent);
            var relevancySingleton = state.EntityManager.CreateEntity(ComponentType.ReadWrite<GhostRelevancy>());
            state.EntityManager.SetName(relevancySingleton, "GhostRelevancy-Singleton");
            SystemAPI.SetSingleton(new GhostRelevancy(m_GhostRelevancySet));

            m_GhostMap = new NativeParallelHashMap<SpawnedGhost, Entity>(1024, Allocator.Persistent);
            m_FreeSpawnedGhostQueue = new NativeQueue<SpawnedGhost>(Allocator.Persistent);

            var spawnedGhostMap = state.EntityManager.CreateEntity(ComponentType.ReadWrite<SpawnedGhostEntityMap>());
            state.EntityManager.SetName(spawnedGhostMap, "SpawnedGhostEntityMapSingleton");
            SystemAPI.SetSingleton(new SpawnedGhostEntityMap{Value = m_GhostMap.AsReadOnly(), SpawnedGhostMapRW = m_GhostMap, ServerDestroyedPrespawns = m_DestroyedPrespawns, m_ServerAllocatedGhostIds = m_AllocatedGhostIds});

            m_PrioritizeChunksMarker = new Profiling.ProfilerMarker("PrioritizeChunks");
            m_GhostGroupMarker = new Profiling.ProfilerMarker("GhostGroup");

#if NETCODE_DEBUG
            m_PacketLogEnableQuery = state.GetEntityQuery(ComponentType.ReadOnly<EnablePacketLogging>());
#endif

            m_GhostPreSerializer = new GhostPreSerializer(state.GetEntityQuery(ComponentType.ReadOnly<GhostInstance>(), ComponentType.ReadOnly<GhostType>(), ComponentType.ReadOnly<PreSerializedGhost>()));

            var dataSingleton = state.EntityManager.CreateEntity(ComponentType.ReadWrite<GhostSendSystemData>());
            state.EntityManager.SetName(dataSingleton, "GhostSystemData-Singleton");
            var data = new GhostSendSystemData();
            data.Initialize();
            SystemAPI.SetSingleton(data);

#if UNITY_EDITOR
            SetupAnalyticsSingleton(state.EntityManager);
#endif

            m_NetworkIdFromEntity = state.GetComponentLookup<NetworkId>();
            m_SnapshotAckFromEntity = state.GetComponentLookup<NetworkSnapshotAck>(false);
            m_GhostTypeFromEntity = state.GetComponentLookup<GhostType>(true);
#if NETCODE_DEBUG
            m_PrefabDebugNameFromEntity = state.GetComponentLookup<PrefabDebugName>(true);
#endif
            m_ConnectionFromEntity = state.GetComponentLookup<NetworkStreamConnection>(true);
            m_GhostFromEntity = state.GetComponentLookup<GhostInstance>(true);
            m_SnapshotTargetFromEntity = state.GetComponentLookup<NetworkStreamSnapshotTargetSize>(true);
            m_EnablePacketLoggingFromEntity = state.GetComponentLookup<EnablePacketLogging>();

            m_GhostSystemStateType = state.GetComponentTypeHandle<GhostCleanup>(true);
            m_PreSerializedGhostType = state.GetComponentTypeHandle<PreSerializedGhost>(true);
            m_GhostComponentType = state.GetComponentTypeHandle<GhostInstance>();
            m_GhostOwnerComponentType = state.GetComponentTypeHandle<GhostOwner>(true);
            m_GhostChildEntityComponentType = state.GetComponentTypeHandle<GhostChildEntity>(true);
            m_PrespawnedGhostIdType = state.GetComponentTypeHandle<PreSpawnedGhostIndex>(true);
            m_GhostTypeComponentType = state.GetComponentTypeHandle<GhostType>(true);
            m_GhostImportanceType = state.GetComponentTypeHandle<GhostImportance>();

            m_EntityType = state.GetEntityTypeHandle();
            m_GhostGroupType = state.GetBufferTypeHandle<GhostGroup>(true);
            m_LinkedEntityGroupType = state.GetBufferTypeHandle<LinkedEntityGroup>(true);
            m_PrespawnGhostBaselineType = state.GetBufferTypeHandle<PrespawnGhostBaseline>(true);
            m_SubsceneGhostComponentType = state.GetSharedComponentTypeHandle<SubSceneGhostComponentHash>();

            m_PrespawnGhostIdRangeFromEntity = state.GetBufferLookup<PrespawnGhostIdRange>();
            m_GhostTypeCollectionFromEntity = state.GetBufferLookup<GhostCollectionPrefabSerializer>(true);
            m_GhostCollectionFromEntity = state.GetBufferLookup<GhostCollectionPrefab>(true);
            m_GhostComponentCollectionFromEntity = state.GetBufferLookup<GhostComponentSerializer.State>(true);
            m_GhostComponentIndexFromEntity = state.GetBufferLookup<GhostCollectionComponentIndex>(true);
            m_CustomSerializerFromEntity = state.GetComponentLookup<GhostCollectionCustomSerializers>(true);
            m_PrespawnAckFromEntity = state.GetBufferLookup<PrespawnSectionAck>(true);
            m_PrespawnSceneLoadedFromEntity = state.GetBufferLookup<PrespawnSceneLoaded>(true);
        }

#if UNITY_EDITOR
        void SetupAnalyticsSingleton(EntityManager entityManager)
        {
            var analyticsSingleton = entityManager.CreateEntity(ComponentType.ReadWrite<GhostSendSystemAnalyticsData>());
            entityManager.SetName(analyticsSingleton, "GhostSystemAnalyticsData-Singleton");
            var analyticsData = new GhostSendSystemAnalyticsData
            {
                UpdateLenSums = m_UpdateLen,
                NumberOfUpdates = m_UpdateCounts,
            };
            SystemAPI.SetSingleton(analyticsData);
        }
#endif

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_GhostPreSerializer.Dispose();
            m_AllocatedGhostIds.Dispose();
            m_FreeGhostIds.Dispose();
            m_DestroyedPrespawns.Dispose();
            m_DestroyedPrespawnsQueue.Dispose();
            m_DespawnAckedByAllTick.Dispose();
            foreach (var connectionState in m_ConnectionStates)
            {
                connectionState.Dispose();
            }
            m_ConnectionStates.Dispose();

            m_ConnectionStateLookup.Dispose();

            m_GhostRelevancySet.Dispose();
            m_ConnectionRelevantCount.Dispose();
            m_ConnectionsToProcess.Dispose();

            state.Dependency.Complete(); // for ghost map access
            m_GhostMap.Dispose();
            m_FreeSpawnedGhostQueue.Dispose();
            m_SceneSectionHashLookup.Dispose();
#if UNITY_EDITOR
            m_UpdateLen.Dispose();
            m_UpdateCounts.Dispose();
#endif
        }

        [BurstCompile]
        struct SpawnGhostJob : IJob
        {
            [ReadOnly] public NativeArray<ConnectionStateData> connectionState;
            public Entity GhostCollectionSingleton;
            [ReadOnly] public BufferLookup<GhostCollectionPrefabSerializer> GhostTypeCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionPrefab> GhostCollectionFromEntity;
            [ReadOnly] public NativeList<ArchetypeChunk> spawnChunks;
            [ReadOnly] public EntityTypeHandle entityType;
            public ComponentTypeHandle<GhostInstance> ghostComponentType;
            public NativeQueue<int> freeGhostIds;
            public NativeArray<int> allocatedGhostIds;
            public EntityCommandBuffer commandBuffer;
            public NativeParallelHashMap<SpawnedGhost, Entity> ghostMap;

            [ReadOnly] public ComponentLookup<GhostType> ghostTypeFromEntity;
            public NetworkTick serverTick;
            public byte forcePreSerialize;
            public NetDebug netDebug;
#if NETCODE_DEBUG
            [ReadOnly] public ComponentLookup<PrefabDebugName> prefabNames;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            [ReadOnly] public ComponentTypeHandle<GhostOwner> ghostOwnerComponentType;
#endif
            public void Execute()
            {
                if (connectionState.Length == 0)
                    return;
                var GhostTypeCollection = GhostTypeCollectionFromEntity[GhostCollectionSingleton];
                var GhostCollection = GhostCollectionFromEntity[GhostCollectionSingleton];
                for (int chunk = 0; chunk < spawnChunks.Length; ++chunk)
                {
                    var entities = spawnChunks[chunk].GetNativeArray(entityType);
                    var ghostTypeComponent = ghostTypeFromEntity[entities[0]];
                    int ghostType;
                    for (ghostType = 0; ghostType < GhostCollection.Length; ++ghostType)
                    {
                        if (GhostCollection[ghostType].GhostType == ghostTypeComponent)
                            break;
                    }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (ghostType >= GhostCollection.Length)
                        throw new InvalidOperationException("Could not find ghost type in the collection");
#endif
                    if (ghostType >= GhostTypeCollection.Length)
                        continue; // serialization data has not been loaded yet
                    var ghosts = spawnChunks[chunk].GetNativeArray(ref ghostComponentType);
                    for (var ent = 0; ent < entities.Length; ++ent)
                    {
                        if (!freeGhostIds.TryDequeue(out var newId))
                        {
                            newId = allocatedGhostIds[0];
                            allocatedGhostIds[0] = newId + 1;
                        }

                        ghosts[ent] = new GhostInstance {ghostId = newId, ghostType = ghostType, spawnTick = serverTick};

                        var spawnedGhost = new SpawnedGhost
                        {
                            ghostId = newId,
                            spawnTick =  serverTick
                        };
                        if (!ghostMap.TryAdd(spawnedGhost, entities[ent]))
                        {
                            netDebug.LogError(FixedString.Format("GhostID {0} already present in the ghost entity map", newId));
                            ghostMap[spawnedGhost] = entities[ent];
                        }

                        var ghostState = new GhostCleanup
                        {
                            ghostId = newId, despawnTick = NetworkTick.Invalid, spawnTick = serverTick
                        };
                        commandBuffer.AddComponent(entities[ent], ghostState);
                        if (forcePreSerialize == 1)
                            commandBuffer.AddComponent<PreSerializedGhost>(entities[ent]);
#if NETCODE_DEBUG
                        FixedString64Bytes prefabNameString = default;
                        if (prefabNames.HasComponent(GhostCollection[ghostType].GhostPrefab))
                            prefabNameString.Append(prefabNames[GhostCollection[ghostType].GhostPrefab].PrefabName);
                        netDebug.DebugLog(FixedString.Format("[Spawn] GID:{0} Prefab:{1} TypeID:{2} spawnTick:{3}", newId, prefabNameString, ghostType, serverTick.ToFixedString()));
#endif
                    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (GhostTypeCollection[ghostType].PredictionOwnerOffset != 0)
                    {
                        if (!spawnChunks[chunk].Has(ref ghostOwnerComponentType))
                        {
                            netDebug.LogError(FixedString.Format("Ghost type is owner predicted but does not have a GhostOwner {0}, {1}", ghostType, ghostTypeComponent.guid0));
                            continue;
                        }
                        if (GhostTypeCollection[ghostType].OwnerPredicted != 0)
                        {
                            // Validate that the entity has a GhostOwner and that the value in the GhostOwner has been initialized
                            var ghostOwners = spawnChunks[chunk].GetNativeArray(ref ghostOwnerComponentType);
                            for (int ent = 0; ent < ghostOwners.Length; ++ent)
                            {
                               if (ghostOwners[ent].NetworkId == 0)
                               {
                                   netDebug.LogError("Trying to spawn an owner predicted ghost which does not have a valid owner set. When using owner prediction you must set GhostOwner.NetworkId when spawning the ghost. If the ghost is not owned by a player you can set NetworkId to -1.");
                               }
                            }
                        }
                    }
#endif
                }
            }
        }

        [BurstCompile]
        struct SerializeJob : IJobParallelForDefer
        {
            public DynamicTypeList DynamicGhostCollectionComponentTypeList;
            public Entity GhostCollectionSingleton;
            [ReadOnly] public BufferLookup<GhostComponentSerializer.State> GhostComponentCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionPrefabSerializer> GhostTypeCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionComponentIndex> GhostComponentIndexFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionPrefab> GhostCollectionFromEntity;
            [NativeDisableContainerSafetyRestriction] DynamicBuffer<GhostComponentSerializer.State> GhostComponentCollection;
            [NativeDisableContainerSafetyRestriction] DynamicBuffer<GhostCollectionPrefabSerializer> GhostTypeCollection;
            [NativeDisableContainerSafetyRestriction] DynamicBuffer<GhostCollectionComponentIndex> GhostComponentIndex;
            public ConcurrentDriverStore concurrentDriverStore;
            [ReadOnly] public NativeList<ArchetypeChunk> despawnChunks;
            [ReadOnly] public NativeList<ArchetypeChunk> ghostChunks;

            [ReadOnly] public NativeArray<ConnectionStateData> connectionState;
            [NativeDisableParallelForRestriction] public ComponentLookup<NetworkSnapshotAck> ackFromEntity;
            [ReadOnly] public ComponentLookup<NetworkStreamConnection> connectionFromEntity;
            [ReadOnly] public ComponentLookup<NetworkId> networkIdFromEntity;

            [ReadOnly] public EntityTypeHandle entityType;
            [ReadOnly] public ComponentTypeHandle<GhostInstance> ghostComponentType;
            [ReadOnly] public ComponentTypeHandle<GhostCleanup> ghostSystemStateType;
            [ReadOnly] public ComponentTypeHandle<PreSerializedGhost> preSerializedGhostType;
            [ReadOnly] public BufferTypeHandle<GhostGroup> ghostGroupType;
            [ReadOnly] public ComponentTypeHandle<GhostChildEntity> ghostChildEntityComponentType;
            [ReadOnly] public ComponentTypeHandle<PreSpawnedGhostIndex> prespawnGhostIdType;
            [ReadOnly] public SharedComponentTypeHandle<SubSceneGhostComponentHash> subsceneHashSharedTypeHandle;

            public GhostRelevancyMode relevancyMode;
            [ReadOnly] public NativeParallelHashMap<RelevantGhostForConnection, int> relevantGhostForConnection;
            [ReadOnly] public NativeArray<int> relevantGhostCountForConnection;
            [ReadOnly] public EntityQueryMask userGlobalRelevantMask;
            [ReadOnly] public EntityQueryMask internalGlobalRelevantMask;

#if UNITY_EDITOR || NETCODE_DEBUG
            [NativeDisableParallelForRestriction] public NativeArray<uint> netStatsBuffer;
#pragma warning disable 649
            [NativeSetThreadIndex] public int ThreadIndex;
#pragma warning restore 649
            public int netStatStride;
            public int netStatSize;
#endif
            [ReadOnly] public StreamCompressionModel compressionModel;

            [ReadOnly] public ComponentLookup<GhostInstance> ghostFromEntity;

            public NetworkTick currentTick;
            public uint localTime;

            public PortableFunctionPointer<GhostImportance.BatchScaleImportanceDelegate> BatchScaleImportance;
            public PortableFunctionPointer<GhostImportance.ScaleImportanceDelegate> ScaleGhostImportance;

            [ReadOnly] public DynamicSharedComponentTypeHandle ghostImportancePerChunkTypeHandle;
            [NativeDisableUnsafePtrRestriction] [ReadOnly] public IntPtr ghostImportanceDataIntPtr;
            [ReadOnly] public DynamicComponentTypeHandle ghostConnectionDataTypeHandle;
            public int ghostConnectionDataTypeSize;
            [ReadOnly] public ComponentLookup<NetworkStreamSnapshotTargetSize> snapshotTargetSizeFromEntity;
            [ReadOnly] public ComponentLookup<GhostType> ghostTypeFromEntity;
            [ReadOnly] public NativeArray<int> allocatedGhostIds;
            [ReadOnly] public NativeList<int> prespawnDespawns;

            [ReadOnly] public EntityStorageInfoLookup childEntityLookup;
            [ReadOnly] public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupType;
            [ReadOnly] public BufferTypeHandle<PrespawnGhostBaseline> prespawnBaselineTypeHandle;
            [ReadOnly] public NativeParallelHashMap<int, ulong> SubSceneHashSharedIndexMap;
            public uint CurrentSystemVersion;
            public NetDebug netDebug;
#if NETCODE_DEBUG
            public PacketDumpLogger netDebugPacket;
            [ReadOnly] public ComponentLookup<PrefabDebugName> prefabNamesFromEntity;
            [ReadOnly] public ComponentLookup<EnablePacketLogging> enableLoggingFromEntity;
            public FixedString32Bytes timestamp;
            public byte enablePerComponentProfiling;
            byte enablePacketLogging;
#endif

            public Entity prespawnSceneLoadedEntity;
            [ReadOnly] public BufferLookup<PrespawnSectionAck> prespawnAckFromEntity;
            [ReadOnly] public BufferLookup<PrespawnSceneLoaded> prespawnSceneLoadedFromEntity;

            Entity connectionEntity;
            UnsafeParallelHashMap<int, NetworkTick> clearHistoryData;
            ConnectionStateData.GhostStateList ghostStateData;
            int connectionIdx;

            public Profiling.ProfilerMarker prioritizeChunksMarker;
            public Profiling.ProfilerMarker ghostGroupMarker;

            public uint FirstSendImportanceMultiplier;
            public int MinSendImportance;
            public int MinDistanceScaledSendImportance;
            public int MaxSendChunks;
            public int MaxSendEntities;
            public int IrrelevantImportanceDownScale;
            public int useCustomSerializer;
            public byte forceSingleBaseline;
            public byte keepSnapshotHistoryOnStructuralChange;
            public byte snaphostHasCompressedGhostSize;
            public int defaultSnapshotPacketSize;
            public int initialTempWriterCapacity;

            [ReadOnly] public NativeParallelHashMap<ArchetypeChunk, SnapshotPreSerializeData> SnapshotPreSerializeData;
#if UNITY_EDITOR
            [NativeDisableParallelForRestriction] public NativeArray<uint> UpdateLen;
            [NativeDisableParallelForRestriction] public NativeArray<uint> UpdateCounts;
#endif

            public unsafe void Execute(int idx)
            {
                DynamicComponentTypeHandle* ghostChunkComponentTypesPtr = DynamicGhostCollectionComponentTypeList.GetData();
                int ghostChunkComponentTypesLength = DynamicGhostCollectionComponentTypeList.Length;
                GhostComponentCollection = GhostComponentCollectionFromEntity[GhostCollectionSingleton];
                GhostTypeCollection = GhostTypeCollectionFromEntity[GhostCollectionSingleton];
                GhostComponentIndex = GhostComponentIndexFromEntity[GhostCollectionSingleton];

                connectionIdx = idx;
                var curConnectionState = connectionState[connectionIdx];
                connectionEntity = curConnectionState.Entity;
                clearHistoryData = curConnectionState.ClearHistory;

                curConnectionState.EnsureGhostStateCapacity(allocatedGhostIds[0], allocatedGhostIds[1]);
                ghostStateData = curConnectionState.GhostStateData;
#if NETCODE_DEBUG
                netDebugPacket = curConnectionState.NetDebugPacket;
                enablePacketLogging = enableLoggingFromEntity.HasComponent(connectionEntity) ? (byte)1 :(byte)0;
                if ((enablePacketLogging == 1) && !netDebugPacket.IsCreated)
                {
                    netDebug.LogError("GhostSendSystem: Packet logger has not been set. Aborting.");
                    return;
                }
#endif
                var connectionId = connectionFromEntity[connectionEntity].Value;
                var concurrent = concurrentDriverStore.GetConcurrentDriver(connectionFromEntity[connectionEntity].DriverId);
                var driver = concurrent.driver;
                var unreliablePipeline = concurrent.unreliablePipeline;
                var unreliableFragmentedPipeline = concurrent.unreliableFragmentedPipeline;
                if (driver.GetConnectionState(connectionId) != NetworkConnection.State.Connected)
                    return;
                int maxSnapshotSizeWithoutFragmentation = NetworkParameterConstants.MTU - driver.MaxHeaderSize(unreliablePipeline);


                int targetSnapshotSize = maxSnapshotSizeWithoutFragmentation;
                if (snapshotTargetSizeFromEntity.HasComponent(connectionEntity))
                {
                    targetSnapshotSize = snapshotTargetSizeFromEntity[connectionEntity].Value;
                }
                else if (defaultSnapshotPacketSize > 0)
                {
                    targetSnapshotSize = math.min(defaultSnapshotPacketSize, targetSnapshotSize);
                }

                if (prespawnSceneLoadedEntity != Entity.Null)
                {
                    PrespawnHelper.UpdatePrespawnAckSceneMap(ref curConnectionState,
                        prespawnSceneLoadedEntity, prespawnAckFromEntity, prespawnSceneLoadedFromEntity);
                }

                var serializeResult = default(SerializeEnitiesResult);
                while (serializeResult != SerializeEnitiesResult.Abort &&
                       serializeResult != SerializeEnitiesResult.Ok)
                {
                    // If the requested packet size if larger than one MTU we have to use the fragmentation pipeline
                    var pipelineToUse = (targetSnapshotSize <= maxSnapshotSizeWithoutFragmentation) ? unreliablePipeline : unreliableFragmentedPipeline;
                    var result = driver.BeginSend(pipelineToUse, connectionId, out var dataStream, targetSnapshotSize);
                    if ((int)Networking.Transport.Error.StatusCode.Success == result)
                    {
                        serializeResult = SerializeEnitiesResult.Unknown;
                        try
                        {
                            ref var snapshotAck = ref ackFromEntity.GetRefRW(connectionEntity).ValueRW;
                            serializeResult = sendEntities(ref dataStream, snapshotAck, ghostChunkComponentTypesPtr, ghostChunkComponentTypesLength);
                            if (serializeResult == SerializeEnitiesResult.Ok)
                            {
                                if ((result = driver.EndSend(dataStream)) >= (int) Networking.Transport.Error.StatusCode.Success)
                                {
                                    snapshotAck.CurrentSnapshotSequenceId++;
                                }
                                else
                                {
                                    netDebug.LogWarning($"Failed to send a snapshot to a client with EndSend error: {result}!");
                                }
                            }
                            else
                            {
                                driver.AbortSend(dataStream);
                            }
                        }
                        finally
                        {

                            //Finally is always called for non butsted code because there is a try-catch in outer caller (worldunmanged)
                            //regardless of the exception thrown (even invalidprogramexception).
                            //For bursted code, the try-finally has some limitation but it is still unwinding the blocks in the correct order
                            //(not in all cases, but it the one used here everything work fine).
                            //In general, the unhandled error and exceptions are all cought first by the outermost try-catch (world unmanged)
                            //and then the try-finally are called in reverse order (stack unwiding).
                            //There are two exeption handling in the ghost send system:
                            //- the one here, that is responsible to abort the data stream.
                            //- one inside the sendEntities method itself, that try to revert some internal state (i.e: the despawn ghost)
                            //
                            //The innermost finally is called first and do not abort the streams.
                            if (serializeResult == SerializeEnitiesResult.Unknown)
                                driver.AbortSend(dataStream);
                        }
                    }
                    else
                    {
                        netDebug.LogError($"Failed to send a snapshot to a client with BeginSend error: {result}!");
                    }
                    targetSnapshotSize += targetSnapshotSize;
                }
            }

            unsafe SerializeEnitiesResult sendEntities(ref DataStreamWriter dataStream, NetworkSnapshotAck snapshotAckCopy, DynamicComponentTypeHandle* ghostChunkComponentTypesPtr, int ghostChunkComponentTypesLength)
            {
#if NETCODE_DEBUG
                FixedString512Bytes debugLog = default;
                if (enablePacketLogging == 1)
                    debugLog = FixedString.Format("\n\n[{0}]", timestamp);
#endif
                var serializerState = new GhostSerializerState
                {
                    GhostFromEntity = ghostFromEntity
                };
                var NetworkId = networkIdFromEntity[connectionEntity].Value;
                var ackTick = snapshotAckCopy.LastReceivedSnapshotByRemote;

                dataStream.WriteByte((byte) NetworkStreamProtocol.Snapshot);

                dataStream.WriteUInt(localTime);
                uint returnTime = snapshotAckCopy.LastReceivedRemoteTime;
                if (returnTime != 0)
                    returnTime += (localTime - snapshotAckCopy.LastReceiveTimestamp);
                dataStream.WriteUInt(returnTime);
                dataStream.WriteInt(snapshotAckCopy.ServerCommandAge);
                dataStream.WriteByte(snapshotAckCopy.CurrentSnapshotSequenceId);
                dataStream.WriteUInt(currentTick.SerializedData);
#if NETCODE_DEBUG
                if (enablePacketLogging == 1)
                {
                    debugLog.Append(FixedString.Format(" Protocol:{0} LocalTime:{1} ReturnTime:{2} CommandAge:{3}",
                        (byte) NetworkStreamProtocol.Snapshot, localTime, returnTime, snapshotAckCopy.ServerCommandAge));
                    debugLog.Append(FixedString.Format(" Tick: {0}, SSId: {1}\n", currentTick.ToFixedString(), snapshotAckCopy.CurrentSnapshotSequenceId));
                }
#endif

                // Write the list of ghost snapshots the client has not acked yet
                var GhostCollection = GhostCollectionFromEntity[GhostCollectionSingleton];
                uint numLoadedPrefabs = snapshotAckCopy.NumLoadedPrefabs;
                if (numLoadedPrefabs > (uint)GhostCollection.Length)
                {
                    // The received ghosts by remote might not have been updated yet
                    numLoadedPrefabs = 0;
                    // Override the copy of the snapshot ack so the GhostChunkSerializer can skip this check
                    snapshotAckCopy.NumLoadedPrefabs = 0;
                }
                uint numNewPrefabs = math.min((uint)GhostCollection.Length - numLoadedPrefabs, GhostSystemConstants.MaxNewPrefabsPerSnapshot);
                dataStream.WritePackedUInt(numNewPrefabs, compressionModel);
#if NETCODE_DEBUG
                if (enablePacketLogging == 1)
                    debugLog.Append(FixedString.Format("NewPrefabs: {0}", numNewPrefabs));
#endif
                if (numNewPrefabs > 0)
                {
                    dataStream.WriteUInt(numLoadedPrefabs);
#if NETCODE_DEBUG
                    if (enablePacketLogging == 1)
                    {
                        debugLog.Append(FixedString.Format(" LoadedPrefabs: {0}\n", numNewPrefabs));
                    }
#endif
                    int prefabNum = (int)numLoadedPrefabs;
                    for (var i = 0; i < numNewPrefabs; ++i)
                    {
                        dataStream.WriteUInt(GhostCollection[prefabNum].GhostType.guid0);
                        dataStream.WriteUInt(GhostCollection[prefabNum].GhostType.guid1);
                        dataStream.WriteUInt(GhostCollection[prefabNum].GhostType.guid2);
                        dataStream.WriteUInt(GhostCollection[prefabNum].GhostType.guid3);
                        dataStream.WriteULong(GhostCollection[prefabNum].Hash);
#if NETCODE_DEBUG
                        if (enablePacketLogging == 1)
                        {
                            debugLog.Append(FixedString.Format("\t {0}-{1}-{2}-{3}",
                                GhostCollection[prefabNum].GhostType.guid0, GhostCollection[prefabNum].GhostType.guid1,
                                GhostCollection[prefabNum].GhostType.guid2,
                                GhostCollection[prefabNum].GhostType.guid3));
                            debugLog.Append(FixedString.Format(" Hash:{0}\n", GhostCollection[prefabNum].Hash));
                        }
#endif
                        ++prefabNum;
                    }
                }


                NativeList<PrioChunk> serialChunks;
                int totalCount, maxCount;
                if (BatchScaleImportance.Ptr.IsCreated)
                {
                    prioritizeChunksMarker.Begin();
                    serialChunks = GatherGhostChunksBatch(out maxCount, out totalCount);
                    prioritizeChunksMarker.End();
                }
                else
                {
                    prioritizeChunksMarker.Begin();
                    serialChunks = GatherGhostChunks(out maxCount, out totalCount);
                    prioritizeChunksMarker.End();
                }

                switch (relevancyMode)
                {
                case GhostRelevancyMode.SetIsRelevant:
                    totalCount = relevantGhostCountForConnection[NetworkId];
                    break;
                case GhostRelevancyMode.SetIsIrrelevant:
                    totalCount -= relevantGhostCountForConnection[NetworkId];
                    break;
                }
                dataStream.WritePackedUInt((uint)totalCount, compressionModel);
#if NETCODE_DEBUG
                if (enablePacketLogging == 1)
                {
                    debugLog.Append(FixedString.Format(" Total: {0}\n", totalCount));
                    // Snapshot header, snapshot data follows
                    netDebugPacket.Log(debugLog);
                    netDebugPacket.Log(FixedString.Format("\t(RelevancyMode: {0})\n", (int) relevancyMode));
                }
#endif
                var lenWriter = dataStream;
                dataStream.WriteUInt(0);
                dataStream.WriteUInt(0);
#if UNITY_EDITOR || NETCODE_DEBUG
                int startPos = dataStream.LengthInBits;
#endif
                uint despawnLen = WriteDespawnGhosts(ref dataStream, ackTick, in snapshotAckCopy);
                if (dataStream.HasFailedWrites)
                {
                    RevertDespawnGhostState(ackTick);
#if NETCODE_DEBUG
                    if (enablePacketLogging == 1)
                        netDebugPacket.Log("Failed to finish writing snapshot.\n");
#endif
                    return SerializeEnitiesResult.Failed;
                }
#if UNITY_EDITOR || NETCODE_DEBUG
                var netStats = netStatsBuffer.GetSubArray(netStatStride * ThreadIndex, netStatSize);
                netStats[1] = netStats[1] + despawnLen;
                netStats[2] = netStats[2] + (uint) (dataStream.LengthInBits - startPos);
                netStats[3] = 0;
                startPos = dataStream.LengthInBits;
#endif

                uint updateLen = 0;
                bool didFillPacket = false;
                var serializerData = new GhostChunkSerializer
                {
                    GhostComponentCollection = GhostComponentCollection,
                    GhostTypeCollection = GhostTypeCollection,
                    GhostComponentIndex = GhostComponentIndex,
                    PrespawnIndexType = prespawnGhostIdType,
                    ghostGroupMarker = ghostGroupMarker,
                    childEntityLookup = childEntityLookup,
                    linkedEntityGroupType = linkedEntityGroupType,
                    prespawnBaselineTypeHandle = prespawnBaselineTypeHandle,
                    entityType = entityType,
                    ghostComponentType = ghostComponentType,
                    ghostSystemStateType = ghostSystemStateType,
                    preSerializedGhostType = preSerializedGhostType,
                    ghostChildEntityComponentType = ghostChildEntityComponentType,
                    ghostGroupType = ghostGroupType,
                    snapshotAck = snapshotAckCopy,
                    chunkSerializationData = *connectionState[connectionIdx].SerializationState,
                    ghostChunkComponentTypesPtr = ghostChunkComponentTypesPtr,
                    ghostChunkComponentTypesLength = ghostChunkComponentTypesLength,
                    currentTick = currentTick,
                    compressionModel = compressionModel,
                    serializerState = serializerState,
                    NetworkId = NetworkId,
                    relevantGhostForConnection = relevantGhostForConnection,
                    relevancyMode = relevancyMode,
                    userGlobalRelevantMask = userGlobalRelevantMask,
                    internalGlobalRelevantMask = internalGlobalRelevantMask,
                    clearHistoryData = clearHistoryData,
                    ghostStateData = ghostStateData,
                    CurrentSystemVersion = CurrentSystemVersion,

                    netDebug = netDebug,
#if NETCODE_DEBUG
                    netDebugPacket = netDebugPacket,
                    enablePacketLogging = enablePacketLogging,
                    enablePerComponentProfiling = enablePerComponentProfiling,
#endif
                    SnapshotPreSerializeData = SnapshotPreSerializeData,
                    forceSingleBaseline = forceSingleBaseline,
                    keepSnapshotHistoryOnStructuralChange = keepSnapshotHistoryOnStructuralChange,
                    snaphostHasCompressedGhostSize = snaphostHasCompressedGhostSize,
                    useCustomSerializer = (byte)useCustomSerializer,
                };
                //usa a better initial size for the temp stream. There is one big of a problem with the current
                //serialization logic: multiple full serialization loops in case the chunk does not fit into the current
                //temp stream. That can happen if either:
                //There are big ghosts (large components or buffers)
                //Lots of small/mid size ghosts (so > 30/40 per chunks) and because of the serialized size
                //(all components termp data are aligned to 32 bits) we can end up in the sitation we are consuming up to 2/3x the size
                //of the temp stream.
                //When that happen, we re-fetch and all data (again and again, also for child) and we retry again.
                //This is EXTREMELY SLOW. By allocating at leat 8/16kb (instead of 1MTU) we ensure that does not happen (or at least quite rarely)
                //gaining already a 2/3 perf out of the box in many cases. I choose a 8 kb buffer, that is a little large, but
                //give overall a very good boost in many scenario.
                //The parameter is tunable though via GhostSendSystemData, so you can tailor that to the game as necessary.
                var streamCapacity = useCustomSerializer == 0
                    ? math.max(initialTempWriterCapacity, dataStream.Capacity)
                    : dataStream.Capacity;
                serializerData.AllocateTempData(maxCount, streamCapacity);
                var numChunks = serialChunks.Length;
                if (MaxSendChunks > 0 && numChunks > MaxSendChunks)
                    numChunks = MaxSendChunks;


                for (int pc = 0; pc < numChunks; ++pc)
                {
                    var chunk = serialChunks[pc].chunk;
                    var ghostType = serialChunks[pc].ghostType;
#if NETCODE_DEBUG
                    serializerData.ghostTypeName = default;
                    if (enablePacketLogging == 1)
                    {
                        if (prefabNamesFromEntity.HasComponent(GhostCollection[ghostType].GhostPrefab))
                            serializerData.ghostTypeName.Append(
                                prefabNamesFromEntity[GhostCollection[ghostType].GhostPrefab].PrefabName);
                    }
#endif

                    // Do not send entities with a ghost type which the client has not acked yet
                    if (ghostType >= numLoadedPrefabs)
                    {
#if NETCODE_DEBUG
                        if (enablePacketLogging == 1)
                            netDebugPacket.Log(FixedString.Format(
                                "Skipping {0} in snapshot as client has not acked the spawn for it.\n",
                                serializerData.ghostTypeName));
#endif
                        continue;
                    }

#if UNITY_EDITOR || NETCODE_DEBUG
                    var prevUpdateLen = updateLen;
#endif
                    var serializeResult = default(SerializeEnitiesResult);
                    try
                    {
                        serializeResult = serializerData.SerializeChunk(serialChunks[pc], ref dataStream,
                            ref updateLen, ref didFillPacket);
                    }
                    finally
                    {
                        //If the result is unknown, an exception may have been throwm inside the serializeChunk.
                        if (serializeResult == SerializeEnitiesResult.Unknown)
                        {
                            //Do not abort the stream. It is aborted in the outhermost loop.
                            RevertDespawnGhostState(ackTick);
                        }
                    }

#if UNITY_EDITOR || NETCODE_DEBUG
                    if (updateLen > prevUpdateLen)
                    {
                        // indexing starts at 4 due to slots 0-3 are reserved.
                        netStats[ghostType * 3 + 4] = netStats[ghostType * 3 + 4] + updateLen - prevUpdateLen;
                        netStats[ghostType * 3 + 5] =
                            netStats[ghostType * 3 + 5] + (uint)(dataStream.LengthInBits - startPos);
                        netStats[ghostType * 3 + 6] = netStats[ghostType * 3 + 6] + 1; // chunk count
                        startPos = dataStream.LengthInBits;
                    }
#endif
                    if (serializeResult == SerializeEnitiesResult.Failed)
                        break;

                    if (MaxSendEntities > 0)
                    {
                        MaxSendEntities -= chunk.Count;
                        if (MaxSendEntities <= 0)
                            break;
                    }
                }

                if (dataStream.HasFailedWrites)
                {
                    RevertDespawnGhostState(ackTick);
                    netDebug.LogError("Size limitation on snapshot did not prevent all errors");
                    return SerializeEnitiesResult.Abort;
                }

                dataStream.Flush();
                lenWriter.WriteUInt(despawnLen);
                lenWriter.WriteUInt(updateLen);
#if UNITY_EDITOR
                if (updateLen > 0)
                {
                    UpdateLen[ThreadIndex] += updateLen;
                    UpdateCounts[ThreadIndex] += 1;
                }
#endif
#if NETCODE_DEBUG
                if (enablePacketLogging == 1)
                    netDebugPacket.Log(FixedString.Format("Despawn: {0} Update:{1} {2}B\n\n", despawnLen, updateLen, dataStream.Length));
#endif

                if (didFillPacket && updateLen == 0)
                {
                    RevertDespawnGhostState(ackTick);
                    return SerializeEnitiesResult.Failed;
                }
                return SerializeEnitiesResult.Ok;
            }

            // Revert all state updates that happened from failing to write despawn packets
            void RevertDespawnGhostState(NetworkTick ackTick)
            {
                ghostStateData.AckedDespawnTick = ackTick;
                ghostStateData.DespawnRepeatCount = 0;
                for (var chunk = 0; chunk < despawnChunks.Length; ++chunk)
                {
                    var ghostStates = despawnChunks[chunk].GetNativeArray(ref ghostSystemStateType);
                    for (var ent = 0; ent < ghostStates.Length; ++ent)
                    {
                        ref var state = ref ghostStateData.GetGhostState(ghostStates[ent]);
                        state.LastDespawnSendTick = NetworkTick.Invalid;
                        if (ghostStateData.AckedDespawnTick.IsValid &&
                            !ghostStates[ent].despawnTick.IsNewerThan(ghostStateData.AckedDespawnTick))
                        {
                            var despawnAckTick = ghostStates[ent].despawnTick;
                            despawnAckTick.Decrement();
                            ghostStateData.AckedDespawnTick = despawnAckTick;
                        }
                    }
                }
                var irrelevant = clearHistoryData.GetKeyArray(Allocator.Temp);
                for (int i = 0; i < irrelevant.Length; ++i)
                {
                    var irrelevantGhost = irrelevant[i];
                    clearHistoryData[irrelevantGhost] = NetworkTick.Invalid;
                }

            }

            /// Write a list of all ghosts which have been despawned after the last acked packet. Return the number of ghost ids written
            uint WriteDespawnGhosts(ref DataStreamWriter dataStream, NetworkTick ackTick, in NetworkSnapshotAck snapshotAck)
            {
                //For despawns we use a custom ghost id encoding.
                //We left shift the ghost id by one bit and exchange the LSB <> MSB.
                //This way we can encode the prespawn and runtime ghosts with just 1 more bit per entity (on average)
                uint EncodeGhostId(int ghostId)
                {
                    uint encodedGhostId = (uint)ghostId;
                    encodedGhostId = (encodedGhostId << 1) | (encodedGhostId >> 31);
                    return encodedGhostId;
                }
#if NETCODE_DEBUG
                FixedString512Bytes debugLog = default;
                FixedString64Bytes msg = "\t[Despawn IDs]\n";
#endif
                uint despawnLen = 0;
                ghostStateData.AckedDespawnTick = ackTick;
                uint despawnRepeatTicks = 5u;
                uint repeatNextFrame = 0;
                uint repeatThisFrame = ghostStateData.DespawnRepeatCount;
                for (var chunk = 0; chunk < despawnChunks.Length; ++chunk)
                {
                    var ghostStates = despawnChunks[chunk].GetNativeArray(ref ghostSystemStateType);
                    for (var ent = 0; ent < ghostStates.Length; ++ent)
                    {
                        ref var state = ref ghostStateData.GetGhostState(ghostStates[ent]);
                        // If the despawn has already been acked we can just mark it as not relevant to make sure it is not sent again
                        // All desapwn messages are sent for despawnRepeatTicks consecutive frames, if any of those is received the despawn is acked
                        if (state.LastDespawnSendTick.IsValid)
                        {
                            bool isReceived = snapshotAck.IsReceivedByRemote(state.LastDespawnSendTick);
                            var despawnCheckTick = state.LastDespawnSendTick;
                            for (uint i = 1; i < despawnRepeatTicks; ++i)
                            {
                                despawnCheckTick.Increment();
                                isReceived |= snapshotAck.IsReceivedByRemote(despawnCheckTick);
                            }
                            if (isReceived)
                            {
                                // Already despawned - mark it as not relevant to make sure it does not go out of sync if the ack mask is full
                                state.Flags &= (~ConnectionStateData.GhostStateFlags.IsRelevant);
                            }
                        }
                        // not relevant, will be despawned by the relevancy system or is alrady despawned
                        if ((state.Flags & ConnectionStateData.GhostStateFlags.IsRelevant) == 0)
                        {
                            if (!clearHistoryData.ContainsKey(ghostStates[ent].ghostId))
                            {
                                // The ghost is irrelevant and not waiting for relevancy despawn, it is already deleted and can be ignored
                                continue;
                            }
                            else
                            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                                // This path is only expected to be taken when a despawn happened while waiting for relevancy despawn
                                // In that case the LastDespawnSentTick should always be zero
                                UnityEngine.Debug.Assert(!state.LastDespawnSendTick.IsValid);
#endif
                                // Treat this as a regular despawn instead, since regular depsawns have higher priority
                                state.LastDespawnSendTick = clearHistoryData[ghostStates[ent].ghostId];
                                clearHistoryData.Remove(ghostStates[ent].ghostId);
                                state.Flags |= ConnectionStateData.GhostStateFlags.IsRelevant;
                            }
                        }

                        // The despawn is pending or will be sent - update the pending despawn tick
                        if (ghostStateData.AckedDespawnTick.IsValid &&
                            !ghostStates[ent].despawnTick.IsNewerThan(ghostStateData.AckedDespawnTick))
                        {
                            // We are going to send (or wait for) a ghost despawned at tick despawnTick, that means
                            // despawnTick cannot be treated as a tick where all desapwns are acked.
                            // We set the despawnAckTick to despawnTick-1 since that is the newest tick that could possibly have all despawns acked
                            var despawnAckTick = ghostStates[ent].despawnTick;
                            despawnAckTick.Decrement();
                            ghostStateData.AckedDespawnTick = despawnAckTick;
                        }
                        // If the despawn was sent less than despawnRepeatTicks ticks ago we must send it again
                        var despawnRepeatTick = state.LastDespawnSendTick;
                        if (state.LastDespawnSendTick.IsValid)
                            despawnRepeatTick.Add(despawnRepeatTicks);
                        if (!state.LastDespawnSendTick.IsValid || !despawnRepeatTick.IsNewerThan(currentTick))
                        {
                            // Depsawn has been sent, waiting for an ack to see if it needs to be resent
                            if (state.LastDespawnSendTick.IsValid && (!ackTick.IsValid || despawnRepeatTick.IsNewerThan(ackTick)))
                                continue;

                            // We cannot break since all ghosts must be checked for despawn ack tick
                            if (despawnLen+repeatThisFrame >= GhostSystemConstants.MaxDespawnsPerSnapshot)
                                continue;

                            // Update when we last sent this and send it
                            state.LastDespawnSendTick = currentTick;
                            despawnRepeatTick = currentTick;
                            despawnRepeatTick.Add(despawnRepeatTicks);
                        }
                        else
                        {
                            // This is a repeat, it will be counted in despawn length and this reserved length can be reduced
                            --repeatThisFrame;
                        }
                        // Check if this despawn is expected to be resent next tick
                        var nextTick = currentTick;
                        nextTick.Increment();
                        if (despawnRepeatTick.IsNewerThan(nextTick))
                            ++repeatNextFrame;
                        dataStream.WritePackedUInt(EncodeGhostId(ghostStates[ent].ghostId), compressionModel);
#if NETCODE_DEBUG
                        if (enablePacketLogging == 1)
                        {
                            if (despawnLen == 0)
                                debugLog.Append(msg);

                            debugLog.Append(FixedString.Format(" {0}", ghostStates[ent].ghostId));
                        }
#endif
                        ++despawnLen;
                    }
                }
                // Send out the current list of destroyed prespawned entities for despawning for all new client's loaded scenes
                // We do this by adding all despawned prespawn to the list of irrelevant ghosts and rely on relevancy depsawns
                var newPrespawnLoadedRanges = connectionState[connectionIdx].NewLoadedPrespawnRanges;
                if (prespawnDespawns.Length > 0 && newPrespawnLoadedRanges.Length > 0)
                {
                    for (int i = 0; i < prespawnDespawns.Length; ++i)
                    {
                        if(clearHistoryData.ContainsKey(prespawnDespawns[i]))
                            continue;

                        //If not in range, skip
                        var ghostId = prespawnDespawns[i];
                        if(ghostId < newPrespawnLoadedRanges[0].Begin ||
                           ghostId > newPrespawnLoadedRanges[newPrespawnLoadedRanges.Length-1].End)
                            continue;

                        //Todo: can use a binary search, like lower-bound in c++
                        int idx = 0;
                        while (idx < newPrespawnLoadedRanges.Length && ghostId > newPrespawnLoadedRanges[idx].End) ++idx;
                        if(idx < newPrespawnLoadedRanges.Length)
                            clearHistoryData.TryAdd(ghostId, NetworkTick.Invalid);
                    }
                }

                // If relevancy is enabled, despawn all ghosts which are irrelevant and has not been acked
                if (!clearHistoryData.IsEmpty)
                {
#if NETCODE_DEBUG
                    if (enablePacketLogging == 1)
                        msg = "\t[IrrelevantDespawn or PrespawnDespawn IDs]\n";
                    var currentLength = despawnLen;
#endif
                    // Write the despawns
                    var irrelevant = clearHistoryData.GetKeyArray(Allocator.Temp);
                    for (int i = 0; i < irrelevant.Length; ++i)
                    {
                        var irrelevantGhost = irrelevant[i];
                        clearHistoryData.TryGetValue(irrelevantGhost, out var despawnTick);
                        // Check if despawn has been acked, if it has update all state and do not try to send a despawn again
                        if (despawnTick.IsValid)
                        {
                            bool isReceived = snapshotAck.IsReceivedByRemote(despawnTick);
                            var despawnCheckTick = despawnTick;
                            for (uint dst = 1; dst < despawnRepeatTicks; ++dst)
                            {
                                despawnCheckTick.Increment();
                                isReceived |= snapshotAck.IsReceivedByRemote(despawnCheckTick);
                            }
                            if (isReceived)
                            {
                                clearHistoryData.Remove(irrelevantGhost);
                                continue;
                            }
                        }
                        // If the despawn was sent less than despawnRepeatTicks ticks ago we must send it again
                        var resendTick = despawnTick;
                        if (resendTick.IsValid)
                            resendTick.Add(despawnRepeatTicks);
                        if (!despawnTick.IsValid || !resendTick.IsNewerThan(currentTick))
                        {
                            // The despawn has been send and we do not yet know if it needs to be resent, so don't send anything
                            if (despawnTick.IsValid && (!ackTick.IsValid || resendTick.IsNewerThan(ackTick)))
                                continue;

                            if (despawnLen+repeatThisFrame >= GhostSystemConstants.MaxDespawnsPerSnapshot)
                                continue;

                            // Send the despawn and update last tick we did send it
                            clearHistoryData[irrelevantGhost] = currentTick;
                            despawnTick = currentTick;
                            resendTick = currentTick;
                            resendTick.Add(despawnRepeatTicks);
                        }
                        else
                        {
                            // This is a repeat, it will be counted in despawn length and this reserved length can be reduced
                            --repeatThisFrame;
                        }
                        // Check if this despawn is expected to be resetn next tick
                        var nextTick = currentTick;
                        nextTick.Increment();
                        if (resendTick.IsNewerThan(nextTick))
                            ++repeatNextFrame;

                        dataStream.WritePackedUInt(EncodeGhostId(irrelevantGhost), compressionModel);
#if NETCODE_DEBUG
                        if (enablePacketLogging == 1)
                        {
                            if (currentLength == despawnLen)
                                debugLog.Append(msg);
                            debugLog.Append(FixedString.Format(" {0}", irrelevantGhost));
                        }
#endif
                        ++despawnLen;
                    }
                }

                ghostStateData.DespawnRepeatCount = repeatNextFrame;
#if NETCODE_DEBUG
                if ((enablePacketLogging == 1) && debugLog.Length > 0)
                    netDebugPacket.Log(debugLog);
#endif
                return despawnLen;
            }

            int FindGhostTypeIndex(Entity ent)
            {
                var GhostCollection = GhostCollectionFromEntity[GhostCollectionSingleton];
                int ghostType;
                var ghostTypeComponent = ghostTypeFromEntity[ent];
                for (ghostType = 0; ghostType < GhostCollection.Length; ++ghostType)
                {
                    if (GhostCollection[ghostType].GhostType == ghostTypeComponent)
                        break;
                }
                if (ghostType >= GhostCollection.Length)
                {
                    netDebug.LogError("Could not find ghost type in the collection");
                    return -1;
                }
                return ghostType;
            }

            /// Collect a list of all chunks which could be serialized and sent. Sort the list so other systems get it in priority order.
            /// Also cleanup any stale ghost state in the map and create new storage buffers for new chunks so all chunks are in a valid state after this has executed
            unsafe NativeList<PrioChunk> GatherGhostChunks(out int maxCount, out int totalCount)
            {
                var serialChunks = new NativeList<PrioChunk>(ghostChunks.Length, Allocator.Temp);
                maxCount = 0;
                totalCount = 0;

                var connectionChunkInfo = childEntityLookup[connectionEntity];
                var connectionHasConnectionData = TryGetComponentPtrInChunk(connectionChunkInfo, ghostConnectionDataTypeHandle, ghostConnectionDataTypeSize, out var connectionDataPtr);
                var chunkStates = connectionState[connectionIdx].SerializationState;
                var scalePriorities = connectionHasConnectionData && ScaleGhostImportance.Ptr.IsCreated;

                for (int chunk = 0; chunk < ghostChunks.Length; ++chunk)
                {
                    var ghostChunk = ghostChunks[chunk];
                    if (!TryGetChunkStateOrNew(ghostChunk, ref *chunkStates, out var chunkState))
                    {
                        continue;
                    }

                    chunkState.SetLastValidTick(currentTick);

                    totalCount += ghostChunk.Count;
                    maxCount = math.max(maxCount, ghostChunk.Count);

                    //Prespawn ghost chunk should be considered only if the subscene wich they belong to as been loaded (acked) by the client.
                    if (ghostChunk.Has(ref prespawnGhostIdType))
                    {
                        var ackedPrespawnSceneMap = connectionState[connectionIdx].AckedPrespawnSceneMap;
                        //Retrieve the subscene hash from the shared component index.
                        var sharedComponentIndex = ghostChunk.GetSharedComponentIndex(subsceneHashSharedTypeHandle);
                        var hash = SubSceneHashSharedIndexMap[sharedComponentIndex];
                        //Skip the chunk if the client hasn't acked/requested streaming that subscene
                        if (!ackedPrespawnSceneMap.ContainsKey(hash))
                        {
#if NETCODE_DEBUG
                            if (enablePacketLogging == 1)
                                netDebugPacket.Log(FixedString.Format("Skipping prespawn chunk with TypeID:{0} for scene {1} not acked by the client\n", chunkState.ghostType, NetDebug.PrintHex(hash)));
#endif
                            continue;
                        }
                    }

                    if (ghostChunk.Has(ref ghostChildEntityComponentType))
                        continue;

                    var ghostType = chunkState.ghostType;
                    var chunkPriority = chunkState.baseImportance *
                                        currentTick.TicksSince(chunkState.GetLastUpdate());
                    if (chunkState.GetAllIrrelevant())
                        chunkPriority /= IrrelevantImportanceDownScale;
                    if (chunkPriority < MinSendImportance)
                        continue;
                    if (scalePriorities && ghostChunk.Has(ref ghostImportancePerChunkTypeHandle))
                    {
                        unsafe
                        {
                            IntPtr chunkTile = new IntPtr(ghostChunk.GetDynamicSharedComponentDataAddress(ref ghostImportancePerChunkTypeHandle));
                            var func = (delegate *unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int, int>)ScaleGhostImportance.Ptr.Value;
                            chunkPriority = func(connectionDataPtr, ghostImportanceDataIntPtr, chunkTile, chunkPriority);
                        }

                        if (chunkPriority < MinDistanceScaledSendImportance)
                            continue;
                    }

                    var pc = new PrioChunk
                    {
                        chunk = ghostChunk,
                        priority = chunkPriority,
                        startIndex = chunkState.GetStartIndex(),
                        ghostType = ghostType
                    };

                    //Using AddNoResize, while tecnically better because does 0 checks, internally use atomics.
                    //That make that slower.
                    serialChunks.Add(pc);
#if NETCODE_DEBUG
                    if (enablePacketLogging == 1)
                        netDebugPacket.Log(FixedString.Format("Adding chunk ID:{0} TypeID:{1} Priority:{2}\n", chunk, ghostType, chunkPriority));
#endif
                }
                NativeArray<PrioChunk> serialChunkArray = serialChunks.AsArray();
                serialChunkArray.Sort();
                return serialChunks;
            }

            static unsafe IntPtr GetComponentPtrInChunk(
                EntityStorageInfo storageInfo,
                DynamicComponentTypeHandle connectionDataTypeHandle,
                int typeSize)
            {
                var ptr = (byte*)storageInfo.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref connectionDataTypeHandle, typeSize).GetUnsafeReadOnlyPtr();
                ptr += typeSize * storageInfo.IndexInChunk;
                return (IntPtr)ptr;
            }

            unsafe bool TryGetChunkStateOrNew(ArchetypeChunk ghostChunk,
                ref UnsafeHashMap<ArchetypeChunk, GhostChunkSerializationState> chunkStates,
                out GhostChunkSerializationState chunkState)
            {
                if (chunkStates.TryGetValue(ghostChunk, out chunkState))
                {
                    if (chunkState.sequenceNumber == ghostChunk.SequenceNumber)
                    {
                        return true;
                    }

                    chunkState.FreeSnapshotData();
                    chunkStates.Remove(ghostChunk);
                }

                var ghosts = ghostChunk.GetComponentDataPtrRO(ref ghostComponentType);
                if (!TryGetChunkGhostType(ghostChunk, ghosts[0], out var chunkGhostType))
                {
                    return false;
                }

                chunkState.ghostType = chunkGhostType;
                chunkState.sequenceNumber = ghostChunk.SequenceNumber;
                ref readonly var prefabSerializer = ref GhostTypeCollection.ElementAtRO(chunkState.ghostType);
                int serializerDataSize = prefabSerializer.SnapshotSize;
                chunkState.baseImportance = prefabSerializer.BaseImportance;
                chunkState.AllocateSnapshotData(serializerDataSize, ghostChunk.Capacity);
                var importanceTick = currentTick;
                importanceTick.Subtract(FirstSendImportanceMultiplier);
                chunkState.SetLastUpdate(importanceTick);

                chunkStates.TryAdd(ghostChunk, chunkState);
#if NETCODE_DEBUG
                if (enablePacketLogging == 1)
                {
                    netDebugPacket.Log(FixedString.Format(
                        "Chunk archetype changed, allocating new one TypeID:{0} LastUpdate:{1}\n",
                        chunkState.ghostType, chunkState.GetLastUpdate().ToFixedString()));
                }
#endif
                return true;
            }

            bool TryGetChunkGhostType(ArchetypeChunk ghostChunk, in GhostInstance ghost, out int chunkGhostType)
            {
                chunkGhostType = ghost.ghostType;
                // Pre spawned ghosts might not have a proper ghost type index yet, we calculate it here for pre spawns
                if (chunkGhostType < 0)
                {
                    var ghostEntity = ghostChunk.GetNativeArray(entityType)[0];
                    chunkGhostType = FindGhostTypeIndex(ghostEntity);
                    if (chunkGhostType < 0)
                    {
                        return false;
                    }
                }

                return chunkGhostType < GhostTypeCollection.Length;
            }

            static bool TryGetComponentPtrInChunk(EntityStorageInfo connectionChunkInfo, DynamicComponentTypeHandle typeHandle, int typeSize, out IntPtr componentPtrInChunk)
            {
                var connectionHasType = connectionChunkInfo.Chunk.Has(ref typeHandle);
                componentPtrInChunk = connectionHasType ? GetComponentPtrInChunk(connectionChunkInfo, typeHandle, typeSize) : default;
                return connectionHasType;
            }

            /// Collect a list of all chunks which could be serialized and sent. Sort the list so other systems get it in priority order.
            /// Also cleanup any stale ghost state in the map and create new storage buffers for new chunks so all chunks are in a valid state after this has executed
            unsafe NativeList<PrioChunk> GatherGhostChunksBatch(out int maxCount, out int totalCount)
            {
                var serialChunks = new NativeList<PrioChunk>(ghostChunks.Length, Allocator.Temp);
                maxCount = 0;
                totalCount = 0;
                var connectionChunkInfo = childEntityLookup[connectionEntity];
                var connectionHasConnectionData = TryGetComponentPtrInChunk(connectionChunkInfo, ghostConnectionDataTypeHandle, ghostConnectionDataTypeSize, out var connectionDataPtr);
                var chunkStates = connectionState[connectionIdx].SerializationState;

                for (int chunk = 0; chunk < ghostChunks.Length; ++chunk)
                {
                    var ghostChunk = ghostChunks[chunk];
                    if (!TryGetChunkStateOrNew(ghostChunk, ref *chunkStates, out var chunkState))
                    {
                        continue;
                    }

                    chunkState.SetLastValidTick(currentTick);
                    totalCount += ghostChunk.Count;
                    maxCount = math.max(maxCount, ghostChunk.Count);

                    //Prespawn ghost chunk should be considered only if the subscene wich they belong to as been loaded (acked) by the client.
                    if (ghostChunk.Has(ref prespawnGhostIdType))
                    {
                        var ackedPrespawnSceneMap = connectionState[connectionIdx].AckedPrespawnSceneMap;
                        //Retrieve the subscene hash from the shared component index.
                        var sharedComponentIndex = ghostChunk.GetSharedComponentIndex(subsceneHashSharedTypeHandle);
                        var hash = SubSceneHashSharedIndexMap[sharedComponentIndex];
                        //Skip the chunk if the client hasn't acked/requested streaming that subscene
                        if (!ackedPrespawnSceneMap.ContainsKey(hash))
                        {
#if NETCODE_DEBUG
                            if (enablePacketLogging == 1)
                                netDebugPacket.Log(FixedString.Format(
                                    "Skipping prespawn chunk with TypeID:{0} for scene {1} not acked by the client\n",
                                    chunkState.ghostType, NetDebug.PrintHex(hash)));
#endif
                            continue;
                        }
                    }

                    if (ghostChunk.Has(ref ghostChildEntityComponentType))
                        continue;

                    var chunkPriority = chunkState.baseImportance *
                                        currentTick.TicksSince(chunkState.GetLastUpdate());
                    if (chunkState.GetAllIrrelevant())
                        chunkPriority /= IrrelevantImportanceDownScale;
                    if (chunkPriority < MinSendImportance)
                        continue;

                    var pc = new PrioChunk
                    {
                        chunk = ghostChunk,
                        priority = chunkPriority,
                        startIndex = chunkState.GetStartIndex(),
                        ghostType = chunkState.ghostType
                    };
                    serialChunks.Add(pc);
                }
                if (connectionHasConnectionData)
                {
                    ref var unsafeList = ref (*serialChunks.GetUnsafeList());
                    var func = (delegate *unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, ref UnsafeList<PrioChunk>, void>)BatchScaleImportance.Ptr.Value;
                    func(connectionDataPtr, ghostImportanceDataIntPtr,
                        GhostComponentSerializer.IntPtrCast(ref ghostImportancePerChunkTypeHandle),
                        ref unsafeList);
                    if (MinDistanceScaledSendImportance > 0)
                    {
                        var chunk = 0;
                        while(chunk < serialChunks.Length)
                        {
                            if (serialChunks.ElementAt(chunk).priority < MinDistanceScaledSendImportance)
                            {
                                serialChunks.RemoveAtSwapBack(chunk);
                            }
                            else
                            {
                                ++chunk;
                            }
                        }
                    }
                }
#if NETCODE_DEBUG
                if (enablePacketLogging == 1)
                {
                    for (int i = 0; i < serialChunks.Length; ++i)
                    {
                        netDebugPacket.Log(FixedString.Format("Adding chunk TypeID:{0} Priority:{1}\n", serialChunks[i].ghostType, serialChunks[i].priority));
                    }
                }
#endif
                var arr = serialChunks.AsArray();
                arr.Sort();
                return serialChunks;
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var systemData = SystemAPI.GetSingleton<GhostSendSystemData>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
#if UNITY_EDITOR || NETCODE_DEBUG
            ref var netStats = ref SystemAPI.GetSingletonRW<GhostStatsCollectionSnapshot>().ValueRW;
            UpdateNetStats(ref netStats, networkTime.ServerTick);
#endif
            // Calculate how many state updates we should send this frame
            SystemAPI.TryGetSingleton<ClientServerTickRate>(out var tickRate);
            tickRate.ResolveDefaults();
            var netTickInterval =
                (tickRate.SimulationTickRate + tickRate.NetworkTickRate - 1) / tickRate.NetworkTickRate;
            var sendThisTick = tickRate.SendSnapshotsForCatchUpTicks || !networkTime.IsCatchUpTick;
            if (sendThisTick)
                ++m_SentSnapshots;

            // Make sure the list of connections and connection state is up to date
            var connections = connectionQuery.ToEntityListAsync(state.WorldUpdateAllocator, out var connectionHandle);

            var relevancySingleton = SystemAPI.GetSingleton<GhostRelevancy>();
            var relevancyMode = relevancySingleton.GhostRelevancyMode;
            EntityQueryMask userGlobalRelevantQueryMask = netcodeEmptyQuery;
            if (relevancySingleton.DefaultRelevancyQuery != default)
                userGlobalRelevantQueryMask = relevancySingleton.DefaultRelevancyQuery.GetEntityQueryMask();

            bool relevancyEnabled = (relevancyMode != GhostRelevancyMode.Disabled);
            // Find the latest tick which has been acknowledged by all clients and cleanup all ghosts destroyed before that
            var currentTick = networkTime.ServerTick;

            // Setup the connections which require cleanup this frame
            // This logic is using length from previous frame, that means we can skip updating connections in some cases
            if (m_ConnectionStates.Length > 0)
                m_CurrentCleanupConnectionState = (m_CurrentCleanupConnectionState + systemData.CleanupConnectionStatePerTick) % m_ConnectionStates.Length;
            else
                m_CurrentCleanupConnectionState = 0;

            // Find the latest tick received by all connections
            m_DespawnAckedByAllTick.Value = currentTick;
            var connectionsToProcess = m_ConnectionsToProcess;
            connectionsToProcess.Clear();
            m_NetworkIdFromEntity.Update(ref state);
            k_Scheduling.Begin();
            state.Dependency = new UpdateConnectionsJob()
            {
                ConnectionRelevantCount = m_ConnectionRelevantCount,
                Connections = connections,
                ConnectionStateLookup = m_ConnectionStateLookup,
                ConnectionStates = m_ConnectionStates,
                ConnectionsToProcess = connectionsToProcess,
                DespawnAckedByAll = m_DespawnAckedByAllTick,
                GhostRelevancySet = m_GhostRelevancySet,
                NetTickInterval = netTickInterval,
                NetworkIdFromEntity = m_NetworkIdFromEntity,
                RelevancyEnabled = relevancyEnabled ? (byte) 1 : (byte)0,
                SendThisTick = sendThisTick ? (byte)1 : (byte)0,
                SentSnapshots = m_SentSnapshots,
            }.Schedule(JobHandle.CombineDependencies(state.Dependency, connectionHandle));
            k_Scheduling.End();

#if NETCODE_DEBUG
            FixedString32Bytes packetDumpTimestamp = default;
            if (!m_PacketLogEnableQuery.IsEmptyIgnoreFilter)
            {
                state.CompleteDependency();
                NetDebugInterop.GetTimestamp(out packetDumpTimestamp);
                FixedString128Bytes worldNameFixed = state.WorldUnmanaged.Name;

                foreach (var packet in SystemAPI.Query<AspectPacket>())
                {
                    if (!m_ConnectionStateLookup.ContainsKey(packet.Entity))
                        continue;

                    var conState = m_ConnectionStates[m_ConnectionStateLookup[packet.Entity]];
                    if (conState.NetDebugPacket.IsCreated)
                        continue;

                    NetDebugInterop.InitDebugPacketIfNotCreated(ref conState.NetDebugPacket, m_LogFolder, worldNameFixed, packet.Id.ValueRO.Value);
                    m_ConnectionStates[m_ConnectionStateLookup[packet.Entity]] = conState;
                    // Find connection state in the list sent to the serialize job and replace with this updated version
                    for (int i = 0; i < connectionsToProcess.Length; ++i)
                    {
                        if (connectionsToProcess[i].Entity != packet.Entity)
                        {
                            continue;
                        }
                        connectionsToProcess[i] = conState;
                        break;
                    }
                }
            }
#endif

            // Prepare a command buffer
            EntityCommandBuffer commandBuffer = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var commandBufferConcurrent = commandBuffer.AsParallelWriter();

            // Setup the tick at which ghosts were despawned, cleanup ghosts which have been despawned and acked by al connections
            var freeGhostIds = m_FreeGhostIds.AsParallelWriter();
            var prespawnDespawn = m_DestroyedPrespawnsQueue.AsParallelWriter();
            var freeSpawnedGhosts = m_FreeSpawnedGhostQueue.AsParallelWriter();
            m_PrespawnGhostIdRangeFromEntity.Update(ref state);
            var prespawnIdRanges = m_PrespawnGhostIdRangeFromEntity[SystemAPI.GetSingletonEntity<PrespawnGhostIdRange>()];
            k_Scheduling.Begin();
            state.Dependency = new GhostDespawnParallelJob
            {
                CommandBufferConcurrent = commandBufferConcurrent,
                CurrentTick = currentTick,
                DespawnAckedByAllTick = m_DespawnAckedByAllTick,
                FreeGhostIds = freeGhostIds,
                FreeSpawnedGhosts = freeSpawnedGhosts,
                GhostMap = m_GhostMap,
                PrespawnDespawn = prespawnDespawn,
                PrespawnIdRanges = prespawnIdRanges,
            }.ScheduleParallel(ghostDespawnQuery, state.Dependency);
            k_Scheduling.End();

            // Copy destroyed entities in the parallel write queue populated by ghost cleanup to a single list
            // and free despawned ghosts from map
            k_Scheduling.Begin();
            state.Dependency = new GhostDespawnSingleJob
            {
                DespawnList = m_DestroyedPrespawns,
                DespawnQueue = m_DestroyedPrespawnsQueue,
                FreeSpawnQueue = m_FreeSpawnedGhostQueue,
                GhostMap = m_GhostMap,
            }.Schedule(state.Dependency);
            k_Scheduling.End();

            // If the ghost collection has not been initialized yet the send ystem can not process any ghosts
            if (!SystemAPI.GetSingleton<GhostCollection>().IsInGame)
            {
                return;
            }

            // Extract all newly spawned ghosts and set their ghost ids
            var ghostCollectionSingleton = SystemAPI.GetSingletonEntity<GhostCollection>();
            var spawnChunks = ghostSpawnQuery.ToArchetypeChunkListAsync(state.WorldUpdateAllocator, out var spawnChunkHandle);
            var netDebug = SystemAPI.GetSingleton<NetDebug>();
#if NETCODE_DEBUG
            m_PrefabDebugNameFromEntity.Update(ref state);
#endif
            m_GhostTypeFromEntity.Update(ref state);
            m_GhostComponentType.Update(ref state);
            m_GhostOwnerComponentType.Update(ref state);
            m_EntityType.Update(ref state);
            m_GhostTypeCollectionFromEntity.Update(ref state);
            m_GhostCollectionFromEntity.Update(ref state);
            //The spawnjob assign the ghost id, tick and track the ghost with a cleanup component. If the
            //ghost chunk has a GhostType that has not been processed yet by the GhostCollectionSystem,
            //the chunk is skipped. However, this leave the entities in a limbo state where the data is not setup
            //yet.
            //It is necessary to check always for the cleanup component being added to the chunk in general in the serialization
            //job to ensure the data has been appropriately set.
            var spawnJob = new SpawnGhostJob
            {
                connectionState = m_ConnectionsToProcess.AsDeferredJobArray(),
                GhostCollectionSingleton = ghostCollectionSingleton,
                GhostTypeCollectionFromEntity = m_GhostTypeCollectionFromEntity,
                GhostCollectionFromEntity = m_GhostCollectionFromEntity,
                spawnChunks = spawnChunks,
                entityType = m_EntityType,
                ghostComponentType = m_GhostComponentType,
                freeGhostIds = m_FreeGhostIds,
                allocatedGhostIds = m_AllocatedGhostIds,
                commandBuffer = commandBuffer,
                ghostMap = m_GhostMap,
                ghostTypeFromEntity = m_GhostTypeFromEntity,
                serverTick = currentTick,
                forcePreSerialize = systemData.m_ForcePreSerialize,
                netDebug = netDebug,
#if NETCODE_DEBUG
                prefabNames = m_PrefabDebugNameFromEntity,
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                ghostOwnerComponentType = m_GhostOwnerComponentType
#endif
            };
            k_Scheduling.Begin();
            state.Dependency = spawnJob.Schedule(JobHandle.CombineDependencies(state.Dependency, spawnChunkHandle));
            k_Scheduling.End();

            // Create chunk arrays for ghosts and despawned ghosts
            var despawnChunks = ghostDespawnQuery.ToArchetypeChunkListAsync(state.WorldUpdateAllocator, out var despawnChunksHandle);
            var ghostChunks = ghostQuery.ToArchetypeChunkListAsync(state.WorldUpdateAllocator, out var ghostChunksHandle);
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, despawnChunksHandle, ghostChunksHandle);

            SystemAPI.TryGetSingletonEntity<PrespawnSceneLoaded>(out var prespawnSceneLoadedEntity);
            PrespawnHelper.PopulateSceneHashLookupTable(prespawnSharedComponents, state.EntityManager, m_SceneSectionHashLookup);

            ref readonly var networkStreamDriver = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRO;
            // If there are any connections to send data to, serialize the data for them in parallel
            UpdateSerializeJobDependencies(ref state);
            var customSerializers = m_CustomSerializerFromEntity[ghostCollectionSingleton];
            var serializeJob = new SerializeJob
            {
                GhostCollectionSingleton = ghostCollectionSingleton,
                GhostComponentCollectionFromEntity = m_GhostComponentCollectionFromEntity,
                GhostTypeCollectionFromEntity = m_GhostTypeCollectionFromEntity,
                GhostComponentIndexFromEntity = m_GhostComponentIndexFromEntity,
                GhostCollectionFromEntity = m_GhostCollectionFromEntity,
                SubSceneHashSharedIndexMap = m_SceneSectionHashLookup,
                concurrentDriverStore = networkStreamDriver.ConcurrentDriverStore,
                despawnChunks = despawnChunks,
                ghostChunks = ghostChunks,
                connectionState = m_ConnectionsToProcess.AsDeferredJobArray(),
                ackFromEntity = m_SnapshotAckFromEntity,
                connectionFromEntity = m_ConnectionFromEntity,
                networkIdFromEntity = m_NetworkIdFromEntity,
                entityType = m_EntityType,
                ghostSystemStateType = m_GhostSystemStateType,
                preSerializedGhostType = m_PreSerializedGhostType,
                prespawnGhostIdType = m_PrespawnedGhostIdType,
                ghostComponentType = m_GhostComponentType,
                ghostGroupType = m_GhostGroupType,
                ghostChildEntityComponentType = m_GhostChildEntityComponentType,
                relevantGhostForConnection = m_GhostRelevancySet,
                userGlobalRelevantMask = userGlobalRelevantQueryMask,
                internalGlobalRelevantMask = internalGlobalRelevantQueryMask,
                relevancyMode = relevancyMode,
                relevantGhostCountForConnection = m_ConnectionRelevantCount.AsDeferredJobArray(),
#if UNITY_EDITOR || NETCODE_DEBUG
                netStatsBuffer = netStats.Data.AsArray(),
                netStatSize = netStats.Size,
                netStatStride = netStats.Stride,
#endif
                compressionModel = m_CompressionModel,
                ghostFromEntity = m_GhostFromEntity,
                currentTick = currentTick,
                localTime = NetworkTimeSystem.TimestampMS,
                snapshotTargetSizeFromEntity = m_SnapshotTargetFromEntity,

                ghostTypeFromEntity = m_GhostTypeFromEntity,

                allocatedGhostIds = m_AllocatedGhostIds,
                prespawnDespawns = m_DestroyedPrespawns,
                childEntityLookup = state.GetEntityStorageInfoLookup(),
                linkedEntityGroupType = m_LinkedEntityGroupType,
                prespawnBaselineTypeHandle = m_PrespawnGhostBaselineType,
                subsceneHashSharedTypeHandle = m_SubsceneGhostComponentType,
                prespawnSceneLoadedEntity = prespawnSceneLoadedEntity,
                prespawnAckFromEntity = m_PrespawnAckFromEntity,
                prespawnSceneLoadedFromEntity = m_PrespawnSceneLoadedFromEntity,

                CurrentSystemVersion = state.GlobalSystemVersion,
                prioritizeChunksMarker = m_PrioritizeChunksMarker,
                ghostGroupMarker = m_GhostGroupMarker,
#if NETCODE_DEBUG
                prefabNamesFromEntity = m_PrefabDebugNameFromEntity,
                enableLoggingFromEntity = m_EnablePacketLoggingFromEntity,
                timestamp = packetDumpTimestamp,
                enablePerComponentProfiling = systemData.m_EnablePerComponentProfiling,
#endif
                netDebug = netDebug,
                FirstSendImportanceMultiplier = systemData.FirstSendImportanceMultiplier,
                MinSendImportance = systemData.MinSendImportance,
                MinDistanceScaledSendImportance = systemData.MinDistanceScaledSendImportance,
                MaxSendChunks = systemData.MaxSendChunks,
                MaxSendEntities = systemData.MaxSendEntities,
                IrrelevantImportanceDownScale = systemData.IrrelevantImportanceDownScale,
                useCustomSerializer = systemData.UseCustomSerializer,
                forceSingleBaseline = systemData.m_ForceSingleBaseline,
                keepSnapshotHistoryOnStructuralChange = systemData.m_KeepSnapshotHistoryOnStructuralChange,
                snaphostHasCompressedGhostSize = GhostSystemConstants.SnaphostHasCompressedGhostSize ? (byte)1u :(byte)0u,
                defaultSnapshotPacketSize = systemData.DefaultSnapshotPacketSize,
                initialTempWriterCapacity = systemData.TempStreamInitialSize,

#if UNITY_EDITOR
                UpdateLen = m_UpdateLen,
                UpdateCounts = m_UpdateCounts,
#endif
            };
            if (!SystemAPI.TryGetSingleton<GhostImportance>(out var importance))
            {
                serializeJob.BatchScaleImportance = default;
                serializeJob.ScaleGhostImportance = default;
            }
            else
            {
                serializeJob.BatchScaleImportance = importance.BatchScaleImportanceFunction;
                serializeJob.ScaleGhostImportance = importance.ScaleImportanceFunction;
            }

            // We don't want to assign default value to type handles as this would lead to a safety error
            if (SystemAPI.TryGetSingletonEntity<GhostImportance>(out var singletonEntity))
            {
                m_GhostImportanceType.Update(ref state);

                var entityStorageInfoLookup = SystemAPI.GetEntityStorageInfoLookup();
                var entityStorageInfo = entityStorageInfoLookup[singletonEntity];

                var ghostImportanceTypeHandle = m_GhostImportanceType;
                GhostImportance config;
                unsafe
                {
                    config = entityStorageInfo.Chunk.GetComponentDataPtrRO(ref ghostImportanceTypeHandle)[entityStorageInfo.IndexInChunk];
                }
                var ghostConnectionDataTypeRO = config.GhostConnectionComponentType;
                var ghostImportancePerChunkDataTypeRO = config.GhostImportancePerChunkDataType;
                var ghostImportanceDataTypeRO = config.GhostImportanceDataType;
                ghostConnectionDataTypeRO.AccessModeType = ComponentType.AccessMode.ReadOnly;
                ghostImportanceDataTypeRO.AccessModeType = ComponentType.AccessMode.ReadOnly;
                ghostImportancePerChunkDataTypeRO.AccessModeType = ComponentType.AccessMode.ReadOnly;
                serializeJob.ghostConnectionDataTypeHandle = state.GetDynamicComponentTypeHandle(ghostConnectionDataTypeRO);
                serializeJob.ghostImportancePerChunkTypeHandle = state.GetDynamicSharedComponentTypeHandle(ghostImportancePerChunkDataTypeRO);
                serializeJob.ghostConnectionDataTypeSize = TypeManager.GetTypeInfo(ghostConnectionDataTypeRO.TypeIndex).TypeSize;

                // Try to get the users importance singleton data from the same "GhostImportance Singleton".
                // If it's not there, don't error, just pass on the null. Thus, treated as optional.
                if (ghostImportanceDataTypeRO.TypeIndex != default && !config.GhostImportanceDataType.IsZeroSized)
                {
                    var ghostImportanceDataTypeSize = TypeManager.GetTypeInfo(ghostImportanceDataTypeRO.TypeIndex).TypeSize;
                    var ghostImportanceDynamicTypeHandle = state.GetDynamicComponentTypeHandle(ghostImportanceDataTypeRO);

                    var hasGhostImportanceTypeInSingletonChunk = entityStorageInfo.Chunk.Has(ref ghostImportanceTypeHandle);
                    unsafe
                    {
                        serializeJob.ghostImportanceDataIntPtr = hasGhostImportanceTypeInSingletonChunk
                            ? (IntPtr) entityStorageInfo.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref ghostImportanceDynamicTypeHandle, ghostImportanceDataTypeSize).GetUnsafeReadOnlyPtr()
                            : IntPtr.Zero;
                    }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (!hasGhostImportanceTypeInSingletonChunk)
                        throw new InvalidOperationException($"You configured your `GhostImportance` singleton to expect that the type '{ghostImportanceDataTypeRO.ToFixedString()}' would also be added to this singleton entity, but the singleton entity does not contain this type. Either remove this requirement, or add this component to the singleton.");
#endif
                }
                else
                {
                    serializeJob.ghostImportanceDataIntPtr = IntPtr.Zero;
                }
            }
            else
            {
                serializeJob.ghostImportancePerChunkTypeHandle = state.GetDynamicSharedComponentTypeHandle(new ComponentType { TypeIndex = TypeIndex.Null, AccessModeType = ComponentType.AccessMode.ReadOnly });
            }

            var ghostComponentCollection = state.EntityManager.GetBuffer<GhostCollectionComponentType>(ghostCollectionSingleton);
            m_GhostTypeComponentType.Update(ref state);

            k_Scheduling.Begin();
            state.Dependency = m_GhostPreSerializer.Schedule(state.Dependency,
                serializeJob.GhostComponentCollectionFromEntity,
                serializeJob.GhostTypeCollectionFromEntity,
                serializeJob.GhostComponentIndexFromEntity,
                serializeJob.GhostCollectionSingleton,
                serializeJob.GhostCollectionFromEntity,
                serializeJob.linkedEntityGroupType,
                serializeJob.childEntityLookup,
                serializeJob.ghostComponentType,
                m_GhostTypeComponentType,
                serializeJob.entityType,
                serializeJob.ghostFromEntity,
                serializeJob.connectionState,
                serializeJob.netDebug,
                currentTick,
                systemData.m_UseCustomSerializer,
                ref state,
                ghostComponentCollection);
            k_Scheduling.End();
            serializeJob.SnapshotPreSerializeData = m_GhostPreSerializer.SnapshotData;

            DynamicTypeList.PopulateList(ref state, ghostComponentCollection, true, ref serializeJob.DynamicGhostCollectionComponentTypeList);

            k_Scheduling.Begin();
            state.Dependency = serializeJob.ScheduleByRef(m_ConnectionsToProcess, 1, state.Dependency);
            k_Scheduling.End();

            var serializeHandle = state.Dependency;
            // Schedule a job to clean up connections
            k_Scheduling.Begin();
            var cleanupHandle = new CleanupGhostSerializationStateJob
            {
                CleanupConnectionStatePerTick = systemData.CleanupConnectionStatePerTick,
                CurrentCleanupConnectionState = m_CurrentCleanupConnectionState,
                ConnectionStates = m_ConnectionStates,
                GhostChunks = ghostChunks,
            }.Schedule(state.Dependency);
            var flushHandle = networkStreamDriver.DriverStore.ScheduleFlushSendAllDrivers(serializeHandle);
            k_Scheduling.End();
            state.Dependency = JobHandle.CombineDependencies(flushHandle, cleanupHandle);
#if NETCODE_DEBUG && !USING_UNITY_LOGGING
            state.Dependency = new FlushNetDebugPacket
            {
                EnablePacketLogging = m_EnablePacketLoggingFromEntity,
                ConnectionStates = m_ConnectionsToProcess.AsDeferredJobArray(),
            }.Schedule(m_ConnectionsToProcess, 1, state.Dependency);
#endif
        }

        void UpdateSerializeJobDependencies(ref SystemState state)
        {
#if NETCODE_DEBUG
            m_PrefabDebugNameFromEntity.Update(ref state);
#endif
            m_GhostTypeFromEntity.Update(ref state);
            m_SnapshotTargetFromEntity.Update(ref state);
            m_GhostGroupType.Update(ref state);
            m_GhostComponentType.Update(ref state);
            m_NetworkIdFromEntity.Update(ref state);
            m_GhostTypeCollectionFromEntity.Update(ref state);
            m_GhostCollectionFromEntity.Update(ref state);
            m_SnapshotAckFromEntity.Update(ref state);
            m_ConnectionFromEntity.Update(ref state);
            m_GhostFromEntity.Update(ref state);
            m_SnapshotTargetFromEntity.Update(ref state);
            m_EnablePacketLoggingFromEntity.Update(ref state);
            m_GhostSystemStateType.Update(ref state);
            m_PreSerializedGhostType.Update(ref state);
            m_GhostChildEntityComponentType.Update(ref state);
            m_PrespawnedGhostIdType.Update(ref state);
            m_GhostGroupType.Update(ref state);
            m_EntityType.Update(ref state);
            m_LinkedEntityGroupType.Update(ref state);
            m_PrespawnGhostBaselineType.Update(ref state);
            m_SubsceneGhostComponentType.Update(ref state);
            m_GhostComponentCollectionFromEntity.Update(ref state);
            m_GhostComponentIndexFromEntity.Update(ref state);
            m_CustomSerializerFromEntity.Update(ref state);
            m_PrespawnAckFromEntity.Update(ref state);
            m_PrespawnSceneLoadedFromEntity.Update(ref state);
        }

        [BurstCompile]
        struct UpdateConnectionsJob : IJob
        {
            [ReadOnly] public ComponentLookup<NetworkId> NetworkIdFromEntity;
            [ReadOnly] public NativeParallelHashMap<RelevantGhostForConnection, int> GhostRelevancySet;
            public NativeList<Entity> Connections;
            public NativeParallelHashMap<Entity, int> ConnectionStateLookup;
            public NativeList<ConnectionStateData> ConnectionStates;
            public NativeList<ConnectionStateData> ConnectionsToProcess;
            public NativeList<int> ConnectionRelevantCount;
            public NativeReference<NetworkTick> DespawnAckedByAll;
            public byte RelevancyEnabled;
            public byte SendThisTick;
            public int NetTickInterval;
            public uint SentSnapshots;

            public void Execute()
            {
                var existing = new NativeParallelHashMap<Entity, int>(Connections.Length, Allocator.Temp);
                int maxConnectionId = 0;
                foreach (var connection in Connections)
                {
                    existing.TryAdd(connection, 1);
                    if (!ConnectionStateLookup.TryGetValue(connection, out var stateIndex))
                    {
                        stateIndex = ConnectionStates.Length;
                        ConnectionStates.Add(ConnectionStateData.Create(connection));
                        ConnectionStateLookup.TryAdd(connection, stateIndex);
                    }
                    maxConnectionId = math.max(maxConnectionId, NetworkIdFromEntity[connection].Value);

                    var ackedByAllTick = DespawnAckedByAll.Value;
                    var snapshot = ConnectionStates[stateIndex].GhostStateData.AckedDespawnTick;
                    if (!snapshot.IsValid)
                        ackedByAllTick = NetworkTick.Invalid;
                    else if (ackedByAllTick.IsValid && ackedByAllTick.IsNewerThan(snapshot))
                        ackedByAllTick = snapshot;
                    DespawnAckedByAll.Value = ackedByAllTick;
                }

                for (int i = 0; i < ConnectionStates.Length; ++i)
                {
                    if (existing.TryGetValue(ConnectionStates[i].Entity, out var val))
                    {
                        continue;
                    }

                    ConnectionStateLookup.Remove(ConnectionStates[i].Entity);
                    ConnectionStates[i].Dispose();
                    if (i != ConnectionStates.Length - 1)
                    {
                        ConnectionStates[i] = ConnectionStates[ConnectionStates.Length - 1];
                        ConnectionStateLookup.Remove(ConnectionStates[i].Entity);
                        ConnectionStateLookup.TryAdd(ConnectionStates[i].Entity, i);
                    }

                    ConnectionStates.RemoveAtSwapBack(ConnectionStates.Length - 1);
                    --i;
                }

                ConnectionRelevantCount.ResizeUninitialized(maxConnectionId+2);
                for (int i = 0; i < ConnectionRelevantCount.Length; ++i)
                    ConnectionRelevantCount[i] = 0;

                // go through all keys in the relevancy set, +1 to the connection idx array
                if (RelevancyEnabled == 1)
                {
                    var values = GhostRelevancySet.GetKeyArray(Allocator.Temp);
                    for (int i = 0; i < values.Length; ++i)
                    {
                        var cid = math.min(values[i].Connection, maxConnectionId+1);
                        ConnectionRelevantCount[cid] += 1;
                    }
                }
                if (SendThisTick == 0)
                    return;
                var sendPerFrame = (ConnectionStates.Length + NetTickInterval - 1) / NetTickInterval;
                var sendStartPos = sendPerFrame * (int) (SentSnapshots % NetTickInterval);

                if (sendStartPos + sendPerFrame > ConnectionStates.Length)
                    sendPerFrame = ConnectionStates.Length - sendStartPos;
                for (int i = 0; i < sendPerFrame; ++i)
                    ConnectionsToProcess.Add(ConnectionStates[sendStartPos + i]);
            }
        }

#if NETCODE_DEBUG && !USING_UNITY_LOGGING
        struct FlushNetDebugPacket : IJobParallelForDefer
        {
            [ReadOnly] public ComponentLookup<EnablePacketLogging> EnablePacketLogging;
            [ReadOnly] public NativeArray<ConnectionStateData> ConnectionStates;
            public void Execute(int index)
            {
                var state = ConnectionStates[index];
                if (EnablePacketLogging.HasComponent(state.Entity))
                {
                    state.NetDebugPacket.Flush();
                }
            }
        }
#endif

        [BurstCompile]
        struct CleanupGhostSerializationStateJob : IJob
        {
            public int CleanupConnectionStatePerTick;
            public int CurrentCleanupConnectionState;
            [ReadOnly] public NativeList<ConnectionStateData> ConnectionStates;
            [ReadOnly] public NativeList<ArchetypeChunk> GhostChunks;

            public unsafe void Execute()
            {
                var conCount = math.min(CleanupConnectionStatePerTick, ConnectionStates.Length);
                var existingChunks = new UnsafeHashMap<ArchetypeChunk, int>(GhostChunks.Length, Allocator.Temp);
                foreach (var chunk in GhostChunks)
                {
                    existingChunks.TryAdd(chunk, 1);
                }
                for (int con = 0; con < conCount; ++con)
                {
                    var conIdx = (con + CurrentCleanupConnectionState) % ConnectionStates.Length;
                    var chunkSerializationData = ConnectionStates[conIdx].SerializationState;
                    var oldChunks = chunkSerializationData->GetKeyArray(Allocator.Temp);
                    foreach (var oldChunk in oldChunks)
                    {
                        if (existingChunks.ContainsKey(oldChunk))
                        {
                            continue;
                        }
                        GhostChunkSerializationState chunkState;
                        chunkSerializationData->TryGetValue(oldChunk, out chunkState);
                        chunkState.FreeSnapshotData();
                        chunkSerializationData->Remove(oldChunk);
                    }
                }
            }
        }

        [BurstCompile]
        struct GhostDespawnSingleJob : IJob
        {
            public NativeQueue<SpawnedGhost> FreeSpawnQueue;
            public NativeQueue<int> DespawnQueue;
            public NativeList<int> DespawnList;
            public NativeParallelHashMap<SpawnedGhost, Entity> GhostMap;

            public void Execute()
            {
                while (DespawnQueue.TryDequeue(out int destroyed))
                {
                    if (!DespawnList.Contains(destroyed))
                    {
                        DespawnList.Add(destroyed);
                    }
                }

                while (FreeSpawnQueue.TryDequeue(out var spawnedGhost))
                {
                    GhostMap.Remove(spawnedGhost);
                }
            }
        }

        [BurstCompile]
        partial struct GhostDespawnParallelJob : IJobEntity
        {
            [ReadOnly] public NativeParallelHashMap<SpawnedGhost, Entity> GhostMap;
            [ReadOnly] public NativeReference<NetworkTick> DespawnAckedByAllTick;
            [ReadOnly] public DynamicBuffer<PrespawnGhostIdRange> PrespawnIdRanges;
            public EntityCommandBuffer.ParallelWriter CommandBufferConcurrent;
            public NativeQueue<int>.ParallelWriter PrespawnDespawn;
            public NativeQueue<int>.ParallelWriter FreeGhostIds;
            public NativeQueue<SpawnedGhost>.ParallelWriter FreeSpawnedGhosts;
            public NetworkTick CurrentTick;

            public void Execute(Entity entity, [EntityIndexInQuery]int entityIndexInQuery, ref GhostCleanup ghost)
            {
                var ackedByAllTick = DespawnAckedByAllTick.Value;
                if (!ghost.despawnTick.IsValid)
                {
                    ghost.despawnTick = CurrentTick;
                }
                else if (ackedByAllTick.IsValid && !ghost.despawnTick.IsNewerThan(ackedByAllTick))
                {
                    if (PrespawnHelper.IsRuntimeSpawnedGhost(ghost.ghostId))
                        FreeGhostIds.Enqueue(ghost.ghostId);
                    CommandBufferConcurrent.RemoveComponent<GhostCleanup>(entityIndexInQuery, entity);
                }
                //Remove the ghost from the mapping as soon as possible, regardless of clients acknowledge
                var spawnedGhost = new SpawnedGhost {ghostId = ghost.ghostId, spawnTick = ghost.spawnTick};
                if (!GhostMap.ContainsKey(spawnedGhost))
                {
                    return;
                }
                FreeSpawnedGhosts.Enqueue(spawnedGhost);
                //If there is no allocated range, do not add to the queue. That means the subscene the
                //prespawn belongs to has been unloaded
                if (PrespawnHelper.IsPrespawnGhostId(ghost.ghostId) && PrespawnIdRanges.GhostIdRangeIndex(ghost.ghostId) >= 0)
                    PrespawnDespawn.Enqueue(ghost.ghostId);
            }
        }

#if UNITY_EDITOR || NETCODE_DEBUG
        void UpdateNetStats(ref GhostStatsCollectionSnapshot netStats, NetworkTick serverTick)
        {
#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif

            var numLoadedPrefabs = SystemAPI.GetSingleton<GhostCollection>().NumLoadedPrefabs;
            const int intsPerCacheLine = JobsUtility.CacheLineSize / 4;
            netStats.Size = numLoadedPrefabs * 3 + 3 + 1;
            // Round up to an even cache line size in order to reduce false sharing
            netStats.Stride = (netStats.Size + intsPerCacheLine-1) & (~(intsPerCacheLine-1));
            netStats.Workers = maxThreadCount;
            netStats.Data.Resize(netStats.Stride * maxThreadCount, NativeArrayOptions.ClearMemory);
            netStats.Data[0] = serverTick.SerializedData;
        }
#endif
    }
}
