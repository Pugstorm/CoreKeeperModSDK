using System.Runtime.InteropServices;

namespace Unity.Networking.Transport.Relay
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RelayMessageDisconnect
    {
        public const int k_Length = RelayMessageHeader.k_Length + RelayAllocationId.k_Length * 2; // Header + FromAllocationId + ToAllocationId

        public RelayMessageHeader Header;

        public RelayAllocationId FromAllocationId;
        public RelayAllocationId ToAllocationId;

        public static RelayMessageDisconnect Create(RelayAllocationId fromAllocationId, RelayAllocationId toAllocationId)
        {
            return new RelayMessageDisconnect
            {
                Header = RelayMessageHeader.Create(RelayMessageType.Disconnect),
                FromAllocationId = fromAllocationId,
                ToAllocationId = toAllocationId,
            };
        }

        public static void Write(ref PacketProcessor packetProcessor, ref RelayAllocationId fromAllocationId, ref RelayAllocationId toAllocationId)
        {
            RelayMessageHeader.Write(ref packetProcessor, RelayMessageType.Disconnect);
            packetProcessor.AppendToPayload<RelayAllocationId>(fromAllocationId);
            packetProcessor.AppendToPayload<RelayAllocationId>(toAllocationId);
        }
    }
}
