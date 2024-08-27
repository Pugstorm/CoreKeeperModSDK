using NUnit.Framework;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.NetCode.Tests;
using Unity.Jobs;
using UnityEngine;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

namespace Unity.NetCode.Tests
{
    public class GhostExtrapolationConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent(entity, new TestExtrapolated());
        }
    }

    public struct TestExtrapolated : IComponentData
    {
        [GhostField(Smoothing=SmoothingAction.InterpolateAndExtrapolate, MaxSmoothingDistance=75)]
        public float Value;
    }
    public struct ExtrapolateBackup : IComponentData
    {
        public float Value;
        public NetworkTick Tick;
        public float Fraction;
    }
    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class MoveExtrapolated : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref TestExtrapolated val) => {
                if (val.Value > 100 && val.Value < 150)
                    val.Value += 100;
                else
                    val.Value += 1;
            }).Run();
        }
    }
    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class CheckInterpolationDistance : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.WithoutBurst().ForEach((ref TestExtrapolated val) => {
                Assert.IsTrue(val.Value < 115 || val.Value > 200);
            }).Run();
        }
    }
    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class CheckExtrapolate : SystemBase
    {
        protected override void OnUpdate()
        {
            var InterpolTick = SystemAPI.GetSingleton<NetworkTime>().InterpolationTick;
            var InterpolFraction = SystemAPI.GetSingleton<NetworkTime>().InterpolationTickFraction;
            Entities.WithoutBurst().ForEach((ref TestExtrapolated val, ref ExtrapolateBackup bkup) => {
                if (bkup.Tick == InterpolTick && bkup.Fraction == InterpolFraction)
                    return;
                Assert.Greater(val.Value, bkup.Value);
                bkup.Value = val.Value;
                bkup.Tick = InterpolTick;
                bkup.Fraction = InterpolFraction;
            }).Run();
        }
    }
    public class ExtrapolationTests
    {
        [Test]
        public void MaxSmoothingDistanceIsUsed()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(MoveExtrapolated), typeof(CheckInterpolationDistance));

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostExtrapolationConverter();
                ghostGameObject.AddComponent<GhostAuthoringComponent>();
                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);
                var tickRate = testWorld.ServerWorld.EntityManager.CreateEntity();
                // Set low net tick rate to make sure interpolation is used
                testWorld.ServerWorld.EntityManager.AddComponentData(tickRate, new ClientServerTickRate {NetworkTickRate = 30});

                testWorld.SpawnOnServer(ghostGameObject);

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Takes longer to replicate a ghost because we only replicate snapshots every other frame
                // (due to NetworkTickRate = 30 + GhostSendSystem.m_ConnectionsToProcess == 0).
                for (int i = 0; i < 9; ++i)
                    testWorld.Tick(frameTime);

                var clientEnt = testWorld.TryGetSingletonEntity<TestExtrapolated>(testWorld.ClientWorlds[0]);
                Assert.AreNotEqual(Entity.Null, clientEnt);

                Assert.Less(testWorld.ClientWorlds[0].EntityManager.GetComponentData<TestExtrapolated>(clientEnt).Value, 100);
                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 256; ++i)
                    testWorld.Tick(frameTime);

                Assert.Greater(testWorld.ClientWorlds[0].EntityManager.GetComponentData<TestExtrapolated>(clientEnt).Value, 200);
            }
        }
        [Test]
        public void ExtrapolationProduceSmoothValues()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(MoveExtrapolated), typeof(CheckExtrapolate));

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new GhostExtrapolationConverter();
                ghostGameObject.AddComponent<GhostAuthoringComponent>();
                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                var tickRate = testWorld.ServerWorld.EntityManager.CreateEntity();
                // Set low net tick rate to make sure interpolation is used
                testWorld.ServerWorld.EntityManager.AddComponentData(tickRate, new ClientServerTickRate {NetworkTickRate = 1});

                var clientTickRate = testWorld.ClientWorlds[0].EntityManager.CreateEntity();
                // Disable interpolation time to make sure extrapolation is used
                var tr = NetworkTimeSystem.DefaultClientTickRate;
                tr.InterpolationTimeNetTicks = 0;
                tr.MaxExtrapolationTimeSimTicks = 120;
                testWorld.ClientWorlds[0].EntityManager.AddComponentData(clientTickRate, tr);

                testWorld.SpawnOnServer(ghostGameObject);

                float frameTime = 1.0f / 60.0f;
                // Connect and make sure the connection could be established
                testWorld.Connect(frameTime);

                // Go in-game
                testWorld.GoInGame();

                // Let the simulation run for a bit since extrapolation requires two snapshots to be received before it does anything
                for (int i = 0; i < 256; ++i)
                    testWorld.Tick(frameTime);

                // Enable the checks
                var clientEnt = testWorld.TryGetSingletonEntity<TestExtrapolated>(testWorld.ClientWorlds[0]);
                Assert.AreNotEqual(Entity.Null, clientEnt);
                testWorld.ClientWorlds[0].EntityManager.AddComponentData(clientEnt, new ExtrapolateBackup {Value = 0});

                // Let the game run for a bit more and verify that they are extrapolated
                for (int i = 0; i < 256; ++i)
                    testWorld.Tick(frameTime);
            }
        }
    }
}
