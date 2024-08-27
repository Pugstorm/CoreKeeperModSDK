using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Networking.Transport.Utilities;

namespace Unity.NetCode
{
    /// <summary>
    /// Temporary type, used to upgrade to new component type, to be removed before final 1.0
    /// </summary>
    [Obsolete("NetworkSnapshotAckComponent has been deprecated. Use GhostInstance instead (UnityUpgradable) -> NetworkSnapshotAck", true)]
    public struct NetworkSnapshotAckComponent : IComponentData
    {}

    /// <summary>Client and Server Component. One per NetworkId entity, stores SnapshotAck and Ping info for a client.</summary>
    public struct NetworkSnapshotAck : IComponentData
    {
        internal void UpdateReceivedByRemote(NetworkTick tick, uint mask)
        {
            if (!tick.IsValid)
            {
                ReceivedSnapshotByRemoteMask3 = 0;
                ReceivedSnapshotByRemoteMask2 = 0;
                ReceivedSnapshotByRemoteMask1 = 0;
                ReceivedSnapshotByRemoteMask0 = 0;
                LastReceivedSnapshotByRemote = NetworkTick.Invalid;
            }
            else if (!LastReceivedSnapshotByRemote.IsValid)
            {
                ReceivedSnapshotByRemoteMask3 = 0;
                ReceivedSnapshotByRemoteMask2 = 0;
                ReceivedSnapshotByRemoteMask1 = 0;
                ReceivedSnapshotByRemoteMask0 = mask;
                LastReceivedSnapshotByRemote = tick;
            }
            else if (tick.IsNewerThan(LastReceivedSnapshotByRemote))
            {
                int shamt = tick.TicksSince(LastReceivedSnapshotByRemote);
                if (shamt >= 256)
                {
                    ReceivedSnapshotByRemoteMask3 = 0;
                    ReceivedSnapshotByRemoteMask2 = 0;
                    ReceivedSnapshotByRemoteMask1 = 0;
                    ReceivedSnapshotByRemoteMask0 = mask;
                }
                else
                {
                    while (shamt >= 64)
                    {
                        ReceivedSnapshotByRemoteMask3 = ReceivedSnapshotByRemoteMask2;
                        ReceivedSnapshotByRemoteMask2 = ReceivedSnapshotByRemoteMask1;
                        ReceivedSnapshotByRemoteMask1 = ReceivedSnapshotByRemoteMask0;
                        ReceivedSnapshotByRemoteMask0 = 0;
                        shamt -= 64;
                    }

                    if (shamt == 0)
                        ReceivedSnapshotByRemoteMask0 |= mask;
                    else
                    {
                        ReceivedSnapshotByRemoteMask3 = (ReceivedSnapshotByRemoteMask3 << shamt) |
                                                        (ReceivedSnapshotByRemoteMask2 >> (64 - shamt));
                        ReceivedSnapshotByRemoteMask2 = (ReceivedSnapshotByRemoteMask2 << shamt) |
                                                        (ReceivedSnapshotByRemoteMask1 >> (64 - shamt));
                        ReceivedSnapshotByRemoteMask1 = (ReceivedSnapshotByRemoteMask1 << shamt) |
                                                        (ReceivedSnapshotByRemoteMask0 >> (64 - shamt));
                        ReceivedSnapshotByRemoteMask0 = (ReceivedSnapshotByRemoteMask0 << shamt) |
                                                        mask;
                    }
                }

                LastReceivedSnapshotByRemote = tick;
            }
        }

