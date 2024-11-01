using System;
using NUnit.Framework;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests
{
    partial class JobSafetyTests : ECSTestsFixture
    {
        struct TestIncrementJob : IJob
        {
            public NativeArray<Entity> entities;
            public ComponentLookup<EcsTestData> data;
            public void Execute()
            {
                for (int i = 0; i != entities.Length; i++)
                {
                    var entity = entities[i];

                    var d = data[entity];
                    d.value++;
                    data[entity] = d;
                }
            }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void ComponentAccessAfterScheduledJobThrows()
        {
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));

            var job = new TestIncrementJob();
            job.entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            job.data = m_Manager.GetComponentLookup<EcsTestData>();

            Assert.AreEqual(42, job.data[job.entities[0]].value);

            var fence = job.Schedule();

            Assert.Throws<System.InvalidOperationException>(() =>
            {
                var f = job.data[job.entities[0]].value;
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                f.GetHashCode();
            });

            fence.Complete();
            Assert.AreEqual(43, job.data[job.entities[0]].value);
        }
#endif

        struct UseComponentLookup : IJob
        {
            public ComponentLookup<EcsTestData> data;
            public void Execute()
            {
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ComponentLookup_UseAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var testDatas = m_Manager.GetComponentLookup<EcsTestData>();

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates ComponentLookup

            Assert.That(() => { var f = testDatas[entity].value; },
                Throws.Exception.TypeOf<ObjectDisposedException>()
                    .With.Message.Contains(
                        "ComponentLookup<Unity.Entities.Tests.EcsTestData> which has been invalidated by a structural change"));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ComponentLookup_UseFromJobAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var testDatas = m_Manager.GetComponentLookup<EcsTestData>();

            var job = new UseComponentLookup();
            job.data = testDatas;

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates ComponentLookup

            Assert.That(() => { job.Schedule().Complete(); },
                Throws.Exception.TypeOf<InvalidOperationException>()
                    .With.Message.Contains(
                        "ComponentLookup<Unity.Entities.Tests.EcsTestData> UseComponentLookup.data which has been invalidated by a structural change."));
        }

        struct UseBufferLookup : IJob
        {
            public BufferLookup<EcsIntElement> data;
            public void Execute()
            {
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void BufferLookup_UseAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            var testDatas = m_Manager.GetBufferLookup<EcsIntElement>();

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates BufferLookup

            Assert.That(() => { var f = testDatas[entity]; },
                Throws.Exception.TypeOf<ObjectDisposedException>()
                    .With.Message.Contains(
                        "BufferLookup<Unity.Entities.Tests.EcsIntElement> which has been invalidated by a structural change."));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void BufferLookup_UseFromJobAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            var testDatas = m_Manager.GetBufferLookup<EcsIntElement>();

            var job = new UseBufferLookup();
            job.data = testDatas;

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates BufferLookup

            Assert.That(() => { job.Schedule().Complete(); },
                Throws.Exception.TypeOf<InvalidOperationException>()
                    .With.Message.Contains(
                        "BufferLookup<Unity.Entities.Tests.EcsIntElement> UseBufferLookup.data which has been invalidated by a structural change."));
        }

        [Test]
        public void GetComponentCompletesJob()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            var job = new TestIncrementJob();
            job.entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            job.data = m_Manager.GetComponentLookup<EcsTestData>();
            query.AddDependency(job.Schedule());

            // Implicit Wait for job, returns value after job has completed.
            Assert.AreEqual(1, m_Manager.GetComponentData<EcsTestData>(entity).value);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void ChunkEntityArrayIsImmutable()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var chunk = m_Manager.GetChunk(entity);

            var array = chunk.GetNativeArray(m_Manager.GetEntityTypeHandle());
            Assert.Throws<InvalidOperationException>(() => { array[0] = Entity.Null; });
            // Dispose appears to get ignored
            array.Dispose();

            // Make sure nothing was mutated or disposed...
            array = chunk.GetNativeArray(m_Manager.GetEntityTypeHandle());
            Assert.AreEqual(entity ,array[0]);
        }
#endif

        [Test]
        public void DestroyEntityCompletesScheduledJobs()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            /*var entity2 =*/ m_Manager.CreateEntity(typeof(EcsTestData));
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            var job = new TestIncrementJob();
            job.entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            job.data = m_Manager.GetComponentLookup<EcsTestData>();
            query.AddDependency(job.Schedule());

            m_Manager.DestroyEntity(entity);

            var componentData = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);

            // @TODO: This is maybe a little bit dodgy way of determining if the job has been completed...
            //        Probably should expose api to inspector job debugger state...
            Assert.AreEqual(1, componentData.Length);
            Assert.AreEqual(1, componentData[0].value);
        }

        // This does what normal TearDown does, minus shutting down engine subsystems
        private void CleanupWorld()
        {
            if (World != null && World.IsCreated)
            {
                // Clean up systems before calling CheckInternalConsistency because we might have filters etc
                // holding on SharedComponentData making checks fail
                while (World.Systems.Count > 0)
                {
                    World.DestroySystemManaged(World.Systems[0]);
                }

                m_ManagerDebug.CheckInternalConsistency();

                World.Dispose();
                World = null;

                World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
                m_PreviousWorld = null;
                m_Manager = default;
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void EntityManagerDestructionDetectsUnregisteredJob()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("job is still running"));

            /*var entity =*/ m_Manager.CreateEntity(typeof(EcsTestData));
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            var job = new TestIncrementJob();
            job.entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            job.data = m_Manager.GetComponentLookup<EcsTestData>();
            var jobHandle = job.Schedule();

            // This should detect the unregistered running job & emit the expected error message
            CleanupWorld();

            // Manually complete the job before cleaning up for real
            jobHandle.Complete();
            CleanupWorld();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void DestroyEntityDetectsUnregisteredJob()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            var job = new TestIncrementJob();
            job.entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            job.data = m_Manager.GetComponentLookup<EcsTestData>();
            var fence = job.Schedule();

            Assert.Throws<System.InvalidOperationException>(() => { m_Manager.DestroyEntity(entity); });

            fence.Complete();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void GetComponentDetectsUnregisteredJob()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            var job = new TestIncrementJob();
            job.entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            job.data = m_Manager.GetComponentLookup<EcsTestData>();
            var jobHandle = job.Schedule();

            Assert.Throws<System.InvalidOperationException>(() => { m_Manager.GetComponentData<EcsTestData>(entity); });

            jobHandle.Complete();
        }

        struct EntityOnlyDependencyJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
            }
        }

        struct NoDependenciesJob : IJobChunk
        {
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
            }
        }

        partial class EntityOnlyDependencySystem : SystemBase
        {
            public JobHandle JobHandle;
            protected override void OnUpdate()
            {
                EntityManager.CreateEntity(typeof(EcsTestData));
                var query = GetEntityQuery(new ComponentType[] {});
                var job = new EntityOnlyDependencyJob
                {
                    EntityTypeHandle = EntityManager.GetEntityTypeHandle()
                };
                Dependency = JobHandle = job.ScheduleParallel(query, Dependency);
            }
        }

        partial class NoComponentDependenciesSystem : SystemBase
        {
            public JobHandle JobHandle;
            protected override void OnUpdate()
            {
                EntityManager.CreateEntity(typeof(EcsTestData));
                var query = GetEntityQuery(new ComponentType[] {});
                var job = new NoDependenciesJob {};

                Dependency = JobHandle = job.ScheduleParallel(query, Dependency);
            }
        }

        partial class DestroyAllEntitiesSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var allEntities = EntityManager.GetAllEntities();
                EntityManager.DestroyEntity(allEntities);
                allEntities.Dispose();
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void StructuralChangeCompletesEntityOnlyDependencyJob()
        {
            var system = World.GetOrCreateSystemManaged<EntityOnlyDependencySystem>();
            system.Update();
            World.GetOrCreateSystemManaged<DestroyAllEntitiesSystem>().Update();
            Assert.IsTrue(JobHandle.CheckFenceIsDependencyOrDidSyncFence(system.JobHandle, new JobHandle()));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void StructuralChangeCompletesNoComponentDependenciesJob()
        {
            var system = World.GetOrCreateSystemManaged<NoComponentDependenciesSystem>();
            system.Update();
            World.GetOrCreateSystemManaged<DestroyAllEntitiesSystem>().Update();
            Assert.IsTrue(JobHandle.CheckFenceIsDependencyOrDidSyncFence(system.JobHandle, new JobHandle()));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void StructuralChangeAfterSchedulingNoDependenciesJobThrows()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entity = m_Manager.CreateEntity(archetype);
            var query = EmptySystem.GetEntityQuery(typeof(EcsTestData));
            var handle = new NoDependenciesJob().ScheduleParallel(query, default);
            Assert.Throws<InvalidOperationException>(() => m_Manager.DestroyEntity(entity));
            handle.Complete();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void StructuralChangeAfterSchedulingEntityOnlyDependencyJobThrows()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entity = m_Manager.CreateEntity(archetype);
            var query = EmptySystem.GetEntityQuery(typeof(EcsTestData));
            var handle = new EntityOnlyDependencyJob {EntityTypeHandle = m_Manager.GetEntityTypeHandle()}.ScheduleParallel(query, default);
            Assert.Throws<InvalidOperationException>(() => m_Manager.DestroyEntity(entity));
            handle.Complete();
        }

        partial class SharedComponentSystem : SystemBase
        {
            EntityQuery query;
            protected override void OnCreate()
            {
                query = GetEntityQuery(ComponentType.ReadOnly<EcsTestSharedComp>());
            }

            struct SharedComponentJobChunk : IJobChunk
            {
                [ReadOnly] public SharedComponentTypeHandle<EcsTestSharedComp> EcsTestSharedCompTypeHandle;
                public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                {
                }
            }

            protected override void OnUpdate()
            {
                Dependency = new SharedComponentJobChunk
                {
                    EcsTestSharedCompTypeHandle = GetSharedComponentTypeHandle<EcsTestSharedComp>()
                }.ScheduleParallel(query, Dependency);
            }
        }

        [Test]
        public void JobsUsingArchetypeChunkSharedComponentTypeSyncOnStructuralChange()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var entity = m_Manager.CreateEntity(archetype);

            var sharedComponentSystem = World.CreateSystemManaged<SharedComponentSystem>();

            sharedComponentSystem.Update();
            // DestroyEntity should sync the job and not cause any safety error
            m_Manager.DestroyEntity(entity);
        }

        partial struct BufferSafetyJobA : IJobEntity
        {
            public NativeArray<int> MyArray;

            public void Execute(in DynamicBuffer<EcsIntElement> buffer)
            {
                MyArray[0] = new EcsIntElement
                {
                    Value = buffer[0].Value + buffer[1].Value + buffer[2].Value,
                };
            }
        }

        struct BufferSafetyJobB : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<Entity> Entities;

            [ReadOnly]
            public BufferLookup<EcsIntElement> BuffersLookup;

            public void Execute(int index)
            {
                var buffer = BuffersLookup[Entities[index]];
                var total = buffer[3].Value;
            }
        }

        partial struct BufferSafetyJobC : IJobEntity
        {
            [ReadOnly]
            public BufferLookup<EcsIntElement> BufferLookupRo;

            public NativeArray<int> MyArray;

            public void Execute(Entity entity)
            {
                var buffer = BufferLookupRo[entity];
                MyArray[0] = new EcsIntElement
                {
                    Value = buffer[0].Value + buffer[1].Value + buffer[2].Value,
                };
            }
        }

        public void SetupDynamicBufferJobTestEnvironment()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsIntElement), typeof(EcsTestData), typeof(EcsTestData2));
            var entity = m_Manager.CreateEntity(archetype);
            var buffer = m_Manager.GetBuffer<EcsIntElement>(entity);
            buffer.Add(new EcsIntElement { Value = 1 });
            buffer.Add(new EcsIntElement { Value = 10 });
            buffer.Add(new EcsIntElement { Value = 100 });
            buffer.Add(new EcsIntElement { Value = 0 });
        }

        partial class TwoJobsDynamicBufferTestSystem : SystemBase
        {
            EntityQuery _query;
            BufferLookup<EcsIntElement> _bfe;
            protected override void OnCreate()
            {
                _query = GetEntityQuery(typeof(EcsIntElement));
                _bfe = GetBufferLookup<EcsIntElement>(true);
            }

            protected override void OnUpdate()
            {
                var jobA = new BufferSafetyJobA{MyArray = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(1, ref World.UpdateAllocator) };
                var jobAHandle = jobA.Schedule(_query, default(JobHandle));

                var jobB = new BufferSafetyJobB
                {
                    Entities = _query.ToEntityArray(World.UpdateAllocator.ToAllocator),
                    BuffersLookup = _bfe,
                };
                var jobBHandle = jobB.Schedule(jobB.Entities.Length, 1, default(JobHandle));
                jobBHandle.Complete();
                jobAHandle.Complete();
            }
        }


        [Test]
        public void TwoJobsUsingDynamicBuffersDontCauseSafetySystemFalsePositiveErrors()
        {
            SetupDynamicBufferJobTestEnvironment();
            var sys = World.CreateSystemManaged<TwoJobsDynamicBufferTestSystem>();
            sys.Update();
        }

        partial class TwoJobsDynamicBufferROTestSystem : SystemBase
        {
            EntityQuery _query;
            BufferLookup<EcsIntElement> _bfe;
            protected override void OnCreate()
            {
                _query = GetEntityQuery(typeof(EcsIntElement));
                _bfe = GetBufferLookup<EcsIntElement>(true);
            }

            protected override void OnUpdate()
            {
                _bfe.Update(this);
                var jobA = new BufferSafetyJobC
                {
                    BufferLookupRo = _bfe,
                    MyArray = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(1, ref World.UpdateAllocator)
                };
                var jobAHandle = jobA.Schedule(_query, default(JobHandle));

                var jobB = new BufferSafetyJobB
                {
                    Entities = _query.ToEntityArray(World.UpdateAllocator.ToAllocator),
                    BuffersLookup = _bfe,
                };
                var jobBHandle = jobB.Schedule(jobB.Entities.Length, 1, default(JobHandle));

                jobAHandle.Complete();
                jobBHandle.Complete();
            }
        }

        [Test]
        public void TwoJobsUsingReadOnlyDynamicBuffersCanRunInParallel_BufferLookup()
        {
            SetupDynamicBufferJobTestEnvironment();
            var sys = World.CreateSystemManaged<TwoJobsDynamicBufferROTestSystem>();
            sys.Update();
        }

        partial struct BufferSafetyJob_TwoReadOnly : IJobEntity
        {
            [ReadOnly]
            public BufferLookup<EcsIntElement> BuffersLookup;

            public void Execute(Entity e, in DynamicBuffer<EcsIntElement> bufferA)
            {
                var bufferB = BuffersLookup[e];

                var totalA = bufferA[0] + bufferA[1] + bufferA[2];
                var totalB = bufferB[0] + bufferB[1] + bufferB[2];
            }
        }

        partial struct BufferSafetyJob_OneRead_OneWrite : IJobEntity
        {
            public BufferLookup<EcsIntElement> BuffersLookup;

            public void Execute(Entity e, in DynamicBuffer<EcsIntElement> bufferA)
            {
                var bufferB = BuffersLookup[e];

                var totalA = bufferA[0] + bufferA[1] + bufferA[2];
                var totalB = bufferB[0] + bufferB[1] + bufferB[2];
                bufferB[3] = new EcsIntElement {Value = totalB};
            }
        }

        unsafe partial struct BufferSafetyJob_GetUnsafePtrReadWrite : IJobEntity
        {
            public void Execute(DynamicBuffer<EcsIntElement> b0, in EcsTestData c1, in EcsTestData2 c2)
            {
                b0.GetUnsafePtr();
            }
        }

        public partial class TestSystem : SystemBase
        {
            public void BufferSafetyJob_GetUnsafePtrReadWrite_Run()
            {
                var job = new BufferSafetyJob_GetUnsafePtrReadWrite();
                job.Run();
            }
            public void BufferSafetyJob_TwoReadOnly_Run()
            {
                var job = new BufferSafetyJob_TwoReadOnly
                {
                    BuffersLookup = GetBufferLookup<EcsIntElement>(true)
                };
                job.Run();
            }

            public void BufferSafetyJob_OneRead_OneWrite_Run()
            {
                var job = new BufferSafetyJob_OneRead_OneWrite
                {
                    BuffersLookup = GetBufferLookup<EcsIntElement>(false)
                };
                job.Run();
            }

            protected override void OnUpdate() { }
        }

        TestSystem _testSystem => World.GetOrCreateSystemManaged<TestSystem>();

        [Test]
        public void SingleJobUsingSameReadOnlyDynamicBuffer()
        {
            SetupDynamicBufferJobTestEnvironment();
            _testSystem.BufferSafetyJob_TwoReadOnly_Run();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void SingleJobUsingSameReadOnlyAndReadWriteDynamicBufferThrows()
        {
            SetupDynamicBufferJobTestEnvironment();
            Assert.Throws<InvalidOperationException>(() =>
            {
                _testSystem.BufferSafetyJob_OneRead_OneWrite_Run();
            });
        }

        [Test]
        public void DynamicBuffer_UnsafePtr_DoesntThrowWhenReadWrite()
        {
            SetupDynamicBufferJobTestEnvironment();
            _testSystem.BufferSafetyJob_GetUnsafePtrReadWrite_Run();
        }

        public partial class DynamicBufferReadOnlySystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .ForEach((
                    Entity e,
                    in DynamicBuffer<EcsIntElement> buffers) =>
                    {
                        unsafe
                        {
                            var ptr = buffers.GetUnsafeReadOnlyPtr();
                        }
                    }).Run();
            }
        }

        [Test]
        public void DynamicBuffer_UnsafeReadOnlyPtr_DoesntThrowWhenReadOnly()
        {
            var ent = m_Manager.CreateEntity(typeof(EcsIntElement));
            var sys = World.CreateSystemManaged<DynamicBufferReadOnlySystem>();
            sys.Update();
            m_Manager.DestroyEntity(ent);
            World.DestroySystemManaged(sys);
        }
    }
}
