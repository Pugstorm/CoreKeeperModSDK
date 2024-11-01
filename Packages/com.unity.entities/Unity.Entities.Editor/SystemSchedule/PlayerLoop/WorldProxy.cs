using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Entities.Editor
{
    class WorldProxy : IEquatable<WorldProxy>
    {
        public readonly ulong SequenceNumber;

        /// <summary>
        /// Handles for root system groups (init, sim, pres).
        /// </summary>
        internal List<SystemProxy> m_RootSystems;

        /// <summary>
        /// Handles for all known systems.
        /// </summary>
        internal List<SystemProxy> m_AllSystems;

        /// <summary>
        /// Handles for all known systems in their scheduling order.
        /// </summary>
        List<SystemProxy> m_AllSystemsInOrder;

        /// <summary>
        /// Data that is relatively constant; only gets recreated when drastic changes happen,
        /// such as: names, parent/children mapping, dependencies, category, recorder, etc.
        /// </summary>
        internal List<ScheduledSystemData> m_SystemData;

        /// <summary>
        /// Data that is changing every frame,
        /// such as: entity matching count, running time, enabled/disabled state, etc.
        /// </summary>
        internal List<SystemFrameData> m_FrameData;

        internal IReadOnlyList<ScheduledSystemData> AllSystemData => m_SystemData;
        internal IReadOnlyList<SystemFrameData> AllFrameData => m_FrameData;

        IReadOnlyList<SystemProxy> RootSystems => m_RootSystems;
        internal IReadOnlyList<SystemProxy> AllSystems => m_AllSystems;
        internal IReadOnlyList<SystemProxy> AllSystemsInOrder => m_AllSystemsInOrder;

        public WorldProxy(ulong sequenceNumber)
        {
            SequenceNumber = sequenceNumber;
            m_AllSystems = new List<SystemProxy>();
            m_AllSystemsInOrder = new List<SystemProxy>();
            m_SystemData = new List<ScheduledSystemData>();
            m_FrameData = new List<SystemFrameData>();
            m_RootSystems = new List<SystemProxy>();
        }

        public void OnGraphChange()
        {
            ResizeFrameDataList();
            GetOrderedSystems();
        }

        void ResizeFrameDataList()
        {
            m_FrameData.Clear();
            for (var i = 0; i < AllSystems.Count; i++)
            {
                m_FrameData.Add(default);
            }
        }

        void GetOrderedSystems()
        {
            foreach (var rootSystem in RootSystems)
            {
                ParseAllSystems(rootSystem);
            }
        }

        void ParseAllSystems(SystemProxy systemProxy)
        {
            m_AllSystemsInOrder.Add(systemProxy);

            for (var i = 0; i < systemProxy.ChildCount; i++)
            {
                ParseAllSystems(m_AllSystems[systemProxy.FirstChildIndexInWorld + i]);
            }
        }

        public void Clear()
        {
            m_AllSystems.Clear();
            m_AllSystemsInOrder.Clear();
            m_SystemData.Clear();
            m_RootSystems.Clear();
        }

        internal int FindSystemIndexFor(ComponentSystemBase sys)
        {
            for (var i = 0; i < m_SystemData.Count; i++)
            {
                var systemData = m_SystemData[i];
                if (ReferenceEquals(systemData.Managed, sys))
                {
                    return i;
                }
            }

            throw new InvalidProgramException($"Couldn't find system ${sys} in World");
        }

        internal int FindSystemIndexFor(SystemHandle systemProxy)
        {
            for (var i = 0; i < m_SystemData.Count; i++)
            {
                if (m_SystemData[i].WorldSystemHandle.Equals(systemProxy))
                    return i;
            }

            throw new InvalidProgramException($"Couldn't find unmanaged system in World");
        }

        internal IReadOnlyList<SystemProxy> GetUpdateBeforeSet(SystemProxy sys)
        {
            var data = m_SystemData[sys.SystemIndex];
            return data.UpdateBeforeIndices.Select(v => m_AllSystems[v]).ToList();
        }

        internal IReadOnlyList<SystemProxy> GetUpdateAfterSet(SystemProxy sys)
        {
            var data = m_SystemData[sys.SystemIndex];
            return data.UpdateAfterIndices.Select(v => m_AllSystems[v]).ToList();
        }

        unsafe SystemState* GetStatePointer(in ScheduledSystemData sys, World world)
        {
            if ((sys.Category & SystemCategory.Unmanaged) == 0)
                return sys.Managed.m_StatePtr;

            if (world != null && world.IsCreated)
                return world.Unmanaged.ResolveSystemState(sys.WorldSystemHandle);

            return null;
        }

        public unsafe void SetSystemEnabledState(int systemIndex, bool value, World world)
        {
            GetStatePointer(m_SystemData[systemIndex], world)->Enabled = value;
        }

        public bool Equals(WorldProxy other)
        {
            return SequenceNumber.Equals(other.SequenceNumber);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldProxy other && Equals(other);
        }

        public override int GetHashCode()
        {
            return SequenceNumber.GetHashCode();
        }
    }
}
