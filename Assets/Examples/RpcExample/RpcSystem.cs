using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

public struct PingPingRPC : IRpcCommand
{
    public FixedString32Bytes Message;
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(RunSimulationSystemGroup))]
public partial class PongSystem : PugSimulationSystemBase
{
    protected override void OnUpdate()
    {
        var ecb = CreateCommandBuffer();

        Entities.WithAll<ReceiveRpcCommandRequest>().ForEach((Entity entity, ref PingPingRPC pingPong) =>
        {
            Debug.Log($"client got {pingPong.Message.Value}");
            pingPong.Message = "Pong";
            var pingEntity = ecb.CreateEntity();
            ecb.AddComponent(pingEntity, pingPong);
            ecb.AddComponent<SendRpcCommandRequest>(pingEntity);
            ecb.DestroyEntity(entity);
        }).WithStoreEntityQueryInField(ref _queryRequiredForUpdate).WithoutBurst().Run();
        
        EntityManager.DestroyEntity(GetEntityQuery(typeof(PingPingRPC), typeof(ReceiveRpcCommandRequest)));
        //ecb.DestroyEntitiesForEntityQuery(_queryRequiredForUpdate);
        
        base.OnUpdate();
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class PingSystem : PugSimulationSystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<NetworkId>()));
        base.OnCreate();
    }

    protected override void OnStartRunning()
    {
        Debug.Log("Create initial ping");
        var pingEntity = EntityManager.CreateEntity(typeof(PingPingRPC), typeof(SendRpcCommandRequest));
        EntityManager.SetComponentData(pingEntity, new PingPingRPC { Message = "Ping" });
        base.OnStartRunning();
    }

    protected override void OnUpdate()
    {
        var ecb = CreateCommandBuffer();

        var response = new PingPingRPC { Message = "Ping" };

        Entities.WithAll<ReceiveRpcCommandRequest>().ForEach((Entity entity, in PingPingRPC pingPong) =>
        {
            Debug.Log($"server got {pingPong.Message}");
            var pongEntity = ecb.CreateEntity();
            ecb.AddComponent(pongEntity, response);
            ecb.AddComponent<SendRpcCommandRequest>(pongEntity);
            ecb.DestroyEntity(entity);
        }).WithoutBurst().Schedule();
        
        base.OnUpdate();
    }
}