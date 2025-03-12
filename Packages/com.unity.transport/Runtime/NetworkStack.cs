using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport.Logging;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.TLS;
using BurstRuntime = Unity.Burst.BurstRuntime;

namespace Unity.Networking.Transport
{
    internal unsafe struct NetworkStack : IDisposable
    {
        private struct NetworkInterfaceFunctions
        {
            private ManagedCallWrapper m_NetworkInterface_Bind_FPtr;
            private ManagedCallWrapper m_NetworkInterface_Listen_FPtr;
            private ManagedCallWrapper m_NetworkInterface_GetLocalEndpoint_FPtr;

            internal static NetworkInterfaceFunctions Create<N>() where N : unmanaged, INetworkInterface
            {
                var functions = new NetworkInterfaceFunctions();
                functions.m_NetworkInterface_Bind_FPtr = new ManagedCallWrapper(&BindWrapper<N>);
                functions.m_NetworkInterface_Listen_FPtr = new ManagedCallWrapper(&ListenWrapper<N>);
                functions.m_NetworkInterface_GetLocalEndpoint_FPtr = new ManagedCallWrapper(&GetLocalEndpointWrapper<N>);
                return functions;
            }

            internal int Bind(ref NetworkStack stack, ref NetworkEndpoint endpoint)
            {
                var arguments = new BindArguments
                {
                    Stack = stack,
                    Endpoint = endpoint,
                };
                m_NetworkInterface_Bind_FPtr.Invoke(ref arguments);
                return arguments.Return;
            }

            internal int Listen(ref NetworkStack stack)
            {
                var arguments = new ListenArguments
                {
                    Stack = stack,
                };
                m_NetworkInterface_Listen_FPtr.Invoke(ref arguments);
                return arguments.Return;
            }

            internal NetworkEndpoint GetLocalEndpoint(ref NetworkStack stack)
            {
                var arguments = new GetLocalEndpointArguments
                {
                    Stack = stack,
                };
                m_NetworkInterface_GetLocalEndpoint_FPtr.Invoke(ref arguments);
                return arguments.Return;
            }

            private struct BindArguments
            {
                public NetworkStack Stack;
                public NetworkEndpoint Endpoint;
                public int Return;
            }
            static private void BindWrapper<N>(void* argumentsPtr, int size) where N : unmanaged, INetworkInterface
            {
                ref var arguments = ref ManagedCallWrapper.ArgumentsFromPtr<BindArguments>(argumentsPtr, size);

                if (arguments.Stack.TryGetLayer<NetworkInterfaceLayer<N>>(out var layer))
                {
                    arguments.Return = layer.Bind(ref arguments.Endpoint);
                    return;
                }

                arguments.Return = -1;
            }

            private struct GetLocalEndpointArguments
            {
                public NetworkStack Stack;
                public NetworkEndpoint Return;
            }
            static private void GetLocalEndpointWrapper<N>(void* argumentsPtr, int size) where N : unmanaged, INetworkInterface
            {
                ref var arguments = ref ManagedCallWrapper.ArgumentsFromPtr<GetLocalEndpointArguments>(argumentsPtr, size);

                if (arguments.Stack.TryGetLayer<NetworkInterfaceLayer<N>>(out var layer))
                {
                    arguments.Return = layer.GetLocalEndpoint();
                    return;
                }

                arguments.Return = default;
            }

            private struct ListenArguments
            {
                public NetworkStack Stack;
                public int Return;
            }
            static private void ListenWrapper<N>(void* argumentsPtr, int size) where N : unmanaged, INetworkInterface
            {
                ref var arguments = ref ManagedCallWrapper.ArgumentsFromPtr<ListenArguments>(argumentsPtr, size);

                if (arguments.Stack.TryGetLayer<NetworkInterfaceLayer<N>>(out var layer))
                {
                    arguments.Return = layer.Listen();
                    return;
                }
            }
        }

        // TODO: disabling the safety check here for now, but we need to remove it asap.
        // As NetworkStack is in NetworkDriver and the driver is passed to multiple
        // jobs being schedulled at the same time we schedule stack update, we
        // are not supposed to write here. We know that for now we don't read/write
        // this in any other place, so it's fine, but moving pipelines to a layer should
        // allow us to fix it.
        [NativeDisableContainerSafetyRestriction]
        private NativeList<NetworkLayerWrapper> m_Layers;
        [NativeDisableContainerSafetyRestriction]
        private NativeList<int> m_AccumulatedPacketPadding;

        private int m_TotalPacketPadding;
        private ConnectionList m_Connections;

        private NetworkInterfaceFunctions m_NetworkInterfaceFunctions;


        // TODO: for now we add an extra byte for pipeline id, should move to its own layer
        internal int PacketPadding => m_TotalPacketPadding + 1;

        internal ConnectionList Connections => m_Connections;

