using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Debug = UnityEngine.Debug;

namespace Unity.NetCode
{
    /// <summary>
    /// Am singleton entity returned by the <see cref="DriverMigrationSystem.StoreWorld"/>
    /// that can be used to load a previously stored driver state into another world.
    /// </summary>
    public struct MigrationTicket : IComponentData
    {
        /// <summary>
        /// A unique value for the ticket.
        /// </summary>
        public int Value;
    }

    /// <summary>
    /// A system that should be used to temporarly keep the internal transport connections alive while transferring then
    /// to another world.
    /// For example, you can rely on the DriverMigrationSystem to re-use the same connections in between a lobby world and the game world.
    /// </summary>
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class DriverMigrationSystem : SystemBase
    {
        /// <summary>
        /// The minimal  internal state necessary to restore all the <see cref="NetworkStreamConnection"/> when the
        /// drivers are migrated to the new world.
        /// </summary>
        internal struct DriverStoreState
        {
            /// <summary>
            /// A copy of the <see cref="NetworkDriverStore"/>
            /// </summary>
            public NetworkDriverStore DriverStore;
            /// <summary>
            /// The next network id that should be assigned to a new incoming connection when there are no free
            /// network id that be reuse.
            /// </summary>
            public int NextId;
            /// <summary>
            /// A list of reusable network id for the incoming connections.
            /// </summary>
            public NativeArray<int> FreeList;
            /// <summary>
            /// The last <see cref="NetworkEndpoint"/> used to either connect to the server or to listen for incoming connections.
            /// </summary>
            public NetworkEndpoint LastEp;
            /// <summary>
            /// Destroy all the allocated resources.
            /// </summary>
            /// <returns></returns>
            public void Dispose()
            {
                DriverStore.Dispose();
                if (FreeList.IsCreated)
                    FreeList.Dispose();
            }
        }

        /// <summary>
        /// Contains the state of drivers and the backup world in which they have been temporary transferred.
        /// </summary>
        internal struct WorldState
        {
            /// <summary>
            /// The internal state of the drivers.
            /// </summary>
            public DriverStoreState DriverStoreState;
            /// <summary>
            /// A temporary backup world, constructed when the driver state is saved. See <see cref="DriverMigrationSystem.StoreWorld"/>.
            /// </summary>
            public World BackupWorld;
        }

        private Dictionary<int, WorldState> driverMap;
        private int m_TicketCounter;

        protected override void OnCreate()
        {
            driverMap = new Dictionary<int, WorldState>();
            m_TicketCounter = 0;
        }

        /// <summary>
        /// Stores NetworkDriver and Connection data for migration of a specific world.
        /// </summary>
        /// <param name="sourceWorld">The world we want to store.</param>
        /// <remarks>Only entities with the type `NetworkStreamConnection` are migrated over to the new World.</remarks>
        /// <returns>A ticket that can be used to retrieve the stored NetworkDriver data.</returns>
        public int StoreWorld(World sourceWorld)
        {
            var ticket = ++m_TicketCounter;

            if (driverMap.ContainsKey(ticket))
                throw new ApplicationException("Unhandled error state, the ticket already exists in driver map.");

            driverMap.Add(ticket, default);

            using var driverSingletonQuery = sourceWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            ref var driverSingleton = ref driverSingletonQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW;
            driverSingletonQuery.CompleteDependency();
            Store(driverSingleton.StoreMigrationState(), ticket);

            using var filter = sourceWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamConnection));
            var backupWorld = new World(sourceWorld.Name, sourceWorld.Flags);

            backupWorld.EntityManager.MoveEntitiesFrom(sourceWorld.EntityManager, filter);

            var worldState = driverMap[ticket];
            worldState.BackupWorld = backupWorld;

            driverMap[ticket] = worldState;
            return ticket;
        }

        /// <summary>
        /// Loads a stored NetworkDriver and Connection data into a new or existing World.
        /// </summary>
        /// <param name="ticket">A ticket to a stored World</param>
        /// <param name="newWorld">An optional world we would want to Load into.</param>
        /// <returns>A prepared world that is ready to have its systems added.</returns>
        /// <remarks>This function needs to be called before any systems are initialized on the world we want to migrate to.</remarks>
        /// <exception cref="ArgumentException">Is thrown incase a invalid world is supplied. Only Netcode worlds work.</exception>
        public World LoadWorld(int ticket, World newWorld = null)
        {
            if (driverMap.TryGetValue(ticket, out var driver))
            {
                if (!driver.BackupWorld.IsCreated)
                    throw new ApplicationException("The driver contains no valid BackupWorld to migrate from.");

                if (newWorld == null)
                    newWorld = driver.BackupWorld;
                else
                {
                    //Debug.Assert(null == newWorld.GetExistingSystem<NetworkStreamReceiveSystem>());

                    var filter = driver.BackupWorld.EntityManager.CreateEntityQuery(typeof(NetworkStreamConnection));
                    newWorld.EntityManager.MoveEntitiesFrom(driver.BackupWorld.EntityManager, filter);
                    driver.BackupWorld.Dispose();
                }

                var e = newWorld.EntityManager.CreateEntity();
                newWorld.EntityManager.AddComponentData(e, new MigrationTicket {Value = ticket});

                return newWorld;
            }
            throw new ArgumentException("You can only migrate a world created by netcode. Make sure you are creating your worlds correctly.");
        }

        internal DriverStoreState Load(int ticket)
        {
            if (driverMap.TryGetValue(ticket, out var driver))
            {
                driverMap.Remove(ticket);
                return driver.DriverStoreState;
            }
            throw new ArgumentException("You can only migrate a world created by netcode. Make sure you are creating your worlds correctly.");
        }

        internal void Store(DriverStoreState state, int ticket)
        {
            Debug.Assert(driverMap.ContainsKey(ticket));
            var worldState = driverMap[ticket];

            worldState.DriverStoreState = state;

            driverMap[ticket] = worldState;
        }


        protected override void OnDestroy()
        {
            foreach (var keyValue in driverMap)
            {
                var state = keyValue.Value;
                state.DriverStoreState.Dispose();
                if (state.BackupWorld.IsCreated)
                    state.BackupWorld.Dispose();
            }
        }

        protected override void OnUpdate()
        {
        }
    }
}
