using System;
using NUnit.Framework;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Entities.Tests.ForEachCodegen
{
    [TestFixture]
    public partial class JobEntityCodeGenTests : ECSTestsFixture
    {
        public enum ScheduleType
        {
            Run,
            Schedule,
            ScheduleParallel
        }

        const int k_EcsTestDataValue = 3;
        const int k_EcsTestData2Value = 4;
        const int k_EcsTestSharedCompValue = 5;
        const int k_DynamicBufferFirstItem = 18;
        const int k_DynamicBufferSecondItem = 19;

        MyTestSystem m_TestSystem;
        static Entity s_TestEntity;

        [SetUp]
        public void SetUp()
        {
            m_TestSystem = World.GetOrCreateSystemManaged<MyTestSystem>();

            var entityArchetype = m_Manager.CreateArchetype(
                typeof(EcsTestData), typeof(EcsTestData2),
                typeof(EcsTestSharedComp), typeof(EcsIntElement),
                typeof(EcsTestTag));

            s_TestEntity = m_Manager.CreateEntity(entityArchetype);
            m_Manager.SetComponentData(s_TestEntity, new EcsTestData { value = k_EcsTestDataValue });
            m_Manager.SetComponentData(s_TestEntity, new EcsTestData2 { value0 = k_EcsTestData2Value });
            m_Manager.SetSharedComponentManaged(s_TestEntity, new EcsTestSharedComp { value = k_EcsTestSharedCompValue });

            var buffer = m_Manager.GetBuffer<EcsIntElement>(s_TestEntity);
            buffer.Add(k_DynamicBufferFirstItem);
            buffer.Add(k_DynamicBufferSecondItem);
        }

        #region AddTwoComponents

        [Test]
        public void AddTwoComponents([Values] ScheduleType scheduleType) => m_TestSystem.AddTwoComponents(scheduleType);

        #endregion

        #region AssignUniformValue

        [Test]
        public void AssignValue([Values] ScheduleType scheduleType) => m_TestSystem.AssignValue(scheduleType);

        #endregion

        #region WithTagParam

        [Test]
        public void WithTagParam([Values] ScheduleType scheduleType) => m_TestSystem.WithTagParam(scheduleType);

        #endregion

        #region WithAll

        public enum JobEntityTestsWithAll
        {
            StaticTag,
            StaticMultiple,
            DynamicTag
        }

        [Test]
        public void WithAll([Values] ScheduleType scheduleType, [Values] JobEntityTestsWithAll jobEntityTestsWithAll)
            => m_TestSystem.WithAll(scheduleType, jobEntityTestsWithAll);

        #endregion

        #region WithNone

        [Test]
        public void WithNone([Values] ScheduleType scheduleType, [Values] bool isStatic) => m_TestSystem.WithNone(scheduleType, isStatic);

        #endregion

        #region WithAny

        [Test]
        public void WithAny([Values] ScheduleType scheduleType, [Values] bool isStatic) => m_TestSystem.WithAny(scheduleType, isStatic);

        #endregion

        #region SharedComponent

        [Test]
        public void WithAllSharedComponent([Values] ScheduleType scheduleType, [Values] bool isStatic)
            => m_TestSystem.WithAllSharedComponent(scheduleType, isStatic);

        [Test]
        public void WithSharedComponentFilter([Values] ScheduleType scheduleType) => m_TestSystem.WithSharedComponentFilterDynamic(scheduleType);

        [Test]
        public void SharedComponent([Values] ScheduleType scheduleType) => m_TestSystem.TestSharedComponent(scheduleType);
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void ManagedSharedComponent([Values] ScheduleType scheduleType) => m_TestSystem.TestManagedSharedComponent(scheduleType);
#endif

        #endregion

        #region ChangeFilter

        public enum JobEntityTestsChangeFilter
        {
            StaticStructAttribute,
            StaticParameterAttribute,
            Dynamic
        }

        [Test]
        public void WithChangeFilter(
            [Values] ScheduleType scheduleTypeBeforeChange,
            [Values] ScheduleType scheduleTypeAfterChange,
            [Values] JobEntityTestsChangeFilter jobEntityTestsChangeFilter)
            => m_TestSystem.WithChangeFilter(scheduleTypeBeforeChange, scheduleTypeAfterChange, jobEntityTestsChangeFilter);

        [Test]
        public void WithFilterButNotQueryDoesntThrow([Values] ScheduleType scheduleType, [Values] bool isStatic)
            => m_TestSystem.WithFilterButNotQueryDoesntThrow(scheduleType, isStatic);

        #endregion

        #region EntityQueryOptions

        public enum JobEntityTestsEntityQueryOptions
        {
            StaticSingleAttribute,
            StaticMultipleAttribute
        }

        [Test]
        public void WithOptions([Values] ScheduleType scheduleType, [Values] JobEntityTestsEntityQueryOptions jobEntityTestsEntityQueryOptions)
            => m_TestSystem.WithOptions(scheduleType, jobEntityTestsEntityQueryOptions);

        #endregion

        #region Combinatorial

        [Test]
        public void WithJobAndThenJobEntity([Values] ScheduleType scheduleType) => m_TestSystem.WithJobAndThenJobEntity(scheduleType);

        [Test]
        public void WithTypeHandle([Values] ScheduleType scheduleType) => m_TestSystem.WithTypeHandle(scheduleType);

        [Test]
        public void MultipleInNestedUsing([Values] ScheduleType scheduleTypeAssign, [Values] ScheduleType scheduleTypeIncrement)
            => m_TestSystem.MultipleInNestedUsing(scheduleTypeAssign, scheduleTypeIncrement);

        #endregion

        #region Buffer

        [Test]
        public void AddToDynamicBuffer([Values] ScheduleType scheduleType) => m_TestSystem.AddToDynamicBuffer(scheduleType);

        [Test]
        public void ModifyDynamicBuffer([Values] ScheduleType scheduleType) => m_TestSystem.MultiplyAllDynamicBufferValues(scheduleType);

        [Test]
        public void SumAllOfBufferIntoEcsTestData([Values] ScheduleType scheduleType) => m_TestSystem.SumAllOfBufferIntoEcsTestData(scheduleType);

        #endregion

        #region EntityIndexInQuery

        [Test]
        public void EntityIndexInQuery([Values] ScheduleType scheduleType) => m_TestSystem.EntityIndexInQuery(scheduleType);

        [Test]
        public void ChunkIndexInQuery([Values] ScheduleType scheduleType) => m_TestSystem.ChunkIndexInQuery(scheduleType);

        [Test]
        public void EntityIndexInChunk([Values] ScheduleType scheduleType) => m_TestSystem.EntityIndexInChunk(scheduleType);

        #endregion


        #region ManagedComponents
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void UnityEngineComponent() => m_TestSystem.UnityEngineComponent();

        [Test]
        public void UnityEngineObject() => m_TestSystem.UnityEngineGameObject();

        [Test]
        public void UnityEngineScriptableObject() => m_TestSystem.UnityEngineScriptableObject();
        [Test]
        public void ManyManagedComponents() => m_TestSystem.ManyManagedComponents();
#endif
#endregion

#region Safety
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void JobDebuggerSafetyThrows([Values] ScheduleType scheduleType) => m_TestSystem.JobDebuggerSafetyThrows(scheduleType);
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
        [Test]
        public void UserDefinedQuerySafetyThrows([Values] ScheduleType scheduleType) => m_TestSystem.UserDefinedQuerySafetyThrows(scheduleType);
        [Test]
        public void UserDefinedQuerySafetyDoesNotThrow([Values] ScheduleType scheduleType) => m_TestSystem.UserDefinedQuerySafetyDoesNotThrow(scheduleType);
#endif

#endregion

#region EnableableComponents
        [Test]
        public void EnableableComponents([Values(ScheduleType.Run, ScheduleType.Schedule)] ScheduleType scheduleType) => m_TestSystem.EnableableComponents(scheduleType);

#endregion

#region DuplicateComponents
        [Test]
        public void DuplicateComponents([Values(ScheduleType.Run, ScheduleType.Schedule)] ScheduleType scheduleType) => m_TestSystem.DuplicateComponents(scheduleType);
#endregion

#region EntityIndexInQuery_ArrayWrites

        [Test]
        public void EntityIndexInQuery_WriteToArray_NoSafetyError() => m_TestSystem.EntityIndexInQuery_WriteToArray();

        [Test]
        public void EntityIndexInQuery_WriteToArray_Enableable_NoSafetyError() =>
            m_TestSystem.EntityIndexInQuery_WriteToArray_Enableable();

#endregion

#region Scheduling

    // Ensures DOTS-6550 won't happen again (auto assign state.Dependency when Explicit handle is provided)
    [Test]
    public void AutoDependencyOnlyAddedWhenExplicitJobHandlePassed() => World.GetOrCreateSystem<MyTestSystem.AutoDependencyOnlyAddedWhenExplicitJobHandlePassedSystem>().Update(World.Unmanaged);

    [Test]
    public void UsingCodeGenInsideAScheduleObjectInit() => m_TestSystem.UsingCodeGenInsideAScheduleObjectInit();

    [Test]
    public void ExtensionMethodToInvoke() => m_TestSystem.ExtensionMethodToInvoke();
#endregion

#region Interfaces
    [Test]
    public void JobEntityChunkBeginEnd() => m_TestSystem.TestJobEntityChunkBeginEnd();
    [Test]
    public void OnChunkEnd() => m_TestSystem.TestOnChunkEnd();
#endregion

        partial class MyTestSystem : SystemBase
        {
            protected override void OnCreate() {}

#region AddTwoComponents
            partial struct AddTwoComponentsJob : IJobEntity
            {
                public void Execute(ref EcsTestData e1, in EcsTestData2 e2) => e1.value += e2.value0;
            }

            public void AddTwoComponents(ScheduleType scheduleType)
            {
                var job = new AddTwoComponentsJob();

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_EcsTestDataValue + k_EcsTestData2Value, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);

            }
#endregion

#region AssignUniformValue

            partial struct AssignValueJob : IJobEntity
            {
                public int Value;
                public void Execute(ref EcsTestData e1, in EcsTestData2 e2) => e1.value = Value;
            }

            public void AssignValue(ScheduleType scheduleType)
            {
                const int valueToAssign = 7;

                var job = new AssignValueJob { Value = valueToAssign };
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(valueToAssign, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

#endregion

#region WithTagParam

            partial struct WithTagParamJob : IJobEntity
            {
                public int Value;
                public void Execute(ref EcsTestData e1, in EcsTestTag _) => e1.value = Value;
            }

            public void WithTagParam(ScheduleType scheduleType)
            {
                const int valueToAssign = 5;
                var job = new WithTagParamJob {Value = valueToAssign};

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(valueToAssign, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

#endregion

#region WithAll

            public void WithAll(ScheduleType scheduleType, JobEntityTestsWithAll jobEntityTestsWithAll)
            {
                switch (jobEntityTestsWithAll)
                {
                    case JobEntityTestsWithAll.StaticTag:
                        WithAllStaticTag(scheduleType);
                        break;
                    case JobEntityTestsWithAll.StaticMultiple:
                        WithAllStaticMultiple(scheduleType);
                        break;
                    case JobEntityTestsWithAll.DynamicTag:
                        WithAllDynamic(scheduleType);
                        break;
                }
            }

            [WithAll(typeof(EcsTestSharedComp))]
            [WithAll(typeof(EcsTestTag), typeof(EcsTestData2))]
            partial struct WithAllStaticMultipleJob : IJobEntity
            {
                public int Value;
                public void Execute(ref EcsTestData e1) => e1.value = Value;
            }

            void WithAllStaticMultiple(ScheduleType scheduleType)
            {
                const int valueToAssign = 5;

                var entityNotMatchingQuery = EntityManager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

                var job = new WithAllStaticTagJob {Value = valueToAssign};

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }
                Assert.AreEqual(valueToAssign, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
                Assert.AreEqual(default(EcsTestData), EntityManager.GetComponentData<EcsTestData>(entityNotMatchingQuery));
            }

            [WithAll(typeof(EcsTestTag))]
            partial struct WithAllStaticTagJob : IJobEntity
            {
                public int Value;
                public void Execute(ref EcsTestData e1, in EcsTestData2 e2) => e1.value = Value;
            }

            void WithAllStaticTag(ScheduleType scheduleType)
            {
                const int valueToAssign = 5;
                var job = new WithAllStaticTagJob {Value = valueToAssign};
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }
                Assert.AreEqual(valueToAssign, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

            partial struct WithAllDynamicJob : IJobEntity
            {
                public int Value;
                public void Execute(ref EcsTestData e1, in EcsTestData2 e2) => e1.value = Value;
            }

            void WithAllDynamic(ScheduleType scheduleType)
            {
                var query = GetEntityQuery(typeof(EcsTestTag), typeof(EcsTestData), typeof(EcsTestData2));

                const int valueToAssign = 5;
                var job = new WithAllDynamicJob {Value = valueToAssign};
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run(query);
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule(query);
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel(query);
                        Dependency.Complete();
                        break;
                }
                Assert.AreEqual(valueToAssign, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }
#endregion

#region WithNone

            public void WithNone(ScheduleType scheduleType, bool isStatic)
            {
                if(isStatic)
                    WithNoneStatic(scheduleType);
                else
                    WithNoneDynamic(scheduleType);
            }

            [WithNone(typeof(EcsTestData2))]
            partial struct WithNoneStaticJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute(ref EcsTestData e1) => e1.value += IncrementBy;
            }

            void WithNoneStatic(ScheduleType scheduleType)
            {
                var job = new WithNoneStaticJob { IncrementBy = 1 };

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_EcsTestDataValue, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value); // Nothing changed
            }

            partial struct WithNoneDynamicJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute(ref EcsTestData e1) => e1.value += IncrementBy;
            }

            void WithNoneDynamic(ScheduleType scheduleType)
            {
                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<EcsTestData>()
                    .WithNone<EcsTestData2>()
                    .Build(this);
                var job = new WithNoneDynamicJob { IncrementBy = 1 };

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run(query);
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule(query);
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel(query);
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_EcsTestDataValue, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value); // Nothing changed
            }

#endregion

#region WithAny

            public void WithAny(ScheduleType scheduleType, bool isStatic)
            {
                if (isStatic)
                    WithAnyStatic(scheduleType);
                else
                    WithAnyDynamic(scheduleType);
            }

            [WithAny(typeof(EcsTestData3))]
            partial struct WithAnyStaticJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute(ref EcsTestData e1) => e1.value += IncrementBy;
            }

            void WithAnyStatic(ScheduleType scheduleType)
            {
                var job = new WithAnyStaticJob { IncrementBy = 1 };


                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_EcsTestDataValue, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value); // Nothing changed
            }

            partial struct WithAnyDynamicJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute(ref EcsTestData e1) => e1.value += IncrementBy;
            }

            void WithAnyDynamic(ScheduleType scheduleType)
            {
                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<EcsTestData>()
                    .WithAnyRW<EcsTestData3>()
                    .Build(this);
                var job = new WithAnyDynamicJob { IncrementBy = 1 };

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run(query);
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule(query);
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel(query);
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_EcsTestDataValue, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value); // Nothing changed
            }