        /// <summary>
        /// Return true if the snapshot for tick <paramref name="tick"/> has been received (from a client perspective)
        /// or acknowledged (from the servers POV)
        /// </summary>
        /// <param name="tick"></param>
        /// <returns></returns>
        public bool IsReceivedByRemote(NetworkTick tick)
        {
            if (!tick.IsValid || !LastReceivedSnapshotByRemote.IsValid)
                return false;
            if (tick.IsNewerThan(LastReceivedSnapshotByRemote))
                return false;
            int bit = LastReceivedSnapshotByRemote.TicksSince(tick);
            if (bit >= 256)
                return false;
            if (bit >= 192)
            {
                bit -= 192;
                return (ReceivedSnapshotByRemoteMask3 & (1ul << bit)) != 0;
            }

            if (bit >= 128)
            {
                bit -= 128;
                return (ReceivedSnapshotByRemoteMask2 & (1ul << bit)) != 0;
            }

            if (bit >= 64)
            {
                bit -= 64;
                return (ReceivedSnapshotByRemoteMask1 & (1ul << bit)) != 0;
            }

            return (ReceivedSnapshotByRemoteMask0 & (1ul << bit)) != 0;
        }

        /// <summary>
        /// The last snapshot (tick) received from the remote peer.
        /// <para>For the client, it represents the last received snapshot received from the server.</para>
        /// <para>For the server, it is the last acknowledge packet that has been received by client.</para>
        /// </summary>
        public NetworkTick LastReceivedSnapshotByRemote;
        private ulong ReceivedSnapshotByRemoteMask0;
        private ulong ReceivedSnapshotByRemoteMask1;
        private ulong ReceivedSnapshotByRemoteMask2;
        private ulong ReceivedSnapshotByRemoteMask3;
        /// <summary>
        /// The field has a different meaning on the client vs on the server:
        /// <para>Client: it is the last received ghost snapshot from the server.</para>
        /// <para>Server: record the last command tick that has been received. Used to discard either out of order or late commands.</para>
        /// </summary>
        public NetworkTick LastReceivedSnapshotByLocal;
        /// <summary>
        /// <para>Client: Records the last Snapshot Sequence Id received by this client.</para>
        /// <para>Server: Increments every time a Snapshot is successfully dispatched (thus, assumed sent).</para>
        /// <para><see cref="SnapshotPacketLoss"/></para>
        /// </summary>
        public byte CurrentSnapshotSequenceId;
        /// <summary>
        /// Client-only, a bitmask that indicates which of the last 32 snapshots has been received
        /// from the server.
        /// On the server it is always 0.
        /// </summary>
        public uint ReceivedSnapshotByLocalMask;
        /// <summary>
        /// Server-only, the number of ghost prefabs loaded by remote client. On the client is not used and it is always 0.
        /// </summary>
        public uint NumLoadedPrefabs;

        /// <summary><inheritdoc cref="SnapshotPacketLossStatistics"/></summary>
        /// <remarks>Client-only.</remarks>
        public SnapshotPacketLossStatistics SnapshotPacketLoss;

        /// <summary>
        /// Update the number of loaded prefabs nad sync the interpolation delay for the remote connection.
        /// </summary>
        /// <remarks>
        /// The state of the component is not changed if the <paramref name="remoteTime"/> is less than <see cref="LastReceivedRemoteTime"/>,
        /// because that will indicate a more recent message has been already processed.
        /// </remarks>
        /// <param name="remoteTime"></param>
        /// <param name="numLoadedPrefabs"></param>
        /// <param name="interpolationDelay"></param>
        internal void UpdateRemoteAckedData(uint remoteTime, uint numLoadedPrefabs, uint interpolationDelay)
        {
            //Because the remote time is updated also by RPC and there is no order guarante for witch is handled
            //first (snapshost or rpc message) it is necessary to accept update if received remoteTime
            //is also equals to the LastReceivedRemoteTime.
            if (remoteTime != 0 && (!SequenceHelpers.IsNewer(LastReceivedRemoteTime, remoteTime) || LastReceivedRemoteTime == 0))
            {
                NumLoadedPrefabs = numLoadedPrefabs;
                RemoteInterpolationDelay = interpolationDelay;
            }
        }

