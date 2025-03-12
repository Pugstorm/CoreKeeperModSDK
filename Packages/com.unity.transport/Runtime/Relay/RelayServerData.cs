using System;
using System.Net;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport.Logging;

#if RELAY_SDK_INSTALLED
using Unity.Services.Relay.Models;
#endif

namespace Unity.Networking.Transport.Relay
{
    /// <summary>Connection information about the relay server.</summary>
    public unsafe struct RelayServerData
    {
        /// <summary>Endpoint the relay server can be reached on.</summary>
        /// <value>Server endpoint (IP address and port).</value>
        public NetworkEndpoint Endpoint;

        /// <summary>Nonce that will be used in the connection handshake.</summary>
        /// <value>HMAC key nonce.</value>
        public ushort Nonce;

        /// <summary>Connection data of the allocation.</summary>
        /// <value>Connection data structure.</value>
        public RelayConnectionData ConnectionData;

        /// <summary>Connection data of the host.</summary>
        /// <value>Connection data structure.</value>
        public RelayConnectionData HostConnectionData;

        /// <summary>Allocation ID for the server connection.</summary>
        /// <value>Allocation ID.</value>
        public RelayAllocationId AllocationId;

        /// <summary>HMAC key used to authentify the connection.</summary>
        /// <value>HMAC key.</value>
        public RelayHMACKey HMACKey;

        /// <summary>Whether the connection is using a secure protocol or not.</summary>
        /// <value>True if using DTLS or WSS, false if using UDP.</value>
        public readonly byte IsSecure;

        /// <summary>Whether the connection is using the WebSocket protocol.</summary>
        /// <value>True if using WSS, false if using UDP or DTLS.</value>
        public readonly byte IsWebSocket;

        // TODO Should be computed on connection binding (but not Burst compatible today).
        internal fixed byte HMAC[32];

        // String representation of the host as provided to the constructor. For IP addresses this
        // serves no purpose at all, but for hostnames it can be useful to keep it around (since we
        // would otherwise lose it after resolving it). For example, this is used for WebSockets.
        internal FixedString512Bytes HostString;

        // Common code of all byte array-based constructors.
        private RelayServerData(byte[] allocationId, byte[] connectionData, byte[] hostConnectionData, byte[] key)
        {
            Nonce = 0;
            AllocationId = RelayAllocationId.FromByteArray(allocationId);
            ConnectionData = RelayConnectionData.FromByteArray(connectionData);
            HostConnectionData = RelayConnectionData.FromByteArray(hostConnectionData);
            HMACKey = RelayHMACKey.FromByteArray(key);

            // Assign temporary values to those. Chained constructors will set them.
            Endpoint = default;
            IsSecure = 0;
            IsWebSocket = 0;

            HostString = default;

            fixed(byte* hmacPtr = HMAC)
            {
                ComputeBindHMAC(hmacPtr, Nonce, ref ConnectionData, ref HMACKey);
            }
        }

#if RELAY_SDK_INSTALLED
        /// <summary>Create a new Relay server data structure from an allocation.</summary>
        /// <param name="allocation">Allocation from which to create the server data.</param>
        /// <param name="connectionType">Type of connection to use ("udp", "dtls", "ws", or "wss").</param>
        public RelayServerData(Allocation allocation, string connectionType)
            : this(allocation.AllocationIdBytes, allocation.ConnectionData, allocation.ConnectionData, allocation.Key)
        {
            // We check against a hardcoded list of strings instead of just trying to find the
            // connection type in the endpoints since it may contains things we don't support
            // (e.g. they provide a "tcp" endpoint which we don't support).
            if (connectionType != "udp" && connectionType != "dtls" && connectionType != "ws" && connectionType != "wss")
                throw new ArgumentException($"Invalid connection type: {connectionType}. Must be udp, dtls, or wss.");

#if UNITY_WEBGL
            if (connectionType == "udp" || connectionType == "dtls")
                DebugLog.LogWarning($"Relay connection type is set to \"{connectionType}\" which is not valid on WebGL. Use \"wss\" instead.");
#endif

            IsWebSocket = connectionType == "ws" || connectionType == "wss" ? (byte)1 : (byte)0;

            foreach (var endpoint in allocation.ServerEndpoints)
            {
                if (endpoint.ConnectionType == connectionType)
                {
                    Endpoint = HostToEndpoint(endpoint.Host, (ushort)endpoint.Port);
                    IsSecure = endpoint.Secure ? (byte)1 : (byte)0;
                    HostString = endpoint.Host;
                }
            }
        }