#endregion

#region SharedComponent

            public void WithAllSharedComponent(ScheduleType scheduleType, bool isStatic)
            {
                if (isStatic)
                    WithAllSharedComponentStatic(scheduleType);
                else
                    WithAllSharedComponentDynamic(scheduleType);
            }

            partial struct WithAllSharedComponentDynamicJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute(ref EcsTestData e1) => e1.value += IncrementBy;
            }

            void WithAllSharedComponentDynamic(ScheduleType scheduleType)
            {
                const int incrementBy = 1;

                var query = GetEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp));
                var job = new WithAllSharedComponentDynamicJob { IncrementBy = incrementBy };

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run(query);
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule(query);
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel(query);
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_EcsTestDataValue + incrementBy, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

            [WithAll(typeof(EcsTestSharedComp))]
            partial struct WithAllSharedComponentStaticJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute(ref EcsTestData e1) => e1.value += IncrementBy;
            }

            void WithAllSharedComponentStatic(ScheduleType scheduleType)
            {
                const int incrementBy = 1;

                var job = new WithAllSharedComponentStaticJob {IncrementBy = incrementBy};

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_EcsTestDataValue + incrementBy, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

            partial struct WithSharedComponentFilterDynamicJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute(ref EcsTestData e1) => e1.value += IncrementBy;
            }

            public void WithSharedComponentFilterDynamic(ScheduleType scheduleType)
            {
                const int incrementBy = 1;

                var query = GetEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp));
                query.SetSharedComponentFilterManaged(new EcsTestSharedComp(k_EcsTestSharedCompValue));

                var job = new WithSharedComponentFilterDynamicJob { IncrementBy = incrementBy };

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_EcsTestDataValue + incrementBy, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

            partial struct SharedComponentJob : IJobEntity
            {
                void Execute(ref EcsTestData data, in EcsTestSharedComp e1) => data.value += e1.value;
            }

            public void TestSharedComponent(ScheduleType scheduleType)
            {
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        new SharedComponentJob().Run();
                        break;
                    case ScheduleType.Schedule:
                        new SharedComponentJob().Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        new SharedComponentJob().ScheduleParallel();
                        Dependency.Complete();
                        break;
                }
                Assert.AreEqual(3+5, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
            partial struct ManagedSharedComponentJob : IJobEntity
            {
                void Execute(ref EcsTestData data, in ManagedSharedData1 e1) => data.value += e1.value.Item1 + e1.value.Item2;
            }

            public void TestManagedSharedComponent(ScheduleType scheduleType)
            {
                EntityManager.AddSharedComponentManaged(s_TestEntity, new ManagedSharedData1 {value = new Tuple<int, int>(2, 3)});
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        new ManagedSharedComponentJob().Run();
                        Assert.AreEqual(3+5, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
                        break;
                    case ScheduleType.Schedule:
                        new ManagedSharedComponentJob().Schedule();
                        Dependency.Complete();
                        Assert.AreEqual(3+5, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
                        break;
                    case ScheduleType.ScheduleParallel:
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        var ex = Assert.Throws<InvalidOperationException>(() =>
                        {
                            new ManagedSharedComponentJob().ScheduleParallel();
                        });
                        Assert.AreEqual(ex.Message, "Tried to ScheduleParallel a job with a managed execute signature. Please use .Run or .Schedule instead.");
#endif
                        Dependency.Complete();
                        break;
                }

            }
#endif

#endregion

#region ChangeFilter

            public void WithChangeFilter(ScheduleType scheduleTypeBeforeChange, ScheduleType scheduleTypeAfterChange, JobEntityTestsChangeFilter jobEntityTestsChangeFilter)
            {
                switch (jobEntityTestsChangeFilter)
                {
                    case JobEntityTestsChangeFilter.StaticStructAttribute:
                        WithChangeFilterStaticStructAttribute(scheduleTypeBeforeChange, scheduleTypeAfterChange);
                        break;

                    case JobEntityTestsChangeFilter.StaticParameterAttribute:
                        WithChangeFilterStaticParameterAttribute(scheduleTypeBeforeChange, scheduleTypeAfterChange);
                        break;

                    case JobEntityTestsChangeFilter.Dynamic:
                        WithChangeFilterDynamic(scheduleTypeBeforeChange, scheduleTypeAfterChange);
                        break;
                }
            }

            [WithChangeFilter(typeof(EcsTestData))]
            partial struct WithChangeFilterStaticStructAttributeJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute(ref EcsTestData e1) => e1.value += IncrementBy;
            }

            void WithChangeFilterStaticStructAttribute(ScheduleType scheduleTypeBeforeChange, ScheduleType scheduleTypeAfterChange)
            {
                const int incrementBy = 1;

                var job = new WithChangeFilterStaticStructAttributeJob { IncrementBy = incrementBy };

                switch (scheduleTypeBeforeChange)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }
                AfterUpdateVersioning();
                BeforeUpdateVersioning();
                // Should not run
                switch (scheduleTypeAfterChange)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_EcsTestDataValue + incrementBy, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

            partial struct WithChangeFilterStaticParameterAttributeJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute([WithChangeFilter] ref EcsTestData e1) => e1.value += IncrementBy;
            }

            void WithChangeFilterStaticParameterAttribute(ScheduleType scheduleTypeBeforeChange, ScheduleType scheduleTypeAfterChange)
            {
                const int incrementBy = 1;

                var job = new WithChangeFilterStaticParameterAttributeJob { IncrementBy = incrementBy };

                switch (scheduleTypeBeforeChange)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }
                AfterUpdateVersioning();
                BeforeUpdateVersioning();
                // Should not run
                switch (scheduleTypeAfterChange)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_EcsTestDataValue + incrementBy, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

            partial struct WithChangeFilterDynamicJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute(ref EcsTestData e1) => e1.value += IncrementBy;
            }

            void WithChangeFilterDynamic(ScheduleType scheduleTypeBeforeChange, ScheduleType scheduleTypeAfterChange)
            {
                const int incrementBy = 1;

                var query = GetEntityQuery(typeof(EcsTestData));
                query.SetChangedVersionFilter(typeof(EcsTestData));
                var job = new WithChangeFilterDynamicJob { IncrementBy = incrementBy };

                switch (scheduleTypeBeforeChange)
                {
                    case ScheduleType.Run:
                        job.Run(query);
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule(query);
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel(query);
                        Dependency.Complete();
                        break;
                }

                AfterUpdateVersioning();
                BeforeUpdateVersioning();

                switch (scheduleTypeAfterChange)
                {
                    case ScheduleType.Run:
                        job.Run(query);
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule(query);
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel(query);
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_EcsTestDataValue + incrementBy, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

            public void WithFilterButNotQueryDoesntThrow(ScheduleType scheduleType, bool isStatic)
            {
                if (isStatic)
                    WithFilterButNotQueryStatic(scheduleType);
                else
                    WithFilterButNotQueryDynamic(scheduleType);
            }

            [WithChangeFilter(typeof(LocalTransform))]
            partial struct WithFilterButNotQueryStaticJob : IJobEntity { public void Execute(Entity _) {} }
            void WithFilterButNotQueryStatic(ScheduleType scheduleType)
            {
                Assert.DoesNotThrow(() =>
                {
                    var job = new WithFilterButNotQueryStaticJob();
                    switch (scheduleType)
                    {
                        case ScheduleType.Run:
                            job.Run();
                            break;
                        case ScheduleType.Schedule:
                            job.Schedule();
                            Dependency.Complete();
                            break;
                        case ScheduleType.ScheduleParallel:
                            job.ScheduleParallel();
                            Dependency.Complete();
                            break;
                    }
                });
            }

            partial struct WithFilterButNotQueryDynamicJob : IJobEntity { public void Execute(Entity _) {} }

            void WithFilterButNotQueryDynamic(ScheduleType scheduleType)
            {
                Assert.DoesNotThrow(() =>
                {
                    var query = new EntityQueryBuilder(Allocator.Temp)
                        .WithAllRW<LocalTransform>()
                        .Build(this);
                    query.SetChangedVersionFilter(typeof(LocalTransform));

                    var job = new WithFilterButNotQueryDynamicJob();

                    switch (scheduleType)
                    {
                        case ScheduleType.Run:
                            job.Run(query);
                            break;
                        case ScheduleType.Schedule:
                            job.Schedule(query);
                            Dependency.Complete();
                            break;
                        case ScheduleType.ScheduleParallel:
                            job.ScheduleParallel(query);
                            Dependency.Complete();
                            break;
                    }
                });
            }

#endregion

#region EntityQueryOptions

            [WriteGroup(typeof(EcsTestData))]
            struct EcsTestDataWriteGroup : IComponentData {}

            public void WithOptions(ScheduleType scheduleType, JobEntityTestsEntityQueryOptions jobEntityTestsEntityQueryOptions)
            {
                switch (jobEntityTestsEntityQueryOptions)
                {
                    case JobEntityTestsEntityQueryOptions.StaticSingleAttribute:
                        WithOptionsStaticSingleAttribute(scheduleType);
                        break;
                    case JobEntityTestsEntityQueryOptions.StaticMultipleAttribute:
                        WithOptionsStaticMultipleAttribute(scheduleType);
                        break;
                }
            }

            [WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.FilterWriteGroup, EntityQueryOptions.IncludePrefab)]
            partial struct WithOptionsStaticSingleAttributeJob : IJobEntity
            {
                public int AssignValue;
                public void Execute(ref EcsTestData testData) => testData.value = AssignValue;
            }

            void WithOptionsStaticSingleAttribute(ScheduleType scheduleType)
            {
                const int assignValue = 2;

                EntityManager.AddComponent(s_TestEntity, new ComponentTypeSet(typeof(Disabled), typeof(Prefab)));
                var writeGroupEntity = EntityManager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataWriteGroup));

                var job = new WithOptionsStaticSingleAttributeJob {AssignValue = assignValue};

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(default(EcsTestData),EntityManager.GetComponentData<EcsTestData>(writeGroupEntity));
                Assert.AreEqual(assignValue,EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

            [WithOptions(EntityQueryOptions.IncludePrefab)]
            [WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.FilterWriteGroup)]
            partial struct WithOptionsStaticMultipleAttributeJob : IJobEntity
            {
                public int AssignValue;
                public void Execute(ref EcsTestData testData) => testData.value = AssignValue;
            }

            void WithOptionsStaticMultipleAttribute(ScheduleType scheduleType)
            {
                const int assignValue = 2;

                EntityManager.AddComponent(s_TestEntity, new ComponentTypeSet(typeof(Disabled), typeof(Prefab)));
                var writeGroupEntity = EntityManager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataWriteGroup));

                var job = new WithOptionsStaticMultipleAttributeJob {AssignValue = assignValue};

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(default(EcsTestData),EntityManager.GetComponentData<EcsTestData>(writeGroupEntity));
                Assert.AreEqual(assignValue,EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

#endregion

#region Combinatorial
            partial struct WithJobAndThenJobEntityJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute(ref EcsTestData e1) => e1.value += IncrementBy;
            }

            public void WithJobAndThenJobEntity(ScheduleType scheduleType)
            {
                var incrementBy = 3;
                const int overwrittenIncrement = 1;

                Job.WithCode(() => { incrementBy = overwrittenIncrement; }).Run();

                var job = new WithJobAndThenJobEntityJob { IncrementBy = incrementBy };

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_EcsTestDataValue + overwrittenIncrement, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

            partial struct ComponentTypeHandleJob : IJobEntity
            {
                [ReadOnly] public ComponentTypeHandle<EcsTestData2> testDataTypeHandle;
                public void Execute(ref EcsTestData data)
                {
                    data.value = (int) testDataTypeHandle.GlobalSystemVersion;
                }
            }

            public void WithTypeHandle(ScheduleType scheduleType)
            {
                var job = new ComponentTypeHandleJob
                {
                    testDataTypeHandle = GetComponentTypeHandle<EcsTestData2>(true)
                };

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual((int)EntityManager.GlobalSystemVersion, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

            partial struct MultipleInNestedUsingAssignJob : IJobEntity
            {
                public int Value;
                public void Execute(ref EcsTestData e1, in EcsTestData2 e2) => e1.value = Value;
            }

            partial struct MultipleInNestedUsingIncrementJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute(ref EcsTestData e1) => e1.value += IncrementBy;
            }

            public void MultipleInNestedUsing(ScheduleType scheduleTypeAssign, ScheduleType scheduleTypeIncrement)
            {
                const int valueToAssign = 5;
                const int incrementBy = 5;

                using (var refStartPos = CollectionHelper.CreateNativeArray<int>(10, World.UpdateAllocator.ToAllocator))
                {
                    using (var refEndPos = CollectionHelper.CreateNativeArray<int>(10, World.UpdateAllocator.ToAllocator))
                    {
                        var assignJob = new MultipleInNestedUsingAssignJob { Value = refEndPos[0] + refStartPos[0] + valueToAssign };
                        var incrementJob = new MultipleInNestedUsingIncrementJob { IncrementBy = incrementBy };

                        switch (scheduleTypeAssign)
                        {
                            case ScheduleType.Run:
                                assignJob.Run();
                                break;
                            case ScheduleType.Schedule:
                                assignJob.Schedule();
                                Dependency.Complete();
                                break;
                            case ScheduleType.ScheduleParallel:
                                assignJob.ScheduleParallel();
                                Dependency.Complete();
                                break;
                        }

                        switch (scheduleTypeIncrement)
                        {
                            case ScheduleType.Run:
                                incrementJob.Run();
                                break;
                            case ScheduleType.Schedule:
                                incrementJob.Schedule();
                                Dependency.Complete();
                                break;
                            case ScheduleType.ScheduleParallel:
                                incrementJob.ScheduleParallel();
                                Dependency.Complete();
                                break;
                        }
                    }
                }

                Assert.AreEqual(valueToAssign + incrementBy, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

#endregion

#region Buffer

            partial struct AddItemToDynamicBuffer : IJobEntity
            {
                public int AddThisToBuffer;
                public void Execute(DynamicBuffer<EcsIntElement> dynamicBuffer) => dynamicBuffer.Add(AddThisToBuffer);
            }

            public void AddToDynamicBuffer(ScheduleType scheduleType)
            {
                const int value = 4;

                var job = new AddItemToDynamicBuffer { AddThisToBuffer = value };
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                var buffer = EntityManager.GetBuffer<EcsIntElement>(s_TestEntity);

                Assert.AreEqual(3, buffer.Length);
                CollectionAssert.AreEqual(new[] {k_DynamicBufferFirstItem, k_DynamicBufferSecondItem, value}, buffer.Reinterpret<int>().AsNativeArray());
            }

            partial struct MultiplyAllDynamicBufferValuesJob : IJobEntity
            {
                public int Multiplier;
                public void Execute(ref EcsTestData e1, DynamicBuffer<EcsIntElement> dynamicBuffer)
                {
                    for (var i = 0; i < dynamicBuffer.Length; ++i)
                        dynamicBuffer[i] = dynamicBuffer[i].Value * Multiplier;
                }
            }

            public void MultiplyAllDynamicBufferValues(ScheduleType scheduleType)
            {
                const int multiplier = 2;

                var job = new MultiplyAllDynamicBufferValuesJob { Multiplier = multiplier };

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                var buffer = EntityManager.GetBuffer<EcsIntElement>(s_TestEntity);
                CollectionAssert.AreEqual(new[] {k_DynamicBufferFirstItem * multiplier, k_DynamicBufferSecondItem * multiplier}, buffer.Reinterpret<int>().AsNativeArray());
            }

            static int SumBufferElements(DynamicBuffer<EcsIntElement> buf)
            {
                var total = 0;
                for (var i = 0; i < buf.Length; i++)
                    total += buf[i].Value;
                return total;
            }

            partial struct SumAllDynamicBufferValues : IJobEntity
            {
                public void Execute(ref EcsTestData e1, DynamicBuffer<EcsIntElement> dynamicBuffer) => e1.value = SumBufferElements(dynamicBuffer);
            }

            public void SumAllOfBufferIntoEcsTestData(ScheduleType scheduleType)
            {
                var job = new SumAllDynamicBufferValues();

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                Assert.AreEqual(k_DynamicBufferFirstItem + k_DynamicBufferSecondItem, EntityManager.GetComponentData<EcsTestData>(s_TestEntity).value);
            }

#endregion

#region Indexes

            partial struct CompareEntityQueryIndex : IJobEntity
            {
                [NativeDisableParallelForRestriction] public NativeArray<int> Successes;
                [NativeSetThreadIndex] int m_NativeThreadIndex;
                public void Execute([Unity.Entities.EntityIndexInQuery]int entityIndexInQuery, in EcsTestData value) => Successes[m_NativeThreadIndex] += entityIndexInQuery == value.value ? 1 : 0;
            }

            public void EntityIndexInQuery(ScheduleType scheduleType)
            {
                var entityArchetype = EntityManager.CreateArchetype(typeof(EcsTestData));

                using var entities = EntityManager.CreateEntity(entityArchetype, 10, Allocator.Temp);
                for (var index = 0; index < entities.Length; index++)
                    EntityManager.SetComponentData(entities[index], new EcsTestData {value = index+1});

#if UNITY_2022_2_14F1_OR_NEWER
                int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
                int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
                var job = new CompareEntityQueryIndex{Successes = CollectionHelper.CreateNativeArray<int>(maxThreadCount, EntityManager.World.UpdateAllocator.ToAllocator) };

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                // No LINQ as this test should also work for DOTS Runtime
                var sum = 0;
                for (var i = 0; i < job.Successes.Length; i++)
                    sum += job.Successes[i];

                Assert.AreEqual(10,sum);
                job.Successes.Dispose();
            }


            [WithAll(typeof(EcsTestDataEnableable))]
            public partial struct EntityIndexInChunkJob : IJobEntity
            {
                [NativeDisableParallelForRestriction] public NativeArray<int> Array;
                [NativeSetThreadIndex] int threadIndex;
                void Execute([EntityIndexInChunk] int index) => Array[threadIndex]+=index;
            }

            public void EntityIndexInChunk(ScheduleType scheduleType)
            {
                using var entities = EntityManager.CreateEntity(EntityManager.CreateArchetype(typeof(EcsTestDataEnableable)), 1000, Allocator.Temp);
                for (var i = 0; i < entities.Length; i+=4)
                    EntityManager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);

#if UNITY_2022_2_14F1_OR_NEWER
                int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
                int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
                var job = new EntityIndexInChunkJob {Array = CollectionHelper.CreateNativeArray<int>(maxThreadCount, World.UpdateAllocator.ToAllocator)};
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }
                var sum = 0;
                foreach (var b in job.Array)
                    sum += b;
                Assert.AreEqual(47064, sum);
                EntityManager.DestroyEntity(entities);
            }

            public partial struct ChunkIndexInQueryJob : IJobEntity
            {
                [NativeDisableParallelForRestriction] public NativeArray<bool> Array;
                void Execute([ChunkIndexInQuery] int index, ref EcsTestData d) => Array[index] = true;
            }

            public void ChunkIndexInQuery(ScheduleType scheduleType) {
                var entities = EntityManager.CreateEntity(EntityManager.CreateArchetype(typeof(EcsTestData)), 1000, Allocator.Temp);
                var chunkCount = GetEntityQuery(typeof(EcsTestData)).CalculateChunkCount();
                var job = new ChunkIndexInQueryJob {Array = CollectionHelper.CreateNativeArray<bool>(chunkCount, World.UpdateAllocator.ToAllocator)};

                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                    case ScheduleType.ScheduleParallel:
                        job.ScheduleParallel();
                        Dependency.Complete();
                        break;
                }

                var passed = true;
                // Don't make linq cause then no Dots Runtime
                foreach (var val in job.Array)
                    passed &= val;
                Assert.True(passed);

                job.Array.Dispose();
                entities.Dispose();
            }

#endregion

#region ManagedComponents
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            public partial struct UnityEngineComponentJob : IJobEntity
            {
                void Execute(Transform transform) => transform.position = Vector3.up;
            }

            public void UnityEngineComponent()
            {
                var go = new GameObject("Original");
                var ghostEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentObject(ghostEntity, go.transform);
                new UnityEngineComponentJob().Run();
                Assert.AreEqual(go.transform.position, Vector3.up);
            }

            public partial struct UnityEngineGameObjectJob : IJobEntity
            {
                void Execute(GameObject go) => go.name = "Changed";
            }

            public void UnityEngineGameObject()
            {
                var go = new GameObject("Original");
                var ghostEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentObject(ghostEntity, go);
                new UnityEngineGameObjectJob().Run();
                Assert.AreEqual(go.name, "Changed");
            }

            public class TestScriptableObject : ScriptableObject
            {
                public int value;
            }

            public partial struct UnityEngineScriptableObjectJob : IJobEntity
            {
                void Execute(TestScriptableObject so) => so.value = 1;
            }

            public void UnityEngineScriptableObject()
            {
                var so = ScriptableObject.CreateInstance<TestScriptableObject>();
                var ghostEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentObject(ghostEntity, so);
                new UnityEngineScriptableObjectJob().Run();
                Assert.AreEqual(so.value, 1);
            }
            public partial struct ManyManagedComponentsJob : IJobEntity
            {
                void Execute(EcsTestManagedComponent t0, EcsTestManagedComponent2 t1, EcsTestManagedComponent3 t2, EcsTestManagedComponent4 t3)
                {
                    Assert.AreEqual("SomeString", t0.value);
                    Assert.AreEqual("SomeString2", t1.value2);
                    Assert.AreEqual("SomeString3", t2.value3);
                    Assert.AreEqual("SomeString4", t3.value4);
                }
            }

            public void ManyManagedComponents()
            {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });
                EntityManager.AddComponentData(entity, new EcsTestManagedComponent2 { value2 = "SomeString2" });
                EntityManager.AddComponentData(entity, new EcsTestManagedComponent3 { value3 = "SomeString3" });
                EntityManager.AddComponentData(entity, new EcsTestManagedComponent4 { value4 = "SomeString4" });

                new ManyManagedComponentsJob().Run();
            }
