using System.Runtime.InteropServices;
using Unity.Collections;

namespace Unity.Networking.Transport.Relay
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RelayMessageRelay
    {
        public const int k_Length = RelayMessageHeader.k_Length + RelayAllocationId.k_Length * 2 + 2; // Header + FromAllocationId + ToAllocationId + DataLength

        public RelayMessageHeader Header;

        public RelayAllocationId FromAllocationId;
        public RelayAllocationId ToAllocationId;
        private ushort m_DataLength;

        public ushort DataLength
        {
            get => SwitchEndianness(m_DataLength);
            set => m_DataLength = SwitchEndianness(value);
        }

        internal static ushort SwitchEndianness(ushort value)
        {
            if (DataStreamWriter.IsLittleEndian)
                return (ushort)((value << 8) | (value >> 8));

            return value;
        }

        public static RelayMessageRelay Create(RelayAllocationId fromAllocationId, RelayAllocationId toAllocationId, ushort dataLength)
        {
            return new RelayMessageRelay
            {
                Header = RelayMessageHeader.Create(RelayMessageType.Relay),
                FromAllocationId = fromAllocationId,
                ToAllocationId = toAllocationId,
                DataLength = dataLength,
            };
        }

        public static void Write(ref PacketProcessor packetProcessor, ref RelayAllocationId fromAllocationId, ref RelayAllocationId toAllocationId, ushort dataLength)
        {
            packetProcessor.PrependToPayload<ushort>(SwitchEndianness(dataLength));
            packetProcessor.PrependToPayload<RelayAllocationId>(toAllocationId);
            packetProcessor.PrependToPayload<RelayAllocationId>(fromAllocationId);
            packetProcessor.PrependToPayload(RelayMessageHeader.Create(RelayMessageType.Relay));
        }
    }
}
