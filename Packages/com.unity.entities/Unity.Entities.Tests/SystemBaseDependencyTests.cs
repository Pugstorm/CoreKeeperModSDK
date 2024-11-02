using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;

#pragma warning disable 649

namespace Unity.Entities.Tests
{
    partial class SystemBaseDependencyTests : ECSTestsFixture
    {
        public partial class ReadSystem1 : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.ForEach((in EcsTestData c0) => {}).Schedule();
            }

            protected override void OnCreate()
            {
                EntityManager.CreateEntity(typeof(EcsTestData));
            }
        }

        public partial class ReadSystem2 : SystemBase
        {
            public bool returnWrongJob = false;
            public bool ignoreInputDeps = false;

            protected override void OnUpdate()
            {
                JobHandle h;
                if (ignoreInputDeps)
                    h = Entities.ForEach((in EcsTestData c0) => {}).Schedule(default);
                else
                    h = Entities.ForEach((in EcsTestData c0) => {}).Schedule(Dependency);

                Dependency = returnWrongJob ? Dependency : h;
            }

            protected override void OnCreate()
            {
                EntityManager.CreateEntity(typeof(EcsTestData));
            }
        }

        public partial class ReadSystem3 : SystemBase
        {
            public EntityQuery m_ReadGroup;

            protected override void OnUpdate() {}
            protected override void OnCreate()
            {
                m_ReadGroup = GetEntityQuery(ComponentType.ReadOnly<EcsTestData>());
            }
        }

        public partial class WriteSystem : SystemBase
        {
            public bool SkipJob = false;

            protected override void OnUpdate()
            {
                if (!SkipJob)
                {
                    Entities.ForEach((ref EcsTestData c0) => {}).Schedule();
                }
            }

            protected override void OnCreate()
            {
                EntityManager.CreateEntity(typeof(EcsTestData));
            }
        }

        public partial class GenericSystem<T> : SystemBase
        {
            public T thing;
            public T thing2;

            protected override void OnUpdate()
            {
                thing = thing2;
            }
        }

        [Test]
        public void CreatingGenericSystem_Works()
        {
            var system = (GenericSystem<int>)World.CreateSystemManaged(typeof(GenericSystem<int>));
            system.thing = 5;
            system.Update();
            Assert.AreEqual(system.thing, system.thing2);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ReturningWrongJobReportsCorrectSystemUpdate()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            WriteSystem ws = World.GetOrCreateSystemManaged<WriteSystem>();
            ReadSystem2 rs2 = World.GetOrCreateSystemManaged<ReadSystem2>();

            LogAssert.Expect(LogType.Error,
                new Regex(@"The system Unity\.Entities\.Tests\.SystemBaseDependencyTests\+ReadSystem2 reads Unity\.Entities\.Tests\.EcsTestData via ReadSystem2:ReadSystem2_.*_LambdaJob_1_Job but that type was not assigned to the Dependency property\. To ensure correct behavior of other systems, the job or a dependency must be assigned to the Dependency property before returning from the OnUpdate method\."));

            rs2.returnWrongJob = true;

            ws.Update();
            rs2.Update();
            Assert.Throws<InvalidOperationException>(()=> { ws.Update(); });
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void IgnoredInputDepsThrowsInCorrectSystemUpdate()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            WriteSystem ws1 = World.GetOrCreateSystemManaged<WriteSystem>();
            ReadSystem2 rs2 = World.GetOrCreateSystemManaged<ReadSystem2>();

            rs2.ignoreInputDeps = true;

            ws1.Update();
            Assert.Throws<System.InvalidOperationException>(() => { rs2.Update(); });
        }

        [Test]
        public void NotSchedulingWriteJobIsHarmless()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            WriteSystem ws1 = World.GetOrCreateSystemManaged<WriteSystem>();

            ws1.Update();
            ws1.SkipJob = true;
            ws1.Update();
        }

        [Test]
        public void NotUsingDataIsHarmless()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            ReadSystem1 rs1 = World.GetOrCreateSystemManaged<ReadSystem1>();
            ReadSystem3 rs3 = World.GetOrCreateSystemManaged<ReadSystem3>();

            rs1.Update();
            rs3.Update();
        }

        [Test]
        public void ReadAfterWrite_JobForEachGroup_Works()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var ws = World.GetOrCreateSystemManaged<WriteSystem>();
            var rs = World.GetOrCreateSystemManaged<ReadSystem2>();

            ws.Update();
            rs.Update();
        }

        partial class UseEcsTestDataFromEntity : SystemBase
        {
            public struct MutateEcsTestDataJob : IJob
            {
                public ComponentLookup<EcsTestData> data;

                public void Execute()
                {
                }
            }

            protected override void OnUpdate()
            {
                var job = new MutateEcsTestDataJob { data = GetComponentLookup<EcsTestData>() };
                Dependency = job.Schedule(Dependency);
            }
        }

        // The writer dependency on EcsTestData is not predeclared during
        // OnCreate, but we still expect the code to work correctly.
        // This should result in a sync point when adding the dependency for the first time.
        [Test]
        public void AddingDependencyTypeDuringOnUpdateSyncsDependency()
        {
            var systemA = World.CreateSystemManaged<UseEcsTestDataFromEntity>();
            var systemB = World.CreateSystemManaged<UseEcsTestDataFromEntity>();

            systemA.Update();
            systemB.Update();
        }

        partial class EmptySystemBase : SystemBase
        {
            protected override void OnUpdate()
            {
            }
        }

        partial class SystemBaseWithJobChunkJob : SystemBase
        {
            public struct EmptyJob : IJobChunk
            {
                public ComponentTypeHandle<EcsTestData> TestDataTypeHandle;
                public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                {
                }
            }

            protected override void OnUpdate()
            {
                Dependency = new EmptyJob
                {
                    TestDataTypeHandle = GetComponentTypeHandle<EcsTestData>()
                }.ScheduleParallel(EntityManager.UniversalQuery, Dependency);
            }
        }

        [Test]
        public void EmptySystemAfterNonEmptySystemDoesntThrow()
        {
            m_Manager.CreateEntity(typeof(EcsTestData));

            var systemA = World.CreateSystemManaged<SystemBaseWithJobChunkJob>();
            var systemB = World.CreateSystemManaged<EmptySystemBase>();

            systemA.Update();
            systemB.Update();
        }

        partial class SystemBaseEntitiesForEachDependencies : SystemBase
        {
            public bool DoRunToCompleteDependencies = false;

            protected override void OnUpdate()
            {
                Entities.ForEach((ref EcsTestData thing) => {}).Schedule();
                Entities.ForEach((ref EcsTestData thing) => {}).ScheduleParallel();

                if (DoRunToCompleteDependencies)
                    Entities.ForEach((ref EcsTestData thing) => {}).Run();

                if (!Dependency.Equals(new JobHandle())) //after completing all jobs an Dependency should be empty jobhandle
                    throw new Exception("Previous dependencies were not forced to completion.");
            }

            protected override void OnCreate()
            {
                EntityManager.CreateEntity(typeof(EcsTestData));
            }
        }

        [Test]
        public void SystemBaseEntitiesForEachDependencies_WithNoRun_HasUncompletedDependencies()
        {
            var system = World.CreateSystemManaged<SystemBaseEntitiesForEachDependencies>();
            system.DoRunToCompleteDependencies = false;

            Assert.Throws<Exception>(() =>
            {
                system.Update();
            });
        }

        [Test]
        public void SystemBaseEntitiesForEachDependencies_WithRun_HasNoUncompletedDependencies()
        {
            var system = World.CreateSystemManaged<SystemBaseEntitiesForEachDependencies>();
            system.DoRunToCompleteDependencies = true;

            Assert.DoesNotThrow(() =>
            {
                system.Update();
            });
        }

        partial class SystemBaseEntitiesForEachComponentLookup : SystemBase
        {
            public bool RunScheduleParallel = false;

            protected override void OnUpdate()
            {
                var dataFromEntity = GetComponentLookup<EcsTestData>(false);

                if (RunScheduleParallel)
                {
                    Entities.ForEach((in EcsTestDataEntity data) =>
                    {
                        dataFromEntity[data.value1] = new EcsTestData() { value = data.value0 };
                    }).ScheduleParallel();
                }
                else
                {
                    Entities.ForEach((in EcsTestDataEntity data) =>
                    {
                        dataFromEntity[data.value1] = new EcsTestData() { value = data.value0 };
                    }).Schedule();
                }
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void SystemBaseEntitiesForEachComponentLookup_Scheduled_ThrowsAppropriateException([Values(false, true)] bool runScheduleParallel)
        {
            var system = World.CreateSystemManaged<SystemBaseEntitiesForEachComponentLookup>();
            system.RunScheduleParallel = runScheduleParallel;

            var archetype = system.EntityManager.CreateArchetype(new ComponentType[] { typeof(EcsTestDataEntity), typeof(EcsTestData) });
            using (var createdEntities = system.EntityManager.CreateEntity(archetype, 100, Allocator.Persistent))
            {
                for (int i = 0; i < createdEntities.Length; ++i)
                    system.EntityManager.SetComponentData(createdEntities[i], new EcsTestDataEntity { value1 = createdEntities[(i + 1) % createdEntities.Length] });

                if (!runScheduleParallel)
                {
                    system.Update();
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() => { system.Update();}); // this throws for parallel writing reasons
                }
            }
        }

        partial class SystemWithSyncPointAfterSchedule : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.ForEach((ref EcsTestData _) => { }).Schedule();

                // this forces a sync-point and must finish the job we just scheduled
                EntityManager.CreateEntity();
            }

            protected override void OnCreate()
            {
                EntityManager.CreateEntity(typeof(EcsTestData));
            }
        }

        [Test]
        public void SystemBase_CanHaveSyncPointAfterSchedule()
        {
            var s = World.CreateSystemManaged<SystemWithSyncPointAfterSchedule>();
            Assert.DoesNotThrow(() => s.Update());
        }

        partial class UseStorageDataFromEntity : SystemBase
        {
            public struct MutateEcsTestDataJob : IJob
            {
                public EntityStorageInfoLookup EntityStorageInfo;

                public void Execute()
                {
                }
            }

            protected override void OnUpdate()
            {
                var job = new MutateEcsTestDataJob { EntityStorageInfo = GetEntityStorageInfoLookup() };
                //Dependency = job.Schedule(Dependency); commented out to show that EntityStorageInfoLookup is always readonly and does not require dependency tracking
            }
        }

        [Test]
        public void StorageDataFromEntity_IsReadOnly_ThrowsNoSyncErrors()
        {
            var systemA = World.CreateSystemManaged<UseStorageDataFromEntity>();
            var systemB = World.CreateSystemManaged<UseStorageDataFromEntity>();

            systemA.Update();
            systemB.Update();
        }

        partial class WritesZeroSizeComponent : SystemBase
        {
            ComponentLookup<EcsTestTagEnableable> _lookup;
            protected override void OnCreate()
            {
                _lookup = GetComponentLookup<EcsTestTagEnableable>(false);
            }

            protected override void OnUpdate()
            {
                _lookup.Update(this);
                var lookupCopy = _lookup;
                Entities
                    .WithNativeDisableParallelForRestriction(lookupCopy)
                    .ForEach((Entity entity) =>
                    {
                        lookupCopy.SetComponentEnabled(entity, false);
                    }).ScheduleParallel();
            }
        }
        partial class ReadsZeroSizeComponent : SystemBase
        {
            EntityQuery _query;
            public int EntityCount;
            protected override void OnCreate()
            {
                _query = GetEntityQuery(typeof(EcsTestTagEnableable));
            }

            protected override void OnUpdate()
            {
                int count = 0;
                Entities
                    .WithAll<EcsTestTagEnableable>()
                    .ForEach((Entity entity) =>
                    {
                        count += 1;
                    }).Run();
                EntityCount = count;
            }
        }

        [Test]
        public void ZeroSizeComponent_WriteDependency_IsTracked()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestTagEnableable));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestTagEnableable));
            using var entities = m_Manager.CreateEntity(archetype, 10000, World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entities.Length; i += 2)
            {
                m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
            }
            // A running job writing to the tag component should systems reading it to block
            var sysWrite = World.CreateSystemManaged<WritesZeroSizeComponent>();
            var sysRead = World.CreateSystemManaged<ReadsZeroSizeComponent>();
            sysWrite.Update();
            sysRead.Update();
            Assert.AreEqual(0, sysRead.EntityCount);
            foreach(var ent in entities)
            {
                Assert.IsFalse(m_Manager.IsComponentEnabled<EcsTestTagEnableable>(ent));
            }
        }

        partial class EntityManagerGetBufferWriteSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var buffer = EntityManager.GetBuffer<EcsIntElement>(SystemAPI.GetSingletonEntity<EcsTestTag>());
                Job.WithCode(() => { buffer.Add(new EcsIntElement {Value = 123}); }).Schedule();
            }
        }

        partial class EntityManagerGetBufferWriteInUpdateSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var buffer = EntityManager.GetBuffer<EcsIntElement>(SystemAPI.GetSingletonEntity<EcsTestTag>());
                buffer.Add(new EcsIntElement {Value = 123});
            }
        }

        partial class EntityManagerGetBufferWriteViaRunSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var buffer = EntityManager.GetBuffer<EcsIntElement>(SystemAPI.GetSingletonEntity<EcsTestTag>());
                Job.WithCode(() => { buffer.Add(new EcsIntElement {Value = 123}); }).Run();
            }
        }

        partial class GetBufferWriteJobSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var buffer = SystemAPI.GetBuffer<EcsIntElement>(SystemAPI.GetSingletonEntity<EcsTestTag>());
                Job.WithCode(() => { buffer.Add(new EcsIntElement {Value = 123}); }).Schedule();
            }
        }

        partial class GetBufferWriteInt2JobSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var buffer = SystemAPI.GetBuffer<EcsIntElement2>(SystemAPI.GetSingletonEntity<EcsTestTag>());
                Job.WithCode(() => { buffer.Add(new EcsIntElement2 {Value0 = 0, Value1 = 1}); }).Schedule();
            }
        }

        partial class GetBufferLookupWriteJobSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var buffer = GetBufferLookup<EcsIntElement>()[SystemAPI.GetSingletonEntity<EcsTestTag>()];
                Job.WithCode(() => { buffer.Add(new EcsIntElement {Value = 123}); }).Schedule();
            }
        }

        partial class GetBufferLookupInEntitiesForEachWriteSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.WithAll<EcsTestTag>().ForEach((Entity entity) =>
                {
                    var buffer = GetBufferLookup<EcsIntElement>()[entity];
                    buffer.Add(new EcsIntElement {Value = 123});
                }).Schedule();
            }
        }

        partial class GetBufferReadInUpdateSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var buffer = SystemAPI.GetBuffer<EcsIntElement>(SystemAPI.GetSingletonEntity<EcsTestTag>());
                Assert.AreEqual(123, buffer[0].Value);
            }
        }

        partial class GetBufferReadOnlyInUpdateSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var buffer = SystemAPI.GetBufferLookup<EcsIntElement>(true)[SystemAPI.GetSingletonEntity<EcsTestTag>()];
                Assert.AreEqual(123, buffer[0].Value);
            }
        }

        partial class GetBufferReadJobSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var buffer = SystemAPI.GetBuffer<EcsIntElement>(SystemAPI.GetSingletonEntity<EcsTestTag>());
                Job.WithCode(() =>
                {
                    Assert.AreEqual(123, buffer[0].Value);
                }).Schedule();
            }
        }

        partial class GetBufferReadOnlyJobSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                EntityManager.CompleteDependencyBeforeRO<EcsIntElement>();
                var buffer = SystemAPI.GetBufferLookup<EcsIntElement>(true)[SystemAPI.GetSingletonEntity<EcsTestTag>()];
                Job.WithCode(() =>
                {
                    Assert.AreEqual(123, buffer[0].Value);
                }).Schedule();
            }
        }

        partial class GetBufferInsideForEachWithEntityIteratorSystem : SystemBase
        {
            protected override void OnCreate()
            {
                EntityManager.CreateEntity(typeof(EcsTestData));
            }

            protected override void OnUpdate()
            {
                Entities.WithAll<EcsTestTag>().ForEach((in Entity tagEntity) =>
                {
                    // Codegen should replace this with GetBufferLookup created in OnUpdate
                    var buffer = SystemAPI.GetBuffer<EcsIntElement>(tagEntity);
                    Assert.AreEqual(123, buffer[0].Value);
                }).Schedule();
            }
        }

        partial class GetBufferInsideForEachWithSingletonSystem : SystemBase
        {
            protected override void OnCreate()
            {
                EntityManager.CreateEntity(typeof(EcsTestData));
            }

            protected override void OnUpdate()
            {
                var tagEntity = SystemAPI.GetSingletonEntity<EcsTestTag>();
                Entities.ForEach((in EcsTestData testData) =>
                {
                    // Codegen should replace this with GetBufferLookup created in OnUpdate
                    var buffer = SystemAPI.GetBuffer<EcsIntElement>(tagEntity);
                    Assert.AreEqual(123, buffer[0].Value);
                }).Schedule();
            }
        }

        partial class GetBufferAndPassAsNativeArrayToJob : SystemBase
        {
            protected override void OnCreate()
            {
                EntityManager.CreateEntity(typeof(EcsTestData));
            }

            protected override void OnUpdate()
            {
                var buffer = SystemAPI.GetBuffer<EcsIntElement>(SystemAPI.GetSingletonEntity<EcsTestTag>());
                var array = buffer.AsNativeArray();
                Entities.ForEach((in EcsTestData testData) =>
                {
                    Assert.AreEqual(123, array[0].Value);
                }).Schedule();
            }
        }

        [Test]
        public void BufferDependencies_WritingToUnrelatedBuffersDoesNotDepend()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsIntElement), typeof(EcsIntElement2));

            var sysWriteInt = World.CreateSystemManaged<EntityManagerGetBufferWriteSystem>();
            var sysWriteInt2 = World.CreateSystemManaged<GetBufferWriteInt2JobSystem>();

            sysWriteInt.Update();
            Assert.DoesNotThrow(() => sysWriteInt2.Update());

            unsafe
            {
                var writeIntHandle = sysWriteInt.CheckedState()->Dependency;
                var writeInt2Handle = sysWriteInt2.CheckedState()->Dependency;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsFalse(JobHandle.CheckFenceIsDependencyOrDidSyncFence(writeInt2Handle, writeIntHandle));
#endif
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void BufferDependencies_GetBufferReadAfterEntityManagerGetBufferWriteThrowsSafetyError()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsIntElement));

            var sysWrite = World.CreateSystemManaged<EntityManagerGetBufferWriteSystem>();
            var sysRead = World.CreateSystemManaged<GetBufferReadInUpdateSystem>();
            sysWrite.Update();

            LogAssert.Expect(LogType.Error, new Regex(".*dependency.*"));
            Assert.Throws<InvalidOperationException>(() => sysRead.Update());
        }

        [Test]
        public void BufferDependencies_WriteToBufferInUpdateViaEntityManagerGetBuffer()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsIntElement));

            var sysWrite = World.CreateSystemManaged<EntityManagerGetBufferWriteInUpdateSystem>();
            var sysRead = World.CreateSystemManaged<GetBufferReadInUpdateSystem>();
            sysWrite.Update();
            Assert.DoesNotThrow(() => sysRead.Update());

            unsafe
            {
                var writeHandle = sysWrite.CheckedState()->Dependency;
                var readHandle = sysRead.CheckedState()->Dependency;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsTrue(JobHandle.CheckFenceIsDependencyOrDidSyncFence(readHandle, writeHandle));
#endif
            }
        }

        [Test]
        public void BufferDependencies_WriteToBufferInRunViaEntityManagerGetBuffer()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsIntElement));

            var sysWrite = World.CreateSystemManaged<EntityManagerGetBufferWriteViaRunSystem>();
            var sysRead = World.CreateSystemManaged<GetBufferReadInUpdateSystem>();
            sysWrite.Update();
            Assert.DoesNotThrow(() => sysRead.Update());

            unsafe
            {
                var writeHandle = sysWrite.CheckedState()->Dependency;
                var readHandle = sysRead.CheckedState()->Dependency;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsTrue(JobHandle.CheckFenceIsDependencyOrDidSyncFence(readHandle, writeHandle));
#endif
            }
        }

        [Test]
        public void BufferDependencies_GetBufferReadInUpdateDependsOnGetBufferWriteJob()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsIntElement));

            var sysWrite = World.CreateSystemManaged<GetBufferWriteJobSystem>();
            var sysRead = World.CreateSystemManaged<GetBufferReadInUpdateSystem>();
            sysWrite.Update();
            Assert.DoesNotThrow(() => sysRead.Update());

            unsafe
            {
                var writeHandle = sysWrite.CheckedState()->Dependency;
                var readHandle = sysRead.CheckedState()->Dependency;
                Assert.IsFalse(writeHandle.Equals(readHandle));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsTrue(JobHandle.CheckFenceIsDependencyOrDidSyncFence(readHandle, writeHandle));
#endif
            }
        }

        [Test]
        public void BufferDependencies_GetBufferReadInUpdateDependsOnGetBufferLookupWriteJob()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsIntElement));

            var sysWrite = World.CreateSystemManaged<GetBufferLookupWriteJobSystem>();
            var sysRead = World.CreateSystemManaged<GetBufferReadInUpdateSystem>();
            sysWrite.Update();
            Assert.DoesNotThrow(() => sysRead.Update());

            unsafe
            {
                var writeHandle = sysWrite.CheckedState()->Dependency;
                var readHandle = sysRead.CheckedState()->Dependency;
                Assert.IsFalse(writeHandle.Equals(readHandle));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsTrue(JobHandle.CheckFenceIsDependencyOrDidSyncFence(readHandle, writeHandle));
#endif
            }
        }

        [Test]
        public void BufferDependencies_GetBufferReadInUpdateDependsOnGetBufferLookupForEachWriteJob()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsIntElement));

            var sysWrite = World.CreateSystemManaged<GetBufferLookupInEntitiesForEachWriteSystem>();
            var sysRead = World.CreateSystemManaged<GetBufferReadInUpdateSystem>();
            sysWrite.Update();
            Assert.DoesNotThrow(() => sysRead.Update());

            unsafe
            {
                var writeHandle = sysWrite.CheckedState()->Dependency;
                var readHandle = sysRead.CheckedState()->Dependency;
                Assert.IsFalse(writeHandle.Equals(readHandle));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsTrue(JobHandle.CheckFenceIsDependencyOrDidSyncFence(readHandle, writeHandle));
#endif
            }
        }

        [Test]
        public void BufferDependencies_GetBufferReadInForEachDependsOnGetBufferLookupWriteJob()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsIntElement));

            var sysWrite = World.CreateSystemManaged<GetBufferLookupWriteJobSystem>();
            var sysRead = World.CreateSystemManaged<GetBufferInsideForEachWithEntityIteratorSystem>();
            sysWrite.Update();
            Assert.DoesNotThrow(() => sysRead.Update());

            unsafe
            {
                var writeHandle = sysWrite.CheckedState()->Dependency;
                var readHandle = sysRead.CheckedState()->Dependency;
                Assert.IsFalse(writeHandle.Equals(readHandle));
                // TODO: This fails, requires investigation DOTS-5964
                //Assert.IsTrue(JobHandle.CheckFenceIsDependencyOrDidSyncFence(readHandle, writeHandle));
            }
        }

        [Test]
        public void BufferDependencies_GetBufferFromSingletonReadInForEachDependsOnGetBufferLookupWriteJob()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsIntElement));

            var sysWrite = World.CreateSystemManaged<GetBufferLookupWriteJobSystem>();
            var sysRead = World.CreateSystemManaged<GetBufferInsideForEachWithSingletonSystem>();
            sysWrite.Update();
            Assert.DoesNotThrow(() => sysRead.Update());

            unsafe
            {
                var writeHandle = sysWrite.CheckedState()->Dependency;
                var readHandle = sysRead.CheckedState()->Dependency;
                Assert.IsFalse(writeHandle.Equals(readHandle));
                // TODO: This fails, requires investigation DOTS-5964
                //Assert.IsTrue(JobHandle.CheckFenceIsDependencyOrDidSyncFence(readHandle, writeHandle));
            }
        }

        [Test]
        public void BufferDependencies_GetBufferAsNativeArrayReadJobDependsOnGetBufferWriteJob()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsIntElement));

            var sysWrite = World.CreateSystemManaged<GetBufferWriteJobSystem>();
            var sysRead = World.CreateSystemManaged<GetBufferAndPassAsNativeArrayToJob>();
            sysWrite.Update();
            Assert.DoesNotThrow(() => sysRead.Update());

            unsafe
            {
                var writeHandle = sysWrite.CheckedState()->Dependency;
                var readHandle = sysRead.CheckedState()->Dependency;
                Assert.IsFalse(writeHandle.Equals(readHandle));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsFalse(JobHandle.CheckFenceIsDependencyOrDidSyncFence(readHandle, writeHandle));
#endif
            }
        }

        [Test]
        public void BufferDependencies_TwoGetBufferReadOnlyJobsShouldNotDependOnEachOther()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsIntElement));

            // Read systems assume first buffer element is 123, so we write them first
            var sysWrite = World.CreateSystemManaged<GetBufferLookupWriteJobSystem>();

            var sysReadOnly = World.CreateSystemManaged<GetBufferReadOnlyJobSystem>();
            var sysReadOnly2 = World.CreateSystemManaged<GetBufferReadOnlyJobSystem>();
            sysWrite.Update();
            sysReadOnly.Update();
            Assert.DoesNotThrow(() => sysReadOnly2.Update());

            unsafe
            {
                var writeHandle = sysWrite.CheckedState()->Dependency;
                var readHandle1 = sysReadOnly.CheckedState()->Dependency;
                var readHandle2 = sysReadOnly2.CheckedState()->Dependency;
                Assert.IsFalse(writeHandle.Equals(readHandle1));
                Assert.IsFalse(writeHandle.Equals(readHandle2));
                Assert.IsFalse(readHandle1.Equals(readHandle2));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsFalse(JobHandle.CheckFenceIsDependencyOrDidSyncFence(readHandle2, readHandle1));
#endif
            }
        }

        [Test]
        public void BufferDependencies_GetBufferShouldNotDependOnGetBufferReadOnly()
        {
            m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsIntElement));

            // Read systems assume first buffer element is 123, so we write them first
            var sysWrite = World.CreateSystemManaged<GetBufferLookupWriteJobSystem>();

            var sysReadOnly = World.CreateSystemManaged<GetBufferReadOnlyJobSystem>();
            var sysRead = World.CreateSystemManaged<GetBufferReadJobSystem>();
            sysWrite.Update();
            sysReadOnly.Update();
            Assert.DoesNotThrow(() => sysRead.Update());

            unsafe
            {
                var writeHandle = sysWrite.CheckedState()->Dependency;
                var readOnlyHandle = sysReadOnly.CheckedState()->Dependency;
                var readHandle = sysRead.CheckedState()->Dependency;
                Assert.IsFalse(writeHandle.Equals(readOnlyHandle));
                Assert.IsFalse(writeHandle.Equals(readHandle));
                Assert.IsFalse(readOnlyHandle.Equals(readHandle));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.IsFalse(JobHandle.CheckFenceIsDependencyOrDidSyncFence(readHandle, readOnlyHandle));
#endif
            }
        }

        public partial class SystemSchedulingTwoParallelJobsWritingToSameArray : SystemBase
        {
            public struct InputStatus
            {
            }

            public struct Discriminator1 : IComponentData { }
            public struct Discriminator2 : IComponentData { }

            public partial struct Discriminator1Job : IJobEntity
            {
                [NativeDisableParallelForRestriction] public NativeArray<InputStatus> InputStatusArray;
                [ReadOnly] public NativeParallelHashMap<Entity, int> EntitiesIndexMap;

                private void Execute(
                    Entity entity,
                    in EcsTestData testData,
                    in Discriminator1 disc)
                {
                    InputStatusArray[EntitiesIndexMap[entity]] = new InputStatus();
                }
            }

            public partial struct Discriminator2Job : IJobEntity
            {
                [NativeDisableParallelForRestriction] public NativeArray<InputStatus> InputStatusArray;
                [ReadOnly] public NativeParallelHashMap<Entity, int> EntitiesIndexMap;

                private void Execute(
                    Entity entity,
                    in EcsTestData testData,
                    in Discriminator2 disc)
                {
                    InputStatusArray[EntitiesIndexMap[entity]] = new InputStatus();
                }
            }

            static readonly int NumEntities = 10;

            private NativeArray<InputStatus> inputStatusArray;
            private NativeParallelHashMap<Entity, int> entitiesIndexMap;
            private EntityQuery entityQuery;

            protected override void OnCreate()
            {
                inputStatusArray = new NativeArray<InputStatus>(NumEntities, Allocator.Persistent);
                entitiesIndexMap = new NativeParallelHashMap<Entity, int>(NumEntities, Allocator.Persistent);

                for (int i = 0; i < NumEntities/2; ++i)
                    EntityManager.CreateEntity(typeof(EcsTestData), typeof(Discriminator1));

                for (int i = 0; i < NumEntities/2; ++i)
                    EntityManager.CreateEntity(typeof(EcsTestData), typeof(Discriminator2));

                entityQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<EcsTestData>()
                    .WithAny<Discriminator1, Discriminator2>()
                    .Build(this);
            }

            protected override void OnDestroy()
            {
                inputStatusArray.Dispose();
                entitiesIndexMap.Dispose();
            }

            protected override void OnUpdate()
            {
                var inputStatus = inputStatusArray;
                var entitiesIndex = entitiesIndexMap;

                var entitiesArray = entityQuery.ToEntityArray(Allocator.TempJob);
                Dependency = Job.WithCode(() =>
                {
                    entitiesIndex.Clear();

                    for (var i = 0; i < entitiesArray.Length; i++)
                        entitiesIndex.Add(entitiesArray[i], i);

                    for (var i = 0; i < inputStatus.Length; i++)
                        inputStatus[i] = new InputStatus { };
                }).Schedule(Dependency);

                entitiesArray.Dispose(Dependency);

                // These two jobs write to the same container and will trigger
                // a parallel write safety exception
                var job0 = new Discriminator1Job
                {
                    InputStatusArray = inputStatusArray,
                    EntitiesIndexMap = entitiesIndex,
                }.ScheduleParallel(Dependency);

                var job1 = new Discriminator2Job
                {
                    InputStatusArray = inputStatusArray,
                    EntitiesIndexMap = entitiesIndex,
                }.ScheduleParallel(Dependency);

                // Don't assign to Dependency to trigger a system safety error message
                //Dependency = JobHandle.CombineDependencies(job0, job1);
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ParallelWrite_Exception_Not_Suppressed()
        {
            // Regression test. Run a system that unsafely writes to a NativeArray from two parallel jobs.
            // The data access is safe but the safety system can't know that. We had an issue where the safety PanicFunction
            // was preventing the real safety message from surfacing
            var sys1 = World.CreateSystem<SystemSchedulingTwoParallelJobsWritingToSameArray>();
            Assert.That(() => { sys1.Update(World.Unmanaged); },
                            Throws.Exception.TypeOf<InvalidOperationException>()
                                .With.Message.Contains(
                                    "The previously scheduled job SystemSchedulingTwoParallelJobsWritingToSameArray:Discriminator1Job writes to the Unity.Collections.NativeArray"));
            LogAssert.Expect(LogType.Error, "The system Unity.Entities.Tests.SystemBaseDependencyTests+SystemSchedulingTwoParallelJobsWritingToSameArray reads Unity.Entities.Tests.EcsTestData via SystemSchedulingTwoParallelJobsWritingToSameArray:Discriminator1Job but that type was not assigned to the Dependency property. To ensure correct behavior of other systems, the job or a dependency must be assigned to the Dependency property before returning from the OnUpdate method.");
        }
    }
}
