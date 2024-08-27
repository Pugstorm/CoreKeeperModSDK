#if USING_OBSOLETE_METHODS_VIA_INTERNALSVISIBLETO
#pragma warning disable 0436
#endif
using System;
using System.IO;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
#if USING_UNITY_LOGGING
using Logger = Unity.Logging.Logger;
using Unity.Logging;
using Unity.Logging.Internal;
using Unity.Logging.Sinks;
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

    /// <summary>
    /// Convert disconnection reason error code into human readable error messages.
    /// </summary>
    [Obsolete("Use ToFixedString extension methods. (RemovedAfter Entities 2.0)", false)]
    public struct DisconnectReasonEnumToString
    {
        /// <summary>
        /// Translate the error code into a human friendly error message.
        /// </summary>
        /// <param name="index">The disconnect error reason</param>
        /// <returns>
        /// A string with the error message
        /// </returns>
        public static FixedString32Bytes Convert(int index)
        {
            return ((NetworkStreamDisconnectReason) index).ToFixedString();
        }
    }

    /// <summary>
    /// ToFixedString utilities for enums.
    /// </summary>
    public static class NetCodeUtils
    {
        /// <summary>
        /// Returns the Fixed String enum value name.
        /// </summary>
        /// <param name="reason"></param>
        /// <returns>Returns the Fixed String enum value name.</returns>
        public static FixedString32Bytes ToFixedString(this NetworkStreamDisconnectReason reason)
        {
            switch (reason)
            {
                case NetworkStreamDisconnectReason.ConnectionClose: return nameof(NetworkStreamDisconnectReason.ConnectionClose);
                case NetworkStreamDisconnectReason.Timeout: return nameof(NetworkStreamDisconnectReason.Timeout);
                case NetworkStreamDisconnectReason.MaxConnectionAttempts: return nameof(NetworkStreamDisconnectReason.MaxConnectionAttempts);
                case NetworkStreamDisconnectReason.ClosedByRemote: return nameof(NetworkStreamDisconnectReason.ClosedByRemote);
                case NetworkStreamDisconnectReason.BadProtocolVersion: return nameof(NetworkStreamDisconnectReason.BadProtocolVersion);
                case NetworkStreamDisconnectReason.InvalidRpc: return nameof(NetworkStreamDisconnectReason.InvalidRpc);
                case NetworkStreamDisconnectReason.AuthenticationFailure: return nameof(NetworkStreamDisconnectReason.AuthenticationFailure);
                case NetworkStreamDisconnectReason.ProtocolError: return nameof(NetworkStreamDisconnectReason.ProtocolError);
                default: return $"DisconnectReason_{(int) reason}";
            }
        }


        /// <summary>
        /// Converts from the Transport state to ours.
        /// </summary>
        /// <param name="transportState"></param>
        /// <param name="hasHandshaked"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static ConnectionState.State ToNetcodeState(this NetworkConnection.State transportState, bool hasHandshaked)
        {
            switch (transportState)
            {
                case NetworkConnection.State.Connected: return Hint.Likely(hasHandshaked) ? ConnectionState.State.Connected : ConnectionState.State.Handshake;
                case NetworkConnection.State.Disconnected: return ConnectionState.State.Disconnected;
                case NetworkConnection.State.Disconnecting: return ConnectionState.State.Disconnected;
                case NetworkConnection.State.Connecting: return ConnectionState.State.Connecting;
                default:
                    throw new ArgumentOutOfRangeException(nameof(transportState), transportState, nameof(ToNetcodeState));
            }
        }

        /// <summary>
        /// Returns the Fixed String enum value name.
        /// </summary>
        /// <param name="state"></param>
        /// <returns>Returns the Fixed String enum value name.</returns>
        public static FixedString32Bytes ToFixedString(this ConnectionState.State state)
        {
            switch (state)
            {
                case ConnectionState.State.Unknown: return nameof(ConnectionState.State.Unknown);
                case ConnectionState.State.Disconnected: return nameof(ConnectionState.State.Disconnected);
                case ConnectionState.State.Connecting: return nameof(ConnectionState.State.Connecting);
                case ConnectionState.State.Handshake: return nameof(ConnectionState.State.Handshake);
                case ConnectionState.State.Connected: return nameof(ConnectionState.State.Connected);
                default: return $"ConnectionState_{(int) state}";
            }
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
#if UNITY_ANDROID || UNITY_IOS
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

        private LogLevelType m_LogLevel;
        internal NativeHashMap<int, FixedString128Bytes>.ReadOnly ComponentTypeNameLookup;

#if USING_UNITY_LOGGING
        private LogLevel m_CurrentLogLevel;
        private LoggerHandle m_LoggerHandle;

        private Logger GetOrCreateLogger()
        {
            Logger logger = null;
            if (m_LoggerHandle.IsValid)
                logger = LoggerManager.GetLogger(m_LoggerHandle);

            if (logger == null)
            {
                logger = new LoggerConfig()
                    .MinimumLevel.Set(m_CurrentLogLevel)
                    .CaptureStacktrace(false)
                    .RedirectUnityLogs(false)
                    //Use correct format that is compatible with current unity logging
                    .WriteTo.UnityDebugLog(minLevel: m_CurrentLogLevel, outputTemplate: new FixedString512Bytes("{Message}"))
                    .CreateLogger();
                m_LoggerHandle = logger.Handle;
            }

            return logger;
        }
#endif
        private void SetLoggerLevel(LogLevelType newLevel)
        {
#if USING_UNITY_LOGGING
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
#endif
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
#if USING_UNITY_LOGGING
            if (!m_LoggerHandle.IsValid)
                return;
            var logger = LoggerManager.GetLogger(m_LoggerHandle);
            logger?.Dispose();

            m_LoggerHandle = default;
#endif
        }

        /// <summary>
        /// If you disable <see cref="UnityEngine.Application.runInBackground"/>, users will experience client disconnects
        /// when tabbing out of (or otherwise un-focusing) your game application.
        /// It is therefore highly recommended to enable "Run in "Background" via ticking `Project Settings... Player... Resolution and Presentation... Run In Background`.
        /// </summary>
        /// <remarks>
        /// Setting <see cref="SuppressApplicationRunInBackgroundWarning"/> to true will allow you to
        /// toggle off "Run in Background" without triggering the advice log.
        /// </remarks>
        public bool SuppressApplicationRunInBackgroundWarning { get; set; }

        /// <summary>Prevents log-spam for <see cref="SuppressApplicationRunInBackgroundWarning"/>.</summary>
        internal bool HasWarnedAboutApplicationRunInBackground { get; set; }

        /// <summary>
        ///     A NetCode RPC will trigger a warning if it hasn't been consumed or destroyed (which is a proxy for 'handled') after
        ///     this many simulation frames (inclusive).
        ///     <see cref="ReceiveRpcCommandRequest.Age" />.
        ///     Set to 0 to opt out.
        /// </summary>
        public ushort MaxRpcAgeFrames { get; set; }

        /// <summary>
        /// The current debug logging level. Default value is <see cref="LogLevelType.Notify"/>.
        /// </summary>
        [ExcludeFromBurstCompatTesting("may use managed objects")]
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
#if USING_UNITY_LOGGING
            Unity.Logging.Log.To(m_LoggerHandle).Debug(msg);
#else
            if(m_LogLevel <= LogLevelType.Debug)
                UnityEngine.Debug.Log(msg);
#endif
        }

        /// <summary>
        /// Print a log message with Notify level priority;
        /// </summary>
        /// <param name="msg">The ascii message string. Unicode are not supported</param>
        public readonly void Log(in FixedString512Bytes msg)
        {
#if USING_UNITY_LOGGING
            Unity.Logging.Log.To(m_LoggerHandle).Info(msg);
#else
            if(m_LogLevel <= LogLevelType.Notify)
                UnityEngine.Debug.Log(msg);
#endif
        }

        /// <summary>
        /// Print a log message with warning priority
        /// </summary>
        /// <param name="msg">The ascii message string. Unicode are not supported</param>
        public readonly void LogWarning(in FixedString512Bytes msg)
        {
#if USING_UNITY_LOGGING
            Unity.Logging.Log.To(m_LoggerHandle).Warning(msg);
#else
            if(m_LogLevel <= LogLevelType.Warning)
                UnityEngine.Debug.LogWarning(msg);
#endif
        }

        /// <summary>
        /// Print a log message with error priority
        /// </summary>
        /// <param name="msg">The ascii message string. Unicode are not supported</param>
        public readonly void LogError(in FixedString512Bytes msg)
        {
#if USING_UNITY_LOGGING
            Unity.Logging.Log.To(m_LoggerHandle).Error(msg);
#else
            if(m_LogLevel <= LogLevelType.Error)
                UnityEngine.Debug.LogError(msg);
#endif
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
