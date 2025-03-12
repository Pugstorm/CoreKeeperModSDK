using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Networking.Transport.Utilities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Burst;

namespace Unity.Networking.Transport
{
    internal struct IPCManager
    {
        public static IPCManager Instance = new IPCManager();

        [StructLayout(LayoutKind.Explicit)]
        internal unsafe struct IPCData
        {
            [FieldOffset(0)] public ushort fromPort;
            [FieldOffset(2)] public int length;
            [FieldOffset(6)] public fixed byte data[NetworkParameterConstants.AbsoluteMaxMessageSize];
        }

        private NativeMultiQueue<IPCData> m_IPCQueue;
        private NativeParallelHashMap<ushort, int> m_IPCChannels;

        internal static JobHandle ManagerAccessHandle;

        public bool IsCreated => m_IPCQueue.IsCreated;

        private int m_RefCount;

        public void AddRef()
        {
            if (m_RefCount == 0)
            {
                m_IPCQueue = new NativeMultiQueue<IPCData>(128);
                m_IPCChannels = new NativeParallelHashMap<ushort, int>(64, Allocator.Persistent);
            }
            ++m_RefCount;
        }

        public void Release()
        {
            --m_RefCount;
            if (m_RefCount == 0)
            {
                CompleteManagerAccess();
                m_IPCQueue.Dispose();
                m_IPCChannels.Dispose();
            }
        }

        internal unsafe void Update(NetworkEndpoint local, ref PacketsQueue sendQueue)
        {
            for (int i = 0; i < sendQueue.Count; i++)
            {
                var packetProcessor = sendQueue[i];

                if (packetProcessor.Length == 0)
                    continue;

                if (!GetChannelByEndpoint(ref packetProcessor.EndpointRef, out var toChannel))
                {
                    if (packetProcessor.EndpointRef.Port == 0)
                        continue;

                    var newEndpoint = CreateEndpoint(packetProcessor.EndpointRef.Port);
                    GetChannelByEndpoint(ref newEndpoint, out toChannel);
                }

                var ipcData = new IPCData();
                packetProcessor.CopyPayload(ipcData.data, NetworkParameterConstants.AbsoluteMaxMessageSize);
                ipcData.length = packetProcessor.Length;
                ipcData.fromPort = local.Port;

                m_IPCQueue.Enqueue(toChannel, ipcData);
            }
        }

        [BurstDiscard]
        private void CompleteManagerAccess()
        {
            if (JobsUtility.IsExecutingJob)
                return;

            ManagerAccessHandle.Complete();
        }

        public unsafe NetworkEndpoint CreateEndpoint(ushort port)
        {
            CompleteManagerAccess();
            int id = 0;
            if (port == 0)
            {
                while (id == 0)
                {
                    port = RandomHelpers.GetRandomUShort();
                    if (!m_IPCChannels.TryGetValue(port, out _))
                    {
                        id = m_IPCChannels.Count() + 1;
                        m_IPCChannels.TryAdd(port, id);
                    }
                }
            }
            else
            {
                if (!m_IPCChannels.TryGetValue(port, out id))
                {
                    id = m_IPCChannels.Count() + 1;
                    m_IPCChannels.TryAdd(port, id);
                }
            }

            var endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(port);

            return endpoint;
        }

        public unsafe bool GetChannelByEndpoint(ref NetworkEndpoint endpoint, out int channel)
        {
            if (!endpoint.IsLoopback)
            {
                channel = -1;
                return false;
            }

            return m_IPCChannels.TryGetValue(endpoint.Port, out channel);
        }

        public unsafe int PeekNext(NetworkEndpoint local, void* slice, out int length, out NetworkEndpoint from)
        {
            CompleteManagerAccess();
            IPCData data;
            from = default;
            length = 0;

            if (!GetChannelByEndpoint(ref local, out var localChannel))
                return 0;

            if (m_IPCQueue.Peek(localChannel, out data))
            {
                UnsafeUtility.MemCpy(slice, data.data, data.length);

                length = data.length;
            }

            from = NetworkEndpoint.LoopbackIpv4.WithPort(data.fromPort);

            return length;
        }

        public bool HasDataAvailable(NetworkEndpoint localEndpoint)
        {
            CompleteManagerAccess();

            if (!GetChannelByEndpoint(ref localEndpoint, out var localChannel))
                return false;

            return m_IPCQueue.Peek(localChannel, out _);
        }

        public unsafe int ReceiveMessageEx(NetworkEndpoint local, void* payloadData, int payloadLen, ref NetworkEndpoint remote)
        {
            if (!GetChannelByEndpoint(ref local, out var localChannel))
                return 0;

            IPCData data;

            if (!m_IPCQueue.Dequeue(localChannel, out data))
            {
                return 0;
            }

            remote = NetworkEndpoint.LoopbackIpv4.WithPort(data.fromPort);

            var totalLength = Math.Min(payloadLen, data.length);
            UnsafeUtility.MemCpy(payloadData, data.data, totalLength);

            if (totalLength < data.length)
                return -10040; // out of memory

            return totalLength;
        }
    }
}
