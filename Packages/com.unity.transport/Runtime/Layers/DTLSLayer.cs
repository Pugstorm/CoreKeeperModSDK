using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport.Logging;
using Unity.Networking.Transport.TLS;
using Unity.Networking.Transport.Relay;
using Unity.TLS.LowLevel;
using UnityEngine;

namespace Unity.Networking.Transport
{
    internal unsafe struct DTLSLayer : INetworkLayer
    {
        // The deferred queue has to be quite large because if we fill it up, a bug in UnityTLS
        // causes the session to fail. Once MTT-3971 is fixed, we can lower this value.
        private const int k_DeferredSendsQueueSize = 64;

        private const int k_DTLSPaddingWithoutRelay = 29;
        private const int k_DTLSPaddingWithRelay = 37;

        private struct DTLSConnectionData
        {
            [NativeDisableUnsafePtrRestriction]
            public Binding.unitytls_client* UnityTLSClientPtr;

            // Client being used for device reconnection. Will replace UnityTLSClientPtr if we
            // actually get a response from the server on this new session.
            [NativeDisableUnsafePtrRestriction]
            public Binding.unitytls_client* ReconnectionClientPtr;

            // Tracks the last time progress was made on the DTLS handshake. Used to delete
            // half-open connections after a while (important for servers since an attacker could
            // just send a ton of client hellos to fill up the connection list).
            public long LastHandshakeUpdate;

            // Tracks the last time a packet was received on this connection. Only used to determine
            // when to initiate device reconnection.
            public long LastReceive;
        }

        internal ConnectionList m_ConnectionList;
        private ConnectionDataMap<DTLSConnectionData> m_ConnectionsData;
        private NativeParallelHashMap<NetworkEndpoint, ConnectionId> m_EndpointToConnectionMap;
        private UnityTLSConfiguration m_UnityTLSConfiguration;
        private PacketsQueue m_DeferredSends;
        private long m_HalfOpenDisconnectTimeout;
        private long m_ReconnectionTimeout;
        private int m_DTLSPadding;

        public int Initialize(ref NetworkSettings settings, ref ConnectionList connectionList, ref int packetPadding)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (connectionList.IsCreated)
                throw new InvalidOperationException("DTLS layer doesn't support underlying connection lists.");
#endif
            var netConfig = settings.GetNetworkConfigParameters();
            var mtu = (ushort)(netConfig.maxMessageSize - packetPadding);

            connectionList = m_ConnectionList = ConnectionList.Create();
            m_ConnectionsData = new ConnectionDataMap<DTLSConnectionData>(1, default(DTLSConnectionData), Allocator.Persistent);
            m_EndpointToConnectionMap = new NativeParallelHashMap<NetworkEndpoint, ConnectionId>(1, Allocator.Persistent);
            m_UnityTLSConfiguration = new UnityTLSConfiguration(ref settings, SecureTransportProtocol.DTLS, mtu);
            m_DeferredSends = new PacketsQueue(k_DeferredSendsQueueSize, netConfig.maxMessageSize);

            m_DeferredSends.SetDefaultDataOffset(packetPadding);

            // We pick a value just past the maximum handshake timeout as our half-open disconnect
            // timeout since after that point, there is no handshake progress possible anymore.
            m_HalfOpenDisconnectTimeout = (netConfig.maxConnectAttempts + 1) * netConfig.connectTimeoutMS;
            m_ReconnectionTimeout = netConfig.reconnectionTimeoutMS;

            m_DTLSPadding = settings.TryGet<RelayNetworkParameter>(out _) ? k_DTLSPaddingWithRelay : k_DTLSPaddingWithoutRelay;

            packetPadding += m_DTLSPadding;

            return 0;
        }

        public void Dispose()
        {
            // Destroy any remaining UnityTLS clients (their memory is managed by UnityTLS).
            for (int i = 0; i < m_ConnectionsData.Length; i++)
            {
                var data = m_ConnectionsData.DataAt(i);
                if (data.UnityTLSClientPtr != null)
                    Binding.unitytls_client_destroy(data.UnityTLSClientPtr);
                if (data.ReconnectionClientPtr != null)
                    Binding.unitytls_client_destroy(data.ReconnectionClientPtr);
            }

            m_ConnectionList.Dispose();
            m_ConnectionsData.Dispose();
            m_EndpointToConnectionMap.Dispose();
            m_UnityTLSConfiguration.Dispose();
            m_DeferredSends.Dispose();
        }

