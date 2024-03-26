using NUnit.Framework;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.NetCode.Tests;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.TestTools;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Physics;

namespace Unity.NetCode.Physics.Tests
{
    public class LagCompensationTestPlayerConverter : TestNetCodeAuthoring.IConverter
    {
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddBuffer<LagCompensationTestCommand>(entity);
            baker.AddComponent(entity, new CommandDataInterpolationDelay());
            baker.AddComponent(entity, new LagCompensationTestPlayer());
            baker.AddComponent(entity, new GhostOwner());
        }
    }

    public struct LagCompensationTestPlayer : IComponentData
    {
    }

    [NetCodeDisableCommandCodeGen]
    public struct LagCompensationTestCommand : ICommandData, ICommandDataSerializer<LagCompensationTestCommand>
    {
        public NetworkTick Tick {get; set;}
        public float3 origin;
        public float3 direction;
        public NetworkTick lastFire;

        public void Serialize(ref DataStreamWriter writer, in RpcSerializerState state, in LagCompensationTestCommand data)
        {
            writer.WriteFloat(data.origin.x);
            writer.WriteFloat(data.origin.y);
            writer.WriteFloat(data.origin.z);
            writer.WriteFloat(data.direction.x);
            writer.WriteFloat(data.direction.y);
            writer.WriteFloat(data.direction.z);
            writer.WriteUInt(data.lastFire.SerializedData);
        }
        public void Serialize(ref DataStreamWriter writer, in RpcSerializerState state, in LagCompensationTestCommand data, in LagCompensationTestCommand baseline, StreamCompressionModel model)
        {
            Serialize(ref writer, state, data);
        }
        public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state, ref LagCompensationTestCommand data)
        {
            data.origin.x = reader.ReadFloat();
            data.origin.y = reader.ReadFloat();
            data.origin.z = reader.ReadFloat();
            data.direction.x = reader.ReadFloat();
            data.direction.y = reader.ReadFloat();
            data.direction.z = reader.ReadFloat();
            data.lastFire = new NetworkTick{SerializedData = reader.ReadUInt()};
        }
        public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state, ref LagCompensationTestCommand data, in LagCompensationTestCommand baseline, StreamCompressionModel model)
        {
            Deserialize(ref reader, state, ref data);
        }
    }
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation|WorldSystemFilterFlags.ServerSimulation)]
    public partial class TestAutoInGameSystem : SystemBase
    {
        BeginSimulationEntityCommandBufferSystem m_BeginSimulationCommandBufferSystem;
        EntityQuery m_PlayerPrefabQuery;
        EntityQuery m_CubePrefabQuery;
        protected override void OnCreate()
        {
            m_BeginSimulationCommandBufferSystem = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
            m_PlayerPrefabQuery = GetEntityQuery(ComponentType.ReadOnly<Prefab>(), ComponentType.ReadOnly<GhostInstance>(), ComponentType.ReadOnly<LagCompensationTestPlayer>());
            m_CubePrefabQuery = GetEntityQuery(ComponentType.ReadOnly<Prefab>(), ComponentType.ReadOnly<GhostInstance>(), ComponentType.Exclude<LagCompensationTestPlayer>());
        }
        protected override void OnUpdate()
        {
            var commandBuffer = m_BeginSimulationCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();

            bool isServer = World.IsServer();
            var playerPrefab = m_PlayerPrefabQuery.ToEntityArray(Allocator.Temp)[0];
            var cubePrefab = m_CubePrefabQuery.ToEntityArray(Allocator.Temp)[0];
            Entities.WithNone<NetworkStreamInGame>().WithoutBurst().ForEach((int entityInQueryIndex, Entity ent, in NetworkId id) =>
            {
                commandBuffer.AddComponent(entityInQueryIndex, ent, new NetworkStreamInGame());
                if (isServer)
                {
                    // Spawn the player so it gets replicated to the client
                    // Spawn the cube when a player connects for simplicity
                    commandBuffer.Instantiate(entityInQueryIndex, cubePrefab);
                    var player = commandBuffer.Instantiate(entityInQueryIndex, playerPrefab);
                    commandBuffer.SetComponent(entityInQueryIndex, player, new GhostOwner{NetworkId = id.Value});
                    commandBuffer.SetComponent(entityInQueryIndex, ent, new CommandTarget{targetEntity = player});
                }
            }).Schedule();
            m_BeginSimulationCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
    [DisableAutoCreation]
    [UpdateInGroup(typeof(CommandSendSystemGroup))]
    [BurstCompile]
    public partial struct LagCompensationTestCommandCommandSendSystem : ISystem
    {
        CommandSendSystem<LagCompensationTestCommand, LagCompensationTestCommand> m_CommandSend;
        [BurstCompile]
        struct SendJob : IJobChunk
        {
            public CommandSendSystem<LagCompensationTestCommand, LagCompensationTestCommand>.SendJobData data;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                data.Execute(chunk, unfilteredChunkIndex);
            }
        }
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_CommandSend.OnCreate(ref state);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!m_CommandSend.ShouldRunCommandJob(ref state))
                return;
            var sendJob = new SendJob{data = m_CommandSend.InitJobData(ref state)};
            state.Dependency = sendJob.Schedule(m_CommandSend.Query, state.Dependency);
        }
    }
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(CommandReceiveSystemGroup))]
    [BurstCompile]
    public partial struct LagCompensationTestCommandCommandReceiveSystem : ISystem
    {
        CommandReceiveSystem<LagCompensationTestCommand, LagCompensationTestCommand> m_CommandRecv;
        [BurstCompile]
        struct ReceiveJob : IJobChunk
        {
            public CommandReceiveSystem<LagCompensationTestCommand, LagCompensationTestCommand>.ReceiveJobData data;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                data.Execute(chunk, unfilteredChunkIndex);
            }
        }
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_CommandRecv.OnCreate(ref state);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var recvJob = new ReceiveJob{data = m_CommandRecv.InitJobData(ref state)};
            state.Dependency = recvJob.Schedule(m_CommandRecv.Query, state.Dependency);
        }
    }

    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class LagCompensationTestCubeMoveSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.WithNone<LagCompensationTestPlayer>().WithAll<GhostInstance>().ForEach((ref LocalTransform trans) => {
                trans.Position.x += 0.1f;
                if (trans.Position.x > 100)
                    trans.Position.x -= 200;
            }).ScheduleParallel();
        }
    }

    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation|WorldSystemFilterFlags.ServerSimulation)]
    public partial class LagCompensationTestHitScanSystem : SystemBase
    {
        public static int HitStatus = 0;
        public static bool EnableLagCompensation = true;
        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            // Do not perform hit-scan when rolling back, only when simulating the latest tick
            if (!networkTime.IsFirstTimeFullyPredictingTick)
                return;
            var collisionHistory = SystemAPI.GetSingleton<PhysicsWorldHistorySingleton>();
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var predictingTick = networkTime.ServerTick;
            var isServer = World.IsServer();
            // Not using burst since there is a static used to update the UI
            Dependency = Entities
                .WithoutBurst()
                .WithReadOnly(physicsWorld)
                .ForEach((DynamicBuffer<LagCompensationTestCommand> commands, in CommandDataInterpolationDelay delay) =>
            {
                // If there is no data for the tick or a fire was not requested - do not process anything
                if (!commands.GetDataAtTick(predictingTick, out var cmd))
                    return;
                if (cmd.lastFire != predictingTick)
                    return;
                var interpolDelay = EnableLagCompensation ? delay.Delay : 0;

                // Get the collision world to use given the tick currently being predicted and the interpolation delay for the connection
                collisionHistory.GetCollisionWorldFromTick(predictingTick, interpolDelay, ref physicsWorld, out var collWorld);
                var rayInput = new Unity.Physics.RaycastInput();
                rayInput.Start = cmd.origin;
                rayInput.End = cmd.origin + cmd.direction * 100;
                rayInput.Filter = Unity.Physics.CollisionFilter.Default;
                bool hit = collWorld.CastRay(rayInput);
                Debug.Log($"LagCompensationTest result on {(isServer ? "SERVER" : "CLIENT")} is {hit} ({cmd.Tick})");
                if (hit)
                {
                    HitStatus |= isServer?1:2;
                }
            }).Schedule(Dependency);
        }
    }
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    [AlwaysSynchronizeSystem]
    [DisableAutoCreation]
    public partial class LagCompensationTestCommandSystem : SystemBase
    {
        public static float3 Target;
        protected override void OnCreate()
        {
            RequireForUpdate<CommandTarget>();
        }
        protected override void OnUpdate()
        {
            var target = SystemAPI.GetSingleton<CommandTarget>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            if (target.targetEntity == Entity.Null)
            {
                foreach (var (ghost, entity) in SystemAPI.Query<RefRO<PredictedGhost>>().WithEntityAccess().WithAll<LagCompensationTestPlayer>())
                {
                    target.targetEntity = entity;
                    SystemAPI.SetSingleton(target);
                }
            }
            if (target.targetEntity == Entity.Null || !networkTime.ServerTick.IsValid || !EntityManager.HasComponent<LagCompensationTestCommand>(target.targetEntity))
                return;

            var buffer = EntityManager.GetBuffer<LagCompensationTestCommand>(target.targetEntity);
            var cmd = default(LagCompensationTestCommand);
            cmd.Tick = networkTime.ServerTick;
            if (math.any(Target != default))
            {
                Entities.WithoutBurst().WithNone<PredictedGhost>().WithAll<GhostInstance>().ForEach((in LocalTransform trans) => {
                    var offset = new float3(0,0,-10);
                    cmd.origin = trans.Position + offset;
                    cmd.direction = Target - offset;
                    cmd.lastFire = cmd.Tick;
                }).Run();

                // If too close to an edge, wait a bit
                if (cmd.origin.x < -90 || cmd.origin.x > 90)
                {
                    buffer.AddCommandData(new LagCompensationTestCommand{Tick = cmd.Tick});
                    return;
                }
                Target = default;

            }
            // Not firing and data for the tick already exists, skip it to make sure a fiew command is not overwritten
            else if (buffer.GetDataAtTick(cmd.Tick, out var dupCmd) && dupCmd.Tick == cmd.Tick)
                return;
            buffer.AddCommandData(cmd);
        }
    }

    public class LagCompensationTests
    {
        [Test]
        public void LagCompensationDoesNotUpdateIfLagCompensationConfigIsNotPresent()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                // Test lag compensation with 100ms ping
                testWorld.DriverSimulatedDelay = 50;
                testWorld.TestSpecificAdditionalAssemblies.Add("Unity.NetCode.Physics,");
                testWorld.TestSpecificAdditionalAssemblies.Add("Unity.Physics,");
                testWorld.Bootstrap(true);

                testWorld.CreateWorlds(true, 1, false);
                Assert.IsFalse(testWorld.TryGetSingletonEntity<LagCompensationConfig>(testWorld.ServerWorld) != Entity.Null);
                Assert.IsFalse(testWorld.TryGetSingletonEntity<LagCompensationConfig>(testWorld.ClientWorlds[0]) != Entity.Null);

                var ep = NetworkEndpoint.LoopbackIpv4;
                ep.Port = 7979;
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ServerWorld).ValueRW.Listen(ep);
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ClientWorlds[0]).ValueRW.Connect(testWorld.ClientWorlds[0].EntityManager, ep);

                for (int i = 0; i < 16; ++i)
                    testWorld.Tick(16f/1000f);

                var serverPhy = testWorld.GetSingleton<PhysicsWorldHistorySingleton>(testWorld.ServerWorld);
                Assert.AreEqual(NetworkTick.Invalid, serverPhy.m_LastStoreTick);
                var clientPhy = testWorld.GetSingleton<PhysicsWorldHistorySingleton>(testWorld.ClientWorlds[0]);
                Assert.AreEqual(NetworkTick.Invalid, clientPhy.m_LastStoreTick);
            }
        }

        [Test]
        [UnityPlatform(RuntimePlatform.OSXEditor, RuntimePlatform.WindowsEditor)]
        public void HitWithLagCompensation()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                // Test lag compensation with 100ms ping
                testWorld.DriverSimulatedDelay = 50;
                testWorld.TestSpecificAdditionalAssemblies.Add("Unity.NetCode.Physics,");
                testWorld.TestSpecificAdditionalAssemblies.Add("Unity.Physics,");

                testWorld.Bootstrap(true,
                    typeof(TestAutoInGameSystem),
                    typeof(LagCompensationTestCubeMoveSystem),
                    typeof(LagCompensationTestCommandCommandSendSystem),
                    typeof(LagCompensationTestCommandCommandReceiveSystem),
                    typeof(LagCompensationTestCommandSystem),
                    typeof(LagCompensationTestHitScanSystem));

                var playerGameObject = new GameObject();
                playerGameObject.AddComponent<TestNetCodeAuthoring>().Converter = new LagCompensationTestPlayerConverter();
                playerGameObject.name = "LagCompensationTestPlayer";
                var ghostAuth = playerGameObject.AddComponent<GhostAuthoringComponent>();
                ghostAuth.DefaultGhostMode = GhostMode.OwnerPredicted;
                var cubeGameObject = new GameObject();
                cubeGameObject.name = "LagCompensationTestCube";
                var collider = cubeGameObject.AddComponent<UnityEngine.BoxCollider>();
                collider.size = new Vector3(1,1,1);

                Assert.IsTrue(testWorld.CreateGhostCollection(
                    playerGameObject, cubeGameObject));

                testWorld.CreateWorlds(true, 1);

                testWorld.ServerWorld.EntityManager.CreateEntity(typeof(LagCompensationConfig));
                testWorld.ClientWorlds[0].EntityManager.CreateEntity(typeof(LagCompensationConfig));
                var ep = NetworkEndpoint.LoopbackIpv4;
                ep.Port = 7979;
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ServerWorld).ValueRW.Listen(ep);
                testWorld.GetSingletonRW<NetworkStreamDriver>(testWorld.ClientWorlds[0]).ValueRW.Connect(testWorld.ClientWorlds[0].EntityManager, ep);

                LagCompensationTestHitScanSystem.HitStatus = 0;
                LagCompensationTestHitScanSystem.EnableLagCompensation = true;
                LagCompensationTestCommandSystem.Target = default;
                // Give the netcode some time to spawn entities and settle on a good time synchronization
                for (int i = 0; i < 128; ++i)
                    testWorld.Tick(1f/60f);
                LagCompensationTestCommandSystem.Target = new float3(-0.35f,0,-0.5f);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(1f/60f);
                Assert.AreEqual(3, LagCompensationTestHitScanSystem.HitStatus);

                // Test miss
                LagCompensationTestHitScanSystem.HitStatus = 0;
                LagCompensationTestCommandSystem.Target = new float3(-0.55f,0,-0.5f);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(1f/60f);
                Assert.AreEqual(0, LagCompensationTestHitScanSystem.HitStatus);

                // Make sure there is no hit without lag compensation
                LagCompensationTestHitScanSystem.HitStatus = 0;
                LagCompensationTestHitScanSystem.EnableLagCompensation = false;
                LagCompensationTestCommandSystem.Target = new float3(-0.35f,0,-0.5f);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(1f/60f);
                Assert.AreEqual(2, LagCompensationTestHitScanSystem.HitStatus);

                // Test miss
                LagCompensationTestHitScanSystem.HitStatus = 0;
                LagCompensationTestCommandSystem.Target = new float3(-0.55f,0,-0.5f);
                for (int i = 0; i < 32; ++i)
                    testWorld.Tick(1f/60f);
                Assert.AreEqual(0, LagCompensationTestHitScanSystem.HitStatus);
            }
        }
    }
}
