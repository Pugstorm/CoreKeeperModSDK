using System;
using AOT;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport.Utilities;
using Unity.Burst;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// This pipeline stage can be used to add artificial network conditions (packet loss, latency,
    /// etc.) to the traffic going through it. This is useful to test a game under conditions closer
    /// to what can be observed in real networks.
    /// </summary>
    [BurstCompile]
    public unsafe struct SimulatorPipelineStage : INetworkPipelineStage
    {
        static TransportFunctionPointer<NetworkPipelineStage.ReceiveDelegate> ReceiveFunctionPointer = new TransportFunctionPointer<NetworkPipelineStage.ReceiveDelegate>(Receive);
        static TransportFunctionPointer<NetworkPipelineStage.SendDelegate> SendFunctionPointer = new TransportFunctionPointer<NetworkPipelineStage.SendDelegate>(Send);
        static TransportFunctionPointer<NetworkPipelineStage.InitializeConnectionDelegate> InitializeConnectionFunctionPointer = new TransportFunctionPointer<NetworkPipelineStage.InitializeConnectionDelegate>(InitializeConnection);

        /// <inheritdoc/>
        public NetworkPipelineStage StaticInitialize(byte* staticInstanceBuffer, int staticInstanceBufferLength, NetworkSettings settings)
        {
            SimulatorUtility.Parameters param = settings.GetSimulatorStageParameters();
            var simulatorParamsSizeOf = UnsafeUtility.SizeOf<SimulatorUtility.Parameters>();
            if (simulatorParamsSizeOf != staticInstanceBufferLength) throw new InvalidOperationException($"simulatorParamsSizeOf {simulatorParamsSizeOf}");
            UnsafeUtility.MemCpy(staticInstanceBuffer, &param, simulatorParamsSizeOf);

            return new NetworkPipelineStage(
                Receive: ReceiveFunctionPointer,
                Send: SendFunctionPointer,
                InitializeConnection: InitializeConnectionFunctionPointer,
                ReceiveCapacity: param.MaxPacketCount * (param.MaxPacketSize + UnsafeUtility.SizeOf<SimulatorUtility.DelayedPacket>()),
                SendCapacity: param.MaxPacketCount * (param.MaxPacketSize + UnsafeUtility.SizeOf<SimulatorUtility.DelayedPacket>()),
                HeaderCapacity: 0,
                SharedStateCapacity: UnsafeUtility.SizeOf<SimulatorUtility.Context>()
            );
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(NetworkPipelineStage.InitializeConnectionDelegate))]
        static void InitializeConnection(byte* staticInstanceBuffer, int staticInstanceBufferLength,
            byte* sendProcessBuffer, int sendProcessBufferLength, byte* recvProcessBuffer, int recvProcessBufferLength,
            byte* sharedProcessBuffer, int sharedProcessBufferLength)
        {
            SimulatorUtility.Parameters param = default;
            UnsafeUtility.MemCpy(&param, staticInstanceBuffer, UnsafeUtility.SizeOf<SimulatorUtility.Parameters>());

            if (staticInstanceBufferLength != UnsafeUtility.SizeOf<SimulatorUtility.Parameters>())
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("staticInstanceBufferLength is wrong length for SimulatorUtility.Parameters!");
#else
                return;
#endif
            }

            if (sharedProcessBufferLength != UnsafeUtility.SizeOf<SimulatorUtility.Context>())
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("sharedProcessBufferLength is wrong length for SimulatorUtility.Context!");
#else
                return;
