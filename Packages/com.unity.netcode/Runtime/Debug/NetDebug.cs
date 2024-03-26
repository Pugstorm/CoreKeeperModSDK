#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif

#if USING_OBSOLETE_METHODS_VIA_INTERNALSVISIBLETO
#pragma warning disable 0436
#endif

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Burst;
using Unity.Entities;
using Logger = Unity.Logging.Logger;

using Unity.Logging;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;

#if NETCODE_DEBUG
namespace Unity.NetCode.LowLevel.Unsafe
{
    internal partial struct NetDebugInterop
    {
        static class Managed
        {
            public static bool _initialized;
            public delegate void _dlg_GetTimeStamp(out FixedString32Bytes timestamp);
            public static object _gcDefeat_GetTimeStamp;

            public delegate void _dlg_GetTimestampWithTick(NetworkTick serverTick, out FixedString128Bytes timestampAndTick);
            public static object _gcDefeat_GetTimestampWithTick;

            public delegate void _dlg_InitDebugPacketIfNotCreated(ref NetDebugPacket m_NetDebugPacket, ref FixedString512Bytes logFolder, ref FixedString128Bytes worldName, int connectionId);
            public static object _gcDefeat_InitDebugPacketIfNotCreated;
        }

        struct TagType_InitDebugPacketIfNotCreated {}
        public static readonly SharedStatic<IntPtr> _bfp_InitDebugPacketIfNotCreated = SharedStatic<IntPtr>.GetOrCreate<TagType_InitDebugPacketIfNotCreated>();

        struct TagType_GetTimestamp {}
        public static readonly SharedStatic<IntPtr> _bfp_GetTimestamp = SharedStatic<IntPtr>.GetOrCreate<TagType_GetTimestamp>();

        struct TagType_GetTimestampWithTick {}
        public static readonly SharedStatic<IntPtr> _bfp_GetTimestampWithTick = SharedStatic<IntPtr>.GetOrCreate<TagType_GetTimestampWithTick>();

        public static void Initialize()
        {
            if (Managed._initialized) { return; }
            Managed._initialized = true;

            Managed._dlg_InitDebugPacketIfNotCreated delegateInitDebugPacket = _wrapper_InitDebugPacketIfNotCreated;
            Managed._gcDefeat_InitDebugPacketIfNotCreated = delegateInitDebugPacket;
            _bfp_InitDebugPacketIfNotCreated.Data = Marshal.GetFunctionPointerForDelegate(delegateInitDebugPacket);

            Managed._dlg_GetTimeStamp delegateGetTimestamp = _wrapper_GetTimestamp;
            Managed._gcDefeat_GetTimeStamp = delegateGetTimestamp;
            _bfp_GetTimestamp.Data = Marshal.GetFunctionPointerForDelegate(delegateGetTimestamp);

            Managed._dlg_GetTimestampWithTick delegateGetTimestampWithTick = _wrapper_GetTimestampWithTick;
            Managed._gcDefeat_GetTimestampWithTick = delegateGetTimestampWithTick;
            _bfp_GetTimestampWithTick.Data = Marshal.GetFunctionPointerForDelegate(delegateGetTimestampWithTick);
        }

        [AOT.MonoPInvokeCallback(typeof(Managed._dlg_InitDebugPacketIfNotCreated))]
        private static void _wrapper_InitDebugPacketIfNotCreated(ref NetDebugPacket m_NetDebugPacket, ref FixedString512Bytes logFolder, ref FixedString128Bytes worldName, int connectionId)
        {
            _InitDebugPacketIfNotCreated(ref m_NetDebugPacket, ref logFolder, ref worldName, connectionId);
        }

        [AOT.MonoPInvokeCallback(typeof(Managed._dlg_GetTimeStamp))]
        private static void _wrapper_GetTimestamp(out FixedString32Bytes timestamp)
        {
            _GetTimestamp(out timestamp);
        }

        [AOT.MonoPInvokeCallback(typeof(Managed._dlg_InitDebugPacketIfNotCreated))]
        private static void _wrapper_GetTimestampWithTick(NetworkTick tick, out FixedString128Bytes timestampWithTick)
        {
            _GetTimestampWithTick(tick, out timestampWithTick);
        }