        [BurstCompile]
        private struct ReceiveJob : IJob
        {
            public ConnectionList Connections;
            public ConnectionDataMap<DTLSConnectionData> ConnectionsData;
            public NativeParallelHashMap<NetworkEndpoint, ConnectionId> EndpointToConnection;
            public PacketsQueue DeferredSends;
            public PacketsQueue ReceiveQueue;
            public long Time;
            public long HalfOpenDisconnectTimeout;
            public long ReconnectionTimeout;
            [NativeDisableUnsafePtrRestriction]
            public Binding.unitytls_client_config* UnityTLSConfig;
            [NativeDisableUnsafePtrRestriction]
            public UnityTLSCallbacks.CallbackContext* UnityTLSCallbackContext;

            public void Execute()
            {
                UnityTLSCallbackContext->ReceivedPacket = default;
                UnityTLSCallbackContext->SendQueue = DeferredSends;
                UnityTLSCallbackContext->SendQueueIndex = -1;
                UnityTLSCallbackContext->PacketPadding = 0;

                ProcessReceivedMessages();
                ProcessConnectionList();
            }

            private void ProcessReceivedMessages()
            {
                var count = ReceiveQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    var packetProcessor = ReceiveQueue[i];

                    if (packetProcessor.Length == 0)
                        continue;

                    UnityTLSCallbackContext->ReceivedPacket = packetProcessor;

                    if (DTLSUtilities.IsClientHello(ref packetProcessor))
                    {
                        ProcessClientHello(packetProcessor.EndpointRef);
                        // Don't drop the packet, handshake check below will cover that.
                    }

                    // Check if packet is from a known endpoint. Drop if not.
                    if (!EndpointToConnection.TryGetValue(packetProcessor.EndpointRef, out var connectionId))
                    {
                        packetProcessor.Drop();
                        continue;
                    }

                    UpdateLastReceiveTime(connectionId);
                    HandlePossibleReconnection(connectionId, ref packetProcessor);

                    packetProcessor.ConnectionRef = connectionId;

                    // If in initial or handshake state, process everything as a handshake message.
                    var clientPtr = ConnectionsData[connectionId].UnityTLSClientPtr;
                    var clientState = Binding.unitytls_client_get_state(clientPtr);
                    if (clientState == Binding.UnityTLSClientState_Init || clientState == Binding.UnityTLSClientState_Handshake)
                    {
                        ProcessHandshakeMessage(ref packetProcessor);
                        packetProcessor.Drop();
                        continue;
                    }

                    // If we get here then we have a data message (or some irrelevant garbage).
                    ProcessDataMessage(ref packetProcessor);
                }
            }

            private void ProcessClientHello(NetworkEndpoint fromEndpoint)
            {
                // Ignore Client Hellos if we already have a connection from that endpoint.
                if (EndpointToConnection.ContainsKey(fromEndpoint))
                    return;

                var connectionId = Connections.StartConnecting(ref fromEndpoint);
                EndpointToConnection.Add(fromEndpoint, connectionId);

                var clientPtr = Binding.unitytls_client_create(Binding.UnityTLSRole_Server, UnityTLSConfig);
                Binding.unitytls_client_init(clientPtr);

                ConnectionsData[connectionId] = new DTLSConnectionData { UnityTLSClientPtr = clientPtr };
            }

            private void ProcessHandshakeMessage(ref PacketProcessor packetProcessor)
            {
                var connectionId = packetProcessor.ConnectionRef;
                var data = ConnectionsData[connectionId];

                UnityTLSCallbackContext->NewPacketsEndpoint = packetProcessor.EndpointRef;

                AdvanceHandshake(data.UnityTLSClientPtr);

                // Update the last handshake update time.
                data.LastHandshakeUpdate = Time;
                ConnectionsData[connectionId] = data;

                // Check if the handshake is over.
                var clientState = Binding.unitytls_client_get_state(data.UnityTLSClientPtr);
                if (clientState == Binding.UnityTLSClientState_Messaging)
                {
                    var role = Binding.unitytls_client_get_role(data.UnityTLSClientPtr);
                    if (role == Binding.UnityTLSRole_Client)
                        Connections.FinishConnectingFromLocal(ref connectionId);
                    else
                        Connections.FinishConnectingFromRemote(ref connectionId);
                }
            }

