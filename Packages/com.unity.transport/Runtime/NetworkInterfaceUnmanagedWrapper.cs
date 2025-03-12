using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport.Utilities;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// An unmanaged network interface that can act as a wrapper for a managed one. Use
    /// <see cref="ManagedNetworkInterfaceExtensions.WrapToUnmanaged"/> to obtain an instance. Do
    /// not create one manually.
    /// </summary>
    public unsafe struct NetworkInterfaceUnmanagedWrapper<T> : INetworkInterface where T : INetworkInterface
    {
        private static ManagedCallWrapper s_LocalEndpoint_FPtr;
        private static ManagedCallWrapper s_Bind_FPtr;
        private static ManagedCallWrapper s_Dispose_FPtr;
        private static ManagedCallWrapper s_Initialize_FPtr;
        private static ManagedCallWrapper s_Listen_FPtr;
        private static ManagedCallWrapper s_ScheduleReceive_FPtr;
        private static ManagedCallWrapper s_ScheduleSend_FPtr;

        private static void InitializeFunctionPointers()
        {
            if (s_LocalEndpoint_FPtr.IsCreated)
                return;

            s_LocalEndpoint_FPtr = new ManagedCallWrapper(&LocalEndpointWrapper);
            s_Bind_FPtr = new ManagedCallWrapper(&BindWrapper);
            s_Dispose_FPtr = new ManagedCallWrapper(&DisposeWrapper);
            s_Initialize_FPtr = new ManagedCallWrapper(&InitializeWrapper);
            s_Listen_FPtr = new ManagedCallWrapper(&ListenWrapper);
            s_ScheduleReceive_FPtr = new ManagedCallWrapper(&ScheduleReceiveWrapper);
            s_ScheduleSend_FPtr = new ManagedCallWrapper(&ScheduleSendWrapper);
        }

        private ManagedReference<T> m_NetworkInterfaceReference;

        internal ManagedReference<T> NetworkInterfaceReference => m_NetworkInterfaceReference;

        internal NetworkInterfaceUnmanagedWrapper(ref T networkInterface)
        {
            InitializeFunctionPointers();
            m_NetworkInterfaceReference = new ManagedReference<T>(ref networkInterface);
        }

        private struct LocalEndpoint_Arguments
        {
            public ManagedReference<T> InterfaceReference;
            public NetworkEndpoint Return;
        }
        private static void LocalEndpointWrapper(void* argumentsPtr, int argumentsSize)
        {
            ref var arguments = ref ManagedCallWrapper.ArgumentsFromPtr<LocalEndpoint_Arguments>(argumentsPtr, argumentsSize);
            arguments.Return = arguments.InterfaceReference.Element.LocalEndpoint;
        }

        /// <inheritdoc/>
        public NetworkEndpoint LocalEndpoint
        {
            get
            {
                var arguments = new LocalEndpoint_Arguments
                {
                    InterfaceReference = m_NetworkInterfaceReference,
                };

                s_LocalEndpoint_FPtr.Invoke(ref arguments);

                return arguments.Return;
            }
        }


        private struct Bind_Arguments
        {
            public ManagedReference<T> InterfaceReference;
            public NetworkEndpoint Endpoint;
            public int Return;
        }
        private static void BindWrapper(void* argumentsPtr, int argumentsSize)
        {
            ref var arguments = ref ManagedCallWrapper.ArgumentsFromPtr<Bind_Arguments>(argumentsPtr, argumentsSize);
            arguments.Return = arguments.InterfaceReference.Element.Bind(arguments.Endpoint);
        }

        /// <inheritdoc/>
        public int Bind(NetworkEndpoint endpoint)
        {
            var arguments = new Bind_Arguments
            {
                InterfaceReference = m_NetworkInterfaceReference,
                Endpoint = endpoint,
            };

            s_Bind_FPtr.Invoke(ref arguments);

            return arguments.Return;
        }

        private struct Dispose_Arguments
        {
            public ManagedReference<T> InterfaceReference;
        }
        private static void DisposeWrapper(void* argumentsPtr, int argumentsSize)
        {
            ref var arguments = ref ManagedCallWrapper.ArgumentsFromPtr<Dispose_Arguments>(argumentsPtr, argumentsSize);
            arguments.InterfaceReference.Element.Dispose();
        }

        public void Dispose()
        {
            var arguments = new Dispose_Arguments
            {
                InterfaceReference = m_NetworkInterfaceReference,
            };
            s_Dispose_FPtr.Invoke(ref arguments);
            m_NetworkInterfaceReference.Dispose();
        }

        private struct Initialize_Arguments
        {
            public ManagedReference<T> InterfaceReference;
            public NetworkSettings NetworkSettings;
            public int PacketPadding;
            public int Return;
        }
        private static void InitializeWrapper(void* argumentsPtr, int argumentsSize)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (argumentsSize != UnsafeUtility.SizeOf<Initialize_Arguments>())
                throw new InvalidOperationException($"The requested argument type size does not match the provided one");
#endif
            // workaround for NetworkSettings being managed
            var arguments = UnsafeUtility.ReadArrayElement<Initialize_Arguments>(argumentsPtr, 0);
            arguments.Return = arguments.InterfaceReference.Element.Initialize(ref arguments.NetworkSettings, ref arguments.PacketPadding);
            UnsafeUtility.WriteArrayElement(argumentsPtr, 0, arguments);
        }

        /// <inheritdoc/>
        public int Initialize(ref NetworkSettings settings, ref int packetPadding)
        {
            var arguments = new Initialize_Arguments
            {
                InterfaceReference = m_NetworkInterfaceReference,
                NetworkSettings = settings,
                PacketPadding = packetPadding,
            };
            // workaround for NetworkSettings being managed
            var unmanagedArguments = stackalloc byte[UnsafeUtility.SizeOf<Initialize_Arguments>()];
            UnsafeUtility.WriteArrayElement(unmanagedArguments, 0, arguments);
            s_Initialize_FPtr.Invoke(unmanagedArguments, UnsafeUtility.SizeOf<Initialize_Arguments>());
            arguments = UnsafeUtility.ReadArrayElement<Initialize_Arguments>(unmanagedArguments, 0);

            // As they are ref arguments we need to reassign them in case they changed
            settings = arguments.NetworkSettings;
            packetPadding = arguments.PacketPadding;

            return arguments.Return;
        }

        private struct Listen_Arguments
        {
            public ManagedReference<T> InterfaceReference;
            public int Return;
        }
        private static void ListenWrapper(void* argumentsPtr, int argumentsSize)
        {
            ref var arguments = ref ManagedCallWrapper.ArgumentsFromPtr<Listen_Arguments>(argumentsPtr, argumentsSize);
            arguments.Return = arguments.InterfaceReference.Element.Listen();
        }

        /// <inheritdoc/>
        public int Listen()
        {
            var arguments = new Listen_Arguments
            {
                InterfaceReference = m_NetworkInterfaceReference,
            };
            s_Listen_FPtr.Invoke(ref arguments);
            return arguments.Return;
        }

        private struct ScheduleReceive_Arguments
        {
            public ManagedReference<T> InterfaceReference;
            public ReceiveJobArguments Arguments;
            public JobHandle Dependency;
            public JobHandle Return;
        }
        private static void ScheduleReceiveWrapper(void* argumentsPtr, int argumentsSize)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (argumentsSize != UnsafeUtility.SizeOf<ScheduleReceive_Arguments>())
                throw new InvalidOperationException($"The requested argument type size does not match the provided one");
