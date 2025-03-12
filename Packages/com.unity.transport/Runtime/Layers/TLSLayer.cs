using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport.Logging;
using Unity.Mathematics;
using Unity.Networking.Transport.TLS;
using Unity.TLS.LowLevel;
using UnityEngine;

namespace Unity.Networking.Transport
{
    internal unsafe struct TLSLayer : INetworkLayer
    {
        // The deferred queue has to be quite large because if we fill it up, a bug in UnityTLS
        // causes the session to fail. Once MTT-3971 is fixed, we can lower this value. Although
        // unlike with DTLS, we likely don't want to decrease it too much since with TLS the
        // certificate chains could be much larger and require more deferred sends.
        private const int k_DeferredSendsQueueSize = 64;

        // Padding used by TLS records. Since UnityTLS creates a record everytime we call
        // unitytls_client_send_data, each packet in the send queue must be ready to accomodate the
        // overhead of a TLS record. The overhead is 5 bytes for the record header, 32 bytes for the
        // MAC (typically 20 bytes, but can be 32 in TLS 1.2), and up to 31 bytes to pad to the
        // block size (for block ciphers, which are commonly 16 bytes but can be up to 32 for the
        // ciphers supported by UnityTLS).
        //
        // TODO See if we can limit what UnityTLS is willing to support in terms of ciphers so that
        //      we can reduce this value (we should normally be fine with a value of 40).
        private const int k_TLSPadding = 5 + 32 + 31;

        private const int k_DecryptBufferSize = NetworkParameterConstants.AbsoluteMaxMessageSize * 2;

        private struct TLSConnectionData
        {
            [NativeDisableUnsafePtrRestriction]
            public Binding.unitytls_client* UnityTLSClientPtr;

            public ConnectionId UnderlyingConnection;
            public Error.DisconnectReason DisconnectReason;

            // Used to delete old half-open connections.
            public long LastHandshakeUpdate;

            // UnityTLS only returns full records when decrypting, so it's possible to receive more
            // than an MTU's worth of data when decrypting a single packet (e.g. if the received
            // packet contains the end of a previous record). This means we can't just replace the
            // received packet's data with its decrypted equivalent (might not be enough room). Thus
            // we decrypt everything in this buffer. What can be fitted inside the packet is then
            // copied there, and any leftovers will be copied in the next packet (which might need
            // to be a newly-enqueued one).
            public fixed byte DecryptBuffer[k_DecryptBufferSize];
            public int DecryptBufferLength;
        }

        internal ConnectionList m_ConnectionList;
        private ConnectionList m_UnderlyingConnectionList;
        private ConnectionDataMap<TLSConnectionData> m_ConnectionsData;
        private NativeParallelHashMap<ConnectionId, ConnectionId> m_UnderlyingIdToCurrentIdMap;
        private UnityTLSConfiguration m_UnityTLSConfiguration;
        private PacketsQueue m_DeferredSends;
        private long m_HalfOpenDisconnectTimeout;

        public int Initialize(ref NetworkSettings settings, ref ConnectionList connectionList, ref int packetPadding)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!connectionList.IsCreated)
                throw new InvalidOperationException("TLS layer expects to have an underlying connection list.");
#endif
            var netConfig = settings.GetNetworkConfigParameters();

            m_UnderlyingConnectionList = connectionList;
            connectionList = m_ConnectionList = ConnectionList.Create();
            m_ConnectionsData = new ConnectionDataMap<TLSConnectionData>(1, default(TLSConnectionData), Allocator.Persistent);
            m_UnderlyingIdToCurrentIdMap = new NativeParallelHashMap<ConnectionId, ConnectionId>(1, Allocator.Persistent);
            m_UnityTLSConfiguration = new UnityTLSConfiguration(ref settings, SecureTransportProtocol.TLS);
            m_DeferredSends = new PacketsQueue(k_DeferredSendsQueueSize, netConfig.maxMessageSize);

            m_DeferredSends.SetDefaultDataOffset(packetPadding);

