using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using System.Linq;
using Unity.Burst;
using Unity.Burst.Intrinsics;

namespace Unity.Entities.Tests
{
    using static AspectUtils;
    [TestFixture]
    partial class EntityQueryTests : ECSTestsFixture
    {
        public enum EntityQueryJobMode
        {
            Immediate,
            Async,
            AsyncObsolete, // removed after Entities 1.0
        };

        ArchetypeChunk[] CreateEntitiesAndReturnChunks(EntityArchetype archetype, int entityCount, Action<Entity> action = null)
        {
            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            m_Manager.CreateEntity(archetype, entities);
            var managedEntities = entities.ToArray();
            entities.Dispose();

            if (action != null)
                foreach (var e in managedEntities)
                    action(e);

            return managedEntities.Select(e => m_Manager.GetChunk(e)).Distinct().ToArray();
        }

        private unsafe T[] ToManagedArray<T>(T* values, int length) where T : unmanaged
        {
            var array = new T[length];
            for (int i = 0; i < length; ++i)
                array[i] = values[i];
            return array;
        }

        private unsafe EntityTypeHandle GetEntityTypeHandle(EntityQuery query)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandles = query._GetImpl()->SafetyHandles;
            var entityType = new EntityTypeHandle(safetyHandles->GetSafetyHandleForEntityTypeHandle());
#else
            var entityType = new EntityTypeHandle();
#endif
            return entityType;
        }

        private unsafe ComponentTypeHandle<T> GetComponentTypeHandle<T>(EntityQuery query)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var impl = query._GetImpl();
            var safetyHandles = impl->SafetyHandles;
            var componentType = new ComponentTypeHandle<T>(
                safetyHandles->GetSafetyHandleForComponentTypeHandle(TypeManager.GetTypeIndex<T>(), true),
                true, impl->_Access->EntityComponentStore->GlobalSystemVersion);
#else
            var componentType = new ComponentTypeHandle<T>();
#endif
            return componentType;
        }

        private unsafe UnsafeMatchingArchetypePtrList GetMatchingArchetypes(EntityQuery query)
        {
            var impl = query._GetImpl();
            return impl->_QueryData->MatchingArchetypes;
        }

        [Test]
        public void ResetFilter_Works()
        {
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestSharedComp, EcsTestData>()
                .Build(m_Manager);
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp));
            Assert.AreEqual(1, query.CalculateChunkCount(), "Shared component filter not working as expected");

            query.SetSharedComponentFilter(new EcsTestSharedComp(17));
            Assert.AreEqual(0, query.CalculateChunkCount());
            query.ResetFilter();
            Assert.AreEqual(1, query.CalculateChunkCount(), "ResetFilter did not clear shared component filter");

            query.AddChangedVersionFilter(typeof(EcsTestData));
            query.SetChangedFilterRequiredVersion(m_Manager.GlobalSystemVersion);
            m_ManagerDebug.IncrementGlobalSystemVersion();
            Assert.AreEqual(0, query.CalculateChunkCount(), "changed filter not working as expected");
            query.ResetFilter();
            Assert.AreEqual(1, query.CalculateChunkCount(), "ResetFilter did not clear change filter");

            query.AddOrderVersionFilter();
            query.SetChangedFilterRequiredVersion(m_Manager.GlobalSystemVersion);
            m_ManagerDebug.IncrementGlobalSystemVersion();
            Assert.AreEqual(0, query.CalculateChunkCount(), "order filter not working as expected");
            query.ResetFilter();
            Assert.AreEqual(1, query.CalculateChunkCount(), "ResetFilter did not clear order filter");
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks()]
        public void SetSharedComponentFilter_ComponentNotRequired_Throws()
        {
            var filterValue = new EcsTestSharedComp(17);
            // Setting a shared component filter for a component that is required in the query description should work.
            using var queryAll = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>().Build(m_Manager);
            Assert.DoesNotThrow(() => queryAll.SetSharedComponentFilter(filterValue));
            // WithDisabled<T>() already throws if T is not enableable, and shared components can't currently be enableable, so this
            // use case is impossible.
            //using var queryDisabled = new EntityQueryBuilder(Allocator.Temp).WithDisabled<EcsTestSharedComp>().Build(m_Manager);
            //Assert.DoesNotThrow(() => queryDisabled.SetSharedComponentFilter(filterValue));
            using var queryPresent = new EntityQueryBuilder(Allocator.Temp).WithPresent<EcsTestSharedComp>().Build(m_Manager);
            Assert.DoesNotThrow(() => queryPresent.SetSharedComponentFilter(filterValue));
            // For components that are not required, setting a shared component filter should throw.
            using var queryAny = new EntityQueryBuilder(Allocator.Temp).WithAny<EcsTestSharedComp,EcsTestSharedComp2>().Build(m_Manager);
            Assert.Throws<InvalidOperationException>(() => queryAny.SetSharedComponentFilter(filterValue));
            using var queryNone = new EntityQueryBuilder(Allocator.Temp).WithNone<EcsTestSharedComp>().Build(m_Manager);
            Assert.Throws<InvalidOperationException>(() => queryNone.SetSharedComponentFilter(filterValue));
            using var queryAbsent = new EntityQueryBuilder(Allocator.Temp).WithAbsent<EcsTestSharedComp>().Build(m_Manager);
            Assert.Throws<InvalidOperationException>(() => queryAbsent.SetSharedComponentFilter(filterValue));
        }

        [Test]
        public void ToArchetypeChunkArray_Works()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData2));
            var archetype12 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, 5000);
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, 5000);
            var createdChunks12 = CreateEntitiesAndReturnChunks(archetype12, 5000);

            var allCreatedChunks = createdChunks1.Concat(createdChunks2).Concat(createdChunks12);

            using var query1 = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            using var query12 = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2));

            var queriedChunks1 = query1.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            var queriedChunks12 = query12.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            var queriedChunksAll = m_Manager.GetAllChunks(World.UpdateAllocator.ToAllocator);
            CollectionAssert.AreEqual(createdChunks1.Concat(createdChunks12), queriedChunks1);
            CollectionAssert.AreEqual(createdChunks12, queriedChunks12);
            CollectionAssert.AreEqual(allCreatedChunks, queriedChunksAll);

            // make sure "empty" chunks aren't returned
            var archetypeE = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            int expectedChunkCountE = 10;
            var createChunksE = CreateEntitiesAndReturnChunks(archetypeE, archetypeE.ChunkCapacity * expectedChunkCountE);
            using var queryE = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(queryE, false);
            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            createChunksE[0].SetComponentEnabled(ref typeHandle, 0, true); // enable one entity in first chunk
            FastAssert.AreEqual(expectedChunkCountE, queryE.CalculateChunkCountWithoutFiltering());
            var queriedChunksE = queryE.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            CollectionAssert.AreEqual(new ArchetypeChunk[] { createChunksE[0] }, queriedChunksE);
        }

        [Test]
        public void ToArchetypeChunkListAsync_Works()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData2));
            var archetype12 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, 5000);
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, 5000);
            var createdChunks12 = CreateEntitiesAndReturnChunks(archetype12, 5000);

            var allCreatedChunks = createdChunks1.Concat(createdChunks2).Concat(createdChunks12);

            var query1 = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var query12 = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2));

            var queriedChunks1 = query1.ToArchetypeChunkListAsync(World.UpdateAllocator.ToAllocator, out var gatherChunksJob1);
            var queriedChunks12 = query12.ToArchetypeChunkListAsync(World.UpdateAllocator.ToAllocator, out var gatherChunksJob2);
            gatherChunksJob1.Complete();
            CollectionAssert.AreEqual(createdChunks1.Concat(createdChunks12), queriedChunks1.AsArray().ToArray());
            gatherChunksJob2.Complete();
            CollectionAssert.AreEqual(createdChunks12, queriedChunks12.AsArray().ToArray());

            var queriedChunksAll = m_Manager.GetAllChunks(World.UpdateAllocator.ToAllocator);
            CollectionAssert.AreEqual(allCreatedChunks, queriedChunksAll);

            // make sure "empty" chunks aren't returned
            var archetypeE = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            int expectedChunkCountE = 10;
            var createChunksE = CreateEntitiesAndReturnChunks(archetypeE, archetypeE.ChunkCapacity * expectedChunkCountE);
            using var queryE = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(queryE, false);
            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            createChunksE[0].SetComponentEnabled(ref typeHandle, 0, true); // enable one entity in first chunk
            FastAssert.AreEqual(expectedChunkCountE, queryE.CalculateChunkCountWithoutFiltering());
            var queriedChunksE = queryE.ToArchetypeChunkListAsync(World.UpdateAllocator.ToAllocator, out var gatherChunksJobE);
            gatherChunksJobE.Complete();
            CollectionAssert.AreEqual(new ArchetypeChunk[] { createChunksE[0] }, queriedChunksE.AsArray().ToArray());
        }

        void SetShared(Entity e, int i)
        {
            m_Manager.SetSharedComponentManaged(e, new EcsTestSharedComp(i));
        }

        [Test]
        public void ToArchetypeChunkArray_FiltersSharedComponents()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData2), typeof(EcsTestSharedComp));

            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, 5000, e => SetShared(e, 1));
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, 5000, e => SetShared(e, 1));
            var createdChunks3 = CreateEntitiesAndReturnChunks(archetype1, 5000, e => SetShared(e, 2));
            var createdChunks4 = CreateEntitiesAndReturnChunks(archetype2, 5000, e => SetShared(e, 2));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp));

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp(1));

            var queriedChunks1 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp(2));

            var queriedChunks2 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            CollectionAssert.AreEquivalent(createdChunks1.Concat(createdChunks2), queriedChunks1);
            CollectionAssert.AreEquivalent(createdChunks3.Concat(createdChunks4), queriedChunks2);

            query.Dispose();
        }

        void SetShared(Entity e, int i, int j)
        {
            m_Manager.SetSharedComponentManaged(e, new EcsTestSharedComp(i));
            m_Manager.SetSharedComponentManaged(e, new EcsTestSharedComp2(j));
        }

        [Test]
        public void ToArchetypeChunkArray_FiltersTwoSharedComponents()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData2), typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2));

            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, 5000, e => SetShared(e, 1, 7));
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, 5000, e => SetShared(e, 1, 7));
            var createdChunks3 = CreateEntitiesAndReturnChunks(archetype1, 5000, e => SetShared(e, 2, 7));
            var createdChunks4 = CreateEntitiesAndReturnChunks(archetype2, 5000, e => SetShared(e, 2, 7));
            var createdChunks5 = CreateEntitiesAndReturnChunks(archetype1, 5000, e => SetShared(e, 1, 8));
            var createdChunks6 = CreateEntitiesAndReturnChunks(archetype2, 5000, e => SetShared(e, 1, 8));
            var createdChunks7 = CreateEntitiesAndReturnChunks(archetype1, 5000, e => SetShared(e, 2, 8));
            var createdChunks8 = CreateEntitiesAndReturnChunks(archetype2, 5000, e => SetShared(e, 2, 8));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2));

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp(1), new EcsTestSharedComp2(7));
            var queriedChunks1 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp(2), new EcsTestSharedComp2(7));
            var queriedChunks2 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp(1), new EcsTestSharedComp2(8));
            var queriedChunks3 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp(2), new EcsTestSharedComp2(8));
            var queriedChunks4 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);


            CollectionAssert.AreEquivalent(createdChunks1.Concat(createdChunks2), queriedChunks1);
            CollectionAssert.AreEquivalent(createdChunks3.Concat(createdChunks4), queriedChunks2);
            CollectionAssert.AreEquivalent(createdChunks5.Concat(createdChunks6), queriedChunks3);
            CollectionAssert.AreEquivalent(createdChunks7.Concat(createdChunks8), queriedChunks4);

            query.Dispose();
        }

        void SetData(Entity e, int i)
        {
            m_Manager.SetComponentData(e, new EcsTestData(i));
        }

        [Test]
        public void ToArchetypeChunkArray_FiltersChangeVersions()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData3));

            m_ManagerDebug.SetGlobalSystemVersion(20);
            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, 5000, e => SetData(e, 1));
            m_ManagerDebug.SetGlobalSystemVersion(30);
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, 5000, e => SetData(e, 2));
            m_ManagerDebug.SetGlobalSystemVersion(40);
            var createdChunks3 = CreateEntitiesAndReturnChunks(archetype3, 5000, e => SetData(e, 3));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            query.SetChangedVersionFilter(typeof(EcsTestData));

            query.SetChangedFilterRequiredVersion(10);
            var queriedChunks1 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(20);
            var queriedChunks2 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(30);
            var queriedChunks3 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(40);
            var queriedChunks4 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            CollectionAssert.AreEquivalent(createdChunks1.Concat(createdChunks2).Concat(createdChunks3), queriedChunks1);
            CollectionAssert.AreEquivalent(createdChunks2.Concat(createdChunks3), queriedChunks2);
            CollectionAssert.AreEquivalent(createdChunks3, queriedChunks3);

            Assert.AreEqual(0, queriedChunks4.Length);

            query.Dispose();
        }

        void SetData(Entity e, int i, int j)
        {
            m_Manager.SetComponentData(e, new EcsTestData(i));
            m_Manager.SetComponentData(e, new EcsTestData2(j));
        }

        [Test]
        public void ToArchetypeChunkArray_FiltersTwoChangeVersions()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData4));

            m_ManagerDebug.SetGlobalSystemVersion(20);
            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, 5000, e => SetData(e, 1, 4));
            m_ManagerDebug.SetGlobalSystemVersion(30);
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, 5000, e => SetData(e, 2, 5));
            m_ManagerDebug.SetGlobalSystemVersion(40);
            var createdChunks3 = CreateEntitiesAndReturnChunks(archetype3, 5000, e => SetData(e, 3, 6));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2));

            query.SetChangedVersionFilter(new ComponentType[] {typeof(EcsTestData), typeof(EcsTestData2)});

            query.SetChangedFilterRequiredVersion(30);

            var testType1 = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
            var testType2 = m_Manager.GetComponentTypeHandle<EcsTestData2>(false);

            var queriedChunks1 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            foreach (var chunk in createdChunks1)
            {
                var array = chunk.GetNativeArray(ref testType1);
                array[0] = new EcsTestData(7);
            }

            var queriedChunks2 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            foreach (var chunk in createdChunks2)
            {
                var array = chunk.GetNativeArray(ref testType2);
                array[0] = new EcsTestData2(7);
            }

            var queriedChunks3 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);


            CollectionAssert.AreEquivalent(createdChunks3, queriedChunks1);
            CollectionAssert.AreEquivalent(createdChunks1.Concat(createdChunks3), queriedChunks2);

            query.Dispose();
        }

        [Test]
        public void ToArchetypeChunkArray_FiltersOrderVersions()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData3));

            m_ManagerDebug.SetGlobalSystemVersion(20);
            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, 5000, e => SetData(e, 1));
            m_ManagerDebug.SetGlobalSystemVersion(30);
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, 5000, e => SetData(e, 2));
            m_ManagerDebug.SetGlobalSystemVersion(40);
            var createdChunks3 = CreateEntitiesAndReturnChunks(archetype3, 5000, e => SetData(e, 3));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            query.SetOrderVersionFilter();

            query.SetChangedFilterRequiredVersion(10);
            var queriedChunks1 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(20);
            var queriedChunks2 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(30);
            var queriedChunks3 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(40);
            var queriedChunks4 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            CollectionAssert.AreEquivalent(createdChunks1.Concat(createdChunks2).Concat(createdChunks3), queriedChunks1);
            CollectionAssert.AreEquivalent(createdChunks2.Concat(createdChunks3), queriedChunks2);
            CollectionAssert.AreEquivalent(createdChunks3, queriedChunks3);

            Assert.AreEqual(0, queriedChunks4.Length);

            query.Dispose();
        }

        [Test]
        public void ToArchetypeChunkArray_FiltersOrderAndChangedVersions()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData3));

            m_ManagerDebug.SetGlobalSystemVersion(20);
            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, 5000, e => SetData(e, 1));
            m_ManagerDebug.SetGlobalSystemVersion(30);
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, 5000, e => SetData(e, 2));
            m_ManagerDebug.SetGlobalSystemVersion(40);
            var createdChunks3 = CreateEntitiesAndReturnChunks(archetype3, 5000, e => SetData(e, 3));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            query.SetOrderVersionFilter();
            query.SetChangedVersionFilter(typeof(EcsTestData));

            query.SetChangedFilterRequiredVersion(10);
            var queriedChunks1 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(20);
            var queriedChunks2 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(30);
            var queriedChunks3 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(40);
            var queriedChunks4 =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            CollectionAssert.AreEquivalent(createdChunks1.Concat(createdChunks2).Concat(createdChunks3), queriedChunks1);
            CollectionAssert.AreEquivalent(createdChunks2.Concat(createdChunks3), queriedChunks2);
            CollectionAssert.AreEquivalent(createdChunks3, queriedChunks3);

            Assert.AreEqual(0, queriedChunks4.Length);

            query.Dispose();
        }

        void SetDataAndShared(Entity e, int data, int shared)
        {
            SetData(e, data);
            SetShared(e, shared);
        }

        [Test]
        public void ToArchetypeChunkArray_FiltersOneSharedOneChangeVersion()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));

            // 9 chunks
            // 3 of archetype1 with 1 shared value
            // 3 of archetype2 with 1 shared value
            // 3 of archetype1 with 2 shared value
            m_ManagerDebug.IncrementGlobalSystemVersion();
            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, archetype1.ChunkCapacity * 3, e => SetDataAndShared(e, 1, 1));
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, archetype2.ChunkCapacity * 3, e => SetDataAndShared(e, 2, 1));
            var createdChunks3 = CreateEntitiesAndReturnChunks(archetype1, archetype1.ChunkCapacity * 3, e => SetDataAndShared(e, 3, 2));

            // query matches all three
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp));

            query.AddChangedVersionFilter(typeof(EcsTestData));
            query.AddSharedComponentFilterManaged(new EcsTestSharedComp {value = 1});

            var queriedChunks1 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(m_Manager.GlobalSystemVersion);
            var queriedChunks2 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            // bumps the version number for TestData1 for createdChunks1
            m_ManagerDebug.IncrementGlobalSystemVersion();
            var typeHandle1 = EmptySystem.GetComponentTypeHandle<EcsTestData>();
            for (int i = 0; i < createdChunks1.Length; ++i)
            {
                var array = createdChunks1[i].GetNativeArray(ref typeHandle1);
                array[0] = new EcsTestData {value = 10};
            }
            var queriedChunks3 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            // bumps the version number for TestData2
            query.SetChangedFilterRequiredVersion(m_Manager.GlobalSystemVersion);
            m_ManagerDebug.IncrementGlobalSystemVersion();
            var typeHandle2 = EmptySystem.GetComponentTypeHandle<EcsTestData2>();
            for (int i = 0; i < createdChunks1.Length; ++i)
            {
                var array = createdChunks1[i].GetNativeArray(ref typeHandle2);
                array[0] = new EcsTestData2 {value1 = 10, value0 = 10};
            }
            var queriedChunks4 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            CollectionAssert.AreEquivalent(createdChunks1.Concat(createdChunks2), queriedChunks1); // query 1 = created 1,2
            Assert.AreEqual(0, queriedChunks2.Length); // query 2 is empty
            CollectionAssert.AreEquivalent(createdChunks1, queriedChunks3); // query 3 = created 1 (version # was bumped)
            Assert.AreEqual(0, queriedChunks4.Length); // query 4 is empty (version # of type we're not change tracking was bumped)

            query.Dispose();
        }

        [Test]
        public void ToArchetypeChunkArray_FiltersOneSharedOrderVersion()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));

            // 9 chunks
            // 3 of archetype1 with 1 shared value
            // 3 of archetype2 with 1 shared value
            // 3 of archetype1 with 2 shared value
            m_ManagerDebug.SetGlobalSystemVersion(10);
            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, archetype1.ChunkCapacity * 3, e => SetDataAndShared(e, 1, 1));
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, archetype2.ChunkCapacity * 3, e => SetDataAndShared(e, 2, 1));
            var createdChunks3 = CreateEntitiesAndReturnChunks(archetype1, archetype1.ChunkCapacity * 3, e => SetDataAndShared(e, 3, 2));

            // query matches all three
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp));

            query.AddOrderVersionFilter();
            query.AddSharedComponentFilterManaged(new EcsTestSharedComp {value = 1});

            var queriedChunks1 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(10);
            var queriedChunks2 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            // bumps the order version number for createdChunks1
            m_ManagerDebug.SetGlobalSystemVersion(20);
            for (int i = 0; i < createdChunks1.Length; ++i)
            {
                var array = createdChunks1[i].GetNativeArray(EmptySystem.GetEntityTypeHandle());
                m_Manager.AddComponent(array, typeof(EcsTestTag));
            }
            var queriedChunks3 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            CollectionAssert.AreEquivalent(createdChunks1.Concat(createdChunks2), queriedChunks1); // query 1 = created 1,2
            Assert.AreEqual(0, queriedChunks2.Length); // query 2 is empty
            Assert.AreEqual(createdChunks1.Length, queriedChunks3.Length); // query 3 = created 1 (version # was bumped) (not collection equivalent because it is a new archetype so the chunk *has* changed)

            query.Dispose();
        }

        [Test]
        public void ToArchetypeChunkArray_FiltersOneSharedTwoChangeVersion()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestSharedComp));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            // 9 chunks
            // 3 of archetype1 with 1 shared value
            // 3 of archetype2 with 1 shared value
            // 3 of archetype1 with 2 shared value
            m_ManagerDebug.SetGlobalSystemVersion(10);
            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, archetype1.ChunkCapacity * 3, e => SetDataAndShared(e, 1, 1));
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, archetype2.ChunkCapacity * 3, e => SetDataAndShared(e, 2, 1));
            var createdChunks3 = CreateEntitiesAndReturnChunks(archetype1, archetype1.ChunkCapacity * 3, e => SetDataAndShared(e, 3, 2));

            // query matches all three
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            query.AddChangedVersionFilter(typeof(EcsTestData));
            query.AddChangedVersionFilter(typeof(EcsTestData2));
            query.AddSharedComponentFilterManaged(new EcsTestSharedComp {value = 1});

            var queriedChunks1 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(10);
            var queriedChunks2 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            // bumps the version number for TestData1 for createdChunks1
            m_ManagerDebug.SetGlobalSystemVersion(20);
            var typeHandle1 = EmptySystem.GetComponentTypeHandle<EcsTestData>();
            for (int i = 0; i < createdChunks1.Length; ++i)
            {
                var array = createdChunks1[i].GetNativeArray(ref typeHandle1);
                array[0] = new EcsTestData {value = 10};
            }
            var queriedChunks3 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            // bumps the version number for TestData2
            query.SetChangedFilterRequiredVersion(20);
            m_ManagerDebug.SetGlobalSystemVersion(30);
            var typeHandle2 = EmptySystem.GetComponentTypeHandle<EcsTestData2>();
            for (int i = 0; i < createdChunks1.Length; ++i)
            {
                var array = createdChunks1[i].GetNativeArray(ref typeHandle2);
                array[0] = new EcsTestData2 {value1 = 10, value0 = 10};
            }
            var queriedChunks4 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            CollectionAssert.AreEquivalent(createdChunks1.Concat(createdChunks2), queriedChunks1); // query 1 = created 1,2
            Assert.AreEqual(0, queriedChunks2.Length); // query 2 is empty
            CollectionAssert.AreEquivalent(createdChunks1, queriedChunks3); // query 3 = created 1 (version # of type1 was bumped)
            CollectionAssert.AreEquivalent(createdChunks1, queriedChunks4); // query 4 = created 1 (version # of type2 was bumped)

            query.Dispose();
        }

        void SetDataAndShared(Entity e, int data, int shared1, int shared2)
        {
            SetData(e, data);
            SetShared(e, shared1, shared2);
        }

        [Test]
        public void ToArchetypeChunkArray_FiltersTwoSharedOneChangeVersion()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2));

            // 9 chunks
            // 3 of archetype1 with 1 shared value1, 3,3 shared value2
            // 3 of archetype2 with 1 shared value1, 4,4 shared value2
            // 3 of archetype1 with 2 shared value1, 3,3 shared value2
            m_ManagerDebug.IncrementGlobalSystemVersion();
            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, archetype1.ChunkCapacity * 3, e => SetDataAndShared(e, 1, 1, 3));
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, archetype2.ChunkCapacity * 3, e => SetDataAndShared(e, 2, 1, 4));
            var createdChunks3 = CreateEntitiesAndReturnChunks(archetype1, archetype1.ChunkCapacity * 3, e => SetDataAndShared(e, 3, 2, 3));

            // query matches all three
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2));

            query.AddChangedVersionFilter(typeof(EcsTestData));
            query.AddSharedComponentFilterManaged(new EcsTestSharedComp {value = 1});
            query.AddSharedComponentFilterManaged(new EcsTestSharedComp2 {value0 = 3, value1 = 3});

            var queriedChunks1 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(m_Manager.GlobalSystemVersion);
            var queriedChunks2 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            // bumps the version number for TestData1 for createdChunks1 and createdChunks2
            m_ManagerDebug.IncrementGlobalSystemVersion();
            var typeHandle1 = EmptySystem.GetComponentTypeHandle<EcsTestData>();
            for (int i = 0; i < createdChunks1.Length; ++i)
            {
                {
                    var array = createdChunks1[i].GetNativeArray(ref typeHandle1);
                    array[0] = new EcsTestData {value = 10};
                }
                {
                    var array = createdChunks3[i].GetNativeArray(ref typeHandle1);
                    array[0] = new EcsTestData {value = 10};
                }
            }
            var queriedChunks3 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            // bumps the version number for TestData2 for createdChunks1
            query.SetChangedFilterRequiredVersion(m_Manager.GlobalSystemVersion);
            m_ManagerDebug.IncrementGlobalSystemVersion();
            var typeHandle2 = EmptySystem.GetComponentTypeHandle<EcsTestData2>();
            for (int i = 0; i < createdChunks1.Length; ++i)
            {
                var array = createdChunks1[i].GetNativeArray(ref typeHandle2);
                array[0] = new EcsTestData2 {value1 = 10, value0 = 10};
            }
            var queriedChunks4 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            CollectionAssert.AreEquivalent(createdChunks1, queriedChunks1); // query 1 = created 1
            Assert.AreEqual(0, queriedChunks2.Length); // query 2 is empty
            CollectionAssert.AreEquivalent(createdChunks1, queriedChunks3); // query 3 = created 1 (version # was bumped and we're filtering out created2)
            Assert.AreEqual(0, queriedChunks4.Length); // query 4 is empty (version # of type we're not change tracking was bumped)

            query.Dispose();
        }

        [Test]
        public void ToArchetypeChunkArray_FiltersTwoSharedTwoChangeVersion()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2));

            // 9 chunks
            // 3 of archetype1 with 1 shared value1, 3,3 shared value2
            // 3 of archetype2 with 1 shared value1, 4,4 shared value2
            // 3 of archetype1 with 2 shared value1, 3,3 shared value2
            m_ManagerDebug.SetGlobalSystemVersion(10);
            var createdChunks1 = CreateEntitiesAndReturnChunks(archetype1, archetype1.ChunkCapacity * 3, e => SetDataAndShared(e, 1, 1, 3));
            var createdChunks2 = CreateEntitiesAndReturnChunks(archetype2, archetype2.ChunkCapacity * 3, e => SetDataAndShared(e, 2, 1, 4));
            var createdChunks3 = CreateEntitiesAndReturnChunks(archetype1, archetype1.ChunkCapacity * 3, e => SetDataAndShared(e, 3, 2, 3));

            // query matches all three
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2));

            query.AddChangedVersionFilter(typeof(EcsTestData));
            query.AddChangedVersionFilter(typeof(EcsTestData2));
            query.AddSharedComponentFilterManaged(new EcsTestSharedComp {value = 1});
            query.AddSharedComponentFilterManaged(new EcsTestSharedComp2 {value0 = 3, value1 = 3});

            var queriedChunks1 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            query.SetChangedFilterRequiredVersion(10);
            var queriedChunks2 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            // bumps the version number for TestData1 for createdChunks1 and createdChunks2
            m_ManagerDebug.SetGlobalSystemVersion(20);
            var typeHandle1 = EmptySystem.GetComponentTypeHandle<EcsTestData>();
            for (int i = 0; i < createdChunks1.Length; ++i)
            {
                {
                    var array = createdChunks1[i].GetNativeArray(ref typeHandle1);
                    array[0] = new EcsTestData {value = 10};
                }
                {
                    var array = createdChunks3[i].GetNativeArray(ref typeHandle1);
                    array[0] = new EcsTestData {value = 10};
                }
            }
            var queriedChunks3 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            // bumps the version number for TestData2 for createdChunks1
            query.SetChangedFilterRequiredVersion(20);
            m_ManagerDebug.SetGlobalSystemVersion(30);
            var typeHandle2 = EmptySystem.GetComponentTypeHandle<EcsTestData2>();
            for (int i = 0; i < createdChunks1.Length; ++i)
            {
                var array = createdChunks1[i].GetNativeArray(ref typeHandle2);
                array[0] = new EcsTestData2 {value1 = 10, value0 = 10};
            }
            var queriedChunks4 = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            CollectionAssert.AreEquivalent(createdChunks1, queriedChunks1); // query 1 = created 1
            Assert.AreEqual(0, queriedChunks2.Length); // query 2 is empty
            CollectionAssert.AreEquivalent(createdChunks1, queriedChunks3); // query 3 = created 1 (version # was bumped and we're filtering out created2)
            CollectionAssert.AreEquivalent(createdChunks1, queriedChunks4); // query 4 = created 1 (version # of type2 was bumped)

            query.Dispose();
        }

        [Test]
        public void FiltersOrderVersion()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));

            m_ManagerDebug.SetGlobalSystemVersion(10);
            var entities = m_Manager.CreateEntity(archetype, archetype.ChunkCapacity, Allocator.Temp);

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            Assert.AreEqual(1, query.CalculateChunkCount());

            query.SetChangedFilterRequiredVersion(10);
            query.SetOrderVersionFilter();

            Assert.AreEqual(0, query.CalculateChunkCount());

            // "Other System runs"
            m_ManagerDebug.SetGlobalSystemVersion(11);
            for (int i = 0; i < archetype.ChunkCapacity; i += 2)
            {
                m_Manager.AddComponent(entities[i], typeof(EcsTestTag));
            }

            Assert.AreEqual(2, query.CalculateChunkCount());

            query.Dispose();
        }

        [Test]
        public void ChangeVersionFollowsEntity()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            query.SetChangedVersionFilter(typeof(EcsTestData));
            query.SetChangedFilterRequiredVersion(10);

            // "Entity is created"
            m_ManagerDebug.SetGlobalSystemVersion(10);
            var entity = m_Manager.CreateEntity(archetype);

            Assert.AreEqual(0, query.CalculateEntityCount());

            // "System runs"
            m_ManagerDebug.SetGlobalSystemVersion(11);
            m_Manager.SetComponentData(entity, new EcsTestData {value = 50});

            Assert.AreEqual(1, query.CalculateEntityCount());

            // "System runs"
            m_ManagerDebug.SetGlobalSystemVersion(12);
            m_Manager.SetComponentData(entity, new EcsTestData {value = 50});
            m_Manager.AddComponent(entity, typeof(EcsTestData2));

            Assert.AreEqual(1, query.CalculateEntityCount());

            query.Dispose();
        }

        // https://github.com/Unity-Technologies/dots/issues/1098
        [Test]
        [TestRequiresDotsDebugOrCollectionChecks()]
        public void TestIssue1098()
        {
            m_Manager.CreateEntity(typeof(EcsTestData));

            using var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestData>().Build(m_Manager);
            // NB: EcsTestData != EcsTestData2
            Assert.Throws<InvalidOperationException>(() => query.ToComponentDataArray<EcsTestData2>(World.UpdateAllocator.ToAllocator));
        }

        public partial class WriteEcsTestDataSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.ForEach((ref EcsTestData data ) => {  }).Schedule();
            }
        }

        public partial class WriteEcsTestDataEnableableSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities.ForEach((ref EcsTestDataEnableable data ) => {  }).Schedule();
            }
        }

        [Test]
        public unsafe void ToArchetypeChunkArray_SyncsChangeFilterTypes()
        {
            m_Manager.CreateEntity(typeof(EcsTestData));

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            query.SetChangedVersionFilter(typeof(EcsTestData));
            var ws1 = World.GetOrCreateSystemManaged<WriteEcsTestDataSystem>();
            ws1.Update();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandle = m_Manager.GetCheckedEntityDataAccess()->DependencyManager->Safety.GetSafetyHandle(TypeManager.GetTypeIndex<EcsTestData>(), false);
            Assert.Throws<InvalidOperationException>(() => AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle));
            var chunks =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle);
