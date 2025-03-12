using System;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Logging;
using UnityEngine;

namespace Unity.Networking.Transport.Relay
{
    /// <summary>Extenstions to <see cref="NetworkDriver"/> related to Unity Relay.</summary>
    public static class NetworkDriverRelayExtensions
    {
        /// <summary>Get the current status of the connection to the relay server.</summary>
        /// <param name="driver">Driver to query the status from.</param>
        /// <returns>Current relay connection status.</returns>
        public static RelayConnectionStatus GetRelayConnectionStatus(this NetworkDriver driver)
        {
            if (driver.m_NetworkStack.TryGetLayer<RelayLayer>(out var layer))
                return layer.ConnectionStatus;
            else
                return RelayConnectionStatus.NotUsingRelay;
        }

        /// <summary>Connect to the relay server without specifying an endpoint.</summary>
        /// <param name="driver">Driver to use for the connection.</param>
        /// <returns>The new connection (or default if connection failed).</returns>
        public static NetworkConnection Connect(this NetworkDriver driver)
        {
            if (driver.CurrentSettings.TryGet<RelayNetworkParameter>(out var parameters))
            {
                return driver.Connect(parameters.ServerData.Endpoint);
            }
            else
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException("Can't call Connect without an endpoint when not using the Relay.");
#else
                DebugLog.LogError("Can't call Connect without an endpoint when not using the Relay.");
                return default;
#endif
            }
        }
    }
}