#endif
#endregion

#region Safety
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            partial struct UserDefinedQuerySafetyJob : IJobEntity
            {
                // MyAspect requires EcsTestData1 and optionally EcsTestData2
                // YourAspect requires EcsTestData3 and EcsTestData4
                public void Execute(MyAspect myAspect, YourAspect yourAspect, EcsTestData5 ecsTestData5)
                {
                }
            }

            public void UserDefinedQuerySafetyThrows(ScheduleType scheduleType)
            {
                var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
                var userDefinedQuery = entityQueryBuilder.WithAll<EcsTestData6>().Build(this);

                Assert.Throws<InvalidOperationException>(() =>
                {
                    switch (scheduleType)
                    {
                        case ScheduleType.Run:
                            new UserDefinedQuerySafetyJob().Run(userDefinedQuery);
                            break;
                        case ScheduleType.Schedule:
                            new UserDefinedQuerySafetyJob().Schedule(userDefinedQuery);
                            break;
                        case ScheduleType.ScheduleParallel:
                            new UserDefinedQuerySafetyJob().ScheduleParallel(userDefinedQuery);
                            break;
                    }
                });
                Dependency.Complete();

                entityQueryBuilder.Dispose();
            }

            partial struct UserDefinedQuerySafetyJob_ScheduledWithReadOnlyData : IJobEntity
            {
                public void Execute(ref EcsTestData _)
                {
                }
            }

            public void UserDefinedQuerySafetyDoesNotThrow(ScheduleType scheduleType)
            {
                var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
                var userDefinedQuery = entityQueryBuilder.WithAll<EcsTestData>().Build(this);

                Assert.DoesNotThrow(() =>
                {
                    switch (scheduleType)
                    {
                        case ScheduleType.Run:
                            new UserDefinedQuerySafetyJob_ScheduledWithReadOnlyData().Run(userDefinedQuery);
                            break;
                        case ScheduleType.Schedule:
                            new UserDefinedQuerySafetyJob_ScheduledWithReadOnlyData().Schedule(userDefinedQuery);
                            break;
                        case ScheduleType.ScheduleParallel:
                            new UserDefinedQuerySafetyJob_ScheduledWithReadOnlyData().ScheduleParallel(userDefinedQuery);
                            break;
                    }
                });
                Dependency.Complete();

                entityQueryBuilder.Dispose();
            }
