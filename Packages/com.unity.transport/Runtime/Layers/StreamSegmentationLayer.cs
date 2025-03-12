using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// Force message segmentation to and from an underlying TCP stream for testing purposes. 
    /// </summary>
    /// <remarks>
    /// This layer is meant to be used on top of a TCPNetworkInterface to induce scenarios of message spliting in 
    /// the receive queue of each peer by splitting outgoing packets larger than a certain segment size. If a host
    /// then sends enough messages in a burst before the remote peer's update, compaction should naturally occur in 
    /// the remote peer's TCP RCVBUF and packets of MTU size produced by the TCPNetworkInterface will have a higher 
    /// probability of containing multiple messages. When both peers are updated concurrently with a fast enough 
    /// channel (e.g. loopback) the remote peer would see a higher probability of partial messages given the original
    /// ones were larger than the segmentation.
    /// </remarks>
    internal struct StreamSegmentationLayer : INetworkLayer
    {
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void Warn(string msg) => DebugLog.LogWarning(msg);
 
        // TODO: Does this need to be configurable?
        const int k_SegmentSize = NetworkParameterConstants.AbsoluteMaxMessageSize / 5;
      
        public int Initialize(ref NetworkSettings settings, ref ConnectionList connectionList, ref int packetPadding)
        {
            return 0;
        }

        public void Dispose() { }

        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dep)
        {
            return new SendJob
            {
                SendQueue = arguments.SendQueue,
            }.Schedule(dep);
        }

        [BurstCompile]
        unsafe struct SendJob : IJob
        {
            public PacketsQueue SendQueue;

            public void Execute()
            {
                // Process all data messages
                var count = SendQueue.Count;
                for (int i = 0; i < count; i++)
                {                    
                    var packetProcessor = SendQueue[i];                  

                    // Split the packet into smaller segments while possible
                    while (packetProcessor.Length > 0)
                    {
                        if (!SendQueue.EnqueuePacket(out var segment))
                        {
                            Warn("Send queue overflow");
                            break;
                        }
                        segment.ConnectionRef = packetProcessor.ConnectionRef;
                        segment.EndpointRef = packetProcessor.EndpointRef;
                        segment.SetUnsafeMetadata(0);

                        var nbytes = Math.Min(packetProcessor.Length, k_SegmentSize);
                        segment.AppendToPayload((byte*)packetProcessor.GetUnsafePayloadPtr() + packetProcessor.Offset, nbytes);                           

                        packetProcessor.SetUnsafeMetadata(packetProcessor.Length - nbytes, packetProcessor.Offset + nbytes);
                    }

                    packetProcessor.Drop();
                }
            }
        }

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dep) => dep;
    }
}
