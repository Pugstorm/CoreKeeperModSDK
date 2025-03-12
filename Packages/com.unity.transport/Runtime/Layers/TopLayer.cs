using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport
{
    // TODO: Leaving a TopLayer here for completeness, but it's doing nothing for now.
    // This will allow us to add jobs to the last stage of receiving packets or the first
    // stage of sending packets.
    internal struct TopLayer : INetworkLayer
    {
        private ConnectionList connections;

        public void Dispose() {}

        public int Initialize(ref NetworkSettings settings, ref ConnectionList connectionList, ref int packetPadding)
        {
            connections = connectionList;
            return 0;
        }

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dependency)
        {
            return new CompleteReceiveJob
            {
                Connections = connections,
                PipelineProcessor = arguments.PipelineProcessor,
                Receiver = arguments.DriverReceiver,
                EventQueue = arguments.EventQueue,
            }.Schedule(dependency);
        }

        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dependency) => dependency;

        [BurstCompile]
        private struct CompleteReceiveJob : IJob
        {
            public ConnectionList Connections;
            public NetworkPipelineProcessor PipelineProcessor;
            public NetworkDriverReceiver Receiver;
            public NetworkEventQueue EventQueue;

            public void Execute()
            {
                GenerateConnectionEvents();

                // TODO: Connection initialization for pipelines, move this to the pipelines layer when ready
                var incomingConnections = Connections.QueryIncomingConnections(Allocator.Temp);
                for (int i = 0; i < incomingConnections.Length; i++)
                {
                    var connection = incomingConnections[i];
                    PipelineProcessor.InitializeConnection(new NetworkConnection(connection));
                }

                var count = Receiver.ReceiveQueue.Count;
                for (int i = 0; i < count; i++)
                {
                    var packetProcessor = Receiver.ReceiveQueue[i];

                    if (packetProcessor.Length <= 0)
                        continue;

                    AppendToStream(ref packetProcessor);
                }

                if (Receiver.ReceiveQueue.Count == Receiver.ReceiveQueue.Capacity)
                    DebugLog.ReceiveQueueIsFull(Receiver.ReceiveQueue.Capacity);

                Receiver.ReceiveQueue.Clear();

                GenerateDisconnectionEvents();
            }

            private unsafe void AppendToStream(ref PacketProcessor packetProcessor)
            {
                if (packetProcessor.ConnectionRef == default)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    DebugLog.LogError("Received a data event for a null connection. Ignoring.");
#endif
                    return;
                }

                var pipelineId = packetProcessor.RemoveFromPayloadStart<byte>();

                if (pipelineId > 0)
                {
                    var connection = new NetworkConnection(packetProcessor.ConnectionRef);
                    var packetPtr = (byte*)packetProcessor.GetUnsafePayloadPtr() + packetProcessor.Offset;
                    PipelineProcessor.Receive(pipelineId, ref Receiver, ref EventQueue, ref connection, packetPtr, packetProcessor.Length);
                }
                else
                {
                    var offset = Receiver.AppendToStream(ref packetProcessor);

                    EventQueue.PushEvent(new NetworkEvent
                    {
                        pipelineId = (short)pipelineId,
                        connectionId = packetProcessor.ConnectionRef.Id,
                        type = NetworkEvent.Type.Data,
                        offset = offset,
                        size = packetProcessor.Length
                    });
                }
            }

            private void GenerateConnectionEvents()
            {
                var newConnections = Connections.QueryFinishedConnections(Allocator.Temp);
                var count = newConnections.Length;
                for (int i = 0; i < count; i++)
                {
                    EventQueue.PushEvent(new NetworkEvent
                    {
                        connectionId = newConnections[i].Id,
                        type = NetworkEvent.Type.Connect
                    });
                }
            }

            private void GenerateDisconnectionEvents()
            {
                var newDisconnections = Connections.QueryFinishedDisconnections(Allocator.Temp);
                var count = newDisconnections.Length;
                for (int i = 0; i < count; i++)
                {
                    var disconnectionCommand = newDisconnections[i];

                    // We don't trigger disconnection events if the disconnection
                    // was requested by the local endpoint.
                    if (disconnectionCommand.Reason == Error.DisconnectReason.Default)
                        continue;

                    var offset = Receiver.AppendToStream((byte)disconnectionCommand.Reason);

                    EventQueue.PushEvent(new NetworkEvent
                    {
                        connectionId = disconnectionCommand.Connection.Id,
                        type = NetworkEvent.Type.Disconnect,
                        offset = offset,
                        size = 1
                    });
                }
            }
        }
    }
}
