#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;

using Unity.Baselib.LowLevel;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport.Logging;
using ErrorState = Unity.Baselib.LowLevel.Binding.Baselib_ErrorState;
using ErrorCode = Unity.Baselib.LowLevel.Binding.Baselib_ErrorCode;

namespace Unity.Networking.Transport
{
    using NetworkSocket = Binding.Baselib_Socket_Handle;

    /// <summary>
    /// A TCP based network interface.
    /// </summary>
    /// <remarks>
    /// Different than <see cref="UDPNetworkInterface"/> this interface keeps a connection list and does not
    /// provide message segmentation. It doesn't event have a concept of messages and simply packs incoming chunks of
    /// data into one or more MTU-sized packets. This means packets put in the receive queue for the upper layer may
    /// contain either multiple "messages" or even an incomplete "message". It still offers the same guarantees of TCP
    /// in that data delivery is reliable and sequenced or else (in scenarios of unrecoverable data loss) the
    /// connection will be closed.
    /// </remarks>
    [BurstCompile]
    internal struct TCPNetworkInterface : INetworkInterface
    {
        static readonly NetworkSocket InvalidSocket = Binding.Baselib_Socket_Handle_Invalid;

        static bool IsValid(NetworkSocket socket) => socket.handle != default && socket.handle != InvalidSocket.handle;

        /// <summary>
        /// This class tracks all baselib socket handles created so that they can be closed on domain reload.
        /// Supposedly, this is cheaper than implementing the full Disposable Pattern in the Network Interface class (?)
        /// </summary>
        class AllSockets
        {
            private AllSockets() {}

#if ENABLE_UNITY_COLLECTIONS_CHECKS

            private struct Socket
            {
                public NetworkSocket NetworkSocket;
            }

            public static void Add(NetworkSocket socket) => instance.sockets.Add(new Socket { NetworkSocket = socket });
            public static void Remove(NetworkSocket socket) => instance.sockets.Remove(new Socket { NetworkSocket  = socket });

            private static readonly AllSockets instance = new();
            private readonly List<Socket> sockets = new();

            ~AllSockets()
            {
                foreach (var socket in sockets)
                    Binding.Baselib_Socket_Close(socket.NetworkSocket);
            }

#else
            public static void Add(NetworkSocket socket) {}
            public static void Remove(NetworkSocket socket) {}
#endif
        }

        static class TCPSocket
        {
            public static unsafe NetworkSocket Listen(ref NetworkEndpoint localEndpoint, out ErrorState errorState)
            {
                var error = default(ErrorState);
                var endpoint = localEndpoint;
                var address = &endpoint.BaselibAddress;
                var socket = Binding.Baselib_Socket_Create((Binding.Baselib_NetworkAddress_Family)address->family, Binding.Baselib_Socket_Protocol.TCP, &error);
                if (error.code == ErrorCode.Success)
                {
                    Binding.Baselib_Socket_Bind(socket, address, Binding.Baselib_NetworkAddress_AddressReuse.Allow, &error);
                    if (error.code == ErrorCode.Success)
                    {
                        // Get the end point bound not so much for the address (a wildcard address should remain unchanged) but
                        // for the port, although it's pretty rare to have a server binding with ANY_PORT (0) for pratical reasons.
                        Binding.Baselib_Socket_GetAddress(socket, address, &error);
                        Binding.Baselib_Socket_TCP_Listen(socket, &error);
                    }

                    if (error.code != ErrorCode.Success)
                    {
                        Binding.Baselib_Socket_Close(socket);
                        socket = InvalidSocket;
                    }
                }

                localEndpoint = endpoint;
                errorState = error;
                return socket;
            }

            public static unsafe NetworkSocket Accept(NetworkSocket listenSocket, out NetworkEndpoint localEndpoint)
            {
                var error = default(ErrorState);
                var acceptedSocket = Binding.Baselib_Socket_TCP_Accept(listenSocket, &error);
                localEndpoint = default;
                if (IsValid(acceptedSocket) && error.code == ErrorCode.Success)
                {
                    var address = default(NetworkEndpoint);
                    Binding.Baselib_Socket_GetAddress(acceptedSocket, &address.BaselibAddress, &error);
                    localEndpoint = address;
                    if (error.code != ErrorCode.Success)
                    {
                        DebugLog.ErrorBaselib("Failed to get local endpoint.", error);
                        Binding.Baselib_Socket_Close(acceptedSocket);
                        acceptedSocket = InvalidSocket;
                    }
                }

                return acceptedSocket;
            }

