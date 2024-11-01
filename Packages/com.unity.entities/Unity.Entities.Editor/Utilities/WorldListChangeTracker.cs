using System;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// Detect world list changes when new worlds are created or old worlds are destroyed.
    /// </summary>
    class WorldListChangeTracker
    {
        int m_Count;
        ulong m_CurrentHash;

        /// <returns>If called after World(s) have been created or destroyed, will return true *once*.</returns>
        public bool HasChanged()
        {
            var newCount = World.All.Count;
            ulong newHash = 0;
            foreach (var world in World.All)
                newHash += world.SequenceNumber;

            var hasChanged = m_Count != newCount || newHash != m_CurrentHash;
            m_CurrentHash = newHash;
            m_Count = newCount;
            return hasChanged;
        }
    }
}