#else
            var chunks =  query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
#endif
            Assert.IsTrue(chunks.Length == 1);
        }

        [Test]
        public unsafe void CalculateEntityCount_SyncsChangeFilterTypes()
        {
            m_Manager.CreateEntity(typeof(EcsTestData));

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            query.SetChangedVersionFilter(typeof(EcsTestData));
            var ws1 = World.GetOrCreateSystemManaged<WriteEcsTestDataSystem>();
            ws1.Update();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandle = m_Manager.GetCheckedEntityDataAccess()->DependencyManager->Safety.GetSafetyHandle(TypeManager.GetTypeIndex<EcsTestData>(), false);

            Assert.Throws<InvalidOperationException>(() => AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle));
            var entityCount = query.CalculateEntityCount();
            AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle);
#else
            var entityCount = query.CalculateEntityCount();
#endif
            Assert.IsTrue(entityCount == 1);
        }

        [Test]
        public unsafe void CalculateEntityCount_SyncsEnableableTypes()
        {
            m_Manager.CreateEntity(typeof(EcsTestDataEnableable));

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            var ws1 = World.GetOrCreateSystemManaged<WriteEcsTestDataEnableableSystem>();
            ws1.Update();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandle = m_Manager.GetCheckedEntityDataAccess()->DependencyManager->Safety.GetSafetyHandle(TypeManager.GetTypeIndex<EcsTestDataEnableable>(), false);

            Assert.Throws<InvalidOperationException>(() => AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle));
            var entityCount = query.CalculateEntityCount();
            AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle);
#else
            var entityCount = query.CalculateEntityCount();
#endif
            Assert.IsTrue(entityCount == 1);
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public unsafe void IsEmpty_SyncsChangeFilterTypes()
        {
            m_Manager.CreateEntity(typeof(EcsTestData));

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            query.SetChangedVersionFilter(typeof(EcsTestData));
            var ws1 = World.GetOrCreateSystemManaged<WriteEcsTestDataSystem>();
            ws1.Update();

            var safetyHandle = m_Manager.GetCheckedEntityDataAccess()->DependencyManager->Safety.GetSafetyHandle(TypeManager.GetTypeIndex<EcsTestData>(), false);

            Assert.Throws<InvalidOperationException>(() => AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle));
            var dummy = query.IsEmpty;
            AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle);
        }
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public unsafe void IsEmpty_SyncsEnableableTypes()
        {
            m_Manager.CreateEntity(typeof(EcsTestDataEnableable));

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            var ws1 = World.GetOrCreateSystemManaged<WriteEcsTestDataEnableableSystem>();
            ws1.Update();

            var safetyHandle = m_Manager.GetCheckedEntityDataAccess()->DependencyManager->Safety.GetSafetyHandle(TypeManager.GetTypeIndex<EcsTestDataEnableable>(), false);

            Assert.Throws<InvalidOperationException>(() => AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle));
            var dummy = query.IsEmpty;
            AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle);
        }
#endif

        [Test]
        public unsafe void Matches_SyncsChangeFilterTypes()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            int entityCount = 1000;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);

            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            query.SetChangedVersionFilter(typeof(EcsTestData));
            var ws1 = World.GetOrCreateSystemManaged<WriteEcsTestDataSystem>();
            ws1.Update();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandle = m_Manager.GetCheckedEntityDataAccess()->DependencyManager->Safety.GetSafetyHandle(TypeManager.GetTypeIndex<EcsTestData>(), false);

            Assert.Throws<InvalidOperationException>(() => AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle));
            bool result = query.Matches(entities[0]);
            AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle);
#else
            bool result = query.Matches(entities[0]);
#endif
            Assert.IsTrue(result);
        }

        [Test]
        public unsafe void Matches_SyncsEnableableTypes()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            int entityCount = 1000;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);

            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            var ws1 = World.GetOrCreateSystemManaged<WriteEcsTestDataEnableableSystem>();
            ws1.Update();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safetyHandle = m_Manager.GetCheckedEntityDataAccess()->DependencyManager->Safety.GetSafetyHandle(TypeManager.GetTypeIndex<EcsTestDataEnableable>(), false);

            Assert.Throws<InvalidOperationException>(() => AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle));
            bool result = query.Matches(entities[0]);
            AtomicSafetyHandle.CheckWriteAndThrow(safetyHandle);
#else
            bool result = query.Matches(entities[0]);
