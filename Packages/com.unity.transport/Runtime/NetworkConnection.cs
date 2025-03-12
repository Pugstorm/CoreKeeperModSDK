using System;
using System.ComponentModel;
using Unity.Collections;

namespace Unity.Networking.Transport
{
    namespace Error
    {
        /// <summary>
        /// Reasons for a <see cref="NetworkEvent.Type.Disconnect"/> event. This can be obtained by
        /// reading a single byte off the <see cref="DataStreamReader"/> obtained when calling
        /// <see cref="NetworkDriver.PopEvent"/> when the popped event is for a disconnection.
        /// </summary>
        public enum DisconnectReason : byte
        {
            // This enum is matched by NetworkStreamDisconnectReason in Netcode for Entities, so any
            // change to it should be discussed and properly synchronized with them first.

            /// <summary>Internal value. Do not use.</summary>
            Default                       = 0,

            /// <summary>Indicates the connection timed out due to inactivity.</summary>
            Timeout                       = 1,

            /// <summary>
            /// Indicates the connection failed to be established because the server could not be
            /// reached (see <see cref="NetworkConfigParameter.maxConnectAttempts"/>).
            /// </summary>
            MaxConnectionAttempts         = 2,

            /// <summary>Indicates the connection was manually closed by the remote peer.</summary>
            ClosedByRemote                = 3,

            // Values 4 and 5 are already used by Netcode for Entites for bad protocol version and
            // invalid RPC, respectively. So we want new values to start at 6.

            /// <summary>
            /// Indicates the connection failed to be established because the remote peer could not
            /// be authenticated. This can only occur if using DTLS or TLS (with WebSockets).
            /// </summary>
            AuthenticationFailure         = 6,

            /// <summary>
            /// Indicates the connection failed because of a low-level protocol error (unexpected
            /// socket error, malformed payload in a TCP stream, etc.). This denotes an error that
            /// is both unexpected and that can't be recovered from. As such it should not be
            /// returned under normal operating circumstances.
            /// </summary>
            ProtocolError                 = 7,

            /// <summary>Obsolete. Will never be returned by the API.</summary>
            [EditorBrowsable(EditorBrowsableState.Never)]
            [Obsolete("Value is not in use anymore and nothing will return it.")]
            Count
        }

        /// <summary>
        /// Status codes that can be returned by many functions in the transport API.
        /// </summary>
        public enum StatusCode
        {
            /// <summary>Operation completed successfully.</summary>
            Success                       =  0,

            /// <summary>Connection is invalid.</summary>
            NetworkIdMismatch             = -1,

            /// <summary>
            /// Connection is invalid. This is usually caused by an attempt to use a connection
            /// that has been already closed.
            /// </summary>
            NetworkVersionMismatch        = -2,

            /// <summary>
            /// State of the connection is invalid for the operation requested. This is usually
            /// caused by an attempt to send on a connecting/closed connection.
            /// </summary>
            NetworkStateMismatch          = -3,

            /// <summary>Packet is too large for the supported capacity.</summary>
            NetworkPacketOverflow         = -4,

            /// <summary>Packet couldn't be sent because the send queue is full.</summary>
            NetworkSendQueueFull          = -5,

            /// <summary>Obsolete. Will never be returned.</summary>
            [Obsolete("Return code is not in use anymore and nothing will return it.")]
            NetworkHeaderInvalid          = -6,

            /// <summary>Attempted to process the same connection in different jobs.</summary>
            NetworkDriverParallelForErr   = -7,

            /// <summary>The <see cref="DataStreamWriter"/> is invalid.</summary>
            NetworkSendHandleInvalid      = -8,

            /// <summary>Obsolete. Will never be returned.</summary>
            [Obsolete("Return code is not in use anymore and nothing will return it.")]
            NetworkArgumentMismatch       = -9,

            /// <summary>
            /// A message couldn't be received because the receive queue is full. This can only be
            /// returned through <see cref="NetworkDriver.ReceiveErrorCode"/>.
            /// </summary>
            NetworkReceiveQueueFull       = -10,

            /// <summary>There was an error from the underlying low-level socket.</summary>
            NetworkSocketError            = -11,
        }
    }

    /// <summary>
    /// Public representation of a connection. This is obtained by calling
    /// <see cref="NetworkDriver.Accept"/> (on servers) or <see cref="NetworkDriver.Connect"/> (on
    /// clients) and acts as a handle to the communication session with a remote peer.
    /// </summary>
    public struct NetworkConnection : IEquatable<NetworkConnection>
    {
        // Be careful when using this directly as it also embeds the driver ID in its upper byte.
        private ConnectionId m_ConnectionId;

        internal ConnectionId ConnectionId => new ConnectionId { Id = InternalId, Version = Version };

        /// <summary>Different states in which a <see cref="NetworkConnection"/> can be.</summary>
        public enum State
        {
            /// <summary>
            /// Connection is closed. No data may be sent on it, and no further event will be
            /// received for the connection.
            /// </summary>
            Disconnected,

            /// <summary>
            /// Indicates the connection is in the process of being disconnected. This is an
            /// internal state, and will be mapped to <see cref="Disconnected"/> when calling
            /// <see cref="NetworkDriver.GetConnectionState"/>.
            /// </summary>
            [EditorBrowsable(EditorBrowsableState.Never)]
            Disconnecting,

