using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Jobs;
using UnityEngine;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Entities.Tests
{
    interface IEcsFooInterface
    {
        int value { get; set; }
    }
    public struct EcsFooTest : IComponentData, IEcsFooInterface
    {
        public int value { get; set; }

        public EcsFooTest(int inValue) { value = inValue; }
    }

    interface IEcsNotUsedInterface
    {
        int value { get; set; }
    }

    [BurstCompile]
    partial class EntityManagerTests : ECSTestsFixture
    {
#if !DOTS_DISABLE_DEBUG_NAMES

        [Test]
        public void InstantiateKeepsName()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.SetName(entity, "Blah");

            var instance = m_Manager.Instantiate(entity);
            Assert.AreEqual("Blah", m_Manager.GetName(instance));
        }

#endif

#if ENTITY_STORE_V1
        [Test]
        public void IncreaseEntityCapacity()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var count = 1024;
            var array = new NativeArray<Entity>(count, Allocator.Temp);
            m_Manager.CreateEntity(archetype, array);
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(i, array[i].Index);
            }

            array.Dispose();
        }
#endif

        [Test]
        public void AddComponent_Entity_EmptyComponentTypeSet_Works()
        {
            var emptyTypeSet = new ComponentTypeSet(Array.Empty<ComponentType>());
            var e = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var beforeArchetypeChunk = m_Manager.GetChunk(e);
            Assert.DoesNotThrow(() => { m_Manager.AddComponent(e, emptyTypeSet); });
            var afterArchetypeChunk = m_Manager.GetChunk(e);
            Assert.AreEqual(beforeArchetypeChunk, afterArchetypeChunk, "Entity chunk/archetype changed after adding empty component set");
        }

        [Test]
        public void AddComponent_EntityArray_EmptyComponentTypeSet_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var emptyTypeSet = new ComponentTypeSet(Array.Empty<ComponentType>());
            using var entities = m_Manager.CreateEntity(archetype, 100, Allocator.Temp);
            var beforeChunks = new NativeArray<ArchetypeChunk>(entities.Length, Allocator.Temp);
            for (int i = 0; i < entities.Length; ++i)
                beforeChunks[i] = m_Manager.GetChunk(entities[i]);
            Assert.DoesNotThrow(() => { m_Manager.AddComponent(entities, emptyTypeSet); });
            var afterChunks = new NativeArray<ArchetypeChunk>(entities.Length, Allocator.Temp);
            for (int i = 0; i < entities.Length; ++i)
                afterChunks[i] = m_Manager.GetChunk(entities[i]);
            CollectionAssert.AreEqual(beforeChunks.ToArray(), afterChunks.ToArray(),
                "Entity chunk/archetype changed after adding empty component set");
        }

        [Test]
        public void AddComponent_EntityQuery_EmptyComponentTypeSet_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var emptyTypeSet = new ComponentTypeSet(Array.Empty<ComponentType>());
            using var entities = m_Manager.CreateEntity(archetype, 100, Allocator.Temp);
            var beforeChunks = new NativeArray<ArchetypeChunk>(entities.Length, Allocator.Temp);
            for (int i = 0; i < entities.Length; ++i)
                beforeChunks[i] = m_Manager.GetChunk(entities[i]);
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            Assert.DoesNotThrow(() => { m_Manager.AddComponent(query, emptyTypeSet); });
            var afterChunks = new NativeArray<ArchetypeChunk>(entities.Length, Allocator.Temp);
            for (int i = 0; i < entities.Length; ++i)
                afterChunks[i] = m_Manager.GetChunk(entities[i]);
            CollectionAssert.AreEqual(beforeChunks.ToArray(), afterChunks.ToArray(),
                "Entity chunk/archetype changed after adding empty component set");
        }

        [Test]
        public void RemoveComponent_Entity_EmptyComponentTypeSet_Works()
        {
            var emptyTypeSet = new ComponentTypeSet(Array.Empty<ComponentType>());
            var e = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var beforeArchetypeChunk = m_Manager.GetChunk(e);
            Assert.DoesNotThrow(() => { m_Manager.RemoveComponent(e, emptyTypeSet); });
            var afterArchetypeChunk = m_Manager.GetChunk(e);
            Assert.AreEqual(beforeArchetypeChunk, afterArchetypeChunk, "Entity chunk/archetype changed after adding empty component set");
        }

        [Test]
        public void RemoveComponent_EntityArray_EmptyComponentTypeSet_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var emptyTypeSet = new ComponentTypeSet(Array.Empty<ComponentType>());
            using var entities = m_Manager.CreateEntity(archetype, 100, Allocator.Temp);
            var beforeChunks = new NativeArray<ArchetypeChunk>(entities.Length, Allocator.Temp);
            for (int i = 0; i < entities.Length; ++i)
                beforeChunks[i] = m_Manager.GetChunk(entities[i]);
            Assert.DoesNotThrow(() => { m_Manager.RemoveComponent(entities, emptyTypeSet); });
            var afterChunks = new NativeArray<ArchetypeChunk>(entities.Length, Allocator.Temp);
            for (int i = 0; i < entities.Length; ++i)
                afterChunks[i] = m_Manager.GetChunk(entities[i]);
            CollectionAssert.AreEqual(beforeChunks.ToArray(), afterChunks.ToArray(),
                "Entity chunk/archetype changed after adding empty component set");
        }

        [Test]
        public void RemoveComponent_EntityQuery_EmptyComponentTypeSet_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var emptyTypeSet = new ComponentTypeSet(Array.Empty<ComponentType>());
            using var entities = m_Manager.CreateEntity(archetype, 100, Allocator.Temp);
            var beforeChunks = new NativeArray<ArchetypeChunk>(entities.Length, Allocator.Temp);
            for (int i = 0; i < entities.Length; ++i)
                beforeChunks[i] = m_Manager.GetChunk(entities[i]);
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>().Build(m_Manager);
            Assert.DoesNotThrow(() => { m_Manager.RemoveComponent(query, emptyTypeSet); });
            var afterChunks = new NativeArray<ArchetypeChunk>(entities.Length, Allocator.Temp);
            for (int i = 0; i < entities.Length; ++i)
                afterChunks[i] = m_Manager.GetChunk(entities[i]);
            CollectionAssert.AreEqual(beforeChunks.ToArray(), afterChunks.ToArray(),
                "Entity chunk/archetype changed after adding empty component set");
        }

        [Test]
        public void AddComponentEmptyNativeArray()
        {
            var array = new NativeArray<Entity>(0, Allocator.Temp);
            m_Manager.AddComponent(array, typeof(EcsTestData));
            array.Dispose();
        }

        public unsafe bool IndexInChunkIsValid(Entity entity)
        {
            var entityInChunk = m_Manager.GetCheckedEntityDataAccess()->EntityComponentStore->GetEntityInChunk(entity);
            return entityInChunk.IndexInChunk < entityInChunk.Chunk.Count;
        }

        [Test]
        public void AddComponentNativeArrayCorrectChunkIndexAfterPacking()
        {
            // This test checks for the bug revealed here https://github.com/Unity-Technologies/dots/issues/2133
            // When packing was done, it was possible for the packed entities to have an incorrect
            // EntityInChunk.IndexInChunk.  A catastrophic case was when IndexInChunk was larger than Chunk.Count.
            var types = new[] { ComponentType.ReadWrite<EcsTestData>()};
            var archetype = m_Manager.CreateArchetype(types);
            var entities = CollectionHelper.CreateNativeArray<Entity>(2, World.UpdateAllocator.ToAllocator);

            // Create four entities so that we create two holes in the chunk when we add a new
            // component to two of them.
            entities[0] = m_Manager.CreateEntity(archetype);
            var checkEntity1 = m_Manager.CreateEntity(archetype);
            entities[1] = m_Manager.CreateEntity(archetype);
            var checkEntity2 = m_Manager.CreateEntity(archetype);

            m_Manager.AddComponent(entities, typeof(EcsTestData2));

            Assert.IsTrue(IndexInChunkIsValid(checkEntity1));
            Assert.IsTrue(IndexInChunkIsValid(checkEntity2));
        }

        [Test]
        public void FoundComponentInterface()
        {
            var fooTypes = m_Manager.GetAssignableComponentTypes(typeof(IEcsFooInterface));
            Assert.AreEqual(1, fooTypes.Count);
            Assert.AreEqual(typeof(EcsFooTest), fooTypes[0]);

            var barTypes = m_Manager.GetAssignableComponentTypes(typeof(IEcsNotUsedInterface));
            Assert.AreEqual(0, barTypes.Count);
        }

        [Test]
        public void FoundComponentInterfaceNonAllocating()
        {
            var list = new List<Type>();
            var fooTypes = m_Manager.GetAssignableComponentTypes(typeof(IEcsFooInterface), list);
            Assert.AreEqual(1, fooTypes.Count);
            Assert.AreEqual(typeof(EcsFooTest), fooTypes[0]);

            list.Clear();
            var barTypes = m_Manager.GetAssignableComponentTypes(typeof(IEcsNotUsedInterface), list);
            Assert.AreEqual(0, barTypes.Count);
        }

        [Test]
        public void VersionIsConsistent()
        {
            Assert.AreEqual(0, m_Manager.EntityOrderVersion);

            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            Assert.AreEqual(1, m_Manager.EntityOrderVersion);

            m_Manager.AddComponentData(entity, new EcsTestData2(0));
            Assert.AreEqual(3, m_Manager.EntityOrderVersion);

            m_Manager.SetComponentData(entity, new EcsTestData2(5));
            Assert.AreEqual(3, m_Manager.EntityOrderVersion); // Shouldn't change when just setting data

            m_Manager.RemoveComponent<EcsTestData2>(entity);
            Assert.AreEqual(5, m_Manager.EntityOrderVersion);

            m_Manager.DestroyEntity(entity);
            Assert.AreEqual(6, m_Manager.EntityOrderVersion);
        }

        [Test]
        public void GetChunkVersions_ReflectsChange()
        {
            m_Manager.Debug.SetGlobalSystemVersion(1);

            var entity = m_Manager.CreateEntity(typeof(EcsTestData));

            m_Manager.Debug.SetGlobalSystemVersion(2);

            var version = m_Manager.GetChunkVersionHash(entity);

            m_Manager.SetComponentData(entity, new EcsTestData());

            var version2 = m_Manager.GetChunkVersionHash(entity);

            Assert.AreNotEqual(version, version2);
        }

        interface TestInterface
        {
        }

        struct TestInterfaceComponent : TestInterface, IComponentData
        {
            public int Value;
        }

        [Test]
        public void GetComponentBoxedSupportsInterface()
        {
            var entity = m_Manager.CreateEntity();

            m_Manager.AddComponentData(entity, new TestInterfaceComponent { Value = 5});
            var obj = m_Manager.Debug.GetComponentBoxed(entity, typeof(TestInterface));

            Assert.AreEqual(typeof(TestInterfaceComponent), obj.GetType());
            Assert.AreEqual(5, ((TestInterfaceComponent)obj).Value);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void GetComponentBoxedThrowsWhenInterfaceNotFound()
        {
            var entity = m_Manager.CreateEntity();
            Assert.Throws<ArgumentException>(() => m_Manager.Debug.GetComponentBoxed(entity, typeof(TestInterface)));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void EntityArchetypeFromDifferentEntityManagerThrows()
        {
            using (var world = new World("Temp"))
            {
                var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
                var nullArchetype = default(EntityArchetype);
                Assert.Throws<ArgumentException>(() => world.EntityManager.CreateEntity(archetype));
                Assert.Throws<ArgumentException>(() => world.EntityManager.CreateEntity(nullArchetype));
            }
        }

        // test for DOTS-3479: changing a chunk's archetype in-place temporarily leaves the new archetype's chunks-with-empty-slots
        // list in an inconsistent state.
        [Test]
        public void ChangeArchetypeInPlace_WithSharedComponents_ChunkIsInFreeSlotList()
        {
            var srcArchetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp), typeof(EcsTestTag));
            var dstArchetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));

            // Create two chunks in srcArchetype with different shared component values
            var ent0 = m_Manager.CreateEntity(srcArchetype);
            m_Manager.SetSharedComponentManaged(ent0, new EcsTestSharedComp {value = 17});
            var ent1 = m_Manager.CreateEntity(srcArchetype);
            m_Manager.SetSharedComponentManaged(ent1, new EcsTestSharedComp {value = 42});

            // move ent1's chunk to dstArchetype
            using(var query = m_Manager.CreateEntityQuery(typeof(EcsTestTag), typeof(EcsTestSharedComp)))
            {
                query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = 42});
                m_Manager.RemoveComponent<EcsTestTag>(query);
            }
            var archetypeChunk = m_Manager.GetChunk(ent1);
            Assert.AreEqual(dstArchetype, archetypeChunk.Archetype);

            // entity's chunk should have plenty of empty slots available
            Assert.AreEqual(1, archetypeChunk.Count);
            Assert.IsFalse(archetypeChunk.Full);

            var ent2 = m_Manager.CreateEntity(dstArchetype);
            // Changing ent2 to the same shared component values as ent1 should move ent2 to the same chunk as ent1.
            // If these asserts fail, it means ent1's chunk wasn't correctly added to dstArchetype's
            // FreeChunksBySharedComponents list (specifically, it was added under a hash of the wrong shared component
            // values), and a new chunk was created for ent2 instead.
            m_Manager.SetSharedComponentManaged(ent2, new EcsTestSharedComp {value = 42});
            Assert.AreEqual(2, archetypeChunk.Count); // ent1's chunk should contain both entities
            Assert.AreEqual(archetypeChunk, m_Manager.GetChunk(ent2)); // ent1 and ent2 should have the same chunk
        }

        [Test]
        public unsafe void ComponentsWithBool()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestComponentWithBool));
            var count = 128;
            var array = new NativeArray<Entity>(count, Allocator.Temp);
            m_Manager.CreateEntity(archetype, array);

            var hash = new NativeParallelHashMap<Entity, bool>(count, Allocator.Temp);

            var cg = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestComponentWithBool>());
            using (var chunks = cg.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator))
            {
                var boolsType = m_Manager.GetComponentTypeHandle<EcsTestComponentWithBool>(false);
                var entsType = m_Manager.GetEntityTypeHandle();

                foreach (var chunk in chunks)
                {
                    var bools = chunk.GetNativeArray(ref boolsType);
                    var entities = chunk.GetNativeArray(entsType);

                    for (var i = 0; i < chunk.Count; ++i)
                    {
                        bools[i] = new EcsTestComponentWithBool {value = (entities[i].Index & 1) == 1};
                        Assert.IsTrue(hash.TryAdd(entities[i], bools[i].value));
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                var data = m_Manager.GetComponentData<EcsTestComponentWithBool>(array[i]);
                Assert.AreEqual((array[i].Index & 1) == 1, data.value);
            }

            array.Dispose();
            hash.Dispose();
        }

        [TypeManager.ForcedMemoryOrderingAttribute(1)]
        struct BigComponentWithAlign1 : IComponentData
        {
            unsafe fixed byte val[1027];
        }

        [TypeManager.ForcedMemoryOrderingAttribute(2)]
        struct ComponentWithAlign8 : IComponentData
        {
            double val;
        }

        [MaximumChunkCapacity(1)]
        struct MaxCapacityTag1 : IComponentData {}

        [MaximumChunkCapacity(2)]
        struct MaxCapacityTag2 : IComponentData {}

        [MaximumChunkCapacity(3)]
        struct MaxCapacityTag3 : IComponentData {}

        [TestCase(typeof(MaxCapacityTag1))]
        [TestCase(typeof(MaxCapacityTag2))]
        [TestCase(typeof(MaxCapacityTag3))]
        [TestCase(typeof(EcsTestTag))]
        public unsafe void ChunkComponentRunIsAligned(Type maxCapacityTagType)
        {
            // Create an entity
            var archetype = m_Manager.CreateArchetype(typeof(BigComponentWithAlign1), typeof(ComponentWithAlign8), maxCapacityTagType);
            var entity = m_Manager.CreateEntity(archetype);
            // Get a pointer to the first bigger-aligned component
            var p0 = m_Manager.GetComponentDataRawRO(entity, TypeManager.GetTypeIndex<Entity>());
            var p1 = m_Manager.GetComponentDataRawRW(entity, TypeManager.GetTypeIndex<BigComponentWithAlign1>());
            var p2 = m_Manager.GetComponentDataRawRW(entity, TypeManager.GetTypeIndex<ComponentWithAlign8>());
            Assert.IsTrue(p0 < p1 && p1 < p2, "Order of components in memory is not as expected");

            // all component arrays need to be cache line aligned
            Assert.True(CollectionHelper.IsAligned(p0, CollectionHelper.CacheLineSize));
            Assert.True(CollectionHelper.IsAligned(p1, CollectionHelper.CacheLineSize));
            Assert.True(CollectionHelper.IsAligned(p2, CollectionHelper.CacheLineSize));
        }

        struct WillFitWithAlign : IComponentData
        {
            // sizeof(T) is not a constant
            public const int kSizeOfEntity = 8;
            public const int kSizeOfBigComponentWithAlign1 = 1027;

            public const int kAlignmentOverhead = 2 * CollectionHelper.CacheLineSize - kSizeOfEntity -
                (kSizeOfBigComponentWithAlign1 & (CollectionHelper.CacheLineSize - 1));
            public const int kWillFitSize =
                Chunk.kBufferSize - kSizeOfEntity - kSizeOfBigComponentWithAlign1 - kAlignmentOverhead;
            unsafe fixed byte val[kWillFitSize];
        }

        struct WontFitWithAlign : IComponentData
        {
            // Make component one byte larger than would fit in chunk
            unsafe fixed byte val[WillFitWithAlign.kWillFitSize + 1];
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public unsafe void CreatingArchetypeWithToLargeEntityThrows()
        {
            Assert.DoesNotThrow(() => m_Manager.CreateArchetype(typeof(BigComponentWithAlign1), typeof(WillFitWithAlign)));
            Assert.Throws<ArgumentException>(() => m_Manager.CreateArchetype(typeof(BigComponentWithAlign1), typeof(WontFitWithAlign)));
        }

        void CreateEntitiesWithDataToRemove(NativeArray<Entity> entities)
        {
            var count = entities.Length;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestDataEntity), typeof(EcsIntElement));

            m_Manager.CreateEntity(archetype, entities);

            for (int i = 0; i < count; i++)
            {
                m_Manager.SetComponentData(entities[i], new EcsTestData { value = i});
                m_Manager.SetComponentData(entities[i], new EcsTestData2 { value0 = 20000 + i, value1 = 40000 + i});
                m_Manager.SetComponentData(entities[i],
                    new EcsTestData3 { value0 = 50000 + i, value1 = 60000 + i, value2 = 70000 + i});
                m_Manager.SetComponentData(entities[i], new EcsTestDataEntity { value0 = i, value1 = entities[i] });
                var buffer = m_Manager.GetBuffer<EcsIntElement>(entities[i]);
                buffer.Add(new EcsIntElement {Value = i});
            }
        }

        void ValidateRemoveComponents(NativeArray<Entity> entities, NativeArray<Entity> entitiesToRemoveData)
        {
            var count = entities.Length;
            var group0 = m_Manager.CreateEntityQuery(typeof(EcsTestDataEntity));
            var entities0 = group0.ToEntityArray(Allocator.Persistent);

            Assert.AreEqual(entities0.Length, count);

            for (int i = 0; i < count; i++)
            {
                var entity = entities[i];
                var testDataEntity = m_Manager.GetComponentData<EcsTestDataEntity>(entity);
                var testBuffer = m_Manager.GetBuffer<EcsIntElement>(entity);
                FastAssert.AreEqual(testDataEntity.value1, entity);
                FastAssert.AreEqual(testDataEntity.value0, testBuffer[0].Value);
            }

            entities0.Dispose();
            group0.Dispose();

            for (int i = 0; i < entitiesToRemoveData.Length; i++)
            {
                FastAssert.IsFalse(m_Manager.HasComponent<EcsTestData>(entitiesToRemoveData[i]));
                FastAssert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entitiesToRemoveData[i]));
            }
        }

        [Test]
        public void BatchRemoveComponents()
        {
            var count = 1024;
            var entities = new NativeArray<Entity>(count, Allocator.Persistent);

            CreateEntitiesWithDataToRemove(entities);

            var entitiesToRemoveData = new NativeArray<Entity>(count / 2, Allocator.Persistent);
            for (int i = 0; i < count / 2; i++)
                entitiesToRemoveData[i] = entities[i * 2];

            m_Manager.RemoveComponent(entitiesToRemoveData, typeof(EcsTestData));

            var group0 = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            var group1 = m_Manager.CreateEntityQuery(typeof(EcsTestData2), typeof(EcsTestData3));

            var entities0 = group0.ToEntityArray(Allocator.Persistent);
            var entities1 = group1.ToEntityArray(Allocator.Persistent);

            Assert.AreEqual(entities0.Length, count / 2);
            Assert.AreEqual(entities1.Length, count);

            entities0.Dispose();
            entities1.Dispose();

            m_Manager.RemoveComponent(entitiesToRemoveData, typeof(EcsTestData2));

            var entities1b = group1.ToEntityArray(Allocator.Persistent);

            Assert.AreEqual(entities1b.Length, count / 2);

            entities1b.Dispose();

            entities.Dispose();
            entitiesToRemoveData.Dispose();

            group0.Dispose();
            group1.Dispose();
        }

        [Test]
        public void BatchRemoveComponentsValuesSimplified()
        {
            var count = 4;
            var entities = new NativeArray<Entity>(count, Allocator.Persistent);

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestDataEntity), typeof(EcsIntElement));

            m_Manager.CreateEntity(archetype, entities);

            for (int i = 0; i < count; i++)
            {
                m_Manager.SetComponentData(entities[i], new EcsTestData { value = i});
                m_Manager.SetComponentData(entities[i], new EcsTestDataEntity { value0 = i, value1 = entities[i] });
                var buffer = m_Manager.GetBuffer<EcsIntElement>(entities[i]);
                buffer.Add(new EcsIntElement {Value = i});
            }

            var entitiesToRemoveData = new NativeArray<Entity>(1, Allocator.Persistent);
            entitiesToRemoveData[0] = entities[0];

            m_Manager.RemoveComponent(entitiesToRemoveData, typeof(EcsTestData));

            ValidateRemoveComponents(entities, entitiesToRemoveData);

            entities.Dispose();
            entitiesToRemoveData.Dispose();
        }

        [Test]
        public void BatchRemoveComponentsValues()
        {
            var count = 1024;
            var entities = new NativeArray<Entity>(count, Allocator.Persistent);

            CreateEntitiesWithDataToRemove(entities);

            var entitiesToRemoveData = new NativeArray<Entity>(count / 2, Allocator.Persistent);
            for (int i = 0; i < count / 2; i++)
            {
                entitiesToRemoveData[i] = entities[i * 2];
            }

            m_Manager.RemoveComponent(entitiesToRemoveData, typeof(EcsTestData));
            m_Manager.RemoveComponent(entitiesToRemoveData, typeof(EcsTestData2));

            ValidateRemoveComponents(entities, entitiesToRemoveData);

            entities.Dispose();
            entitiesToRemoveData.Dispose();
        }

        [Test]
        public void BatchRemoveComponentsValuesWholeChunks()
        {
            var count = 1024;
            var entities = new NativeArray<Entity>(count, Allocator.Persistent);

            CreateEntitiesWithDataToRemove(entities);

            var entitiesToRemoveData = new NativeArray<Entity>(count / 2, Allocator.Persistent);
            for (int i = 0; i < count / 2; i++)
                entitiesToRemoveData[i] = entities[i];

            m_Manager.RemoveComponent(entitiesToRemoveData, typeof(EcsTestData));
            m_Manager.RemoveComponent(entitiesToRemoveData, typeof(EcsTestData2));

            ValidateRemoveComponents(entities, entitiesToRemoveData);

            entities.Dispose();
            entitiesToRemoveData.Dispose();
        }

        [Test]
        public void BatchRemoveComponentsValuesDuplicates()
        {
            var count = 1024;
            var entities = new NativeArray<Entity>(count, Allocator.Persistent);

            CreateEntitiesWithDataToRemove(entities);

            var entitiesToRemoveData = new NativeArray<Entity>(count / 2, Allocator.Persistent);
            for (int i = 0; i < count / 2; i++)
                entitiesToRemoveData[i] = entities[i % 16];

            m_Manager.RemoveComponent(entitiesToRemoveData, typeof(EcsTestData));
            m_Manager.RemoveComponent(entitiesToRemoveData, typeof(EcsTestData2));

            ValidateRemoveComponents(entities, entitiesToRemoveData);

            entities.Dispose();
            entitiesToRemoveData.Dispose();
        }

        [Test]
        public void BatchRemoveComponentsValuesBatches()
        {
            var count = 1024;
            var entities = new NativeArray<Entity>(count, Allocator.Persistent);

            CreateEntitiesWithDataToRemove(entities);

            var entitiesToRemoveData = new NativeArray<Entity>(count / 2, Allocator.Persistent);
            for (int i = 0; i < count / 2; i++)
            {
                var skipOffset = (i >> 5) * 40;
                var offset = i & 0x1f;
                entitiesToRemoveData[i] = entities[skipOffset + offset];
            }

            m_Manager.RemoveComponent(entitiesToRemoveData, typeof(EcsTestData));
            m_Manager.RemoveComponent(entitiesToRemoveData, typeof(EcsTestData2));

            ValidateRemoveComponents(entities, entitiesToRemoveData);

            for (int i = 0; i < entitiesToRemoveData.Length; i++)
            {
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData>(entitiesToRemoveData[i]));
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entitiesToRemoveData[i]));
            }

            entities.Dispose();
            entitiesToRemoveData.Dispose();
        }

        [Test]
        public void BatchAddComponents([Values(1,5,10,100,1000)] int count)
        {
            var entities = new NativeArray<Entity>(count, Allocator.Persistent);
            var entitiesToAddData = new NativeArray<Entity>((count+1) / 2, Allocator.Persistent);

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            m_Manager.CreateEntity(archetype, entities);

            for (int i = 0; i < entitiesToAddData.Length; i++)
                entitiesToAddData[i] = entities[i*2];

            m_Manager.AddComponent(entitiesToAddData, typeof(EcsTestData2));

            for (int i = 0; i < count; ++i)
            {
                // even-numbered entities should have the component, odds shouldn't
                Assert.AreEqual((i % 2) == 0, m_Manager.HasComponent<EcsTestData2>(entities[i]));
            }

            entitiesToAddData.Dispose();
            entities.Dispose();
        }

        [Test]
        public void AddComponentQueryWithArray()
        {
            m_Manager.CreateEntity(typeof(EcsTestData2));
            m_Manager.CreateEntity();
            m_Manager.CreateEntity();
            m_Manager.CreateEntity();

            var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator);
            var data = new NativeArray<EcsTestData>(entities.Length, Allocator.Temp);
            for (int i = 0; i != data.Length; i++)
                data[i] = new EcsTestData(entities[i].Index);

            m_Manager.AddComponentData(m_Manager.UniversalQuery, data);

            for (int i = 0; i != data.Length; i++)
                Assert.AreEqual(entities[i].Index, data[i].value);
            Assert.AreEqual(4, entities.Length);

            data.Dispose();
        }

        [Test]
        public void AddSharedComponentData_WithEntityQuery_ThatHasNoMatch_WillNotCorruptInternalState()
        {
            var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            m_Manager.AddSharedComponentManaged(entityQuery, new EcsTestSharedComp(1));
            m_Manager.Debug.CheckInternalConsistency();
        }

        [Test]
        public void SetSharedComponentData_WithEntityQuery_ThatHasNoMatch_WillNotCorruptInternalState()
        {
            var entityQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            m_Manager.SetSharedComponentManaged(entityQuery, new EcsTestSharedComp(1));
            m_Manager.Debug.CheckInternalConsistency();
        }

        [StructLayout(LayoutKind.Explicit, Size = 3)]
        public struct ComponentDataByteThen2PaddingBytes : IComponentData
        {
            [FieldOffset(0)]
            public byte mByte;
        }

        [StructLayout(LayoutKind.Explicit, Size = 3)]
        public struct ComponentPaddingByteThenDataByteThenPaddingByte: IComponentData
        {
            [FieldOffset(1)]
            public byte mByte;
        }

        /// <summary>
        /// This test is to ensure we can rely on padding bytes being zero-initialized such that memory comparisons
        /// of chunks written to using SetComponent or by storing structs via chunk pointers is fine. A user will
        /// only encounter problems where default(c) != default(c) if they explicitly alter the padding bytes of the struct
        /// they copy into chunk memory, to which we say, they must intend for this struct to be different than one that
        /// didn't have it's padding bytes explicitly altered.
        ///
        /// Relevant part of the standard that ensures types are by default zero initialized:
        /// ECMA-335 I.12.6.8
        /// All memory allocated for static variables (other than those assigned RVAs within a PE
        /// file, see Partition II) and objects shall be zeroed before they are made visible to any user code.
        /// </summary>
        [Test]
        public unsafe void ValidatePaddingBytesAreOverwrittenWhenReusingChunks()
        {
            const byte kPaddingValue = 0xFF;
            const byte kMemoryInitPattern = 0xAA;
            Assertions.Assert.AreNotEqual(kMemoryInitPattern, kPaddingValue);
            Assertions.Assert.AreNotEqual(0, kPaddingValue);

            m_ManagerDebug.UseMemoryInitPattern = true;
            m_ManagerDebug.MemoryInitPattern = kMemoryInitPattern;

            NativeParallelHashSet<ulong> seenChunkBuffers = new NativeParallelHashSet<ulong>(8, World.UpdateAllocator.ToAllocator);
            var dppArchetype = m_Manager.CreateArchetype(typeof(ComponentDataByteThen2PaddingBytes));
            var pdpArchetype = m_Manager.CreateArchetype(typeof(ComponentPaddingByteThenDataByteThenPaddingByte));
            Assert.AreEqual(dppArchetype.ChunkCapacity, pdpArchetype.ChunkCapacity);

            // Allocate entities to fill multiple chunks with components with known "bad" padding bytes
            var entityCount = dppArchetype.ChunkCapacity * 2;
            var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < entityCount; ++i)
            {
                var entity = m_Manager.CreateEntity(dppArchetype);
                entities[i] = entity;

                var component = new ComponentDataByteThen2PaddingBytes();
                var pData = (byte*) UnsafeUtility.AddressOf(ref component);
                pData[0] = (byte) entity.Index; // data byte
                pData[1] = kPaddingValue;       // padding byte
                pData[2] = kPaddingValue;       // padding byte

                m_Manager.SetComponentData(entity, component);
            }

            Assert.AreEqual(2, dppArchetype.ChunkCount);
            {
                var chunks = dppArchetype.Archetype->Chunks;
                for (int i = 0, count = chunks.Count; i < count; ++i)
                    seenChunkBuffers.Add((ulong)chunks[i].Buffer);
            }

            // Validate the components have the byte pattern we expect
            foreach (var entity in entities)
            {
                var component = m_Manager.GetComponentData<ComponentDataByteThen2PaddingBytes>(entity);
                var pData = (byte*) UnsafeUtility.AddressOf(ref component);
                Assert.AreEqual((byte) entity.Index, pData[0]);
                Assert.AreEqual(kPaddingValue, pData[1] );
                Assert.AreEqual(kPaddingValue, pData[2]);
            }

            // Delete all entities in the second chunk, pushing the chunk back on the freelist
            for (int i = dppArchetype.ChunkCapacity; i < entityCount; ++i)
                m_Manager.DestroyEntity(entities[i]);
            Assert.AreEqual(1, dppArchetype.ChunkCount);

            // Allocate a new archetype with the opposite stripping pattern
            // and ensure the chunk retrieved is a chunk we've seen already
            var newEntityCount = pdpArchetype.ChunkCapacity;
            var newEntities = CollectionHelper.CreateNativeArray<Entity>(newEntityCount, World.UpdateAllocator.ToAllocator);
            for (int i = 0; i < newEntityCount; ++i)
            {
                var entity = m_Manager.CreateEntity(pdpArchetype);
                newEntities[i] = entity;

                var component = new ComponentPaddingByteThenDataByteThenPaddingByte();
                var pData = (byte*) UnsafeUtility.AddressOf(ref component);
                Assertions.Assert.AreEqual(0, pData[0]); // pData[0] -- padding byte left uninitialized
                pData[1] = (byte) entity.Index; // data byte
                Assertions.Assert.AreEqual(0, pData[0]); // pData[2] -- padding byte left uninitialized

                m_Manager.SetComponentData(entity, component);
            }
            Assert.AreEqual(1, pdpArchetype.ChunkCount);
            Assert.IsTrue(seenChunkBuffers.Contains((ulong)pdpArchetype.Archetype->Chunks[0].Buffer));

            // Validate the components have zero initialized padding bytes and not the poisoned padding
            // i.e. what you store to chunk memory is what you get. You are not affected by the
            // non-zero-initialized chunk memory state
            foreach (var entity in newEntities)
            {
                var component = m_Manager.GetComponentData<ComponentPaddingByteThenDataByteThenPaddingByte>(entity);
                var pData = (byte*) UnsafeUtility.AddressOf(ref component);
                Assert.AreEqual((byte)0, pData[0] );
                Assert.AreEqual((byte) entity.Index, pData[1]);
                Assert.AreEqual((byte)0, pData[2]);
            }
        }

        [Test]
        [Ignore("Fix had to be reverted, check issue #2996")]
        public void Fix1602()
        {
            var startArchetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var startGroup = m_Manager.CreateEntityQuery(typeof(EcsTestData), ComponentType.Exclude<EcsTestTag>());
            var endGroup = m_Manager.CreateEntityQuery(typeof(EcsTestTag));
            for (int i = 0; i < 100; i++)
            {
                var entity = m_Manager.CreateEntity(startArchetype);
                m_Manager.AddComponent<EcsTestTag>(startGroup);
            }
            Assert.AreEqual(startGroup.CalculateChunkCount(), 0);
            Assert.AreEqual(endGroup.CalculateChunkCount(), 1);
            startGroup.Dispose();
            endGroup.Dispose();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Atomic Safety Handles for dispose checks")]
        public void EntityManager_DoubleDispose_UsesCustomOwnerTypeName()
        {
            World tempWorld = new World("TestWorld");
            var entityManager = tempWorld.EntityManager;
            tempWorld.Dispose();
            Assert.That(() => entityManager.DestroyInstance(),
                Throws.Exception.TypeOf<ObjectDisposedException>()
                    .With.Message.Contains("EntityManager"));
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        class TestInterfaceManagedComponent : TestInterface, IComponentData
        {
            public int Value;
        }

        [Test]
        public void VersionIsConsistent_ManagedComponents()
        {
            Assert.AreEqual(0, m_Manager.EntityOrderVersion);

            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            Assert.AreEqual(1, m_Manager.EntityOrderVersion);

            m_Manager.AddComponentData(entity, new EcsTestData2(0));
            Assert.AreEqual(3, m_Manager.EntityOrderVersion);

            m_Manager.SetComponentData(entity, new EcsTestData2(5));
            Assert.AreEqual(3, m_Manager.EntityOrderVersion); // Shouldn't change when just setting data

            m_Manager.AddComponentData(entity, new EcsTestManagedComponent());
            Assert.AreEqual(5, m_Manager.EntityOrderVersion);

            m_Manager.SetComponentData(entity, new EcsTestManagedComponent() { value = "new" });
            Assert.AreEqual(5, m_Manager.EntityOrderVersion); // Shouldn't change when just setting data

            m_Manager.RemoveComponent<EcsTestData2>(entity);
            Assert.AreEqual(7, m_Manager.EntityOrderVersion);

            m_Manager.DestroyEntity(entity);
            Assert.AreEqual(8, m_Manager.EntityOrderVersion);
        }

        [Test]
        public void GetComponentBoxedSupportsInterface_ManagedComponent()
        {
            var entity = m_Manager.CreateEntity();

            m_Manager.AddComponentData(entity, new TestInterfaceManagedComponent { Value = 5 });
            var obj = m_Manager.Debug.GetComponentBoxed(entity, typeof(TestInterface));

            Assert.AreEqual(typeof(TestInterfaceManagedComponent), obj.GetType());
            Assert.AreEqual(5, ((TestInterfaceManagedComponent)obj).Value);
        }


        [Test]
        public unsafe void ManagedComponent_DoubleAdd_NoDoubleDispose()
        {
            // regression test for DOTS-5122
            var ent = m_Manager.CreateEntity();
            using var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator);
            var value = new EcsTestManagedCompWithRefCount();
            ecb.AddComponent(ent, value);
            ecb.AddComponent(ent, value); // oops, accidental redundant add!
            ecb.Playback(m_Manager);
            Assert.AreEqual(1, value.RefCount); // without the double-dispose guard, this refcount would be zero here. That would be bad.
            var newValue = new EcsTestManagedCompWithRefCount();
            m_Manager.SetComponentObject(ent, ComponentType.ReadWrite<EcsTestManagedCompWithRefCount>(), newValue);
            Assert.AreEqual(0, value.RefCount);
            Assert.AreEqual(1, newValue.RefCount);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires extra debug checks")]
        public void ManagedComponent_MoveComponent_Works()
        {
            var srcEntity = m_Manager.CreateEntity();
            var dstEntity = m_Manager.CreateEntity();
            var destroyedEntity = m_Manager.CreateEntity();
            m_Manager.DestroyEntity(destroyedEntity);

            // null src is an error (in debug builds only)
            Assert.Throws<ArgumentException>(() => m_Manager.MoveComponent<EcsTestDisposableManagedComponent>(Entity.Null, dstEntity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestDisposableManagedComponent>(dstEntity));
            // null dst is an error
            Assert.Throws<ArgumentException>(() => m_Manager.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, Entity.Null));
            // invalid but non-null src is an error
            Assert.Throws<ArgumentException>(() => m_Manager.MoveComponent<EcsTestDisposableManagedComponent>(destroyedEntity, dstEntity));
            // invalid but non-null dst is an error
            Assert.Throws<ArgumentException>(() => m_Manager.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, destroyedEntity));
            // missing target component on src is an error
            Assert.Throws<ArgumentException>(() => m_Manager.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, dstEntity));

            var componentValue = "value1";
            m_Manager.AddComponentData(srcEntity, new EcsTestDisposableManagedComponent { Value = componentValue });
            // src == dst is a no-op
            m_Manager.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, srcEntity);
            Assert.AreEqual(componentValue, m_Manager.GetComponentData<EcsTestDisposableManagedComponent>(srcEntity).Value);
            Assert.IsFalse(m_Manager.HasComponent<EcsTestDisposableManagedComponent>(dstEntity));
            // If dst doesn't have the target component, it's added, and the value is not disposed
            m_Manager.MoveComponent<EcsTestDisposableManagedComponent>(srcEntity, dstEntity);
            Assert.IsFalse(m_Manager.HasComponent<EcsTestDisposableManagedComponent>(srcEntity));
            Assert.AreEqual(componentValue, m_Manager.GetComponentData<EcsTestDisposableManagedComponent>(dstEntity).Value);
        }

        [Test]
        public void ManagedComponent_MoveComponent_DestEntityHasTargetComponent_Works()
        {
            var srcEnt = m_Manager.CreateEntity();
            var dstEnt = m_Manager.CreateEntity();
            var value1 = new EcsTestDisposableManagedComponent { Value = "Value 1" };
            var value2 = new EcsTestDisposableManagedComponent { Value = "Value 2" };

            // dst's T is null / default-initialized
            m_Manager.AddComponentData(srcEnt, value1);
            m_Manager.AddComponent<EcsTestDisposableManagedComponent>(dstEnt);
            Assert.DoesNotThrow(() => m_Manager.MoveComponent<EcsTestDisposableManagedComponent>(srcEnt,dstEnt));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestDisposableManagedComponent>(srcEnt));
            Assert.AreEqual(value1,m_Manager.GetComponentData<EcsTestDisposableManagedComponent>(dstEnt));

            // dst's T value matches src's
            m_Manager.AddComponentData(srcEnt, value1);
            Assert.DoesNotThrow(() => m_Manager.MoveComponent<EcsTestDisposableManagedComponent>(srcEnt,dstEnt));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestDisposableManagedComponent>(srcEnt));
            Assert.AreEqual(value1,m_Manager.GetComponentData<EcsTestDisposableManagedComponent>(dstEnt));

            // dst's T value does not match src. In this case, we need to make sure dst's old value is correctly disposed and doesn't leak.
            m_Manager.AddComponentData(srcEnt, value2);
            Assert.DoesNotThrow(() => m_Manager.MoveComponent<EcsTestDisposableManagedComponent>(srcEnt,dstEnt));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestDisposableManagedComponent>(srcEnt));
            Assert.AreEqual(value2,m_Manager.GetComponentData<EcsTestDisposableManagedComponent>(dstEnt));
        }

        [Test]
        public unsafe void ManagedComponents()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestManagedComponent));
            var count = 128;
            var array = new NativeArray<Entity>(count, Allocator.Temp);
            m_Manager.CreateEntity(archetype, array);

            var hash = new NativeParallelHashMap<Entity, FixedString64Bytes>(count, Allocator.Temp);

            var cg = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestManagedComponent>());
            using (var chunks = cg.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator))
            {
                var ManagedComponentType = m_Manager.GetComponentTypeHandle<EcsTestManagedComponent>(false);
                var entsType = m_Manager.GetEntityTypeHandle();

                foreach (var chunk in chunks)
                {
                    var components = chunk.GetManagedComponentAccessor(ref ManagedComponentType, m_Manager);
                    var entities = chunk.GetNativeArray(entsType);

                    Assert.AreEqual(chunk.Count, components.Length);

                    for (var i = 0; i < chunk.Count; ++i)
                    {
                        components[i] = new EcsTestManagedComponent { value = entities[i].Index.ToString() };
                        Assert.IsTrue(hash.TryAdd(entities[i], new FixedString64Bytes(components[i].value)));
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                var data = m_Manager.GetComponentData<EcsTestManagedComponent>(array[i]);
                Assert.AreEqual(array[i].Index.ToString(), data.value);
            }

            array.Dispose();
            hash.Dispose();
        }

        [Test]
        public void GetManagedComponentAccessor_ReturnsEmptyArray_IfTypeIsMissing()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData4), typeof(EcsTestData5));
            var count = 128;
            using(var array = new NativeArray<Entity>(count, Allocator.Temp))
            {
                m_Manager.CreateEntity(archetype, array);

                var cg = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData4>());
                using (var chunks = cg.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator))
                {
                    var managedComponentType = m_Manager.GetComponentTypeHandle<EcsTestManagedComponent>(false);

                    foreach (var chunk in chunks)
                    {
                        var components = chunk.GetManagedComponentAccessor(ref managedComponentType, m_Manager);
                        Assert.AreEqual(0, components.Length);
                    }
                }
            }
        }

        public class ComplexManagedComponent : IComponentData
        {
            public class MyClass
            {
                public string data;
            }

            public ComplexManagedComponent()
            {
                String = "default";
                Class = new MyClass() { data = "default" };
                List = new List<MyClass>();
            }

            public string String;
            public List<MyClass> List;
            public MyClass Class;
        }

        [Test]
        public void Instantiate_DeepClone_ManagedComponents()
        {
            var entity = m_Manager.CreateEntity();

            var originalString = "SomeString";
            var originalClassData = "SomeData";
            var originalClass = new ComplexManagedComponent.MyClass() { data = originalClassData };
            var originalListClassData = "SomeListData";
            var originalListClass = new ComplexManagedComponent.MyClass() { data = originalListClassData };
            var originalList = new List<ComplexManagedComponent.MyClass>();
            originalList.Add(originalListClass);
            var originalManagedComponent = new ComplexManagedComponent()
            {
                String = originalString,
                List = originalList,
                Class = originalClass
            };
            m_Manager.AddComponentData(entity, originalManagedComponent);

            void ValidateInstance(ComplexManagedComponent inst)
            {
                Assert.AreEqual("SomeString", inst.String);
                Assert.AreEqual("SomeData", inst.Class.data);
                Assert.AreEqual(1, inst.List.Count);
                Assert.AreEqual("SomeListData", inst.List[0].data);
            }

            var instance = m_Manager.Instantiate(entity);
            var instanceComponent = m_Manager.GetComponentData<ComplexManagedComponent>(instance);
            ValidateInstance(instanceComponent);

            // We change our managed component but our new instance should be unaffected
            originalString = "SomeOtherString";
            originalClassData = "SomeOtherData";
            originalListClassData = "SomeOtherListData";
            originalList.Clear();

            Assert.AreEqual("SomeString", instanceComponent.String);
            Assert.AreEqual("SomeData", instanceComponent.Class.data);
            Assert.AreEqual(1, instanceComponent.List.Count);
            Assert.AreEqual("SomeListData", instanceComponent.List[0].data);
            ValidateInstance(instanceComponent);

            Assert.AreSame(m_Manager.GetComponentData<ComplexManagedComponent>(entity), originalManagedComponent);
            Assert.AreNotSame(instanceComponent, originalManagedComponent);
        }

        class ManagedComponentWithArray : IComponentData
        {
            public int[] IntArray;
        }

        [Test]
        public void Instantiate_DeepClone_ManagedComponentWithZeroSizedArray()
        {
            var originalEntity = m_Manager.CreateEntity();
            var originalComponent = new ManagedComponentWithArray { IntArray = new int[0] };

            m_Manager.AddComponentData(originalEntity, originalComponent);

            var instance = m_Manager.Instantiate(originalEntity);
            var instanceComponent = m_Manager.GetComponentData<ManagedComponentWithArray>(instance);

            Assert.That(originalComponent.IntArray, Is.Not.SameAs(instanceComponent.IntArray));
            Assert.That(instanceComponent.IntArray.Length, Is.EqualTo(0));
        }

        [Test]
        public void Instantiate_DeepClone_ManagedComponentWithNullArray()
        {
            var originalEntity = m_Manager.CreateEntity();
            var originalComponent = new ManagedComponentWithArray { IntArray = null };

            m_Manager.AddComponentData(originalEntity, originalComponent);

            var instance = m_Manager.Instantiate(originalEntity);
            var instanceComponent = m_Manager.GetComponentData<ManagedComponentWithArray>(instance);

            Assert.That(instanceComponent.IntArray, Is.Null);
        }

        class ManagedComponentWithNestedClass : IComponentData
        {
#pragma warning disable 649
            public class NestedClass
            {
                public float x;
            }

            public NestedClass Nested;
        }
