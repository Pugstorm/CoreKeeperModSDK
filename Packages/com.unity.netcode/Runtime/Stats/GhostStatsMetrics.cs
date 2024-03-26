#if UNITY_EDITOR || NETCODE_DEBUG
using System;
using Unity.Collections;
using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("GhostMetricsMonitorComponent has been deprecated. Use GhostMetricsMonitor instead (UnityUpgradable) -> GhostMetricsMonitor", true)]
    public struct GhostMetricsMonitorComponent : IComponentData
    {}

    /// <summary>
    /// Present on both client and server world, singleton component that enables monitoring of ghost metrics.
    /// </summary>
    public struct GhostMetricsMonitor : IComponentData
    {
        /// <summary>
        /// The server tick that we received an update to our metrics.
        /// </summary>
        public NetworkTick CapturedTick;
    }

    /// <summary>
    /// Singleton component for Network and Time Related Metrics.
    /// </summary>
    public struct NetworkMetrics : IComponentData
    {
        /// <summary>
        /// Only meaningful on the client that run at variable step rate. On the server is always 1.0. Always in range is (0.0 and 1.0].
        /// </summary>
        public float SampleFraction;
        /// <summary>
        /// The average value for Time Scale
        /// </summary>
        public float TimeScale;
        /// <summary>
        /// </summary>
        public float InterpolationOffset;
        /// <summary>
        /// </summary>
        public float InterpolationScale;
        /// <summary>
        /// The age of the command stream
        /// </summary>
        public float CommandAge;
        /// <summary>
        /// Estimated round-trip time.
        /// </summary>
        public float Rtt;
        /// <summary>
        /// Estimated jitter.
        /// </summary>
        public float Jitter;
        /// <summary>
        /// </summary>
        public float SnapshotAgeMin;
        /// <summary>
        /// </summary>
        public float SnapshotAgeMax;
    }

    /// <summary>
    /// Snapshot metrics singleton component.
    /// </summary>
    public struct SnapshotMetrics : IComponentData
    {
        /// <summary>
        /// The server tick when the snapshot metrics where collected.
        /// </summary>
        public uint SnapshotTick;
        /// <summary>
        /// Total size of the snapshot packet.
        /// </summary>
        public uint TotalSizeInBits;
        /// <summary>
        /// Total count of ghosts inside the snapshot packet.
        /// </summary>
        public uint TotalGhostCount;
        /// <summary>
        /// Despawn count.
        /// </summary>
        public uint DestroyInstanceCount;
        /// <summary>
        /// Size of the despawn packet.
        /// </summary>
        public uint DestroySizeInBits;
    }

    /// <summary>
    /// Monitor serialization timings of ghosts.
    /// <remarks>
    /// In order to know what value each index refers to, we need to also grab the Indices from <see cref="GhostNames"/>.
    /// </remarks>
    /// </summary>
    public struct GhostSerializationMetrics : IBufferElementData
    {
        /// <summary>
        /// Ghost Serialization time in microseconds
        /// </summary>
        public float LastRecordedValue;
    }

    /// <summary>
    /// Monitor prediction errors of ghosts.
    /// <remarks>
    /// In order to know what value each index refers to, we need to also grab the Indices from <see cref="PredictionErrorNames"/>.
    /// </remarks>
    /// </summary>
    public struct PredictionErrorMetrics : IBufferElementData
    {
        /// <summary>
        /// Last recorder prediction error metric
        /// </summary>
        public float Value;
    }

    /// <summary>
    /// A list of all currently available Prediction Error names.
    /// This list maps 1-1 with <see cref="PredictionErrorMetrics"/>
    /// </summary>
    public struct PredictionErrorNames : IBufferElementData
    {
        /// <summary>
        /// Name of the prediction error type
        /// </summary>
        public FixedString128Bytes Name;
    }
    /// <summary>
    /// A list of all currently available Ghosts.
    /// This list maps 1-1 with <see cref="GhostSerializationMetrics"/> and <see cref="GhostMetrics"/>
    /// </summary>
    public struct GhostNames : IBufferElementData
    {
        /// <summary>
        /// Name of the Ghost type
        /// </summary>
        public FixedString64Bytes Name;
    }

    /// <summary>
    /// A list of serialized ghosts metrics.
    /// <remarks>To find the corresponding ghost name for each metric, each index in this buffer is a 1 to 1 mapping of <see cref="GhostNames"/></remarks>
    /// </summary>
    public struct GhostMetrics : IBufferElementData
    {
        /// <summary>
        /// How many instances of this ghost was in the serialized packet.
        /// </summary>
        public uint InstanceCount;
        /// <summary>
        /// The size of the serialized ghost in bits
        /// </summary>
        public uint SizeInBits;
        /// <summary>
        /// <remarks>Only Available on Server</remarks>
        /// How many chunks we needed to go through in order to create the snapshot.
        /// </summary>
        public uint ChunkCount;   // server
        /// <summary>
        /// <remarks>Only Available on Client</remarks>
        /// The uncompressed size of the ghost.
        /// </summary>
        public uint Uncompressed; // client
    }
}

#endif
