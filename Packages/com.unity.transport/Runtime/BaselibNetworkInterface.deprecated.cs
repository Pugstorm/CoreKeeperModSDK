using Unity.Jobs;
using System;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport
{
    public static class BaselibNetworkParameterExtensions
    {
        [Obsolete("To set receiveQueueCapacity and sendQueueCapacity parameters use WithNetworkConfigParameters()", false)]
        public static ref NetworkSettings WithBaselibNetworkInterfaceParameters(
            ref this NetworkSettings settings,
            int receiveQueueCapacity = 0,
            int sendQueueCapacity = 0,
            uint maximumPayloadSize = 0
        )
        {
            var parameter = new BaselibNetworkParameter
            {
                receiveQueueCapacity = receiveQueueCapacity,
                sendQueueCapacity = sendQueueCapacity,
                maximumPayloadSize = maximumPayloadSize,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }
    }

    /// <summary>Obsolete. Set the receive/send queue capacities with <see cref="NetworkConfigParameter"/> instead.</summary>
    [Obsolete("To set receiveQueueCapacity and sendQueueCapacity parameters use NetworkConfigParameter", false)]
    public struct BaselibNetworkParameter : INetworkParameter
    {
        public int receiveQueueCapacity;
        public int sendQueueCapacity;
        public uint maximumPayloadSize;

        public bool Validate()
        {
            var valid = true;

            if (receiveQueueCapacity <= 0)
            {
                valid = false;
                DebugLog.ErrorValueIsNegative("ReceiveQueueCapacity", receiveQueueCapacity);
            }
            if (sendQueueCapacity <= 0)
            {
                valid = false;
                DebugLog.ErrorValueIsNegative("SendQueueCapacity", sendQueueCapacity);
            }

            return valid;
        }
    }

    /// <summary>Obsolete. Use <see cref="UDPNetworkInterface"/> instead.</summary>
    [Obsolete("BaselibNetworkInterface has been deprecated. Use UDPNetworkInterface instead (UnityUpgradable) -> UDPNetworkInterface")]
    public struct BaselibNetworkInterface : INetworkInterface
    {
        public NetworkEndpoint LocalEndpoint
            => throw new System.NotImplementedException();

        public int Initialize(ref NetworkSettings settings, ref int packetPadding)
            => throw new System.NotImplementedException();

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dep)
            => throw new System.NotImplementedException();

        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dep)
            => throw new System.NotImplementedException();

        public int Bind(NetworkEndpoint endpoint)
            => throw new System.NotImplementedException();

        public int Listen()
            => throw new System.NotImplementedException();

        public unsafe void Dispose()
            => throw new System.NotImplementedException();
    }
}
