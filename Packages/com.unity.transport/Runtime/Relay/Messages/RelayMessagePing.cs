using System.Runtime.InteropServices;

namespace Unity.Networking.Transport.Relay
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RelayMessagePing
    {
        public const int k_Length = RelayMessageHeader.k_Length + RelayAllocationId.k_Length + 2; // Header + FromAllocationId + SequenceNumber

        public RelayMessageHeader Header;
        public RelayAllocationId FromAllocationId;
        public ushort SequenceNumber;

        public static RelayMessagePing Create(RelayAllocationId fromAllocationId)
        {
            return new RelayMessagePing
            {
                Header = RelayMessageHeader.Create(RelayMessageType.Ping),
                FromAllocationId = fromAllocationId,
                SequenceNumber = 1
            };
        }

        public static void Write(ref PacketProcessor packetProcessor, ref RelayAllocationId fromAllocationId)
        {
            RelayMessageHeader.Write(ref packetProcessor, RelayMessageType.Ping);
            packetProcessor.AppendToPayload<RelayAllocationId>(fromAllocationId);
            packetProcessor.AppendToPayload<ushort>(1);
        }
    }
}
