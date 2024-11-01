using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(RunSimulationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(SendClientInputSystem))]
public partial class InvertPlayerMovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var (clientInputRW, clientInputData) in
                 SystemAPI.Query<RefRW<ClientInput>, RefRW<ClientInputData>>().WithAll<GhostOwnerIsLocal>()) // GhostOwnerIsLocal => this is the local player
        {
            ref var clientInput = ref clientInputRW.ValueRW;
            clientInput.movementDirection *= -1f;
            // ClientInputData used to avoid unnecessary bytes being sent, add ClientInput to raw data struct which doesn't do any padding on serialization
			clientInputData.ValueRW = UnsafeUtility.As<ClientInput, ClientInputData>(ref clientInput);
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class MoveCowsAwayFromPlayerSystem : PugSimulationSystemBase
{
    protected override void OnUpdate()
    {
        // No need to dispose World allocator
        var playerPositions = new NativeList<float3>(World.UpdateAllocator.ToAllocator);

        Entities.WithAll<PlayerGhost>().ForEach((in LocalTransform translation) =>
        {
            playerPositions.Add(translation.Position);
        }).Schedule();
        
        var deltaTime = SystemAPI.Time.DeltaTime;
        var distanceToMoveSq = 5 * 5;
        // Filter on cattle just to avoid looping through everything
        Entities.WithAll<CattleCD>().ForEach((ref LocalTransform translation, in ObjectDataCD objectData) =>
            {
                if (objectData.objectID != ObjectID.Cow)
                {
                    return;
                }
                
                foreach (var playerPos in playerPositions)
                {
                    if (math.distancesq(translation.Position, playerPos) < distanceToMoveSq)
                    {
                        var dir = math.normalizesafe(translation.Position - playerPos);
                        translation.Position += dir * deltaTime;
                    }
                }
            }).Schedule();
        
        base.OnUpdate();
    }
}