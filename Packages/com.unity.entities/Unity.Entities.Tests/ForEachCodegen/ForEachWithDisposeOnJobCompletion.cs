using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Unity.Entities.Tests.ForEachCodegen
{
    [TestFixture]
    public partial class ForEachWithDisposeOnCompletion : ECSTestsFixture
    {
        TestSystemType TestSystem;

        [SetUp]
        public void SetUp()
        {
            TestSystem = World.GetOrCreateSystemManaged<TestSystemType>();
        }

        struct CanContainDisposedStruct
        {
            public SupportsDisposeOnCompletion SupportsDisposeOnCompletion;
        }

        class CanContainDisposedClass
        {
            public SupportsDisposeOnCompletion SupportsDisposeOnCompletion = new SupportsDisposeOnCompletion();
        }

        struct SupportsDisposeOnCompletionJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            internal unsafe int* m_Ptr;
            internal Allocator m_Allocator;

            public unsafe void Execute()
            {
                *m_Ptr = 0;
                Memory.Unmanaged.Free(m_Ptr, m_Allocator);
            }
        }

        [NativeContainer]
        struct SupportsDisposeOnCompletion : IDisposable
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle m_Safety;
#endif
            Allocator m_Allocator;
            [NativeDisableUnsafePtrRestriction]
            public unsafe int* m_Ptr;

            public unsafe SupportsDisposeOnCompletion(Allocator allocator)
            {
                m_Allocator = allocator;
                m_Ptr = (int*)Memory.Unmanaged.Allocate(sizeof(int), 16, allocator);
                *m_Ptr = 54321;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_Safety = AtomicSafetyHandle.Create();
#endif
            }

            public unsafe void Dispose()
            {
                *m_Ptr = 0;
                Memory.Unmanaged.Free(m_Ptr, m_Allocator);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
                AtomicSafetyHandle.Release(m_Safety);
#endif
            }

            public unsafe JobHandle Dispose(JobHandle inputDeps)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.Release(m_Safety);
#endif
                return new SupportsDisposeOnCompletionJob { m_Ptr = m_Ptr, m_Allocator = m_Allocator }.Schedule(inputDeps);
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            public void CheckCanRead() => AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#else
            public void CheckCanRead() {}
#endif

            public unsafe bool HasBeenDisposed()
            {
                try
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckDeallocateAndThrow(m_Safety);
#endif
                    if (*m_Ptr == 54321)
                        return false;
                }
                catch
                {
                    return true;
                }
                return false;
            }

            public void Release()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (!HasBeenDisposed())
                    AtomicSafetyHandle.Release(m_Safety);