        public static void InitDebugPacketIfNotCreated(ref NetDebugPacket m_NetDebugPacket, ref FixedString512Bytes logFolder, ref FixedString128Bytes worldName, int connectionId)
        {
// TODO: Burst (1.7.3) does not provide a BurstCompiler.IsEnabled for DOTS Runtime. Remove once a newer version adds this property
#if !UNITY_DOTSRUNTIME
            if (BurstCompiler.IsEnabled)
            {
                CheckInteropClassInitialized(_bfp_InitDebugPacketIfNotCreated.Data);
                var fp = new FunctionPointer<Managed._dlg_InitDebugPacketIfNotCreated>(_bfp_InitDebugPacketIfNotCreated.Data);
                fp.Invoke(ref m_NetDebugPacket, ref logFolder, ref worldName, connectionId);
                return;
            }
#endif

            _InitDebugPacketIfNotCreated(ref m_NetDebugPacket, ref logFolder, ref worldName, connectionId);
        }

        public static void GetTimestamp(out FixedString32Bytes timestamp)
        {
// TODO: Burst (1.7.3) does not provide a BurstCompiler.IsEnabled for DOTS Runtime. Remove once a newer version adds this property
#if !UNITY_DOTSRUNTIME
            if (BurstCompiler.IsEnabled)
            {
                CheckInteropClassInitialized(_bfp_GetTimestamp.Data);
                var fp = new FunctionPointer<Managed._dlg_GetTimeStamp>(_bfp_GetTimestamp.Data);
                fp.Invoke(out timestamp);
                return;
            }
#endif

            _GetTimestamp(out timestamp);
        }

        public static void GetTimestampWithTick(NetworkTick serverTick, out FixedString128Bytes timestampWithTick)
        {
// TODO: Burst (1.7.3) does not provide a BurstCompiler.IsEnabled for DOTS Runtime. Remove once a newer version adds this property
#if !UNITY_DOTSRUNTIME
            if (BurstCompiler.IsEnabled)
            {
                CheckInteropClassInitialized(_bfp_GetTimestampWithTick.Data);
                var fp = new FunctionPointer<Managed._dlg_GetTimestampWithTick>(_bfp_GetTimestampWithTick.Data);
                fp.Invoke(serverTick, out timestampWithTick);
                return;
            }
#endif

            _GetTimestampWithTick(serverTick, out timestampWithTick);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckInteropClassInitialized(IntPtr intPtr)
        {
            if (intPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Burst Interop Classes must be initialized manually");
            }
        }
    }

    [GenerateBurstMonoInterop("NetDebugInterop")]
    [BurstCompile]
    internal partial struct NetDebugInterop
    {
        [BurstMonoInteropMethod]
        [BurstDiscard]
        private static void _InitDebugPacketIfNotCreated(ref NetDebugPacket netDebugPacket, ref FixedString512Bytes logFolder, ref FixedString128Bytes worldName, int connectionId)
        {
            if (!netDebugPacket.IsCreated)
            {
                netDebugPacket.Init(ref logFolder, ref worldName, connectionId);
            }
        }

        [BurstMonoInteropMethod]
        [BurstDiscard]
        private static void _GetTimestamp(out FixedString32Bytes timestamp)
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
        }

        [BurstMonoInteropMethod]
        [BurstDiscard]
        private static void _GetTimestampWithTick(NetworkTick predictedTick, out FixedString128Bytes timestampAndTick)
        {
            _GetTimestamp(out var timestamp);
            if (predictedTick.IsValid)
                timestampAndTick = FixedString.Format("[{0}][PredictedTick:{1}]", timestamp, (predictedTick.TickIndexForValidTick));
            else
                timestampAndTick = FixedString.Format("[{0}][PredictedTick:Invalid]", timestamp);
        }
    }

    public struct NetDebugPacket
    {
        private LoggerHandle m_NetDebugPacketLoggerHandle;

        public void Init(ref FixedString512Bytes logFolder, ref FixedString128Bytes worldName, int connectionId)
        {
            LogMemoryManagerParameters.GetDefaultParameters(out var parameters);
            parameters.InitialBufferCapacity *= 64;
            parameters.OverflowBufferSize *= 32;

            m_NetDebugPacketLoggerHandle = new LoggerConfig()
                .OutputTemplate("{Message}")
                .MinimumLevel.Set(LogLevel.Verbose)
                .WriteTo.File($"{logFolder}/NetcodePacket-{worldName}-{connectionId}.log")
                .CreateLogger(parameters).Handle;
        }

