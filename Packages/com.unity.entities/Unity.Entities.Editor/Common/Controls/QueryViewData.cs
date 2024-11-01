using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;

namespace Unity.Entities.Editor
{
    struct QueryViewData : IEquatable<QueryViewData>
    {
        public readonly int QueryId;
        public readonly QueryViewDataContext Context;
        static readonly string k_Query = L10n.Tr("Query");

        ComponentViewData[] m_CachedComponents;

        public ComponentViewData[] Components => m_CachedComponents ??= Context.Query.GetComponentDataFromQuery().ToArray();

        public readonly string QueryName => $"{k_Query} #{QueryId}";

        public QueryViewData(int queryId, [NotNull] ComponentViewData[] components)
        {
            QueryId = queryId;
            m_CachedComponents = components;
            Context = default;
        }

        public QueryViewData(int queryId, EntityQuery query, SystemProxy systemProxy, World world)
        {
            QueryId = queryId;
            Context = new QueryViewDataContext(query, systemProxy, world);
            m_CachedComponents = default;
        }

        public bool Equals(QueryViewData other) => QueryId == other.QueryId;

        internal readonly struct QueryViewDataContext
        {
            public readonly EntityQuery Query;
            public readonly SystemProxy SystemProxy;
            public readonly World World;

            public QueryViewDataContext(EntityQuery query, SystemProxy systemProxy, World world)
            {
                Query = query;
                SystemProxy = systemProxy;
                World = world;
            }
        }
    }
}