#endif
            }
        }

        public enum ScheduleType
        {
            Run,
            Schedule,
            ScheduleParallel
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Atomic Safety Handle for dispose checks")]
        public void DisposeOnCompletion_DisposesAtEnd([Values] ScheduleType scheduleType)
        {
            m_Manager.CreateEntity(typeof(EcsTestFloatData));
            var c = new SupportsDisposeOnCompletion(Allocator.Temp);
            try
            {
                Assert.DoesNotThrow(() => TestSystem.DisposeOnCompletion(c, scheduleType));
                Assert.IsTrue(c.HasBeenDisposed(), "Dispose has not been called");
            }
            finally
            {
                c.Release();
            }
        }

        [Test]
        public void DisposeNativeArray_DisposesAtEnd()
        {
            var testArray = new NativeArray<int>(100, Allocator.Temp);
            var isCreated = false;
            Assert.DoesNotThrow(() => isCreated = TestSystem.DisposeNativeArray(testArray));
            Assert.IsFalse(isCreated);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Atomic Safety Handle for dispose checks")]
        public void DisposeInsideStructOnJobCompletion_DisposesAtEnd([Values] ScheduleType scheduleType)
        {
            m_Manager.CreateEntity(typeof(EcsTestFloatData));
            var c = new CanContainDisposedStruct {SupportsDisposeOnCompletion = new SupportsDisposeOnCompletion(Allocator.Temp)};
            try
            {
                Assert.DoesNotThrow(() => TestSystem.DisposeInsideStructOnJobCompletion(c, scheduleType));
                Assert.IsTrue(c.SupportsDisposeOnCompletion.HasBeenDisposed(), "Dispose has not been called for contained struct");
            }
            finally
            {
                c.SupportsDisposeOnCompletion.Release();
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Atomic Safety Handle for dispose checks")]
        public void DisposeInsideClassOnJobCompletion_WithRun_DisposesAtEnd()
        {
            m_Manager.CreateEntity(typeof(EcsTestFloatData));
            var c = new CanContainDisposedClass {SupportsDisposeOnCompletion = new SupportsDisposeOnCompletion(Allocator.Temp)};
            try
            {
                Assert.DoesNotThrow(() => TestSystem.DisposeInsideClassOnJobCompletion_WithRun(c));
                Assert.IsTrue(c.SupportsDisposeOnCompletion.HasBeenDisposed(), "Dispose has not been called for contained struct");
            }
            finally
            {
                c.SupportsDisposeOnCompletion.Release();
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Atomic Safety Handle for dispose checks")]
        public void DisposeOnCompletion_WithStructuralChanges_Disposes()
        {
            m_Manager.CreateEntity(typeof(EcsTestFloatData));
            var c = new SupportsDisposeOnCompletion(Allocator.Temp);
            try
            {
                Assert.DoesNotThrow(() => TestSystem.DisposeOnCompletion_WithStructuralChanges(c));
                Assert.IsTrue(c.HasBeenDisposed(), "Dispose has not been called");
            }
            finally
            {
                c.Release();
            }
        }

        partial class TestSystemType : SystemBase
        {
            protected override void OnUpdate()
            {
            }

            public bool DisposeNativeArray(NativeArray<int> testArray)
            {
                Entities
                    .WithReadOnly(testArray)
                    .WithDisposeOnCompletion(testArray)
                    .ForEach((Entity entity) =>
                    {
                        var length = testArray.Length;
                    })
                    .Run();

                return testArray.IsCreated;
            }

            public void DisposeOnCompletion(SupportsDisposeOnCompletion c, ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.WithDisposeOnCompletion(c).ForEach((ref EcsTestFloatData _) => { c.CheckCanRead(); }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.WithDisposeOnCompletion(c).ForEach((ref EcsTestFloatData _) => { c.CheckCanRead(); }).Schedule(default).Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.WithReadOnly(c).WithDisposeOnCompletion(c).ForEach((ref EcsTestFloatData _) => { c.CheckCanRead(); }).ScheduleParallel(default).Complete();
                        break;
                }
            }

            public void DisposeInsideStructOnJobCompletion(CanContainDisposedStruct c, ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        Entities.WithDisposeOnCompletion(c).ForEach((ref EcsTestFloatData _) => { c.SupportsDisposeOnCompletion.CheckCanRead(); }).Run();
                        break;
                    case ScheduleType.Schedule:
                        Entities.WithDisposeOnCompletion(c).ForEach((ref EcsTestFloatData _) => { var temp = c; c.SupportsDisposeOnCompletion.CheckCanRead(); }).Schedule(default).Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        Entities.WithReadOnly(c).WithDisposeOnCompletion(c).ForEach((ref EcsTestFloatData _) => { var temp = c; c.SupportsDisposeOnCompletion.CheckCanRead(); }).Schedule(default).Complete();
                        break;
                }
            }

            public void DisposeInsideClassOnJobCompletion_WithRun(CanContainDisposedClass c)
            {
                Entities.WithoutBurst().WithDisposeOnCompletion(c).ForEach((ref EcsTestFloatData _) => { c.SupportsDisposeOnCompletion.CheckCanRead(); }).Run();
            }

            public void DisposeOnCompletion_WithStructuralChanges(SupportsDisposeOnCompletion c)
            {
                Entities.WithStructuralChanges().WithDisposeOnCompletion(c).ForEach((ref EcsTestFloatData _) => { c.CheckCanRead(); }).Run();
            }
        }
    }
}
