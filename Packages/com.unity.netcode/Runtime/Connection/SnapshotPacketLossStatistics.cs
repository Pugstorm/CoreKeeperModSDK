using Unity.Collections;
using Unity.Mathematics;

namespace Unity.NetCode
{
    /// <summary>
    /// Added to <see cref="NetworkMetrics"/>, stores snapshot related loss calculated via <see cref="GhostReceiveSystem"/>.
    /// </summary>
    /// <remarks>Very similar approach to <see cref="Unity.Networking.Transport.UnreliableSequencedPipelineStage.Statistics"/>.</remarks>
    public struct SnapshotPacketLossStatistics
    {
        /// <summary>Count of snapshot packets received - on the client - from the server.</summary>
        public ulong NumPacketsReceived;
        /// <summary>Counts the number of snapshot packets dropped (i.e. "culled") due to invalid SequenceId. I.e. Implies the packet arrived, but out of order.</summary>
        public ulong NumPacketsCulledOutOfOrder;
        /// <summary>The Netcode package can only process one snapshot per render frame. If 2+ arrive on the same frame, we'll clobber one of them without processing it.</summary>
        /// <remarks>This is also called a "Packet Burst".</remarks>
        public ulong NumPacketsCulledAsArrivedOnSameFrame;
        /// <summary>Detects gaps in <see cref="NetworkSnapshotAck.CurrentSnapshotSequenceId"/> to determine real packet loss.</summary>
        public ulong NumPacketsDroppedNeverArrived;

        /// <summary>Percentage of all snapshot packets - that we assume must have been sent to us (based on SequenceId) - which are lost due to network-caused packet loss.</summary>
        public double NetworkPacketLossPercent => NumPacketsReceived != 0 ? NumPacketsDroppedNeverArrived / (double) (NumPacketsReceived + NumPacketsDroppedNeverArrived) : 0;
        /// <summary>Percentage of all snapshot packets - that we assume must have been sent to us (based on SequenceId) - which are lost due to arriving out of order (and thus being culled).</summary>
        public double OutOfOrderPacketLossPercent => NumPacketsReceived != 0 ? NumPacketsCulledOutOfOrder / (double) (NumPacketsReceived + NumPacketsDroppedNeverArrived) : 0;
        /// <summary>Percentage of all snapshot packets - that we assume must have been sent to us (based on SequenceId) - which are culled due to arriving on the same frame as another snapshot.</summary>
        public double ArrivedOnTheSameFrameClobberedPacketLossPercent => NumPacketsReceived != 0 ? NumPacketsCulledAsArrivedOnSameFrame / (double) (NumPacketsReceived + NumPacketsDroppedNeverArrived) : 0;
        /// <summary>Percentage of all snapshot packets - that we assume must have been sent to us (based on SequenceId) - which are dropped (for any reason).</summary>
        public double CombinedPacketLossPercent => NumPacketsReceived != 0 ? (CombinedPacketLossCount) / (double) (NumPacketsReceived + NumPacketsDroppedNeverArrived) : 0;
        /// <summary>Count of packets lost in some form.</summary>
        public ulong CombinedPacketLossCount => NumPacketsDroppedNeverArrived + NumPacketsCulledOutOfOrder + NumPacketsCulledAsArrivedOnSameFrame;

        public static SnapshotPacketLossStatistics operator +(SnapshotPacketLossStatistics a, SnapshotPacketLossStatistics b)
        {
            a.NumPacketsReceived += b.NumPacketsReceived;
            a.NumPacketsCulledOutOfOrder += b.NumPacketsCulledOutOfOrder;
            a.NumPacketsCulledAsArrivedOnSameFrame += b.NumPacketsCulledAsArrivedOnSameFrame;
            a.NumPacketsDroppedNeverArrived += b.NumPacketsDroppedNeverArrived;
            return a;
        }

        public static SnapshotPacketLossStatistics operator -(SnapshotPacketLossStatistics a, SnapshotPacketLossStatistics b)
        {
            // Guard subtraction as it can get negative when we're polling 3s intervals.
            a.NumPacketsReceived -= math.min(a.NumPacketsReceived, b.NumPacketsReceived);
            a.NumPacketsCulledOutOfOrder -= math.min(a.NumPacketsCulledOutOfOrder, b.NumPacketsCulledOutOfOrder);
            a.NumPacketsCulledAsArrivedOnSameFrame -= math.min(a.NumPacketsCulledAsArrivedOnSameFrame, b.NumPacketsCulledAsArrivedOnSameFrame);
            a.NumPacketsDroppedNeverArrived -= math.min(a.NumPacketsDroppedNeverArrived, b.NumPacketsDroppedNeverArrived);
            return a;
        }

        /// <summary>
        /// Dumps all the statistic info.
        /// </summary>
        /// <returns>Dumps all the statistic info.</returns>
        public FixedString512Bytes ToFixedString() => $"SPLS[received:{NumPacketsReceived}, combinedPL:{CombinedPacketLossCount} {(int) (CombinedPacketLossPercent*100)}%, networkPL:{NumPacketsDroppedNeverArrived} {(int) (NetworkPacketLossPercent*100)}%, outOfOrderPL:{NumPacketsCulledOutOfOrder} {(int) (OutOfOrderPacketLossPercent*100)}%, clobberedPL:{NumPacketsCulledAsArrivedOnSameFrame} {(int) (ArrivedOnTheSameFrameClobberedPacketLossPercent*100)}%]";

        /// <inheritdoc cref="ToFixedString"/>
        public override string ToString() => ToFixedString().ToString();
    }
}
