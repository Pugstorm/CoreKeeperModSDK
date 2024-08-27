#if USING_UNITY_LOGGING
using Unity.Logging;
using Unity.Logging.Sinks;

namespace Unity.NetCode.Tests
{
    public static class LoggingForward
    {
        /// <summary>
        /// Forwards loggings to the Unity DebugLog sink to ensure that errors in tests actually cause the tests to fail.
        /// The test framework does not by default pick up logging package errors as errors.
        /// </summary>
        public static void ForwardUnityLoggingToDebugLog()
        {
            static void AddUnityDebugLogSink(Unity.Logging.Logger logger)
            {
                // This is a bit of a hack since we can't disable a logger sink.
                logger.GetOrCreateSink<UnityDebugLogSink>(new UnityDebugLogSink.Configuration(logger.Config.WriteTo, LogFormatterText.Formatter,
                    minLevelOverride: logger.MinimalLogLevelAcrossAllSystems, outputTemplateOverride: "{Message}"));
                logger.GetSink<StdOutSinkSystem>()?.SetMinimalLogLevel(LogLevel.Fatal);
                logger.GetSink<UnityEditorConsoleSink>()?.SetMinimalLogLevel(LogLevel.Fatal);
            }

            Unity.Logging.Internal.LoggerManager.OnNewLoggerCreated(AddUnityDebugLogSink);
            Unity.Logging.Internal.LoggerManager.CallForEveryLogger(AddUnityDebugLogSink);

            // Self log enabled, so any error inside logging will cause Debug.LogError -> failed test
            Unity.Logging.Internal.Debug.SelfLog.SetMode(Unity.Logging.Internal.Debug.SelfLog.Mode.EnabledInUnityEngineDebugLogError);
        }
    }
}
#endif