        /// <summary>Create a new Relay server data structure from a join allocation.</summary>
        /// <param name="allocation">Allocation from which to create the server data.</param>
        /// <param name="connectionType">Type of connection to use ("udp", "dtls", "ws", or "wss").</param>
        public RelayServerData(JoinAllocation allocation, string connectionType)
            : this(allocation.AllocationIdBytes, allocation.ConnectionData, allocation.HostConnectionData, allocation.Key)
        {
            // We check against a hardcoded list of strings instead of just trying to find the
            // connection type in the endpoints since it may contains things we don't support
            // (e.g. they provide a "tcp" endpoint which we don't support).
            if (connectionType != "udp" && connectionType != "dtls" && connectionType != "ws" && connectionType != "wss")
                throw new ArgumentException($"Invalid connection type: {connectionType}. Must be udp, dtls, or wss.");

#if UNITY_WEBGL
            if (connectionType == "udp" || connectionType == "dtls")
                DebugLog.LogWarning($"Relay connection type is set to \"{connectionType}\" which is not valid on WebGL. Use \"wss\" instead.");
#endif

            IsWebSocket = connectionType == "ws" || connectionType == "wss" ? (byte)1 : (byte)0;

            foreach (var endpoint in allocation.ServerEndpoints)
            {
                if (endpoint.ConnectionType == connectionType)
                {
                    Endpoint = HostToEndpoint(endpoint.Host, (ushort)endpoint.Port);
                    IsSecure = endpoint.Secure ? (byte)1 : (byte)0;
                    HostString = endpoint.Host;
                }
            }
        }

#endif

        /// <summary>Create a new Relay server data structure.</summary>
        /// <remarks>
        /// If a hostname is provided as the "host" parameter, this constructor will perform a DNS
        /// resolution to map it to an IP address. If the hostname is not in the OS cache, this
        /// operation can possibly block for a long time (between 20 and 120 milliseconds). If this
        /// is a concern, perform the DNS resolution asynchronously and pass in the resulting IP
        /// address directly (for example with <c>System.Net.Dns.GetHostEntryAsync"</c>).
        /// </remarks>
        /// <param name="host">IP address or hostname of the Relay server.</param>
        /// <param name="port">Port of the Relay server.</param>
        /// <param name="allocationId">ID of the Relay allocation.</param>
        /// <param name="connectionData">Connection data of the allocation.</param>
        /// <param name="hostConnectionData">Connection data of the host (same as previous for hosts).</param>
        /// <param name="key">HMAC signature of the allocation.</param>
        /// <param name="isSecure">Whether the Relay connection is to be secured or not.</param>
        /// <param name="isWebSocket">Whether the Relay connection is using WebSockets or not.</param>
        public RelayServerData(string host, ushort port, byte[] allocationId, byte[] connectionData,
                               byte[] hostConnectionData, byte[] key, bool isSecure, bool isWebSocket)
            : this(allocationId, connectionData, hostConnectionData, key)
        {
            Endpoint = HostToEndpoint(host, port);
            IsSecure = isSecure ? (byte)1 : (byte)0;
            IsWebSocket = isWebSocket ? (byte)1 : (byte)0;
            HostString = host;
        }

        // Keeping this WebSocket-less version around to avoid breaking the API...
        /// <inheritdoc cref="RelayServerData(string, ushort, byte[], byte[], byte[], byte[], bool, bool)"/>
        public RelayServerData(string host, ushort port, byte[] allocationId, byte[] connectionData,
                               byte[] hostConnectionData, byte[] key, bool isSecure)
            : this(host, port, allocationId, connectionData, hostConnectionData, key, isSecure, false)
        {}