#endif
            partial struct JobDebuggerSafetyThrowsJob : IJobEntity
            {
                public int IncrementBy;
                public void Execute(ref EcsTestData e1) => e1.value += IncrementBy;
            }

            public void JobDebuggerSafetyThrows(ScheduleType scheduleType)
            {
                var jobHandle = new JobDebuggerSafetyThrowsJob{IncrementBy = 1}.Schedule(Dependency);

                Assert.Throws<InvalidOperationException>(() =>
                {
                    switch (scheduleType)
                    {
                        case ScheduleType.Run:
                            new JobDebuggerSafetyThrowsJob {IncrementBy = 1}.Run();
                            break;
                        case ScheduleType.Schedule:
                            new JobDebuggerSafetyThrowsJob {IncrementBy = 1}.Schedule();
                            break;
                        case ScheduleType.ScheduleParallel:
                            new JobDebuggerSafetyThrowsJob {IncrementBy = 1}.ScheduleParallel();
                            break;
                    }
                });
                Dependency.Complete();
                jobHandle.Complete();
            }
#endregion

#region EnableableComponents

            partial struct EnableableComponentsJob : IJobEntity
            {
                public NativeReference<int> Sum;
                public void Execute(ref EcsTestDataEnableable e1) => Sum.Value++;
            }

            public void EnableableComponents(ScheduleType scheduleType)
            {
                // Create 50 enabled entities of 100 query matches
                using var entities = CollectionHelper.CreateNativeArray<Entity>(100, World.UpdateAllocator.ToAllocator);
                EntityManager.CreateEntity(EntityManager.CreateArchetype(typeof(EcsTestDataEnableable)), entities);
                for (int i = 0; i < entities.Length; i+=2)
                    EntityManager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);

                // Init and Schedule job
                using var sum = new NativeReference<int>(World.UpdateAllocator.ToAllocator);
                var job = new EnableableComponentsJob {Sum = sum};
                switch (scheduleType)
                {
                    case ScheduleType.Run:
                        job.Run();
                        break;
                    case ScheduleType.Schedule:
                        job.Schedule();
                        Dependency.Complete();
                        break;
                }

                // Asserts
                Assert.AreEqual(50,sum.Value);
                EntityManager.DestroyEntity(entities);
            }

