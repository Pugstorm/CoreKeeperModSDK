using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace Unity.Entities.Tests
{
    class IJobChunkTests : ECSTestsFixture
    {
        [BurstCompile(CompileSynchronously = true)]
        struct WriteChunkIndex : IJobChunk
        {
            public ComponentTypeHandle<EcsTestData> EcsTestTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var testDataArray = chunk.GetNativeArray(ref EcsTestTypeHandle);
                testDataArray[0] = new EcsTestData
                {
                    value = unfilteredChunkIndex
                };
            }
        }

        // Not Burst compiling since we are Asserting in this job
        struct WriteChunkInfoToArray : IJobChunk
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<ArchetypeChunk> Chunks;
            [NativeDisableParallelForRestriction]
            public NativeArray<bool> ChunkUseEnabledMasks;
            [NativeDisableParallelForRestriction]
            public NativeArray<v128> ChunkEnabledMasks;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // We expect the Chunks array to be uninitialized until written by this job.
                // If this fires, some other job thread has filled in this batch's info already!
                Assert.IsTrue(Chunks[unfilteredChunkIndex].Invalid());
                Assert.NotZero(chunk.Count);

                Chunks[unfilteredChunkIndex] = chunk;
                ChunkUseEnabledMasks[unfilteredChunkIndex] = useEnabledMask;
                ChunkEnabledMasks[unfilteredChunkIndex] = chunkEnabledMask;
            }
        }

        [Test]
        public void IJobChunk_Run_DoesNotThrow()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var entityCount = 100;
                m_Manager.CreateEntity(archetype, entityCount);
                var job = new WriteChunkIndex
                {
                    EcsTestTypeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(false)
                };
                Assert.DoesNotThrow(() => { job.Run(query); });
            }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void IJobChunk_RunWithoutDependency_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var entityCount = 100;
                m_Manager.CreateEntity(archetype, entityCount);
                var job = new WriteChunkIndex
                {
                    EcsTestTypeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(false)
                };
                var handle = job.Schedule(query, default);
                Assert.Throws<InvalidOperationException>(() => { job.Run(query); });
                handle.Complete();
            }
        }
