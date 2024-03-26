#if UNITY_EDITOR || NETCODE_DEBUG
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.NetCode
{
    internal struct GhostStats : IComponentData
    {
        public bool IsConnected;
    }
    internal struct GhostStatsCollectionCommand : IComponentData
    {
        public NativeArray<uint> Value;
    }
    internal struct GhostStatsCollectionSnapshot : IComponentData
    {
        public int Size;
        public int Stride;
        public int Workers;
        public NativeList<uint> Data;
    }
    internal struct GhostStatsCollectionPredictionError : IComponentData
    {
        public NativeList<float> Data;
    }
    internal struct GhostStatsCollectionMinMaxTick : IComponentData
    {
        public NativeArray<NetworkTick> Value;
    }
    /// <summary>
    /// The GhostStatsCollectionSystem is responsible to hold all sent and received snapshot statitics on both client
    /// and server.
    /// The collected stats are then sent to the Network Debugger tools for visualization (when the debugger is connected attached) by
    /// the <see cref="GhostStatsConnection"/> at the end of the frame.
    /// </summary>
    // This is updating first in the receive system group to make sure this system is the first stats collection
    // running any given frame since this system sets up the current tick for the stats
    [UpdateInGroup(typeof(NetworkReceiveSystemGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ThinClientSimulation)]
    [BurstCompile]
    unsafe internal partial struct GhostStatsCollectionSystem : ISystem
    {
        private static GhostStatsConnection _sGhostStatsConnection;
        private uint m_UpdateId;
        private bool m_HasMonitor;

        /// <summary>
        /// Append to the collection the snapshost prefab stats  or the given tick. Used and populated by the <see cref="GhostSendSystem"/>
        /// </summary>
        /// <param name="stats"></param>
        /// <param name="collectionData"></param>
        private void AddSnapshotStats(NativeArray<uint> stats, in GhostStatsCollectionData collectionData)
        {
            var statsTick = new NetworkTick{SerializedData = stats[0]};
            if (!statsTick.IsValid || m_SnapshotStats.Length < stats.Length-1 || m_SnapshotTicks.Length >= 255 || (!m_HasMonitor && collectionData.m_StatIndex < 0) || !collectionData.m_CollectionTick.IsValid)
                return;
            for (int i = 1; i < stats.Length; ++i)
                m_SnapshotStats[i-1] += stats[i];
            m_SnapshotTicks.Add(statsTick.TickIndexForValidTick);
        }

        /// <summary>
        /// Append to the collection the send/recv commands stats for the given tick. Used by the <see cref="NetworkStreamReceiveSystem"/>
        /// and <see cref="CommandSendPacketSystem"/>.
        /// </summary>
        /// <param name="stats"></param>
        /// <param name="collectionData"></param>
        private void AddCommandStats(NativeArray<uint> stats, in GhostStatsCollectionData collectionData)
        {
            var statsTick = new NetworkTick{SerializedData = stats[0]};
            if (!statsTick.IsValid || m_CommandTicks.Length >= 255 || (!m_HasMonitor && collectionData.m_StatIndex < 0) || !collectionData.m_CollectionTick.IsValid)
                return;
            m_CommandStats += stats[1];
            if (m_CommandTicks.Length == 0 || m_CommandTicks[m_CommandTicks.Length-1] != stats[0])
                m_CommandTicks.Add(statsTick.TickIndexForValidTick);
        }
        /// <summary>
        /// Append to the collection the prediction error calculatd by <see cref="GhostPredictionDebugSystem"/> for the given tick
        /// </summary>
        /// <param name="stats"></param>
        /// <param name="collectionData"></param>
        private void AddPredictionErrorStats(NativeArray<float> stats, in GhostStatsCollectionData collectionData)
        {
            if (m_SnapshotTicks.Length >= 255 || (!m_HasMonitor && collectionData.m_StatIndex < 0) || !collectionData.m_CollectionTick.IsValid)
                return;
            for (int i = 0; i < stats.Length; ++i)
                m_PredictionErrors[i] = math.max(stats[i], m_PredictionErrors[i]);
        }

        /// <summary>
        /// Append to the collection the number of discarded snapshots/commmands (respectively received by client and server)
        /// </summary>
        /// <param name="stats"></param>
        /// <param name="collectionData"></param>
        private void AddDiscardedPackets(uint stats, in GhostStatsCollectionData collectionData)
        {
            if (m_SnapshotTicks.Length >= 255 || (!m_HasMonitor && collectionData.m_StatIndex < 0) || !collectionData.m_CollectionTick.IsValid)
                return;

            m_DiscardedPackets += stats;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_SnapshotStats = new NativeList<uint>(3, Allocator.Persistent);
            m_SnapshotTicks = new NativeList<uint>(16, Allocator.Persistent);
            m_PredictionErrors = new NativeList<float>(0, Allocator.Persistent);
            m_TimeSamples = new NativeList<TimeSample>(16, Allocator.Persistent);
            m_CommandTicks = new NativeList<uint>(16, Allocator.Persistent);

            m_PacketQueue = new NativeList<Packet>(16, Allocator.Persistent);
            m_PacketPool = new NativeList<byte>(4096, Allocator.Persistent);
            m_PacketPool.Resize(m_PacketPool.Capacity, NativeArrayOptions.UninitializedMemory);

            m_LastNameAndErrorArray = new NativeText(4096, Allocator.Persistent);

            m_CommandStatsData = new NativeArray<uint>(3, Allocator.Persistent);
            var typeList = new NativeArray<ComponentType>(6, Allocator.Temp);
            typeList[0] = ComponentType.ReadWrite<GhostStats>();
            typeList[1] = ComponentType.ReadWrite<GhostStatsCollectionCommand>();
            typeList[2] = ComponentType.ReadWrite<GhostStatsCollectionSnapshot>();
            typeList[3] = ComponentType.ReadWrite<GhostStatsCollectionPredictionError>();
            typeList[4] = ComponentType.ReadWrite<GhostStatsCollectionMinMaxTick>();
            typeList[5] = ComponentType.ReadWrite<GhostStatsCollectionData>();
            var statEnt = state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(typeList));
            FixedString64Bytes singletonName = "GhostStatsCollectionSingleton";
            state.EntityManager.SetName(statEnt, singletonName);

            SystemAPI.SetSingleton(new GhostStatsCollectionCommand{Value = m_CommandStatsData});

            m_SnapshotStatsData = new NativeList<uint>(128, Allocator.Persistent);
            SystemAPI.SetSingleton(new GhostStatsCollectionSnapshot{Data = m_SnapshotStatsData});

            m_PredictionErrorStatsData = new NativeList<float>(128, Allocator.Persistent);
            SystemAPI.SetSingleton(new GhostStatsCollectionPredictionError{Data = m_PredictionErrorStatsData});

#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            m_MinMaxTickStatsData = new NativeArray<NetworkTick>(maxThreadCount * JobsUtility.CacheLineSize/4, Allocator.Persistent);
            SystemAPI.SetSingleton(new GhostStatsCollectionMinMaxTick{Value = m_MinMaxTickStatsData});

            var ghostcollectionData = new GhostStatsCollectionData
            {
                m_PacketPool = m_PacketPool,
                m_PacketQueue = m_PacketQueue,
                m_LastNameAndErrorArray = m_LastNameAndErrorArray,
                m_SnapshotStats = m_SnapshotStats,
                m_PredictionErrors = m_PredictionErrors,
                m_StatIndex = -1,
                m_UsedPacketPoolSize = 0
            };
            ghostcollectionData.UpdateMaxPacketSize(m_SnapshotStatsData.Length, m_PredictionErrors.Length);
            SystemAPI.SetSingleton(ghostcollectionData);

            m_Recorders = new NativeList<ProfilerRecorder>(Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_LastNameAndErrorArray.Dispose();
            m_PacketQueue.Dispose();
            m_CommandTicks.Dispose();
            m_SnapshotTicks.Dispose();
            m_TimeSamples.Dispose();
            m_CommandStatsData.Dispose();
            m_SnapshotStatsData.Dispose();
            m_PredictionErrorStatsData.Dispose();
            m_MinMaxTickStatsData.Dispose();
            m_PacketPool.Dispose();
            m_SnapshotStats.Dispose();
            m_PredictionErrors.Dispose();
            if (m_Recorders.IsCreated)
            {
                foreach (var recorder in m_Recorders)
                {
                    recorder.Dispose();
                }
                m_Recorders.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_HasMonitor = SystemAPI.TryGetSingleton<GhostMetricsMonitor>(out var monitorComponent);

            ref var collectionData = ref SystemAPI.GetSingletonRW<GhostStatsCollectionData>().ValueRW;

            SystemAPI.SetSingleton(new GhostStats{IsConnected = collectionData.m_StatIndex >= 0});

            if ((!m_HasMonitor && collectionData.m_StatIndex < 0) || state.WorldUnmanaged.IsThinClient())
                return;
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;
            if (currentTick != collectionData.m_CollectionTick)
            {
                UpdateMetrics(ref state, currentTick);
                BeginCollection(currentTick, ref collectionData);
            }

            state.CompleteDependency(); // We complete the dependency. This is needed because NetworkSnapshotAck is written by a job in NetworkStreamReceiveSystem
            AddCommandStats(m_CommandStatsData, collectionData);
            AddDiscardedPackets(m_CommandStatsData[2], collectionData);
            m_CommandStatsData[0] = 0;
            m_CommandStatsData[1] = 0;
            m_CommandStatsData[2] = 0;
            // First uint is tick
            if (m_SnapshotStatsData.Length > 0 && m_SnapshotStatsData[0] != 0)
            {
                ref var snapshotStats = ref SystemAPI.GetSingletonRW<GhostStatsCollectionSnapshot>().ValueRW;
                for (int worker = 1; worker < snapshotStats.Workers; ++worker)
                {
                    int statOffset = worker * snapshotStats.Stride;
                    for (int i = 1; i < snapshotStats.Size; ++i)
                    {
                        m_SnapshotStatsData[i] += m_SnapshotStatsData[statOffset+i];
                        m_SnapshotStatsData[statOffset+i] = 0;
                    }
                }
                AddSnapshotStats(m_SnapshotStatsData.AsArray().GetSubArray(0, snapshotStats.Size), collectionData);
                for (int i = 0; i < snapshotStats.Size; ++i)
                {
                    m_SnapshotStatsData[i] = 0;
                }
            }
            if (m_PredictionErrorStatsData.Length > 0)
            {
                AddPredictionErrorStats(m_PredictionErrorStatsData.AsArray(), collectionData);
                m_PredictionErrorStatsData.Clear();
            }

            m_SnapshotTickMin = m_MinMaxTickStatsData[0];
            m_SnapshotTickMax = m_MinMaxTickStatsData[1];
            m_MinMaxTickStatsData[0] = NetworkTick.Invalid;
            m_MinMaxTickStatsData[1] = NetworkTick.Invalid;

            // Gather the min/max age stats
#if UNITY_2022_2_14F1_OR_NEWER
            int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
            int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
            var intsPerCacheLine = JobsUtility.CacheLineSize/4;
            for (int i = 1; i < maxThreadCount; ++i)
            {
                if (m_MinMaxTickStatsData[intsPerCacheLine*i].IsValid &&
                    (!m_SnapshotTickMin.IsValid ||
                    m_SnapshotTickMin.IsNewerThan(m_MinMaxTickStatsData[intsPerCacheLine*i])))
                    m_SnapshotTickMin = m_MinMaxTickStatsData[intsPerCacheLine*i];
                if (m_MinMaxTickStatsData[intsPerCacheLine*i+1].IsValid &&
                    (!m_SnapshotTickMax.IsValid ||
                    m_MinMaxTickStatsData[intsPerCacheLine*i+1].IsNewerThan(m_SnapshotTickMax)))
                    m_SnapshotTickMax = m_MinMaxTickStatsData[intsPerCacheLine*i+1];
                m_MinMaxTickStatsData[intsPerCacheLine*i] = NetworkTick.Invalid;
                m_MinMaxTickStatsData[intsPerCacheLine*i+1] = NetworkTick.Invalid;
            }

            if (!setupRecorders && SystemAPI.TryGetSingletonEntity<GhostMetricsMonitor>(out var entity))
            {
                if (state.EntityManager.HasComponent<GhostNames>(entity) &&
                    state.EntityManager.HasComponent<GhostSerializationMetrics>(entity))
                {
                    var ghostNames = SystemAPI.GetSingletonBuffer<GhostNames>();
                    if (ghostNames.Length > 0)
                    {
                        var job = new ProfilerRecorderJob
                        {
                            names = ghostNames,
                            recorders = m_Recorders
                        };
                        state.Dependency = job.Schedule(state.Dependency);
                        setupRecorders = true;
                    }
                }
            }

            if (!SystemAPI.HasSingleton<UnscaledClientTime>() || !SystemAPI.HasSingleton<NetworkSnapshotAck>())
                return;

            var ack = SystemAPI.GetSingleton<NetworkSnapshotAck>();
            var networkTimeSystemStats = SystemAPI.GetSingleton<NetworkTimeSystemStats>();
            int minAge = m_SnapshotTickMax.IsValid?currentTick.TicksSince(m_SnapshotTickMax):0;
            int maxAge = m_SnapshotTickMin.IsValid?currentTick.TicksSince(m_SnapshotTickMin):0;
            var timeSample = new TimeSample
            {
                sampleFraction = networkTime.ServerTickFraction,
                timeScale = networkTimeSystemStats.GetAverageTimeScale(),
                interpolationScale = networkTimeSystemStats.GetAverageIterpTimeScale(),
                interpolationOffset = networkTimeSystemStats.currentInterpolationFrames,
                commandAge = ack.ServerCommandAge / 256f,
                rtt = ack.EstimatedRTT,
                jitter = ack.DeviationRTT,
                snapshotAgeMin = minAge,
                snapshotAgeMax = maxAge,
            };
            if (m_TimeSamples.Length < 255)
                m_TimeSamples.Add(timeSample);
        }

        void BeginCollection(NetworkTick currentTick, ref GhostStatsCollectionData collectionData)
        {
            if (collectionData.m_StatIndex >= 0 && collectionData.m_CollectionTick.IsValid)
                BuildPacket(ref collectionData);

            collectionData.m_CollectionTick = currentTick;
            m_SnapshotTicks.Clear();
            m_TimeSamples.Clear();
            for (int i = 0; i < m_SnapshotStats.Length; ++i)
            {
                m_SnapshotStats[i] = 0;
            }
            for (int i = 0; i < m_PredictionErrors.Length; ++i)
            {
                m_PredictionErrors[i] = 0;
            }

            m_CommandTicks.Clear();
            m_CommandStats = 0;
            m_DiscardedPackets = 0;
        }

        void BuildPacket(ref GhostStatsCollectionData statsData)
        {
            statsData.EnsurePoolSize(statsData.m_MaxPacketSize);
            int binarySize = 0;
            var binaryData = ((byte*)statsData.m_PacketPool.GetUnsafePtr()) + statsData.m_UsedPacketPoolSize;
            *(uint*) binaryData = statsData.m_CollectionTick.TickIndexForValidTick;
            binarySize += 4;
            binaryData[binarySize++] = (byte) statsData.m_StatIndex;
            binaryData[binarySize++] = (byte) m_TimeSamples.Length;
            binaryData[binarySize++] = (byte) m_SnapshotTicks.Length;
            binaryData[binarySize++] = (byte) m_CommandTicks.Length;
            binaryData[binarySize++] = 0; // rpcs
            binaryData[binarySize++] = (byte)m_DiscardedPackets;
            binaryData[binarySize++] = 0; // unused
            binaryData[binarySize++] = 0; // unused

            for (int i = 0; i < m_TimeSamples.Length; ++i)
            {
                float* timeSample = (float*) (binaryData + binarySize);
                timeSample[0] = m_TimeSamples[i].sampleFraction;
                timeSample[1] = m_TimeSamples[i].timeScale;
                timeSample[2] = m_TimeSamples[i].interpolationOffset;
                timeSample[3] = m_TimeSamples[i].interpolationScale;
                timeSample[4] = m_TimeSamples[i].commandAge;
                timeSample[5] = m_TimeSamples[i].rtt;
                timeSample[6] = m_TimeSamples[i].jitter;
                timeSample[7] = m_TimeSamples[i].snapshotAgeMin;
                timeSample[8] = m_TimeSamples[i].snapshotAgeMax;
                binarySize += 36;
            }
            // Write snapshots
            for (int i = 0; i < m_SnapshotTicks.Length; ++i)
            {
                *(uint*) (binaryData + binarySize) = m_SnapshotTicks[i];
                binarySize += 4;
            }
            for (int i = 0; i < m_SnapshotStats.Length; ++i)
            {
                *(uint*) (binaryData + binarySize) = m_SnapshotStats[i];
                binarySize += 4;
            }
            // Write prediction errors
            for (int i = 0; i < m_PredictionErrors.Length; ++i)
            {
                *(float*) (binaryData + binarySize) = m_PredictionErrors[i];
                binarySize += 4;
            }
            // Write commands
            for (int i = 0; i < m_CommandTicks.Length; ++i)
            {
                *(uint*) (binaryData + binarySize) = m_CommandTicks[i];
                binarySize += 4;
            }
            *(uint*) (binaryData + binarySize) = m_CommandStats;
            binarySize += 4;

            statsData.m_PacketQueue.Add(new Packet
            {
                dataSize = binarySize,
                dataOffset = statsData.m_UsedPacketPoolSize
            });
            statsData.m_UsedPacketPoolSize += binarySize;
        }


        internal struct Packet
        {
            public int dataSize;
            public int dataOffset;
            public bool isString;
        }

        private bool setupRecorders;

        private NativeList<Packet> m_PacketQueue;
        private NativeList<byte> m_PacketPool;

        private NativeList<ProfilerRecorder> m_Recorders;
        private NetworkTick m_SnapshotTickMin;
        private NetworkTick m_SnapshotTickMax;
        private NativeList<TimeSample> m_TimeSamples;
        private NativeList<uint> m_SnapshotTicks;
        private NativeList<uint> m_SnapshotStats;
        private NativeList<float> m_PredictionErrors;
        private uint m_CommandStats;
        private uint m_DiscardedPackets;
        private NativeList<uint> m_CommandTicks;

        private NativeText m_LastNameAndErrorArray;
        private NativeArray<uint> m_CommandStatsData;
        private NativeList<uint> m_SnapshotStatsData;
        private NativeList<float> m_PredictionErrorStatsData;
        private NativeArray<NetworkTick> m_MinMaxTickStatsData;

        struct TimeSample
        {
            public float sampleFraction;
            public float timeScale;
            public float interpolationOffset;
            public float interpolationScale;
            public float commandAge;
            public float rtt;
            public float jitter;
            public float snapshotAgeMin;
            public float snapshotAgeMax;
        }
        void UpdateMetrics(ref SystemState state, NetworkTick currentTick)
        {
            var hasTimeSamples = m_TimeSamples.Length > 0;
            var hasSnapshotSamples = m_SnapshotTicks.Length > 0;
            var hasSnapshotStats = m_SnapshotStats.Length > 0;
            var hasPredictionErrors = m_PredictionErrors.Length > 0;

            uint totalSize = 0;
            uint totalCount = 0;

            if (SystemAPI.TryGetSingletonEntity<GhostMetricsMonitor>(out var entity))
            {
                ref var simulationMetrics = ref SystemAPI.GetSingletonRW<GhostMetricsMonitor>().ValueRW;
                simulationMetrics.CapturedTick = currentTick;

                if (hasTimeSamples && state.EntityManager.HasComponent<NetworkMetrics>(entity))
                {
                    ref var networkMetrics = ref SystemAPI.GetSingletonRW<NetworkMetrics>().ValueRW;

                    networkMetrics.SampleFraction = m_TimeSamples[0].sampleFraction;
                    networkMetrics.TimeScale = m_TimeSamples[0].timeScale;
                    networkMetrics.InterpolationOffset = m_TimeSamples[0].interpolationOffset;
                    networkMetrics.InterpolationScale = m_TimeSamples[0].interpolationScale;
                    networkMetrics.CommandAge = m_TimeSamples[0].commandAge;
                    networkMetrics.Rtt = m_TimeSamples[0].rtt;
                    networkMetrics.Jitter = m_TimeSamples[0].jitter;
                    networkMetrics.SnapshotAgeMin = m_TimeSamples[0].snapshotAgeMin;
                    networkMetrics.SnapshotAgeMax = m_TimeSamples[0].snapshotAgeMax;
                }
                if (hasPredictionErrors && state.EntityManager.HasComponent<PredictionErrorMetrics>(entity))
                {
                    if (SystemAPI.TryGetSingletonBuffer<PredictionErrorMetrics>(out var predictionErrorMetrics))
                    {
                        predictionErrorMetrics.Clear();
                        var count = m_PredictionErrors.Length;

                        for (int i = 0; i < count; i++)
                        {
                            predictionErrorMetrics.Add(new PredictionErrorMetrics
                            {
                                Value = m_PredictionErrors[i]
                            });
                        }
                    }
                }

                if (hasSnapshotStats && state.EntityManager.HasComponent<GhostMetrics>(entity))
                {
                    if (SystemAPI.TryGetSingletonBuffer<GhostMetrics>(out var ghostMetrics))
                    {
                        ghostMetrics.Clear();
                        var count = m_SnapshotStats.Length;
                        Assert.IsTrue((count - 3) % 3 == 0);

                        for (int i = 3; i < count; i += 3)
                        {
                            ghostMetrics.Add(new GhostMetrics
                            {
                                InstanceCount = m_SnapshotStats[i],
                                SizeInBits = m_SnapshotStats[i + 1],
                                ChunkCount = m_SnapshotStats[i + 2],
                                Uncompressed = m_SnapshotStats[i + 2],
                            });
                            totalSize += m_SnapshotStats[i + 1];
                            totalCount += m_SnapshotStats[i];
                        }
                    }
                }
                if (hasSnapshotSamples && state.EntityManager.HasComponent<SnapshotMetrics>(entity))
                {
                    ref var snapshotMetrics = ref SystemAPI.GetSingletonRW<SnapshotMetrics>().ValueRW;

                    snapshotMetrics.SnapshotTick = m_SnapshotTicks[0];
                    snapshotMetrics.TotalSizeInBits = totalSize;
                    snapshotMetrics.TotalGhostCount = totalCount;
                    snapshotMetrics.DestroyInstanceCount = hasSnapshotStats ? m_SnapshotStats[0] : 0;
                    snapshotMetrics.DestroySizeInBits = hasSnapshotStats ? m_SnapshotStats[1] : 0;
                }
            }

            if (m_Recorders.IsCreated && SystemAPI.TryGetSingletonBuffer<GhostSerializationMetrics>(out var serializationMetrics))
            {
                serializationMetrics.Clear();
                var count = m_Recorders.Length;

                for (int i = 0; i < count; i++)
                {
                    serializationMetrics.Add(new GhostSerializationMetrics
                    {
                        LastRecordedValue = m_Recorders[i].LastValue
                    });
                }
            }
        }

        struct ProfilerRecorderJob : IJob
        {
            public DynamicBuffer<GhostNames> names;
            public NativeList<ProfilerRecorder> recorders;
            public void Execute()
            {
                for (int i = 0; i < names.Length; i++)
                {
                    recorders.Add(ProfilerRecorder.StartNew(new ProfilerCategory("GhostSendSystem"),
                        names[i].Name.Value));
                }
            }
        }
    }

    internal struct GhostStatsCollectionData : IComponentData
    {
        public NativeList<byte> m_PacketPool;
        public NativeList<GhostStatsCollectionSystem.Packet> m_PacketQueue;
        public NativeText m_LastNameAndErrorArray;
        public NativeList<uint> m_SnapshotStats;
        public NativeList<float> m_PredictionErrors;
        public int m_StatIndex;
        public int m_UsedPacketPoolSize;
        public int m_MaxPacketSize;
        public NetworkTick m_CollectionTick;

        public void EnsurePoolSize(int packetSize)
        {
            if (m_UsedPacketPoolSize + packetSize > m_PacketPool.Length)
            {
                int newLen = m_PacketPool.Length*2;
                while (m_UsedPacketPoolSize + packetSize > newLen)
                    newLen *= 2;
                m_PacketPool.Resize(newLen, NativeArrayOptions.UninitializedMemory);
            }
        }

        public void UpdateMaxPacketSize(int snapshotStatsLength, int predictionErrorsLength)
        {
            // Calculate a new max packet size
            var packetSize = 8 + 20 * 255 + 4 * snapshotStatsLength + 4 * predictionErrorsLength + 4 * 255;
            if (packetSize == m_MaxPacketSize)
                return;
            m_MaxPacketSize = packetSize;

            // Drop all pending packets not yet in the queue
            m_CollectionTick = NetworkTick.Invalid;
        }

        /// <summary>
        /// Setup the ghosts prefabs and error names (used by the NetworkDebugger tool). Called after the prefab collection has been
        /// processed by the <see cref="GhostCollectionSystem"/>
        /// </summary>
        /// <param name="nameList"></param>
        /// <param name="errorList"></param>
        /// <param name="worldName"></param>
        public void SetGhostNames(in FixedString128Bytes worldName, NativeList<FixedString64Bytes> nameList, NativeList<PredictionErrorNames> errorList)
        {
            // Add a pending packet with the new list of names
            m_LastNameAndErrorArray.Clear();
            m_LastNameAndErrorArray.Append((FixedString32Bytes)"\"name\":\"");
            m_LastNameAndErrorArray.Append(worldName);
            m_LastNameAndErrorArray.Append((FixedString32Bytes)"\",\"ghosts\":[\"Destroy\"");
            for (int i = 0; i < nameList.Length; ++i)
            {
                m_LastNameAndErrorArray.Append(',');
                m_LastNameAndErrorArray.Append('"');
                m_LastNameAndErrorArray.Append(nameList[i]);
                m_LastNameAndErrorArray.Append('"');
            }

            m_LastNameAndErrorArray.Append((FixedString32Bytes)"], \"errors\":[");
            if (errorList.Length > 0)
            {
                m_LastNameAndErrorArray.Append('"');
                m_LastNameAndErrorArray.Append(errorList[0].Name);
                m_LastNameAndErrorArray.Append('"');
            }
            for (int i = 1; i < errorList.Length; ++i)
            {
                m_LastNameAndErrorArray.Append(',');
                m_LastNameAndErrorArray.Append('"');
                m_LastNameAndErrorArray.Append(errorList[i].Name);
                m_LastNameAndErrorArray.Append('"');
            }

            m_LastNameAndErrorArray.Append(']');

            if (m_SnapshotStats.Length != ((nameList.Length + 1) * 3))
            {
                m_SnapshotStats.Clear();
                m_SnapshotStats.ResizeUninitialized((nameList.Length + 1) * 3);
            }

            if (m_PredictionErrors.Length != errorList.Length)
            {
                m_PredictionErrors.Clear();
                m_PredictionErrors.ResizeUninitialized(errorList.Length);
            }

            if (m_StatIndex < 0)
                return;

            AppendNamePacket();
        }

        public unsafe void AppendNamePacket()
        {
            FixedString64Bytes header = "{\"index\":";
            header.Append(m_StatIndex);
            header.Append(',');
            FixedString32Bytes footer = "}";

            var totalLen = header.Length + m_LastNameAndErrorArray.Length + footer.Length;
            EnsurePoolSize(totalLen);

            var binaryData = ((byte*)m_PacketPool.GetUnsafePtr()) + m_UsedPacketPoolSize;
            UnsafeUtility.MemCpy(binaryData, header.GetUnsafePtr(), header.Length);
            UnsafeUtility.MemCpy(binaryData + header.Length, m_LastNameAndErrorArray.GetUnsafePtr(), m_LastNameAndErrorArray.Length);
            UnsafeUtility.MemCpy(binaryData + header.Length + m_LastNameAndErrorArray.Length, footer.GetUnsafePtr(), footer.Length);

            m_PacketQueue.Add(new GhostStatsCollectionSystem.Packet
            {
                dataSize = totalLen,
                dataOffset = m_UsedPacketPoolSize,
                isString = true
            });
            m_UsedPacketPoolSize += totalLen;
            // Make sure the packet size is big enough for the new snapshot stats
            UpdateMaxPacketSize(m_SnapshotStats.Length, m_PredictionErrors.Length);
        }

    }
}
#endif
