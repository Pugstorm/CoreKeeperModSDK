#if !HAVOK_PHYSICS_EXISTS

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Systems;

namespace Unity.Physics.Authoring
{
#if UNITY_EDITOR

    // A system which draws any trigger events produced by the physics step system
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [BurstCompile]
    internal partial struct DisplayTriggerEventsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsDebugDisplayData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out PhysicsDebugDisplayData debugDisplay) || debugDisplay.DrawTriggerEvents == 0)
                return;

            state.Dependency = new DisplayTriggerEventsJob()
            {
                World = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                Offset = debugDisplay.Offset,
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        //Job which iterates over trigger events and writes display info to rendering buffers.
        [BurstCompile]
        private unsafe struct DisplayTriggerEventsJob : ITriggerEventsJob
        {
            [ReadOnly] public PhysicsWorld World;

            public float3 Offset;

            public void Execute(TriggerEvent triggerEvent)
            {
                RigidBody bodyA = World.Bodies[triggerEvent.BodyIndexA];
                RigidBody bodyB = World.Bodies[triggerEvent.BodyIndexB];

                Aabb aabbA = bodyA.CalculateAabb();
                Aabb aabbB = bodyB.CalculateAabb();
                PhysicsDebugDisplaySystem.Line(aabbA.Center, aabbB.Center, Unity.DebugDisplay.ColorIndex.Yellow, Offset);
            }
        }
    }
#endif
}

#endif
