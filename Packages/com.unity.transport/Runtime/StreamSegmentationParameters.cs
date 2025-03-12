using System;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// Settings used as a tag to indicate the network stack should include a StreamSegmentationLayer.
    /// </summary>
    internal struct StreamSegmentationParameter : INetworkParameter
    {
        public bool Validate() => true;
    }

    internal static class StreamSegmentationParameterExtensions
    {
        public static ref NetworkSettings WithStreamSegmentationParameters(
            ref this NetworkSettings    settings)
        {
            var parameter = new StreamSegmentationParameter();
            settings.AddRawParameterStruct(ref parameter);
            return ref settings;
        }
    }
}
