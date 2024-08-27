using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Networking.Transport;

namespace Unity.NetCode
{
    /// <summary>
    ///     The <see cref="EntityCommandBufferSystem" /> at the end of the <see cref="NetworkReceiveSystemGroup" /> that
    ///     is used to sync connection entity state (like creation and destruction).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(NetworkReceiveSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial class NetworkGroupCommandBufferSystem : EntityCommandBufferSystem
    {
        private EntityQuery m_ConnectionQuery;
        private EntityQuery m_IncorrectlyDisposedConnectionsQuery;

        /// <summary>
        ///     Call <see cref="SystemAPI.GetSingleton{T}" /> to get this component for this system, and then call
        ///     <see cref="CreateCommandBuffer" /> on this singleton to create an ECB to be played back by this system.
        /// </summary>
        /// <remarks>
        ///     Useful if you want to record entity commands now, but play them back at a later point in
        ///     the frame, or early in the next frame.
        /// </remarks>
        public unsafe struct Singleton : IComponentData, IECBSingleton
        {
            internal UnsafeList<EntityCommandBuffer>* pendingBuffers;
            internal AllocatorManager.AllocatorHandle allocator;

            /// <summary>
            ///     Create a command buffer for the parent system to play back.
            /// </summary>
            /// <remarks>
            ///     The command buffers created by this method are automatically added to the system's list of
            ///     pending buffers.
            /// </remarks>
            /// <param name="world">The world in which to play it back.</param>
            /// <returns>The command buffer to record to.</returns>
            public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
            {
                return EntityCommandBufferSystem.CreateCommandBuffer(ref *pendingBuffers, allocator, world);
            }

            /// <summary>
            ///     Sets the list of command buffers to play back when this system updates.
            /// </summary>
            /// <remarks>
            ///     This method is only intended for internal use, but must be in the public API due to language
            ///     restrictions. Command buffers created with <see cref="CreateCommandBuffer" /> are automatically added to
            ///     the system's list of pending buffers to play back.
            /// </remarks>
            /// <param name="buffers">
            ///     The list of buffers to play back. This list replaces any existing pending command buffers on this
            ///     system.
            /// </param>
            public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
            {
                pendingBuffers = (UnsafeList<EntityCommandBuffer>*) UnsafeUtility.AddressOf(ref buffers);
            }

            /// <summary>
            ///     Set the allocator that command buffers created with this singleton should be allocated with.
            /// </summary>
            /// <param name="allocatorIn">The allocator to use</param>
            public void SetAllocator(Allocator allocatorIn)
            {
                allocator = allocatorIn;
            }

            /// <summary>
            ///     Set the allocator that command buffers created with this singleton should be allocated with.
            /// </summary>
            /// <param name="allocatorIn">The allocator to use</param>
            public void SetAllocator(AllocatorManager.AllocatorHandle allocatorIn)
            {
                allocator = allocatorIn;
            }
        }

        /// <inheritdoc cref="EntityCommandBufferSystem.OnCreate" />
        protected override void OnCreate()
        {
            base.OnCreate();
            this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);

            m_IncorrectlyDisposedConnectionsQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>(), ComponentType.Exclude<IncomingRpcDataStreamBuffer>());
            m_ConnectionQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            PatchConnectionEvents(ref CheckedStateRef);
        }

        /// <summary>
        ///     Patch up connectionEvent ECB entities from earlier in the frame, which now exist.
        /// </summary>
        /// <param name="state"></param>
        [BurstCompile]
        private void PatchConnectionEvents(ref SystemState state)
        {
            ref var networkStreamDriver = ref SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRW;
            var netDebug = SystemAPI.GetSingleton<NetDebug>();

            NativeArray<NetworkStreamConnection> connections = default;
            NativeArray<Entity> entities = default;
            var connectionEvents = networkStreamDriver.ConnectionEventsList;
            for (var i = 0; i < connectionEvents.Length; i++)
            {
                ref var connectionEvent = ref connectionEvents.ElementAt(i);
                if (connectionEvent.ConnectionEntity.Index >= 0)
                    continue;

                if (!connections.IsCreated)
                {
                    m_ConnectionQuery.CompleteDependency();
                    connections = m_ConnectionQuery.ToComponentDataArray<NetworkStreamConnection>(Allocator.Temp);
                    entities = m_ConnectionQuery.ToEntityArray(Allocator.Temp);
                }

                if (!TrySetFromConnectionId(connections, entities, connectionEvent.ConnectionId, ref connectionEvent.ConnectionEntity))
                {
                    netDebug.LogError($"Unable to find Connection Entity after ECB Playback, for NetCodeConnectionEvent: {connectionEvent.ToFixedString()}! Forced to set to Entity.Null.");
                    connectionEvent.ConnectionEntity = Entity.Null;
                }

                static bool TrySetFromConnectionId(NativeArray<NetworkStreamConnection> ids, NativeArray<Entity> entities, NetworkConnection searchId, ref Entity toSet)
                {
                    for (int i = 0; i < ids.Length; i++)
                    {
                        if (ids[i].Value.ConnectionId == searchId.ConnectionId)
                        {
                            toSet = entities[i];
                            return true;
                        }
                    }

                    return false;
                }
            }

            // Apply these events.
            networkStreamDriver.ConnectionEventsForTick = connectionEvents.AsReadOnly();

            if(!m_IncorrectlyDisposedConnectionsQuery.IsEmpty)
            {
                var incorrectlyDisposedConnectionEntities = m_IncorrectlyDisposedConnectionsQuery.ToEntityArray(Allocator.Temp);
                var incorrectlyDisposedConnections = m_IncorrectlyDisposedConnectionsQuery.ToComponentDataArray<NetworkStreamConnection>(Allocator.Temp);
                for (int i = 0; i < incorrectlyDisposedConnections.Length; i++)
                {
                    netDebug.LogError($"The entity for {incorrectlyDisposedConnections[i].Value.ToFixedString()} ({incorrectlyDisposedConnectionEntities[i].ToFixedString()}) has been incorrectly disposed in '{state.WorldUnmanaged.Name}'! You should never dispose the connection entity yourself! Instead, call Disconnect on the driver with it. Manually disconnecting it for you now.");
                    networkStreamDriver.DriverStore.Disconnect(incorrectlyDisposedConnections[i]);
                }
                state.EntityManager.RemoveComponent<NetworkStreamConnection>(m_IncorrectlyDisposedConnectionsQuery);
                state.EntityManager.DestroyEntity(incorrectlyDisposedConnectionEntities);
            }
        }
    }
}