#endif
            Assert.IsTrue(result);
        }

        [Test]
        public void Matches_WithFiltering_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable), typeof(EcsTestSharedComp));
            int entityCount = 1000;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            var chunkFilterValue = new EcsTestSharedComp { value = 17 };
            for (var i = 0; i < entityCount; ++i)
            {
                int entityVariant = i % 4;
                // For each group of 4 entities:
                // 0 has a required component disabled and fails the chunk filter
                // 1 fails the chunk filter
                // 2 has a required component disabled
                // 3 should match the query
                if (entityVariant == 0 || entityVariant == 2)
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                if (entityVariant == 2 || entityVariant == 3)
                    m_Manager.SetSharedComponent(entities[i], chunkFilterValue);
            }

            using var queryBuilder =
                new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestDataEnableable, EcsTestSharedComp>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            query.SetSharedComponentFilter(chunkFilterValue);

            for (var i = 0; i < entityCount; ++i)
            {
                int entityVariant = i % 4;
                bool expectMatches = (entityVariant == 3);
                Assert.AreEqual(expectMatches, query.Matches(entities[i]), $"Incorrect Matches() results for Entity {i}");
            }
        }

        [Test]
        public void ToArchetypeChunkArray_Filtered_Works([Values] EntityQueryJobMode jobMode)
        {
            // Note - test is setup so that each entity is in its own chunk, this checks that entity indices are correct
            var a = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData));
            var b = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData2));
            var c = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData3));

            m_Manager.SetSharedComponentManaged(a, new EcsTestSharedComp {value = 123});
            m_Manager.SetSharedComponentManaged(b, new EcsTestSharedComp {value = 456});
            m_Manager.SetSharedComponentManaged(c, new EcsTestSharedComp {value = 123});

            var chunkA = m_Manager.GetChunk(a);
            var chunkB = m_Manager.GetChunk(b);
            var chunkC = m_Manager.GetChunk(c);

            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp)))
            {
                query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 123});
                NativeArray<ArchetypeChunk> chunks;
                switch (jobMode)
                {
                    case EntityQueryJobMode.AsyncObsolete:
                    {
#pragma warning disable 0618
                        chunks = query.CreateArchetypeChunkArrayAsync(World.UpdateAllocator.ToAllocator, out JobHandle jobHandle);
#pragma warning restore 0618
                        jobHandle.Complete();
                        break;
                    }
                    case EntityQueryJobMode.Async:
                    {
                        var chunkList = query.ToArchetypeChunkListAsync(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
                        jobHandle.Complete();
                        chunks = chunkList.ToArray(World.UpdateAllocator.ToAllocator);
                        break;
                    }
                    case EntityQueryJobMode.Immediate:
                    default:
                        chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
                        break;
                }


                CollectionAssert.AreEqual(new[] {chunkA, chunkC}, chunks.ToArray());

                query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 456});

                switch (jobMode)
                {
                    case EntityQueryJobMode.AsyncObsolete:
                    {
#pragma warning disable 0618
                        chunks = query.CreateArchetypeChunkArrayAsync(World.UpdateAllocator.ToAllocator, out JobHandle jobHandle);
#pragma warning restore 0618
                        jobHandle.Complete();
                        break;
                    }
                    case EntityQueryJobMode.Async:
                    {
                        var chunkList = query.ToArchetypeChunkListAsync(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
                        jobHandle.Complete();
                        chunks = chunkList.ToArray(World.UpdateAllocator.ToAllocator);
                        break;
                    }
                    case EntityQueryJobMode.Immediate:
                    default:
                        chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
                        break;
                }

                CollectionAssert.AreEquivalent(new[] {chunkB}, chunks.ToArray());
            }
        }

        [Test]
        public void ToArchetypeChunkArray_Unfiltered_Works([Values] EntityQueryJobMode jobMode)
        {
            // Note - test is setup so that each entity is in its own chunk, this checks that entity indices are correct
            var a = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData));
            var b = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData3));
            var c = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData3));
            var d = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData3));

            m_Manager.SetSharedComponentManaged(b, new EcsTestSharedComp {value = 123});
            m_Manager.SetSharedComponentManaged(c, new EcsTestSharedComp {value = 456});
            m_Manager.SetSharedComponentManaged(d, new EcsTestSharedComp {value = 789});

            var chunkB = m_Manager.GetChunk(b);
            var chunkC = m_Manager.GetChunk(c);
            var chunkD = m_Manager.GetChunk(d);

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp), typeof(EcsTestData3));
            NativeArray<ArchetypeChunk> chunks;
            switch (jobMode)
            {
                case EntityQueryJobMode.AsyncObsolete:
                {
#pragma warning disable 0618
                    chunks = query.CreateArchetypeChunkArrayAsync(World.UpdateAllocator.ToAllocator, out JobHandle jobHandle);
#pragma warning restore 0618
                    jobHandle.Complete();
                    break;
                }
                case EntityQueryJobMode.Async:
                {
                    var chunkList = query.ToArchetypeChunkListAsync(World.UpdateAllocator.ToAllocator,
                        out JobHandle jobHandle);
                    jobHandle.Complete();
                    chunks = chunkList.ToArray(World.UpdateAllocator.ToAllocator);
                    break;
                }
                case EntityQueryJobMode.Immediate:
                default:
                    chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
                    break;
            }

            CollectionAssert.AreEquivalent(new[] {chunkB, chunkC, chunkD}, chunks.ToArray());
        }

        [Test]
        public void ToEntityArray_Filtered_Works([Values] EntityQueryJobMode jobMode)
        {
            // Note - test is setup so that each entity is in its own chunk, this checks that entity indices are correct
            var a = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData));
            var b = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData2));
            var c = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData3));

            m_Manager.SetSharedComponentManaged(a, new EcsTestSharedComp {value = 123});
            m_Manager.SetSharedComponentManaged(b, new EcsTestSharedComp {value = 456});
            m_Manager.SetSharedComponentManaged(c, new EcsTestSharedComp {value = 123});

            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp)))
            {
                query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 123});
                NativeArray<Entity> entities;
                switch (jobMode)
                {
                    case EntityQueryJobMode.AsyncObsolete:
                    {
#pragma warning disable 0618
                        entities = query.ToEntityArrayAsync(World.UpdateAllocator.ToAllocator, out JobHandle jobHandle);
#pragma warning restore 0618
                        jobHandle.Complete();
                        break;
                    }
                    case EntityQueryJobMode.Async:
                    {
                        var entityList = query.ToEntityListAsync(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
                        jobHandle.Complete();
                        entities = entityList.ToArray(World.UpdateAllocator.ToAllocator);
                        break;
                    }
                    case EntityQueryJobMode.Immediate:
                    default:
                        entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                        break;
                }


                CollectionAssert.AreEqual(new[] {a, c}, entities.ToArray());

                query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 456});

                switch (jobMode)
                {
                    case EntityQueryJobMode.AsyncObsolete:
                    {
#pragma warning disable 0618
                        entities = query.ToEntityArrayAsync(World.UpdateAllocator.ToAllocator, out JobHandle jobHandle);
#pragma warning restore 0618
                        jobHandle.Complete();
                        break;
                    }
                    case EntityQueryJobMode.Async:
                    {
                        var entityList = query.ToEntityListAsync(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
                        jobHandle.Complete();
                        entities = entityList.ToArray(World.UpdateAllocator.ToAllocator);
                        break;
                    }
                    case EntityQueryJobMode.Immediate:
                    default:
                        entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                        break;
                }

                CollectionAssert.AreEqual(new[] {b}, entities.ToArray());
            }
        }

        [Test]
        public void ToEntityArray_Unfiltered_Works([Values] EntityQueryJobMode jobMode)
        {
            int count = 1000;
            for (int i = 0; i < count ; i++)
            {
                var entity = m_Manager.CreateEntity(typeof(EcsTestData3));
                if (i % 2 == 0)
                {
                    m_Manager.AddComponentData(entity, new EcsTestData
                    {
                        value = i
                    });
                }
            }

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData3));
            NativeArray<Entity> entities;
            switch (jobMode)
            {
                case EntityQueryJobMode.AsyncObsolete:
                {
#pragma warning disable 0618
                    entities = query.ToEntityArrayAsync(World.UpdateAllocator.ToAllocator, out JobHandle jobHandle);
#pragma warning restore 0618
                    jobHandle.Complete();
                    break;
                }
                case EntityQueryJobMode.Async:
                {
                    var entityList = query.ToEntityListAsync(World.UpdateAllocator.ToAllocator,
                        out JobHandle jobHandle);
                    jobHandle.Complete();
                    entities = entityList.ToArray(World.UpdateAllocator.ToAllocator);
                    break;
                }
                case EntityQueryJobMode.Immediate:
                default:
                    entities = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                    break;
            }

            Assert.AreEqual(count / 2, entities.Length);
            for (int i = 0; i < count / 2 ; i++)
            {
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entities[i]));
                var value = m_Manager.GetComponentData<EcsTestData>(entities[i]).value;
                Assert.AreEqual(i * 2,value);
            }
        }

        [Test]
        public void ToComponentDataArray_Filtered_Works([Values] EntityQueryJobMode jobMode)
        {
            var ecsTestData1 = new EcsTestData
            {
                value = 1
            };
            var ecsTestData2 = new EcsTestData
            {
                value = 2
            };
            var ecsTestData3 = new EcsTestData
            {
                value = 3
            };

            var a = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData));
            m_Manager.SetComponentData(a,ecsTestData1);
            var b = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData));
            m_Manager.SetComponentData(b,ecsTestData2);
            var c = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData));
            m_Manager.SetComponentData(c,ecsTestData3);

            m_Manager.SetSharedComponentManaged(a, new EcsTestSharedComp {value = 123});
            m_Manager.SetSharedComponentManaged(b, new EcsTestSharedComp {value = 456});
            m_Manager.SetSharedComponentManaged(c, new EcsTestSharedComp {value = 123});

            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestSharedComp),typeof(EcsTestData)))
            {
                NativeArray<EcsTestData> components;

                switch (jobMode)
                {
                    case EntityQueryJobMode.AsyncObsolete:
                    {
#pragma warning disable 0618
                        components = query.ToComponentDataArrayAsync<EcsTestData>(World.UpdateAllocator.ToAllocator, out JobHandle jobHandle);
#pragma warning restore 0618
                        jobHandle.Complete();
                        break;
                    }
                    case EntityQueryJobMode.Async:
                    {
                        var list = query.ToComponentDataListAsync<EcsTestData>(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
                        jobHandle.Complete();
                        components = list.ToArray(World.UpdateAllocator.ToAllocator);
                        break;
                    }
                    case EntityQueryJobMode.Immediate:
                    default:
                        components = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                        break;
                }

                CollectionAssert.AreEquivalent(new[] {ecsTestData1,ecsTestData2,ecsTestData3}, components);

                query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 123});

                switch (jobMode)
                {
                    case EntityQueryJobMode.AsyncObsolete:
                    {
#pragma warning disable 0618
                        components = query.ToComponentDataArrayAsync<EcsTestData>(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
#pragma warning restore 0618
                        jobHandle.Complete();
                        break;
                    }
                    case EntityQueryJobMode.Async:
                    {
                        var list = query.ToComponentDataListAsync<EcsTestData>(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
                        jobHandle.Complete();
                        components = list.ToArray(World.UpdateAllocator.ToAllocator);
                        break;
                    }
                    case EntityQueryJobMode.Immediate:
                        components = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                        break;
                }

                CollectionAssert.AreEqual(new[] {ecsTestData1,ecsTestData3}, components);

                query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 456});

                switch (jobMode)
                {
                    case EntityQueryJobMode.AsyncObsolete:
                    {
#pragma warning disable 0618
                        components = query.ToComponentDataArrayAsync<EcsTestData>(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
#pragma warning restore 0618
                        jobHandle.Complete();
                        break;
                    }
                    case EntityQueryJobMode.Async:
                    {
                        var list = query.ToComponentDataListAsync<EcsTestData>(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
                        jobHandle.Complete();
                        components = list.ToArray(World.UpdateAllocator.ToAllocator);
                        break;
                    }
                    case EntityQueryJobMode.Immediate:
                    default:
                        components = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                        break;
                }

                CollectionAssert.AreEqual(new[] {ecsTestData2}, components);
            }
        }

        [Test]
        public void ToComponentDataArray_Unfiltered_Works([Values] EntityQueryJobMode jobMode)
        {
            var ecsData20 = new EcsTestData
            {
                value = 20
            };
            var ecsData40 = new EcsTestData
            {
                value = 40
            };
            var ecsData60 = new EcsTestData
            {
                value = 60
            };

            var ecsData2_20_40 = new EcsTestData2
            {
                value0 = 20,
                value1 = 40
            };

            var ecsData2_60_80 = new EcsTestData2
            {
                value0 = 60,
                value1 = 80
            };

            var ecsData3_20_40_60 = new EcsTestData3
            {
                value0 = 20,
                value1 = 40,
                value2 = 60,
            };

            var ecsData3_80_100_120 = new EcsTestData3
            {
                value0 = 80,
                value1 = 100,
                value2 = 120,
            };

            var a = m_Manager.CreateEntity( typeof(EcsTestData));
            m_Manager.SetComponentData(a,ecsData20);
            var b = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            m_Manager.SetComponentData(b,ecsData2_20_40);
            m_Manager.SetComponentData(b,ecsData40);
            var c = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData3));
            m_Manager.SetComponentData(c,ecsData3_20_40_60);
            m_Manager.SetComponentData(c,ecsData60);

            var d = m_Manager.CreateEntity(typeof(EcsTestData2));
            m_Manager.SetComponentData(d,ecsData2_60_80);
            var e = m_Manager.CreateEntity(typeof(EcsTestData3));
            m_Manager.SetComponentData(e,ecsData3_80_100_120);

            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                NativeArray<EcsTestData> components;
                switch (jobMode)
                {
                    case EntityQueryJobMode.AsyncObsolete:
                    {
#pragma warning disable 0618
                        components = query.ToComponentDataArrayAsync<EcsTestData>(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
#pragma warning restore 0618
                        jobHandle.Complete();
                        break;
                    }
                    case EntityQueryJobMode.Async:
                    {
                        var list = query.ToComponentDataListAsync<EcsTestData>(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
                        jobHandle.Complete();
                        components = list.ToArray(World.UpdateAllocator.ToAllocator);
                        break;
                    }
                    case EntityQueryJobMode.Immediate:
                    default:
                        components = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                        break;
                }
                CollectionAssert.AreEqual(new[] {ecsData20,ecsData40,ecsData60}, components);
            }

            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData2)))
            {
                NativeArray<EcsTestData2> components2;
                switch (jobMode)
                {
                    case EntityQueryJobMode.AsyncObsolete:
                    {
#pragma warning disable 0618
                        components2 = query.ToComponentDataArrayAsync<EcsTestData2>(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
#pragma warning restore 0618
                        jobHandle.Complete();
                        break;
                    }
                    case EntityQueryJobMode.Async:
                    {
                        var list = query.ToComponentDataListAsync<EcsTestData2>(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
                        jobHandle.Complete();
                        components2 = list.ToArray(World.UpdateAllocator.ToAllocator);
                        break;
                    }
                    case EntityQueryJobMode.Immediate:
                    default:
                        components2 = query.ToComponentDataArray<EcsTestData2>(World.UpdateAllocator.ToAllocator);
                        break;
                }
                CollectionAssert.AreEqual(new[] {ecsData2_20_40, ecsData2_60_80}, components2);
            }

            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData3)))
            {
                NativeArray<EcsTestData3> components3;
                switch (jobMode)
                {
                    case EntityQueryJobMode.AsyncObsolete:
                    {
#pragma warning disable 0618
                        components3 = query.ToComponentDataArrayAsync<EcsTestData3>(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
#pragma warning restore 0618
                        jobHandle.Complete();
                        break;
                    }
                    case EntityQueryJobMode.Async:
                    {
                        var list = query.ToComponentDataListAsync<EcsTestData3>(World.UpdateAllocator.ToAllocator,
                            out JobHandle jobHandle);
                        jobHandle.Complete();
                        components3 = list.ToArray(World.UpdateAllocator.ToAllocator);
                        break;
                    }
                    case EntityQueryJobMode.Immediate:
                    default:
                        components3 = query.ToComponentDataArray<EcsTestData3>(World.UpdateAllocator.ToAllocator);
                        break;
                }
                CollectionAssert.AreEqual(new[] {ecsData3_20_40_60, ecsData3_80_100_120}, components3);
            }
        }

        unsafe struct TestChunkBaseIndexJob : IJobChunk
        {
            [ReadOnly] public NativeArray<int> ChunkBaseEntityIndices;
            [ReadOnly] public ComponentTypeHandle<EcsTestData> TypeHandle;

            public void Execute(in ArchetypeChunk chunk, int chunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var chunkValues = (EcsTestDataEnableable*)chunk.GetComponentDataPtrRO(ref TypeHandle);
                int expectedBaseEntityIndex = (useEnabledMask && (chunkEnabledMask.ULong0 & 0x1ul) == 0)
                    ? chunkValues[1].value
                    : chunkValues[0].value;
                Assert.AreEqual(expectedBaseEntityIndex, ChunkBaseEntityIndices[chunkIndex]);
            }
        }

        public enum EnabledBitsMode
        {
            NoEnableableComponents,
            NoComponentsDisabled,
            FewComponentsDisabled,
            ManyComponentsDisabled,
            MostComponentsDisabled,
        }

        [Test]
        unsafe public void CalculateFilteredChunkIndexArray_Works([Values] bool enableChunkFilter,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.FewComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            var componentTypes = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? new ComponentType[] { typeof(EcsTestData), typeof(EcsTestSharedComp) }
                : new ComponentType[] { typeof(EcsTestData), typeof(EcsTestTagEnableable), typeof(EcsTestSharedComp) };
            EntityArchetype archetype = m_Manager.CreateArchetype(componentTypes);
            using var query = m_Manager.CreateEntityQuery(componentTypes);
            int entityCount = 1000;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            int expectedMatchingEntityCount = 0;
            for (int i = 0; i < entityCount; ++i)
            {
                if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents && (i % 10) == 0)
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                    m_Manager.SetComponentData(entities[i], new EcsTestData(-i));
                }
                else
                {
                    m_Manager.SetComponentData(entities[i], new EcsTestData(expectedMatchingEntityCount++));
                }
            }
            if (enableChunkFilter)
            {
                // All the entities created above should pass the filter. Create a bunch of extra ones that don't.
                var filterPassValue = default(EcsTestSharedComp);
                var filterFailValue = new EcsTestSharedComp(17);
                query.SetSharedComponentFilter(filterPassValue);
                using var nonMatchingEntities =
                    m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
                for (int i = 0; i < entityCount; ++i)
                {
                    m_Manager.SetSharedComponentManaged(nonMatchingEntities[i], filterFailValue);
                }
            }
            int expectedArrayLength = query.CalculateChunkCountWithoutFiltering();

            using var filteredChunkIndexArray = query.CalculateFilteredChunkIndexArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(expectedArrayLength, filteredChunkIndexArray.Length);

            // TODO(DOTS-6512): A better way to validate this test would be to use filteredChunks = query.ToArchetypeChunkArray().
            // If the array element isn't -1, then the corresponding element in filteredChunks must have the same Chunk*.
            var unfilteredChunks = query._GetImpl()->GetMatchingChunkCache();
            var matchingArchetypes = query._GetImpl()->_QueryData->MatchingArchetypes;
            int numFilteredChunks = 0;
            var queryFilter = query._GetImpl()->_Filter;
            for (int unfilteredChunkIndex = 0; unfilteredChunkIndex < unfilteredChunks.Length; ++unfilteredChunkIndex)
            {
                var chunk = unfilteredChunks.ChunkIndices[unfilteredChunkIndex];
                int chunkIndexInArchetype = unfilteredChunks.ChunkIndexInArchetype->Ptr[unfilteredChunkIndex];
                int matchingArchetypeIndex = unfilteredChunks.PerChunkMatchingArchetypeIndex->Ptr[unfilteredChunkIndex];
                var matchingArchetype = matchingArchetypes.Ptr[matchingArchetypeIndex];
                if (enableChunkFilter && !matchingArchetype->ChunkMatchesFilter(chunkIndexInArchetype, ref queryFilter))
                {
                    Assert.AreEqual(-1, filteredChunkIndexArray[unfilteredChunkIndex]);
                    continue;
                }
                if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                {
                    ChunkIterationUtility.GetEnabledMask(chunk, matchingArchetype, out var chunkEnabledMask);
                    if (chunkEnabledMask.ULong0 == 0 && chunkEnabledMask.ULong1 == 0)
                    {
                        Assert.AreEqual(-1, filteredChunkIndexArray[unfilteredChunkIndex]);
                        continue;
                    }
                }
                Assert.AreEqual(numFilteredChunks++, filteredChunkIndexArray[unfilteredChunkIndex]);
            }
            Assert.AreEqual(query.CalculateChunkCount(), numFilteredChunks);
        }

        [Test]
        unsafe public void CalculateFilteredChunkIndexArrayAsync_Works([Values] bool enableChunkFilter,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.FewComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            var componentTypes = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? new ComponentType[] { typeof(EcsTestData), typeof(EcsTestSharedComp) }
                : new ComponentType[] { typeof(EcsTestData), typeof(EcsTestTagEnableable), typeof(EcsTestSharedComp) };
            EntityArchetype archetype = m_Manager.CreateArchetype(componentTypes);
            using var query = m_Manager.CreateEntityQuery(componentTypes);
            int entityCount = 1000;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            int expectedMatchingEntityCount = 0;
            for (int i = 0; i < entityCount; ++i)
            {
                if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents && (i % 10) == 0)
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                    m_Manager.SetComponentData(entities[i], new EcsTestData(-i));
                }
                else
                {
                    m_Manager.SetComponentData(entities[i], new EcsTestData(expectedMatchingEntityCount++));
                }
            }
            if (enableChunkFilter)
            {
                // All the entities created above should pass the filter. Create a bunch of extra ones that don't.
                var filterPassValue = default(EcsTestSharedComp);
                var filterFailValue = new EcsTestSharedComp(17);
                query.SetSharedComponentFilter(filterPassValue);
                using var nonMatchingEntities =
                    m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
                for (int i = 0; i < entityCount; ++i)
                {
                    m_Manager.SetSharedComponentManaged(nonMatchingEntities[i], filterFailValue);
                }
            }
            int expectedArrayLength = query.CalculateChunkCountWithoutFiltering();

            using var filteredChunkIndexArray = query.CalculateFilteredChunkIndexArrayAsync(
                World.UpdateAllocator.ToAllocator, default, out var chunkIndexJobHandle);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Reading the output array should fail until the job is complete
            Assert.Throws<InvalidOperationException>(() => {var foo = filteredChunkIndexArray[0];});
#endif
            Assert.AreEqual(expectedArrayLength, filteredChunkIndexArray.Length);

            chunkIndexJobHandle.Complete();

            // TODO(DOTS-6512): A better way to validate this test would be to use filteredChunks = query.ToArchetypeChunkArray().
            // If the array element isn't -1, then the corresponding element in filteredChunks must have the same Chunk*.
            var unfilteredChunks = query._GetImpl()->GetMatchingChunkCache();
            var matchingArchetypes = query._GetImpl()->_QueryData->MatchingArchetypes;
            int numFilteredChunks = 0;
            var queryFilter = query._GetImpl()->_Filter;
            for (int unfilteredChunkIndex = 0; unfilteredChunkIndex < unfilteredChunks.Length; ++unfilteredChunkIndex)
            {
                var chunk = unfilteredChunks.ChunkIndices[unfilteredChunkIndex];
                int chunkIndexInArchetype = unfilteredChunks.ChunkIndexInArchetype->Ptr[unfilteredChunkIndex];
                int matchingArchetypeIndex = unfilteredChunks.PerChunkMatchingArchetypeIndex->Ptr[unfilteredChunkIndex];
                var matchingArchetype = matchingArchetypes.Ptr[matchingArchetypeIndex];
                if (enableChunkFilter && !matchingArchetype->ChunkMatchesFilter(chunkIndexInArchetype, ref queryFilter))
                {
                    Assert.AreEqual(-1, filteredChunkIndexArray[unfilteredChunkIndex]);
                    continue;
                }
                if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                {
                    ChunkIterationUtility.GetEnabledMask(chunk, matchingArchetype, out var chunkEnabledMask);
                    if (chunkEnabledMask.ULong0 == 0 && chunkEnabledMask.ULong1 == 0)
                    {
                        Assert.AreEqual(-1, filteredChunkIndexArray[unfilteredChunkIndex]);
                        continue;
                    }
                }
                Assert.AreEqual(numFilteredChunks++, filteredChunkIndexArray[unfilteredChunkIndex]);
            }
            Assert.AreEqual(query.CalculateChunkCount(), numFilteredChunks);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void CalculateFilteredChunkIndexArrayAsync_TempMemory_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            Assert.Throws<ArgumentException>(() => query.CalculateFilteredChunkIndexArrayAsync(Allocator.Temp,
                default, out var chunkIndexJobHandle));
        }

        [Test]
        public void CalculateBaseEntityIndexArray_Works([Values] bool enableChunkFilter,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.FewComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            var componentTypes = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? new ComponentType[] { typeof(EcsTestData), typeof(EcsTestSharedComp) }
                : new ComponentType[] { typeof(EcsTestData), typeof(EcsTestTagEnableable), typeof(EcsTestSharedComp) };
            EntityArchetype archetype = m_Manager.CreateArchetype(componentTypes);
            using var query = m_Manager.CreateEntityQuery(componentTypes);
            int entityCount = 1000;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            int expectedMatchingEntityCount = 0;
            for (int i = 0; i < entityCount; ++i)
            {
                if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents && (i % 10) == 0)
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                    m_Manager.SetComponentData(entities[i], new EcsTestData(-i));
                }
                else
                {
                    m_Manager.SetComponentData(entities[i], new EcsTestData(expectedMatchingEntityCount++));
                }
            }
            if (enableChunkFilter)
            {
                // All the entities created above should pass the filter. Create a bunch of extra ones that don't.
                var filterPassValue = default(EcsTestSharedComp);
                var filterFailValue = new EcsTestSharedComp(17);
                query.SetSharedComponentFilter(filterPassValue);
                using var nonMatchingEntities =
                    m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
                for (int i = 0; i < entityCount; ++i)
                {
                    m_Manager.SetSharedComponentManaged(nonMatchingEntities[i], filterFailValue);
                }
            }
            int expectedArrayLength = query.CalculateChunkCountWithoutFiltering();

            using var chunkBaseIndexArray = query.CalculateBaseEntityIndexArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(expectedArrayLength, chunkBaseIndexArray.Length);

            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(true);
            var testJob = new TestChunkBaseIndexJob
            {
                ChunkBaseEntityIndices = chunkBaseIndexArray,
                TypeHandle = typeHandle,
            };
            testJob.Run(query);
        }

        [Test]
        public void CalculateBaseEntityIndexArrayAsync_Works([Values] bool enableChunkFilter,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.FewComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            var componentTypes = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? new ComponentType[] { typeof(EcsTestData), typeof(EcsTestSharedComp) }
                : new ComponentType[] { typeof(EcsTestData), typeof(EcsTestTagEnableable), typeof(EcsTestSharedComp) };
            EntityArchetype archetype = m_Manager.CreateArchetype(componentTypes);
            using var query = m_Manager.CreateEntityQuery(componentTypes);
            int entityCount = 1000;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            int expectedMatchingEntityCount = 0;
            for (int i = 0; i < entityCount; ++i)
            {
                if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents && (i % 10) == 0)
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                    m_Manager.SetComponentData(entities[i], new EcsTestData(-i));
                }
                else
                {
                    m_Manager.SetComponentData(entities[i], new EcsTestData(expectedMatchingEntityCount++));
                }
            }
            if (enableChunkFilter)
            {
                // All the entities created above should pass the filter. Create a bunch of extra ones that don't.
                var filterPassValue = default(EcsTestSharedComp);
                var filterFailValue = new EcsTestSharedComp(17);
                query.SetSharedComponentFilter(filterPassValue);
                using var nonMatchingEntities =
                    m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
                for (int i = 0; i < entityCount; ++i)
                {
                    m_Manager.SetSharedComponentManaged(nonMatchingEntities[i], filterFailValue);
                }
            }
            int expectedArrayLength = query.CalculateChunkCountWithoutFiltering();

            using var chunkBaseIndexArray = query.CalculateBaseEntityIndexArrayAsync(World.UpdateAllocator.ToAllocator, default,
                out var chunkIndexJobHandle);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Reading the output array should fail until the job is complete
            Assert.Throws<InvalidOperationException>(() => {var foo = chunkBaseIndexArray[0];});
