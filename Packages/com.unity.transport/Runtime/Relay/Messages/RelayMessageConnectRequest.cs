using System.Runtime.InteropServices;

namespace Unity.Networking.Transport.Relay
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RelayMessageConnectRequest
    {
        public const int k_Length = RelayMessageHeader.k_Length + RelayAllocationId.k_Length + 1 + RelayConnectionData.k_Length; // Header + AllocationId + ToConnectionDataLength + ToConnectionData;

        public RelayMessageHeader Header;

        public RelayAllocationId AllocationId;
        public byte ToConnectionDataLength;
        public RelayConnectionData ToConnectionData;

        public static RelayMessageConnectRequest Create(RelayAllocationId allocationId, RelayConnectionData toConnectionData)
        {
            return new RelayMessageConnectRequest
            {
                Header = RelayMessageHeader.Create(RelayMessageType.ConnectRequest),
                AllocationId = allocationId,
                ToConnectionDataLength = 255,
                ToConnectionData = toConnectionData,
            };
        }

        public static void Write(ref PacketProcessor packetProcessor, ref RelayAllocationId allocationId, ref RelayConnectionData toConnectionData)
        {
            RelayMessageHeader.Write(ref packetProcessor, RelayMessageType.ConnectRequest);
            packetProcessor.AppendToPayload<RelayAllocationId>(allocationId);
            packetProcessor.AppendToPayload<byte>(255);
            packetProcessor.AppendToPayload<RelayConnectionData>(toConnectionData);
        }
    }
}
