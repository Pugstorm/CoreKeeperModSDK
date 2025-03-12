using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport.Logging;
using Unity.Networking.Transport.Utilities;

namespace Unity.Networking.Transport
{
    internal struct LogLayer : INetworkLayer
    {
        private FixedString32Bytes m_DriverIdentifier;

        public void Dispose() {}

        public int Initialize(ref NetworkSettings settings, ref ConnectionList connectionList, ref int packetPadding)
        {
            if (settings.TryGet<LoggingParameter>(out var identifier))
                m_DriverIdentifier = identifier.DriverName;
            else
                m_DriverIdentifier = "unidentified";
            return 0;
        }

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dependency)
        {
            return new LogJob
            {
                Label = $"[{m_DriverIdentifier}] Received",
                Queue = arguments.ReceiveQueue,
            }.Schedule(dependency);
        }

        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dependency)
        {
            return new LogJob
            {
                Label = $"[{m_DriverIdentifier}] Sent",
                Queue = arguments.SendQueue,
            }.Schedule(dependency);
        }

        [BurstCompile]
        private struct LogJob : IJob
        {
            public FixedString64Bytes Label;
            public PacketsQueue Queue;

            public void Execute()
            {
                var count = Queue.Count;
                if (count == 0) return;
                
                var buffer = new UnsafeText(4096, Allocator.Temp);

                for (int i = 0; i < count; i++)
                {
                    var packetProcessor = Queue[i];

                    if (packetProcessor.Length <= 0)
                        continue;
                    
#if USE_UNITY_LOGGING
                    
                    static FormatError GetDataAsText<T>(ref T buffer, ref PacketProcessor packetProcessor) where T : unmanaged, INativeList<byte>, IUTF8Bytes
                    {
                        var length = packetProcessor.Length;
                        for (var i = 0; i < length; i++)
                        {
                            var value = packetProcessor.GetPayloadDataRef<byte>(i);
                            FixedString32Bytes temp = $"{value:x2}";
                            if (buffer.Append(temp) == FormatError.Overflow)
                                return FormatError.Overflow;
                            if (buffer.Append(' ') == FormatError.Overflow)
                                return FormatError.Overflow;
                        }
                        return FormatError.None;
                    }

                    buffer.Length = 0;
                    GetDataAsText(ref buffer, ref packetProcessor);
                    
                    Unity.Logging.Log.Info("{Label} {BytesCount} bytes [Endpoint: {Endpoint}]: {Data}", Label, packetProcessor.Length, packetProcessor.EndpointRef.ToFixedString(), buffer);
#else
                    static bool AppendPayload(ref FixedString4096Bytes str, ref PacketProcessor packetProcessor)
                    {
                        var length = packetProcessor.Length;
                        for (var i = 0; i < length; i++)
                        {
                            var value = packetProcessor.GetPayloadDataRef<byte>(i);
                            FixedString32Bytes temp = $"{value:x2}";
                            if (str.Append(temp) == FormatError.Overflow)
                                return false;
                            if (str.Append(' ') == FormatError.Overflow)
                                return false;
                        }
                        return true;
                    }

                    var str = new FixedString4096Bytes(Label);
                    str.Append(FixedString.Format(" {0} bytes [Endpoint: {1}]: ", packetProcessor.Length, packetProcessor.EndpointRef.ToFixedString()));
                    if (AppendPayload(ref str, ref packetProcessor))
                    {
                        UnityEngine.Debug.Log(str);
                    }
                    else
                    {
                        UnityEngine.Debug.Log(str);
                        UnityEngine.Debug.Log("Message truncated");
                    }
#endif
                }
            }
        }
    }
}
