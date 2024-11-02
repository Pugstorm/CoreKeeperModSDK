#if ENABLE_PROFILER
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.LowLevel;
using UnityEngine.Profiling;

namespace Unity.Entities
{
    partial class EntitiesProfiler
    {
        unsafe struct StaticData : IDisposable
        {
            bool m_Initialized;
            Guid m_Guid;
            UnsafeList<WorldData> m_WorldsData;
            UnsafeList<SystemData> m_SystemsData;
            UnsafeList<ArchetypeData> m_ArchetypesData;
            SpinLock m_ArchetypesDataLock;
            bool m_LastProfilerEnabled;

            public Guid Guid => m_Guid;

            public StaticData(int worldCount, int systemCount, int archetypeCount)
            {
                m_Guid = new Guid("5ac699399f504fa189b6a3758e252685");
                m_WorldsData = new UnsafeList<WorldData>(worldCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                m_SystemsData = new UnsafeList<SystemData>(systemCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                m_ArchetypesData = new UnsafeList<ArchetypeData>(archetypeCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                m_ArchetypesDataLock = new SpinLock();
                m_LastProfilerEnabled = false;
                m_Initialized = true;
            }

            public void Dispose()
            {
                m_WorldsData.Dispose();
                m_SystemsData.Dispose();
                m_ArchetypesData.Dispose();
                m_Initialized = false;
            }

            public void AddWorld(World world)
            {
                if (!m_Initialized || !m_LastProfilerEnabled || !Profiler.enabled)
                    return;

                m_WorldsData.Add(new WorldData(world));
            }

            public void AddSystem(SystemTypeIndex systemType, in SystemHandle systemHandle)
            {
                if (!m_Initialized || !m_LastProfilerEnabled || !Profiler.enabled)
                    return;

                //todo: check if system handle is not valid (e.g. because system is destroyed)
                m_SystemsData.Add(new SystemData(systemType, systemHandle));
            }

            public void AddArchetype(Archetype* archetype)
            {
                if (!m_Initialized || !m_LastProfilerEnabled || !Profiler.enabled)
                    return;

                m_ArchetypesDataLock.Acquire();
                try
                {
                    m_ArchetypesData.Add(new ArchetypeData(archetype));
                }
                finally
                {
                    m_ArchetypesDataLock.Release();
                }
            }

            public void Flush()
            {
                if (!m_Initialized)
                    return;

                var enabled = Profiler.enabled;

                // If profiler was not enabled last time, we must re-send all session data
                if (!m_LastProfilerEnabled && enabled)
                {
                    // If we fail to get session data, postpone to next frame
                    if (!ResetSessionMetaData())
                        return;
                }
                m_LastProfilerEnabled = enabled;

                if (!enabled)
                    return;

                FlushSessionMetaData(in m_Guid, (int)DataTag.WorldData, ref m_WorldsData);
                FlushSessionMetaData(in m_Guid, (int)DataTag.SystemData, ref m_SystemsData);

                m_ArchetypesDataLock.Acquire();
                try
                {
                    FlushSessionMetaData(in m_Guid, (int)DataTag.ArchetypeData, ref m_ArchetypesData);
                }
                finally
                {
                    m_ArchetypesDataLock.Release();
                }
            }

            bool ResetSessionMetaData()
            {
                m_ArchetypesDataLock.Acquire();
                try
                {
                    m_WorldsData.Clear();
                    m_SystemsData.Clear();
                    m_ArchetypesData.Clear();

                    for (var i = 0; i < World.All.Count; ++i)
                    {
                        var world = World.All[i];
                        if (!world.IsCreated)
                            continue;

                        // If world is in exclusive transaction, postpone to next frame
                        if (!world.EntityManager.CanBeginExclusiveEntityTransaction())
                            return false;

                        m_WorldsData.Add(new WorldData(world));
                        using (var systems = world.Unmanaged.GetAllSystems(Allocator.Temp))
                        {
                            for (var systemIter = 0; systemIter < systems.Length; ++systemIter)
                            {
                                var system = systems[systemIter];
                                var systemType = world.Unmanaged.ResolveSystemState(system)->m_SystemTypeIndex;
                                if (systemType == SystemTypeIndex.Null)
                                    continue;

                                m_SystemsData.Add(new SystemData(systemType, system));
                            }
                        }

                        using (var archetypes = new NativeList<EntityArchetype>(Allocator.Temp))
                        {
                            world.EntityManager.GetAllArchetypes(archetypes);
                            for (var archetypeIter = 0; archetypeIter < archetypes.Length; ++archetypeIter)
                                m_ArchetypesData.Add(new ArchetypeData(archetypes[archetypeIter].Archetype));
                        }
                    }
                }
                finally
                {
                    m_ArchetypesDataLock.Release();
                }
                return true;
            }
        }
    }
}
#endif
