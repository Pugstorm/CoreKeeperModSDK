using System;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.NetCode
{
    /// <summary>
    /// Specify how the ghosts added to the relevancy set should be used.
    /// </summary>
    public enum GhostRelevancyMode
    {
        /// <summary>
        /// The default. No relevancy will applied under any circumstances.
        /// </summary>
        Disabled,
        /// <summary>
        /// Only ghosts added to relevancy set (`GhostRelevancySet`, below) are considered "relevant to that client", and thus serialized for the specified connection (where possible, obviously, as eventual consistency and importance scaling rules still apply).
        /// </summary>
        /// <remarks>
        /// Note that applying this setting will cause all ghosts to default to not be replicated to any client. It's a useful default when it's rare or impossible for a player to be viewing the entire world.
        /// </remarks>
        SetIsRelevant,
        /// <summary>
        /// Ghosts added to relevancy set (<see cref="GhostRelevancy.GhostRelevancySet"/>) are considered "not-relevant to that client", and thus will be not serialized for the specified connection.
        /// In other words: Set this mode if you want to specifically ignore specific entities for a given client.
        /// </summary>
        SetIsIrrelevant
    }

    /// <summary>
    /// A connection-ghost pair, used to populate the <see cref="GhostRelevancy"/> set at runtime, by declaring which ghosts are relevant for a given connection.
    /// Behaviour is dependent upon on <see cref="GhostRelevancyMode"/>.
    /// </summary>
    public struct RelevantGhostForConnection : IEquatable<RelevantGhostForConnection>, IComparable<RelevantGhostForConnection>
    {
        /// <summary>
        /// Construct a new instance with the given connection id and ghost
        /// </summary>
        /// <param name="connection">The connection id</param>
        /// <param name="ghost"></param>
        public RelevantGhostForConnection(int connection, int ghost)
        {
            Connection = connection;
            Ghost = ghost;
        }
        /// <summary>
        /// return whenever the <paramref name="other"/> RelevantGhostForConnection is equals the current instance.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(RelevantGhostForConnection other)
        {
            return Connection == other.Connection && Ghost == other.Ghost;
        }
        /// <summary>
        /// Comparison operator, used for sorting.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(RelevantGhostForConnection other)
        {
            if (Connection == other.Connection)
                return Ghost - other.Ghost;
            return Connection - other.Connection;
        }
        /// <summary>
        /// A hash code suitable to insert the RelevantGhostForConnection into an hashmap or
        /// other key-value pair containers. Is guarantee to be unique for the connection, ghost pairs.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return (Connection << 24) | Ghost;
        }
        /// <summary>
        /// The connection for which this ghost is relevant.
        /// </summary>
        public int Connection;
        /// <summary>
        /// the ghost id of the entity.
        /// </summary>
        public int Ghost;
    }

    /// <summary>
    /// Singleton entity present on the server.
    /// Every frame, collect the set of ghosts that should be (or should not be) replicated to a given client.
    /// </summary>
    /// <remarks>
    /// Use GhostRelevancy to avoid replicating entities that the player can neither see, nor interact with.
    /// </remarks>
    public struct GhostRelevancy : IComponentData
    {
        internal GhostRelevancy(NativeParallelHashMap<RelevantGhostForConnection, int> set)
        {
            GhostRelevancySet = set;
            GhostRelevancyMode = GhostRelevancyMode.Disabled;
            DefaultRelevancyQuery = default;
        }
        /// <summary>
        /// Specify if the ghosts present in the <see cref="GhostRelevancySet"/> should be replicated (relevant) or not replicated
        /// (irrelevant) to the the client.
        /// </summary>
        public GhostRelevancyMode GhostRelevancyMode;
        /// <summary>
        /// A sorted collection of (connection, ghost) pairs, that should be used to specify which ghosts, for a given
        /// connection, should be replicated (or not replicated, based on the <see cref="GhostRelevancyMode"/>) for the current
        /// simulated tick.
        /// For per-component type rules, see <see cref="DefaultRelevancyQuery"/>.
        /// </summary>
        public readonly NativeParallelHashMap<RelevantGhostForConnection, int> GhostRelevancySet;

        /// <summary>
        /// Use this query to specify the default per-component type rules about which ghosts should be relevant.
        /// Note, however, that this filter is overridden by <see cref="GhostRelevancySet"/>.
        /// For example
        /// Mode = SetIsRelevant, DefaultRelevancyQuery = Any&lt;MyComponentA&gt;, GhostRelevancySet = ghostWithComponentB
        /// - All ghosts with MyComponentA + the single ghostWithComponentB will be relevant
        /// Mode = SetIsIrrelevant, DefaultRelevancyQuery = Any&lt;MyComponentA&gt;, GhostRelevancySet = ghostWithComponentA
        /// - All ghosts with MyComponentA will be relevant, except the single ghostWithComponentA
        /// </summary>
        /// <remarks>
        /// Since this is translating to a <see cref="EntityQueryMask"/> internally, the same restrictions apply for filtering.
        /// Ensure your query uses the Any filter if you have multiple ghost types which should all be considered always relevant by default.
        /// </remarks>
        public EntityQuery DefaultRelevancyQuery;
    }
}