        /// <summary>Create a new Relay server data structure (low-level constructor).</summary>
        /// <param name="endpoint">Endpoint of the Relay server.</param>
        /// <param name="nonce">Nonce used in connection handshake (preferably random).</param>
        /// <param name="allocationId">ID of the Relay allocation.</param>
        /// <param name="connectionData">Connection data of the allocation.</param>
        /// <param name="hostConnectionData">Connection data of the host (use default for hosts).</param>
        /// <param name="key">HMAC signature of the allocation.</param>
        /// <param name="isSecure">Whether the Relay connection is to be secured or not.</param>
        /// <param name="isWebSocket">Whether the Relay connection is using WebSockets or not.</param>
        public RelayServerData(ref NetworkEndpoint endpoint, ushort nonce, ref RelayAllocationId allocationId,
                               ref RelayConnectionData connectionData, ref RelayConnectionData hostConnectionData,
                               ref RelayHMACKey key, bool isSecure, bool isWebSocket)
        {
            Endpoint = endpoint;
            Nonce = nonce;
            AllocationId = allocationId;
            ConnectionData = connectionData;
            HostConnectionData = hostConnectionData;
            HMACKey = key;

            IsSecure = isSecure ? (byte)1 : (byte)0;
            IsWebSocket = isWebSocket ? (byte)1 : (byte)0;

            fixed(byte* hmacPtr = HMAC)
            {
                ComputeBindHMAC(hmacPtr, Nonce, ref connectionData, ref key);
            }

            HostString = endpoint.ToFixedString();
        }

        // Keeping this WebSocket-less version around to avoid breaking the API...
        /// <inheritdoc cref="RelayServerData(ref NetworkEndpont, ushort, ref RelayAllocationId, ref RelayConnectionData, ref RelayConnectionData, ref RelayHMACKey, bool, bool)"/>
        public RelayServerData(ref NetworkEndpoint endpoint, ushort nonce, ref RelayAllocationId allocationId,
                               ref RelayConnectionData connectionData, ref RelayConnectionData hostConnectionData,
                               ref RelayHMACKey key, bool isSecure)
            : this(ref endpoint, nonce, ref allocationId, ref connectionData, ref hostConnectionData, ref key, isSecure, false)
        {}

        /// <summary>Increment the nonce and recompute the HMAC.</summary>
        public void IncrementNonce()
        {
            Nonce++;

            fixed(byte* hmacPtr = HMAC)
            {
                ComputeBindHMAC(hmacPtr, Nonce, ref ConnectionData, ref HMACKey);
            }
        }

        private static void ComputeBindHMAC(byte* result, ushort nonce, ref RelayConnectionData connectionData, ref RelayHMACKey key)
        {
            const int keyArrayLength = 64;
            var keyArray = stackalloc byte[keyArrayLength];

            fixed(byte* keyValue = &key.Value[0])
            {
                UnsafeUtility.MemCpy(keyArray, keyValue, keyArrayLength);

                const int messageLength = 263;

                var messageBytes = stackalloc byte[messageLength];

                messageBytes[0] = 0xDA;
                messageBytes[1] = 0x72;
                // ... zeros
                messageBytes[5] = (byte)nonce;
                messageBytes[6] = (byte)(nonce >> 8);
                messageBytes[7] = 255;

                fixed(byte* connValue = &connectionData.Value[0])
                {
                    UnsafeUtility.MemCpy(messageBytes + 8, connValue, 255);
                }

                HMACSHA256.ComputeHash(keyValue, keyArrayLength, messageBytes, messageLength, result);
            }
        }

        private static NetworkEndpoint HostToEndpoint(string host, ushort port)
        {
            NetworkEndpoint endpoint;

            if (NetworkEndpoint.TryParse(host, port, out endpoint, NetworkFamily.Ipv4))
                return endpoint;

            if (NetworkEndpoint.TryParse(host, port, out endpoint, NetworkFamily.Ipv6))
                return endpoint;

            // If IPv4 and IPv6 parsing didn't work, we're dealing with a hostname. In this case,
            // perform a DNS resolution to figure out what its underlying IP address is. For WebGL,
            // use a hardcoded IP address since most browsers don't support making DNS resolutions
            // directly from JavaScript. This is safe to do since on WebGL the network interface
            // will never make use of actual endpoints (other than to put in the connection list).
#if UNITY_WEBGL && !UNITY_EDITOR
            return NetworkEndpoint.AnyIpv4.WithPort(port);
#else
            var addresses = Dns.GetHostEntry(host).AddressList;
            if (addresses.Length > 0)
            {
                var address = addresses[0].ToString();
                var family = addresses[0].AddressFamily;
                return NetworkEndpoint.Parse(address, port, (NetworkFamily)family);
            }

            DebugLog.ErrorRelayMapHostFailure(host);
            return default;
#endif
        }
    }
}
