using Unity.Physics.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

namespace Unity.Physics.Authoring
{
#if UNITY_EDITOR

    /// Create and dispatch a DisplayMassPropertiesJob
    [UpdateInGroup(typeof(PhysicsDebugDisplayGroup))]
    [BurstCompile]
    internal partial struct DisplayMassPropertiesSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsDebugDisplayData>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out PhysicsDebugDisplayData debugDisplay) || debugDisplay.DrawMassProperties == 0)
                return;

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var dynamicsWorld = physicsWorld.DynamicsWorld;
            state.Dependency = new DisplayMassPropertiesJob
            {
                MotionDatas = dynamicsWorld.MotionDatas,
                MotionVelocities = dynamicsWorld.MotionVelocities,
		Offset = debugDisplay.Offset,
            }.Schedule(dynamicsWorld.MotionDatas.Length, 16, state.Dependency);
        }

        // Job to write mass properties info to a rendering buffer for any moving bodies
        // Attempts to build a box which has the same inertia tensor as the body.
        [BurstCompile]
        struct DisplayMassPropertiesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<MotionData> MotionDatas;
            [ReadOnly] public NativeArray<MotionVelocity> MotionVelocities;

            public float3 Offset;

            public void Execute(int m)
            {
                float3 com = MotionDatas[m].WorldFromMotion.pos;
                quaternion o = MotionDatas[m].WorldFromMotion.rot;

                float3 invInertiaLocal = MotionVelocities[m].InverseInertia;
                float invMass = MotionVelocities[m].InverseMass;

                var I = math.rcp(invInertiaLocal);

                // Reverse the inertia tensor computation to build a box which has the inertia tensor I.
                // The diagonal inertia of a box with dimensions h,w,d and mass m is:
                // I_x = 1/12 m (w^2 + d^2)
                // I_y = 1/12 m (d^2 + h^2)
                // I_z = 1/12 m (w^2 + h^2)
                //
                // Define k := I * 12 / m
                // Then k = (w^2 + d^2, d^2 + h^2, w^2 + h^2)
                // => w^2 = k_x - d^2, d^2 = k_y - h^2, h^2 = k_z - w^2
                // By manipulation:
                // 2w^2 = k_x - k_y + k_z
                // => w = ((0.5)(k_x - k_y + k_z))^-1
                // Then, substitution gives h and d.

                var k = 12f * invMass * I;

                // Mapping the inertia tensor to a box will lead to box dimensions in complex space for unphysical tensors.
                // In this case, some box dimensions will not be drawn (dimension set to 0 if their value is NaN).
                float w = math.sqrt((k.x - k.y + k.z) * 0.5f);
                float h = math.sqrt(k.z - w * w);
                float d = math.sqrt(k.y - h * h);

                var boxSize = new float3(
                    math.select(0, h, math.isfinite(h)),
                    math.select(0, w, math.isfinite(w)),
                    math.select(0, d, math.isfinite(d)));
                PhysicsDebugDisplaySystem.Box(boxSize, com, o, DebugDisplay.ColorIndex.Magenta, Offset);
            }
        }
    }
#endif
}
