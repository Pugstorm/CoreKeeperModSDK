using System;
using System.Collections.Generic;
using Unity.Assertions;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Burst.Intrinsics;

namespace Unity.NetCode
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderFirst=true)]
    [BurstCompile]
    internal partial struct GhostPredictionDisableSimulateSystem : ISystem
    {
        ComponentTypeHandle<Simulate> m_SimulateHandle;
        ComponentTypeHandle<PredictedGhost> m_PredictedHandle;
        ComponentTypeHandle<GhostChildEntity> m_GhostChildEntityHandle;
        BufferTypeHandle<LinkedEntityGroup> m_LinkedEntityGroupHandle;
        EntityQuery m_PredictedQuery;
        EntityQuery m_NetworkTimeSingleton;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_SimulateHandle = state.GetComponentTypeHandle<Simulate>();
            m_PredictedHandle = state.GetComponentTypeHandle<PredictedGhost>(true);
            m_GhostChildEntityHandle = state.GetComponentTypeHandle<GhostChildEntity>(true);
            m_LinkedEntityGroupHandle = state.GetBufferTypeHandle<LinkedEntityGroup>(true);
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<Simulate>()
                .WithAll<GhostInstance, PredictedGhost>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState);
            m_PredictedQuery = state.GetEntityQuery(builder);
            m_NetworkTimeSingleton = state.GetEntityQuery(ComponentType.ReadOnly<NetworkTime>());
        }
        [BurstCompile]
        struct TogglePredictedJob : IJobChunk
        {
            public ComponentTypeHandle<Simulate> simulateHandle;
            [ReadOnly] public ComponentTypeHandle<PredictedGhost> predictedHandle;
            [ReadOnly] public ComponentTypeHandle<GhostChildEntity> ghostChildEntityHandle;
            [ReadOnly] public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupHandle;
            public EntityStorageInfoLookup storageInfoFromEntity;
            public NetworkTick tick;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);

                var predicted = chunk.GetNativeArray(ref predictedHandle);

                if (chunk.Has(ref linkedEntityGroupHandle))
                {
                    var linkedEntityGroupArray = chunk.GetBufferAccessor(ref linkedEntityGroupHandle);

                    for(int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; i++)
                    {
                        var shouldPredict = predicted[i].ShouldPredict(tick);
                        if (chunk.IsComponentEnabled(ref simulateHandle, i) != shouldPredict)
                        {
                            chunk.SetComponentEnabled(ref simulateHandle, i, shouldPredict);
                            var linkedEntityGroup = linkedEntityGroupArray[i];
                            for (int child = 1; child < linkedEntityGroup.Length; ++child)
                            {
                                var storageInfo = storageInfoFromEntity[linkedEntityGroup[child].Value];
                                if (storageInfo.Chunk.Has(ref ghostChildEntityHandle) && storageInfo.Chunk.Has(ref simulateHandle))
                                    storageInfo.Chunk.SetComponentEnabled(ref simulateHandle, storageInfo.IndexInChunk, shouldPredict);
                            }
                        }
                    }
                }
                else
                {
                    for(int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; i++)
                    {
                        chunk.SetComponentEnabled(ref simulateHandle, i, predicted[i].ShouldPredict(tick));
                    }
                }
            }
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = m_NetworkTimeSingleton.GetSingleton<NetworkTime>();
            var tick = networkTime.ServerTick;
            m_SimulateHandle.Update(ref state);
            m_PredictedHandle.Update(ref state);
            m_GhostChildEntityHandle.Update(ref state);
            m_LinkedEntityGroupHandle.Update(ref state);
            var predictedJob = new TogglePredictedJob
            {
                simulateHandle = m_SimulateHandle,
                predictedHandle = m_PredictedHandle,
                ghostChildEntityHandle = m_GhostChildEntityHandle,
                linkedEntityGroupHandle = m_LinkedEntityGroupHandle,
                storageInfoFromEntity = state.GetEntityStorageInfoLookup(),
                tick = tick
            };
            state.Dependency = predictedJob.ScheduleParallel(m_PredictedQuery, state.Dependency);
        }
    }

    class NetcodeServerPredictionRateManager : IRateManager
    {
        private EntityQuery m_NetworkTimeQuery;
        private bool m_DidPushTime;
        internal NetcodeServerPredictionRateManager(ComponentSystemGroup group)
        {
            m_NetworkTimeQuery = group.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkTime>());
        }
        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            const NetworkTimeFlags serverPredictionFlags = NetworkTimeFlags.IsInPredictionLoop |
                                        NetworkTimeFlags.IsFirstPredictionTick |
                                        NetworkTimeFlags.IsFinalPredictionTick |
                                        NetworkTimeFlags.IsFinalFullPredictionTick |
                                        NetworkTimeFlags.IsFirstTimeFullyPredictingTick;
            //Here it is better to get the singleon entity
            ref var networkTime = ref m_NetworkTimeQuery.GetSingletonRW<NetworkTime>().ValueRW;
            if (!m_DidPushTime)
            {
                networkTime.Flags |= serverPredictionFlags;
                m_DidPushTime = true;
                return true;
            }
            // Reset all the prediction flags. They are not valid outside the prediction loop
            networkTime.Flags &= ~serverPredictionFlags;
            m_DidPushTime = false;
            return false;
        }
        public float Timestep
        {
            get
            {
                throw new System.NotImplementedException();
            }
            set
            {
                throw new System.NotImplementedException();
            }
        }
    }
    unsafe class NetcodeClientPredictionRateManager : IRateManager
    {
        private EntityQuery m_NetworkTimeQuery;
        private EntityQuery m_ClientServerTickRateQuery;
        private EntityQuery m_ClientTickRateQuery;

        private EntityQuery m_AppliedPredictedTicksQuery;
        private EntityQuery m_UniqueInputTicksQuery;

        private EntityQuery m_GhostQuery;
        private EntityQuery m_GhostChildQuery;

        private NetworkTick m_LastFullPredictionTick;

        private int m_TickIdx;
        private NetworkTick m_TargetTick;
        private NetworkTime m_CurrentTime;
        private float m_FixedTimeStep;
        private double m_ElapsedTime;

        private NativeArray<NetworkTick> m_AppliedPredictedTickArray;
        private int m_NumAppliedPredictedTicks;

        private uint m_MaxBatchSize;
        private uint m_MaxBatchSizeFirstTimeTick;
        private DoubleRewindableAllocators* m_OldGroupAllocators = null;

        public struct TickComparer : IComparer<NetworkTick>
        {
            public TickComparer(NetworkTick target)
            {
                m_TargetTick = target;
            }
            NetworkTick m_TargetTick;
            public int Compare(NetworkTick x, NetworkTick y)
            {
                var ageX = m_TargetTick.TicksSince(x);
                var ageY = m_TargetTick.TicksSince(y);
                // Sort by decreasing age, which gives increasing ticks with oldest tick first
                return ageY - ageX;
            }
        }

        internal NetcodeClientPredictionRateManager(ComponentSystemGroup group)
        {
            // Create the queries for singletons
            m_NetworkTimeQuery = group.World.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkTime>());
            m_ClientServerTickRateQuery = group.World.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ClientServerTickRate>());
            m_ClientTickRateQuery = group.World.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ClientTickRate>());

            m_AppliedPredictedTicksQuery = group.World.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<GhostPredictionGroupTickState>());
            m_UniqueInputTicksQuery = group.World.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<UniqueInputTickMap>());

            var builder = new EntityQueryDesc
            {
                All = new[]{ComponentType.ReadWrite<Simulate>(), ComponentType.ReadOnly<GhostInstance>()},
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            };
            m_GhostQuery = group.World.EntityManager.CreateEntityQuery(builder);
            builder = new EntityQueryDesc
            {
                All = new[]{ComponentType.ReadWrite<Simulate>(), ComponentType.ReadOnly<GhostChildEntity>()},
                Options = EntityQueryOptions.IgnoreComponentEnabledState
            };
            m_GhostChildQuery = group.World.EntityManager.CreateEntityQuery(builder);
        }
        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            ref var networkTime = ref m_NetworkTimeQuery.GetSingletonRW<NetworkTime>().ValueRW;
            if (m_TickIdx == 0)
            {
                networkTime.PredictedTickIndex = 0;
                m_CurrentTime = networkTime;

                m_AppliedPredictedTicksQuery.CompleteDependency();
                m_UniqueInputTicksQuery.CompleteDependency();

                var appliedPredictedTicks = m_AppliedPredictedTicksQuery.GetSingletonRW<GhostPredictionGroupTickState>().ValueRW.AppliedPredictedTicks;
                var uniqueInputTicks = m_UniqueInputTicksQuery.GetSingletonRW<UniqueInputTickMap>().ValueRW.TickMap;


                // Nothing to predict
                if (!m_CurrentTime.ServerTick.IsValid || appliedPredictedTicks.IsEmpty)
                {
                    uniqueInputTicks.Clear();
                    appliedPredictedTicks.Clear();
                    return false;
                }

                m_TargetTick = m_CurrentTime.ServerTick;
                m_ClientServerTickRateQuery.TryGetSingleton<ClientServerTickRate>(out var clientServerTickRate);
                clientServerTickRate.ResolveDefaults();
                m_FixedTimeStep = clientServerTickRate.SimulationFixedTimeStep;
                m_ElapsedTime = group.World.Time.ElapsedTime;
                if (networkTime.IsPartialTick)
                {
                    m_TargetTick.Decrement();
                    m_ElapsedTime -= m_FixedTimeStep * networkTime.ServerTickFraction;
                }
                // We must simulate the last full tick since the history backup is applied there
                appliedPredictedTicks.TryAdd(m_TargetTick, m_TargetTick);
                // We must simulate at the tick we used as last full tick last time since smoothing and error reporting is happening there
                if (m_LastFullPredictionTick.IsValid && m_TargetTick.IsNewerThan(m_LastFullPredictionTick))
                    appliedPredictedTicks.TryAdd(m_LastFullPredictionTick, m_LastFullPredictionTick);

                m_AppliedPredictedTickArray = appliedPredictedTicks.GetKeyArray(Allocator.Temp);

                NetworkTick oldestTick = NetworkTick.Invalid;
                for (int i = 0; i < m_AppliedPredictedTickArray.Length; ++i)
                {
                    NetworkTick appliedTick = m_AppliedPredictedTickArray[i];
                    if (!oldestTick.IsValid || oldestTick.IsNewerThan(appliedTick))
                        oldestTick = appliedTick;
                }
                if (!oldestTick.IsValid)
                {
                    uniqueInputTicks.Clear();
                    appliedPredictedTicks.Clear();
                    return false;
                }
                bool hasNew = false;
                for (var i = oldestTick; i != m_TargetTick; i.Increment())
                {
                    var nextTick = i;
                    nextTick.Increment();
                    if (uniqueInputTicks.TryGetValue(nextTick, out var inputTick))
                    {
                        hasNew |= appliedPredictedTicks.TryAdd(i, i);
                    }
                }
                uniqueInputTicks.Clear();
                if (hasNew)
                    m_AppliedPredictedTickArray = appliedPredictedTicks.GetKeyArray(Allocator.Temp);

                appliedPredictedTicks.Clear();
                m_AppliedPredictedTickArray.Sort(new TickComparer(m_CurrentTime.ServerTick));

                m_NumAppliedPredictedTicks = m_AppliedPredictedTickArray.Length;
                // remove everything newer than the target tick
                while (m_NumAppliedPredictedTicks > 0 && m_AppliedPredictedTickArray[m_NumAppliedPredictedTicks-1].IsNewerThan(m_TargetTick))
                    --m_NumAppliedPredictedTicks;
                // remove everything older than "server tick - max inputs"
                int toRemove = 0;
                while (toRemove < m_NumAppliedPredictedTicks && (uint)m_CurrentTime.ServerTick.TicksSince(m_AppliedPredictedTickArray[toRemove]) > CommandDataUtility.k_CommandDataMaxSize)
                    ++toRemove;
                if (toRemove > 0)
                {
                    m_NumAppliedPredictedTicks -= toRemove;
                    for (int i = 0; i < m_NumAppliedPredictedTicks; ++i)
                        m_AppliedPredictedTickArray[i] = m_AppliedPredictedTickArray[i+toRemove];
                }

                networkTime.Flags |= NetworkTimeFlags.IsInPredictionLoop | NetworkTimeFlags.IsFirstPredictionTick;
                networkTime.Flags &= ~(NetworkTimeFlags.IsFinalPredictionTick|NetworkTimeFlags.IsFinalFullPredictionTick|NetworkTimeFlags.IsFirstTimeFullyPredictingTick);

                group.World.EntityManager.SetComponentEnabled<Simulate>(m_GhostQuery, false);
                group.World.EntityManager.SetComponentEnabled<Simulate>(m_GhostChildQuery, false);

                m_ClientTickRateQuery.TryGetSingleton<ClientTickRate>(out var clientTickRate);
                if (clientTickRate.MaxPredictionStepBatchSizeRepeatedTick < 1)
                    clientTickRate.MaxPredictionStepBatchSizeRepeatedTick = 1;
                if (clientTickRate.MaxPredictionStepBatchSizeFirstTimeTick < 1)
                    clientTickRate.MaxPredictionStepBatchSizeFirstTimeTick = 1;
                m_MaxBatchSize = (uint)clientTickRate.MaxPredictionStepBatchSizeRepeatedTick;
                m_MaxBatchSizeFirstTimeTick = (uint)clientTickRate.MaxPredictionStepBatchSizeFirstTimeTick;
                if (!m_LastFullPredictionTick.IsValid)
                    m_MaxBatchSize = m_MaxBatchSizeFirstTimeTick;
                m_TickIdx = 1;
            }
            else
            {
                networkTime.Flags &= ~NetworkTimeFlags.IsFirstPredictionTick;
                group.World.PopTime();
                group.World.RestoreGroupAllocator(m_OldGroupAllocators);
            }
            if (m_TickIdx < m_NumAppliedPredictedTicks)
            {
                NetworkTick predictingTick = m_AppliedPredictedTickArray[m_TickIdx];
                NetworkTick prevTick = m_AppliedPredictedTickArray[m_TickIdx-1];
                uint batchSize = (uint)predictingTick.TicksSince(prevTick);
                if (batchSize > m_MaxBatchSize)
                {
                    batchSize = m_MaxBatchSize;
                    predictingTick = prevTick;
                    predictingTick.Add(batchSize);
                    m_AppliedPredictedTickArray[m_TickIdx-1] = predictingTick;
                }
                else
                {
                    ++m_TickIdx;
                }
                uint tickAge = (uint)m_TargetTick.TicksSince(predictingTick);

                // If we just reached the last full tick we predicted last time, switch to use the separate long step setting for new ticks
                if (predictingTick == m_LastFullPredictionTick)
                    m_MaxBatchSize = m_MaxBatchSizeFirstTimeTick;

                if (predictingTick == m_CurrentTime.ServerTick)
                    networkTime.Flags |= NetworkTimeFlags.IsFinalPredictionTick;
                if (predictingTick == m_TargetTick)
                    networkTime.Flags |= NetworkTimeFlags.IsFinalFullPredictionTick;
                if (!m_LastFullPredictionTick.IsValid || predictingTick.IsNewerThan(m_LastFullPredictionTick))
                {
                    networkTime.Flags |= NetworkTimeFlags.IsFirstTimeFullyPredictingTick;
                    m_LastFullPredictionTick = predictingTick;
                }
                networkTime.ServerTick = predictingTick;
                networkTime.SimulationStepBatchSize = (int)batchSize;
                networkTime.ServerTickFraction = 1f;
                group.World.PushTime(new TimeData(m_ElapsedTime - m_FixedTimeStep*tickAge, m_FixedTimeStep*batchSize));
                m_OldGroupAllocators = group.World.CurrentGroupAllocators;
                group.World.SetGroupAllocator(group.RateGroupAllocators);
                networkTime.PredictedTickIndex++;
                return true;
            }

            if (m_TickIdx == m_NumAppliedPredictedTicks && m_CurrentTime.ServerTickFraction < 1f)
            {
#if UNITY_EDITOR || NETCODE_DEBUG
                if(networkTime.IsFinalPredictionTick)
                    throw new InvalidOperationException("IsFinalPredictionTick should not be set before executing the final prediction tick");
#endif
                networkTime.ServerTick = m_CurrentTime.ServerTick;
                networkTime.SimulationStepBatchSize = 1;
                networkTime.ServerTickFraction = m_CurrentTime.ServerTickFraction;
                networkTime.Flags |= NetworkTimeFlags.IsFinalPredictionTick;
                networkTime.Flags &= ~(NetworkTimeFlags.IsFinalFullPredictionTick | NetworkTimeFlags.IsFirstTimeFullyPredictingTick);
                group.World.PushTime(new TimeData(group.World.Time.ElapsedTime, m_FixedTimeStep * m_CurrentTime.ServerTickFraction));
                m_OldGroupAllocators = group.World.CurrentGroupAllocators;
                group.World.SetGroupAllocator(group.RateGroupAllocators);
                ++m_TickIdx;
                networkTime.PredictedTickIndex++;
                return true;
            }
            group.World.EntityManager.SetComponentEnabled<Simulate>(m_GhostQuery, true);
            group.World.EntityManager.SetComponentEnabled<Simulate>(m_GhostChildQuery, true);