            // Note that connection in baselib is async. You have to check the completion using IsConnected.
            public static unsafe NetworkSocket Connect(NetworkEndpoint remoteEndoint)
            {
                var address = &remoteEndoint.BaselibAddress;
                var error = default(ErrorState);
                var socket = Binding.Baselib_Socket_Create((Binding.Baselib_NetworkAddress_Family)address->family, Binding.Baselib_Socket_Protocol.TCP, &error);
                if (error.code == ErrorCode.Success)
                {
                    Binding.Baselib_Socket_TCP_Connect(socket, address, Binding.Baselib_NetworkAddress_AddressReuse.Allow, &error);
                }

                if (error.code != ErrorCode.Success)
                {
                    Binding.Baselib_Socket_Close(socket);
                    socket = InvalidSocket;
                }

                return socket;
            }

            public static unsafe bool IsConnectionReady(NetworkSocket socket, out ErrorState errorState)
            {
                var error = default(ErrorState);
                var sockError = default(ErrorState);
                var sockFd = new Binding.Baselib_Socket_PollFd
                {
                    handle = socket,
                    requestedEvents = Binding.Baselib_Socket_PollEvents.Connected,
                    errorState = &sockError
                };

                Binding.Baselib_Socket_Poll(&sockFd, 1, 0, &error);
                errorState = error;
                if (error.code == ErrorCode.Success)
                {
                    if (sockFd.errorState->code != ErrorCode.Success)
                    {
                        errorState = *sockFd.errorState;
                        return false;
                    }

                    return (sockFd.resultEvents & Binding.Baselib_Socket_PollEvents.Connected) != 0;
                }

                return false;
            }

            public static void Close(NetworkSocket socket)
            {
                Binding.Baselib_Socket_Close(socket);
            }

            public static unsafe int Send(NetworkSocket socket, byte* data, int length, out ErrorState errorState)
            {
                var nbytes = 0;
                var error = default(ErrorState);

                if (length > 0)
                    nbytes = (int)Binding.Baselib_Socket_TCP_Send(socket, (IntPtr)data, (uint)length, &error);

                errorState = error;
                return nbytes;
            }

            public static unsafe int Receive(NetworkSocket socket, byte* data, int capacity, out ErrorState errorState)
            {
                var nbytes = 0;
                var error = default(ErrorState);

                if (capacity > 0)
                    nbytes = (int)Binding.Baselib_Socket_TCP_Recv(socket, (IntPtr)data, (uint)capacity, &error);

                errorState = error;
                return nbytes;
            }
        }

        unsafe struct InternalData
        {
            public NetworkSocket ListenSocket;      // the listen socket for servers (not used by clients)
            public NetworkEndpoint ListenEndpoint;  // endpoint bound by the listen socket
            public int ConnectTimeoutMS;            // maximum time to wait for a connection to complete
            public int MaxConnectAttempts;          // maximum number of connect retries
        }

        unsafe struct ConnectionData
        {
            public NetworkSocket Socket;

            public long ConnectTime;                // Connect start time
            public long LastConnectAttemptTime;     // Time of the last attempt
            public int LastConnectAttempt;          // Number of attempts so far
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private NativeList<NetworkSocket> m_SocketsPendingAdd;
        private NativeList<NetworkSocket> m_SocketsPendingRemove;
#endif

        private NativeReference<InternalData> m_InternalData;

        // Maps a connection id from the connection list to its connection data.
        private ConnectionDataMap<ConnectionData> m_ConnectionMap;

        // List of connection information carried over to the layer above
        private ConnectionList m_ConnectionList;

        internal ConnectionList CreateConnectionList()
        {
            m_ConnectionList = ConnectionList.Create();
            return m_ConnectionList;
        }

        /// <summary>
        /// Returns the local endpoint bound to the listen socket.
        /// </summary>
        /// <value>NetworkEndpoint</value>
        public unsafe NetworkEndpoint LocalEndpoint
        {
            get
            {
                // We return the first local endpoint that looks valid in the connection list, and
                // fall back to the listen endpoint if there is none. This strategy works fine on
                // clients. Technically on servers it may not always return the expected value if
                // listening on 0.0.0.0 (because there could be multiple local endpoints), but it
                // should be fine 99% of the time.
                for (int i = 0; i < m_ConnectionList.Count; i++)
                {
                    var data = m_ConnectionMap[m_ConnectionList.ConnectionAt(i)];

                    if (data.Socket.handle != IntPtr.Zero)
                    {
                        var endpoint = default(NetworkEndpoint);
                        var error = default(ErrorState);

                        Binding.Baselib_Socket_GetAddress(data.Socket, &endpoint.BaselibAddress, &error);
                        if (error.code == (int)ErrorCode.Success && endpoint.Port != 0)
                        {
                            return endpoint;
                        }
                    }
                }

                return m_InternalData.Value.ListenEndpoint;
            }
        }