#endregion

#region DuplicateComponents

        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        partial struct DuplicateComponentsJob : IJobEntity
        {
            public void Execute(RefRW<EcsTestDataEnableable> e1, EnabledRefRW<EcsTestDataEnableable> e2)
            {
                if (e2.ValueRO)
                {
                    e1.ValueRW.value++;
                    e2.ValueRW = false;
                }
            }
        }
        public void DuplicateComponents(ScheduleType scheduleType)
        {
            using var entities = CollectionHelper.CreateNativeArray<Entity>(100, World.UpdateAllocator.ToAllocator);
            EntityManager.CreateEntity(EntityManager.CreateArchetype(typeof(EcsTestDataEnableable)), entities);

            for (int i = 0; i < entities.Length; i+=2)
                EntityManager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);

            var job = new DuplicateComponentsJob();
            switch (scheduleType)
            {
                case ScheduleType.Run:
                    job.Run();
                    break;
                case ScheduleType.Schedule:
                    job.Schedule();
                    Dependency.Complete();
                    break;
            }

            for (var index = 0; index < entities.Length; index++)
            {
                var entity = entities[index];

                Assert.IsFalse(EntityManager.IsComponentEnabled<EcsTestDataEnableable>(entity));
                Assert.AreEqual(index % 2 == 0 ? 0 : 1, EntityManager.GetComponentData<EcsTestDataEnableable>(entity).value);
            }

            EntityManager.DestroyEntity(entities);
        }