            /// <summary>
            /// Connection is in the process of being established. No data may be sent on it and the
            /// next event will be either <see cref="NetworkEvent.Type.Connect"/> if the connection
            /// was successfully established, or <see cref="NetworkEvent.Type.Disconnect"/> if it
            /// failed to reach the server.
            /// </summary>
            Connecting,

            /// <summary>Connection is open. Can send and receive data on it.</summary>
            Connected,
        }

        internal NetworkConnection(ConnectionId connectionId)
        {
            m_ConnectionId = connectionId;
        }

        /// <summary>
        /// Close an active connection. Strictly identical to <see cref="Close"/>.
        /// </summary>
        /// <param name="driver">Driver on which to perform the operation.</param>
        /// <returns>0 on success, a negative error code otherwise.</returns>
        public int Disconnect(NetworkDriver driver)
        {
            return driver.Disconnect(this);
        }

        /// <summary>Pop the next available event on the connection.</summary>
        /// <param name="driver">Driver from which to get the event.</param>
        /// <param name="stream">
        /// Reader into the data associated to the event. Only populated if the returned event is
        /// <see cref="NetworkEvent.Type.Data"/> or <see cref="NetworkEvent.Type.Disconnect"/>,
        /// where respectively the data will be either the received payload, or the disconnection
        /// reason (as a single byte).
        /// </param>
        /// <returns>
        /// Type of the popped event. <see cref="NetworkEvent.Type.Empty"/> if nothing to pop.
        /// </returns>
        public NetworkEvent.Type PopEvent(NetworkDriver driver, out DataStreamReader stream)
        {
            return driver.PopEventForConnection(this, out stream);
        }

        /// <summary>Pop the next available event on the connection.</summary>
        /// <param name="driver">Driver from which to get the event.</param>
        /// <param name="stream">
        /// Reader into the data associated to the event. Only populated if the returned event is
        /// <see cref="NetworkEvent.Type.Data"/> or <see cref="NetworkEvent.Type.Disconnect"/>,
        /// where respectively the data will be either the received payload, or the disconnection
        /// reason (as a single byte).
        /// </param>
        /// <param name="pipeline">Pipeline on which the data was received.</param>
        /// <returns>
        /// Type of the popped event. <see cref="NetworkEvent.Type.Empty"/> if nothing to pop.
        /// </returns>
        public NetworkEvent.Type PopEvent(NetworkDriver driver, out DataStreamReader stream, out NetworkPipeline pipeline)
        {
            return driver.PopEventForConnection(this, out stream, out pipeline);
        }

        /// <summary>
        /// Close an active connection. Strictly identical to <see cref="Disconnect"/>.
        /// </summary>
        /// <param name="driver">Driver on which to perform the operation.</param>
        /// <returns>0 on success, a negative error code otherwise.</returns>
        public int Close(NetworkDriver driver)
        {
            return driver.Disconnect(this);
        }

        /// <summary>
        /// Whether the connection was correctly obtained from a call to
        /// <see cref="NetworkDriver.Accept"/> or <see cref="NetworkDriver.Connect"/>.
        /// </summary>
        /// <value>True if created correctly, false otherwise.</value>
        public bool IsCreated
        {
            get { return m_ConnectionId.Version != 0; }
        }

        /// <summary>Get the current state of a connection.</summary>
        /// <param name="driver">Driver to get the state from.</param>
        /// <returns>Current state of the connection.</returns>
        public State GetState(NetworkDriver driver)
        {
            return driver.GetConnectionState(this);
        }

        public static bool operator==(NetworkConnection lhs, NetworkConnection rhs)
        {
            return lhs.m_ConnectionId == rhs.m_ConnectionId;
        }

        public static bool operator!=(NetworkConnection lhs, NetworkConnection rhs)
        {
            return lhs.m_ConnectionId != rhs.m_ConnectionId;
        }

        public override bool Equals(object o)
        {
            return this == (NetworkConnection)o;
        }

        public bool Equals(NetworkConnection o)
        {
            return this == o;
        }

        public override int GetHashCode()
        {
            return m_ConnectionId.GetHashCode();
        }

        public override string ToString()
        {
            return $"NetworkConnection[id{InternalId},v{Version}]";
        }

        /// <summary>
        /// Return a fixed string representation of the connection. For use in contexts where
        /// <see cref="ToString"/> can't be used (e.g. Burst-compiled code).
        /// </summary>
        /// <returns>Fixed string representation of the connection.</returns>
        public FixedString128Bytes ToFixedString()
        {
            return FixedString.Format("NetworkConnection[id{0},v{1}]", InternalId, Version);
        }

        internal int InternalId => m_ConnectionId.Id;

        // Have to remove the driver ID that's stored in the upper byte of the version.
        internal int Version => m_ConnectionId.Version & 0x00FFFFFF;

        // The driver ID is stored in the upper byte of the version. The reason for doing this
        // instead of using a separate field is to preserve compatibility with NGO which assumes
        // NetworkConnection to have the same size as a ulong.
        internal int DriverId
        {
            get => m_ConnectionId.Version >> 24;
            set => m_ConnectionId.Version |= (value << 24);
        }
    }
}