            private void ProcessDataMessage(ref PacketProcessor packetProcessor)
            {
                var connectionId = packetProcessor.ConnectionRef;
                var originalPacketOffset = packetProcessor.Offset;

                // UnityTLS is going to read the encrypted packet directly from the packet
                // processor. Since it can't write the decrypted data in the same place, we need to
                // create a temporary buffer to store that data.
                var tempBuffer = new NativeArray<byte>(ReceiveQueue.PayloadCapacity, Allocator.Temp);

                var decryptedLength = new UIntPtr();
                var result = Binding.unitytls_client_read_data(ConnectionsData[connectionId].UnityTLSClientPtr,
                    (byte*)tempBuffer.GetUnsafePtr(), new UIntPtr((uint)tempBuffer.Length), &decryptedLength);

                if (result == Binding.UNITYTLS_SUCCESS)
                {
                    packetProcessor.SetUnsafeMetadata(0, originalPacketOffset);
                    packetProcessor.AppendToPayload(tempBuffer.GetUnsafePtr(), (int)decryptedLength.ToUInt32());
                }
                else
                {
                    // Probably irrelevant garbage. Drop the packet silently. We don't want to log
                    // here since this could be used to flood the logs with errors. If this is an
                    // actual failure in UnityTLS that would require disconnecting, then all future
                    // receives will also fail and the connection will eventually timeout.
                    packetProcessor.Drop();
                }
            }

            private void HandlePossibleReconnection(ConnectionId connection, ref PacketProcessor packetProcessor)
            {
                var data = ConnectionsData[connection];
                if (data.ReconnectionClientPtr == null)
                    return;

                // When it comes to reconnection, there are basically two scenarios: our IP address
                // has changed, or it hasn't. If the our IP address has changed, then we need to
                // create a new DTLS session with the new address. If it hasn't changed, then we
                // need to maintain our existing one (we can't create a new one since the server
                // will see any traffic from us as coming from the old session).
                //
                // This is why when we detect a need for reconnection, we create a whole new DTLS
                // session on the side (ReconnectionClientPtr). If we get an answer from the server
                // on that session (if we get a Server Hello), we know we changed IP address and
                // start using the new session. Otherwise, it means we didn't change IP address and
                // must keep using the new session.

                if (DTLSUtilities.IsServerHello(ref packetProcessor))
                {
                    Binding.unitytls_client_destroy(data.UnityTLSClientPtr);
                    data.UnityTLSClientPtr = data.ReconnectionClientPtr;
                    data.ReconnectionClientPtr = null;
                }
                else
                {
                    Binding.unitytls_client_destroy(data.ReconnectionClientPtr);
                    data.ReconnectionClientPtr = null;
                }

                ConnectionsData[connection] = data;
            }

            private void ProcessConnectionList()
            {
                var count = Connections.Count;
                for (int i = 0; i < count; i++)
                {
                    var connectionId = Connections.ConnectionAt(i);

                    HandleConnectionState(connectionId);
                    CheckForFailedClient(connectionId);
                    CheckForHalfOpenConnection(connectionId);
                    CheckForReconnection(connectionId);
                }
            }

            private void HandleConnectionState(ConnectionId connection)
            {
                var connectionEndpoint = Connections.GetConnectionEndpoint(connection);
                var connectionState = Connections.GetConnectionState(connection);

                switch (connectionState)
                {
                    case NetworkConnection.State.Connecting:
                        if (EndpointToConnection.TryAdd(connectionEndpoint, connection))
                        {
                            var clientPtr = Binding.unitytls_client_create(Binding.UnityTLSRole_Client, UnityTLSConfig);
                            Binding.unitytls_client_init(clientPtr);

                            ConnectionsData[connection] = new DTLSConnectionData
                            {
                                UnityTLSClientPtr = clientPtr,
                                LastHandshakeUpdate = Time
                            };
                        }

                        // No matter if the connection is newly-connected or not, try to make
                        // progress on the handshake (e.g. with Client Hello resends).
                        UnityTLSCallbackContext->ReceivedPacket = default;
                        UnityTLSCallbackContext->NewPacketsEndpoint = connectionEndpoint;
                        AdvanceHandshake(ConnectionsData[connection].UnityTLSClientPtr);

                        break;
                    case NetworkConnection.State.Disconnecting:
                        Disconnect(connection);
                        break;
                }
            }