        internal static void Initialize(out NetworkStack stack)
        {
            stack = default;
            stack.m_Layers = new NativeList<NetworkLayerWrapper>(0, Allocator.Persistent);
            stack.m_AccumulatedPacketPadding = new NativeList<int>(0, Allocator.Persistent);
        }

        internal static void InitializeForSettings<N>
            (out NetworkStack stack, ref N networkInterface, ref NetworkSettings networkSettings,
            out PacketsQueue sendQueue, out PacketsQueue receiveQueue) where N : unmanaged, INetworkInterface
        {
            Initialize(out stack);

            stack.m_NetworkInterfaceFunctions = NetworkInterfaceFunctions.Create<N>();

            stack.AddLayer(new BottomLayer(), ref networkSettings);
            stack.AddLayer(new NetworkInterfaceLayer<N>(networkInterface), ref networkSettings);

            // We get again the interface as ref so the CreateQueues method is applied to the actual layer interface copy
            ref var interfaceRef = ref stack.m_Layers.ElementAt(stack.m_Layers.Length - 1).CastRef<NetworkInterfaceLayer<N>>().m_NetworkInterface;
            CreateQueues(ref interfaceRef, ref networkSettings, out sendQueue, out receiveQueue);
            // assign it so the new values are copied back
            networkInterface = interfaceRef;

            // stack.AddLayer(new LogLayer(), ref networkSettings); // This will print packets for debugging

            var isRelay = networkSettings.TryGet<RelayNetworkParameter>(out _);

            var isSecure = isRelay
                ? networkSettings.GetRelayParameters().ServerData.IsSecure == 1
                : networkSettings.TryGet<TLS.SecureNetworkProtocolParameter>(out _);

#if !UNITY_WEBGL || UNITY_EDITOR
            if (networkInterface is TCPNetworkInterface || networkInterface is WebSocketNetworkInterface)
            {
                // Uncomment to induce message splitting for debugging.
                if (networkSettings.TryGet<StreamSegmentationParameter>(out _))
                    stack.AddLayer(new StreamSegmentationLayer(), ref networkSettings);

                // If using the TCP interface or WebSocket interface (on non-WebGL platforms), we need
                // to add the TLS layer before the simulator layer, since it expects a reliable stream.
                if (isSecure)
                    stack.AddLayer(new TLSLayer(), ref networkSettings);

                // TCP interface requires a layer to manage the stream to datagram transition.
                if (networkInterface is TCPNetworkInterface)
                    stack.AddLayer(new StreamToDatagramLayer(), ref networkSettings);

                // On non-WebGL platforms, add the WebSocket layer if using WebSocket interface.
                if (networkInterface is WebSocketNetworkInterface)
                    stack.AddLayer(new WebSocketLayer(), ref networkSettings);
            }
#endif // !UNITY_WEBGL || UNITY_EDITOR

            // Now we can add the simulator layer, which should be as low in the stack as possible.
            if (networkSettings.TryGet<NetworkSimulatorParameter>(out _))
                stack.AddLayer(new SimulatorLayer(), ref networkSettings);

            // Determine if we need to add a DTLS layer or not. These layers can only be added on
            // non-WebGL platforms that support UnityTLS. Hence the complicated #if condition.
#if !UNITY_WEBGL || UNITY_EDITOR
            if (isSecure && !(networkInterface is TCPNetworkInterface || networkInterface is WebSocketNetworkInterface))
                stack.AddLayer(new DTLSLayer(), ref networkSettings);
#endif

            if (isRelay)
            {
                if (networkInterface is IPCNetworkInterface)
                    throw new InvalidOperationException("Relay cannot be used with the IPC interface");

                stack.AddLayer(new RelayLayer(), ref networkSettings);
            }

            stack.AddLayer(new SimpleConnectionLayer(), ref networkSettings);
            stack.AddLayer(new TopLayer(), ref networkSettings);
        }

        public void Dispose()
        {
            var layersCount = m_Layers.Length;
            for (int i = 0; i < layersCount; i++)
            {
                m_Layers.ElementAt(i).Dispose();
            }

            m_Layers.Dispose();
            m_AccumulatedPacketPadding.Dispose();
        }

        internal void AddLayer<T>(T layer, ref NetworkSettings settings) where T : unmanaged, INetworkLayer
            => AddLayer(ref layer, ref settings);

        internal void AddLayer<T>(ref T layer, ref NetworkSettings settings) where T : unmanaged, INetworkLayer
        {
            var oldConnections = m_Connections;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var oldPadding = m_TotalPacketPadding;
            var result = layer.Initialize(ref settings, ref m_Connections, ref m_TotalPacketPadding);
            if (m_TotalPacketPadding < oldPadding)
            {
                throw new InvalidOperationException($"Layer {typeof(T).ToString()} has decreased total padding. Negative packet paddings are invalid.");
            }
#else
            var result = layer.Initialize(ref settings, ref m_Connections, ref m_TotalPacketPadding);
#endif

            if (result != 0)
                DebugLog.ErrorStackInitFailure(typeof(T).ToString(), result);

            m_Layers.Add(NetworkLayerWrapper.Create(ref layer));
            m_AccumulatedPacketPadding.Add(m_TotalPacketPadding);

            // If a new connection list has been created we notify the BottomLayer so it will do the
            // proper cleanup on every udpate.
            if (m_Connections.IsCreated && oldConnections != m_Connections)
                m_Layers.ElementAt(0).CastRef<BottomLayer>().AddConnectionList(ref m_Connections);
        }

