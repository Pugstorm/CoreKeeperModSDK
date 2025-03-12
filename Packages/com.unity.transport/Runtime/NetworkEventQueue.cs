using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Networking.Transport
{
    [StructLayout(LayoutKind.Explicit)]
    public struct NetworkEvent
    {
        /// <summary>
        /// Types of network events that can be returned by <see cref="NetworkDriver.PopEvent"/>.
        /// </summary>
        public enum Type : short
        {
            /// <summary>No event. Returned when there are no more events to pop.</summary>
            Empty = 0,

            /// <summary>
            /// A message was received on the connection. The contents of the message can be read
            /// from the <see cref="DataStreamReader"/> that was returned along with the event.
            /// Furthermore, the pipeline on which the message was received will also be returned
            /// (for the variants of <c>PopEvent</c> that return it).
            /// </summary>
            Data,

            /// <summary>
            /// Connection has been successfully established. From this point on it's okay to send
            /// data on the connection. Returned after calling <see cref="NetworkDriver.Connect"/>
            /// when the connection was successful. Note that servers do not get this event (new
            /// connections are notified through the <see cref="NetworkDriver.Accept"/> call).
            /// </summary>
            Connect,

            /// <summary>
            /// Connection has been closed, or has failed to be established. The reason for the
            /// disconnection can be read as a single byte off the <see cref="DataStreamReader"/>
            /// obtained along with the event. That single byte will represent a value from the
            /// <see cref="Error.DisconnectReason"/> enum.
            /// </summary>
            Disconnect
        }

        [FieldOffset(0)] internal Type type;
        [FieldOffset(2)] internal short pipelineId;
        [FieldOffset(4)] internal int connectionId;
        [FieldOffset(8)] internal int offset;
        [FieldOffset(12)] internal int size;
    }

    internal struct NetworkEventQueue : IDisposable
    {
        private int MaxEvents
        {
            get { return m_ConnectionEventQ.Length / (m_ConnectionEventHeadTail.Length / 2); }
        }
        public NetworkEventQueue(int queueSizePerConnection)
        {
            m_MasterEventQ = new NativeQueue<SubQueueItem>(Allocator.Persistent);
            m_ConnectionEventQ = new NativeList<NetworkEvent>(queueSizePerConnection, Allocator.Persistent);
            m_ConnectionEventHeadTail = new NativeList<int>(2, Allocator.Persistent);
            m_ConnectionEventQ.ResizeUninitialized(queueSizePerConnection);
            m_ConnectionEventHeadTail.Add(0);
            m_ConnectionEventHeadTail.Add(0);
        }

        public void Dispose()
        {
            m_MasterEventQ.Dispose();
            m_ConnectionEventQ.Dispose();
            m_ConnectionEventHeadTail.Dispose();
        }

        // The returned stream is valid until PopEvent is called again or until the main driver updates

        public NetworkEvent.Type PopEvent(out int id, out int offset, out int size)
        {
            return PopEvent(out id, out offset, out size, out var _);
        }

        public NetworkEvent.Type PopEvent(out int id, out int offset, out int size, out int pipelineId)
        {
            offset = 0;
            size = 0;
            id = -1;
            pipelineId = 0;

            while (true)
            {
                SubQueueItem ev;
                if (!m_MasterEventQ.TryDequeue(out ev))
                {
                    return NetworkEvent.Type.Empty;
                }

                if (m_ConnectionEventHeadTail[ev.connection * 2] == ev.idx)
                {
                    id = ev.connection;
                    return PopEventForConnection(ev.connection, out offset, out size, out pipelineId);
                }
            }
        }

        public NetworkEvent.Type PopEventForConnection(int connectionId, out int offset, out int size)
        {
            return PopEventForConnection(connectionId, out offset, out size, out var _);
        }

        public NetworkEvent.Type PopEventForConnection(int connectionId, out int offset, out int size, out int pipelineId)
        {
            offset = 0;
            size = 0;
            pipelineId = 0;

            if (connectionId < 0 || connectionId >= m_ConnectionEventHeadTail.Length / 2)
                return NetworkEvent.Type.Empty;

            int idx = m_ConnectionEventHeadTail[connectionId * 2];

            if (idx >= m_ConnectionEventHeadTail[connectionId * 2 + 1])
                return NetworkEvent.Type.Empty;

            m_ConnectionEventHeadTail[connectionId * 2] = idx + 1;
            NetworkEvent ev = m_ConnectionEventQ[connectionId * MaxEvents + idx];
            pipelineId = ev.pipelineId;

            if (ev.type == NetworkEvent.Type.Data || ev.type == NetworkEvent.Type.Disconnect)
            {
                offset = ev.offset;
                size = ev.size;
            }

            return ev.type;
        }

        public int GetCountForConnection(int connectionId)
        {
            if (connectionId < 0 || connectionId >= m_ConnectionEventHeadTail.Length / 2)
                return 0;
            return m_ConnectionEventHeadTail[connectionId * 2 + 1] - m_ConnectionEventHeadTail[connectionId * 2];
        }

        /// ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
        /// internal helper functions ::::::::::::::::::::::::::::::::::::::::::
        public void PushEvent(NetworkEvent ev)
        {
            int curMaxEvents = MaxEvents;
            if (ev.connectionId >= m_ConnectionEventHeadTail.Length / 2)
            {
                // Connection id out of range, grow the number of connections in the queue
                int oldSize = m_ConnectionEventHeadTail.Length;
                m_ConnectionEventHeadTail.ResizeUninitialized((ev.connectionId + 1) * 2);
                for (; oldSize < m_ConnectionEventHeadTail.Length; ++oldSize)
                    m_ConnectionEventHeadTail[oldSize] = 0;
                m_ConnectionEventQ.ResizeUninitialized((m_ConnectionEventHeadTail.Length / 2) * curMaxEvents);
            }
            int idx = m_ConnectionEventHeadTail[ev.connectionId * 2 + 1];
            if (idx >= curMaxEvents)
            {
                // Grow the max items per queue and remap the queues
                int oldMax = curMaxEvents;
                while (idx >= curMaxEvents)
                    curMaxEvents *= 2;
                int maxConnections = m_ConnectionEventHeadTail.Length / 2;
                m_ConnectionEventQ.ResizeUninitialized(maxConnections * curMaxEvents);
                for (int con = maxConnections - 1; con >= 0; --con)
                {
                    for (int i = m_ConnectionEventHeadTail[con * 2 + 1] - 1; i >= m_ConnectionEventHeadTail[con * 2]; --i)
                    {
                        m_ConnectionEventQ[con * curMaxEvents + i] = m_ConnectionEventQ[con * oldMax + i];
                    }
                }
            }

            m_ConnectionEventQ[ev.connectionId * curMaxEvents + idx] = ev;
            m_ConnectionEventHeadTail[ev.connectionId * 2 + 1] = idx + 1;

            m_MasterEventQ.Enqueue(new SubQueueItem {connection = ev.connectionId, idx = idx});
        }

        internal void Clear()
        {
            m_MasterEventQ.Clear();
            for (int i = 0; i < m_ConnectionEventHeadTail.Length; ++i)
            {
                m_ConnectionEventHeadTail[i] = 0;
            }
        }

        struct SubQueueItem
        {
            public int connection;
            public int idx;
        }

        private NativeQueue<SubQueueItem> m_MasterEventQ;
        private NativeList<NetworkEvent> m_ConnectionEventQ;
        private NativeList<int> m_ConnectionEventHeadTail;

        public Concurrent ToConcurrent()
        {
            Concurrent concurrent;
            concurrent.m_ConnectionEventQ = m_ConnectionEventQ;
            concurrent.m_ConnectionEventHeadTail = new Concurrent.ConcurrentConnectionQueue(m_ConnectionEventHeadTail);
            return concurrent;
        }

        public struct Concurrent
        {
            [NativeContainer]
            [NativeContainerIsAtomicWriteOnly]
            internal unsafe struct ConcurrentConnectionQueue
            {
                [NativeDisableUnsafePtrRestriction] private UnsafeList<int>* m_ConnectionEventHeadTail;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                private AtomicSafetyHandle m_Safety;
#endif
                public ConcurrentConnectionQueue(NativeList<int> queue)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    m_Safety = NativeListUnsafeUtility.GetAtomicSafetyHandle(ref queue);
                    AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                    m_ConnectionEventHeadTail = (UnsafeList<int>*)NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref queue);
                }

                public int Length
                {
                    get { return m_ConnectionEventHeadTail->Length; }
                }

                public int Dequeue(int connectionId)
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                    int idx = -1;
                    if (connectionId < 0 || connectionId >= m_ConnectionEventHeadTail->Length / 2)
                        return -1;
                    while (idx < 0)
                    {
                        idx = ((int*)m_ConnectionEventHeadTail->Ptr)[connectionId * 2];
                        if (idx >= ((int*)m_ConnectionEventHeadTail->Ptr)[connectionId * 2 + 1])
                            return -1;
                        if (Interlocked.CompareExchange(ref ((int*)m_ConnectionEventHeadTail->Ptr)[connectionId * 2], idx + 1,
                            idx) != idx)
                            idx = -1;
                    }

                    return idx;
                }
            }
            private int MaxEvents
            {
                get { return m_ConnectionEventQ.Length / (m_ConnectionEventHeadTail.Length / 2); }
            }

            public NetworkEvent.Type PopEventForConnection(int connectionId, out int offset, out int size)
            {
                return PopEventForConnection(connectionId, out offset, out size, out var _);
            }

            public NetworkEvent.Type PopEventForConnection(int connectionId, out int offset, out int size, out int pipelineId)
            {
                offset = 0;
                size = 0;
                pipelineId = 0;

                int idx = m_ConnectionEventHeadTail.Dequeue(connectionId);
                if (idx < 0)
                    return NetworkEvent.Type.Empty;
                NetworkEvent ev = m_ConnectionEventQ[connectionId * MaxEvents + idx];
                pipelineId = ev.pipelineId;

                if (ev.type == NetworkEvent.Type.Data || ev.type == NetworkEvent.Type.Disconnect)
                {
                    offset = ev.offset;
                    size = ev.size;
                }

                return ev.type;
            }

            [ReadOnly] internal NativeList<NetworkEvent> m_ConnectionEventQ;
            internal ConcurrentConnectionQueue m_ConnectionEventHeadTail;
        }
    }
}
