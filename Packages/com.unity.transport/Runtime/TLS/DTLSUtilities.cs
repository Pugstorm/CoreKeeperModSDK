using System.Runtime.CompilerServices;

namespace Unity.Networking.Transport.TLS
{
    /// <summary>Utility functions used by the DTLS layer.</summary>
    internal unsafe static class DTLSUtilities
    {
        /// <summary>Check if a DTLS message is a Client Hello message.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsClientHello(ref PacketProcessor packetProcessor)
        {
            // A DTLS message is a client hello if it's content type (offset 0 in the header) has
            // value 0x16 and if the handshake type (offset 13 in the header) has value 0x01.
            // Furthermore, any valid client hello will be at least 25 bytes long.
            //
            // Relying on DTLS header details like that isn't really optimal. Ideally we'd have our
            // DTLS library expose something to check if a message is a handshake message (and which
            // type of handshake message it is), but until then we rely on this hack.

            var ptr = (byte*)packetProcessor.GetUnsafePayloadPtr() + packetProcessor.Offset;
            var size = packetProcessor.Length;

            return size >= 25 && ptr[0] == (byte)0x16 && ptr[13] == (byte)0x01;
        }

        /// <summary>Check if a DTLS message is a Server Hello message.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsServerHello(ref PacketProcessor packetProcessor)
        {
            // A DTLS message is a server hello if it's content type (offset 0 in the header) has
            // value 0x16 and if the handshake type (offset 13 in the header) has value 0x02.
            // Furthermore, any valid server hello will be at least 25 bytes long.
            //
            // Relying on DTLS header details like that isn't really optimal. Ideally we'd have our
            // DTLS library expose something to check if a message is a handshake message (and which
            // type of handshake message it is), but until then we rely on this hack.

            var ptr = (byte*)packetProcessor.GetUnsafePayloadPtr() + packetProcessor.Offset;
            var size = packetProcessor.Length;

            return size >= 25 && ptr[0] == (byte)0x16 && ptr[13] == (byte)0x02;
        }
    }
}