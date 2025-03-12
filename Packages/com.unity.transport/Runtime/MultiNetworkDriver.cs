using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// The <c>MultiNetworkDriver</c> structure is a way to manipulate multiple instances of
    /// <see cref="NetworkDriver"/> at the same time. This abstraction is meant to make it easy to
    /// work with servers that must accept different connection types (e.g. both UDP and WebSocket
    /// connections). This is useful for cross-play support across different platforms.
    /// </summary>
    /// <example>
    /// This code below shows how to create a <c>MultiNetworkDriver</c> that accepts both UDP and
    /// WebSocket connections.
    /// <code>
    ///     var udpDriver = NetworkDriver.Create(new UDPNetworkInterface());
    ///     udpDriver.Bind(NetworkEndpoint.AnyIpv4.WithPort(7777)); // UDP port
    ///     udpDriver.Listen();
    ///
    ///     var wsDriver = NetworkDriver.Create(new WebSocketNetworkInterface());
    ///     wsDriver.Bind(NetworkEndpoint.AnyIpv4.WithPort(7777)); // TCP port
    ///     wsDriver.Listen();
    ///
    ///     var multiDriver = MultiNetworkDriver.Create();
    ///     multiDriver.AddDriver(udpDriver);
    ///     multiDriver.AddDriver(wsDriver);
    /// </code>
    /// The created <c>MultiNetworkDriver</c> can then be used as one would use a
    /// <see cref="NetworkDriver"/> since they share most of the same APIs.
    /// </example>
    public struct MultiNetworkDriver : IDisposable
    {
        /// <summary>
        /// The maximum number of drivers that can be added to a <c>MultiNetworkDriver</c>.
        /// </summary>
        /// <value>Number of drivers.</value>
        public const int MaxDriverCount = 4;

        // Those need to be internal so that GetDriverRef can access them.
        internal NetworkDriver Driver1;
        internal NetworkDriver Driver2;
        internal NetworkDriver Driver3;
        internal NetworkDriver Driver4;

        /// <summary>Number of drivers that have been added.</summary>
        /// <value>Number of drivers.</value>
        public int DriverCount { get; private set; }

        /// <summary>Whether the <c>MultiNetworkDriver</c> has been created.</summary>
        /// <value>True if created, false otherwise.</value>
        public bool IsCreated => Driver1.IsCreated;

        /// <summary>Create a new <c>MultiNetworkDriver</c> instance.</summary>
        /// <returns>The new <c>MultiNetworkDriver</c> instance.</returns>
        public static MultiNetworkDriver Create()
        {
            var multiDriver = default(MultiNetworkDriver);

            // When safety checks are enabled, the safety system will not allow us to pass
            // default-valued containers to jobs. That's problematic for MultiNetworkDriver because
            // all non-added drivers would normally be default-valued. Instead, when safety checks
            // are enabled, we just create all drivers with do-nothing interfaces.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            multiDriver.Driver1 = NetworkDriver.Create(new DummyNetworkInterface());
            multiDriver.Driver2 = NetworkDriver.Create(new DummyNetworkInterface());
            multiDriver.Driver3 = NetworkDriver.Create(new DummyNetworkInterface());
            multiDriver.Driver4 = NetworkDriver.Create(new DummyNetworkInterface());
#else
            multiDriver.Driver1 = default;
            multiDriver.Driver2 = default;
            multiDriver.Driver3 = default;
            multiDriver.Driver4 = default;
#endif

            multiDriver.DriverCount = 0;
            return multiDriver;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckNewDriver(NetworkDriver driver)
        {
            if (!driver.IsCreated)
                throw new ArgumentException("Invalid driver (driver is not created).");

            // We can't just check the number of connections in the list because that would include
            // connections that are disconnected, and connections pending an Accept call.
            var connections = driver.m_NetworkStack.Connections;
            var pendingAccept = connections.QueryIncomingConnections(Allocator.Temp);
            if (connections.Count - connections.FreeList.Count - pendingAccept.Length > 0)
                throw new ArgumentException("Invalid driver (driver already has active connections).");

            // We only care about the number of pipelines of the first (valid) driver we have, since
            // the validation will ensure all other (valid) drivers will have the same number.
            for (int id = 1; id <= DriverCount; id++)
            {
                var pipelines = this.GetDriverRef(id).PipelineCount;
                if (pipelines != driver.PipelineCount)
                    throw new ArgumentException($"Invalid driver (driver must have {pipelines} pipelines, but has {driver.PipelineCount}).");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckDriverId(int id)
        {
            if (id < 1 || id > DriverCount)
                throw new ArgumentException($"Invalid driver ID {id} (must be between 1 and {DriverCount}).");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckConnection(NetworkConnection connection)
        {
            // Default-valued ID probably means the user passed in a connection that wasn't obtained
            // from one of the methods provided by MultiNetworkDriver. Raise a specific exception
            // for this case since the one from CheckDriverId won't be very useful.
            if (connection.DriverId == 0)
                throw new ArgumentException("Invalid NetworkConnection (likely not obtained from MultiNetworkDriver).");

            CheckDriverId(connection.DriverId);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private unsafe void CheckWriterHandle(DataStreamWriter writer)
        {
            var pendingSendPtr = (NetworkDriver.Concurrent.PendingSend*)writer.m_SendHandleData;
            if (pendingSendPtr == null)
                throw new ArgumentException("Invalid DataStreamWriter (likely not obtained from BeginSend call).");

            // As for CheckConnection, we handle the default-valued ID specifically to raise an
            // exception with a more useful error message than what we'd get with CheckDriverId.
            if (pendingSendPtr->Connection.DriverId == 0)
                throw new ArgumentException("Invalid DataStreamWriter (likely not obtained from MultiNetworkDriver).");

            CheckDriverId(pendingSendPtr->Connection.DriverId);
        }

        /// <summary>
        /// Add a <see cref="NetworkDriver"/> instance to the <c>MultiNetworkDriver</c>. This driver
        /// instance must not already have any active connections, and must have the same number of
        /// pipelines as previously added instances. Drivers that are intended to take on a server
        /// role must also already be in the listening state.
        /// </summary>
        /// <remarks>
        /// The <c>MultiNetworkDriver</c> takes ownership of the <see cref="NetworkDriver"/>. While
        /// it is possible to keep operating on the <see cref="NetworkDriver"/> once it's been
        /// added, this is not recommended (at least for the operations that are already covered by
        /// the <c>MultiNetworkDriver</c> API).
        /// </remarks>
        /// <param name="driver"><see cref="NetworkDriver"/> instance to add.</param>
        /// <returns>
        /// An ID identifying the <see cref="NetworkDriver"/> inside the <c>MultiNetworkDriver</c>.
        /// This ID can be used to retrieve the driver using the <see cref="GetDriver"/> method.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// If the <c>MultiNetworkDriver</c> is at capacity (see <see cref="MaxDriverCount"/>).
        /// </exception>
        /// <exception cref="ArgumentException">
        /// If the driver already has active connections, or if it doesn't have as many pipelines as
        /// previously added drivers. Note that this exception is only thrown when safety checks are
        /// enabled (i.e. in the editor), otherwise the driver will be added anyway.
        /// </exception>
        public int AddDriver(NetworkDriver driver)
        {
            CheckNewDriver(driver);

            if (DriverCount == MaxDriverCount)
                throw new InvalidOperationException("Capacity of MultiNetworkDriver has been reached.");

            DriverCount++;
            this.GetDriverRef(DriverCount).Dispose();
            this.GetDriverRef(DriverCount) = driver;
            return DriverCount;
        }

        /// <summary>
        /// Get a <see cref="NetworkDriver"/> previously added with <see cref="AddDriver"/>.
        /// </summary>
        /// <remarks>
        /// This method is provided as an escape hatch for use cases not covered by the
        /// <c>MultiNetworkDriver</c> API. Using this method to perform operations on a driver that
        /// could be performed through <c>MultiNetworkDriver</c> is likely to result in errors or
        /// corrupted state.
        /// </remarks>
        /// <param name="id">ID of the driver as returned by <see cref="AddDriver"/>.</param>
        /// <returns>The <see cref="NetworkDriver"/> referred to by <c>id</c>.</returns>
        /// <exception cref="ArgumentException">
        /// If <c>id</c> does not refer to a driver that's part of the <c>MultiNetworkDriver</c>.
        /// </exception>
        public NetworkDriver GetDriver(int id)
        {
            CheckDriverId(id);
            return this.GetDriverRef(id);
        }

        /// <summary>
        /// Get the <see cref="NetworkDriver"/> associated with the given connection. The connection
        /// must have been obtained from the <c>MultiNetworkDriver</c> before.
        /// </summary>
        /// <param name="connection">Connection to get the driver of.</param>
        /// <returns>The <see cref="NetworkDriver"/> associated to <c>connection</c>.</returns>
        /// <exception cref="ArgumentException">
        /// If <c>connection</c> was not obtained by a prior call to this <c>MultiNetworkDriver</c>.
        /// </exception>
        public NetworkDriver GetDriverForConnection(NetworkConnection connection)
        {
            CheckConnection(connection);
            return this.GetDriverRef(connection.DriverId);
        }

        public void Dispose()
        {
            DriverCount = 0;
            for (int id = 1; id <= MaxDriverCount; id++)
            {
                this.GetDriverRef(id).Dispose();
            }
        }

        /// <inheritdoc cref="NetworkDriver.ToConcurrent"/>
        public Concurrent ToConcurrent()
        {
            return new Concurrent
            {
                Driver1 = Driver1.ToConcurrent(),
                Driver2 = Driver2.ToConcurrent(),
                Driver3 = Driver3.ToConcurrent(),
                Driver4 = Driver4.ToConcurrent(),
            };
        }

        /// <summary>
        /// Structure that can be used to access a <c>MultiNetworkDriver</c> from multiple jobs.
        /// Only a subset of operations are supported because not all operations are safe to perform
        /// concurrently. Must be obtained with the <see cref="ToConcurrent"/> method.
        /// </summary>
        public struct Concurrent
        {
            // Those need to be internal so that GetDriverRef can access them.
            internal NetworkDriver.Concurrent Driver1;
            internal NetworkDriver.Concurrent Driver2;
            internal NetworkDriver.Concurrent Driver3;
            internal NetworkDriver.Concurrent Driver4;

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckConnection(NetworkConnection connection)
            {
                // Default-valued ID probably means the user passed in a connection that wasn't
                // obtained from one of the methods provided by MultiNetworkDriver. Raise a specific
                // exception for this case since the one from GetDriverRef won't be very useful.
                if (connection.DriverId == 0)
                    throw new ArgumentException("Invalid NetworkConnection (likely not obtained from MultiNetworkDriver).");
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private unsafe void CheckWriterHandle(DataStreamWriter writer)
            {
                var pendingSendPtr = (NetworkDriver.Concurrent.PendingSend*)writer.m_SendHandleData;
                if (pendingSendPtr == null)
                    throw new ArgumentException("Invalid DataStreamWriter (likely not obtained from BeginSend call).");

                // As for CheckConnection, we handle the default-valued ID specifically to raise an
                // exception with a more useful error message than what we'd get with GetDriverRef.
                if (pendingSendPtr->Connection.DriverId == 0)
                    throw new ArgumentException("Invalid DataStreamWriter (likely not obtained from MultiNetworkDriver).");
            }

            /// <inheritdoc cref="MultiNetworkDriver.GetConnectionState"/>
            public NetworkConnection.State GetConnectionState(NetworkConnection connection)
            {
                CheckConnection(connection);
                return this.GetDriverRef(connection.DriverId).GetConnectionState(connection);
            }

            /// <inheritdoc cref="MultiNetworkDriver.PopEventForConnection(NetworkConnection, out DataStreamReader)"/>
            public NetworkEvent.Type PopEventForConnection(NetworkConnection connection, out DataStreamReader reader)
            {
                return PopEventForConnection(connection, out reader, out var _);
            }

            /// <inheritdoc cref="MultiNetworkDriver.PopEventForConnection(NetworkConnection, out DataStreamReader, out NetworkPipeline)"/>
            public NetworkEvent.Type PopEventForConnection(NetworkConnection connection, out DataStreamReader reader, out NetworkPipeline pipe)
            {
                CheckConnection(connection);
                return this.GetDriverRef(connection.DriverId).PopEventForConnection(connection, out reader, out pipe);
            }

            /// <inheritdoc cref="MultiNetworkDriver.BeginSend(NetworkConnection, out DataStreamWriter, int)"/>
            public int BeginSend(NetworkConnection connection, out DataStreamWriter writer, int requiredPayloadSize = 0)
            {
                return BeginSend(NetworkPipeline.Null, connection, out writer, requiredPayloadSize);
            }

            /// <inheritdoc cref="MultiNetworkDriver.BeginSend(NetworkPipeline, NetworkConnection, out DataStreamWriter, int)"/>
            public int BeginSend(NetworkPipeline pipe, NetworkConnection connection, out DataStreamWriter writer, int requiredPayloadSize = 0)
            {
                CheckConnection(connection);
                return this.GetDriverRef(connection.DriverId).BeginSend(pipe, connection, out writer, requiredPayloadSize);
            }

            /// <inheritdoc cref="MultiNetworkDriver.EndSend"/>
            public unsafe int EndSend(DataStreamWriter writer)
            {
                CheckWriterHandle(writer);

                var pendingSendPtr = (NetworkDriver.Concurrent.PendingSend*)writer.m_SendHandleData;
                if (pendingSendPtr == null)
                    return (int)Error.StatusCode.NetworkSendHandleInvalid;

                return this.GetDriverRef(pendingSendPtr->Connection.DriverId).EndSend(writer);
            }

            /// <inheritdoc cref="MultiNetworkDriver.AbortSend"/>
            public unsafe void AbortSend(DataStreamWriter writer)
            {
                CheckWriterHandle(writer);

                var pendingSendPtr = (NetworkDriver.Concurrent.PendingSend*)writer.m_SendHandleData;
                if (pendingSendPtr == null)
                {
                    DebugLog.LogError("Invalid DataStreamWriter (likely not obtained from BeginSend call).");
                    return;
                }

                this.GetDriverRef(pendingSendPtr->Connection.DriverId).AbortSend(writer);
            }
        }

        /// <inheritdoc cref="NetworkDriver.ScheduleUpdate"/>
        public JobHandle ScheduleUpdate(JobHandle dependency = default)
        {
            var job = dependency;
            for (int id = 1; id <= DriverCount; id++)
            {
                ref var driver = ref this.GetDriverRef(id);
                job = JobHandle.CombineDependencies(job, driver.ScheduleUpdate(dependency));
            }

            return job;
        }

        /// <inheritdoc cref="NetworkDriver.ScheduleFlushSend"/>
        public JobHandle ScheduleFlushSend(JobHandle dependency = default)
        {
            var job = dependency;
            for (int id = 1; id <= DriverCount; id++)
            {
                ref var driver = ref this.GetDriverRef(id);
                job = JobHandle.CombineDependencies(job, driver.ScheduleFlushSend(dependency));
            }

            return job;
        }

        /// <inheritdoc cref="NetworkDriver.RegisterPipelineStage"/>
        public void RegisterPipelineStage<T>(T stage) where T : unmanaged, INetworkPipelineStage
        {
            for (int id = 1; id <= DriverCount; id++)
            {
                this.GetDriverRef(id).RegisterPipelineStage<T>(stage);
            }
        }

        /// <inheritdoc cref="NetworkDriver.CreatePipeline"/>
        public NetworkPipeline CreatePipeline(params Type[] stages)
        {
            var stageIds = new NativeArray<NetworkPipelineStageId>(stages.Length, Allocator.Temp);
            for (int i = 0; i < stages.Length; i++)
            {
                stageIds[i] = NetworkPipelineStageId.Get(stages[i]);
            }
            return CreatePipeline(stageIds);
        }

        /// <inheritdoc cref="NetworkDriver.CreatePipeline"/>
        public NetworkPipeline CreatePipeline(NativeArray<NetworkPipelineStageId> stages)
        {
            // Can return any pipeline created since they're all going to be the same.
            NetworkPipeline pipeline = default;
            for (int id = 1; id <= DriverCount; id++)
            {
                pipeline = this.GetDriverRef(id).CreatePipeline(stages);
            }

            return pipeline;
        }

        /// <inheritdoc cref="NetworkDriver.Accept"/>
        public NetworkConnection Accept()
        {
            for (int id = 1; id <= DriverCount; id++)
            {
                if (this.GetDriverRef(id).Listening)
                {
                    var connection = this.GetDriverRef(id).Accept();
                    if (connection != default)
                    {
                        connection.DriverId = id;
                        return connection;
                    }
                }
            }

            return default;
        }

        /// <inheritdoc cref="NetworkDriver.Connect"/>
        /// <param name="driverId">
        /// ID of the driver to connect, as obtained with <see cref="AddDriver"/>.
        /// </param>
        public NetworkConnection Connect(int driverId, NetworkEndpoint endpoint)
        {
            CheckDriverId(driverId);

            var connection = this.GetDriverRef(driverId).Connect(endpoint);
            if (connection != default)
            {
                connection.DriverId = driverId;
            }

            return connection;
        }

        /// <inheritdoc cref="NetworkDriver.Disconnect"/>
        /// <exception cref="ArgumentException">
        /// If <c>connection</c> was not obtained by a prior call to this <c>MultiNetworkDriver</c>.
        /// </exception>
        public void Disconnect(NetworkConnection connection)
        {
            CheckConnection(connection);
            this.GetDriverRef(connection.DriverId).Disconnect(connection);
        }

        /// <inheritdoc cref="NetworkDriver.GetConnectionState"/>
        /// <exception cref="ArgumentException">
        /// If <c>connection</c> was not obtained by a prior call to this <c>MultiNetworkDriver</c>.
        /// </exception>
        public NetworkConnection.State GetConnectionState(NetworkConnection connection)
        {
            CheckConnection(connection);
            return this.GetDriverRef(connection.DriverId).GetConnectionState(connection);
        }

        /// <inheritdoc cref="NetworkDriver.GetRemoteEndpoint"/>
        /// <exception cref="ArgumentException">
        /// If <c>connection</c> was not obtained by a prior call to this <c>MultiNetworkDriver</c>.
        /// </exception>
        public NetworkEndpoint GetRemoteEndpoint(NetworkConnection connection)
        {
            CheckConnection(connection);
            return this.GetDriverRef(connection.DriverId).GetRemoteEndpoint(connection);
        }

        /// <inheritdoc cref="NetworkDriver.PopEvent(out NetworkConnection, out DataStreamReader)"/>
        public NetworkEvent.Type PopEvent(out NetworkConnection connection, out DataStreamReader reader)
        {
            return PopEvent(out connection, out reader, out _);
        }

        /// <inheritdoc cref="NetworkDriver.PopEvent(out NetworkConnection, out DataStreamReader, out NetworkPipeline)"/>
        public NetworkEvent.Type PopEvent(out NetworkConnection connection, out DataStreamReader reader, out NetworkPipeline pipe)
        {
            connection = default;
            reader = default;
            pipe = default;

            for (int id = 1; id <= DriverCount; id++)
            {
                var ev = this.GetDriverRef(id).PopEvent(out connection, out reader, out pipe);
                if (ev != NetworkEvent.Type.Empty)
                {
                    connection.DriverId = id;
                    return ev;
                }
            }

            return NetworkEvent.Type.Empty;
        }

        /// <inheritdoc cref="NetworkDriver.PopEventForConnection(NetworkConnection, out DataStreamReader)"/>
        /// <exception cref="ArgumentException">
        /// If <c>connection</c> was not obtained by a prior call to this <c>MultiNetworkDriver</c>.
        /// </exception>
        public NetworkEvent.Type PopEventForConnection(NetworkConnection connection, out DataStreamReader reader)
        {
            return PopEventForConnection(connection, out reader, out _);
        }

        /// <inheritdoc cref="NetworkDriver.PopEventForConnection(NetworkConnection, out DataStreamReader, out NetworkPipeline)"/>
        /// <exception cref="ArgumentException">
        /// If <c>connection</c> was not obtained by a prior call to this <c>MultiNetworkDriver</c>.
        /// </exception>
        public NetworkEvent.Type PopEventForConnection(NetworkConnection connection, out DataStreamReader reader, out NetworkPipeline pipe)
        {
            CheckConnection(connection);
            return this.GetDriverRef(connection.DriverId).PopEventForConnection(connection, out reader, out pipe);
        }

        /// <inheritdoc cref="NetworkDriver.BeginSend(NetworkConnection, out DataStreamWriter, int)"/>
        /// <exception cref="ArgumentException">
        /// If <c>connection</c> was not obtained by a prior call to this <c>MultiNetworkDriver</c>.
        /// </exception>
        public int BeginSend(NetworkConnection connection, out DataStreamWriter writer, int requiredPayloadSize = 0)
        {
            return BeginSend(NetworkPipeline.Null, connection, out writer, requiredPayloadSize);
        }

        /// <inheritdoc cref="NetworkDriver.BeginSend(NetworkPipeline, NetworkConnection, out DataStreamWriter, int)"/>
        /// <exception cref="ArgumentException">
        /// If <c>connection</c> was not obtained by a prior call to this <c>MultiNetworkDriver</c>.
        /// </exception>
        public int BeginSend(NetworkPipeline pipe, NetworkConnection connection, out DataStreamWriter writer, int requiredPayloadSize = 0)
        {
            CheckConnection(connection);
            return this.GetDriverRef(connection.DriverId).BeginSend(pipe, connection, out writer, requiredPayloadSize);
        }

        /// <inheritdoc cref="NetworkDriver.EndSend"/>
        /// <exception cref="ArgumentException">
        /// If <c>writer</c> was not obtained by a prior call to <see cref="BeginSend"/>.
        /// </exception>
        public unsafe int EndSend(DataStreamWriter writer)
        {
            CheckWriterHandle(writer);

            var pendingSendPtr = (NetworkDriver.Concurrent.PendingSend*)writer.m_SendHandleData;
            if (pendingSendPtr == null)
                return (int)Error.StatusCode.NetworkSendHandleInvalid;

            return this.GetDriverRef(pendingSendPtr->Connection.DriverId).EndSend(writer);
        }

        /// <inheritdoc cref="NetworkDriver.AbortSend"/>
        /// <exception cref="ArgumentException">
        /// If <c>writer</c> was not obtained by a prior call to <see cref="BeginSend"/>.
        /// </exception>
        public unsafe void AbortSend(DataStreamWriter writer)
        {
            CheckWriterHandle(writer);

            var pendingSendPtr = (NetworkDriver.Concurrent.PendingSend*)writer.m_SendHandleData;
            if (pendingSendPtr == null)
            {
                DebugLog.LogError("Invalid DataStreamWriter (likely not obtained from BeginSend call).");
                return;
            }

            this.GetDriverRef(pendingSendPtr->Connection.DriverId).AbortSend(writer);
        }
    }

    internal static class MultiNetworkDriverExtensions
    {
        // A structure is not allowed to return its own fields by reference. Using an extension
        // method extends the scope enough to make this safe. See this for details:
        // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs8170

        internal static ref NetworkDriver GetDriverRef(this ref MultiNetworkDriver multiDriver, int id)
        {
            switch (id)
            {
                case 1: return ref multiDriver.Driver1;
                case 2: return ref multiDriver.Driver2;
                case 3: return ref multiDriver.Driver3;
                case 4: return ref multiDriver.Driver4;
            }

            throw new ArgumentException($"Invalid driver ID {id}.");
        }

        internal static ref NetworkDriver.Concurrent GetDriverRef(this ref MultiNetworkDriver.Concurrent multiDriver, int id)
        {
            switch (id)
            {
                case 1: return ref multiDriver.Driver1;
                case 2: return ref multiDriver.Driver2;
                case 3: return ref multiDriver.Driver3;
                case 4: return ref multiDriver.Driver4;
            }

            throw new ArgumentException($"Invalid driver ID {id}.");
        }
    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    internal struct DummyNetworkInterface : INetworkInterface
    {
        public NetworkEndpoint LocalEndpoint { get => default; }

        public int Initialize(ref NetworkSettings settings, ref int packetPadding) => 0;
        public void Dispose() {}

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dep) => dep;
        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dep) => dep;

        public int Bind(NetworkEndpoint endpoint) => 0;
        public int Listen() => 0;
    }
#endif
}