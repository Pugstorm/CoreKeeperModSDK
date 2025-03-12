using System;
using Unity.Collections;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// Provides an API for managing the NetworkDriver connections.
    /// </summary>
    internal struct ConnectionList : IDisposable
    {
        private struct ConnectionData
        {
            public NetworkEndpoint Endpoint;
            public NetworkConnection.State State;
        }

        internal struct CompletedDisconnection
        {
            public ConnectionId Connection;
            public Error.DisconnectReason Reason;
        }

        private ConnectionDataMap<ConnectionData> m_Connections;


        /// <summary>
        /// Stores all connections that completed the disconnection.
        /// </summary>
        private NativeQueue<CompletedDisconnection> m_FinishedDisconnections;

        /// <summary>
        /// Stores all connections (not requested by the remote endpoint) that completed the connection.
        /// </summary>
        private NativeQueue<ConnectionId> m_FinishedConnections;

        /// <summary>
        /// Stores all connections (requested by the remote endpoint) that completed the connection.
        /// </summary>
        private NativeQueue<ConnectionId> m_IncomingConnections;

        /// <summary>
        /// Stores all connections that can be created by reusing a previously released slot.
        /// </summary>
        private NativeQueue<ConnectionId> m_FreeList;

        /// <summary>
        /// The current count of connections.
        /// </summary>
        public int Count => m_Connections.Length;

        public bool IsCreated => m_Connections.IsCreated;

        internal NativeQueue<ConnectionId> FreeList => m_FreeList;

        internal ConnectionId ConnectionAt(int index) => m_Connections.ConnectionAt(index);
        internal NetworkEndpoint GetConnectionEndpoint(ConnectionId connectionId) => m_Connections[connectionId].Endpoint;
        internal NetworkConnection.State GetConnectionState(ConnectionId connectionId) => m_Connections[connectionId].State;

        internal NativeArray<ConnectionId> QueryFinishedConnections(Allocator allocator) => m_FinishedConnections.ToArray(allocator);
        internal NativeArray<ConnectionId> QueryIncomingConnections(Allocator allocator) => m_IncomingConnections.ToArray(allocator);
        internal NativeArray<CompletedDisconnection> QueryFinishedDisconnections(Allocator allocator) => m_FinishedDisconnections.ToArray(allocator);

        public static ConnectionList Create()
        {
            return new ConnectionList(Allocator.Persistent);
        }

        private ConnectionList(Allocator allocator)
        {
            var defaultConnectionData = new ConnectionData { State = NetworkConnection.State.Disconnected };
            m_Connections = new ConnectionDataMap<ConnectionData>(1, defaultConnectionData, allocator);
            m_FinishedDisconnections = new NativeQueue<CompletedDisconnection>(allocator);
            m_FinishedConnections = new NativeQueue<ConnectionId>(allocator);
            m_FreeList = new NativeQueue<ConnectionId>(allocator);
            m_IncomingConnections = new NativeQueue<ConnectionId>(allocator);
        }

        public void Dispose()
        {
            m_Connections.Dispose();
            m_FinishedDisconnections.Dispose();
            m_FinishedConnections.Dispose();
            m_IncomingConnections.Dispose();
            m_FreeList.Dispose();
        }

        private ConnectionId GetNewConnection()
        {
            if (m_FreeList.TryDequeue(out var connectionId))
            {
                // There is one free connection slot that we can reuse
                // its version has been already increased.
                return connectionId;
            }
            else
            {
                return new ConnectionId
                {
                    Id = m_Connections.Length,
                    Version = 1,
                };
            }
        }

        /// <summary>
        /// Creates a new connection to the provided address and sets its state to Connecting.
        /// </summary>
        /// <param name="address">The endpoint to connect to.</param>
        /// <returns>Returns the ConnectionId identifier for the new created connection.</returns>
        /// <remarks>The connection is going to be fully connected only when FinishConnecting() is called.</remarks>
        internal ConnectionId StartConnecting(ref NetworkEndpoint address)
        {
            var connection = GetNewConnection();

            m_Connections[connection] = new ConnectionData
            {
                Endpoint = address,
                State = NetworkConnection.State.Connecting,
            };

            return connection;
        }

        /// <summary>
        /// Completes a connection started by the local endpoint in Connecting state by setting it to Connected.
        /// </summary>
        /// <param name="connectionId">The connecting connection to be completed.</param>
        internal void FinishConnectingFromLocal(ref ConnectionId connectionId)
        {
            // TODO: we might want to restric the connection completion to the layer that
            // owns the connection list.

            CompleteConnecting(ref connectionId);
            m_FinishedConnections.Enqueue(connectionId);
        }

        /// <summary>
        /// Completes a connection started by the remote endpoint in Connecting state by setting it to Connected.
        /// </summary>
        /// <param name="connectionId">The connecting connection to be completed.</param>
        internal void FinishConnectingFromRemote(ref ConnectionId connectionId)
        {
            // TODO: we might want to restric the connection completion to the layer that
            // owns the connection list.

            CompleteConnecting(ref connectionId);
            m_IncomingConnections.Enqueue(connectionId);
        }

        private void CompleteConnecting(ref ConnectionId connectionId)
        {
            var connectionData = m_Connections[connectionId];

            if (connectionData.State != NetworkConnection.State.Connecting)
            {
                DebugLog.ConnectionCompletingWrongState(connectionData.State);
                return;
            }

            connectionData.State = NetworkConnection.State.Connected;
            m_Connections[connectionId] = connectionData;
        }

        internal ConnectionId AcceptConnection()
        {
            if (!m_IncomingConnections.TryDequeue(out var connectionId))
                return default;

            var connectionState = GetConnectionState(connectionId);
            if (connectionState != NetworkConnection.State.Connected)
            {
                DebugLog.ConnectionAcceptWrongState(connectionId, connectionState);
                return default;
            }

            return connectionId;
        }

        internal bool IsConnectionAccepted(ref ConnectionId connectionId)
        {
            if (m_IncomingConnections.Count == 0)
                return true;

            var unacceptedConnections = QueryIncomingConnections(Allocator.Temp);
            if (unacceptedConnections.Contains(connectionId))
                return false;

            return true;
        }

        /// <summary>
        /// Sets the state of the connection to Disconnected.
        /// </summary>
        /// <param name="connectionId">The connection to disconnect.</param>
        /// <remarks>The connection is going to be fully disconnected only when CompleteDisconnection is called.</remarks>
        internal void StartDisconnecting(ref ConnectionId connectionId)
        {
            var connectionData = m_Connections[connectionId];

            if (connectionData.State == NetworkConnection.State.Disconnected ||
                connectionData.State == NetworkConnection.State.Disconnecting)
            {
                DebugLog.LogWarning("Attempting to disconnect an already disconnected connection");
                return;
            }

            connectionData.State = NetworkConnection.State.Disconnecting;
            m_Connections[connectionId] = connectionData;
        }

        /// <summary>
        /// Completes a disconnection by setting the state of the connection to Disconneted.
        /// </summary>
        /// <param name="connectionId">The disconnecting connection to be completed.</param>
        /// <param name="reason">The disconnect reason</param>
        /// <remarks>
        /// A Disconnect event with the provided reason will be enqueued at the begining of the next ScheduleUpdate() call.
        /// The resources associated to the connection will be released at the begining of the next ScheduleUpdate() call.
        /// </remarks>
        internal void FinishDisconnecting(ref ConnectionId connectionId, Error.DisconnectReason reason = Error.DisconnectReason.Default)
        {
            // TODO: we might want to restric the disconnection completion to the layer that
            // owns the connection list.

            var connectionData = m_Connections[connectionId];

            if (connectionData.State != NetworkConnection.State.Disconnecting)
            {
                DebugLog.ConnectionFinishWrongState(connectionData.State);
                return;
            }

            m_FinishedDisconnections.Enqueue(new CompletedDisconnection
            {
                Connection = connectionId,
                Reason = reason,
            });
            
            connectionData.State = NetworkConnection.State.Disconnected;
            m_Connections[connectionId] = connectionData;
        }

        /// <summary>
        /// Cleanup of queues for connections/disconnections that has been completed.
        /// </summary>
        internal void Cleanup()
        {
            while (m_FinishedDisconnections.TryDequeue(out var disconnectionRequest))
            {
                var overridingConnection = disconnectionRequest.Connection;
                overridingConnection.Version++;

                // This will "initialize" the new available connection left by the disconnected one.
                m_Connections.ClearData(ref overridingConnection);

                m_FreeList.Enqueue(overridingConnection);
            }

            m_FinishedConnections.Clear();
        }

        internal void UpdateConnectionAddress(ref ConnectionId connection, ref NetworkEndpoint address)
        {
            var connectionData = m_Connections[connection];
            if (connectionData.Endpoint != address)
            {
                connectionData.Endpoint = address;
                m_Connections[connection] = connectionData;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is ConnectionList list &&
                this == list;
        }

        public override int GetHashCode()
        {
            return m_Connections.GetHashCode();
        }

        public static unsafe bool operator==(ConnectionList a, ConnectionList b)
        {
            return a.m_Connections == b.m_Connections;
        }

        public static unsafe bool operator!=(ConnectionList a, ConnectionList b)
        {
            return !(a == b);
        }
    }
}
