#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
#if NETCODE_DEBUG
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Unity.NetCode.LowLevel.Unsafe
{
    [GenerateBurstMonoInterop("NetDebugInterop")]
    [BurstCompile]
    internal partial struct NetDebugInterop
    {
        [BurstDiscard]
        private static void _GetTimestamp(out FixedString32Bytes timestamp)
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
        }

        [BurstDiscard]
        private static void _GetTimestampWithTick(NetworkTick predictedTick, out FixedString128Bytes timestampAndTick)
        {
            _GetTimestamp(out var timestamp);
            if (predictedTick.IsValid)
                timestampAndTick = FixedString.Format("[{0}][PredictedTick:{1}]", timestamp, (predictedTick.TickIndexForValidTick));
            else
                timestampAndTick = FixedString.Format("[{0}][PredictedTick:Invalid]", timestamp);
        }
        [BurstMonoInteropMethod]
        [BurstDiscard]
        private static void _InitDebugPacketIfNotCreated(ref PacketDumpLogger netDebugPacket, in FixedString512Bytes logFolder, in FixedString128Bytes worldName, int connectionId)
        {
            if (!netDebugPacket.IsCreated)
            {
                netDebugPacket.Init(in logFolder, in worldName, connectionId);
            }
        }
    }

    internal partial struct NetDebugInterop
    {
        public static bool _initialized;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void _dlg_GetTimeStamp(out FixedString32Bytes timestamp);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void _dlg_GetTimestampWithTick(NetworkTick serverTick, out FixedString128Bytes timestampAndTick);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void _dlg_InitDebugPacketIfNotCreated(ref PacketDumpLogger netDebugPacket, in FixedString512Bytes logFolder, in FixedString128Bytes worldName, int connectionId);

        [AOT.MonoPInvokeCallback(typeof(_dlg_GetTimeStamp))]
        private static void _wrapper_GetTimestamp(out FixedString32Bytes timestamp)
        {
            _GetTimestamp(out timestamp);
        }
        [AOT.MonoPInvokeCallback(typeof(_dlg_GetTimestampWithTick))]
        private static void _wrapper_GetTimestampWithTick(NetworkTick tick, out FixedString128Bytes timestampWithTick)
        {
            _GetTimestampWithTick(tick, out timestampWithTick);
        }
        [AOT.MonoPInvokeCallback(typeof(_dlg_InitDebugPacketIfNotCreated))]
        private static void _wrapper_InitDebugPacketIfNotCreated(ref PacketDumpLogger netDebugPacket, in FixedString512Bytes logFolder, in FixedString128Bytes worldName, int connectionId)
        {
            _InitDebugPacketIfNotCreated(ref netDebugPacket, logFolder, worldName, connectionId);
        }

        public static void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            ManagedFunctionPtr<_dlg_GetTimeStamp, NetDebugInterop>.Init(_wrapper_GetTimestamp);
            ManagedFunctionPtr<_dlg_GetTimestampWithTick, NetDebugInterop>.Init(_wrapper_GetTimestampWithTick);
            ManagedFunctionPtr<_dlg_InitDebugPacketIfNotCreated, NetDebugInterop>.Init(_wrapper_InitDebugPacketIfNotCreated);

        }

        public static unsafe void InitDebugPacketIfNotCreated(ref PacketDumpLogger m_NetDebugPacket, in FixedString512Bytes logFolder, in FixedString128Bytes worldName, int connectionId)
        {
            if (BurstCompiler.IsEnabled)
            {
                var ptr = ManagedFunctionPtr<_dlg_InitDebugPacketIfNotCreated, NetDebugInterop>.Ptr;
                ((delegate *unmanaged[Cdecl]<ref PacketDumpLogger, in FixedString512Bytes, in FixedString512Bytes, int, void>)ptr)(
                    ref m_NetDebugPacket, logFolder, worldName, connectionId);
                return;
            }

            _InitDebugPacketIfNotCreated(ref m_NetDebugPacket, logFolder, worldName, connectionId);
        }

        public static unsafe void GetTimestamp(out FixedString32Bytes timestamp)
        {
            if (BurstCompiler.IsEnabled)
            {
                var ptr = ManagedFunctionPtr<_dlg_GetTimeStamp, NetDebugInterop>.Ptr;
                ((delegate *unmanaged[Cdecl]<out FixedString32Bytes, void>)ptr)(out timestamp);
                return;
            }

            _GetTimestamp(out timestamp);
        }

        public static unsafe void GetTimestampWithTick(NetworkTick serverTick,
            out FixedString128Bytes timestampWithTick)
        {
            if (BurstCompiler.IsEnabled)
            {
                var ptr = ManagedFunctionPtr<_dlg_GetTimestampWithTick, NetDebugInterop>.Ptr;
                ((delegate *unmanaged[Cdecl]<NetworkTick, out FixedString128Bytes, void>)ptr)(serverTick, out timestampWithTick);
                return;
            }

            _GetTimestampWithTick(serverTick, out timestampWithTick);
        }
    }
}
#endif
