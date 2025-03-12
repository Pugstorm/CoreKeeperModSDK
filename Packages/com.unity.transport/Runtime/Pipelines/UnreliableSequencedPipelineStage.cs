using System;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport.Utilities;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// Pipeline stage that can be used to ensure the ordering of packets sent through it. Note that
    /// it only guarantees the ordering, it does not make any reliability guarantees. This pipeline
    /// stage basically just drops any packet that arrives out-of-order. For reliability guarantees,
    /// use the <see cref="ReliableSequencedPipelineStage"/>.
    /// </summary>
    [BurstCompile]
    public unsafe struct UnreliableSequencedPipelineStage : INetworkPipelineStage
    {
        /// <summary>Stores per-instance data.</summary>
        public struct SequenceId
        {
            /// <summary>Incrementing Id representing the next packet sequence number (or the last received sequence number).</summary>
            internal ushort Value;
            
            /// <summary>Pretty-printed statistics.</summary>
            /// <returns>Pretty-printed statistics.</returns>
            public FixedString64Bytes ToFixedString() => $"USPS.SequenceId[{Value}]";
            
            /// <inheritdoc cref="ToFixedString"/>
            public override string ToString() => ToFixedString().ToString();
        }

        /// <summary>
        /// Stores <see cref="UnreliableSequencedPipelineStage"/> statistics.
        /// </summary>
        public struct Statistics
        {
            /// <summary>Count of total packets processed through this stage.</summary>
            public ulong NumPacketsSent;
            /// <summary>Count of total packets processed through this stage.</summary>
            public ulong NumPacketsReceived;
            /// <summary>Counts the number of packets dropped (i.e. "culled") due to invalid SequenceId. I.e. Implies the packet arrived, but after the one(s) before it.</summary>
            public ulong NumPacketsCulledOutOfOrder; 
            /// <summary>Detects gaps in SequenceId to determine real packet loss.</summary>
            public ulong NumPacketsDroppedNeverArrived; 

            /// <summary>Percentage of all packets - that we assume must have been sent to us (based on SequenceId) - which are lost due to network-caused packet loss.</summary>
            public double NetworkPacketLossPercent => NumPacketsReceived != 0 ? NumPacketsDroppedNeverArrived / (double) (NumPacketsReceived + NumPacketsDroppedNeverArrived) : 0;
            /// <summary>Percentage of all packets - that we assume must have been sent to us (based on SequenceId) - which are lost due to arriving out of order (and thus being culled).</summary>
            public double OutOfOrderPacketLossPercent => NumPacketsReceived != 0 ? NumPacketsCulledOutOfOrder / (double) (NumPacketsReceived + NumPacketsDroppedNeverArrived) : 0;
            /// <summary>Percentage of all packets - that we assume must have been sent to us (based on SequenceId) - which are dropped (for either reason).</summary>
            public double CombinedPacketLossPercent => NumPacketsReceived != 0 ? (NumPacketsDroppedNeverArrived + NumPacketsCulledOutOfOrder) / (double) (NumPacketsReceived + NumPacketsDroppedNeverArrived) : 0;

            /// <summary>Pretty-printed statistics.</summary>
            /// <returns>Pretty-printed statistics.</returns>
            public FixedString512Bytes ToFixedString() => $"USPS.Stats[sent: {NumPacketsSent}, recv:{NumPacketsReceived}, dropped:{NumPacketsDroppedNeverArrived} ({(int)(NetworkPacketLossPercent*100f)}%), outOfOrder:{NumPacketsCulledOutOfOrder} ({(int)(OutOfOrderPacketLossPercent*100f)}%), combined:{(int)(CombinedPacketLossPercent*100f)}%]";

            /// <inheritdoc cref="ToFixedString"/>
            public override string ToString() => ToFixedString().ToString();
        }
        
        static TransportFunctionPointer<NetworkPipelineStage.ReceiveDelegate> ReceiveFunctionPointer = new TransportFunctionPointer<NetworkPipelineStage.ReceiveDelegate>(Receive);
        static TransportFunctionPointer<NetworkPipelineStage.SendDelegate> SendFunctionPointer = new TransportFunctionPointer<NetworkPipelineStage.SendDelegate>(Send);
        static TransportFunctionPointer<NetworkPipelineStage.InitializeConnectionDelegate> InitializeConnectionFunctionPointer = new TransportFunctionPointer<NetworkPipelineStage.InitializeConnectionDelegate>(InitializeConnection);

        /// <inheritdoc/>
        public NetworkPipelineStage StaticInitialize(byte* staticInstanceBuffer, int staticInstanceBufferLength, NetworkSettings settings)
        {
            return new NetworkPipelineStage(
                Receive: ReceiveFunctionPointer,
                Send: SendFunctionPointer,
                InitializeConnection: InitializeConnectionFunctionPointer,
                ReceiveCapacity: UnsafeUtility.SizeOf<SequenceId>(),
                SendCapacity: UnsafeUtility.SizeOf<SequenceId>(),
                HeaderCapacity: UnsafeUtility.SizeOf<ushort>(),
                SharedStateCapacity: UnsafeUtility.SizeOf<Statistics>()
            );
        }

        /// <inheritdoc/>
        public int StaticSize => 0;

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(NetworkPipelineStage.ReceiveDelegate))]
        private static void Receive(ref NetworkPipelineContext ctx, ref InboundRecvBuffer inboundBuffer, ref NetworkPipelineStage.Requests requests, int systemHeaderSize)
        {
            var inboundArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(inboundBuffer.buffer, inboundBuffer.bufferLength, Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandle = AtomicSafetyHandle.GetTempMemoryHandle();
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref inboundArray, safetyHandle);
#endif
            var reader = new DataStreamReader(inboundArray);
            ushort sequenceId = reader.ReadUShort();
            
            var sequenceInstance = (SequenceId*)ctx.internalProcessBuffer;
            var stats = (Statistics*)ctx.internalSharedProcessBuffer;
            stats->NumPacketsReceived++;
            if (SequenceHelpers.GreaterThan16(sequenceId, sequenceInstance->Value))
            {
                // Get the delta between this packet, and the last packet received. Assume all of the ones between must have been dropped.
                stats->NumPacketsDroppedNeverArrived += (ulong)(SequenceHelpers.AbsDistance(sequenceId, sequenceInstance->Value)) - 1;
                sequenceInstance->Value = sequenceId;
                
                // Skip over the part of the buffer which contains the header
                inboundBuffer = inboundBuffer.Slice(sizeof(ushort));
                return;
            }

            // Drop (i.e. "cull") the packet.
            inboundBuffer = default;
            stats->NumPacketsCulledOutOfOrder++;
            // Technically a packet we skipped over was counted as dropped, but it just arrived.
            stats->NumPacketsDroppedNeverArrived--; 
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(NetworkPipelineStage.SendDelegate))]
        private static int Send(ref NetworkPipelineContext ctx, ref InboundSendBuffer inboundBuffer, ref NetworkPipelineStage.Requests requests, int systemHeaderSize)
        {
            var sequenceInstance = (SequenceId*)ctx.internalProcessBuffer;
            var stats = (Statistics*)ctx.internalSharedProcessBuffer;
            ctx.header.WriteUShort(sequenceInstance->Value);
            sequenceInstance->Value++;
            stats->NumPacketsSent++;
            
            return (int)Error.StatusCode.Success;
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(NetworkPipelineStage.InitializeConnectionDelegate))]
        private static void InitializeConnection(byte* staticInstanceBuffer, int staticInstanceBufferLength,
            byte* sendProcessBuffer, int sendProcessBufferLength, byte* recvProcessBuffer, int recvProcessBufferLength,
            byte* sharedProcessBuffer, int sharedProcessBufferLength)
        {
            if (recvProcessBufferLength != UnsafeUtility.SizeOf<SequenceId>())
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("recvProcessBufferLength is wrong length for UnreliableSequencedPipelineStage.SequenceId!");
#else
                return;
#endif
            }    
            if (sendProcessBufferLength != UnsafeUtility.SizeOf<SequenceId>())
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("sendProcessBufferLength is wrong length for UnreliableSequencedPipelineStage.SequenceId!");
#else
                return;
#endif
            }  
            if (sharedProcessBufferLength != UnsafeUtility.SizeOf<Statistics>())
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("sharedProcessBufferLength is wrong length for UnreliableSequencedPipelineStage.Statistics!");
#else
                return;
#endif
            }

            // The receive processing buffer contains the current sequence ID, initialize it to -1 as it will be incremented when used.
            var recv = (SequenceId*) recvProcessBuffer;
            *recv = new SequenceId
            {
                Value = 65535,
            };
        }
    }
}
