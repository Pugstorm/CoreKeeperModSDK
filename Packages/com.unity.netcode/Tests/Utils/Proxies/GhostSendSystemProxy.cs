using System.Collections.Generic;
using Unity.Entities;
using Unity.PerformanceTesting;
using Unity.Profiling;

namespace Unity.NetCode.Tests
{
#if UNITY_EDITOR
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(GhostSendSystem))]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class SimpleGhostSendSystemProxy : ComponentSystemGroup
    {
        static readonly ProfilerMarker k_Update = new ProfilerMarker("GhostSendSystem_OnUpdate");
        static readonly ProfilerMarker k_CompleteTrackedJobs = new ProfilerMarker("GhostSendSystem_CompleteAllTrackedJobs");

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<GhostCollection>();
            RequireForUpdate<NetworkStreamInGame>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            var ghostSendSystem = World.GetExistingSystem<GhostSendSystem>();
            var simulationSystemGroup = World.GetExistingSystemManaged<SimulationSystemGroup>();
            simulationSystemGroup.RemoveSystemFromUpdateList(ghostSendSystem);
            AddSystemToUpdateList(ghostSendSystem);
        }

        protected override void OnUpdate()
        {
            EntityManager.CompleteAllTrackedJobs();

            k_CompleteTrackedJobs.Begin();
            k_Update.Begin();
            base.OnUpdate();
            k_Update.End();
            EntityManager.CompleteAllTrackedJobs();
            k_CompleteTrackedJobs.End();
        }
    }

    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(GhostSendSystem))]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class GhostSendSystemProxy : ComponentSystemGroup
    {
        List<SampleGroup> m_GhostSampleGroups;
        readonly SampleGroup m_SerializationGroup = new SampleGroup("SpeedOfLightGroup", SampleUnit.Nanosecond);

        int m_ConnectionCount;
        bool m_IsSetup;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ConnectionCount = 0;
            RequireForUpdate<GhostCollection>();
            RequireForUpdate<NetworkStreamInGame>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            var ghostSendSystem = World.GetExistingSystem<GhostSendSystem>();
            var simulationSystemGroup = World.GetExistingSystemManaged<SimulationSystemGroup>();
            simulationSystemGroup.RemoveSystemFromUpdateList(ghostSendSystem);
            AddSystemToUpdateList(ghostSendSystem);
        }

        public void ConfigureSendSystem(NetcodeScenarioUtils.ScenarioParams parameters)
        {
            ref var ghostSendSystemData = ref SystemAPI.GetSingletonRW<GhostSendSystemData>().ValueRW;
            ghostSendSystemData.ForceSingleBaseline = parameters.GhostSystemParams.ForceSingleBaseline;
            ghostSendSystemData.ForcePreSerialize = parameters.GhostSystemParams.ForcePreSerialize;
        }

        public void SetupStats(int prefabCount, NetcodeScenarioUtils.ScenarioParams parameters)
        {
            m_ConnectionCount = parameters.NumClients;

            var capacity = 2 + (3 * prefabCount);
            m_GhostSampleGroups = new List<SampleGroup>(capacity)
            {
                new SampleGroup("Total Replicated Ghosts", SampleUnit.Undefined),
                new SampleGroup("Total Replicated Ghost Length in Bytes", SampleUnit.Byte)
            };

            var id = 0;
            for (int i = 2; i < capacity; i += 3)
            {
                m_GhostSampleGroups.Add(new SampleGroup($"GhostType[{id}] Serialized Entities", SampleUnit.Undefined));
                m_GhostSampleGroups.Add(new SampleGroup($"GhostType[{id}] Total Length in Bytes", SampleUnit.Byte));
                m_GhostSampleGroups.Add(new SampleGroup($"GhostType[{id}] Bits / Entity", SampleUnit.Byte));
                id++;
            }

            m_IsSetup = true;
        }

        protected override void OnUpdate()
        {
            var numLoadedPrefabs = SystemAPI.GetSingleton<GhostCollection>().NumLoadedPrefabs;

            var markers = new[]
            {
                "PrioritizeChunks", "GhostGroup",
                "GhostSendSystem:SerializeJob",
                "GhostSendSystem:SerializeJob (Burst)"
            };

            EntityManager.CompleteAllTrackedJobs();

            var k_MarkerName = "GhostSendSystem:SerializeJob (Burst)";
            using (var recorder = new ProfilerRecorder(new ProfilerCategory("SpeedOfLight.GhostSendSystem"),
                       k_MarkerName,
                       1,
                       ProfilerRecorderOptions.SumAllSamplesInFrame))
            {
                recorder.Reset();
                recorder.Start();
                if (m_IsSetup)
                {
                    using (Measure.ProfilerMarkers(markers))
                    {
                        using (Measure.Scope("GhostSendSystem_OnUpdate"))
                        {
                            base.OnUpdate();
                            EntityManager.CompleteAllTrackedJobs();

#if UNITY_EDITOR || NETCODE_DEBUG
                            var netStats = SystemAPI.GetSingletonRW<GhostStatsCollectionSnapshot>().ValueRW;
                            for (int worker = 1; worker < netStats.Workers; ++worker)
                            {
                                int statOffset = worker * netStats.Stride;
                                for (int i = 1; i < netStats.Size; ++i)
                                {
                                    netStats.Data[i] += netStats.Data[statOffset + i];
                                    netStats.Data[statOffset + i] = 0;
                                }
                            }

                            uint totalCount = 0;
                            uint totalLength = 0;

                            for (int i = 0; i < numLoadedPrefabs; ++i)
                            {
                                var count = netStats.Data[i * 3 + 4];
                                var length = netStats.Data[i * 3 + 5];
                                uint soloLength = 0;
                                if (count > 0)
                                    soloLength = length / count;

                                Measure.Custom(m_GhostSampleGroups[2 + 3 * i],
                                    count / m_ConnectionCount); // Serialized Entities
                                Measure.Custom(m_GhostSampleGroups[2 + 3 * i + 1],
                                    length / m_ConnectionCount / 8); // Total Length in Bytes
                                Measure.Custom(m_GhostSampleGroups[2 + 3 * i + 2], soloLength); // Bits / Entity

                                totalCount += count;
                                totalLength += length;
                            }

                            Measure.Custom(m_GhostSampleGroups[0], totalCount / m_ConnectionCount);
                            Measure.Custom(m_GhostSampleGroups[1], totalLength / m_ConnectionCount / 8);
#endif
                        }
                    }
                }
                else
                {
                    base.OnUpdate();
                    EntityManager.CompleteAllTrackedJobs();
                }

                if (m_IsSetup)
                    Measure.Custom(m_SerializationGroup, recorder.CurrentValueAsDouble / (1000 * 1000));
            }
        }
    }
#endif
}
