using System;
using System.Runtime.InteropServices;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Networking.Transport.Logging;
using Unity.TLS.LowLevel;
using UnityEngine;

namespace Unity.Networking.Transport.TLS
{
    /// <summary>Callbacks to use with UnityTLS.</summary>
    internal unsafe static class UnityTLSCallbacks
    {
        /// <summary>Structure that callbacks expect a pointer to as their user data.</summary>
        public struct CallbackContext
        {
            /// <summary>The last received encrypted packet (if any).</summary>
            public PacketProcessor ReceivedPacket;

            /// <summary>Queue from which to grab packets to send/replace.</summary>
            public PacketsQueue SendQueue;

            /// <summary>Index of the packet to replace in the send queue (if any).</summary>
            /// <remarks>Negative value means enqueueing a new packet on the queue.</remarks>
            public int SendQueueIndex;

            /// <summary>Padding already reserved for the layer in the packet.</summary>
            public int PacketPadding;

            /// <summary>Endpoint of newly-enqueued packets.</summary>
            public NetworkEndpoint NewPacketsEndpoint;

            /// <summary>Connection ID of newly-enqueued packets.</summary>
            public ConnectionId NewPacketsConnection;
        }

        // TODO Most of the boilerplate here could be avoided by using C# 9.0 function pointers.

        private static Binding.unitytls_client_data_send_callback s_SendCallbackDelegate;
        private static Binding.unitytls_client_data_receive_callback s_ReceiveCallbackDelegate;
        private static Binding.unitytls_client_log_callback s_LogCallbackDelegate;

        private struct FunctionPointersKey {}

        private static readonly SharedStatic<FunctionPointer<Binding.unitytls_client_data_send_callback>>
            s_SendCallbackPtr = SharedStatic<FunctionPointer<Binding.unitytls_client_data_send_callback>>
                .GetOrCreate<FunctionPointer<Binding.unitytls_client_data_send_callback>, FunctionPointersKey>();

        private static readonly SharedStatic<FunctionPointer<Binding.unitytls_client_data_receive_callback>>
            s_ReceiveCallbackPtr = SharedStatic<FunctionPointer<Binding.unitytls_client_data_receive_callback>>
                .GetOrCreate<FunctionPointer<Binding.unitytls_client_data_receive_callback>, FunctionPointersKey>();

        private static readonly SharedStatic<FunctionPointer<Binding.unitytls_client_log_callback>>
            s_LogCallbackPtr = SharedStatic<FunctionPointer<Binding.unitytls_client_log_callback>>
                .GetOrCreate<FunctionPointer<Binding.unitytls_client_log_callback>, FunctionPointersKey>();

        private static bool s_Initialized;

        /// <summary>Function pointer to the send callback.</summary>
        public static IntPtr SendCallbackPtr => s_Initialized ? s_SendCallbackPtr.Data.Value : IntPtr.Zero;

        /// <summary>Function pointer to the receive callback.</summary>
        public static IntPtr ReceiveCallbackPtr => s_Initialized ? s_ReceiveCallbackPtr.Data.Value : IntPtr.Zero;

        /// <summary>Function pointer to the log callback.</summary>
        public static IntPtr LogCallbackPtr => s_Initialized ? s_LogCallbackPtr.Data.Value : IntPtr.Zero;

        /// <summary>Initialize the function pointers of the callbacks.</summary>
        /// <remarks>Must be called from managed code.</remarks>
        public static void Initialize()
        {
            if (!s_Initialized)
            {
                s_Initialized = true;

                s_SendCallbackDelegate = SendCallback;
                s_ReceiveCallbackDelegate = ReceiveCallback;
                s_LogCallbackDelegate = LogCallback;

                var sendPtr = Marshal.GetFunctionPointerForDelegate(s_SendCallbackDelegate);
                s_SendCallbackPtr.Data = new FunctionPointer<Binding.unitytls_client_data_send_callback>(sendPtr);

                var recvPtr = Marshal.GetFunctionPointerForDelegate(s_ReceiveCallbackDelegate);
                s_ReceiveCallbackPtr.Data = new FunctionPointer<Binding.unitytls_client_data_receive_callback>(recvPtr);

                var logPtr = Marshal.GetFunctionPointerForDelegate(s_LogCallbackDelegate);
                s_LogCallbackPtr.Data = new FunctionPointer<Binding.unitytls_client_log_callback>(logPtr);
            }
        }

