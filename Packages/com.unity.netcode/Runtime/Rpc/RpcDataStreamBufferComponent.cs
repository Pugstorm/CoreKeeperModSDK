using System;
using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("OutgoingRpcDataStreamBufferComponent has been deprecated. Use OutgoingRpcDataStreamBuffer instead (UnityUpgradable) -> OutgoingRpcDataStreamBuffer", true)]
    [InternalBufferCapacity(0)]
    public struct OutgoingRpcDataStreamBufferComponent
    {
        /// <summary>
        /// The element value.
        /// </summary>
        public byte Value;
    }
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("IncomingRpcDataStreamBufferComponent has been deprecated. Use IncomingRpcDataStreamBuffer instead (UnityUpgradable) -> IncomingRpcDataStreamBuffer", true)]
    [InternalBufferCapacity(0)]
    public struct IncomingRpcDataStreamBufferComponent
    {
        /// <summary>
        /// The element value.
        /// </summary>
        public byte Value;
    }

    /// <summary>
    /// One per NetworkConnection. Stores queued, outgoing RPC data.
    /// Thus, buffer size is related to client-authored RPC count * size.
    /// InternalBufferCapacity is zero as RPCs can vary in size, and we don't want to constantly
    /// move the RPC data into and out of the chunk.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct OutgoingRpcDataStreamBuffer : IBufferElementData
    {
        /// <summary>
        /// The element value.
        /// </summary>
        public byte Value;
    }

    /// <summary>
    /// One per NetworkConnection. Stores queued, incoming RPC data.
    /// Thus, buffer size is related to inbound-from-server RPC count * size.
    /// InternalBufferCapacity is zero as RPCs can vary in size, and we don't want to constantly
    /// move the RPC data into and out of the chunk.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct IncomingRpcDataStreamBuffer : IBufferElementData
    {
        /// <summary>
        /// The element value.
        /// </summary>
        public byte Value;
    }
}
