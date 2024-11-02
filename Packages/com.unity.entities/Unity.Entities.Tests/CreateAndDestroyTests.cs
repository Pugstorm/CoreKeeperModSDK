using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections.NotBurstCompatible;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests
{
    class CreateAndDestroyTests : ECSTestsFixture
    {
        [Test]
        unsafe public void CreateAndDestroyOne()
        {
            var entity = CreateEntityWithDefaultData(10);
            m_Manager.DestroyEntity(entity);
            AssertDoesNotExist(entity);
        }

        [Test]
        unsafe public void DestroyNullIsIgnored()
        {
            m_Manager.DestroyEntity(default(Entity));
        }

        [Test]
        unsafe public void DestroyTwiceIsIgnored()
        {
            var entity = CreateEntityWithDefaultData(10);
            m_Manager.DestroyEntity(entity);
            m_Manager.DestroyEntity(entity);
        }

        [Test]
        unsafe public void EmptyEntityIsNull()
        {
            CreateEntityWithDefaultData(10);
            Assert.IsFalse(m_Manager.Exists(new Entity()));
        }

        [Test]
        unsafe public void CreateAndDestroyTwo()
        {
            var entity0 = CreateEntityWithDefaultData(10);
            var entity1 = CreateEntityWithDefaultData(11);

            m_Manager.DestroyEntity(entity0);

            AssertDoesNotExist(entity0);
            AssertComponentData(entity1, 11);

            m_Manager.DestroyEntity(entity1);
            AssertDoesNotExist(entity0);
            AssertDoesNotExist(entity1);
        }

        [TestCaseGeneric(typeof(EcsTestData))]
        [TestCaseGeneric(typeof(EcsCleanup1))]
        unsafe public void CreateZeroEntities<TComponent>()
            where TComponent : unmanaged, IComponentData
        {
            var array = new NativeArray<Entity>(0, Allocator.Temp);
            m_Manager.CreateEntity(m_Manager.CreateArchetype(typeof(TComponent)), array);
            array.Dispose();
        }

        [Test]
        unsafe public void InstantiateZeroEntities()
        {
            var array = new NativeArray<Entity>(0, Allocator.Temp);

            var srcEntity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.Instantiate(srcEntity , array);
            array.Dispose();
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void Instantiate_NegativeCount_Throws()
        {
            var prefab = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var ent = m_Manager.Instantiate(prefab, -1, Allocator.Persistent);
            });
        }

        [Test]
        public void CreateZeroEntities_NoArray()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var entities = m_Manager.GetAllEntities();
            Assert.AreEqual(0, entities.Length);
            entities.Dispose();

            m_Manager.CreateEntity(archetype, 0);

            entities = m_Manager.GetAllEntities();
            Assert.AreEqual(0, entities.Length);
            entities.Dispose();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void CreateEntity_NegativeCount_Throws()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            Assert.Throws<ArgumentOutOfRangeException>(() => m_Manager.CreateEntity(archetype, -1));
        }

        [Test]
        public void CreateMultipleEntities_NoArray()
        {
            var entities = m_Manager.GetAllEntities();
            Assert.AreEqual(0, entities.Length);
            entities.Dispose();

            var types = new ComponentType[] {typeof(EcsTestData), typeof(EcsTestData2)};
            var archetype = m_Manager.CreateArchetype(types);

            m_Manager.CreateEntity(archetype, 10);

            entities = m_Manager.GetAllEntities();
            Assert.AreEqual(10, entities.Length);

            var query = m_Manager.CreateEntityQuery(types);
            Assert.AreEqual(10, query.CalculateEntityCount());

            entities.Dispose();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires NativeArray allocator checks which are guarded by ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void CreateEntity_AllocatorNoneOrInvalidThrows()
        {
            var types = new ComponentType[] {typeof(EcsTestData), typeof(EcsTestData2)};
            var archetype = m_Manager.CreateArchetype(types);

            Assert.Throws<ArgumentException>(() => m_Manager.CreateEntity(archetype, 10, Allocator.None));
            Assert.Throws<ArgumentException>(() => m_Manager.CreateEntity(archetype, 10, Allocator.Invalid));
        }

        [Test]
        unsafe public void CreateAndDestroyThree()
        {
            var entity0 = CreateEntityWithDefaultData(10);
            var entity1 = CreateEntityWithDefaultData(11);

            m_Manager.DestroyEntity(entity0);

            var entity2 = CreateEntityWithDefaultData(12);


            AssertDoesNotExist(entity0);

            AssertComponentData(entity1, 11);
            AssertComponentData(entity2, 12);
        }

        [Test]
        [Ignore("TODO(DOTS-5601): optimize performance and add checks that ensure memory consumption is in reasonable range")]
        public void CreateEntity_CreateAlmostTooManyEntities()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var capacity = archetype.ChunkCapacity;
            var almostTooManyEntities = ((EntityComponentStore.k_MaximumEntitiesPerWorld / capacity) * capacity) - 1;
            Assert.IsTrue(almostTooManyEntities == (int) almostTooManyEntities);
            Assert.DoesNotThrow(() => m_Manager.CreateEntity(archetype, (int)almostTooManyEntities));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void CreateEntity_InvalidEntityArchetypeThrows()
        {
            var archetype = new EntityArchetype();
            Assert.Throws<ArgumentException>(() => m_Manager.CreateEntity(archetype));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires NativeArray allocator checks which are guarded by ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void CreateEntity_PassArray_InvalidEntityArchetypeThrows()
        {
            var arr = new NativeArray<Entity>(10, Allocator.Temp);
            var archetype = new EntityArchetype();
            Assert.Throws<ArgumentException>(() => m_Manager.CreateEntity(archetype, arr));
            arr.Dispose();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires NativeArray allocator checks which are guarded by ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void CreateEntity_ReturnArray_InvalidEntityArchetypeThrows()
        {
            var archetype = new EntityArchetype();
            Assert.Throws<ArgumentException>(() => m_Manager.CreateEntity(archetype, 10, Allocator.Temp));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Requires entity data access safety checks")]
        public void CreateEntity_NoArray_InvalidEntityArchetypeThrows()
        {
            var archetype = new EntityArchetype();
            Assert.Throws<ArgumentException>(() => m_Manager.CreateEntity(archetype, 10));
        }

        [Test]
        [ConditionalIgnore("IgnoreForCoverage", "Fails randonly when ran with code coverage enabled")]
        unsafe public void CreateAndDestroyStressTest()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var entities = new NativeArray<Entity>(10000, Allocator.Persistent);

            m_Manager.CreateEntity(archetype, entities);

            for (int i = 0; i < entities.Length; i++)
                AssertComponentData(entities[i], 0);

            m_Manager.DestroyEntity(entities);
            entities.Dispose();
        }

        [Test]
        unsafe public void CreateAndDestroyShuffleStressTest()
        {
            Entity[] entities = new Entity[10000];
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i] = CreateEntityWithDefaultData(i);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                if (i % 2 == 0)
                    m_Manager.DestroyEntity(entities[i]);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                if (i % 2 == 0)
                {
                    AssertDoesNotExist(entities[i]);
                }
                else
                {
                    AssertComponentData(entities[i], i);
                }
            }

            for (int i = 0; i < entities.Length; i++)
            {
                if (i % 2 == 1)
                    m_Manager.DestroyEntity(entities[i]);
            }

            for (int i = 0; i < entities.Length; i++)
            {
                AssertDoesNotExist(entities[i]);
            }
        }

        [Test]
        unsafe public void InstantiateStressTest()
        {
            var entities = new NativeArray<Entity>(10000, Allocator.Persistent);
            var srcEntity = CreateEntityWithDefaultData(5);

            m_Manager.Instantiate(srcEntity, entities);

            for (int i = 0; i < entities.Length; i++)
                AssertComponentData(entities[i], 5);

            m_Manager.DestroyEntity(entities);
            entities.Dispose();
        }

        [Test]
        public void AddRemoveComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var entity = m_Manager.CreateEntity(archetype);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entity));

            m_Manager.AddComponentData<EcsTestData3>(entity, new EcsTestData3(3));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData3>(entity));

            Assert.AreEqual(3, m_Manager.GetComponentData<EcsTestData3>(entity).value0);
            Assert.AreEqual(3, m_Manager.GetComponentData<EcsTestData3>(entity).value1);
            Assert.AreEqual(3, m_Manager.GetComponentData<EcsTestData3>(entity).value2);

            m_Manager.RemoveComponent<EcsTestData2>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData3>(entity));

            Assert.AreEqual(3, m_Manager.GetComponentData<EcsTestData3>(entity).value0);
            Assert.AreEqual(3, m_Manager.GetComponentData<EcsTestData3>(entity).value1);
            Assert.AreEqual(3, m_Manager.GetComponentData<EcsTestData3>(entity).value2);
        }

        [Test]
        public void AddRemoveComponent_FailForInt()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var entity = m_Manager.CreateEntity(archetype);

            Assert.That(() => { m_Manager.AddComponent<int>(entity); },
                Throws.ArgumentException.With.Message.Contains("All ComponentType must be known at compile time."));
            Assert.That(() => { m_Manager.RemoveComponent<int>(entity); },
                Throws.ArgumentException.With.Message.Contains("All ComponentType must be known at compile time."));
            Assert.That(() => { m_Manager.AddComponent(entity, typeof(int)); },
                Throws.ArgumentException.With.Message.Contains("All ComponentType must be known at compile time."));
            Assert.That(() => { m_Manager.RemoveComponent(entity, typeof(int)); },
                Throws.ArgumentException.With.Message.Contains("All ComponentType must be known at compile time."));
        }

        [Test]
        public void AddRemoveComponent_FailForNonIComponentData()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var entity = m_Manager.CreateEntity(archetype);

            Assert.That(() => { m_Manager.AddComponent<EcsTestNonComponent>(entity); },
                Throws.ArgumentException.With.Message.Contains("All ComponentType must be known at compile time."));
            Assert.That(() => { m_Manager.RemoveComponent<EcsTestNonComponent>(entity); },
                Throws.ArgumentException.With.Message.Contains("All ComponentType must be known at compile time."));
            Assert.That(() => { m_Manager.AddComponent(entity, typeof(EcsTestNonComponent)); },
                Throws.ArgumentException.With.Message.Contains("All ComponentType must be known at compile time."));
            Assert.That(() => { m_Manager.RemoveComponent(entity, typeof(EcsTestNonComponent)); },
                Throws.ArgumentException.With.Message.Contains("All ComponentType must be known at compile time."));
        }

        [Test]
        public void AddRemoveSharedComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var entity = m_Manager.CreateEntity(archetype);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestSharedComp>(entity));

            m_Manager.AddComponent<EcsTestSharedComp>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestSharedComp>(entity));

            Assert.AreEqual(0, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity).value);

            m_Manager.RemoveComponent<EcsTestData2>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestSharedComp>(entity));

            Assert.AreEqual(0, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity).value);

            m_Manager.RemoveComponent<EcsTestSharedComp>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestSharedComp>(entity));
        }

        [Test]
        public void AddRemoveBufferComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var entity = m_Manager.CreateEntity(archetype);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsIntElement>(entity));

            m_Manager.AddComponent<EcsIntElement>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsIntElement>(entity));

            Assert.AreEqual(0, m_Manager.GetBuffer<EcsIntElement>(entity).Length);

            m_Manager.RemoveComponent<EcsTestData2>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsIntElement>(entity));

            Assert.AreEqual(0, m_Manager.GetBuffer<EcsIntElement>(entity).Length);

            m_Manager.RemoveComponent<EcsIntElement>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsIntElement>(entity));
        }

        [Test]
        public void AddRemoveCleanupComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var entity = m_Manager.CreateEntity(archetype);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsCleanup1>(entity));

            m_Manager.AddComponent<EcsCleanup1>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsCleanup1>(entity));

            Assert.AreEqual(0, m_Manager.GetComponentData<EcsCleanup1>(entity).Value);

            m_Manager.RemoveComponent<EcsTestData2>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsCleanup1>(entity));

            Assert.AreEqual(0, m_Manager.GetComponentData<EcsCleanup1>(entity).Value);

            m_Manager.RemoveComponent<EcsCleanup1>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsCleanup1>(entity));
        }

        [Test]
        public void AddRemoveCleanupSharedComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var entity = m_Manager.CreateEntity(archetype);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsCleanupShared1>(entity));

            m_Manager.AddComponent<EcsCleanupShared1>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsCleanupShared1>(entity));

            Assert.AreEqual(0, m_Manager.GetSharedComponentManaged<EcsCleanupShared1>(entity).Value);

            m_Manager.RemoveComponent<EcsTestData2>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsCleanupShared1>(entity));

            Assert.AreEqual(0, m_Manager.GetSharedComponentManaged<EcsCleanupShared1>(entity).Value);

            m_Manager.RemoveComponent<EcsCleanupShared1>(entity);
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsCleanupShared1>(entity));
        }

        [Test]
        public void RemoveMultipleComponents()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4));
            var entity = m_Manager.CreateEntity(archetype);

            m_Manager.RemoveComponent(entity, new ComponentTypeSet(typeof(EcsTestData2), typeof(EcsTestData4)));
            Assert.AreEqual(m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData3)), m_Manager.GetChunk(entity).Archetype);
            m_Manager.DestroyEntity(entity);
        }

        [Test]
        public void RemoveMultipleComponents_EmptyComponentTypes()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4));
            var entity = m_Manager.CreateEntity(archetype);

            m_Manager.RemoveComponent(entity, new ComponentTypeSet());
            Assert.AreEqual(archetype, m_Manager.GetChunk(entity).Archetype);
            m_Manager.DestroyEntity(entity);
        }

        [Test]
        public void RemoveMultipleComponents_InvalidTarget_DoesNotThrow()
        {
            var entity = Entity.Null;
            Assert.DoesNotThrow((() => m_Manager.RemoveComponent(entity, new ComponentTypeSet())));
        }

        [Test]
        public void RemoveMultipleComponents_ZeroComponentsPresent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3));
            var entity = m_Manager.CreateEntity(archetype);

            m_Manager.RemoveComponent(entity, new ComponentTypeSet(typeof(EcsTestTag), typeof(EcsTestData4)));
            Assert.AreEqual(archetype, m_Manager.GetChunk(entity).Archetype);
            m_Manager.DestroyEntity(entity);
        }

        [Test]
        public void RemoveMultipleComponents_SharedComponentValuesPreserved()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(SharedData1), typeof(SharedData2));
            var entity = m_Manager.CreateEntity(archetype);
            m_Manager.SetSharedComponentManaged(entity, new SharedData1() { value = 5});
            m_Manager.SetSharedComponentManaged(entity, new SharedData2() { value = 9});

            m_Manager.RemoveComponent(entity, new ComponentTypeSet(typeof(EcsTestData2), typeof(EcsTestData4)));
            Assert.AreEqual(m_Manager.CreateArchetype(typeof(EcsTestData), typeof(SharedData1), typeof(SharedData2)),
                m_Manager.GetChunk(entity).Archetype);
            Assert.AreEqual(5, m_Manager.GetSharedComponentManaged<SharedData1>(entity).value);
            Assert.AreEqual(9, m_Manager.GetSharedComponentManaged<SharedData2>(entity).value);
            m_Manager.DestroyEntity(entity);
        }

        [Test]
        public void AddComponentSetsValueOfComponentToDefault()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var dummyEntity = m_Manager.CreateEntity(archetype);
            m_Manager.Debug.PoisonUnusedDataInAllChunks(archetype, 0xCD);

            var entity = m_Manager.CreateEntity();
            m_Manager.AddComponent(entity, ComponentType.ReadWrite<EcsTestData>());
            Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData>(entity).value);

            m_Manager.DestroyEntity(dummyEntity);
            m_Manager.DestroyEntity(entity);
        }

        [Test]
        public void ReadOnlyAndNonReadOnlyArchetypeAreEqual()
        {
            var arch = m_Manager.CreateArchetype(ComponentType.ReadOnly(typeof(EcsTestData)));
            var arch2 = m_Manager.CreateArchetype(typeof(EcsTestData));
            Assert.AreEqual(arch, arch2);
        }

        [Test]
        public void ExcludeArchetypeReactToAddRemoveComponent()
        {
            var subtractiveArch = m_Manager.CreateEntityQuery(ComponentType.Exclude(typeof(EcsTestData)), typeof(EcsTestData2));

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var entity = m_Manager.CreateEntity(archetype);
            Assert.AreEqual(0, subtractiveArch.CalculateEntityCount());

            m_Manager.RemoveComponent<EcsTestData>(entity);
            Assert.AreEqual(1, subtractiveArch.CalculateEntityCount());

            m_Manager.AddComponentData<EcsTestData>(entity, new EcsTestData());
            Assert.AreEqual(0, subtractiveArch.CalculateEntityCount());
        }

        [Test]
        public void ChunkCountsAreCorrect()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entity = m_Manager.CreateEntity(archetype);

            Assert.AreEqual(1, archetype.ChunkCount);

            m_Manager.AddComponent(entity, typeof(EcsTestData2));
            Assert.AreEqual(0, archetype.ChunkCount);

            unsafe {
                Assert.IsTrue(archetype.Archetype->Chunks.Count == 0);
                Assert.AreEqual(0, archetype.Archetype->EntityCount);

                var archetype2 = m_Manager.GetCheckedEntityDataAccess()->EntityComponentStore->GetArchetype(entity);
                Assert.AreEqual(1, archetype2->Chunks.Count);
                Assert.AreEqual(1, archetype2->EntityCount);
            }
        }

        [Test]
        public void AddComponentsWorks()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entity = m_Manager.CreateEntity(archetype);

            // Create dummy archetype with unrelated type to override the internal cache in the entity manager
            // This exposed a bug in AddComponent
            m_Manager.CreateArchetype(typeof(EcsTestData4));

            var typesToAdd = new ComponentTypeSet(new ComponentType[] {typeof(EcsTestData3), typeof(EcsTestData2)});
            m_Manager.AddComponent(entity, typesToAdd);

            var expectedTotalTypes = new ComponentTypeSet(new ComponentType[] {typeof(Simulate), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData)});
            var actualTotalTypes = m_Manager.GetComponentTypes(entity);

            Assert.AreEqual(expectedTotalTypes.Length, actualTotalTypes.Length);
            for (var i = 0; i < expectedTotalTypes.Length; ++i)
                Assert.AreEqual(expectedTotalTypes.GetTypeIndex(i), actualTotalTypes[i].TypeIndex);

            actualTotalTypes.Dispose();
        }

        [Test]
        public unsafe void AddComponent_Multiple_WithUnmanagedComponentTypeSetConstructor_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entity = m_Manager.CreateEntity(archetype);

            // Create dummy archetype with unrelated type to override the internal cache in the entity manager
            // This exposed a bug in AddComponent
            m_Manager.CreateArchetype(typeof(EcsTestData4));

            var typesToAdd = new ComponentTypeSet(new FixedList128Bytes<ComponentType>
                {typeof(EcsTestData3), typeof(EcsTestData2)});
            m_Manager.AddComponent(entity, typesToAdd);

            var expectedTotalTypes = new ComponentTypeSet(new ComponentType[] {typeof(Simulate), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData)});
            var actualTotalTypes = m_Manager.GetComponentTypes(entity);

            Assert.AreEqual(expectedTotalTypes.Length, actualTotalTypes.Length);
            for (var i = 0; i < expectedTotalTypes.Length; ++i)
                Assert.AreEqual(expectedTotalTypes.GetTypeIndex(i), actualTotalTypes[i].TypeIndex);

            actualTotalTypes.Dispose();
        }

        [Test]
        public void GetAllEntitiesCorrectCount()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entity = m_Manager.CreateEntity(archetype0);

            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var moreEntities = new NativeArray<Entity>(1024, Allocator.Temp);
            m_Manager.CreateEntity(archetype1, moreEntities);

            var foundEntities = m_Manager.GetAllEntities();
            Assert.AreEqual(1024 + 1, foundEntities.Length);

            foundEntities.Dispose();
            moreEntities.Dispose();
        }

        [Test]
        public void GetAllEntitiesCorrectValues()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entity = m_Manager.CreateEntity(archetype0);
            m_Manager.SetComponentData(entity, new EcsTestData { value = 1000000});

            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var moreEntities = new NativeArray<Entity>(1024, Allocator.Temp);
            m_Manager.CreateEntity(archetype1, moreEntities);
            for (int i = 0; i < 1024; i++)
            {
                m_Manager.SetComponentData(moreEntities[i], new EcsTestData { value = i + 1});
            }

            var foundEntities = m_Manager.GetAllEntities();

            Assert.AreEqual(1025, foundEntities.Length);

            var sum = 0;
            var expectedSum = 1524800;
            for (int i = 0; i < 1025; i++)
            {
                sum += m_Manager.GetComponentData<EcsTestData>(foundEntities[i]).value;
            }

            Assert.AreEqual(expectedSum, sum);

            foundEntities.Dispose();
            moreEntities.Dispose();
        }

        [Test]
        public void AddComponent_WithTypeIndices_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entity = m_Manager.CreateEntity(archetype);

            var typesToAdd = new ComponentTypeSet(typeof(EcsTestData3), typeof(EcsTestData2));

            m_Manager.AddComponent(entity, typesToAdd);

            var expectedTotalTypes = new ComponentTypeSet(typeof(Simulate), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData));

            var actualTotalTypes = m_Manager.GetComponentTypes(entity);

            Assert.AreEqual(expectedTotalTypes.Length, actualTotalTypes.Length);
            for (var i = 0; i < expectedTotalTypes.Length; ++i)
                Assert.AreEqual(expectedTotalTypes.GetTypeIndex(i), actualTotalTypes[i].TypeIndex);

            actualTotalTypes.Dispose();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void AddComponent_InvalidEntity1_ShouldThrow()
        {
            var invalidEnt = m_Manager.CreateEntity();
            m_Manager.DestroyEntity(invalidEnt);
            Assert.That(() => { m_Manager.AddComponent<EcsTestData>(invalidEnt); },
                Throws.InvalidOperationException.With.Message.Contains("entity does not exist"));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void AddComponents_InvalidEntity1_ShouldThrow()
        {
            var entity = Entity.Null;
            Assert.Throws<InvalidOperationException>((() => m_Manager.AddComponent(entity, new ComponentTypeSet())));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void AddComponentBatched_InvalidEntities_ShouldThrow([Values(10, 100)] int entityCount)
        {
            var invalidEnt = m_Manager.CreateEntity();
            m_Manager.DestroyEntity(invalidEnt);

            for (int i = 0; i < entityCount; ++i)
            {
                m_Manager.CreateEntity();
            }

            var entities = m_Manager.UniversalQuery.ToEntityArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(entities.Length, entityCount);

            entities[0] = invalidEnt;
            Assert.That(() => { m_Manager.AddComponent<EcsTestData>(entities); },
                Throws.ArgumentException.With.Message.Contains("An EntityManager command is operating on an invalid entity."));
        }

        [Test]
        public void AddComponent_WithSharedComponents_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            var entity = m_Manager.CreateEntity(archetype);

            var sharedComponentValue = new EcsTestSharedComp(1337);
            m_Manager.SetSharedComponentManaged(entity, sharedComponentValue);

            var typesToAdd = new ComponentTypeSet(typeof(EcsTestData3), typeof(EcsTestSharedComp2));

            m_Manager.AddComponent(entity, typesToAdd);

            Assert.AreEqual(m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity), sharedComponentValue);

            var expectedTotalTypes = new ComponentTypeSet(typeof(Simulate), typeof(EcsTestData),
                typeof(EcsTestData3), typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2));

            var actualTotalTypes = m_Manager.GetComponentTypes(entity);

            Assert.AreEqual(expectedTotalTypes.Length, actualTotalTypes.Length);
            for (var i = 0; i < expectedTotalTypes.Length; ++i)
                Assert.AreEqual(expectedTotalTypes.GetTypeIndex(i), actualTotalTypes[i].TypeIndex);

            actualTotalTypes.Dispose();
        }

        [Test]
        public void InstantiateWithCleanupComponent()
        {
            for (int i = 0; i < 1000; ++i)
            {
                var src = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsCleanup1), typeof(EcsTestData2));

                m_Manager.SetComponentData(src, new EcsTestData {value = i * 123});
                m_Manager.SetComponentData(src, new EcsTestData2 {value0 = i * 456, value1 = i * 789});

                var dst = m_Manager.Instantiate(src);

                Assert.AreEqual(i * 123, m_Manager.GetComponentData<EcsTestData>(dst).value);
                Assert.AreEqual(i * 456, m_Manager.GetComponentData<EcsTestData2>(dst).value0);
                Assert.AreEqual(i * 789, m_Manager.GetComponentData<EcsTestData2>(dst).value1);

                Assert.IsFalse(m_Manager.HasComponent<EcsCleanup1>(dst));
            }
        }

        [TypeManager.ForcedMemoryOrderingAttribute(2)]
        struct EcsSharedForcedOrder : ISharedComponentData
        {
            public int Value;

            public EcsSharedForcedOrder(int value)
            {
                Value = value;
            }
        }

        [TypeManager.ForcedMemoryOrderingAttribute(1)]
        struct EcsCleanupSharedForcedOrder : ICleanupSharedComponentData
        {
            public int Value;

            public EcsCleanupSharedForcedOrder(int value)
            {
                Value = value;
            }
        }

        [Test]
        public void InstantiateWithCleanupSharedComponent()
        {
            var srcEntity = m_Manager.CreateEntity();

            var sharedValue = new EcsSharedForcedOrder(123);
            var systemValue = new EcsCleanupSharedForcedOrder(234);

            m_Manager.AddSharedComponentManaged(srcEntity, sharedValue);
            m_Manager.AddSharedComponentManaged(srcEntity, systemValue);

            var versionSharedBefore = m_Manager.GetSharedComponentOrderVersion(sharedValue);
            var versionSystemBefore = m_Manager.GetSharedComponentOrderVersion(systemValue);

            var dstEntity = m_Manager.Instantiate(srcEntity);
            var sharedValueCopied = m_Manager.GetSharedComponentManaged<EcsSharedForcedOrder>(dstEntity);

            var versionSharedAfter = m_Manager.GetSharedComponentOrderVersion(sharedValue);
            var versionSystemAfter = m_Manager.GetSharedComponentOrderVersion(systemValue);

            Assert.IsTrue(m_Manager.HasComponent<EcsSharedForcedOrder>(dstEntity));
            Assert.IsFalse(m_Manager.HasComponent<EcsCleanupSharedForcedOrder>(dstEntity));

            Assert.AreEqual(sharedValue, sharedValueCopied);

            Assert.AreNotEqual(versionSharedBefore, versionSharedAfter);
            Assert.AreEqual(versionSystemBefore, versionSystemAfter);
        }

        [Test]
        public void AddTagComponentTwiceByValue()
        {
            var entity = m_Manager.CreateEntity();

            var added0 = m_Manager.AddComponentData(entity, new EcsTestTag());
            var added1 = m_Manager.AddComponentData(entity, new EcsTestTag());

            Assert.That(added0, Is.True);
            Assert.That(added1, Is.False);
        }

        [Test]
        public void AddTagComponentTwiceByType()
        {
            var entity = m_Manager.CreateEntity();

            var added0 = m_Manager.AddComponent(entity, ComponentType.ReadWrite<EcsTestTag>());
            var added1 = m_Manager.AddComponent(entity, ComponentType.ReadWrite<EcsTestTag>());

            Assert.That(added0, Is.True);
            Assert.That(added1, Is.False);
        }

        [Test]
        public void AddTagComponentTwiceToGroup()
        {
            m_Manager.CreateEntity();

            m_Manager.AddComponent(m_Manager.UniversalQuery, ComponentType.ReadWrite<EcsTestTag>());

            // Not an error (null operation)
            m_Manager.AddComponent(m_Manager.UniversalQuery, ComponentType.ReadWrite<EcsTestTag>());
        }

        [Test]
        public void AddComponentThenSetThenAddDoesNotChangeValue()
        {
            m_Manager.CreateEntity();

            var value0 = new EcsTestData(1);
            m_Manager.AddComponent(m_Manager.UniversalQuery, ComponentType.ReadWrite<EcsTestData>());
            var entities = m_Manager.UniversalQuery.ToEntityArray(Allocator.Persistent);
            for (int i = 0; i < entities.Length; i++)
                m_Manager.SetComponentData(entities[i], value0);

            var addedQuery = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            m_Manager.AddComponent(addedQuery, ComponentType.ReadWrite<EcsTestData>());
            var values = addedQuery.ToComponentDataArray<EcsTestData>(Allocator.Persistent);
            for (int i = 0; i < entities.Length; i++)
                Assert.AreEqual(value0.value, values[i].value);

            addedQuery.Dispose();
            entities.Dispose();
            values.Dispose();
        }

        [Test]
        public void AddComponentTwiceByTypeArray()
        {
            var entity = m_Manager.CreateEntity();

            m_Manager.AddComponent(entity, new ComponentTypeSet(ComponentType.ReadWrite<EcsTestTag>()));
            var archetypeBefore = m_Manager.GetChunk(entity).Archetype;
            m_Manager.AddComponent(entity, new ComponentTypeSet(ComponentType.ReadWrite<EcsTestTag>()));
            var archetypeAfter = m_Manager.GetChunk(entity).Archetype;
            Assert.AreEqual(archetypeBefore, archetypeAfter);

            m_Manager.AddComponent(entity, new ComponentTypeSet(ComponentType.ReadWrite<EcsTestData>()));
            archetypeBefore = m_Manager.GetChunk(entity).Archetype;
            m_Manager.AddComponent(entity, new ComponentTypeSet(ComponentType.ReadWrite<EcsTestData>()));
            archetypeAfter = m_Manager.GetChunk(entity).Archetype;
            Assert.AreEqual(archetypeBefore, archetypeAfter);
        }

        [Test]
        public void AddChunkComponentTwice()
        {
            var entity = m_Manager.CreateEntity();

            var added0 = m_Manager.AddChunkComponentData<EcsTestTag>(entity);
            var added1 = m_Manager.AddChunkComponentData<EcsTestTag>(entity);

            var added2 = m_Manager.AddChunkComponentData<EcsTestData>(entity);
            var added3 = m_Manager.AddChunkComponentData<EcsTestData>(entity);

            Assert.That(added0, Is.True);
            Assert.That(added1, Is.False);
            Assert.That(added2, Is.True);
            Assert.That(added3, Is.False);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void AddChunkComponent_Works()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData2));

            // Adding a chunk component through an entity should be visible through both ArchetypeChunk and EntityManager interfaces
            Assert.IsTrue(m_Manager.AddChunkComponentData<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasChunkComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.GetChunk(entity).HasChunkComponent<EcsTestData>());
            // Setting the chunk component value through EntityManager should be visible through both chunk and EntityManager interfaces
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(entity), new EcsTestData { value = 17 });
            Assert.AreEqual(17, m_Manager.GetChunkComponentData<EcsTestData>(entity).value);
            Assert.AreEqual(17, m_Manager.GetChunkComponentData<EcsTestData>(m_Manager.GetChunk(entity)).value);
            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
            Assert.AreEqual(17, m_Manager.GetChunk(entity).GetChunkComponentData(ref typeHandle).value);

            // Adding a second chunk component through a query should be visible through both the ArchetypeChunk and EntityManager interfaces
            var chunk2 = m_Manager.GetChunk(entity);
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData2>().Build(m_Manager);
            m_Manager.AddChunkComponentData(query, new EcsTestDataEnableable{value=23});
            Assert.IsTrue(m_Manager.HasChunkComponent<EcsTestDataEnableable>(entity));
            Assert.IsTrue(m_Manager.GetChunk(entity).HasChunkComponent<EcsTestDataEnableable>());
            Assert.AreEqual(23, m_Manager.GetChunkComponentData<EcsTestDataEnableable>(entity).value);
            Assert.AreEqual(23, m_Manager.GetChunkComponentData<EcsTestDataEnableable>(m_Manager.GetChunk(entity)).value);
            var typeHandle2 = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            Assert.AreEqual(23, m_Manager.GetChunk(entity).GetChunkComponentData(ref typeHandle2).value);

            // The first chunk component is still present, and its value is preserved in this case, because the adding
            // the second chunk component didn't change the entity's chunk.
            Assert.AreEqual(chunk2.m_Chunk, m_Manager.GetChunk(entity).m_Chunk);
            Assert.IsTrue(m_Manager.HasChunkComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.GetChunk(entity).HasChunkComponent<EcsTestData>());
            Assert.AreEqual(17, m_Manager.GetChunkComponentData<EcsTestData>(entity).value);
            Assert.AreEqual(17, m_Manager.GetChunkComponentData<EcsTestData>(m_Manager.GetChunk(entity)).value);
            var typeHandle3 = m_Manager.GetComponentTypeHandle<EcsTestData>(false); // typeHandle was invalidated by the AddChunkComponentData call
            Assert.AreEqual(17, m_Manager.GetChunk(entity).GetChunkComponentData(ref typeHandle3).value);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void AddChunkComponent_AlreadyExists_Works()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData2));
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData2>().Build(m_Manager);

            m_Manager.AddComponent<EcsTestTag>(query);
            // Adding a tag component that already exists is not an error
            Assert.DoesNotThrow(() => m_Manager.AddComponent<EcsTestTag>(query));

            m_Manager.AddComponentData(entity, new EcsTestData{value = 17});
            // Adding a component that already exists is not an error
            Assert.DoesNotThrow(() => m_Manager.AddComponent<EcsTestData>(query));
            // Adding a component value when the component already exists is not an error; it just acts like SetComponentData
            Assert.DoesNotThrow(() => m_Manager.AddComponentData(entity, new EcsTestData { value = 23 }));
            Assert.AreEqual(23, m_Manager.GetComponentData<EcsTestData>(entity).value);

            // ...same for shared components.
            m_Manager.AddSharedComponent(query, new EcsTestSharedComp{value=23});
            // Adding a shared component value when the component already exists is not an error; it just acts like SetSharedComponentData
            Assert.DoesNotThrow(() => m_Manager.AddSharedComponent(query, new EcsTestSharedComp{value=37}));
            Assert.AreEqual(37, m_Manager.GetSharedComponent<EcsTestSharedComp>(entity).value);

            // Everything above should be true for chunk components as well.
            m_Manager.AddChunkComponentData<AnotherEcsTestTag>(entity);
            // Adding a tag chunk component that already exists is not an error
            Assert.DoesNotThrow(() => m_Manager.AddChunkComponentData<AnotherEcsTestTag>(entity));

            m_Manager.AddChunkComponentData(query, new EcsTestFloatData {Value = 123});
            // Adding a chunk component value when the component already exists is not an error; it just acts like SetChunkComponentData
            Assert.DoesNotThrow(() => m_Manager.AddChunkComponentData(query, new EcsTestFloatData {Value = 456}));
            Assert.AreEqual(456, m_Manager.GetChunkComponentData<EcsTestFloatData>(entity).Value);
        }

        [Test]
        public void AddSharedComponentTwice()
        {
            var entity = m_Manager.CreateEntity();

            var added0 = m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp());
            var added1 = m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp());

            Assert.That(added0, Is.True);
            Assert.That(added1, Is.False);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires NativeArray allocator checks which are guarded by ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void SetArchetype_InvalidEntityArchetypeThrows()
        {
            var entity = m_Manager.CreateEntity();
            var archetype = new EntityArchetype();
            Assert.Throws<ArgumentException>(delegate { m_Manager.SetArchetype(entity, archetype); });
        }

        [Test]
        public void SetArchetypeWithSharedComponentWorks()
        {
            var entity = m_Manager.CreateEntity();

            m_Manager.AddComponentData(entity, new EcsTestData(1));
            m_Manager.AddComponentData(entity, new EcsTestData2(2));
            m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp(3));
            m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp3(4));
            var newArchetype = m_Manager.CreateArchetype(typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestSharedComp3), typeof(EcsTestSharedComp2));

            m_Manager.SetArchetype(entity, newArchetype);
            Assert.AreEqual(newArchetype, m_Manager.GetChunk(entity).Archetype);

            Assert.AreEqual(2, m_Manager.GetComponentData<EcsTestData2>(entity).value0);
            Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData3>(entity).value0);
            Assert.AreEqual(0, m_Manager.GetSharedComponentManaged<EcsTestSharedComp2>(entity).value0);
            Assert.AreEqual(4, m_Manager.GetSharedComponentManaged<EcsTestSharedComp3>(entity).value0);
        }

        [Test]
        [TestRequiresCollectionChecks("Requires NativeArray allocator checks which are guarded by ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void SetArchetypeRemovingStateComponentThrows()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsCleanup1));
            Assert.Throws<ArgumentException>(() => m_Manager.SetArchetype(entity, m_Manager.CreateArchetype(typeof(EcsTestData))));
            Assert.Throws<ArgumentException>(() => m_Manager.SetArchetype(entity, m_Manager.CreateArchetype()));
            Assert.Throws<ArgumentException>(() => m_Manager.SetArchetype(entity, m_Manager.CreateArchetype(typeof(EcsCleanupTag1))));
        }

        [Test]
        public void SetArchetypePreservingStateComponentNoThrows()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsCleanup1));
            Assert.DoesNotThrow(() => m_Manager.SetArchetype(entity, m_Manager.CreateArchetype(typeof(EcsCleanup1))));
        }

        [Test]
        public void SetArchetypeAddingStateComponent()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsCleanup1));
            var newArchetype = m_Manager.CreateArchetype(typeof(EcsCleanup1), typeof(EcsCleanupTag1));
            m_Manager.SetArchetype(entity, newArchetype);
            Assert.AreEqual(newArchetype, m_Manager.GetChunk(entity).Archetype);
        }

        [Test]
        public void AddComponentTagTwiceWithEntityArray()
        {
            var entities = CollectionHelper.CreateNativeArray<Entity>(3, World.UpdateAllocator.ToAllocator);

            entities[0] = m_Manager.CreateEntity();
            entities[1] = m_Manager.CreateEntity(typeof(EcsTestTag));
            entities[2] = m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsTestData2));

            m_Manager.AddComponent(entities, typeof(EcsTestTag));

            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(entities[0]));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(entities[1]));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(entities[2]));
        }

        [Test]
        public void AddComponentTagWithDuplicateEntities()
        {
            var entities = CollectionHelper.CreateNativeArray<Entity>(2, World.UpdateAllocator.ToAllocator);
            var e = m_Manager.CreateEntity();
            entities[0] = e;
            entities[1] = e;

            m_Manager.AddComponent(entities, typeof(EcsTestTag));

            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(e));
        }

        [Test]
        public void AddComponentTagWithMultipleDuplicateEntities()
        {
            var entities = CollectionHelper.CreateNativeArray<Entity>(5, World.UpdateAllocator.ToAllocator);
            var e1 = m_Manager.CreateEntity();
            var e2 = m_Manager.CreateEntity();
            var e3 = m_Manager.CreateEntity();

            // e1 and e2 have duplicates, e3 is unique.
            entities[0] = e1;
            entities[1] = e2;
            entities[2] = e1;
            entities[3] = e3;
            entities[4] = e2;

            m_Manager.AddComponent(entities, typeof(EcsTestTag));

            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(e1));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(e2));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(e3));
        }

        [Test]
        public void AddComponentTwiceWithEntityCommandBuffer()
        {
            using (var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var entity = ecb.CreateEntity();
                ecb.AddComponent(entity, new EcsTestTag());
                ecb.AddComponent(entity, new EcsTestTag());
                Assert.DoesNotThrow(() => ecb.Playback(m_Manager));
            }

            // without fixup

            using (var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var entity = ecb.CreateEntity();
                ecb.AddComponent(entity, new EcsTestData());
                ecb.AddComponent(entity, new EcsTestData());
                Assert.DoesNotThrow(() => ecb.Playback(m_Manager));
            }

            // with fixup
            using (var ecb = new EntityCommandBuffer(World.UpdateAllocator.ToAllocator))
            {
                var entity = ecb.CreateEntity();
                var other = ecb.CreateEntity();
                ecb.AddComponent(entity, new EcsTestDataEntity { value1 = other });
                ecb.AddComponent(entity, new EcsTestDataEntity { value1 = other });
                Assert.DoesNotThrow(() => ecb.Playback(m_Manager));
            }
        }

        [Test]
        public void AddBufferComponentTwice()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddBuffer<EcsIntElement>(entity);
            Assert.DoesNotThrow(() => m_Manager.AddBuffer<EcsIntElement>(entity));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires NativeArray allocator checks which are guarded by ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void DestroyEntityQueryWithLinkedEntityGroupPartialDestroyThrows()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestTag));
            var child = m_Manager.CreateEntity(typeof(EcsTestData2));

            var group = m_Manager.AddBuffer<LinkedEntityGroup>(entity);
            group.Add(entity);
            group.Add(child);


            var query = m_Manager.CreateEntityQuery(typeof(EcsTestTag));
            // we are destroying entity but it has a LinkedEntityGroup and child is not being destroyed. That's an error.
            Assert.Throws<ArgumentException>(() => m_Manager.DestroyEntity(query));
            // Just double checking that its a precondition & no leaking state
            Assert.Throws<ArgumentException>(() => m_Manager.DestroyEntity(query));
            Assert.AreEqual(2, m_Manager.UniversalQuery.CalculateEntityCount());

            // And after failed destroy, correct destroys do work
            m_Manager.DestroyEntity(m_Manager.UniversalQuery);
            Assert.AreEqual(0, m_Manager.UniversalQuery.CalculateEntityCount());
        }

        [Test]
        public void GetCreatedAndDestroyedEntities()
        {
            using (var state = new NativeList<int>(World.UpdateAllocator.ToAllocator))
            using (var created = new NativeList<Entity>(World.UpdateAllocator.ToAllocator))
            using (var destroyed = new NativeList<Entity>(World.UpdateAllocator.ToAllocator))
            {
                void CallCreatedAndDestroyed() =>
#pragma warning disable CS0618 // Type or member is obsolete
                    m_Manager.GetCreatedAndDestroyedEntitiesAsync(state, created, destroyed).Complete();
#pragma warning restore

                // Create e0 & e1
                var e0 = m_Manager.CreateEntity();
                var e1 = m_Manager.CreateEntity();
                CallCreatedAndDestroyed();
                Assert.AreEqual(new[] { e0, e1 }, created.ToArrayNBC());
                Assert.AreEqual(0, destroyed.Length);

                // Create e3, destroy e0
                var e3 = m_Manager.CreateEntity();
                m_Manager.DestroyEntity(e0);
                CallCreatedAndDestroyed();
                Assert.AreEqual(new[] { e3 }, created.ToArrayNBC());
                Assert.AreEqual(new[] { e0 }, destroyed.ToArrayNBC());

                // Change nothing
                CallCreatedAndDestroyed();
                Assert.AreEqual(0, created.Length);
                Assert.AreEqual(0, destroyed.Length);

                // Destroy e2 and e3
                m_Manager.DestroyEntity(e1);
                m_Manager.DestroyEntity(e3);
                CallCreatedAndDestroyed();
                Assert.AreEqual(0, created.Length);
                Assert.AreEqual(new[] { e1, e3 }, destroyed.ToArrayNBC());

                // Create e4
                var e4 = m_Manager.CreateEntity();
                CallCreatedAndDestroyed();
                Assert.AreEqual(new[] { e4 }, created.AsArray());
                Assert.AreEqual(0, destroyed.Length);

                // Create & Destroy
                m_Manager.DestroyEntity(m_Manager.CreateEntity());
                CallCreatedAndDestroyed();
                Assert.AreEqual(0, created.Length);
                Assert.AreEqual(0, destroyed.Length);
            }
        }

        [Test(Description = "Validate that EntityManager.GetCreatedAndDestroyedEntities returns only entities otherwise visible to the user through the EntityManager")]
        public void EntityManager_GetCreatedAndDestroyedEntities_Skips_ChunkHeaders()
        {
            using (var state = new NativeList<int>(World.UpdateAllocator.ToAllocator))
            using (var created = new NativeList<Entity>(World.UpdateAllocator.ToAllocator))
            using (var destroyed = new NativeList<Entity>(World.UpdateAllocator.ToAllocator))
            {
#pragma warning disable CS0618 // Type or member is obsolete
                m_Manager.GetCreatedAndDestroyedEntities(state, created, destroyed);
#pragma warning restore

                var entity0 = m_Manager.CreateEntity(typeof(BoundsComponent), ComponentType.ChunkComponent<ChunkBoundsComponent>());
                m_Manager.SetComponentData(entity0, new BoundsComponent {boundsMin = new Mathematics.float3(-10, -10, -10), boundsMax = new Mathematics.float3(0, 0, 0)});
                var entity1 = m_Manager.CreateEntity(typeof(BoundsComponent), ComponentType.ChunkComponent<ChunkBoundsComponent>());
                m_Manager.SetComponentData(entity1, new BoundsComponent {boundsMin = new Mathematics.float3(0, 0, 0), boundsMax = new Mathematics.float3(10, 10, 10)});

#pragma warning disable CS0618 // Type or member is obsolete
                m_Manager.GetCreatedAndDestroyedEntities(state, created, destroyed);
#pragma warning restore

                var metaQuery = m_Manager.CreateEntityQuery(typeof(ChunkBoundsComponent), typeof(ChunkHeader));
                var metaBoundsCount = metaQuery.CalculateEntityCount();
                var allEntities = m_Manager.GetAllEntities();

                Assert.AreEqual(1, metaBoundsCount);
                Assert.AreEqual(allEntities.Length, created.Length, "The list of created entities does not match the list returned by the EntityManager");
                Assert.AreEqual(allEntities[0], created[0]);
                Assert.AreEqual(allEntities[1], created[1]);
            }
        }

        [Test]
        public void EntityManager_GetAllEntities_WithAndWithoutMetaEntities()
        {
            var a = m_Manager.CreateEntity(ComponentType.ChunkComponent<EcsTestData>());

            using (var entities = m_Manager.GetAllEntities(Allocator.Temp))
            {
                CollectionAssert.AreEquivalent(new[] {a}, entities);
            }

            using (var entities = m_Manager.GetAllEntities(Allocator.Temp, EntityManager.GetAllEntitiesOptions.IncludeMeta))
            {
                var meta = m_Manager.Debug.GetMetaChunkEntity(a);
                CollectionAssert.AreEquivalent(new[] {a, meta}, entities);
            }
        }

        [Test]
        public void EntityManager_GetAllEntities_WithAndWithoutSystemEntities()
        {
            var a = m_Manager.CreateEntity(ComponentType.ReadWrite<SystemInstance>());
            var b = m_Manager.CreateEntity(ComponentType.ReadWrite<EcsTestData>());

            using (var entities = m_Manager.GetAllEntities(Allocator.Temp))
            {
                CollectionAssert.AreEquivalent(new[] { b }, entities);
            }

            using (var entities = m_Manager.GetAllEntities(Allocator.Temp, EntityManager.GetAllEntitiesOptions.IncludeSystems))
            {
                CollectionAssert.AreEquivalent(new[] { a, b }, entities);
            }
        }

        [Test]
        public void EntityManager_UniversalQueryVsUniversalQueryWithSystems()
        {
            var a = m_Manager.CreateEntity(ComponentType.ReadWrite<SystemInstance>());
            var b = m_Manager.CreateEntity(ComponentType.ReadWrite<EcsTestData>());

            using (var entities = m_Manager.GetAllEntities(Allocator.Temp))
            {
                using (var universalEntities = m_Manager.UniversalQuery.ToEntityArray(Allocator.Temp))
                {
                    CollectionAssert.AreEquivalent(universalEntities, entities);
                }
            }

            using (var entities = m_Manager.GetAllEntities(Allocator.Temp, EntityManager.GetAllEntitiesOptions.IncludeSystems))
            {
                using (var universalEntities = m_Manager.UniversalQueryWithSystems.ToEntityArray(Allocator.Temp))
                {
                    CollectionAssert.AreEquivalent(universalEntities, entities);
                }
            }
        }
    }
}
