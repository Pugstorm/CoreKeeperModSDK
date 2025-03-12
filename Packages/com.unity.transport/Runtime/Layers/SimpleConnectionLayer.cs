using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport.Logging;
using Unity.Networking.Transport.Utilities;

namespace Unity.Networking.Transport
{
    internal struct SimpleConnectionLayer : INetworkLayer
    {
        internal const byte k_ProtocolVersion = 1;
        internal const int k_HeaderSize = 1 + ConnectionToken.k_Length;
        internal const int k_HandshakeSize = 4 + k_HeaderSize;
        internal const uint k_ProtocolSignatureAndVersion = 0x00505455 | (k_ProtocolVersion << 3 * 8); // Reversed for endianness

        internal enum ConnectionState
        {
            Default = 0,
            AwaitingAccept,
            Established,
            DisconnectionSent,
        }

        internal enum HandshakeType : byte
        {
            ConnectionRequest = 1,
            ConnectionAccept = 2,
        }

        internal enum MessageType : byte
        {
            Data = 1,
            Disconnect = 2,
            Heartbeat = 3,
        }

        internal struct SimpleConnectionData
        {
            public ConnectionId UnderlyingConnection;
            public ConnectionToken Token;
            public ConnectionState State;
            public long LastReceiveTime;
            public long LastSendTime;
            public int ConnectionAttempts;
            public Error.DisconnectReason DisconnectReason;
        }

        internal unsafe struct ControlPacketCommand
        {
            private const int k_Capacity = k_HandshakeSize;
            public ConnectionId Connection;
            private fixed byte Data[k_Capacity];
            private int Length;

            public void CopyTo(ref PacketProcessor packetProcessor)
            {
                fixed(void* dataPtr = Data)
                packetProcessor.AppendToPayload(dataPtr, Length);
            }

            public ControlPacketCommand(ConnectionId connection, HandshakeType type, ref ConnectionToken token)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (UnsafeUtility.SizeOf<uint>() +
                    UnsafeUtility.SizeOf<HandshakeType>() +
                    UnsafeUtility.SizeOf<ConnectionToken>() > k_HandshakeSize ||
                    k_HandshakeSize > k_Capacity)
                    throw new System.OverflowException();
#endif
                Connection = connection;
                fixed(byte* dataPtr = Data)
                {
                    *(uint*)dataPtr = k_ProtocolSignatureAndVersion;
                    UnsafeUtility.CopyStructureToPtr(ref type, dataPtr + UnsafeUtility.SizeOf<uint>());
                    UnsafeUtility.CopyStructureToPtr(ref token, dataPtr + UnsafeUtility.SizeOf<uint>() + UnsafeUtility.SizeOf<HandshakeType>());
                }
                Length = k_HandshakeSize;
            }