        /// <summary>
        /// Initializes a instance of the UDPNetworkInterface struct.
        /// </summary>
        public unsafe int Initialize(ref NetworkSettings settings, ref int packetPadding)
        {
            // TODO: We might at some point want to apply receiveQueueCapacity to SO_RECVBUF and sendQueueCapacity to SO_SENDBUF
            var networkConfiguration = settings.GetNetworkConfigParameters();

            var state = new InternalData
            {
                ListenEndpoint = NetworkEndpoint.AnyIpv4,
                ListenSocket = InvalidSocket,
                ConnectTimeoutMS = Math.Max(0, networkConfiguration.connectTimeoutMS),
                MaxConnectAttempts = Math.Max(1, networkConfiguration.maxConnectAttempts),
            };

            m_InternalData = new NativeReference<InternalData>(state, Allocator.Persistent);
            m_ConnectionMap = new ConnectionDataMap<ConnectionData>(1, default, Allocator.Persistent);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_SocketsPendingAdd = new NativeList<NetworkSocket>(Allocator.Persistent);
            m_SocketsPendingRemove = new NativeList<NetworkSocket>(Allocator.Persistent);
#endif
            return 0;
        }

        // TODO: We may want to allow binding clients as well in the future. Granted, it's normally pretty useless, but
        // it would harmonize the behavior with the UDP interface, where we allow binding clients.

        /// <summary>
        /// Binds to the local endpoint passed. This is only applicable for a listening server. Outgoing connections
        /// do not have to bind.
        /// </summary>
        /// <param name="endpoint">A valid ipv4 or ipv6 address</param>
        /// <value>int</value>
        public unsafe int Bind(NetworkEndpoint endpoint)
        {
            var state = m_InternalData.Value;
            state.ListenEndpoint = endpoint;
            m_InternalData.Value = state;

            return 0;
        }

        public unsafe int Listen()
        {
            var state = m_InternalData.Value;
            state.ListenSocket = TCPSocket.Listen(ref state.ListenEndpoint, out var error);
            if (error.code != ErrorCode.Success)
            {
                DebugLog.ErrorBaselibBind(error, state.ListenEndpoint.Port);
                return (int)Error.StatusCode.NetworkSocketError;
            }

            AllSockets.Add(state.ListenSocket);
            m_InternalData.Value = state;
            return 0;
        }

        public void Dispose()
        {
            var socket = m_InternalData.Value.ListenSocket;
            if (IsValid(socket))
            {
                TCPSocket.Close(socket);
                AllSockets.Remove(socket);
            }

            m_InternalData.Dispose();

            for (int i = 0; i < m_ConnectionMap.Length; ++i)
            {
                socket = m_ConnectionMap.DataAt(i).Socket;
                if (IsValid(socket))
                    TCPSocket.Close(socket);

                AllSockets.Remove(socket);
            }


            m_ConnectionMap.Dispose();
            m_ConnectionList.Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_SocketsPendingAdd.Dispose();
            m_SocketsPendingRemove.Dispose();
#endif
        }

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dep)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Remove closed sockets from the list of sockets to be closed in a domain reload.
            // This is here because we can't use managed AllSockets to track sockets from within burst compiled jobs.
            for (int i = 0; i < m_SocketsPendingRemove.Length; i++)
                AllSockets.Remove(m_SocketsPendingRemove[i]);
            m_SocketsPendingRemove.Clear();

            // Add connecting sockets to the list of sockets to be closed in a domain reload.
            // This is here because we can't use managed AllSockets to track sockets from within burst compiled jobs.
            for (int i = 0; i < m_SocketsPendingAdd.Length; i++)
                AllSockets.Add(m_SocketsPendingAdd[i]);
            m_SocketsPendingAdd.Clear();
#endif

            return new ReceiveJob
            {
                ReceiveQueue = arguments.ReceiveQueue,
                InternalData = m_InternalData,
                ConnectionList = m_ConnectionList,
                ConnectionMap = m_ConnectionMap,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                SocketsPendingAdd = m_SocketsPendingAdd,
                SocketsPendingRemove = m_SocketsPendingRemove,

#endif
                Time = arguments.Time,
            }.Schedule(dep);
        }

        [BurstCompile]
        struct ReceiveJob : IJob
        {
            public PacketsQueue ReceiveQueue;
            public NativeReference<InternalData> InternalData;
            public ConnectionList ConnectionList;

