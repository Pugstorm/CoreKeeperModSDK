using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Networking.Transport;

namespace Unity.NetCode
{
    /// <summary>
    /// The transport category/type use by a NetworkDriver.
    /// </summary>
    public enum TransportType : int
    {
        /// <summary>
        /// Not configured, or unsupported tramsport interface. The transport type for a registered driver instance
        /// is always valid, unless the driver creation fail.
        /// </summary>
        Invalid = 0,
        /// <summary>
        /// An inter-process like communication channel with 0 latency and guaratee delivery.
        /// </summary>
        IPC,
        /// <summary>
        /// A socket based communication channel. WebSocket, UDP, TCP or any similar communication channel fit that category.
        /// </summary>
        Socket,
    }

    /// <summary>
    /// Store and manage an array of NetworkDriver. The capacity is fixed to <see cref="Capacity"/>.
    /// The driver registration should start by calling BeginDriverRegistration() and terminate with EndDriverRegistration().
    /// The store also provide some accessor and utlilty methods.
    /// </summary>
    public struct NetworkDriverStore
    {
        /// <summary>
        /// Struct that contains a <see cref="NetworkDriver"/> and relative pipelines.
        /// </summary>
        public struct NetworkDriverInstance
        {
            /// <summary>
            /// The <see cref="NetworkDriver"/> instance. Can be invalid if the NetworkDriver instance has not
            /// been initialized.
            /// </summary>
            public NetworkDriver driver;
            /// <summary>
            /// The pipeline used for sending reliable messages
            /// </summary>
            public NetworkPipeline reliablePipeline;
            /// <summary>
            /// The pipeline used for sending unreliable messages and snapshots
            /// </summary>
            public NetworkPipeline unreliablePipeline;
            /// <summary>
            /// The pipeline used for sending big unreliable messages that requires fragmentation.
            /// </summary>
            public NetworkPipeline unreliableFragmentedPipeline;
            /// <summary>
            /// Flag set when the driver pipelines uses the <see cref="SimulatorPipelineStage"/>.
            /// </summary>
            public bool simulatorEnabled
            {
                get { return m_simulatorEnabled == 1; }
                set { m_simulatorEnabled = value ? (byte)1 : (byte)0; }
            }
            private byte m_simulatorEnabled;

            internal void StopListening()
            {
                #pragma warning disable 0618
                driver.StopListening();
                #pragma warning restore 0618
            }
        }

        /// <summary>
        /// Struct that contains a the <see cref="NetworkDriver.Concurrent"/> version of the <see cref="NetworkDriver"/>
        /// and relative pipelines.
        /// </summary>
        internal struct Concurrent
        {
            /// <summary>
            /// The <see cref="NetworkDriver.Concurrent"/> version of the network driver.
            /// </summary>
            public NetworkDriver.Concurrent driver;
            /// <summary>
            /// The pipeline used for sending reliable messages
            /// </summary>
            public NetworkPipeline reliablePipeline;
            /// <summary>
            /// The pipeline used for sending unreliable messages and snapshots
            /// </summary>
            public NetworkPipeline unreliablePipeline;
            /// <summary>
            /// The pipeline used for sending big unreliable messages that requires fragmentation.
            /// </summary>
            public NetworkPipeline unreliableFragmentedPipeline;
        }

        internal struct NetworkDriverData
        {
            public NetworkDriverInstance instance;
            public TransportType transportType;

            public void Dispose()
            {
                if (instance.driver.IsCreated)
                    instance.driver.Dispose();
            }

            public bool IsCreated => instance.driver.IsCreated;
        }

        private NetworkDriverData m_Driver0;
        private NetworkDriverData m_Driver1;
        private NetworkDriverData m_Driver2;
        private int m_numDrivers;