        public bool IsCreated => m_NetDebugPacketLoggerHandle.IsValid;

        public void Log(in FixedString512Bytes msg)
        {
            Unity.Logging.Log.To(m_NetDebugPacketLoggerHandle).Info(msg);
        }
    }
}
#endif

namespace Unity.NetCode
{
    /// <summary>
    /// Add this component to connection entities <see cref="NetworkStreamConnection"/> to get more detailed Netcode
    /// debug information (`Debug` level) in general or to enable ghost snapshot or packet logging per connection.
    /// Debug information can be toggled globally in the Playmode Tools Window and in the `NetCodeDebugConfigAuthoring` component.
    /// </summary>
    public struct EnablePacketLogging : IComponentData
    { }

#if NETCODE_DEBUG
    /// <summary>
    /// The name of ghost prefab. Used for debugging purpose to pretty print ghost names. Available only once the
    /// NETCODE_DEBUG define is set.
    /// </summary>
    public struct PrefabDebugName : IComponentData
    {
        public FixedString64Bytes Name;
    }
#endif

    /// <summary>
    /// Convert disconnection reason error code into human readable error messages.
    /// </summary>
    public struct DisconnectReasonEnumToString
    {
        private static readonly FixedString32Bytes ConnectionClose = "ConnectionClose";
        private static readonly FixedString32Bytes Timeout = "Timeout";
        private static readonly FixedString32Bytes MaxConnectionAttempts = "MaxConnectionAttempts";
        private static readonly FixedString32Bytes ClosedByRemote = "ClosedByRemote";
        private static readonly FixedString32Bytes BadProtocolVersion = "BadProtocolVersion";
        private static readonly FixedString32Bytes InvalidRpc = "InvalidRpc";

        /// <summary>
        /// Translate the error code into a human friendly error message.
        /// </summary>
        /// <param name="index">The disconnect error reason</param>
        /// <returns>
        /// A string with the error message
        /// </returns>
        public static FixedString32Bytes Convert(int index)
        {
            switch (index)
            {
                case 0: return ConnectionClose;
                case 1: return Timeout;
                case 2: return MaxConnectionAttempts;
                case 3: return ClosedByRemote;
                case 4: return BadProtocolVersion;
                case 5: return InvalidRpc;
            }
            return "";
        }
    }

    /// <summary>Singleton handling NetCode logging and log management.</summary>
    public struct NetDebug : IComponentData
    {
        /// <summary>
        /// Use this method to retrieve the platform specific folder where the NetCode logs files
        /// will be stored.
        /// On Desktop it use the <see cref="UnityEngine.Application.consoleLogPath"/> is used.
        /// For mobile, the <see cref="UnityEngine.Application.persistentDataPath"/> is used.
        /// For DOTS Runtime builds, it is possible to customise the output by using the -logfile command line switch.
        ///
        /// In all cases, if the log path is null or empty, the Logs folder in the current directory is used instead.
        /// </summary>
        /// <returns>A string containg the log folder full path</returns>
        public static string LogFolderForPlatform()
        {
#if UNITY_DOTSRUNTIME
            var args = Environment.GetCommandLineArgs();
            var optIndex = System.Array.IndexOf(args, "-logFile");
            if (optIndex >=0 && ++optIndex < (args.Length - 1) && !args[optIndex].StartsWith('-'))
                return args[optIndex];
            //FIXME: should return the common application log path (if that exist defined somewhere)
#elif UNITY_ANDROID || UNITY_IOS
            var persistentLogPath = UnityEngine.Application.persistentDataPath;
            if (!string.IsNullOrEmpty(persistentLogPath))
                return persistentLogPath;
#else
            //by default logs are output in the same location as player and console output does
            var consoleLogPath = UnityEngine.Application.consoleLogPath;
            if (!string.IsNullOrEmpty(consoleLogPath))
                return Path.GetDirectoryName(UnityEngine.Application.consoleLogPath);
#endif
            return "Logs";
        }

        //TODO: logging should already give us a good folder for that purpose by default
        internal static FixedString512Bytes GetAndCreateLogFolder()
        {
            var logPath = LogFolderForPlatform();
            if (!Directory.Exists(logPath))
                Directory.CreateDirectory(logPath);
            return logPath;
        }

