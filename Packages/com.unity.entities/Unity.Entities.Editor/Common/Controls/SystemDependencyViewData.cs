using System;

namespace Unity.Entities.Editor
{
    readonly struct SystemDependencyViewData : IEquatable<SystemDependencyViewData>
    {
        // We need this SystemProxy in order to inspect the system in inspector and highlight it within Systems window.
        public readonly SystemProxy SystemProxy;
        public readonly string SystemName;

        public SystemDependencyViewData(SystemProxy systemProxy, string systemName)
        {
            SystemProxy = systemProxy;
            SystemName = systemName;
        }

        public bool Equals(SystemDependencyViewData other)
        {
            // We only need to check the system name and its content for the display, therefore no need to compare the
            // SystemProxy instance itself.
            return SystemName == other.SystemName;
        }
    }
}
