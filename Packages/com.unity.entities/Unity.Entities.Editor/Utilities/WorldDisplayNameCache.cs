using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.Entities.Editor
{
    class WorldDisplayNameCache
    {
        readonly Dictionary<string, bool> m_WorldNameDuplicationContainer = new Dictionary<string, bool>();
        readonly Dictionary<ulong, string> m_WorldDisplayNameBySequenceNumber = new Dictionary<ulong, string>();
        readonly Func<World, bool> m_Filter;
        readonly Func<World, string> m_DisplayNameProvider;

        public WorldDisplayNameCache(Func<World, bool> filter = null, Func<World, string> displayNameProvider = null)
        {
            m_Filter = filter;
            m_DisplayNameProvider = displayNameProvider ?? DefaultDisplayNameFormatter;
        }

        public void RebuildCache()
        {
            m_WorldNameDuplicationContainer.Clear();
            m_WorldDisplayNameBySequenceNumber.Clear();

            foreach (var world in World.All)
            {
                if (m_Filter != null && !m_Filter(world))
                    continue;

                if (m_WorldNameDuplicationContainer.ContainsKey(world.Name))
                    m_WorldNameDuplicationContainer[world.Name] = true;
                else
                    m_WorldNameDuplicationContainer[world.Name] = false;
            }

            foreach (var world in World.All)
            {
                if (m_Filter != null && !m_Filter(world))
                    continue;

                m_WorldDisplayNameBySequenceNumber[world.SequenceNumber] = m_WorldNameDuplicationContainer[world.Name] ? m_DisplayNameProvider(world) : world.Name;
            }
        }

        public string GetWorldDisplayName(World world)
        {
            if (world == null)
                return null;

            if (m_WorldDisplayNameBySequenceNumber.Count == 0)
                RebuildCache();

            return m_WorldDisplayNameBySequenceNumber.TryGetValue(world.SequenceNumber, out var name) ? name : world.Name;
        }

        static string DefaultDisplayNameFormatter(World w) => $"{w.Name} (#{w.SequenceNumber})";
    }
}
