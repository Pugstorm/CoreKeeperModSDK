using Unity.Collections;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport.Utilities
{
    /// <summary>Extensions for <see cref="FragmentationUtility.Parameters"/>.</summary>
    public static class FragmentationStageParameterExtensions
    {
        /// <summary>
        /// Sets the <see cref="FragmentationUtility.Parameters"/> in the settings.
        /// </summary>
        /// <param name="settings">Settings to modify.</param>
        /// <param name="payloadCapacity">
        /// Maximum size that can be fragmented by the <see cref="FragmentationPipelineStage"/>.
        /// Attempting to send a message larger than that will result in the send operation
        /// returning <see cref="Error.StatusCode.NetworkPacketOverflow"/>. Maximum value is
        /// ~20MB for unreliable packets, and ~88KB for reliable ones.
        /// </param>
        /// <returns>Settings structure with modified values.</returns>
        public static ref NetworkSettings WithFragmentationStageParameters(
            ref this NetworkSettings settings,
            int payloadCapacity = FragmentationUtility.Parameters.k_DefaultPayloadCapacity
        )
        {
            var parameter = new FragmentationUtility.Parameters
            {
                PayloadCapacity = payloadCapacity,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>
        /// Gets the <see cref="FragmentationUtility.Parameters"/> in the settings.
        /// </summary>
        /// <param name="settings">Settings to get parameters from.</param>
        /// <returns>Structure containing the fragmentation parameters.</returns>
        public static FragmentationUtility.Parameters GetFragmentationStageParameters(ref this NetworkSettings settings)
        {
            if (!settings.TryGet<FragmentationUtility.Parameters>(out var parameters))
            {
                parameters.PayloadCapacity = FragmentationUtility.Parameters.k_DefaultPayloadCapacity;
            }

            return parameters;
        }
    }

    /// <summary>Utility types for the <see cref="FragmentationPipelineStage"/>.</summary>
    public struct FragmentationUtility
    {
        /// <summary>Parameters for the <see cref="FragmentationPipelineStage"/>.</summary>
        public struct Parameters : INetworkParameter
        {
            internal const int k_DefaultPayloadCapacity = 4 * 1024;
            internal const int k_MaxPayloadCapacity = NetworkParameterConstants.AbsoluteMaxMessageSize * (0xFFFF >> 2);

            /// <summary>
            /// Maximum size that can be fragmented by the <see cref="FragmentationPipelineStage"/>.
            /// Attempting to send a message larger than that will result in the send operation
            /// returning <see cref="Error.StatusCode.NetworkPacketOverflow"/>. Maximum value is
            /// ~20MB for unreliable packets, and ~88KB for reliable ones.
            /// </summary>
            /// <value>Maximum payload capacity.</value>
            public int PayloadCapacity;

            /// <inheritdoc/>
            public bool Validate()
            {
                var valid = true;

                if (PayloadCapacity <= 0)
                {
                    valid = false;
                    DebugLog.ErrorValueIsZeroOrNegative("PayloadCapacity", PayloadCapacity);
                }

                if (PayloadCapacity >= k_MaxPayloadCapacity)
                {
                    valid = false;
                    DebugLog.ErrorFragmentationMaxPayloadTooLarge(PayloadCapacity, k_MaxPayloadCapacity);
                }

                return valid;
            }
        }
    }
}