        private ushort m_MaxRpcAgeFrames;
        private LogLevelType m_LogLevel;

#if NETCODE_DEBUG
        internal NativeParallelHashMap<int, FixedString128Bytes>.ReadOnly ComponentTypeNameLookup;
#endif

        private LogLevel m_CurrentLogLevel;
        private LoggerHandle m_LoggerHandle;

        private void SetLoggerLevel(LogLevelType newLevel)
        {
            m_CurrentLogLevel = newLevel switch
            {
                LogLevelType.Debug => Logging.LogLevel.Debug,
                LogLevelType.Notify => Logging.LogLevel.Info,
                LogLevelType.Warning => Logging.LogLevel.Warning,
                LogLevelType.Error => Logging.LogLevel.Error,
                LogLevelType.Exception => Logging.LogLevel.Fatal,
                _ => throw new ArgumentOutOfRangeException()
            };

            var logger = GetOrCreateLogger();
            logger.SetMinimalLogLevelAcrossAllSinks(m_CurrentLogLevel);
        }

        private Logger GetOrCreateLogger()
        {
            Logger logger = null;
            if (m_LoggerHandle.IsValid)
                logger = LoggerManager.GetLogger(m_LoggerHandle);

            if (logger == null)
            {
                logger = new LoggerConfig().MinimumLevel
                    .Set(m_CurrentLogLevel)
#if !UNITY_DOTSRUNTIME
                    //Use correct format that is compatible with current unity logging
                    .WriteTo.UnityDebugLog(minLevel: m_CurrentLogLevel, outputTemplate: new FixedString512Bytes("{Message}"))
#else
                    .WriteTo.StdOut()
                    .WriteTo.File($"{NetDebug.GetAndCreateLogFolder()}/Netcode-{Guid.NewGuid()}.txt")
#endif
                    .CreateLogger();
                m_LoggerHandle = logger.Handle;
            }

            return logger;
        }

        internal void Initialize()
        {
            MaxRpcAgeFrames = 4;
            LogLevel = LogLevelType.Notify;
        }

        /// <summary>
        /// Destroy the internal resources allocated by the debug logger and flush any pending messages.
        /// </summary>
        public void Dispose()
        {
            if (!m_LoggerHandle.IsValid)
                return;
            var logger = LoggerManager.GetLogger(m_LoggerHandle);
            logger?.Dispose();

            m_LoggerHandle = default;
        }

        /// <summary>
        ///     A NetCode RPC will trigger a warning if it hasn't been consumed or destroyed (which is a proxy for 'handled') after
        ///     this many simulation frames (inclusive).
        ///     <see cref="ReceiveRpcCommandRequest.Age" />.
        ///     Set to 0 to opt out.
        /// </summary>
        public ushort MaxRpcAgeFrames
        {
            get => m_MaxRpcAgeFrames;
            set
            {
                m_MaxRpcAgeFrames = value;
            }
        }
        /// <summary>
        /// The current debug logging level. Default value is <see cref="LogLevelType.Notify"/>.
        /// </summary>
        public LogLevelType LogLevel
        {
            set
            {
                m_LogLevel = value;

                SetLoggerLevel(m_LogLevel);
            }
            get => m_LogLevel;
        }

        /// <summary>
        /// The available NetCode logging levels. <see cref="Notify"/> is the default. Use the
        /// <see cref="NetCodeDebugConfig"/> component to configure the logging level.
        /// </summary>
        public enum LogLevelType
        {
            /// <summary>
            /// Debug level. This is the most verbose and only debug messages should use this.
            /// </summary>
            Debug = 1,
            /// <summary>
            /// Default debug level. Non-spamming messages that contains useful information and that don't have measurable performance
            /// impact can use this.
            /// </summary>
            Notify = 2,
            /// <summary>
            /// Level to use for non-critical errors or potential issues.
            /// </summary>
            Warning = 3,
            /// <summary>
            /// Level to use for all error messages (critical or not).
            /// </summary>
            Error = 4,
            /// <summary>
            /// When set, only exception will be output.
            /// </summary>
            Exception = 5,
        }

