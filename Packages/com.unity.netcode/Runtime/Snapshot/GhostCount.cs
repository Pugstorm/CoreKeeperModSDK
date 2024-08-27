using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Singleton component with APIs and collections required for Ghost counting.
    /// </summary>
    [BurstCompile]
    public struct GhostCount : IComponentData
    {
        /// <summary>
        /// The total number of ghosts on the server the last time a snapshot was received. Use this and GhostCountOnClient to figure out how much of the state the client has received.
        /// </summary>
        public int GhostCountOnServer => m_GhostCompletionCount[0];

        /// <summary>
        /// The total number of ghosts received by this client the last time a snapshot was received. The number of received ghosts can be different from the number of currently spawned ghosts. Use this and GhostCountOnServer to figure out how much of the state the client has received.
        /// </summary>
        public int GhostCountOnClient => m_GhostCompletionCount[1];

        private NativeArray<int> m_GhostCompletionCount;

        /// <summary>
        /// Construct and initialize the new ghost count instance.
        /// </summary>
        /// <param name="ghostCompletionCount"></param>
        internal GhostCount(NativeArray<int> ghostCompletionCount)
        {
            m_GhostCompletionCount = ghostCompletionCount;
        }

        /// <summary>
        /// Logs 'GhostCount[c:X,s:X]'.
        /// </summary>
        /// <returns>Logs 'GhostCount[c:X,s:X]'.</returns>
        public FixedString128Bytes ToFixedString() => $"GhostCount[c:{GhostCountOnClient},s:{GhostCountOnServer}]";

        /// <inheritdoc cref="ToFixedString"/>
        public override string ToString() => ToFixedString().ToString();
    }
}