            // The job system doesn't like that ConnectionData stores an IntPtr as the socket handle. This problem also
            // happens in UDPNetworkInterface which is also forced to use [NativeDisableUnsafePtrRestriction].
            // Without the decorator the job system throws the exception:
            //   "ReceiveJob.ConnectionMap.m_DefaultDataValue.Socket.handle uses unsafe Pointers which is not allowed.
            //   Unsafe Pointers can lead to crashes and no safety against race conditions can be provided.\nIf you
            //   really need to use unsafe pointers, you can disable this..."
            [NativeDisableUnsafePtrRestriction]
            public ConnectionDataMap<ConnectionData> ConnectionMap;

            public long Time;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            public NativeList<NetworkSocket> SocketsPendingRemove;
            public NativeList<NetworkSocket> SocketsPendingAdd;

            private void AllSocketsDeferredAdd(NetworkSocket socket) => SocketsPendingAdd.Add(socket);
            private void AllSocketsDeferredRemove(NetworkSocket socket) => SocketsPendingRemove.Add(socket);
#else
            private void AllSocketsDeferredAdd(NetworkSocket socket) {}
            private void AllSocketsDeferredRemove(NetworkSocket socket) {}
#endif

            private void Abort(ref ConnectionId connectionId, ref ConnectionData connectionData, Error.DisconnectReason reason = default)
            {
                ConnectionList.FinishDisconnecting(ref connectionId, reason);
                ConnectionMap.ClearData(ref connectionId);
                TCPSocket.Close(connectionData.Socket);
                AllSocketsDeferredRemove(connectionData.Socket);
            }