#if UNITY_EDITOR || NETCODE_DEBUG
            if (!networkTime.IsFinalPredictionTick)
                throw new InvalidOperationException("IsFinalPredictionTick should not be set before executing the final prediction tick");
            if (networkTime.ServerTick != m_CurrentTime.ServerTick)
                throw new InvalidOperationException("ServerTick should be equals to current server tick at the end of the prediction loop");
            if (math.abs(networkTime.ServerTickFraction-m_CurrentTime.ServerTickFraction) > 1e-6f)
                throw new InvalidOperationException("ServerTickFraction should be equals to current tick fraction at the end of the prediction loop");
#endif
            // Reset all the prediction flags. They are not valid outside the prediction loop
            networkTime.Flags &= ~(NetworkTimeFlags.IsInPredictionLoop |
                                   NetworkTimeFlags.IsFirstPredictionTick |
                                   NetworkTimeFlags.IsFinalPredictionTick |
                                   NetworkTimeFlags.IsFinalFullPredictionTick |
                                   NetworkTimeFlags.IsFirstTimeFullyPredictingTick);
            networkTime.SimulationStepBatchSize = m_CurrentTime.SimulationStepBatchSize;
            m_TickIdx = 0;
            return false;
        }
        public float Timestep
        {
            get
            {
                throw new System.NotImplementedException();
            }
            set
            {
                throw new System.NotImplementedException();
            }
        }
    }

    unsafe class NetcodePredictionFixedRateManager : IRateManager
    {
        public float Timestep
        {
            get => m_TimeStep;
            set
            {
                m_TimeStep = value;
#if UNITY_EDITOR || NETCODE_DEBUG
                m_DeprecatedTimeStep = value;
#endif
            }
        }

        int m_RemainingUpdates;
        float m_TimeStep;
        double m_ElapsedTime;
        private EntityQuery networkTimeQuery;
        //used to track invalid usage of the TimeStep setter.
#if UNITY_EDITOR || NETCODE_DEBUG
        float m_DeprecatedTimeStep;
        public float DeprecatedTimeStep
        {
            get=> m_DeprecatedTimeStep;
            set => m_DeprecatedTimeStep = value;
        }

#endif
        DoubleRewindableAllocators* m_OldGroupAllocators = null;

        public NetcodePredictionFixedRateManager(float defaultTimeStep)
        {
            SetTimeStep(defaultTimeStep);
        }

        public void OnCreate(ComponentSystemGroup group)
        {
            networkTimeQuery = group.EntityManager.CreateEntityQuery(typeof(NetworkTime));
        }

        public void SetTimeStep(float timeStep)
        {
            m_TimeStep = timeStep;
#if UNITY_EDITOR || NETCODE_DEBUG
            m_DeprecatedTimeStep = 0f;
#endif
        }

        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            // if this is true, means we're being called a second or later time in a loop
            if (m_RemainingUpdates > 0)
            {
                group.World.PopTime();
                group.World.RestoreGroupAllocator(m_OldGroupAllocators);
                --m_RemainingUpdates;
            }
            else if(m_TimeStep > 0f)
            {
                // Add epsilon to account for floating point inaccuracy
                m_RemainingUpdates = (int)((group.World.Time.DeltaTime + 0.001f) / m_TimeStep);
                if (m_RemainingUpdates > 0)
                {
                    var networkTime = networkTimeQuery.GetSingleton<NetworkTime>();
                    m_ElapsedTime = group.World.Time.ElapsedTime;
                    if (networkTime.IsPartialTick)
                    {
                        //dt = m_FixedTimeStep * networkTime.ServerTickFraction;
                        //elapsed since last full tick = m_ElapsedTime - dt;
                        m_ElapsedTime -= group.World.Time.DeltaTime;
                        m_ElapsedTime += m_RemainingUpdates * m_TimeStep;
                    }
                }
            }
            if (m_RemainingUpdates == 0)
                return false;
            group.World.PushTime(new TimeData(
                elapsedTime: m_ElapsedTime - (m_RemainingUpdates-1)*m_TimeStep,
                deltaTime: m_TimeStep));
            m_OldGroupAllocators = group.World.CurrentGroupAllocators;
            group.World.SetGroupAllocator(group.RateGroupAllocators);
            return true;
        }
    }

    /// <summary>
    /// <para>The parent group for all "deterministic" gameplay systems that modify predicted ghosts.
    /// This system group runs for both the client and server worlds at a fixed time step, as specified by
    /// the <see cref="ClientServerTickRate.SimulationTickRate"/> setting.</para>
    /// <para>On the server, this group is only updated once per tick, because it runs in tandem with the <see cref="SimulationSystemGroup"/>
    /// (i.e. at a fixed time step, at the same rate).
    /// On the client, the group implements the client-side prediction logic by running the client simulation ahead of the server.</para>
    /// <para><b>Importantly: Because the client is predicting ahead of the server, all systems in this group will be updated multiple times
    /// per simulation frame, every single time the client receives a new snapshot (see <see cref="ClientServerTickRate.NetworkTickRate"/>
    /// and <see cref="ClientServerTickRate.SimulationTickRate"/>). This is called "rollback and re-simulation".</b></para>
    /// <para>These re-simulation prediction group ticks also get more frequent at higher pings.
    /// I.e. Simplified: A 200ms client will likely re-simulate roughly x2 more frames than a 100ms connection, with caveats.
    /// And note: The number of predicted, re-simulated frames can easily reach double digits. Thus, systems in this group
    /// must be exceptionally fast, and are likely your CPU "hot path".
    /// <i>To help mitigate this, take a look at prediction group batching here <see cref="ClientTickRate.MaxPredictionStepBatchSizeRepeatedTick"/>.</i></para>
    /// <para>Pragmatically: This group contains most of the game simulation (or, at least, all simulation that should be "predicted"
    /// (i.e. simulation that is the same on both client and server)). On the server, all prediction logic is treated as
    /// authoritative game state (although thankfully it only needs to be simulated once, as it's authoritative).</para>
    /// <para>Note: This SystemGroup is intentionally added to non-netcode worlds, to help enable single-player testing.</para>
    /// </summary>
    /// <remarks> To reiterate: Because child systems in this group are updated so frequently (multiple times per frame on the client,
    /// and for all predicted ghosts on the server), this group is usually the most expensive on both builds.
    /// Pay particular attention to the systems that run in this group to keep your performance in check.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst=true)]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
    public partial class PredictedSimulationSystemGroup : ComponentSystemGroup
    {}

    /// <summary>
    /// <para>A fixed update group inside the ghost prediction. This is equivalent to <see cref="FixedStepSimulationSystemGroup"/> but for prediction.
    /// The fixed update group can have a higher update frequency than the rest of the prediction, and it does not do partial ticks.</para>
    /// <para>Note: This SystemGroup is intentionally added to non-netcode worlds, to help enable single-player testing.</para>
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderFirst = true)]
    public partial class PredictedFixedStepSimulationSystemGroup : ComponentSystemGroup
    {
        private BeginFixedStepSimulationEntityCommandBufferSystem m_BeginFixedStepSimulationEntityCommandBufferSystem;
        private EndFixedStepSimulationEntityCommandBufferSystem m_EndFixedStepSimulationEntityCommandBufferSystem;

        /// <summary>
        /// Set the timestep used by this group, in seconds. The default value is 1/60 seconds.
        /// </summary>
        public float Timestep
        {
            get
            {
                return RateManager?.Timestep ?? 0f;
            }
            [Obsolete("The PredictedFixedStepSimulationSystemGroup.TimeStep setter has been deprecated and will be removed (RemovedAfter Entities 1.0)." +
                      "Please use the ClientServerTickRate.PredictedFixedStepSimulationTickRatio to set the desired rate for this group. " +
                      "Any TimeStep value set using the RateManager directly will be overwritten with the setting provided in the ClientServerTickRate", false)]
            set
            {
                if (RateManager != null) RateManager.Timestep = value;
            }
        }

        /// <summary>
        /// Set the current time step as ratio at which the this group run in respect to the simulation/prediction loop. Default value is 1,
        /// that it, the group run at the same fixed rate as the <see cref="PredictedSimulationSystemGroup"/>.
        /// </summary>
        /// <param name="tickRate">The ClientServerTickRate used for the simulation.</param>
        internal void ConfigureTimeStep(in ClientServerTickRate tickRate)
        {
            if(RateManager == null)
                return;
            tickRate.Validate();
            var fixedTimeStep = tickRate.PredictedFixedStepSimulationTimeStep;
            var rateManager = ((NetcodePredictionFixedRateManager)RateManager);
#if UNITY_EDITOR || NETCODE_DEBUG
            if (rateManager.DeprecatedTimeStep != 0f)
            {
                var timestep = RateManager.Timestep;
                if (math.distance(timestep, fixedTimeStep) > 1e-4f)
                {
                    UnityEngine.Debug.LogWarning($"The PredictedFixedStepSimulationSystemGroup.TimeStep is {timestep}ms ({math.ceil(1f/timestep)}FPS) but should be equals to ClientServerTickRate.PredictedFixedStepSimulationTimeStep: {fixedTimeStep}ms ({math.ceil(1f/fixedTimeStep)}FPS).\n" +
                                                 "The current timestep will be changed to match the ClientServerTickRate settings. You should never set the rate of this system directly with neither the PredictedFixedStepSimulationSystemGroup.TimeStep nor the RateManager.TimeStep method.\n " +
                                                 "Instead, you must always configure the desired rate by changing the ClientServerTickRate.PredictedFixedStepSimulationTickRatio property.");
                }
            }
#endif
            rateManager.SetTimeStep(tickRate.PredictedFixedStepSimulationTimeStep);
        }

        /// <summary>
        /// Default constructor which sets up a fixed rate manager.
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public PredictedFixedStepSimulationSystemGroup()
        {
            //we are passing 0 as time step so the group does not run until a proper setting is setup.
            SetRateManagerCreateAllocator(new NetcodePredictionFixedRateManager(0f));
        }
        protected override void OnCreate()
        {
            base.OnCreate();
            ((NetcodePredictionFixedRateManager)RateManager).OnCreate(this);
            m_BeginFixedStepSimulationEntityCommandBufferSystem = World.GetExistingSystemManaged<BeginFixedStepSimulationEntityCommandBufferSystem>();
            m_EndFixedStepSimulationEntityCommandBufferSystem = World.GetExistingSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }
        protected override void OnUpdate()
        {
            m_BeginFixedStepSimulationEntityCommandBufferSystem.Update();
            base.OnUpdate();
            m_EndFixedStepSimulationEntityCommandBufferSystem.Update();
        }
    }

    /// <summary>
    /// Temporary type for upgradability, to be removed before 1.0
    /// </summary>
    [Obsolete("'GhostPredictionSystemGroup' has been renamed to 'PredictedSimulationSystemGroup'. (UnityUpgradable) -> PredictedSimulationSystemGroup")]
    [DisableAutoCreation]
    public partial class GhostPredictionSystemGroup : ComponentSystemGroup
    {}
    /// <summary>
    /// Temporary type for upgradability, to be removed before 1.0
    /// </summary>
    [Obsolete("'FixedStepGhostPredictionSystemGroup' has been renamed to 'PredictedFixedStepSimulationSystemGroup'. (UnityUpgradable) -> PredictedFixedStepSimulationSystemGroup")]
    [DisableAutoCreation]
    public partial class FixedStepGhostPredictionSystemGroup : ComponentSystemGroup
    {}
}