#pragma warning restore 649

        [Test]
        public void Instantiate_DeepClone_ManagedComponentWithNullReferenceType()
        {
            var originalEntity = m_Manager.CreateEntity();
            var originalComponent = new ManagedComponentWithNestedClass { Nested = null };

            m_Manager.AddComponentData(originalEntity, originalComponent);

            var instance = m_Manager.Instantiate(originalEntity);
            var instanceComponent = m_Manager.GetComponentData<ManagedComponentWithNestedClass>(instance);

            Assert.That(instanceComponent.Nested, Is.Null);
        }

        [Test]
        public void AddComponentKeepsObjectReference()
        {
            var entity = m_Manager.CreateEntity();
            var obj = new EcsTestManagedComponent();
            m_Manager.AddComponentData(entity, obj);
            m_Manager.AddComponentData(entity, new EcsTestData());
            m_Manager.AddComponentData(entity, new ComplexManagedComponent());
            Assert.AreSame(m_Manager.GetComponentData<EcsTestManagedComponent>(entity), obj);
        }

        [Test]
        public void ManagedAddComponentDefaultsToNull()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddComponent<EcsTestManagedComponent>(entity);
            Assert.AreEqual(null, m_Manager.GetComponentData<EcsTestManagedComponent>(entity));

            m_Manager.RemoveComponent<EcsTestManagedComponent>(entity);
            Assert.IsFalse(m_Manager.HasComponent<EcsTestManagedComponent>(entity));
        }

        [Test]
        public void AddComponentWithExplicitNull()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddComponentData<EcsTestManagedComponent>(entity, null);
            Assert.AreEqual(null, m_Manager.GetComponentData<EcsTestManagedComponent>(entity));
        }

        [Test]
        public void SetManagedComponentDataToNull()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddComponentData<EcsTestManagedComponent>(entity, new EcsTestManagedComponent());
            m_Manager.SetComponentData<EcsTestManagedComponent>(entity, null);
            Assert.AreEqual(null, m_Manager.GetComponentData<EcsTestManagedComponent>(entity));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void AddMismatchedManagedComponentThrows()
        {
            var entity = m_Manager.CreateEntity();
            Assert.Throws<ArgumentException>(() =>
            {
                m_Manager.AddComponentData<EcsTestManagedComponent>(entity, new EcsTestManagedComponent2());
            });
        }

        [Test]
        public void CreateEntity_InputUsesAllocatorTemp_Works([Values(1, 10, 100, 1000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype();
            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            m_Manager.CreateEntity(archetype, entities);
            foreach (var ent in entities)
                Assert.IsTrue(m_Manager.Exists(ent));
            entities.Dispose();
        }

        [Test]
        public void Instantiate_InputUsesAllocatorTemp_Works([Values(1, 10, 100, 1000)] int entityCount)
        {
            var prefabEnt = m_Manager.CreateEntity(typeof(EcsTestData));
            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            m_Manager.Instantiate(prefabEnt, entities);
            foreach (var ent in entities)
            {
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(ent));
            }
            entities.Dispose();
        }

        [Test]
        public void CopyEntities_InputUsesAllocatorTemp_Works([Values(1, 10, 100, 1000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var srcEntities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (int i = 0; i < entityCount; ++i)
            {
                srcEntities[i] = m_Manager.CreateEntity(archetype);
                m_Manager.SetComponentData(srcEntities[i], new EcsTestData(i));
            }

            var dstEntities = new NativeArray<Entity>(entityCount, Allocator.Temp);

            m_Manager.CopyEntitiesInternal(srcEntities, dstEntities);

            for (int i = 0; i < srcEntities.Length; ++i)
            {
                Assert.AreEqual(archetype, m_Manager.GetChunk(dstEntities[i]).Archetype);
                Assert.AreEqual(m_Manager.GetComponentData<EcsTestData>(srcEntities[i]),
                    m_Manager.GetComponentData<EcsTestData>(dstEntities[i]));
            }

            srcEntities.Dispose();
            dstEntities.Dispose();
        }

        [Test]
        public void CopyEntitiesFrom_InputUsesAllocatorTemp_Works([Values(1, 10, 100, 1000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var srcEntities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (int i = 0; i < entityCount; ++i)
            {
                srcEntities[i] = m_Manager.CreateEntity(archetype);
                m_Manager.SetComponentData(srcEntities[i], new EcsTestData(i));
            }

            using var dstWorld = new World("Copy Destination World");
            var dstEntities = new NativeArray<Entity>(entityCount, Allocator.Temp);

            dstWorld.EntityManager.CopyEntitiesFrom(m_Manager, srcEntities, dstEntities);

            for (int i = 0; i < srcEntities.Length; ++i)
            {
                // Can't compare archetypes across Worlds?
                // Assert.AreEqual(archetype, dstWorld.EntityManager.GetChunk(dstEntities[i]).Archetype);
                Assert.AreEqual(m_Manager.GetComponentData<EcsTestData>(srcEntities[i]),
                    dstWorld.EntityManager.GetComponentData<EcsTestData>(dstEntities[i]));
            }

            srcEntities.Dispose();
            dstEntities.Dispose();
        }

        [Test]
        public void DestroyEntity_InputUsesAllocatorTemp_Works([Values(1, 10, 100, 1000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype();
            var entities = new NativeArray<Entity>(entityCount, Allocator.Temp);
            for (int i = 0; i < entityCount; ++i)
                entities[i] = m_Manager.CreateEntity(archetype);
            m_Manager.DestroyEntity(entities);
            foreach (var ent in entities)
                Assert.IsFalse(m_Manager.Exists(ent));
            entities.Dispose();
        }

        [Test]
        public void AddComponent_InputUsesAllocatorTemp_Works([Values(1, 10, 100, 1000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype();
            var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Temp);
            m_Manager.AddComponent<EcsTestData>(entities);
            foreach (var ent in entities)
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(ent));
            entities.Dispose();
        }

        [Test]
        public void RemoveComponent_InputUsesAllocatorTemp_Works([Values(1, 10, 100, 1000)] int entityCount)
        {
            var archetype = m_Manager.CreateArchetype();
            var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Temp);
            foreach (var ent in entities)
                m_Manager.AddComponent<EcsTestData>(ent);
            m_Manager.RemoveComponent<EcsTestData>(entities);
            foreach (var ent in entities)
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData>(ent));
            entities.Dispose();
        }

        [Test]
        public void GetEntityInfo_InvalidEntity_ReturnsEntityInvalid()
        {
#if ENTITY_STORE_V1
            var invalidEntity = new Entity {Index = m_Manager.EntityCapacity + 1, Version = 1};
            var info = m_Manager.Debug.GetEntityInfo(invalidEntity);
            Assert.AreEqual(info, "Entity.Invalid","Entity with Large Index failed test");
#endif

            var invalidEntity2 = new Entity {Index = -1, Version = 1};
            var info2 = m_Manager.Debug.GetEntityInfo(invalidEntity2);
            Assert.AreEqual(info2, "Entity.Invalid", "Entity with Negative Index failed test");
        }

        partial class WriteSignalSystem: SystemBase
        {
            struct WriteWhenSignaledJob : IJob
            {
                [ReadOnly]
                public ComponentLookup<EcsTestData> TestData;

                [NativeDisableUnsafePtrRestriction]
                public unsafe volatile int* Result;

                public unsafe void Execute()
                {
                    *Result =  1;
                }
            }

            protected override unsafe void OnUpdate()
            {
                const int COMPONENT_DATA_VALUE = 10;
                var entity = EntityManager.CreateEntity(typeof(EcsTestData));

                EntityManager.SetComponentData(entity, new EcsTestData
                {
                    value = COMPONENT_DATA_VALUE
                });
                var testData = GetComponentLookup<EcsTestData>(true);

                int JobResult = 0;
                Dependency = new WriteWhenSignaledJob
                {
                    Result = &JobResult,
                    TestData = testData,
                }.Schedule(Dependency);

                EcsTestData data = *(EcsTestData*) EntityManager.GetComponentDataRawRO(entity,
                    TypeManager.GetTypeIndex(typeof(EcsTestData)));

                Dependency.Complete();

                Assert.AreEqual(1, JobResult);
                Assert.AreEqual(COMPONENT_DATA_VALUE, data.value);
            }
        }

        [Test]
        public unsafe void GetComponentDataRaw_SafetyRO()
        {
            var system = World.CreateSystemManaged<WriteSignalSystem>();
            system.Update();
        }

#if !DOTS_DISABLE_DEBUG_NAMES
        [Test]
        public void EntityName_GetName_SetName_Managed_Works()
        {
            var archetype = m_Manager.CreateArchetype();
            var entity = m_Manager.CreateEntity(archetype);
            Assert.AreEqual("", m_Manager.GetName(entity));
            var name = "Test entity";
            m_Manager.SetName(entity,name);
            Assert.AreEqual(name, m_Manager.GetName(entity));
        }

        [Test]
        public void EntityName_SetName_SupportMultipleUpdates()
        {
            var entity = m_Manager.CreateEntity();
            Assert.That(m_Manager.GetName(entity), Is.Empty);

            m_Manager.SetName(entity, "Name");
            Assert.That(m_Manager.GetName(entity), Is.EqualTo("Name"));

            m_Manager.SetName(entity, "OtherName");
            Assert.That(m_Manager.GetName(entity), Is.EqualTo("OtherName"));
        }

        [Test]
        public void EntityName_FixedString_SetName_SupportMultipleUpdates()
        {
            var entity = m_Manager.CreateEntity();
            Assert.That(m_Manager.GetName(entity), Is.Empty);

            m_Manager.SetName(entity, new FixedString32Bytes("Name"));
            Assert.That(m_Manager.GetName(entity), Is.EqualTo("Name"));

            m_Manager.SetName(entity, new FixedString32Bytes("OtherName"));
            Assert.That(m_Manager.GetName(entity), Is.EqualTo("OtherName"));
        }

        [Test]
        public unsafe void EntityName_CopyName_SupportOverwritingExistingName()
        {
            var entityA = m_Manager.CreateEntity();
            var entityB = m_Manager.CreateEntity();
            m_Manager.SetName(entityA, "EntityA");
            m_Manager.SetName(entityB, "EntityB");
            Assert.That(m_Manager.GetName(entityA), Is.EqualTo("EntityA"));
            Assert.That(m_Manager.GetName(entityB), Is.EqualTo("EntityB"));

            m_Manager.GetCheckedEntityDataAccess()->EntityComponentStore->CopyName(entityB, entityA);

            Assert.That(m_Manager.GetName(entityA), Is.EqualTo("EntityA"));
            Assert.That(m_Manager.GetName(entityB), Is.EqualTo("EntityA"));
        }

        [Test]
        public void EntityName_GetName_SetName_FixedString_Works()
        {
            var archetype = m_Manager.CreateArchetype();
            var entity = m_Manager.CreateEntity(archetype);

            m_Manager.GetName(entity, out var actualName);
            Assert.AreEqual(new FixedString64Bytes(""), actualName);
            var name = new FixedString64Bytes("Test entity");
            m_Manager.SetName(entity,name);
            m_Manager.GetName(entity, out actualName);
            Assert.AreEqual(name,actualName);
        }


        [Test]
        public void EntityName_GetName_InvalidEntity()
        {
            string errorMsg = "ENTITY_NOT_FOUND";
            Entity invalidEntity = new Entity
            {
                Version = 9,
                Index = 2
            };

            m_Manager.GetName(invalidEntity, out var invalidFixed);
            //test unmanaged string
            Assert.AreEqual(new FixedString64Bytes(errorMsg), invalidFixed);
            //test managed string
            Assert.AreEqual(errorMsg,m_Manager.GetName(invalidEntity));
        }

        [Test]
        public unsafe void EntityName_SetName_InvalidEntity()
        {
            Assert.DoesNotThrow(() => m_Manager.SetName(Entity.Null,"managed argument"));
            Assert.DoesNotThrow(() => m_Manager.SetName(Entity.Null, new FixedString64Bytes("unmanaged argument")));
        }

        [BurstCompile(CompileSynchronously = true)]
        static unsafe void BurstSetNamesFn(ref EntityManager manager,FixedString64Bytes* names, in Entity* entities, int size)
        {
            for (int i = 0; i < size; i++)
            {
                manager.SetName(entities[i],names[i]);
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        static unsafe void BurstGetNamesFn(ref EntityManager manager,FixedString64Bytes* names, in Entity* entities, int size)
        {
            for (int i = 0; i < size; i++)
            {
                manager.GetName(entities[i],out var fixedName);
                names[i] = fixedName;
            }
        }

        unsafe delegate void BurstGetNameDelegate(ref EntityManager manager,FixedString64Bytes* names,
         in Entity* entities, int size);
        unsafe delegate void BurstSetNameDelegate(ref EntityManager manager,FixedString64Bytes* names,
         in Entity* entities, int size);


        [Test]
        public unsafe void EntityName_SetName_Burst()
        {
            var fp = BurstCompiler.CompileFunctionPointer<BurstSetNameDelegate>(BurstSetNamesFn);
            var invoke = fp.Invoke;

            int size = 100;
            var names = CollectionHelper.CreateNativeArray<FixedString64Bytes>(size, World.UpdateAllocator.ToAllocator);
            var entities = CollectionHelper.CreateNativeArray<Entity>(size, World.UpdateAllocator.ToAllocator);

            for (int i = 0; i < size; i++)
            {
                names[i] = new FixedString64Bytes("Entity " + i);
                entities[i] = m_Manager.CreateEntity();
            }

            invoke(ref m_Manager,(FixedString64Bytes*)names.GetUnsafePtr(),(Entity*)entities.GetUnsafeReadOnlyPtr(), size);

            for (int i = 0; i < size; i++)
            {
                Assert.AreEqual("Entity " + i, m_Manager.GetName(entities[i]));
            }
        }

        [Test]
        public unsafe void EntityName_GetName_Burst()
        {
            var fp = BurstCompiler.CompileFunctionPointer<BurstGetNameDelegate>(BurstGetNamesFn);
            var invoke = fp.Invoke;

            int size = 100;

            var entities = CollectionHelper.CreateNativeArray<Entity>(size, World.UpdateAllocator.ToAllocator);

            using(var names = CollectionHelper.CreateNativeArray<FixedString64Bytes>(size,World.UpdateAllocator.ToAllocator))
            {

                for (int i = 0; i < size; i++)
                {
                    entities[i] = m_Manager.CreateEntity();
                    m_Manager.SetName(entities[i], "Entity " + i);
                }

                invoke(ref m_Manager,(FixedString64Bytes*)names.GetUnsafePtr(),(Entity*)entities.GetUnsafeReadOnlyPtr(), size);

                for (int i = 0; i < size; i++)
                {
                    Assert.AreEqual("Entity " + i,names[i]);
                }
            }
        }

        [Test]
        public void EntityName_MoveEntities()
        {
            using(var srcWorld = new World("source"))
            using (var dstWorld = new World("destination"))
            {

                var srcEM = srcWorld.EntityManager;
                var entityA = srcEM.CreateEntity(typeof(EcsTestData));
                var entityB = srcEM.CreateEntity(typeof(EcsTestData));
                var entityC = srcEM.CreateEntity(typeof(EcsTestData));

                srcEM.SetName(entityA, "A");
                srcEM.SetName(entityB, "B");
                srcEM.SetName(entityC, "C");


                var dstEM = dstWorld.EntityManager;
                var entityD = dstEM.CreateEntity(typeof(EcsTestData));
                dstEM.SetName(entityD, "D");
                using (var remap = srcEM.CreateEntityRemapArray(World.UpdateAllocator.ToAllocator))
                {
                    var dstQuery = dstEM.CreateEntityQuery(typeof(EcsTestData));

                    //sanity check: one ECSTestData entity before MoveEntities
                    Assert.AreEqual(1, dstQuery.CalculateEntityCount());

                    dstEM.MoveEntitiesFrom(srcEM, srcEM.UniversalQuery, remap);

                    //old entity handles should still be valid
                    Assert.AreEqual("D", dstEM.GetName(entityD));


                    using (var queriedEntities = dstQuery.ToEntityArray(World.UpdateAllocator.ToAllocator))
                    {
                        Assert.AreEqual(4, queriedEntities.Length);

                        //make sure moved entities are now in destination EntityManager, with proper names
                        var expectedNames = new[] {"A", "B", "C", "D"};
                        var acutalNames = new string[expectedNames.Length];

                        for (int i = 0; i < queriedEntities.Length; i++)
                        {
                            acutalNames[i] = dstEM.GetName(queriedEntities[i]);
                        }

                        CollectionAssert.AreEquivalent(expectedNames, acutalNames);
                    }
                }
            }
        }

        [Test]
        public void EntityName_CopyName()
        {
            var entityA = m_Manager.CreateEntity(typeof(EcsTestData));
            var entityB = m_Manager.CreateEntity(typeof(EcsTestData));
            var entityC = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetName(entityA,"A");
            m_Manager.SetName(entityB,"B");
            m_Manager.SetName(entityC,"C");

            using(var srcEntities = CollectionHelper.CreateNativeArray<Entity>(new[] {entityA, entityB, entityC}, World.UpdateAllocator.ToAllocator))
            using(var dstEntities = CollectionHelper.CreateNativeArray<Entity>(srcEntities.Length,World.UpdateAllocator.ToAllocator))
            {

                //underlying function calls into EntityComponentStore.CopyName
                m_Manager.CopyEntitiesInternal(srcEntities,dstEntities);

                //make sure entities of interest are actually cloned in the manager
                var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

                using (var queriedEntities = query.ToEntityArray(World.UpdateAllocator.ToAllocator))
                {

                    //length sanity check
                    Assert.AreEqual(srcEntities.Length + dstEntities.Length, queriedEntities.Length);


                    //make sure the clones have the proper names
                    var expectedNames = new[] {"A", "A", "B", "B", "C", "C"};
                    var acutalNames = new string[expectedNames.Length];

                    for (int i = 0; i < queriedEntities.Length; i++)
                    {
                        acutalNames[i] = m_Manager.GetName(queriedEntities[i]);
                    }


                    CollectionAssert.AreEquivalent(expectedNames, acutalNames);
                }

            }
        }

        #region EntityManagerSpan

        [Test]
        public void CreateEntity_With_Span_Works()
        {
            //Entity from unburstable code
            var archetypeNonBurst = m_Manager.CreateArchetype(typeof(EcsTestData),typeof(EcsTestData2));
            Entity entityFromListNonBurst = m_Manager.CreateEntity(archetypeNonBurst);

            //Entity from burstable code list
            var componentList = new NativeList<ComponentType>(World.UpdateAllocator.ToAllocator)
            {
                ComponentType.ReadWrite<EcsTestData>(),
                ComponentType.ReadWrite<EcsTestData2>()
            };
            var archetypeBurst = m_Manager.CreateArchetype(componentList.AsArray());
            Entity entityFromListBurst = m_Manager.CreateEntity(archetypeBurst);

            //Entity from burstable code list span
            Entity entityFromSpan = m_Manager.CreateEntity(stackalloc [] {
                ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>()
            });

            //Now i should check if they have the same components/their archetype is the same
            //In this case I will check that these three are returned in the same query using WithAll
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EcsTestData, EcsTestData2>()
                .Build(EmptySystem);

            Assert.AreEqual(query.CalculateEntityCount(), 3);
        }

        [Test]
        public void CreateArchetype_With_Span_Works()
        {

            //Archetype from unburstable code
            var archetypeFromListNonBurst = m_Manager.CreateArchetype(typeof(EcsTestData),typeof(EcsTestData2));

            //Archetype from burstable code list
            var componentList = new NativeList<ComponentType>(World.UpdateAllocator.ToAllocator)
            {
                ComponentType.ReadWrite<EcsTestData>(),
                ComponentType.ReadWrite<EcsTestData2>()
            };
            var archetypeFromListBurst = m_Manager.CreateArchetype(componentList.AsArray());

            //Archetype from burstable code span
            var archetypeFromSpan = m_Manager.CreateArchetype(stackalloc[]
            {
                ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<EcsTestData2>()
            });

            //Now i should check if they have the archetypes are equal
            Assert.AreEqual(archetypeFromListBurst, archetypeFromSpan);
            Assert.AreEqual(archetypeFromListNonBurst, archetypeFromSpan);
            Assert.AreEqual(archetypeFromListBurst, archetypeFromListNonBurst);
        }

        #endregion

        [Test]
        public void GetAspect()
        {
            var entity0 = m_Manager.CreateEntity();
            m_Manager.AddComponentData(entity0, new EcsTestData(42));
            var aspect0 = m_Manager.GetAspect<EcsTestAspect0RW>(entity0);
            Assert.IsTrue(aspect0.EcsTestData.IsValid, "the aspect could not build the RefRW of EcsTestAspect0.EcsTestData");
            Assert.AreEqual(aspect0.EcsTestData.ValueRO.value, 42, "the component the RefRW of EcsTestAspect0.EcsTestData points to does not have the correct value");

            aspect0.EcsTestData.ValueRW.value = 420;
            var componentAfterWrite = m_Manager.GetComponentData<EcsTestData>(entity0);
            Assert.AreEqual(componentAfterWrite.value, 420, "The written value from the aspect's RefRW did not overwrite the component data");
        }

        partial struct ScheduleEcsTestDataForWritingSystem : ISystem
        {
            struct Job : IJob
            {
                public ComponentTypeHandle<EcsTestData> handle;
                public void Execute() {}
            }

            ComponentTypeHandle<EcsTestData> m_Handle;
            public void OnCreate(ref SystemState state)
                => m_Handle = state.GetComponentTypeHandle<EcsTestData>(); // Needs to be there for system to know which component dependencies were added.
            public void OnUpdate(ref SystemState state)
                => state.Dependency = new Job{handle = m_Handle}.Schedule(state.Dependency);
        }

        partial struct ScheduleEcsTestData2ForReadingSystem : ISystem
        {
            struct Job : IJob
            {
                [ReadOnly] public ComponentTypeHandle<EcsTestData2> handle;
                public void Execute() {}
            }

            ComponentTypeHandle<EcsTestData2> m_Handle;
            public void OnCreate(ref SystemState state)
                => m_Handle = state.GetComponentTypeHandle<EcsTestData2>(true); // Needs to be there for system to know which component dependencies were added.
            public void OnUpdate(ref SystemState state)
                => state.Dependency = new Job{handle = m_Handle}.Schedule(state.Dependency);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CompleteDependencyBeforeRO([Values] bool withComplete)
        {
            var e = m_Manager.CreateEntity(typeof(EcsTestData));
            var dataFromEntity = m_Manager.GetComponentLookup<EcsTestData>(true);

            // Creating a system will now complete the dependency since it creates and writes to the system entity,
            // so create them both first before updating.
            var scheduleEcsTestDataForWritingSystemRef = World.CreateSystem<ScheduleEcsTestDataForWritingSystem>();
            var scheduleEcsTestData2ForReadingSystemRef = World.CreateSystem<ScheduleEcsTestData2ForReadingSystem>();
            scheduleEcsTestDataForWritingSystemRef.Update(World.Unmanaged);
            scheduleEcsTestData2ForReadingSystemRef.Update(World.Unmanaged);

            Assert.Throws<InvalidOperationException>(() =>
            {
                var data = dataFromEntity[e];
            });

            if (withComplete)
            {
                m_Manager.CompleteDependencyBeforeRO<EcsTestData>();
                var data = dataFromEntity[e];
            }

            Assert.Throws<InvalidOperationException>(() =>
            {
                dataFromEntity[e] = new EcsTestData();
            });
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CompleteDependencyBeforeRW([Values] bool withComplete)
        {
            var e = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var data1FromEntity = m_Manager.GetComponentLookup<EcsTestData>(true);
            var data2FromEntity = m_Manager.GetComponentLookup<EcsTestData2>(false);

            // Creating a system will now complete the dependency since it creates and writes to the system entity,
            // so create them both first before updating.
            var scheduleEcsTestDataForWritingSystemRef = World.CreateSystem<ScheduleEcsTestDataForWritingSystem>();
            var scheduleEcsTestData2ForReadingSystemRef = World.CreateSystem<ScheduleEcsTestData2ForReadingSystem>();
            scheduleEcsTestDataForWritingSystemRef.Update(World.Unmanaged);
            scheduleEcsTestData2ForReadingSystemRef.Update(World.Unmanaged);

            // Can't read from EcsTestData as ScheduleEcsTestDataForWritingSystem is writing to it in a job
            Assert.Throws<InvalidOperationException>(() =>
            {
                var data = data1FromEntity[e];
            });
            // Can't write to EcsTestData2 as ScheduleEcsTestData2ForReadingSystem is reading from it in a job
            Assert.Throws<InvalidOperationException>(() =>
            {
                data2FromEntity[e] = new EcsTestData2();
            });

            if (withComplete)
            {
                m_Manager.CompleteDependencyBeforeRW<EcsTestData2>();
                data2FromEntity[e] = new EcsTestData2();
            }

            // Should still not be allowed as CompleteDependencyBeforeRW should only have completed one system.
            Assert.Throws<InvalidOperationException>(() =>
            {
                var data = data1FromEntity[e];
            });
        }

        partial struct SystemWithSystemEntityData : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponentData(state.SystemHandle, new EcsTestData());
                state.EntityManager.AddComponentData(state.SystemHandle, new EcsTestData2());
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public unsafe void GetComponentDataRW_Completes_BothDependencies()
        {
            var systemWithEntityData = World.CreateSystem<SystemWithSystemEntityData>();

            var scheduleEcsTestDataForWritingSystem = World.CreateSystem<ScheduleEcsTestDataForWritingSystem>();
            var scheduleEcsTestData2ForReadingSystem = World.CreateSystem<ScheduleEcsTestData2ForReadingSystem>();
            scheduleEcsTestDataForWritingSystem.Update(World.Unmanaged);
            scheduleEcsTestData2ForReadingSystem.Update(World.Unmanaged);

            World.EntityManager.GetComponentDataRW<EcsTestData>(systemWithEntityData);
            World.EntityManager.GetComponentDataRW<EcsTestData2>(systemWithEntityData);

            Assert.IsTrue(JobHandle.CheckFenceIsDependencyOrDidSyncFence(World.Unmanaged.ResolveSystemStateRef(scheduleEcsTestDataForWritingSystem).Dependency, default));
            Assert.IsTrue(JobHandle.CheckFenceIsDependencyOrDidSyncFence(World.Unmanaged.ResolveSystemStateRef(scheduleEcsTestData2ForReadingSystem).Dependency, default));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public unsafe void GetComponentData_Completes_OnlyWrite_Dependency()
        {
            var systemWithEntityData = World.CreateSystem<SystemWithSystemEntityData>();

            var scheduleEcsTestDataForWritingSystem = World.CreateSystem<ScheduleEcsTestDataForWritingSystem>();
            var scheduleEcsTestData2ForReadingSystem = World.CreateSystem<ScheduleEcsTestData2ForReadingSystem>();
            scheduleEcsTestDataForWritingSystem.Update(World.Unmanaged);
            scheduleEcsTestData2ForReadingSystem.Update(World.Unmanaged);

            World.EntityManager.GetComponentData<EcsTestData>(systemWithEntityData);
            World.EntityManager.GetComponentData<EcsTestData2>(systemWithEntityData);

            Assert.IsTrue(JobHandle.CheckFenceIsDependencyOrDidSyncFence(World.Unmanaged.ResolveSystemStateRef(scheduleEcsTestDataForWritingSystem).Dependency, default));
            Assert.IsFalse(JobHandle.CheckFenceIsDependencyOrDidSyncFence(World.Unmanaged.ResolveSystemStateRef(scheduleEcsTestData2ForReadingSystem).Dependency, default));
        }

#endif // !DOTS_DISABLE_DEBUG_NAMES

#if DOTS_DISABLE_DEBUG_NAMES
        public void EntityName_Disable_GetName_SetName_Managed()
        {
            var entity = m_Manager.CreateEntity();
            Assert.AreEqual("", m_Manager.GetName(entity));
            var name = "Test entity";
            m_Manager.SetName(entity,name);
            Assert.AreEqual("", m_Manager.GetName(entity));
        }

        public void EntityName_Disable_GetName_SetName_Fixed()
        {
            var entity = m_Manager.CreateEntity();
            var name = new FixedString64Bytes("Test entity");
            m_Manager.SetName(entity,name);
            m_Manager.GetName(entity,out var newName);
            Assert.AreEqual(new FixedString64Bytes(), newName);
        }
#endif
#endif
    }
}