            public unsafe void Execute()
            {
                // Accept new connections.
                if (IsValid(InternalData.Value.ListenSocket))
                {
                    var acceptedSocket = TCPSocket.Accept(InternalData.Value.ListenSocket, out var localEndpoint);
                    if (IsValid(acceptedSocket))
                    {
                        AllSocketsDeferredAdd(acceptedSocket);

                        var connectionId = ConnectionList.StartConnecting(ref localEndpoint);
                        ConnectionList.FinishConnectingFromRemote(ref connectionId);
                        ConnectionMap[connectionId] = new ConnectionData
                        {
                            Socket = acceptedSocket
                        };
                    }
                }

                // Update each connection from the connection list
                var count = ConnectionList.Count;
                for (int i = 0; i < count; i++)
                {
                    var connectionId = ConnectionList.ConnectionAt(i);
                    var connectionState = ConnectionList.GetConnectionState(connectionId);

                    if (connectionState == NetworkConnection.State.Disconnected)
                        continue;

                    var connectionData = ConnectionMap[connectionId];

                    // Detect if the upper layer is requesting to connect.
                    if (connectionState == NetworkConnection.State.Connecting)
                    {
                        // Initialize ConnectTime. The time here is a signed 64bit and we're never going to run at time
                        // 0 so if the connection has ConnectTime == 0 it's the creation of this connection data and
                        // just have to initialize the ConnectTime in a way to trigger the first connection try.
                        if (connectionData.ConnectTime == 0)
                        {
                            connectionData.ConnectTime = Time;
                            connectionData.LastConnectAttemptTime = Math.Max(0, Time - InternalData.Value.ConnectTimeoutMS);
                        }

                        // Disconnect if maximum connection attempts reached
                        if (connectionData.LastConnectAttempt >= InternalData.Value.MaxConnectAttempts)
                        {
                            ConnectionList.StartDisconnecting(ref connectionId);
                            Abort(ref connectionId, ref connectionData, Error.DisconnectReason.MaxConnectionAttempts);
                            continue;
                        }

                        // Check if it's time to retry connect
                        if (Time - connectionData.LastConnectAttemptTime >= InternalData.Value.ConnectTimeoutMS)
                        {
                            var remoteEndpoint = ConnectionList.GetConnectionEndpoint(connectionId);
                            if (!IsValid(connectionData.Socket))
                            {
                                connectionData.Socket = TCPSocket.Connect(remoteEndpoint);
                                if (IsValid(connectionData.Socket))
                                    AllSocketsDeferredAdd(connectionData.Socket);
                            }

                            connectionData.LastConnectAttempt++;
                            connectionData.LastConnectAttemptTime = Time;
                        }

                        if (IsValid(connectionData.Socket))
                        {
                            if (TCPSocket.IsConnectionReady(connectionData.Socket, out var readyError))
                            {
                                ConnectionList.FinishConnectingFromLocal(ref connectionId);
                            }
                            else if (readyError.code != ErrorCode.Success)
                            {
                                // If something went wrong trying to complete the connection just close this socket
                                // and in the next attempt we'll create a new one.
                                TCPSocket.Close(connectionData.Socket);
                                AllSocketsDeferredRemove(connectionData.Socket);
                                connectionData.Socket = InvalidSocket;
                            }
                        }

                        ConnectionMap[connectionId] = connectionData;
                        continue;
                    }

                    // Detect if the upper layer is requesting to disconnect.
                    if (connectionState == NetworkConnection.State.Disconnecting)
                    {
                        Abort(ref connectionId, ref connectionData);
                        continue;
                    }

                    var endpoint = ConnectionList.GetConnectionEndpoint(connectionId);
                    ErrorState error = default;
                    // Read data from the connection if we can. Receive should return chunks of up to MTU.
                    // Close the connection in case of a receive error.
                    while (true)
                    {
                        // No need to disconnect in case the receive queue becomes full just let the TCP socket buffer
                        // the incoming data.
                        if (!ReceiveQueue.EnqueuePacket(out var packetProcessor))
                            break;

                        packetProcessor.ConnectionRef = connectionId;
                        packetProcessor.EndpointRef = endpoint;
                        var nbytes = TCPSocket.Receive(connectionData.Socket, (byte*)packetProcessor.GetUnsafePayloadPtr(), packetProcessor.BytesAvailableAtEnd, out error);
                        if (error.code != ErrorCode.Success || nbytes <= 0)
                        {
                            packetProcessor.Drop();
                            break;
                        }
                        packetProcessor.SetUnsafeMetadata(nbytes);
                    }

                    if (error.code != ErrorCode.Success)
                    {
                        ConnectionList.StartDisconnecting(ref connectionId);
                        Abort(ref connectionId, ref connectionData, Error.DisconnectReason.ProtocolError);
                        break;
                    }

                    // Update the connection data
                    ConnectionMap[connectionId] = connectionData;
                }
            }
        }

        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dep)
        {
            return new SendJob
            {
                SendQueue = arguments.SendQueue,
                InternalData = m_InternalData,
                ConnectionMap = m_ConnectionMap,
                ConnectionList = m_ConnectionList,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                SocketsPendingRemove = m_SocketsPendingRemove,
#endif
            }.Schedule(dep);
        }

        [BurstCompile]
        unsafe struct SendJob : IJob
        {
            public PacketsQueue SendQueue;

            public NativeReference<InternalData> InternalData;

            public ConnectionList ConnectionList;

            // See the comment in ReceiveJob
            [NativeDisableUnsafePtrRestriction]
            public ConnectionDataMap<ConnectionData> ConnectionMap;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            public NativeList<NetworkSocket> SocketsPendingRemove;

            private void AllSocketsDeferredRemove(NetworkSocket socket) => SocketsPendingRemove.Add(socket);
#else
            private void AllSocketsDeferredRemove(NetworkSocket socket) {}
#endif

            public void Execute()
            {
                // Each packet is sent individually. The connection is aborted if a packet cannot be transmiited
                // entirely.
                for (int i = 0; i < SendQueue.Count; i++)
                {
                    var packetProcessor = SendQueue[i];
                    if (packetProcessor.Length == 0)
                        continue;

                    var connectionId = packetProcessor.ConnectionRef;
                    var connectionState = ConnectionList.GetConnectionState(connectionId);

                    if (connectionState == NetworkConnection.State.Disconnected)
                        continue;

                    var connectionData = ConnectionMap[connectionId];

                    var nbytes = TCPSocket.Send(connectionData.Socket, (byte*)packetProcessor.GetUnsafePayloadPtr() + packetProcessor.Offset, packetProcessor.Length, out var error);
                    if (error.code != ErrorCode.Success || nbytes != packetProcessor.Length)
                    {
                        if (error.code != ErrorCode.Disconnected)
                        {
                            // Likely incomplete send. Still want to disconnect but log an error.
                            var endpoint = ConnectionList.GetConnectionEndpoint(connectionId).ToFixedString();
                            DebugLog.LogError($"Overflow of OS send buffer while trying to send data to {endpoint}. Closing connection.");
                        }

                        ConnectionList.StartDisconnecting(ref connectionId);
                        ConnectionList.FinishDisconnecting(ref connectionId, Error.DisconnectReason.ProtocolError);
                        ConnectionMap.ClearData(ref connectionId);
                        TCPSocket.Close(connectionData.Socket);
                        AllSocketsDeferredRemove(connectionData.Socket);
                        continue;
                    }

                    ConnectionMap[connectionId] = connectionData;
                }
            }
        }
    }
}
#endif
