using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Unity.NetCode.Tests
{
#if UNITY_EDITOR
    public class NetcodeScenarioUtils
    {
        public struct ScenarioDesc
        {
            public float FrameTime;
            public GameObject[] GhostPrefabs;
            public Type[] Systems;
            public Type[] GhostComponentForVerification;
        }

        public struct ScenarioParams
        {
            public struct GhostSystemParameters
            {
                public int MinSendImportance;
                public int MinDistanceScaledSendImportance;
                public int MaxSendChunks;
                public int MaxSendEntities;
                public bool ForceSingleBaseline;
                public bool ForcePreSerialize;
            }

            public GhostSystemParameters GhostSystemParams;
            public int NumClients;
            public int WarmupFrames;
            public int DurationInFrames;
            public bool UseThinClients;
            public bool SetCommandTarget;

            public int[] SpawnCount;
        }

        public static void ExecuteScenario(ScenarioDesc scenario, ScenarioParams parameters)
        {
            using (var scenarioWorld = new NetCodeTestWorld())
            {
                var hasProxy = false;
                foreach (var system in scenario.Systems)
                {
                    if (system == typeof(GhostSendSystemProxy))
                    {
                        hasProxy = true;
                        break;
                    }
                }
                if (!hasProxy)
                {
                    Type[] systems = new Type[scenario.Systems.Length + 1];
                    scenario.Systems.CopyTo(systems, 0);
                    systems[scenario.Systems.Length] = typeof(GhostSendSystemProxy);
                    scenarioWorld.Bootstrap(true, systems);
                }
                else
                    scenarioWorld.Bootstrap(true, scenario.Systems);

                var frameTime = scenario.FrameTime;

                Assert.IsTrue(scenarioWorld.CreateGhostCollection(scenario.GhostPrefabs));

                // create worlds, spawn and connect
                scenarioWorld.CreateWorlds(true, parameters.NumClients, parameters.UseThinClients);

                scenarioWorld.Connect(frameTime);

                var ghostSendProxy = scenarioWorld.ServerWorld.GetOrCreateSystemManaged<GhostSendSystemProxy>();
                // ForcePreSerialize must be set before going in-game or it will not be applied
                ghostSendProxy.ConfigureSendSystem(parameters);

                // start simulation
                scenarioWorld.GoInGame();

                // instantiate

                var type = ComponentType.ReadOnly<NetworkId>();
                var connections = scenarioWorld.ServerWorld.EntityManager.CreateEntityQuery(type).ToEntityArray(Allocator.Temp);
                Assert.IsTrue(connections.Length == parameters.NumClients);
                Assert.IsTrue(scenario.GhostPrefabs.Length == parameters.SpawnCount.Length);

                var collectionEnt = scenarioWorld.TryGetSingletonEntity<NetCodeTestPrefabCollection>(scenarioWorld.ServerWorld);

                for (int i = 0; i < scenario.GhostPrefabs.Length; i++)
                {
                    if (parameters.SpawnCount[i] == 0)
                        continue;

                    var collection = scenarioWorld.ServerWorld.EntityManager.GetBuffer<NetCodeTestPrefab>(collectionEnt);
                    var prefabs = scenarioWorld.ServerWorld.EntityManager.Instantiate(collection[i].Value, parameters.SpawnCount[i], Allocator.Temp);

                    if (scenarioWorld.ServerWorld.EntityManager.HasComponent<GhostOwner>(prefabs[0]))
                    {
                        Assert.IsTrue(prefabs.Length == parameters.NumClients);

                        for (int j = 0; j < connections.Length; ++j)
                        {
                            var networkComponent = scenarioWorld.ServerWorld.EntityManager.GetComponentData<NetworkId>(connections[j]);

                            scenarioWorld.ServerWorld.EntityManager.SetComponentData(prefabs[j], new GhostOwner { NetworkId = networkComponent.Value });
                            if (parameters.SetCommandTarget)
                                scenarioWorld.ServerWorld.EntityManager.SetComponentData(connections[j], new CommandTarget {targetEntity = prefabs[j]});
                        }
                    }

                    Assert.IsTrue(prefabs != default);
                    Assert.IsTrue(prefabs.Length == parameters.SpawnCount[i]);
                }
                connections.Dispose();

                // warmup
                for (int i = 0; i < parameters.WarmupFrames; ++i)
                    scenarioWorld.Tick(frameTime);

                // run simulation
                ghostSendProxy.SetupStats(scenario.GhostPrefabs.Length, parameters);

                for (int i = 0; i < parameters.DurationInFrames; ++i)
                    scenarioWorld.Tick(frameTime);

                for (int i = 0; i < scenario.GhostComponentForVerification?.Length; i++)
                {
                    using var query = scenarioWorld.ServerWorld.EntityManager.CreateEntityQuery(
                        scenario.GhostComponentForVerification[i]);
                    Assert.IsTrue(parameters.SpawnCount[i] == query.CalculateEntityCount());
                }
            }
        }
    }
#endif
}