#endif

            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(true);
            var testJob = new TestChunkBaseIndexJob
            {
                ChunkBaseEntityIndices = chunkBaseIndexArray,
                TypeHandle = typeHandle,
            };
            var testJobHandle = testJob.Schedule(query, chunkIndexJobHandle);
            testJobHandle.Complete();

            Assert.AreEqual(expectedArrayLength, chunkBaseIndexArray.Length);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void CalculateBaseEntityIndexArrayAsync_TempMemory_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            Assert.Throws<ArgumentException>(() => query.CalculateBaseEntityIndexArrayAsync(Allocator.Temp,
                default, out var chunkIndexJobHandle));
        }

        [Test]
        public void CalculateEntityCount_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var entityA = m_Manager.CreateEntity(archetype);
            var entityB = m_Manager.CreateEntity(archetype);

            m_Manager.SetSharedComponentManaged(entityA, new EcsTestSharedComp {value = 10});

            var query = EmptySystem.GetEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp));

            var entityCountBeforeFilter = query.CalculateChunkCount();

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 10});
            var entityCountAfterSetFilter = query.CalculateChunkCount();

            var entityCountUnfilteredAfterSetFilter = query.CalculateChunkCountWithoutFiltering();

            Assert.AreEqual(2, entityCountBeforeFilter);
            Assert.AreEqual(1, entityCountAfterSetFilter);
            Assert.AreEqual(2, entityCountUnfilteredAfterSetFilter);
        }

        [Test]
        public void CalculateChunkCount_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var entityA = m_Manager.CreateEntity(archetype);
            var entityB = m_Manager.CreateEntity(archetype);

            m_Manager.SetSharedComponentManaged(entityA, new EcsTestSharedComp {value = 10});

            var query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestData, EcsTestSharedComp>()
                .Build(EmptySystem);

            // Test with no filter
            Assert.AreEqual(2, query.CalculateChunkCount());

            // Test with chunk filter (filter out one of the matching chunks)
            query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 10});
            Assert.AreEqual(1, query.CalculateChunkCount());
            Assert.AreEqual(2, query.CalculateChunkCountWithoutFiltering());

            // Test with None + enableable components (makes sure empty chunks aren't counted)
            var query2 = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestData>().WithNone<EcsTestDataEnableable>()
                .Build(EmptySystem);
            var entityC = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            Assert.AreEqual(2, query2.CalculateChunkCount());
            Assert.AreEqual(3, query2.CalculateChunkCountWithoutFiltering());
        }

        [Test]
        public void IsEmpty_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var entities = m_Manager.CreateEntity(archetype, archetype.ChunkCapacity, Allocator.Temp);
            for (int i = 0; i < entities.Length; i += 2)
            {
                m_Manager.SetSharedComponentManaged(entities[i], new EcsTestSharedComp {value = 10});
            }

            var query = EmptySystem.GetEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp));

            Assert.IsFalse(query.IsEmpty);
            Assert.IsFalse(query.IsEmptyIgnoreFilter);

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 10});
            Assert.IsFalse(query.IsEmpty);
            Assert.IsFalse(query.IsEmptyIgnoreFilter);

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 50});
            Assert.IsTrue(query.IsEmpty);
            Assert.IsFalse(query.IsEmptyIgnoreFilter);
        }

        private struct TestTag0 : IComponentData {}
        private struct TestTag1 : IComponentData {}
        private struct TestTag2 : IComponentData {}
        private struct TestTag3 : IComponentData {}
        private struct TestTag4 : IComponentData {}
        private struct TestTag5 : IComponentData {}
        private struct TestTag6 : IComponentData {}
        private struct TestTag7 : IComponentData {}
        private struct TestTag8 : IComponentData {}
        private struct TestTag9 : IComponentData {}
        private struct TestTag10 : IComponentData {}
        private struct TestTag11 : IComponentData {}
        private struct TestTag12 : IComponentData {}
        private struct TestTag13 : IComponentData {}
        private struct TestTag14 : IComponentData {}
        private struct TestTag15 : IComponentData {}
        private struct TestTag16 : IComponentData {}
        private struct TestTag17 : IComponentData {}

        private struct TestDefaultData : IComponentData
        {
            private int value;
        }

        private void MakeExtraQueries(int size)
        {
            var TagTypes = new Type[]
            {
                typeof(TestTag0),
                typeof(TestTag1),
                typeof(TestTag2),
                typeof(TestTag3),
                typeof(TestTag4),
                typeof(TestTag5),
                typeof(TestTag6),
                typeof(TestTag7),
                typeof(TestTag8),
                typeof(TestTag9),
                typeof(TestTag10),
                typeof(TestTag11),
                typeof(TestTag12),
                typeof(TestTag13),
                typeof(TestTag14),
                typeof(TestTag15),
                typeof(TestTag16),
                typeof(TestTag17)
            };

            var typeCount = CollectionHelper.Log2Ceil(size);
            for (int i = 0; i < size; i++)
            {
                var typeList = new List<ComponentType>();
                for (int typeIndex = 0; typeIndex < typeCount; typeIndex++)
                {
                    if ((i & (1 << typeIndex)) != 0)
                        typeList.Add(TagTypes[typeIndex]);
                }

                typeList.Add(typeof(TestDefaultData));

                var types = typeList.ToArray();
                var archetype = m_Manager.CreateArchetype(types);

                m_Manager.CreateEntity(archetype);
                var query = EmptySystem.GetEntityQuery(types);
                query.GetEntityQueryMask();
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void GetEntityQueryMaskThrowsOnOverflow()
        {
            Assert.That(() => MakeExtraQueries(1200),
                Throws.Exception.With.Message.Matches("You have reached the limit of 1024 unique EntityQueryMasks, and cannot generate any more."));
        }

        [Test]
        public unsafe void GetEntityQueryMask_ReturnsCachedMask()
        {
            var queryMatches = EmptySystem.GetEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));
            var queryMaskMatches = queryMatches.GetEntityQueryMask();

            var queryMaskMatches2 = queryMatches.GetEntityQueryMask();

            Assert.True(queryMaskMatches.Mask == queryMaskMatches2.Mask &&
                queryMaskMatches.Index == queryMaskMatches2.Index &&
                queryMaskMatches.EntityComponentStore == queryMaskMatches2.EntityComponentStore);
        }

        [Test]
        public void GetEntityQueryMask_MatchesIgnoreFilter_IgnoresFilter()
        {
            var queryMatches = EmptySystem.GetEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));
            queryMatches.SetSharedComponentFilterManaged(new EcsTestSharedComp(42));

            var matching = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));
            var different = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            Assert.IsTrue(queryMatches.GetEntityQueryMask().MatchesIgnoreFilter(matching));
            Assert.IsFalse(queryMatches.GetEntityQueryMask().MatchesIgnoreFilter(different));
        }

        [Test]
        public void EntityQueryMask_MatchesIgnoreFilter_Works()
        {
            var archetypeMatches = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));
            var archetypeDoesntMatch = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData3), typeof(EcsTestSharedComp));

            var entity = m_Manager.CreateEntity(archetypeMatches);
            var entityOnlyNeededToPopulateArchetype = m_Manager.CreateEntity(archetypeDoesntMatch);

            var queryMatches = EmptySystem.GetEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));
            var queryDoesntMatch = EmptySystem.GetEntityQuery(typeof(EcsTestData), typeof(EcsTestData3), typeof(EcsTestSharedComp));

            var queryMaskMatches = queryMatches.GetEntityQueryMask();

            var queryMaskDoesntMatch = queryDoesntMatch.GetEntityQueryMask();

            Assert.True(queryMaskMatches.MatchesIgnoreFilter(entity));
            Assert.True(queryMaskMatches.MatchesIgnoreFilter(m_Manager.GetChunk(entity)));
            Assert.True(queryMaskMatches.Matches(m_Manager.GetChunk(entity).Archetype));
            Assert.False(queryMaskDoesntMatch.MatchesIgnoreFilter(entity));
            Assert.False(queryMaskDoesntMatch.MatchesIgnoreFilter(m_Manager.GetChunk(entity)));
            Assert.False(queryMaskDoesntMatch.Matches(m_Manager.GetChunk(entity).Archetype));
        }

        [Test]
        public void MatchesEntity_Works()
        {
            var archetypeMatches = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));
            var archetypeDoesntMatch = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData3), typeof(EcsTestSharedComp));

            var entity = m_Manager.CreateEntity(archetypeMatches);
            var entityDoesntMatch = m_Manager.CreateEntity(archetypeDoesntMatch);

            var query = EmptySystem.GetEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            Assert.True(query.Matches(entity));
            Assert.False(query.Matches(entityDoesntMatch));
        }

        [Test]
        public void MatchesEntity_Filtered_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            var entity = m_Manager.CreateEntity(archetype);
            m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp{value = 10});

            var query = EmptySystem.GetEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            Assert.True(query.Matches(entity));

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp(5));
            Assert.False(query.Matches(entity));

            query.SetSharedComponentFilterManaged(new EcsTestSharedComp(10));
            Assert.True(query.Matches(entity));
        }

        [Test]
        public void EntityQueryMask_MatchesIgnoreFilter_ArchetypeAddedAfterMaskCreation_Works()
        {
            var archetypeBefore = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var query = EmptySystem.GetEntityQuery(typeof(EcsTestData));
            var queryMask = query.GetEntityQueryMask();

            var archetypeAfter = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData3));
            var entity = m_Manager.CreateEntity(archetypeAfter);

            Assert.True(queryMask.MatchesIgnoreFilter(entity));
        }

        [Test]
        public void ChunkListCaching_CheckCacheConsistency_PassesForNewEmptyQuery()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            Assert.AreEqual(0, archetype1.ChunkCount);
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                // Freshly-created empty queries which don't match any chunks yet are invalid, but not inconsistent
                Assert.IsFalse(query.IsCacheValid, "cached chunk list is valid on freshly-created empty query");
                // In this one case, we except all the consistency checks to pass even though the IsCacheValid flag is false,
                // so we need to pass forceCheckInvalidCache=true
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(true), "cached chunk list is inconsistent on freshly-created empty query");
                // After updating the cache (called automatically when the cache is accessed with GetMatchingChunkCache),
                // it should be consistent.
                query.ForceUpdateCache();
                Assert.IsTrue(query.IsCacheValid, "cached chunk list is invalid after ForceUpdateCache() call");
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(), "cached chunk list is inconsistent after ForceUpdateCache() call");
            }
        }

        [Test]
        public void ChunkListCaching_CheckCacheConsistency_FailsForNewNonEmptyQuery()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var ent1 = m_Manager.CreateEntity(archetype1);
            Assert.AreEqual(1, archetype1.ChunkCount);
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                // Freshly-created queries are both invalid and inconsistent if they match any chunks
                Assert.IsFalse(query.IsCacheValid, "cached chunk list is valid on freshly-created query");
                Assert.Throws<ArgumentException>(() => query.CheckChunkListCacheConsistency(true), "cached chunk list is consistent on freshly-created query");
                // After updating the cache (called automatically when the cache is accessed with GetMatchingChunkCache),
                // it should be consistent.
                query.ForceUpdateCache();
                Assert.IsTrue(query.IsCacheValid, "cached chunk list is invalid after ForceUpdateCache() call");
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(), "cached chunk list is inconsistent after ForceUpdateCache() call");
            }
        }

        [Test]
        public void ChunkListCaching_CheckCacheConsistency_FailsAfterAddingAndRemovingChunks()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var ent1 = m_Manager.CreateEntity(archetype1);
            Assert.AreEqual(1, archetype1.ChunkCount);
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                // Start with a valid, consistent cache
                query.ForceUpdateCache();
                Assert.IsTrue(query.IsCacheValid, "cached chunk list is invalid after ForceUpdateCache() call");
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(),
                    "cached chunk list is inconsistent after ForceUpdateCache() call");
                // Adding a new chunk AND removing an existing chunk to/from one of the matching archetypes should
                // invalidate the cache AND render it inconsistent.
                var ent2 = m_Manager.CreateEntity(archetype1);
                m_Manager.SetSharedComponentManaged(ent2, new EcsTestSharedComp {value = 17});
                Assert.AreEqual(2, archetype1.ChunkCount);
                m_Manager.DestroyEntity(ent1);
                Assert.AreEqual(1, archetype1.ChunkCount);
                Assert.IsFalse(query.IsCacheValid, "cached chunk list is valid after adding and removing chunk");
                Assert.Throws<ArgumentException>(() => query.CheckChunkListCacheConsistency(true), "cached chunk list is consistent after adding and removing chunk");
                // Updating the cache should leave it valid and consistent.
                query.ForceUpdateCache();
                Assert.IsTrue(query.IsCacheValid, "cached chunk list is invalid after ForceUpdateCache() call");
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(), "cached chunk list is inconsistent after ForceUpdateCache() call");
            }
        }

        [Test]
        public void ChunkListCaching_CheckCacheConsistency_FailsAfterAddingChunks()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var ent1 = m_Manager.CreateEntity(archetype1);
            Assert.AreEqual(1, archetype1.ChunkCount);
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                // Start with a valid, consistent cache
                query.ForceUpdateCache();
                Assert.IsTrue(query.IsCacheValid, "cached chunk list is invalid after ForceUpdateCache() call");
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(),
                    "cached chunk list is inconsistent after ForceUpdateCache() call");
                // Adding a new chunk to one of the matching archetypes should invalidate the cache AND render it inconsistent.
                var ent2 = m_Manager.CreateEntity(archetype1);
                m_Manager.SetSharedComponentManaged(ent2, new EcsTestSharedComp {value = 17});
                Assert.AreEqual(2, archetype1.ChunkCount);
                Assert.IsFalse(query.IsCacheValid, "cached chunk list is valid after adding chunk");
                Assert.Throws<ArgumentException>(() => query.CheckChunkListCacheConsistency(true), "cached chunk list is consistent after adding chunk");
                // Updating the cache should leave it valid and consistent.
                query.ForceUpdateCache();
                Assert.IsTrue(query.IsCacheValid, "cached chunk list is invalid after ForceUpdateCache() call");
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(), "cached chunk list is inconsistent after ForceUpdateCache() call");
            }
        }

        [Test]
        public void ChunkListCaching_CheckCacheConsistency_FailsAfterRemovingChunks()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var ent1 = m_Manager.CreateEntity(archetype1);
            Assert.AreEqual(1, archetype1.ChunkCount);
            var ent2 = m_Manager.CreateEntity(archetype1);
            m_Manager.SetSharedComponentManaged(ent2, new EcsTestSharedComp {value = 17});
            Assert.AreEqual(2, archetype1.ChunkCount);
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                // Start with a valid, consistent cache
                query.ForceUpdateCache();
                Assert.IsTrue(query.IsCacheValid, "cached chunk list is invalid after ForceUpdateCache() call");
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(),
                    "cached chunk list is inconsistent after ForceUpdateCache() call");
                // Removing a chunk from one of the matching archetypes should invalidate the cache AND render it inconsistent.
                m_Manager.DestroyEntity(ent2);
                Assert.AreEqual(1, archetype1.ChunkCount);
                Assert.IsFalse(query.IsCacheValid, "cached chunk list is valid after removing chunk");
                Assert.Throws<ArgumentException>(() => query.CheckChunkListCacheConsistency(true), "cached chunk list is consistent after removing chunk");
                // Updating the cache should leave it valid and consistent.
                query.ForceUpdateCache();
                Assert.IsTrue(query.IsCacheValid, "cached chunk list is invalid after ForceUpdateCache() call");
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(), "cached chunk list is inconsistent after ForceUpdateCache() call");
            }
        }

        [Test]
        public void ChunkListCaching_CheckCacheConsistency_PassesAfterAddingNewArchetype()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var ent1 = m_Manager.CreateEntity(archetype1);
            Assert.AreEqual(1, archetype1.ChunkCount);
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                // Start with a valid, consistent cache
                query.ForceUpdateCache();
                Assert.IsTrue(query.IsCacheValid, "cached chunk list is invalid after ForceUpdateCache() call");
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(), "cached chunk list is inconsistent after ForceUpdateCache() call");
                // Adding a new archetype that matches the query should NOT immediately invalidate the cache or render it inconsistent,
                // as the new archetype has no chunks to match.
                var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData),
                    typeof(EcsTestData2), typeof(EcsTestSharedComp));
                Assert.IsTrue(query.IsCacheValid, "cached chunk list is invalid after creating new empty archetype");
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(), "cached chunk list is inconsistent after creating new empty archetype");
            }
        }

        [Test]
        public void ChunkListCaching_CheckCacheConsistency_PassesAfterAddingEntityToExistingChunk()
        {
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var ent1 = m_Manager.CreateEntity(archetype1);
            Assert.AreEqual(1, archetype1.ChunkCount);
            using (var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                // Start with a valid, consistent cache
                query.ForceUpdateCache();
                Assert.IsTrue(query.IsCacheValid, "cached chunk list is invalid after ForceUpdateCache() call");
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(), "cached chunk list is inconsistent after ForceUpdateCache() call");
                // Structural changes which don't add/remove new chunks should neither invalidate the cache nor render it inconsistent;
                // no chunks were added/removed/changed.
                var ent2 = m_Manager.CreateEntity(archetype1);
                Assert.AreEqual(1, archetype1.ChunkCount);
                Assert.IsTrue(query.IsCacheValid, "cached chunk list is invalid after adding new entity to existing chunk");
                Assert.DoesNotThrow(() => query.CheckChunkListCacheConsistency(), "cached chunk list is inconsistent after adding new entity to existing chunk");
            }
        }

        [Test]
        public void ChunkListCaching_AddComponent()
        {
            // new archetype, no query
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData));

            // new query, exisiting archetype, no entities
            var queryA = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            // new chunk of matching archetype
            var entities = m_Manager.CreateEntity(archetypeA, archetypeA.ChunkCapacity, Allocator.Temp);

            queryA.ForceUpdateCache();
            Assert.IsTrue(queryA.IsCacheValid);
            Assert.DoesNotThrow(() => queryA.CheckChunkListCacheConsistency());

            for (int i = 0; i < entities.Length; ++i)
            {
                m_Manager.AddComponent(entities[i], ComponentType.ReadWrite<EcsTestData2>());
            }

            Assert.IsFalse(queryA.IsCacheValid);
            Assert.Throws<ArgumentException>(() => queryA.CheckChunkListCacheConsistency(true));
        }

        [Test]
        public void ChunkListCaching_RemoveComponent()
        {
            // new archetype, no query
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData), ComponentType.ReadWrite<EcsTestData2>());

            var queryA = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestData>()
                .WithNone<EcsTestData2>()
                .Build(m_Manager);

            var entities = m_Manager.CreateEntity(archetypeA, archetypeA.ChunkCapacity, Allocator.Temp);

            queryA.ForceUpdateCache();
            Assert.IsTrue(queryA.IsCacheValid);
            Assert.DoesNotThrow(() => queryA.CheckChunkListCacheConsistency());

            for (int i = 0; i < entities.Length; ++i)
            {
                m_Manager.RemoveComponent(entities[i], ComponentType.ReadWrite<EcsTestData2>());
            }

            Assert.IsFalse(queryA.IsCacheValid);
            Assert.Throws<ArgumentException>(() => queryA.CheckChunkListCacheConsistency(true));
        }

        [Test]
        public void ChunkListCaching_MoveEntitiesFrom()
        {
            var queryC = m_Manager.CreateEntityQuery(typeof(EcsTestData3));
            Assert.IsFalse(queryC.IsCacheValid);
            queryC.ForceUpdateCache();
            Assert.IsTrue(queryC.IsCacheValid);
            Assert.DoesNotThrow(() => queryC.CheckChunkListCacheConsistency());

            // move from another world
            using(var newWorld = new World("testworld"))
            {
                var archetype = newWorld.EntityManager.CreateArchetype(typeof(EcsTestData3));
                newWorld.EntityManager.CreateEntity(archetype);
                var query = newWorld.EntityManager.CreateEntityQuery(typeof(EcsTestData3));

                newWorld.EntityManager.UniversalQuery.ForceUpdateCache();
                Assert.IsTrue(newWorld.EntityManager.UniversalQuery.IsCacheValid);
                Assert.DoesNotThrow(() => newWorld.EntityManager.UniversalQuery.CheckChunkListCacheConsistency());

                m_Manager.MoveEntitiesFrom(newWorld.EntityManager);

                Assert.IsFalse(newWorld.EntityManager.UniversalQuery.IsCacheValid);
            }

            Assert.IsFalse(queryC.IsCacheValid);
            Assert.Throws<ArgumentException>(() => queryC.CheckChunkListCacheConsistency(true));
        }

        [Test]
        public void ChunkListCaching_CopyAndReplaceEntitiesFrom()
        {
            var queryC = m_Manager.CreateEntityQuery(typeof(EcsTestData3));
            queryC.ForceUpdateCache();
            Assert.IsTrue(queryC.IsCacheValid);
            Assert.DoesNotThrow(() => queryC.CheckChunkListCacheConsistency());

            // move from another world
            using(var newWorld = new World("testworld"))
            {
                var archetype = newWorld.EntityManager.CreateArchetype(typeof(EcsTestData3));
                newWorld.EntityManager.CreateEntity(archetype);

                newWorld.EntityManager.UniversalQuery.ForceUpdateCache();
                Assert.IsTrue(newWorld.EntityManager.UniversalQuery.IsCacheValid);
                Assert.DoesNotThrow(() => newWorld.EntityManager.UniversalQuery.CheckChunkListCacheConsistency());

#pragma warning disable CS0618 // Type or member is obsolete
                m_Manager.CopyAndReplaceEntitiesFrom(newWorld.EntityManager);
#pragma warning restore CS0618 // Type or member is obsolete

                Assert.IsTrue(newWorld.EntityManager.UniversalQuery.IsCacheValid);
                Assert.DoesNotThrow(() => newWorld.EntityManager.UniversalQuery.CheckChunkListCacheConsistency());
            }

            Assert.IsFalse(queryC.IsCacheValid);
            Assert.Throws<ArgumentException>(() => queryC.CheckChunkListCacheConsistency(true));

        }

        [Test]
        public void ChunkListCaching_Instantiate()
        {
            // new archetype, no query
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData));

            // new query, exisiting archetype, no entities
            var queryA = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            // new chunk of matching archetype
            var entityA = m_Manager.CreateEntity(archetypeA);

            queryA.ForceUpdateCache();
            Assert.IsTrue(queryA.IsCacheValid);
            Assert.DoesNotThrow(() => queryA.CheckChunkListCacheConsistency());

            m_Manager.Instantiate(entityA, archetypeA.ChunkCapacity, Allocator.Temp);

            Assert.IsFalse(queryA.IsCacheValid);
            Assert.Throws<ArgumentException>(() => queryA.CheckChunkListCacheConsistency(true));
        }

        [Test]
        public void ChunkListCaching_AddSharedComponent()
        {
            // new archetype, no query
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData));

            // new query, exisiting archetype, no entities
            var queryA = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            // new chunk of matching archetype
            var entities = m_Manager.CreateEntity(archetypeA, archetypeA.ChunkCapacity, Allocator.Temp);

            queryA.ForceUpdateCache();
            Assert.IsTrue(queryA.IsCacheValid);
            Assert.DoesNotThrow(() => queryA.CheckChunkListCacheConsistency());

            for (int i = 0; i < entities.Length; ++i)
            {
                m_Manager.AddSharedComponentManaged(entities[i], new EcsTestSharedComp{value = 10});
            }

            Assert.IsFalse(queryA.IsCacheValid);
            Assert.Throws<ArgumentException>(() => queryA.CheckChunkListCacheConsistency(true));
        }

        [Test]
        public void ChunkListCaching_SetSharedComponent()
        {
            // new archetype, no query
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));

            // new query, exisiting archetype, no entities
            var queryA = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            // new chunk of matching archetype
            var entities = m_Manager.CreateEntity(archetypeA, archetypeA.ChunkCapacity, Allocator.Temp);

            queryA.ForceUpdateCache();
            Assert.IsTrue(queryA.IsCacheValid);
            Assert.DoesNotThrow(() => queryA.CheckChunkListCacheConsistency());

            for (int i = 0; i < entities.Length; ++i)
            {
                m_Manager.SetSharedComponentManaged(entities[i], new EcsTestSharedComp{value = 10});
            }

            Assert.IsFalse(queryA.IsCacheValid);
            Assert.Throws<ArgumentException>(() => queryA.CheckChunkListCacheConsistency(true));
        }

        [Test]
        public void ChunkListCaching_DestroyEntity_Query()
        {
            // new archetype, no query
            var archetypeA = m_Manager.CreateArchetype(typeof(EcsTestData));

            // new query, exisiting archetype, no entities
            var queryA = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            // new chunk of matching archetype
            using(var entitiesA = m_Manager.CreateEntity(archetypeA, archetypeA.ChunkCapacity, World.UpdateAllocator.ToAllocator))
            using(var entitiesB = m_Manager.CreateEntity(archetypeA, archetypeA.ChunkCapacity, World.UpdateAllocator.ToAllocator))
            {
                queryA.ForceUpdateCache();
                Assert.IsTrue(queryA.IsCacheValid);
                Assert.DoesNotThrow(() => queryA.CheckChunkListCacheConsistency());

                // destroy entities
                m_Manager.DestroyEntity(queryA);
                Assert.IsFalse(queryA.IsCacheValid);
                Assert.Throws<ArgumentException>(() => queryA.CheckChunkListCacheConsistency(true));
            }
        }

        struct WriteComponentJob<T> : IJob
        {
            public ComponentTypeHandle<T> TypeHandle;
            public void Execute()
            {
            }
        }
        struct ReadComponentJob<T> : IJob
        {
            [ReadOnly] public ComponentTypeHandle<T> TypeHandle;
            public void Execute()
            {
            }
        }

        [Test]
        public void CalculateEntityCount_WithoutFiltering_WithEnableableComponent_IgnoresDisabledComponents()
        {
            int entityCount = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData4), typeof(EcsTestDataEnableable));
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            int disabledCount = 0;
            for (int i = 0; i < entities.Length; i += 100)
            {
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                disabledCount += 1;
            }
            // With filtering, the disabled components should not be counted
            Assert.AreEqual(entityCount - disabledCount, query.CalculateEntityCount());
            // Without filtering, the disabled components should still be counted
            Assert.AreEqual(entityCount, query.CalculateEntityCountWithoutFiltering());
        }

        unsafe struct ReadOnlyArrayContainer : IDisposable
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;
            static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<ReadOnlyArrayContainer>();

            [NativeSetClassTypeToNullOnSchedule]
            DisposeSentinel m_DisposeSentinel;
