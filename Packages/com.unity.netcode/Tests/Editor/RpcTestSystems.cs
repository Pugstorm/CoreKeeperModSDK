using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine.Assertions;

namespace Unity.NetCode.Tests
{
    [BurstCompile]
    public struct SimpleRpcCommand : IComponentData, IRpcCommandSerializer<SimpleRpcCommand>
    {
        public void Serialize(ref DataStreamWriter writer, in RpcSerializerState state, in SimpleRpcCommand data)
        {
        }

        public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state, ref SimpleRpcCommand data)
        {
        }

        public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
        {
            return InvokeExecuteFunctionPointer;
        }

        [BurstCompile(DisableDirectCall = true)]
        [AOT.MonoPInvokeCallback(typeof(RpcExecutor.ExecuteDelegate))]
        private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
        {
            RpcExecutor.ExecuteCreateRequestComponent<SimpleRpcCommand, SimpleRpcCommand>(ref parameters);
        }

        static readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer =
            new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);
    }

    [BurstCompile]
    public struct SerializedRpcCommand : IComponentData, IRpcCommandSerializer<SerializedRpcCommand>
    {
        public int intValue;
        public short shortValue;
        public float floatValue;

        public void Serialize(ref DataStreamWriter writer, in RpcSerializerState state, in SerializedRpcCommand data)
        {
            writer.WriteInt(data.intValue);
            writer.WriteShort(data.shortValue);
            writer.WriteFloat(data.floatValue);
        }

        public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state, ref SerializedRpcCommand data)
        {
            data.intValue = reader.ReadInt();
            data.shortValue = reader.ReadShort();
            data.floatValue = reader.ReadFloat();
        }

        public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
        {
            return InvokeExecuteFunctionPointer;
        }

        [BurstCompile(DisableDirectCall = true)]
        [AOT.MonoPInvokeCallback(typeof(RpcExecutor.ExecuteDelegate))]
        private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
        {
            var serializedData = default(SerializedRpcCommand);
            serializedData.Deserialize(ref parameters.Reader, parameters.DeserializerState, ref serializedData);

            var entity = parameters.CommandBuffer.CreateEntity(parameters.JobIndex);
            parameters.CommandBuffer.AddComponent(parameters.JobIndex, entity,
                new ReceiveRpcCommandRequest {SourceConnection = parameters.Connection});
            parameters.CommandBuffer.AddComponent(parameters.JobIndex, entity, serializedData);
        }

        static readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer =
            new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);
    }

    [BurstCompile]
    public struct SerializedLargeRpcCommand : IComponentData, IRpcCommandSerializer<SerializedLargeRpcCommand>
    {
        public FixedString512Bytes stringValue;

        public void Serialize(ref DataStreamWriter writer, in RpcSerializerState state, in SerializedLargeRpcCommand data)
        {
            writer.WriteFixedString512(data.stringValue);
        }

        public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state, ref SerializedLargeRpcCommand data)
        {
            data.stringValue = reader.ReadFixedString512();
        }

        public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
        {
            return InvokeExecuteFunctionPointer;
        }

        [BurstCompile(DisableDirectCall = true)]
        [AOT.MonoPInvokeCallback(typeof(RpcExecutor.ExecuteDelegate))]
        private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
        {
            var serializedData = default(SerializedLargeRpcCommand);
            serializedData.Deserialize(ref parameters.Reader, parameters.DeserializerState, ref serializedData);

            var entity = parameters.CommandBuffer.CreateEntity(parameters.JobIndex);
            parameters.CommandBuffer.AddComponent(parameters.JobIndex, entity,
                new ReceiveRpcCommandRequest {SourceConnection = parameters.Connection});
            parameters.CommandBuffer.AddComponent(parameters.JobIndex, entity, serializedData);
        }

        static readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer =
            new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);
    }

    [BurstCompile]
    public struct ClientIdRpcCommand : IComponentData, IRpcCommandSerializer<ClientIdRpcCommand>
    {
        public int Id;

        public void Serialize(ref DataStreamWriter writer, in RpcSerializerState state, in ClientIdRpcCommand data)
        {
            writer.WriteInt(data.Id);
        }

        public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state, ref ClientIdRpcCommand data)
        {
            data.Id = reader.ReadInt();
        }

        public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
        {
            return InvokeExecuteFunctionPointer;
        }

        [BurstCompile(DisableDirectCall = true)]
        [AOT.MonoPInvokeCallback(typeof(RpcExecutor.ExecuteDelegate))]
        private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
        {
            var serializedData = default(ClientIdRpcCommand);
            serializedData.Deserialize(ref parameters.Reader, parameters.DeserializerState, ref serializedData);

            var entity = parameters.CommandBuffer.CreateEntity(parameters.JobIndex);
            parameters.CommandBuffer.AddComponent(parameters.JobIndex, entity,
                new ReceiveRpcCommandRequest {SourceConnection = parameters.Connection});
            parameters.CommandBuffer.AddComponent(parameters.JobIndex, entity, serializedData);
        }

        static readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer =
            new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);
    }

    public struct InvalidRpcCommand : IComponentData, IRpcCommandSerializer<InvalidRpcCommand>
    {
        public void Serialize(ref DataStreamWriter writer, in RpcSerializerState state, in InvalidRpcCommand data)
        {
        }

        public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state, ref InvalidRpcCommand data)
        {
        }

        public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
        {
            return new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>();
        }
    }

    [BurstCompile]
    public struct RpcWithEntity : IComponentData, IRpcCommandSerializer<RpcWithEntity>
    {
        public Entity entity;

        public void Serialize(ref DataStreamWriter writer, in RpcSerializerState state, in Unity.NetCode.Tests.RpcWithEntity data)
        {
            if (state.GhostFromEntity.HasComponent(data.entity))
            {
                var ghostComponent = state.GhostFromEntity[data.entity];
                writer.WriteInt(ghostComponent.ghostId);
                writer.WriteUInt(ghostComponent.spawnTick.SerializedData);
            }
            else
            {
                writer.WriteInt(0);
                writer.WriteUInt(NetworkTick.Invalid.SerializedData);
            }
        }

        public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state,  ref RpcWithEntity data)
        {
            var ghostId = reader.ReadInt();
            var spawnTick = new NetworkTick{SerializedData = reader.ReadUInt()};
            data.entity = Entity.Null;
            if (ghostId != 0 && spawnTick.IsValid)
            {
                if (state.ghostMap.TryGetValue(new SpawnedGhost{ghostId = ghostId, spawnTick = spawnTick}, out var ghostEnt))
                    data.entity = ghostEnt;
            }
        }

        [BurstCompile(DisableDirectCall = true)]
        [AOT.MonoPInvokeCallback(typeof(RpcExecutor.ExecuteDelegate))]
        private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
        {
            RpcExecutor.ExecuteCreateRequestComponent<RpcWithEntity, RpcWithEntity>(ref parameters);
        }

        static readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer =
            new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);
        public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
        {
            return InvokeExecuteFunctionPointer;
        }
    }

    #region Send Systems
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ClientRcpSendSystem : SystemBase
    {
        public static int SendCount = 0;
        public Entity Remote = Entity.Null;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkId>();
        }

        protected override void OnUpdate()
        {
            if (SendCount > 0)
            {
                var req = EntityManager.CreateEntity();
                EntityManager.AddComponentData(req, new SimpleRpcCommand());
                EntityManager.AddComponentData(req, new SendRpcCommandRequest {TargetConnection = Remote});
                --SendCount;
            }
        }
    }

    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ServerRpcBroadcastSendSystem : SystemBase
    {
        public static int SendCount = 0;

        protected override void OnUpdate()
        {
            if (SendCount > 0)
            {
                var req = EntityManager.CreateEntity();
                EntityManager.AddComponentData(req, new SimpleRpcCommand());
                EntityManager.AddComponentData(req,
                    new SendRpcCommandRequest {TargetConnection = Entity.Null});
                --SendCount;
            }
        }
    }

    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class MalformedClientRcpSendSystem : SystemBase
    {
        public static int[] SendCount = new int[2];
        public static ClientIdRpcCommand[] Cmds = new ClientIdRpcCommand[2];

        private int worldId;

        protected override void OnCreate()
        {
            //This is the most correct and best practice to use on the client side.
            //However, it still does not catch the issue when a client enqueue an rpc in the same frame we tag the connection
            //as RequestDisconnected (enqued in the command buffer)
            //Even if we would tag the connection synchronously (in the middle of the frame)
            //if the client system is schedule to execute AFTER the RpcCommandRequestSystem (or the RpcSystem) or the system that
            //change the connection state, clients can still queue commands even though the connection will be closed.
            RequireForUpdate<NetworkId>();
            worldId = NetCodeTestWorld.CalculateWorldId(World);
        }

        protected override void OnUpdate()
        {
            if (SendCount[worldId] > 0)
            {
                var entity = SystemAPI.GetSingletonEntity<NetworkId>();
                var req = EntityManager.CreateEntity();
                EntityManager.AddComponentData(req, Cmds[worldId]);
                EntityManager.AddComponentData(req, new SendRpcCommandRequest {TargetConnection = Entity.Null});
                --SendCount[worldId];
            }
        }
    }

    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class SerializedServerRcpSendSystem : SystemBase
    {
        public static int SendCount = 0;
        public static SerializedRpcCommand Cmd;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkId>();
        }

        protected override void OnUpdate()
        {
            if (SendCount > 0)
            {
                var req = EntityManager.CreateEntity();
                EntityManager.AddComponentData(req, Cmd);
                EntityManager.AddComponentData(req,
                    new SendRpcCommandRequest {TargetConnection = Entity.Null});
                --SendCount;
            }
        }
    }

    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class SerializedClientRcpSendSystem : SystemBase
    {
        public static int SendCount = 0;
        public static SerializedRpcCommand Cmd;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkId>();
        }

        protected override void OnUpdate()
        {
            if (SendCount > 0)
            {
                var req = EntityManager.CreateEntity();
                EntityManager.AddComponentData(req, Cmd);
                EntityManager.AddComponentData(req,
                    new SendRpcCommandRequest {TargetConnection = Entity.Null});
                --SendCount;
            }
        }
    }

    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class SerializedClientLargeRcpSendSystem : SystemBase
    {
        public static int SendCount = 0;
        public static SerializedLargeRpcCommand Cmd;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkId>();
        }

        protected override void OnUpdate()
        {
            while (SendCount > 0)
            {
                var req = EntityManager.CreateEntity();
                EntityManager.AddComponentData(req, Cmd);
                EntityManager.AddComponentData(req,
                    new SendRpcCommandRequest {TargetConnection = Entity.Null});
                --SendCount;
            }
        }
    }

    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class FlawedClientRcpSendSystem : SystemBase
    {
        public static int SendCount = 0;

        protected override void OnUpdate()
        {
            if (SystemAPI.HasSingleton<NetworkStreamConnection>() && !SystemAPI.HasSingleton<NetworkId>() && SendCount > 0)
            {
                var req = EntityManager.CreateEntity();
                EntityManager.AddComponentData(req, default(SimpleRpcCommand));
                EntityManager.AddComponentData(req,
                    new SendRpcCommandRequest {TargetConnection = Entity.Null});
                --SendCount;
            }
        }
    }
    #endregion

    #region Receive Systems
    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ServerMultipleRpcReceiveSystem : SystemBase
    {
        public static int[] ReceivedCount = new int[2];

        protected override void OnUpdate()
        {
            var PostUpdateCommands = new EntityCommandBuffer(Allocator.Temp);
            Entities.WithoutBurst().ForEach((Entity entity, ref ClientIdRpcCommand cmd, ref ReceiveRpcCommandRequest req) =>
            {
                PostUpdateCommands.DestroyEntity(entity);
                if (cmd.Id >= 0 && cmd.Id < 2)
                    ReceivedCount[cmd.Id]++;
            }).Run();
            PostUpdateCommands.Playback(EntityManager);
        }
    }


    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class MultipleClientBroadcastRpcReceiveSystem : SystemBase
    {
        public static int[] ReceivedCount = new int[2];

        private int worldId;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkId>();
            worldId = NetCodeTestWorld.CalculateWorldId(World);
        }

        protected override void OnUpdate()
        {
            var PostUpdateCommands = new EntityCommandBuffer(Allocator.Temp);
            var currentWorldId = worldId;
            Entities.WithoutBurst()
                .WithAll<SimpleRpcCommand>()
                .ForEach((Entity entity, ref ReceiveRpcCommandRequest req) =>
            {
                PostUpdateCommands.DestroyEntity(entity);
                ++ReceivedCount[currentWorldId];
            }).Run();
            PostUpdateCommands.Playback(EntityManager);
        }
    }

    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class ServerRpcReceiveSystem : SystemBase
    {
        public static int ReceivedCount = 0;

        protected override void OnUpdate()
        {
            var PostUpdateCommands = new EntityCommandBuffer(Allocator.Temp);
            Entities.WithoutBurst()
                .WithAll<SimpleRpcCommand>()
                .ForEach((Entity entity, ref ReceiveRpcCommandRequest req) =>
            {
                PostUpdateCommands.DestroyEntity(entity);
                ++ReceivedCount;
            }).Run();
            PostUpdateCommands.Playback(EntityManager);
        }
    }

    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ClientRpcReceiveSystem : SystemBase
    {
        public int ReceivedCount = 0;

        protected override void OnUpdate()
        {
            var PostUpdateCommands = new EntityCommandBuffer(Allocator.Temp);
            Entities.WithoutBurst()
                .WithAll<SimpleRpcCommand>()
                .ForEach((Entity entity, ref ReceiveRpcCommandRequest req) =>
                {
                    PostUpdateCommands.DestroyEntity(entity);
                    ++ReceivedCount;
                }).Run();
            PostUpdateCommands.Playback(EntityManager);
        }
    }

    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class SerializedClientRpcReceiveSystem : SystemBase
    {
        public static int ReceivedCount = 0;
        public static SerializedRpcCommand ReceivedCmd;

        protected override void OnUpdate()
        {
            var PostUpdateCommands = new EntityCommandBuffer(Allocator.Temp);
            Entities.WithoutBurst().ForEach((Entity entity, ref SerializedRpcCommand cmd, ref ReceiveRpcCommandRequest req) =>
            {
                ReceivedCmd = cmd;
                PostUpdateCommands.DestroyEntity(entity);
                ++ReceivedCount;
            }).Run();
            PostUpdateCommands.Playback(EntityManager);
        }
    }

    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class SerializedServerRpcReceiveSystem : SystemBase
    {
        public static int ReceivedCount = 0;
        public static SerializedRpcCommand ReceivedCmd;

        protected override void OnUpdate()
        {
            var PostUpdateCommands = new EntityCommandBuffer(Allocator.Temp);
            Entities.WithoutBurst().ForEach((Entity entity, ref SerializedRpcCommand cmd, ref ReceiveRpcCommandRequest req) =>
            {
                ReceivedCmd = cmd;
                PostUpdateCommands.DestroyEntity(entity);
                ++ReceivedCount;
            }).Run();
            PostUpdateCommands.Playback(EntityManager);
        }
    }
    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class SerializedServerLargeRpcReceiveSystem : SystemBase
    {
        public static int ReceivedCount = 0;
        public static SerializedLargeRpcCommand ReceivedCmd;

        protected override void OnUpdate()
        {
            var PostUpdateCommands = new EntityCommandBuffer(Allocator.Temp);
            Entities.WithoutBurst().ForEach((Entity entity, ref SerializedLargeRpcCommand cmd, ref ReceiveRpcCommandRequest req) =>
            {
                ReceivedCmd = cmd;
                PostUpdateCommands.DestroyEntity(entity);
                ++ReceivedCount;
            }).Run();
            PostUpdateCommands.Playback(EntityManager);
        }
    }
    #endregion

    [DisableAutoCreation]
    [UpdateInGroup(typeof(RpcCommandRequestSystemGroup))]
    [CreateAfter(typeof(RpcSystem))]
    [BurstCompile]
    partial struct SerializedLargeRpcCommandRequestSystem : ISystem
    {
        RpcCommandRequest<SerializedLargeRpcCommand, SerializedLargeRpcCommand> m_Request;
        [BurstCompile]
        struct SendRpc : IJobChunk
        {
            public RpcCommandRequest<SerializedLargeRpcCommand, SerializedLargeRpcCommand>.SendRpcData data;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                data.Execute(chunk, unfilteredChunkIndex);
            }
        }
        public void OnCreate(ref SystemState state)
        {
            m_Request.OnCreate(ref state);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sendJob = new SendRpc{data = m_Request.InitJobData(ref state)};
            state.Dependency = sendJob.Schedule(m_Request.Query, state.Dependency);
        }
    }
    [DisableAutoCreation]
    [UpdateInGroup(typeof(RpcCommandRequestSystemGroup))]
    [CreateAfter(typeof(RpcSystem))]
    [BurstCompile]
    partial struct SerializedRpcCommandRequestSystem : ISystem
    {
        RpcCommandRequest<SerializedRpcCommand, SerializedRpcCommand> m_Request;
        [BurstCompile]
        struct SendRpc : IJobChunk
        {
            public RpcCommandRequest<SerializedRpcCommand, SerializedRpcCommand>.SendRpcData data;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                data.Execute(chunk, unfilteredChunkIndex);
            }
        }
        public void OnCreate(ref SystemState state)
        {
            m_Request.OnCreate(ref state);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sendJob = new SendRpc{data = m_Request.InitJobData(ref state)};
            state.Dependency = sendJob.Schedule(m_Request.Query, state.Dependency);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(RpcCommandRequestSystemGroup))]
    [CreateAfter(typeof(RpcSystem))]
    [BurstCompile]
    partial struct NonSerializedRpcCommandRequestSystem : ISystem
    {
        RpcCommandRequest<SimpleRpcCommand, SimpleRpcCommand> m_Request;
        [BurstCompile]
        struct SendRpc : IJobChunk
        {
            public RpcCommandRequest<SimpleRpcCommand, SimpleRpcCommand>.SendRpcData data;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                data.Execute(chunk, unfilteredChunkIndex);
            }
        }
        public void OnCreate(ref SystemState state)
        {
            m_Request.OnCreate(ref state);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sendJob = new SendRpc{data = m_Request.InitJobData(ref state)};
            state.Dependency = sendJob.Schedule(m_Request.Query, state.Dependency);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(RpcCommandRequestSystemGroup))]
    [CreateAfter(typeof(RpcSystem))]
    [BurstCompile]
    partial struct MultipleClientSerializedRpcCommandRequestSystem : ISystem
    {
        RpcCommandRequest<ClientIdRpcCommand, ClientIdRpcCommand> m_Request;
        [BurstCompile]
        struct SendRpc : IJobChunk
        {
            public RpcCommandRequest<ClientIdRpcCommand, ClientIdRpcCommand>.SendRpcData data;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                data.Execute(chunk, unfilteredChunkIndex);
            }
        }
        public void OnCreate(ref SystemState state)
        {
            m_Request.OnCreate(ref state);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sendJob = new SendRpc{data = m_Request.InitJobData(ref state)};
            state.Dependency = sendJob.Schedule(m_Request.Query, state.Dependency);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(RpcCommandRequestSystemGroup))]
    [CreateAfter(typeof(RpcSystem))]
    [BurstCompile]
    partial struct InvalidRpcCommandRequestSystem : ISystem
    {
        RpcCommandRequest<InvalidRpcCommand, InvalidRpcCommand> m_Request;
        [BurstCompile]
        struct SendRpc : IJobChunk
        {
            public RpcCommandRequest<InvalidRpcCommand, InvalidRpcCommand>.SendRpcData data;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                data.Execute(chunk, unfilteredChunkIndex);
            }
        }
        public void OnCreate(ref SystemState state)
        {
            m_Request.OnCreate(ref state);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sendJob = new SendRpc{data = m_Request.InitJobData(ref state)};
            state.Dependency = sendJob.Schedule(m_Request.Query, state.Dependency);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(RpcCommandRequestSystemGroup))]
    [CreateAfter(typeof(RpcSystem))]
    [BurstCompile]
    partial struct RpcWithEntityRpcCommandRequestSystem : ISystem
    {
        RpcCommandRequest<RpcWithEntity, RpcWithEntity> m_Request;
        [BurstCompile]
        struct SendRpc : IJobChunk
        {
            public RpcCommandRequest<RpcWithEntity, RpcWithEntity>.SendRpcData data;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                data.Execute(chunk, unfilteredChunkIndex);
            }
        }
        public void OnCreate(ref SystemState state)
        {
            m_Request.OnCreate(ref state);
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sendJob = new SendRpc{data = m_Request.InitJobData(ref state)};
            state.Dependency = sendJob.Schedule(m_Request.Query, state.Dependency);
        }
    }
}
