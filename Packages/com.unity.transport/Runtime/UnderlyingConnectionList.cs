using Unity.Collections;

namespace Unity.Networking.Transport
{
    internal interface IUnderlyingConnectionList
    {
        /// <summary>
        /// Tries to open a new connection in the underlying layer.
        /// </summary>
        /// <param name="endpoint">The endpoint to connect to.</param>
        /// <param name="underlyingConnection">The connection id in the underlying layer. Default will create a new connection and will override this value.</param>
        /// <returns>Returns true if the connection is fully stablished.</returns>
        /// <remarks>
        /// Returning false means the connection was created but it has not been fully stablished yet,
        /// eg. there is a handshake pending to complete.
        /// </remarks>
        bool TryConnect(ref NetworkEndpoint endpoint, ref ConnectionId underlyingConnection);

        /// <summary>
        /// Disconnect a connection in the underlying layer.
        /// </summary>
        /// <param name="connectionId">The connection to disconnect.</param>
        void Disconnect(ref ConnectionId connectionId);

        /// <summary>
        /// Gets the list of disconnections in the underlying layer for the current update.
        /// </summary>
        /// <param name="allocator">The allocator to use for the NativeArray</param>
        /// <returns>Returns a NativeArray with the disconnections of the underlying layer.</returns>
        public NativeArray<ConnectionList.CompletedDisconnection> QueryFinishedDisconnections(Allocator allocator);
    }

    internal struct NullUnderlyingConnectionList : IUnderlyingConnectionList
    {
        public bool TryConnect(ref NetworkEndpoint endpoint, ref ConnectionId underlyingConnection)
        {
            underlyingConnection = default;
            return true;
        }

        public void Disconnect(ref ConnectionId connectionId) {}

        public NativeArray<ConnectionList.CompletedDisconnection> QueryFinishedDisconnections(Allocator allocator) => default;
    }

    internal struct UnderlyingConnectionList : IUnderlyingConnectionList
    {
        private ConnectionList Connections;

        public UnderlyingConnectionList(ref ConnectionList connections)
        {
            Connections = connections;
        }

        public bool TryConnect(ref NetworkEndpoint endpoint, ref ConnectionId underlyingConnection)
        {
            if (underlyingConnection != default)
            {
                if (Connections.GetConnectionState(underlyingConnection) == NetworkConnection.State.Connected)
                    return true;
            }
            else
            {
                underlyingConnection = Connections.StartConnecting(ref endpoint);
            }
            return false;
        }

        public void Disconnect(ref ConnectionId connectionId)
        {
            var state = Connections.GetConnectionState(connectionId);
            if (state != NetworkConnection.State.Disconnecting && state != NetworkConnection.State.Disconnected)
            {
                Connections.StartDisconnecting(ref connectionId);
            }
        }

        public NativeArray<ConnectionList.CompletedDisconnection> QueryFinishedDisconnections(Allocator allocator)
            => Connections.QueryFinishedDisconnections(allocator);
    }
}
