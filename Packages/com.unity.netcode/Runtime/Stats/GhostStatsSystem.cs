#if UNITY_EDITOR || NETCODE_DEBUG
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace Unity.NetCode
{
    /// <summary>
    /// Commit to the Network Debugger tools all the stats collected by the server and clients worlds
    /// during the last frame. It is also responsible to handle the connection to the debugging tool.
    /// The system explicity run in the DefaultWorld (since it is responsible to persist and mains the connection to the
    /// debugging systems, regardless of the existance of the client/server worlds).
    /// </summary>
    class GhostStatsConnection : IDisposable
    {
        #if UNITY_EDITOR
        [MenuItem("Multiplayer/Window: NetDbg (Browser)", priority = 75)]
        public static void OpenDebugger()
        {
            System.Diagnostics.Process.Start(Path.GetFullPath("Packages/com.unity.netcode/Runtime/Stats/netdbg.html"));
        }
        #endif

        struct GhostStatsToSend
        {
            readonly public World managedWorld;
            public EntityQuery singletonQuery;

            public GhostStatsToSend(World world, EntityQuery query)
            {
                managedWorld = world;
                singletonQuery = query;
            }
        }
        private DebugWebSocket m_Socket;
        private List<GhostStatsToSend> m_StatsCollections;
        internal static ushort Port = 8787;

        public uint UpdateId;
        public int RefCount;

        public GhostStatsConnection()
        {
            if (Port != 0)
                m_Socket = new DebugWebSocket(Port);
        }

        public void Dispose()
        {
            if (m_Socket != null)
                m_Socket.Dispose();
        }

        public void Update()
        {
            if (m_Socket == null)
                return;
            if (m_Socket.AcceptNewConnection())
            {
                m_StatsCollections = new List<GhostStatsToSend>();
                foreach (var world in World.All)
                {
                    if(world.IsThinClient())
                        continue;

                    var collectionDataQry = world.EntityManager.CreateEntityQuery(
                        ComponentType.ReadWrite<GhostStatsCollectionData>(),
                        ComponentType.ReadWrite<GhostStatsCollectionCommand>(),
                        ComponentType.ReadWrite<GhostStatsCollectionSnapshot>(),
                        ComponentType.ReadWrite<GhostStatsCollectionPredictionError>(),
                        ComponentType.ReadWrite<GhostStatsCollectionMinMaxTick>());

                    if (!collectionDataQry.HasSingleton<GhostStatsCollectionData>())
                    {
                        continue;
                    }
                    SetStatIndex(collectionDataQry, m_StatsCollections.Count);
                    m_StatsCollections.Add(new GhostStatsToSend(world, collectionDataQry));
                }
            }

            //Remove stats if the world has been disposed
            if (m_StatsCollections != null)
            {
                for (var con = m_StatsCollections.Count - 1; con >= 0; --con)
                {
                    if ((!m_StatsCollections[con].managedWorld.IsCreated))
                        m_StatsCollections.RemoveAt(con);
                }
            }

            if (!m_Socket.HasConnection)
            {
                if (m_StatsCollections != null)
                {
                    for (var con = 0; con < m_StatsCollections.Count; ++con)
                        SetStatIndex(m_StatsCollections[con].singletonQuery, -1);
                    m_StatsCollections = null;
                }
                return;
            }

            if (m_StatsCollections == null || m_StatsCollections.Count == 0)
                return;

            for (var con = 0; con < m_StatsCollections.Count; ++con)
            {
                SendPackets(ref m_StatsCollections[con].singletonQuery.GetSingletonRW<GhostStatsCollectionData>().ValueRW);
            }
        }

        private void SendPackets(ref GhostStatsCollectionData data)
        {
            foreach (var packet in data.m_PacketQueue)
            {
                if (packet.isString)
                    m_Socket.SendText(data.m_PacketPool.AsArray().GetSubArray(packet.dataOffset, packet.dataSize));
                else
                    m_Socket.SendBinary(data.m_PacketPool.AsArray().GetSubArray(packet.dataOffset, packet.dataSize));
            }
            data.m_PacketQueue.Clear();
            data.m_UsedPacketPoolSize = 0;
        }

        private unsafe void SetStatIndex(EntityQuery query, int index)
        {
            //Reset the collected stats when we invalidate the index
            ref var statsData = ref query.GetSingletonRW<GhostStatsCollectionData>().ValueRW;
            statsData.m_StatIndex = index;
            statsData.m_CollectionTick = NetworkTick.Invalid;
            statsData.m_PacketQueue.Clear();
            statsData.m_UsedPacketPoolSize = 0;
            if (statsData.m_LastNameAndErrorArray.Length > 0)
                statsData.AppendNamePacket();

            //Complete any pending jobs before resetting singleton data
            query.CompleteDependency();
            ref var commandStatsData = ref query.GetSingletonRW<GhostStatsCollectionCommand>().ValueRW;
            ref var snapshotCollectionData = ref query.GetSingletonRW<GhostStatsCollectionSnapshot>().ValueRW;
            ref var predictionErrorData = ref query.GetSingletonRW<GhostStatsCollectionPredictionError>().ValueRW;
            ref readonly var minMaxTickData = ref query.GetSingletonRW<GhostStatsCollectionMinMaxTick>().ValueRO;
            commandStatsData.Value[0] = 0;
            commandStatsData.Value[1] = 0;
            commandStatsData.Value[2] = 0;
            snapshotCollectionData.Data.Clear();
            predictionErrorData.Data.Clear();
            UnsafeUtility.MemClear(minMaxTickData.Value.GetUnsafePtr(), UnsafeUtility.SizeOf<NetworkTick>()*minMaxTickData.Value.Length);
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    internal partial class GhostStatsFlushSystem : SystemBase
    {
        private static GhostStatsConnection s_GhostStatsConnection;
        private uint m_UpdateId;
        protected override void OnCreate()
        {
            if (s_GhostStatsConnection == null)
                s_GhostStatsConnection = new GhostStatsConnection();
            ++s_GhostStatsConnection.RefCount;
        }

        protected override void OnDestroy()
        {
            if (--s_GhostStatsConnection.RefCount <= 0)
            {
                s_GhostStatsConnection.Dispose();
                s_GhostStatsConnection = null;
            }
        }

        protected override void OnUpdate()
        {
            if (s_GhostStatsConnection == null)
                return;

            if (m_UpdateId == s_GhostStatsConnection.UpdateId)
            {
                ++s_GhostStatsConnection.UpdateId;
                s_GhostStatsConnection.Update();
            }
            m_UpdateId = s_GhostStatsConnection.UpdateId;
        }
    }
}
#endif

