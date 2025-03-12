#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport.Logging;
using Unity.Networking.Transport.Utilities;

namespace Unity.Networking.Transport
{
    internal struct WebSocketLayer : INetworkLayer
    {
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void Warn(string msg) =>  DebugLog.LogWarning(msg);

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void WarnIf(bool condition, string msg) { if (condition) DebugLog.LogWarning(msg); }

        // Maps a connection id from the connection list to its connection data.
        private ConnectionDataMap<ConnectionData> m_ConnectionMap;

        private ConnectionList m_ConnectionList;
        private ConnectionList m_UnderlyingConnectionList;
        private ConnectionDataMap<ConnectionId> m_UnderlyingConnectionMap;

        private WebSocket.Settings m_Settings;

        unsafe struct ConnectionData
        {
            public ConnectionId UnderlyingConnectionId;

            public WebSocket.State WebSocketState;
            public WebSocket.Role Role;

            public WebSocket.Buffer SendBuffer;
            public WebSocket.Buffer RecvBuffer;

            public WebSocket.Payload RecvPayload;

            private byte isReceivingPayload;
            public bool IsReceivingPayload
            {
                get => isReceivingPayload > 0;
                set => isReceivingPayload = (byte)(value ? 1 : 0);
            }

            private byte isWaitingForPong;
            public bool IsWaitingForPong
            {
                get => isWaitingForPong > 0;
                set => isWaitingForPong = (byte)(value ? 1 : 0);
            }

            public WebSocket.Keys Keys;

            public long CreateTimeStamp;
            public long CloseTimeStamp;
            public long ReceiveTimeStamp;

            public Error.DisconnectReason DisconnectReason;

            public bool IsClient => Role == WebSocket.Role.Client;
        }

        public int Initialize(ref NetworkSettings settings, ref ConnectionList connectionList, ref int packetPadding)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!connectionList.IsCreated)
                throw new InvalidOperationException($"{GetType().Name} requires an underlying connection list to track packets.");
#endif

            packetPadding += WebSocket.MaxHeaderSize;

            var networkConfigParameters = settings.GetNetworkConfigParameters();

            m_Settings = new WebSocket.Settings
            {
                ConnectTimeoutMS = Math.Max(0, networkConfigParameters.connectTimeoutMS),
                DisconnectTimeoutMS = Math.Max(0, networkConfigParameters.disconnectTimeoutMS),
                HeartbeatTimeoutMS = Math.Max(0, networkConfigParameters.heartbeatTimeoutMS)
            };

            if (connectionList.IsCreated)
                m_UnderlyingConnectionList = connectionList;

            m_ConnectionList = connectionList = ConnectionList.Create();
            m_ConnectionMap = new ConnectionDataMap<ConnectionData>(1, default, Allocator.Persistent);
            m_UnderlyingConnectionMap = new ConnectionDataMap<ConnectionId>(1, default, Allocator.Persistent);