            // We pick the maximum handshake timeout as our half-open disconnect timeout since after
            // that point, there is no progress possible anymore on the handshake.
            m_HalfOpenDisconnectTimeout = netConfig.maxConnectAttempts * netConfig.connectTimeoutMS;

            packetPadding += k_TLSPadding;

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
            }

            m_ConnectionList.Dispose();
            m_ConnectionsData.Dispose();
            m_UnderlyingIdToCurrentIdMap.Dispose();
            m_UnityTLSConfiguration.Dispose();
            m_DeferredSends.Dispose();
        }

        [BurstCompile]
        private struct ReceiveJob : IJob
        {
            public ConnectionList Connections;
            public ConnectionList UnderlyingConnections;
            public ConnectionDataMap<TLSConnectionData> ConnectionsData;
            public NativeParallelHashMap<ConnectionId, ConnectionId> UnderlyingIdToCurrentId;
            public PacketsQueue DeferredSends;
            public PacketsQueue ReceiveQueue;
            public long Time;
            public long HalfOpenDisconnectTimeout;
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
                ProcessUnderlyingConnectionList();
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

                    // If we don't know about the underlying connection, then assume this is the
                    // first message from a client (a client hello). If it turns out it isn't, then
                    // the UnityTLS client will just fail, eventually causing a disconnection.
                    if (!UnderlyingIdToCurrentId.TryGetValue(packetProcessor.ConnectionRef, out var connectionId))
                    {
                        connectionId = ProcessClientHello(ref packetProcessor);
                        // Don't drop the packet, handshake check below will cover that.
                    }

                    packetProcessor.ConnectionRef = connectionId;

                    // If in initial or handshake state, process everything as a handshake message.
                    var clientPtr = ConnectionsData[connectionId].UnityTLSClientPtr;
                    var clientState = Binding.unitytls_client_get_state(clientPtr);
                    if (clientState == Binding.UnityTLSClientState_Init || clientState == Binding.UnityTLSClientState_Handshake)
                    {
                        ProcessHandshakeMessage(ref packetProcessor);

                        // If there's still data left in the packet, then it's likely actual data
                        // following completion of the handshake. In this case we don't want to go
                        // to the next packet and we want to keep processing it.
                        if (packetProcessor.Length == 0)
                            continue;

                        // Refresh the client state for the check below.
                        clientState = Binding.unitytls_client_get_state(clientPtr);
                    }

                    // Just ignore any new packets if the client is failed. Let CheckForFailedClient
                    // handle it later when we process the connection list.
                    if (clientState == Binding.UnityTLSClientState_Fail)
                    {
                        packetProcessor.Drop();
                        continue;
                    }

                    // If we get here then we must be dealing with an actual data message.
                    ProcessDataMessage(ref packetProcessor);
                }
            }

            private ConnectionId ProcessClientHello(ref PacketProcessor packetProcessor)
            {
                var connectionId = Connections.StartConnecting(ref packetProcessor.EndpointRef);
                UnderlyingIdToCurrentId.Add(packetProcessor.ConnectionRef, connectionId);

                var clientPtr = Binding.unitytls_client_create(Binding.UnityTLSRole_Server, UnityTLSConfig);
                Binding.unitytls_client_init(clientPtr);

                ConnectionsData[connectionId] = new TLSConnectionData
                {
                    UnityTLSClientPtr = clientPtr,
                    UnderlyingConnection = packetProcessor.ConnectionRef,
                };

                return connectionId;
            }

            private void ProcessHandshakeMessage(ref PacketProcessor packetProcessor)
            {
                var connectionId = packetProcessor.ConnectionRef;
                var data = ConnectionsData[connectionId];

                UnityTLSCallbackContext->NewPacketsEndpoint = packetProcessor.EndpointRef;
                UnityTLSCallbackContext->NewPacketsConnection = data.UnderlyingConnection;

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
                var originalPacketOffset = packetProcessor.Offset;
                var connectionId = packetProcessor.ConnectionRef;
                var data = ConnectionsData[connectionId];

                // The function unitytls_client_read_data exits after reading a single record (or
                // failing to read an full one). Since there could be multiple records per packet,
                // we need to call it until we've fully read the received packet.
                while (packetProcessor.Length > 0)
                {
                    // Pointer and length of next available space in decryption buffer.
                    var bufferPtr = data.DecryptBuffer + data.DecryptBufferLength;
                    var bufferLength = k_DecryptBufferSize - data.DecryptBufferLength;

                    var decryptedLength = new UIntPtr();
                    var result = Binding.unitytls_client_read_data(
                        data.UnityTLSClientPtr, bufferPtr, new UIntPtr((uint)bufferLength), &decryptedLength);

                    var wouldBlock = result == Binding.UNITYTLS_USER_WOULD_BLOCK;
                    if (wouldBlock || (result == Binding.UNITYTLS_SUCCESS && decryptedLength.ToUInt32() == 0))
                    {
                        // The "would block" error (or a successful return with nothing decrypted)
                        // means UnityTLS saw the beginning of a record but couldn't read it in
                        // full. There can't be anything more to read at this point.
                        break;
                    }
                    else if (result != Binding.UNITYTLS_SUCCESS)
                    {
                        // The error will be picked up in CheckForFailedClient.
                        DebugLog.ErrorTLSDecryptFailed(result);
                        break;
                    }

                    data.DecryptBufferLength += (int)decryptedLength.ToUInt32();
                }

                // We've now read the entire packet, now copy the decrypted data back in it.
                packetProcessor.SetUnsafeMetadata(0, originalPacketOffset);
                CopyDecryptBufferToPacket(ref data, ref packetProcessor);

                ConnectionsData[connectionId] = data;
            }

            private void ProcessUnderlyingConnectionList()
            {
                // The only thing we care about in the underlying connection list is disconnections.
                var disconnects = UnderlyingConnections.QueryFinishedDisconnections(Allocator.Temp);

                var count = disconnects.Length;
                for (int i = 0; i < count; i++)
                {
                    var underlyingConnectionId = disconnects[i].Connection;

                    // Happens if we initiated the disconnection.
                    if (!UnderlyingIdToCurrentId.TryGetValue(underlyingConnectionId, out var connectionId))
                        continue;

                    // If our connection is not disconnecting, then it means the layer below
                    // triggered the disconnection on its own, so start disconnecting.
                    if (Connections.GetConnectionState(connectionId) != NetworkConnection.State.Disconnecting)
                        Connections.StartDisconnecting(ref connectionId);

                    Disconnect(connectionId, disconnects[i].Reason);
                }
            }

            private void ProcessConnectionList()
            {
                var count = Connections.Count;
                for (int i = 0; i < count; i++)
                {
                    var connectionId = Connections.ConnectionAt(i);
                    var connectionState = Connections.GetConnectionState(connectionId);

                    switch (connectionState)
                    {
                        case NetworkConnection.State.Connected:
                            HandleConnectedState(connectionId);
                            break;
                        case NetworkConnection.State.Connecting:
                            HandleConnectingState(connectionId);
                            break;
                        case NetworkConnection.State.Disconnecting:
                            HandleDisconnectingState(connectionId);
                            break;
                    }

                    CheckForFailedClient(connectionId);
                    CheckForHalfOpenConnection(connectionId);
                }
            }

            private void HandleConnectedState(ConnectionId connection)
            {
                var data = ConnectionsData[connection];

                // Only thing we need to check in the connected state is if we need to enqueue any
                // leftover decrypted data. Note that we don't do anything if we can't enqueue a new
                // packet because it's safe to leave the data in its decryption buffer (we'll just
                // receive it on the next update).
                if (data.DecryptBufferLength > 0 && ReceiveQueue.EnqueuePacket(out var packetProcessor))
                {
                    packetProcessor.ConnectionRef = connection;
                    packetProcessor.EndpointRef = Connections.GetConnectionEndpoint(connection);
                    CopyDecryptBufferToPacket(ref data, ref packetProcessor);

                    ConnectionsData[connection] = data;
                }
            }

            private void HandleConnectingState(ConnectionId connection)
            {
                var endpoint = Connections.GetConnectionEndpoint(connection);
                var underlyingId = ConnectionsData[connection].UnderlyingConnection;

                // First we hear of this connection, start connecting on the underlying layer and
                // create the UnityTLS context (we won't use it now, but it'll be there already).
                if (underlyingId == default)
                {
                    underlyingId = UnderlyingConnections.StartConnecting(ref endpoint);
                    UnderlyingIdToCurrentId.Add(underlyingId, connection);

                    var clientPtr = Binding.unitytls_client_create(Binding.UnityTLSRole_Client, UnityTLSConfig);
                    Binding.unitytls_client_init(clientPtr);

                    ConnectionsData[connection] = new TLSConnectionData
                    {
                        UnityTLSClientPtr = clientPtr,
                        UnderlyingConnection = underlyingId,
                        LastHandshakeUpdate = Time,
                    };
                }

                // Make progress on the handshake if underlying connection is completed.
                if (UnderlyingConnections.GetConnectionState(underlyingId) == NetworkConnection.State.Connected)
                {
                    var clientPtr = ConnectionsData[connection].UnityTLSClientPtr;
                    var clientState = Binding.unitytls_client_get_state(clientPtr);
                    if (clientState == Binding.UnityTLSClientState_Init || clientState == Binding.UnityTLSClientState_Handshake)
                    {
                        UnityTLSCallbackContext->ReceivedPacket = default;
                        UnityTLSCallbackContext->NewPacketsEndpoint = endpoint;
                        UnityTLSCallbackContext->NewPacketsConnection = underlyingId;
                        AdvanceHandshake(clientPtr);
                    }
                }
            }

            private void HandleDisconnectingState(ConnectionId connection)
            {
                var underlyingId = ConnectionsData[connection].UnderlyingConnection;
                UnderlyingConnections.StartDisconnecting(ref underlyingId);
                Disconnect(connection, ConnectionsData[connection].DisconnectReason);
            }

            private void CheckForFailedClient(ConnectionId connection)
            {
                var data = ConnectionsData[connection];
                var clientPtr = data.UnityTLSClientPtr;

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
                        DebugLog.ErrorTLSHandshakeFailed(handshakeStep);
                    }

                    UnderlyingConnections.StartDisconnecting(ref data.UnderlyingConnection);
                    Connections.StartDisconnecting(ref connection);

                    data.DisconnectReason = Error.DisconnectReason.AuthenticationFailure;
                    ConnectionsData[connection] = data;
                }
            }

            private void CheckForHalfOpenConnection(ConnectionId connection)
            {
                var data = ConnectionsData[connection];

                if (data.UnityTLSClientPtr == null)
                    return;

                var clientState = Binding.unitytls_client_get_state(data.UnityTLSClientPtr);
                if (clientState != Binding.UnityTLSClientState_Init && clientState != Binding.UnityTLSClientState_Handshake)
                    return;

                if (Time - data.LastHandshakeUpdate > HalfOpenDisconnectTimeout)
                {
                    UnderlyingConnections.StartDisconnecting(ref data.UnderlyingConnection);
                    Connections.StartDisconnecting(ref connection);

                    data.DisconnectReason = Error.DisconnectReason.Timeout;
                    ConnectionsData[connection] = data;
                }
            }

            private void CopyDecryptBufferToPacket(ref TLSConnectionData data, ref PacketProcessor packetProcessor)
            {
                fixed(byte* decryptBuffer = data.DecryptBuffer)
                {
                    var copyLength = math.min(packetProcessor.BytesAvailableAtEnd, data.DecryptBufferLength);
                    packetProcessor.AppendToPayload(decryptBuffer, copyLength);
                    data.DecryptBufferLength -= copyLength;

                    // If there's still data in the decryption buffer, copy it to the beginning.
                    if (data.DecryptBufferLength > 0)
                    {
                        UnsafeUtility.MemMove(decryptBuffer, decryptBuffer + copyLength, data.DecryptBufferLength);
                    }
                }
            }

            private void AdvanceHandshake(Binding.unitytls_client* clientPtr)
            {
                while (Binding.unitytls_client_handshake(clientPtr) == Binding.UNITYTLS_HANDSHAKE_STEP);
            }

            private void Disconnect(ConnectionId connection, Error.DisconnectReason reason)
            {
                var data = ConnectionsData[connection];
                if (data.UnityTLSClientPtr != null)
                    Binding.unitytls_client_destroy(data.UnityTLSClientPtr);

                UnderlyingIdToCurrentId.Remove(data.UnderlyingConnection);
                ConnectionsData.ClearData(ref connection);
                Connections.FinishDisconnecting(ref connection, reason);
            }
        }

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dependency)
        {
            return new ReceiveJob
            {
                Connections = m_ConnectionList,
                UnderlyingConnections = m_UnderlyingConnectionList,
                ConnectionsData = m_ConnectionsData,
                UnderlyingIdToCurrentId = m_UnderlyingIdToCurrentIdMap,
                DeferredSends = m_DeferredSends,
                ReceiveQueue = arguments.ReceiveQueue,
                Time = arguments.Time,
                HalfOpenDisconnectTimeout = m_HalfOpenDisconnectTimeout,
                UnityTLSConfig = m_UnityTLSConfiguration.ConfigPtr,
                UnityTLSCallbackContext = m_UnityTLSConfiguration.CallbackContextPtr,
            }.Schedule(dependency);
        }

        [BurstCompile]
        private struct SendJob : IJob
        {
            public ConnectionList Connections;
            public ConnectionDataMap<TLSConnectionData> ConnectionsData;
            public PacketsQueue SendQueue;
            public PacketsQueue DeferredSends;
            [NativeDisableUnsafePtrRestriction]
            public UnityTLSCallbacks.CallbackContext* UnityTLSCallbackContext;

            public void Execute()
            {
                UnityTLSCallbackContext->SendQueue = SendQueue;
                UnityTLSCallbackContext->PacketPadding = k_TLSPadding;

                // Encrypt all the packets in the send queue.
                var count = SendQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    var packetProcessor = SendQueue[i];
                    if (packetProcessor.Length == 0)
                        continue;

                    UnityTLSCallbackContext->SendQueueIndex = i;

                    var connectionId = packetProcessor.ConnectionRef;
                    packetProcessor.ConnectionRef = ConnectionsData[connectionId].UnderlyingConnection;

                    var connectionState = Connections.GetConnectionState(connectionId);
                    var clientPtr = ConnectionsData[connectionId].UnityTLSClientPtr;
                    if (connectionState != NetworkConnection.State.Connected || clientPtr == null)
                    {
                        packetProcessor.Drop();
                        continue;
                    }

                    var packetPtr = (byte*)packetProcessor.GetUnsafePayloadPtr() + packetProcessor.Offset;
                    var result = Binding.unitytls_client_send_data(clientPtr, packetPtr, new UIntPtr((uint)packetProcessor.Length));
                    if (result != Binding.UNITYTLS_SUCCESS)
                    {
                        DebugLog.ErrorTLSEncryptFailed(result);
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

                        // Remove the TLS padding from the offset.
                        packetProcessor.SetUnsafeMetadata(0, packetProcessor.Offset - k_TLSPadding);

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
                Connections = m_ConnectionList,
                ConnectionsData = m_ConnectionsData,
                SendQueue = arguments.SendQueue,
                DeferredSends = m_DeferredSends,
                UnityTLSCallbackContext = m_UnityTLSConfiguration.CallbackContextPtr,
            }.Schedule(dependency);
        }
    }
}
