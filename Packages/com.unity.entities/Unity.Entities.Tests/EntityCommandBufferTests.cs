using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests
{
    [BurstCompile]
    partial class EntityCommandBufferTests : ECSTestsFixture
    {
        World m_World2;
        EntityManager m_Manager2;
        EntityManager.EntityManagerDebug m_ManagerDebug2;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            m_World2 = new World("Test World 2");
            m_Manager2 = m_World2.EntityManager;
            m_ManagerDebug2 = new EntityManager.EntityManagerDebug(m_Manager2);
        }

        [TearDown]
        public override void TearDown()
        {
            if (m_World2.IsCreated)
            {
                // Clean up systems before calling CheckInternalConsistency because we might have filters etc
                // holding on SharedComponentData making checks fail
                while (m_World2.Systems.Count > 0)
                {
                    m_World2.DestroySystemManaged(m_World2.Systems[0]);
                }

                m_ManagerDebug2.CheckInternalConsistency();

                m_World2.Dispose();
                m_World2 = null;
            }

            base.TearDown();
        }

        [Test]
        public void EmptyOK()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.Playback(m_Manager);
        }

        [Test]
        public void Playback_WithSinglePlaybackPolicy_ThrowsOnMultiplePlaybacks()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.SinglePlayback);
            // First playback should succeed
            Assert.DoesNotThrow(() => {cmds.Playback(m_Manager); });
            // Subsequent playback attempts fail
            Assert.Throws<InvalidOperationException>(() => {cmds.Playback(m_Manager); });
            // Playback on a second EntityManager fails
            Assert.Throws<InvalidOperationException>(() => {cmds.Playback(m_Manager2); });
        }

        [Test]
        public void Playback_WithMultiPlaybackPolicy_DoesNotThrow()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            // First playback should succeed
            Assert.DoesNotThrow(() => {cmds.Playback(m_Manager); });
            // Subsequent playback attempts should not fail
            Assert.DoesNotThrow(() => {cmds.Playback(m_Manager); });
            // Playback on a second EntityManager also does not fail
            Assert.DoesNotThrow(() => {cmds.Playback(m_Manager2); });
        }

        unsafe bool CleanupListsAreEmpty(EntityCommandBufferChain* chain)
        {
            if (chain == null)
                return true;
            var cleanup = chain->m_Cleanup;
            if (cleanup->BufferCleanupList != null || cleanup->EntityArraysCleanupList != null)
                return false;
            return CleanupListsAreEmpty(chain->m_NextChain);
        }

        unsafe bool CleanupListsAreEmpty(EntityCommandBuffer ecb)
        {
            if (ecb.m_Data == null)
                return true;
            if (!CleanupListsAreEmpty(&ecb.m_Data->m_MainThreadChain))
                return false;
            if (ecb.m_Data->m_ThreadedChains != null)
            {
#if UNITY_2022_2_14F1_OR_NEWER
                int maxThreadCount = JobsUtility.ThreadIndexCount;
#else
                int maxThreadCount = JobsUtility.MaxJobThreadCount;
#endif
                for (int i = 0; i < maxThreadCount; ++i)
                {
                    if (!CleanupListsAreEmpty(&ecb.m_Data->m_ThreadedChains[i]))
                        return false;
                }
            }
            return true;
        }

        [Test]
        public void Cleanup_ManyChains_NoStackOverflow()
        {
            // Create an ECB with a pathologically large number of chains
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.SinglePlayback);
            var ecbp = ecb.AsParallelWriter();
            for (int sortKey = 10000; sortKey > 0; --sortKey)
            {
                ecbp.CreateEntity(sortKey, archetype);
            }
            Assert.DoesNotThrow(() => ecb.Dispose());
        }

        [Test]
        public void Playback_WithSinglePlaybackSuccess_DisposesCapturedEntityArrays()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            m_Manager.CreateEntity(archetype, 100);
            using(var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            using(var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.SinglePlayback))
            {
                cmds.AddComponent<EcsTestTag>(query, EntityQueryCaptureMode.AtRecord);
                Assert.IsFalse(CleanupListsAreEmpty(cmds), "ECB has empty cleanup lists prior to playback");

                cmds.Playback(m_Manager);
                Assert.IsFalse(CleanupListsAreEmpty(cmds), "ECB with SinglePlayback has empty cleanup lists after playback");
            }
            // TODO(DOTS-4497): at this point (after the ecb has been Disposed),
            // we should assert that the allocator passed to the ECB constructor has no remaining unreleased
            // allocations. However, that isn't currently possible.
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void Playback_WithSinglePlaybackError_DisposesCapturedEntityArrays()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            m_Manager.CreateEntity(archetype, 100);
            var ent = m_Manager.CreateEntity(archetype);
            using(var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            using(var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.SinglePlayback))
            {
                cmds.AddComponent<EcsTestData2>(ent);
                cmds.AddComponent<EcsTestTag>(query, EntityQueryCaptureMode.AtRecord); // entity array is captured here
                Assert.IsFalse(CleanupListsAreEmpty(cmds), "ECB has empty cleanup lists prior to playback");

                m_Manager.DestroyEntity(ent); // this will force an ECB playback error before the entity array command is played back
                Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
                // The entity array should still be present in the cleanup list
                Assert.IsFalse(CleanupListsAreEmpty(cmds), "ECB with SinglePlayback has empty cleanup lists after playback");
            }
            // TODO(DOTS-4497): at this point (after the ecb has been Disposed),
            // we should assert that the allocator passed to the ECB constructor has no remaining unreleased
            // allocations. However, that isn't currently possible.
        }

        [BurstCompile(CompileSynchronously = true)]
        struct TestJob : IJob
        {
            public EntityCommandBuffer Buffer;

            public void Execute()
            {
                var e = Buffer.CreateEntity();
                Buffer.AddComponent(e, new EcsTestData { value = 1 });
            }
        }

        internal partial class TestEntityCommandBufferSystem : EntityCommandBufferSystem
        {
            public unsafe struct Singleton : IComponentData, IECBSingleton
            {
                public UnsafeList<EntityCommandBuffer>* pendingBuffers;
                internal Allocator allocator;

                public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
                {
                    return EntityCommandBufferSystem.CreateCommandBuffer(ref *pendingBuffers, allocator, world);
                }

                public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
                {
                    pendingBuffers = (UnsafeList<EntityCommandBuffer>*)UnsafeUtility.AddressOf(ref buffers);
                }
                public void SetAllocator(Allocator allocatorIn)
                {
                    allocator = allocatorIn;
                }
            }
            protected override unsafe void OnCreate()
            {
                base.OnCreate();

                this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
            }
        }

        partial class TestECBPlaybackSystem : EntityCommandBufferSystem {}

        partial class TestECBRecordingSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                unsafe
                {
                    var ecb = World.GetOrCreateSystemManaged<TestECBPlaybackSystem>().CreateCommandBuffer();
                    Assert.AreEqual(ecb.OriginSystemHandle, m_StatePtr->m_Handle);
                }
            }
        }

        [Test]
        public void EntityCommandBuffer_RecordingSystem_ValidHandle()
        {
            using (var world = new World("World A"))
            {
                var sim = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
                var ecbRecordingSystem = world.GetOrCreateSystemManaged<TestECBRecordingSystem>();
                sim.AddSystemToUpdateList(ecbRecordingSystem);

                var pres = world.GetOrCreateSystemManaged<PresentationSystemGroup>();
                var ecbPlaybackSystem = world.GetOrCreateSystemManaged<TestECBPlaybackSystem>();
                pres.AddSystemToUpdateList(ecbPlaybackSystem);

                world.Update();
            }
        }

        [BurstCompile]
        internal partial struct TestECBSystemInteractionSystem : ISystem
        {
            public Entity DeferredEntity;

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                var ecb = SystemAPI.GetSingletonRW<TestEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

                DeferredEntity = ecb.CreateEntity();
                ecb.AddComponent(DeferredEntity, new EcsTestData(43));
            }
        }

        [Test]
        public void CanCreateCommandBuffer_FromBurstedISystem_AndPlaybackSucceeds()
        {
            var barrier = World.GetOrCreateSystemManaged<TestEntityCommandBufferSystem>();
            var s = World.GetOrCreateSystem<TestECBSystemInteractionSystem>();
            s.Update(World.Unmanaged);
            barrier.FlushPendingBuffers(true);

            var q = World.EntityManager.CreateEntityQuery(typeof(EcsTestData));
            Assert.AreEqual(World.EntityManager.GetComponentData<EcsTestData>(q.ToEntityArray(Allocator.Temp)[0])
                    .value,
                43);
        }


#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void EntityCommandBufferSystem_DisposeAfterPlaybackError_Succeeds()
        {
            TestEntityCommandBufferSystem barrier = World.GetOrCreateSystemManaged<TestEntityCommandBufferSystem>();
            EntityCommandBuffer cmds = barrier.CreateCommandBuffer();

            // Schedule a job that writes concurrently to the ECB
            const int kCreateCount = 256;
            var job = new TestParallelJob
            {
                CommandBuffer = cmds.AsParallelWriter(),
            }.Schedule(kCreateCount, 64);
            // Intentionally omit this call, to trigger a safety manager exception during playback.
            //barrier.AddJobHandleForProducer(job)

            // This should throw an error; the job is still writing to the buffer we're playing back.
            Assert.Throws<ArgumentException>(() => { barrier.FlushPendingBuffers(true); }); // playback & dispose ECBs

            // ...but the ECB should have been successfully disposed.
            Assert.AreEqual(1, barrier.PendingBuffers.Length);
            Assert.IsFalse(barrier.PendingBuffers[0].IsCreated);

            job.Complete();
        }

        [Test]
        public void EntityCommandBufferConcurrent_PlaybackDuringWrite_UsesCustomOwnerTypeName()
        {
            using EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            const int kCreateCount = 10000;
            var job = new TestParallelJob
            {
                CommandBuffer = cmds.AsParallelWriter(),
            }.Schedule(kCreateCount, 64);
            Assert.That(() => cmds.Playback(m_Manager), Throws.InvalidOperationException.With.Message.Contains("EntityCommandBuffer"));
            job.Complete();
        }

        [Test]
        public void SingleWriterEnforced()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            var job = new TestJob {Buffer = cmds};

            var e = cmds.CreateEntity();
            cmds.AddComponent(e, new EcsTestData { value = 42 });

            var handle = job.Schedule();

            Assert.Throws<InvalidOperationException>(() => { cmds.CreateEntity(); });
            Assert.Throws<InvalidOperationException>(() => { job.Buffer.CreateEntity(); });

            handle.Complete();

            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager2);

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var arr = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(2, arr.Length);
            Assert.AreEqual(42, arr[0].value);
            Assert.AreEqual(1, arr[1].value);
            query.Dispose();

            var query2 = m_Manager2.CreateEntityQuery(typeof(EcsTestData));
            var arr2 = query2.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(2, arr2.Length);
            Assert.AreEqual(42, arr2[0].value);
            Assert.AreEqual(1, arr2[1].value);
            query2.Dispose();
        }

        [Test]
        public void DisposeWhileJobRunningThrows()
        {
            var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var job = new TestJob {Buffer = cmds};

            var handle = job.Schedule();

            Assert.Throws<InvalidOperationException>(() => { cmds.Dispose(); });

            handle.Complete();
        }

        [Test]
        public void ModifiesWhileJobRunningThrows()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var job = new TestJob {Buffer = cmds};

            var handle = job.Schedule();

            Assert.Throws<InvalidOperationException>(() => { cmds.CreateEntity(); });

            handle.Complete();
        }

        [Test]
        public void PlaybackWhileJobRunningThrows()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var job = new TestJob {Buffer = cmds};

            var handle = job.Schedule();

            Assert.Throws<InvalidOperationException>(() => { cmds.Playback(m_Manager); });

            handle.Complete();
        }
#endif

        struct EntityCommandBufferPlaybackJob : IJob
        {
            public EntityCommandBuffer Buffer;
            public ExclusiveEntityTransaction Manager;

            public void Execute()
            {
                Buffer.Playback(Manager);
            }
        }

        [Test]
        public void PlaybackWithExclusiveEntityTransactionInJob()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var job = new TestJob {Buffer = cmds};

            var e = cmds.CreateEntity();
            cmds.AddComponent(e, new EcsTestData { value = 42 });

            e = cmds.CreateEntity();
            cmds.AddSharedComponent(e, new EcsTestSharedComp(19));

            var jobHandle = job.Schedule();

            var manager = m_Manager.BeginExclusiveEntityTransaction();

            var playbackJob = new EntityCommandBufferPlaybackJob()
            {
                Buffer = cmds,
                Manager = manager
            };

            m_Manager.ExclusiveEntityTransactionDependency = playbackJob.Schedule(jobHandle);
            m_Manager.EndExclusiveEntityTransaction();

            var group = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var arr = group.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(2, arr.Length);
            Assert.AreEqual(42, arr[0].value);
            Assert.AreEqual(1, arr[1].value);
            group.Dispose();

            var sharedGroup = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp));
            var entities = sharedGroup.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, entities.Length);
            Assert.AreEqual(19, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entities[0]).value);
            sharedGroup.Dispose();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct TestParallelJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            public void Execute(int index)
            {
                var e = CommandBuffer.CreateEntity(index);
                CommandBuffer.AddComponent(index, e, new EcsTestData {value = index});
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void EntityCommandBufferConcurrent_PlaybackDuringWrite_ThrowsInvalidOperation()
        {
            using EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            const int kCreateCount = 10000;
            var job = new TestParallelJob
            {
                CommandBuffer = cmds.AsParallelWriter(),
            }.Schedule(kCreateCount, 64);
            Assert.Throws<InvalidOperationException>(() => { cmds.Playback(m_Manager); });
            job.Complete();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void EntityCommandBufferConcurrent_DisposeDuringWrite_ThrowsInvalidOperation()
        {
            EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            const int kCreateCount = 10000;
            var job = new TestParallelJob
            {
                CommandBuffer = cmds.AsParallelWriter(),
            }.Schedule(kCreateCount, 64);
            Assert.Throws<InvalidOperationException>(() => { cmds.Dispose(); });
            job.Complete();
        }

        [Test]
        public void CreateEntity()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            var e = cmds.CreateEntity();
            cmds.AddComponent(e, new EcsTestData { value = 12 });
            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager2);
            {
                var group = m_Manager.CreateEntityQuery(typeof(EcsTestData));
                var arr = group.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, arr.Length);
                Assert.AreEqual(12, arr[0].value);
                group.Dispose();
            }
            {
                var group = m_Manager2.CreateEntityQuery(typeof(EcsTestData));
                var arr = group.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, arr.Length);
                Assert.AreEqual(12, arr[0].value);
                group.Dispose();
            }
        }

        [Test]
        public void CreateEntityWithArchetype()
        {
            var a = m_Manager.CreateArchetype(typeof(EcsTestData));

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            var e = cmds.CreateEntity(a);
            cmds.SetComponent(e, new EcsTestData { value = 12 });
            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager);

            {
                var group = m_Manager.CreateEntityQuery(typeof(EcsTestData));
                var arr = group.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(2, arr.Length);
                Assert.AreEqual(12, arr[0].value);
                Assert.AreEqual(12, arr[1].value);
                group.Dispose();
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void CreateEntityWithArchetype_InvalidThrows()
        {
            var a = new EntityArchetype();
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            Assert.Throws<ArgumentException>(() => cmds.CreateEntity(a));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void CreateEntityWithArchetype_Parallel_InvalidThrows()
        {
            var a = new EntityArchetype();
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var cmds = ecb.AsParallelWriter();
            Assert.Throws<ArgumentException>(() => cmds.CreateEntity(0, a));
        }

        [Test]
        public void CreateTwoComponents()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            var e = cmds.CreateEntity();
            cmds.AddComponent(e, new EcsTestData { value = 12 });
            cmds.AddComponent(e, new EcsTestData2 { value0 = 1, value1 = 2 });
            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager2);

            {
                var group = m_Manager.CreateEntityQuery(typeof(EcsTestData));
                var arr = group.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, arr.Length);
                Assert.AreEqual(12, arr[0].value);
                group.Dispose();
            }

            {
                var group = m_Manager.CreateEntityQuery(typeof(EcsTestData2));
                var arr = group.ToComponentDataArray<EcsTestData2>(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, arr.Length);
                Assert.AreEqual(1, arr[0].value0);
                Assert.AreEqual(2, arr[0].value1);
                group.Dispose();
            }

            {
                var group = m_Manager2.CreateEntityQuery(typeof(EcsTestData));
                var arr = group.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, arr.Length);
                Assert.AreEqual(12, arr[0].value);
                group.Dispose();
            }

            {
                var group = m_Manager2.CreateEntityQuery(typeof(EcsTestData2));
                var arr = group.ToComponentDataArray<EcsTestData2>(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, arr.Length);
                Assert.AreEqual(1, arr[0].value0);
                Assert.AreEqual(2, arr[0].value1);
                group.Dispose();
            }
        }

        [Test]
        public void TestMultiChunks()
        {
            const int count = 65536;

            var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            cmds.MinimumChunkSize = 512;

            for (int i = 0; i < count; i++)
            {
                var e = cmds.CreateEntity();
                cmds.AddComponent(e, new EcsTestData { value = i });
                cmds.AddComponent(e, new EcsTestData2 { value0 = i, value1 = i });
            }

            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager2);

            {
                var group = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2));
                var arr = group.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                var arr2 = group.ToComponentDataArray<EcsTestData2>(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(count, arr.Length);
                for (int i = 0; i < count; ++i)
                {
                    FastAssert.AreEqual(i, arr[i].value);
                    FastAssert.AreEqual(i, arr2[i].value0);
                    FastAssert.AreEqual(i, arr2[i].value1);
                }
                group.Dispose();
            }

            {
                var group = m_Manager2.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2));
                var arr = group.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                var arr2 = group.ToComponentDataArray<EcsTestData2>(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(count, arr.Length);
                for (int i = 0; i < count; ++i)
                {
                    FastAssert.AreEqual(i, arr[i].value);
                    FastAssert.AreEqual(i, arr2[i].value0);
                    FastAssert.AreEqual(i, arr2[i].value1);
                }
                group.Dispose();
            }
            cmds.Dispose();
        }

        [Test]
        public void AddSharedComponent()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var entity = m_Manager.CreateEntity();
            cmds.AddSharedComponent(entity, new EcsTestSharedComp(10));
            cmds.AddSharedComponent(entity, new EcsTestSharedComp2(20));
            cmds.AddSharedComponent(entity, new EcsTestSharedComp3(0));

            cmds.Playback(m_Manager);

            Assert.AreEqual(10, m_Manager.GetSharedComponent<EcsTestSharedComp>(entity).value);
            Assert.AreEqual(20, m_Manager.GetSharedComponent<EcsTestSharedComp2>(entity).value1);
            Assert.AreEqual(0, m_Manager.GetSharedComponent<EcsTestSharedComp3>(entity).value0);
            Assert.AreEqual(0, m_Manager.GetSharedComponentIndex<EcsTestSharedComp3>(entity));
        }

        [Test]
        public void AddSharedComponentDefault()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);

            var e = cmds.CreateEntity();
            cmds.AddSharedComponent(e, new EcsTestSharedComp(10));
            cmds.AddSharedComponent(e, new EcsTestSharedComp2(20));

            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager2);

            {
                var sharedComp1List = new List<EcsTestSharedComp>();
                var sharedComp2List = new List<EcsTestSharedComp2>();

                m_Manager.GetAllUniqueSharedComponentsManaged(sharedComp1List);
                m_Manager.GetAllUniqueSharedComponentsManaged(sharedComp2List);

                // the count must be 2 - the default value of the shared component and the one we actually set
                Assert.AreEqual(2, sharedComp1List.Count);
                Assert.AreEqual(2, sharedComp2List.Count);

                Assert.AreEqual(10, sharedComp1List[1].value);
                Assert.AreEqual(20, sharedComp2List[1].value1);
            }
            {
                var sharedComp1List = new List<EcsTestSharedComp>();
                var sharedComp2List = new List<EcsTestSharedComp2>();

                m_Manager2.GetAllUniqueSharedComponentsManaged(sharedComp1List);
                m_Manager2.GetAllUniqueSharedComponentsManaged(sharedComp2List);

                // the count must be 2 - the default value of the shared component and the one we actually set
                Assert.AreEqual(2, sharedComp1List.Count);
                Assert.AreEqual(2, sharedComp2List.Count);

                Assert.AreEqual(10, sharedComp1List[1].value);
                Assert.AreEqual(20, sharedComp2List[1].value1);
            }
        }

        [Test]
        public void AddComponent()
        {
            var e = m_Manager.CreateEntity();

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            cmds.AddComponent(e, new EcsTestData(10));
            cmds.AddComponent<EcsTestTag>(e);
            cmds.AddComponent(e, ComponentType.ReadWrite<EcsTestData3>());

            cmds.Playback(m_Manager);

            Assert.AreEqual(10, m_Manager.GetComponentData<EcsTestData>(e).value);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(e));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData3>(e));
        }

        [Test]
        public unsafe void UnsafeAddComponent()
        {
            var e = m_Manager.CreateEntity();

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var component = new EcsTestData(10);
            var typeIndex = TypeManager.GetTypeIndex<EcsTestData>();
            var typeSize = TypeManager.GetTypeInfo(typeIndex).TypeSize;
            cmds.UnsafeAddComponent(e, typeIndex, typeSize, &component);

            cmds.Playback(m_Manager);

            Assert.AreEqual(10, m_Manager.GetComponentData<EcsTestData>(e).value);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public unsafe void UnsafeAddComponent_NullPtr_Asserts()
        {
            var e = m_Manager.CreateEntity();

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var typeIndex = TypeManager.GetTypeIndex<EcsTestData>();
            var typeSize = TypeManager.GetTypeInfo(typeIndex).TypeSize;

            try
            {
                cmds.UnsafeAddComponent(e, typeIndex, typeSize, null);
            }
            catch (Exception exception)
            {
                if (exception.Message.Contains("componentDataPtr is null!"))
                    return;

                throw;
            }

            Assert.Fail("Did not catch expected ArgumentException exception.");
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public unsafe void UnsafeSetComponent_NullPtr_Asserts()
        {
            var e = m_Manager.CreateEntity();

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var typeIndex = TypeManager.GetTypeIndex<EcsTestData>();
            var typeSize = TypeManager.GetTypeInfo(typeIndex).TypeSize;
            cmds.AddComponent<EcsTestData>(e);

            try
            {
                cmds.UnsafeSetComponent(e, typeIndex, typeSize, null);
            }
            catch (Exception exception)
            {
                if (exception.Message.Contains("componentDataPtr is null!"))
                    return;

                throw;
            }

            Assert.Fail("Did not catch expected ArgumentException exception.");
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public unsafe void UnsafeAddComponent_WrongSize_Asserts()
        {
            var e = m_Manager.CreateEntity();

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var component = new EcsTestData(10);
            var typeIndex = TypeManager.GetTypeIndex<EcsTestData>();
            var typeSize = TypeManager.GetTypeInfo(typeIndex).TypeSize * 2;

            try
            {
                cmds.UnsafeAddComponent(e, typeIndex, typeSize, &component);
            }
            catch (Exception exception)
            {
                if (exception.Message.Contains("Type size does not match TypeManager's size!"))
                    return;

                throw;
            }

            Assert.Fail("Did not catch expected ArgumentException exception.");
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public unsafe void UnsafeSetComponent_WrongSize_Asserts()
        {
            var e = m_Manager.CreateEntity();

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var component = new EcsTestData(10);
            var typeIndex = TypeManager.GetTypeIndex<EcsTestData>();
            var typeSize = TypeManager.GetTypeInfo(typeIndex).TypeSize * 2;
            cmds.AddComponent<EcsTestData>(e);

            try
            {
                cmds.UnsafeSetComponent(e, typeIndex, typeSize, &component);
            }
            catch (Exception exception)
            {
                if (exception.Message.Contains("Type size does not match TypeManager's size!"))
                    return;

                throw;
            }

            Assert.Fail("Did not catch expected ArgumentException exception.");
        }

        [Test]
        public unsafe void UnsafeSetComponent()
        {
            var e = m_Manager.CreateEntity();

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var component = new EcsTestData(10);
            var typeIndex = TypeManager.GetTypeIndex<EcsTestData>();
            var typeSize = TypeManager.GetTypeInfo(typeIndex).TypeSize;
            cmds.AddComponent<EcsTestData>(e);
            cmds.UnsafeSetComponent(e, typeIndex, typeSize, &component);

            cmds.Playback(m_Manager);

            Assert.AreEqual(10, m_Manager.GetComponentData<EcsTestData>(e).value);
        }

        [Test]
        public void AddComponent_Multiple_NoneExistPrior()
        {
            var e = m_Manager.CreateEntity();

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent(e, new ComponentTypeSet(typeof(EcsTestTag), typeof(EcsTestData3)));
            cmds.Playback(m_Manager);

            Assert.IsTrue(m_Manager.HasComponent<Simulate>(e)); // implicitly added to all entities
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(e));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData3>(e));
            Assert.AreEqual(3, m_Manager.GetComponentCount(e));
        }

        [Test]
        public void AddComponents_SomeExistPrior()
        {
            var e = m_Manager.CreateEntity();
            m_Manager.AddComponent<EcsTestData3>(e);

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent(e, new ComponentTypeSet(typeof(EcsTestTag), typeof(EcsTestData3)));
            cmds.Playback(m_Manager);

            Assert.IsTrue(m_Manager.HasComponent<Simulate>(e)); // implicitly added to all entities
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(e));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData3>(e));
            Assert.AreEqual(3, m_Manager.GetComponentCount(e));
        }

        [Test]
        public void AddComponents_AllExistPrior()
        {
            var e = m_Manager.CreateEntity();
            m_Manager.AddComponent<EcsTestData3>(e);
            m_Manager.AddComponent<EcsTestTag>(e);

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent(e, new ComponentTypeSet(typeof(EcsTestTag), typeof(EcsTestData3)));
            cmds.Playback(m_Manager);

            Assert.IsTrue(m_Manager.HasComponent<Simulate>(e)); // implicitly added to all entities
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(e));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData3>(e));
            Assert.AreEqual(3, m_Manager.GetComponentCount(e));
        }


        [BurstCompile(CompileSynchronously = true)]
        struct TestParallelJob_AddComponents : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            public void Execute(int index)
            {
                var e = CommandBuffer.CreateEntity(index);
                var types = new ComponentTypeSet(ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>());
                CommandBuffer.AddComponent(index, e, types);
            }
        }

        [Test]
        public void AddComponents_Parallel()
        {
            using EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            const int kCreateCount = 10000;  // the bigger, the more likely to catch race conditions
            var job = new TestParallelJob_AddComponents
            {
                CommandBuffer = cmds.AsParallelWriter(),
            }.Schedule(kCreateCount, 64);
            job.Complete();
            cmds.Playback(m_Manager);

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2));
            Assert.AreEqual(kCreateCount,  query.CalculateEntityCount());
            query.Dispose();
        }

        [Test]
        public void RemoveComponents()
        {
            var e = m_Manager.CreateEntity();
            m_Manager.AddComponent(e, new ComponentTypeSet(typeof(EcsTestTag), typeof(EcsTestData3)));

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.RemoveComponent(e, new ComponentTypeSet(typeof(EcsTestTag), typeof(EcsTestData3)));
            cmds.Playback(m_Manager);

            Assert.IsTrue(m_Manager.HasComponent<Simulate>(e));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestTag>(e));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(e));
            Assert.AreEqual(1, m_Manager.GetComponentCount(e));

            // same thing again, but reverse order of types in ComponentTypes
            m_Manager.AddComponent(e, new ComponentTypeSet(typeof(EcsTestTag), typeof(EcsTestData3)));

            using var cmds2 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds2.RemoveComponent(e, new ComponentTypeSet(typeof(EcsTestData3), typeof(EcsTestTag)));
            cmds2.Playback(m_Manager);

            Assert.IsTrue(m_Manager.HasComponent<Simulate>(e));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestTag>(e));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(e));
            Assert.AreEqual(1, m_Manager.GetComponentCount(e));
        }

        [Test]
        public void AddSharedComponentWithDefaultValue()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var e = m_Manager.CreateEntity();
            cmds.AddSharedComponent(e, new EcsTestSharedComp(0));

            cmds.Playback(m_Manager);

            var sharedCompList = new List<EcsTestSharedComp>();
            m_Manager.GetAllUniqueSharedComponentsManaged<EcsTestSharedComp>(sharedCompList);

            Assert.AreEqual(1, sharedCompList.Count);
            Assert.AreEqual(0, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(e).value);
        }

        [Test]
        public void SetSharedComponent()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var e = m_Manager.CreateEntity();
            cmds.AddSharedComponent(e, new EcsTestSharedComp(10));
            cmds.SetSharedComponent(e, new EcsTestSharedComp(33));
            var e2 = cmds.CreateEntity();
            cmds.AddComponent<EcsTestTag>(e2);
            cmds.AddSharedComponent(e, default(EcsTestSharedCompEntity));
            cmds.SetSharedComponent(e, new EcsTestSharedCompEntity(e2));

            cmds.Playback(m_Manager);

            m_Manager.GetAllUniqueSharedComponents<EcsTestSharedComp>(out var sharedCompList, World.UpdateAllocator.ToAllocator);

            Assert.AreEqual(2, sharedCompList.Length);
            Assert.AreEqual(33, sharedCompList[1].value);

            var actualValue = m_Manager.GetSharedComponent<EcsTestSharedCompEntity>(e);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(actualValue.value));
        }

        [Test]
        public void SetSharedComponentDefault()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);

            var e = cmds.CreateEntity();
            cmds.AddSharedComponent(e, new EcsTestSharedComp(10));
            cmds.SetSharedComponent(e, new EcsTestSharedComp());

            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager2);

            {
                var sharedCompList = new List<EcsTestSharedComp>();
                m_Manager.GetAllUniqueSharedComponentsManaged<EcsTestSharedComp>(sharedCompList);

                Assert.AreEqual(1, sharedCompList.Count);
                Assert.AreEqual(0, sharedCompList[0].value);
            }
            {
                var sharedCompList = new List<EcsTestSharedComp>();
                m_Manager2.GetAllUniqueSharedComponentsManaged<EcsTestSharedComp>(sharedCompList);

                Assert.AreEqual(1, sharedCompList.Count);
                Assert.AreEqual(0, sharedCompList[0].value);
            }
        }


        [Test]
        public void SetSharedComponentNonDefault()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            var index = TypeManager.GetTypeIndex<EcsTestSharedComp>();

            var e = cmds.CreateEntity();
            cmds.AddSharedComponent(e, new EcsTestSharedComp());
            cmds.UnsafeSetSharedComponentManagedNonDefault(e, new EcsTestSharedComp(10), index);

            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager2);

            {
                var sharedCompList = new List<EcsTestSharedComp>();
                m_Manager.GetAllUniqueSharedComponentsManaged<EcsTestSharedComp>(sharedCompList);

                Assert.AreEqual(2, sharedCompList.Count);
                Assert.AreEqual(10, sharedCompList[1].value);
            }
            {
                var sharedCompList = new List<EcsTestSharedComp>();
                m_Manager2.GetAllUniqueSharedComponentsManaged<EcsTestSharedComp>(sharedCompList);

                Assert.AreEqual(2, sharedCompList.Count);
                Assert.AreEqual(10, sharedCompList[1].value);
            }
        }

        [Test]
        public void RemoveSharedComponent()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var entity = m_Manager.CreateEntity();
            var sharedComponent = new EcsTestSharedComp(10);
            m_Manager.AddSharedComponentManaged(entity, sharedComponent);

            cmds.RemoveComponent<EcsTestSharedComp>(entity);

            cmds.Playback(m_Manager);

            Assert.IsFalse(m_Manager.HasComponent<EcsTestSharedComp>(entity), "The shared component was not removed.");
        }

        [Test]
        public void SetEntityEnabled_ECB()
        {
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                Entity e0 = cmds.CreateEntity();
                cmds.SetEnabled(e0, false);

                cmds.Playback(m_Manager);
            }

            var entities = m_Manager.GetAllEntities();
            Assert.AreEqual(1, entities.Length);
            Assert.IsTrue(m_Manager.HasComponent<Disabled>(entities[0]));

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetEnabled(entities[0], true);

                cmds.Playback(m_Manager);
            }

            Assert.IsFalse(m_Manager.HasComponent<Disabled>(entities[0]));
        }

        [Test]
        public unsafe void PrePlaybackValidation_NormalPlaybackOccurs()
        {
            using var cmds = new EntityCommandBuffer(Allocator.TempJob);
            Entity e0 = cmds.CreateEntity();
            cmds.AddComponent(e0, new EcsTestData {value = 1});

            try
            {
                EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = true;
                Assert.DoesNotThrow(() => cmds.Playback(m_Manager));
                EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = false;
                Assert.AreEqual(1, cmds.PassedPrePlaybackValidation);
                // command playback should still be visible, assuming validation didn't fail
                using(var entities = m_Manager.CreateEntityQuery(typeof(EcsTestData)).ToEntityArray(Allocator.Temp))
                {
                    Assert.AreEqual(1, entities.Length);
                    Assert.AreEqual(1, m_Manager.GetComponentData<EcsTestData>(entities[0]).value);
                }
            }
            finally
            {
                EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = false;
            }
        }

        [Test]
        public void SetComponentEnabled_ECB()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2));
            Entity e0 = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponentEnabled<EcsTestDataEnableable2>(e0, false);
            Entity e1 = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponentEnabled(e1, typeof(EcsTestDataEnableable), false);

            // toggle "enabled" state on both components of both entities
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetComponentEnabled<EcsTestDataEnableable>(e0, false);
                cmds.SetComponentEnabled<EcsTestDataEnableable2>(e0, true);
                cmds.SetComponentEnabled(e1, typeof(EcsTestDataEnableable), true);
                cmds.SetComponentEnabled(e1, typeof(EcsTestDataEnableable2), false);
                cmds.Playback(m_Manager);
            }

            Assert.IsFalse(m_Manager.IsComponentEnabled<EcsTestDataEnableable>(e0));
            Assert.IsTrue(m_Manager.IsComponentEnabled<EcsTestDataEnableable2>(e0));
            Assert.IsTrue(m_Manager.IsComponentEnabled(e1, typeof(EcsTestDataEnableable)));
            Assert.IsFalse(m_Manager.IsComponentEnabled(e1, typeof(EcsTestDataEnableable2)));
        }