        /// <summary>
        /// Store the time (local) at which a message/packet has been received,
        /// as well as the latest received remote time (than will send back to the remote peer) and update the
        /// <see cref="EstimatedRTT"/> and <see cref="DeviationRTT"/> for the connection.
        /// </summary>
        /// <remarks>
        /// The state of the component is not changed if the <paramref name="remoteTime"/> is less than <see cref="LastReceivedRemoteTime"/>,
        /// because that will indicate a more recent message has been already processed.
        /// </remarks>
        /// <param name="remoteTime"></param>
        /// <param name="localTimeMinusRTT"></param>
        /// <param name="localTime"></param>
        internal void UpdateRemoteTime(uint remoteTime, uint localTimeMinusRTT, uint localTime)
        {
            //Because we sync time using both RPC and snapshot it is more correct to also accept
            //update the stats for a remotetime that is equals to the last received one.
            if (remoteTime != 0 && (!SequenceHelpers.IsNewer(LastReceivedRemoteTime, remoteTime) || LastReceivedRemoteTime == 0))
            {
                LastReceivedRemoteTime = remoteTime;
                LastReceiveTimestamp = localTime;
                if (localTimeMinusRTT == 0)
                    return;
                uint lastReceivedRTT = localTime - localTimeMinusRTT;
                // Highest bit set means we got a negative value, which can happen on low ping due to clock difference between client and server
                if ((lastReceivedRTT & (1<<31)) != 0)
                    lastReceivedRTT = 0;
                if (EstimatedRTT == 0)
                    EstimatedRTT = lastReceivedRTT;
                else
                    EstimatedRTT = EstimatedRTT * 0.875f + lastReceivedRTT * 0.125f;
                DeviationRTT = DeviationRTT * 0.75f + math.abs(lastReceivedRTT - EstimatedRTT) * 0.25f;
            }
        }

        /// <inheritdoc cref="CalculateSequenceIdDelta(byte,byte,bool)"/>
        internal readonly int CalculateSequenceIdDelta(byte current, bool isSnapshotConfirmedNewer) => CalculateSequenceIdDelta(current, CurrentSnapshotSequenceId, isSnapshotConfirmedNewer);

        /// <summary>
        /// Returns the delta (in ticks) between <see cref="current"/> and <see cref="last"/> SequenceIds, but assumes
        /// that <see cref="NetworkTime.ServerTick"/> logic (to discard old snapshots) is correct.
        /// Thus:
        /// - If the snapshot is confirmed newer, we can check a delta of '0 to byte.MaxValue'.
        /// - If the snapshot is confirmed old, we can check a delta of '0 to -byte.MaxValue'.
        /// </summary>
        internal static int CalculateSequenceIdDelta(byte current, byte last, bool isSnapshotConfirmedNewer)
        {
            if (isSnapshotConfirmedNewer)
                return (byte)(current - last);
            return -(byte)(last - current);
        }

        /// <summary>
        /// The last remote time stamp received by the connection. The remote time is sent back (via command for the client or in the snapshot for the server)
        /// and used to calculate the round trip time for the connection.
        /// </summary>
        public uint LastReceivedRemoteTime;
        /// <summary>
        /// The local time stamp at which the connection has received the last message. Used to calculate the elapsed "processing" time and reported to
        /// the remote peer to correctly update the round trip time.
        /// </summary>
        public uint LastReceiveTimestamp;
        /// <summary>
        /// The calculated exponential smoothing average connection round trip time.
        /// </summary>
        public float EstimatedRTT;
        /// <summary>
        /// The round trip time average deviation from the <see cref="EstimatedRTT"/>. It is not a real standard deviation but an approximation
        /// using a simpler exponential smoothing average.
        /// </summary>
        public float DeviationRTT;
        /// <summary>
        /// How late the commands are received by server. Is a negative fixedPoint Q24:8 number that measure how many ticks behind the server
        /// was when he received the command, and it used as feedback by the <see cref="NetworkTimeSystem"/> to synchronize the
        /// <see cref="NetworkTime.ServerTick"/> such that the client always runs ahead of the server.
        /// A positive number indicates that the client is running behind the server.
        /// A negative number indicates that the client is running ahead of the server.
        /// </summary>
        public int ServerCommandAge;
        /// <summary>
        /// The reported interpolation delay reported by the client (in number of ticks).
        /// </summary>
        public uint RemoteInterpolationDelay;
    }
}