            private void CheckForFailedClient(ConnectionId connection)
            {
                var clientPtr = ConnectionsData[connection].UnityTLSClientPtr;
                if (clientPtr == null)
                    return;

                ulong dummy;
                var errorState = Binding.unitytls_client_get_errorsState(clientPtr, &dummy);
                var clientState = Binding.unitytls_client_get_state(clientPtr);

                if (errorState != Binding.UNITYTLS_SUCCESS || clientState == Binding.UnityTLSClientState_Fail)
                {
                    // The only way to get a failed client is because of a failed handshake.
                    if (clientState == Binding.UnityTLSClientState_Fail)
                    {
                        // TODO Would be nice to translate the numerical step in a string.
                        var handshakeStep = Binding.unitytls_client_get_handshake_state(clientPtr);
                        DebugLog.ErrorDTLSHandshakeFailed(handshakeStep);
                    }

                    Connections.StartDisconnecting(ref connection);
                    Disconnect(connection, Error.DisconnectReason.AuthenticationFailure);
                }
            }

            private void CheckForHalfOpenConnection(ConnectionId connection)
            {
                var clientPtr = ConnectionsData[connection].UnityTLSClientPtr;
                if (clientPtr == null)
                    return;

                // Check client state; Init and Handshake state means a half-open connection.
                var clientState = Binding.unitytls_client_get_state(clientPtr);
                if (clientState != Binding.UnityTLSClientState_Init && clientState != Binding.UnityTLSClientState_Handshake)
                    return;

                // Check if connection has been half-open for too long.
                var lastHandshakeUpdate = ConnectionsData[connection].LastHandshakeUpdate;
                if (Time - lastHandshakeUpdate > HalfOpenDisconnectTimeout)
                {
                    Connections.StartDisconnecting(ref connection);
                    Disconnect(connection, Error.DisconnectReason.Timeout);
                }
            }

            private void CheckForReconnection(ConnectionId connection)
            {
                var data = ConnectionsData[connection];
                if (data.UnityTLSClientPtr == null)
                    return;

                // Already reconnecting, nothing to do.
                if (data.ReconnectionClientPtr != null)
                    return;

                // No reconnection for servers.
                var role = Binding.unitytls_client_get_role(data.UnityTLSClientPtr);
                if (role == Binding.UnityTLSRole_Server)
                    return;

                // Check if we have not received anything for too long and we have to reconnect.
                if (data.LastReceive > 0 && Time - data.LastReceive > ReconnectionTimeout)
                {
                    data.ReconnectionClientPtr = Binding.unitytls_client_create(Binding.UnityTLSRole_Client, UnityTLSConfig);
                    Binding.unitytls_client_init(data.ReconnectionClientPtr);

                    UnityTLSCallbackContext->NewPacketsEndpoint = Connections.GetConnectionEndpoint(connection);
                    AdvanceHandshake(data.ReconnectionClientPtr);

                    ConnectionsData[connection] = data;
                }
            }

            private void AdvanceHandshake(Binding.unitytls_client* clientPtr)
            {
                while (Binding.unitytls_client_handshake(clientPtr) == Binding.UNITYTLS_HANDSHAKE_STEP) ;
            }

            private void UpdateLastReceiveTime(ConnectionId connection)
            {
                var data = ConnectionsData[connection];
                data.LastReceive = Time;
                ConnectionsData[connection] = data;
            }

