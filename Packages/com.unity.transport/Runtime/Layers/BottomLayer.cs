using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Networking.Transport
{
    /// <summary>
    /// The BottomLayer is the first one executed on receiving,
    /// and the last one executed on sending.
    /// </summary>
    internal struct BottomLayer : INetworkLayer
    {
        private NativeList<ConnectionList> m_ConnectionLists;

        internal void AddConnectionList(ref ConnectionList connections)
        {
            m_ConnectionLists.Add(connections);
        }

        public int Initialize(ref NetworkSettings settings, ref ConnectionList connectionList, ref int packetPadding)
        {
            m_ConnectionLists = new NativeList<ConnectionList>(1, Allocator.Persistent);
            return 0;
        }

        public void Dispose()
        {
            m_ConnectionLists.Dispose();
        }

        public JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dependency)
        {
            var jobs = dependency;

            foreach (var connectionList in m_ConnectionLists)
            {
                jobs = JobHandle.CombineDependencies(jobs, new ConnectionListCleanup
                {
                    Connections = connectionList,
                }.Schedule(dependency));
            }

            return jobs;
        }

        [BurstCompile]
        private struct ConnectionListCleanup : IJob
        {
            public ConnectionList Connections;

            public void Execute()
            {
                Connections.Cleanup();
            }
        }

        public JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dependency)
        {
            return new ClearJob
            {
                SendQueue = arguments.SendQueue,
            }.Schedule(dependency);
        }

        [BurstCompile]
        private struct ClearJob : IJob
        {
            public PacketsQueue SendQueue;

            public void Execute()
            {
                SendQueue.Clear();
            }
        }
    }
}
