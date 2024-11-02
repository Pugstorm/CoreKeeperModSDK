using System;
using NUnit.Framework;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Assert = FastAssert;

namespace Unity.Entities.Tests
{
    partial class ComponentEnabledBitFeatureTests : ECSTestsFixture
    {
        partial class TouchEnabledEntitiesSystem : SystemBase
        {
            private EntityQuery _query;
            protected override void OnCreate()
            {
                _query = GetEntityQuery(typeof(EcsTestDataEnableable), typeof(EcsTestData));
            }

            partial struct TouchEnabledEntitiesJob : IJobEntity
            {
                public void Execute(ref EcsTestData data)
                {
                    data.value = 1;
                }
            }

            protected override void OnUpdate()
            {
                var job = new TouchEnabledEntitiesJob();
                job.Run(_query);
            }
        }

        [Test]
        public void IJobEntity_WithEnabledBits_ProcessesExpectedEntities()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            int entityCount = 10000;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            for(int i=0; i<entityCount; ++i)
            {
                m_Manager.SetComponentData(entities[i], new EcsTestData(0));
                if (i % 10 == 0)
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
            }

            var system = World.CreateSystemManaged<TouchEnabledEntitiesSystem>();
            system.Update();
            for(int i=0; i<entityCount; ++i)
            {
                int expected = m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entities[i]) ? 1 : 0;
                int actual = m_Manager.GetComponentData<EcsTestData>(entities[i]).value;
                if (expected != actual)
                    Assert.AreEqual(expected, actual, $"Mismatch in entity {i} -- expected={expected} actual={actual}");
            }
        }

        struct SetValueJob : IJobChunk
        {
            public ComponentTypeHandle<EcsTestDataEnableable> TypeRW;
            public int SetValue;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var data = chunk.GetNativeArray(ref TypeRW);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while(enumerator.NextEntityIndex(out var i))
                {
                    data[i] = new EcsTestDataEnableable(SetValue);
                }
            }
        }

        struct DisableEveryOtherEntityJob : IJobChunk
        {
            public ComponentTypeHandle<EcsTestDataEnableable> TypeRW;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while(enumerator.NextEntityIndex(out var i))
                {
                    var enabledValue = i % 2 == 0;
                    chunk.SetComponentEnabled(ref TypeRW, i, enabledValue);
                }
            }
        }

        partial struct SetValueToIndexJob : IJobEntity
        {
            public void Execute(ref EcsTestDataEnableable data, [Unity.Entities.EntityIndexInQuery] int entityIndexInQuery)
            {
                data = new EcsTestDataEnableable(entityIndexInQuery);
            }
        }

        public enum ScheduleMode
        {
            Parallel, Single, Run
        }

        [Test]
        public void IJobChunk_GeneratesCorrectBatches([Values] ScheduleMode mode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            using (var entities = m_Manager.CreateEntity(archetype, archetype.ChunkCapacity, World.UpdateAllocator.ToAllocator))
            using (var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>()))
            {
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[10], false);
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[63], false);
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[64], false);

                var setValue = 10;
                var job = new SetValueJob
                {
                    TypeRW = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false),
                    SetValue = setValue
                };
                if (mode == ScheduleMode.Parallel)
                    job.ScheduleParallel(query, default).Complete();
                else if (mode == ScheduleMode.Single)
                    job.Schedule(query, default).Complete();
                else if (mode == ScheduleMode.Run)
                    job.Run(query);

                for (int i = 0; i < entities.Length; ++i)
                {
                    if (i == 10 || i == 63 || i == 64)
                    {
                        Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value);
                        continue;
                    }

                    Assert.AreEqual(10, m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value);
                }
            }
        }

        [Test]
        public void IJobChunk_ParallelJob_GeneratesExpectedBatches()
        {
            var chunkCount = 10;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            using (var entities = m_Manager.CreateEntity(archetype, archetype.ChunkCapacity * chunkCount, World.UpdateAllocator.ToAllocator))
            using (var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>()))
            {
                var jobHandle = new DisableEveryOtherEntityJob
                {
                    TypeRW = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false),
                }.ScheduleParallel(query, default);

                var setValue = 10;
                jobHandle = new SetValueJob
                {
                    TypeRW = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false),
                    SetValue = setValue
                }.ScheduleParallel(query, jobHandle);

                jobHandle.Complete();

                for (int i = 0; i < entities.Length; ++i)
                {
                    var chunkIndex = i / archetype.ChunkCapacity;
                    var indexInChunk = i - (chunkIndex * archetype.ChunkCapacity);
                    var expectedValue = indexInChunk % 2 == 0 ? 10 : 0;
                    Assert.AreEqual(expectedValue, m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value);
                }
            }
        }

        [Test]
        public void IJobChunk_WithFiltering_ParallelJob_GeneratesExpectedBatches()
        {
            var chunkCount = 10;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable), typeof(EcsTestSharedComp));
            using (var entities = m_Manager.CreateEntity(archetype, archetype.ChunkCapacity * chunkCount, World.UpdateAllocator.ToAllocator))
            using (var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>(), typeof(EcsTestSharedComp)))
            {
                for (int i = 0; i < entities.Length; ++i)
                {
                    if(i % 2 == 0)
                        m_Manager.SetSharedComponentManaged(entities[i], new EcsTestSharedComp(10));
                }

                query.SetSharedComponentFilterManaged(new EcsTestSharedComp(10));

                var jobHandle = new DisableEveryOtherEntityJob
                {
                    TypeRW = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false),
                }.ScheduleParallel(query, default);

                var setValue = 10;
                jobHandle = new SetValueJob
                {
                    TypeRW = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false),
                    SetValue = setValue
                }.ScheduleParallel(query, jobHandle);

                jobHandle.Complete();

                for (int i = 0; i < entities.Length; ++i)
                {
                    if(i % 2 != 0)
                        Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value);
                    else
                    {
                        var indexInFilteredEntities = i / 2;
                        var chunkIndex = indexInFilteredEntities / archetype.ChunkCapacity;
                        var indexInChunk = indexInFilteredEntities - (chunkIndex * archetype.ChunkCapacity);
                        var expectedValue = indexInChunk % 2 == 0 ? 10 : 0;
                        Assert.AreEqual(expectedValue,
                            m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value);
                    }
                }
            }
        }

        partial class IJobEntity_GeneratesCorrectBatches_TestSystem : SystemBase
        {
            EntityQuery _query;
            ComponentTypeHandle<EcsTestDataEnableable> _typeHandle;
            public ScheduleMode Mode;

            protected override void OnCreate()
            {
                _query = GetEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>());
                _typeHandle = GetComponentTypeHandle<EcsTestDataEnableable>(false);
            }

            protected override void OnUpdate()
            {
                new DisableEveryOtherEntityJob
                {
                    TypeRW = _typeHandle,
                }.Run(_query);

                var job = new SetValueToIndexJob();
                if (Mode == ScheduleMode.Parallel)
                    job.ScheduleParallel(_query, default).Complete();
                else if (Mode == ScheduleMode.Single)
                    job.Schedule(_query, default).Complete();
                else if (Mode == ScheduleMode.Run)
                    job.Run(_query);
            }
        }

        [Test]
        public void IJobEntity_GeneratesCorrectBatches([Values] ScheduleMode mode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            using var entities =
                m_Manager.CreateEntity(archetype, archetype.ChunkCapacity, World.UpdateAllocator.ToAllocator);

            var sys = World.CreateSystemManaged<IJobEntity_GeneratesCorrectBatches_TestSystem>();
            sys.Mode = mode;
            sys.Update();

            for (int i = 0; i < entities.Length; ++i)
            {
                var chunkIndex = i / archetype.ChunkCapacity;
                var indexInChunk = i - (chunkIndex * archetype.ChunkCapacity);
                var halfChunkCapacity = archetype.ChunkCapacity / 2;
                var expectedValue = indexInChunk % 2 == 0 ? chunkIndex * halfChunkCapacity + indexInChunk / 2 : 0;
                Assert.AreEqual(expectedValue, m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value);
            }
        }

        partial class IJobEntity_GeneratesCorrectBatches_ParallelJob_TestSystem : SystemBase
        {
            EntityQuery _query;
            ComponentTypeHandle<EcsTestDataEnableable> _typeHandle;
            public bool EnableQueryFilter = false;

            protected override void OnCreate()
            {
                _query = GetEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>(), ComponentType.ReadOnly<EcsTestSharedComp>());
                _typeHandle = GetComponentTypeHandle<EcsTestDataEnableable>(false);
            }

            protected override void OnUpdate()
            {
                if (EnableQueryFilter)
                    _query.SetSharedComponentFilterManaged(new EcsTestSharedComp(10));
                var jobHandle = new DisableEveryOtherEntityJob
                {
                    TypeRW = _typeHandle,
                }.ScheduleParallel(_query, default);

                var job = new SetValueToIndexJob();
                job.ScheduleParallel(_query, jobHandle).Complete();
            }
        }

        [Test]
        public void IJobEntity_ParallelJob_GeneratesExpectedBatches()
        {
            var chunkCount = 10;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable), typeof(EcsTestSharedComp));
            using var entities = m_Manager.CreateEntity(archetype, archetype.ChunkCapacity * chunkCount,
                World.UpdateAllocator.ToAllocator);

            var sys = World.CreateSystemManaged<IJobEntity_GeneratesCorrectBatches_ParallelJob_TestSystem>();
            sys.EnableQueryFilter = false;
            sys.Update();

            for (int i = 0; i < entities.Length; ++i)
            {
                var chunkIndex = i / archetype.ChunkCapacity;
                var indexInChunk = i - (chunkIndex * archetype.ChunkCapacity);
                var halfChunkCapacity = archetype.ChunkCapacity / 2;
                var expectedValue = indexInChunk % 2 == 0 ? chunkIndex * halfChunkCapacity + indexInChunk / 2 : 0;
                Assert.AreEqual(expectedValue, m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value);
            }
        }

        [Test]
        public void IJobEntity_WithFiltering_ParallelJob_GeneratesExpectedBatches()
        {
            var chunkCount = 10;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable), typeof(EcsTestSharedComp));
            using var entities =
                m_Manager.CreateEntity(archetype, archetype.ChunkCapacity * chunkCount,
                    World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entities.Length; ++i)
            {
                if (i % 2 == 0)
                    m_Manager.SetSharedComponentManaged(entities[i], new EcsTestSharedComp(10));
            }

            var sys = World.CreateSystemManaged<IJobEntity_GeneratesCorrectBatches_ParallelJob_TestSystem>();
            sys.EnableQueryFilter = true;
            sys.Update();

            for (int i = 0; i < entities.Length; ++i)
            {
                // if the entity is not in a chunk processed with filtering
                if (i % 2 != 0)
                    Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value);
                else
                {
                    var indexInFilteredEntities = i / 2;
                    var chunkIndex = indexInFilteredEntities / archetype.ChunkCapacity;
                    var indexInChunk = indexInFilteredEntities - (chunkIndex * archetype.ChunkCapacity);
                    var halfChunkCapacity = archetype.ChunkCapacity / 2;
                    var expectedValue =
                        indexInChunk % 2 == 0 ? chunkIndex * halfChunkCapacity + indexInChunk / 2 : 0;
                    Assert.AreEqual(expectedValue,
                        m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value);
                }
            }
        }

        [Test]
        public unsafe void GetEnabledMask_AllHaveRequiredComponentDisabled_SkipChunk()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2));
            int entitiesPerChunk = archetype.ChunkCapacity;
            using var entities =
                m_Manager.CreateEntity(archetype, entitiesPerChunk / 2, World.UpdateAllocator.ToAllocator);
            foreach (var ent in entities)
            {
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(ent, false);
            }
            m_Manager.SetComponentEnabled<EcsTestDataEnableable2>(entities[0], false);

            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestDataEnableable>()
                .WithNone<EcsTestDataEnableable2>()
                .Build(m_Manager);
            Assert.AreEqual(0, query.CalculateChunkCount());
            Assert.AreEqual(1, query.CalculateChunkCountWithoutFiltering());
            var queryImpl = query._GetImpl();
            var queryData = queryImpl->_QueryData;
            var queryChunkCache = queryImpl->GetMatchingChunkCache();
            var matchIndex = queryChunkCache.PerChunkMatchingArchetypeIndex->Ptr[0];
            var matchingArchetype = queryData->MatchingArchetypes.Ptr[matchIndex];
            var chunk = queryChunkCache.ChunkIndices[0];
            ChunkIterationUtility.GetEnabledMask(chunk, matchingArchetype, out var chunkEnabledMask);
            Assert.AreEqual(0, chunkEnabledMask.ULong0);
            Assert.AreEqual(0, chunkEnabledMask.ULong1);
        }

        [Test]
        public unsafe void GetEnabledMask_NoneHaveExcludedComponentDisabled_SkipChunk()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2));
            int entitiesPerChunk = archetype.ChunkCapacity;
            using var entities =
                m_Manager.CreateEntity(archetype, entitiesPerChunk / 2, World.UpdateAllocator.ToAllocator);
            // Disable the required component on one entity, to make sure the "no excluded components disabled" check
            // takes precedence over the "some required components disabled" check.
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[0], false);

            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestDataEnableable>()
                .WithNone<EcsTestDataEnableable2>()
                .Build(m_Manager);
            Assert.AreEqual(0, query.CalculateChunkCount());
            Assert.AreEqual(1, query.CalculateChunkCountWithoutFiltering());
            var queryImpl = query._GetImpl();
            var queryData = queryImpl->_QueryData;
            var queryChunkCache = queryImpl->GetMatchingChunkCache();
            var matchIndex = queryChunkCache.PerChunkMatchingArchetypeIndex->Ptr[0];
            var matchingArchetype = queryData->MatchingArchetypes.Ptr[matchIndex];
            var chunk = queryChunkCache.ChunkIndices[0];
            ChunkIterationUtility.GetEnabledMask(chunk, matchingArchetype, out var chunkEnabledMask);
            Assert.AreEqual(0, chunkEnabledMask.ULong0);
            Assert.AreEqual(0, chunkEnabledMask.ULong1);
        }

        [Test]
        public unsafe void GetEnabledMask_SomeHaveRequiredComponentDisabled_HasExpectedMask()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2));
            int entitiesPerChunk = archetype.ChunkCapacity;
            using var entities =
                m_Manager.CreateEntity(archetype, entitiesPerChunk / 2, World.UpdateAllocator.ToAllocator);
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[0], false);
            foreach (var ent in entities)
            {
                m_Manager.SetComponentEnabled<EcsTestDataEnableable2>(ent, false);
            }

            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestDataEnableable>()
                .WithNone<EcsTestDataEnableable2>()
                .Build(m_Manager);
            Assert.AreEqual(1, query.CalculateChunkCount());
            var queryImpl = query._GetImpl();
            var queryData = queryImpl->_QueryData;
            var queryChunkCache = queryImpl->GetMatchingChunkCache();
            var matchIndex = queryChunkCache.PerChunkMatchingArchetypeIndex->Ptr[0];
            var matchingArchetype = queryData->MatchingArchetypes.Ptr[matchIndex];
            var chunk = queryChunkCache.ChunkIndices[0];
            // The chunk should be half-full, with the first entity not enabled (it has the required component disabled)
            ChunkIterationUtility.GetEnabledMask(chunk, matchingArchetype, out var chunkEnabledMask);
            Assert.AreEqual(0xFFFFFFFFFFFFFFFE, chunkEnabledMask.ULong0);
            Assert.AreEqual(0, chunkEnabledMask.ULong1);
        }

        [Test]
        public unsafe void GetEnabledMask_SomeHaveExcludedComponentDisabled_HasExpectedMask()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2));
            int entitiesPerChunk = archetype.ChunkCapacity;
            using var entities =
                m_Manager.CreateEntity(archetype, entitiesPerChunk / 2, World.UpdateAllocator.ToAllocator);
            m_Manager.SetComponentEnabled<EcsTestDataEnableable2>(entities[0], false);

            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestDataEnableable>()
                .WithNone<EcsTestDataEnableable2>()
                .Build(m_Manager);
            Assert.AreEqual(1, query.CalculateChunkCount());
            var queryImpl = query._GetImpl();
            var queryData = queryImpl->_QueryData;
            var queryChunkCache = queryImpl->GetMatchingChunkCache();
            var matchIndex = queryChunkCache.PerChunkMatchingArchetypeIndex->Ptr[0];
            var matchingArchetype = queryData->MatchingArchetypes.Ptr[matchIndex];
            var chunk = queryChunkCache.ChunkIndices[0];
            // All entities have the required component enabled, but only the first has the excluded component disabled
            ChunkIterationUtility.GetEnabledMask(chunk, matchingArchetype, out var chunkEnabledMask);
            Assert.AreEqual(0x1, chunkEnabledMask.ULong0);
            Assert.AreEqual(0, chunkEnabledMask.ULong1);
        }

        [Test]
        public unsafe void GetEnabledMask_SomeHaveDisabledComponentEnabled_HasExpectedMask()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2));
            int entitiesPerChunk = archetype.ChunkCapacity;
            using var entities =
                m_Manager.CreateEntity(archetype, entitiesPerChunk / 2, World.UpdateAllocator.ToAllocator);
            m_Manager.SetComponentEnabled<EcsTestDataEnableable2>(entities[0], false);

            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestDataEnableable>()
                .WithDisabled<EcsTestDataEnableable2>()
                .Build(m_Manager);
            Assert.AreEqual(1, query.CalculateChunkCount());
            var queryImpl = query._GetImpl();
            var queryData = queryImpl->_QueryData;
            var queryChunkCache = queryImpl->GetMatchingChunkCache();
            var matchIndex = queryChunkCache.PerChunkMatchingArchetypeIndex->Ptr[0];
            var matchingArchetype = queryData->MatchingArchetypes.Ptr[matchIndex];
            var chunk = queryChunkCache.ChunkIndices[0];
            // All entities have the required component enabled, but only the first has the excluded component disabled
            ChunkIterationUtility.GetEnabledMask(chunk, matchingArchetype, out var chunkEnabledMask);
            Assert.AreEqual(0x1, chunkEnabledMask.ULong0);
            Assert.AreEqual(0, chunkEnabledMask.ULong1);
        }

        [Test]
        public unsafe void GetEnabledMask_AllHaveRequiredComponentEnabledAndExcludedComponentDisabled_HasExpectedMask()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2));
            int entitiesPerChunk = archetype.ChunkCapacity;
            using var entities =
                m_Manager.CreateEntity(archetype, entitiesPerChunk / 2, World.UpdateAllocator.ToAllocator);
            foreach (var ent in entities)
            {
                m_Manager.SetComponentEnabled<EcsTestDataEnableable2>(ent, false);
            }

            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestDataEnableable>()
                .WithNone<EcsTestDataEnableable2>()
                .Build(m_Manager);
            Assert.AreEqual(1, query.CalculateChunkCount());
            var queryImpl = query._GetImpl();
            var queryData = queryImpl->_QueryData;
            var queryChunkCache = queryImpl->GetMatchingChunkCache();
            var matchIndex = queryChunkCache.PerChunkMatchingArchetypeIndex->Ptr[0];
            var matchingArchetype = queryData->MatchingArchetypes.Ptr[matchIndex];
            var chunk = queryChunkCache.ChunkIndices[0];
            // All entities have the required component enabled, but only the first has the excluded component disabled
            ChunkIterationUtility.GetEnabledMask(chunk, matchingArchetype, out var chunkEnabledMask);
            Assert.AreEqual(ulong.MaxValue, chunkEnabledMask.ULong0);
            Assert.AreEqual(0, chunkEnabledMask.ULong1);
        }

        [Test]
        public void IsEmpty_RespectsEnabledBits()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            var archetypeB = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable2));

            var entityA = m_Manager.CreateEntity(archetypeA);
            var entityB = m_Manager.CreateEntity(archetypeB);

            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entityA, false);

            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable)))
            {
                Assert.IsTrue(query.IsEmpty);
            }

            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable2)))
            {
                Assert.IsFalse(query.IsEmpty);
            }

            using (var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>()
                .WithNone<EcsTestDataEnableable>()
                .Build(m_Manager))
            {
                Assert.IsFalse(query.IsEmpty);
            }
        }

        [Test]
        public void CalculateEntityCount_RespectsEnabledBits()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            var archetypeB = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable2));

            var entityA = m_Manager.CreateEntity(archetypeA);
            m_Manager.CreateEntity(archetypeA, 10);
            var entityB = m_Manager.CreateEntity(archetypeB);
            m_Manager.CreateEntity(archetypeB, 10);

            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entityA, false);

            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable)))
            {
                var entityCount = query.CalculateEntityCount();
                Assert.AreEqual(10, entityCount);
            }

            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable2)))
            {
                var entityCount = query.CalculateEntityCount();
                Assert.AreEqual(11, entityCount);
            }

            using (var query = new EntityQueryBuilder(Allocator.Temp)
                       .WithAll<EcsTestData>()
                       .WithNone<EcsTestDataEnableable>()
                       .Build(m_Manager))
            {
                var entityCount = query.CalculateEntityCount();
                Assert.AreEqual(12, entityCount);
            }
        }

        [Test]
        public void ToComponentDataArray_RespectsEnabledBits()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            var archetypeB = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable2));

            var entityCount = 10;
            using (var entitiesA = m_Manager.CreateEntity(archetypeA, entityCount, World.UpdateAllocator.ToAllocator))
            using (var entitiesB = m_Manager.CreateEntity(archetypeB, entityCount, World.UpdateAllocator.ToAllocator))
            {
                for (int entityIndex = 0; entityIndex < entityCount; ++entityIndex)
                {
                    m_Manager.SetComponentData(entitiesA[entityIndex], new EcsTestData(entityIndex));
                    m_Manager.SetComponentData(entitiesB[entityIndex], new EcsTestData(entityIndex));
                }
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entitiesA[3], false);

                using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData),typeof(EcsTestDataEnableable)))
                using (var array = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(9, array.Length);
                    for (int i = 0; i < array.Length; ++i)
                    {
                        var expectedValue = i > 2 ? i + 1 : i;
                        Assert.AreEqual(expectedValue, array[i].value);
                    }
                }

                using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData),typeof(EcsTestDataEnableable2)))
                using (var array = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(10, array.Length);
                    for (int i = 0; i < array.Length; ++i)
                    {
                        var expectedValue = i;
                        Assert.AreEqual(expectedValue, array[i].value);
                    }
                }

                using (var query = new EntityQueryBuilder(Allocator.Temp)
                           .WithAll<EcsTestData>()
                           .WithNone<EcsTestDataEnableable>()
                           .Build(m_Manager))
                using (var array = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(11, array.Length);
                    Assert.AreEqual(3, array[0].value);
                    for (int i = 1; i < array.Length; ++i)
                    {
                        var expectedValue = i - 1;
                        Assert.AreEqual(expectedValue, array[i].value);
                    }
                }
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void ToComponentDataArray_Managed_RespectsEnabledBits()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestManagedDataEntity), typeof(EcsTestDataEnableable));
            var archetypeB = m_Manager.CreateArchetype(typeof(EcsTestManagedDataEntity), typeof(EcsTestDataEnableable2));

            var entityCount = 10;
            using (var entitiesA = m_Manager.CreateEntity(archetypeA, entityCount, World.UpdateAllocator.ToAllocator))
            using (var entitiesB = m_Manager.CreateEntity(archetypeB, entityCount, World.UpdateAllocator.ToAllocator))
            {
                for (int entityIndex = 0; entityIndex < entityCount; ++entityIndex)
                {
                    m_Manager.SetComponentData(entitiesA[entityIndex], new EcsTestManagedDataEntity("test", Entity.Null, entityIndex));
                    m_Manager.SetComponentData(entitiesB[entityIndex], new EcsTestManagedDataEntity("test", Entity.Null, entityIndex));
                }
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entitiesA[3], false);

                using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestManagedDataEntity),typeof(EcsTestDataEnableable)))
                {
                    var array = query.ToComponentDataArray<EcsTestManagedDataEntity>();
                    Assert.AreEqual(9, array.Length);
                    for (int i = 0; i < array.Length; ++i)
                    {
                        var expectedValue = i > 2 ? i + 1 : i;
                        Assert.AreEqual(expectedValue, array[i].value2);
                    }
                }

                using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestManagedDataEntity),typeof(EcsTestDataEnableable2)))
                {
                    var array = query.ToComponentDataArray<EcsTestManagedDataEntity>();

                    Assert.AreEqual(10, array.Length);
                    for (int i = 0; i < array.Length; ++i)
                    {
                        var expectedValue = i;
                        Assert.AreEqual(expectedValue, array[i].value2);
                    }
                }

                using (var query = new EntityQueryBuilder(Allocator.Temp)
                           .WithAll<EcsTestManagedDataEntity>()
                           .WithNone<EcsTestDataEnableable>()
                           .Build(m_Manager))
                {
                    var array = query.ToComponentDataArray<EcsTestManagedDataEntity>();
                    Assert.AreEqual(11, array.Length);
                    Assert.AreEqual(3, array[0].value2);
                    for (int i = 1; i < array.Length; ++i)
                    {
                        var expectedValue = i - 1;
                        Assert.AreEqual(expectedValue, array[i].value2);
                    }
                }
            }
        }
