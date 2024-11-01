using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Entities.Tests
{
    using static AspectUtils;
    struct Component01 : IComponentData
    {
        public int Value;

        public Component01(int value)
        {
            Value = value;
        }
    }

    struct Component02 : IComponentData
    {
        public int Value;
    }

    readonly partial struct EcsTestDataAspect : IAspect
    {
        public readonly RefRW<EcsTestData> TestComponent;
    }

    partial class TestSystemWithEmptyJob : SystemBase
    {
        public JobHandle ScheduledJobHandle;

        struct EmptyJob : IJob
        {
            public void Execute()
            {
                // Do nothing.
            }
        }

        protected override void OnUpdate()
        {
            var job = new EmptyJob();
            ScheduledJobHandle = job.Schedule(Dependency);
            Dependency = ScheduledJobHandle;
        }
    }

    struct EmptyJob : IJobChunk
    {
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            // Do nothing.
        }
    }

    [TestFixture]
    class ConstrainedEntityCreationTests : ECSTestsFixture
    {
        [Test]
        public void CreateEntity_WithArchetypeNotMatchingQuery_Works()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            // We enumerate with a query for EcsTestData, but create an entity CreateComponent.
            // Since those two will never match each other, it is safe to create an entity in this loop
            var notMatchingArchetype = m_Manager.CreateArchetype(typeof(Component01));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                m_Manager.CreateEntity();
                m_Manager.CreateEntity(notMatchingArchetype);
                m_Manager.CreateEntity(typeof(Component01));
                m_Manager.CreateEntity(notMatchingArchetype, TmpNA(2));
                m_Manager.CreateEntity(notMatchingArchetype, 2, World.UpdateAllocator.ToAllocator);
                m_Manager.CreateEntity(notMatchingArchetype, 2);
            }

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
            Assert.AreEqual(OriginalEntitiesCount * 8, Component01EntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void CreateEntity_WithArchetypeMatchingQuery_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            // We enumerate with a query for EcsTestData, and then also create an entity with EcsTestData
            // If we were to allow this, it would mean that depending on where you are in the iteration
            // (the new entity gets added to a new chunk vs the chunk we are currently iterating over) you get different behaviour.
            // While this is fully deterministic, it is quite unexpected and not controlled behaviour.
            // So instead we throw an exception when creating an entity whose archetype matches what we are currently enumerating
            var matchingArchetype = m_Manager.CreateArchetype(typeof(EcsTestData));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                AssertValueConsistency(aspect);

                Assert.Throws<InvalidOperationException>(() => m_Manager.CreateEntity(matchingArchetype));
                Assert.Throws<InvalidOperationException>(() => m_Manager.CreateEntity(typeof(EcsTestData)));
                Assert.Throws<InvalidOperationException>(() => m_Manager.CreateEntity(matchingArchetype, TmpNA(2)));
                Assert.Throws<InvalidOperationException>(() => m_Manager.CreateEntity(matchingArchetype, 2, World.UpdateAllocator.ToAllocator));
                Assert.Throws<InvalidOperationException>(() => m_Manager.CreateEntity(matchingArchetype, 2));
            }

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        public void CreateEntity_WithArchetypeNotMatchingQuery_CompletesAllScheduledJobs()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var notMatchingArchetype = m_Manager.CreateArchetype(typeof(Component01));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(query, ref typeHandle, () => m_Manager.CreateEntity(notMatchingArchetype));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(query, ref typeHandle, () => m_Manager.CreateEntity());
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(query, ref typeHandle, () => m_Manager.CreateEntity(typeof(Component01)));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(query, ref typeHandle, () => m_Manager.CreateEntity(notMatchingArchetype, TmpNA(2)));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(query, ref typeHandle, () => m_Manager.CreateEntity(notMatchingArchetype, 2, World.UpdateAllocator.ToAllocator));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(query, ref typeHandle, () => m_Manager.CreateEntity(notMatchingArchetype, 2));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
            Assert.AreEqual(OriginalEntitiesCount * 8, Component01EntitiesCount);
        }

        [Test]
        public void Instantiate_WithArchetypeNotMatchingQuery_Works()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toInstantiate = m_Manager.CreateEntity(typeof(Component01));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                m_Manager.Instantiate(toInstantiate);
                m_Manager.Instantiate(toInstantiate, TmpNA(2));
                m_Manager.Instantiate(toInstantiate, 2, World.UpdateAllocator.ToAllocator);
            }

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
            Assert.AreEqual(OriginalEntitiesCount * 5 + 1, Component01EntitiesCount);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CreateEntity_WhileUnregisteredJobIsScheduled_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var notMatchingArchetype = m_Manager.CreateArchetype(typeof(Component01));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            ScheduleJobAndAssertCodeThrows(query, typeHandle, () => m_Manager.CreateEntity(notMatchingArchetype));
            ScheduleJobAndAssertCodeThrows(query, typeHandle, () => m_Manager.CreateEntity());
            ScheduleJobAndAssertCodeThrows(query, typeHandle, () => m_Manager.CreateEntity(typeof(EcsTestData)));
            ScheduleJobAndAssertCodeThrows(query, typeHandle, () => m_Manager.CreateEntity(notMatchingArchetype, TmpNA(2)));
            ScheduleJobAndAssertCodeThrows(query, typeHandle, () => m_Manager.CreateEntity(notMatchingArchetype, 2, World.UpdateAllocator.ToAllocator));
            ScheduleJobAndAssertCodeThrows(query, typeHandle, () => m_Manager.CreateEntity(notMatchingArchetype, 2));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void Instantiate_WithArchetypeMatchingQuery_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toInstantiate = m_Manager.GetAllEntities()[0];

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                AssertValueConsistency(aspect);

                Assert.Throws<InvalidOperationException>(() => m_Manager.Instantiate(toInstantiate));
                Assert.Throws<InvalidOperationException>(() => m_Manager.Instantiate(toInstantiate, TmpNA(2)));
                Assert.Throws<InvalidOperationException>(() => m_Manager.Instantiate(toInstantiate, 2, World.UpdateAllocator.ToAllocator));
            }

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void Instantiate_WhileUnregisteredJobIsScheduled_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toInstantiate = m_Manager.CreateEntity(typeof(Component01));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            ScheduleJobAndAssertCodeThrows(query, typeHandle, () => m_Manager.Instantiate(toInstantiate));
            ScheduleJobAndAssertCodeThrows(query, typeHandle, () => m_Manager.Instantiate(toInstantiate, TmpNA(2)));
            ScheduleJobAndAssertCodeThrows(query, typeHandle, () => m_Manager.Instantiate(toInstantiate, 2, World.UpdateAllocator.ToAllocator));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void Instantiate_WithArchetypeNotMatchingQuery_CompletesAllScheduledJobs()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toInstantiate = m_Manager.CreateEntity(typeof(Component01));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(query, ref typeHandle, () => m_Manager.Instantiate(toInstantiate));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(query, ref typeHandle, () => m_Manager.Instantiate(toInstantiate, TmpNA(2)));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(query, ref typeHandle, () => m_Manager.Instantiate(toInstantiate, 2, World.UpdateAllocator.ToAllocator));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
            Assert.AreEqual(OriginalEntitiesCount * 5 + 1, Component01EntitiesCount);
        }

        [Test]
        public void CopyEntities_WithArchetypeNotMatchingQuery_Works()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toCopy = m_Manager.CreateEntity(typeof(Component01));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                m_Manager.CopyEntitiesInternal(TmpNA(toCopy, toCopy), TmpNA(2));
            }

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
            Assert.AreEqual(OriginalEntitiesCount * 2 + 1, Component01EntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void CopyEntities_WithArchetypeMatchingQuery_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toCopy = m_Manager.GetAllEntities()[0];

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                AssertValueConsistency(aspect);
                Assert.Throws<InvalidOperationException>(() => m_Manager.Instantiate(toCopy));
            }

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CopyEntities_WhileUnregisteredJobIsScheduled_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toCopy = m_Manager.CreateEntity(typeof(Component01));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            ScheduleJobAndAssertCodeThrows(query, typeHandle, () => m_Manager.CopyEntitiesInternal(TmpNA(toCopy, toCopy), TmpNA(2)));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        public unsafe void CreateEntityOnlyArchetype_Works()
        {
            var archetype = m_Manager.CreateArchetypeWithoutSimulateComponent(null, 0);
            Assert.AreEqual(1, archetype.TypesCount);
            Assert.AreEqual(TypeManager.GetTypeIndex<Entity>(), archetype.Types[0].TypeIndex);
        }

        [Test]
        public void CreateArchetype_WithArchetypeNotMatchingQuery_Works()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                m_Manager.CreateArchetype(typeof(Component01));
                m_Manager.CreateArchetype(TmpNA(typeof(Component01)));
            }

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void CreateArchetype_WithArchetypeMatchingQuery_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                Assert.Throws<InvalidOperationException>(() => m_Manager.CreateArchetype(typeof(EcsTestData)));
                Assert.Throws<InvalidOperationException>(() => m_Manager.CreateArchetype(TmpNA(typeof(EcsTestData))));
            }

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        public void CreateArchetype_WithArchetypeNotMatchingQuery_CompletesAllScheduledJobs()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(query, ref typeHandle, () => m_Manager.CreateArchetype(typeof(Component01)));
            CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(query, ref typeHandle, () => m_Manager.CreateArchetype(TmpNA(typeof(Component01))));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CreateArchetype_WhileUnregisteredJobIsScheduled_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            ScheduleJobAndAssertCodeThrows(query, typeHandle, () => m_Manager.CreateArchetype(typeof(Component01)));
            ScheduleJobAndAssertCodeThrows(query, typeHandle, () => m_Manager.CreateArchetype(TmpNA(typeof(Component01))));

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void Instantiate_Prefab_WithArchetypeMatchingQuery_Throws()
        {
            var prefabEntity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(Prefab));
            m_Manager.CreateEntity(typeof(EcsTestData));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                Assert.Throws<InvalidOperationException>(() => m_Manager.Instantiate(prefabEntity));
                Assert.Throws<InvalidOperationException>(() => m_Manager.Instantiate(prefabEntity, TmpNA(2)));
                Assert.Throws<InvalidOperationException>(() => m_Manager.Instantiate(prefabEntity, 2, World.UpdateAllocator.ToAllocator));
            }
        }

        [Test]
        public void CopyEntities_Prefab_WithArchetypeNotMatchingQuery_Works()
        {
            m_Manager.CreateEntity(typeof(EcsTestData));

            var prefabs = new[]
            {
                m_Manager.CreateEntity(typeof(EcsTestData), typeof(Prefab)),
                m_Manager.CreateEntity(typeof(EcsTestData), typeof(Prefab))
            };

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
                m_Manager.CopyEntitiesInternal(TmpNA(prefabs), TmpNA(2));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void AddComponent_InForeach_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var notMatchingArchetype = m_Manager.CreateArchetype(typeof(Component01));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                var entity = m_Manager.CreateEntity(notMatchingArchetype);
                Assert.Throws<InvalidOperationException>(() => m_Manager.AddComponent(entity, typeof(Component02)));
            }

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void RemoveComponent_InForeach_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var notMatchingArchetype = m_Manager.CreateArchetype(typeof(Component01));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                var entity = m_Manager.CreateEntity(notMatchingArchetype);
                Assert.Throws<InvalidOperationException>(() => m_Manager.RemoveComponent(entity, typeof(Component01)));
            }

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void DestroyEntity_InForeach_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var notMatchingArchetype = m_Manager.CreateArchetype(typeof(Component01));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                var entity = m_Manager.CreateEntity(notMatchingArchetype);
                Assert.Throws<InvalidOperationException>(() => m_Manager.DestroyEntity(entity));
            }

            Assert.AreEqual(OriginalEntitiesCount, EcsTestDataEntitiesCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query enumerator safety checks")]
        public void CommandBuffer_Playback_Throws()
        {
            SetupEntitiesForConsistencyCheck(OriginalEntitiesCount);

            var toInstantiate = m_Manager.CreateEntity(typeof(Component01));

            var typeHandle = new EcsTestDataAspect.TypeHandle(ref EmptySystem.CheckedStateRef);
            var query = EmptySystem.GetEntityQuery(GetRequiredComponents<EcsTestDataAspect>());

            var commandBuffer = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                var entity = commandBuffer.Instantiate(toInstantiate);
                commandBuffer.SetComponent(entity, new Component01());

                Assert.Throws<InvalidOperationException>(() => commandBuffer.Playback(m_Manager));
            }
        }

        private const int OriginalEntitiesCount = 256;
        private const int EcsTestDataValue = 0xCAFFE;

        private NativeArray<Entity> TmpNA(int count)
        {
            return CollectionHelper.CreateNativeArray<Entity>(count, World.UpdateAllocator.ToAllocator);
        }

        private NativeArray<Entity> TmpNA(params Entity[] entities)
        {
            return CollectionHelper.CreateNativeArray(entities, World.UpdateAllocator.ToAllocator);
        }

        private NativeArray<ComponentType> TmpNA(params ComponentType[] componentTypes)
        {
            return CollectionHelper.CreateNativeArray<ComponentType>(componentTypes, World.UpdateAllocator.ToAllocator);
        }

        private static void AssertValueConsistency(EcsTestDataAspect aspect)
        {
            Assert.AreEqual(
                EcsTestDataValue, aspect.TestComponent.ValueRO.value,
                "EcsTestData value is not consistent with the expected one. Entities may have been shuffled or unexpected entities where added to the manager.");
        }

        private void SetupEntitiesForConsistencyCheck(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var entity = m_Manager.CreateEntity(typeof(EcsTestData));
                m_Manager.SetComponentData(entity, new EcsTestData(EcsTestDataValue));
            }
        }

        private int Component01EntitiesCount => EmptySystem.GetEntityQuery(typeof(Component01)).CalculateEntityCount();

        private int EcsTestDataEntitiesCount => EmptySystem.GetEntityQuery(typeof(EcsTestData)).CalculateEntityCount();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // Uses the job debugger to check that the job has been explicitly completed.
        private static bool IsJobExplicitlyCompleted(JobHandle handle)
        {
            Assert.IsTrue(Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled);
            return JobHandle.CheckFenceIsDependencyOrDidSyncFence(handle, default);
        }
#endif

        private void CreateSystemAndAssertAllScheduledJobsAreCompletedAfterRunningCode(EntityQuery query, ref EcsTestDataAspect.TypeHandle typeHandle, Action code)
        {
            var system = World.CreateSystemManaged<TestSystemWithEmptyJob>();

            system.Update();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.IsFalse(IsJobExplicitlyCompleted(system.ScheduledJobHandle));
#endif

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle)) code();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.IsTrue(IsJobExplicitlyCompleted(system.ScheduledJobHandle));
#endif

            World.DestroySystemManaged(system);
            typeHandle.Update(ref EmptySystem.CheckedStateRef);
        }

        private void ScheduleJobAndAssertCodeThrows(EntityQuery query, EcsTestDataAspect.TypeHandle typeHandle, Action code)
        {
            var job = new EmptyJob();
            var handle = job.Schedule(query, default);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.IsFalse(IsJobExplicitlyCompleted(handle));
#endif

            foreach (var aspect in EcsTestDataAspect.Query(query, typeHandle))
            {
                Assert.Throws<InvalidOperationException>(() => code());
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.IsFalse(IsJobExplicitlyCompleted(handle));
#endif

            handle.Complete();
        }
    }
}
