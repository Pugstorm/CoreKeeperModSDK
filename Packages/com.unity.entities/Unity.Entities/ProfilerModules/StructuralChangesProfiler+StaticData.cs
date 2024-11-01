#if ENABLE_PROFILER
using System;
using Unity.Profiling;

namespace Unity.Entities
{
    partial class StructuralChangesProfiler
    {
        struct StaticData : IDisposable
        {
            readonly Guid m_Guid;
            readonly ProfilerCategory m_Category;

            public TimeCounter CreateEntityCounter;
            public TimeCounter DestroyEntityCounter;
            public TimeCounter AddComponentCounter;
            public TimeCounter RemoveComponentCounter;
            public TimeCounter SetSharedComponentCounter;

            public Guid Guid => m_Guid;
            public ProfilerCategory Category => m_Category;

            public StaticData(int dummy = 0)
            {
                m_Guid = new Guid("7e866afa654f4469aef462540c0192fa");
                m_Category = new ProfilerCategory(k_CategoryName);
                CreateEntityCounter = new TimeCounter(k_CreateEntityCounterName);
                DestroyEntityCounter = new TimeCounter(k_DestroyEntityCounterName);
                AddComponentCounter = new TimeCounter(k_AddComponentCounterName);
                RemoveComponentCounter = new TimeCounter(k_RemoveComponentCounterName);
                SetSharedComponentCounter = new TimeCounter(k_SetSharedComponentCounterName);
            }

            public void Dispose()
            {
            }
        }
    }
}
#endif