        internal bool TryGetLayer<T>(out T layer) where T : unmanaged, INetworkLayer
        {
            foreach (var layerWrapper in m_Layers)
            {
                if (layerWrapper.IsType<T>())
                {
                    layer = layerWrapper.CastRef<T>();
                    return true;
                }
            }
            layer = default;
            return false;
        }

        internal static unsafe void CreateQueues<N>(ref N networkInterface, ref NetworkSettings settings, out PacketsQueue sendQueue, out PacketsQueue receiveQueue)
            where N : unmanaged, INetworkInterface
        {
            var networkConfig = settings.GetNetworkConfigParameters();
            var sendQueueCapacity = networkConfig.sendQueueCapacity;
            var receiveQueueCapacity = networkConfig.receiveQueueCapacity;
            var payloadSize = networkConfig.maxMessageSize;

#if !UNITY_WEBGL || UNITY_EDITOR
            if (BurstRuntime.GetHashCode64<N>() == BurstRuntime.GetHashCode64<UDPNetworkInterface>())
            {
                fixed(void* interfacePtr = &networkInterface)
                {
                    ref var udpInterface = ref *(UDPNetworkInterface*)interfacePtr;
                    udpInterface.CreateQueues(sendQueueCapacity, receiveQueueCapacity, payloadSize, out sendQueue, out receiveQueue);
                }
            }
            else
#endif
            {
                receiveQueue = new PacketsQueue(receiveQueueCapacity, payloadSize);
                sendQueue = new PacketsQueue(sendQueueCapacity, payloadSize);
            }

            if (sendQueue.Capacity != networkConfig.sendQueueCapacity)
            {
                sendQueue.Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException(string.Format(
                    "The provided buffers count ({0}) must be equal to the sendQueueCapacity ({1})",
                    sendQueue.Capacity,
                    networkConfig.sendQueueCapacity));
#else
                DebugLog.ErrorStackSendCreateWrongBufferCount(sendQueue.Capacity, networkConfig.sendQueueCapacity);
#endif
            }

            if (receiveQueue.Capacity != networkConfig.receiveQueueCapacity)
            {
                receiveQueue.Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException(string.Format(
                    "The provided buffers count ({0}) must be equal to the receiveQueueCapacity ({1})",
                    receiveQueue.Capacity,
                    networkConfig.receiveQueueCapacity));
#else
                DebugLog.ErrorStackReceiveCreateWrongBufferCount(sendQueue.Capacity, networkConfig.sendQueueCapacity);
#endif
            }
        }

        internal int Bind(ref NetworkEndpoint endpoint) => m_NetworkInterfaceFunctions.Bind(ref this, ref endpoint);

        internal int Listen() => m_NetworkInterfaceFunctions.Listen(ref this);

        internal NetworkEndpoint GetLocalEndpoint() => m_NetworkInterfaceFunctions.GetLocalEndpoint(ref this);

        internal JobHandle ScheduleReceive(ref NetworkDriverReceiver driverReceiver, ref ConnectionList connectionList,
            ref NetworkEventQueue eventQueue, ref NetworkPipelineProcessor pipelineProcessor, long time, JobHandle dependency)
        {
            var jobArguments = new ReceiveJobArguments
            {
                ReceiveQueue = driverReceiver.ReceiveQueue,
                DriverReceiver = driverReceiver,
                ReceiveResult = driverReceiver.Result,
                EventQueue = eventQueue,
                PipelineProcessor = pipelineProcessor,
                Time = time,
            };

            var length = m_Layers.Length;
            for (var i = 0; i < length; ++i)
                dependency = m_Layers.ElementAt(i).ScheduleReceive(ref jobArguments, dependency);

            return dependency;
        }

        internal JobHandle ScheduleSend(ref NetworkDriverSender driverSender, long time, JobHandle dependency)
        {
            var jobArguments = new SendJobArguments
            {
                SendQueue = driverSender.SendQueue,
                Time = time,
            };

            dependency = driverSender.FlushPackets(dependency);

            for (var i = m_Layers.Length - 1; i >= 0; --i)
            {
                jobArguments.SendQueue.SetDefaultDataOffset(m_AccumulatedPacketPadding[i]);
                dependency = m_Layers.ElementAt(i).ScheduleSend(ref jobArguments, dependency);
            }

            return dependency;
        }
    }
}