#endregion


#region EntityIndexInQuery_ArrayWrites

            partial struct EntityIndexInQuery_WriteToArray_Job : IJobEntity
            {
                public NativeArray<float> OutValues;
                public void Execute([Unity.Entities.EntityIndexInQuery] int entityIndexInQuery, in EcsTestFloatData e1)
                {
                    OutValues[entityIndexInQuery] = e1.Value;
                }
            }

            public void EntityIndexInQuery_WriteToArray()
            {
                var archetype = EntityManager.CreateArchetype(typeof(EcsTestFloatData));
                int entityCount = 1000;
                using var entities = EntityManager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
                for (int i = 0; i < entityCount; ++i)
                {
                    EntityManager.SetComponentData(entities[i], new EcsTestFloatData{Value = i});
                }
                using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestFloatData>().Build(EntityManager);
                Assert.AreEqual(entityCount, query.CalculateEntityCount());
                using var outputValues =
                    CollectionHelper.CreateNativeArray<float>(entityCount, World.UpdateAllocator.ToAllocator);

                new EntityIndexInQuery_WriteToArray_Job { OutValues = outputValues }.ScheduleParallel(query, default).Complete();

                for (int i = 0, count=outputValues.Length; i < count; ++i)
                {
                    Assert.AreEqual((float)i, outputValues[i]);
                }
                EntityManager.DestroyEntity(entities);
            }

            partial struct EntityIndexInQuery_WriteToArray_Enableable_Job : IJobEntity
            {
                public NativeArray<int> OutValues;
                public void Execute([Unity.Entities.EntityIndexInQuery] int entityIndexInQuery, in EcsTestDataEnableable e1)
                {
                    OutValues[entityIndexInQuery] = e1.value;
                }
            }

            public void EntityIndexInQuery_WriteToArray_Enableable()
            {
                var archetype = EntityManager.CreateArchetype(typeof(EcsTestDataEnableable));
                int entityCount = 1000;
                using var entities = EntityManager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
                int matchingEntityCount = 0;
                for (int i = 0; i < entityCount; ++i)
                {
                    if ((i % 10) == 0)
                    {
                        EntityManager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                        EntityManager.SetComponentData(entities[i], new EcsTestDataEnableable(-i));
                        continue;
                    }
                    EntityManager.SetComponentData(entities[i], new EcsTestDataEnableable(matchingEntityCount++));
                }
                using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestDataEnableable>().Build(EntityManager);
                using var outputValues =
                    CollectionHelper.CreateNativeArray<int>(query.CalculateEntityCount(), World.UpdateAllocator.ToAllocator);

                new EntityIndexInQuery_WriteToArray_Enableable_Job { OutValues = outputValues }.ScheduleParallel(query, default).Complete();

                for (int i = 0, count=outputValues.Length; i < count; ++i)
                {
                    Assert.AreEqual(i, outputValues[i]);
                }
                EntityManager.DestroyEntity(entities);
            }


