using Unity.Core;
using Unity.Entities;
using Unity.Profiling;
using Unity.Collections;
using Unity.Mathematics;

namespace Unity.NetCode
{
    unsafe class NetcodeServerRateManager : IRateManager
    {
        private EntityQuery m_NetworkTimeQuery;
        private EntityQuery m_ClientSeverTickRateQuery;
        private ProfilerMarker m_fixedUpdateMarker;
        private float m_AccumulatedTime;

        private Count m_UpdateCount;
        private int m_CurrentTickAge;
        private bool m_DidPushTime;
        DoubleRewindableAllocators* m_OldGroupAllocators = null;
        private readonly PredictedFixedStepSimulationSystemGroup m_PredictedFixedStepSimulationSystemGroup;
        private struct Count
        {
            // The total number of step the simulation should take
            public int Total;
            // The number of short steps, if for example Total is 4 and Short is 1 the update will
            // take 3 long steps followed by on short step
            public int Short;
            // The length of the long steps, if this is for example 3 the long steps should use deltaTime*3
            // while the short steps should reduce it by one and use deltaTime*2
            public int Length;
        }

        private Count GetUpdateCount(float deltaTime, float fixedTimeStep, int maxTimeSteps, int maxTimeStepLength)
        {
            m_AccumulatedTime += deltaTime;
            int updateCount = (int)(m_AccumulatedTime / fixedTimeStep);
            m_AccumulatedTime = m_AccumulatedTime % fixedTimeStep;
            int shortSteps = 0;
            int length = 1;
            if (updateCount > maxTimeSteps)
            {
                // Required length
                length = (updateCount + maxTimeSteps - 1) / maxTimeSteps;
                if (length > maxTimeStepLength)
                    length = maxTimeStepLength;
                else
                {
                    // Check how many will need to be long vs short
                    shortSteps = length * maxTimeSteps - updateCount;
                }
                updateCount = maxTimeSteps;
            }
            return new Count
            {
                Total = updateCount,
                Short = shortSteps,
                Length = length
            };
        }
        private void AdjustTargetFrameRate(int tickRate, float fixedTimeStep)
        {
            //
            // If running as headless we nudge the Application.targetFramerate back and forth
            // around the actual framerate -- always trying to have a remaining time of half a frame
            // The goal is to have the while loop above tick exactly 1 time
            //
            // The reason for using targetFramerate is to allow Unity to sleep between frames
            // reducing cpu usage on server.
            //
            int rate = tickRate;
            if (m_AccumulatedTime > 0.75f * fixedTimeStep)
                rate += 2; // higher rate means smaller deltaTime which means remaining accumulatedTime gets smaller
            else if (m_AccumulatedTime < 0.25f * fixedTimeStep)
                rate -= 2; // lower rate means bigger deltaTime which means remaining accumulatedTime gets bigger

            UnityEngine.Application.targetFrameRate = rate;
        }
        internal NetcodeServerRateManager(ComponentSystemGroup group)
        {
            // Create the queries for singletons
            m_NetworkTimeQuery = group.World.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkTime>());
            m_ClientSeverTickRateQuery = group.World.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ClientServerTickRate>());
            m_PredictedFixedStepSimulationSystemGroup = group.World.GetExistingSystemManaged<PredictedFixedStepSimulationSystemGroup>();

            m_fixedUpdateMarker = new ProfilerMarker("ServerFixedUpdate");

            var netTimeEntity = group.World.EntityManager.CreateEntity(ComponentType.ReadWrite<NetworkTime>());
            group.World.EntityManager.SetName(netTimeEntity, "NetworkTimeSingleton");
            m_NetworkTimeQuery.SetSingleton(new NetworkTime
            {
                ServerTick = new NetworkTick(0),
                ServerTickFraction = 1f,
            });
        }
        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            m_ClientSeverTickRateQuery.TryGetSingleton<ClientServerTickRate>(out var tickRate);
            tickRate.ResolveDefaults();
            if (m_DidPushTime)
            {
                group.World.PopTime();
                group.World.RestoreGroupAllocator(m_OldGroupAllocators);
                m_fixedUpdateMarker.End();
            }
            else
            {
                m_UpdateCount = GetUpdateCount(group.World.Time.DeltaTime, tickRate.SimulationFixedTimeStep, tickRate.MaxSimulationStepsPerFrame, tickRate.MaxSimulationStepBatchSize);
                m_CurrentTickAge = m_UpdateCount.Total-1;
                m_PredictedFixedStepSimulationSystemGroup.ConfigureTimeStep(tickRate);
#if UNITY_SERVER && !UNITY_EDITOR
                if (tickRate.TargetFrameRateMode != ClientServerTickRate.FrameRateMode.BusyWait)
#else
                if (tickRate.TargetFrameRateMode == ClientServerTickRate.FrameRateMode.Sleep)
#endif
                {
                    AdjustTargetFrameRate(tickRate.SimulationTickRate, tickRate.SimulationFixedTimeStep);
                }
            }
            if (m_CurrentTickAge < 0)
            {
                m_DidPushTime = false;
                return false;
            }
            if (m_CurrentTickAge == (m_UpdateCount.Short - 1))
                --m_UpdateCount.Length;
            var dt = tickRate.SimulationFixedTimeStep * m_UpdateCount.Length;
            // Check for wrap around
            ref var networkTime = ref m_NetworkTimeQuery.GetSingletonRW<NetworkTime>().ValueRW;
            var currentServerTick = networkTime.ServerTick;
            currentServerTick.Increment();
            var nextTick = currentServerTick;
            nextTick.Add((uint)(m_UpdateCount.Length - 1));
            networkTime.ServerTick = nextTick;
            networkTime.InterpolationTick = networkTime.ServerTick;
            networkTime.SimulationStepBatchSize = m_UpdateCount.Length;
            if (m_CurrentTickAge == 0)
                networkTime.Flags &= ~NetworkTimeFlags.IsCatchUpTick;
            else
                networkTime.Flags |= NetworkTimeFlags.IsCatchUpTick;
            networkTime.ElapsedNetworkTime += dt;
            group.World.PushTime(new TimeData(networkTime.ElapsedNetworkTime, dt));
            m_DidPushTime = true;
            m_OldGroupAllocators = group.World.CurrentGroupAllocators;
            group.World.SetGroupAllocator(group.RateGroupAllocators);
            --m_CurrentTickAge;
            m_fixedUpdateMarker.Begin();
            return true;
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

    /// <summary>
    /// Update the <see cref="SimulationSystemGroup"/> of a client world from another world (usually the default world)
    /// Used only for DOTSRuntime and tests or other specific use cases.
    /// </summary>
#if !UNITY_CLIENT || UNITY_SERVER || UNITY_EDITOR
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    internal partial class TickServerSimulationSystem : TickComponentSystemGroup
    {
    }
#endif
}