#if !DOTS_DISABLE_DEBUG_NAMES
        [Test]
        public void EntityName_SetName_ECB()
        {
            var name = new FixedString64Bytes("Test");

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            Entity e0 = cmds.CreateEntity();
            cmds.AddComponent(e0,typeof(EcsTestData));
            cmds.SetName(e0,name);

            cmds.Playback(m_Manager);

            using(var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var e = query.GetSingletonEntity();
                m_Manager.GetName(e, out var actualName);
                Assert.AreEqual(name,actualName);
            }

        }
#endif //!DOTS_DISABLE_DEBUG_NAMES

        [Test]
        public void AddAndSetComponent_ComponentDoesNotExist_Succeeds()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var entity = m_Manager.CreateEntity();
            cmds.AddComponent(entity, ComponentType.ReadWrite<EcsTestData>());
            cmds.SetComponent(entity, new EcsTestData(42));

            Assert.DoesNotThrow(() => { cmds.Playback(m_Manager); });

            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(entity).value);
        }

        [Test]
        public void AddAndSetComponent_ComponentExists_NewValueWins()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var entity = m_Manager.CreateEntity();
            cmds.AddComponent(entity, new EcsTestData(17));

            cmds.AddComponent(entity, ComponentType.ReadWrite<EcsTestData>());
            cmds.SetComponent(entity, new EcsTestData(42));

            Assert.DoesNotThrow(() => { cmds.Playback(m_Manager); });

            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(entity).value);
        }

        [Test]
        public void AddComponentData_ComponentExists_NewValueWins()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var entity = m_Manager.CreateEntity();
            cmds.AddComponent(entity, new EcsTestData(17));
            cmds.AddComponent(entity, new EcsTestData(42));

            Assert.DoesNotThrow(() => { cmds.Playback(m_Manager); });

            Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(entity).value);
        }

        [Test]
        public void AddSharedComponent_ComponentDoesNotExist_Succeeds()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var entity = m_Manager.CreateEntity();
            cmds.AddSharedComponent(entity, new EcsTestSharedComp(42));

            Assert.DoesNotThrow(() => { cmds.Playback(m_Manager); });

            Assert.AreEqual(42, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity).value);
        }

        [Test]
        public void AddSharedComponent_ComponentExists_NewValueWins()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var entity = m_Manager.CreateEntity();
            cmds.AddSharedComponent(entity, new EcsTestSharedComp(17));
            cmds.AddSharedComponent(entity, new EcsTestSharedComp(42));

            Assert.DoesNotThrow(() => { cmds.Playback(m_Manager); });

            Assert.AreEqual(42, m_Manager.GetSharedComponent<EcsTestSharedComp>(entity).value);
        }

        [Test]
        public void AddSharedComponent_WithEntityFixup_Works()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var entity = m_Manager.CreateEntity();
            var e2 = cmds.CreateEntity();
            cmds.AddComponent<EcsTestTag>(e2);
            cmds.AddSharedComponent(entity, new EcsTestSharedCompEntity(e2));

            Assert.DoesNotThrow(() => { cmds.Playback(m_Manager); });

            var actualValue = m_Manager.GetSharedComponent<EcsTestSharedCompEntity>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(actualValue.value));
        }

        [Test]
        public void AddComponentForEntityQuery([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var entity = m_Manager.CreateEntity();
            var entity2 = m_Manager.CreateEntity();
            var data1 = new EcsTestData();
            m_Manager.AddComponentData(entity, data1);
            m_Manager.AddComponentData(entity2, data1);

            // these entities don't match the query and so should remain unaffected
            m_Manager.CreateEntity();
            m_Manager.CreateEntity(typeof(EcsTestData3));
            m_Manager.CreateEntity(typeof(EcsTestData4));

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                cmds.AddComponent(entityQuery, typeof(EcsTestData2), queryCaptureMode);
                cmds.Playback(m_Manager);
            }

            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData2)))
            using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(2, entities.Length, "Wrong number of entities with added component.");

                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                }
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void AddManagedComponentForEntityQuery_WithValue_CaptureAtRecord()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));

            using (var originalEntities = m_Manager.CreateEntity(archetype, 2, World.UpdateAllocator.ToAllocator))
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var originalVal = new EcsTestManagedComponent();
                cmds.AddComponentObject(entityQuery, originalVal);

                // modifying entities between record and playback should be OK
                m_Manager.AddComponent<EcsTestData5>(originalEntities[0]);

                cmds.Playback(m_Manager);

                using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, entities.Length, "Wrong number of entities with added component.");
                    CollectionAssert.AreEquivalent(originalEntities, entities);

                    for (int i = 0; i < entities.Length; i++)
                    {
                        var e = entities[i];
                        var val = m_Manager.GetComponentObject<EcsTestManagedComponent>(e);
                        Assert.AreSame(originalVal, val);
                    }
                }
            }
        }

        [Test]
        public void AddManagedComponentForEntityQuery_WithValue_HasComponentAlready_CaptureAtRecord()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestManagedComponent), typeof(EcsTestData));

            using (var originalEntities = m_Manager.CreateEntity(archetype, 2, World.UpdateAllocator.ToAllocator))
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var originalVal = new EcsTestManagedComponent();
                cmds.AddComponentObject(entityQuery, originalVal);

                // modifying entities between record and playback should be OK
                m_Manager.AddComponent<EcsTestData5>(originalEntities[0]);

                cmds.Playback(m_Manager);

                using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, entities.Length, "Wrong number of entities with added component.");
                    CollectionAssert.AreEquivalent(originalEntities, entities);

                    for (int i = 0; i < entities.Length; i++)
                    {
                        var e = entities[i];
                        var val = m_Manager.GetComponentObject<EcsTestManagedComponent>(e);
                        Assert.AreSame(originalVal, val);
                    }
                }
            }
        }

        [Test]
        public void SetManagedComponentForEntityQuery_CaptureAtRecord()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestManagedComponent), typeof(EcsTestData));

            using (var originalEntities = m_Manager.CreateEntity(archetype, 2, World.UpdateAllocator.ToAllocator))
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var originalVal = new EcsTestManagedComponent();
                cmds.SetComponentObject(entityQuery, originalVal);

                // modifying entities between record and playback should be OK
                m_Manager.AddComponent<EcsTestData5>(originalEntities[0]);

                cmds.Playback(m_Manager);

                using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, entities.Length, "Wrong number of entities with added component.");
                    CollectionAssert.AreEquivalent(originalEntities, entities);

                    for (int i = 0; i < entities.Length; i++)
                    {
                        var e = entities[i];
                        var val = m_Manager.GetComponentObject<EcsTestManagedComponent>(e);
                        Assert.AreSame(originalVal, val);
                    }
                }
            }
        }
#endif

        [Test]
        public void AddComponentForEntityQuery_CaptureAtRecord_SetValue()
        {
            var entity = m_Manager.CreateEntity();
            var entity2 = m_Manager.CreateEntity();
            var data1 = new EcsTestData();
            m_Manager.AddComponentData(entity, data1);
            m_Manager.AddComponentData(entity2, data1);
            m_Manager.AddComponentData(entity2, new EcsTestData2(8));  // entity that already has the component should have it set

            // these entities don't match the query and so should remain unaffected
            m_Manager.CreateEntity();
            m_Manager.CreateEntity(typeof(EcsTestData3));
            m_Manager.CreateEntity(typeof(EcsTestData4));

            var testVal = new EcsTestData2(5);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                cmds.AddComponent(entityQuery, testVal);
                cmds.Playback(m_Manager);
            }

            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData2)))
            using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(2, entities.Length, "Wrong number of entities with added component.");

                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    Assert.AreEqual(testVal, m_Manager.GetComponentData<EcsTestData2>(e),
                        "A component did not have the correct value.");

                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                }
            }
        }


        [Test]
        public void AddSharedComponentForEntityQuery_WithValue([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var entity = m_Manager.CreateEntity();
            var entity2 = m_Manager.CreateEntity();
            var entity3 = m_Manager.CreateEntity();
            var data1 = new EcsTestData();
            m_Manager.AddComponentData(entity, data1);
            m_Manager.AddComponentData(entity2, data1);
            m_Manager.AddComponentData(entity3, data1);
            m_Manager.AddSharedComponentManaged(entity2, new EcsTestSharedComp(8));  // entity that already has the component should have it set
            m_Manager.AddSharedComponentManaged(entity3, new EcsTestSharedComp(9));  // entity that already has the component should have it set

            // these entities don't match the query and so should remain unaffected
            m_Manager.CreateEntity();
            m_Manager.CreateEntity(typeof(EcsTestData3));
            m_Manager.CreateEntity(typeof(EcsTestData4));

            var testVal = new EcsTestSharedComp(5);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                cmds.AddSharedComponent(entityQuery, testVal, queryCaptureMode);
                cmds.Playback(m_Manager);
            }

            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp)))
            using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(3, entities.Length, "Wrong number of entities with added component.");

                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    Assert.AreEqual(5, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(e).value,
                        "A component did not have the correct value.");

                    Assert.AreEqual(3, m_Manager.GetComponentCount(e)); // +1 for Simulate tag
                    Assert.IsTrue(e == entity || e == entity2 || e == entity3, "Wrong entity.");
                }
            }
        }

        [Test]
        public void SetSharedComponentForEntityQuery_AllEntitiesHaveTheSharedComponent([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var entity = m_Manager.CreateEntity();
            var entity2 = m_Manager.CreateEntity();
            var entity3 = m_Manager.CreateEntity();
            var data1 = new EcsTestData();
            m_Manager.AddComponentData(entity, data1);
            m_Manager.AddComponentData(entity2, data1);
            m_Manager.AddComponentData(entity3, data1);
            m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp(10));
            m_Manager.AddSharedComponentManaged(entity2, new EcsTestSharedComp(8));
            m_Manager.AddSharedComponentManaged(entity3, new EcsTestSharedComp(9));  // entity that already has the component should have it set

            // these entities don't match the query and so should remain unaffected
            m_Manager.CreateEntity();
            m_Manager.CreateEntity(typeof(EcsTestData3));
            m_Manager.CreateEntity(typeof(EcsTestData4));

            var testVal = new EcsTestSharedComp(5);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                cmds.SetSharedComponent(entityQuery, testVal, queryCaptureMode);
                cmds.Playback(m_Manager);
            }

            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp)))
            using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(3, entities.Length, "Wrong number of entities with added component.");

                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    Assert.AreEqual(5, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(e).value,
                         "A component did not have the correct value.");

                    Assert.AreEqual(3, m_Manager.GetComponentCount(e)); // +1 for Simulate tag
                    Assert.IsTrue(e == entity || e == entity2 || e == entity3, "Wrong entity.");
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        static void setIsBurstDisabled(ref bool arg)
        {
            reallySetIsBurstDisabled(ref arg);
        }

        [BurstDiscard]
        static void reallySetIsBurstDisabled(ref bool arg)
        {
            arg = true;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void SetSharedComponentForEntityQuery_Playback_AllEntitiesMustExistAndHaveTheSharedComponent([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var entity = m_Manager.CreateEntity();
            var entity2 = m_Manager.CreateEntity();
            var data1 = new EcsTestData();
            m_Manager.AddComponentData(entity, data1);
            m_Manager.AddComponentData(entity2, data1);
            m_Manager.AddSharedComponentManaged(entity2, new EcsTestSharedComp(8));

            // these entities don't match the query and so should remain unaffected
            m_Manager.CreateEntity();
            m_Manager.CreateEntity(typeof(EcsTestData3));
            m_Manager.CreateEntity(typeof(EcsTestData4));

            var testVal = new EcsTestSharedComp(5);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                cmds.SetSharedComponent(entityQuery, testVal, queryCaptureMode);
                TestDelegate testDelegate = () => cmds.Playback(m_Manager);
                Assert.Throws<ArgumentException>(testDelegate);
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void AddComponentForEntityQuery_ManagedComponent([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var entity = m_Manager.CreateEntity();
            var entity2 = m_Manager.CreateEntity();
            var data1 = new EcsTestData();
            m_Manager.AddComponentData(entity, data1);
            m_Manager.AddComponentData(entity2, data1);

            // these entities don't match the query and so should remain unaffected
            m_Manager.CreateEntity();
            m_Manager.CreateEntity(typeof(EcsTestData3));
            m_Manager.CreateEntity(typeof(EcsTestData4));

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                cmds.AddComponent(entityQuery, typeof(EcsTestManagedComponent), queryCaptureMode);
                cmds.Playback(m_Manager);
            }

            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestManagedComponent)))
            using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(2, entities.Length, "Wrong number of entities with added component.");

                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                }
            }
        }
