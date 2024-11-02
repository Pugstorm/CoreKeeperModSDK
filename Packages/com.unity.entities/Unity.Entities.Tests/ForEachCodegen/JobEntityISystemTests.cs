using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.Tests.InAnotherAssembly;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests.ForEachCodegen
{
    [TestFixture]
    public unsafe partial class JobEntityISystemTests : ECSTestsFixture
    {
        internal static Entity TestEntity;
        internal static Entity DisabledEntity;

        [SetUp]
        public void SetUp()
        {
            World.GetOrCreateSystem<JobEntityISystemTestsSystem>();

            var myArch = m_Manager.CreateArchetype(
                ComponentType.ReadWrite<EcsTestData>(),
                ComponentType.ReadWrite<EcsTestData2>(),
                ComponentType.ReadWrite<EcsTestSharedComp>(),
                ComponentType.ReadWrite<EcsTestSharedComp2>(),
                ComponentType.ReadWrite<EcsIntElement>(),
                ComponentType.ReadWrite<EcsTestTag>(),
                ComponentType.ReadWrite<EcsTestDataEnableable>(),
                ComponentType.ReadWrite<EcsTestTagEnableable>());

            TestEntity = m_Manager.CreateEntity(myArch);
            m_Manager.SetComponentData(TestEntity, new EcsTestData { value = 3 });
            m_Manager.SetComponentData(TestEntity, new EcsTestData2 { value0 = 4 });
            var buffer = m_Manager.GetBuffer<EcsIntElement>(TestEntity);
            buffer.Add(new EcsIntElement { Value = 18 });
            buffer.Add(new EcsIntElement { Value = 19 });
            m_Manager.SetSharedComponentManaged(TestEntity, new EcsTestSharedComp { value = 5 });
            m_Manager.SetSharedComponentManaged(TestEntity, new EcsTestSharedComp2 { value0 = 11, value1 = 13 });

            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(TestEntity, false);
            m_Manager.SetComponentEnabled<EcsTestTagEnableable>(TestEntity, false);

            DisabledEntity = m_Manager.CreateEntity(typeof(Disabled), typeof(EcsTestData3));
        }

        ref JobEntityISystemTestsSystem GetTestSystemUnsafe()
        {
            var systemHandle = World.GetExistingSystem<JobEntityISystemTestsSystem>();
            if (systemHandle == default)
                throw new System.InvalidOperationException("This system does not exist any more");
            return ref World.Unmanaged.GetUnsafeSystemRef<JobEntityISystemTestsSystem>(systemHandle);
        }

        ref SystemState GetSystemStateRef()
        {
            var systemHandle = World.GetExistingSystem<JobEntityISystemTestsSystem>();
            var statePtr = World.Unmanaged.ResolveSystemState(systemHandle);
            if (statePtr == null)
                throw new System.InvalidOperationException("No system state exists any more for this system");
            return ref *statePtr;
        }

        [Test]
        public void ToggleEnabled() => GetTestSystemUnsafe().ToggleEnabled(ref GetSystemStateRef());

        [Test]
        public void CheckEnabled() => GetTestSystemUnsafe().CheckEnabled(ref GetSystemStateRef());

        [Test]
        public void SimplestCaseWithRefWrappers() => GetTestSystemUnsafe().SimplestCaseWithRefWrappers(ref GetSystemStateRef());

        [Test]
        public void SimplestCase() => GetTestSystemUnsafe().SimplestCase(ref GetSystemStateRef());

        [Test]
        public void ScheduleJobUsingExtensionMethod() => GetTestSystemUnsafe().ScheduleJobUsingExtensionMethod(ref GetSystemStateRef());

        [Test]
        public void SimplestCaseInAnotherAssembly() => GetTestSystemUnsafe().SimplestCaseInAnotherAssembly(ref GetSystemStateRef());

        [Test]
        public void WithTagComponent() => GetTestSystemUnsafe().WithTagComponent(ref GetSystemStateRef());

        [Test]
        public void WithTagComponentRefParam() => GetTestSystemUnsafe().WithTagComponentRefParam(ref GetSystemStateRef());

        [Test]
        public void WithNone() => GetTestSystemUnsafe().WithNone(ref GetSystemStateRef());

        [Test]
        public void WithDisabled() => GetTestSystemUnsafe().WithDisabled(ref GetSystemStateRef());

        [Test]
        public void EnableDisabled() => GetTestSystemUnsafe().EnableDisabled(ref GetSystemStateRef());

        [Test]
        public void WithAbsent() => GetTestSystemUnsafe().WithAbsent(ref GetSystemStateRef());

        [Test]
        public void WithPresent() => GetTestSystemUnsafe().WithPresent(ref GetSystemStateRef());

        [Test]
        public void WithAny_DoesntExecute_OnEntityWithoutThatComponent() => GetTestSystemUnsafe().WithAny_DoesntExecute_OnEntityWithoutThatComponent(ref GetSystemStateRef());

        #region SharedComponent

        [Test]
        public void WithAllSharedComponent() => GetTestSystemUnsafe().WithAllSharedComponentData(ref GetSystemStateRef());

        [Test]
        public void SharedComponent() => GetTestSystemUnsafe().TestSharedComponent(ref GetSystemStateRef());
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void ManagedSharedComponent() => GetTestSystemUnsafe().TestManagedSharedComponent(ref GetSystemStateRef());

        [Test]
        public void JobEntityWithManagedField() => GetTestSystemUnsafe().JobEntityWithManagedField(ref GetSystemStateRef());

        [Test]
        public void JobEntityWithManagedFieldAndManagedComponent() => GetTestSystemUnsafe().JobEntityWithManagedFieldAndManagedComponent(ref GetSystemStateRef());
#endif

        #endregion

        [Test]
        public void WithEntityQueryOption_DisabledEntity() => GetTestSystemUnsafe().WithEntityQueryOption_DisabledEntity(ref GetSystemStateRef());

        [Test]
        public void AddToDynamicBuffer() => GetTestSystemUnsafe().AddToDynamicBuffer(ref GetSystemStateRef());

        [Test]
        public void ModifyDynamicBuffer() => GetTestSystemUnsafe().ModifyDynamicBuffer(ref GetSystemStateRef());

        [Test]
        public void IterateExistingDynamicBufferReadOnly() => GetTestSystemUnsafe().IterateExistingDynamicBufferReadOnly(ref GetSystemStateRef());

        [Test]
        public void IJobEntity_EntityParameter_NoWarnings() => GetTestSystemUnsafe().IJobEntity_EntityParameter_NoWarnings(ref GetSystemStateRef());

        [Test]
        public void Schedule_CombineDependencies_Works() => GetTestSystemUnsafe().Schedule_CombineDependencies_Works(ref GetSystemStateRef());

        [Test]
        public void EntityQueryDoesntUseDefaultQuery()
        {
            var system = World.GetOrCreateSystem<DefaultJobEntityQueryNotAddedAsQueryOfSystem>();
            system.Update(World.Unmanaged);
            var singleton = m_Manager.GetComponentData<DefaultJobEntityQueryNotAddedAsQueryOfSystem.Singleton>(system);
            Assert.False(singleton.HasRunOnUpdate);
        }

        // Todo: add back when IJE supports .DisposeOnCompletion like IJobChunk
        // [Test]
        // public void DisposeNativeArray_DisposesAtEnd() => GetTestSystemUnsafe().DisposeNativeArray(ref GetSystemStateRef());

        // Todo: impl when SystemAPI works for IJE
        // [Test]
        // public void ForEach_SystemStateAccessor_Matches() => GetTestSystemUnsafe().ForEach_SystemStateAccessor_Matches_SystemAPI(ref GetSystemStateRef());
    }

    [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
    partial struct ToggleEnabledJob : IJobEntity
    {
        void Execute(EnabledRefRW<EcsTestDataEnableable> enabledRef1, EnabledRefRW<EcsTestTagEnableable> enabledRefR2)
        {
            enabledRef1.ValueRW = !enabledRef1.ValueRO;
            enabledRefR2.ValueRW = !enabledRefR2.ValueRO;
        }
    }

    [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
    partial struct CheckEnabledJob : IJobEntity
    {
        void Execute(EnabledRefRO<EcsTestDataEnableable> enabledRef, ref EcsTestData data)
        {
            if (!enabledRef.ValueRO)
                data.value *= 2;
        }
    }

    partial struct SimplestCaseJobWithRefWrappers : IJobEntity
    {
        void Execute(RefRW<EcsTestData> e1, RefRO<EcsTestData2> e2) => e1.ValueRW.value += e2.ValueRO.value0;
    }
    partial struct SimplestCaseJob : IJobEntity
    {
        void Execute(ref EcsTestData e1, in EcsTestData2 e2) => e1.value += e2.value0;
    }

    partial struct SimpleEntityJob : IJobEntity
    {
        void Execute(Entity e) { }
    }

    [WithAll(typeof(EcsTestTag))]
    partial struct WithTagComponentJob : IJobEntity
    {
        void Execute(ref EcsTestData e1) => e1.value = 5;
    }

    partial struct WithTagComponentRefParamJob : IJobEntity
    {
        void Execute(ref EcsTestData e1, RefRO<EcsTestTag> tag)
        {
            e1.value = 5;
            Assert.DoesNotThrow(() => Debug.Log(tag.ValueRO));
        }
    }

    [WithNone(typeof(EcsTestData2))]
    partial struct WithNoneJob : IJobEntity
    {
        public int one;
        void Execute(ref EcsTestData e1) => e1.value += one;
    }

    [WithDisabled(typeof(EcsTestDataEnableable))]
    partial struct WithDisabledJob : IJobEntity
    {
        public int disabledValue;

        void Execute(ref EcsTestData e1) => e1.value = disabledValue;
    }

    [WithDisabled(typeof(EcsTestDataEnableable))]
    partial struct EnableDisabledJob : IJobEntity
    {
        void Execute(EnabledRefRW<EcsTestDataEnableable> e1) => e1.ValueRW = true;
    }

    [WithAbsent(typeof(EcsTestData3))]
    partial struct WithAbsentJob : IJobEntity
    {
        public int absentValue;

        void Execute(ref EcsTestData e1) => e1.value = absentValue;
    }

    [WithPresent(typeof(EcsTestDataEnableable))]
    partial struct WithPresentJob : IJobEntity
    {
        public int presentValue;

        void Execute(ref EcsTestData e1) => e1.value = presentValue;
    }

    [WithAny(typeof(EcsTestData3))]
    partial struct WithAny_DoesntExecute_OnEntityWithoutThatComponentJob : IJobEntity
    {
        public int one;
        void Execute(ref EcsTestData e1) => e1.value += one;
    }

    [WithOptions(EntityQueryOptions.IncludeDisabledEntities)]
    partial struct WithEntityQueryOption_DisabledEntityJob : IJobEntity
    {
        public NativeReference<Entity> disabledEntityReference;
        void Execute(Entity entity, in EcsTestData3 data3) => disabledEntityReference.Value = entity;
    }

    partial struct AddToDynamicBufferJob : IJobEntity
    {
        void Execute(ref EcsTestData e1, ref DynamicBuffer<EcsIntElement> buf) => buf.Add(4);
    }

    partial struct ModifyDynamicBufferJob : IJobEntity
    {
        void Execute(ref DynamicBuffer<EcsIntElement> buf)
        {
            for (int i = 0; i < buf.Length; ++i)
                buf[i] = buf[i].Value * 2;
        }
    }

    partial struct IterateExistingDynamicBufferReadOnlyJob : IJobEntity
    {
        void Execute(ref EcsTestData e1, in DynamicBuffer<EcsIntElement> buf)
        {
            e1.value = 0;
            for (var i = 0; i != buf.Length; i++)
                e1.value += buf[i].Value;
        }
    }

    partial struct DisposeNativeArrayJob : IJobEntity
    {
        [ReadOnly][DeallocateOnJobCompletion] public NativeArray<int> testArray;
        void Execute(Entity e) {}
    }

    partial struct JobEntityISystemTestsSystem : ISystem
    {
        public EntityQuery m_StoredQuery;

        public void ToggleEnabled(ref SystemState state)
        {
            var job = new ToggleEnabledJob();
            job.Run();

            Assert.IsTrue(state.EntityManager.IsComponentEnabled<EcsTestDataEnableable>(JobEntityISystemTests.TestEntity));
            Assert.IsTrue(state.EntityManager.IsComponentEnabled<EcsTestTagEnableable>(JobEntityISystemTests.TestEntity));
        }

        public void CheckEnabled(ref SystemState state)
        {
            var job = new CheckEnabledJob();
            job.Run();

            Assert.AreEqual(6, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        public void SimplestCaseWithRefWrappers(ref SystemState state)
        {
            new SimplestCaseJobWithRefWrappers().Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(7, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        public void SimplestCase(ref SystemState state)
        {
            new SimplestCaseJob().Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(7, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        public void ScheduleJobUsingExtensionMethod(ref SystemState state)
        {
            var job = new SimplestCaseJob();

            var handle = IJobEntityExtensions.ScheduleParallel(job, state.Dependency);
            handle.Complete();

            Assert.AreEqual(7, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        public void SimplestCaseInAnotherAssembly(ref SystemState state)
        {
            state.EntityManager.AddComponentData(JobEntityISystemTests.TestEntity, new EcsTestDataInAnotherAssembly { value = 3 });
            state.EntityManager.AddComponentData(JobEntityISystemTests.TestEntity, new EcsTestData2InAnotherAssembly { value0 = 4 });
            new SimplestCaseJobInAnotherAssembly().Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(7, state.EntityManager.GetComponentData<EcsTestDataInAnotherAssembly>(JobEntityISystemTests.TestEntity).value);
        }


        public void WithTagComponent(ref SystemState state)
        {
            new WithTagComponentJob().Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(5, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        public void WithTagComponentRefParam(ref SystemState state)
        {
            new WithTagComponentRefParamJob().Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(5, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        public void WithNone(ref SystemState state)
        {
            new WithNoneJob{one = 1}.Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(3, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        public void WithDisabled(ref SystemState state)
        {
            new WithDisabledJob{ disabledValue = 1 }.Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(1, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        public void EnableDisabled(ref SystemState state)
        {
            Assert.AreEqual(1, SystemAPI.QueryBuilder().WithDisabled<EcsTestDataEnableable>().Build().CalculateEntityCount());
            Assert.AreEqual(0, SystemAPI.QueryBuilder().WithAll<EcsTestDataEnableable>().Build().CalculateEntityCount());
            new EnableDisabledJob{}.Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(0, SystemAPI.QueryBuilder().WithDisabled<EcsTestDataEnableable>().Build().CalculateEntityCount());
            Assert.AreEqual(1, SystemAPI.QueryBuilder().WithAll<EcsTestDataEnableable>().Build().CalculateEntityCount());
        }

        public void WithAbsent(ref SystemState state)
        {
            new WithAbsentJob{ absentValue= 1}.Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(1, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        public void WithPresent(ref SystemState state)
        {
            // Test with the present component disabled
            state.EntityManager.SetComponentEnabled<EcsTestDataEnableable>(JobEntityISystemTests.TestEntity, false);
            new WithPresentJob{ presentValue= 1}.Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(1, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
            // test again with the present component enabled
            state.EntityManager.SetComponentEnabled<EcsTestDataEnableable>(JobEntityISystemTests.TestEntity, true);
            new WithPresentJob{ presentValue= 2}.Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(2, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        public void WithAny_DoesntExecute_OnEntityWithoutThatComponent(ref SystemState state)
        {
            new WithAny_DoesntExecute_OnEntityWithoutThatComponentJob { one = 1 }.Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(3, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        #region SharedComponent
        [WithAll(typeof(EcsTestSharedComp))]
        partial struct WithAllSharedComponentDataJob : IJobEntity
        {
            public int one;
            void Execute(ref EcsTestData e1) => e1.value += one;
        }

        public void WithAllSharedComponentData(ref SystemState state)
        {
            new WithAllSharedComponentDataJob { one = 1 }.Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(4, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        partial struct SharedComponentJob : IJobEntity
        {
            void Execute(ref EcsTestData data, in EcsTestSharedComp e1) => data.value += e1.value;
        }

        public void TestSharedComponent(ref SystemState state)
        {
            new SharedComponentJob().Run();
            Assert.AreEqual(3+5, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        #if !UNITY_DISABLE_MANAGED_COMPONENTS
        partial struct ManagedSharedComponentJob : IJobEntity
        {
            void Execute(ref EcsTestData data, in ManagedSharedData1 e1) => data.value += e1.value.Item1 + e1.value.Item2;
        }

        public void TestManagedSharedComponent(ref SystemState state)
        {
            state.EntityManager.AddSharedComponentManaged(JobEntityISystemTests.TestEntity, new ManagedSharedData1 {value = new Tuple<int, int>(2, 3)});
            new ManagedSharedComponentJob().Run();
            Assert.AreEqual(3+5, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        public void JobEntityWithManagedField(ref SystemState state)
        {
            try
            {
                new JobEntityWithManagedFieldJob { JobName = "MyThrustJob" }.Run();
            }
            catch (InvalidOperationException ex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.That(ex.Message, Is.EqualTo("JobEntityWithManagedFieldJob.JobData.JobName is not a value type. Job structs may not contain any reference types."));
#else
                Assert.That(ex.Message, Is.EqualTo("JobEntityWithManagedFieldJob is not a value type. Job structs may not contain any reference types."));
#endif
            }
        }

        partial struct JobEntityWithManagedFieldJob : IJobEntity
        {
            public string JobName;
            public void Execute() => Assert.AreEqual("MyThrustJob", JobName);
        }

        public void JobEntityWithManagedFieldAndManagedComponent(ref SystemState state)
        {
            new JobEntityWithManagedFieldAndManagedComponentJob { JobName = "MyThrustJob" }.Run();
        }

        // Note if a job has a managed component it will run without the job system
        partial struct JobEntityWithManagedFieldAndManagedComponentJob : IJobEntity
        {
            public string JobName;
            public void Execute(EcsTestManagedComponent _) => Assert.AreEqual("MyThrustJob", JobName);
        }

        #endif

        #endregion

        public void WithEntityQueryOption_DisabledEntity(ref SystemState state)
        {
            using var disabledEntityRef = new NativeReference<Entity>(state.WorldUpdateAllocator);
            new WithEntityQueryOption_DisabledEntityJob{disabledEntityReference = disabledEntityRef}.Run();
            Assert.AreEqual(JobEntityISystemTests.DisabledEntity, disabledEntityRef.Value);
        }

        public void AddToDynamicBuffer(ref SystemState state)
        {
            new AddToDynamicBufferJob().Schedule(state.Dependency).Complete();
            var buffer = state.EntityManager.GetBuffer<EcsIntElement>(JobEntityISystemTests.TestEntity);
            Assert.AreEqual(3, buffer.Length);
            CollectionAssert.AreEqual(new[] {18, 19, 4}, buffer.Reinterpret<int>().AsNativeArray());
        }

        public void ModifyDynamicBuffer(ref SystemState state)
        {
            new ModifyDynamicBufferJob().Schedule();
            state.Dependency.Complete();
            var buffer = state.EntityManager.GetBuffer<EcsIntElement>(JobEntityISystemTests.TestEntity);
            CollectionAssert.AreEqual(new[] {18 * 2, 19 * 2}, buffer.Reinterpret<int>().AsNativeArray());
        }

        public void IterateExistingDynamicBufferReadOnly(ref SystemState state)
        {
            new IterateExistingDynamicBufferReadOnlyJob().Schedule();
            state.Dependency.Complete();
            Assert.AreEqual(18 + 19, state.EntityManager.GetComponentData<EcsTestData>(JobEntityISystemTests.TestEntity).value);
        }

        public void IJobEntity_EntityParameter_NoWarnings(ref SystemState state)
        {
            new SimpleEntityJob().Schedule();
            state.Dependency.Complete();
            LogAssert.NoUnexpectedReceived();
        }

        public void Schedule_CombineDependencies_Works(ref SystemState state)
        {
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(ref state);
            using var dataList =
                query.ToComponentDataListAsync<EcsTestData>(state.WorldUpdateAllocator, out var gatherJob);
            state.Dependency = new SimplestCaseJob
            {
            }.Schedule(JobHandle.CombineDependencies(state.Dependency, gatherJob));
            state.Dependency.Complete();
        }

        // Todo: add back when IJE supports .DisposeOnCompletion like IJobChunk
        // public void DisposeNativeArray(ref SystemState state)
        // {
        //     var testArray = new NativeArray<int>(100, Allocator.TempJob);
        //     new DisposeNativeArrayJob{testArray=testArray}.Schedule().Complete();
        //     Assert.IsFalse(testArray.IsCreated);
        // }

        // Todo: impl when SystemAPI works for IJE
        // public void ForEach_SystemStateAccessor_Matches_SystemAPI(ref SystemState systemState)
        // {
        //     var elapsedTime = systemState.Time.ElapsedTime;
        //     var timesMatch = false;
        //
        //     SystemAPI.Entities
        //         .ForEach(() =>
        //         {
        //             var innerElapsedTime = SystemAPI.Time.ElapsedTime;
        //             if (innerElapsedTime == elapsedTime)
        //                 timesMatch = true;
        //         })
        //         .Run();
        //
        //     Assert.IsTrue(timesMatch);
        // }
    }

    [RequireMatchingQueriesForUpdate]
    partial struct DefaultJobEntityQueryNotAddedAsQueryOfSystem : ISystem
    {
        partial struct JobWithPassingDefaultQuery : IJobEntity
        {
            void Execute(EcsTestData data) {}
        }

        public struct Singleton : IComponentData
        {
            public bool HasRunOnUpdate;
        }

        public void OnCreate(ref SystemState state) =>
            state.EntityManager.AddComponentData(state.SystemHandle, new Singleton());

        public void OnUpdate(ref SystemState state)
        {
            // Only set if passed in query is present, not if IJobEntity query is.
            state.EntityManager.SetComponentData(state.SystemHandle, new Singleton{HasRunOnUpdate = true});
            new JobWithPassingDefaultQuery().Schedule(SystemAPI.QueryBuilder().WithAll<EcsTestData, EcsTestData4>().Build());
        }
    }
}
