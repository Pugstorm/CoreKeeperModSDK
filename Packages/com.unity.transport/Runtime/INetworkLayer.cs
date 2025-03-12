using System;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Networking.Transport
{
    internal interface INetworkLayer : IDisposable
    {
        /// <summary>
        /// Initialize the layer.
        /// </summary>
        /// <remarks>
        /// The connection list and packet padding arguments can be modified if necessary. A layer
        /// implementing its own connections should replace the connection list with a new one. A
        /// layer that may add padding to start of a packet should increment the packet padding
        /// value by the maximum padding it may add.
        /// </remarks>
        /// <param name="settings">Configuration settings provided by the user.</param>
        /// <param name="connectionList">Connection list of the layer below.</param>
        /// <param name="packetPadding">Total padding of the layers below.</param>
        int Initialize(ref NetworkSettings settings, ref ConnectionList connectionList, ref int packetPadding);

        /// <summary>
        /// Schedule a job processing all received messages (and other internal bookkeeping).
        /// </summary>
        /// <param name="arguments">Arguments that can be used by the receive job.</param>
        /// <param name="dependency">Job that the new job should depend on.</param>
        JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dependency);

        /// <summary>
        /// Schedule a job sending all packets in the send queue.
        /// </summary>
        /// <param name="arguments">Arguments that can be used by the send job.</param>
        /// <param name="dependency">Job that the new job should depend on.</param>
        JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dependency);
    }

    /// <summary>Arguments used by send jobs.</summary>
    public struct SendJobArguments
    {
        /// <summary>A queue containing all the packets to be sent.</summary>
        /// <value>Queue of packets.</value>
        public PacketsQueue SendQueue;

        /// <summary>The current update time value.</summary>
        /// <value>Timestamp in milliseconds.</value>
        public long Time;
    }

    /// <summary>Arguments used by receive jobs.</summary>
    public struct ReceiveJobArguments
    {
        /// <summary>A queue containing all the packets to be received.</summary>
        /// <value>Queue of packets.</value>
        public PacketsQueue ReceiveQueue;

        /// <summary>The result of the receive operation.</summary>
        /// <value>Result of the receive operation.</value>
        public OperationResult ReceiveResult;

        /// <summary>The current update time value.</summary>
        /// <value>Timestamp in milliseconds.</value>
        public long Time;

        internal NetworkDriverReceiver DriverReceiver;
        internal NetworkEventQueue EventQueue;
        internal NetworkPipelineProcessor PipelineProcessor;
    }
}
