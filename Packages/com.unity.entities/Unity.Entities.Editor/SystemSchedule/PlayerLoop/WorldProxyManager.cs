using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Entities.Editor
{
    class WorldProxyManager
    {
        readonly Dictionary<World, WorldProxy> m_WorldProxyForLocalWorldsDict = new Dictionary<World, WorldProxy>();
        readonly Dictionary<WorldProxy, IWorldProxyUpdater> m_WorldProxyUpdaterDict = new Dictionary<WorldProxy, IWorldProxyUpdater>();

        public WorldProxy GetWorldProxyForGivenWorld(World world)
        {
            if (world == null || !world.IsCreated)
                throw new ArgumentNullException(nameof(world));

            if (m_WorldProxyForLocalWorldsDict.TryGetValue(world, out var worldProxy))
            {
                return worldProxy;
            }

            throw new ArgumentException($"WorldProxy for given world {world.Name} does not exist or is null");
        }

        public List<IWorldProxyUpdater> GetAllWorldProxyUpdaters()
        {
            return m_WorldProxyUpdaterDict.Values.ToList();
        }

        WorldProxy m_SelectedWorldProxy;

        public WorldProxy SelectedWorldProxy
        {
            get => m_SelectedWorldProxy;
            set
            {
                if (m_SelectedWorldProxy != null && m_SelectedWorldProxy.Equals(value))
                    return;

                m_SelectedWorldProxy = value;
                SetActiveUpdater();
            }
        }

        public bool IsFullPlayerLoop { get; set; }

        public void CreateWorldProxiesForAllWorlds()
        {
            foreach (var world in World.All)
            {
                if (m_WorldProxyForLocalWorldsDict.Keys.Contains(world))
                    continue;

                GetOrCreateNewWorldProxyForGivenWorld(world);
            }

            CleanUpWorldProxyDictionary();
        }

        public void RebuildWorldProxyForGivenWorld(World world)
        {
            var worldProxy = GetOrCreateNewWorldProxyForGivenWorld(world);
            if (m_WorldProxyUpdaterDict.TryGetValue(worldProxy, out var localWorldProxyUpdater))
                localWorldProxyUpdater.ResetWorldProxy();
        }

        WorldProxy GetOrCreateNewWorldProxyForGivenWorld(World world)
        {
            if (world == null || !world.IsCreated)
                throw new ArgumentNullException(nameof(world));

            CleanUpWorldProxyDictionary();

            if (m_WorldProxyForLocalWorldsDict.TryGetValue(world, out var worldProxy))
                return worldProxy;

            worldProxy = new WorldProxy(world.SequenceNumber);
            var updater = new LocalWorldProxyUpdater(world, worldProxy);
            updater.PopulateWorldProxy();
            if (IsFullPlayerLoop)
                updater.EnableUpdater();

            m_WorldProxyForLocalWorldsDict.Add(world, worldProxy);
            m_WorldProxyUpdaterDict.Add(worldProxy, updater);

            return worldProxy;
        }

        void CleanUpWorldProxyDictionary()
        {
            foreach (var world in m_WorldProxyForLocalWorldsDict.Keys.ToList())
            {
                if (world.IsCreated || !m_WorldProxyForLocalWorldsDict.TryGetValue(world, out var worldProxy))
                    continue;

                RemoveInvalidWorldProxy(world, worldProxy);
            }
        }

        void RemoveInvalidWorldProxy(World world, WorldProxy worldProxy)
        {
            m_WorldProxyUpdaterDict.TryGetValue(worldProxy, out var updater);
            updater.DisableUpdater();
            m_WorldProxyUpdaterDict.Remove(worldProxy);
            m_WorldProxyForLocalWorldsDict.Remove(world);
        }

        void EnableAllUpdaters()
        {
            foreach (var updater in m_WorldProxyUpdaterDict.Values)
            {
                updater.EnableUpdater();
            }
        }

        public void Dispose()
        {
            foreach (var updater in m_WorldProxyUpdaterDict.Values)
            {
                updater.DisableUpdater();
            }
        }

        void SetActiveUpdater()
        {
            if (m_SelectedWorldProxy == null)
                return;

            if (IsFullPlayerLoop)
            {
                EnableAllUpdaters();
                return;
            }

            foreach (var kvp in m_WorldProxyUpdaterDict)
            {
                var worldProxy = kvp.Key;
                var updater = kvp.Value;

                if (worldProxy.Equals(m_SelectedWorldProxy))
                {
                    updater.EnableUpdater();
                }
                else
                {
                    updater.DisableUpdater();
                }
            }
        }
    }
}
