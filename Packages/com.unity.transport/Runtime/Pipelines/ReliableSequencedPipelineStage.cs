using System;
using AOT;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport.Utilities;
using Unity.Burst;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// <para>
    /// This pipeline stage can be used to ensure that packets sent through it will be delivered,
    /// and will be delivered in order. This is done by sending acknowledgements for received
    /// packets, and resending packets that have not been acknowledged in a while.
    /// </para>
    /// <para>
    /// Note that a consequence of these guarantees is that if a packet is lost, subsequent packets
    /// will not be delivered until the lost packet has been resent and delivered. This is called
    /// <see href="https://en.wikipedia.org/wiki/Head-of-line_blocking">head-of-line blocking</see>
    /// and can add significant latency to delivered packets when it occurs. For this reason, only
    /// send through this pipeline traffic which must absolutely be delivered in order (e.g. RPCs
    /// or player actions). State updates that will be resent later anyway (e.g. snapshots) should
    /// not be sent through this pipeline stage.
    /// </para>
    /// <para>
    /// Another reason to limit the amount of traffic sent through this pipeline is because it has
    /// limited bandwidth. Because of the need to keep packets around in case they need to be
    /// resent, only a limited number of packets can be in-flight at a time. This limit, called the
    /// window size, is 32 by default and can be increased to 64. See the documentation on pipelines
    /// for further details.
    /// </para>
    /// </summary>
    [BurstCompile]
    public unsafe struct ReliableSequencedPipelineStage : INetworkPipelineStage
    {
        static TransportFunctionPointer<NetworkPipelineStage.ReceiveDelegate> ReceiveFunctionPointer = new TransportFunctionPointer<NetworkPipelineStage.ReceiveDelegate>(Receive);
        static TransportFunctionPointer<NetworkPipelineStage.SendDelegate> SendFunctionPointer = new TransportFunctionPointer<NetworkPipelineStage.SendDelegate>(Send);
        static TransportFunctionPointer<NetworkPipelineStage.InitializeConnectionDelegate> InitializeConnectionFunctionPointer = new TransportFunctionPointer<NetworkPipelineStage.InitializeConnectionDelegate>(InitializeConnection);

        /// <inheritdoc/>
        public NetworkPipelineStage StaticInitialize(byte* staticInstanceBuffer, int staticInstanceBufferLength, NetworkSettings settings)
        {
            ReliableUtility.Parameters param = settings.GetReliableStageParameters();
            UnsafeUtility.MemCpy(staticInstanceBuffer, &param, UnsafeUtility.SizeOf<ReliableUtility.Parameters>());
            return new NetworkPipelineStage(
                Receive: ReceiveFunctionPointer,
                Send: SendFunctionPointer,
                InitializeConnection: InitializeConnectionFunctionPointer,
                ReceiveCapacity: ReliableUtility.ProcessCapacityNeeded(param),
                SendCapacity: ReliableUtility.ProcessCapacityNeeded(param),
                HeaderCapacity: ReliableUtility.PacketHeaderWireSize(param.WindowSize),
                SharedStateCapacity: ReliableUtility.SharedCapacityNeeded(param)
            );
        }

        /// <inheritdoc/>
        public int StaticSize => UnsafeUtility.SizeOf<ReliableUtility.Parameters>();

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(NetworkPipelineStage.ReceiveDelegate))]
        private static void Receive(ref NetworkPipelineContext ctx, ref InboundRecvBuffer inboundBuffer, ref NetworkPipelineStage.Requests requests, int systemHeaderSize)
        {
            // Request a send update to see if a queued packet needs to be resent later or if an ack packet should be sent
            requests = NetworkPipelineStage.Requests.SendUpdate;
            bool needsResume = false;

            var header = default(ReliableUtility.PacketHeader);
            var slice = default(InboundRecvBuffer);
            ReliableUtility.Context* reliable = (ReliableUtility.Context*)ctx.internalProcessBuffer;
            ReliableUtility.SharedContext* shared = (ReliableUtility.SharedContext*)ctx.internalSharedProcessBuffer;

            if (reliable->Resume == ReliableUtility.NullEntry)
            {
                if (inboundBuffer.buffer == null)
                {
                    inboundBuffer = slice;
                    return;
                }
                var inboundArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(inboundBuffer.buffer, inboundBuffer.bufferLength, Allocator.Invalid);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var safetyHandle = AtomicSafetyHandle.GetTempMemoryHandle();
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref inboundArray, safetyHandle);
#endif
                var reader = new DataStreamReader(inboundArray);
                reader.ReadBytesUnsafe((byte*)&header, ReliableUtility.PacketHeaderWireSize(ctx));

                if (header.Type == (ushort)ReliableUtility.PacketType.Ack)
                {
                    ReliableUtility.ReadAckPacket(ctx, header);
                    inboundBuffer = default;
                    return;
                }

                var result = ReliableUtility.Read(ctx, header);

                if (result >= 0)
                {
                    var nextExpectedSequenceId = (ushort)(reliable->Delivered + 1);
                    if (result == nextExpectedSequenceId)
                    {
                        reliable->Delivered = result;
                        slice = inboundBuffer.Slice(ReliableUtility.PacketHeaderWireSize(ctx));

                        if (needsResume = SequenceHelpers.GreaterThan16((ushort)shared->ReceivedPackets.Sequence, (ushort)result))
                        {
                            reliable->Resume = (ushort)(result + 1);
                        }
                    }
                    else
                    {
                        ReliableUtility.SetPacket(ctx.internalProcessBuffer, result, inboundBuffer.Slice(ReliableUtility.PacketHeaderWireSize(ctx)));
                        slice = ReliableUtility.ResumeReceive(ctx, reliable->Delivered + 1, ref needsResume);
                    }
                }
            }
            else
            {
                slice = ReliableUtility.ResumeReceive(ctx, reliable->Resume, ref needsResume);
            }
            if (needsResume)
                requests |= NetworkPipelineStage.Requests.Resume;
            inboundBuffer = slice;
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(NetworkPipelineStage.SendDelegate))]
        private static int Send(ref NetworkPipelineContext ctx, ref InboundSendBuffer inboundBuffer, ref NetworkPipelineStage.Requests requests, int systemHeaderSize)
        {
            // Request an update to see if a queued packet needs to be resent later or if an ack packet should be sent
            requests = NetworkPipelineStage.Requests.Update;

            var header = new ReliableUtility.PacketHeader();
            var reliable = (ReliableUtility.Context*)ctx.internalProcessBuffer;

            // Release any packets that might have been acknowledged since the last call.
            ReliableUtility.ReleaseAcknowledgedPackets(ctx);

            if (inboundBuffer.buffer != null)
            {
                reliable->LastSentTime = ctx.timestamp;

                if (ReliableUtility.Write(ctx, inboundBuffer, ref header) < 0)
                {
                    // We failed to store the packet for possible later resends, abort and report this as a send error
                    inboundBuffer = default;
                    requests |= NetworkPipelineStage.Requests.Error;
                    return (int)Error.StatusCode.NetworkSendQueueFull;
                }

                ctx.header.Clear();
                ctx.header.WriteBytesUnsafe((byte*)&header, ReliableUtility.PacketHeaderWireSize(ctx));
                reliable->PreviousTimestamp = ctx.timestamp;
                return (int)Error.StatusCode.Success;
            }

            // At this point we know we're either in a resume or update call.

            if (reliable->Resume != ReliableUtility.NullEntry)
            {
                reliable->LastSentTime = ctx.timestamp;
                inboundBuffer = ReliableUtility.ResumeSend(ctx, out header);

                // Check if we need to resume again after this packet.
                reliable->Resume = ReliableUtility.GetNextSendResumeSequence(ctx);
                if (reliable->Resume != ReliableUtility.NullEntry)
                    requests |= NetworkPipelineStage.Requests.Resume;

                ctx.header.Clear();
                ctx.header.WriteBytesUnsafe((byte*)&header, ReliableUtility.PacketHeaderWireSize(ctx));
                reliable->PreviousTimestamp = ctx.timestamp;
                return (int)Error.StatusCode.Success;
            }

            // At this point we know we're in an update call.

            // Check if we need to resume (e.g. resend packets).
            reliable->Resume = ReliableUtility.GetNextSendResumeSequence(ctx);
            if (reliable->Resume != ReliableUtility.NullEntry)
                requests |= NetworkPipelineStage.Requests.Resume;

            if (ReliableUtility.ShouldSendAck(ctx))
            {
                reliable->LastSentTime = ctx.timestamp;

                ReliableUtility.WriteAckPacket(ctx, ref header);

                ctx.header.WriteBytesUnsafe((byte*)&header, ReliableUtility.PacketHeaderWireSize(ctx));
                reliable->PreviousTimestamp = ctx.timestamp;

                // TODO: Sending dummy byte over since the pipeline won't send an empty payload (ignored on receive)
                inboundBuffer.bufferWithHeadersLength = inboundBuffer.headerPadding + 1;
                inboundBuffer.bufferWithHeaders = (byte*)UnsafeUtility.Malloc(inboundBuffer.bufferWithHeadersLength, 8, Allocator.Temp);
                inboundBuffer.SetBufferFromBufferWithHeaders();
                return (int)Error.StatusCode.Success;
            }

            reliable->PreviousTimestamp = ctx.timestamp;
            return (int)Error.StatusCode.Success;
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(NetworkPipelineStage.InitializeConnectionDelegate))]
        private static void InitializeConnection(byte* staticInstanceBuffer, int staticInstanceBufferLength,
            byte* sendProcessBuffer, int sendProcessBufferLength, byte* recvProcessBuffer, int recvProcessBufferLength,
            byte* sharedProcessBuffer, int sharedProcessBufferLength)
        {
            ReliableUtility.Parameters param;
            UnsafeUtility.MemCpy(&param, staticInstanceBuffer, UnsafeUtility.SizeOf<ReliableUtility.Parameters>());

            if (sharedProcessBufferLength != ReliableUtility.SharedCapacityNeeded(param))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("sharedProcessBufferLength is wrong length for ReliableUtility.Parameters!");
#else
                return;
#endif
            }

            if (sendProcessBufferLength + recvProcessBufferLength < ReliableUtility.ProcessCapacityNeeded(param) * 2)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("sendProcessBufferLength + recvProcessBufferLength is wrong length for ReliableUtility.ProcessCapacityNeeded!");
#else
                return;
#endif
            }

            ReliableUtility.InitializeContext(sharedProcessBuffer, sharedProcessBufferLength, sendProcessBuffer, sendProcessBufferLength, recvProcessBuffer, recvProcessBufferLength, param);
        }
    }
}
