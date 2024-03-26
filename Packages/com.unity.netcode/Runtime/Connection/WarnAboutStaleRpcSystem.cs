#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
#if NETCODE_DEBUG
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Burst;

namespace Unity.NetCode
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct WarnAboutStaleRpcSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReceiveRpcCommandRequest>();
        }
        [BurstCompile]
        partial struct WarnAboutStaleRpc : IJobEntity
        {
            public NetDebug netDebug;
            public FixedString128Bytes worldName;
            public void Execute(Entity entity, ref ReceiveRpcCommandRequest command)
            {
                if (!command.IsConsumed && ++command.Age >= netDebug.MaxRpcAgeFrames)
                {
                    var warning = (FixedString512Bytes) $"[{worldName}] NetCode RPC {entity.ToFixedString()} has not been consumed or destroyed for '{command.Age}' (MaxRpcAgeFrames) frames! Assumed unhandled. Either a) call .Consume(), or b) remove the ReceiveRpcCommandRequestComponent component, or c) destroy the entity.";
                    netDebug.LogWarning(warning);

                    command.Consume();
                }
            }
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var warnJob = new WarnAboutStaleRpc
            {
                netDebug = SystemAPI.GetSingleton<NetDebug>(),
                worldName = state.WorldUnmanaged.Name
            };
            state.Dependency = warnJob.Schedule(state.Dependency);
        }
    }
}
#endif