            public ControlPacketCommand(ConnectionId connection, MessageType type, ref ConnectionToken token)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (UnsafeUtility.SizeOf<MessageType>() +
                    UnsafeUtility.SizeOf<ConnectionToken>() > k_HeaderSize ||
                    k_HeaderSize > k_Capacity)
                    throw new System.OverflowException();
#endif
                Connection = connection;
                fixed(byte* dataPtr = Data)
                {
                    UnsafeUtility.CopyStructureToPtr(ref type, dataPtr);
                    UnsafeUtility.CopyStructureToPtr(ref token, dataPtr + UnsafeUtility.SizeOf<MessageType>());
                }
                Length = k_HeaderSize;
            }
        }

        private ConnectionList m_ConnectionList;
        private ConnectionList m_UnderlyingConnectionList;
        private ConnectionDataMap<SimpleConnectionData> m_ConnectionsData;
        private NativeList<ControlPacketCommand> m_ControlCommands;
        private NativeParallelHashMap<ConnectionToken, ConnectionId> m_TokensHashMap;
        private int m_ConnectTimeout;
        private int m_DisconnectTimeout;
        private int m_HeartbeatTimeout;
        private int m_MaxConnectionAttempts;

        public int Initialize(ref NetworkSettings settings, ref ConnectionList connectionList, ref int packetPadding)
        {
            packetPadding += k_HeaderSize;

            var networkConfigParameters = settings.GetNetworkConfigParameters();

            if (connectionList.IsCreated)
                m_UnderlyingConnectionList = connectionList;

            m_ConnectTimeout = networkConfigParameters.connectTimeoutMS;
            m_DisconnectTimeout = networkConfigParameters.disconnectTimeoutMS;
            m_HeartbeatTimeout = networkConfigParameters.heartbeatTimeoutMS;
            m_MaxConnectionAttempts = networkConfigParameters.maxConnectAttempts;

            connectionList = m_ConnectionList = ConnectionList.Create();
            m_ConnectionsData = new ConnectionDataMap<SimpleConnectionData>(1, default(SimpleConnectionData), Collections.Allocator.Persistent);
            m_ControlCommands = new NativeList<ControlPacketCommand>(Allocator.Persistent);
            m_TokensHashMap = new NativeParallelHashMap<ConnectionToken, ConnectionId>(1, Allocator.Persistent);

            return 0;
        }

        public void Dispose()
        {
            m_ConnectionList.Dispose();
            m_ConnectionsData.Dispose();
            m_ControlCommands.Dispose();
            m_TokensHashMap.Dispose();
        }

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dependency)
        {
            if (m_UnderlyingConnectionList.IsCreated)
            {
                var underlyingConnections = new UnderlyingConnectionList(ref m_UnderlyingConnectionList);
                return ScheduleReceive(new ReceiveJob<UnderlyingConnectionList>(), underlyingConnections, ref arguments, dependency);
            }
            else
            {
                return ScheduleReceive(new ReceiveJob<NullUnderlyingConnectionList>(), default, ref arguments, dependency);
            }
        }

        private JobHandle ScheduleReceive<T>(ReceiveJob<T> job, T underlyingConnectionList, ref ReceiveJobArguments arguments, JobHandle dependency)
            where T : unmanaged, IUnderlyingConnectionList
        {
            job.Connections = m_ConnectionList;
            job.ConnectionsData = m_ConnectionsData;
            job.UnderlyingConnections = underlyingConnectionList;
            job.ReceiveQueue = arguments.ReceiveQueue;
            job.ControlCommands = m_ControlCommands;
            job.TokensHashMap = m_TokensHashMap;
            job.Time = arguments.Time;
            job.ConnectTimeout = m_ConnectTimeout;
            job.MaxConnectionAttempts = m_MaxConnectionAttempts;
            job.DisconnectTimeout = m_DisconnectTimeout;
            job.HeartbeatTimeout = m_HeartbeatTimeout;

            return job.Schedule(dependency);
        }

        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dependency)
        {
            return new SendJob
            {
                Connections = m_ConnectionList,
                ConnectionsData = m_ConnectionsData,
                SendQueue = arguments.SendQueue,
                ControlCommands = m_ControlCommands,
                Time = arguments.Time,
            }.Schedule(dependency);
        }

        [BurstCompile]
        private struct SendJob : IJob
        {
            public ConnectionList Connections;
            public ConnectionDataMap<SimpleConnectionData> ConnectionsData;
            public PacketsQueue SendQueue;
            public NativeList<ControlPacketCommand> ControlCommands;
            public long Time;

            public void Execute()
            {
                // Process all data messages
                var count = SendQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    var packetProcessor = SendQueue[i];
                    if (packetProcessor.Length == 0)
                        continue;

                    var connection = packetProcessor.ConnectionRef;
                    var connectionData = ConnectionsData[connection];
                    var connectionToken = connectionData.Token;

                    packetProcessor.PrependToPayload(connectionToken);
                    packetProcessor.PrependToPayload((byte)MessageType.Data);

                    packetProcessor.ConnectionRef = connectionData.UnderlyingConnection;

                    connectionData.LastSendTime = Time;
                    ConnectionsData[connection] = connectionData;
                }

                // Send all control messages
                var controlCommandsCount = ControlCommands.Length;
                for (int i = 0; i < controlCommandsCount; i++)
                    SendControlCommand(ref ControlCommands.ElementAt(i));
                ControlCommands.Clear();
            }

            private void SendControlCommand(ref ControlPacketCommand controlCommand)
            {
                if (SendQueue.EnqueuePacket(out var packetProcessor))
                {
                    packetProcessor.EndpointRef = Connections.GetConnectionEndpoint(controlCommand.Connection);

                    controlCommand.CopyTo(ref packetProcessor);

                    var connectionData = ConnectionsData[controlCommand.Connection];
                    connectionData.LastSendTime = Time;
                    ConnectionsData[controlCommand.Connection] = connectionData;

                    packetProcessor.ConnectionRef = connectionData.UnderlyingConnection;
                }
            }
        }

        [BurstCompile]
        internal struct ReceiveJob<T> : IJob where T : unmanaged, IUnderlyingConnectionList
        {
            public ConnectionList Connections;
            public ConnectionDataMap<SimpleConnectionData> ConnectionsData;
            public T UnderlyingConnections;
            public PacketsQueue ReceiveQueue;
            public NativeList<ControlPacketCommand> ControlCommands;
            public NativeParallelHashMap<ConnectionToken, ConnectionId> TokensHashMap;
            public long Time;
            public int ConnectTimeout;
            public int DisconnectTimeout;
            public int HeartbeatTimeout;
            public int MaxConnectionAttempts;

            public void Execute()
            {
                ProcessReceivedMessages();
                ProcessConnectionStates();
            }

            private void ProcessConnectionStates()
            {
                // Disconnect if underlying connection is disconnected.
                var underlyingDisconnections = UnderlyingConnections.QueryFinishedDisconnections(Allocator.Temp);
                var count = underlyingDisconnections.Length;
                for (int i = 0; i < count; i++)
                {
                    var disconnection = underlyingDisconnections[i];

                    var connectionId = FindConnectionByUnderlyingConnection(ref disconnection.Connection);
                    if (!connectionId.IsCreated)
                        continue;

                    var connectionState = Connections.GetConnectionState(connectionId);
                    if (connectionState == NetworkConnection.State.Disconnected ||
                        connectionState == NetworkConnection.State.Disconnecting)
                    {
                        continue;
                    }

                    var connectionData = ConnectionsData[connectionId];

                    Connections.StartDisconnecting(ref connectionId);
                    connectionData.DisconnectReason = disconnection.Reason;
                    connectionData.State = ConnectionState.Default;
                    ConnectionsData[connectionId] = connectionData;
                }

                count = Connections.Count;
                for (int i = 0; i < count; i++)
                {
                    var connectionId = Connections.ConnectionAt(i);
                    var connectionState = Connections.GetConnectionState(connectionId);

                    switch (connectionState)
                    {
                        case NetworkConnection.State.Disconnecting:
                            ProcessDisconnecting(ref connectionId);
                            break;
                        case NetworkConnection.State.Connecting:
                            ProcessConnecting(ref connectionId);
                            break;
                        case NetworkConnection.State.Connected:
                            ProcessConnected(ref connectionId);
                            break;
                    }
                }
            }

            private void ProcessDisconnecting(ref ConnectionId connectionId)
            {
                var connectionData = ConnectionsData[connectionId];

                // If we still need to send a disconnect, don't disconnect and queue up the message.
                var needToSendDisconnect = connectionData.State == ConnectionState.Established &&
                    connectionData.DisconnectReason != Error.DisconnectReason.ClosedByRemote;
                if (needToSendDisconnect)
                {
                    ControlCommands.Add(new ControlPacketCommand(connectionId, MessageType.Disconnect, ref connectionData.Token));
                    connectionData.State = ConnectionState.DisconnectionSent;
                    ConnectionsData[connectionId] = connectionData;
                }
                else
                {
                    UnderlyingConnections.Disconnect(ref connectionData.UnderlyingConnection);
                    Connections.FinishDisconnecting(ref connectionId, connectionData.DisconnectReason);
                    TokensHashMap.Remove(connectionData.Token);
                }
            }

            private void ProcessConnecting(ref ConnectionId connectionId)
            {
                var connectionData = ConnectionsData[connectionId];
                var connectionState = connectionData.State;

                if (connectionState == ConnectionState.Default)
                {
                    var endpoint = Connections.GetConnectionEndpoint(connectionId);

                    if (UnderlyingConnections.TryConnect(ref endpoint, ref connectionData.UnderlyingConnection))
                    {
                        // The connection was just created, we need to initialize it.
                        connectionData.State = ConnectionState.AwaitingAccept;
                        connectionData.Token = RandomHelpers.GetRandomConnectionToken();
                        connectionData.LastSendTime = Time;
                        connectionData.ConnectionAttempts++;

                        TokensHashMap.Add(connectionData.Token, connectionId);

                        ControlCommands.Add(new ControlPacketCommand(connectionId, HandshakeType.ConnectionRequest, ref connectionData.Token));

                        ConnectionsData[connectionId] = connectionData;
                        return;
                    }

                    ConnectionsData[connectionId] = connectionData;
                }

                // Check for connect timeout and connection attempts.
                // Note that while connecting, LastSendTime can only track connection requests.
                if (Time - connectionData.LastSendTime > ConnectTimeout)
                {
                    if (connectionData.ConnectionAttempts >= MaxConnectionAttempts)
                    {
                        Connections.StartDisconnecting(ref connectionId);
                        connectionData.DisconnectReason = Error.DisconnectReason.MaxConnectionAttempts;

                        ConnectionsData[connectionId] = connectionData;

                        ProcessDisconnecting(ref connectionId);
                    }
                    else
                    {
                        connectionData.ConnectionAttempts++;
                        connectionData.LastSendTime = Time;

                        ConnectionsData[connectionId] = connectionData;

                        // Send connect request only if underlying connection has been fully established.
                        if (connectionState == ConnectionState.AwaitingAccept)
                            ControlCommands.Add(new ControlPacketCommand(connectionId, HandshakeType.ConnectionRequest, ref connectionData.Token));
                    }
                }
            }

            private void ProcessConnected(ref ConnectionId connectionId)
            {
                var connectionData = ConnectionsData[connectionId];

                // Check for the disconnect timeout.
                if (Time - connectionData.LastReceiveTime > DisconnectTimeout)
                {
                    Connections.StartDisconnecting(ref connectionId);
                    connectionData.DisconnectReason = Error.DisconnectReason.Timeout;
                    ConnectionsData[connectionId] = connectionData;
                    ProcessDisconnecting(ref connectionId);
                }

                // Check for the heartbeat timeout.
                if (HeartbeatTimeout > 0 && Time - connectionData.LastSendTime > HeartbeatTimeout)
                {
                    ControlCommands.Add(new ControlPacketCommand(connectionId, MessageType.Heartbeat, ref connectionData.Token));
                }
            }

            private void ProcessReceivedMessages()
            {
                var count = ReceiveQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    var packetProcessor = ReceiveQueue[i];

                    if (packetProcessor.Length == 0)
                        continue;

                    if (ProcessHandshakeReceive(ref packetProcessor))
                    {
                        packetProcessor.Drop();
                        continue;
                    }

                    if (packetProcessor.Length < k_HeaderSize)
                    {
                        packetProcessor.Drop();
                        continue;
                    }

                    var messageType = (MessageType)packetProcessor.RemoveFromPayloadStart<byte>();
                    var connectionToken = packetProcessor.RemoveFromPayloadStart<ConnectionToken>();
                    var connectionId = FindConnectionByToken(ref connectionToken);

                    if (!connectionId.IsCreated || Connections.GetConnectionState(connectionId) != NetworkConnection.State.Connected)
                    {
                        packetProcessor.Drop();
                        continue;
                    }

                    switch (messageType)
                    {
                        case MessageType.Disconnect:
                        {
                            var connectionData = ConnectionsData[connectionId];

                            Connections.StartDisconnecting(ref connectionId);
                            connectionData.DisconnectReason = Error.DisconnectReason.ClosedByRemote;
                            ConnectionsData[connectionId] = connectionData;

                            ProcessDisconnecting(ref connectionId);

                            packetProcessor.Drop();
                            break;
                        }
                        case MessageType.Data:
                        {
                            PreprocessMessage(ref connectionId, ref packetProcessor.EndpointRef);
                            packetProcessor.ConnectionRef = connectionId;
                            break;
                        }
                        case MessageType.Heartbeat:
                        {
                            PreprocessMessage(ref connectionId, ref packetProcessor.EndpointRef);
                            packetProcessor.Drop();
                            break;
                        }
                        default:
                            DebugLog.ReceivedMessageWasNotProcessed(messageType);
                            packetProcessor.Drop();
                            break;
                    }
                }
            }

            private void PreprocessMessage(ref ConnectionId connectionId, ref NetworkEndpoint endpoint)
            {
                var connectionData = ConnectionsData[connectionId];

                // Update the endpoint for reconnection, but only if the connection was previously
                // fully establilshed.
                if (connectionData.State == ConnectionState.Established)
                    Connections.UpdateConnectionAddress(ref connectionId, ref endpoint);

                // Any valid message updates last receive time
                connectionData.LastReceiveTime = Time;

                ConnectionsData[connectionId] = connectionData;
            }

            private ConnectionId FindConnectionByToken(ref ConnectionToken token)
            {
                if (TokensHashMap.TryGetValue(token, out var connectionId))
                    return connectionId;

                return default;
            }

            private ConnectionId FindConnectionByUnderlyingConnection(ref ConnectionId underlyingConnection)
            {
                var count = ConnectionsData.Length;
                for (int i = 0; i < count; i++)
                {
                    var connectionData = ConnectionsData.DataAt(i);
                    if (connectionData.UnderlyingConnection == underlyingConnection)
                        return ConnectionsData.ConnectionAt(i);
                }

                return default;
            }

            private bool ProcessHandshakeReceive(ref PacketProcessor packetProcessor)
            {
                if (packetProcessor.Length != SimpleConnectionLayer.k_HandshakeSize)
                    return false;

                if ((packetProcessor.GetPayloadDataRef<int>(0) & 0xFFFFFF00) == (k_ProtocolSignatureAndVersion & 0xFFFFFF00))
                {
                    var protocolVersion = packetProcessor.GetPayloadDataRef<byte>(3);
                    if (protocolVersion != k_ProtocolVersion)
                    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        DebugLog.ProtocolMismatch(k_ProtocolVersion, protocolVersion);
#endif
                        return true;
                    }

                    var handshakeSeq = (HandshakeType)packetProcessor.GetPayloadDataRef<byte>(4);
                    var connectionToken = packetProcessor.GetPayloadDataRef<ConnectionToken>(5);
                    var connectionId = FindConnectionByToken(ref connectionToken);
                    var connectionData = ConnectionsData[connectionId];
                    switch (handshakeSeq)
                    {
                        case HandshakeType.ConnectionRequest:
                        {
                            // Whole new connection request for a new connection.
                            if (!connectionId.IsCreated)
                            {
                                connectionId = Connections.StartConnecting(ref packetProcessor.EndpointRef);
                                Connections.FinishConnectingFromRemote(ref connectionId);
                                connectionData = new SimpleConnectionData
                                {
                                    State = ConnectionState.Established,
                                    Token = connectionToken,
                                    UnderlyingConnection = packetProcessor.ConnectionRef,
                                };
                                TokensHashMap.Add(connectionToken, connectionId);
                            }

                            connectionData.LastSendTime = Time;
                            ControlCommands.Add(new ControlPacketCommand(connectionId, HandshakeType.ConnectionAccept, ref connectionToken));
                            break;
                        }

                        case HandshakeType.ConnectionAccept:
                        {
                            if (connectionId.IsCreated &&
                                connectionData.State == ConnectionState.AwaitingAccept)
                            {
                                connectionData.State = ConnectionState.Established;
                                Connections.FinishConnectingFromLocal(ref connectionId);
                            }
                            else
                            {
                                // Received a connection accept for an unknown connection
                                return true;
                            }

                            break;
                        }

                        // We got a malformed packet
                        default:
                            return true;
                    }

                    connectionData.LastReceiveTime = Time;
                    ConnectionsData[connectionId] = connectionData;
                    return true;
                }

                return false;
            }
        }
    }
}