        /// <summary>
        /// The fixed capacity of the driver container.
        /// </summary>
        public const int Capacity = 3;
        /// <summary>
        /// The first assigned uniqued identifier to each driver.
        /// </summary>
        public const int FirstDriverId = 1;
        /// <summary>
        /// The number of registed drivers. Must be always less then the total driver <see cref="Capacity"/>.
        /// </summary>
        public int DriversCount => m_numDrivers;
        /// <summary>
        /// The first driver id present in the store.
        /// Can be used to iterate over all registered drivers in a for loop.
        /// </summary>
        /// <example><code>
        /// for(int i= driverStore.FirstDriver; i &lt; driverStore.LastDriver; ++i)
        /// {
        ///      ref var instance = ref driverStore.GetDriverInstance(i);
        ///      ....
        /// }
        /// </code></example>
        public int FirstDriver => FirstDriverId;
        /// <summary>
        /// The last driver id present in the store.
        /// Can be used to iterate over all registered drivers in a for loop.
        /// </summary>
        /// <example><code>
        /// for(int i= driverStore.FirstDriver; i &lt; driverStore.LastDriver; ++i)
        /// {
        ///      ref var instance = ref driverStore.GetDriverInstance(i).
        ///      ....
        /// }
        /// </code></example>
        public int LastDriver => FirstDriverId + m_numDrivers;
        /// <summary>
        /// Return true if the driver store contains a driver that has a simulator pipeline.
        /// </summary>
        public bool IsAnyUsingSimulator
        {
            get
            {
                for (var i = FirstDriver; i <= LastDriver; ++i)
                {
                    var driverInstance = GetDriverInstance(i);
                    if (driverInstance.simulatorEnabled) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Add a new driver to the store. Throw exception if all drivers slot are already occupied or the driver is not created/valid
        /// </summary>
        /// <returns>The assigned driver id </returns>
        /// <param name="driverType"></param>
        /// <param name="driverInstance"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public int RegisterDriver(TransportType driverType, in NetworkDriverInstance driverInstance)
        {
            if (driverInstance.driver.IsCreated == false)
                throw new InvalidOperationException("Cannot register non valid driver (IsCreated == false)");
            if (m_numDrivers == Capacity)
                throw new InvalidOperationException("Cannot register more driver. All slot are already used");

            int nextDriverId = FirstDriverId + m_numDrivers;
            ++m_numDrivers;
            ref var driverRef = ref m_Driver0;
            switch (nextDriverId)
            {
                case 1:
                    driverRef = ref m_Driver0;
                    break;
                case 2:
                    driverRef = ref m_Driver1;
                    break;
                case 3:
                    driverRef = ref m_Driver2;
                    break;
            }
            if (driverRef.IsCreated)
                driverRef.Dispose();
            driverRef.transportType = driverType;
            driverRef.instance = driverInstance;
            return nextDriverId;
        }

        /// <summary>
        /// Reset the current state of the store and must be called before registering the drivers.
        /// </summary>
        internal void BeginDriverRegistration()
        {
            m_numDrivers = 0;
            m_Driver0.Dispose();
            m_Driver1.Dispose();
            m_Driver2.Dispose();
        }

        /// <summary>
        /// Finalize the registration phase by initializing all missing driver instances with a NullNetworkInterface.
        /// This final step is necessary to make the job safety system able to track all the safety handles.
        /// </summary>
        internal void EndDriverRegistration()
        {
            //The ifdef is to prevent allocating driver internal data when not necessary.
            //Allocating all drivers is necessary only in case safety handles are enabled.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!m_Driver0.IsCreated)
                m_Driver0.instance.driver = NetworkDriver.Create(new NullNetworkInterface());
            if (!m_Driver1.IsCreated)
                m_Driver1.instance.driver = NetworkDriver.Create(new NullNetworkInterface());
            if (!m_Driver2.IsCreated)
                m_Driver2.instance.driver = NetworkDriver.Create(new NullNetworkInterface());
#endif
        }

        /// <summary>
        /// Return a concurrent version of the store that can be used in parallel jobs.
        /// </summary>
        internal ConcurrentDriverStore ToConcurrent()
        {
            var store = new ConcurrentDriverStore();
            //The if is necessary here because if ENABLE_UNITY_COLLECTIONS_CHECKS is not defined we don't
            //create all the drivers instances
            if (m_Driver0.IsCreated)
                store.m_Concurrent0 = new Concurrent
                {
                    driver = m_Driver0.instance.driver.ToConcurrent(),
                    reliablePipeline = m_Driver0.instance.reliablePipeline,
                    unreliablePipeline = m_Driver0.instance.unreliablePipeline,
                    unreliableFragmentedPipeline = m_Driver0.instance.unreliableFragmentedPipeline,
                };
            if (m_Driver1.IsCreated)
                store.m_Concurrent1 = new Concurrent
                {
                    driver = m_Driver1.instance.driver.ToConcurrent(),
                    reliablePipeline = m_Driver1.instance.reliablePipeline,
                    unreliablePipeline = m_Driver1.instance.unreliablePipeline,
                    unreliableFragmentedPipeline = m_Driver1.instance.unreliableFragmentedPipeline,
                };
            if (m_Driver2.IsCreated)
                store.m_Concurrent2 = new Concurrent
                {
                    driver = m_Driver2.instance.driver.ToConcurrent(),
                    reliablePipeline = m_Driver2.instance.reliablePipeline,
                    unreliablePipeline = m_Driver2.instance.unreliablePipeline,
                    unreliableFragmentedPipeline = m_Driver2.instance.unreliableFragmentedPipeline,
                };
            return store;
        }

        /// <summary>
        /// Dispose all the registered drivers instances and their allocated resources.
        /// </summary>
        public void Dispose()
        {
            m_Driver0.Dispose();
            m_Driver1.Dispose();
            m_Driver2.Dispose();
        }

        ///<summary>
        /// Return the <see cref="NetworkDriverInstance"/> instance with the given driverId.
        /// </summary>
        /// <param name="driverId">the id of the driver. Should be always greater or equals than <see cref="FirstDriverId"/></param>
        /// <remarks>
        /// The method return a copy of the driver instance not a reference. While this is suitable for almost all the use cases,
        /// since the driver is trivially copyable, be aware that calling some of the Driver class methods, like ScheduleUpdate,
        /// that update internal driver data (that aren't suited to be copied around) may not work as expected.
        /// </remarks>
        /// <returns>The <see cref="NetworkDriverInstance"/> at for the given id.</returns>
        /// <exception cref="InvalidOperationException">Throw an exception if a driver is not found</exception>
        public readonly NetworkDriverInstance GetDriverInstance(int driverId)
        {
            switch (driverId)
            {
                case 1:
                    return m_Driver0.instance;
                case 2:
                    return m_Driver1.instance;
                case 3:
                    return m_Driver2.instance;
                default:
                    throw new InvalidOperationException($"Cannot find NetworkDriver with id {driverId}");
            }
        }

        /// <summary>
        /// Return the <see cref="NetworkDriver"/> with the given driver id.
        /// </summary>
        /// <param name="driverId">the id of the driver. Should be always greater or equals than <see cref="FirstDriverId"/></param>
        /// <returns>The <see cref="NetworkDriverInstance"/> at for the given id.</returns>
        /// <exception cref="InvalidOperationException">Throw an exception if a driver is not found</exception>
        public readonly NetworkDriver GetNetworkDriver(int driverId)
        {
            switch (driverId)
            {
                case 1:
                    return m_Driver0.instance.driver;
                case 2:
                    return m_Driver1.instance.driver;
                case 3:
                    return m_Driver2.instance.driver;
                default:
                    throw new InvalidOperationException($"Cannot find NetworkDriver with id {driverId}");
            }
        }

        /// <summary>
        /// Return the transport type used by the registered driver.
        /// </summary>
        /// <param name="driverId">the id of the driver. Should be always greater or equals than <see cref="FirstDriverId"/></param>
        /// <returns>The <see cref="TransportType"/> of driver</returns>
        /// <exception cref="InvalidOperationException">Throw an exception if a driver is not found</exception>
        public TransportType GetDriverType(int driverId)
        {
            switch (driverId)
            {
                case 1:
                    return m_Driver0.transportType;
                case 2:
                    return m_Driver1.transportType;
                case 3:
                    return m_Driver2.transportType;
                default:
                    throw new InvalidOperationException($"Cannot find NetworkDriver with id {driverId}");
            }
        }

        /// <summary>
        /// Return the state of the <see cref="NetworkStreamConnection"/> connection.
        /// </summary>
        /// <param name="connection">A client or server connection</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Throw an exception if the driver associated to the connection is not found</exception>
        public NetworkConnection.State GetConnectionState(NetworkStreamConnection connection)
        {
            ref var driverData = ref m_Driver0;
            switch (connection.DriverId)
            {
                case 1:
                    driverData = ref m_Driver0; break;
                case 2:
                    driverData = ref m_Driver1; break;
                case 3:
                    driverData = ref m_Driver2; break;
                default:
                    throw new InvalidOperationException($"Cannot find NetworkDriver with id {connection.DriverId}");
            }
            return connection.Value.GetState(driverData.instance.driver);
        }

        /// <summary>
        /// Signature for all functions that can be used to visit the registered drivers in the store using the <see cref="ForEachDriver"/> method.
        /// <param name="driver">a reference to a <see cref="NetworkDriverInstance"/></param>
        /// <param name="driverId">the id of the driver</param>
        /// </summary>
        public delegate void DriverVisitor(ref NetworkDriverInstance driver, int driverId);

        /// <summary>
        /// Invoke the delegate on all registered drivers.
        /// </summary>
        /// <param name="visitor"></param>
        public void ForEachDriver(DriverVisitor visitor)
        {
            if (m_numDrivers == 0)
                return;
            visitor(ref m_Driver0.instance, FirstDriverId);
            if (m_numDrivers > 1)
                visitor(ref m_Driver1.instance, FirstDriverId+1);
            if (m_numDrivers > 2)
                visitor(ref m_Driver2.instance, FirstDriverId+2);
        }

        /// <summary>
        /// Utility method to disconnect the <see cref="NetworkStreamConnection" /> connection.
        /// </summary>
        /// <param name="connection"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void Disconnect(NetworkStreamConnection connection)
        {
            ref var driverData = ref m_Driver0;
            switch (connection.DriverId)
            {
                case 1:
                    driverData = m_Driver0; break;
                case 2:
                    driverData = m_Driver1; break;
                case 3:
                    driverData = m_Driver2; break;
                default:
                    throw new InvalidOperationException($"Cannot find NetworkDriver with id {connection.DriverId}");
            }
            driverData.instance.driver.Disconnect(connection.Value);
        }

        internal JobHandle ScheduleUpdateAllDrivers(JobHandle dependency)
        {
            if (m_numDrivers == 0)
                return dependency;
            JobHandle driver0 = m_Driver0.instance.driver.ScheduleUpdate(dependency);
            JobHandle driver1 = default, driver2 = default;
            if (m_numDrivers > 1)
                driver1 = m_Driver1.instance.driver.ScheduleUpdate(dependency);
            if (m_numDrivers > 2)
                driver2 = m_Driver2.instance.driver.ScheduleUpdate(dependency);
            return JobHandle.CombineDependencies(driver0, driver1, driver2);
        }

        /// <summary>
        /// Invoke <see cref="NetworkDriver.ScheduleFlushSend"/> on all registered drivers in the store
        /// </summary>
        /// <param name="dependency">A job handle whom all flush jobs depend upon</param>
        /// <returns>The combined handle of all the scheduled jobs.</returns>
        public JobHandle ScheduleFlushSendAllDrivers(JobHandle dependency)
        {
            if (m_numDrivers == 0)
                return dependency;
            JobHandle driver0 = m_Driver0.instance.driver.ScheduleFlushSend(dependency);
            JobHandle driver1 = default, driver2 = default;
            if (m_numDrivers > 1)
                driver1 = m_Driver1.instance.driver.ScheduleFlushSend(dependency);
            if (m_numDrivers > 2)
                driver2 = m_Driver2.instance.driver.ScheduleFlushSend(dependency);
            return JobHandle.CombineDependencies(driver0, driver1, driver2);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        /// <summary>
        /// A do-nothing network interface for internal use. All the NetworkDriver slots in the <see cref="NetworkDriverStore"/>
        /// that are not registered are initialized with this interface.
        /// </summary>
        internal struct NullNetworkInterface : INetworkInterface
        {
            public NetworkEndpoint LocalEndpoint => throw new NotImplementedException();

            public int Bind(NetworkEndpoint endpoint) => throw new NotImplementedException();

            public void Dispose() { }

            public int Initialize(ref NetworkSettings settings, ref int packetPadding) => 0;

            public int Listen() => throw new NotImplementedException();

            public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dep) => throw new NotImplementedException();

            public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dep) => throw new NotImplementedException();
        }
#endif
    }

    /// <summary>
    /// The concurrent version of the DriverStore. Contains the concurrent copy of the drivers and relative pipelines.
    /// </summary>
    internal struct ConcurrentDriverStore
    {
        internal NetworkDriverStore.Concurrent m_Concurrent0;
        internal NetworkDriverStore.Concurrent m_Concurrent1;
        internal NetworkDriverStore.Concurrent m_Concurrent2;

        /// <summary>
        /// Get the concurrent driver with the given driver id
        /// </summary>
        /// <param name="driverId">the id of the driver. Must always greater or equals <see cref="NetworkDriverStore.FirstDriverId"/></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public NetworkDriverStore.Concurrent GetConcurrentDriver(int driverId)
        {
            switch (driverId)
            {
                case 1:
                    return m_Concurrent0;
                case 2:
                    return m_Concurrent1;
                case 3:
                    return m_Concurrent2;
                default:
                    throw new InvalidOperationException($"Cannot find concurrent driver with id {driverId}");
            }
        }
    }
}
