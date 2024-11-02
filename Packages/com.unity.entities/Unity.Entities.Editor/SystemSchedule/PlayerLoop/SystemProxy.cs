using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

namespace Unity.Entities.Editor
{
    unsafe struct SystemProxy : IEquatable<SystemProxy>
    {
        public readonly WorldProxy WorldProxy;
        public readonly int SystemIndex;
        public readonly bool BelongToCurrentWorld;

        public SystemProxy(WorldProxy worldProxy, int systemIndex, bool belongToCurrentWorld = true)
        {
            WorldProxy = worldProxy;
            SystemIndex = systemIndex;
            World = null;
            BelongToCurrentWorld = belongToCurrentWorld;
        }

        public SystemProxy(WorldProxy worldProxy, int systemIndex, World world, bool belongToCurrentWorld = true)
        {
            WorldProxy = worldProxy;
            SystemIndex = systemIndex;
            World = world;
            BelongToCurrentWorld = belongToCurrentWorld;
        }

        public SystemProxy(ComponentSystemBase b, WorldProxy worldProxy, bool belongToCurrentWorld = true)
        {
            WorldProxy = worldProxy;
            SystemIndex = WorldProxy.FindSystemIndexFor(b);
            World = b.World;
            BelongToCurrentWorld = belongToCurrentWorld;
        }

        public SystemProxy(SystemHandle h, World w, WorldProxy worldProxy, bool belongToCurrentWorld = true)
        {
            WorldProxy = worldProxy;
            SystemIndex = WorldProxy.FindSystemIndexFor(h);
            World = w;
            BelongToCurrentWorld = belongToCurrentWorld;
        }

        ScheduledSystemData ScheduledSystemData
        {
            get
            {
                if (SystemIndex >= WorldProxy.AllSystemData.Count || SystemIndex < 0)
                    return default;

                return WorldProxy.AllSystemData[SystemIndex];
            }
        }

        SystemFrameData FrameData
        {
            get
            {
                if (SystemIndex >= WorldProxy.AllFrameData.Count || SystemIndex < 0)
                    return default;

                return WorldProxy.AllFrameData[SystemIndex];
            }
        }

        public SystemCategory Category
        {
            get
            {
                if (SystemIndex >= WorldProxy.AllSystemData.Count || SystemIndex < 0)
                    return SystemCategory.Unknown;

                return WorldProxy.AllSystemData[SystemIndex].Category;
            }
        }

        public bool Valid => WorldProxy != null;

        public SystemProxy Parent => ScheduledSystemData.ParentIndex == -1 ? default : WorldProxy.AllSystems[ScheduledSystemData.ParentIndex];

        public int ChildCount => ScheduledSystemData.ChildCount;
        public int FirstChildIndexInWorld => ScheduledSystemData.ChildIndex;

        public string NicifiedDisplayName => ScheduledSystemData.NicifiedDisplayName;
        public string TypeName => ScheduledSystemData.TypeName;
        public string TypeFullName => ScheduledSystemData.FullName;
        public string Namespace => ScheduledSystemData.Namespace;

        public void SetEnabled(bool value)
        {
            WorldProxy.SetSystemEnabledState(SystemIndex, value, World);
        }

        public bool Enabled => FrameData.Enabled;

        public bool IsRunning => FrameData.IsRunning;

        public int TotalEntityMatches => FrameData.EntityCount;

        public float RunTimeMillisecondsForDisplay => FrameData.LastFrameRuntimeMilliseconds;

        public IReadOnlyList<SystemProxy> UpdateBeforeSet => WorldProxy.GetUpdateBeforeSet(this);

        public IReadOnlyList<SystemProxy> UpdateAfterSet => WorldProxy.GetUpdateAfterSet(this);

        public IEnumerable<string> GetComponentTypesUsedByQueries()
        {
            if (World == null || !World.IsCreated)
                return Enumerable.Empty<string>();

            if (!Valid)
                return Enumerable.Empty<string>();

            using var hashPool = PooledHashSet<string>.Make();
            var hashset = hashPool.Set;

            var ptr = StatePointer;
            if (ptr != null && ptr->EntityQueries.Length > 0)
            {
                var queries = ptr->EntityQueries;
                for (var i = 0; i < queries.Length; i++)
                {
                    using var queryTypeList = queries[i].GetQueryTypes().ToPooledList();
                    foreach (var name in queryTypeList.List.Select(queryType => TypeUtility.GetTypeDisplayName(queryType.GetManagedType())))
                    {
                        hashset.Add(name);
                    }
                }
            }

            return hashset.ToArray();
        }

        public bool Equals(SystemProxy other)
        {
            if (!other.Valid)
                return false;

            return WorldProxy.SequenceNumber.Equals(other.WorldProxy.SequenceNumber) &&
                   SystemIndex == other.SystemIndex;
        }

        public override int GetHashCode()
        {
            var hash = WorldProxy.GetHashCode();
            hash = hash * 31 + SystemIndex;
            return hash;
        }

        public override bool Equals(object obj)
        {
            if (obj is SystemProxy sel)
            {
                return Equals(sel);
            }

            return false;
        }

        public static bool operator ==(SystemProxy lhs, SystemProxy rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(SystemProxy lhs, SystemProxy rhs)
        {
            return !lhs.Equals(rhs);
        }

        public override string ToString()
        {
            return TypeFullName;
        }

        public World World { get; }

        public SystemState* StatePointerForQueryResults => StatePointer;

        SystemState* StatePointer
        {
            get
            {
                if (World == null || !World.IsCreated)
                    return null;

                if ((Category & SystemCategory.Unmanaged) == 0)
                    return ScheduledSystemData.Managed.m_StatePtr;

                var world = World;
                if (world != null && world.IsCreated)
                    return world.Unmanaged.ResolveSystemState(ScheduledSystemData.WorldSystemHandle);

                return null;
            }
        }
    }
}