            private void Disconnect(ConnectionId connection, Error.DisconnectReason reason = Error.DisconnectReason.Default)
            {
                EndpointToConnection.Remove(Connections.GetConnectionEndpoint(connection));
                Connections.FinishDisconnecting(ref connection, reason);

                var data = ConnectionsData[connection];
                if (data.UnityTLSClientPtr != null)
                    Binding.unitytls_client_destroy(data.UnityTLSClientPtr);
                if (data.ReconnectionClientPtr != null)
                    Binding.unitytls_client_destroy(data.ReconnectionClientPtr);

                ConnectionsData.ClearData(ref connection);
            }
        }

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dependency)
        {
            return new ReceiveJob
            {
                Connections = m_ConnectionList,
                ConnectionsData = m_ConnectionsData,
                EndpointToConnection = m_EndpointToConnectionMap,
                DeferredSends = m_DeferredSends,
                ReceiveQueue = arguments.ReceiveQueue,
                Time = arguments.Time,
                HalfOpenDisconnectTimeout = m_HalfOpenDisconnectTimeout,
                ReconnectionTimeout = m_ReconnectionTimeout,
                UnityTLSConfig = m_UnityTLSConfiguration.ConfigPtr,
                UnityTLSCallbackContext = m_UnityTLSConfiguration.CallbackContextPtr,
            }.Schedule(dependency);
        }

        [BurstCompile]
        private struct SendJob : IJob
        {
            public ConnectionDataMap<DTLSConnectionData> ConnectionsData;
            public NativeParallelHashMap<NetworkEndpoint, ConnectionId> EndpointToConnection;
            public PacketsQueue SendQueue;
            public PacketsQueue DeferredSends;
            public int DTLSPadding;
            [NativeDisableUnsafePtrRestriction]
            public UnityTLSCallbacks.CallbackContext* UnityTLSCallbackContext;

            public void Execute()
            {
                UnityTLSCallbackContext->SendQueue = SendQueue;
                UnityTLSCallbackContext->PacketPadding = DTLSPadding;

                // Encrypt all the packets in the send queue.
                var sendCount = SendQueue.Count;
                for (int i = 0; i < sendCount; i++)
                {
                    var packetProcessor = SendQueue[i];
                    if (packetProcessor.Length == 0)
                        continue;

                    UnityTLSCallbackContext->SendQueueIndex = i;

                    if (!EndpointToConnection.TryGetValue(packetProcessor.EndpointRef, out var connectionId))
                    {
                        packetProcessor.Drop();
                        continue;
                    }

                    var clientPtr = ConnectionsData[connectionId].UnityTLSClientPtr;
                    var packetPtr = (byte*)packetProcessor.GetUnsafePayloadPtr() + packetProcessor.Offset;

                    // Only way this can happen is if we're reconnecting.
                    var clientState = Binding.unitytls_client_get_state(clientPtr);
                    if (clientState != Binding.UnityTLSClientState_Messaging)
                    {
                        packetProcessor.Drop();
                        continue;
                    }

                    var result = Binding.unitytls_client_send_data(clientPtr, packetPtr, new UIntPtr((uint)packetProcessor.Length));
                    if (result != Binding.UNITYTLS_SUCCESS)
                    {
                        DebugLog.ErrorDTLSEncryptFailed(result);
                        packetProcessor.Drop();
                    }
                }

                // Add all the deferred sends to the send queue (they're already encrypted).
                var deferredCount = DeferredSends.Count;
                for (int i = 0; i < deferredCount; i++)
                {
                    var deferredPacketProcessor = DeferredSends[i];
                    if (deferredPacketProcessor.Length == 0)
                        continue;

                    if (SendQueue.EnqueuePacket(out var packetProcessor))
                    {
                        packetProcessor.EndpointRef = deferredPacketProcessor.EndpointRef;
                        packetProcessor.ConnectionRef = deferredPacketProcessor.ConnectionRef;

                        // Remove the DTLS padding from the offset.
                        packetProcessor.SetUnsafeMetadata(0, packetProcessor.Offset - DTLSPadding);

                        packetProcessor.AppendToPayload(deferredPacketProcessor);
                    }
                }

                DeferredSends.Clear();
            }
        }

        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dependency)
        {
            return new SendJob
            {
                ConnectionsData = m_ConnectionsData,
                EndpointToConnection = m_EndpointToConnectionMap,
                SendQueue = arguments.SendQueue,
                DeferredSends = m_DeferredSends,
                DTLSPadding = m_DTLSPadding,
                UnityTLSCallbackContext = m_UnityTLSConfiguration.CallbackContextPtr,
            }.Schedule(dependency);
        }
    }
}