#endif

            [NativeDisableUnsafePtrRestriction]
            private UnsafeList<Entity>* m_Data;

            private readonly Allocator m_Allocator;

            public ReadOnlyArrayContainer(int initialCapacity, Allocator allocator)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 2, allocator);

                CollectionHelper.SetStaticSafetyId(ref m_Safety, ref s_staticSafetyId.Data, "Unity.Entities.Tests.EntityQueryTests.ReadOnlyArrayContainer");
#endif

                m_Data = UnsafeList<Entity>.Create(initialCapacity, allocator);
                m_Allocator = allocator;
            }

            public void AddRange(NativeArray<Entity> entities)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
                m_Data->AddRange(entities.GetUnsafeReadOnlyPtr(), entities.Length);
            }

            public NativeArray<Entity> GetValueArray()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
                var arraySafety = m_Safety;
                AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif

                var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Entity>(m_Data->Ptr, m_Data->Length, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.SetAllowSecondaryVersionWriting(arraySafety, false);
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
                return array;
            }

            public void Dispose()
            {
                if (m_Data != null)
                {
                    UnsafeList<Entity>.Destroy(m_Data);
                    m_Data = null;
                }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            }
        }

        struct ReadFromEntityJob : IJob
        {
            [ReadOnly] public ComponentTypeHandle<Entity> TypeHandle;
            public void Execute()
            {
            }
        }

        struct WriteToEntityJob : IJob
        {
            public ComponentTypeHandle<Entity> TypeHandle;
            public void Execute()
            {
            }
        }


        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ToEntityArray_WithRunningJob_NoDependency_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, 10000, World.UpdateAllocator.ToAllocator);
            NativeArray<Entity> output = default;

            // Test with a job that reads from entity data; this should work fine.
            var typeHandleRO = m_Manager.GetComponentTypeHandle<Entity>(true);
            var jobHandleRO = new ReadFromEntityJob { TypeHandle = typeHandleRO }.Schedule();
            //query.AddDependency(jobHandleRO); //This test makes sure we detect the race condition if this dependency is NOT established.
            Assert.DoesNotThrow(() => { output = query.ToEntityArray(World.UpdateAllocator.ToAllocator); });
            jobHandleRO.Complete();
            Assert.AreEqual(entities.Length, output.Length);
            for (int i = 0; i < entities.Length; i++)
                FastAssert.AreEqual(entities[i], output[i]);
            if (output.IsCreated)
                output.Dispose();

            // It's highly unusual for a job to have write access to the Entity component, but for symmetry with the subsequent
            // tests we'll test it here anyway.
            var typeHandleRW = m_Manager.GetComponentTypeHandle<Entity>(false);
            var jobHandleRW = new WriteToEntityJob { TypeHandle = typeHandleRW }.Schedule();
            //query.AddDependency(jobHandleRW); //This test makes sure we detect the race condition if this dependency is NOT established.
            Assert.Throws<InvalidOperationException>(() => { output = query.ToEntityArray(World.UpdateAllocator.ToAllocator); });
            jobHandleRW.Complete();
        }

        [Test]
        public unsafe void ToEntityArray_WithRunningJob_WithDependency_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, 10000, World.UpdateAllocator.ToAllocator);
            NativeArray<Entity> output = default;

            // query.AddDependency() doesn't track dependencies for the Entity type, so we need to go deeper for this test
            var dependencyManager = m_Manager.GetCheckedEntityDataAccess()->DependencyManager;
            var entityTypeIndex = TypeManager.GetTypeIndex<Entity>();

            // Test with a job that reads from entity data; this should work fine.
            var typeHandleRO = m_Manager.GetComponentTypeHandle<Entity>(true);
            var jobHandleRO = new ReadFromEntityJob { TypeHandle = typeHandleRO }.Schedule();
            //query.AddDependency(jobHandleRO); // not sufficient; the query doesn't consider Entity a reader/writer type
            dependencyManager->AddDependency(&entityTypeIndex, 1, null, 0, jobHandleRO);
            Assert.DoesNotThrow(() => { output = query.ToEntityArray(World.UpdateAllocator.ToAllocator); });
            jobHandleRO.Complete();
            Assert.AreEqual(entities.Length, output.Length);
            for (int i = 0; i < entities.Length; i++)
                FastAssert.AreEqual(entities[i], output[i]);
            if (output.IsCreated)
                output.Dispose();

            // It's highly unusual for a job to have write access to the Entity component, but for symmetry with the subsequent
            // tests we'll test it here anyway.
            var typeHandleRW = m_Manager.GetComponentTypeHandle<Entity>(false);
            var jobHandleRW = new WriteToEntityJob { TypeHandle = typeHandleRW }.Schedule();
            //query.AddDependency(jobHandleRW); // not sufficient; the query doesn't consider Entity a reader/writer type
            dependencyManager->AddDependency(null, 0, &entityTypeIndex, 1, jobHandleRW);
            Assert.DoesNotThrow(() => { output = query.ToEntityArray(World.UpdateAllocator.ToAllocator); });
            jobHandleRW.Complete();
            Assert.AreEqual(entities.Length, output.Length);
            for (int i = 0; i < entities.Length; i++)
                FastAssert.AreEqual(entities[i], output[i]);
            if (output.IsCreated)
                output.Dispose();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ToComponentDataArray_WithRunningJob_NoDependency_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, 1000, World.UpdateAllocator.ToAllocator);

            for (int i = 0; i < entities.Length; ++i)
            {
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));
            }

            // test with running job reading from component data. This should work fine.
            NativeArray<EcsTestData> testValues = default;
            using var readValues = query.ToComponentDataListAsync<EcsTestData>(World.UpdateAllocator.ToAllocator, out var readJobHandle);
            //query.AddDependency(readJobHandle); //This test makes sure we detect the race condition if this dependency is NOT established.
            Assert.DoesNotThrow(() => { testValues = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator); });
            readJobHandle.Complete();
            Assert.AreEqual(readValues.Length, testValues.Length);
            for (int i = 0; i < readValues.Length; i++)
                FastAssert.AreEqual(readValues[i].value, testValues[i].value);

            if (testValues.IsCreated)
                testValues.Dispose();

            // test with running job writing to component data. This is a race condition.
            var typeHandleRW = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
            var writeJobHandle = new WriteComponentJob<EcsTestData> { TypeHandle = typeHandleRW }.Schedule();
            Assert.Throws<InvalidOperationException>(() => { testValues = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator); });
            writeJobHandle.Complete();
            if (testValues.IsCreated)
                testValues.Dispose();
        }

        [Test]
        public void ToComponentDataArray_WithRunningJob_WithDependency_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, 1000, World.UpdateAllocator.ToAllocator);

            var newValues = new NativeList<EcsTestData>(entities.Length, World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entities.Length; ++i)
            {
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));
                newValues.Add(new EcsTestData(2*i));
            }

            // test with running job reading from component data
            NativeArray<EcsTestData> testValues = default;
            using var readValues = query.ToComponentDataListAsync<EcsTestData>(World.UpdateAllocator.ToAllocator, out var readJobHandle);
            query.AddDependency(readJobHandle); // ensure the following query operation sees this job when it completes its dependencies
            Assert.DoesNotThrow(() => { testValues = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator); });
            Assert.AreEqual(readValues.Length, testValues.Length);
            for (int i = 0; i < readValues.Length; i++)
                FastAssert.AreEqual(readValues[i].value, testValues[i].value);
            testValues.Dispose();

            // test with running job writing to component data
            query.CopyFromComponentDataListAsync(newValues, out var writeJobHandle);
            query.AddDependency(writeJobHandle); // ensure the following query operation sees this job when it completes its dependencies
            Assert.DoesNotThrow(() => { testValues = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator); });
            Assert.AreEqual(newValues.Length, testValues.Length);
            for (int i = 0; i < newValues.Length; i++)
                FastAssert.AreEqual(newValues[i].value, testValues[i].value);
            testValues.Dispose();

            newValues.Dispose();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CopyFromComponentDataArray_WithRunningJob_NoDependency_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, 1000, World.UpdateAllocator.ToAllocator);

            NativeArray<EcsTestData> newValues =
                CollectionHelper.CreateNativeArray<EcsTestData>(entities.Length, World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entities.Length; ++i)
            {
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));
                newValues[i] = new EcsTestData(2*i);
            }

            // test with running job reading from component data. This is a race condition.
            var typeHandleRO = m_Manager.GetComponentTypeHandle<EcsTestData>(true);
            var readJobHandle = new ReadComponentJob<EcsTestData> { TypeHandle = typeHandleRO }.Schedule();
            Assert.Throws<InvalidOperationException>(() => { query.CopyFromComponentDataArray(newValues); });
            readJobHandle.Complete();

            // test with running job writing to component data. This is a race condition.
            var typeHandleRW = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
            var writeJobHandle = new WriteComponentJob<EcsTestData> { TypeHandle = typeHandleRW }.Schedule();
            Assert.Throws<InvalidOperationException>(() => { query.CopyFromComponentDataArray(newValues); });
            writeJobHandle.Complete();

            newValues.Dispose();
        }

        [Test]
        public void CopyFromComponentDataArray_WithRunningJob_WithDependency_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, 1000, World.UpdateAllocator.ToAllocator);

            NativeArray<EcsTestData> newValues =
                CollectionHelper.CreateNativeArray<EcsTestData>(entities.Length, World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entities.Length; ++i)
            {
                m_Manager.SetComponentData(entities[i], new EcsTestData(i));
                newValues[i] = new EcsTestData(2*i);
            }

            // test with running job reading from component data
            using var readValues = query.ToComponentDataListAsync<EcsTestData>(World.UpdateAllocator.ToAllocator, out var readJobHandle);
            query.AddDependency(readJobHandle); // ensure the following query operation sees this job when it completes its dependencies
            Assert.DoesNotThrow(() => { query.CopyFromComponentDataArray(newValues); });
            for (int i = 0; i < entities.Length; ++i)
            {
                FastAssert.AreEqual(i, readValues[i].value);
                FastAssert.AreEqual(newValues[i].value, m_Manager.GetComponentData<EcsTestData>(entities[i]).value);
            }

            // test with running job writing to component data
            query.CopyFromComponentDataListAsync(readValues, out var writeJobHandle);
            query.AddDependency(writeJobHandle); // ensure the following query operation sees this job when it completes its dependencies
            Assert.DoesNotThrow(() => { query.CopyFromComponentDataArray(newValues); });
            for (int i = 0; i < entities.Length; ++i)
            {
                FastAssert.AreEqual(newValues[i].value, m_Manager.GetComponentData<EcsTestData>(entities[i]).value);
            }
        }

        public partial struct WriteEnableableSystem : ISystem
        {
            private EntityQuery _query;

            public void OnCreate(ref SystemState state)
            {
                _query = SystemAPI.QueryBuilder()
                    .WithAll<EcsTestData>()
                    .WithDisabledRW<EcsTestDataEnableable>()
                    .Build();
            }

            public void OnUpdate(ref SystemState state)
            {
                state.Dependency = new TestJob().ScheduleParallel(_query, state.Dependency);
            }

            private partial struct TestJob : IJobEntity
            {
                private void Execute(in EcsTestData test, EnabledRefRW<EcsTestDataEnableable> test2)
                {
                }
            }
        }
        public partial struct WithNoneEnableableSystem : ISystem
        {
            private EntityQuery _query;

            public void OnCreate(ref SystemState state)
            {
                _query = SystemAPI.QueryBuilder()
                    .WithAll<EcsTestData>()
                    .WithNone<EcsTestDataEnableable>()
                    .Build();
            }

            public void OnUpdate(ref SystemState state)
            {
                state.Dependency = new TestJob().ScheduleParallel(_query, state.Dependency);
            }

            private partial struct TestJob : IJobEntity
            {
                private void Execute(in EcsTestData test)
                {
                }
            }
        }
        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void WithNone_EnableableType_HasCorrectJobDependencies()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var entities = m_Manager.CreateEntity(archetype, 1000, World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entities.Length; ++i)
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
            // A system with a query including WithNone<EnableableT>() (which implicitly reads the enabled bits
            // of EnableableT, but does not directly manipulate the type)
            // must wait on jobs that write to EnableableT (which may toggle its enabled bits).
            var sys1 = World.CreateSystem<WriteEnableableSystem>();
            var sys2 = World.CreateSystem<WithNoneEnableableSystem>();
            sys1.Update(World.Unmanaged);
            sys2.Update(World.Unmanaged);
        }

        public partial struct WithAnyEnableableSystem : ISystem {
            private EntityQuery _query;

            public void OnCreate(ref SystemState state) {
                _query = SystemAPI.QueryBuilder()
                    .WithAll<EcsTestData>()
                    .WithAny<EcsTestDataEnableable,EcsTestData2>()
                    .Build();
            }

            public void OnUpdate(ref SystemState state) {
                state.Dependency = new TestJob().ScheduleParallel(_query, state.Dependency);
            }

            private partial struct TestJob : IJobEntity {
                private void Execute(in EcsTestData test) {
                }
            }
        }
        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void WithAny_EnableableType_HasCorrectJobDependencies() {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var entities = m_Manager.CreateEntity(archetype, 1000, World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entities.Length; ++i)
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
            // A system with a query including WithAny<EnableableT,...>() (which implicitly reads the enabled bits
            // of EnableableT, but does not directly manipulate the type)
            // must wait on jobs that write to EnableableT (which may toggle its enabled bits).
            var sys1 = World.CreateSystem<WriteEnableableSystem>();
            var sys2 = World.CreateSystem<WithAnyEnableableSystem>();
            sys1.Update(World.Unmanaged);
            sys2.Update(World.Unmanaged);
        }

        struct ReadFromEnableableComponentJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<EcsTestDataEnableable> TypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
            }
        }

        struct WriteToEnableableComponentJob : IJobChunk
        {
            public ComponentTypeHandle<EcsTestDataEnableable> TypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ToEntityArray_WithWritingJobToEnableableType_Throws()
        {
            // It's highly unusual for a job to have write access to the Entity component, but for symmetry with the subsequent
            // tests we'll test it here anyway.
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var entities = m_Manager.CreateEntity(archetype, 10000, World.UpdateAllocator.ToAllocator);
            NativeArray<Entity> output = default;
            // read-only job should work fine
            var typeHandleRO = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(true);
            var readJobHandle = new ReadFromEnableableComponentJob { TypeHandle = typeHandleRO }.ScheduleParallel(query, default);
            Assert.DoesNotThrow(() => { output = query.ToEntityArray(World.UpdateAllocator.ToAllocator); });
            readJobHandle.Complete();
            Assert.AreEqual(entities.Length, output.Length);
            output.Dispose();
            // writing job should throw
            var typeHandleRW = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            var writeJobHandle = new WriteToEnableableComponentJob { TypeHandle = typeHandleRW }.ScheduleParallel(query, default);
            Assert.Throws<InvalidOperationException>(() => { output = query.ToEntityArray(World.UpdateAllocator.ToAllocator); });
            writeJobHandle.Complete();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ToComponentDataArray_WithWritingJobToEnableableType_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var entities = m_Manager.CreateEntity(archetype, 10000, World.UpdateAllocator.ToAllocator);
            NativeArray<EcsTestData> values = default;
            // read-only job should work fine
            var typeHandleRO = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(true);
            var readJobHandle = new ReadFromEnableableComponentJob { TypeHandle = typeHandleRO }.ScheduleParallel(query, default);
            Assert.DoesNotThrow(() => { values = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator); });
            readJobHandle.Complete();
            Assert.AreEqual(entities.Length, values.Length);
            values.Dispose();
            // writing job should throw
            var typeHandleRW = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            var writeJobHandle = new WriteToEnableableComponentJob { TypeHandle = typeHandleRW }.ScheduleParallel(query, default);
            Assert.Throws<InvalidOperationException>(() => { values = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator); });
            writeJobHandle.Complete();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CopyFromComponentDataArray_WithWritingJobToEnableableType_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var entities = m_Manager.CreateEntity(archetype, 10000, World.UpdateAllocator.ToAllocator);
            using var values =
                CollectionHelper.CreateNativeArray<EcsTestData>(entities.Length, World.UpdateAllocator.ToAllocator);
            // read-only job should work fine
            var typeHandleRO = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(true);
            var readJobHandle = new ReadFromEnableableComponentJob { TypeHandle = typeHandleRO }.ScheduleParallel(query, default);
            Assert.DoesNotThrow(() => { query.CopyFromComponentDataArray(values); });
            readJobHandle.Complete();
            Assert.AreEqual(entities.Length, values.Length);
            // writing job should throw
            var typeHandleRW = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            var writeJobHandle = new WriteToEnableableComponentJob { TypeHandle = typeHandleRW }.ScheduleParallel(query, default);
            Assert.Throws<InvalidOperationException>(() => { query.CopyFromComponentDataArray(values); });
            writeJobHandle.Complete();
        }

        partial class DisableTagComponentsSystemRO : SystemBase
        {
            protected override void OnCreate()
            {
            }

            protected override void OnUpdate()
            {
                Entities
                    .WithAll<EcsTestTagEnableable>()
                    .ForEach((Entity entity) =>
                    {
                    }).ScheduleParallel();
            }
        }

        partial class DisableTagComponentsSystemRW : SystemBase
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

        [Test]
        public void SetEnabledBitsOnAllChunks_WithJobReadingFromEnableableTag_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestTagEnableable));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestTagEnableable));
            using var entities = m_Manager.CreateEntity(archetype, 10000, World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entities.Length; i += 2)
            {
                m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
            }
            // A running job reading from the tag component should cause SetEnabledBitsOnAllChunks() to block
            var sysRO = World.CreateSystemManaged<DisableTagComponentsSystemRO>();
            sysRO.Update();
            Assert.DoesNotThrow(() => { m_Manager.SetComponentEnabled<EcsTestTagEnableable>(query, true); });
            Assert.AreEqual(entities.Length, query.CalculateEntityCount());
            foreach(var ent in entities)
            {
                FastAssert.IsTrue(m_Manager.IsComponentEnabled<EcsTestTagEnableable>(ent));
            }
        }

        [Test]
        public void SetEnabledBitsOnAllChunks_WithJobWritingToEnableableTag_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestTagEnableable));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestTagEnableable));
            using var entities = m_Manager.CreateEntity(archetype, 10000, World.UpdateAllocator.ToAllocator);

            // A running job writing to the component should cause SetEnabledBitsOnAllChunks() to block
            for (int i = 0; i < entities.Length; i += 2)
            {
                m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
            }
            var sysRW = World.CreateSystemManaged<DisableTagComponentsSystemRW>();
            sysRW.Update();
            Assert.DoesNotThrow(() => {m_Manager.SetComponentEnabled<EcsTestTagEnableable>(query, true); });
            Assert.AreEqual(entities.Length, query.CalculateEntityCount());
            foreach(var ent in entities)
            {
                FastAssert.IsTrue(m_Manager.IsComponentEnabled<EcsTestTagEnableable>(ent));
            }

            // writing job should throw (blocks until job is complete)
            //Assert.DoesNotThrow(() => { query.SetEnabledBitsOnAllChunks<EcsTestTagEnableable>(false); });
            //writeJobHandle.Complete();
            //Assert.AreEqual(entities.Length, query.CalculateEntityCount());
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CalculateEntityCount_WithWritingJobToEnableableType_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var entities = m_Manager.CreateEntity(archetype, 10000, World.UpdateAllocator.ToAllocator);
            int count = 0;
            // read-only job should work fine
            var typeHandleRO = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(true);
            var readJobHandle = new ReadFromEnableableComponentJob { TypeHandle = typeHandleRO }.ScheduleParallel(query, default);
            Assert.DoesNotThrow(() => { count = query.CalculateEntityCount(); });
            readJobHandle.Complete();
            Assert.AreEqual(entities.Length, count);
            // writing job should throw
            var typeHandleRW = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            var writeJobHandle = new WriteToEnableableComponentJob { TypeHandle = typeHandleRW }.ScheduleParallel(query, default);
            Assert.Throws<InvalidOperationException>(() => { count = query.CalculateEntityCount(); });
            writeJobHandle.Complete();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void Matches_WithWritingJobToEnableableType_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData,EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            using var entities = m_Manager.CreateEntity(archetype, 10000, World.UpdateAllocator.ToAllocator);
            bool matches = false;
            // read-only job should work fine
            var typeHandleRO = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(true);
            var readJobHandle = new ReadFromEnableableComponentJob { TypeHandle = typeHandleRO }.ScheduleParallel(query, default);
            Assert.DoesNotThrow(() => { matches = query.MatchesIgnoreFilter(entities[0]); });
            Assert.IsTrue(matches);
            matches = query.Matches(entities[0]);
            readJobHandle.Complete();
            //Assert.IsTrue(matches);
            // writing job should throw
            var typeHandleRW = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            var writeJobHandle = new WriteToEnableableComponentJob { TypeHandle = typeHandleRW }.ScheduleParallel(query, default);
            Assert.Throws<InvalidOperationException>(() => { matches = query.Matches(entities[0]); });
            writeJobHandle.Complete();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void IsEmpty_WithWritingJobToEnableableType_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestDataEnableable));
            using var entities = m_Manager.CreateEntity(archetype, 10000, World.UpdateAllocator.ToAllocator);
            bool empty = false;
            // read-only job should work fine
            var typeHandleRO = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(true);
            var readJobHandle = new ReadFromEnableableComponentJob { TypeHandle = typeHandleRO }.ScheduleParallel(query, default);
            Assert.DoesNotThrow(() => { empty = query.IsEmpty; });
            readJobHandle.Complete();
            Assert.IsFalse(empty);
            // writing job should throw
            var typeHandleRW = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            var writeJobHandle = new WriteToEnableableComponentJob { TypeHandle = typeHandleRW }.ScheduleParallel(query, default);
            Assert.Throws<InvalidOperationException>(() => { empty = query.IsEmpty; });
            writeJobHandle.Complete();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void WithEntityType_Throws()
        {
            // Entity is always included as an implicit type. Including it in the components list
            // messes with query equality testing.
            Assert.Throws<ArgumentException>(() =>
            {
                m_Manager.CreateEntityQuery(typeof(Entity), typeof(EcsTestData));
            });
        }

        partial class EntityQueryBuilderTestSystem : SystemBase
        {
            public EntityQuery Query;
            public JobHandle JobDependency => Dependency;

            protected override void OnCreate()
            {
                Query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<EcsTestData>()
                    .WithAll<EcsTestData2>()
                    .WithNone<EcsTestTag>()
                    .Build(this);
            }

            protected override void OnUpdate()
            {
                Entities.WithNone<EcsTestTag>().ForEach((ref EcsTestData sum, in EcsTestData2 addends) =>
                {
                    sum.value = addends.value0 + addends.value1;
                }).Schedule();
            }
        }

        [Test]
        public void SystemBase_GetEntityQueryFromEntityQueryBuilder_Works()
        {
            var entityWithTag = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestTag));
            var entityWithoutTag = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            m_Manager.SetComponentData(entityWithoutTag, new EcsTestData2{ value0 = 27, value1 = 15 });

            var sys = World.GetOrCreateSystemManaged<EntityQueryBuilderTestSystem>();
            sys.Update();

            Assert.AreEqual(1, sys.Query.CalculateEntityCount());

            sys.JobDependency.Complete();
            var arr = sys.Query.ToComponentDataArray<EcsTestData>(Allocator.Temp);
            Assert.AreEqual(42, arr[0].value);
        }

        [Test]
        public void EntityQueryBuilder_WithAnyRW_Works()
        {
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData2));
            var entity12 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var entity12Tag = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestTag));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAnyRW<EcsTestData, EcsTestData2>()
                .WithNone<EcsTestTag>()
                .Build(EmptySystem);

            CollectionAssert.AreEquivalent(new[]{entity1, entity2, entity12},
                query.ToEntityArray(Allocator.Temp).ToArray());
        }

        [Test]
        public void EntityQueryBuilder_WithDisabled_Works()
        {
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestDataEnableable2));
            var entity12 = m_Manager.CreateEntity(typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable2>(entity12,false);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithDisabled<EcsTestDataEnableable2>()
                .Build(EmptySystem);
            var query2 = new EntityQueryBuilder(Allocator.Temp)
                .WithDisabledRW<EcsTestDataEnableable2>()
                .Build(EmptySystem);

            CollectionAssert.AreEquivalent(new[]{entity12},
                query.ToEntityArray(Allocator.Temp).ToArray());
            CollectionAssert.AreEquivalent(new[]{entity12},
                query2.ToEntityArray(Allocator.Temp).ToArray());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void EntityQueryBuilder_WithDisabled_NotEnableableComponent_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new EntityQueryBuilder(Allocator.Temp)
                .WithDisabled<EcsTestData>()
                .Build(EmptySystem));
        }

        [Test]
        public void EntityQueryBuilder_WithAbsent_Works()
        {
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestDataEnableable2));
            var entity12 = m_Manager.CreateEntity(typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable2>(entity12,false);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAbsent<EcsTestDataEnableable2>()
                .Build(EmptySystem);

            CollectionAssert.AreEquivalent(new[]{entity1},
                query.ToEntityArray(Allocator.Temp).ToArray());
        }

        [Test]
        public void EntityQueryBuilder_WithPresent_Works()
        {
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestDataEnableable2));
            var entity12 = m_Manager.CreateEntity(typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable2>(entity12,false);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithPresent<EcsTestDataEnableable2>()
                .Build(EmptySystem);

            CollectionAssert.AreEquivalent(new[]{entity2,entity12},
                query.ToEntityArray(Allocator.Temp).ToArray());
        }

        [Test]
        public void EntityQueryBuilder_ConstructedWithoutAllocator_Throws()
        {
            Assert.Throws<NullReferenceException>(() => {
                var builder = new EntityQueryBuilder();
                builder.WithAll<EcsTestData>();
            });
        }

        [Test]
        public void EntityQueryBuilder_WithSharedComponent_Works()
        {
            var entityWithShared1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var entityWithShared2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var entityWithoutShared = m_Manager.CreateEntity(typeof(EcsTestData));

            m_Manager.SetSharedComponent(entityWithShared1, new EcsTestSharedComp{ value = 1});
            m_Manager.SetSharedComponent(entityWithShared2, new EcsTestSharedComp{ value = 2});

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData, EcsTestSharedComp>()
                .Build(EmptySystem);

            CollectionAssert.AreEquivalent(new[]{entityWithShared1,entityWithShared2},
                query.ToEntityArray(Allocator.Temp).ToArray());
        }

        [Test]
        public void EntityQueryBuilder_WithChunkComponent_Works()
        {
            var entityWithChunkComponent = m_Manager.CreateEntity(ComponentType.ChunkComponent<EcsTestData>());
            var entityWithNonChunkComponent = m_Manager.CreateEntity(typeof(EcsTestData));
            var entityWithOtherComponent = m_Manager.CreateEntity(typeof(EcsTestData2));

            var componentTypes = new FixedList32Bytes<ComponentType>();
            componentTypes.Add(ComponentType.ChunkComponent<EcsTestData>());
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll(ref componentTypes)
                .Build(EmptySystem);

            var query2 = new EntityQueryBuilder(Allocator.Temp)
                .WithAllChunkComponent<EcsTestData>()
                .Build(EmptySystem);

            var query3 = new EntityQueryBuilder(Allocator.Temp)
                .WithAllChunkComponentRW<EcsTestData>()
                .Build(EmptySystem);

            CollectionAssert.AreEquivalent(new[]{entityWithChunkComponent},
                query.ToEntityArray(Allocator.Temp).ToArray());
            CollectionAssert.AreEquivalent(new[]{entityWithChunkComponent},
                query2.ToEntityArray(Allocator.Temp).ToArray());
            CollectionAssert.AreEquivalent(new[]{entityWithChunkComponent},
                query3.ToEntityArray(Allocator.Temp).ToArray());
        }

        [Test]
        public void EntityQueryBuilder_WithAnyChunkComponent_Works()
        {
            var entityWithChunkComponent1 = m_Manager.CreateEntity(ComponentType.ChunkComponent<EcsTestData>());
            var entityWithChunkComponent2 = m_Manager.CreateEntity(ComponentType.ChunkComponent<EcsTestData2>());
            var entityWithNonChunkComponent = m_Manager.CreateEntity(typeof(EcsTestData));
            var entityWithOtherComponent = m_Manager.CreateEntity(typeof(EcsTestData2));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAnyChunkComponent<EcsTestData>()
                .WithAnyChunkComponentRW<EcsTestData2>()
                .Build(EmptySystem);

            CollectionAssert.AreEquivalent(new[]{entityWithChunkComponent1,entityWithChunkComponent2},
                query.ToEntityArray(Allocator.Temp).ToArray());
        }

        [Test]
        public void EntityQueryBuilder_WithNoneChunkComponent_Works()
        {
            var entityWithChunkComponent = m_Manager.CreateEntity(ComponentType.ChunkComponent<EcsTestData>());
            var entityWithNonChunkComponent = m_Manager.CreateEntity(typeof(EcsTestData));
            var entityWithOtherComponent = m_Manager.CreateEntity(typeof(EcsTestData2));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithNoneChunkComponent<EcsTestData>()
                .Build(EmptySystem);

            CollectionAssert.AreEquivalent(new[]{entityWithNonChunkComponent,entityWithOtherComponent},
                query.ToEntityArray(Allocator.Temp).ToArray());
        }

        [Test]
        public void EntityQueryBuilder_WithCleanupComponent_Works()
        {
            var entityWithCleanup = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsCleanup1));
            var destroyedEntityWithCleanup = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsCleanup1));
            var entityWithoutCleanup = m_Manager.CreateEntity(typeof(EcsTestData));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsCleanup1>()
                .Build(EmptySystem);

            CollectionAssert.AreEquivalent(new[]{entityWithCleanup,destroyedEntityWithCleanup},
                query.ToEntityArray(Allocator.Temp).ToArray());
            m_Manager.DestroyEntity(destroyedEntityWithCleanup);
            CollectionAssert.AreEquivalent(new[]{entityWithCleanup,destroyedEntityWithCleanup},
                query.ToEntityArray(Allocator.Temp).ToArray());
        }

        [Test]
        public void EntityQueryBuilder_WithAspect_Works()
        {
            var entityWithAspect = m_Manager.CreateEntity(GetRequiredComponents<MyAspect>());
            var entityWithNothing = m_Manager.CreateEntity();
            var entityWithOtherComponent = m_Manager.CreateEntity(typeof(EcsTestData2));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAspect<MyAspect>()
                .Build(EmptySystem);

            CollectionAssert.AreEquivalent(new[]{entityWithAspect},
                query.ToEntityArray(Allocator.Temp).ToArray());
        }

        [Test]
        public void EntityQueryBuilder_WithAspect_Alias_WithAll_Works()
        {
            var entityWithAspect = m_Manager.CreateEntity(GetRequiredComponents<MyAspect>());
            var entityWithNothing = m_Manager.CreateEntity();
            var entityWithOtherComponent = m_Manager.CreateEntity(typeof(EcsTestData2));

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestData>()
                .WithAspect<MyAspect>()
                .Build(EmptySystem);

            CollectionAssert.AreEquivalent(new[] { entityWithAspect },
                query.ToEntityArray(Allocator.Temp).ToArray());
        }

        partial struct EmptyQueryISystem : ISystem
        {
            public EntityQuery EmptyQuery;

            public void OnCreate(ref SystemState state)
            {
                EmptyQuery = new EntityQueryBuilder(Allocator.Temp).Build(ref state);
            }
            }

        [Test]
        public void EntityQueryBuilder_EmptyQueryGetsFinalized()
        {
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData2));

            var system = World.Unmanaged.GetUnsafeSystemRef<EmptyQueryISystem>(World.CreateSystem<EmptyQueryISystem>());

            CollectionAssert.AreEquivalent(new[]{entity1,entity2},
                system.EmptyQuery.ToEntityArray(Allocator.Temp).ToArray());
        }

        partial struct MultipleQueryISystem : ISystem
        {
            EntityQuery m_Query1;
            EntityQuery m_Query2;
            EntityQuery m_Query3;

            public void OnCreate(ref SystemState state)
            {
                m_Query1 = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestData>().Build(ref state);
                m_Query2 = new EntityQueryBuilder(Allocator.Temp).WithAll<EndSimulationEntityCommandBufferSystem.Singleton>().Build(ref state);
                m_Query3 = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(ref state);
            }
        }

        [Test]
        public unsafe void MultipleQueryISystem_EntityQueryCache_ValidAfterReallocation()
        {
            var systemRef = World.CreateSystem<MultipleQueryISystem>();
            var systemState = World.Unmanaged.ResolveSystemState(systemRef);

            Assert.AreEqual(3,systemState->EntityQueries.Length);
            Assert.AreEqual(4,systemState->EntityQueries.Capacity);
        }

        partial struct MultipleQueryISystemWithSourcegen : ISystem
        {
            EntityQuery m_AvailableTargets;

            // One query added in OnCreate, two added by sourcegenerated OnCreateForCompiler
            public void OnCreate(ref SystemState state)
            {
                m_AvailableTargets = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(ref state);
            }

            public void OnUpdate(ref SystemState state)
            {
                SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
                foreach (var translation in SystemAPI.Query<RefRW<EcsTestData>>())
                {}
            }
        }

        [Test]
        public unsafe void MultipleQueryISystemWithSourcegen_EntityQueryCache_ValidAfterReallocation()
        {
            var systemRef = World.CreateSystem<MultipleQueryISystemWithSourcegen>();
            var systemState = World.Unmanaged.ResolveSystemState(systemRef);

            Assert.AreEqual(3,systemState->EntityQueries.Length);
            Assert.AreEqual(4,systemState->EntityQueries.Capacity);
        }

        public partial class CachedSystemQueryTestSystem : SystemBase
        {
            protected override void OnCreate()
            {
                // Caches a query in the system.
                // This occurs before the implicit query is created and will be first in the cached list.
                GetEntityQuery(new EntityQueryDesc
                {
                    All = new[] {ComponentType.ReadWrite<EcsTestData>()},
                    None = new[] {ComponentType.ReadOnly<EcsTestTag>()}
                });
            }

            protected override void OnUpdate()
            {
                 Entities.ForEach((ref EcsTestData data) => { data.value = 10; }).Schedule();
            }
        }
        [Test]
        public void CachedSystemQueryReturnsOnlyExactQuery()
        {
            var entityA = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestTag));
            var entityB = m_Manager.CreateEntity(typeof(EcsTestData));

            var testSystem = World.GetOrCreateSystemManaged<CachedSystemQueryTestSystem>();
            testSystem.Update();

            Assert.AreEqual(2, testSystem.EntityQueries.Length);
            Assert.AreEqual(10, m_Manager.GetComponentData<EcsTestData>(entityA).value);
            Assert.AreEqual(10, m_Manager.GetComponentData<EcsTestData>(entityB).value);

            var queryA = new EntityQueryBuilder(Allocator.Temp).WithAllRW<EcsTestData>().WithNone<EcsTestTag>()
                .Build(testSystem);
            var queryB = testSystem.GetEntityQuery(ComponentType.ReadWrite<EcsTestData>(), ComponentType.Exclude<EcsTestTag>());

            Assert.AreEqual(queryA, queryB);
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        private class ManagedComponent : IComponentData
        {
            public int Value;
        }

        [Test]
        public void Managed_ToComponentDataArray_Respects_Filter()
        {
            const int kShared1 = 5;
            const int kShared2 = 7;
            const int kShared3 = 11;

            for (int i = 0; i < kShared1; ++i)
            {
                var entity = m_Manager.CreateEntity(typeof(ManagedComponent), typeof(EcsTestSharedComp));
                m_Manager.SetComponentData(entity, new ManagedComponent() { Value = 1 });
                m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp() { value = 1 });
            }
            for (int i = 0; i < kShared2; ++i)
            {
                var entity = m_Manager.CreateEntity(typeof(ManagedComponent), typeof(EcsTestSharedComp));
                m_Manager.SetComponentData(entity, new ManagedComponent() { Value = 2 });
                m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp() { value = 2 });
            }
            for (int i = 0; i < kShared3; ++i)
            {
                var entity = m_Manager.CreateEntity(typeof(ManagedComponent), typeof(EcsTestSharedComp));
                m_Manager.SetComponentData(entity, new ManagedComponent() { Value = 3 });
                m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp() { value = 3 });
            }

            var allSharedComponents = new List<EcsTestSharedComp>();
            m_Manager.GetAllUniqueSharedComponentsManaged(allSharedComponents);

            var query = m_Manager.CreateEntityQuery(typeof(ManagedComponent), typeof(EcsTestSharedComp));
            foreach (var shared in allSharedComponents)
            {
                query.SetSharedComponentFilterManaged(shared);
                var comps = query.ToComponentDataArray<ManagedComponent>();

                if (shared.value == 1)
                    Assert.AreEqual(kShared1, comps.Length);
                else if (shared.value == 2)
                    Assert.AreEqual(kShared2, comps.Length);
                else if (shared.value == 3)
                    Assert.AreEqual(kShared3, comps.Length);

                foreach (var comp in comps)
                    Assert.AreEqual(shared.value, comp.Value);
            }
            query.ResetFilter();
        }