#endif

        [Test]
        public void AddComponentForEntityQuery_SharedComponent([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var entity = m_Manager.CreateEntity();
            var entity2 = m_Manager.CreateEntity();
            var data1 = new EcsTestData();
            m_Manager.AddComponentData(entity, data1);
            m_Manager.AddComponentData(entity2, data1);

            // these entities don't match the query and so should remain unaffected
            var entity3 = m_Manager.CreateEntity();
            var entity4 = m_Manager.CreateEntity(typeof(EcsTestData3));
            var entity5 = m_Manager.CreateEntity(typeof(EcsTestData4));

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                cmds.AddComponent(entityQuery, typeof(EcsTestSharedComp), queryCaptureMode);
                cmds.Playback(m_Manager);
            }

            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp)))
            using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(2, entities.Length, "Wrong number of entities with added component.");

                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                }
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires move entities safety checks")]
        public void AddComponentForEntityQuery_SharedComponent_TooMany([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            // test must be updated when kMaxSharedComponentCount is changed
            Assert.AreEqual(16, EntityComponentStore.kMaxSharedComponentCount);

            var entity = m_Manager.CreateEntity(
                typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3), typeof(EcsTestSharedComp4),
                typeof(EcsTestSharedComp5), typeof(EcsTestSharedComp6), typeof(EcsTestSharedComp7), typeof(EcsTestSharedComp8),
                typeof(EcsTestSharedComp9), typeof(EcsTestSharedComp10), typeof(EcsTestSharedComp11), typeof(EcsTestSharedComp12),
                typeof(EcsTestSharedComp13), typeof(EcsTestSharedComp14));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2),
                typeof(EcsTestSharedComp3), typeof(EcsTestSharedComp4));

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp)))
            {
                var types = new ComponentTypeSet(typeof(EcsTestSharedComp15), typeof(EcsTestSharedComp16), typeof(EcsTestSharedComp17));
                cmds.AddComponent(entityQuery, types, queryCaptureMode);

                // Assert.Throws "Cannot add more than {kMaxSharedComponentCount} SharedComponent to a single Archetype"
                Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            }
        }

        [Test]
        public void AddComponentForEntityQuery_MultipleComponents([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var entity = m_Manager.CreateEntity();
            var entity2 = m_Manager.CreateEntity();
            var data1 = new EcsTestData();
            m_Manager.AddComponentData(entity, data1);
            m_Manager.AddComponentData(entity2, data1);

            // these entities don't match the query and so should remain unaffected
            m_Manager.CreateEntity();
            m_Manager.CreateEntity(typeof(EcsTestData3));
            m_Manager.CreateEntity(typeof(EcsTestData4));

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var types = new ComponentTypeSet(typeof(EcsTestData2), typeof(EcsTestData3));
                cmds.AddComponent(entityQuery, types, queryCaptureMode);
                cmds.Playback(m_Manager);
            }

            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData2), typeof(EcsTestData3)))
            using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(2, entities.Length, "Wrong number of entities with added components.");
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                }
            }
        }

        [Test]
        public void RemoveComponentForEntityQuery([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            // these entities don't match the query and so should remain unaffected
            m_Manager.CreateEntity();
            m_Manager.CreateEntity(typeof(EcsTestData3));
            m_Manager.CreateEntity(typeof(EcsTestData4));

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                cmds.RemoveComponent(entityQuery, typeof(EcsTestData2), queryCaptureMode);
                cmds.Playback(m_Manager);
            }

            using (var entityQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestData>()
                .WithNone<EcsTestData2>()
                .Build(m_Manager))
            using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(2, entities.Length, "Wrong number of entities with removed component.");

                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                }
            }
        }

        [Test]
        public void RemoveComponentForEntityQuery_SharedComponent([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var entity = m_Manager.CreateEntity(archetype);
            var entity2 = m_Manager.CreateEntity(archetype);

            // these entities don't match the query and so should remain unaffected
            m_Manager.CreateEntity();
            m_Manager.CreateEntity(typeof(EcsTestData3));
            m_Manager.CreateEntity(typeof(EcsTestData4));

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                cmds.RemoveComponent(entityQuery, typeof(EcsTestSharedComp), queryCaptureMode);
                cmds.Playback(m_Manager);
            }

            using (var entityQuery = new EntityQueryBuilder(Allocator.Temp)
                       .WithAllRW<EcsTestData>()
                       .WithNone<EcsTestSharedComp>()
                       .Build(m_Manager))
            using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(2, entities.Length, "Wrong number of entities with removed component.");

                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                }
            }
        }

        [Test]
        public void RemoveComponentForEntityQuery_MultipleComponents([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));
            var entity = m_Manager.CreateEntity(archetype);
            var entity2 = m_Manager.CreateEntity(archetype);

            // these entities don't match the query and so should remain unaffected
            m_Manager.CreateEntity();
            m_Manager.CreateEntity(typeof(EcsTestData3));
            m_Manager.CreateEntity(typeof(EcsTestData4));

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var types = new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData3));
                cmds.RemoveComponent(entityQuery, types, queryCaptureMode);
                cmds.Playback(m_Manager);
            }

            using (var entityQuery = new EntityQueryBuilder(Allocator.Temp)
                       .WithAllRW<EcsTestData2>()
                       .WithNone<EcsTestData,EcsTestData3>()
                       .Build(m_Manager))
            using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(2, entities.Length, "Wrong number of entities with removed component.");
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                    Assert.IsTrue(e == entity || e == entity2, "Wrong entity.");
                }
            }
        }

        [Test]
        public void DestroyEntitiesForEntityQuery([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            m_Manager.CreateEntity(archetype, 5);

            // these entities don't match the query and so should remain unaffected
            m_Manager.CreateEntity();
            m_Manager.CreateEntity(typeof(EcsTestData3));
            m_Manager.CreateEntity(typeof(EcsTestData4));

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                cmds.DestroyEntity(entityQuery, queryCaptureMode);
                cmds.Playback(m_Manager);
            }

            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(0, entities.Length, "Wrong number of entities destroyed.");
            }

            using (var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(3, entities.Length, "Wrong number of entities remaining.");
            }
        }

        [Test]
        public void AddComponentToEntityQueryWithFilter([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp));

            var entity1 = m_Manager.CreateEntity(archetype);
            var sharedComponent1 = new EcsTestSharedComp {value = 10};
            m_Manager.SetSharedComponentManaged(entity1, sharedComponent1);

            var entity2 = m_Manager.CreateEntity(archetype);
            var sharedComponent2 = new EcsTestSharedComp {value = 130};
            m_Manager.SetSharedComponentManaged(entity2, sharedComponent2);

            var entity3 = m_Manager.CreateEntity(archetype);
            m_Manager.SetSharedComponentManaged(entity3, sharedComponent2);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp)))
            {
                entityQuery.SetSharedComponentFilterManaged(sharedComponent2);
                cmds.AddComponent(entityQuery, typeof(EcsTestData2), queryCaptureMode);

                // modifying an entity in between recording and playback means it won't be processed by AtPlayback,
                // but will still be processed by AtRecord
                m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp { value = 200 });
                m_Manager.AddComponent<EcsTestData3>(entity2);

                cmds.Playback(m_Manager);

                using var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator);
                CollectionAssert.AreEquivalent(new[] { entity1, entity2, entity3 }, entities.ToArray());
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity1), "EcsTestData2 should not have been added");
                Assert.AreEqual(queryCaptureMode == EntityQueryCaptureMode.AtRecord,
                    m_Manager.HasComponent<EcsTestData2>(entity2)); // this entity was modified between recording and playback
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity3), "EcsTestData2 should have been added");
            }
        }

        [Test]
        public void AddComponentsToEntityQueryWithFilter([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp));

            var entity1 = m_Manager.CreateEntity(archetype);
            var sharedComponent1 = new EcsTestSharedComp {value = 10};
            m_Manager.SetSharedComponentManaged(entity1, sharedComponent1);

            var entity2 = m_Manager.CreateEntity(archetype);
            var sharedComponent2 = new EcsTestSharedComp {value = 130};
            m_Manager.SetSharedComponentManaged(entity2, sharedComponent2);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp)))
            {
                entityQuery.SetSharedComponentFilterManaged(sharedComponent2);
                cmds.AddComponent(entityQuery, new ComponentTypeSet(typeof(EcsTestData2), typeof(EcsTestData3)),
                    queryCaptureMode);

                if (queryCaptureMode == EntityQueryCaptureMode.AtRecord)
                {
                    // modifying the entity in between recording and playback should be OK
                    m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp { value = 200 });
                    m_Manager.AddComponent<EcsTestData3>(entity2);
                }

                cmds.Playback(m_Manager);

                using (var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, entities.Length);

                    for (var i = 0; i < entities.Length; i++)
                    {
                        var e = entities[i];
                        var shared = m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(e);
                        if (shared.value == 10)
                        {
                            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(e));
                            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(e));
                        } else
                        {
                            Assert.AreEqual(queryCaptureMode == EntityQueryCaptureMode.AtRecord ? 200 : 130, shared.value);
                            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(e));
                            Assert.IsTrue(m_Manager.HasComponent<EcsTestData3>(e));
                        }
                    }
                }
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires move entities safety checks")]
        public void AddComponentToEntityQuery_OnDifferentEntityManager_Throws([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var entity = m_Manager.CreateEntity();
            var data1 = new EcsTestData();
            m_Manager.AddComponentData(entity, data1);

            // create a query in one manager ...
            // ... but playback on a different manager
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                cmds.AddComponent(entityQuery, typeof(EcsTestData2), queryCaptureMode);
                Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager2));
            }
        }

        [Test]
        public void RemoveComponentFromEntityQueryWithFilter([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestData));

            var entity1 = m_Manager.CreateEntity(archetype);
            var sharedComponent1 = new EcsTestSharedComp {value = 10};
            m_Manager.SetSharedComponentManaged(entity1, sharedComponent1);

            var entity2 = m_Manager.CreateEntity(archetype);
            var sharedComponent2 = new EcsTestSharedComp {value = 130};
            m_Manager.SetSharedComponentManaged(entity2, sharedComponent2);

            var entity3 = m_Manager.CreateEntity(archetype);
            m_Manager.SetSharedComponentManaged(entity3, sharedComponent2);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp), typeof(EcsTestData)))
            {
                entityQuery.SetSharedComponentFilterManaged(sharedComponent2);
                cmds.RemoveComponent(entityQuery, typeof(EcsTestData), queryCaptureMode);

                // modifying an entity in between recording and playback means it won't be processed by AtPlayback,
                // but will still be processed by AtRecord
                m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp { value = 200 });
                m_Manager.AddComponent<EcsTestData3>(entity2);

                cmds.Playback(m_Manager);

                using var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator);
                CollectionAssert.AreEquivalent(new[] { entity1, entity2, entity3 }, entities.ToArray());
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity1), "EcsTestData should not have been remove");
                Assert.AreEqual(queryCaptureMode == EntityQueryCaptureMode.AtRecord,
                    !m_Manager.HasComponent<EcsTestData>(entity2)); // this entity was modified between recording and playback
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData>(entity3), "EcsTestData2 should have been removed");
            }
        }

        [Test]
        public void RemoveComponentsFromEntityQueryWithFilter([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestData), typeof(EcsTestData2));

            var entity1 = m_Manager.CreateEntity(archetype);
            var sharedComponent1 = new EcsTestSharedComp {value = 10};
            m_Manager.SetSharedComponentManaged(entity1, sharedComponent1);

            var entity2 = m_Manager.CreateEntity(archetype);
            var sharedComponent2 = new EcsTestSharedComp {value = 130};
            m_Manager.SetSharedComponentManaged(entity2, sharedComponent2);

            var entity3 = m_Manager.CreateEntity(archetype);
            m_Manager.SetSharedComponentManaged(entity3, sharedComponent2);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp), typeof(EcsTestData)))
            {
                entityQuery.SetSharedComponentFilterManaged(sharedComponent2);
                cmds.RemoveComponent(entityQuery, new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2)),
                    queryCaptureMode);

                // modifying an entity in between recording and playback means it won't be processed by AtPlayback,
                // but will still be processed by AtRecord
                m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp { value = 200 });
                m_Manager.AddComponent<EcsTestData3>(entity2);

                cmds.Playback(m_Manager);

                using var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator);
                CollectionAssert.AreEquivalent(new[] { entity1, entity2, entity3 }, entities.ToArray());
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity1), "EcsTestData should not have been removed");
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity1), "EcsTestData2 should not have been removed");
                Assert.AreEqual(queryCaptureMode == EntityQueryCaptureMode.AtRecord,
                    !m_Manager.HasComponent<EcsTestData>(entity2)); // this entity was modified between recording and playback
                Assert.AreEqual(queryCaptureMode == EntityQueryCaptureMode.AtRecord,
                    !m_Manager.HasComponent<EcsTestData2>(entity2)); // this entity was modified between recording and playback
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData>(entity3), "EcsTestData should have been removed");
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity3), "EcsTestData2 should have been removed");
            }
        }

        [Test]
        public void AddSharedComponentDataToEntityQueryWithFilter([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestData));

            var originalEntities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(25, ref World.UpdateAllocator);
            for (int i = 0; i < 50; i++)
            {
                var entity = m_Manager.CreateEntity(archetype);

                m_Manager.SetComponentData(entity, new EcsTestData());
                if (i % 2 == 0)
                {
                    m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(0));
                    originalEntities[i / 2] = entity;
                }
                else
                {
                    m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(1));
                }
            }

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp), typeof(EcsTestData)))
            {
                entityQuery.SetSharedComponentFilterManaged(new EcsTestSharedComp(0));
                var shared2 = new EcsTestSharedComp2();
                cmds.AddSharedComponent(entityQuery, shared2, queryCaptureMode);

                // modifying the entity in between recording and playback should be OK
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                cmds.Playback(m_Manager);

                using (var entityQuery2 = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestData)))
                using (var entities = entityQuery2.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    CollectionAssert.AreEquivalent(entities, originalEntities);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        var value = m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entities[i]).value;
                        Assert.AreEqual(0, value, "The shared component was not correctly added based on the EntityQueryFilter.");
                    }
                }
            }
        }

        [Test]
        public void DestroyEntitiesFromEntityQuery([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));

            for (int i = 0; i < 50; i++)
            {
                var entity = m_Manager.CreateEntity(archetype);
                m_Manager.SetComponentData(entity, new EcsTestData());
            }

            var entity2 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(entity2, new EcsTestData2());

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                cmds.DestroyEntity(entityQuery, queryCaptureMode);

                // modifying the entity in between recording and playback should be OK
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                cmds.Playback(m_Manager);

                using (var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(1, entities.Length, "Entities were not all deleted based on the EntityQuery.");
                    Assert.IsTrue(m_Manager.HasComponent(entities[0], typeof(EcsTestData2)), "This entity should not have been deleted based on the EntityQuery.");
                }
            }
        }

        [Test]
        public void DestroyEntitiesFromEntityQueryWithFilter([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestData));

            for (int i = 0; i < 50; i++)
            {
                var entity = m_Manager.CreateEntity(archetype);

                m_Manager.SetComponentData(entity, new EcsTestData());
                m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(i % 2));
            }

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp), typeof(EcsTestData)))
            {
                entityQuery.SetSharedComponentFilterManaged(new EcsTestSharedComp(0));
                cmds.DestroyEntity(entityQuery, queryCaptureMode);

                // modifying the entity in between recording and playback should be OK
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                cmds.Playback(m_Manager);

                using (var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(25, entities.Length,
                        "Half of the entities should be deleted based on the filter of the EntityQuery.");
                    for (int i = 0; i < entities.Length; i++)
                    {
                        var value = m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entities[i]).value;
                        Assert.AreEqual(1, value, "Entity should have been deleted based on the EntityQueryFilter.");
                    }
                }
            }
        }

        [Test]
        public void ChangeEntityQueryFilterDoesNotImpactRecordedCommand_CaptureAtRecord()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp));

            var entity1 = m_Manager.CreateEntity(archetype);
            var sharedComponent1 = new EcsTestSharedComp {value = 10};
            m_Manager.SetSharedComponentManaged(entity1, sharedComponent1);

            var entity2 = m_Manager.CreateEntity(archetype);
            var sharedComponent2 = new EcsTestSharedComp {value = 130};
            m_Manager.SetSharedComponentManaged(entity2, sharedComponent2);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp)))
            {
                entityQuery.SetSharedComponentFilterManaged(sharedComponent2);
                cmds.AddComponent(entityQuery, typeof(EcsTestData2), EntityQueryCaptureMode.AtRecord);
                entityQuery.SetSharedComponentFilterManaged(sharedComponent1);

                // modifying the entity in between recording and playback should be OK
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                cmds.Playback(m_Manager);

                using (var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, entities.Length);

                    for (var i = 0; i < entities.Length; i++)
                    {
                        var e = entities[i];
                        var shared = m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(e);
                        if (shared.value == 10)
                        {
                            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(e));
                        } else
                        {
                            Assert.AreEqual(130, shared.value);
                            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(e));
                        }
                    }
                }
            }
        }

        [Test]
        public void DeleteEntityQueryDoesNotImpactRecordedCommand_CaptureAtRecord()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp));

            var entity1 = m_Manager.CreateEntity(archetype);
            var sharedComponent1 = new EcsTestSharedComp {value = 10};
            m_Manager.SetSharedComponentManaged(entity1, sharedComponent1);

            var entity2 = m_Manager.CreateEntity(archetype);
            var sharedComponent2 = new EcsTestSharedComp {value = 130};
            m_Manager.SetSharedComponentManaged(entity2, sharedComponent2);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp));
                entityQuery.SetSharedComponentFilterManaged(sharedComponent2);
                cmds.AddComponent(entityQuery, typeof(EcsTestData2), EntityQueryCaptureMode.AtRecord);

                // modifying the entity in between recording and playback should be OK
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                entityQuery.Dispose();

                cmds.Playback(m_Manager);

                using (var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, entities.Length);

                    for (var i = 0; i < entities.Length; i++)
                    {
                        var e = entities[i];
                        var shared = m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(e);
                        if (shared.value == 10)
                        {
                            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(e));
                        } else
                        {
                            Assert.AreEqual(130, shared.value);
                            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(e));
                        }
                    }
                }
            }
        }

        [BurstCompile]
        struct TestJobWithManagedSharedData : IJob
        {
            public EntityCommandBuffer Buffer;
            public EcsTestSharedComp2 Blah;

            public void Execute()
            {
                var e = Buffer.CreateEntity();
                Buffer.AddSharedComponent(e, Blah);
            }
        }

        [Test]
        public void JobWithSharedComponentData()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var job = new TestJobWithManagedSharedData { Buffer = cmds, Blah = new EcsTestSharedComp2(12) };

            job.Schedule().Complete();
            cmds.Playback(m_Manager);

            var list = new List<EcsTestSharedComp2>();
            m_Manager.GetAllUniqueSharedComponentsManaged<EcsTestSharedComp2>(list);

            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(0, list[0].value0);
            Assert.AreEqual(0, list[0].value1);
            Assert.AreEqual(12, list[1].value0);
            Assert.AreEqual(12, list[1].value1);
        }

        [BurstCompile(CompileSynchronously = true)]
        public struct TestBurstCommandBufferJob : IJob
        {
            public Entity e0;
            public Entity e1;
            public EntityCommandBuffer Buffer;

            public void Execute()
            {
                Buffer.DestroyEntity(e0);
                Buffer.DestroyEntity(e1);
            }
        }

        [Test]
        public void TestCommandBufferDelete()
        {
            Entity[] entities = new Entity[2];
            for (int i = 0; i < entities.Length; ++i)
            {
                entities[i] = m_Manager.CreateEntity();
                m_Manager.AddComponentData(entities[i], new EcsTestData { value = i });
            }

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            new TestBurstCommandBufferJob
            {
                e0 = entities[0],
                e1 = entities[1],
                Buffer = cmds,
            }.Schedule().Complete();

            cmds.Playback(m_Manager);

            var allEntities = m_Manager.GetAllEntities();
            int count = allEntities.Length;
            allEntities.Dispose();

            Assert.AreEqual(0, count);
        }

        [Test]
        public void TestCommandBufferDeleteWithCleanup()
        {
            Entity[] entities = new Entity[2];
            for (int i = 0; i < entities.Length; ++i)
            {
                entities[i] = m_Manager.CreateEntity();
                m_Manager.AddComponentData(entities[i], new EcsTestData { value = i });
                m_Manager.AddComponentData(entities[i], new EcsCleanup1 { Value = i });
            }

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            new TestBurstCommandBufferJob
            {
                e0 = entities[0],
                e1 = entities[1],
                Buffer = cmds,
            }.Schedule().Complete();

            cmds.Playback(m_Manager);

            var allEntities = m_Manager.GetAllEntities();
            int count = allEntities.Length;
            allEntities.Dispose();

            Assert.AreEqual(entities.Length, count);
        }

        [Test]
        public void TestCommandBufferDeleteRemoveCleanup()
        {
            Entity[] entities = new Entity[2];
            for (int i = 0; i < entities.Length; ++i)
            {
                entities[i] = m_Manager.CreateEntity();
                m_Manager.AddComponentData(entities[i], new EcsTestData { value = i });
                m_Manager.AddComponentData(entities[i], new EcsCleanup1 { Value = i });
            }

            {
                using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                new TestBurstCommandBufferJob
                {
                    e0 = entities[0],
                    e1 = entities[1],
                    Buffer = cmds,
                }.Schedule().Complete();

                cmds.Playback(m_Manager);
            }

            {
                using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                for (var i = 0; i < entities.Length; i++)
                {
                    cmds.RemoveComponent<EcsCleanup1>(entities[i]);
                }

                cmds.Playback(m_Manager);
            }

            var allEntities = m_Manager.GetAllEntities();
            int count = allEntities.Length;
            allEntities.Dispose();

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Instantiate()
        {
            var e = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e, new EcsTestData(5));

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.Instantiate(e);
            cmds.Instantiate(e);
            cmds.Playback(m_Manager);

            VerifyEcsTestData(3, 5);
        }

        [Test]
        public void InstantiateWithNativeArray()
        {
            var e = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e, new EcsTestData(5));

            using var outEntities = CollectionHelper.CreateNativeArray<Entity>(2, World.UpdateAllocator.ToAllocator);
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.Instantiate(e, outEntities);
            cmds.Playback(m_Manager);

            VerifyEcsTestData(3, 5);
        }

        [Test]
        public void InstantiateWithSetComponentDataWorks()
        {
            var e = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e, new EcsTestData(5));

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var e1 = cmds.Instantiate(e);
            cmds.SetComponent(e1, new EcsTestData(11));

            var e2 = cmds.Instantiate(e);
            cmds.SetComponent(e2, new EcsTestData(11));

            cmds.Playback(m_Manager);

            m_Manager.DestroyEntity(e);

            VerifyEcsTestData(2, 11);
        }

        [Test]
        public void DestroyEntityTwiceWorks()
        {
            var e = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e, new EcsTestData(5));

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            cmds.DestroyEntity(e);
            cmds.DestroyEntity(e);

            cmds.Playback(m_Manager);

            Assert.IsFalse(m_Manager.Exists(e));
        }

        [Test]
        public void AddSharedComponent_WhenComponentHasEntityField_DoesNotRemap()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var es = m_Manager.CreateEntity();

            cmds.AddSharedComponent(es, new EcsTestSharedCompEntity(es));

            cmds.Playback(m_Manager);

            Assert.AreEqual(es, m_Manager.GetSharedComponent<EcsTestSharedCompEntity>(es).value);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void DestroyInvalidEntity()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var entityBuffer = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(1, ref World.UpdateAllocator);
            var e = cmds.CreateEntity();
            cmds.AddComponent(e, new EcsTestData { value = 12 });
            entityBuffer[0] = e;
            cmds.Playback(m_Manager);

            var savedEntity = entityBuffer[0];

            using var cmds2 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds2.DestroyEntity(savedEntity);

            // savedEntity is invalid, so playing back this ECB should throw an exception
            Assert.Throws<InvalidOperationException>(() =>
            {
                cmds2.Playback(m_Manager);
            });
        }

        [Test]
        public void TestShouldPlaybackFalse()
        {
            var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.CreateEntity();
            cmds.ShouldPlayback = false;
            cmds.Playback(m_Manager);
            cmds.Dispose();

            var allEntities = m_Manager.GetAllEntities();
            int count = allEntities.Length;
            allEntities.Dispose();

            Assert.AreEqual(0, count);
        }

        [BurstCompile(CompileSynchronously = true)]
        struct TestConcurrentJob : IJob
        {
            public EntityCommandBuffer.ParallelWriter Buffer;

            public void Execute()
            {
                Entity e = Buffer.CreateEntity(0);
                Buffer.AddComponent(0, e, new EcsTestData { value = 1 });
            }
        }

        [Test]
        public void ConcurrentRecord()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.CreateEntity();
            new TestConcurrentJob { Buffer = cmds.AsParallelWriter() }.Schedule().Complete();
            cmds.Playback(m_Manager);

            var allEntities = m_Manager.GetAllEntities();
            int count = allEntities.Length;
            allEntities.Dispose();

            Assert.AreEqual(2, count);
        }

        [BurstCompile(CompileSynchronously = true)]
        struct TestConcurrentParallelForJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter Buffer;

            public void Execute(int index)
            {
                Entity e = Buffer.CreateEntity(index);
                Buffer.AddComponent(index, e, new EcsTestData { value = index });
            }
        }

        [Test]
        public void ConcurrentRecordParallelFor()
        {
            const int kCreateCount = 10000;
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.CreateEntity();
            new TestConcurrentParallelForJob { Buffer = cmds.AsParallelWriter() }.Schedule(kCreateCount, 64).Complete();
            cmds.Playback(m_Manager);

            var allEntities = m_Manager.GetAllEntities();
            int count = allEntities.Length;
            Assert.AreEqual(kCreateCount + 1, count);
            bool[] foundEntity = new bool[kCreateCount];
            for (int i = 0; i < foundEntity.Length; ++i)
            {
                foundEntity[i] = false;
            }
            for (int i = 0; i < count; ++i)
            {
                if (m_Manager.HasComponent<EcsTestData>(allEntities[i]))
                {
                    var data1 = m_Manager.GetComponentData<EcsTestData>(allEntities[i]);
                    FastAssert.IsFalse(foundEntity[data1.value]);
                    foundEntity[data1.value] = true;
                }
            }
            for (int i = 0; i < foundEntity.Length; ++i)
            {
                FastAssert.IsTrue(foundEntity[i]);
            }
            allEntities.Dispose();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct TestConcurrentInstantiateJob : IJobParallelFor
        {
            public Entity MasterCopy;
            public EntityCommandBuffer.ParallelWriter Buffer;

            public void Execute(int index)
            {
                Entity e = Buffer.Instantiate(index, MasterCopy);
                Buffer.AddComponent(index, e, new EcsTestData { value = index });
            }
        }

        [Test]
        public void ConcurrentRecordInstantiate()
        {
            const int kInstantiateCount = 10000;
            Entity master = m_Manager.CreateEntity();
            m_Manager.AddComponentData(master, new EcsTestData2 {value0 = 42, value1 = 17});

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            new TestConcurrentInstantiateJob { Buffer = cmds.AsParallelWriter(), MasterCopy = master }.Schedule(kInstantiateCount, 64).Complete();
            cmds.Playback(m_Manager);

            var allEntities = m_Manager.GetAllEntities();
            int count = allEntities.Length;
            Assert.AreEqual(kInstantiateCount + 1, count); // +1 for the master entity
            bool[] foundEntity = new bool[kInstantiateCount];
            for (int i = 0; i < foundEntity.Length; ++i)
            {
                foundEntity[i] = false;
            }
            for (int i = 0; i < count; ++i)
            {
                var data2 = m_Manager.GetComponentData<EcsTestData2>(allEntities[i]);
                FastAssert.AreEqual(data2.value0, 42);
                FastAssert.AreEqual(data2.value1, 17);
                if (m_Manager.HasComponent<EcsTestData>(allEntities[i]))
                {
                    var data1 = m_Manager.GetComponentData<EcsTestData>(allEntities[i]);
                    FastAssert.IsFalse(foundEntity[data1.value]);
                    foundEntity[data1.value] = true;
                }
            }
            for (int i = 0; i < foundEntity.Length; ++i)
            {
                FastAssert.IsTrue(foundEntity[i]);
            }
            allEntities.Dispose();
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void PlaybackInvalidatesBuffers()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var e = cmds.CreateEntity();
            DynamicBuffer<EcsIntElement> buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3 });
            cmds.Playback(m_Manager);

            // Should not be possible to access the temporary buffer after playback.
            Assert.Throws<ObjectDisposedException>(() =>
            {
                buffer.Add(1);
            });
        }

        [Test]
        public void DisposeBeforePlayback_DynamicBufferAllocationsDontLeak()
        {
            // Ideally, we'd assert that the following sequence of calls does not leak any memory.

            // Using a rewindable allocator here should normally bypass most of ECB.Dispose(), but the presence of
            // DynamicBuffer commands requires the full dispose code to run anyway.
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var e = ecb.CreateEntity();
            var db = ecb.AddBuffer<EcsIntElement>(e);
            var elements = CollectionHelper.CreateNativeArray<EcsIntElement>(db.Capacity * 2, Allocator.Temp);
            db.AddRange(elements);
        }

        [Test(Description = "Once a buffer command is played back, it has no side effects on the ECB.")]
        public void BufferChanged_BetweenPlaybacks_HasNoEffectOnECB()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            var e = cmds.CreateEntity();
            DynamicBuffer<EcsIntElement> buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            cmds.Playback(m_Manager);

            var entities = m_Manager.GetAllEntities();
            var emBuffer = m_Manager.GetBuffer<EcsIntElement>(entities[0]);
            for (int i = 0; i < emBuffer.Length; ++i)
                emBuffer[i] = 5;
            entities.Dispose();

            cmds.Playback(m_Manager);

            entities = m_Manager.GetAllEntities();
            Assert.AreEqual(2, entities.Length);
            var b = m_Manager.GetBuffer<EcsIntElement>(entities[1]);
            for (int i = 0; i < b.Length; ++i)
                Assert.AreEqual(i + 1, b[i].Value);

            entities.Dispose();
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void ArrayAliasesOfPendingBuffersAreInvalidateOnResize()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var e = cmds.CreateEntity();
            var buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3 });
            var array = buffer.AsNativeArray();
            buffer.Add(12);
            Assert.Throws<ObjectDisposedException>(() =>

            {
                int val = array[0];
            });
            // Refresh array alias
            array = buffer.AsNativeArray();
            cmds.Playback(m_Manager);

            // Should not be possible to access the temporary buffer after playback.
            Assert.Throws<ObjectDisposedException>(() =>
            {
                buffer.Add(1);
            });
            // Array should not be accessible after playback
            Assert.Throws<ObjectDisposedException>(() =>
            {
                int l = array[0];
            });
        }

        [Test]
        public void AddBufferNoOverflow()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var e = cmds.CreateEntity();
            DynamicBuffer<EcsIntElement> buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3 });
            cmds.Playback(m_Manager);
            VerifySingleBuffer(m_Manager, 3);
        }

        [Test]
        public void AddBufferNoOverflow_MultiplePlaybacks()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            var e = cmds.CreateEntity();
            DynamicBuffer<EcsIntElement> buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3 });
            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager2);
            VerifySingleBuffer(m_Manager, 3);
            VerifySingleBuffer(m_Manager2, 3);
        }

        [Test]
        public void AddBufferOverflow()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var e = cmds.CreateEntity();
            DynamicBuffer<EcsIntElement> buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            cmds.Playback(m_Manager);
            VerifySingleBuffer(m_Manager, 10);
        }

        [Test]
        public void AddBufferOverflow_MultiplePlaybacks()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            var e = cmds.CreateEntity();
            DynamicBuffer<EcsIntElement> buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager2);
            VerifySingleBuffer(m_Manager, 10);
            VerifySingleBuffer(m_Manager2, 10);
        }

        [Test]
        public void AddBufferOverflow_MultiplePlaybacks_SingleManager()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            var e = cmds.CreateEntity();
            DynamicBuffer<EcsIntElement> buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager);

            var allEntities = m_Manager.GetAllEntities();
            Assert.AreEqual(2, allEntities.Length);
            for (int j = 0; j < allEntities.Length; ++j)
            {
                var resultBuffer = m_Manager.GetBuffer<EcsIntElement>(allEntities[j]);
                Assert.AreEqual(10, resultBuffer.Length);

                for (int i = 0; i < 10; ++i)
                {
                    Assert.AreEqual(i + 1, resultBuffer[i].Value);
                }
            }

            allEntities.Dispose();
        }

        [Test]
        public void AddBufferExplicit()
        {
            var e = m_Manager.CreateEntity();
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3 });
            cmds.Playback(m_Manager);

            VerifySingleBuffer(3);
        }

        [Test]
        public void SetBufferExplicit()
        {
            var e = m_Manager.CreateEntity(typeof(EcsIntElement));
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            DynamicBuffer<EcsIntElement> buffer = cmds.SetBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3 });
            cmds.Playback(m_Manager);
            VerifySingleBuffer(3);
        }

        [Test]
        public void AppendToBufferExplicit()
        {
            var e = m_Manager.CreateEntity();
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3 });
            cmds.AppendToBuffer(e, new EcsIntElement {Value = 4});
            cmds.Playback(m_Manager);

            VerifySingleBuffer(4);
        }

        [Test]
        public void AppendToBufferExplicit_LargerThanInternalCapacity()
        {
            var e = m_Manager.CreateEntity();
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            cmds.AppendToBuffer(e, new EcsIntElement {Value = 9});
            cmds.Playback(m_Manager);

            VerifySingleBuffer(9);
        }

        [Test]
        public void AppendToBufferExplicit_WithinExternalCapacity()
        {
            var e = m_Manager.CreateEntity();
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            cmds.AppendToBuffer(e, new EcsIntElement {Value = 10});
            cmds.Playback(m_Manager);

            VerifySingleBuffer(10);
        }

        [Test]
        public void AppendToBufferExplicit_LargerThanExternalCapacity()
        {
            var e = m_Manager.CreateEntity();
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
            cmds.AppendToBuffer(e, new EcsIntElement {Value = 17});
            cmds.Playback(m_Manager);

            VerifySingleBuffer(17);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void AppendToBuffer_BufferDoesNotExist_Fails()
        {
            var e = m_Manager.CreateEntity();
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AppendToBuffer(e, new EcsIntElement {Value = 9});
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
        }

        [Test]
        public void AppendToBufferWithEntity_DelayedFixup_ContainsRealizedEntity()
        {
            int kNumOfBuffers = 12; // Must be > 2
            int kNumOfDeferredEntities = 12;

            using EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            Entity[] e = new Entity[kNumOfBuffers];

            for (int n = 0; n < kNumOfBuffers; n++)
            {
                e[n] = m_Manager.CreateEntity();
                m_Manager.AddBuffer<EcsComplexEntityRefElement>(e[n]);
                for (int i = 0; i < kNumOfDeferredEntities; i++)
                    cmds.AppendToBuffer(e[n], new EcsComplexEntityRefElement() {Entity = cmds.CreateEntity()});
            }

            cmds.RemoveComponent<EcsComplexEntityRefElement>(e[0]);
            cmds.DestroyEntity(e[1]);

            cmds.Playback(m_Manager);

            Assert.IsFalse(m_Manager.HasComponent<EcsComplexEntityRefElement>(e[0]));
            Assert.IsFalse(m_Manager.Exists(e[1]));

            for (int n = 2; n < kNumOfBuffers; n++)
            {
                var outbuf = m_Manager.GetBuffer<EcsComplexEntityRefElement>(e[n]);
                Assert.AreEqual(kNumOfDeferredEntities, outbuf.Length);
                for (int i = 0; i < outbuf.Length; i++)
                {
                    Assert.IsTrue(m_Manager.Exists(outbuf[i].Entity));
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct AppendToBufferJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            public NativeArray<Entity> Entities;

            public void Execute(int index)
            {
                var sourceBuffer = new NativeArray<EcsIntElement>(100, Allocator.Temp);

                for (var i = 0; i < sourceBuffer.Length; ++i)
                    CommandBuffer.AppendToBuffer<EcsIntElement>(index, Entities[index], new EcsIntElement {Value = i});

                sourceBuffer.Dispose();
            }
        }

        [Test]
        public void AppendToBuffer_DoesNotThrowInJob()
        {
            var archetype = m_Manager.CreateArchetype(ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<EcsIntElement>());
            var entities = new NativeArray<Entity>(100, Allocator.Persistent);
            m_Manager.CreateEntity(archetype, entities);

            using EntityCommandBuffer cb = new EntityCommandBuffer(Allocator.Persistent);
            var handle = new AppendToBufferJob()
            {
                CommandBuffer = cb.AsParallelWriter(),
                Entities = entities
            }.Schedule(100, 1);
            handle.Complete();
            cb.Playback(m_Manager);

            for (var i = 0; i < 100; ++i)
            {
                var buffer = m_Manager.GetBuffer<EcsIntElement>(entities[i]);
                Assert.AreEqual(100, buffer.Length);
            }

            entities.Dispose();
        }

        [Test]
        public void AddBuffer_BufferDoesNotExist_Succeeds()
        {
            var e = m_Manager.CreateEntity(typeof(EcsIntElement));
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            DynamicBuffer<EcsIntElement> buffer = cmds.AddBuffer<EcsIntElement>(e);
            buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3 });
            cmds.Playback(m_Manager);
            VerifySingleBuffer(3);
        }

        [Test]
        public void AddBuffer_BufferExists_NewValueWins()
        {
            var e = m_Manager.CreateEntity(typeof(EcsIntElement));
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            DynamicBuffer<EcsIntElement> bufferOld = cmds.AddBuffer<EcsIntElement>(e);
            DynamicBuffer<EcsIntElement> bufferNew = cmds.AddBuffer<EcsIntElement>(e);
            bufferNew.CopyFrom(new EcsIntElement[] {1, 2, 3});
            // Writes to old buffer at this point are still valid, but will be ignored.
            Assert.DoesNotThrow(() => { bufferOld.CopyFrom(new EcsIntElement[] {4, 5, 6}); });
            cmds.Playback(m_Manager);
            VerifySingleBuffer(3);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires move entities safety checks")]
        public void AddBuffer_OnEntityFromOtherWorld_Fails()
        {
            var e = m_Manager.CreateEntity();
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                DynamicBuffer<EcsIntElement> buffer = cmds.AddBuffer<EcsIntElement>(e);
                buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3 });
                Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager2));

                using (var allEntities = m_Manager.GetAllEntities())
                {
                    Assert.AreEqual(1, allEntities.Length);
                    Assert.IsFalse(m_Manager.HasComponent<EcsIntElement>(e));
                }
            }
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void AddBuffer_AfterDispose_WithoutPlayback_Throws()
        {
            var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var e = cmds.CreateEntity();
            var buffer = cmds.AddBuffer<EcsIntElement>(e);

            Assert.DoesNotThrow(() => buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3, 4, 5 }));

            cmds.Dispose();

            Assert.Throws<ObjectDisposedException>(
                () => buffer.CopyFrom(new EcsIntElement[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void ModifyingPrefabEntityThrows_AddComponent_AllVariations()
        {
            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = true;

            var e = m_Manager.CreateEntity(typeof(Prefab));

            //Add Component
            var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent<EcsTestData>(e);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Add Component With Fixup
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent<EcsTestDataEntity>(e);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Add Buffer
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddBuffer<EcsIntElement>(e);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Add Buffer With Fixup
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddBuffer<EcsComplexEntityRefElement>(e);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Add Unmanaged Shared Component
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddSharedComponent(e, new EcsTestSharedComp(5));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Add Shared Component
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddSharedComponentManaged(e, new EcsStringSharedComponent {Value = "test"});
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Add Multiple Components
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent(e, new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2)));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            var e2 = m_Manager.CreateEntity();
            var entities = m_Manager.GetAllEntities();

            //Add Component For Multiple Entities
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent<EcsTestData>(entities);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Add Multiple Components For Multiple Entities
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent(entities, new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2)));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Add Shared Component For Multiple Entities
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddSharedComponentManaged(entities, new EcsStringSharedComponent {Value = "test"});
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Add Component in Linked Entity Group
            var mask = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>()).GetEntityQueryMask();
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponentForLinkedEntityGroup(e, mask, new EcsTestData(42));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

