using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode
{
    /// <summary>
    /// Add this component to each connection to determine which tiles the connection should prioritize.
    /// This will be passed as argument to the built-in scale function to compute Importance.
    /// See <see cref="GhostDistanceImportance"/> implementation.
    /// </summary>
    public struct GhostConnectionPosition : IComponentData
    {
        /// <summary>
        /// Position of the tile in world coordinates
        /// </summary>
        public float3 Position;
        /// <summary>
        /// Currently not updated by any systems. Made available for custom importance implementations.
        /// </summary>
        public quaternion Rotation;
        /// <summary>
        /// Currently not updated by any systems. Made available for custom importance implementations.
        /// </summary>
        public float4 ViewSize;
    }

    /// <summary>
    /// The default configuration data for <see cref="GhostImportance"/>.
    /// Uses tiling to group entities into spatial chunks, allowing chunks to be prioritized based on distance (via the
    /// <see cref="GhostDistancePartitioningSystem"/>), effectively giving you performant distance-based importance scaling.
    /// </summary>
    public struct GhostDistanceData : IComponentData
    {
        /// <summary>
        /// Dimensions of the tile.
        /// </summary>
        public int3 TileSize;
        /// <summary>
        /// Offset of the tile center
        /// </summary>
        public int3 TileCenter;
        /// <summary>
        /// Width of the tile border. When deciding whether an entity is on one or the other,
        /// the border where it can enter is determined by this parameter.
        /// </summary>
        public float3 TileBorderWidth;
    }

    /// <summary>
    /// Computes distance based importance scaling.
    /// I.e. Entities far away from a clients importance focal point (via <see cref="GhostConnectionPosition"/>) will be sent less often.
    /// </summary>
    [BurstCompile]
    public struct GhostDistanceImportance
    {
        /// <summary>
        /// Pointer to the <see cref="BatchScale"/> static method.
        /// </summary>
        public static readonly PortableFunctionPointer<GhostImportance.BatchScaleImportanceDelegate> BatchScaleFunctionPointer =
            new PortableFunctionPointer<GhostImportance.BatchScaleImportanceDelegate>(BatchScale);

        /// <summary>
        /// Pointer to the <see cref="Scale"/> static method.
        /// </summary>
        public static readonly PortableFunctionPointer<GhostImportance.ScaleImportanceDelegate> ScaleFunctionPointer =
            new PortableFunctionPointer<GhostImportance.ScaleImportanceDelegate>(Scale);

        [BurstCompile(DisableDirectCall = true)]
        [AOT.MonoPInvokeCallback(typeof(GhostImportance.ScaleImportanceDelegate))]
        private static int Scale(IntPtr connectionDataPtr, IntPtr distanceDataPtr, IntPtr chunkTilePtr, int basePriority)
        {
            var distanceData = GhostComponentSerializer.TypeCast<GhostDistanceData>(distanceDataPtr);
            var centerTile = (int3)((GhostComponentSerializer.TypeCast<GhostConnectionPosition>(connectionDataPtr).Position - distanceData.TileCenter) / distanceData.TileSize);
            var chunkTile = GhostComponentSerializer.TypeCast<GhostDistancePartitionShared>(chunkTilePtr);
            var delta = chunkTile.Index - centerTile;
            var distSq = math.dot(delta, delta);
            basePriority *= 1000;
            // 3 makes sure all adjacent tiles are considered the same as the tile the connection is in - required since it might be close to the edge
            if (distSq > 3)
                basePriority /= distSq;
            return basePriority;
        }

        [BurstCompile(DisableDirectCall = true)]
        [AOT.MonoPInvokeCallback(typeof(GhostImportance.BatchScaleImportanceDelegate))]
        private unsafe static void BatchScale(IntPtr connectionDataPtr, IntPtr distanceDataPtr, IntPtr sharedComponentTypeHandlePtr,
            ref UnsafeList<PrioChunk> chunks)
        {
            var distanceData = GhostComponentSerializer.TypeCast<GhostDistanceData>(distanceDataPtr);
            var centerTile = (int3)((GhostComponentSerializer.TypeCast<GhostConnectionPosition>(connectionDataPtr).Position - distanceData.TileCenter) / distanceData.TileSize);
            var sharedType = GhostComponentSerializer.TypeCast<DynamicSharedComponentTypeHandle>(sharedComponentTypeHandlePtr);
            for (int i = 0; i < chunks.Length ; ++i)
            {
                ref var data = ref chunks.ElementAt(i);
                var basePriority = data.priority;
                if (data.chunk.Has(ref sharedType))
                {
                    var chunkTile = (GhostDistancePartitionShared*)data.chunk.GetDynamicSharedComponentDataAddress(ref sharedType);
                    var delta = chunkTile->Index - centerTile;
                    var distSq = math.dot(delta, delta);
                    basePriority *= 1000;
                    // 3 makes sure all adjacent tiles are considered the same as the tile the connection is in - required since it might be close to the edge
                    if (distSq > 3)
                        basePriority /= distSq;
                    data.priority = basePriority;
                }
            }
        }
    }
}