#endif
            }

            SimulatorUtility.InitializeContext(param, sharedProcessBuffer);
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(NetworkPipelineStage.SendDelegate))]
        static int Send(ref NetworkPipelineContext ctx, ref InboundSendBuffer inboundBuffer, ref NetworkPipelineStage.Requests requests, int systemHeaderSize)
        {
            var context = (SimulatorUtility.Context*)ctx.internalSharedProcessBuffer;
            var param = *(SimulatorUtility.Parameters*)ctx.staticInstanceBuffer;

            if (param.Mode == ApplyMode.ReceivedPacketsOnly || param.Mode == ApplyMode.Off)
                return (int)Error.StatusCode.Success;

            var inboundPacketSize = inboundBuffer.headerPadding + inboundBuffer.bufferLength;
            if (inboundPacketSize > param.MaxPacketSize)
            {
                DebugLog.SimulatorIncomingTooLarge(inboundBuffer.headerPadding + inboundBuffer.bufferLength, param.MaxPacketSize);
                return (int)Error.StatusCode.NetworkPacketOverflow;
            }

            var timestamp = ctx.timestamp;

            if (inboundBuffer.buffer != null)
            {
                context->PacketCount++;

                if (SimulatorUtility.ShouldDropPacket(context, param, timestamp))
                {
                    inboundBuffer = default;
                    return (int)Error.StatusCode.Success;
                }

                if (param.FuzzFactor > 0)
                {
                    SimulatorUtility.FuzzPacket(context, ref param, ref inboundBuffer);
                }

                if (SimulatorUtility.ShouldDuplicatePacket(context, ref param))
                {
                    if (SimulatorUtility.TryDelayPacket(ref ctx, ref param, ref inboundBuffer, ref requests, timestamp))
                    {
                        context->PacketCount++;
                    }
                }

                if (SimulatorUtility.TrySkipDelayingPacket(ref param, ref requests, context) || !SimulatorUtility.TryDelayPacket(ref ctx, ref param, ref inboundBuffer, ref requests, timestamp))
                {
                    return (int)Error.StatusCode.Success;
                }
            }

            InboundSendBuffer returnPacket = default;
            if (SimulatorUtility.GetDelayedPacket(ref ctx, ref returnPacket, ref requests, timestamp))
            {
                inboundBuffer = returnPacket;
                return (int)Error.StatusCode.Success;
            }

            inboundBuffer = default;
            return (int)Error.StatusCode.Success;
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(NetworkPipelineStage.ReceiveDelegate))]
        static void Receive(ref NetworkPipelineContext ctx, ref InboundRecvBuffer inboundBuffer, ref NetworkPipelineStage.Requests requests, int systemHeaderSize)
        {
            var context = (SimulatorUtility.Context*)ctx.internalSharedProcessBuffer;
            var param = *(SimulatorUtility.Parameters*)ctx.staticInstanceBuffer;

            if (param.Mode == ApplyMode.SentPacketsOnly || param.Mode == ApplyMode.Off)
                return;

            if (inboundBuffer.bufferLength > param.MaxPacketSize)
            {
                DebugLog.SimulatorIncomingTooLarge(inboundBuffer.bufferLength, param.MaxPacketSize);
                return;
            }

            var timestamp = ctx.timestamp;

            // Inbound buffer is empty if this is a resumed receive
            if (inboundBuffer.buffer != null)
            {
                context->PacketCount++;

                if (SimulatorUtility.ShouldDropPacket(context, param, timestamp))
                {
                    inboundBuffer = default;
                    return;
                }

                var bufferVec = default(InboundSendBuffer);
                bufferVec.bufferWithHeaders = inboundBuffer.buffer;
                bufferVec.bufferWithHeadersLength = inboundBuffer.bufferLength;
                bufferVec.buffer = inboundBuffer.buffer;
                bufferVec.bufferLength = inboundBuffer.bufferLength;
                bufferVec.headerPadding = 0;


                if (SimulatorUtility.ShouldDuplicatePacket(context, ref param))
                {
                    if (SimulatorUtility.TryDelayPacket(ref ctx, ref param, ref bufferVec, ref requests, timestamp))
                    {
                        context->PacketCount++;
                    }
                }

                if (SimulatorUtility.TrySkipDelayingPacket(ref param, ref requests, context) || !SimulatorUtility.TryDelayPacket(ref ctx, ref param, ref bufferVec, ref requests, timestamp))
                {
                    return;
                }
            }

            InboundSendBuffer returnPacket = default;
            if (SimulatorUtility.GetDelayedPacket(ref ctx, ref returnPacket, ref requests, timestamp))
            {
                inboundBuffer.buffer = returnPacket.bufferWithHeaders;
                inboundBuffer.bufferLength = returnPacket.bufferWithHeadersLength;
                return;
            }

            inboundBuffer = default;
        }

        /// <inheritdoc/>
        public int StaticSize => UnsafeUtility.SizeOf<SimulatorUtility.Parameters>();
    }

    /// <summary>
    /// Obsolete. Use <see cref="SimulatorPipelineStage"/> with an <see cref="ApplyMode"/>
    /// configured for sending instead.
    /// </summary>
    [BurstCompile]
    [Obsolete("Use SimulatorPipelineStage with an ApplyMode set for sending instead.")]
    public unsafe struct SimulatorPipelineStageInSend : INetworkPipelineStage
    {
        static TransportFunctionPointer<NetworkPipelineStage.ReceiveDelegate> ReceiveFunctionPointer = new TransportFunctionPointer<NetworkPipelineStage.ReceiveDelegate>(Receive);
        static TransportFunctionPointer<NetworkPipelineStage.SendDelegate> SendFunctionPointer = new TransportFunctionPointer<NetworkPipelineStage.SendDelegate>(Send);
        static TransportFunctionPointer<NetworkPipelineStage.InitializeConnectionDelegate> InitializeConnectionFunctionPointer = new TransportFunctionPointer<NetworkPipelineStage.InitializeConnectionDelegate>(InitializeConnection);
        public NetworkPipelineStage StaticInitialize(byte* staticInstanceBuffer, int staticInstanceBufferLength, NetworkSettings settings)
        {
            SimulatorUtility.Parameters param = settings.GetSimulatorStageParameters();

            UnsafeUtility.MemCpy(staticInstanceBuffer, &param, UnsafeUtility.SizeOf<SimulatorUtility.Parameters>());

            return new NetworkPipelineStage(
                Receive: ReceiveFunctionPointer,
                Send: SendFunctionPointer,
                InitializeConnection: InitializeConnectionFunctionPointer,
                ReceiveCapacity: 0,
                SendCapacity: param.MaxPacketCount * (param.MaxPacketSize + UnsafeUtility.SizeOf<SimulatorUtility.DelayedPacket>()),
                HeaderCapacity: 0,
                SharedStateCapacity: UnsafeUtility.SizeOf<SimulatorUtility.Context>()
            );
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(NetworkPipelineStage.InitializeConnectionDelegate))]
        static void InitializeConnection(byte* staticInstanceBuffer, int staticInstanceBufferLength,
            byte* sendProcessBuffer, int sendProcessBufferLength, byte* recvProcessBuffer, int recvProcessBufferLength,
            byte* sharedProcessBuffer, int sharedProcessBufferLength)
        {
            SimulatorUtility.Parameters param = default;
            UnsafeUtility.MemCpy(&param, staticInstanceBuffer, UnsafeUtility.SizeOf<SimulatorUtility.Parameters>());

            if (staticInstanceBufferLength != UnsafeUtility.SizeOf<SimulatorUtility.Parameters>())
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("staticInstanceBufferLength is wrong length for SimulatorUtility.Parameters!");
#else
                return;
#endif
            }

            if (sharedProcessBufferLength != UnsafeUtility.SizeOf<SimulatorUtility.Context>())
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("sharedProcessBufferLength is wrong length for SimulatorUtility.Context!");
#else
                return;
