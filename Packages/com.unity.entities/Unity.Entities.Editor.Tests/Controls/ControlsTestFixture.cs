using NUnit.Framework;

namespace Unity.Entities.Editor.Tests
{
    class ControlsTestFixture
    {
        public World m_TestWorld;
        public SystemBase m_SystemA;
        public SystemBase m_SystemB;
        WorldProxyManager m_WorldProxyManager;
        public WorldProxy m_WorldProxy;

        [SetUp]
        public void SetUp()
        {
            m_TestWorld = new World("Relationship Test world");
            var group = m_TestWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            m_SystemA = m_TestWorld.GetOrCreateSystemManaged<TestSystemsForControls.SystemA>();
            m_SystemB = m_TestWorld.GetOrCreateSystemManaged<TestSystemsForControls.SystemB>();
            group.AddSystemToUpdateList(m_SystemA);
            group.AddSystemToUpdateList(m_SystemB);
            group.SortSystems();

            m_WorldProxyManager = new WorldProxyManager();
            m_WorldProxyManager.CreateWorldProxiesForAllWorlds();
            m_WorldProxy = m_WorldProxyManager.GetWorldProxyForGivenWorld(m_TestWorld);
        }

        [TearDown]
        public void TearDown()
        {
            m_TestWorld.Dispose();
        }
    }
}