        /// <summary>
        /// Print the log message with Debug level priority;
        /// </summary>
        /// <param name="msg">The ascii message string. Unicode are not supported</param>
        public readonly void DebugLog(in FixedString512Bytes msg)
        {
            Unity.Logging.Log.To(m_LoggerHandle).Debug(msg);
        }

        /// <summary>
        /// Print a log message with Notify level priority;
        /// </summary>
        /// <param name="msg">The ascii message string. Unicode are not supported</param>
        public readonly void Log(in FixedString512Bytes msg)
        {
            Unity.Logging.Log.To(m_LoggerHandle).Info(msg);
        }

        /// <summary>
        /// Print a log message with warning priority
        /// </summary>
        /// <param name="msg">The ascii message string. Unicode are not supported</param>
        public readonly void LogWarning(in FixedString512Bytes msg)
        {
            Unity.Logging.Log.To(m_LoggerHandle).Warning(msg);
        }

        /// <summary>
        /// Print a log message with error priority
        /// </summary>
        /// <param name="msg">The ascii message string. Unicode are not supported</param>
        public readonly void LogError(in FixedString512Bytes msg)
        {
            Unity.Logging.Log.To(m_LoggerHandle).Error(msg);
        }

        /// <summary>
        /// Utility method to print an unsigned integer bitmask as string.
        /// All the MSB zeros before the first bit set are skipped.
        /// Ex:
        /// mask: 00010 0001 0000 0010
        /// will be printed as "10000100000010"
        /// </summary>
        /// <param name="mask">The bit mask to print</param>
        /// <returns></returns>
        internal static FixedString64Bytes PrintMask(uint mask)
        {
            FixedString64Bytes maskString = default;
            for (int i = 0; i < 32; ++i)
            {
                var bit = (mask>>31)&1;
                mask <<= 1;
                if (maskString.Length == 0 && bit == 0)
                    continue;
                maskString.Append(bit);
            }

            if (maskString.Length == 0)
                maskString = "0";
            return maskString;
        }

        /// <summary>
        /// Method that print a human readable error message when the version protocol mismatch.
        /// </summary>
        /// <param name="error"></param>
        /// <param name="protocolVersion"></param>
        internal static void AppendProtocolVersionError(ref FixedString512Bytes error, NetworkProtocolVersion protocolVersion)
        {
            error.Append(FixedString.Format("NetCode={0} Game={1}", protocolVersion.NetCodeVersion,
                protocolVersion.GameVersion));
            FixedString32Bytes msg = " RpcCollection=";
            error.Append(msg);
            error.Append(protocolVersion.RpcCollectionVersion);
            msg = " ComponentCollection=";
            error.Append(msg);
            error.Append(protocolVersion.ComponentCollectionVersion);
        }

        /// <summary>
        /// Print an unsigned long integer in hexadecimal format.
        /// </summary>
        /// <param name="value">the integer number to convert</param>
        /// <param name="bitSize">the number of bits we want to print. Must be a multiple of 4.</param>
        /// <returns></returns>
        internal static FixedString32Bytes PrintHex(ulong value, int bitSize)
        {
            FixedString32Bytes temp = new FixedString32Bytes();
            temp.Add((byte)'0');
            temp.Add((byte)'x');
            if (value == 0)
            {
                temp.Add((byte)'0');
                return temp;
            }
            int i = bitSize;
            do
            {
                i -= 4;
                int nibble = (int) (value >> i) & 0xF;
                if(nibble == 0 && temp.Length == 2)
                    continue;
                nibble += (nibble >= 10) ? 'A' - 10 : '0';
                temp.Add((byte)nibble);
            } while (i > 0);
            return temp;
        }
        /// <summary>
        /// Print an unsigned integer in hexadecimal format
        /// </summary>
        /// <param name="value">The unsigned value to convert</param>
        /// <returns></returns>
        public static FixedString32Bytes PrintHex(uint value)
        {
            return PrintHex(value, 32);
        }
        /// <summary>
        /// Print a unsigned long integer in hexadecimal format
        /// </summary>
        /// <param name="value">The unsigned value to convert</param>
        /// <returns></returns>
        public static FixedString32Bytes PrintHex(ulong value)
        {
            return PrintHex(value, 64);
        }
    }
}

#if USING_OBSOLETE_METHODS_VIA_INTERNALSVISIBLETO
#pragma warning restore 0436
#endif
