using System.Runtime.InteropServices;

namespace Unity.Networking.Transport.Relay
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RelayMessageHeader
    {
        public const int k_Length = 4;
        public const ushort k_Signature = 0x72DA;
        public const byte k_Version = 0;

        public ushort Signature;
        public byte Version;
        public RelayMessageType Type;

        public bool IsValid()
        {
            return Signature == 0x72DA && Version == 0;
        }

        public static RelayMessageHeader Create(RelayMessageType type)
        {
            return new RelayMessageHeader
            {
                Signature = k_Signature,
                Version = k_Version,
                Type = type,
            };
        }

        public static void Write(ref PacketProcessor packetProcessor, RelayMessageType type)
        {
            packetProcessor.AppendToPayload<ushort>(k_Signature);
            packetProcessor.AppendToPayload<byte>(k_Version);
            packetProcessor.AppendToPayload<RelayMessageType>(type);
        }
    }

    internal enum RelayMessageType : byte
    {
        Bind = 0,
        BindReceived = 1,
        Ping = 2,
        ConnectRequest = 3,
        Accepted = 6,
        Disconnect = 9,
        Relay = 10,
        Error = 12,
    }
}
