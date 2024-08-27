using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Structure that contains the ghost <see cref="ArchetypeChunk"/> to serialize.
    /// Each chunk has its own priority, that is calculated based on the importance scaling
    /// factor (set for each ghost prefab at authoring time) and that can be further scaled
    /// using a custom <see cref="GhostImportance.ScaleImportanceFunction"/> or
    /// <see cref="GhostImportance.BatchScaleImportanceFunction"/>.
    /// </summary>
    public struct PrioChunk : IComparable<PrioChunk>
    {
        /// <summary>
        /// The ghost chunk that should be processed.
        /// </summary>
        public ArchetypeChunk chunk;
        /// <summary>
        /// The priority of the chunk. When using the <see cref="GhostImportance.BatchScaleImportanceFunction"/>
        /// scaling, it is the method responsibility to update this with the scaled priority.
        /// </summary>
        public int priority;
        /// <summary>
        /// The first entity index in the chunk that should be serialized. Normally 0, but if was not possible to
        /// serialize the whole chunk, the next time we will start replicating ghosts from that index.
        /// </summary>
        internal int startIndex;
        /// <summary>
        /// The type index in the <see cref="GhostCollectionPrefab"/> used to retrieve the information for
        /// serializing the ghost.
        /// </summary>
        internal int ghostType;
        /// <summary>
        /// Used for sorting the based on the priority in descending order.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(PrioChunk other)
        {
            // Reverse priority for sorting
            return other.priority - priority;
        }
    }
    /// <summary>
    /// Singleton component used to control importance settings
    /// </summary>
    [BurstCompile]
    public struct GhostImportance : IComponentData
    {
        /// <summary>
        /// Scale importance delegate. This describes the interface <see cref="GhostSendSystem"/> will use to compute
        /// importance scaling. The higher importance value returned from this method, the more often a ghost's data is synchronized.
        /// See <see cref="GhostDistanceImportance"/> for example implementation.
        /// </summary>
        /// <param name="connectionData">Per connection data. Ex. position in the world that should be prioritized.</param>
        /// <param name="importanceData">Optional configuration data. Ex. Each tile's configuration. Handle IntPtr.Zero!</param>
        /// <param name="chunkTile">Per chunk information. Ex. each entity's tile index.</param>
        /// <param name="basePriority">Priority computed by <see cref="GhostSendSystem"/> after computing tick when last updated and irrelevance.</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ScaleImportanceDelegate(IntPtr connectionData, IntPtr importanceData, IntPtr chunkTile, int basePriority);

        /// <summary>
        /// Default implementation of <see cref="ScaleImportanceDelegate"/>. Will return basePriority without computation.
        /// </summary>
        public static readonly PortableFunctionPointer<ScaleImportanceDelegate> NoScaleFunctionPointer =
            new PortableFunctionPointer<ScaleImportanceDelegate>(NoScale);

        /// <summary>
        /// Scale importance delegate. This describes the interface <see cref="GhostSendSystem"/> will use to compute
        /// importance scaling.
        /// The method is responsible to modify the <see cref="PrioChunk.priority"/> property for all the chunks (the higher the prioriy, the more often a ghost's data is synchronized).
        /// See <see cref="GhostDistanceImportance"/> for example implementation.
        /// </summary>
        /// <param name="connectionData">Per connection data. Ex. position in the world that should be prioritized.</param>
        /// <param name="importanceData">Optional configuration data. Ex. Each tile's configuration. Handle IntPtr.Zero!</param>
        /// <param name="sharedComponentTypeHandlePtr"><see cref="DynamicSharedComponentTypeHandle"/> to retrieve the per-chunk tile information. Ex. each chunk's tile index.</param>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void BatchScaleImportanceDelegate(IntPtr connectionData, IntPtr importanceData, IntPtr sharedComponentTypeHandlePtr,
            ref UnsafeList<PrioChunk> chunkData);
        /// <summary>
        /// This function pointer will be invoked with collected data as described in <see cref="BatchScaleImportanceDelegate"/>.
        /// <para>It is mandatory to set either this or <see cref="BatchScaleImportanceFunction"/> function pointer.
        /// It is also valid to set both, in which case the BatchScaleImportanceFunction is preferred.
        /// </para>
        /// </summary>
        public PortableFunctionPointer<ScaleImportanceDelegate> ScaleImportanceFunction;
        /// <summary>
        /// This function pointer will be invoked with collected data as described in <see cref="BatchScaleImportanceDelegate"/>.
        /// <para>It is mandatory to set either this or <see cref="ScaleImportanceFunction"/> function pointer.
        /// It is also valid to set both, in which case the BatchScaleImportanceFunction is preferred.
        /// </para>
        /// </summary>
        public PortableFunctionPointer<BatchScaleImportanceDelegate> BatchScaleImportanceFunction;
        /// <summary>
        /// ComponentType for connection data. <see cref="GhostSendSystem"/> will query for this component type before
        /// invoking the function assigned to <see cref="BatchScaleImportanceFunction"/>.
        /// </summary>
        public ComponentType GhostConnectionComponentType;
        /// <summary>
        /// Optional singleton ComponentType for configuration data.
        /// Leave default if not required. <see cref="IntPtr.Zero"/> will be passed into the <see cref="BatchScaleImportanceFunction"/>.
        /// <see cref="GhostSendSystem"/> will query for this component type, passing the data into the
        /// <see cref="BatchScaleImportanceFunction"/> function when invoking it.
        /// </summary>
        public ComponentType GhostImportanceDataType;
        /// <summary>
        /// ComponentType for per chunk data. Must be a shared component type! Each chunk represents a group of entities,
        /// collected as they share some importance-related value (e.g. distance to the players character controller).
        /// <see cref="GhostSendSystem"/> will query for this component type before invoking the function assigned to <see cref="BatchScaleImportanceFunction"/>.
        /// </summary>
        public ComponentType GhostImportancePerChunkDataType;

        [BurstCompile(DisableDirectCall = true)]
        [AOT.MonoPInvokeCallback(typeof(ScaleImportanceDelegate))]
        static int NoScale(IntPtr connectionData, IntPtr importanceData, IntPtr chunkTile, int basePriority)
        {
            return basePriority;
        }
    }
}