#endregion


#region Scheduling
        partial struct AutoDependencyOnlyAddedWhenExplicitJobHandlePassedJob : IJobEntity
        {
            public ComponentLookup<EcsTestData> datas;
            void Execute(Entity e) => datas[e] = new EcsTestData(5);
        }

        internal partial struct AutoDependencyOnlyAddedWhenExplicitJobHandlePassedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state)
            {
                var lookup = SystemAPI.GetComponentLookup<EcsTestData>();
                var a = new AutoDependencyOnlyAddedWhenExplicitJobHandlePassedJob{datas = lookup};
                var x = a.Schedule(state.Dependency);
                Assert.That(state.Dependency, Is.Not.EqualTo(x));
                state.Dependency = x;
                Assert.That(state.Dependency, Is.EqualTo(x));
            }
        }

        partial struct UsingCodeGenInsideAScheduleObjectInitJob : IJobEntity
        {
            public ComponentLookup<EcsTestData> datas;
            void Execute(Entity e) => datas[e] = new EcsTestData(5);
        }
        public void UsingCodeGenInsideAScheduleObjectInit()
        {
            Assert.DoesNotThrow(() =>
            {
                Assert.That(SystemAPI.GetComponent<EcsTestData>(s_TestEntity).value, Is.EqualTo(k_EcsTestDataValue));
                new UsingCodeGenInsideAScheduleObjectInitJob
                {
                    datas = SystemAPI.GetComponentLookup<EcsTestData>()
                }.Schedule(SystemAPI.QueryBuilder().WithAll<EcsTestData>().Build(), Dependency).Complete();
                Assert.That(SystemAPI.GetComponent<EcsTestData>(s_TestEntity).value, Is.EqualTo(5));
            });
        }

        partial struct ExtensionMethodToInvokeJob : IJobEntity
        {
            void Execute(ref EcsTestData data) => data.value += 1;
        }
        public void ExtensionMethodToInvoke()
        {
            Assert.That(SystemAPI.GetComponent<EcsTestData>(s_TestEntity).value, Is.EqualTo(k_EcsTestDataValue));
            IJobEntityExtensions.Run(new ExtensionMethodToInvokeJob());
            Assert.That(SystemAPI.GetComponent<EcsTestData>(s_TestEntity).value, Is.EqualTo(k_EcsTestDataValue+1));
        }

