namespace Unity.Entities.Editor
{
    /// <summary>
    /// Detect world system list changes when new systems are added or destroyed.
    /// </summary>
    class WorldSystemListChangeTracker
    {
        readonly World m_World;
        int m_LastWorldVersion;

        public WorldSystemListChangeTracker(World world)
        {
            m_World = world;
        }

        public bool HasChanged()
        {
            var currentWorldVersion = m_World.Version;
            if (m_LastWorldVersion == currentWorldVersion)
                return false;

            m_LastWorldVersion = currentWorldVersion;
            return true;
        }
    }
}
