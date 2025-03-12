using Unity.Collections;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport.Logging
{
    /// <summary>Parameters related to how UTP logs messages. Currently unused.</summary>
    public struct LoggingParameter : INetworkParameter
    {
        /// <summary>Label to use for this driver in the logs.</summary>
        /// <value>Label as a fixed-length string.</value>
        public FixedString32Bytes DriverName;

        /// <inheritdoc/>
        public bool Validate()
        {
            if (DriverName.IsEmpty)
            {
                DebugLog.LogError("The driver name must not be empty.");
                return false;
            }

            return true;
        }
    }

    /// <summary>Extension methods related to <see cref="LoggingParameter"/>.</summary>
    public static class LoggingParameterExtensions
    {
        /// <summary>
        /// Sets the <see cref="FragmentationUtility.Parameters"/> in the settings.
        /// </summary>
        /// <param name="settings">Settings to modify.</param>
        /// <param name="driverName">Label to use for this driver in the logs.</param>
        /// <returns>Settings structure with modified values.</returns>
        public static ref NetworkSettings WithLoggingParameters(ref this NetworkSettings settings, FixedString32Bytes driverName)
        {
            var parameter = new LoggingParameter { DriverName = driverName };
            settings.AddRawParameterStruct(ref parameter);
            return ref settings;
        }
    }
}