#if !UNITY_DISABLE_MANAGED_COMPONENTS
            // Add Managed Component
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent<EcsTestManagedComponent>(e);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Add Managed Component For Multiple Entities
            var query = m_Manager.CreateEntityQuery(typeof(Prefab));
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent(query, new EcsTestManagedComponent());
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();
#endif

            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void ModifyingPrefabEntityThrows_RemoveComponent_AllVariations()
        {
            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = true;

            var e = m_Manager.CreateEntity(typeof(Prefab), typeof(EcsTestData));

            //Remove Component
            var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.RemoveComponent<EcsTestData>(e);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Remove Multiple Components
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.RemoveComponent(e, new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2)));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            var e2 = m_Manager.CreateEntity();
            var entities = m_Manager.GetAllEntities();

            //Remove Component From Multiple Entities
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.RemoveComponent<EcsTestData>(entities);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Remove Multiple Components From Multiple Entities
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.RemoveComponent(entities, new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2)));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void ModifyingPrefabEntityThrows_SetComponent_AllVariations()
        {
            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = true;

            var e = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup), typeof(EcsTestData), typeof(EcsTestDataEntity), typeof(EcsIntElement), typeof(EcsComplexEntityRefElement), typeof(EcsTestSharedComp), typeof(EcsStringSharedComponent), typeof(EcsIntElementEnableable));

            //Set Component
            var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetComponent(e, new EcsTestData(42));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Set Component With Fixup
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetComponent(e, new EcsTestDataEntity());
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Set Buffer
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetBuffer<EcsIntElement>(e);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Set Buffer With Fixup
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetBuffer<EcsComplexEntityRefElement>(e);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Append to Buffer
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AppendToBuffer(e, new EcsIntElement());
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Append to Buffer With Fixup
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AppendToBuffer(e, new EcsComplexEntityRefElement());
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Set Unmanaged Shared Component
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetSharedComponent(e, new EcsTestSharedComp(5));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Set Shared Component
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetSharedComponentManaged(e, new EcsStringSharedComponent {Value = "test"});
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            var e2 = m_Manager.CreateEntity();
            var entities = m_Manager.GetAllEntities();

            // Set Unmanaged Shared Component For Multiple Entities
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetSharedComponent(entities, new EcsTestSharedComp(5));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Set Shared Component For Multiple Entities
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetSharedComponentManaged(entities, new EcsStringSharedComponent {Value = "test"});
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Set Enabled
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetEnabled(e, false);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Set Component Enabled
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetComponentEnabled<EcsIntElementEnableable>(e, false);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Set Component in Linked Entity Group
            var mask = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>()).GetEntityQueryMask();
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetComponentForLinkedEntityGroup(e, mask, new EcsTestData(42));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Replace Component in Linked Entity Group
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.ReplaceComponentForLinkedEntityGroup(e, new EcsTestData(42));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();


#if !DOTS_DISABLE_DEBUG_NAMES
            //Set Name
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetName(e, "Test");
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();
#endif