#endif

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void QueryFromWrongWorldThrows()
        {
            using (var world = new World("temp"))
            using (var array = new NativeArray<EcsTestData2>(1, Allocator.Persistent))
            {
                Assert.Throws<ArgumentException>(() => m_Manager.AddComponent(world.EntityManager.UniversalQuery, typeof(EcsTestData)));
                Assert.Throws<ArgumentException>(() => m_Manager.AddComponent(world.EntityManager.UniversalQuery, new ComponentTypeSet(typeof(EcsTestData))));
                Assert.Throws<ArgumentException>(() => m_Manager.AddComponentData(world.EntityManager.UniversalQuery, array));
                Assert.Throws<ArgumentException>(() => m_Manager.AddSharedComponentManaged(world.EntityManager.UniversalQuery, new EcsTestSharedComp()));
                Assert.Throws<ArgumentException>(() => m_Manager.SetSharedComponentManaged(world.EntityManager.UniversalQuery, new EcsTestSharedComp(1)));
                Assert.Throws<ArgumentException>(() => m_Manager.DestroyEntity(world.EntityManager.UniversalQuery));
                Assert.Throws<ArgumentException>(() => m_Manager.RemoveComponent<EcsTestData>(world.EntityManager.UniversalQuery));
                Assert.Throws<ArgumentException>(() => m_Manager.RemoveComponent(world.EntityManager.UniversalQuery, new ComponentTypeSet(typeof(EcsTestData))));
                Assert.Throws<ArgumentException>(() => m_Manager.AddChunkComponentData(world.EntityManager.UniversalQuery, new EcsTestData(1)));
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                Assert.Throws<ArgumentException>(() => m_Manager.AddChunkComponentData(world.EntityManager.UniversalQuery, new EcsTestManagedComponent() { value = "SomeString" }));
#endif
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void QueryAlreadyDisposedThrowsThrows()
        {
            EntityQuery query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            query.Dispose();
            using (var array = new NativeArray<EcsTestData2>(1, Allocator.Persistent))
            {
                Assert.Throws<ArgumentException>(() => m_Manager.AddComponent(query, typeof(EcsTestData)));
                Assert.Throws<ArgumentException>(() => m_Manager.AddComponent(query, new ComponentTypeSet(typeof(EcsTestData))));
                Assert.Throws<ArgumentException>(() => m_Manager.AddComponentData(query, array));
                Assert.Throws<ArgumentException>(() => m_Manager.AddSharedComponentManaged(query, new EcsTestSharedComp()));
                Assert.Throws<ArgumentException>(() => m_Manager.SetSharedComponentManaged(query, new EcsTestSharedComp(1)));
                Assert.Throws<ArgumentException>(() => m_Manager.DestroyEntity(query));
                Assert.Throws<ArgumentException>(() => m_Manager.RemoveComponent<EcsTestData>(query));
                Assert.Throws<ArgumentException>(() => m_Manager.RemoveComponent(query, new ComponentTypeSet(typeof(EcsTestData))));
                Assert.Throws<ArgumentException>(() => m_Manager.AddChunkComponentData(query, new EcsTestData(1)));

// Relies on dispose checks from Atomic Safety Handles
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.Throws<ObjectDisposedException>(() => query.GetEntityQueryMask());
#endif

#if !UNITY_DISABLE_MANAGED_COMPONENTS
                Assert.Throws<ArgumentException>(() => m_Manager.AddChunkComponentData(query, new EcsTestManagedComponent() { value = "SomeString" }));
#endif
            }
        }

        [Test]
        public void BuilderIsDisposable()
        {
            EntityQuery query;

            {
                using var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData, EcsTestData2>();
                query = EmptySystem.GetEntityQuery(builder);
            }

            Assert.AreEqual(2, query.GetReadAndWriteTypes().Length);
        }

        [Test]
        public unsafe void FinalizeQueryIsIdempotent()
        {
            var query1 = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(EmptySystem);
            // Note: the builder passed to GetEntityQuery (or Build) will be finalized twice, so this one needs more.
            var builder2 = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>()
                .FinalizeQueryInternal()
                .FinalizeQueryInternal()
                .FinalizeQueryInternal()
                .FinalizeQueryInternal();

            Assert.AreEqual(1, builder2._builderDataPtr->_indexData.Length, "multiply-finalized query builder should still only have one query desc.");
            Assert.IsTrue(query1.CompareQuery(builder2), "multiply-finalized query builder should compare equally with non-finalized query.");
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void EntityQueryBuilder_WithOptions_ThrowsIfCalledTwice()
        {
            // Calling once with combined options is fine.
            var builder1 = new EntityQueryBuilder(Allocator.Temp);
            builder1.WithAll<EcsTestData>();
            builder1.WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab);
            builder1.WithNone<EcsTestTag>();
            var query1 = builder1.Build(EmptySystem);

            // Calling on separate archetype queries is fine
            var builder2 = new EntityQueryBuilder(Allocator.Temp);
            builder2.WithAll<EcsTestData>().WithOptions(EntityQueryOptions.IncludeDisabledEntities);
            builder2.AddAdditionalQuery().WithAll<EcsTestData2>().WithOptions(EntityQueryOptions.IncludePrefab);
            var query2 = builder1.Build(EmptySystem);

            // Calling twice on the same query should throw
            Assert.Throws<InvalidOperationException>(() =>
            {
                var builder3 = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<EcsTestData>()
                    .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                    .WithOptions(EntityQueryOptions.IncludePrefab);
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void ToArchetypeChunkListAsync_TempMemory_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            Assert.Throws<ArgumentException>(() =>
            {
                query.ToArchetypeChunkListAsync(Allocator.Temp, out JobHandle jobhandle);
            });
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ToArchetypeChunkListAsync_ReadLengthBeforeComplete_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            int entityCount = 1000;
            m_Manager.CreateEntity(archetype, entityCount);
            int expectedChunkCount = query.CalculateChunkCount();
            using var chunkList = query.ToArchetypeChunkListAsync(World.UpdateAllocator.ToAllocator, out JobHandle jobhandle);
            Assert.Throws<InvalidOperationException>(() =>
            {
                int len = chunkList.Length;
            });
            jobhandle.Complete();
            Assert.AreEqual(expectedChunkCount, chunkList.Length);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void ToEntityListAsync_TempMemory_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            Assert.Throws<ArgumentException>(() =>
            {
                query.ToEntityListAsync(Allocator.Temp, out JobHandle jobhandle);
            });
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ToEntityListAsync_ReadLengthBeforeComplete_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            int entityCount = 17;
            m_Manager.CreateEntity(archetype, entityCount);
            using var entityList = query.ToEntityListAsync(World.UpdateAllocator.ToAllocator, out JobHandle jobhandle);
            Assert.Throws<InvalidOperationException>(() =>
            {
                int len = entityList.Length;
            });
            jobhandle.Complete();
            Assert.AreEqual(entityCount, entityList.Length);
        }

        [Test]
        public void ToEntityArray_TempMemory_DoesNotThrow()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            Assert.DoesNotThrow(() =>
            {
                query.ToEntityArray(Allocator.Temp).Dispose();
            });

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            m_Manager.CreateEntity(archetype, 100000);
            Assert.DoesNotThrow(() =>
            {
                query.ToEntityArray(Allocator.Temp).Dispose();
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void ToComponentDataListAsync_TempMemory_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            Assert.Throws<ArgumentException>(() =>
            {
                query.ToComponentDataListAsync<EcsTestData>(Allocator.Temp, out JobHandle jobhandle);
            });
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ToComponentDataListAsync_ReadLengthBeforeComplete_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            int entityCount = 17;
            m_Manager.CreateEntity(archetype, entityCount);
            using var valueList = query.ToComponentDataListAsync<EcsTestData>(World.UpdateAllocator.ToAllocator, out JobHandle jobhandle);
            Assert.Throws<InvalidOperationException>(() =>
            {
                int len = valueList.Length;
            });
            jobhandle.Complete();
            Assert.AreEqual(entityCount, valueList.Length);
        }

        [Test]
        public void ToComponentDataArrayTempMemoryDoesNotThrow()
        {
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));


            Assert.DoesNotThrow(() =>
            {
                query.ToComponentDataArray<EcsTestData>(Allocator.Temp).Dispose();
            });

            query.Dispose();

            //create very large Query to test AsyncComplete path
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            m_Manager.CreateEntity(archetype, 100000);

            query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            Assert.DoesNotThrow(() =>
            {
                query.ToComponentDataArray<EcsTestData>(Allocator.Temp).Dispose();
            });

            query.Dispose();

        }

        [Test]
        [Obsolete("Remove this test along with CopyFromComponentDataArrayAsync")]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void CopyFromComponentDataArrayAsync_TempMemory_Throws()
        {
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            var components = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);

            Assert.DoesNotThrow(() =>
            {
                query.CopyFromComponentDataArray(components);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                query.CopyFromComponentDataArrayAsync(components, out JobHandle jobhandle);
            });

            query.Dispose();
            components.Dispose();

            //create very large Query to test AsyncComplete path
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            m_Manager.CreateEntity(archetype, 100000);

            query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            components = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);


            Assert.DoesNotThrow(() =>
            {
                query.CopyFromComponentDataArray(components);
            });

            query.Dispose();
            components.Dispose();

        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void CopyFromComponentDataListAsync_TempMemory_Throws()
        {
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            var components = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);

            Assert.DoesNotThrow(() =>
            {
                query.CopyFromComponentDataArray(components);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                using var componentDataList = new NativeList<EcsTestData>(100, Allocator.Temp);
                query.CopyFromComponentDataListAsync(componentDataList, out JobHandle jobhandle);
            });

            query.Dispose();
            components.Dispose();

            //create very large Query to test AsyncComplete path
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            m_Manager.CreateEntity(archetype, 100000);

            query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            components = query.ToComponentDataArray<EcsTestData>(Allocator.Temp);


            Assert.DoesNotThrow(() =>
            {
                query.CopyFromComponentDataArray(components);
            });

            query.Dispose();
            components.Dispose();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CopyFromComponentDataListAsync_AddToListBeforeComplete_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            int entityCount = 17;
            m_Manager.CreateEntity(archetype, entityCount);
            var valueList = query.ToComponentDataListAsync<EcsTestData>(World.UpdateAllocator.ToAllocator, out JobHandle jobhandle);
            jobhandle.Complete();
            query.CopyFromComponentDataListAsync(valueList, out var scatterHandle);
            Assert.Throws<InvalidOperationException>(() =>
            {
                valueList.Add(new EcsTestData(23));
            });
            scatterHandle.Complete();

            valueList.Add(new EcsTestData(23));
            Assert.AreEqual(entityCount+1, valueList.Length);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void ToComponentDataArray_TypeNotInQuery_Throws()
        {
            var query = EmptySystem.GetEntityQuery(typeof(EcsTestData));

            JobHandle jobHandle;
            Assert.Throws<InvalidOperationException>(() =>
            {
                query.ToComponentDataListAsync<EcsTestData2>(Allocator.Persistent, out jobHandle);
            });
            Assert.Throws<InvalidOperationException>(() =>
            {
                query.ToComponentDataArray<EcsTestData2>(Allocator.Persistent);
            });
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            Assert.Throws<InvalidOperationException>(() =>
            {
                query.ToComponentDataArray<EcsTestManagedComponent>();
            });
#endif
        }

        [Test]
        public unsafe void CopyFromComponentDataArray_Works([Values] EntityQueryJobMode jobMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var values = CollectionHelper.CreateNativeArray<EcsTestData, RewindableAllocator>(archetype.ChunkCapacity * 2, ref World.UpdateAllocator);
            for (int i = 0; i < archetype.ChunkCapacity * 2; ++i)
            {
                values[i] = new EcsTestData{value = i};
            }

            m_Manager.CreateEntity(archetype, archetype.ChunkCapacity * 2, Allocator.Temp);
            var query = EmptySystem.GetEntityQuery(typeof(EcsTestData));
            switch (jobMode)
            {
                case EntityQueryJobMode.AsyncObsolete:
                {
#pragma warning disable 0618
                    query.CopyFromComponentDataArrayAsync(values, out JobHandle jobHandle);
#pragma warning restore 0618
                    jobHandle.Complete();
                    break;
                }
                case EntityQueryJobMode.Async:
                {
                    var valuesList = new NativeList<EcsTestData>(values.Length, World.UpdateAllocator.ToAllocator);
                    valuesList.CopyFrom(values);
                    query.CopyFromComponentDataListAsync(valuesList, out JobHandle jobHandle);
                    jobHandle.Complete();
                    break;
                }
                case EntityQueryJobMode.Immediate:
                default:
                    query.CopyFromComponentDataArray(values);
                    break;
            }

            var dataArray = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            CollectionAssert.AreEquivalent(values, dataArray);

            dataArray.Dispose();
            values.Dispose();
        }

        [Test]
        public void CopyFromComponentDataArray_SetsChangeVersion([Values] EntityQueryJobMode jobMode)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            m_Manager.CreateEntity(archetype, archetype.ChunkCapacity * 2);

            var values = CollectionHelper.CreateNativeArray<EcsTestData, RewindableAllocator>(archetype.ChunkCapacity * 2, ref World.UpdateAllocator);
            for (int i = 0; i < archetype.ChunkCapacity * 2; ++i)
            {
                values[i] = new EcsTestData{value = i};
            }

            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(true);
            var query = EmptySystem.GetEntityQuery(typeof(EcsTestData));
            using var chunksOld = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < chunksOld.Length; ++i)
            {
                Assert.AreEqual(typeHandle.GlobalSystemVersion, chunksOld[i].GetChangeVersion(ref typeHandle));
            }

            uint fakeSystemVersion = 42;
            m_ManagerDebug.SetGlobalSystemVersion(fakeSystemVersion);
            switch (jobMode)
            {
                case EntityQueryJobMode.AsyncObsolete:
                {
#pragma warning disable 0618
                    query.CopyFromComponentDataArrayAsync(values, out JobHandle jobHandle);
#pragma warning restore 0618
                    jobHandle.Complete();
                    break;
                }
                case EntityQueryJobMode.Async:
                {
                    var valuesList = new NativeList<EcsTestData>(values.Length, World.UpdateAllocator.ToAllocator);
                    valuesList.CopyFrom(values);
                    query.CopyFromComponentDataListAsync(valuesList, out JobHandle jobHandle);
                    jobHandle.Complete();
                    break;
                }
                case EntityQueryJobMode.Immediate:
                default:
                    query.CopyFromComponentDataArray(values);
                    break;
            }
            values.Dispose();

            using var chunksNew = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < chunksOld.Length; ++i)
            {
                Assert.AreEqual(fakeSystemVersion, chunksNew[i].GetChangeVersion(ref typeHandle));
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void CopyFromComponentDataListAsync_TypeNotInQuery_Throws()
        {
            var query = EmptySystem.GetEntityQuery(typeof(EcsTestData));

            JobHandle jobHandle;
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (var array = new NativeArray<EcsTestData2>(0, Allocator.Persistent))
                {
                    query.CopyFromComponentDataArray(array);
                }
            });
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (var array = new NativeArray<EcsTestData2>(0, Allocator.Persistent))
                {
#pragma warning disable 0618
                    query.CopyFromComponentDataArrayAsync(array, out jobHandle);
#pragma warning restore 0618
                }
            });
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (var list = new NativeList<EcsTestData2>(0, Allocator.Persistent))
                {
                    query.CopyFromComponentDataListAsync(list, out jobHandle);
                }
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void UseDisposedQueryThrows()
        {
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            query.Dispose();
            Assert.Throws<ArgumentException>(
                () => m_Manager.AddComponent(query, typeof(EcsTestData2)));
        }

        [Test]
        public void ArchetypesCreatedInExclusiveEntityTransaction()
        {
            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var transaction = m_Manager.BeginExclusiveEntityTransaction();
            transaction.CreateEntity(typeof(EcsTestData));
            m_Manager.EndExclusiveEntityTransaction();

            Assert.AreEqual(1, query.CalculateEntityCount());
        }

        [Test]
        public unsafe void QueryDescAndEntityQueryHaveEqualAccessPermissions()
        {
            var queryA = m_Manager.CreateEntityQuery(ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>());
            var queryB = m_Manager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] {ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>()}
            });

            var queryDataA = queryA._GetImpl()->_QueryData;
            var queryDataB = queryB._GetImpl()->_QueryData;
            Assert.AreEqual(queryDataA->RequiredComponentsCount, queryDataB->RequiredComponentsCount);
            Assert.IsTrue(UnsafeUtility.MemCmp(queryDataA->RequiredComponents, queryDataB->RequiredComponents, sizeof(ComponentType) * queryDataA->RequiredComponentsCount) == 0);

            queryA.Dispose();
            queryB.Dispose();
        }

        [Test]
        public unsafe void CachingWorks()
        {
            var q1 = EmptySystem.GetEntityQuery(typeof(EcsTestData));
            var q2 = EmptySystem.GetEntityQuery(typeof(EcsTestData2));
            var q3 = EmptySystem.GetEntityQuery(typeof(EcsTestData));

            Assert.AreEqual((IntPtr)q1.__impl, (IntPtr)q3.__impl);
            Assert.AreNotEqual((IntPtr)q2.__impl, (IntPtr)q3.__impl);
            Assert.AreEqual(EntityDataAccess.kBuiltinEntityQueryCount + 2, m_Manager.GetCheckedEntityDataAccess()->AliveEntityQueries.Count());
        }

        [Test]
        public unsafe void LivePointerTrackingWorks()
        {
            var q1 = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var q2 = m_Manager.CreateEntityQuery(typeof(EcsTestData2));

            Assert.AreEqual(EntityDataAccess.kBuiltinEntityQueryCount + 2, m_Manager.GetCheckedEntityDataAccess()->AliveEntityQueries.Count());

            q1.Dispose();

            Assert.AreEqual(EntityDataAccess.kBuiltinEntityQueryCount + 1, m_Manager.GetCheckedEntityDataAccess()->AliveEntityQueries.Count());

            q2.Dispose();

            Assert.AreEqual(EntityDataAccess.kBuiltinEntityQueryCount, m_Manager.GetCheckedEntityDataAccess()->AliveEntityQueries.Count());
        }

        [Test]
        public unsafe void EntityQueryBuilder_CreateBuilder()
        {
            var builder = new EntityQueryBuilder(Allocator.Temp);
            builder.WithAll<EcsTestData>();
            builder.WithNone<EcsTestData2>();
            builder.WithPresent<EcsTestDataEnableable>();

            var query = builder.Build(EmptySystem);
            var queryData = query._GetImpl()->_QueryData;

            Assert.AreEqual(1, queryData->ArchetypeQueryCount);

            var archetypeQuery = queryData->ArchetypeQueries[0];
            Assert.AreEqual(1, archetypeQuery.AllCount);
            Assert.AreEqual(1, archetypeQuery.NoneCount);
            Assert.AreEqual(0, archetypeQuery.AnyCount);
            Assert.AreEqual(0, archetypeQuery.DisabledCount);
            Assert.AreEqual(0, archetypeQuery.AbsentCount);
            Assert.AreEqual(1, archetypeQuery.PresentCount);

            Assert.AreEqual(ComponentType.ReadOnly<EcsTestData>().TypeIndex, archetypeQuery.All[0]);
            Assert.AreEqual(ComponentType.ReadOnly<EcsTestData2>().TypeIndex, archetypeQuery.None[0]);
            Assert.AreEqual(ComponentType.ReadOnly<EcsTestDataEnableable>().TypeIndex, archetypeQuery.Present[0]);

            builder.Dispose();
        }

        [Test]
        public unsafe void EntityQueryBuilder_CreateBuilder_FluentSyntax()
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>()
                .WithNone<EcsTestData2>()
                .WithPresent<EcsTestDataEnableable>()
                .Build(EmptySystem);

            var queryData = query._GetImpl()->_QueryData;

            Assert.AreEqual(1, queryData->ArchetypeQueryCount);

            var archetypeQuery = queryData->ArchetypeQueries[0];
            Assert.AreEqual(1, archetypeQuery.AllCount);
            Assert.AreEqual(1, archetypeQuery.NoneCount);
            Assert.AreEqual(0, archetypeQuery.AnyCount);
            Assert.AreEqual(0, archetypeQuery.DisabledCount);
            Assert.AreEqual(0, archetypeQuery.AbsentCount);
            Assert.AreEqual(1, archetypeQuery.PresentCount);

            Assert.AreEqual(ComponentType.ReadOnly<EcsTestData>().TypeIndex, archetypeQuery.All[0]);
            Assert.AreEqual(ComponentType.ReadOnly<EcsTestData2>().TypeIndex, archetypeQuery.None[0]);
            Assert.AreEqual(ComponentType.ReadOnly<EcsTestDataEnableable>().TypeIndex, archetypeQuery.Present[0]);
        }

        [Test]
        public unsafe void EntityQueryBuilder_CreateMultipleArchetypeQueries()
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>().WithNone<EcsTestData2>()
                .AddAdditionalQuery()
                .WithAll<EcsTestData2>().WithAny<EcsTestData3, EcsTestData4>()
                .WithPresent<EcsTestDataEnableable>()
                .Build(EmptySystem);

            var queryData = query._GetImpl()->_QueryData;

            Assert.AreEqual(2, queryData->ArchetypeQueryCount);

            var archetypeQuery1 = queryData->ArchetypeQueries[0];
            Assert.AreEqual(1, archetypeQuery1.AllCount);
            Assert.AreEqual(1, archetypeQuery1.NoneCount);
            Assert.AreEqual(0, archetypeQuery1.AnyCount);
            Assert.AreEqual(0, archetypeQuery1.DisabledCount);
            Assert.AreEqual(0, archetypeQuery1.AbsentCount);
            Assert.AreEqual(0, archetypeQuery1.PresentCount);

            Assert.AreEqual(ComponentType.ReadOnly<EcsTestData>().TypeIndex, archetypeQuery1.All[0]);
            Assert.AreEqual(ComponentType.ReadOnly<EcsTestData2>().TypeIndex, archetypeQuery1.None[0]);

            var archetypeQuery2 = queryData->ArchetypeQueries[1];

            Assert.AreEqual(1, archetypeQuery2.AllCount);
            Assert.AreEqual(0, archetypeQuery2.NoneCount);
            Assert.AreEqual(2, archetypeQuery2.AnyCount);
            Assert.AreEqual(0, archetypeQuery2.DisabledCount);
            Assert.AreEqual(0, archetypeQuery2.AbsentCount);
            Assert.AreEqual(1, archetypeQuery2.PresentCount);

            Assert.AreEqual(ComponentType.ReadOnly<EcsTestData2>().TypeIndex, archetypeQuery2.All[0]);
            Assert.That(ToManagedArray(archetypeQuery2.Any, archetypeQuery2.AnyCount), Is.EquivalentTo(new TypeIndex[] {
                    ComponentType.ReadOnly<EcsTestData3>().TypeIndex,
                    ComponentType.ReadOnly<EcsTestData4>().TypeIndex,
            }));
            Assert.AreEqual(ComponentType.ReadOnly<EcsTestDataEnableable>().TypeIndex, archetypeQuery2.Present[0]);
        }

        [Test]
        public unsafe void EntityQueryBuilder_CreateMultipleDistinctQueries()
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>().WithNone<EcsTestData2>();

            var query1 = builder.Build(EmptySystem);

            builder.Reset();

            var query2 = builder.WithAll<EcsTestData2>().WithAny<EcsTestData3, EcsTestData4>().Build(EmptySystem);

            var queryData1 = query1._GetImpl()->_QueryData;

            Assert.AreEqual(1, queryData1->ArchetypeQueryCount);

            var archetypeQuery1 = queryData1->ArchetypeQueries[0];
            Assert.AreEqual(1, archetypeQuery1.AllCount);
            Assert.AreEqual(1, archetypeQuery1.NoneCount);
            Assert.AreEqual(0, archetypeQuery1.AnyCount);
            Assert.AreEqual(0, archetypeQuery1.DisabledCount);
            Assert.AreEqual(0, archetypeQuery1.AbsentCount);
            Assert.AreEqual(0, archetypeQuery1.PresentCount);

            Assert.AreEqual(ComponentType.ReadOnly<EcsTestData>().TypeIndex, archetypeQuery1.All[0]);
            Assert.AreEqual(ComponentType.ReadOnly<EcsTestData2>().TypeIndex, archetypeQuery1.None[0]);


            var queryData2 = query2._GetImpl()->_QueryData;

            Assert.AreEqual(1, queryData2->ArchetypeQueryCount);
            var archetypeQuery2 = queryData2->ArchetypeQueries[0];

            Assert.AreEqual(1, archetypeQuery2.AllCount);
            Assert.AreEqual(0, archetypeQuery2.NoneCount);
            Assert.AreEqual(2, archetypeQuery2.AnyCount);
            Assert.AreEqual(0, archetypeQuery2.DisabledCount);
            Assert.AreEqual(0, archetypeQuery2.AbsentCount);
            Assert.AreEqual(0, archetypeQuery2.PresentCount);

            Assert.AreEqual(ComponentType.ReadOnly<EcsTestData2>().TypeIndex, archetypeQuery2.All[0]);
            Assert.That(ToManagedArray(archetypeQuery2.Any, archetypeQuery2.AnyCount), Is.EquivalentTo(new TypeIndex[] {
                    ComponentType.ReadOnly<EcsTestData3>().TypeIndex,
                    ComponentType.ReadOnly<EcsTestData4>().TypeIndex,
            }));
            builder.Dispose();
        }

        [Test]
        public unsafe void EntityQueryBuilder_UsesReferenceSemantics_NonFluentSyntax()
        {
            var nonfluentBuilder = new EntityQueryBuilder(Allocator.Temp);
            nonfluentBuilder.WithAll<EcsTestData>();
            nonfluentBuilder.AddAdditionalQuery();
            nonfluentBuilder.WithNone<EcsTestData2>();
            nonfluentBuilder.AddAdditionalQuery();
            nonfluentBuilder.WithAny<EcsTestData3>();
            nonfluentBuilder.AddAdditionalQuery();
            nonfluentBuilder.WithOptions(EntityQueryOptions.IncludePrefab);

            var nonfluentQuery = nonfluentBuilder.Build(EmptySystem);
            Assert.AreEqual(4, nonfluentQuery._GetImpl()->_QueryData->ArchetypeQueryCount);
        }

        [Test]
        public unsafe void EntityQueryBuilder_UsesReferenceSemantics_FluentSyntax()
        {
            var fluentQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>()
                .AddAdditionalQuery()
                .WithNone<EcsTestData2>()
                .AddAdditionalQuery()
                .WithAny<EcsTestData3>()
                .AddAdditionalQuery()
                .WithOptions(EntityQueryOptions.IncludePrefab)
                .Build(EmptySystem);

            Assert.AreEqual(4, fluentQuery._GetImpl()->_QueryData->ArchetypeQueryCount);
        }

        [Test]
        public unsafe void EntityQueryBuilder_UsesReferenceSemantics_MixedFluentSyntax()
        {
            var mixedFluentBuilder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData>();
            mixedFluentBuilder.AddAdditionalQuery()
                .WithNone<EcsTestData2>()
                .AddAdditionalQuery()
                .WithAny<EcsTestData3>();
            mixedFluentBuilder.AddAdditionalQuery()
                .WithOptions(EntityQueryOptions.IncludePrefab);

            var mixedFluentQuery = mixedFluentBuilder.Build(EmptySystem);
            Assert.AreEqual(4, mixedFluentQuery._GetImpl()->_QueryData->ArchetypeQueryCount);
        }

        [Test]
        public unsafe void EntityQueryBuilder_UsesReferenceSemantics_InternalListRealloc()
        {
            var originalBuilder = new EntityQueryBuilder(Allocator.Temp);
            var anotherVariable = originalBuilder;
            var originalCapacity = anotherVariable._builderDataPtr->_indexData.Capacity;
            while (anotherVariable._builderDataPtr->_indexData.Capacity == originalCapacity)
            {
                anotherVariable.AddAdditionalQuery();
                anotherVariable.WithAll<EcsTestData>();
            }

            Assert.AreEqual(anotherVariable._builderDataPtr->_indexData.Length, originalBuilder._builderDataPtr->_indexData.Length,
                "EntityQueryBuilder internal list was reallocated and previous reference to it was not updated.");
        }

        [Test]
        public unsafe void EntityQueryBuilder_UsesReferenceSemantics_InternalListRealloc_Fluent()
        {
            var originalBuilder = new EntityQueryBuilder(Allocator.Temp);
            IntPtr originalPtr = (IntPtr)originalBuilder._builderDataPtr->_typeData.Ptr;
            var reallocdBuilder = originalBuilder
                .WithAll<EcsTestData, EcsTestData2, EcsTestData3, EcsTestData4, EcsTestData5, EcsTestData6, EcsTestData7>()
                .WithAll<EcsTestData8, EcsTestData9>()
                .FinalizeQueryInternal();

            // We can't loop here like the non-fluent variation above, so we just need to check
            // that realloc happened, and if this fails, add some additional calls above.
            Assert.AreNotEqual(originalPtr, (IntPtr)reallocdBuilder._builderDataPtr->_typeData.Ptr,
                "Need to add additional calls in test so that reallocdBuilder reallocs its UnsafeLists.");

            Assert.AreEqual(reallocdBuilder._builderDataPtr->_typeData.Capacity,
                            originalBuilder._builderDataPtr->_typeData.Capacity,
                "EntityQueryBuilder internal list was reallocated and previous reference to it was not updated.");
            Assert.AreEqual(reallocdBuilder._builderDataPtr->_typeData.Length,
                            originalBuilder._builderDataPtr->_typeData.Length,
                "EntityQueryBuilder internal list was reallocated and previous reference to it was not updated.");
        }

        [Test]
        public void EntityQueryBuilder_CreateEntityQueryOutsideOfSystem()
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData, EcsTestData2>()
                .WithNone<EcsTestData3>()
                .Build(m_Manager);

            var positiveArchetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var negativeArchetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));
            m_Manager.CreateEntity(positiveArchetype, 3);
            m_Manager.CreateEntity(negativeArchetype, 2);

            Assert.AreEqual(3, query.CalculateEntityCount());
        }

        [Test]
        public unsafe void EntityQuery_CreatedByEntityManager_DestroyedByEntityManager()
        {
            var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData, EcsTestData2>();
            var queryFromEntityManager = m_Manager.CreateEntityQuery(builder);
            var queryFromBuild = builder.Build(m_Manager);
            var queryDisposedEarly = builder.Build(m_Manager);

            Assert.IsTrue(m_Manager.IsQueryValid(queryFromEntityManager));
            Assert.IsTrue(m_Manager.IsQueryValid(queryFromBuild));
            Assert.IsTrue(m_Manager.IsQueryValid(queryDisposedEarly));

            // For now, queries can be disposed early manually
            queryDisposedEarly.Dispose();

            Assert.IsFalse(m_Manager.IsQueryValid(queryDisposedEarly));

            World.Dispose();


            // There's no good way to test that these have been disposed. These queries are a
            // value-copy of the ones stored in EntityManager.m_EntityQueries. Their __impl
            // pointers have already been disposed and freed, so these copies will point to freed
            // memory (they should normally not live longer than the World). The AliveEntityQueries
            // map in EntityDataAccess, which is used to check IsQueryValid, is disposed with the
            // EntityManager.

            // WARNING: these __impl pointers have already been freed, so this may cause a read
            // access violation or other crash.
            try
            {
                Assert.AreEqual((IntPtr)0, (IntPtr)queryFromEntityManager.__impl->_QueryData);
                Assert.AreEqual((IntPtr)0, (IntPtr)queryFromBuild.__impl->_QueryData);
            }
            catch (AccessViolationException e)
            {
                Debug.Log($"Reading freed pointer during test caused {e}");
            }
            catch (NullReferenceException e)
            {
                Debug.Log($"Reading freed pointer during test caused {e}");
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public partial struct BurstCompiledUnmanagedSystemEntityQueryBuilder : ISystem
        {
            private EntityQuery _Query;

            ComponentTypeHandle<EcsTestFloatData3> _RotationTypeHandle;
            ComponentTypeHandle<EcsTestFloatData> _RotationSpeedTypeHandle;

            [BurstCompile(CompileSynchronously = true)]
            unsafe struct MyJob : IJobChunk
            {
                public ComponentTypeHandle<EcsTestFloatData3> RotationTypeHandle;
                [ReadOnly] public ComponentTypeHandle<EcsTestFloatData> RotationSpeedTypeHandle;

                public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                {
                }
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnCreate(ref SystemState state)
            {
                _Query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<EcsTestFloatData3>()
                    .WithAll<EcsTestFloatData>()
                    .Build(ref state);
                _RotationTypeHandle = state.GetComponentTypeHandle<EcsTestFloatData3>();
                _RotationSpeedTypeHandle = state.GetComponentTypeHandle<EcsTestFloatData>();
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                _RotationTypeHandle.Update(ref state);
                _RotationSpeedTypeHandle.Update(ref state);

                var job = new MyJob
                {
                    RotationTypeHandle = _RotationTypeHandle,
                    RotationSpeedTypeHandle = _RotationSpeedTypeHandle,
                };
                Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobs(ref job, _Query);
            }
        }

        partial class TestGroup : ComponentSystemGroup
        {
        }

        [Test]
        public void BurstCompiledUnmanagedSystemEntityQueryBuilderWorks()
        {
            var group = World.CreateSystemManaged<TestGroup>();
            var sys = World.CreateSystem<BurstCompiledUnmanagedSystemEntityQueryBuilder>();
            group.AddSystemToUpdateList(sys);
            Assert.DoesNotThrow(() => group.Update());
            group.CompleteDependencyInternal();
        }

        [Test]
        public void GetEntityQueryDesc()
        {
            var queryDesc = new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>() },
                Any = new[] { ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadWrite<EcsTestData4>() },
                None = new[] { ComponentType.ReadOnly<EcsTestFloatData>(), ComponentType.ReadWrite<EcsTestFloatData2>() },
                Disabled = new[] { ComponentType.ReadOnly<EcsTestTagEnableable>(), ComponentType.ReadWrite<EcsTestDataEnableable>() },
                Absent = new[] { ComponentType.ReadOnly<EcsIntElement>(), ComponentType.ReadWrite<EcsIntElement2>() },
                Present = new[] { ComponentType.ReadOnly<EcsTestDataEnableable2>(), ComponentType.ReadWrite<EcsTestFloatData3>() },
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
            };

            using var query = m_Manager.CreateEntityQuery(queryDesc);
            var entityQueryDesc = query.GetEntityQueryDesc();
            foreach (var all in entityQueryDesc.All)
                Assert.IsTrue(queryDesc.All.Contains(all));
            foreach (var any in entityQueryDesc.Any)
                Assert.IsTrue(queryDesc.Any.Contains(any));
            foreach (var disabled in entityQueryDesc.Disabled)
                Assert.IsTrue(queryDesc.Disabled.Contains(disabled));

            // All `None` components are silently given `ReadOnly` access mode during query creation
            var actualNone = entityQueryDesc.None.Select(n => n.TypeIndex).ToArray();
            var expectedNone = queryDesc.None.Select(n => n.TypeIndex).ToArray();

            foreach (var none in expectedNone)
                Assert.IsTrue(actualNone.Contains(none));

            var actualAbsent = entityQueryDesc.Absent.Select(n => n.TypeIndex).ToArray();
            var expectedAbsent = queryDesc.Absent.Select(n => n.TypeIndex).ToArray();

            // All `Absent` components are silently given `ReadOnly` access mode during query creation
            foreach (var absent in expectedAbsent)
                Assert.IsTrue(actualAbsent.Contains(absent));
        }

        [Test]
        public void CreateEntityQuery_DifferentEntityQueryOptions_DontUseCachedQueryData()
        {
            using var query1 = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestData>()
                .Build(m_Manager);
            using var query2 = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestData>()
                .WithOptions(EntityQueryOptions.IncludePrefab)
                .Build(m_Manager);
            var ent1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var ent2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(Prefab));
            Assert.AreEqual(1, query1.CalculateEntityCount());
            Assert.AreEqual(2, query2.CalculateEntityCount());
        }

        [Test]
        public void CreateEntityQuery_DifferentEntityQueryOptions_IncludeSystem()
        {
            using var query1 = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestData>()
                .Build(m_Manager);
            using var query2 = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<EcsTestData>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(m_Manager);
            var ent1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var ent2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(SystemInstance));
            Assert.AreEqual(1, query1.CalculateEntityCount());
            Assert.AreEqual(2, query2.CalculateEntityCount());
        }

        [Test]
        public void CreateEntityQuery_TooManyEnableableComponents_Throws()
        {
            // this test must change if the limit is increased
            Assert.AreEqual(8, EntityQueryManager.MAX_ENABLEABLE_COMPONENTS_PER_QUERY);
            NUnit.Framework.Assert.DoesNotThrow(() => m_Manager.CreateEntityQuery(
                typeof(EcsIntElementEnableable),
                typeof(EcsIntElementEnableable2),
                typeof(EcsIntElementEnableable3),
                typeof(EcsIntElementEnableable4),
                typeof(EcsTestDataEnableable),
                typeof(EcsTestDataEnableable2),
                typeof(EcsTestDataEnableable3),
                typeof(EcsTestDataEnableable4)));
            NUnit.Framework.Assert.Throws<ArgumentException>(() => m_Manager.CreateEntityQuery(
                typeof(EcsIntElementEnableable),
                typeof(EcsIntElementEnableable2),
                typeof(EcsIntElementEnableable3),
                typeof(EcsIntElementEnableable4),
                typeof(EcsTestDataEnableable),
                typeof(EcsTestDataEnableable2),
                typeof(EcsTestDataEnableable3),
                typeof(EcsTestDataEnableable4),
                typeof(EcsTestDataEnableable5)));
        }

        [Test]
        public void ComponentType_Combine_Works()
        {
            var readWriteReplacesReadOnly = ComponentType.Combine(new[] { ComponentType.ReadWrite<EcsTestData>() }, new[] { ComponentType.ReadOnly<EcsTestData>() });
            CollectionAsserts.CompareSorted(new[] {ComponentType.ReadWrite<EcsTestData>() }, readWriteReplacesReadOnly);

            var readWriteReplacesReadOnly2 = ComponentType.Combine(new[] { ComponentType.ReadOnly<EcsTestData>() }, new[] { ComponentType.ReadWrite<EcsTestData>() });
            CollectionAsserts.CompareSorted(new[] {ComponentType.ReadWrite<EcsTestData>() }, readWriteReplacesReadOnly2);

            var fourComponentsCombined = ComponentType.Combine(new[] { ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadWrite<EcsTestData>() }, new[] {ComponentType.ReadWrite<EcsTestData>(),  ComponentType.ReadOnly<EcsTestData3>() , ComponentType.ReadWrite<EcsTestData>()});
            CollectionAsserts.CompareSorted(new[] {ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadOnly<EcsTestData3>() }, fourComponentsCombined);

            var twoComponentsCombined = ComponentType.Combine(new[] { ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadWrite<EcsTestData>() }, new[] { ComponentType.ReadOnly<EcsTestData3>() });
            CollectionAsserts.CompareSorted(new[] {ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadOnly<EcsTestData3>() }, twoComponentsCombined);

            var singleAndEmptyCombine = ComponentType.Combine(new[] { ComponentType.ReadOnly<EcsTestData>() }, Array.Empty<ComponentType>());
            CollectionAsserts.CompareSorted(new[] {ComponentType.ReadOnly<EcsTestData>() }, singleAndEmptyCombine);

            var emptyCombine = ComponentType.Combine(Array.Empty<ComponentType>(), Array.Empty<ComponentType>());
            CollectionAsserts.CompareSorted(Array.Empty<ComponentType>(), emptyCombine);

            var excludeCombine = ComponentType.Combine(new[] { ComponentType.ReadOnly<EcsTestData>(), ComponentType.Exclude<EcsTestData2>() });
            CollectionAsserts.CompareSorted(new[] { ComponentType.ReadOnly<EcsTestData>(), ComponentType.Exclude<EcsTestData2>() }, excludeCombine);

            var excludeDouble = ComponentType.Combine(new[] { ComponentType.Exclude<EcsTestData>(), ComponentType.Exclude<EcsTestData>()  });
            CollectionAsserts.CompareSorted(new[] { ComponentType.Exclude<EcsTestData>() }, excludeDouble);

#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            Assert.Throws<ArgumentException>( () => ComponentType.Combine(new[] { ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadOnly<EcsTestData3>(), ComponentType.Exclude<EcsTestData>() }));
            Assert.Throws<ArgumentException>( () => ComponentType.Combine(new[] { ComponentType.Exclude<EcsTestData>(), ComponentType.ReadWrite<EcsTestData>() }));
#endif
        }

        [BurstCompile]
        partial struct AsyncGatherScatterSystem : ISystem
        {
            // Disables all but every 3rd entity
            [BurstCompile]
            partial struct DisableJob : IJobEntity
            {
                [NativeDisableParallelForRestriction] public ComponentLookup<EcsTestDataEnableable> Lookup;
                void Execute(Entity e)
                {
                    int val = Lookup[e].value;
                    if ((val % 3) != 0)
                    {
                        Lookup.SetComponentEnabled(e, false);
                    }
                }
            }

            // Negates every value in the provided array
            [BurstCompile]
            struct ProcessJob : IJob
            {
                public NativeArray<EcsTestDataEnableable> ValuesArray;
                public void Execute()
                {
                    int valueCount = ValuesArray.Length;
                    for (int i = 0; i < valueCount; ++i)
                    {
                        int x = ValuesArray[i].value;
                        ValuesArray[i] = new EcsTestDataEnableable(-x);
                    }
                }
            }

            private EntityQuery _query;
            private ComponentLookup<EcsTestDataEnableable> _lookup;
            private int _expectedValueCount;

            public void OnCreate(ref SystemState state)
            {
                _query = state.GetEntityQuery(typeof(EcsTestDataEnableable));
                _lookup = state.GetComponentLookup<EcsTestDataEnableable>(false);
            }

            public void OnUpdate(ref SystemState state)
            {
                _lookup.Update(ref state);
                // Schedule job that disables entities
                var disableJob = new DisableJob { Lookup = _lookup };
                var disableJobHandle = disableJob.ScheduleByRef(_query, state.Dependency);
                // Extract component values. Must explicitly depend on disableJobHandle, since it was scheduled within the same system.
                var valueList = _query.ToComponentDataListAsync<EcsTestDataEnableable>(
                    state.WorldUpdateAllocator, disableJobHandle, out var gatherJobHandle);
                // Process gathered values
                var processJob = new ProcessJob { ValuesArray = valueList.AsDeferredJobArray() };
                var processJobHandle = processJob.Schedule(gatherJobHandle);
                // Scatter processed values back to entities
                _query.CopyFromComponentDataListAsync(valueList, processJobHandle, out var scatterJobHandle);

                scatterJobHandle.Complete();
                Assert.AreEqual(_query.CalculateEntityCount(), valueList.Length);
                state.Dependency = default;
            }
        }

        [Test]
        public void AsyncGatherScatter_Integration()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            int entityCount = 10000;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Persistent);
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));

            for (int i = 0; i < entityCount; ++i)
            {
                m_Manager.SetComponentData(entities[i], new EcsTestDataEnableable(i));
            }

            var sysHandle = World.CreateSystem<AsyncGatherScatterSystem>();
            sysHandle.Update(World.Unmanaged);

            for (int i = 0; i < entityCount; ++i)
            {
                bool expectedEnabled = ((i % 3) == 0);
                bool actualEnabled = m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entities[i]);
                FastAssert.AreEqual(expectedEnabled, actualEnabled, $"Entity {i} mismatch in enabled state");

                int expectedValue = expectedEnabled ? -i : i;
                int actualValue = m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value;
                FastAssert.AreEqual(expectedValue, actualValue, $"Entity {i} value mismatch");
            }
        }

        [Test]
        [Obsolete("Remove this test along with ToEntityArrayAsync")]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void ToEntityArrayAsync_WithEnableableComponents_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            Assert.Throws<InvalidOperationException>(() => query.ToEntityArrayAsync(World.UpdateAllocator.ToAllocator, out var jobhandle));
        }

        [Test]
        [Obsolete("Remove this test along with ToEntityArrayAsync")]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void ToComponentDataArrayAsync_WithEnableableComponents_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            Assert.Throws<InvalidOperationException>(() => query.ToComponentDataArrayAsync<EcsTestDataEnableable>(World.UpdateAllocator.ToAllocator, out var jobhandle));
        }

        [Test]
        [Obsolete("Remove this test along with ToEntityArrayAsync")]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void CopyFromComponentDataArrayAsync_WithEnableableComponents_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            using var values =
                CollectionHelper.CreateNativeArray<EcsTestDataEnableable>(1, World.UpdateAllocator.ToAllocator);
            Assert.Throws<InvalidOperationException>(() => query.CopyFromComponentDataArrayAsync(values, out var jobhandle));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ToEntityListAsync_ConcurrentJobWritesToComponent_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            var entityList = query.ToEntityListAsync(World.UpdateAllocator.ToAllocator, out var gatherJobHandle);
            // A concurrent job that reads the component we're gathering (EcsTestData) should be fine
            var readFromEntityJob = new ReadComponentJob<Entity>
                { TypeHandle = m_Manager.GetComponentTypeHandle<Entity>(true) };
            Assert.DoesNotThrow(() => { readFromEntityJob.Run();});
            // A concurrent job that writes to the component we're gathering (Entity) should throw
            var writeToEntityJob = new WriteComponentJob<Entity>
                { TypeHandle = m_Manager.GetComponentTypeHandle<Entity>(false) };
            Assert.Throws<InvalidOperationException>(() => { var writeJobHandle = writeToEntityJob.Schedule();});
            // With the proper dependency, this is fine
            Assert.DoesNotThrow(() => { writeToEntityJob.Schedule(gatherJobHandle).Complete();
            });
        }

        [Test]
        public void ToEntityListAsync_ExistingConcurrentJobWritesToComponent_DoesNotThrow()
        {
            var writeJob = new WriteComponentJob<Entity>
                { TypeHandle = m_Manager.GetComponentTypeHandle<Entity>(false) };
            var writeJobHandle = writeJob.Schedule();

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            query.AddDependency(writeJobHandle);
            // The job scheduled here should add the existing job as a dependency. Unfortunately we can't test that, but
            // at the very least it shouldn't throw.
            using var entityList = query.ToEntityListAsync(World.UpdateAllocator.ToAllocator, out var gatherJobHandle);
            gatherJobHandle.Complete();
        }

        [Test]
        public void ToComponentDataListAsync_ExistingConcurrentJobWritesToComponent_DoesNotThrow()
        {
            var writeJob = new WriteComponentJob<EcsTestDataEnableable>
                { TypeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false) };
            var writeJobHandle = writeJob.Schedule();

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            query.AddDependency(writeJobHandle);
            // The job scheduled here should add the existing job as a dependency. Unfortunately we can't test that, but
            // at the very least it shouldn't throw.
            using var valueList = query.ToComponentDataListAsync<EcsTestDataEnableable>(World.UpdateAllocator.ToAllocator, out var gatherJobHandle);
            gatherJobHandle.Complete();
        }

        [Test]
        public void CopyFromComponentDataListAsync_ExistingConcurrentJobWritesToComponent_DoesNotThrow()
        {
            var readJob = new ReadComponentJob<EcsTestDataEnableable>
                { TypeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(true) };
            var readJobHandle = readJob.Schedule();

            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            query.AddDependency(readJobHandle);
            // The job scheduled here should add the existing job as a dependency. Unfortunately we can't test that, but
            // at the very least it shouldn't throw.
            using var valueList =
                new NativeList<EcsTestDataEnableable>(query.CalculateEntityCount(), World.UpdateAllocator.ToAllocator);
            valueList.ResizeUninitialized(valueList.Capacity);
            query.CopyFromComponentDataListAsync(valueList, out var scatterJobHandle);
            scatterJobHandle.Complete();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void ToComponentDataListAsync_ConcurrentJobWritesToComponent_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable), typeof(EcsTestData));
            var valueList = query.ToComponentDataListAsync<EcsTestData>(World.UpdateAllocator.ToAllocator, out var gatherJobHandle);
            // A concurrent job that reads the component we're gathering (EcsTestData) should be fine
            var readFromComponentJob = new ReadComponentJob<EcsTestData>
                { TypeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(true) };
            Assert.DoesNotThrow(() => { readFromComponentJob.Run();});
            // A concurrent job that writes to the component we're gathering (EcsTestData) should throw
            var writeToComponentJob = new WriteComponentJob<EcsTestData>
                { TypeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(false) };
            Assert.Throws<InvalidOperationException>(() => { var writeJobHandle = writeToComponentJob.Schedule();});
            // With the proper dependency, this is fine
            Assert.DoesNotThrow(() => { writeToComponentJob.Schedule(gatherJobHandle).Complete(); });
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CopyFromComponentDataListAsync_ConcurrentJobReadsOrWritesComponent_Throws()
        {
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable), typeof(EcsTestData));
            var valueList = new NativeList<EcsTestData>(query.CalculateEntityCount(), World.UpdateAllocator.ToAllocator);
            valueList.ResizeUninitialized(valueList.Capacity);
            query.CopyFromComponentDataListAsync(valueList, out var gatherJobHandle);
            // A concurrent job that reads the component we're gathering (EcsTestData) should throw
            var readFromComponentJob = new ReadComponentJob<EcsTestData>
                { TypeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(true) };
            Assert.Throws<InvalidOperationException>(() => { var readJobHandle = readFromComponentJob.Schedule();});
            // With the proper dependency, this is fine
            Assert.DoesNotThrow(() => { readFromComponentJob.Schedule(gatherJobHandle).Complete(); });

            query.CopyFromComponentDataListAsync(valueList, out gatherJobHandle);
            // A concurrent job that writes the component we're gathering (EcsTestData) should throw
            var writeToComponentJob = new WriteComponentJob<EcsTestData>
                { TypeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(false) };
            Assert.Throws<InvalidOperationException>(() => { var writeJobHandle = writeToComponentJob.Schedule();});
            // With the proper dependency, this is fine
            Assert.DoesNotThrow(() => { writeToComponentJob.Schedule(gatherJobHandle).Complete(); });
        }

        partial class CalculateEntityCount_WithAny_System : SystemBase
        {
            private EntityQuery _query;
            private EntityQuery _queryFromForEach;
            protected override void OnCreate()
            {
                _query = GetEntityQuery(ComponentType.ReadOnly<TestTag0>(), ComponentType.ReadOnly<TestTag1>());
            }

            protected override void OnUpdate()
            {
                int expectedCount = _query.CalculateEntityCount();
                int actualCount = 0;
                // Should match 20 entities
                Entities
                    .WithAll<TestTag0>()
                    .WithAny<TestTag2,TestTag3>()
                    .ForEach((Entity entity, in TestTag1 bComponent) =>
                    {
                        actualCount++;
                    }).Run();
                // Should match the remaining 10 entities
                Entities
                    .WithAll<TestTag0>()
                    .ForEach((Entity entity, in TestTag4 eComponent) =>
                    {
                        actualCount++;
                    }).Run();
                Assert.AreEqual(30, expectedCount, "Query on common components should match all 30 entities");
                Assert.AreEqual(30, actualCount, "Between the two jobs, all 30 entities should be found once each");
                //Assert.AreEqual(expectedCount, actualCount);
            }
        }

        [Test]
        public void CalculateEntityCount_WithAny_Works()
        {
            var archetype012 = m_Manager.CreateArchetype(typeof(TestTag0), typeof(TestTag1), typeof(TestTag2));
            var archetype013 = m_Manager.CreateArchetype(typeof(TestTag0), typeof(TestTag1), typeof(TestTag3));
            var archetype014 = m_Manager.CreateArchetype(typeof(TestTag0), typeof(TestTag1), typeof(TestTag4));
            m_Manager.CreateEntity(archetype012, 10);
            m_Manager.CreateEntity(archetype013, 10);
            m_Manager.CreateEntity(archetype014, 10);
            var sys = World.CreateSystemManaged<CalculateEntityCount_WithAny_System>();
            sys.Update();
        }

        struct JobWithQuery : IJob
        {
            [NativeDisableUnsafePtrRestriction] // Suppresses the usual "no pointers in jobs" safety error
            public EntityQuery Query;

            public NativeReference<int> QueryEntityCount;
            public byte ExpectToThrow;

            void CountEntities()
            {
                QueryEntityCount.Value = Query.CalculateEntityCount();
            }

            public void Execute()
            {
                if (ExpectToThrow != 0)
                    Assert.That(CountEntities, Throws.InvalidOperationException
                        .With.Message.Contains("This EntityQuery operation is not safe to use in job code outside of an ExclusiveEntityTransaction"));
                else
                    CountEntities();
            }
        }

        // TODO(DOTS-8574): Re-enable this test when the corresponding debug check is enabled by default.