#endif

        struct ChunkIndexJob : IJobChunk
        {
            public NativeArray<int> OutPerChunkData;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // If JobsUtility.PatchBufferMinMaxRanges() is not called correctly, this array write will fail
                OutPerChunkData[unfilteredChunkIndex] = unfilteredChunkIndex;
            }
        }

        [Test,TestRequiresCollectionChecks]
        public void IJobChunk_DefaultArrayRangeCheck_Works([Values] bool enableChunkFilter)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            int entityCount = 1000;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData,EcsTestSharedComp>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            int sharedComponentFilterValue = 17;
            if (enableChunkFilter)
            {
                query.SetSharedComponentFilter(new EcsTestSharedComp { value = sharedComponentFilterValue });
                for (int i = 0; i < entityCount; i += 2)
                {
                    m_Manager.SetSharedComponent(entities[i],
                        new EcsTestSharedComp { value = sharedComponentFilterValue });
                }
            }

            int chunkCount = query.CalculateChunkCountWithoutFiltering();
            var outputPerChunkData = CollectionHelper.CreateNativeArray<int>(chunkCount, World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < chunkCount; ++i)
                outputPerChunkData[i] = -1;
            var job = new ChunkIndexJob{
                OutPerChunkData = outputPerChunkData,
            };
            job.ScheduleParallelByRef(query, default).Complete();

            query.ResetFilter();
            using var unfilteredChunks = query.ToArchetypeChunkArray(Allocator.TempJob);
            var sharedComponentTypeHandle = m_Manager.GetSharedComponentTypeHandle<EcsTestSharedComp>();
            for (int i = 0; i < chunkCount; ++i)
            {
                int expectedValue = i;
                if (enableChunkFilter)
                    expectedValue = unfilteredChunks[i].GetSharedComponent(sharedComponentTypeHandle).value == sharedComponentFilterValue ? i : -1;
                Assertions.Assert.AreEqual(expectedValue, outputPerChunkData[i]);
            }
            outputPerChunkData.Dispose();
        }

        struct ChunkBaseEntityIndexJob : IJobChunk
        {
            [ReadOnly] public NativeArray<int> ChunkBaseEntityIndices;
            public NativeArray<int> OutPerEntityData;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                int baseEntityIndex = ChunkBaseEntityIndices[unfilteredChunkIndex];
                int validEntitiesInChunk = 0;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while(enumerator.NextEntityIndex(out int i))
                {
                    int entityIndexInQuery = baseEntityIndex + validEntitiesInChunk;
                    // If JobsUtility.PatchBufferMinMaxRanges() is not called correctly, this array write will fail
                    OutPerEntityData[entityIndexInQuery] = i;
                    ++validEntitiesInChunk;
                }
            }
        }

        [Test,TestRequiresCollectionChecks]
        public void IJobChunk_OptionalArrayRangeCheck_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            int entityCount = 1000;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            using var chunkBaseEntityIndices = query.CalculateBaseEntityIndexArray(World.UpdateAllocator.ToAllocator);
            using var outputPerEntityData = CollectionHelper.CreateNativeArray<int>(entityCount, World.UpdateAllocator.ToAllocator);
            var job = new ChunkBaseEntityIndexJob{
                ChunkBaseEntityIndices = chunkBaseEntityIndices,
                OutPerEntityData = outputPerEntityData
            };
            Internal.InternalCompilerInterface.JobChunkInterface.ScheduleParallelByRef(ref job, query, default, chunkBaseEntityIndices).Complete();
            for (int i = 0; i < entityCount; ++i)
            {
                Assertions.Assert.AreEqual(i % archetype.ChunkCapacity,
                    outputPerEntityData[i]);
            }
        }

        public enum ScheduleMode
        {
            Parallel, Single, Run, RunWithoutJobs
        }

        [TestCase(ScheduleMode.Parallel)]
        [TestCase(ScheduleMode.Single)]
        [TestCase(ScheduleMode.Run)]
        [TestCase(ScheduleMode.RunWithoutJobs)]
        public void IJobChunk_WithoutFiltering_ExecutesOnExpectedChunks(ScheduleMode mode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entityCount = 10000;
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            for (var i = 0; i < entityCount; ++i)
            {
                m_Manager.SetComponentData(entities[i], new EcsTestData {value = -1});
            }

            int queryUnfilteredChunkCount = query.CalculateChunkCountWithoutFiltering();
            using var chunks = CollectionHelper.CreateNativeArray<ArchetypeChunk>(queryUnfilteredChunkCount,
                    World.UpdateAllocator.ToAllocator);
            using var chunkUseEnabledMasks =
                CollectionHelper.CreateNativeArray<bool>(queryUnfilteredChunkCount, World.UpdateAllocator.ToAllocator);
            using var chunkEnabledMasks =
                CollectionHelper.CreateNativeArray<v128>(queryUnfilteredChunkCount, World.UpdateAllocator.ToAllocator);

            var job = new WriteChunkInfoToArray
            {
                Chunks = chunks,
                ChunkUseEnabledMasks = chunkUseEnabledMasks,
                ChunkEnabledMasks = chunkEnabledMasks,
            };
            if (mode == ScheduleMode.Parallel)
                job.ScheduleParallel(query, default).Complete();
            else if (mode == ScheduleMode.Single)
                job.Schedule(query, default).Complete();
            else if (mode == ScheduleMode.Run)
                job.Run(query);
            else if (mode == ScheduleMode.RunWithoutJobs)
                Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobs(ref job, query);

            var entityTypeHandle = m_Manager.GetEntityTypeHandle();
            int markedEntityCount = 0;
            for (int chunkIndex = 0; chunkIndex < chunks.Length; ++chunkIndex)
            {
                var chunk = chunks[chunkIndex];
                if (chunk.Invalid())
                    continue; // this is fine; empty/filtered batches will be skipped and left uninitialized.
                FastAssert.Greater(chunk.Count, 0); // empty batches should not have been Execute()ed
                FastAssert.AreEqual(chunk.Count, chunk.Count);
                FastAssert.IsFalse(chunkUseEnabledMasks[chunkIndex]);
                //Assert.AreEqual(default(v128), chunkUseEnabledMasks[chunkIndex]); // contents are undefined
                var chunkEntities = chunk.GetNativeArray(entityTypeHandle);
                for (int i = 0; i < chunkEntities.Length; ++i)
                {
                    FastAssert.AreEqual(-1, m_Manager.GetComponentData<EcsTestData>(chunkEntities[i]).value);
                    m_Manager.SetComponentData(chunkEntities[i], new EcsTestData {value = 1});
                    markedEntityCount++;
                }
            }

            Assert.AreEqual(entities.Length, markedEntityCount);
            for (int i = 0; i < entities.Length; ++i)
            {
                FastAssert.AreEqual(1, m_Manager.GetComponentData<EcsTestData>(entities[i]).value);
            }
        }

        [TestCase(ScheduleMode.Parallel)]
        [TestCase(ScheduleMode.Single)]
        [TestCase(ScheduleMode.Run)]
        [TestCase(ScheduleMode.RunWithoutJobs)]
        public void IJobChunk_WithFiltering_ExecutesOnExpectedChunks(ScheduleMode mode)
        {
            var entityCount = 10000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp));
            query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 17});
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            for (var i = 0; i < entityCount; ++i)
            {
                m_Manager.SetComponentData(entities[i], new EcsTestData {value = -1});
                if ((i % 2) == 0)
                {
                    m_Manager.SetSharedComponentManaged(entities[i], new EcsTestSharedComp {value = 17});
                }
            }

            int queryUnfilteredChunkCount = query.CalculateChunkCountWithoutFiltering();
            using var chunks = CollectionHelper.CreateNativeArray<ArchetypeChunk>(queryUnfilteredChunkCount,
                    World.UpdateAllocator.ToAllocator);
            using var chunkUseEnabledMasks =
                CollectionHelper.CreateNativeArray<bool>(queryUnfilteredChunkCount, World.UpdateAllocator.ToAllocator);
            using var chunkEnabledMasks =
                CollectionHelper.CreateNativeArray<v128>(queryUnfilteredChunkCount, World.UpdateAllocator.ToAllocator);

            var job = new WriteChunkInfoToArray
            {
                Chunks = chunks,
                ChunkUseEnabledMasks = chunkUseEnabledMasks,
                ChunkEnabledMasks = chunkEnabledMasks,
            };
            if (mode == ScheduleMode.Parallel)
                job.ScheduleParallel(query, default).Complete();
            else if (mode == ScheduleMode.Single)
                job.Schedule(query, default).Complete();
            else if (mode == ScheduleMode.Run)
                job.Run(query);
            else if (mode == ScheduleMode.RunWithoutJobs)
                Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobs(ref job, query);

            var entityTypeHandle = m_Manager.GetEntityTypeHandle();
            int markedEntityCount = 0;
            for (int chunkIndex = 0; chunkIndex < chunks.Length; ++chunkIndex)
            {
                var chunk = chunks[chunkIndex];
                if (chunk.Invalid())
                    continue; // this is fine; empty/filtered batches will be skipped and left uninitialized.
                FastAssert.Greater(chunk.Count, 0); // empty batches should not have been Execute()ed
                FastAssert.AreEqual(chunk.Count, chunk.Count);
                FastAssert.IsFalse(chunkUseEnabledMasks[chunkIndex]);
                //Assert.AreEqual(default(v128), chunkUseEnabledMasks[chunkIndex]); // contents are undefined
                var chunkEntities = chunk.GetNativeArray(entityTypeHandle);
                for (int i = 0; i < chunkEntities.Length; ++i)
                {
                    FastAssert.AreEqual(-1, m_Manager.GetComponentData<EcsTestData>(chunkEntities[i]).value);
                    FastAssert.AreEqual(17, m_Manager.GetSharedComponent<EcsTestSharedComp>(chunkEntities[i]).value);
                    m_Manager.SetComponentData(chunkEntities[i], new EcsTestData {value = 1});
                    markedEntityCount++;
                }
            }

            Assert.AreEqual(query.CalculateEntityCount(), markedEntityCount);
            for (int i = 0; i < entities.Length; ++i)
            {
                int testValue = m_Manager.GetComponentData<EcsTestData>(entities[i]).value;
                if (m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entities[i]).value == 17)
                {
                    FastAssert.AreEqual(1, testValue);
                }
                else
                {
                    FastAssert.AreEqual(-1, testValue);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct WriteToArray : IJobChunk
        {
            public NativeArray<int> MyArray;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                for (int i = 0; i < MyArray.Length; i++)
                {
                    MyArray[i] = unfilteredChunkIndex;
                }
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ParallelArrayWriteTriggersSafetySystem()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData));
            using(var entitiesA = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(archetypeA.ChunkCapacity, ref World.UpdateAllocator))
            using(var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            using (var local = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(archetypeA.ChunkCapacity * 2, ref World.UpdateAllocator))
            {
                m_Manager.CreateEntity(archetypeA, entitiesA);
                LogAssert.Expect(LogType.Exception, new Regex("IndexOutOfRangeException: *"));
                new WriteToArray
                {
                    MyArray = local
                }.ScheduleParallel(query, default).Complete();
            }
        }

        [Test]
        public void SingleArrayWriteDoesNotTriggerSafetySystem()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData));
            using(var entitiesA = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(archetypeA.ChunkCapacity, ref World.UpdateAllocator))
            using(var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            using (var local = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(archetypeA.ChunkCapacity * 2, ref World.UpdateAllocator))
            {
                m_Manager.CreateEntity(archetypeA, entitiesA);
                new WriteToArray
                {
                    MyArray = local
                }.Schedule(query, default).Complete();
            }
        }

        // Not Burst compiling since we are Asserting in this job
        struct CheckChunkIndices : IJobChunk
        {
            public ComponentTypeHandle<EcsTestData> EcsTestDataTypeHandle;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var testData = chunk.GetNativeArray(ref EcsTestDataTypeHandle);
                Assert.AreEqual(unfilteredChunkIndex, testData[0].value);
            }
        }

        [Test]
        public void IJobChunk_WithNoEnableable_HasCorrectIndices(
            [Values(ScheduleMode.Parallel, ScheduleMode.Single, ScheduleMode.Run, ScheduleMode.RunWithoutJobs)] ScheduleMode scheduleMode)
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));

            using(var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp)))
            using (var entities = m_Manager.CreateEntity(archetypeA, 100, World.UpdateAllocator.ToAllocator))
            {
                var val = 0;
                foreach (var entity in entities)
                {
                    m_Manager.SetComponentData(entity, new EcsTestData() {value = val});
                    m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp() {value = val});
                    val++;
                }

                var job = new CheckChunkIndices {EcsTestDataTypeHandle = EmptySystem.GetComponentTypeHandle<EcsTestData>() };

                switch (scheduleMode)
                {
                    case ScheduleMode.Parallel:
                        job.ScheduleParallel(query, default).Complete();
                        break;
                    case ScheduleMode.Single:
                        job.Schedule(query, default).Complete();
                        break;
                    case ScheduleMode.Run:
                        job.Run(query);
                        break;
                    case ScheduleMode.RunWithoutJobs:
                        Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobs(ref job, query);
                        break;
                }
            }
        }

        struct LargeChunkJob : IJobChunk
        {
            public FixedString4096Bytes StrA;
            public FixedString4096Bytes StrB;
            public FixedString4096Bytes StrC;
            public FixedString4096Bytes StrD;
            [NativeDisableParallelForRestriction]
            public NativeArray<int> TotalLengths;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                TotalLengths[0] = StrA.Length + StrB.Length + StrC.Length + StrD.Length;
            }
        }

        [Test]
        public void IJobChunk_LargeJobStruct_ScheduleByRefWorks(
            [Values(ScheduleMode.Parallel, ScheduleMode.Single, ScheduleMode.Run)] ScheduleMode scheduleMode)
        {
            m_Manager.CreateEntity(typeof(EcsTestData));
            using(var lengths = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(1, ref World.UpdateAllocator))
            using(var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var job = new LargeChunkJob
                {
                        StrA = "A",
                        StrB = "BB",
                        StrC = "CCC",
                        StrD = "DDDD",
                        TotalLengths = lengths,
                };
                if (scheduleMode == ScheduleMode.Parallel)
                    Assert.DoesNotThrow(() => { job.ScheduleParallelByRef(query, default).Complete(); });
                else if (scheduleMode == ScheduleMode.Single)
                    Assert.DoesNotThrow(() => { job.ScheduleByRef(query, default).Complete(); });
                else if (scheduleMode == ScheduleMode.Run)
                    Assert.DoesNotThrow(() => { job.RunByRef(query); });
                Assert.AreEqual(lengths[0], 10);
            }
        }

        [TestCase(JobRunType.Schedule)]
        [TestCase(JobRunType.ScheduleByRef)]
        [TestCase(JobRunType.Run)]
        [TestCase(JobRunType.RunByRef)]
        [TestCase(JobRunType.RunWithoutJobs)]
        public void IJobChunk_Jobs_FromBurst(JobRunType runType)
        {
            if (!IsBurstEnabled())  // No need to error on burst compilation failure in job if no burst
                return;

            var sys = World.CreateSystem<IJobChunk_Jobs_ISystem_WithBurst>();
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp));
            using var entities = m_Manager.CreateEntity(archetype, 3, World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entities.Length; ++i)
                m_Manager.SetSharedComponent(entities[i], new EcsTestSharedComp { value = i });

            var singletonArchetype = m_Manager.CreateArchetype(typeof(JobRunTypeComp));
            var singletonEntity = m_Manager.CreateEntity(singletonArchetype);
            m_Manager.SetComponentData(singletonEntity, new JobRunTypeComp { type = runType });

            sys.Update(World.Unmanaged);
            var Result = World.EntityManager.GetComponentData<ResultData>(sys);
            Assert.AreEqual(entities.Length, Result.Result);
        }

        struct AddOne_EnabledBits: IJobChunk
        {
            public ComponentTypeHandle<EcsTestDataEnableable> TestDataHandle;
            public unsafe void Execute(in ArchetypeChunk chunk, int chunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var enumerator = new ChunkEntityEnumerator(useEnabledMask,chunkEnabledMask,chunk.Count);
                var components = (EcsTestDataEnableable*)chunk.GetComponentDataPtrRW(ref TestDataHandle);
                while(enumerator.NextEntityIndex(out var i))
                {
                    components[i].value++;
                }
            }
        }

        [Test]
        public void IJobChunk_ChunkEntityEnumerator_NoDisabledBits()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            //fill up multiple chunks of data
            m_Manager.CreateEntity(archetype, archetype.ChunkCapacity * 2);

            var job = new AddOne_EnabledBits
            {
                TestDataHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false)
            };

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            job.Run(query);

            var components = query.ToComponentDataArray<EcsTestDataEnableable>(World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < components.Length; i++)
            {
                if(components[i].value != 1)
                    Assert.AreEqual(1,components[i].value,$"Index {i} expected 1 but got {components[i].value}");
            }
        }

        [Test]
        public void IJobChunk_ChunkEntityEnumerator_HalfDisabledBits()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            //fill up multiple chunks of data
            var entities = m_Manager.CreateEntity(archetype, archetype.ChunkCapacity * 2,World.UpdateAllocator.ToAllocator);

            for (int i = 1; i < entities.Length; i += 2)
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);

            var job = new AddOne_EnabledBits
            {
                TestDataHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false)
            };

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));

            job.Run(query);

            for (int i = 0; i < entities.Length; i++)
            {
                var value = i % 2 == 0 ? 1 : 0;
                var actual = m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value;
                if(value != actual)
                    Assert.AreEqual(value, actual,$"Index {i} expected {value} but got {actual}");
            }
        }

        [Test]
        public void IJobChunk_ChunkEntityEnumerator_EnabledBitsRanges()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            //fill up multiple chunks of data
            var entities = m_Manager.CreateEntity(archetype, archetype.ChunkCapacity * 2,World.UpdateAllocator.ToAllocator);

            //large ranges of enabled bits with some sparse disabling
            for (int i = 0; i < entities.Length; i += 10)
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);

            var job = new AddOne_EnabledBits
            {
                TestDataHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false)
            };

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));

            job.Run(query);

            for (int i = 0; i < entities.Length; i++)
            {
                var value = i % 10 == 0 ? 0 : 1;
                var actual = m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value;
                Assert.AreEqual(value, actual,$"Index {i} expected {value} but got {actual}");
            }
        }

        struct WriteComponentJobChunk<T> : IJobChunk
        {
            public ComponentTypeHandle<T> TypeHandle;
            public void Execute(in ArchetypeChunk chunk, int chunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
            }
        }
        struct WriteComponentJob<T> : IJob
        {
            public ComponentTypeHandle<T> TypeHandle;
            public void Execute()
            {
            }
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void ConcurrentJob_WritesToEnableableComponentInQuery_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            // Schedule job against a query which includes enableable types (which are not explicitly referenced by the
            // job itself, but which are nonetheless read concurrently to determine which entities should be processed)
            var job1 = new WriteComponentJobChunk<EcsTestData> { TypeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(false) };
            var handle1 = job1.Schedule(query, default);
            // Schedule a second job which writes to the enableable component in the query.
            var job2 = new WriteComponentJob<EcsTestDataEnableable>
                { TypeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false) };
            Assert.Throws<InvalidOperationException>(() => job2.Schedule());
            // With the correct dependency, it's fine.
            Assert.DoesNotThrow(() => job2.Schedule(handle1).Complete());
        }
