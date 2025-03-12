using System;
using System.Runtime.InteropServices;

namespace Unity.Networking.Transport.Protocols
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct UdpCHeader
    {
        [Flags]
        public enum HeaderFlags : byte
        {
            HasPipeline = 0x1
        }

        public const int Length = 2 + ConnectionToken.k_Length;  //explanation of constant 2 in this expression = sizeof(Type) + sizeof(HeaderFlags)
        public byte Type;
        public HeaderFlags Flags;
        public ConnectionToken ConnectionToken;
    }
}