#endif
            }

            if (param.Mode == ApplyMode.AllPackets)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("SimulatorPipelineStageInSend applies to sent packets only, and is deprecated. Please use SimulatorPipleineStage with SimulatorUtility.Parameters.Mode = ApplyMode.AllPackets.");
#else
                return;
#endif
            }

            SimulatorUtility.InitializeContext(param, sharedProcessBuffer);
        }

        // This is a copy/paste duplication of SimulatorPipelineStage.Send as this class is now deprecated.
        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(NetworkPipelineStage.SendDelegate))]
        static int Send(ref NetworkPipelineContext ctx, ref InboundSendBuffer inboundBuffer, ref NetworkPipelineStage.Requests requests, int systemHeaderSize)
        {
            var context = (SimulatorUtility.Context*)ctx.internalSharedProcessBuffer;
            var param = *(SimulatorUtility.Parameters*)ctx.staticInstanceBuffer;
            if (param.Mode == ApplyMode.ReceivedPacketsOnly || param.Mode == ApplyMode.Off)
                return (int)Error.StatusCode.Success;

            var inboundPacketSize = inboundBuffer.headerPadding + inboundBuffer.bufferLength;
            if (inboundPacketSize > param.MaxPacketSize)
            {
                DebugLog.SimulatorIncomingTooLarge(inboundBuffer.headerPadding + inboundBuffer.bufferLength, param.MaxPacketSize);
                return (int)Error.StatusCode.NetworkPacketOverflow;
            }

            var timestamp = ctx.timestamp;

            if (inboundBuffer.buffer != null)
            {
                context->PacketCount++;

                if (SimulatorUtility.ShouldDropPacket(context, param, timestamp))
                {
                    inboundBuffer = default;
                    return (int)Error.StatusCode.Success;
                }

                if (param.FuzzFactor > 0)
                {
                    SimulatorUtility.FuzzPacket(context, ref param, ref inboundBuffer);
                }

                if (SimulatorUtility.ShouldDuplicatePacket(context, ref param))
                {
                    if (SimulatorUtility.TryDelayPacket(ref ctx, ref param, ref inboundBuffer, ref requests, timestamp))
                    {
                        context->PacketCount++;
                    }
                }

                if (SimulatorUtility.TrySkipDelayingPacket(ref param, ref requests, context) || !SimulatorUtility.TryDelayPacket(ref ctx, ref param, ref inboundBuffer, ref requests, timestamp))
                {
                    return (int)Error.StatusCode.Success;
                }
            }

            InboundSendBuffer returnPacket = default;
            if (SimulatorUtility.GetDelayedPacket(ref ctx, ref returnPacket, ref requests, timestamp))
            {
                inboundBuffer = returnPacket;
                return (int)Error.StatusCode.Success;
            }
            inboundBuffer = default;
            return (int)Error.StatusCode.Success;
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(NetworkPipelineStage.ReceiveDelegate))]
        static void Receive(ref NetworkPipelineContext ctx, ref InboundRecvBuffer inboundBuffer,
            ref NetworkPipelineStage.Requests requests, int systemHeaderSize)
        {
        }

        public int StaticSize => UnsafeUtility.SizeOf<SimulatorUtility.Parameters>();
    }
}
