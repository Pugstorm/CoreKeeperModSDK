#if ENABLE_PROFILER
using Unity.Profiling;

namespace Unity.Entities
{
    partial class StructuralChangesProfiler
    {
        readonly struct TimeCounter
        {
            readonly ProfilerCounterValue<long> m_Counter;

            public long Value
            {
                get => m_Counter.Value;
                set => m_Counter.Value = value;
            }

            public TimeCounter(string name)
            {
                m_Counter = new ProfilerCounterValue<long>(s_Data.Category, name, ProfilerMarkerDataUnit.TimeNanoseconds,
                    ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);
            }
        }
    }
}
#endif