#endregion
#region Interfaces
        partial struct JobEntityChunkBeginEndJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                => chunk.HasChunkComponent<EcsTestData2>();
            void Execute(ref EcsTestData data) => data.value = 5;
            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted) {}
        }

        public void TestJobEntityChunkBeginEnd()
        {
            var e1 = EntityManager.CreateEntity(ComponentType.ReadWrite<EcsTestData>());
            var e2 = EntityManager.CreateEntity(ComponentType.ReadWrite<EcsTestData>());
            EntityManager.AddChunkComponentData<EcsTestData2>(e2);
            new JobEntityChunkBeginEndJob().Run();

            Assert.That(SystemAPI.GetComponent<EcsTestData>(e1).value==0);
            Assert.That(SystemAPI.GetComponent<EcsTestData>(e2).value==5);
        }

        partial struct OnChunkEndJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public ComponentTypeHandle<EcsTestData2> data2Handle;
            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                => chunk.HasChunkComponent(ref data2Handle);
            void Execute(ref EcsTestData data) => data.value = 5;

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
            {
                if (chunkWasExecuted)
                    chunk.SetChunkComponentData(ref data2Handle, new EcsTestData2(10));
            }
        }

        public void TestOnChunkEnd()
        {
            var e1 = EntityManager.CreateEntity(ComponentType.ReadWrite<EcsTestData>());
            var e2 = EntityManager.CreateEntity(ComponentType.ReadWrite<EcsTestData>());
            EntityManager.AddChunkComponentData<EcsTestData2>(e2);
            new OnChunkEndJob{data2Handle = SystemAPI.GetComponentTypeHandle<EcsTestData2>()}.Run();

            Assert.That(SystemAPI.GetComponent<EcsTestData>(e1).value==0);
            Assert.That(SystemAPI.GetComponent<EcsTestData>(e2).value==5);
            Assert.That(EntityManager.GetChunkComponentData<EcsTestData2>(e2).GetValue()==10);
        }
#endregion
        protected override void OnUpdate() { }
        }
    }
}

// Todo: Re-enable this once Generic passed IJE works as well as NonSystem scheduling
// internal static class JobEntityTestExtensions
// {
//     internal static void ScheduleFromScheduleType<T>(this T job, JobEntityCodeGenTests.ScheduleType scheduleType) where T : struct, IJobEntity
//     {
//         switch (scheduleType)
//         {
//             case JobEntityCodeGenTests.ScheduleType.Run:
//                 job.Run();
//                 break;
//             case JobEntityCodeGenTests.ScheduleType.Schedule:
//                 job.Schedule().Complete();
//                 break;
//             case JobEntityCodeGenTests.ScheduleType.ScheduleParallel:
//                 job.ScheduleParallel().Complete();
//                 break;
//         }
//     }
//
//     internal static void ScheduleFromScheduleType<T>(this T job, EntityQuery query, JobEntityCodeGenTests.ScheduleType scheduleType) where T : struct, IJobEntity
//     {
//         switch (scheduleType)
//         {
//             case JobEntityCodeGenTests.ScheduleType.Run:
//                 job.Run(query);
//                 break;
//             case JobEntityCodeGenTests.ScheduleType.Schedule:
//                 job.Schedule(query).Complete();
//                 break;
//             case JobEntityCodeGenTests.ScheduleType.ScheduleParallel:
//                 job.ScheduleParallel(query).Complete();
//                 break;
//         }
//     }
// }
