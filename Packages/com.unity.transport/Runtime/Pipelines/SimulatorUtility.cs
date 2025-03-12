using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Networking.Transport.Logging;
using Random = Unity.Mathematics.Random;

namespace Unity.Networking.Transport.Utilities
{
    /// <summary>Extensions for <see cref="SimulatorUtility.Parameters"/>.</summary>
    public static class SimulatorStageParameterExtensions
    {
        /// <summary>
        /// Sets the <see cref="SimulatorUtility.Parameters"/> in the settings.
        /// </summary>
        /// <param name="settings">Settings to modify.</param>
        /// <param name="maxPacketCount">
        /// The maximum amount of packets the pipeline can keep track of. This used when a
        /// packet is delayed, the packet is stored in the pipeline processing buffer and can
        /// be later brought back.
        /// </param>
        /// <param name="maxPacketSize">
        /// The maximum size of a packet which the simulator stores. If a packet exceeds this
        /// size it will bypass the simulator.
        /// </param>
        /// <param name="mode">
        /// Whether to apply simulation to received or sent packets (defaults to both).
        /// </param>
        /// <param name="packetDelayMs">
        /// Fixed delay in milliseconds to apply to all packets which pass through.
        /// </param>
        /// <param name="packetJitterMs">
        /// Variance of the delay that gets added to all packets that pass through. For example,
        /// setting this value to 5 will result in the delay being a random value within 5
        /// milliseconds of the value set with <c>PacketDelayMs</c>.
        /// </param>
        /// <param name="packetDropInterval">
        /// Fixed interval to drop packets on. This is most suitable for tests where predictable
        /// behaviour is desired, as every X-th packet will be dropped. For example, if the
        /// value is 5 every fifth packet is dropped.
        /// </param>
        /// <param name="packetDropPercentage">Percentage of packets that will be dropped.</param>
        /// <param name="packetDuplicationPercentage">
        /// Percentage of packets that will be duplicated. Packets are duplicated at most once
        /// and will not be duplicated if they were first deemed to be dropped.
        /// </param>
        /// <param name="fuzzFactor">
        /// See <see cref="SimulatorUtility.Parameters.FuzzFactor"/> for details.
        /// </param>
        /// <param name="fuzzOffset">
        /// To be used along the fuzz factor. The offset is the offset inside the packet where
        /// fuzzing should start. Useful to avoid fuzzing headers for example.
        /// </param>
        /// <param name="randomSeed">
        /// Value to use to seed the random number generator. For non-deterministic behavior, use a
        /// dynamic value here (e.g. the result of a call to <c>Stopwatch.GetTimestamp</c>).
        /// </param>
        /// <returns>Settings structure with modified values.</returns>
        public static ref NetworkSettings WithSimulatorStageParameters(
            ref this NetworkSettings settings,
            int maxPacketCount,
            int maxPacketSize = NetworkParameterConstants.AbsoluteMaxMessageSize,
            ApplyMode mode = ApplyMode.AllPackets,
            int packetDelayMs = 0,
            int packetJitterMs = 0,
            int packetDropInterval = 0,
            int packetDropPercentage = 0,
            int packetDuplicationPercentage = 0,
            int fuzzFactor = 0,
            int fuzzOffset = 0,
            uint randomSeed = 0
        )
        {
            var parameter = new SimulatorUtility.Parameters
            {
                MaxPacketCount = maxPacketCount,
                MaxPacketSize = maxPacketSize,
                Mode = mode,
                PacketDelayMs = packetDelayMs,
                PacketJitterMs = packetJitterMs,
                PacketDropInterval = packetDropInterval,
                PacketDropPercentage = packetDropPercentage,
                PacketDuplicationPercentage = packetDuplicationPercentage,
                FuzzFactor = fuzzFactor,
                FuzzOffset = fuzzOffset,
                RandomSeed = randomSeed,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>
        /// Gets the <see cref="SimulatorUtility.Parameters"/> in the settings.
        /// </summary>
        /// <param name="settings">Settings to get parameters from.</param>
        /// <returns>Structure containing the simulator parameters.</returns>
        public static SimulatorUtility.Parameters GetSimulatorStageParameters(ref this NetworkSettings settings)
        {
            settings.TryGet<SimulatorUtility.Parameters>(out var parameters);
            return parameters;
        }

        // TODO This ModifySimulatorStageParameters() extension method is NOT a pattern we want
        //      repeated throughout the code. At some point we'll want to deprecate it and replace
        //      it with a proper general mechanism to modify settings at runtime (see MTT-4161).

        /// <summary>Modify the parameters of the simulator pipeline stage.</summary>
        /// <remarks>
        /// Some parameters (e.g. max packet count and size) are not modifiable. These need to be
        /// passed unmodified to this function (can't just leave them at 0). The current parameters
        /// can be obtained using <see cref="NetworkDriver.CurrentSettings" />.
        /// </remarks>
        /// <param name="driver">Driver to modify.</param>
        /// <param name="newParams">New parameters for the simulator stage.</param>
        public static unsafe void ModifySimulatorStageParameters(this NetworkDriver driver, SimulatorUtility.Parameters newParams)
        {
            var stageId = NetworkPipelineStageId.Get<SimulatorPipelineStage>();
            var currentParams = driver.GetWriteablePipelineParameter<SimulatorUtility.Parameters>(default, stageId);

            if (currentParams->MaxPacketCount != newParams.MaxPacketCount)
            {
                DebugLog.LogError("Simulator stage maximum packet count can't be modified.");
                return;
            }

            if (currentParams->MaxPacketSize != newParams.MaxPacketSize)
            {
                DebugLog.LogError("Simulator stage maximum packet size can't be modified.");
                return;
            }

            *currentParams = newParams;
            driver.m_NetworkSettings.AddRawParameterStruct(ref newParams);
        }
    }

    /// <summary>
    /// Denotes whether or not the <see cref="SimulatorPipelineStage"/> should apply to sent or
    /// received packets (or both).
    /// </summary>
    public enum ApplyMode : byte
    {
        // We put received packets first so that the default value will match old behavior.
        /// <summary>Only apply simulator pipeline to received packets.</summary>
        ReceivedPacketsOnly,
        /// <summary>Only apply simulator pipeline to sent packets.</summary>
        SentPacketsOnly,
        /// <summary>Apply simulator pipeline to both sent and received packets.</summary>
        AllPackets,
        /// <summary>Don't apply the simulator pipeline. Used for runtime toggling.</summary>
        Off,
    }

    /// <summary>Utility types for the <see cref="SimulatorPipelineStage"/>.</summary>
    public static class SimulatorUtility
    {
        /// <summary>
        /// Configuration parameters for the simulator pipeline stage.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Parameters : INetworkParameter
        {
            /// <summary>
            /// The maximum amount of packets the pipeline can keep track of. This used when a
            /// packet is delayed, the packet is stored in the pipeline processing buffer and can
            /// be later brought back.
            /// </summary>
            /// <value>Number of packets.</value>
            public int MaxPacketCount;

            /// <summary>
            /// The maximum size of a packet which the simulator stores. If a packet exceeds this
            /// size it will bypass the simulator.
            /// </summary>
            /// <value>Packet size in bytes.</value>
            public int MaxPacketSize;

            /// <summary>
            /// Value to use to seed the random number generator. For non-deterministic behavior, use a
            /// dynamic value here (e.g. the result of a call to <c>Stopwatch.GetTimestamp</c>).
            /// </summary>
            /// <value>Seed for the RNG.</value>
            public uint RandomSeed;

            /// <inheritdoc cref="ApplyMode"/>
            public ApplyMode Mode;

            /// <summary>Fixed delay to apply to all packets which pass through.</summary>
            /// <value>Delay in milliseconds.</value>
            public int PacketDelayMs;

            /// <summary>
            /// Variance of the delay that gets added to all packets that pass through. For example,
            /// setting this value to 5 will result in the delay being a random value within 5
            /// milliseconds of the value set with <see cref="PacketDelayMs"/>.
            /// </summary>
            /// <value>Jitter in milliseconds.</value>
            public int PacketJitterMs;

            /// <summary>
            /// Fixed interval to drop packets on. This is most suitable for tests where predictable
            /// behaviour is desired, as every X-th packet will be dropped. For example, if the
            /// value is 5 every fifth packet is dropped.
            /// </summary>
            /// <value>Interval in number of packets.</value>
            public int PacketDropInterval;

            /// <summary>Percentage of packets that will be dropped.</summary>
            /// <value>Percentage (0-100).</value>
            public int PacketDropPercentage;

            /// <summary>
            /// Percentage of packets that will be duplicated. Packets are duplicated at most once
            /// and will not be duplicated if they were first deemed to be dropped.
            /// </summary>
            /// <value>Percentage (0-100).</value>
            public int PacketDuplicationPercentage;

            /// <summary>
            /// The fuzz factor is a percentage that represents both the proportion of packets that
            /// should be fuzzed, and the probability of any bit being flipped in the packet. For
            /// example, a value of 5 means about 5% of packets will be modified, and for each
            /// packet modified, each bit has a 5% chance of being flipped.
            /// </summary>
            /// <remarks>
            /// The presence of this parameter in the simulator pipeline stage should not be
            /// understood as this being a condition that should be tested against when doing
            /// network simulations. In real networks, corrupted packets basically never make it all
            /// the way to the user (they'll get dropped before that). This parameter is mostly
            /// useful to test a netcode solution against maliciously-crafted packets.
            /// </remarks>
            /// <value>Percentage (0-100).</value>
            public int FuzzFactor;

            /// <summary>
            /// To be used along the fuzz factor. The offset is the offset inside the packet where
            /// fuzzing should start. Useful to avoid fuzzing headers for example.
            /// </summary>
            /// <value>Offset in bytes.</value>
            public int FuzzOffset;

            /// <inheritdoc/>
            public bool Validate() => true;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Context
        {
            public Random Random;
            public int PacketCount;
            public int ReadyPackets;
            public int WaitingPackets;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DelayedPacket
        {
            public int processBufferOffset;
            public ushort packetSize;
            public ushort packetHeaderPadding;
            public long delayUntil;
        }

        internal static unsafe void InitializeContext(Parameters param, byte* sharedProcessBuffer)
        {
            // Store parameters in the shared buffer space
            Context* ctx = (Context*)sharedProcessBuffer;
            ctx->Random = new Random();
            if (param.RandomSeed > 0)
                ctx->Random.InitState(param.RandomSeed);
            else
                ctx->Random.InitState();
        }

        private static unsafe bool GetEmptyDataSlot(NetworkPipelineContext ctx, byte* processBufferPtr, ref int packetPayloadOffset,
            ref int packetDataOffset)
        {
            var param = *(Parameters*)ctx.staticInstanceBuffer;
            var dataSize = UnsafeUtility.SizeOf<DelayedPacket>();
            var packetPayloadStartOffset = param.MaxPacketCount * dataSize;

            bool foundSlot = false;
            for (int i = 0; i < param.MaxPacketCount; i++)
            {
                packetDataOffset = dataSize * i;
                DelayedPacket* packetData = (DelayedPacket*)(processBufferPtr + packetDataOffset);

                // Check if this slot is empty
                if (packetData->delayUntil == 0)
                {
                    foundSlot = true;
                    packetPayloadOffset = packetPayloadStartOffset + param.MaxPacketSize * i;
                    break;
                }
            }

            return foundSlot;
        }

        internal static unsafe bool GetDelayedPacket(ref NetworkPipelineContext ctx, ref InboundSendBuffer delayedPacket,
            ref NetworkPipelineStage.Requests requests, long currentTimestamp)
        {
            requests = NetworkPipelineStage.Requests.None;
            var param = *(Parameters*)ctx.staticInstanceBuffer;

            var dataSize = UnsafeUtility.SizeOf<DelayedPacket>();
            byte* processBufferPtr = (byte*)ctx.internalProcessBuffer;
            var simCtx = (Context*)ctx.internalSharedProcessBuffer;
            int oldestPacketIndex = -1;
            long oldestTime = long.MaxValue;
            int readyPackets = 0;
            int packetsInQueue = 0;
            for (int i = 0; i < param.MaxPacketCount; i++)
            {
                DelayedPacket* packet = (DelayedPacket*)(processBufferPtr + dataSize * i);
                if ((int)packet->delayUntil == 0) continue;
                packetsInQueue++;

                if (packet->delayUntil > currentTimestamp) continue;
                readyPackets++;

                if (oldestTime <= packet->delayUntil) continue;
                oldestPacketIndex = i;
                oldestTime = packet->delayUntil;
            }

            simCtx->WaitingPackets = packetsInQueue;

            // If more than one item has expired timer we need to resume this pipeline stage
            if (readyPackets > 1)
            {
                requests |= NetworkPipelineStage.Requests.Resume;
            }
            // If more than one item is present (but doesn't have expired timer) we need to re-run the pipeline
            // in a later update call
            else if (packetsInQueue > 0)
            {
                requests |= NetworkPipelineStage.Requests.Update;
            }

            if (oldestPacketIndex >= 0)
            {
                DelayedPacket* packet = (DelayedPacket*)(processBufferPtr + dataSize * oldestPacketIndex);
                packet->delayUntil = 0;

                delayedPacket.bufferWithHeaders = ctx.internalProcessBuffer + packet->processBufferOffset;
                delayedPacket.bufferWithHeadersLength = packet->packetSize;
                delayedPacket.headerPadding = packet->packetHeaderPadding;
                delayedPacket.SetBufferFromBufferWithHeaders();
                return true;
            }

            return false;
        }

        internal static unsafe void FuzzPacket(Context *ctx, ref Parameters param, ref InboundSendBuffer inboundBuffer)
        {
            int fuzzFactor = param.FuzzFactor;
            int fuzzOffset = param.FuzzOffset;
            int rand = ctx->Random.NextInt(0, 100);
            if (rand > fuzzFactor)
                return;

            var length = inboundBuffer.bufferLength;
            for (int i = fuzzOffset; i < length; ++i)
            {
                for (int j = 0; j < 8; ++j)
                {
                    if (fuzzFactor > ctx->Random.NextInt(0, 100))
                    {
                        inboundBuffer.buffer[i] ^= (byte)(1 << j);
                    }
                }
            }
        }

        /// <summary>Storing it twice will trigger a resend.</summary>
        internal static unsafe bool TryDelayPacket(ref NetworkPipelineContext ctx, ref Parameters param, ref InboundSendBuffer inboundBuffer,
            ref NetworkPipelineStage.Requests requests,
            long timestamp)
        {
            var simCtx = (Context*)ctx.internalSharedProcessBuffer;

            // Find empty slot in bookkeeping data space to track this packet
            int packetPayloadOffset = 0;
            int packetDataOffset = 0;
            var processBufferPtr = (byte*)ctx.internalProcessBuffer;
            bool foundSlot = GetEmptyDataSlot(ctx, processBufferPtr, ref packetPayloadOffset, ref packetDataOffset);

            if (!foundSlot)
            {
                DebugLog.SimulatorNoSpace(param.MaxPacketCount);
                return false;
            }

            UnsafeUtility.MemCpy(ctx.internalProcessBuffer + packetPayloadOffset + inboundBuffer.headerPadding, inboundBuffer.buffer, inboundBuffer.bufferLength);

            // Add tracking for this packet so we can resurrect later
            DelayedPacket packet;
            var addedDelay = math.max(0, param.PacketDelayMs + simCtx->Random.NextInt(param.PacketJitterMs * 2) - param.PacketJitterMs);
            packet.delayUntil = timestamp + addedDelay;
            packet.processBufferOffset = packetPayloadOffset;
            packet.packetSize = (ushort)(inboundBuffer.headerPadding + inboundBuffer.bufferLength);
            packet.packetHeaderPadding = (ushort)inboundBuffer.headerPadding;
            byte* packetPtr = (byte*)&packet;
            UnsafeUtility.MemCpy(processBufferPtr + packetDataOffset, packetPtr, UnsafeUtility.SizeOf<DelayedPacket>());

            // Schedule an update call so packet can be resurrected later
            requests |= NetworkPipelineStage.Requests.Update;
            return true;
        }

        /// <summary>
        /// Optimization.
        /// We want to skip <see cref="TryDelayPacket"/> in the case where we have no delay to avoid mem-copies.
        /// Also ensures requests are updated if there are other packets in the store.
        /// </summary>
        /// <returns>True if we can skip delaying this packet.</returns>
        internal static unsafe bool TrySkipDelayingPacket(ref Parameters param, ref NetworkPipelineStage.Requests requests, Context* simCtx)
        {
            if (param.PacketDelayMs == 0 && param.PacketJitterMs == 0)
            {
                if (simCtx->WaitingPackets > 0)
                    requests |= NetworkPipelineStage.Requests.Update;
                return true;
            }
            return false;
        }

        internal static unsafe bool ShouldDropPacket(Context* ctx, Parameters param, long timestamp)
        {
            if (param.PacketDropInterval > 0 && ((ctx->PacketCount - 1) % param.PacketDropInterval) == 0)
                return true;
            if (param.PacketDropPercentage > 0)
            {
                var chance = ctx->Random.NextInt(0, 100);
                if (chance < param.PacketDropPercentage)
                    return true;
            }

            return false;
        }

        internal static unsafe bool ShouldDuplicatePacket(Context* ctx, ref Parameters param)
        {
            if (param.PacketDuplicationPercentage > 0)
            {
                var chance = ctx->Random.NextInt(0, 100);
                if (chance < param.PacketDuplicationPercentage)
                    return true;
            }

            return false;
        }
    }
}
