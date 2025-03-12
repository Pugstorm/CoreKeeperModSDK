using System;

namespace Unity.Networking.Transport
{
    internal struct PacketBuffer
    {
        public IntPtr Metadata;
        public IntPtr Payload;
        public IntPtr Endpoint;
    }
}
