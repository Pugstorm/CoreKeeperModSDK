using Unity.Burst;
using Unity.Jobs;

namespace Unity.Networking.Transport
{
    internal struct NetworkInterfaceLayer<N> : INetworkLayer where N : unmanaged, INetworkInterface
    {
        internal N m_NetworkInterface;

        public NetworkInterfaceLayer(N networkInterface)
        {
            m_NetworkInterface = networkInterface;
        }

        public unsafe int Initialize(ref NetworkSettings settings, ref ConnectionList connectionList, ref int packetPadding)
        {
            var result = m_NetworkInterface.Initialize(ref settings, ref packetPadding);
            if (result != 0)
                return result;

            // This is here only to support current websocket implementation where it requires to keep a reference to the connection list.
            if (BurstRuntime.GetHashCode64<N>() == BurstRuntime.GetHashCode64<WebSocketNetworkInterface>())
            {
                fixed(void* interfacePtr = &m_NetworkInterface)
                {
                    ref var nif = ref *(WebSocketNetworkInterface*)interfacePtr;
                    connectionList = nif.CreateConnectionList();
                }
            }
#if !UNITY_WEBGL || UNITY_EDITOR
            else if (BurstRuntime.GetHashCode64<N>() == BurstRuntime.GetHashCode64<TCPNetworkInterface>())
            {
                fixed(void* interfacePtr = &m_NetworkInterface)
                {
                    ref var nif = ref *(TCPNetworkInterface*)interfacePtr;
                    connectionList = nif.CreateConnectionList();
                }
            }
#endif
            return 0;
        }

        public int Bind(ref NetworkEndpoint endpoint)
        {
            return m_NetworkInterface.Bind(endpoint);
        }

        public int Listen() => m_NetworkInterface.Listen();

        public NetworkEndpoint GetLocalEndpoint() => m_NetworkInterface.LocalEndpoint;

        public void Dispose()
        {
            m_NetworkInterface.Dispose();
        }

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dependency)
        {
            return m_NetworkInterface.ScheduleReceive(ref arguments, dependency);
        }

        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dependency)
        {
            return m_NetworkInterface.ScheduleSend(ref arguments, dependency);
        }
    }
}