#if UNITY_DOTS_DEBUG_ENTITYQUERY_THREAD_CHECKS
        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("This only fails when debug checks are active")]
        public void EntityQueryInJob_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            int entityCount = 1000;
            m_Manager.CreateEntity(archetype, entityCount);
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            using var outputCount = new NativeReference<int>(0, Allocator.TempJob);
            new JobWithQuery
            {
                Query = query,
                QueryEntityCount = outputCount,
                ExpectToThrow = 1,
            }.Schedule().Complete();
            new JobWithQuery
            {
                Query = query,
                QueryEntityCount = outputCount,
                ExpectToThrow = 1,
            }.Run(); // still considered a failure for now, even though Run() is main-thread & thus thread-safe.
        }
#endif

        [Test]
        public void EntityQueryInJob_WithExclusiveEntityTransaction_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            int entityCount = 1000;
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            var outputCount = new NativeReference<int>(0, Allocator.TempJob);
            var eet = m_Manager.BeginExclusiveEntityTransaction();
            using var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            eet.CreateEntity(archetype, entities);

            new JobWithQuery
            {
                Query = query,
                QueryEntityCount = outputCount,
                ExpectToThrow = 0,
            }.Schedule().Complete();
            Assert.AreEqual(entityCount, outputCount.Value);

            outputCount.Value = 0;
            new JobWithQuery
            {
                Query = query,
                QueryEntityCount = outputCount,
                ExpectToThrow = 0,
            }.Run();
            Assert.AreEqual(entityCount, outputCount.Value);

            m_Manager.EndExclusiveEntityTransaction();
            outputCount.Dispose();
        }

        [Test]
        public void CreateEntityQuery_MultiQueryDesc_MoreRequiredComponentsInAdditionalQuery_Works()
        {
            // Regression test for DOTS-9397
            Assert.DoesNotThrow(() =>
            {
                // Test the special case where the first query desc has zero required components,
                // and the second has at least one.
                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAbsent<EcsTestData>()
                    .AddAdditionalQuery()
                    .WithAll<EcsTestData3>()
                    .Build(m_Manager);
            });

            Assert.DoesNotThrow(() =>
            {
                // Test the general case where the second query desc has more required components than the first.
                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<EcsTestData>()
                    .AddAdditionalQuery()
                    .WithAll<EcsTestData2,EcsTestData3>()
                    .Build(m_Manager);
            });
        }
    }
}
