using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// A simple layer to provide message segmentation.
    /// </summary>
    /// <remarks>
    /// This layer takes care of segmenting messages over stream-oriented layers at the cost of a minimum overhead 
    /// (only 2 bytes per message). Stream oriented layers such as <see cref="TCPNetworkInterface"/> and the 
    /// <see cref="TLSLayer"/> cannot offer the guarantee that a packet in the receive queue corresponds to exactly one 
    /// message and thus require explicit segmentation. This layer should work correctly over datagram oriented layers 
    /// but its use then would be redundant and only waste packet space since datagram oriented layers provide message 
    /// segmentation for free. This layer is also unnecessary where there are already other layers in the stack that 
    /// can provide message segmentation such as the <see cref="WebSocketLayer"/>.
    /// </remarks>
    internal struct StreamToDatagramLayer : INetworkLayer
    {
        const int k_HeaderSize = sizeof(ushort);

        // Maps a connection id from the connection list to its connection data.
        private ConnectionDataMap<ConnectionData> m_ConnectionMap;
       
        public unsafe struct StreamToDatagramLayerPacketBuffer
        {
            public const int Capacity = 2 * NetworkParameterConstants.AbsoluteMaxMessageSize; 

            public fixed byte Data[Capacity];                
            public int Length;
        }

        unsafe struct ConnectionData
        {
            public StreamToDatagramLayerPacketBuffer RecvBuffer;
            public int ReceiveIgnore;
        }

        public int Initialize(ref NetworkSettings settings, ref ConnectionList connectionList, ref int packetPadding)
        {
            packetPadding += k_HeaderSize;
            m_ConnectionMap = new ConnectionDataMap<ConnectionData>(1, default, Allocator.Persistent);

            return 0;
        }

        public void Dispose()
        {
            m_ConnectionMap.Dispose();
        }

        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dep)
        {
            return new SendJob
            {
                SendQueue = arguments.SendQueue,
            }.Schedule(dep);
        }

        [BurstCompile]
        struct SendJob : IJob
        {
            public PacketsQueue SendQueue;

            static ushort HostToNetwork(ushort value)
                => (ushort)(BitConverter.IsLittleEndian ? ((value & 0xFF) << 8) | ((value >> 8) & 0xFF) : value);

            public void Execute()
            {
                // Process all data messages
                var count = SendQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    var packetProcessor = SendQueue[i];
                    // Don't send empty packets or packets larger than we can receive on the other side.
                    if (packetProcessor.Length == 0 || (ushort)packetProcessor.Length > (SendQueue.PayloadCapacity - k_HeaderSize))
                    {
                        packetProcessor.Drop();
                        continue;
                    }

                    var msglen = HostToNetwork((ushort)packetProcessor.Length);
                    packetProcessor.PrependToPayload(msglen);
                }
            }
        }

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dep)
        {
            return new ReceiveJob
            {
                ReceiveQueue = arguments.ReceiveQueue,
                ConnectionMap = m_ConnectionMap,
            }.Schedule(dep);
        }

        [BurstCompile]
        unsafe struct ReceiveJob : IJob
        {
            public PacketsQueue ReceiveQueue;
            public ConnectionDataMap<ConnectionData> ConnectionMap;

            public void Execute()
            {
                // Process all data messages
                var count = ReceiveQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    var packetProcessor = ReceiveQueue[i];
                    if (packetProcessor.Length == 0 || packetProcessor.Length > ReceiveQueue.PayloadCapacity)
                    {
                        packetProcessor.Drop();
                        continue;
                    }

                    var connectionId = packetProcessor.ConnectionRef;
                    var connectionData = ConnectionMap[connectionId];

                    // Determine if we're ignoring incoming data from an excessively large message
                    var ignored = Math.Max(0, Math.Min(connectionData.ReceiveIgnore, packetProcessor.Length));
                    // There is data beyond the ignored point copy in the packet buffer
                    if (ignored < packetProcessor.Length)
                    {
                        var nbytes = packetProcessor.Length - ignored;
                        UnsafeUtility.MemCpy(connectionData.RecvBuffer.Data + connectionData.RecvBuffer.Length, (byte*)packetProcessor.GetUnsafePayloadPtr() + packetProcessor.Offset + ignored, nbytes);
                        connectionData.RecvBuffer.Length += nbytes;
                    }

                    connectionData.ReceiveIgnore -= ignored;

                    // At this point connectionData.ReceivePacketBuffer.Length is > 0 only if
                    // connectionData.ReceiveIgnore == 0, in other words, if we're not ignoring anything anymore.
                    var total = connectionData.RecvBuffer.Length; 
                    var start = 0;
                    while (total >= k_HeaderSize)
                    {
                        // Try to pack a message 
                        var msglen = (ushort)(((connectionData.RecvBuffer.Data[start] & 0xFF)  << 8) + (connectionData.RecvBuffer.Data[start +  1] & 0xFF));
                        total -= k_HeaderSize;

                        // If incoming message is too large, just ignore
                        if (msglen > ReceiveQueue.PayloadCapacity - k_HeaderSize)
                        {
                            connectionData.ReceiveIgnore = Math.Max(0, msglen - total);
                            total = Math.Max(0, total - msglen);
                            start += k_HeaderSize + msglen;
                        }
                        else if (msglen == 0) // if message is empty there is nothing to do but advance in the buffer
                        {
                            // Skip the msg size
                            start += k_HeaderSize;  
                        }
                        else if (msglen <= total)
                        {
                            // Skip the msg size
                            start += k_HeaderSize; 

                            if (ReceiveQueue.EnqueuePacket(out var newPacketProcessor))
                            {
                                newPacketProcessor.ConnectionRef = packetProcessor.ConnectionRef;
                                newPacketProcessor.EndpointRef = packetProcessor.EndpointRef;
                                newPacketProcessor.AppendToPayload(connectionData.RecvBuffer.Data + start, msglen);
                            }

                            total -= msglen;
                            start += msglen;
                        }
                    }

                    // Move data to the beginning of the buffer if needed
                    if (start > 0 && start < connectionData.RecvBuffer.Length)
                        UnsafeUtility.MemMove(connectionData.RecvBuffer.Data, connectionData.RecvBuffer.Data + start, connectionData.RecvBuffer.Length - start);

                    // Update the buffer length. It could have been partially consumed (start < Length), totally
                    // consumed (start == Length) or not consumed at all (start == 0).
                    connectionData.RecvBuffer.Length -= start;
                    
                    ConnectionMap[connectionId] = connectionData;
                    packetProcessor.Drop();
                }
            }
        }
    }
}