#endif
            // workaround for NetworkPacketReceiver being managed
            var arguments = UnsafeUtility.ReadArrayElement<ScheduleReceive_Arguments>(argumentsPtr, 0);
            arguments.Return = arguments.InterfaceReference.Element.ScheduleReceive(ref arguments.Arguments, arguments.Dependency);
            UnsafeUtility.WriteArrayElement(argumentsPtr, 0, arguments);
        }

        /// <inheritdoc/>
        public JobHandle ScheduleReceive(ref ReceiveJobArguments receiveJobArguments, JobHandle dep)
        {
            var arguments = new ScheduleReceive_Arguments
            {
                InterfaceReference = m_NetworkInterfaceReference,
                Arguments = receiveJobArguments,
                Dependency = dep,
            };
            // workaround for NetworkSettings being managed
            var unmanagedArguments = stackalloc byte[UnsafeUtility.SizeOf<ScheduleReceive_Arguments>()];
            UnsafeUtility.WriteArrayElement(unmanagedArguments, 0, arguments);
            s_ScheduleReceive_FPtr.Invoke(unmanagedArguments, UnsafeUtility.SizeOf<ScheduleReceive_Arguments>());
            arguments = UnsafeUtility.ReadArrayElement<ScheduleReceive_Arguments>(unmanagedArguments, 0);
            return arguments.Return;
        }

        private struct ScheduleSend_Arguments
        {
            public ManagedReference<T> InterfaceReference;
            public SendJobArguments Arguments;
            public JobHandle Dependency;
            public JobHandle Return;
        }
        private static void ScheduleSendWrapper(void* argumentsPtr, int argumentsSize)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (argumentsSize != UnsafeUtility.SizeOf<ScheduleSend_Arguments>())
                throw new InvalidOperationException($"The requested argument type size does not match the provided one");
