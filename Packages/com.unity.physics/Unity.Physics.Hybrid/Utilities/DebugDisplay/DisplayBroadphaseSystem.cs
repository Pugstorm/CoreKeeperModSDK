using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Systems;

namespace Unity.Physics.Authoring
{
#if UNITY_EDITOR

    /// Job which walks the broadphase tree and displays the
    /// bounding box of leaf nodes.
    [BurstCompile]
    internal struct DisplayBroadphaseJob : IJob
    {
        [ReadOnly]
        public NativeArray<BoundingVolumeHierarchy.Node> StaticNodes;

        [ReadOnly]
        public NativeArray<BoundingVolumeHierarchy.Node> DynamicNodes;

        public float3 Offset;

        internal void DrawLeavesRecursive(NativeArray<BoundingVolumeHierarchy.Node> nodes, Unity.DebugDisplay.ColorIndex color, int nodeIndex)
        {
            if (nodes[nodeIndex].IsLeaf)
            {
                bool4 leavesValid = nodes[nodeIndex].AreLeavesValid;
                for (int l = 0; l < 4; l++)
                {
                    if (leavesValid[l])
                    {
                        Aabb aabb = nodes[nodeIndex].Bounds.GetAabb(l);
                        float3 center = aabb.Center;
                        PhysicsDebugDisplaySystem.Box(aabb.Extents, center, quaternion.identity, color, Offset);
                    }
                }

                return;
            }

            for (int i = 0; i < 4; i++)
            {
                if (nodes[nodeIndex].IsChildValid(i))
                {
                    DrawLeavesRecursive(nodes, color, nodes[nodeIndex].Data[i]);
                }
            }
        }

        public void Execute()
        {
            DrawLeavesRecursive(StaticNodes, Unity.DebugDisplay.ColorIndex.Yellow, 1);
            DrawLeavesRecursive(DynamicNodes, Unity.DebugDisplay.ColorIndex.Red, 1);
        }
    }

    // Creates DisplayBroadphaseJobs
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(PhysicsDebugDisplayGroup))]
    [BurstCompile]
    internal partial struct DisplayBroadphaseAabbsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsDebugDisplayData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out PhysicsDebugDisplayData debugDisplay) || debugDisplay.DrawBroadphase == 0)
                return;

            Broadphase broadphase = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld.Broadphase;

            state.Dependency = new DisplayBroadphaseJob
            {
                StaticNodes = broadphase.StaticTree.Nodes,
                DynamicNodes = broadphase.DynamicTree.Nodes,
                Offset = debugDisplay.Offset,
            }.Schedule(state.Dependency);
        }
    }
#endif
}
