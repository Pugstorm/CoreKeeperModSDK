using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Networking.Transport.Relay
{
    internal static class RelayMessageBind
    {
        private const byte k_ConnectionDataLength = 255;
        private const byte k_HMACLength = 32;
        public const int Length = RelayMessageHeader.k_Length + 1 + 2 + 1 + k_ConnectionDataLength + k_HMACLength; // Header + AcceptMode + Nonce + ConnectionDataLength + ConnectionData + HMAC;

        // public RelayMessageHeader Header;
        // public byte AcceptMode;
        // public ushort Nonce;
        // public byte ConnectionDataLength;
        // public fixed byte ConnectionData[k_ConnectionDataLength];
        // public fixed byte HMAC[k_HMACLength];

        public static unsafe void Write(DataStreamWriter writer, byte acceptMode, ushort nonce, byte* connectionDataPtr, byte* hmac)
        {
            var header = RelayMessageHeader.Create(RelayMessageType.Bind);


            writer.WriteBytesUnsafe((byte*)&header, RelayMessageHeader.k_Length);
            writer.WriteByte(acceptMode);
            writer.WriteUShort(nonce);
            writer.WriteByte(k_ConnectionDataLength);
            writer.WriteBytesUnsafe(connectionDataPtr, k_ConnectionDataLength);
            writer.WriteBytesUnsafe(hmac, k_HMACLength);
        }

        public static unsafe void Write(ref PacketProcessor packetProcessor, ref RelayServerData serverData)
        {
            RelayMessageHeader.Write(ref packetProcessor, RelayMessageType.Bind);
            packetProcessor.AppendToPayload<byte>(0);
            packetProcessor.AppendToPayload<ushort>(serverData.Nonce);
            packetProcessor.AppendToPayload<byte>(k_ConnectionDataLength);
            packetProcessor.AppendToPayload<RelayConnectionData>(serverData.ConnectionData);

            fixed(byte* hmacPtr = serverData.HMAC)
            packetProcessor.AppendToPayload(hmacPtr, k_HMACLength);
        }
    }
}
