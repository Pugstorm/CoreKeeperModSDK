using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport.Relay
{
    /// <summary>Extensions for <see cref="RelayNetworkParameter"/>.</summary>
    public static class RelayParameterExtensions
    {
        /// <summary>
        /// Sets the <see cref="RelayNetworkParameter"/> in the settings.
        /// </summary>
        /// <param name="settings">Settings to modify.</param>
        /// <param name="serverData">Connection information about the relay server.</param>
        /// <param name="relayConnectionTimeMS">
        /// Frequency at which the relay server will be pinged to maintain the connection alive.
        /// Should be set to less than 10 seconds (default is 3 seconds) since that's the time
        /// after which the relay server will sever the connection if there is no activity.
        /// </param>
        /// <returns>Settings structure with modified values.</returns>
        public static ref NetworkSettings WithRelayParameters(
            ref this NetworkSettings settings,
            ref RelayServerData serverData,
            int relayConnectionTimeMS = RelayNetworkParameter.k_DefaultConnectionTimeMS
        )
        {
            var parameter = new RelayNetworkParameter
            {
                ServerData = serverData,
                RelayConnectionTimeMS = relayConnectionTimeMS,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>
        /// Gets the <see cref="RelayNetworkParameter"/> in the settings.
        /// </summary>
        /// <param name="settings">Settings to get parameters from.</param>
        /// <returns>Structure containing the relay parameters.</returns>
        public static RelayNetworkParameter GetRelayParameters(ref this NetworkSettings settings)
        {
            if (!settings.TryGet<RelayNetworkParameter>(out var parameters))
            {
                throw new System.InvalidOperationException($"Can't extract Relay parameters: {nameof(RelayNetworkParameter)} must be provided to the {nameof(NetworkSettings)}");
            }

            return parameters;
        }
    }

    /// <summary>Parameters for the Unity Relay connection.</summary>
    public struct RelayNetworkParameter : INetworkParameter
    {
        internal const int k_DefaultConnectionTimeMS = 3000;

        /// <summary>Connection information about the relay server.</summary>
        /// <value>Server data structure.</value>
        public RelayServerData ServerData;

        /// <summary>
        /// Frequency at which the relay server will be pinged to maintain the connection alive.
        /// Should be set to less than 10 seconds (default is 3 seconds) since that's the time
        /// after which the relay server will sever the connection if there is no activity.
        /// </summary>
        /// <value>Frequency in milliseconds.</value>
        public int RelayConnectionTimeMS;

        /// <inheritdoc/>
        public unsafe bool Validate()
        {
            var valid = true;

            if (ServerData.Endpoint == default)
            {
                valid = false;
                DebugLog.ErrorRelayServerDataEndpoint(ServerData.Endpoint);
            }
            if (ServerData.AllocationId == default)
            {
                valid = false;
                DebugLog.ErrorRelayServerDataAllocationId(ServerData.AllocationId);
            }
            if (RelayConnectionTimeMS < 0)
            {
                valid = false;
                DebugLog.ErrorValueIsNegative("RelayConnectionTimeMS", RelayConnectionTimeMS);
            }

            return valid;
        }
    }
}
