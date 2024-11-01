using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Assertions;
using Unity.Profiling;
using UnityEditor;

namespace Unity.Entities.Editor
{
    class LocalWorldProxyUpdater : IWorldProxyUpdater
    {
        static ProfilerMarker s_UpdateTransientDataFromLocalWorldMarker =
            new ProfilerMarker(ProfilerCategory.Internal, "Update Local World Stats");

        readonly Cooldown m_Cooldown = new Cooldown(TimeSpan.FromMilliseconds(Constants.Inspector.CoolDownTime));
        readonly World m_LocalWorld;
        readonly WorldProxy m_WorldProxy;
        readonly WorldSystemListChangeTracker m_SystemListChangeTracker;
        bool m_IsActive;
        bool m_IsDirty;

        public LocalWorldProxyUpdater(World world, WorldProxy worldProxy)
        {
            m_LocalWorld = world;
            m_WorldProxy = worldProxy;
            m_SystemListChangeTracker = new WorldSystemListChangeTracker(world);
        }

        public bool IsDirty()
        {
            return m_IsDirty;
        }

        public void SetClean()
        {
            m_IsDirty = false;
        }

        public bool IsActive()
        {
            return m_IsActive;
        }

        public void EnableUpdater()
        {
            if (m_IsActive)
                return;

            EditorApplication.update += UpdateWorldProxy;
            m_IsActive = true;
            UpdateFrameData();
        }

        public void DisableUpdater()
        {
            if (!m_IsActive)
                return;

            EditorApplication.update -= UpdateWorldProxy;
            m_IsActive = false;
        }

        void UpdateWorldProxy()
        {
            if (!m_Cooldown.Update(DateTime.Now))
                return;

            if (m_LocalWorld == null || !m_LocalWorld.IsCreated || !m_WorldProxy.AllSystems.Any())
                return;

            UpdateFrameData();

            if (!m_SystemListChangeTracker.HasChanged())
                return;

            ResetWorldProxy();
            m_IsDirty = true;
        }

        public void ResetWorldProxy()
        {
            m_WorldProxy.Clear();
            PopulateWorldProxy();
            UpdateFrameData();
        }

        public void PopulateWorldProxy()
        {
            var rootTypes = new[]
            {
                typeof(InitializationSystemGroup),
                typeof(SimulationSystemGroup),
                typeof(PresentationSystemGroup)
            };

            var workQueue = new Queue<GroupSystemInQueue>();

            foreach (var rootType in rootTypes)
            {
                var sys = m_LocalWorld.GetExistingSystemManaged(rootType);
                if (sys == null) // will happen in subset world
                    continue;

                var handle = CreateSystemProxy(m_WorldProxy, sys.World, sys.World == m_LocalWorld);
                m_WorldProxy.m_SystemData.Add(new ScheduledSystemData(sys, -1));

                workQueue.Enqueue(new GroupSystemInQueue { group = (ComponentSystemGroup) sys, index = handle.SystemIndex });
            }

            // Iterate through groups, making sure that all group children end up in sequential order in resulting list
            while (workQueue.Count > 0)
            {
                var work = workQueue.Dequeue();
                var group = work.group;
                var groupIndex = work.index;

                var firstChildIndex = m_WorldProxy.m_AllSystems.Count;

                ref var updateSystemList = ref group.m_MasterUpdateList;
                var numSystems = updateSystemList.Length;
                var removedSystemCount = 0;
                for (var i = 0; i < numSystems; i++)
                {
                    var updateIndex = updateSystemList[i];

                    ComponentSystemBase sys = null;
                    ScheduledSystemData sd;
                    World creationWorld;

                    if (updateIndex.IsManaged)
                    {
                        sys = group.ManagedSystems[updateIndex.Index];
                        sd = new ScheduledSystemData(sys, groupIndex);
                    }
                    else
                    {
                        var unmanagedSystem = group.UnmanagedSystems[updateIndex.Index];
                        creationWorld = FindCreationWorld(unmanagedSystem.m_WorldSeqNo);
                        if (creationWorld != m_LocalWorld)
                        {
                            removedSystemCount++;
                            continue;
                        }

                        sd = new ScheduledSystemData(unmanagedSystem, creationWorld, groupIndex);
                    }

                    var handle = CreateSystemProxy(m_WorldProxy, updateIndex.IsManaged? sys?.World : m_LocalWorld, !updateIndex.IsManaged || sys?.World == m_LocalWorld);

                    if (sd.Recorder != null)
                        sd.Recorder.enabled = true;

                    m_WorldProxy.m_SystemData.Add(sd);

                    if (sys is ComponentSystemGroup childGroup)
                    {
                        workQueue.Enqueue(new GroupSystemInQueue { group = childGroup, index = handle.SystemIndex });
                    }
                }

                // Now update the system data and group handle with child info.
                var data = m_WorldProxy.m_SystemData[groupIndex];
                data.ChildIndex = firstChildIndex;
                data.ChildCount = numSystems - removedSystemCount;
                m_WorldProxy.m_SystemData[groupIndex] = data;
            }

            // Now go back through each system and update their dependencies
            for (var i = 0; i < m_WorldProxy.m_SystemData.Count; i++)
            {
                var data = m_WorldProxy.m_SystemData[i];
                data.UpdateBeforeIndices = SystemIndicesForDependenciesFromLocalWorld<UpdateBeforeAttribute>(data, m_WorldProxy);
                data.UpdateAfterIndices = SystemIndicesForDependenciesFromLocalWorld<UpdateAfterAttribute>(data, m_WorldProxy);
                m_WorldProxy.m_SystemData[i] = data;
            }

            m_WorldProxy.m_RootSystems.AddRange(m_WorldProxy.m_AllSystems.Where(s => rootTypes.Any(t => s.TypeName == t.Name)));

            m_WorldProxy.OnGraphChange();
        }