        // For some reason UnityTLS doesn't expose those in the bindings...
        private const int UNITYTLS_ERR_SSL_WANT_READ = -0x6900;
        private const int UNITYTLS_ERR_SSL_WANT_WRITE = -0x6880;

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(Binding.unitytls_client_data_send_callback))]
        private static int SendCallback(IntPtr userData, byte* data, UIntPtr dataLength, uint status)
        {
            var ctx = (CallbackContext*)userData;
            var length = (int)dataLength.ToUInt32();

            // If we're provided a send queue index, it means we're expected to write the data in
            // the packet it refers to. Otherwise we take a packet from the provided send queue (in
            // the case of TLS, this could mean writing the payload over multiple packets).
            if (ctx->SendQueueIndex >= 0)
            {
                var packet = ctx->SendQueue[ctx->SendQueueIndex];

                // Compute the new packet offset. The stack has already reserved the packet padding
                // at the start of the packet, we want to use this space for the encrypted packet.
                var newOffset = packet.Offset - ctx->PacketPadding;
                if (newOffset < 0)
                {
                    DebugLog.ErrorTLSInvalidOffset(packet.Offset, ctx->PacketPadding);
                    // TODO Is this really the correct error code for this situation?
                    return UNITYTLS_ERR_SSL_WANT_WRITE;
                }

                // We reset the metadata to that of a 0-length packet at the right offset so that
                // AppendToPayload copies the encrypted data at the right place.
                packet.SetUnsafeMetadata(0, newOffset);
                packet.AppendToPayload(data, length);
            }
            else
            {
                var offset = 0;
                while (offset < length)
                {
                    // TODO What's the correct way of handling partial sends?
                    if (!ctx->SendQueue.EnqueuePacket(out var packet))
                        return offset > 0 ? offset : UNITYTLS_ERR_SSL_WANT_WRITE;

                    // No need to adjust the offset when sending directly through the send queue,
                    // but we do need to set the endpoint and connection appropriately.
                    packet.EndpointRef = ctx->NewPacketsEndpoint;
                    packet.ConnectionRef = ctx->NewPacketsConnection;

                    var packetLength = math.min(length - offset, packet.BytesAvailableAtEnd);
                    packet.AppendToPayload(data + offset, packetLength);

                    offset += packetLength;
                }
            }

            return length;
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(Binding.unitytls_client_data_receive_callback))]
        private static int ReceiveCallback(IntPtr userData, byte* data, UIntPtr dataLength, uint status)
        {
            var ctx = (CallbackContext*)userData;
            if (!ctx->ReceivedPacket.IsCreated || ctx->ReceivedPacket.Length == 0)
                return UNITYTLS_ERR_SSL_WANT_READ;

            // The value of dataLength is the length of the buffer provided by UnityTLS. For DTLS,
            // this is always some super large value that's sure to accomodate the entire received
            // packet. But when using TLS, the values will be much smaller because UnityTLS expects
            // to read the stream little pieces at a time.
            var removeLength = math.min((int)dataLength.ToUInt32(), ctx->ReceivedPacket.Length);
            ctx->ReceivedPacket.RemoveFromPayloadStart(data, removeLength);

            return removeLength;
        }

        [BurstCompile(DisableDirectCall = true)]
        [MonoPInvokeCallback(typeof(Binding.unitytls_client_log_callback))]
        private static void LogCallback(int level, byte* file, UIntPtr line, byte* function, byte* message, UIntPtr messageLength)
        {
            FixedString512Bytes log = "[UnityTLS";

            // Append the file name and line number if provided.
            if (file != null && RawStringLength(file) != 0)
            {
                log.Append(':');
                log.Append(file, RawStringLength(file));
                log.Append(':');
                log.Append(line.ToUInt32());
            }

            // Append the function name if provided.
            if (function != null && RawStringLength(function) != 0)
            {
                log.Append(':');
                log.Append(function, RawStringLength(function));
            }

            // Append the actual log message.
            log.Append(']');
            log.Append(' ');
            log.Append(message, (int)messageLength.ToUInt32());

            // TODO Use the log level to pick the right logging method.
            DebugLog.Log(log);
        }

        // Can't believe we're reimplementing strlen() from C...
        private static int RawStringLength(byte *str)
        {
            var length = 0;
            while (str[length] != '\0')
                length++;
            return length;
        }
    }
}