#if !UNITY_DISABLE_MANAGED_COMPONENTS
            // Set Managed Component
            m_Manager.AddComponent<EcsTestManagedComponent>(e);
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetComponent<EcsTestManagedComponent>(e, new EcsTestManagedComponent());
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Set Managed Component For Multiple Entities
            var query = m_Manager.CreateEntityQuery(typeof(Prefab));
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.SetComponent(query, new EcsTestManagedComponent());
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();
#endif

            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void ModifyingPrefabEntityThrows_DestroyEntity_AllVariations()
        {
            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = true;

            var e = m_Manager.CreateEntity(typeof(Prefab), typeof(EcsTestData));

            //Destroy Entity
            var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.DestroyEntity(e);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            var e2 = m_Manager.CreateEntity();
            var entities = m_Manager.GetAllEntities();

            //Destroy Multiple Entities
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.DestroyEntity(entities);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void CreatingPrefabEntityThrows()
        {
            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = true;

            var archetype = m_Manager.CreateArchetype(typeof(Prefab));

            //Create Entity
            var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.CreateEntity(archetype);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires move entities safety checks")]
        public void AddingPrefabComponentThrows_AllVariations()
        {
            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = true;

            var e = m_Manager.CreateEntity();

            //Add Component
            var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent<Prefab>(e);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Add Multiple Components
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent(e, new ComponentTypeSet(typeof(Prefab), typeof(EcsTestData2)));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Add Component in Linked Entity Group
            var mask = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>()).GetEntityQueryMask();
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponentForLinkedEntityGroup(e, mask, new Prefab());
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            var e2 = m_Manager.CreateEntity();
            var entities = m_Manager.GetAllEntities();

            //Add Component For Multiple Entities
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent<Prefab>(entities);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            // Add Multiple Components For Multiple Entities
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent(entities, new ComponentTypeSet(typeof(Prefab), typeof(EcsTestData2)));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void RemovingPrefabComponentThrows_AllVariations()
        {
            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = true;

            var e = m_Manager.CreateEntity(typeof(Prefab));

            //Remove Component
            var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.RemoveComponent<Prefab>(e);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Remove Multiple Components
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.RemoveComponent(e, new ComponentTypeSet(typeof(Prefab)));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            var e2 = m_Manager.CreateEntity();
            var entities = m_Manager.GetAllEntities();

            //Remove Component From Multiple Entities
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.RemoveComponent<Prefab>(entities);
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            //Remove Multiple Components From Multiple Entities
            cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.RemoveComponent(entities, new ComponentTypeSet(typeof(Prefab)));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            cmds.Dispose();

            EntityCommandBuffer.ENABLE_PRE_PLAYBACK_VALIDATION = false;
        }

        [Test]
        public void ParallelOnMainThread()
        {
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var c = cmds.AsParallelWriter();
                Assert.DoesNotThrow(() => c.CreateEntity(0));
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct DeterminismTestJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter Cmds;

            public void Execute(int index)
            {
                Entity e = Cmds.CreateEntity(index);
                Cmds.AddComponent(index, e, new EcsTestData { value = index });
            }
        }

        [Test]
        public void DeterminismTest()
        {
            const int kRepeat = 10000;
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var e = cmds.CreateEntity(); // implicitly, sortIndex=Int32.MaxValue on the main thread
            cmds.AddComponent(e, new EcsTestData { value = kRepeat });
            new DeterminismTestJob { Cmds = cmds.AsParallelWriter() }.Schedule(kRepeat, 64).Complete();
            cmds.Playback(m_Manager);

            var allEntities = m_Manager.GetAllEntities();
            int count = allEntities.Length;
            Assert.AreEqual(kRepeat + 1, count);
            for (int i = 0; i < count; ++i)
            {
                var data = m_Manager.GetComponentData<EcsTestData>(allEntities[i]);
                FastAssert.AreEqual(i, data.value);
            }
            allEntities.Dispose();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void NoTempAllocatorInConcurrent()
        {
            using var cmds = new EntityCommandBuffer(Allocator.Temp);
#pragma warning disable 0219 // assigned but its value is never used
            Assert.Throws<InvalidOperationException>(() => { EntityCommandBuffer.ParallelWriter c = cmds.AsParallelWriter(); });
#pragma warning restore 0219
        }

        private void VerifySingleBuffer(int length)
        {
            VerifySingleBuffer(m_Manager, length);
        }

        private static void VerifySingleBuffer(EntityManager manager, int length)
        {
            var allEntities = manager.GetAllEntities();
            Assert.AreEqual(1, allEntities.Length);
            var resultBuffer = manager.GetBuffer<EcsIntElement>(allEntities[0]);
            Assert.AreEqual(length, resultBuffer.Length);

            for (int i = 0; i < length; ++i)
            {
                Assert.AreEqual(i + 1, resultBuffer[i].Value);
            }
            allEntities.Dispose();
        }

        private void VerifyEcsTestData(int length, int expectedValue)
        {
            var allEntities = m_Manager.GetAllEntities();
            Assert.AreEqual(length, allEntities.Length);

            for (int i = 0; i < length; ++i)
            {
                Assert.AreEqual(expectedValue, m_Manager.GetComponentData<EcsTestData>(allEntities[i]).value);
            }
            allEntities.Dispose();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct BufferCopyFromNativeArrayJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            public NativeArray<Entity> Entities;
            [ReadOnly]
            public NativeArray<EcsIntElement> sourceArray;
            public void Execute(int index)
            {
                var buffer = CommandBuffer.AddBuffer<EcsIntElement>(index, Entities[index]);

                buffer.CopyFrom(sourceArray);
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct BufferCopyFromNativeSliceJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            public NativeArray<Entity> Entities;
            [ReadOnly]
            public NativeSlice<EcsIntElement> sourceSlice;
            public void Execute(int index)
            {
                var buffer = CommandBuffer.AddBuffer<EcsIntElement>(index, Entities[index]);

                buffer.CopyFrom(sourceSlice);
            }
        }

        // https://unity3d.atlassian.net/browse/DOTSR-1435
        // These tests cause crashes in the IL2CPP runner. Cause not yet debugged.
        // Only fails in Multi-Threaded
        [Test]
        public void BufferCopyFromNativeArrayDoesNotThrowInJob()
        {
            var archetype = m_Manager.CreateArchetype(ComponentType.ReadWrite<EcsTestData>());
            var entities = new NativeArray<Entity>(100, Allocator.Persistent);
            m_Manager.CreateEntity(archetype, entities);

            var testArray = CollectionHelper.CreateNativeArray<EcsIntElement, RewindableAllocator>(100, ref World.UpdateAllocator);

            for (int i = 0; i < testArray.Length; i++)
            {
                testArray[i] = i;
            }

            using EntityCommandBuffer cb = new EntityCommandBuffer(Allocator.Persistent);
            var handle = new BufferCopyFromNativeArrayJob()
            {
                CommandBuffer = cb.AsParallelWriter(),
                Entities = entities,
                sourceArray = testArray
            }.Schedule(100, 1);

            handle.Complete();
            cb.Playback(m_Manager);

            for (var i = 0; i < 100; ++i)
            {
                var buffer = m_Manager.GetBuffer<EcsIntElement>(entities[i]);
                Assert.AreEqual(100, buffer.Length);
                for (int j = 0; j < buffer.Length; j++)
                {
                    Assert.AreEqual(testArray[j].Value, buffer[j].Value);
                }
            }

            entities.Dispose();
        }

        [Test]
        public void BufferCopyFromNativeSliceDoesNotThrowInJob()
        {
            var archetype = m_Manager.CreateArchetype(ComponentType.ReadWrite<EcsTestData>());
            var entities = new NativeArray<Entity>(100, Allocator.Persistent);
            m_Manager.CreateEntity(archetype, entities);
            var testArray = CollectionHelper.CreateNativeArray<EcsIntElement, RewindableAllocator>(100, ref World.UpdateAllocator);

            for (int i = 0; i < testArray.Length; i++)
            {
                testArray[i] = i;
            }

            var testSlice = new NativeSlice<EcsIntElement>(testArray);

            using EntityCommandBuffer cb = new EntityCommandBuffer(Allocator.Persistent);
            var handle = new BufferCopyFromNativeSliceJob()
            {
                CommandBuffer = cb.AsParallelWriter(),
                Entities = entities,
                sourceSlice = testSlice
            }.Schedule(100, 1);
            handle.Complete();
            cb.Playback(m_Manager);

            for (var i = 0; i < 100; ++i)
            {
                var buffer = m_Manager.GetBuffer<EcsIntElement>(entities[i]);
                Assert.AreEqual(100, buffer.Length);
                for (int j = 0; j < buffer.Length; j++)
                {
                    Assert.AreEqual(testSlice[j].Value, buffer[j].Value);
                }
            }

            entities.Dispose();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void EntityCommandBufferSystemPlaybackExceptionIsolation()
        {
            var entityCommandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            var buf1 = entityCommandBufferSystem.CreateCommandBuffer();
            var buf2 = entityCommandBufferSystem.CreateCommandBuffer();

            var e1 = buf1.CreateEntity();
            buf1.AddComponent(e1, new EcsTestData());
            buf1.AddComponent(e1, new EcsTestData());

            var e2 = buf2.CreateEntity();
            buf2.AddComponent(e2, new EcsTestData());
            buf2.DestroyEntity(e2);
            buf2.AddComponent(e2, new EcsTestData());

            // We exp both command buffers to execute, and an exception thrown afterwards
            // Essentially we want isolation of two systems that might fail independently.
            Assert.Throws<ArgumentException>(() => { entityCommandBufferSystem.Update(); });
            Assert.AreEqual(1, EmptySystem.GetEntityQuery(typeof(EcsTestData)).CalculateEntityCount());

            // On second run, we expect all buffers to be removed...
            // So no more exceptions thrown.
            entityCommandBufferSystem.Update();

            Assert.AreEqual(1, EmptySystem.GetEntityQuery(typeof(EcsTestData)).CalculateEntityCount());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void DeferredEntity_FromDifferentCommandBuffer_WithNoDeferredEntities_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using(var ecb1 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var ecb2 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var deferredEnt = ecb1.CreateEntity(archetype);
                ecb1.SetComponent(deferredEnt, new EcsTestData(17));
                ecb1.Playback(m_Manager);

                ecb2.SetComponent(deferredEnt, new EcsTestData(23));
                Assert.Throws<InvalidOperationException>(() => ecb2.Playback(m_Manager));
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void DeferredEntity_CreatedFromDifferentCommandBuffer_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using(var ecb1 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var ecb2 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var deferredEnt = ecb1.CreateEntity(archetype);
                ecb1.SetComponent(deferredEnt, new EcsTestData(17));
                ecb1.Playback(m_Manager);

                // Create one deferred entity in ecb2, so that deferredEnt's index isn't out of range
                var dummyEnt = ecb2.CreateEntity(archetype);
                ecb2.SetComponent(deferredEnt, new EcsTestData(23));
                Assert.Throws<InvalidOperationException>(() => ecb2.Playback(m_Manager));
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void DeferredEntity_InstantiatedFromDifferentCommandBuffer_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using(var ecb1 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var ecb2 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var prefab = m_Manager.CreateEntity(archetype);
                var deferredEnt = ecb1.Instantiate(prefab);
                ecb1.SetComponent(deferredEnt, new EcsTestData(17));
                ecb1.Playback(m_Manager);

                // Create one deferred entity in ecb2, so that deferredEnt's index isn't out of range
                var dummyEnt = ecb2.CreateEntity(archetype);
                ecb2.SetComponent(deferredEnt, new EcsTestData(23));
                Assert.Throws<InvalidOperationException>(() => ecb2.Playback(m_Manager));
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void DeferredEntity_CreatedFromDifferentCommandBuffer_ParallelWriter_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using(var ecb1 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var ecb2 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var writer1 = ecb1.AsParallelWriter();
                var deferredEnt = writer1.CreateEntity(0, archetype);
                writer1.SetComponent(0, deferredEnt, new EcsTestData(17));
                ecb1.Playback(m_Manager);

                // Create one deferred entity in ecb2, so that deferredEnt's index isn't out of range
                var dummyEnt = ecb2.CreateEntity(archetype);
                ecb2.SetComponent(deferredEnt, new EcsTestData(23));
                Assert.Throws<InvalidOperationException>(() => ecb2.Playback(m_Manager));
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void DeferredEntity_InstantiatedFromDifferentCommandBuffer_ParallelWriter_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using(var ecb1 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var ecb2 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var prefab = m_Manager.CreateEntity(archetype);
                var writer1 = ecb1.AsParallelWriter();
                var deferredEnt = writer1.Instantiate(0, prefab);
                writer1.SetComponent(0, deferredEnt, new EcsTestData(17));
                ecb1.Playback(m_Manager);

                // Create one deferred entity in ecb2, so that deferredEnt's index isn't out of range
                var dummyEnt = ecb2.CreateEntity(archetype);
                ecb2.SetComponent(deferredEnt, new EcsTestData(23));
                Assert.Throws<InvalidOperationException>(() => ecb2.Playback(m_Manager));
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void DeferredEntity_OutOfRangeIndex_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using(var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var deferredEnt = ecb.CreateEntity(archetype);
                deferredEnt.Index = -1000;
                ecb.SetComponent(deferredEnt, new EcsTestData(17));
                Assert.Throws<InvalidOperationException>(() => ecb.Playback(m_Manager));
            }
        }

        [Test]
        public void AddComponent_WhenDataContainsDeferredEntity_ThrowsOnMultiplePlaybacks()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            Entity e0 = cmds.CreateEntity();
            cmds.AddComponent(e0, new EcsTestDataEntity(1, e0));

            Assert.DoesNotThrow(() => cmds.Playback(m_Manager));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager2));
        }

        [Test]
        public void AddComponents_WhenDataContainsDeferredEntity_ThrowsOnMultiplePlaybacks()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            Entity e0 = cmds.CreateEntity();
            cmds.AddComponent(e0, new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2)));

            Assert.DoesNotThrow(() => cmds.Playback(m_Manager));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager2));
        }

        [Test]
        public void AddComponent_WhenDataContainsDeferredEntity_DeferredEntityIsResolved()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            Entity e0 = cmds.CreateEntity();
            cmds.AddComponent(e0, new EcsTestDataEntity(1, e0));

            cmds.Playback(m_Manager);

            using (var group = m_Manager.CreateEntityQuery(typeof(EcsTestDataEntity)))
            {
                var e = group.GetSingletonEntity();
                Assert.AreEqual(e, m_Manager.GetComponentData<EcsTestDataEntity>(e).value1);
            }
        }

        [Test]
        public void AddComponents_WhenDataContainsDeferredEntity_DeferredEntityIsResolved()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            Entity e0 = cmds.CreateEntity();
            cmds.AddComponent(e0, new ComponentTypeSet(typeof(EcsTestDataEntity)));
            cmds.SetComponent(e0, new EcsTestDataEntity(1, e0));
            cmds.Playback(m_Manager);

            using (var group = m_Manager.CreateEntityQuery(typeof(EcsTestDataEntity)))
            {
                var e = group.GetSingletonEntity();
                Assert.AreEqual(e, m_Manager.GetComponentData<EcsTestDataEntity>(e).value1);
            }
        }

        [Test]
        public void EntityCommands_WithManyDeferredEntities_PerformAsExpected()
        {
            using EntityCommandBuffer cmds = new EntityCommandBuffer(Allocator.Persistent);

#if UNITY_DOTSPLAYER_IL2CPP && !DEVELOP    // IL2CPP is a little slow in debug; reduce the number of tests in DEBUG (but not DEVELOP).
            const int step = 100;
#else
            const int step = 1;
#endif

            for (int i = 0; i < 2500; i += step)
            {
                Entity e = cmds.CreateEntity();
                cmds.AddComponent(e, new EcsTestData(i));
                cmds.SetComponent(e, new EcsTestData(i + 1));
                cmds.AddBuffer<EcsIntElement>(e);
                cmds.SetBuffer<EcsIntElement>(e);
                cmds.DestroyEntity(e);
            }
            cmds.Playback(m_Manager);

            var allEntities = m_Manager.GetAllEntities();
            Assert.AreEqual(0, allEntities.Length);
            allEntities.Dispose();
        }

        [Test]
        public void InstantiateEntity_BatchMode_DisabledIfEntityDirty()
        {
            using EntityCommandBuffer cmds = new EntityCommandBuffer(Allocator.Persistent);
            Entity esrc = m_Manager.CreateEntity();

            Entity edst0 = cmds.Instantiate(esrc);
            cmds.AddComponent(esrc, new EcsTestData2(12));
            Entity edst1 = cmds.Instantiate(esrc);
            cmds.AddComponent(esrc, new EcsTestDataEntity(33, edst1));

            cmds.Playback(m_Manager);

            var realDst1 = m_Manager.GetComponentData<EcsTestDataEntity>(esrc).value1;
            Assert.AreEqual(12, m_Manager.GetComponentData<EcsTestData2>(realDst1).value1);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void UninitializedEntityCommandBufferThrows()
        {
            using EntityCommandBuffer cmds = new EntityCommandBuffer();
            var exception = Assert.Throws<NullReferenceException>(() => cmds.CreateEntity());
            Assert.AreEqual("The EntityCommandBuffer has not been initialized! The EntityCommandBuffer needs to be passed an Allocator when created!", exception.Message);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void UninitializedConcurrentEntityCommandBufferThrows()
        {
            EntityCommandBuffer.ParallelWriter cmds = new EntityCommandBuffer.ParallelWriter();
            var exception = Assert.Throws<NullReferenceException>(() => cmds.CreateEntity(0));
            Assert.AreEqual("The EntityCommandBuffer has not been initialized! The EntityCommandBuffer needs to be passed an Allocator when created!", exception.Message);
        }

        [Test]
        public void AddOrSetBufferWithEntity_NeedsFixup_ThrowsOnMultiplePlayback([Values(true, false)] bool setBuffer)
        {
            using EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            Entity e0 = m_Manager.CreateEntity();
            Entity e1 = m_Manager.CreateEntity();
            Entity e2 = m_Manager.CreateEntity();

            if (setBuffer)
                m_Manager.AddComponent(e1, typeof(EcsComplexEntityRefElement));

            var deferred0 = cmds.CreateEntity();
            var deferred1 = cmds.CreateEntity();
            var deferred2 = cmds.CreateEntity();

            cmds.AddComponent(e0, new EcsTestDataEntity() { value1 = deferred0 });
            cmds.AddComponent(e1, new EcsTestDataEntity() { value1 = deferred1 });
            cmds.AddComponent(e2, new EcsTestDataEntity() { value1 = deferred2 });

            var buf = setBuffer ? cmds.SetBuffer<EcsComplexEntityRefElement>(e1) : cmds.AddBuffer<EcsComplexEntityRefElement>(e1);
            buf.Add(new EcsComplexEntityRefElement() {Entity = e0});
            buf.Add(new EcsComplexEntityRefElement() {Entity = deferred1});
            buf.Add(new EcsComplexEntityRefElement() {Entity = deferred2});
            buf.Add(new EcsComplexEntityRefElement() {Entity = deferred0});

            Assert.DoesNotThrow(() => cmds.Playback(m_Manager));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager));
            Assert.Throws<InvalidOperationException>(() => cmds.Playback(m_Manager2));
        }

        [Test]
        public void AddOrSetBufferWithEntity_NeedsFixup_ContainsRealizedEntity([Values(true, false)] bool setBuffer)
        {
            using EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            Entity e0 = m_Manager.CreateEntity();
            Entity e1 = m_Manager.CreateEntity();
            Entity e2 = m_Manager.CreateEntity();

            if (setBuffer)
                m_Manager.AddComponent(e1, typeof(EcsComplexEntityRefElement));

            {
                var deferred0 = cmds.CreateEntity();
                var deferred1 = cmds.CreateEntity();
                var deferred2 = cmds.CreateEntity();

                cmds.AddComponent(e0, new EcsTestDataEntity() { value1 = deferred0 });
                cmds.AddComponent(e1, new EcsTestDataEntity() { value1 = deferred1 });
                cmds.AddComponent(e2, new EcsTestDataEntity() { value1 = deferred2 });

                var buf = setBuffer ? cmds.SetBuffer<EcsComplexEntityRefElement>(e1) : cmds.AddBuffer<EcsComplexEntityRefElement>(e1);
                buf.Add(new EcsComplexEntityRefElement() {Entity = e0});
                buf.Add(new EcsComplexEntityRefElement() {Entity = deferred1});
                buf.Add(new EcsComplexEntityRefElement() {Entity = deferred2});
                buf.Add(new EcsComplexEntityRefElement() {Entity = deferred0});
                cmds.Playback(m_Manager);
            }
            {
                var outbuf = m_Manager.GetBuffer<EcsComplexEntityRefElement>(e1);
                Assert.AreEqual(4, outbuf.Length);
                var expect0 = m_Manager.GetComponentData<EcsTestDataEntity>(e0).value1;
                var expect1 = m_Manager.GetComponentData<EcsTestDataEntity>(e1).value1;
                var expect2 = m_Manager.GetComponentData<EcsTestDataEntity>(e2).value1;
                Assert.AreEqual(e0, outbuf[0].Entity);
                Assert.AreEqual(expect1, outbuf[1].Entity);
                Assert.AreEqual(expect2, outbuf[2].Entity);
                Assert.AreEqual(expect0, outbuf[3].Entity);
            }
        }

        [Test]
        public void BufferWithEntity_DelayedFixup_ContainsRealizedEntity()
        {
            int kNumOfBuffers = 12; // Must be > 2
            int kNumOfDeferredEntities = 12;

            using EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            Entity[] e = new Entity[kNumOfBuffers];

            for (int n = 0; n < kNumOfBuffers; n++)
            {
                e[n] = m_Manager.CreateEntity();
                var buf = cmds.AddBuffer<EcsComplexEntityRefElement>(e[n]);
                for (int i = 0; i < kNumOfDeferredEntities; i++)
                    buf.Add(new EcsComplexEntityRefElement() {Entity = cmds.CreateEntity()});
            }

            cmds.RemoveComponent<EcsComplexEntityRefElement>(e[0]);
            cmds.DestroyEntity(e[1]);

            cmds.Playback(m_Manager);

            Assert.IsFalse(m_Manager.HasComponent<EcsComplexEntityRefElement>(e[0]));
            Assert.IsFalse(m_Manager.Exists(e[1]));

            for (int n = 2; n < kNumOfBuffers; n++)
            {
                var outbuf = m_Manager.GetBuffer<EcsComplexEntityRefElement>(e[n]);
                Assert.AreEqual(kNumOfDeferredEntities, outbuf.Length);
                for (int i = 0; i < outbuf.Length; i++)
                {
                    Assert.IsTrue(m_Manager.Exists(outbuf[i].Entity));
                }
            }
        }

        [Test]
        public void AddComponentForLinkedEntityGroup_Works()
        {
            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity();
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestData>(child);
                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);

            var mask = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>()).GetEntityQueryMask();

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddComponentForLinkedEntityGroup(instance, mask, new EcsTestData2(42));
                cmds.Playback(m_Manager);
            }

            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData,EcsTestData2>()
                .Build(m_Manager);
            using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(5, results.Length);
                for (int i = 0; i < results.Length; i++)
                {
                    Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData2>(results[i]).value0);
                }
            }

            array.Dispose();
        }

        [Test]
        public void AddComponentForLinkedEntityGroup_WithEntityFixup_Works()
        {
            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity();
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestData>(child);
                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);

            var mask = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>()).GetEntityQueryMask();

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var e2 = cmds.CreateEntity();
                cmds.AddComponent<EcsTestTag>(e2);
                cmds.AddComponentForLinkedEntityGroup(instance, mask, new EcsTestDataEntity(17,e2));
                cmds.Playback(m_Manager);
            }

            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData,EcsTestDataEntity>()
                .Build(m_Manager);
            using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(5, results.Length);
                for (int i = 0; i < results.Length; i++)
                {
                    var value = m_Manager.GetComponentData<EcsTestDataEntity>(results[i]);
                    Assert.AreEqual(17, value.value0);
                    Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(value.value1));
                }
            }

            array.Dispose();
        }

        [Test]
        public void AddComponentForLinkedEntityGroup_Tag_Works()
        {
            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity(typeof(Prefab));
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestData>(child);
                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);
            var mask = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>()).GetEntityQueryMask();

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddComponentForLinkedEntityGroup(instance, mask, ComponentType.ReadWrite<EcsTestTag>());
                cmds.Playback(m_Manager);
            }

            var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<EcsTestTag>());
            using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(5, results.Length);
            }

            array.Dispose();
        }

        [Test]
        public void AddComponentForLinkedEntityGroup_AfterNewArchetypesAdded_Works()
        {
            var prefabEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var legEntities = new NativeArray<Entity>(10, Allocator.Temp);
            for (var i = 0; i < 10; i++)
            {
                legEntities[i] = m_Manager.CreateEntity(typeof(Prefab));
                if (i % 2 == 0) // Half the children have EcsTestData
                    m_Manager.AddComponent<EcsTestData>(legEntities[i]);
            }
            var legBuffer = m_Manager.GetBuffer<LinkedEntityGroup>(prefabEntity);
            legBuffer.Add(prefabEntity);
            foreach (var e in legEntities)
                legBuffer.Add(e);
            var instance = m_Manager.Instantiate(prefabEntity);
            var firstChild = m_Manager.GetBuffer<LinkedEntityGroup>(instance)[1].Value;
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(firstChild));

            using var childQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>()
                .Build(m_Manager);
            using var childEntities = childQuery.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(5, childEntities.Length);
            var mask = childQuery.GetEntityQueryMask();

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            // move one of the children with EcsTestData to a new archetype, not currently present in query's MatchingArchetypes list
            ecb.AddComponent<EcsTestTag>(firstChild);
            // This command should still target the instance
            ecb.AddComponentForLinkedEntityGroup(instance, mask, new EcsTestData2(42));
            ecb.Playback(m_Manager);

            var resultsQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData,EcsTestData2>()
                .Build(m_Manager);
            using var results = resultsQuery.ToEntityArray(World.UpdateAllocator.ToAllocator);
            CollectionAssert.AreEqual(childEntities.ToArray(), results.ToArray());
            foreach(var e in results)
                Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData2>(e).value0);
        }

        [Test]
        public void SetComponentForLinkedEntityGroup_Works()
        {
            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity(typeof(Prefab));
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestData>(child);
                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            var mask = query.GetEntityQueryMask();

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetComponentForLinkedEntityGroup(instance, mask, new EcsTestData(42));
                cmds.Playback(m_Manager);
            }

            using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(5, results.Length);
                for (int i = 0; i < results.Length; i++)
                {
                    Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(results[i]).value);
                }
            }

            array.Dispose();
        }

        [Test]
        public void SetComponentForLinkedEntityGroup_WithEntityFixup_Works()
        {
            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity(typeof(Prefab));
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestDataEntity>(child);
                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup { Value = array[i] });
            }

            var instance = m_Manager.Instantiate(rootEntity);
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestDataEntity>()
                .Build(m_Manager);
            var mask = query.GetEntityQueryMask();

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var e2 = cmds.CreateEntity();
                cmds.AddComponent<EcsTestTag>(e2);
                cmds.SetComponentForLinkedEntityGroup(instance, mask, new EcsTestDataEntity(42, e2));
                cmds.Playback(m_Manager);
            }

            using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(5, results.Length);
                for (int i = 0; i < results.Length; i++)
                {
                    var value = m_Manager.GetComponentData<EcsTestDataEntity>(results[i]);
                    Assert.AreEqual(42, value.value0);
                    Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(value.value1));
                }
            }

            array.Dispose();
        }

        [Test]
        public void SetComponentForLinkedEntityGroup_AfterNewArchetypesAdded_Works()
        {
            var prefabEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var legEntities = new NativeArray<Entity>(10, Allocator.Temp);
            for (var i = 0; i < 10; i++)
            {
                legEntities[i] = m_Manager.CreateEntity(typeof(Prefab));
                if (i % 2 == 0) // Half the children have EcsTestData
                    m_Manager.AddComponent<EcsTestData>(legEntities[i]);
            }
            var legBuffer = m_Manager.GetBuffer<LinkedEntityGroup>(prefabEntity);
            legBuffer.Add(prefabEntity);
            foreach (var e in legEntities)
                legBuffer.Add(e);
            var instance = m_Manager.Instantiate(prefabEntity);
            var firstChild = m_Manager.GetBuffer<LinkedEntityGroup>(instance)[1].Value;
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(firstChild));

            using var childQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>()
                .Build(m_Manager);
            Assert.AreEqual(5, childQuery.CalculateEntityCount());
            var mask = childQuery.GetEntityQueryMask();

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            // move one of the children with EcsTestData to a new archetype, not currently present in query's MatchingArchetypes list
            ecb.AddComponent<EcsTestTag>(firstChild);
            // This command should still target the instance
            ecb.SetComponentForLinkedEntityGroup(instance, mask, new EcsTestData(42));
            ecb.Playback(m_Manager);

            using var results = childQuery.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(5, results.Length);
            foreach(var e in results)
                Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(e).value);
        }


        [Test]
        public void ReplaceComponentForLinkedEntityGroup_Works()
        {
            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity(typeof(Prefab));
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestData>(child);
                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.ReplaceComponentForLinkedEntityGroup(instance, new EcsTestData(42));
                cmds.Playback(m_Manager);
            }

            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(5, results.Length);
                for (int i = 0; i < results.Length; i++)
                {
                    Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(results[i]).value);
                }
            }

            array.Dispose();
        }

        [Test]
        public void ReplaceComponentForLinkedEntityGroup_WithEntityFixup_Works()
        {
            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity(typeof(Prefab));
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestDataEntity>(child);
                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var e2 = cmds.CreateEntity();
                cmds.AddComponent<EcsTestTag>(e2);
                cmds.ReplaceComponentForLinkedEntityGroup(instance, new EcsTestDataEntity(42,e2));
                cmds.Playback(m_Manager);
            }

            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestDataEntity>().Build(m_Manager);
            using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(5, results.Length);
                for (int i = 0; i < results.Length; i++)
                {
                    var value = m_Manager.GetComponentData<EcsTestDataEntity>(results[i]);
                    Assert.AreEqual(42, value.value0);
                    Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(value.value1));
                }
            }

            array.Dispose();
        }

        [Test]
        public void AddComponentForLinkedEntityGroup_Parallel_Works()
        {
            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity(typeof(Prefab));
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestData>(child);
                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);
            var mask = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>()).GetEntityQueryMask();

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AsParallelWriter().AddComponentForLinkedEntityGroup(1, instance, mask, new EcsTestData2(42));
                cmds.Playback(m_Manager);
            }

            var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>());
            using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(5, results.Length);
                for (int i = 0; i < results.Length; i++)
                {
                    Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData2>(results[i]).value0);
                }
            }

            array.Dispose();
        }

        [Test]
        public void AddComponentForLinkedEntityGroup_Tag_Parallel_Works()
        {
            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity(typeof(Prefab));
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestData>(child);
                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);
            var mask = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>()).GetEntityQueryMask();

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AsParallelWriter().AddComponentForLinkedEntityGroup(1, instance, mask, ComponentType.ReadWrite<EcsTestTag>());
                cmds.Playback(m_Manager);
            }

            var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<EcsTestTag>());
            using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(5, results.Length);
            }

            array.Dispose();
        }

        [Test]
        public void SetComponentForLinkedEntityGroup_Parallel_Works()
        {
            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity(typeof(Prefab));
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestData>(child);
                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);
            var mask = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>()).GetEntityQueryMask();

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AsParallelWriter().SetComponentForLinkedEntityGroup(1, instance, mask, new EcsTestData(42));
                cmds.Playback(m_Manager);
            }

            var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());
            using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(5, results.Length);
                for (int i = 0; i < results.Length; i++)
                {
                    Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(results[i]).value);
                }
            }

            array.Dispose();
        }

        [Test]
        public void ReplaceComponentForLinkedEntityGroup_Parallel_Works()
        {
            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity(typeof(Prefab));
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestData>(child);
                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AsParallelWriter().ReplaceComponentForLinkedEntityGroup(1, instance, new EcsTestData(42));
                cmds.Playback(m_Manager);
            }

            var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());
            using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(5, results.Length);
                for (int i = 0; i < results.Length; i++)
                {
                    Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(results[i]).value);
                }
            }

            array.Dispose();
        }

        [Test]
        public void CommandsForLinkedEntityGroup_ComponentWithEntityReference_Works()
        {
            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);
            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity();
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestData>(child);
                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var mask = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>()).GetEntityQueryMask();

            using EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var deferredEntity = cmds.CreateEntity();
            cmds.AddComponentForLinkedEntityGroup(rootEntity, mask, new EcsTestDataEntity{value0 = 17, value1 = deferredEntity});
            cmds.SetComponentForLinkedEntityGroup(rootEntity, mask, new EcsTestDataEntity{value0 = 42, value1 = deferredEntity});
            cmds.ReplaceComponentForLinkedEntityGroup(rootEntity, new EcsTestDataEntity{value0 = 23, value1 = deferredEntity});
            cmds.Playback(m_Manager);

            for (int i = 0; i < array.Length; ++i)
            {
                if (m_Manager.HasComponent<EcsTestData>(array[i]))
                {
                    var testData = m_Manager.GetComponentData<EcsTestDataEntity>(array[i]);
                    Assert.AreEqual(23, testData.value0);
                    Assert.IsTrue(m_Manager.Exists(testData.value1));
                }
                else
                {
                    Assert.IsFalse(m_Manager.HasComponent<EcsTestDataEntity>(array[i]));
                }
            }

            array.Dispose();
        }

        void VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(bool shouldThrow, TestDelegate code)
        {
            if (shouldThrow)
            {
                var ex = Assert.Throws<ArgumentException>(code);
                Assert.IsTrue(ex.Message.Contains("deferred"));
            }
            else
            {
                code();
            }
        }

        void RunDeferredTest(Entity entity)
        {
            bool isDeferredEntity = entity.Index < 0;

            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.AddComponent(entity, typeof(EcsTestData)));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.RemoveComponent(entity, typeof(EcsTestData)));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.AddComponent(entity, new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2))));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.AddComponentData(entity, new EcsTestData()));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.SetComponentData(entity, new EcsTestData()));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.GetComponentData<EcsTestData>(entity));

            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entity));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.IsComponentEnabled(entity, typeof(EcsTestDataEnableable)));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entity, true));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.SetComponentEnabled(entity, typeof(EcsTestDataEnableable), true));

            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp()));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp()));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.RemoveComponent(entity, typeof(EcsTestSharedComp)));

            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.AddBuffer<EcsIntElement>(entity));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.GetBuffer<EcsIntElement>(entity));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.Exists(entity));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.HasComponent(entity, typeof(EcsTestData2)));
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.GetComponentCount(entity));

            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.DestroyEntity(entity));

#if !DOTS_DISABLE_DEBUG_NAMES
            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.SetName(entity,"Name"));

            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.SetName(entity,new FixedString64Bytes("Name")));

            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.GetName(entity));

            VerifyCommand_Or_CheckThatItThrowsIfEntityIsDeferred(
                isDeferredEntity,
                () => m_Manager.GetName(entity, out var fixed_name));