#endif
            // workaround for NetworkPacketReceiver being managed
            var arguments = UnsafeUtility.ReadArrayElement<ScheduleSend_Arguments>(argumentsPtr, 0);
            arguments.Return = arguments.InterfaceReference.Element.ScheduleSend(ref arguments.Arguments, arguments.Dependency);
            UnsafeUtility.WriteArrayElement(argumentsPtr, 0, arguments);
        }

        /// <inheritdoc/>
        public JobHandle ScheduleSend(ref SendJobArguments sendJobArguments, JobHandle dep)
        {
            var arguments = new ScheduleSend_Arguments
            {
                InterfaceReference = m_NetworkInterfaceReference,
                Arguments = sendJobArguments,
                Dependency = dep,
            };
            // workaround for NetworkSettings being managed
            var unmanagedArguments = stackalloc byte[UnsafeUtility.SizeOf<ScheduleSend_Arguments>()];
            UnsafeUtility.WriteArrayElement(unmanagedArguments, 0, arguments);
            s_ScheduleSend_FPtr.Invoke(unmanagedArguments, UnsafeUtility.SizeOf<ScheduleSend_Arguments>());
            arguments = UnsafeUtility.ReadArrayElement<ScheduleSend_Arguments>(unmanagedArguments, 0);
            return arguments.Return;
        }
    }

    /// <summary>Extension methods to work with a managed <see cref="INetworkInterface"/>.</summary>
    public static class ManagedNetworkInterfaceExtensions
    {
        /// <summary>
        /// Creates an unmanaged wrapper for a managed <see cref="INetworkInterface"/>. Network
        /// interface are required to be unmanaged (e.g. Burst-compatible), but there are cases
        /// where this is impractical. This method allows creating an unmanaged version of a
        /// managed network interface, at the cost of a slight performance overhead.
        /// </summary>
        /// <typeparam name="T">The type of the managed network interface.</typeparam>
        /// <param name="networkInterface">Interface instance to wrap.</param>
        /// <returns>Unmanaged wrapper instance for the network interface.</returns>
        /// <exception cref="InvalidOperationException">
        /// If the type network interface is already an unmanaged type.
        /// </exception>
        public static NetworkInterfaceUnmanagedWrapper<T> WrapToUnmanaged<T>(this T networkInterface) where T : INetworkInterface
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (networkInterface == null)
                throw new ArgumentNullException(nameof(networkInterface));

            if (!typeof(T).IsValueType)
                throw new InvalidOperationException($"Non struct NetworkInterfaces ({typeof(T)}) are not supported yet due to a bug on windows (#1412477).");

            if (UnsafeUtility.IsUnmanaged<T>())
                throw new InvalidOperationException($"The network interface type {typeof(T).Name} is already an unmanaged type.");
#endif
            return new NetworkInterfaceUnmanagedWrapper<T>(ref networkInterface);
        }
    }
}