            return 0;
        }

        public void Dispose()
        {
            m_ConnectionList.Dispose();
            m_ConnectionMap.Dispose();
            m_UnderlyingConnectionMap.Dispose();
        }

        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dep)
        {
            return new SendJob
            {
                SendQueue = arguments.SendQueue,
                UnderlyingConnectionList = m_UnderlyingConnectionList,
                ConnectionList = m_ConnectionList,
                ConnectionMap = m_ConnectionMap,
                Settings = m_Settings,
                Rand = new Mathematics.Random((uint)TimerHelpers.GetTicks()),
            }.Schedule(dep);
        }

        [BurstCompile]
        unsafe struct SendJob : IJob
        {
            public PacketsQueue SendQueue;
            public ConnectionList UnderlyingConnectionList;
            public ConnectionList ConnectionList;
            public ConnectionDataMap<ConnectionData> ConnectionMap;
            public WebSocket.Settings Settings;
            public Mathematics.Random Rand;

            public void Execute()
            {
                if (!UnderlyingConnectionList.IsCreated)
                    return;

                // Process all data messages
                var count = SendQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    var packetProcessor = SendQueue[i];
                    // Don't send empty packets or packets larger than we can receive on the other side.
                    if (packetProcessor.Length == 0 || (ushort)packetProcessor.Length > (SendQueue.PayloadCapacity - WebSocket.MaxHeaderSize))
                        continue;

                    var connectionId = packetProcessor.ConnectionRef;
                    var connectionState = ConnectionList.GetConnectionState(connectionId);

                    // RFC6455 says we can only send data in the OPEN state but the layers assume they are allowed to
                    // send packets even if starting a disconnection right in the next line so let packets go through
                    // while a CLOSE hasn't been sent.
                    var connectionData = ConnectionMap[connectionId];
                    packetProcessor.ConnectionRef = connectionData.UnderlyingConnectionId;
                    if ((connectionState == NetworkConnection.State.Connected && connectionData.WebSocketState == WebSocket.State.Open)
                        || (connectionState == NetworkConnection.State.Disconnecting && connectionData.WebSocketState != WebSocket.State.Closing))
                    {
                        if (!WebSocket.Binary(ref packetProcessor, connectionData.IsClient, Rand.NextUInt()))
                        {
                            Warn("Failed to encode send packet");
                            packetProcessor.Drop();
                        }
                    }
                    else
                    {
                        packetProcessor.Drop();
                    }
                }

                // Send buffered handshake packets. User data is packed directly into WebSocket Binary packets above
                // because packets from the upper layer are guaranteed to be no greater than
                // SendQueue.PayloadCapacity - WebSocket.MaxHeaderSize bytes.
                count = ConnectionList.Count;
                for (int i  = 0; i < count; i++)
                {
                    var connectionId = ConnectionList.ConnectionAt(i);
                    var connectionState = ConnectionList.GetConnectionState(connectionId);

                    if (connectionState == NetworkConnection.State.Disconnected)
                        continue;

                    var connectionData = ConnectionMap[connectionId];

                    // Send buffered WebSocket packets in possibly multiple MTU-sized packets. In theory, the loop
                    // might have to stall if the send queue becomes full but this is extremely unlikely. The send
                    // buffer should always have one (or more) complete packets to be transmitted.
                    var total = connectionData.SendBuffer.Length;
                    var pending = total;
                    var endpoint = ConnectionList.GetConnectionEndpoint(connectionId);
                    while (pending > 0 && SendQueue.EnqueuePacket(out var packetProcessor))
                    {
                        packetProcessor.ConnectionRef = connectionData.UnderlyingConnectionId;
                        packetProcessor.EndpointRef = endpoint;

                        // Pack the maximum possible amount of bytes from the send buffer into a packet. Must deduct
                        // the size of the header saved in the packet for this layer because the send buffer already
                        // contains both header and payload data.
                        var offset = packetProcessor.Offset - WebSocket.MaxHeaderSize;
                        var nbytes = Math.Min(pending, packetProcessor.BytesAvailableAtEnd + WebSocket.MaxHeaderSize);
                        packetProcessor.SetUnsafeMetadata(0, offset);
                        packetProcessor.AppendToPayload(connectionData.SendBuffer.Data + total - pending, nbytes);
                        pending -= nbytes;
                    }

                    // Move any data that remains pending to the beginning of the buffer for the next iteration
                    if (pending > 0)
                        UnsafeUtility.MemMove(connectionData.SendBuffer.Data, connectionData.SendBuffer.Data + total - pending, pending);

                    connectionData.SendBuffer.Length = pending;
                    ConnectionMap[connectionId] = connectionData;
                }
            }
        }

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dep)
        {
            return new ReceiveJob
            {
                ReceiveQueue = arguments.ReceiveQueue,
                UnderlyingConnectionList = new UnderlyingConnectionList(ref m_UnderlyingConnectionList),
                ConnectionList = m_ConnectionList,
                ConnectionMap = m_ConnectionMap,
                UnderlyingConnectionMap = m_UnderlyingConnectionMap,
                Settings = m_Settings,
                Rand = new Mathematics.Random((uint)TimerHelpers.GetTicks()),
                Time = arguments.Time,
            }.Schedule(dep);
        }

        [BurstCompile]
        unsafe struct ReceiveJob : IJob
        {
            public PacketsQueue ReceiveQueue;

            public ConnectionList ConnectionList;
            public ConnectionDataMap<ConnectionData> ConnectionMap;
            public UnderlyingConnectionList UnderlyingConnectionList;
            public ConnectionDataMap<ConnectionId> UnderlyingConnectionMap;
            public WebSocket.Settings Settings;
            public Mathematics.Random Rand;
            public long Time;

            // Close and clear data from disconnected underlying connections.
            void ProcessUnderlyingDisconnections()
            {
                var disconnectionList = UnderlyingConnectionList.QueryFinishedDisconnections(Allocator.Temp);
                var count = disconnectionList.Length;
                for (int i = 0; i < count; i++)
                {
                    var underlyingConnectionId = disconnectionList[i].Connection;
                    var connectionId = UnderlyingConnectionMap[underlyingConnectionId];
                    if (ConnectionList.ConnectionAt(connectionId.Id) == connectionId)
                    {
                        // We only want to initiate and finish the disconnection if we're not
                        // already disconnecting, since that would indicate that the disconnection
                        // is coming from the layer below. If we are already disconnecting then it
                        // means we (or a layer above) initiated the disconnection and this will be
                        // handled elsewhere.
                        var state = ConnectionList.GetConnectionState(connectionId);
                        if (state != NetworkConnection.State.Disconnecting && state != NetworkConnection.State.Disconnected)
                        {
                            ConnectionList.StartDisconnecting(ref connectionId);
                            ConnectionList.FinishDisconnecting(ref connectionId, disconnectionList[i].Reason);
                        }
                    }
                }
            }

            // Process WebSocket State changes for each connection. WebSocket State transitions happen in
            // combination with the current connection state (from the main connection list).
            void ProcessConnectionStates()
            {
                var count = ConnectionList.Count;
                for (int i = 0; i < count; i++)
                {
                    var connectionId = ConnectionList.ConnectionAt(i);
                    var connectionState = ConnectionList.GetConnectionState(connectionId);
                    switch (connectionState)
                    {
                        case NetworkConnection.State.Disconnecting:
                        {
                            var connectionData = ConnectionMap[connectionId];
                            switch (connectionData.WebSocketState)
                            {
                                case WebSocket.State.ClosedAndFlushed:
                                    break;
                                // If the WebSocket is closed, wait until the send buffer is flushed to the
                                // underlying layer.
                                case WebSocket.State.Closed:
                                    if (connectionData.SendBuffer.Length == 0)
                                        connectionData.WebSocketState = WebSocket.State.ClosedAndFlushed;
                                    break;
                                // If a WebSocket CLOSE has been sent, check for the close timeout waiting for a
                                // reply. A WebSocket CLOSE reply is expected in the receive queue and no other
                                // packet will be sent after the send buffer is flushed. If the remote peer does
                                // not close the connection within the time limit we can force it.
                                case WebSocket.State.Closing:
                                    if (Time - connectionData.CloseTimeStamp > Settings.DisconnectTimeoutMS)
                                    {
                                        // Nothing in the send buffer is relevant anymore because a timeout is final.
                                        connectionData.WebSocketState = WebSocket.State.ClosedAndFlushed;
                                        connectionData.DisconnectReason = Error.DisconnectReason.Default;
                                    }
                                    break;
                                // If we were still in the middle of the handshake, the upper layer must want to
                                // abort the connection, so we can just force a disconnection.
                                case WebSocket.State.Opening:
                                    // Nothing in the send buffer is relevant anymore because an abort is final.
                                    connectionData.WebSocketState = WebSocket.State.ClosedAndFlushed;
                                    connectionData.DisconnectReason = Error.DisconnectReason.Default;
                                    break;
                                // If the upper layer is trying to disconnect normally, send a WebSocket Close and
                                // wait for a CLOSE reply. If the send buffer is full and we cannot send a CLOSE
                                // now ignore. In the next scheduled received we can try again and the send queue
                                // will eventually free up for the send buffer to flush and make room for the close.
                                // This is safe as no user data can be sent by this connection since it's not
                                // NetworkConnection.State.Connected anymore.
                                case WebSocket.State.Open:
                                    if (WebSocket.Close(ref connectionData.SendBuffer, WebSocket.StatusCode.Normal, connectionData.IsClient, Rand.NextUInt()))
                                    {
                                        connectionData.WebSocketState = WebSocket.State.Closing;
                                        connectionData.CloseTimeStamp = Time;
                                    }
                                    break;
                            }

                            // If the WebSocket connection is closed-and-flushed we can close the underlying
                            // connection and discard all the connection data. Note that this may take multiple
                            // schedules as the layer below is not required to disconnect immediately.
                            if (connectionData.WebSocketState == WebSocket.State.ClosedAndFlushed)
                            {
                                UnderlyingConnectionList.Disconnect(ref connectionData.UnderlyingConnectionId);
                                ConnectionList.FinishDisconnecting(ref connectionId, connectionData.DisconnectReason);
                            }

                            ConnectionMap[connectionId] = connectionData;
                        }
                        break;
                        case NetworkConnection.State.Connecting:
                        {
                            var connectionData = ConnectionMap[connectionId];

                            switch (connectionData.WebSocketState)
                            {
                                // If this is a brand new CLIENT connection from which we have to send an
                                // HTTP UPGARDE try to complete an underlying connection and initialize the
                                // connection data.
                                case WebSocket.State.None:
                                {
                                    var remoteEndpoint = ConnectionList.GetConnectionEndpoint(connectionId);
                                    if (!UnderlyingConnectionList.TryConnect(ref remoteEndpoint, ref connectionData.UnderlyingConnectionId))
                                    {
                                        ConnectionMap[connectionId] = connectionData;
                                        if (connectionData.UnderlyingConnectionId.IsCreated)
                                            UnderlyingConnectionMap[connectionData.UnderlyingConnectionId] = connectionId;

                                        continue;
                                    }

                                    connectionData.WebSocketState = WebSocket.State.Opening;
                                    connectionData.Role = WebSocket.Role.Client;
                                    connectionData.Keys.Key[0] = Rand.NextUInt();
                                    connectionData.Keys.Key[1] = Rand.NextUInt();
                                    connectionData.Keys.Key[2] = Rand.NextUInt();
                                    connectionData.Keys.Key[3] = Rand.NextUInt();
                                    connectionData.CreateTimeStamp = Time;

                                    WebSocket.Connect(ref connectionData.SendBuffer, ref remoteEndpoint, ref connectionData.Keys);
                                }
                                break;
                                // If this is a active (client) connection for which an HTTP UPGARDE has already
                                // been sent and we're waiting for an HTTP SWITCHING PROTOCOL; or this is a
                                // passive (server) connection from which we're waiting for a complete (and valid)
                                // HTTP UPGRADE, check the connection timeout.
                                case WebSocket.State.Opening:
                                    if (Time - connectionData.CreateTimeStamp >  Settings.ConnectTimeoutMS)
                                    {
                                        // Nothing in the send buffer is releavent anymore because a timeout is final.
                                        ConnectionList.StartDisconnecting(ref connectionId);
                                        connectionData.WebSocketState = WebSocket.State.ClosedAndFlushed;
                                        connectionData.DisconnectReason = Error.DisconnectReason.MaxConnectionAttempts;
                                    }
                                    break;
                                default:
                                    Warn($"Invalid connection state: {connectionState}, {connectionData.WebSocketState}");
                                    break;
                            }
                            ConnectionMap[connectionId] = connectionData;
                        }
                        break;
                        case NetworkConnection.State.Connected:
                        {
                            var connectionData = ConnectionMap[connectionId];
                            if (connectionData.WebSocketState == WebSocket.State.Open)
                            {
                                if (Settings.HeartbeatTimeoutMS > 0 && !connectionData.IsWaitingForPong &&
                                    Time - connectionData.ReceiveTimeStamp > Settings.HeartbeatTimeoutMS)
                                {
                                    // If there is not enough space in the send buffer we can try again in the
                                    // next schedule, this is not a critial error.
                                    if (WebSocket.Ping(ref connectionData.SendBuffer, connectionData.IsClient, Rand.NextUInt()))
                                    {
                                        connectionData.IsWaitingForPong = true;
                                    }
                                }
                            }
                            else
                            {
                                Warn($"Invalid connection state: {connectionState}, {connectionData.WebSocketState}");
                            }
                            ConnectionMap[connectionId] = connectionData;
                        }
                        break;
                        case NetworkConnection.State.Disconnected:
                            // Do nothing. This is not a default fallback clause so the compiler can generate a warning
                            // if someone one day defines a new connection state and forgets to handle it here.
                            break;
                    }
                }
            }

            // Process all data messages. All packets pass through the RecvBuffer to be segmented into
            // individual HTTP handshake messages or WebSocket frames.
            void ProcessReceivedMessages()
            {
                var count = ReceiveQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    var packetProcessor = ReceiveQueue[i];

                    // Must check packets up to ReceiveQueue.PayloadCapacity here (including WebSocket.MaxHeaderSize)
                    // because in theory, a handshake packet larger than MTU would be split by the layer below (or
                    // network interface) into potentially multiple MTU-sized segments with no header.
                    if (packetProcessor.Length == 0 || packetProcessor.Length > ReceiveQueue.PayloadCapacity)
                    {
                        packetProcessor.Drop();
                        continue;
                    }

                    var underlyingConnectionId = packetProcessor.ConnectionRef;
                    var connectionId = UnderlyingConnectionMap[underlyingConnectionId];

                    // If this is a packet from an untracked connection it might be a new  HTTP UPGRADE (complete or
                    // incomplete), so start a new connection to decode whatever it is. This is assuming the underlying
                    // layer would never let a packet pass for a connection that is not fully connected in that level.
                    if (!connectionId.IsCreated)
                    {
                        connectionId = ConnectionList.StartConnecting(ref packetProcessor.EndpointRef);
                        ConnectionMap[connectionId] = new ConnectionData
                        {
                            UnderlyingConnectionId = underlyingConnectionId,

                            // Different than active (client) connections, passive (server) connections do not require
                            // WebSocket.Keys to be initialized.
                            WebSocketState = WebSocket.State.Opening,
                            Role = WebSocket.Role.Server,
                            CreateTimeStamp = Time
                        };

                        UnderlyingConnectionMap[underlyingConnectionId] = connectionId;
                    }

                    // If the connection is not disconnected and the WebSocket is not closed yet, process the received
                    // packet.
                    var connectionState = ConnectionList.GetConnectionState(connectionId);
                    if (connectionState != NetworkConnection.State.Disconnected)
                    {
                        var connectionData = ConnectionMap[connectionId];
                        if (connectionData.WebSocketState != WebSocket.State.Closed
                            && connectionData.WebSocketState != WebSocket.State.ClosedAndFlushed)
                        {
                            var available = connectionData.RecvBuffer.Available;
                            if (packetProcessor.Length <= available)
                            {
                                // Copy packet data into the receive buffer.
                                packetProcessor.CopyPayload(connectionData.RecvBuffer.Data + connectionData.RecvBuffer.Length, packetProcessor.Length);
                                connectionData.RecvBuffer.Length += packetProcessor.Length;

                                // If handshake is incomplete, try to complete.
                                if (connectionData.WebSocketState == WebSocket.State.Opening)
                                {
                                    // WebSocket.Handshake returns WebSocket.State.Opening (incomplete),
                                    // WebSocket.State.Open (complete) or WebSocket.State.Closed (error).
                                    // If an error occurs, an error message may get written into the send buffer to
                                    // be transmitted by the next send job scheduled.
                                    connectionData.WebSocketState = WebSocket.Handshake(ref connectionData.RecvBuffer,
                                        ref connectionData.SendBuffer, connectionData.IsClient, ref connectionData.Keys);

                                    if (connectionData.WebSocketState == WebSocket.State.Closed)
                                    {
                                        ConnectionList.StartDisconnecting(ref connectionId);
                                        connectionData.DisconnectReason = Error.DisconnectReason.ProtocolError;
                                    }
                                    else if (connectionData.WebSocketState == WebSocket.State.Open)
                                    {
                                        connectionData.ReceiveTimeStamp = Time;
                                        if (connectionData.IsClient)
                                            ConnectionList.FinishConnectingFromLocal(ref connectionId);
                                        else
                                            ConnectionList.FinishConnectingFromRemote(ref connectionId);
                                    }
                                }

                                // If handshake is complete, we can receive PING/PONG/DATA/CLOSE or a CLOSE reply.
                                // It's naturally possible to receive data before an expected CLOSE reply either
                                // because the data was already in flight or because the remote peer wants to
                                // complete its transmission. We are not required to reply to PINGs after a CLOSE is
                                // received but we are [required] while waiting for a CLOSE reply.
                                if (connectionData.WebSocketState == WebSocket.State.Open || connectionData.WebSocketState == WebSocket.State.Closing)
                                    ProcessWebSocketFrames(ref connectionId, ref connectionData);
                            }
                            else
                            {
                                // Insufficient buffer space at this level means an unrecoverable data loss.
                                // The WebSocket Protocol is an independent TCP-based protocol so there is no reason to
                                // presume the remote peer will be able to retransmit the lost packets in which case
                                // we're left with no other option but to disconnect.
                                Warn("Insufficient receive buffer.");
                                ConnectionList.StartDisconnecting(ref connectionId);
                                connectionData.WebSocketState = WebSocket.State.Closed;
                                connectionData.DisconnectReason = Error.DisconnectReason.ProtocolError;
                            }
                        }
                        ConnectionMap[connectionId] = connectionData;
                    }

                    // Packet is always dropped because websocket messages must be processed by ProcessWebSocketFrames (eg. can be fragmented)
                    // and the upper layer should only see the content of connectionData.RecvBuffer when the message is valid and complete.
                    packetProcessor.Drop();
                }
            }

            // Process all websocket frames in the recv buffer. Only happens after the hadnshake is complete.
            // Unfragmented WebSocket messages are handled immediately. Fragmented WebSocket messages are assembled in
            // RecvPayload and handled once complete.
            void ProcessWebSocketFrames(ref ConnectionId connectionId, ref ConnectionData connectionData)
            {
                var total = connectionData.RecvBuffer.Length;
                if (total <= 0)
                    return;

                var maxPayloadSize = ReceiveQueue.PayloadCapacity - WebSocket.MaxHeaderSize;

                var pending = total;
                fixed(byte* start = connectionData.RecvBuffer.Data)
                {
                    byte* end = start + total;
                    byte* data = start;

                    while (data < end)
                    {
                        // Yield if minimum header has not beend received yet
                        if (end - data < 2)
                            break;

                        // Calculate the header size
                        var headerSize = 2;
                        var payloadByte = data[1] & 0x7f;
                        if ((data[1] & 0x80) != 0)
                            headerSize += 4;
                        if (payloadByte == 126)
                            headerSize += 2;
                        else if (payloadByte == 127)
                            headerSize += 8;

                        // Yield if complete header has not beend received yet
                        if (end - data < headerSize)
                            break;

                        var isClient = connectionData.IsClient;
                        var isFragment = (data[0] & 0x80) == 0;
                        var opcode = data[0] & 0x0F;
                        var masked = (data[1] & 0x80) != 0;

                        // Receiving a message with invalid header bits or a masked message on the client or an
                        // unmasked message on the server are all protocol errors.
                        // Currently only support fragmentation of binary frames. RFC6455 states control frames may be
                        // injected in the middle of a fragmented message but cannot be fragmented themselves. The
                        // fragments of one message cannot be interleaved between the fragments of another message
                        // either.
                        var invalidHeaderBits = (data[0] & 0x70) != 0;
                        var invalidMasking = masked == isClient;
                        var invalidFragmentation = isFragment && opcode != (int)WebSocket.Opcode.Continuation && opcode != (int)WebSocket.Opcode.BinaryData;

                        if (invalidHeaderBits || invalidMasking || invalidFragmentation)
                        {
                            WarnIf(invalidHeaderBits, "Received message with invalid reserved header bits");
                            WarnIf(invalidMasking, "Received message with unexpected masking");
                            WarnIf(invalidFragmentation, "Received a fragmented control frame.");

                            Abort(ref connectionId, ref connectionData, WebSocket.StatusCode.ProtocolError, isClient);
                            return;
                        }

                        // A complete header is available so figure out how big the payload is.
                        var wsPayloadSize = 0UL;
                        if (payloadByte == 127)
                        {
                            wsPayloadSize = ((ulong)data[6] << 56) + ((ulong)data[7] << 48) +
                                ((ulong)data[6] << 40) + ((ulong)data[7] << 32) +
                                ((ulong)data[6] << 24) + ((ulong)data[7] << 16) +
                                ((ulong)data[8] << 8)  +  (ulong)data[9];
                        }
                        else if (payloadByte == 126)
                        {
                            wsPayloadSize = ((ulong)data[2] << 8) + data[3];
                        }
                        else
                        {
                            wsPayloadSize = (ulong)payloadByte;
                        }

                        // We don't support payloads larger than maxPayloadSize.
                        if (wsPayloadSize > (ulong)maxPayloadSize)
                        {
                            Warn($"Received a message with a payload size of {wsPayloadSize} which exceeds maximum of {maxPayloadSize} supported");
                            Abort(ref connectionId, ref connectionData, WebSocket.StatusCode.MessageTooBig, isClient);
                            return;
                        }

                        var payloadSize = (int)wsPayloadSize;

                        // Update pointer to start of payload
                        data += headerSize;

                        // Yield if complete payload has not been received yet
                        if (end - data < payloadSize)
                            break;

                        // Unmask the payload
                        if (masked)
                        {
                            var maskBytes = data - 4;
                            for (int i = 0; i < payloadSize; ++i)
                                data[i] = (byte)(data[i] ^ maskBytes[i & 3]);
                        }

                        if (opcode == (int)WebSocket.Opcode.Continuation)
                        {
                            // There must have been a previous binary frame to continue. Although allowed by the spec
                            // we don't support empty binrary frames. The RFC apparently allows for a data frame to be
                            // flagged as a fragment and yet carry 0 payload data. This would be just a waste of
                            // bandwidth and a possible indication of a malicious peer as further continuations could
                            // remain empty indefinitely. This check ensures we just accept continuations if we were
                            // already rebuilding a fragmented message (IsReceivingPayload is true). And this will only
                            // be the case if the first fragment was not empty because we ignore ignore empty data
                            // frames when handling WebSocket.Opcode.BinaryData.
                            var invalidFragment = !connectionData.IsReceivingPayload;
                            var invalidTotalSize = (connectionData.RecvPayload.Length + payloadSize) > maxPayloadSize;
                            if (invalidFragment || invalidTotalSize)
                            {
                                WarnIf(invalidFragment, "Received a continuation that doesn't belong to a message");
                                WarnIf(invalidTotalSize, $"Total message size is larger than maximum of {maxPayloadSize} supported");

                                var status = invalidFragment ? WebSocket.StatusCode.ProtocolError : WebSocket.StatusCode.MessageTooBig;
                                Abort(ref connectionId, ref connectionData, status, isClient);
                                return;
                            }

                            // A sender MAY create fragments of any size for non-control messages which includes
                            // empty fragments (no payload).
                            if (payloadSize > 0)
                            {
                                fixed(byte* recvPayload = connectionData.RecvPayload.Data)
                                {
                                    UnsafeUtility.MemCpy(recvPayload + connectionData.RecvPayload.Length, data, payloadSize);
                                    connectionData.RecvPayload.Length += payloadSize;

                                    // If this is the last fragment we can push the message
                                    if (!isFragment)
                                    {
                                        if (!ReceiveQueue.EnqueuePacket(out var packetProcessor))
                                        {
                                            Abort(ref connectionId, ref connectionData, WebSocket.StatusCode.InternalError, isClient);
                                            return;
                                        }

                                        packetProcessor.ConnectionRef = connectionId;
                                        packetProcessor.EndpointRef = ConnectionList.GetConnectionEndpoint(connectionId);
                                        packetProcessor.AppendToPayload(recvPayload, connectionData.RecvPayload.Length);

                                        connectionData.RecvPayload.Length = 0;
                                        connectionData.IsReceivingPayload = false;
                                    }
                                }
                            }
                        }
                        else if (opcode == (int)WebSocket.Opcode.TextData)
                        {
                            Abort(ref connectionId, ref connectionData, WebSocket.StatusCode.UnsupportedDataType, isClient);
                            return;
                        }
                        else if (opcode == (int)WebSocket.Opcode.BinaryData)
                        {
                            // A sender MAY create fragments of any size for non-control messages which includes
                            // empty fragments (no payload).
                            if (payloadSize > 0)
                            {
                                // If this is a fragment store in the RecvPayload
                                if (isFragment)
                                {
                                    fixed(byte* payload = connectionData.RecvPayload.Data)
                                    {
                                        UnsafeUtility.MemCpy(payload + connectionData.RecvPayload.Length, data, payloadSize);
                                        connectionData.RecvPayload.Length = payloadSize;
                                        connectionData.IsReceivingPayload = true;
                                    }
                                }
                                // If this is a complete message, push into the receive queue
                                else
                                {
                                    // A new binary message can only arrive if we're not expecting any more fragments
                                    // from a previous message.
                                    if (connectionData.IsReceivingPayload)
                                    {
                                        Abort(ref connectionId, ref connectionData, WebSocket.StatusCode.ProtocolError, isClient);
                                        return;
                                    }

                                    if (!ReceiveQueue.EnqueuePacket(out var packetProcessor))
                                    {
                                        Abort(ref connectionId, ref connectionData, WebSocket.StatusCode.InternalError, isClient);
                                        return;
                                    }

                                    packetProcessor.ConnectionRef = connectionId;
                                    packetProcessor.EndpointRef = ConnectionList.GetConnectionEndpoint(connectionId);
                                    packetProcessor.AppendToPayload(data, payloadSize);
                                }
                            }
                        }
                        else if (opcode == (int)WebSocket.Opcode.Close)
                        {
                            // If this is  a close reply (a CLOSE had previously been sent) we can close immediately;
                            // otherwise this must be a CLOSE request in which case we can close after sending a CLOSE
                            // reply.
                            if (connectionData.WebSocketState == WebSocket.State.Closing)
                            {
                                connectionData.WebSocketState = WebSocket.State.ClosedAndFlushed;
                                UnderlyingConnectionList.Disconnect(ref connectionData.UnderlyingConnectionId);
                                ConnectionList.FinishDisconnecting(ref connectionId, connectionData.DisconnectReason);
                            }
                            else
                            {
                                // Spec recommends echoing the status code of the CLOSE received which comes encoded
                                // in big endian.
                                var status = payloadSize > 1 ? (WebSocket.StatusCode)((data[0] << 8) + data[1]) : WebSocket.StatusCode.Normal;
                                Abort(ref connectionId, ref connectionData, status, isClient);
                            }
                            return;
                        }
                        else if (opcode == (int)WebSocket.Opcode.Ping)
                        {
                            // There is no point in sending anything else even PONGs after we have sent a CLOSE request.
                            if (connectionData.WebSocketState != WebSocket.State.Closing)
                            {
                                if (!WebSocket.Pong(ref connectionData.SendBuffer, data, payloadSize, isClient, Rand.NextUInt()))
                                {
                                    Warn("Insufficient send buffer.");
                                    Abort(ref connectionId, ref connectionData, WebSocket.StatusCode.InternalError, isClient);
                                    return;
                                }
                            }
                        }
                        else if (opcode == (int)WebSocket.Opcode.Pong)
                        {
                            connectionData.IsWaitingForPong = false;
                        }
                        else
                        {
                            // Unsupported opcode
                            Warn($"Received message with an unsupported opcode: 0x{opcode:X2}");
                            Abort(ref connectionId, ref connectionData, WebSocket.StatusCode.ProtocolError, isClient);
                            return;
                        }

                        data += payloadSize;
                        pending -= headerSize;
                        pending -= payloadSize;

                        // Save the time of the latest frame received
                        connectionData.ReceiveTimeStamp = Time;
                    }

                    if (pending > 0 && pending != total)
                        UnsafeUtility.MemMove(start, start + total - pending, pending);
                }
                connectionData.RecvBuffer.Length = pending;
            }

            void Abort(ref ConnectionId connectionId, ref ConnectionData connectionData, WebSocket.StatusCode status, bool isClient)
            {
                if (connectionData.WebSocketState != WebSocket.State.Closing)
                {
                    // There is no point in checking if a CLOSE message could be written to the send buffer here since
                    // we're bound to close the socket without waiting for a reply anyway.
                    WebSocket.Close(ref connectionData.SendBuffer, status, isClient, Rand.NextUInt());
                }

                var state = ConnectionList.GetConnectionState(connectionId);
                if (state != NetworkConnection.State.Disconnected && state != NetworkConnection.State.Disconnecting)
                    ConnectionList.StartDisconnecting(ref connectionId);
                connectionData.WebSocketState = WebSocket.State.Closed;

                connectionData.DisconnectReason = Error.DisconnectReason.ProtocolError;
            }

            public void Execute()
            {
                ProcessReceivedMessages();
                ProcessUnderlyingDisconnections();
                ProcessConnectionStates();
            }
        }
    }
}
#endif