#endif
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void DeferredEntities_UsedInTheEntityManager_ShouldThrow()
        {
            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
                var deferred = cmds.CreateEntity(archetype);
                cmds.AddComponent(cmds.CreateEntity(), new EcsTestDataEntity()
                {
                    value1 = deferred
                });

                RunDeferredTest(deferred);

                cmds.Playback(m_Manager);
                using (var group = m_Manager.CreateEntityQuery(typeof(EcsTestDataEntity)))
                using (var arr = group.ToComponentDataArray<EcsTestDataEntity>(World.UpdateAllocator.ToAllocator))
                {
                    RunDeferredTest(arr[0].value1);
                }
            }
        }

        [Test]
        public void IsEmpty_Works()
        {
            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                Assert.IsTrue(cmds.IsEmpty, "newly-created EntityCommandBuffer has IsEmpty=false");
                cmds.CreateEntity();
                Assert.IsFalse(cmds.IsEmpty, "EntityCommandBuffer with one recorded commands has IsEmpty=true");
            }
        }

        [Test]
        public void AddSharedComponent_WithEntityQuery_ThatHasNoMatch_WillNotCorruptInternalState([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData));
                cmds.AddSharedComponent(entityQuery, new EcsTestSharedComp(1), queryCaptureMode);

                // modifying the entity in between recording and playback should be OK
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                cmds.Playback(m_Manager);
            }
            m_Manager.Debug.CheckInternalConsistency();
        }

        [Test]
        public void AddAndRemoveComponent_EntityQueryCacheIsValid()
        {
            var ent = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            using(EntityQuery query2 = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), ComponentType.Exclude<EcsTestData3>()))
            using(EntityQuery query3 = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3)))
            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddComponent<EcsTestData3>(ent);
                cmds.RemoveComponent<EcsTestData3>(ent);
                cmds.Playback(m_Manager);
                Assert.IsFalse(query2.IsCacheValid);
                Assert.IsFalse(query3.IsCacheValid);
            }
        }

        [Test]
        public void RemoveAndAddComponent_EntityQueryCacheIsValid()
        {
            var ent = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            using(EntityQuery query2 = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2)))
            using(EntityQuery query1 = m_Manager.CreateEntityQuery(typeof(EcsTestData), ComponentType.Exclude<EcsTestData2>()))
            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.RemoveComponent<EcsTestData2>(ent);
                cmds.AddComponent<EcsTestData2>(ent);
                cmds.Playback(m_Manager);
                Assert.IsFalse(query2.IsCacheValid);
                Assert.IsFalse(query1.IsCacheValid);
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        private void VerifyEcsTestManagedComponent(int length, string expectedValue)
        {
            var allEntities = m_Manager.GetAllEntities();
            Assert.AreEqual(length, allEntities.Length);

            for (int i = 0; i < length; ++i)
            {
                var component = m_Manager.GetComponentData<EcsTestManagedComponent>(allEntities[i]);
                Assert.AreEqual(expectedValue, component.value);
            }
            allEntities.Dispose();
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void Playback_EmptyCommandBuffer_DoesntInvalidateSafetyHandles()
        {
            var ent = m_Manager.CreateEntity(typeof(EcsIntElement));
            var bfe = m_Manager.GetBufferLookup<EcsIntElement>();
            var buf = bfe[ent];
            using (var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                ecb.Playback(m_Manager);
            }
            Assert.DoesNotThrow(() => AtomicSafetyHandle.CheckReadAndThrow(buf.m_Safety0));
        }
#endif

        [Test]
        public void CreateEntity_ManagedComponents()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            var e = cmds.CreateEntity();
            cmds.AddComponent(e, new EcsTestManagedComponent { value = "SomeString" });
            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager2);

            {
                var group = m_Manager.CreateEntityQuery(typeof(EcsTestManagedComponent));
                var arr = group.ToComponentDataArray<EcsTestManagedComponent>();
                Assert.AreEqual(1, arr.Length);
                Assert.AreEqual("SomeString", arr[0].value);
                group.Dispose();
            }
            {
                var group = m_Manager2.CreateEntityQuery(typeof(EcsTestManagedComponent));
                var arr = group.ToComponentDataArray<EcsTestManagedComponent>();
                Assert.AreEqual(1, arr.Length);
                Assert.AreEqual("SomeString", arr[0].value);
                group.Dispose();
            }
        }

        [Test]
        public void CreateEntityWithArchetype_ManagedComponents()
        {
            var a = m_Manager.CreateArchetype(typeof(EcsTestManagedComponent));

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var e = cmds.CreateEntity(a);
            cmds.SetComponent(e, new EcsTestManagedComponent { value = "SomeString" });
            cmds.Playback(m_Manager);

            var group = m_Manager.CreateEntityQuery(typeof(EcsTestManagedComponent));
            var arr = group.ToComponentDataArray<EcsTestManagedComponent>();
            Assert.AreEqual(1, arr.Length);
            Assert.AreEqual("SomeString", arr[0].value);
            group.Dispose();
        }

        [Test]
        public void CreateTwoComponents_ManagedComponents()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator, PlaybackPolicy.MultiPlayback);
            var e = cmds.CreateEntity();
            cmds.AddComponent(e, new EcsTestManagedComponent { value = "SomeString" });
            cmds.AddComponent(e, new EcsTestManagedComponent2 { value = "SomeString", value2 = "SomeOtherString" });
            cmds.Playback(m_Manager);
            cmds.Playback(m_Manager2);

            using(var group = m_Manager.CreateEntityQuery(typeof(EcsTestManagedComponent)))
            {
                var component = EntityQueryManagedComponentExtensions.GetSingleton<EcsTestManagedComponent>(group);
                Assert.AreEqual("SomeString", component.value);
            }

            using(var group = m_Manager.CreateEntityQuery(typeof(EcsTestManagedComponent2)))
            {
                var component = EntityQueryManagedComponentExtensions.GetSingleton<EcsTestManagedComponent2>(group);
                Assert.AreEqual("SomeString", component.value);
                Assert.AreEqual("SomeOtherString", component.value2);
            }
            using(var group = m_Manager2.CreateEntityQuery(typeof(EcsTestManagedComponent)))
            {
                var component = EntityQueryManagedComponentExtensions.GetSingleton<EcsTestManagedComponent>(group);
                Assert.AreEqual("SomeString", component.value);
            }

            using (var group = m_Manager2.CreateEntityQuery(typeof(EcsTestManagedComponent2)))
            {
                var component = EntityQueryManagedComponentExtensions.GetSingleton<EcsTestManagedComponent2>(group);
                Assert.AreEqual("SomeString", component.value);
                Assert.AreEqual("SomeOtherString", component.value2);
            }
        }

        [Test]
        public void AddComponent_ManagedComponents()
        {
            var e = m_Manager.CreateEntity();

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            cmds.AddComponent(e, new EcsTestManagedComponent() { value = "SomeString" });
            cmds.AddComponent<EcsTestTag>(e);
            cmds.AddComponent(e, ComponentType.ReadWrite<EcsTestManagedComponent3>());

            cmds.Playback(m_Manager);

            Assert.AreEqual("SomeString", m_Manager.GetComponentData<EcsTestManagedComponent>(e).value);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(e));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent3>(e));
        }

        [Test]
        public void AddComponents_ManagedComponents()
        {
            var e = m_Manager.CreateEntity();

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent(e, new ComponentTypeSet(typeof(EcsTestManagedComponent), typeof(EcsTestTag), typeof(EcsTestManagedComponent3)));
            cmds.SetComponent(e, new EcsTestManagedComponent() { value = "SomeString" });
            cmds.Playback(m_Manager);

            Assert.AreEqual("SomeString", m_Manager.GetComponentData<EcsTestManagedComponent>(e).value);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(e));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent3>(e));
        }

        [Test]
        public void AddComponentToEntityQuery_ManagedComponents([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var entity = m_Manager.CreateEntity();
            var data1 = new EcsTestManagedComponent();
            m_Manager.AddComponentData(entity, data1);
            m_Manager.AddComponent(entity, typeof(EcsTestManagedComponent));

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestManagedComponent)))
            {
                cmds.AddComponent(entityQuery, typeof(EcsTestManagedComponent2), queryCaptureMode);

                // modifying the entity in between recording and playback should be OK
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                cmds.Playback(m_Manager);

                using (var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(1, entities.Length);
                    Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent2>(entities[0]), "The component was not added to the entities within the entity query.");
                }
            }
        }

        [Test]
        public void AddComponentToEntityQueryWithFilter_ManagedComponents([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp));

            var entity1 = m_Manager.CreateEntity(archetype);
            var sharedComponent1 = new EcsTestSharedComp { value = 10 };
            m_Manager.SetSharedComponentManaged(entity1, sharedComponent1);

            var entity2 = m_Manager.CreateEntity(archetype);
            var sharedComponent2 = new EcsTestSharedComp { value = 130 };
            m_Manager.SetSharedComponentManaged(entity2, sharedComponent2);

            var entity3 = m_Manager.CreateEntity(archetype);
            m_Manager.SetSharedComponentManaged(entity3, sharedComponent2);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp)))
            {
                entityQuery.SetSharedComponentFilterManaged(sharedComponent2);
                cmds.AddComponent(entityQuery, typeof(EcsTestManagedComponent2), queryCaptureMode);

                // modifying an entity in between recording and playback means it won't be processed by AtPlayback,
                // but will still be processed by AtRecord
                m_Manager.SetSharedComponent(entity2, new EcsTestSharedComp { value = 200 });
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                cmds.Playback(m_Manager);

                using var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator);
                CollectionAssert.AreEquivalent(new[] { entity1, entity2, entity3 }, entities.ToArray());
                Assert.IsFalse(m_Manager.HasComponent<EcsTestManagedComponent2>(entity1), "EcsTestManagedComponent2 should not have been added");
                Assert.AreEqual(queryCaptureMode == EntityQueryCaptureMode.AtRecord,
                    m_Manager.HasComponent<EcsTestManagedComponent2>(entity2)); // this entity was modified between recording and playback
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent2>(entity3), "EcsTestManagedComponent2 should have been added");
            }
        }

        [Test]
        public void AddComponentsToEntityQueryWithFilter_ManagedComponents([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp));

            var entity1 = m_Manager.CreateEntity(archetype);
            var sharedComponent1 = new EcsTestSharedComp { value = 10 };
            m_Manager.SetSharedComponentManaged(entity1, sharedComponent1);

            var entity2 = m_Manager.CreateEntity(archetype);
            var sharedComponent2 = new EcsTestSharedComp { value = 130 };
            m_Manager.SetSharedComponentManaged(entity2, sharedComponent2);

            var entity3 = m_Manager.CreateEntity(archetype);
            m_Manager.SetSharedComponentManaged(entity3, sharedComponent2);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp)))
            {
                entityQuery.SetSharedComponentFilterManaged(sharedComponent2);
                cmds.AddComponent(entityQuery, new ComponentTypeSet(typeof(EcsTestManagedComponent2)),
                    queryCaptureMode);

                // modifying an entity in between recording and playback means it won't be processed by AtPlayback,
                // but will still be processed by AtRecord
                m_Manager.SetSharedComponent(entity2, new EcsTestSharedComp { value = 200 });
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                cmds.Playback(m_Manager);

                using var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator);
                CollectionAssert.AreEquivalent(new[] { entity1, entity2, entity3 }, entities.ToArray());
                Assert.IsFalse(m_Manager.HasComponent<EcsTestManagedComponent2>(entity1), "EcsTestManagedComponent2 should not have been added");
                Assert.AreEqual(queryCaptureMode == EntityQueryCaptureMode.AtRecord,
                    m_Manager.HasComponent<EcsTestManagedComponent2>(entity2)); // this entity was modified between recording and playback
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent2>(entity3), "EcsTestManagedComponent2 should have been added");
            }
        }

        [Test]
        public void RemoveComponentFromEntityQuery_ManagedComponents([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestManagedComponent));
            var entity = m_Manager.CreateEntity(archetype);
            var data1 = new EcsTestManagedComponent();
            m_Manager.SetComponentData(entity, data1);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestManagedComponent)))
            {
                cmds.RemoveComponent(entityQuery, typeof(EcsTestManagedComponent), queryCaptureMode);

                // modifying the entity in between recording and playback should be OK
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                cmds.Playback(m_Manager);

                using (var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(1, entities.Length);
                    Assert.IsFalse(m_Manager.HasComponent<EcsTestManagedComponent>(entities[0]),
                        "The component was not removed from the entities in the entity query.");
                }
            }
        }

        [Test]
        public void RemoveComponentFromEntityQuery_ValidateComponents_ManagedComponents([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3), typeof(EcsTestManagedComponent4));

            var entity = m_Manager.CreateEntity(archetype);
            var data1 = new EcsTestManagedComponent() { value = "SomeString" };
            var data2 = new EcsTestManagedComponent2() { value = "SomeOtherString" };
            var data3 = new EcsTestManagedComponent3() { value = "YetAnotherString" };
            var data4 = new EcsTestManagedComponent4() { value = "SoManyStrings" };
            m_Manager.SetComponentData(entity, data1);
            m_Manager.SetComponentData(entity, data2);
            m_Manager.SetComponentData(entity, data3);
            m_Manager.SetComponentData(entity, data4);

            {
                var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent2>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent3>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent4>(entities[0]));
                Assert.AreEqual("SomeString", m_Manager.GetComponentData<EcsTestManagedComponent>(entities[0]).value);
                Assert.AreEqual("SomeOtherString", m_Manager.GetComponentData<EcsTestManagedComponent2>(entities[0]).value);
                Assert.AreEqual("YetAnotherString", m_Manager.GetComponentData<EcsTestManagedComponent3>(entities[0]).value);
                Assert.AreEqual("SoManyStrings", m_Manager.GetComponentData<EcsTestManagedComponent4>(entities[0]).value);
            }

            var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestManagedComponent));
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.RemoveComponent(entityQuery, typeof(EcsTestManagedComponent2), queryCaptureMode);

            // modifying the entity in between recording and playback should be OK
            m_Manager.AddComponent<EcsTestData3>(entityQuery);

            cmds.Playback(m_Manager);

            {
                var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent>(entities[0]));
                Assert.IsFalse(m_Manager.HasComponent<EcsTestManagedComponent2>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent3>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent4>(entities[0]));
                Assert.AreEqual("SomeString", m_Manager.GetComponentData<EcsTestManagedComponent>(entities[0]).value);
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                Assert.Throws<ArgumentException>(() => { m_Manager.GetComponentData<EcsTestManagedComponent2>(entities[0]); });
#endif
                Assert.AreEqual("YetAnotherString", m_Manager.GetComponentData<EcsTestManagedComponent3>(entities[0]).value);
                Assert.AreEqual("SoManyStrings", m_Manager.GetComponentData<EcsTestManagedComponent4>(entities[0]).value);
            }
            entityQuery.Dispose();
        }

        [Test]
        public void RemoveComponentFromEntityQueryWithFilter_ManagedComponents([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestManagedComponent));

            var entity1 = m_Manager.CreateEntity(archetype);
            var sharedComponent1 = new EcsTestSharedComp {value = 10};
            m_Manager.SetSharedComponentManaged(entity1, sharedComponent1);

            var entity2 = m_Manager.CreateEntity(archetype);
            var sharedComponent2 = new EcsTestSharedComp {value = 130};
            m_Manager.SetSharedComponentManaged(entity2, sharedComponent2);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery =
                m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp), typeof(EcsTestManagedComponent)))
            {
                entityQuery.SetSharedComponentFilterManaged(sharedComponent2);
                cmds.RemoveComponent(entityQuery, typeof(EcsTestManagedComponent), queryCaptureMode);

                // modifying the entity in between recording and playback should be OK
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                cmds.Playback(m_Manager);

                using (var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, entities.Length);

                    for (var i = 0; i < entities.Length; i++)
                    {
                        var e = entities[i];
                        var shared = m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(e);
                        if (shared.value == 10)
                        {
                            Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent>(e));
                        } else
                        {
                            Assert.AreEqual(130, shared.value);
                            Assert.IsFalse(m_Manager.HasComponent<EcsTestManagedComponent>(e));
                        }
                    }
                }
            }
        }

        [Test]
        public void RemoveComponentsFromEntityQueryWithFilter_ManagedComponents([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestManagedComponent));

            var entity1 = m_Manager.CreateEntity(archetype);
            var sharedComponent1 = new EcsTestSharedComp { value = 10 };
            m_Manager.SetSharedComponentManaged(entity1, sharedComponent1);

            var entity2 = m_Manager.CreateEntity(archetype);
            var sharedComponent2 = new EcsTestSharedComp { value = 130 };
            m_Manager.SetSharedComponentManaged(entity2, sharedComponent2);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp), typeof(EcsTestManagedComponent)))
            {
                entityQuery.SetSharedComponentFilterManaged(sharedComponent2);
                cmds.RemoveComponent(entityQuery, new ComponentTypeSet(typeof(EcsTestManagedComponent)),
                    queryCaptureMode);

                // modifying the entity in between recording and playback should be OK
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                cmds.Playback(m_Manager);

                using (var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, entities.Length);

                    for (var i = 0; i < entities.Length; i++)
                    {
                        var e = entities[i];
                        var shared = m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(e);
                        if (shared.value == 10)
                        {
                            Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent>(e));
                        } else
                        {
                            Assert.AreEqual(130, shared.value);
                            Assert.IsFalse(m_Manager.HasComponent<EcsTestManagedComponent>(e));
                        }
                    }
                }
            }
        }

        [Test]
        public void RemoveComponentsFromEntityQuery_ManagedComponents([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestManagedComponent));

            var entity = m_Manager.CreateEntity(archetype);
            var data1 = new EcsTestManagedComponent();
            m_Manager.SetComponentData(entity, data1);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestManagedComponent)))
            {
                cmds.RemoveComponent(entityQuery, new ComponentTypeSet(typeof(EcsTestManagedComponent)), queryCaptureMode);

                // modifying the entity in between recording and playback should be OK
                m_Manager.AddComponent<EcsTestData3>(entityQuery);

                cmds.Playback(m_Manager);

                using (var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(1, entities.Length);
                    Assert.IsFalse(m_Manager.HasComponent<EcsTestManagedComponent>(entities[0]), "The component was not removed from the entities in the entity query.");
                }
            }
        }

        [Test]
        public void RemoveComponentsFromEntityQuery_ValidateComponents_ManagedComponents([Values] EntityQueryCaptureMode queryCaptureMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3), typeof(EcsTestManagedComponent4));

            var entity = m_Manager.CreateEntity(archetype);
            var data1 = new EcsTestManagedComponent() { value = "SomeString" };
            var data2 = new EcsTestManagedComponent2() { value = "SomeOtherString" };
            var data3 = new EcsTestManagedComponent3() { value = "YetAnotherString" };
            var data4 = new EcsTestManagedComponent4() { value = "SoManyStrings" };
            m_Manager.SetComponentData(entity, data1);
            m_Manager.SetComponentData(entity, data2);
            m_Manager.SetComponentData(entity, data3);
            m_Manager.SetComponentData(entity, data4);

            {
                var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent2>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent3>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent4>(entities[0]));
                Assert.AreEqual("SomeString", m_Manager.GetComponentData<EcsTestManagedComponent>(entities[0]).value);
                Assert.AreEqual("SomeOtherString", m_Manager.GetComponentData<EcsTestManagedComponent2>(entities[0]).value);
                Assert.AreEqual("YetAnotherString", m_Manager.GetComponentData<EcsTestManagedComponent3>(entities[0]).value);
                Assert.AreEqual("SoManyStrings", m_Manager.GetComponentData<EcsTestManagedComponent4>(entities[0]).value);
            }

            var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestManagedComponent));
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.RemoveComponent(entityQuery, new ComponentTypeSet(typeof(EcsTestManagedComponent2)),
                queryCaptureMode);

            // modifying the entity in between recording and playback should be OK
            m_Manager.AddComponent<EcsTestData3>(entityQuery);

            cmds.Playback(m_Manager);

            {
                var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator);
                Assert.AreEqual(1, entities.Length);
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent>(entities[0]));
                Assert.IsFalse(m_Manager.HasComponent<EcsTestManagedComponent2>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent3>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent4>(entities[0]));
                Assert.AreEqual("SomeString", m_Manager.GetComponentData<EcsTestManagedComponent>(entities[0]).value);
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                Assert.Throws<ArgumentException>(() => { m_Manager.GetComponentData<EcsTestManagedComponent2>(entities[0]); });
#endif
                Assert.AreEqual("YetAnotherString", m_Manager.GetComponentData<EcsTestManagedComponent3>(entities[0]).value);
                Assert.AreEqual("SoManyStrings", m_Manager.GetComponentData<EcsTestManagedComponent4>(entities[0]).value);
            }
            entityQuery.Dispose();
        }

        [Test]
        public void Instantiate_ManagedComponents()
        {
            var e = m_Manager.CreateEntity();
            var component = new EcsTestManagedComponent() { value = "SomeString" };
            m_Manager.AddComponentData(e, component);

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.Instantiate(e);
            cmds.Instantiate(e);
            cmds.Playback(m_Manager);

            VerifyEcsTestManagedComponent(3, "SomeString");
        }

        [Test]
        public void InstantiateWithSetComponentDataWorks_ManagedComponents()
        {
            var e = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e, new EcsTestManagedComponent() { value = "SomeString" });

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            var e1 = cmds.Instantiate(e);
            cmds.SetComponent(e1, new EcsTestManagedComponent() { value = "SomeOtherString" });

            var e2 = cmds.Instantiate(e);
            cmds.SetComponent(e2, new EcsTestManagedComponent() { value = "SomeOtherString" });

            cmds.Playback(m_Manager);

            m_Manager.DestroyEntity(e);

            VerifyEcsTestManagedComponent(2, "SomeOtherString");
        }

        [Test]
        public void DestroyEntityTwiceWorks_ManagedComponents()
        {
            var e = m_Manager.CreateEntity();
            m_Manager.AddComponentData(e, new EcsTestManagedComponent());

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            cmds.DestroyEntity(e);
            cmds.DestroyEntity(e);

            cmds.Playback(m_Manager);

            Assert.IsFalse(m_Manager.Exists(e));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void DestroyInvalidEntity_ManagedComponents()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var entityBuffer = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(1, ref World.UpdateAllocator);
            var e = cmds.CreateEntity();
            cmds.AddComponent(e, new EcsTestManagedComponent { value = "SomeString" });
            entityBuffer[0] = e;
            cmds.Playback(m_Manager);

            var savedEntity = entityBuffer[0];

            using var cmds2 = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds2.DestroyEntity(savedEntity);

            // savedEntity is invalid, so playing back this ECB should throw an exception
            Assert.Throws<System.InvalidOperationException>(() =>
            {
                cmds2.Playback(m_Manager);
            });
        }

        [Test]
        public void PlaybackWithExclusiveEntityTransactionInJob_ManagedComponents()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var job = new TestJob {Buffer = cmds};

            var e = cmds.CreateEntity();
            cmds.AddComponent(e, new EcsTestManagedComponent { value = "SomeString" });

            var jobHandle = job.Schedule();

            var manager = m_Manager.BeginExclusiveEntityTransaction();

            var playbackJob = new EntityCommandBufferPlaybackJob()
            {
                Buffer = cmds,
                Manager = manager
            };

            m_Manager.ExclusiveEntityTransactionDependency = playbackJob.Schedule(jobHandle);
            m_Manager.EndExclusiveEntityTransaction();

            var group = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var arr = group.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, arr.Length);
            Assert.AreEqual(1, arr[0].value);
            group.Dispose();

            var managedGroup = m_Manager.CreateEntityQuery(typeof(EcsTestManagedComponent));
            var managedComponentArray = managedGroup.ToComponentDataArray<EcsTestManagedComponent>();
            Assert.AreEqual(1, managedComponentArray.Length);
            Assert.AreEqual("SomeString", managedComponentArray[0].value);
            managedGroup.Dispose();
        }

        [Test]
#if ENABLE_IL2CPP
        [Ignore("DOTS-7524 - \"System.ExecutionEngineException : An unresolved indirect call lookup failed\" is thrown when executed with an IL2CPP build")]
#endif
        public void AddManagedComponent_WithEntityPatch()
        {
            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);

            Entity e0 = cmds.CreateEntity();
            cmds.AddComponent(e0, new EcsTestManagedDataEntity {value1 = e0});

            cmds.Playback(m_Manager);

            using(var group = m_Manager.CreateEntityQuery(typeof(EcsTestManagedDataEntity)))
            {
                var e = group.GetSingletonEntity();
                Assert.AreEqual(e, m_Manager.GetComponentData<EcsTestManagedDataEntity>(e).value1);
            }
        }

        [Test]
        public void SetComponentEnabled_ManagedComponents_ECB()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestManagedComponentEnableable),
                typeof(EcsTestManagedComponentEnableable2));
            Entity e0 = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponentEnabled<EcsTestManagedComponentEnableable2>(e0, false);
            Entity e1 = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponentEnabled(e1, typeof(EcsTestManagedComponentEnableable), false);

            // toggle "enabled" state on both components of both entities
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetComponentEnabled<EcsTestManagedComponentEnableable>(e0, false);
                cmds.SetComponentEnabled<EcsTestManagedComponentEnableable2>(e0, true);
                cmds.SetComponentEnabled(e1, typeof(EcsTestManagedComponentEnableable), true);
                cmds.SetComponentEnabled(e1, typeof(EcsTestManagedComponentEnableable2), false);
                cmds.Playback(m_Manager);
            }

            Assert.IsFalse(m_Manager.IsComponentEnabled<EcsTestManagedComponentEnableable>(e0));
            Assert.IsTrue(m_Manager.IsComponentEnabled<EcsTestManagedComponentEnableable2>(e0));
            Assert.IsTrue(m_Manager.IsComponentEnabled(e1, typeof(EcsTestManagedComponentEnableable)));
            Assert.IsFalse(m_Manager.IsComponentEnabled(e1, typeof(EcsTestManagedComponentEnableable2)));
        }

#endif //!UNITY_DISABLE_MANAGED_COMPONENTS

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void CommandsAfterPlayback_Throws()
        {
            var archetype = m_Manager.CreateArchetype(ComponentType.ReadOnly<EcsTestData>());
            var e = m_Manager.CreateEntity(archetype);
            var query = m_Manager.CreateEntityQuery(ComponentType.ReadOnly<EcsTestData>());

            using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.Playback(m_Manager);

            Assert.Throws<InvalidOperationException>(() => cmds.CreateEntity());
            Assert.Throws<InvalidOperationException>(() => cmds.CreateEntity(archetype));
            Assert.Throws<InvalidOperationException>(() => cmds.Instantiate(e));
            using (var outEntities = CollectionHelper.CreateNativeArray<Entity>(100, World.UpdateAllocator.ToAllocator))
            {
                Assert.Throws<InvalidOperationException>(() => cmds.Instantiate(e,outEntities));
            }
            Assert.Throws<InvalidOperationException>(() => cmds.DestroyEntity(e));
            Assert.Throws<InvalidOperationException>(() => cmds.DestroyEntity(query, EntityQueryCaptureMode.AtRecord));
            Assert.Throws<InvalidOperationException>(() => cmds.DestroyEntity(query, EntityQueryCaptureMode.AtPlayback));
            Assert.Throws<InvalidOperationException>(() => cmds.AddBuffer<EcsIntElement>(e));
            Assert.Throws<InvalidOperationException>(() => cmds.SetBuffer<EcsIntElement>(e));
            Assert.Throws<InvalidOperationException>(() => cmds.AddSharedComponent(e, new EcsTestSharedComp(10)));
            Assert.Throws<InvalidOperationException>(() => cmds.SetSharedComponent(e, new EcsTestSharedComp(10)));
            Assert.Throws<InvalidOperationException>(() => cmds.AddComponent(e, new EcsTestData2(10)));
            Assert.Throws<InvalidOperationException>(() => cmds.AddComponent(e, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData2>())));
            Assert.Throws<InvalidOperationException>(() => cmds.SetComponent(e, new EcsTestData2(10)));
            Assert.Throws<InvalidOperationException>(() => cmds.SetComponent(e, new EcsTestData2(10)));
            Assert.Throws<InvalidOperationException>(() => cmds.AddComponent(query, ComponentType.ReadOnly<EcsTestData2>(), EntityQueryCaptureMode.AtRecord));
            Assert.Throws<InvalidOperationException>(() => cmds.AddComponent(query, ComponentType.ReadOnly<EcsTestData2>(), EntityQueryCaptureMode.AtPlayback));
            Assert.Throws<InvalidOperationException>(() => cmds.AddComponent(e, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData2>())));
            Assert.Throws<InvalidOperationException>(() => cmds.RemoveComponent<EcsTestData>(e));
            Assert.Throws<InvalidOperationException>(() => cmds.RemoveComponent(query, ComponentType.ReadOnly<EcsTestData>(), EntityQueryCaptureMode.AtRecord));
            Assert.Throws<InvalidOperationException>(() => cmds.RemoveComponent(query, ComponentType.ReadOnly<EcsTestData>(), EntityQueryCaptureMode.AtPlayback));
            Assert.Throws<InvalidOperationException>(() => cmds.RemoveComponent(e, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData>())));
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            Assert.Throws<InvalidOperationException>(() => cmds.AddComponent(e, new EcsTestManagedComponent()));
            Assert.Throws<InvalidOperationException>(() => cmds.SetComponent(e, new EcsTestManagedComponent()));
            Assert.Throws<InvalidOperationException>(() => cmds.RemoveComponent<EcsTestManagedComponent>(e));
