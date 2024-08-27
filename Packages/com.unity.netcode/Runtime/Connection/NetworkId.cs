using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;


namespace Unity.NetCode
{
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("NetworkIdComponent has been deprecated. Use NetworkId instead (UnityUpgradable) -> NetworkId", true)]
    public struct NetworkIdComponent : IComponentData
    {}

    /// <summary>
    /// The connection identifier assigned by the server to the incoming client connection.
    /// The NetworkIdComponent is used as temporary client identifier for the current session. When a client disconnects,
    /// its network id can be reused by the server, and assigned to a new, incoming connection (on a a "first come, first serve" basis).
    /// Thus, there is no guarantee that a disconnecting client will receive the same network id once reconnected.
    /// As such, the network identifier should never be used to persist - and then retrieve - information for a given client/player.
    /// </summary>
    public struct NetworkId : IComponentData
    {
        /// <summary>
        /// The network identifier assigned by the server. A valid identifier it is always greater than 0.
        /// </summary>
        public int Value;

        /// <summary>
        /// Returns 'NID[value]'.
        /// </summary>
        /// <returns>Returns 'NID[value]'.</returns>
        public FixedString32Bytes ToFixedString()
        {
            var s = new FixedString32Bytes((FixedString32Bytes)"NID[");
            s.Append(Value);
            s.Append(']');
            return s;
        }

        /// <inheritdoc cref="ToFixedString"/>>
        public override string ToString() => ToFixedString().ToString();
    }

    /// <summary>
    /// System RPC sent from the server to client to assign a newtork id  (see <see cref="NetworkId"/>) to a new
    /// accepted connection.
    /// </summary>
    [BurstCompile]
    internal struct RpcSetNetworkId : IComponentData, IRpcCommandSerializer<RpcSetNetworkId>
    {
        public int nid;
        public int simTickRate;
        public int netTickRate;
        public int simMaxSteps;
        public int simMaxStepLength;
        public int fixStepTickRatio;

        public void Serialize(ref DataStreamWriter writer, in RpcSerializerState state, in RpcSetNetworkId data)
        {
            writer.WriteInt(data.nid);
            writer.WriteInt(data.simTickRate);
            writer.WriteInt(data.netTickRate);
            writer.WriteInt(data.simMaxSteps);
            writer.WriteInt(data.simMaxStepLength);
            writer.WriteInt(data.fixStepTickRatio);
        }

        public void Deserialize(ref DataStreamReader reader, in RpcDeserializerState state, ref RpcSetNetworkId data)
        {
            data.nid = reader.ReadInt();
            data.simTickRate = reader.ReadInt();
            data.netTickRate = reader.ReadInt();
            data.simMaxSteps = reader.ReadInt();
            data.simMaxStepLength = reader.ReadInt();
            data.fixStepTickRatio = reader.ReadInt();
        }

        [BurstCompile(DisableDirectCall = true)]
        [AOT.MonoPInvokeCallback(typeof(RpcExecutor.ExecuteDelegate))]
        private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
        {
            var rpcData = default(RpcSetNetworkId);
            var rpcSerializer = default(RpcSetNetworkId);
            rpcSerializer.Deserialize(ref parameters.Reader, parameters.DeserializerState, ref rpcData);

            parameters.CommandBuffer.AddComponent(parameters.JobIndex, parameters.Connection, new NetworkId {Value = rpcData.nid});
            var ent = parameters.CommandBuffer.CreateEntity(parameters.JobIndex);
            parameters.CommandBuffer.AddComponent(parameters.JobIndex, ent, new ClientServerTickRateRefreshRequest
            {
                MaxSimulationStepsPerFrame = rpcData.simMaxSteps,
                NetworkTickRate = rpcData.netTickRate,
                SimulationTickRate = rpcData.simTickRate,
                MaxSimulationStepBatchSize = rpcData.simMaxStepLength,
                PredictedFixedStepSimulationTickRatio = rpcData.fixStepTickRatio
            });
            parameters.CommandBuffer.SetName(parameters.JobIndex, parameters.Connection, new FixedString64Bytes(FixedString.Format("NetworkConnection ({0})", rpcData.nid)));
        }

        static readonly PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer =
            new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);
        public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
        {
            return InvokeExecuteFunctionPointer;
        }
    }
}
