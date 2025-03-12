using System;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport.Protocols;
using Unity.Networking.Transport.Utilities;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// <para>
    /// The IPC network interface implements the functionality of a network interface over an
    /// in-memory buffer. Operations will be instantaneous, but can only be used to communicate with
    /// other <see cref="NetworkDriver"/> instances inside the same process (so IPC really means
    /// intra-process and not inter-process here). Useful for testing, or to implement a single
    /// player mode in a multiplayer game.
    /// </para>
    /// <para>
    /// Note that the interface expects loopback addresses when binding/connecting. It is
    /// recommended to only use <see cref="NetworkEndpoint.LoopbackIpv4"/> when dealing with the IPC
    /// network interface, and to use different ports for different drivers (see example).
    /// </para>
    /// </summary>
    /// <example>
    /// This example code establishes an in-process communication channel between two drivers:
    /// <code>
    ///     var driver1 = NetworkDriver.Create(new IPCNetworkInterface());
    ///     driver1.Bind(NetworkEndpoint.LoopbackIpv4.WithPort(1));
    ///     driver1.Listen();
    ///
    ///     var driver2 = NetworkDriver.Create(new IPCNetworkInterface());
    ///     driver2.Bind(NetworkEndpoint.LoopbackIpv4.WithPort(2));
    ///
    ///     var connection2to1 = driver2.Connect(NetworkEndpoint.LoopbackIpv4.WithPort(1));
    ///
    ///     // Need to schedule updates for driver2 to send the connection request, and for
    ///     // driver1 to then process it. Since this all happens in-memory, one update is
    ///     // sufficient to accomplish this (no network latency involved).
    ///     driver2.ScheduleUpdate().Complete();
    ///     driver1.ScheduleUpdate().Complete();
    ///
    ///     var connection1to2 = driver1.Accept();
    /// </code>
    /// </example>
    [BurstCompile]
    public struct IPCNetworkInterface : INetworkInterface
    {
        [ReadOnly] private NativeArray<NetworkEndpoint> m_LocalEndpoint;

        /// <inheritdoc/>
        public NetworkEndpoint LocalEndpoint => m_LocalEndpoint[0];

        /// <inheritdoc/>
        public int Initialize(ref NetworkSettings settings, ref int packetPadding)
        {
            IPCManager.Instance.AddRef();
            m_LocalEndpoint = new NativeArray<NetworkEndpoint>(1, Allocator.Persistent);
            return 0;
        }

        public void Dispose()
        {
            m_LocalEndpoint.Dispose();
            IPCManager.Instance.Release();
        }

        [BurstCompile]
        struct SendUpdate : IJob
        {
            public IPCManager ipcManager;
            public PacketsQueue SendQueue;
            public NetworkEndpoint localEndPoint;

            public void Execute()
            {
                ipcManager.Update(localEndPoint, ref SendQueue);
            }
        }

        [BurstCompile]
        struct ReceiveJob : IJob
        {
            public PacketsQueue ReceiveQueue;
            public OperationResult ReceiveResult;
            public IPCManager ipcManager;
            public NetworkEndpoint localEndPoint;

            public unsafe void Execute()
            {
                while (ipcManager.HasDataAvailable(localEndPoint))
                {
                    if (!ReceiveQueue.EnqueuePacket(out var packetProcessor))
                    {
                        ReceiveResult.ErrorCode = (int)Error.StatusCode.NetworkReceiveQueueFull;
                        return;
                    }

                    var ptr = packetProcessor.GetUnsafePayloadPtr();
                    var endpoint = default(NetworkEndpoint);
                    var result = NativeReceive(ptr, packetProcessor.Capacity, ref endpoint);

                    packetProcessor.EndpointRef = endpoint;

                    if (result <= 0)
                    {
                        if (result != 0)
                            ReceiveResult.ErrorCode = -result;
                        return;
                    }

                    packetProcessor.SetUnsafeMetadata(result);
                }
            }

            unsafe int NativeReceive(void* data, int length, ref NetworkEndpoint address)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (length <= 0)
                    throw new ArgumentException("Can't receive into 0 bytes or less of buffer memory");
#endif
                return ipcManager.ReceiveMessageEx(localEndPoint, data, length, ref address);
            }
        }

        /// <inheritdoc/>
        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dep)
        {
            var job = new ReceiveJob
            {
                ReceiveQueue = arguments.ReceiveQueue,
                ipcManager = IPCManager.Instance,
                localEndPoint = m_LocalEndpoint[0],
                ReceiveResult = arguments.ReceiveResult,
            };
            dep = job.Schedule(JobHandle.CombineDependencies(dep, IPCManager.ManagerAccessHandle));
            IPCManager.ManagerAccessHandle = dep;
            return dep;
        }

        /// <inheritdoc/>
        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dep)
        {
            var sendJob = new SendUpdate {ipcManager = IPCManager.Instance, SendQueue = arguments.SendQueue, localEndPoint = m_LocalEndpoint[0]};
            dep = sendJob.Schedule(JobHandle.CombineDependencies(dep, IPCManager.ManagerAccessHandle));
            IPCManager.ManagerAccessHandle = dep;
            return dep;
        }

        /// <inheritdoc/>
        public unsafe int Bind(NetworkEndpoint endpoint)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!endpoint.IsLoopback && !endpoint.IsAny)
                throw new InvalidOperationException($"Trying to bind IPC interface to a non-loopback endpoint ({endpoint})");
#endif

            m_LocalEndpoint[0] = IPCManager.Instance.CreateEndpoint(endpoint.Port);
            return 0;
        }

        /// <inheritdoc/>
        public int Listen()
        {
            return 0;
        }
    }
}
