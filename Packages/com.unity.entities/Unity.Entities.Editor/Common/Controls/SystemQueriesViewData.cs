using System;
using JetBrains.Annotations;

namespace Unity.Entities.Editor
{
    struct SystemQueriesViewData : IEquatable<SystemQueriesViewData>
    {
        public readonly SystemProxy SystemProxy;
        public readonly SystemKind Kind;
        public readonly QueryViewData[] Queries;
        bool m_IsDuplicatedTypeName;
        string m_CachedDisplayName;

        public SystemQueriesViewData(SystemProxy systemProxy, SystemKind kind, [NotNull] QueryViewData[] queries,
            bool isDuplicatedTypeName = false)
        {
            SystemProxy = systemProxy;
            Kind = kind;
            Queries = queries;
            m_IsDuplicatedTypeName = isDuplicatedTypeName;
            m_CachedDisplayName = null;
        }

        public string SystemName => m_CachedDisplayName ??= m_IsDuplicatedTypeName
            ? $"{SystemProxy.NicifiedDisplayName} ({SystemProxy.Namespace})"
            : SystemProxy.NicifiedDisplayName;

        public bool MarkAsDuplicatedTypeName()
        {
            if (m_IsDuplicatedTypeName)
                return false;

            m_IsDuplicatedTypeName = true;
            m_CachedDisplayName = null;
            return true;
        }

        public enum SystemKind : byte
        {
            Regular = 0,
            Unmanaged = 1,
            CommandBufferBegin = 2,
            CommandBufferEnd = 3
        }

        public bool Equals(SystemQueriesViewData other) => Kind == other.Kind && string.Equals(SystemProxy.TypeFullName, other.SystemProxy.TypeFullName, StringComparison.InvariantCultureIgnoreCase);
    }
}