#endif
    }

    struct ResultData : IComponentData
    {
        public int Result;
    }

    [BurstCompile(CompileSynchronously = true)]
    partial struct IJobChunk_Jobs_ISystem_WithBurst : ISystem
    {
        struct TestJob : IJobChunk
        {
            public NativeReference<int> Count;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Count.Value += 1;
            }
        }

        EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.AddComponentData(state.SystemHandle, new ResultData
            {
                Result = 0
            });
            _query = state.GetEntityQuery(typeof(EcsTestSharedComp));
        }

        [BurstCompile(CompileSynchronously = true)]
        public void OnUpdate(ref SystemState state)
        {
            bool burstCompiled = true;
            ECSTestsCommonBase.TestBurstCompiled(ref burstCompiled);
            if (!burstCompiled)
                throw new InvalidOperationException("Expected burst compiled job schedule code");

            using var count = new NativeReference<int>(state.m_WorldUnmanaged.UpdateAllocator.ToAllocator);
            var job = new TestJob { Count = count };

            var runType = SystemAPI.GetSingleton<JobRunTypeComp>();

            switch (runType.type)
            {
                case JobRunType.Schedule: job.Schedule(_query, state.Dependency).Complete(); break;
                case JobRunType.ScheduleByRef: job.ScheduleByRef(_query, state.Dependency).Complete(); break;
                case JobRunType.Run: job.Run(_query); break;
                case JobRunType.RunByRef: job.RunByRef(_query); break;
                case JobRunType.RunWithoutJobs: Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobs(ref job, _query); break;
            }

            ref var Result = ref state.EntityManager.GetComponentDataRW<ResultData>(state.SystemHandle).ValueRW;
            Result.Result = count.Value;
        }
    }
}
