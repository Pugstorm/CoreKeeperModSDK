using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.TestTools;


namespace Unity.NetCode.Tests
{
    //FIXME this will break serialization. It is non handled and must be documented
    [GhostEnabledBit]
    struct BufferWithReplicatedEnableBits: IBufferElementData, IEnableableComponent
    {
        public byte value;
    }

    //Added to the ISystem state entity, track the number of time a system update has been called
    struct SystemExecutionCounter : IComponentData
    {
        public int value;
    }
    public class PredictionTestConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            //Transform is replicated, Owner is replicated (components with sizes)
            baker.AddComponent(entity, new GhostOwner());
            //Buffer with enable bits, replicated
            //TODO: missing: Buffer with enable bits, no replicated fields. This break serialization
            //baker.AddBuffer<BufferWithReplicatedEnableBits>().ResizeUninitialized(3);
            baker.AddBuffer<EnableableBuffer>(entity).ResizeUninitialized(3);
            //Empty enable flags
            baker.AddComponent<EnableableFlagComponent>(entity);
            //Non empty enable flags
            baker.AddComponent(entity, new ReplicatedEnableableComponentWithNonReplicatedField{value = 9999});
        }
    }
    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class PredictionTestPredictionSystem : SystemBase
    {
        public static bool s_IsEnabled;
        protected override void OnUpdate()
        {
            if (!s_IsEnabled)
                return;
            var deltaTime = SystemAPI.Time.DeltaTime;

            Entities.WithAll<Simulate, GhostInstance>().ForEach((ref LocalTransform trans) => {
                // Make sure we advance by one unit per tick, makes it easier to debug the values
                trans.Position.x += deltaTime * 60.0f;
            }).ScheduleParallel();
        }
    }
    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(GhostUpdateSystem))]
    [UpdateBefore(typeof(GhostReceiveSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class InvalidateAllGhostDataBeforeUpdate : SystemBase
    {
        protected override void OnCreate()
        {
            EntityManager.AddComponent<SystemExecutionCounter>(SystemHandle);
        }

        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var tick = networkTime.ServerTick;
            if(!tick.IsValid)
                return;
            //Do not invalidate full ticks. The backup is not restored in that case
            if(!networkTime.IsPartialTick)
                return;
            Entities
                .WithoutBurst()
                .WithAll<GhostInstance>().ForEach((
                    Entity ent,
                    ref LocalTransform trans,
                    ref DynamicBuffer<EnableableBuffer> buffer,
                    //ref DynamicBuffer<BufferWithReplicatedEnableBits> nonReplicatedBuffer,
                    ref ReplicatedEnableableComponentWithNonReplicatedField comp) =>
            {
                for (int el = 0; el < buffer.Length; ++el)
                    buffer[el] = new EnableableBuffer { value = 100*(int)tick.SerializedData };

                // for (int el = 0; el < nonReplicatedBuffer.Length; ++el)
                //     nonReplicatedBuffer[el] = new BufferWithReplicatedEnableBits { value = (byte)tick.SerializedData };

                trans.Position = new float3(-10 * tick.SerializedData, -10 * tick.SerializedData, -10 * tick.SerializedData);
                trans.Scale = -10f*tick.SerializedData;
                comp.value = -10*(int)tick.SerializedData;
                EntityManager.SetComponentEnabled<ReplicatedEnableableComponentWithNonReplicatedField>(ent, false);
                EntityManager.SetComponentEnabled<EnableableFlagComponent>(ent, false);
            }).Run();
            var counter = SystemAPI.GetComponentRW<SystemExecutionCounter>(SystemHandle);
            ++counter.ValueRW.value;
        }
    }
    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [UpdateAfter(typeof(GhostUpdateSystem))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class CheckRestoreFromBackupIsCorrect : SystemBase
    {
        protected override void OnCreate()
        {
            EntityManager.AddComponent<SystemExecutionCounter>(SystemHandle);
        }

        protected override void OnUpdate()
        {
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            if(!tick.IsValid)
                return;
            Entities
                .WithoutBurst()
                .WithAll<Simulate, GhostInstance>().ForEach((
                    Entity ent,
                    ref LocalTransform trans,
                    ref DynamicBuffer<EnableableBuffer> buffer,
                    ref ReplicatedEnableableComponentWithNonReplicatedField comp) =>
                {
                    Assert.IsTrue(trans.Position.x > 0f);
                    Assert.IsTrue(trans.Position.y > 0f);
                    Assert.IsTrue(trans.Position.z > 0f);
                    Assert.IsTrue(math.abs(1f - trans.Scale) < 1e-4f);

                    //enable bits must be replicated
                    Assert.IsTrue(EntityManager.IsComponentEnabled<ReplicatedEnableableComponentWithNonReplicatedField>(ent));
                    Assert.IsTrue(EntityManager.IsComponentEnabled<EnableableFlagComponent>(ent));
                    //This component is not replicated. As such its values is never restored.
                    Assert.AreEqual(-10*(int)tick.SerializedData, comp.value);
                    for (int el = 0; el < buffer.Length; ++el)
                         Assert.AreEqual(1000 * (el+1), buffer[el].value);
                }).Run();
            var counter = SystemAPI.GetComponentRW<SystemExecutionCounter>(SystemHandle);
            ++counter.ValueRW.value;
        }
    }

    public class PredictionTests
    {
        [TestCase((uint)0x229321)]
        [TestCase((uint)100)]
        [TestCase((uint)0x7FFF011F)]
        [TestCase((uint)0x7FFFFF00)]
        [TestCase((uint)0x7FFFFFF0)]
        [TestCase((uint)0x7FFFF1F0)]
        public void PredictionTickEvolveCorrectly(uint serverTickData)
        {
            var serverTick = new NetworkTick(serverTickData);
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(PredictionTestPredictionSystem));
                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new PredictionTestConverter();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostConfig.DefaultGhostMode = GhostMode.Predicted;
                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));
                testWorld.CreateWorlds(true, 1);
                testWorld.SetServerTick(serverTick);
                Assert.IsTrue(testWorld.Connect(frameTime, 4));
                testWorld.GoInGame();
                var serverEnt = testWorld.SpawnOnServer(0);
                Assert.AreNotEqual(Entity.Null, serverEnt);
                for(int i=0;i<256;++i)
                    testWorld.Tick(1.0f/60f);
            }
        }

        const float frameTime = 1.0f / 60.0f;
        [Test]
        public void PartialPredictionTicksAreRolledBack()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(PredictionTestPredictionSystem));
                PredictionTestPredictionSystem.s_IsEnabled = true;

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new PredictionTestConverter();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostConfig.DefaultGhostMode = GhostMode.Predicted;

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                var serverEnt = testWorld.SpawnOnServer(ghostGameObject);
                Assert.AreNotEqual(Entity.Null, serverEnt);
                var buffer = testWorld.ServerWorld.EntityManager.GetBuffer<EnableableBuffer>(serverEnt);
                for (int i = 0; i < buffer.Length; ++i)
                    buffer[i] = new EnableableBuffer { value = 1000 * (i + 1) };
                // var nonReplicatedBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<BufferWithReplicatedEnableBits>(serverEnt);
                // for (int i = 0; i < nonReplicatedBuffer.Length; ++i)
                //     nonReplicatedBuffer[i] = new BufferWithReplicatedEnableBits { value = (byte)(10 * (i + 1)) };

                // Connect and make sure the connection could be established
                Assert.IsTrue(testWorld.Connect(frameTime, 4));

                // Go in-game
                testWorld.GoInGame();

                // Let the game run for a bit so the ghosts are spawned on the client
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                var clientEnt = testWorld.TryGetSingletonEntity<GhostOwner>(testWorld.ClientWorlds[0]);
                Assert.AreNotEqual(Entity.Null, clientEnt);

                var prevServer = testWorld.ServerWorld.EntityManager.GetComponentData<LocalTransform>(serverEnt).Position;
                var prevClient = testWorld.ClientWorlds[0].EntityManager.GetComponentData<LocalTransform>(clientEnt).Position;

                for (int i = 0; i < 64; ++i)
                {
                    testWorld.Tick(frameTime / 4);

                    var curServer = testWorld.ServerWorld.EntityManager.GetComponentData<LocalTransform>(serverEnt);
                    var curClient = testWorld.ClientWorlds[0].EntityManager.GetComponentData<LocalTransform>(clientEnt);
                    testWorld.ServerWorld.EntityManager.CompleteAllTrackedJobs();
                    // Server does not do fractional ticks so it will not advance the position every frame
                    Assert.GreaterOrEqual(curServer.Position.x, prevServer.x);
                    testWorld.ClientWorlds[0].EntityManager.CompleteAllTrackedJobs();
                    // Client does fractional ticks and position should be always increasing
                    Assert.Greater(curClient.Position.x, prevClient.x);
                    prevServer = curServer.Position;
                    prevClient = curClient.Position;
                }
                // Stop updating, let it run for a while and check that they ended on the same value
                PredictionTestPredictionSystem.s_IsEnabled = false;
                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(frameTime);

                prevServer = testWorld.ServerWorld.EntityManager.GetComponentData<LocalTransform>(serverEnt).Position;
                prevClient = testWorld.ClientWorlds[0].EntityManager.GetComponentData<LocalTransform>(clientEnt).Position;
                Assert.IsTrue(math.distance(prevServer, prevClient) < 0.01);
            }
        }

        [TestCase(1)]
        [TestCase(20)]
        [TestCase(30)]
        [TestCase(40)]
        public void HistoryBufferIsRollbackCorrectly(int ghostCount)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true,
                    typeof(PredictionTestPredictionSystem),
                    typeof(InvalidateAllGhostDataBeforeUpdate),
                    typeof(CheckRestoreFromBackupIsCorrect));

                var ghostGameObject = new GameObject();
                ghostGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new PredictionTestConverter();
                var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
                ghostConfig.DefaultGhostMode = GhostMode.Predicted;

                Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

                testWorld.CreateWorlds(true, 1);

                for (int i = 0; i < ghostCount; ++i)
                {
                    var serverEnt = testWorld.SpawnOnServer(ghostGameObject);
                    var buffer = testWorld.ServerWorld.EntityManager.GetBuffer<EnableableBuffer>(serverEnt);
                    for (int el = 0; el < buffer.Length; ++el)
                        buffer[el] = new EnableableBuffer { value = 1000 * (el+ 1) };
                    testWorld.ServerWorld.EntityManager.SetComponentData(serverEnt, LocalTransform.FromPosition(new float3(0f, 10f, 100f)));
                    // var nonReplicatedBuffer = testWorld.ServerWorld.EntityManager.GetBuffer<BufferWithReplicatedEnableBits>(serverEnt);
                    // for (int el = 0; el < nonReplicatedBuffer.Length; ++el)
                    //     nonReplicatedBuffer[el] = new BufferWithReplicatedEnableBits { value = (byte)(10 * (el + 1)) };
                }
                // Connect and make sure the connection could be established
                Assert.IsTrue(testWorld.Connect(frameTime, 4));

                // Go in-game
                testWorld.GoInGame();

                PredictionTestPredictionSystem.s_IsEnabled = true;
                for (int i = 0; i < 64; ++i)
                {
                    testWorld.Tick(frameTime / 4);
                }
                testWorld.ClientWorlds[0].EntityManager.CompleteAllTrackedJobs();
                PredictionTestPredictionSystem.s_IsEnabled = false;
                var counter1 = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SystemExecutionCounter>(
                        testWorld.ClientWorlds[0].GetExistingSystem<InvalidateAllGhostDataBeforeUpdate>());
                var counter2 = testWorld.ClientWorlds[0].EntityManager.GetComponentData<SystemExecutionCounter>(
                    testWorld.ClientWorlds[0].GetExistingSystem<InvalidateAllGhostDataBeforeUpdate>());
                Assert.Greater(counter1.value, 0);
                Assert.Greater(counter2.value, 0);
                Assert.AreEqual(counter1.value, counter2.value);
            }
        }

        [TestCase(120)]
        [TestCase(90)]
        [TestCase(82)]
        [TestCase(45)]
        public void NetcodeClientPredictionRateManager_WillWarnWhenMismatchSimulationTickRate(int simulationTickRate)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                SetupPredictionAndTickRate(simulationTickRate, testWorld);

                LogAssert.Expect(LogType.Warning, $"1 / {nameof(PredictedFixedStepSimulationSystemGroup)}.{nameof(ComponentSystemGroup.RateManager)}.{nameof(IRateManager.Timestep)}(ms): {60}(FPS) " +
                                               $"must be an integer multiple of {nameof(ClientServerTickRate)}.{nameof(ClientServerTickRate.SimulationTickRate)}:{simulationTickRate}(FPS).\n" +
                                               $"Timestep will default to 1 / SimulationTickRate: {1f / simulationTickRate} to fix this issue for now.");
                var timestep = testWorld.ClientWorlds[0].GetOrCreateSystemManaged<PredictedFixedStepSimulationSystemGroup>().RateManager.Timestep;
                Assert.That(timestep, Is.EqualTo(1f / simulationTickRate));
            }
        }

        [TestCase(30)]
        [TestCase(20)]
        public void NetcodeClientPredictionRateManager_WillNotWarnWhenMatchingSimulationTickRate(int simulationTickRate)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                SetupPredictionAndTickRate(simulationTickRate, testWorld);

                LogAssert.Expect(LogType.Warning,
                    @"Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Unity.NetCode.NetworkTimeSystem targeting Unity.Entities.UpdateWorldTimeSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.InitializationSystemGroup))],
or by manually adding both systems to the same group's update list.");
                LogAssert.NoUnexpectedReceived();
                var timestep = testWorld.ClientWorlds[0].GetOrCreateSystemManaged<PredictedFixedStepSimulationSystemGroup>().RateManager.Timestep;
                Assert.That(timestep, Is.EqualTo(1f / 60f));
            }
        }

        static void SetupPredictionAndTickRate(int simulationTickRate, NetCodeTestWorld testWorld)
        {
            testWorld.Bootstrap(true);

            var ghostGameObject = new GameObject();
            var ghostConfig = ghostGameObject.AddComponent<GhostAuthoringComponent>();
            ghostConfig.DefaultGhostMode = GhostMode.Predicted;

            Assert.IsTrue(testWorld.CreateGhostCollection(ghostGameObject));

            testWorld.CreateWorlds(true, 1);

            var serverEnt = testWorld.SpawnOnServer(ghostGameObject);
            var ent = testWorld.ServerWorld.EntityManager.CreateEntity();
            testWorld.ServerWorld.EntityManager.AddComponentData(ent, new ClientServerTickRate
            {
                SimulationTickRate = simulationTickRate,
            });
            Assert.AreNotEqual(Entity.Null, serverEnt);

            // Connect and make sure the connection could be established
            Assert.IsTrue(testWorld.Connect(frameTime, 8));

            // Go in-game
            testWorld.GoInGame();

            // Let the game run for a bit so the ghosts are spawned on the client
            for (int i = 0; i < 16; ++i)
                testWorld.Tick(frameTime);
        }
    }
}