#endif
            query.Dispose();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct DestroyEntity_EntityArray_Job : IJob
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public NativeArray<Entity> TestEntities;
            public NativeArray<Entity> DeferredEntities;
            public Entity Prefab;
            public void Execute()
            {
                Ecb.DestroyEntity(0, TestEntities);
                Ecb.Instantiate(0, Prefab, DeferredEntities);
                Ecb.DestroyEntity(0, DeferredEntities);
            }
        }

        [Test]
        public void DestroyEntity_TargetIsEntityArray_Works()
        {
            const int TEST_ENTITY_COUNT = 10;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var prefab = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(Prefab));
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            using var testEntities = m_Manager.CreateEntity(archetype, 2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            using var controlEntities = m_Manager.CreateEntity(archetype, 2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            using var deferredEntities = CollectionHelper.CreateNativeArray<Entity>(2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            // Test main thread writer
            ecb.DestroyEntity(testEntities.GetSubArray(0, TEST_ENTITY_COUNT));
            ecb.Instantiate(prefab, deferredEntities.GetSubArray(0, TEST_ENTITY_COUNT));
            ecb.DestroyEntity(deferredEntities.GetSubArray(0, TEST_ENTITY_COUNT));
            // Test ParallelWriter
            new DestroyEntity_EntityArray_Job
            {
                Ecb = ecb.AsParallelWriter(),
                TestEntities = testEntities.GetSubArray(TEST_ENTITY_COUNT, TEST_ENTITY_COUNT),
                DeferredEntities = deferredEntities.GetSubArray(TEST_ENTITY_COUNT, TEST_ENTITY_COUNT),
                Prefab = prefab,
            }.Run();

            ecb.Playback(m_Manager);
            for (int i = 0; i < testEntities.Length; ++i)
                Assert.IsFalse(m_Manager.Exists(testEntities[i]));
            for (int i = 0; i < controlEntities.Length; ++i)
                Assert.IsTrue(m_Manager.Exists(controlEntities[i]));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData3), typeof(EcsTestData2));
            using var finalEntities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(0, finalEntities.Length);
        }

        [BurstCompile(CompileSynchronously = true)]
        struct AddComponent_EntityArray_Job : IJob
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public NativeArray<Entity> TestEntities;
            public NativeArray<Entity> DeferredEntities;
            public Entity Prefab;
            public void Execute()
            {
                Ecb.AddComponent<EcsTestData2>(0, TestEntities);
                Ecb.Instantiate(0, Prefab, DeferredEntities);
                Ecb.AddComponent<EcsTestData2>(0, DeferredEntities);
            }
        }

        // In the EntityDataAccess, we will take a batched approach when the number of entities is >10 for this command
        [Test]
        public void AddComponent_TargetIsEntityArray_Works([Values(10, 15)] int testEntityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var prefab = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(Prefab));
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            using var testEntities = m_Manager.CreateEntity(archetype, 2*testEntityCount, World.UpdateAllocator.ToAllocator);
            using var controlEntities = m_Manager.CreateEntity(archetype, 2*testEntityCount, World.UpdateAllocator.ToAllocator);
            using var deferredEntities = CollectionHelper.CreateNativeArray<Entity>(2*testEntityCount, World.UpdateAllocator.ToAllocator);
            // Test main thread writer
            ecb.AddComponent<EcsTestData2>(testEntities.GetSubArray(0, testEntityCount));
            ecb.Instantiate(prefab, deferredEntities.GetSubArray(0, testEntityCount));
            ecb.AddComponent<EcsTestData2>(deferredEntities.GetSubArray(0, testEntityCount));
            // Test ParallelWriter
            new AddComponent_EntityArray_Job
            {
                Ecb = ecb.AsParallelWriter(),
                TestEntities = testEntities.GetSubArray(testEntityCount, testEntityCount),
                DeferredEntities = deferredEntities.GetSubArray(testEntityCount, testEntityCount),
                Prefab = prefab,
            }.Run();

            ecb.Playback(m_Manager);
            for (int i = 0; i < testEntities.Length; ++i)
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(testEntities[i]));
            for (int i = 0; i < controlEntities.Length; ++i)
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(controlEntities[i]));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData3), typeof(EcsTestData2));
            using var finalEntities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(deferredEntities.Length, finalEntities.Length);
        }

        [BurstCompile(CompileSynchronously = true)]
        struct AddComponents_EntityArray_Job : IJob
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public NativeArray<Entity> TestEntities;
            public NativeArray<Entity> DeferredEntities;
            public Entity Prefab;
            public ComponentTypeSet TypeSetToAdd;
            public void Execute()
            {
                Ecb.AddComponent(0, TestEntities, TypeSetToAdd);
                Ecb.Instantiate(0, Prefab, DeferredEntities);
                Ecb.AddComponent(0, DeferredEntities, TypeSetToAdd);
            }
        }

        // In the EntityDataAccess, we will take a batched approach when the number of entities is >10 for this command
        [Test]
        public void AddComponents_TargetIsEntityArray_Works([Values(10, 15)] int testEntityCount)
        {
            var typesToAdd = new ComponentTypeSet(typeof(EcsTestData2));
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var prefab = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(Prefab));
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            using var testEntities = m_Manager.CreateEntity(archetype, 2*testEntityCount, World.UpdateAllocator.ToAllocator);
            using var controlEntities = m_Manager.CreateEntity(archetype, 2*testEntityCount, World.UpdateAllocator.ToAllocator);
            using var deferredEntities = CollectionHelper.CreateNativeArray<Entity>(2*testEntityCount, World.UpdateAllocator.ToAllocator);
            // Test main thread writer
            ecb.AddComponent(testEntities.GetSubArray(0, testEntityCount), typesToAdd);
            ecb.Instantiate(prefab, deferredEntities.GetSubArray(0, testEntityCount));
            ecb.AddComponent(deferredEntities.GetSubArray(0, testEntityCount), typesToAdd);
            // Test ParallelWriter
            new AddComponents_EntityArray_Job
            {
                Ecb = ecb.AsParallelWriter(),
                TestEntities = testEntities.GetSubArray(testEntityCount, testEntityCount),
                DeferredEntities = deferredEntities.GetSubArray(testEntityCount, testEntityCount),
                Prefab = prefab,
                TypeSetToAdd = typesToAdd,
            }.Run();

            ecb.Playback(m_Manager);
            for (int i = 0; i < testEntities.Length; ++i)
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(testEntities[i]));
            for (int i = 0; i < controlEntities.Length; ++i)
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(controlEntities[i]));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData3), typeof(EcsTestData2));
            using var finalEntities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(deferredEntities.Length, finalEntities.Length);
        }

        [BurstCompile(CompileSynchronously = true)]
        struct AddComponentWithValue_EntityArray_Job : IJob
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public NativeArray<Entity> TestEntities;
            public NativeArray<Entity> DeferredEntities;
            public Entity Prefab;
            public EcsTestData2 Value;
            public void Execute()
            {
                Ecb.AddComponent(0, TestEntities, Value);
                Ecb.Instantiate(0, Prefab, DeferredEntities);
                Ecb.AddComponent(0, DeferredEntities, Value);
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct AddComponentWithValue_EntityArray_WithEntityFixup_Job : IJob
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public NativeArray<Entity> TestEntities;
            public NativeArray<Entity> DeferredEntities;
            public Entity Prefab;
            public int Value0;
            public void Execute()
            {
                var e2 = Ecb.CreateEntity(0);
                Ecb.AddComponent<EcsTestTag>(0, e2);
                var value = new EcsTestDataEntity(Value0, e2);
                Ecb.AddComponent(0, TestEntities, value);
                Ecb.Instantiate(0, Prefab, DeferredEntities);
                Ecb.AddComponent(0, DeferredEntities, value);
            }
        }

        [Test]
        public void AddComponentWithValue_TargetIsEntityArray_Works()
        {
            const int TEST_ENTITY_COUNT = 10;
            var value = new EcsTestData2(17);
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var prefab = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(Prefab));
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            using var testEntities = m_Manager.CreateEntity(archetype, 2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            using var controlEntities = m_Manager.CreateEntity(archetype, 2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            using var deferredEntities = CollectionHelper.CreateNativeArray<Entity>(2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            // Test main thread writer
            ecb.AddComponent(testEntities.GetSubArray(0, TEST_ENTITY_COUNT), value);
            ecb.Instantiate(prefab, deferredEntities.GetSubArray(0, TEST_ENTITY_COUNT));
            ecb.AddComponent(deferredEntities.GetSubArray(0, TEST_ENTITY_COUNT), value);
            // Test ParallelWriter
            new AddComponentWithValue_EntityArray_Job
            {
                Ecb = ecb.AsParallelWriter(),
                TestEntities = testEntities.GetSubArray(TEST_ENTITY_COUNT, TEST_ENTITY_COUNT),
                DeferredEntities = deferredEntities.GetSubArray(TEST_ENTITY_COUNT, TEST_ENTITY_COUNT),
                Prefab = prefab,
                Value = value,
            }.Run();

            ecb.Playback(m_Manager);
            for (int i = 0; i < testEntities.Length; ++i)
                Assert.AreEqual(value, m_Manager.GetComponentData<EcsTestData2>(testEntities[i]));
            for (int i = 0; i < controlEntities.Length; ++i)
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(controlEntities[i]));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData3), typeof(EcsTestData2));
            using var finalEntities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(deferredEntities.Length, finalEntities.Length);
            for (int i = 0; i < finalEntities.Length; ++i)
                Assert.AreEqual(value, m_Manager.GetComponentData<EcsTestData2>(finalEntities[i]));
        }

        [Test]
        public void AddComponentWithValue_TargetIsEntityArray_WithEntityFixup_Works()
        {
            const int TEST_ENTITY_COUNT = 10;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var prefab = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(Prefab));
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var e2 = ecb.CreateEntity();
            ecb.AddComponent<EcsTestTag>(e2);
            var value = new EcsTestDataEntity(42,e2);
            using var testEntities = m_Manager.CreateEntity(archetype, 2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            using var controlEntities = m_Manager.CreateEntity(archetype, 2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            using var deferredEntities = CollectionHelper.CreateNativeArray<Entity>(2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            // Test main thread writer
            ecb.AddComponent(testEntities.GetSubArray(0, TEST_ENTITY_COUNT), value);
            ecb.Instantiate(prefab, deferredEntities.GetSubArray(0, TEST_ENTITY_COUNT));
            ecb.AddComponent(deferredEntities.GetSubArray(0, TEST_ENTITY_COUNT), value);
            // Test ParallelWriter
            new AddComponentWithValue_EntityArray_WithEntityFixup_Job
            {
                Ecb = ecb.AsParallelWriter(),
                TestEntities = testEntities.GetSubArray(TEST_ENTITY_COUNT, TEST_ENTITY_COUNT),
                DeferredEntities = deferredEntities.GetSubArray(TEST_ENTITY_COUNT, TEST_ENTITY_COUNT),
                Prefab = prefab,
                Value0 = 42,
            }.Run();

            ecb.Playback(m_Manager);
            for (int i = 0; i < testEntities.Length; ++i)
            {
                var actualValue = m_Manager.GetComponentData<EcsTestDataEntity>(testEntities[i]);
                Assert.AreEqual(value.value0, actualValue.value0);
                Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(actualValue.value1));
            }

            for (int i = 0; i < controlEntities.Length; ++i)
                Assert.IsFalse(m_Manager.HasComponent<EcsTestDataEntity>(controlEntities[i]));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData3), typeof(EcsTestDataEntity));
            using var finalEntities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(deferredEntities.Length, finalEntities.Length);
            for (int i = 0; i < finalEntities.Length; ++i)
            {
                var actualValue = m_Manager.GetComponentData<EcsTestDataEntity>(finalEntities[i]);
                Assert.AreEqual(42, actualValue.value0);
                Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(actualValue.value1));
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct RemoveComponent_EntityArray_Job : IJob
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public NativeArray<Entity> TestEntities;
            public NativeArray<Entity> DeferredEntities;
            public Entity Prefab;
            public void Execute()
            {
                Ecb.RemoveComponent<EcsTestData2>(0, TestEntities);
                Ecb.Instantiate(0, Prefab, DeferredEntities);
                Ecb.RemoveComponent<EcsTestData2>(0, DeferredEntities);
            }
        }

        // In the EntityDataAccess, we will take a batched approach when the number of entities is >10 for this command
        [Test]
        public void RemoveComponent_TargetIsEntityArray_Works([Values(10, 15)] int testEntityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var prefab = m_Manager.CreateEntity(typeof(EcsTestData2), typeof(EcsTestData3), typeof(Prefab));
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            using var testEntities = m_Manager.CreateEntity(archetype, 2*testEntityCount, World.UpdateAllocator.ToAllocator);
            using var controlEntities = m_Manager.CreateEntity(archetype, 2*testEntityCount, World.UpdateAllocator.ToAllocator);
            using var deferredEntities = CollectionHelper.CreateNativeArray<Entity>(2*testEntityCount, World.UpdateAllocator.ToAllocator);
            // Test main thread writer
            ecb.RemoveComponent<EcsTestData2>(testEntities.GetSubArray(0, testEntityCount));
            ecb.Instantiate(prefab, deferredEntities.GetSubArray(0, testEntityCount));
            ecb.RemoveComponent<EcsTestData2>(deferredEntities.GetSubArray(0, testEntityCount));
            // Test ParallelWriter
            new RemoveComponent_EntityArray_Job
            {
                Ecb = ecb.AsParallelWriter(),
                TestEntities = testEntities.GetSubArray(testEntityCount, testEntityCount),
                DeferredEntities = deferredEntities.GetSubArray(testEntityCount, testEntityCount),
                Prefab = prefab,
            }.Run();

            ecb.Playback(m_Manager);
            for (int i = 0; i < testEntities.Length; ++i)
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(testEntities[i]));
            for (int i = 0; i < controlEntities.Length; ++i)
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(controlEntities[i]));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData3), ComponentType.Exclude<EcsTestData2>());
            using var finalEntities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(deferredEntities.Length, finalEntities.Length);
        }


        [BurstCompile(CompileSynchronously = true)]
        struct RemoveComponents_EntityArray_Job : IJob
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public NativeArray<Entity> TestEntities;
            public NativeArray<Entity> DeferredEntities;
            public Entity Prefab;
            public ComponentTypeSet TypeSetToRemove;
            public void Execute()
            {
                Ecb.RemoveComponent(0, TestEntities, TypeSetToRemove);
                Ecb.Instantiate(0, Prefab, DeferredEntities);
                Ecb.RemoveComponent(0, DeferredEntities, TypeSetToRemove);
            }
        }

        // In the EntityDataAccess, we will take a batched approach when the number of entities is >10 for this command
        [Test]
        public void RemoveComponents_TargetIsEntityArray_Works([Values(10, 15)] int testEntityCount)
        {
            var typesToRemove = new ComponentTypeSet(typeof(EcsTestData2));
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var prefab = m_Manager.CreateEntity(typeof(EcsTestData2), typeof(EcsTestData3), typeof(Prefab));
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            using var testEntities = m_Manager.CreateEntity(archetype, 2*testEntityCount, World.UpdateAllocator.ToAllocator);
            using var controlEntities = m_Manager.CreateEntity(archetype, 2*testEntityCount, World.UpdateAllocator.ToAllocator);
            using var deferredEntities = CollectionHelper.CreateNativeArray<Entity>(2*testEntityCount, World.UpdateAllocator.ToAllocator);
            // Test main thread writer
            ecb.RemoveComponent(testEntities.GetSubArray(0, testEntityCount), typesToRemove);
            ecb.Instantiate(prefab, deferredEntities.GetSubArray(0, testEntityCount));
            ecb.RemoveComponent(deferredEntities.GetSubArray(0, testEntityCount), typesToRemove);
            // Test ParallelWriter
            new RemoveComponents_EntityArray_Job
            {
                Ecb = ecb.AsParallelWriter(),
                TestEntities = testEntities.GetSubArray(testEntityCount, testEntityCount),
                DeferredEntities = deferredEntities.GetSubArray(testEntityCount, testEntityCount),
                Prefab = prefab,
                TypeSetToRemove = typesToRemove,
            }.Run();

            ecb.Playback(m_Manager);
            for (int i = 0; i < testEntities.Length; ++i)
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(testEntities[i]));
            for (int i = 0; i < controlEntities.Length; ++i)
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(controlEntities[i]));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData3), ComponentType.Exclude<EcsTestData2>());
            using var finalEntities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(deferredEntities.Length, finalEntities.Length);
        }

        //[BurstCompile(CompileSynchronously = true)]
        struct AddSharedComponentWithValue_EntityArray_Job : IJob
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public NativeArray<Entity> TestEntities;
            public NativeArray<Entity> DeferredEntities;
            public Entity Prefab;
            public EcsTestSharedComp Value;
            public void Execute()
            {
                Ecb.AddSharedComponent(0, TestEntities, Value);
                Ecb.Instantiate(0, Prefab, DeferredEntities);
                Ecb.AddSharedComponent(0, DeferredEntities, Value);
            }
        }

        [Test]
        public void AddSharedComponentWithValue_TargetIsEntityArray_Works()
        {
            const int TEST_ENTITY_COUNT = 10;
            var value = new EcsTestSharedComp(17);
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var prefab = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(Prefab));
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            using var testEntities = m_Manager.CreateEntity(archetype, 2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            using var controlEntities = m_Manager.CreateEntity(archetype, 2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            using var deferredEntities = CollectionHelper.CreateNativeArray<Entity>(2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            // Test main thread writer
            ecb.AddSharedComponent(testEntities.GetSubArray(0, TEST_ENTITY_COUNT), value);
            ecb.Instantiate(prefab, deferredEntities.GetSubArray(0, TEST_ENTITY_COUNT));
            ecb.AddSharedComponent(deferredEntities.GetSubArray(0, TEST_ENTITY_COUNT), value);
            // Test ParallelWriter
            new AddSharedComponentWithValue_EntityArray_Job
            {
                Ecb = ecb.AsParallelWriter(),
                TestEntities = testEntities.GetSubArray(TEST_ENTITY_COUNT, TEST_ENTITY_COUNT),
                DeferredEntities = deferredEntities.GetSubArray(TEST_ENTITY_COUNT, TEST_ENTITY_COUNT),
                Prefab = prefab,
                Value = value,
            }.Run();

            ecb.Playback(m_Manager);
            for (int i = 0; i < testEntities.Length; ++i)
                Assert.AreEqual(value.value, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(testEntities[i]).value);
            for (int i = 0; i < controlEntities.Length; ++i)
                Assert.IsFalse(m_Manager.HasComponent<EcsTestSharedComp>(controlEntities[i]));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData3), typeof(EcsTestSharedComp));
            using var finalEntities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(deferredEntities.Length, finalEntities.Length);
            for (int i = 0; i < finalEntities.Length; ++i)
                Assert.AreEqual(value, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(finalEntities[i]));
        }

        //[BurstCompile(CompileSynchronously = true)]
        struct SetSharedComponentWithValue_EntityArray_Job : IJob
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public NativeArray<Entity> TestEntities;
            public NativeArray<Entity> DeferredEntities;
            public Entity Prefab;
            public EcsTestSharedComp Value;
            public void Execute()
            {
                Ecb.SetSharedComponent(0, TestEntities, Value);
                Ecb.Instantiate(0, Prefab, DeferredEntities);
                Ecb.SetSharedComponent(0, DeferredEntities, Value);
            }
        }

        [Test]
        public void SetSharedComponent_DefaultValue_IsNotDuplicated()
        {
            const int TEST_ENTITY_COUNT = 10;
            var value = new EcsTestSharedComp(17);

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            using var testEntities = m_Manager.CreateEntity(archetype, 3 * TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            ecb.SetSharedComponent(testEntities.GetSubArray(TEST_ENTITY_COUNT, TEST_ENTITY_COUNT), value);
            ecb.SetSharedComponent(testEntities.GetSubArray(2 * TEST_ENTITY_COUNT, TEST_ENTITY_COUNT), default(EcsTestSharedComp));
            ecb.Playback(m_Manager);

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp));

            query.SetSharedComponentFilter(value);
            Assert.AreEqual(TEST_ENTITY_COUNT, query.CalculateEntityCount());

            query.SetSharedComponentFilter(default(EcsTestSharedComp));
            Assert.AreEqual(2 * TEST_ENTITY_COUNT, query.CalculateEntityCount());
        }

        [Test]
        public void SetSharedComponent_TargetIsEntityArray_Works()
        {
            const int TEST_ENTITY_COUNT = 10;
            var value = new EcsTestSharedComp(17);
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var prefab = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestSharedComp), typeof(Prefab));
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            using var testEntities = m_Manager.CreateEntity(archetype, 2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            using var controlEntities = m_Manager.CreateEntity(archetype, 2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            using var deferredEntities = CollectionHelper.CreateNativeArray<Entity>(2*TEST_ENTITY_COUNT, World.UpdateAllocator.ToAllocator);
            // Test main thread writer
            ecb.SetSharedComponent(testEntities.GetSubArray(0, TEST_ENTITY_COUNT), value);
            ecb.Instantiate(prefab, deferredEntities.GetSubArray(0, TEST_ENTITY_COUNT));
            ecb.SetSharedComponent(deferredEntities.GetSubArray(0, TEST_ENTITY_COUNT), value);
            // Test ParallelWriter
            new SetSharedComponentWithValue_EntityArray_Job
            {
                Ecb = ecb.AsParallelWriter(),
                TestEntities = testEntities.GetSubArray(TEST_ENTITY_COUNT, TEST_ENTITY_COUNT),
                DeferredEntities = deferredEntities.GetSubArray(TEST_ENTITY_COUNT, TEST_ENTITY_COUNT),
                Prefab = prefab,
                Value = value,
            }.Run();

            ecb.Playback(m_Manager);
            for (int i = 0; i < testEntities.Length; ++i)
                Assert.AreEqual(value, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(testEntities[i]));
            for (int i = 0; i < controlEntities.Length; ++i)
                Assert.AreNotEqual(value, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(controlEntities[i]));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData3), typeof(EcsTestSharedComp));
            using var finalEntities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(deferredEntities.Length, finalEntities.Length);
            for (int i = 0; i < finalEntities.Length; ++i)
                Assert.AreEqual(value, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(finalEntities[i]));
        }

        unsafe struct SetSharedComponentWithNonDefaultValue_Job : IJob
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public Entity TestEntity;
            public Entity Prefab;
            public EcsTestSharedComp Value;
            [NativeDisableUnsafePtrRestriction]
            public void* valuePtr;
            public TypeIndex typeIndex;
            public void Execute()
            {
                Ecb.UnsafeSetSharedComponentNonDefault(0, TestEntity, valuePtr, typeIndex);
                var deferredEntity = Ecb.Instantiate(0, Prefab);
                Ecb.UnsafeSetSharedComponentNonDefault(0, deferredEntity, valuePtr, typeIndex);
            }
        }

        [Test]
        public unsafe void SetSharedComponentNonDefault_TargetIsEntity_Works()
        {
            var value = new EcsTestSharedComp(17);
            void* ptr = &value;

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var prefab = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestSharedComp), typeof(Prefab));

            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var testEntity = m_Manager.CreateEntity(archetype);
            var controlEntity = m_Manager.CreateEntity(archetype);

            // Test ParallelWriter
            new SetSharedComponentWithNonDefaultValue_Job()
            {
                Ecb = ecb.AsParallelWriter(),
                TestEntity = testEntity,
                Prefab = prefab,
                valuePtr = ptr,
                typeIndex = TypeManager.GetTypeIndex<EcsTestSharedComp>()
            }.Run();

            ecb.Playback(m_Manager);
            Assert.AreEqual(value, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(testEntity));
            Assert.AreNotEqual(value, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(controlEntity));

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData3), typeof(EcsTestSharedComp));
            using var finalEntities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, finalEntities.Length);

            for (int i = 0; i < finalEntities.Length; ++i)
                Assert.AreEqual(value, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(finalEntities[i]));
        }

        [BurstCompile]
        internal partial struct TestECBSystem : ISystem
        {
            private EntityCommandBuffer ecb;

            [BurstCompile]
            public void OnCreate(ref SystemState state)
            {
                ecb = new EntityCommandBuffer(Allocator.Temp);
            }

            [BurstCompile]
            public void OnDestroy(ref SystemState state)
            {
                ecb.Dispose();
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                var e = ecb.CreateEntity();
                ecb.AddComponent(e, new EcsTestData(42));
                ecb.Playback(state.EntityManager);
            }
        }

        [Test]
        public void ECBWithinBurstedSystem_Works()
        {
            var burstedSystem = World.GetOrCreateSystem<TestECBSystem>();
            burstedSystem.Update(World.Unmanaged);

            var entities = m_Manager.GetAllEntities();
            Assert.AreEqual(1, entities.Length);
            Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(entities[0]).value);
        }

        struct EntityPrefabData : IComponentData
        {
            public Entity prefab;
        }

        [BurstCompile]
        internal partial struct ECBWithPrefabSystem : ISystem
        {
            private EntityCommandBuffer ecb;

            [BurstCompile]
            public void OnCreate(ref SystemState state)
            {
                ecb = new EntityCommandBuffer(Allocator.Temp);
                state.EntityManager.AddComponent<EntityPrefabData>(state.SystemHandle);
            }

            [BurstCompile]
            public void OnDestroy(ref SystemState state)
            {
                ecb.Dispose();
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                var PrefabData = state.EntityManager.GetComponentData<EntityPrefabData>(state.SystemHandle);
                var e = ecb.Instantiate(PrefabData.prefab);
                ecb.Playback(state.EntityManager);
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void ECBWithinBurstedSystem_InstantiatePrefabWithManaged_Works()
        {
            var e = m_Manager.CreateEntity(typeof(Prefab), typeof(EcsTestManagedComponent));
            var prefabObject = new EcsTestManagedComponent { value = "Test" };
            m_Manager.SetComponentObject(e, typeof(EcsTestManagedComponent), prefabObject);

            UpdateSystemForBurstedSystem(e);

            var entities = m_Manager.GetAllEntities();
            Assert.AreEqual(2, entities.Length);

            var instances = m_Manager.CreateEntityQuery(typeof(EcsTestManagedComponent)).ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, instances.Length);
            Assert.That(m_Manager.HasComponent<EcsTestManagedComponent>(instances[0]));

            var entityObject = m_Manager.GetComponentObject<EcsTestManagedComponent>(instances[0]);
            Assert.AreEqual("Test", entityObject.value);
            Assert.IsFalse(object.ReferenceEquals(prefabObject, entityObject));
        }

        public void UpdateSystemForBurstedSystem(Entity e)
        {
            var burstedSystem = World.GetOrCreateSystem<ECBWithPrefabSystem>();
            ref var PrefabData = ref World.EntityManager.GetComponentDataRW<EntityPrefabData>(burstedSystem).ValueRW;
            PrefabData.prefab = e;
            burstedSystem.Update(World.Unmanaged);
        }
#endif

        public FixedString128Bytes GetSystemDebugName(in SystemHandle systemHandle)
        {
            unsafe
            {
                var systemState = m_Manager.GetCheckedEntityDataAccess()->m_WorldUnmanaged.ResolveSystemState(systemHandle);
                EntityCommandBuffer.PlaybackWithTraceProcessor.ExtractDebugName(systemState, out var name);
                return name;
            }
        }

        internal partial class TestPlaybackWithTraceSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                unsafe
                {
                    var cmds = World.GetOrCreateSystemManaged<TestPlaybackWithTraceECBPlaybackSystem>().CreateCommandBuffer();
                    cmds.CreateEntity();
                }
            }
        }

        internal partial class TestPlaybackWithTraceECBPlaybackSystem : EntityCommandBufferSystem {}

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public unsafe void PlaybackWithTrace_ValidSystem()
        {
            using (var world = new World("World A"))
            {
                EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

                var ecbRecordingSystem = world.GetOrCreateSystemManaged<TestPlaybackWithTraceSystem>();
                var ecbPlaybackSystem = world.GetOrCreateSystemManaged<TestPlaybackWithTraceECBPlaybackSystem>();

                var recordingSystemName = world.Unmanaged.ResolveSystemState(ecbRecordingSystem.SystemHandle)->DebugName;
                var playbackSystemName = world.Unmanaged.ResolveSystemState(ecbPlaybackSystem.SystemHandle)->DebugName;

                ecbRecordingSystem.Update();
                ecbPlaybackSystem.Update();

                LogAssert.Expect(LogType.Log, $"Starting EntityCommandBuffer playback in {playbackSystemName}; recorded from {recordingSystemName}.");
                LogAssert.Expect(LogType.Log, $"Ending EntityCommandBuffer playback in {playbackSystemName}; recorded from {recordingSystemName}.");

                EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_CreateEntity()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.CreateEntity();
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log, $"Creating 1 entity; recorded from {originSystemDebugName}.");
            }

            using (var entities = m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(1, entities.Length);
            }

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_InstantiateEntity()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var prefab = m_Manager.CreateEntity();
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(prefab, entityName);
#endif

            using(var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entities = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator))
            {
                cmds.Instantiate(prefab, entities);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Instantiating {entities.Length} instance(s) of entity {entityName}({prefab.Index},{prefab.Version}); recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual(11, m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator).Length);

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_DestroyEntity()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity();
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            Assert.AreEqual(1, m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator).Length);

            var index = entity.Index;
            var version = entity.Version;

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.DestroyEntity(entity);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log, $"Destroying entity {entityName}({index},{version}); recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual(0, m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator).Length);

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddUnmanagedSharedComponent()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity();
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddSharedComponent(entity, new EcsTestSharedComp());
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Adding unmanaged shared component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestSharedComp>()}; recorded from {originSystemDebugName}.");
            }

            Assert.IsTrue(m_Manager.HasComponent<EcsTestSharedComp>(entity));

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetUnmanagedSharedComponentData()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsStringSharedComponent));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            Assert.AreEqual(1, m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator).Length);

            var index = entity.Index;
            var version = entity.Version;

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetSharedComponent(entity, new EcsTestSharedComp(42));
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Setting unmanaged shared component on entity {entityName}({index},{version}) for component index {TypeManager.GetTypeIndex<EcsTestSharedComp>()}; recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual(42, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity).value);

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddSharedComponent()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity();
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddSharedComponentManaged(entity, new EcsStringSharedComponent {Value = ""});
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Adding shared component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsStringSharedComponent>()}; recorded from {originSystemDebugName}.");
            }

            Assert.IsTrue(m_Manager.HasComponent<EcsStringSharedComponent>(entity));

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetSharedComponentData()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity(typeof(EcsStringSharedComponent));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            Assert.AreEqual(1, m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator).Length);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetSharedComponentManaged(entity, new EcsStringSharedComponent {Value = "Test"});
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Setting shared component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsStringSharedComponent>()}; recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual("Test", m_Manager.GetSharedComponentManaged<EcsStringSharedComponent>(entity).Value);

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddComponent()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity();
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddComponent<EcsTestData>(entity);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Adding component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestData>()}; recorded from {originSystemDebugName}.");
            }

            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetComponent()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetComponent(entity, new EcsTestData(10));
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Setting component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestData>()}; recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual(10, m_Manager.GetComponentData<EcsTestData>(entity).value);

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_RemoveComponent()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity(typeof(EcsTestTag));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.RemoveComponent<EcsTestTag>(entity);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Removing component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestTag>()}; recorded from {originSystemDebugName}.");
            }

            Assert.IsFalse(m_Manager.HasComponent<EcsTestTag>(entity));

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddBufferComponent()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity();
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddBuffer<EcsIntElement>(entity);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Adding dynamic buffer component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsIntElement>()}; recorded from {originSystemDebugName}.");
            }

            Assert.IsTrue(m_Manager.HasComponent<EcsIntElement>(entity));

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetBufferComponent()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var buffer = cmds.SetBuffer<EcsIntElement>(entity);
                buffer.Add(new EcsIntElement {Value = 10});
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Setting dynamic buffer component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsIntElement>()}; recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual(10, m_Manager.GetBuffer<EcsIntElement>(entity)[0].Value);

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AppendToBuffer()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AppendToBuffer(entity, new EcsIntElement {Value = 10});
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Appending element to dynamic buffer component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsIntElement>()}; recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual(10, m_Manager.GetBuffer<EcsIntElement>(entity)[0].Value);

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddComponentWithEntityFixup()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity();
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddComponent<EcsTestDataEntity>(entity);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Adding component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestDataEntity>()}; recorded from {originSystemDebugName}.");
            }

            Assert.IsTrue(m_Manager.HasComponent<EcsTestDataEntity>(entity));

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetComponentWithEntityFixup()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity(typeof(EcsTestDataEntity));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var e = cmds.CreateEntity();
                cmds.SetComponent(entity, new EcsTestDataEntity{value0 = 10, value1 = e});
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Setting component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestDataEntity>()}; recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual(10, m_Manager.GetComponentData<EcsTestDataEntity>(entity).value0);

