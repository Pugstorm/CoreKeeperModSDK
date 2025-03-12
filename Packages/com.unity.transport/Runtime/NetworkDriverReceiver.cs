using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport.Utilities;

namespace Unity.Networking.Transport
{
    internal struct NetworkDriverReceiver : IDisposable
    {
        private const int k_InitialDataStreamSize = 4096;

        private PacketsQueue m_ReceiveQueue;
        private NativeList<byte> m_DataStream;
        private OperationResult m_Result;

        internal OperationResult Result => m_Result;

        // TODO: evaluate moving this to the NetworkStack
        internal PacketsQueue ReceiveQueue => m_ReceiveQueue;

        internal NetworkDriverReceiver(PacketsQueue receiveQueue)
        {
            m_ReceiveQueue = receiveQueue;
            m_DataStream = new NativeList<byte>(k_InitialDataStreamSize, Allocator.Persistent);
            m_Result = new OperationResult("receive", Allocator.Persistent);
        }

        public void Dispose()
        {
            if (m_DataStream.IsCreated)
            {
                m_ReceiveQueue.Dispose();
                m_DataStream.Dispose();
                m_Result.Dispose();
            }
        }

        internal NativeArray<byte> GetDataStreamSubArray(int offset, int size)
        {
            return m_DataStream.AsArray().GetSubArray(offset, size);
        }

        internal void ClearStream()
        {
            m_DataStream.Clear();
        }

        private unsafe int AppendToStream(byte* dataPtr, int dataLength)
        {
            m_DataStream.ResizeUninitializedTillPowerOf2(m_DataStream.Length + dataLength);
            var offset = m_DataStream.Length;

            m_DataStream.Length = offset + dataLength;
            UnsafeUtility.MemCpy((byte*)m_DataStream.GetUnsafePtr() + offset, dataPtr, dataLength);

            return offset;
        }

        internal unsafe int AppendToStream(ref PacketProcessor packetProcessor)
        {
            m_DataStream.ResizeUninitializedTillPowerOf2(m_DataStream.Length + packetProcessor.Length);
            var offset = m_DataStream.Length;

            m_DataStream.Length = offset + packetProcessor.Length;
            packetProcessor.CopyPayload((byte*)m_DataStream.GetUnsafePtr() + offset, packetProcessor.Length);

            return offset;
        }

        // Interface to add disconnect reasons to the stream
        internal int AppendToStream(byte value)
        {
            m_DataStream.ResizeUninitializedTillPowerOf2(m_DataStream.Length + 1);
            var offset = m_DataStream.Length;

            m_DataStream.Length = offset + 1;
            m_DataStream[offset] = value;

            return offset;
        }

        // Interface for receiving data from a pipeline
        internal unsafe void PushDataEvent(NetworkConnection con, int pipelineId, byte* dataPtr, int dataLength, ref NetworkEventQueue eventQueue)
        {
            var sliceOffset = AppendToStream(dataPtr, dataLength);

            eventQueue.PushEvent(new NetworkEvent
            {
                pipelineId = (short)pipelineId,
                connectionId = con.InternalId,
                type = NetworkEvent.Type.Data,
                offset = sliceOffset,
                size = dataLength
            });
        }
    }
}
