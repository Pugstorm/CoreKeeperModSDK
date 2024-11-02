#if ENABLE_PROFILER
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;

namespace Unity.Entities
{
    partial class StructuralChangesProfiler
    {
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "ENABLE_PROFILER")]
        public struct Recorder : IDisposable
        {
            readonly struct Scope
            {
                public readonly SystemHandle ExecutingSystem;
                public readonly long StartTimestamp;
                public readonly ulong WorldSequenceNumber;
                public readonly StructuralChangeType Type;

                public Scope(long timeStamp, StructuralChangeType type, ulong worldSequenceNumber, SystemHandle executingSystem)
                {
                    StartTimestamp = timeStamp;
                    Type = type;
                    WorldSequenceNumber = worldSequenceNumber;
                    ExecutingSystem = executingSystem;
                }
            }

            UnsafeStack<Scope> m_ScopeStack;
            UnsafeList<StructuralChangeData> m_Changes;

            public Recorder(Allocator allocator)
            {
                m_ScopeStack = default;
                m_Changes = default;
                Initialize(allocator);
            }

            public void Dispose()
            {
                m_ScopeStack.Dispose();
                m_Changes.Dispose();
            }

            public void Initialize(Allocator allocator)
            {
                m_ScopeStack = new UnsafeStack<Scope>(8, allocator);
                m_Changes = new UnsafeList<StructuralChangeData>(16, allocator);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Begin(StructuralChangeType type, in WorldUnmanaged world)
            {
                if (!s_Initialized) // Enabled test is done at call site
                    return;

                var now = ProfilerUnsafeUtility.Timestamp;
                world.GetInfo(out var sequenceNumber, out var executingSystem);
                m_ScopeStack.Push(new Scope(now, type, sequenceNumber, executingSystem));
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void End()
            {
                if (!s_Initialized || m_ScopeStack.IsEmpty) // Enabled test is done at call site
                    return;

                var now = ProfilerUnsafeUtility.Timestamp;
                var scope = m_ScopeStack.Top();
                var elapsed = GetElapsedNanoseconds(now - scope.StartTimestamp);

                switch (scope.Type)
                {
                    case StructuralChangeType.CreateEntity:
                        s_Data.CreateEntityCounter.Value += elapsed;
                        break;
                    case StructuralChangeType.DestroyEntity:
                        s_Data.DestroyEntityCounter.Value += elapsed;
                        break;
                    case StructuralChangeType.AddComponent:
                        s_Data.AddComponentCounter.Value += elapsed;
                        break;
                    case StructuralChangeType.RemoveComponent:
                        s_Data.RemoveComponentCounter.Value += elapsed;
                        break;
                    case StructuralChangeType.SetSharedComponent:
                        s_Data.SetSharedComponentCounter.Value += elapsed;
                        break;
                    default:
                        throw new NotImplementedException($"Structural change type {scope.Type} not implemented.");
                }

                m_Changes.Add(new StructuralChangeData(elapsed, scope.Type, scope.WorldSequenceNumber, scope.ExecutingSystem));
                m_ScopeStack.Pop();
            }

            public void Flush()
            {
                EntitiesProfiler.FlushFrameMetaData(s_Data.Guid, 0, ref m_Changes);
            }

            static long GetElapsedNanoseconds(long elapsed)
            {
                var conversionRatio = ProfilerUnsafeUtility.TimestampToNanosecondsConversionRatio;
                return elapsed * conversionRatio.Numerator / conversionRatio.Denominator;
            }
        }
    }
}
#endif
