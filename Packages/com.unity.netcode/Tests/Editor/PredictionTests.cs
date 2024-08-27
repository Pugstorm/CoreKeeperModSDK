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

    [DisableAutoCreation]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    partial struct CheckElapsedTime : ISystem
    {
        private double ElapsedTime;
        public void OnUpdate(ref SystemState state)
        {
            var timestep = state.World.GetExistingSystemManaged<PredictedFixedStepSimulationSystemGroup>().Timestep;
            var time = SystemAPI.Time;
            if (ElapsedTime == 0.0)
            {
                ElapsedTime = time.ElapsedTime;
            }
            var totalElapsed = math.fmod(time.ElapsedTime - ElapsedTime,  timestep);
            //the elapsed time must be always an integral multiple of the time step
            Assert.LessOrEqual(totalElapsed, 1e-6);
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
                testWorld.Connect(frameTime);
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
                testWorld.Connect(frameTime);

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
                testWorld.Connect(frameTime);

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

        [TestCase(90)]
        [TestCase(82)]
        [TestCase(45)]
        public void NetcodeClientPredictionRateManager_WillWarnWhenMismatchSimulationTickRate(int fixedStepRate)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true);
                testWorld.CreateWorlds(true, 1);
                testWorld.ServerWorld.GetOrCreateSystemManaged<PredictedFixedStepSimulationSystemGroup>().RateManager.Timestep = 1f/fixedStepRate;
                testWorld.ClientWorlds[0].GetOrCreateSystemManaged<PredictedFixedStepSimulationSystemGroup>().RateManager.Timestep = 1f/fixedStepRate;

                // Connect and make sure the connection could be established
                const float frameTime = 1f / 60f;
                testWorld.Connect(frameTime);
                //Expect 2, one for server, one for the client
                LogAssert.Expect(LogType.Warning, $"The PredictedFixedStepSimulationSystemGroup.TimeStep is {1f/fixedStepRate}ms ({fixedStepRate}FPS) but should be equals to ClientServerTickRate.PredictedFixedStepSimulationTimeStep: {1f/60f}ms ({60f}FPS).\n" +
                                                  "The current timestep will be changed to match the ClientServerTickRate settings. You should never set the rate of this system directly with neither the PredictedFixedStepSimulationSystemGroup.TimeStep nor the RateManager.TimeStep method.\n " +
                                                  "Instead, you must always configure the desired rate by changing the ClientServerTickRate.PredictedFixedStepSimulationTickRatio property.");

                LogAssert.Expect(LogType.Warning, $"The PredictedFixedStepSimulationSystemGroup.TimeStep is {1f/fixedStepRate}ms ({fixedStepRate}FPS) but should be equals to ClientServerTickRate.PredictedFixedStepSimulationTimeStep: {1f/60f}ms ({60f}FPS).\n" +
                                                  "The current timestep will be changed to match the ClientServerTickRate settings. You should never set the rate of this system directly with neither the PredictedFixedStepSimulationSystemGroup.TimeStep nor the RateManager.TimeStep method.\n " +
                                                  "Instead, you must always configure the desired rate by changing the ClientServerTickRate.PredictedFixedStepSimulationTickRatio property.");

                //Check that the simulation tick rate are the same
                var clientRate = testWorld.GetSingleton<ClientServerTickRate>(testWorld.ClientWorlds[0]);
                Assert.AreEqual(60, clientRate.SimulationTickRate);
                Assert.AreEqual(1, clientRate.PredictedFixedStepSimulationTickRatio);
                var serverTimeStep = testWorld.ServerWorld.GetOrCreateSystemManaged<PredictedFixedStepSimulationSystemGroup>().Timestep;
                var clientTimestep = testWorld.ClientWorlds[0].GetOrCreateSystemManaged<PredictedFixedStepSimulationSystemGroup>().Timestep;
                Assert.That(serverTimeStep, Is.EqualTo(1f / clientRate.SimulationTickRate));
                Assert.That(clientTimestep, Is.EqualTo(1f / clientRate.SimulationTickRate));

                //Also check that if the value is overriden, it is still correctly set to the right value
                for (int i = 0; i < 8; ++i)
                {
                    testWorld.ServerWorld.GetOrCreateSystemManaged<PredictedFixedStepSimulationSystemGroup>().RateManager.Timestep = 1f/fixedStepRate;
                    testWorld.ClientWorlds[0].GetOrCreateSystemManaged<PredictedFixedStepSimulationSystemGroup>().RateManager.Timestep = 1f/fixedStepRate;
                    testWorld.Tick(1f / 60f);
                    serverTimeStep = testWorld.ServerWorld.GetOrCreateSystemManaged<PredictedFixedStepSimulationSystemGroup>().Timestep;
                    clientTimestep = testWorld.ClientWorlds[0].GetOrCreateSystemManaged<PredictedFixedStepSimulationSystemGroup>().Timestep;
                    LogAssert.Expect(LogType.Warning, $"The PredictedFixedStepSimulationSystemGroup.TimeStep is {1f/fixedStepRate}ms ({fixedStepRate}FPS) but should be equals to ClientServerTickRate.PredictedFixedStepSimulationTimeStep: {1f/60f}ms ({60f}FPS).\n" +
                                                      "The current timestep will be changed to match the ClientServerTickRate settings. You should never set the rate of this system directly with neither the PredictedFixedStepSimulationSystemGroup.TimeStep nor the RateManager.TimeStep method.\n " +
                                                      "Instead, you must always configure the desired rate by changing the ClientServerTickRate.PredictedFixedStepSimulationTickRatio property.");
                    LogAssert.Expect(LogType.Warning, $"The PredictedFixedStepSimulationSystemGroup.TimeStep is {1f/fixedStepRate}ms ({fixedStepRate}FPS) but should be equals to ClientServerTickRate.PredictedFixedStepSimulationTimeStep: {1f/60f}ms ({60f}FPS).\n" +
                                                      "The current timestep will be changed to match the ClientServerTickRate settings. You should never set the rate of this system directly with neither the PredictedFixedStepSimulationSystemGroup.TimeStep nor the RateManager.TimeStep method.\n " +
                                                      "Instead, you must always configure the desired rate by changing the ClientServerTickRate.PredictedFixedStepSimulationTickRatio property.");
                    Assert.That(clientTimestep, Is.EqualTo(1f / clientRate.SimulationTickRate));
                    Assert.That(serverTimeStep, Is.EqualTo(1f / clientRate.SimulationTickRate));
                }
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void PredictedFixedStepSimulation_ElapsedTimeReportedCorrectly(int ratio)
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.Bootstrap(true, typeof(CheckElapsedTime));
                testWorld.CreateWorlds(true, 1);
                var tickRate = testWorld.ServerWorld.EntityManager.CreateEntity(typeof(ClientServerTickRate));
                testWorld.ServerWorld.EntityManager.SetComponentData(tickRate, new ClientServerTickRate
                {
                    PredictedFixedStepSimulationTickRatio = ratio
                });
                const float frameTime = 1f / 60f;
                testWorld.Connect(frameTime);
                //Check that the simulation tick rate are the same
                var clientRate = testWorld.GetSingleton<ClientServerTickRate>(testWorld.ClientWorlds[0]);
                Assert.AreEqual(60, clientRate.SimulationTickRate);
                Assert.AreEqual(ratio, clientRate.PredictedFixedStepSimulationTickRatio);
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(1f / 60f);
                }
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(1f / 30f);
                }
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(1f / 45f);
                }
                for (int i = 0; i < 16; ++i)
                {
                    testWorld.Tick(1f / 117f);
                }
            }
        }
    }
}