#if ENTITY_STORE_V1
            Assert.AreEqual(1, m_Manager.GetComponentData<EcsTestDataEntity>(entity).value1.Index);
            Assert.AreEqual(1, m_Manager.GetComponentData<EcsTestDataEntity>(entity).value1.Version);
#endif

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddBufferComponentWithEntityFixup()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity();
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddBuffer<EcsComplexEntityRefElement>(entity);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Adding dynamic buffer component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsComplexEntityRefElement>()}; recorded from {originSystemDebugName}.");
            }

            Assert.IsTrue(m_Manager.HasComponent<EcsComplexEntityRefElement>(entity));

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetBufferComponentWithEntityFixup()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity(typeof(EcsComplexEntityRefElement));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var e = cmds.CreateEntity();
                var buffer = cmds.SetBuffer<EcsComplexEntityRefElement>(entity);
                buffer.Add(new EcsComplexEntityRefElement {Dummy = 10, Entity = e});
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Setting dynamic buffer component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsComplexEntityRefElement>()}; recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual(10, m_Manager.GetBuffer<EcsComplexEntityRefElement>(entity)[0].Dummy);

#if ENTITY_STORE_V1
            Assert.AreEqual(1, m_Manager.GetBuffer<EcsComplexEntityRefElement>(entity)[0].Entity.Index);
            Assert.AreEqual(1, m_Manager.GetBuffer<EcsComplexEntityRefElement>(entity)[0].Entity.Version);
#endif

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AppendToBufferWithEntityFixup()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity(typeof(EcsComplexEntityRefElement));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var e = cmds.CreateEntity();
                cmds.AppendToBuffer(entity, new EcsComplexEntityRefElement {Dummy = 10, Entity = e});
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Appending element to dynamic buffer component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsComplexEntityRefElement>()}; recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual(10, m_Manager.GetBuffer<EcsComplexEntityRefElement>(entity)[0].Dummy);

#if ENTITY_STORE_V1
            Assert.AreEqual(1, m_Manager.GetBuffer<EcsComplexEntityRefElement>(entity)[0].Entity.Index);
            Assert.AreEqual(1, m_Manager.GetBuffer<EcsComplexEntityRefElement>(entity)[0].Entity.Version);
#endif

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetEnabled()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            var enabled = false;

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetEnabled(entity, enabled);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                var enabledString = enabled ? "ENABLED" : "DISABLED";
                LogAssert.Expect(LogType.Log,
                    $"Setting entity {entityName}({entity.Index},{entity.Version}) to {enabledString}; recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual(enabled, m_Manager.IsEnabled(entity));

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetComponentEnabled()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            var enabled = false;

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetComponentEnabled<EcsTestDataEnableable>(entity, enabled);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                var enabledString = enabled ? "TRUE" : "FALSE";
                LogAssert.Expect(LogType.Log,
                    $"Setting component enableable on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestDataEnableable>()} to {enabledString}; recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual(enabled, m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entity));

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

#if !DOTS_DISABLE_DEBUG_NAMES
        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetName()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity();
            FixedString64Bytes entityName = "";
            m_Manager.GetName(entity, out var name);

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetName(entity, entityName);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Setting name on entity {name}({entity.Index},{entity.Version}) with name {entityName}; recorded from {originSystemDebugName}.");
            }

            m_Manager.GetName(entity, out entityName);
            Assert.AreEqual(entityName, entityName);

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }
#endif

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_DestroyMultipleEntities()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var archetype = m_Manager.CreateArchetype();
            var testEntities = m_Manager.CreateEntity(archetype, 5, World.UpdateAllocator.ToAllocator);
            var testEntitiesNames = CollectionHelper.CreateNativeArray<FixedString64Bytes>(5, World.UpdateAllocator.ToAllocator);

            FixedString64Bytes name = "";

            for (int i = 0; i < testEntities.Length; i++)
            {
#if !DOTS_DISABLE_DEBUG_NAMES
                name = $"TestEntity{i}";
#endif
                m_Manager.SetName(testEntities[i], name);
                testEntitiesNames[i] = name;
            }

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.DestroyEntity(testEntities);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);

                for (int i = 0; i < testEntities.Length; i++)
                {
                    var entity = testEntities[i];
                    LogAssert.Expect(LogType.Log, $"Destroying entity {testEntitiesNames[i]}({entity.Index},{entity.Version}); recorded from {originSystemDebugName}.");
                }
            }

            Assert.AreEqual(0, m_Manager.GetAllEntities(World.UpdateAllocator.ToAllocator).Length);

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddComponentForMultipleEntities()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));

            using (var testEntities = m_Manager.CreateEntity(archetype, 5, World.UpdateAllocator.ToAllocator))
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddComponent<EcsTestData2>(testEntities);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);

                for (int i = 0; i < testEntities.Length; i++)
                {
                    var entity = testEntities[i];
                    m_Manager.GetName(entity, out var entityName);
                    LogAssert.Expect(LogType.Log,
                        $"Adding component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestData2>()}; recorded from {originSystemDebugName}.");

                    Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
                }
            }

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddUnmanagedSharedComponentForMultipleEntities()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var archetype = m_Manager.CreateArchetype();

            using (var testEntities = m_Manager.CreateEntity(archetype, 5, World.UpdateAllocator.ToAllocator))
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddSharedComponent(testEntities, new EcsTestSharedComp(10));
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);

                for (int i = 0; i < testEntities.Length; i++)
                {
                    var entity = testEntities[i];
                    m_Manager.GetName(entity, out var entityName);
                    LogAssert.Expect(LogType.Log,
                        $"Adding unmanaged shared component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestSharedComp>()}; recorded from {originSystemDebugName}.");

                    Assert.IsTrue(m_Manager.HasComponent<EcsTestSharedComp>(entity));
                }
            }

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetUnmanagedSharedComponentForMultipleEntities()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp));

            using(var testEntities = m_Manager.CreateEntity(archetype, 5, World.UpdateAllocator.ToAllocator))
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetSharedComponent(testEntities, new EcsTestSharedComp(10));
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);

                for (int i = 0; i < testEntities.Length; i++)
                {
                    var entity = testEntities[i];
                    m_Manager.GetName(entity, out var entityName);
                    LogAssert.Expect(LogType.Log,
                        $"Setting unmanaged shared component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestSharedComp>()}; recorded from {originSystemDebugName}.");

                    Assert.AreEqual(10, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity).value);
                }
            }

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddSharedComponentForMultipleEntities()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var archetype = m_Manager.CreateArchetype();

            using (var testEntities = m_Manager.CreateEntity(archetype, 5, World.UpdateAllocator.ToAllocator))
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddSharedComponentManaged(testEntities, new EcsStringSharedComponent {Value = "Test"});
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);

                for (int i = 0; i < testEntities.Length; i++)
                {
                    var entity = testEntities[i];
                    m_Manager.GetName(entity, out var entityName);
                    LogAssert.Expect(LogType.Log,
                        $"Adding shared component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsStringSharedComponent>()}; recorded from {originSystemDebugName}.");

                    Assert.IsTrue(m_Manager.HasComponent<EcsStringSharedComponent>(entity));
                }
            }

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetSharedComponentForMultipleEntities()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var archetype = m_Manager.CreateArchetype(typeof(EcsStringSharedComponent));

            using (var testEntities = m_Manager.CreateEntity(archetype, 5, World.UpdateAllocator.ToAllocator))
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetSharedComponentManaged(testEntities, new EcsStringSharedComponent {Value = "Test"});
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);

                for (int i = 0; i < testEntities.Length; i++)
                {
                    var entity = testEntities[i];
                    m_Manager.GetName(entity, out var entityName);
                    LogAssert.Expect(LogType.Log,
                        $"Setting shared component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsStringSharedComponent>()}; recorded from {originSystemDebugName}.");

                    Assert.AreEqual("Test",
                        m_Manager.GetSharedComponentManaged<EcsStringSharedComponent>(entity).Value);
                }
            }

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_RemoveComponentForMultipleEntities()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));

            using (var testEntities = m_Manager.CreateEntity(archetype, 5, World.UpdateAllocator.ToAllocator))
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.RemoveComponent<EcsTestData>(testEntities);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);

                for (int i = 0; i < testEntities.Length; i++)
                {
                    var entity = testEntities[i];
                    m_Manager.GetName(entity, out var entityName);
                    LogAssert.Expect(LogType.Log,
                        $"Removing component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestData>()}; recorded from {originSystemDebugName}.");

                    Assert.IsFalse(m_Manager.HasComponent<EcsTestData>(entity));
                }
            }

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddMultipleComponentsForMultipleEntities()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var archetype = m_Manager.CreateArchetype();
            var types = new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2));

            using (var testEntities = m_Manager.CreateEntity(archetype, 5, World.UpdateAllocator.ToAllocator))
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddComponent(testEntities, types);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);

                for (int i = 0; i < testEntities.Length; i++)
                {
                    for (int j = 0; j < types.Length; j++)
                    {
                        var typeIndex = types.GetComponentType(j).TypeIndex;
                        var entity = testEntities[i];
                        m_Manager.GetName(entity, out var entityName);
                        LogAssert.Expect(LogType.Log,
                            $"Adding component on entity {entityName}({entity.Index},{entity.Version}) for component index {typeIndex}; recorded from {originSystemDebugName}.");

                        Assert.IsTrue(m_Manager.HasComponent(entity, types.GetComponentType(j)));
                    }
                }
            }

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_RemoveMultipleComponentsFromMultipleEntities()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var types = new ComponentTypeSet(typeof(EcsTestData), typeof(EcsTestData2));

            using (var testEntities = m_Manager.CreateEntity(archetype, 5, World.UpdateAllocator.ToAllocator))
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.RemoveComponent(testEntities, types);
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);

                for (int i = 0; i < testEntities.Length; i++)
                {
                    for (int j = 0; j < types.Length; j++)
                    {
                        var typeIndex = types.GetComponentType(j).TypeIndex;
                        var entity = testEntities[i];
                        m_Manager.GetName(entity, out var entityName);
                        LogAssert.Expect(LogType.Log,
                            $"Removing component on entity {entityName}({entity.Index},{entity.Version}) for component index {typeIndex}; recorded from {originSystemDebugName}.");

                        Assert.IsFalse(m_Manager.HasComponent(entity, types.GetComponentType(j)));
                    }
                }
            }

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddComponentForLinkedEntityGroup_Works()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(rootEntity, entityName);
#endif

            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity(typeof(Prefab));

                if (i % 2 == 0)
                {
                    m_Manager.AddComponent<EcsTestData>(child);
                }

                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);
            var mask = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>()).GetEntityQueryMask();

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddComponentForLinkedEntityGroup(instance, mask, new EcsTestData2(42));
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                cmds.Playback(m_Manager);

                var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>());
                using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(5, results.Length);
                    for (int i = 0; i < results.Length; i++)
                    {
                        LogAssert.Expect(LogType.Log,
                            $"Adding component to {entityName}({instance.Index},{instance.Version})'s linked entity ({results[i].Index},{results[i].Version}) for component index {TypeManager.GetTypeIndex<EcsTestData2>()}; recorded from {originSystemDebugName}.");
                        Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData2>(results[i]).value0);
                    }
                }
            }

            array.Dispose();
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetComponentForLinkedEntityGroup_Works()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(rootEntity, entityName);
#endif

            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity(typeof(Prefab));

                if (i % 2 == 0)
                {
                    m_Manager.AddComponent<EcsTestData>(child);
                }

                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);
            var mask = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>()).GetEntityQueryMask();

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetComponentForLinkedEntityGroup(instance, mask, new EcsTestData(42));
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                cmds.Playback(m_Manager);

                var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());
                using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(5, results.Length);
                    for (int i = 0; i < results.Length; i++)
                    {
                        LogAssert.Expect(LogType.Log,
                            $"Setting component to {entityName}({instance.Index},{instance.Version})'s linked entity ({results[i].Index},{results[i].Version}) for component index {TypeManager.GetTypeIndex<EcsTestData>()}; recorded from {originSystemDebugName}.");
                        Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(results[i]).value);
                    }
                }
            }

            array.Dispose();
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_ReplaceComponentForLinkedEntityGroup_Works()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var rootEntity = m_Manager.CreateEntity(typeof(Prefab), typeof(LinkedEntityGroup));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(rootEntity, entityName);
#endif

            var array = CollectionHelper.CreateNativeArray<Entity>(10, World.UpdateAllocator.ToAllocator);

            for (var i = 0; i < 10; i++)
            {
                var child = m_Manager.CreateEntity(typeof(Prefab));

                if (i % 2 == 0)
                {
                    m_Manager.AddComponent<EcsTestData>(child);
                }

                array[i] = child;
            }

            var linkedBuffer = m_Manager.AddBuffer<LinkedEntityGroup>(rootEntity);
            linkedBuffer.Add(rootEntity);
            for (var i = 0; i < 10; i++)
            {
                linkedBuffer.Add(new LinkedEntityGroup {Value = array[i]});
            }

            var instance = m_Manager.Instantiate(rootEntity);

            using (EntityCommandBuffer cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.ReplaceComponentForLinkedEntityGroup(instance, new EcsTestData(42));
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                cmds.Playback(m_Manager);

                var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());
                using (var results = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(5, results.Length);
                    for (int i = 0; i < results.Length; i++)
                    {
                        LogAssert.Expect(LogType.Log,
                            $"Replacing component to {entityName}({instance.Index},{instance.Version})'s linked entity ({results[i].Index},{results[i].Version}) for component index {TypeManager.GetTypeIndex<EcsTestData>()}; recorded from {originSystemDebugName}.");
                        Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(results[i]).value);
                    }
                }
            }

            array.Dispose();
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [BurstCompile(CompileSynchronously = true)]
        static void DisposeEcb(ref EntityCommandBuffer ecb)
        {
            ecb.Dispose();
        }

        [Test]
        public void AddManagedComponent_DisposeFromBurst_CleanedUp()
        {
            var entity = m_Manager.CreateEntity();
            var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            cmds.AddComponent(entity, new EcsTestManagedComponent());
            cmds.Playback(m_Manager);

            Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent>(entity));

            Assert.DoesNotThrow(() => { DisposeEcb(ref cmds); });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void MoveManagedComponent_Works()
        {
            var srcEntity = m_Manager.CreateEntity();
            var dstEntity = m_Manager.CreateEntity();
            var destroyedEntity = m_Manager.CreateEntity();
            m_Manager.DestroyEntity(destroyedEntity);
            {
                // null src is a recording-time error
                using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                Assert.Throws<InvalidOperationException>(() => cmds.MoveComponent<EcsTestDisposableManagedComponent>(Entity.Null, dstEntity));
            }
            {
                // null dst is a recording-time error
                using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                Assert.Throws<InvalidOperationException>(() => cmds.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, Entity.Null));
            }
            {
                // invalid but non-null src is a playback-time error
                using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                cmds.MoveComponent<EcsTestDisposableManagedComponent>(destroyedEntity, dstEntity);
                Assert.Throws<ArgumentException>(() => cmds.Playback(m_Manager));
            }
            {
                // invalid but non-null dst is a playback-time error
                using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                cmds.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, destroyedEntity);
                Assert.Throws<ArgumentException>(() => cmds.Playback(m_Manager));
            }
            {
                // missing target component on src is a playback-time error
                using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                cmds.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, dstEntity);
                Assert.Throws<ArgumentException>(() => cmds.Playback(m_Manager));
            }

            var value1 = new EcsTestDisposableManagedComponent { Value = "value1" };
            var value2 = new EcsTestDisposableManagedComponent { Value = "value2" };
            m_Manager.AddComponentData(srcEntity, value1);
            {
                // src == dst is a no-op
                using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                cmds.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, srcEntity);
                Assert.DoesNotThrow(() => cmds.Playback(m_Manager));
                Assert.AreEqual(value1, m_Manager.GetComponentData<EcsTestDisposableManagedComponent>(srcEntity));
                Assert.IsFalse(m_Manager.HasComponent<EcsTestDisposableManagedComponent>(dstEntity));
            }
            {
                // If dst doesn't have the target component, it's added, and the value is not disposed
                using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                cmds.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, dstEntity);
                Assert.DoesNotThrow(() => cmds.Playback(m_Manager));
                Assert.IsFalse(m_Manager.HasComponent<EcsTestDisposableManagedComponent>(srcEntity));
                Assert.AreEqual(value1, m_Manager.GetComponentData<EcsTestDisposableManagedComponent>(dstEntity));
            }
            {
                // dst's T is null / default-initialized should work
                m_Manager.AddComponentData(srcEntity, value1);
                m_Manager.AddComponent<EcsTestDisposableManagedComponent>(dstEntity);
                using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                cmds.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, dstEntity);
                Assert.DoesNotThrow(() => cmds.Playback(m_Manager));
                Assert.IsFalse(m_Manager.HasComponent<EcsTestDisposableManagedComponent>(srcEntity));
                Assert.AreEqual(value1, m_Manager.GetComponentData<EcsTestDisposableManagedComponent>(dstEntity));
            }
            {
                // dst's T value matches src's: should work
                m_Manager.AddComponentData(srcEntity, value1);
                using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                cmds.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, dstEntity);
                Assert.DoesNotThrow(() => cmds.Playback(m_Manager));
                Assert.IsFalse(m_Manager.HasComponent<EcsTestDisposableManagedComponent>(srcEntity));
                Assert.AreEqual(value1, m_Manager.GetComponentData<EcsTestDisposableManagedComponent>(dstEntity));
            }
            {
                // dst's T value does not match src. In this case, we need to make sure dst's old value is correctly disposed and doesn't leak.
                m_Manager.AddComponentData(srcEntity, value2);
                using var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
                cmds.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, dstEntity);
                Assert.DoesNotThrow(() => cmds.Playback(m_Manager));
                Assert.IsFalse(m_Manager.HasComponent<EcsTestDisposableManagedComponent>(srcEntity));
                Assert.AreEqual(value2, m_Manager.GetComponentData<EcsTestDisposableManagedComponent>(dstEntity));
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddManagedComponent()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity();
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.AddComponent(entity, new EcsTestManagedComponent());
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Adding managed component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestManagedComponent>()}; recorded from {originSystemDebugName}.");
            }

            Assert.IsTrue(m_Manager.HasComponent<EcsTestManagedComponent>(entity));

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetManagedComponentData()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var entity = m_Manager.CreateEntity(typeof(EcsTestManagedComponent));
            FixedString64Bytes entityName = "";

#if !DOTS_DISABLE_DEBUG_NAMES
            entityName = "TestEntity";
            m_Manager.SetName(entity, entityName);
#endif

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                cmds.SetComponent(entity, new EcsTestManagedComponent {value = "Test"});
                cmds.Playback(m_Manager);
                var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                LogAssert.Expect(LogType.Log,
                    $"Setting managed component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestManagedComponent>()}; recorded from {originSystemDebugName}.");
            }

            Assert.AreEqual("Test", m_Manager.GetComponentData<EcsTestManagedComponent>(entity).value);

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_AddComponentObjectForMultipleEntities()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestManagedComponent), typeof(EcsTestData));
            m_Manager.CreateEntity(archetype, 5, World.UpdateAllocator.ToAllocator);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var originalVal = new EcsTestManagedComponent();
                cmds.AddComponentObject(entityQuery, originalVal);
                cmds.Playback(m_Manager);

                using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        var entity = entities[i];
                        m_Manager.GetName(entity, out var entityName);
                        var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                        LogAssert.Expect(LogType.Log,
                            $"Adding component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestManagedComponent>()}; recorded from {originSystemDebugName}.");

                        var val = m_Manager.GetComponentObject<EcsTestManagedComponent>(entity);
                        Assert.AreSame(originalVal, val);
                    }
                }
            }

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity command buffer safety checks")]
        public void PlaybackWithTrace_SetComponentObjectForMultipleEntities()
        {
            EntityCommandBuffer.PLAYBACK_WITH_TRACE = true;

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestManagedComponent), typeof(EcsTestData));
            m_Manager.CreateEntity(archetype, 5, World.UpdateAllocator.ToAllocator);

            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            using (var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var originalVal = new EcsTestManagedComponent();
                cmds.SetComponentObject(entityQuery, originalVal);
                cmds.Playback(m_Manager);

                using (var entities = entityQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        var entity = entities[i];
                        m_Manager.GetName(entity, out var entityName);
                        var originSystemDebugName = GetSystemDebugName(cmds.OriginSystemHandle);
                        LogAssert.Expect(LogType.Log,
                            $"Setting component on entity {entityName}({entity.Index},{entity.Version}) for component index {TypeManager.GetTypeIndex<EcsTestManagedComponent>()}; recorded from {originSystemDebugName}.");

                        var val = m_Manager.GetComponentObject<EcsTestManagedComponent>(entity);
                        Assert.AreSame(originalVal, val);
                    }
                }
            }

            EntityCommandBuffer.PLAYBACK_WITH_TRACE = false;
        }
#endif
        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void PassingNullEntityToECBThrows()
        {
            using (var cmds = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                Assert.Throws<InvalidOperationException>(
                    () => cmds.DestroyEntity(Entity.Null),
                    "Invalid Entity.Null passed. ECBCommand.DestroyEntity");
            }
        }
    }
}