        SystemProxy CreateSystemProxy(WorldProxy worldProxy, World world, bool belongToCurrentWorld)
        {
            var thisIndex = worldProxy.m_AllSystems.Count;
            var systemProxy = new SystemProxy(worldProxy, thisIndex, world, belongToCurrentWorld);
            worldProxy.m_AllSystems.Add(systemProxy);
            return systemProxy;
        }

        struct GroupSystemInQueue
        {
            public ComponentSystemGroup group;
            public int index;
        }

        public unsafe void UpdateFrameData()
        {
            using (s_UpdateTransientDataFromLocalWorldMarker.Auto())
            {
                if (m_WorldProxy.m_AllSystems.Count != m_WorldProxy.m_FrameData.Count)
                    return;

                for (var i = 0; i < m_WorldProxy.m_AllSystems.Count; i++)
                {
                    var sys = m_WorldProxy.m_SystemData[i];
                    var state = GetStatePointer(sys);

                    SystemFrameData frameData = default;
                    if (state != null)
                    {
                        frameData.Enabled = state->Enabled;
                        frameData.IsRunning = state->ShouldRunSystem();
                        frameData.EntityCount = ComputeEntityCount(state);

                        if (sys.Recorder != null)
                        {
                            frameData.LastFrameRuntimeMilliseconds = (sys.Recorder.elapsedNanoseconds / 1e6f);
                        }
                    }

                    m_WorldProxy.m_FrameData[i] = frameData;
                }
            }
        }

        unsafe int ComputeEntityCount(SystemState* state)
        {
            var count = 0;
            var entityQueries = state->EntityQueries;
            for (var i = 0; i < entityQueries.Length; i++)
            {
                count += entityQueries[i].CalculateEntityCount();
            }

            return count;
        }

        unsafe SystemState* GetStatePointer(in ScheduledSystemData sys)
        {
            if ((sys.Category & SystemCategory.Unmanaged) == 0)
                return sys.Managed.m_StatePtr;

            var creationWorld = FindCreationWorld(sys.WorldSystemHandle.m_WorldSeqNo);
            if (creationWorld != null && creationWorld.IsCreated)
                return creationWorld.Unmanaged.ResolveSystemState(sys.WorldSystemHandle);

            return null;
        }

        int[] SystemIndicesForDependenciesFromLocalWorld<TAttribute>(in ScheduledSystemData systemData, WorldProxy worldProxy)
            where TAttribute : System.Attribute
        {
            if (systemData.ParentIndex == -1)
                return Array.Empty<int>();

            // Dependencies only matter within the same group
            var parentData = worldProxy.m_SystemData[systemData.ParentIndex];
            var baseChildIndex = parentData.ChildIndex;

            var systemType = GetLocalSystemType(systemData);
            var depTypes = SystemDependencyUtilities.GetSystemAttributes<TAttribute>(systemType);
            var slist = new List<int>();

            // TODO find a faster way to resolve this
            foreach (var depType in depTypes)
            {
                for (var ci = 0; ci < parentData.ChildCount; ci++)
                {
                    if (GetLocalSystemType(worldProxy.m_SystemData[baseChildIndex + ci]) == depType)
                    {
                        slist.Add(baseChildIndex + ci);
                        break;
                    }
                }
            }
            return slist.ToArray();
        }

        //
        // Misc
        //
        static World FindCreationWorld(uint worldSeqNo)
        {
            foreach (var world in World.All)
            {
                if (world.SequenceNumber == worldSeqNo)
                    return world;
            }

            return null;
        }

        static unsafe Type GetLocalSystemType(in ScheduledSystemData sd)
        {
            if (sd.Managed != null)
                return sd.Managed.GetType();

            Assert.IsTrue((sd.Category & SystemCategory.Unmanaged) != 0);
            var world = FindCreationWorld(sd.WorldSystemHandle.m_WorldSeqNo);
            return SystemBaseRegistry.GetStructType(world.Unmanaged.ResolveSystemState(sd.WorldSystemHandle)->UnmanagedMetaIndex);
        }
    }
}