#endif

        [Test]
        public void ToEntityArray_RespectsEnabledBits()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            var archetypeB = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable2));

            var entityCount = 10;
            using (var entitiesA = m_Manager.CreateEntity(archetypeA, entityCount, World.UpdateAllocator.ToAllocator))
            using (var entitiesB = m_Manager.CreateEntity(archetypeB, entityCount, World.UpdateAllocator.ToAllocator))
            {
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entitiesA[3], false);

                using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable)))
                using (var array = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(9, array.Length);
                    for (int i = 0; i < array.Length; ++i)
                    {
                        var expectedIndexInChunk = i > 2 ? i + 1 : i;
                        var expectedValue = entitiesA[expectedIndexInChunk];
                        Assert.AreEqual(expectedValue, array[i]);
                    }
                }

                using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable2)))
                using (var array = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(10, array.Length);
                    for (int i = 0; i < array.Length; ++i)
                    {
                        var expectedIndexInChunk = i;
                        var expectedValue = entitiesB[expectedIndexInChunk];
                        Assert.AreEqual(expectedValue, array[i]);
                    }
                }

                using (var query = new EntityQueryBuilder(Allocator.Temp)
                           .WithAll<EcsTestData>()
                           .WithNone<EcsTestDataEnableable>()
                           .Build(m_Manager))
                using (var array = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(11, array.Length);
                    Assert.AreEqual(entitiesA[3], array[0]);
                    for (int i = 1; i < array.Length; ++i)
                    {
                        var expectedIndexInChunk = i - 1;
                        var expectedValue = entitiesB[expectedIndexInChunk];
                        Assert.AreEqual(expectedValue, array[i]);
                    }
                }
            }
        }

        [Test]
        public void CopyFromComponentDataArray_RespectsEnabledBits()
        {
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            var archetypeB = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable2));

            var entityCount = 10;
            using (var entitiesA = m_Manager.CreateEntity(archetypeA, entityCount, World.UpdateAllocator.ToAllocator))
            using (var entitiesB = m_Manager.CreateEntity(archetypeB, entityCount, World.UpdateAllocator.ToAllocator))
            {
                // Reset component data in chunk
                for (int i = 0; i < entityCount; ++i)
                {
                    m_Manager.SetComponentData(entitiesA[i], new EcsTestData(-1));
                    m_Manager.SetComponentData(entitiesB[i], new EcsTestData(-1));
                }
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entitiesA[3], false);

                var arrayA = CollectionHelper.CreateNativeArray<EcsTestData>(9, World.UpdateAllocator.ToAllocator);
                using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestDataEnableable)))
                {
                    for (int i = 0; i < arrayA.Length; ++i)
                    {
                        arrayA[i] = new EcsTestData(i);
                    }

                    Assert.DoesNotThrow(()=>
                    {
                        query.CopyFromComponentDataArray(arrayA);
                    });

                    for (int i = 0; i < entitiesA.Length; ++i)
                    {
                        var expectedValue = i == 3 ? -1 : i > 3 ? i - 1 : i;
                        Assert.AreEqual(expectedValue, m_Manager.GetComponentData<EcsTestData>(entitiesA[i]).value);
                    }
                }
                arrayA.Dispose();

                // Reset component data in chunk
                for (int i = 0; i < entityCount; ++i)
                {
                    m_Manager.SetComponentData(entitiesA[i], new EcsTestData(-1));
                    m_Manager.SetComponentData(entitiesB[i], new EcsTestData(-1));
                }

                var arrayB = CollectionHelper.CreateNativeArray<EcsTestData>(10, World.UpdateAllocator.ToAllocator);
                using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestDataEnableable2)))
                {
                    for (int i = 0; i < arrayB.Length; ++i)
                    {
                        arrayB[i] = new EcsTestData(i);
                    }

                    Assert.DoesNotThrow(()=>
                    {
                        query.CopyFromComponentDataArray(arrayB);
                    });

                    for (int i = 0; i < entitiesB.Length; ++i)
                    {
                        var expectedValue = i;
                        Assert.AreEqual(expectedValue, m_Manager.GetComponentData<EcsTestData>(entitiesB[i]).value);
                    }
                }
                arrayB.Dispose();

                // Reset component data in chunk
                for (int i = 0; i < entityCount; ++i)
                {
                    m_Manager.SetComponentData(entitiesA[i], new EcsTestData(-1));
                    m_Manager.SetComponentData(entitiesB[i], new EcsTestData(-1));
                }

                var arrayC = CollectionHelper.CreateNativeArray<EcsTestData>(11, World.UpdateAllocator.ToAllocator);
                using (var query = new EntityQueryBuilder(Allocator.Temp)
                           .WithAll<EcsTestData>()
                           .WithNone<EcsTestDataEnableable>()
                           .Build(m_Manager))
                {
                    for (int i = 0; i < arrayC.Length; ++i)
                    {
                        arrayC[i] = new EcsTestData(i);
                    }

                    Assert.DoesNotThrow(()=>
                    {
                        query.CopyFromComponentDataArray(arrayC);
                    });

                    for (int i = 0; i < entitiesA.Length; ++i)
                    {
                        var expectedValue = i == 3 ? 0 : -1;
                        Assert.AreEqual(expectedValue, m_Manager.GetComponentData<EcsTestData>(entitiesA[i]).value);
                    }

                    for (int i = 0; i < entitiesB.Length; ++i)
                    {
                        var expectedValue = i + 1;
                        Assert.AreEqual(expectedValue, m_Manager.GetComponentData<EcsTestData>(entitiesB[i]).value);
                    }
                }
                arrayC.Dispose();
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public unsafe void SetEnabledBitsOnAllChunks_TypeNotInQuery_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp), typeof(EcsTestDataEnableable));
            Assert.Throws<InvalidOperationException>(() => m_Manager.SetComponentEnabled<EcsTestDataEnableable3>(query, false));
        }

        [Test]
        public void SetEnabledBitsOnAllChunks_WithoutFilter_Works()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2), typeof(EcsTestData3));
            var archetype4 = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2), typeof(EcsTestData4));
            int entitiesPerArchetype = 1000;
            m_Manager.CreateEntity(archetype1, entitiesPerArchetype);
            m_Manager.CreateEntity(archetype2, entitiesPerArchetype);
            m_Manager.CreateEntity(archetype3, entitiesPerArchetype);
            m_Manager.CreateEntity(archetype4, entitiesPerArchetype);
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp), typeof(EcsTestDataEnableable));
            Assert.AreEqual(4*entitiesPerArchetype, query.CalculateEntityCount());
            using var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);

            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(query, false);
            foreach (var ent in entities)
            {
                Assert.IsFalse(m_Manager.IsComponentEnabled<EcsTestDataEnableable>(ent));
                Assert.IsTrue(m_Manager.IsComponentEnabled<EcsTestDataEnableable2>(ent));
            }

            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(query, true);
            foreach (var ent in entities)
            {
                Assert.IsTrue(m_Manager.IsComponentEnabled<EcsTestDataEnableable>(ent));
                Assert.IsTrue(m_Manager.IsComponentEnabled<EcsTestDataEnableable2>(ent));
            }
        }

        [Test]
        public void SetEnabledBitsOnAllChunks_WithFilter_Works()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2), typeof(EcsTestData3));
            var archetype4 = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2), typeof(EcsTestData4));
            int entitiesPerArchetype = 1000;
            m_Manager.CreateEntity(archetype1, entitiesPerArchetype);
            m_Manager.CreateEntity(archetype2, entitiesPerArchetype);
            m_Manager.CreateEntity(archetype3, entitiesPerArchetype);
            m_Manager.CreateEntity(archetype4, entitiesPerArchetype);
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp), typeof(EcsTestDataEnableable));
            Assert.AreEqual(4*entitiesPerArchetype, query.CalculateEntityCount());
            using var entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
            var filterValue = new EcsTestSharedComp { value = 17 };
            for(int i=0; i<entities.Length; ++i)
            {
                if (i % 4 == 0)
                    m_Manager.SetSharedComponentManaged(entities[i], filterValue);
            }
            query.SetSharedComponentFilterManaged(filterValue);

            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(query, false);
            foreach (var ent in entities)
            {
                var sharedValue = m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(ent);
                Assert.AreNotEqual(sharedValue.value == filterValue.value, m_Manager.IsComponentEnabled<EcsTestDataEnableable>(ent));
                Assert.IsTrue(m_Manager.IsComponentEnabled<EcsTestDataEnableable2>(ent));
            }

            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(query, true);
            foreach (var ent in entities)
            {
                Assert.IsTrue(m_Manager.IsComponentEnabled<EcsTestDataEnableable>(ent));
                Assert.IsTrue(m_Manager.IsComponentEnabled<EcsTestDataEnableable2>(ent));
            }
        }
    }
}
