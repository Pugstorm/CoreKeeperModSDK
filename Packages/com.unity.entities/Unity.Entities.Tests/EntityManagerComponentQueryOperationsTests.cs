using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Tests
{
    class EntityManagerComponentQueryOperationsTests : ECSTestsFixture
    {
        [Test]
        public void AddRemoveChunkComponent_WithQuery_Works()
        {
            var metaChunkQuery = m_Manager.CreateEntityQuery(typeof(ChunkHeader));

            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));

            var query1 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());

            m_Manager.AddChunkComponentData(query1, new EcsTestData3(7));

            Assert.IsTrue(m_Manager.HasComponent(entity1, ComponentType.ChunkComponent<EcsTestData3>()));
            var val1 = m_Manager.GetChunkComponentData<EcsTestData3>(entity1).value0;
            Assert.AreEqual(7, val1);

            Assert.IsTrue(m_Manager.HasComponent(entity2, ComponentType.ChunkComponent<EcsTestData3>()));
            var val2 = m_Manager.GetChunkComponentData<EcsTestData3>(entity2).value0;
            Assert.AreEqual(7, val2);

            Assert.IsFalse(m_Manager.HasComponent(entity3, ComponentType.ChunkComponent<EcsTestData3>()));

            Assert.AreEqual(2, metaChunkQuery.CalculateEntityCount());

            m_ManagerDebug.CheckInternalConsistency();

            var query2 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData2>(), ComponentType.ChunkComponent<EcsTestData3>());

            m_Manager.RemoveChunkComponentData<EcsTestData3>(query2);

            Assert.IsFalse(m_Manager.HasComponent(entity2, ComponentType.ChunkComponent<EcsTestData3>()));

            Assert.AreEqual(1, metaChunkQuery.CalculateEntityCount());

            m_Manager.DestroyEntity(entity1);
            m_Manager.DestroyEntity(entity2);
            m_Manager.DestroyEntity(entity3);
            metaChunkQuery.Dispose();
            query1.Dispose();
            query2.Dispose();
        }

        public enum EnabledBitsMode
        {
            NoEnableableComponents,
            NoComponentsDisabled,
            FewComponentsDisabled,
            ManyComponentsDisabled,
        }

        [Test]
        public void AddComponent_WithQuery_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            m_Manager.AddComponent<EcsTestData3>(query);
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                bool expectedHas1 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i]);
                Assert.AreEqual(expectedHas1, m_Manager.HasComponent<EcsTestData3>(entities1[i]));

                bool expectedHas2 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities2[i]);
                Assert.AreEqual(expectedHas2, m_Manager.HasComponent<EcsTestData3>(entities2[i]));

                Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entities3[i]));
            }
        }

        [Test]
        public void AddComponent_WithQuery_UpdatesOrderVersions([Values] EnabledBitsMode enabledBitsMode)
        {
            m_Manager.Debug.IncrementGlobalSystemVersion();
            int entitiesPerArchetype = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, entitiesPerArchetype, Allocator.Persistent);
            var chunkOrderVersions = new NativeArray<uint>(entitiesPerArchetype, Allocator.Temp);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                chunkOrderVersions[i] = m_Manager.GetChunk(entities[i]).GetOrderVersion();
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            // not affected by the query, but we need component types with non-zero order versions
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestFloatData));
            // read order versions
            uint orderVersionForTag = (uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>();
            uint orderVersionForData = (uint)m_Manager.GetComponentOrderVersion<EcsTestData>();
            uint orderVersionForData3 = (uint)m_Manager.GetComponentOrderVersion<EcsTestData3>();
            uint orderVersionForFloat = (uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>();

            m_Manager.Debug.IncrementGlobalSystemVersion();
            m_Manager.AddComponent<EcsTestData3>(query);

            // validate order versions
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>(), orderVersionForTag));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData>(), orderVersionForData));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData3>(), orderVersionForData3));
            Assert.IsFalse(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>(), orderVersionForFloat));
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                uint newChunkOrderVersion = m_Manager.GetChunk(entities[i]).GetOrderVersion();
                Assert.AreEqual(true, ChangeVersionUtility.DidChange(newChunkOrderVersion, chunkOrderVersions[i]));
            }
        }

        [Test]
        public void AddComponent_Tag_WithQuery_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            m_Manager.AddComponent<EcsTestTag>(query);
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                bool expectedHas1 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i]);
                Assert.AreEqual(expectedHas1, m_Manager.HasComponent<EcsTestTag>(entities1[i]));

                bool expectedHas2 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities2[i]);
                Assert.AreEqual(expectedHas2, m_Manager.HasComponent<EcsTestTag>(entities2[i]));

                Assert.IsFalse(m_Manager.HasComponent<EcsTestTag>(entities3[i]));
            }
        }

        [Test]
        public void AddComponent_Tag_WithQuery_UpdatesOrderVersions([Values] EnabledBitsMode enabledBitsMode)
        {
            m_Manager.Debug.IncrementGlobalSystemVersion();
            int entitiesPerArchetype = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, entitiesPerArchetype, Allocator.Persistent);
            var chunkOrderVersions = new NativeArray<uint>(entitiesPerArchetype, Allocator.Temp);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                chunkOrderVersions[i] = m_Manager.GetChunk(entities[i]).GetOrderVersion();
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            // not affected by the query, but we need component types with non-zero order versions
            var e2 = m_Manager.CreateEntity(typeof(AnotherEcsTestTag), typeof(EcsTestFloatData));
            // read order versions
            uint orderVersionForTag = (uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>();
            uint orderVersionForData = (uint)m_Manager.GetComponentOrderVersion<EcsTestData>();
            uint orderVersionForTag2 = (uint)m_Manager.GetComponentOrderVersion<AnotherEcsTestTag>();
            uint orderVersionForFloat = (uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>();

            m_Manager.Debug.IncrementGlobalSystemVersion();
            m_Manager.AddComponent<AnotherEcsTestTag>(query);

            // validate order versions
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>(), orderVersionForTag));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData>(), orderVersionForData));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<AnotherEcsTestTag>(), orderVersionForTag2));
            Assert.IsFalse(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>(), orderVersionForFloat));
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                uint newChunkOrderVersion = m_Manager.GetChunk(entities[i]).GetOrderVersion();
                Assert.AreEqual(true, ChangeVersionUtility.DidChange(newChunkOrderVersion, chunkOrderVersions[i]));
            }
        }

        [Test]
        public void AddComponents_WithQuery_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            m_Manager.AddComponent(query, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>()));
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                bool expectedHas1 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i]);
                Assert.AreEqual(expectedHas1, m_Manager.HasComponent<EcsTestData3>(entities1[i]));
                Assert.AreEqual(expectedHas1, m_Manager.HasComponent<EcsTestData4>(entities1[i]));

                bool expectedHas2 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities2[i]);
                Assert.AreEqual(expectedHas2, m_Manager.HasComponent<EcsTestData3>(entities2[i]));
                Assert.AreEqual(expectedHas2, m_Manager.HasComponent<EcsTestData4>(entities2[i]));

                Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entities3[i]));
                Assert.IsFalse(m_Manager.HasComponent<EcsTestData4>(entities3[i]));
            }
        }

        [Test]
        public void AddComponents_WithQuery_UpdatesOrderVersions([Values] EnabledBitsMode enabledBitsMode)
        {
            m_Manager.Debug.IncrementGlobalSystemVersion();
            int entitiesPerArchetype = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, entitiesPerArchetype, Allocator.Persistent);
            var chunkOrderVersions = new NativeArray<uint>(entitiesPerArchetype, Allocator.Temp);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                chunkOrderVersions[i] = m_Manager.GetChunk(entities[i]).GetOrderVersion();
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            // not affected by the query, but we need component types with non-zero order versions
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestData4), typeof(EcsTestFloatData));
            // read order versions
            uint orderVersionForTag = (uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>();
            uint orderVersionForData = (uint)m_Manager.GetComponentOrderVersion<EcsTestData>();
            uint orderVersionForData3 = (uint)m_Manager.GetComponentOrderVersion<EcsTestData3>();
            uint orderVersionForData4 = (uint)m_Manager.GetComponentOrderVersion<EcsTestData4>();
            uint orderVersionForFloat = (uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>();

            m_Manager.Debug.IncrementGlobalSystemVersion();
            m_Manager.AddComponent(query, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestData3>(), ComponentType.ReadOnly<EcsTestData4>()));

            // validate order versions
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>(), orderVersionForTag));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData>(), orderVersionForData));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData3>(), orderVersionForData3));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData4>(), orderVersionForData4));
            Assert.IsFalse(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>(), orderVersionForFloat));
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                uint newChunkOrderVersion = m_Manager.GetChunk(entities[i]).GetOrderVersion();
                Assert.AreEqual(true, ChangeVersionUtility.DidChange(newChunkOrderVersion, chunkOrderVersions[i]));
            }
        }

        [Test]
        public void RemoveComponent_WithQuery_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData3), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData3), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData3), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            m_Manager.RemoveComponent<EcsTestData3>(query);
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                bool expectedHas1 = !m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i]);
                Assert.AreEqual(expectedHas1, m_Manager.HasComponent<EcsTestData3>(entities1[i]));

                bool expectedHas2 = !m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities2[i]);
                Assert.AreEqual(expectedHas2, m_Manager.HasComponent<EcsTestData3>(entities2[i]));

                Assert.IsTrue(m_Manager.HasComponent<EcsTestData3>(entities3[i]));
            }
        }

        [Test]
        public void RemoveComponent_WithQuery_UpdatesOrderVersions([Values] EnabledBitsMode enabledBitsMode)
        {
            m_Manager.Debug.IncrementGlobalSystemVersion();
            int entitiesPerArchetype = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData3), typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, entitiesPerArchetype, Allocator.Persistent);
            var chunkOrderVersions = new NativeArray<uint>(entitiesPerArchetype, Allocator.Temp);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                chunkOrderVersions[i] = m_Manager.GetChunk(entities[i]).GetOrderVersion();
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            // not affected by the query, but we need component types with non-zero order versions
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestFloatData));
            // read order versions
            uint orderVersionForTag = (uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>();
            uint orderVersionForData = (uint)m_Manager.GetComponentOrderVersion<EcsTestData>();
            uint orderVersionForData3 = (uint)m_Manager.GetComponentOrderVersion<EcsTestData3>();
            uint orderVersionForFloat = (uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>();

            m_Manager.Debug.IncrementGlobalSystemVersion();
            m_Manager.RemoveComponent<EcsTestData3>(query);

            // validate order versions
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>(), orderVersionForTag));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData>(), orderVersionForData));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData3>(), orderVersionForData3));
            Assert.IsFalse(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>(), orderVersionForFloat));
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                uint newChunkOrderVersion = m_Manager.GetChunk(entities[i]).GetOrderVersion();
                Assert.AreEqual(true, ChangeVersionUtility.DidChange(newChunkOrderVersion, chunkOrderVersions[i]));
            }
        }

        [Test]
        public void RemoveComponent_Tag_WithQuery_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestTag), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestTag), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestTag), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            m_Manager.RemoveComponent<EcsTestTag>(query);
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                bool expectedHas1 = !m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i]);
                Assert.AreEqual(expectedHas1, m_Manager.HasComponent<EcsTestTag>(entities1[i]));

                bool expectedHas2 = !m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities2[i]);
                Assert.AreEqual(expectedHas2, m_Manager.HasComponent<EcsTestTag>(entities2[i]));

                Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(entities3[i]));
            }
        }

        [Test]
        public void RemoveComponent_Tag_WithQuery_UpdatesOrderVersions([Values] EnabledBitsMode enabledBitsMode)
        {
            m_Manager.Debug.IncrementGlobalSystemVersion();
            int entitiesPerArchetype = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(AnotherEcsTestTag), typeof(EcsTestData3), typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, entitiesPerArchetype, Allocator.Persistent);
            var chunkOrderVersions = new NativeArray<uint>(entitiesPerArchetype, Allocator.Temp);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                chunkOrderVersions[i] = m_Manager.GetChunk(entities[i]).GetOrderVersion();
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            // not affected by the query, but we need component types with non-zero order versions
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestFloatData));
            // read order versions
            uint orderVersionForTag = (uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>();
            uint orderVersionForTag2 = (uint)m_Manager.GetComponentOrderVersion<AnotherEcsTestTag>();
            uint orderVersionForData = (uint)m_Manager.GetComponentOrderVersion<EcsTestData>();
            uint orderVersionForData3 = (uint)m_Manager.GetComponentOrderVersion<EcsTestData3>();
            uint orderVersionForFloat = (uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>();

            m_Manager.Debug.IncrementGlobalSystemVersion();
            m_Manager.RemoveComponent<AnotherEcsTestTag>(query);

            // validate order versions
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>(), orderVersionForTag));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<AnotherEcsTestTag>(), orderVersionForTag2));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData>(), orderVersionForData));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData3>(), orderVersionForData3));
            Assert.IsFalse(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>(), orderVersionForFloat));
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                uint newChunkOrderVersion = m_Manager.GetChunk(entities[i]).GetOrderVersion();
                Assert.AreEqual(true, ChangeVersionUtility.DidChange(newChunkOrderVersion, chunkOrderVersions[i]));
            }
        }

        [Test]
        public void RemoveComponents_WithQuery_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestTag), typeof(EcsTestData3), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestTag), typeof(EcsTestData3), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestTag), typeof(EcsTestData3), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            m_Manager.RemoveComponent(query, new ComponentTypeSet(ComponentType.ReadOnly<EcsTestTag>(), ComponentType.ReadOnly<EcsTestData3>()));
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                bool expectedHas1 = !m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i]);
                Assert.AreEqual(expectedHas1, m_Manager.HasComponent<EcsTestTag>(entities1[i]));
                Assert.AreEqual(expectedHas1, m_Manager.HasComponent<EcsTestData3>(entities1[i]));

                bool expectedHas2 = !m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities2[i]);
                Assert.AreEqual(expectedHas2, m_Manager.HasComponent<EcsTestTag>(entities2[i]));
                Assert.AreEqual(expectedHas2, m_Manager.HasComponent<EcsTestData3>(entities2[i]));

                Assert.IsTrue(m_Manager.HasComponent<EcsTestTag>(entities3[i]));
                Assert.IsTrue(m_Manager.HasComponent<EcsTestData3>(entities3[i]));
            }
        }

        [Test]
        public void RemoveComponents_WithQuery_UpdatesOrderVersions([Values] EnabledBitsMode enabledBitsMode)
        {
            m_Manager.Debug.IncrementGlobalSystemVersion();
            int entitiesPerArchetype = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(AnotherEcsTestTag), typeof(EcsTestData3), typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, entitiesPerArchetype, Allocator.Persistent);
            var chunkOrderVersions = new NativeArray<uint>(entitiesPerArchetype, Allocator.Temp);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                chunkOrderVersions[i] = m_Manager.GetChunk(entities[i]).GetOrderVersion();
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            // not affected by the query, but we need component types with non-zero order versions
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestFloatData));
            // read order versions
            uint orderVersionForTag = (uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>();
            uint orderVersionForTag2 = (uint)m_Manager.GetComponentOrderVersion<AnotherEcsTestTag>();
            uint orderVersionForData = (uint)m_Manager.GetComponentOrderVersion<EcsTestData>();
            uint orderVersionForData3 = (uint)m_Manager.GetComponentOrderVersion<EcsTestData3>();
            uint orderVersionForFloat = (uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>();

            m_Manager.Debug.IncrementGlobalSystemVersion();
            m_Manager.RemoveComponent(query, new ComponentTypeSet(ComponentType.ReadOnly<AnotherEcsTestTag>(), ComponentType.ReadOnly<EcsTestData3>()));

            // validate order versions
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>(), orderVersionForTag));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<AnotherEcsTestTag>(), orderVersionForTag2));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData>(), orderVersionForData));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData3>(), orderVersionForData3));
            Assert.IsFalse(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>(), orderVersionForFloat));
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                uint newChunkOrderVersion = m_Manager.GetChunk(entities[i]).GetOrderVersion();
                Assert.AreEqual(true, ChangeVersionUtility.DidChange(newChunkOrderVersion, chunkOrderVersions[i]));
            }
        }

        [Test]
        public void DestroyEntity_WithQuery_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            using var expectedEntities = new NativeList<Entity>(entitiesPerArchetype * 3, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                    expectedEntities.Add(entities1[i]);
                    expectedEntities.Add(entities2[i]);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                    expectedEntities.Add(entities1[i]);
                    expectedEntities.Add(entities2[i]);
                }
                expectedEntities.Add(entities3[i]);
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            m_Manager.DestroyEntity(query);

            var query2 = new EntityQueryBuilder(Allocator.Temp).WithAny<EcsTestData,EcsTestData2>().Build(m_Manager);
            using var remainingEntities = query2.ToEntityArray(Allocator.Persistent);
            CollectionAssert.AreEquivalent(expectedEntities.AsArray().ToArray(), remainingEntities.ToArray());
        }

        [Test]
        public void DestroyEntity_WithQuery_UpdatesOrderVersions([Values] EnabledBitsMode enabledBitsMode)
        {
            m_Manager.Debug.IncrementGlobalSystemVersion();
            int entitiesPerArchetype = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, entitiesPerArchetype, Allocator.Persistent);
            var chunkOrderVersions = new NativeArray<uint>(entitiesPerArchetype, Allocator.Temp);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                chunkOrderVersions[i] = m_Manager.GetChunk(entities[i]).GetOrderVersion();
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            // not affected by the query, but we need component types with non-zero order versions
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestFloatData));
            // read order versions
            uint orderVersionForTag = (uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>();
            uint orderVersionForData = (uint)m_Manager.GetComponentOrderVersion<EcsTestData>();
            uint orderVersionForData3 = (uint)m_Manager.GetComponentOrderVersion<EcsTestData3>();
            uint orderVersionForFloat = (uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>();

            m_Manager.Debug.IncrementGlobalSystemVersion();
            m_Manager.DestroyEntity(query);

            // validate order versions
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>(), orderVersionForTag));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData>(), orderVersionForData));
            Assert.IsFalse(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData3>(), orderVersionForData3));
            Assert.IsFalse(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>(), orderVersionForFloat));
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (!m_Manager.Exists(entities[i]))
                    continue;
                uint newChunkOrderVersion = m_Manager.GetChunk(entities[i]).GetOrderVersion();
                Assert.AreEqual(true, ChangeVersionUtility.DidChange(newChunkOrderVersion, chunkOrderVersions[i]));
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void DestroyEntity_WithQuery_LinkedEntityGroupsNotDestroyed_Throws([Values(EnabledBitsMode.NoComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData), typeof(LinkedEntityGroup));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            // set up linked entity groups.
            using var linkedEntities2 = m_Manager.CreateEntity(archetype2, 2, Allocator.Persistent);
            m_Manager.SetComponentEnabled<EcsTestTagEnableable>(linkedEntities2[0], false);
            // Add an entity to the LinkedEntityGroup which does not match the query, because it has a required component disabled.
            var leg1 = m_Manager.GetBuffer<LinkedEntityGroup>(entities1[0], false);
            leg1.Add(linkedEntities2[0]);

            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData, EcsTestTagEnableable>()
                .Build(m_Manager);
            Assert.Throws<ArgumentException>(() => { m_Manager.DestroyEntity(query); });
        }

        [Test]
        public void AddSharedComponent_WithQuery_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            int sharedComponentValue = 7;
            m_Manager.AddSharedComponent(query, new EcsTestSharedComp(sharedComponentValue));
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                bool expectedHas1 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i]);
                Assert.AreEqual(expectedHas1, m_Manager.HasComponent<EcsTestSharedComp>(entities1[i]));
                if (expectedHas1)
                    Assert.AreEqual(sharedComponentValue, m_Manager.GetSharedComponent<EcsTestSharedComp>(entities1[i]).value);

                bool expectedHas2 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities2[i]);
                Assert.AreEqual(expectedHas2, m_Manager.HasComponent<EcsTestSharedComp>(entities2[i]));
                if (expectedHas2)
                    Assert.AreEqual(sharedComponentValue, m_Manager.GetSharedComponent<EcsTestSharedComp>(entities2[i]).value);

                Assert.IsFalse(m_Manager.HasComponent<EcsTestSharedComp>(entities3[i]));
            }
        }

        [Test]
        public void AddSharedComponent_WithQuery_UpdatesOrderVersions([Values] EnabledBitsMode enabledBitsMode)
        {
            m_Manager.Debug.IncrementGlobalSystemVersion();
            int entitiesPerArchetype = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, entitiesPerArchetype, Allocator.Persistent);
            var chunkOrderVersions = new NativeArray<uint>(entitiesPerArchetype, Allocator.Temp);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                chunkOrderVersions[i] = m_Manager.GetChunk(entities[i]).GetOrderVersion();
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);

            // not affected by the query, but we need component types with non-zero order versions
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestFloatData));
            // read order versions
            uint orderVersionForTag = (uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>();
            uint orderVersionForData = (uint)m_Manager.GetComponentOrderVersion<EcsTestData>();
            uint orderVersionForSharedData = (uint)m_Manager.GetComponentOrderVersion<EcsTestSharedComp>();
            uint orderVersionForFloat = (uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>();

            m_Manager.Debug.IncrementGlobalSystemVersion();
            int sharedComponentValue = 7;
            m_Manager.AddSharedComponent(query, new EcsTestSharedComp(sharedComponentValue));

            // validate order versions
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>(), orderVersionForTag));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData>(), orderVersionForData));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestSharedComp>(), orderVersionForSharedData));
            Assert.IsFalse(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>(), orderVersionForFloat));
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                uint newChunkOrderVersion = m_Manager.GetChunk(entities[i]).GetOrderVersion();
                Assert.AreEqual(true, ChangeVersionUtility.DidChange(newChunkOrderVersion, chunkOrderVersions[i]));
            }
        }

        [Test]
        public void AddSharedComponentManaged_WithQuery_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            string sharedComponentValue = "value";
            m_Manager.AddSharedComponentManaged(query, new EcsTestSharedCompManaged(sharedComponentValue));
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                bool expectedHas1 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i]);
                Assert.AreEqual(expectedHas1, m_Manager.HasComponent<EcsTestSharedCompManaged>(entities1[i]));
                if (expectedHas1)
                    Assert.AreEqual(sharedComponentValue, m_Manager.GetSharedComponentManaged<EcsTestSharedCompManaged>(entities1[i]).value);

                bool expectedHas2 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities2[i]);
                Assert.AreEqual(expectedHas2, m_Manager.HasComponent<EcsTestSharedCompManaged>(entities2[i]));
                if (expectedHas2)
                    Assert.AreEqual(sharedComponentValue, m_Manager.GetSharedComponentManaged<EcsTestSharedCompManaged>(entities2[i]).value);

                Assert.IsFalse(m_Manager.HasComponent<EcsTestSharedCompManaged>(entities3[i]));
            }
        }

        [Test]
        public void AddSharedComponentManaged_WithQuery_UpdatesOrderVersions([Values] EnabledBitsMode enabledBitsMode)
        {
            m_Manager.Debug.IncrementGlobalSystemVersion();
            int entitiesPerArchetype = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, entitiesPerArchetype, Allocator.Persistent);
            var chunkOrderVersions = new NativeArray<uint>(entitiesPerArchetype, Allocator.Temp);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                chunkOrderVersions[i] = m_Manager.GetChunk(entities[i]).GetOrderVersion();
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);

            // not affected by the query, but we need component types with non-zero order versions
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestFloatData));
            // read order versions
            uint orderVersionForTag = (uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>();
            uint orderVersionForData = (uint)m_Manager.GetComponentOrderVersion<EcsTestData>();
            uint orderVersionForSharedData = (uint)m_Manager.GetComponentOrderVersion<EcsTestSharedCompManaged>();
            uint orderVersionForFloat = (uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>();

            m_Manager.Debug.IncrementGlobalSystemVersion();
            string sharedComponentValue = "value";
            m_Manager.AddSharedComponentManaged(query, new EcsTestSharedCompManaged(sharedComponentValue));

            // validate order versions
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>(), orderVersionForTag));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData>(), orderVersionForData));
            Assert.IsTrue(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestSharedCompManaged>(), orderVersionForSharedData));
            Assert.IsFalse(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>(), orderVersionForFloat));
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                uint newChunkOrderVersion = m_Manager.GetChunk(entities[i]).GetOrderVersion();
                Assert.AreEqual(true, ChangeVersionUtility.DidChange(newChunkOrderVersion, chunkOrderVersions[i]));
            }
        }

        [Test]
        public void AddSharedComponent_WithQuery_ChunksWithTargetComponentChangeValue([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData), typeof(EcsTestSharedComp));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            int sharedComponentValue = 7;
            m_Manager.AddSharedComponent(query, new EcsTestSharedComp(sharedComponentValue));
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                // Entities in archetype1 that matched the query should now have EcsTestSharedComp with the expected value
                bool expectedHas1 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i]);
                Assert.AreEqual(expectedHas1, m_Manager.HasComponent<EcsTestSharedComp>(entities1[i]));
                if (expectedHas1)
                    Assert.AreEqual(sharedComponentValue, m_Manager.GetSharedComponent<EcsTestSharedComp>(entities1[i]).value);
                // Entities in archetype2 already have EcsTestSharedComp, but should still get the new component value
                Assert.AreEqual(true, m_Manager.HasComponent<EcsTestSharedComp>(entities2[i]));
                var expectedValue2 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities2[i])
                    ? sharedComponentValue : default(EcsTestSharedComp).value;
                Assert.AreEqual(expectedValue2, m_Manager.GetSharedComponent<EcsTestSharedComp>(entities2[i]).value);
            }
        }

        [Test]
        public void AddSharedComponentManaged_WithQuery_ChunksWithTargetComponentChangeValue([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestData), typeof(EcsTestSharedCompManaged));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            string sharedComponentValue = "value";
            m_Manager.AddSharedComponentManaged(query, new EcsTestSharedCompManaged(sharedComponentValue));
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                // Entities in archetype1 that matched the query should now have EcsTestSharedComp with the expected value
                bool expectedHas1 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i]);
                Assert.AreEqual(expectedHas1, m_Manager.HasComponent<EcsTestSharedCompManaged>(entities1[i]));
                if (expectedHas1)
                    Assert.AreEqual(sharedComponentValue, m_Manager.GetSharedComponentManaged<EcsTestSharedCompManaged>(entities1[i]).value);
                // Entities in archetype2 already have EcsTestSharedComp, but should still get the new component value
                Assert.AreEqual(true, m_Manager.HasComponent<EcsTestSharedCompManaged>(entities2[i]));
                var expectedValue2 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities2[i])
                    ? sharedComponentValue : default(EcsTestSharedCompManaged).value;
                Assert.AreEqual(expectedValue2, m_Manager.GetSharedComponentManaged<EcsTestSharedCompManaged>(entities2[i]).value);
            }
        }

        [Test]
        public void SetSharedComponent_WithQuery_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedComp), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedComp), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedComp), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            int sharedComponentValue = 7;
            m_Manager.SetSharedComponent(query, new EcsTestSharedComp(sharedComponentValue));
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                int expectedValue1 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i]) ? sharedComponentValue : 0;
                Assert.AreEqual(expectedValue1, m_Manager.GetSharedComponent<EcsTestSharedComp>(entities1[i]).value);

                int expectedValue2 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities2[i]) ? sharedComponentValue : 0;
                Assert.AreEqual(expectedValue2, m_Manager.GetSharedComponent<EcsTestSharedComp>(entities2[i]).value);

                Assert.AreEqual(0, m_Manager.GetSharedComponent<EcsTestSharedComp>(entities3[i]).value);
            }
        }

        [Test]
        public void SetSharedComponent_WithQuery_UpdatesOrderVersions([Values] EnabledBitsMode enabledBitsMode)
        {
            m_Manager.Debug.IncrementGlobalSystemVersion();
            int entitiesPerArchetype = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedComp), typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, entitiesPerArchetype, Allocator.Persistent);
            var chunkOrderVersions = new NativeArray<uint>(entitiesPerArchetype, Allocator.Temp);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                chunkOrderVersions[i] = m_Manager.GetChunk(entities[i]).GetOrderVersion();
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);

            // not affected by the query, but we need component types with non-zero order versions
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestFloatData));
            // read order versions
            uint orderVersionForTag = (uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>();
            uint orderVersionForData = (uint)m_Manager.GetComponentOrderVersion<EcsTestData>();
            uint orderVersionForSharedData = (uint)m_Manager.GetComponentOrderVersion<EcsTestSharedComp>();
            uint orderVersionForFloat = (uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>();

            m_Manager.Debug.IncrementGlobalSystemVersion();
            int sharedComponentValue = 7;
            m_Manager.SetSharedComponent(query, new EcsTestSharedComp(sharedComponentValue));

            // validate order versions
            bool expectChange = enabledBitsMode == EnabledBitsMode.FewComponentsDisabled ||
                                enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled;
            Assert.AreEqual(expectChange, ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>(), orderVersionForTag));
            Assert.AreEqual(expectChange, ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData>(), orderVersionForData));
            Assert.AreEqual(expectChange, ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestSharedComp>(), orderVersionForSharedData));
            Assert.IsFalse(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>(), orderVersionForFloat));
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                uint newChunkOrderVersion = m_Manager.GetChunk(entities[i]).GetOrderVersion();
                Assert.AreEqual(expectChange, ChangeVersionUtility.DidChange(newChunkOrderVersion, chunkOrderVersions[i]));
            }
        }

        [Test]
        public void SetSharedComponentManaged_WithQuery_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedCompManaged), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedCompManaged), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedCompManaged), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            var sharedComponentValue = new EcsTestSharedCompManaged("value");
            m_Manager.SetSharedComponentManaged(query, sharedComponentValue);
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                var expectedValue1 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i])
                    ? sharedComponentValue : default(EcsTestSharedCompManaged);
                Assert.AreEqual(expectedValue1, m_Manager.GetSharedComponentManaged<EcsTestSharedCompManaged>(entities1[i]));

                var expectedValue2 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities2[i])
                    ? sharedComponentValue : default(EcsTestSharedCompManaged);
                Assert.AreEqual(expectedValue2, m_Manager.GetSharedComponentManaged<EcsTestSharedCompManaged>(entities2[i]));

                Assert.AreEqual(default(EcsTestSharedCompManaged), m_Manager.GetSharedComponentManaged<EcsTestSharedCompManaged>(entities3[i]));
            }
        }

        [Test]
        public void SetSharedComponentManaged_WithQuery_UpdatesOrderVersions([Values] EnabledBitsMode enabledBitsMode)
        {
            m_Manager.Debug.IncrementGlobalSystemVersion();
            int entitiesPerArchetype = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedCompManaged), typeof(EcsTestData));
            using var entities = m_Manager.CreateEntity(archetype, entitiesPerArchetype, Allocator.Persistent);
            var chunkOrderVersions = new NativeArray<uint>(entitiesPerArchetype, Allocator.Temp);
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities[i], false);
                }
                chunkOrderVersions[i] = m_Manager.GetChunk(entities[i]).GetOrderVersion();
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);

            // not affected by the query, but we need component types with non-zero order versions
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData3), typeof(EcsTestFloatData));
            // read order versions
            uint orderVersionForTag = (uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>();
            uint orderVersionForData = (uint)m_Manager.GetComponentOrderVersion<EcsTestData>();
            uint orderVersionForSharedData = (uint)m_Manager.GetComponentOrderVersion<EcsTestSharedCompManaged>();
            uint orderVersionForFloat = (uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>();

            m_Manager.Debug.IncrementGlobalSystemVersion();
            string sharedComponentValue = "value";
            m_Manager.SetSharedComponentManaged(query, new EcsTestSharedCompManaged(sharedComponentValue));

            // validate order versions
            bool expectChange = enabledBitsMode == EnabledBitsMode.FewComponentsDisabled ||
                                enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled;
            Assert.AreEqual(expectChange, ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestTagEnableable>(), orderVersionForTag));
            Assert.AreEqual(expectChange, ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestData>(), orderVersionForData));
            Assert.AreEqual(expectChange, ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestSharedCompManaged>(), orderVersionForSharedData));
            Assert.IsFalse(ChangeVersionUtility.DidChange((uint)m_Manager.GetComponentOrderVersion<EcsTestFloatData>(), orderVersionForFloat));
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                uint newChunkOrderVersion = m_Manager.GetChunk(entities[i]).GetOrderVersion();
                Assert.AreEqual(expectChange, ChangeVersionUtility.DidChange(newChunkOrderVersion, chunkOrderVersions[i]));
            }
        }

        [Test]
        public void SetSharedComponent_WithQuery_ChunksAlreadyHaveNewValue_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedComp), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedComp), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedComp), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            int sharedComponentValue = 7;
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                m_Manager.SetSharedComponent(entities2[i], new EcsTestSharedComp(sharedComponentValue));
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            m_Manager.SetSharedComponent(query, new EcsTestSharedComp(sharedComponentValue));
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                int expectedValue1 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i]) ? sharedComponentValue : 0;
                Assert.AreEqual(expectedValue1, m_Manager.GetSharedComponent<EcsTestSharedComp>(entities1[i]).value);

                int expectedValue2 = sharedComponentValue;
                Assert.AreEqual(expectedValue2, m_Manager.GetSharedComponent<EcsTestSharedComp>(entities2[i]).value);

                Assert.AreEqual(0, m_Manager.GetSharedComponent<EcsTestSharedComp>(entities3[i]).value);
            }
        }

        [Test]
        public void SetSharedComponentManaged_WithQuery_ChunksAlreadyHaveNewValue_Works([Values] EnabledBitsMode enabledBitsMode)
        {
            int entitiesPerArchetype = 1000;
            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedCompManaged), typeof(EcsTestData));
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedCompManaged), typeof(EcsTestData), typeof(EcsTestData2));
            var archetype3 = m_Manager.CreateArchetype(typeof(EcsTestTagEnableable), typeof(EcsTestSharedCompManaged), typeof(EcsTestData2));
            using var entities1 = m_Manager.CreateEntity(archetype1, entitiesPerArchetype, Allocator.Persistent);
            using var entities2 = m_Manager.CreateEntity(archetype2, entitiesPerArchetype, Allocator.Persistent);
            using var entities3 = m_Manager.CreateEntity(archetype3, entitiesPerArchetype, Allocator.Persistent);
            var sharedComponentValue = new EcsTestSharedCompManaged("value");
            for (int i = 0; i < entitiesPerArchetype; i++)
            {
                m_Manager.SetSharedComponentManaged(entities2[i], sharedComponentValue);
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                {
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities1[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities2[i], false);
                    m_Manager.SetComponentEnabled<EcsTestTagEnableable>(entities3[i], false);
                }
            }
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestData>();
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestTagEnableable>();
            using var query = queryBuilder.Build(m_Manager);
            m_Manager.SetSharedComponentManaged(query, sharedComponentValue);
            for (int i = 0; i < entitiesPerArchetype; ++i)
            {
                var expectedValue1 = m_Manager.IsComponentEnabled<EcsTestTagEnableable>(entities1[i])
                    ? sharedComponentValue : default(EcsTestSharedCompManaged);
                Assert.AreEqual(expectedValue1, m_Manager.GetSharedComponentManaged<EcsTestSharedCompManaged>(entities1[i]));

                var expectedValue2 = sharedComponentValue;
                Assert.AreEqual(expectedValue2, m_Manager.GetSharedComponentManaged<EcsTestSharedCompManaged>(entities2[i]));

                Assert.AreEqual(default(EcsTestSharedCompManaged), m_Manager.GetSharedComponentManaged<EcsTestSharedCompManaged>(entities3[i]));
            }
        }

        [Test]
        public void AddRemoveAnyComponent_WithQuery_WorksWithVariousTypes()
        {
            var componentTypes = new ComponentType[] { typeof(EcsTestTag), typeof(EcsTestData4), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp) };

            foreach (var type in componentTypes)
            {
                // We want a clean slate for the m_manager so teardown and setup before the test
                TearDown();
                Setup();

                var metaChunkQuery = m_Manager.CreateEntityQuery(typeof(ChunkHeader));

                var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
                var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
                var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));

                var query1 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());

                m_Manager.AddComponent(query1, type);


                Assert.IsTrue(m_Manager.HasComponent(entity1, type));
                Assert.IsTrue(m_Manager.HasComponent(entity2, type));
                Assert.IsFalse(m_Manager.HasComponent(entity3, type));

                if (type.IsChunkComponent)
                    Assert.AreEqual(2, metaChunkQuery.CalculateEntityCount());

                if (type == ComponentType.ReadWrite<EcsTestSharedComp>())
                {
                    m_Manager.SetSharedComponent(entity1, new EcsTestSharedComp(1));
                    m_Manager.SetSharedComponent(entity2, new EcsTestSharedComp(2));
                }

                m_ManagerDebug.CheckInternalConsistency();

                var query2 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData2>(), type);

                m_Manager.RemoveComponent(query2, type);

                Assert.IsFalse(m_Manager.HasComponent(entity2, ComponentType.ChunkComponent<EcsTestData3>()));

                if (type.IsChunkComponent)
                    Assert.AreEqual(1, metaChunkQuery.CalculateEntityCount());
            }
        }

        [Test]
        public void AddRemoveAnyComponentManaged_WithQuery_WorksWithVariousTypes()
        {
            var componentTypes = new ComponentType[] { typeof(EcsTestTag), typeof(EcsTestData4), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp) };

            foreach (var type in componentTypes)
            {
                // We want a clean slate for the m_manager so teardown and setup before the test
                TearDown();
                Setup();

                var metaChunkQuery = m_Manager.CreateEntityQuery(typeof(ChunkHeader));

                var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
                var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
                var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));

                var query1 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());

                m_Manager.AddComponent(query1, type);


                Assert.IsTrue(m_Manager.HasComponent(entity1, type));
                Assert.IsTrue(m_Manager.HasComponent(entity2, type));
                Assert.IsFalse(m_Manager.HasComponent(entity3, type));

                if (type.IsChunkComponent)
                    Assert.AreEqual(2, metaChunkQuery.CalculateEntityCount());

                if (type == ComponentType.ReadWrite<EcsTestSharedComp>())
                {
                    m_Manager.SetSharedComponentManaged(entity1, new EcsTestSharedComp(1));
                    m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp(2));
                }

                m_ManagerDebug.CheckInternalConsistency();

                var query2 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData2>(), type);

                m_Manager.RemoveComponent(query2, type);

                Assert.IsFalse(m_Manager.HasComponent(entity2, ComponentType.ChunkComponent<EcsTestData3>()));

                if (type.IsChunkComponent)
                    Assert.AreEqual(1, metaChunkQuery.CalculateEntityCount());
            }
        }

        [Test]
        public void AddMultipleComponents_WithQuery_Works()
        {
            var componentTypes = new ComponentTypeSet(
                typeof(EcsTestTag), typeof(EcsTestData2), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp));

            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            m_Manager.AddComponent(query, componentTypes);

            m_ManagerDebug.CheckInternalConsistency();

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestTag), typeof(EcsTestData2), ComponentType.ChunkComponent<EcsTestData4>(),
                typeof(EcsTestSharedComp));
            Assert.AreEqual(archetype, m_Manager.GetChunk(entity1).Archetype);
            Assert.AreEqual(archetype, m_Manager.GetChunk(entity2).Archetype);

            // verify entity3 is unchanged
            Assert.AreEqual(m_Manager.CreateArchetype(typeof(EcsTestData2)), m_Manager.GetChunk(entity3).Archetype);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void AddMultipleComponents_WithQuery_AddingEntityComponentTypeThrows()
        {
            var componentTypes = new ComponentTypeSet(
                typeof(EcsTestTag), typeof(Entity), typeof(EcsTestData2), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp));

            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));
            var archetype1 = m_Manager.GetChunk(entity1).Archetype;
            var archetype2 = m_Manager.GetChunk(entity2).Archetype;
            var archetype3 = m_Manager.GetChunk(entity3).Archetype;

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            Assert.Throws<ArgumentException>(() => m_Manager.AddComponent(query, componentTypes));

            m_ManagerDebug.CheckInternalConsistency();

            // entities should be unchanged
            Assert.AreEqual(archetype1, m_Manager.GetChunk(entity1).Archetype);
            Assert.AreEqual(archetype2, m_Manager.GetChunk(entity2).Archetype);
            Assert.AreEqual(archetype3, m_Manager.GetChunk(entity3).Archetype);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void AddMultipleComponents_WithQuery_ExceedMaxSharedComponentsThrows()
        {
            var componentTypes = new ComponentTypeSet(new ComponentType[] {
                typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3), typeof(EcsTestSharedComp4),
                typeof(EcsTestSharedComp5), typeof(EcsTestSharedComp6), typeof(EcsTestSharedComp7), typeof(EcsTestSharedComp8),
                typeof(EcsTestSharedComp9), typeof(EcsTestSharedComp10), typeof(EcsTestSharedComp11), typeof(EcsTestSharedComp12),
                typeof(EcsTestSharedComp13), typeof(EcsTestSharedComp14), typeof(EcsTestSharedComp15), typeof(EcsTestSharedComp16)
            });

            Assert.AreEqual(16, EntityComponentStore.kMaxSharedComponentCount);   // if kMaxSharedComponentCount changes, need to update this test

            var entity1 = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData), typeof(EcsTestSharedComp17));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData), typeof(EcsTestData2));
            var entity3 = m_Manager.CreateEntity(typeof(EcsTestSharedComp), typeof(EcsTestData2));
            var archetype1 = m_Manager.GetChunk(entity1).Archetype;
            var archetype2 = m_Manager.GetChunk(entity2).Archetype;
            var archetype3 = m_Manager.GetChunk(entity3).Archetype;

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            Assert.That(() => m_Manager.AddComponent(query, componentTypes), Throws.InvalidOperationException
                  .With.Message.StartsWith($"Cannot add more than {EntityComponentStore.kMaxSharedComponentCount} SharedComponent's to a single Archetype"));

            m_ManagerDebug.CheckInternalConsistency();

            // entities should be unchanged
            Assert.AreEqual(archetype1, m_Manager.GetChunk(entity1).Archetype);
            Assert.AreEqual(archetype2, m_Manager.GetChunk(entity2).Archetype);
            Assert.AreEqual(archetype3, m_Manager.GetChunk(entity3).Archetype);
        }

        [Test]
        public void AddMultipleComponents_WithQuery_SharedComponentValuesPreserved()
        {
            // test for entities of different chunks getting their correct shared values
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            m_Manager.SetSharedComponentManaged(entity1, new EcsTestSharedComp() {value = 5});
            m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp() {value = 9});

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData2));
            m_Manager.AddComponent(query, new ComponentTypeSet(typeof(EcsTestData2), typeof(EcsTestData4)));

            m_ManagerDebug.CheckInternalConsistency();

            Assert.AreEqual(5, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity1).value);
            Assert.AreEqual(9, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity2).value);

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData4), typeof(EcsTestSharedComp));
            Assert.AreEqual(archetype, m_Manager.GetChunk(entity1).Archetype);
            Assert.AreEqual(archetype, m_Manager.GetChunk(entity2).Archetype);
        }

        private struct EcsTestDataHuge : IComponentData
        {
            public FixedString4096Bytes value0;
            public FixedString4096Bytes value1;
            public FixedString4096Bytes value2;
            public FixedString4096Bytes value3;
            public FixedString4096Bytes value4;

            public EcsTestDataHuge(FixedString4096Bytes inValue)
            {
                value4 = value3 = value2 = value1 = value0 = inValue;
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void AddMultipleComponents_WithQuery_ExceedChunkCapacityThrows()
        {
            var componentTypes = new ComponentTypeSet(typeof(EcsTestDataHuge)); // add really big component(s)

            Assert.AreEqual(16320, Chunk.kChunkBufferSize);   // if chunk size changes, need to update this test

            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));
            var archetype1 = m_Manager.GetChunk(entity1).Archetype;
            var archetype2 = m_Manager.GetChunk(entity2).Archetype;
            var archetype3 = m_Manager.GetChunk(entity3).Archetype;

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            Assert.That(() => m_Manager.AddComponent(query, componentTypes), Throws.InvalidOperationException
                            .With.Message.Contains("Entity archetype component data is too large."));

            m_ManagerDebug.CheckInternalConsistency();

            // entities should be unchanged
            Assert.AreEqual(archetype1, m_Manager.GetChunk(entity1).Archetype);
            Assert.AreEqual(archetype2, m_Manager.GetChunk(entity2).Archetype);
            Assert.AreEqual(archetype3, m_Manager.GetChunk(entity3).Archetype);
        }

        [Test]
        public void AddMultipleComponents_WithNativeArray()
        {
            var componentTypes = new ComponentTypeSet(typeof(EcsTestData),
                typeof(EcsTestTag), typeof(EcsTestData2), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp));

            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));

            using(var entities = CollectionHelper.CreateNativeArray<Entity>( new[] { entity1, entity2, entity3 },World.UpdateAllocator.ToAllocator))
            {
                m_Manager.AddComponent(entities, componentTypes);

                m_ManagerDebug.CheckInternalConsistency();

                var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestTag), typeof(EcsTestData2), ComponentType.ChunkComponent<EcsTestData4>(),
                    typeof(EcsTestSharedComp));
                Assert.AreEqual(archetype, m_Manager.GetChunk(entity1).Archetype);
                Assert.AreEqual(archetype, m_Manager.GetChunk(entity2).Archetype);
                Assert.AreEqual(archetype, m_Manager.GetChunk(entity3).Archetype);
            }

        }



        [Test]
        public void RemoveMultipleComponents_WithQuery()
        {
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsTestData), typeof(EcsTestData4), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp) );
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData4), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp) );
            var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData2));

            m_Manager.RemoveComponent(query, new ComponentTypeSet(typeof(EcsTestData2), typeof(EcsTestData4), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp)));

            m_ManagerDebug.CheckInternalConsistency();

            var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTag), typeof(EcsTestData), typeof(EcsTestData4), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp) );
            var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetype3 = m_Manager.CreateArchetype();
            Assert.AreEqual(archetype1, m_Manager.GetChunk(entity1).Archetype);
            Assert.AreEqual(archetype2, m_Manager.GetChunk(entity2).Archetype);
            Assert.AreEqual(archetype3, m_Manager.GetChunk(entity3).Archetype);
        }

        [Test]
        public void RemoveMultipleComponents_WithQuery_SharedComponentValuesPreserved()
        {
            // test for entities of different chunks getting their correct shared values
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            m_Manager.SetSharedComponentManaged(entity1, new EcsTestSharedComp() {value = 5});
            m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp() {value = 9});

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData2));
            m_Manager.RemoveComponent(query, new ComponentTypeSet(typeof(EcsTestData2), typeof(EcsTestData4)));

            m_ManagerDebug.CheckInternalConsistency();

            Assert.AreEqual(5, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity1).value);
            Assert.AreEqual(9, m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity2).value);

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp));
            Assert.AreEqual(archetype, m_Manager.GetChunk(entity1).Archetype);
            Assert.AreEqual(archetype, m_Manager.GetChunk(entity2).Archetype);
        }

        [Test]
        public void RemoveAnyComponent_WithQuery_IgnoresChunksThatDontHaveTheComponent()
        {
            var componentTypes = new ComponentType[]
            {
                typeof(EcsTestTag), typeof(EcsTestData4), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp)
            };

            foreach (var type in componentTypes)
            {
                // We want a clean slate for the m_manager so teardown and setup before the test
                TearDown();
                Setup();

                var metaChunkQuery = m_Manager.CreateEntityQuery(typeof(ChunkHeader));

                var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
                var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
                var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));

                var query1 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());

                m_Manager.AddComponent(query1, type);

                Assert.IsTrue(m_Manager.HasComponent(entity1, type));
                Assert.IsTrue(m_Manager.HasComponent(entity2, type));
                Assert.IsFalse(m_Manager.HasComponent(entity3, type));

                if (type.IsChunkComponent)
                    Assert.AreEqual(2, metaChunkQuery.CalculateEntityCount());

                if (type == ComponentType.ReadWrite<EcsTestSharedComp>())
                {
                    m_Manager.SetSharedComponentManaged(entity1, new EcsTestSharedComp(1));
                    m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp(2));
                }

                m_ManagerDebug.CheckInternalConsistency();

                m_Manager.RemoveComponent(m_Manager.UniversalQuery, type);

                Assert.AreEqual(0, m_Manager.CreateEntityQuery(type).CalculateEntityCount());
            }
        }

        [Test]
        public void RemoveMultipleComponents_WithNativeArray()
        {
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestTag), typeof(EcsTestData), typeof(EcsTestData4), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp) );
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData4), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp) );
            var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));

            using(var entities = CollectionHelper.CreateNativeArray<Entity>( new[] { entity1, entity2, entity3 },World.UpdateAllocator.ToAllocator))
            {

                m_Manager.RemoveComponent(entities, new ComponentTypeSet(typeof(EcsTestData2), typeof(EcsTestData4), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp)));

                m_ManagerDebug.CheckInternalConsistency();

                var archetype1 = m_Manager.CreateArchetype(typeof(EcsTestTag), typeof(EcsTestData));
                var archetype2 = m_Manager.CreateArchetype(typeof(EcsTestData));
                var archetype3 = m_Manager.CreateArchetype();
                Assert.AreEqual(archetype1, m_Manager.GetChunk(entity1).Archetype);
                Assert.AreEqual(archetype2, m_Manager.GetChunk(entity2).Archetype);
                Assert.AreEqual(archetype3, m_Manager.GetChunk(entity3).Archetype);
            }

        }

        uint GetComponentDataVersion<T>(Entity e) where T :
#if UNITY_DISABLE_MANAGED_COMPONENTS
        struct,
#endif
        IComponentData
        {
            var typeHandle = m_Manager.GetComponentTypeHandle<T>(true);
            return m_Manager.GetChunk(e).GetChangeVersion(ref typeHandle);
        }

        uint GetSharedComponentDataVersion<T>(Entity e) where T : struct, ISharedComponentData
        {
            return m_Manager.GetChunk(e).GetChangeVersion(m_Manager.GetSharedComponentTypeHandle<T>());
        }

        [Test]
        public void AddRemoveComponent_WithQuery_PreservesChangeVersions()
        {
            m_ManagerDebug.SetGlobalSystemVersion(10);
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));

            m_ManagerDebug.SetGlobalSystemVersion(20);

            m_Manager.SetComponentData(entity2, new EcsTestData2(7));
            m_Manager.SetComponentData(entity3, new EcsTestData2(8));

            Assert.AreEqual(10, GetComponentDataVersion<EcsTestData>(entity1));
            Assert.AreEqual(10, GetComponentDataVersion<EcsTestData>(entity2));
            Assert.AreEqual(20, GetComponentDataVersion<EcsTestData2>(entity2));
            Assert.AreEqual(20, GetComponentDataVersion<EcsTestData2>(entity3));

            m_ManagerDebug.SetGlobalSystemVersion(30);

            m_Manager.AddSharedComponentManaged(m_Manager.UniversalQuery, new EcsTestSharedComp(1));
            m_ManagerDebug.SetGlobalSystemVersion(40);
            m_Manager.AddComponent(m_Manager.UniversalQuery, typeof(EcsTestTag));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity1));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity2));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity3));

            Assert.AreEqual(40, GetComponentDataVersion<EcsTestTag>(entity1));
            Assert.AreEqual(40, GetComponentDataVersion<EcsTestTag>(entity2));
            Assert.AreEqual(40, GetComponentDataVersion<EcsTestTag>(entity3));

            m_ManagerDebug.SetGlobalSystemVersion(50);

            m_Manager.RemoveComponent(m_Manager.UniversalQuery, typeof(EcsTestSharedComp2));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity1));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity2));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity3));

            m_ManagerDebug.SetGlobalSystemVersion(60);

            m_Manager.RemoveComponent(m_Manager.UniversalQuery, typeof(EcsTestSharedComp));
            Assert.AreEqual(10, GetComponentDataVersion<EcsTestData>(entity1));
            Assert.AreEqual(10, GetComponentDataVersion<EcsTestData>(entity2));
            Assert.AreEqual(20, GetComponentDataVersion<EcsTestData2>(entity2));
            Assert.AreEqual(20, GetComponentDataVersion<EcsTestData2>(entity3));

            m_ManagerDebug.SetGlobalSystemVersion(70);
            m_Manager.AddSharedComponent(m_Manager.UniversalQuery, new EcsTestSharedComp3(1));
            Assert.AreEqual(70, GetSharedComponentDataVersion<EcsTestSharedComp3>(entity1));
            Assert.AreEqual(70, GetSharedComponentDataVersion<EcsTestSharedComp3>(entity2));
            Assert.AreEqual(70, GetSharedComponentDataVersion<EcsTestSharedComp3>(entity3));
            Assert.AreEqual(40, GetComponentDataVersion<EcsTestTag>(entity1));
            Assert.AreEqual(40, GetComponentDataVersion<EcsTestTag>(entity2));
            Assert.AreEqual(40, GetComponentDataVersion<EcsTestTag>(entity3));

        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void AddRemoveChunkComponent_WithQuery_Works_ManagedComponents()
        {
            var metaChunkQuery = m_Manager.CreateEntityQuery(typeof(ChunkHeader));

            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));

            var query1 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());

            m_ManagerDebug.CheckInternalConsistency();
            m_Manager.AddChunkComponentData(query1, new EcsTestManagedComponent() { value = "SomeString" });
            m_ManagerDebug.CheckInternalConsistency();

            Assert.IsTrue(m_Manager.HasComponent(entity1, ComponentType.ChunkComponent<EcsTestManagedComponent>()));
            var val1 = m_Manager.GetChunkComponentData<EcsTestManagedComponent>(entity1).value;
            Assert.AreEqual("SomeString", val1);

            Assert.IsTrue(m_Manager.HasComponent(entity2, ComponentType.ChunkComponent<EcsTestManagedComponent>()));
            var val2 = m_Manager.GetChunkComponentData<EcsTestManagedComponent>(entity2).value;
            Assert.AreEqual("SomeString", val2);

            Assert.IsFalse(m_Manager.HasComponent(entity3, ComponentType.ChunkComponent<EcsTestManagedComponent>()));

            Assert.AreEqual(2, metaChunkQuery.CalculateEntityCount());

            m_ManagerDebug.CheckInternalConsistency();

            var query2 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData2>(), ComponentType.ChunkComponent<EcsTestManagedComponent>());

            m_Manager.RemoveChunkComponentData<EcsTestManagedComponent>(query2);

            Assert.IsFalse(m_Manager.HasComponent(entity2, ComponentType.ChunkComponent<EcsTestManagedComponent>()));

            Assert.AreEqual(1, metaChunkQuery.CalculateEntityCount());

            m_Manager.DestroyEntity(entity1);
            m_Manager.DestroyEntity(entity2);
            m_Manager.DestroyEntity(entity3);
            metaChunkQuery.Dispose();
            query1.Dispose();
            query2.Dispose();
        }

        [Test]
        public void AddRemoveAnyComponent_WithQuery_WorksWithVariousTypes_ManagedComponents()
        {
            var componentTypes = new ComponentType[]
            {
                typeof(EcsTestTag), typeof(EcsTestData4), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp),
                typeof(EcsTestManagedComponent), ComponentType.ChunkComponent<EcsTestManagedComponent>()
            };

            foreach (var type in componentTypes)
            {
                // We want a clean slate for the m_manager so teardown and setup before the test
                TearDown();
                Setup();

                var metaChunkQuery = m_Manager.CreateEntityQuery(typeof(ChunkHeader));

                var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
                var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
                var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));

                var query1 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());

                m_Manager.AddComponent(query1, type);

                Assert.IsTrue(m_Manager.HasComponent(entity1, type));
                Assert.IsTrue(m_Manager.HasComponent(entity2, type));
                Assert.IsFalse(m_Manager.HasComponent(entity3, type));

                if (type.IsChunkComponent)
                    Assert.AreEqual(2, metaChunkQuery.CalculateEntityCount());

                if (type == ComponentType.ReadWrite<EcsTestSharedComp>())
                {
                    m_Manager.SetSharedComponentManaged(entity1, new EcsTestSharedComp(1));
                    m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp(2));
                }

                m_ManagerDebug.CheckInternalConsistency();

                var query2 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData2>(), type);

                m_Manager.RemoveComponent(query2, type);

                Assert.IsFalse(m_Manager.HasComponent(entity2, ComponentType.ChunkComponent<EcsTestData3>()));

                if (type.IsChunkComponent)
                    Assert.AreEqual(1, metaChunkQuery.CalculateEntityCount());
            }
        }

        [Test]
        public void RemoveAnyComponent_WithGroup_IgnoresChunksThatDontHaveTheComponent_ManagedComponents()
        {
            var componentTypes = new ComponentType[]
            {
                typeof(EcsTestTag), typeof(EcsTestData4), ComponentType.ChunkComponent<EcsTestData4>(), typeof(EcsTestSharedComp),
                typeof(EcsTestManagedComponent), ComponentType.ChunkComponent<EcsTestManagedComponent>()
            };

            foreach (var type in componentTypes)
            {
                // We want a clean slate for the m_manager so teardown and setup before the test
                TearDown();
                Setup();

                var metaChunkQuery = m_Manager.CreateEntityQuery(typeof(ChunkHeader));

                var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
                var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
                var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));

                var query1 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());

                m_Manager.AddComponent(query1, type);

                Assert.IsTrue(m_Manager.HasComponent(entity1, type));
                Assert.IsTrue(m_Manager.HasComponent(entity2, type));
                Assert.IsFalse(m_Manager.HasComponent(entity3, type));

                if (type.IsChunkComponent)
                    Assert.AreEqual(2, metaChunkQuery.CalculateEntityCount());

                if (type == ComponentType.ReadWrite<EcsTestSharedComp>())
                {
                    m_Manager.SetSharedComponentManaged(entity1, new EcsTestSharedComp(1));
                    m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp(2));
                }

                m_ManagerDebug.CheckInternalConsistency();

                m_Manager.RemoveComponent(m_Manager.UniversalQuery, type);

                Assert.AreEqual(0, m_Manager.CreateEntityQuery(type).CalculateEntityCount());
            }
        }

        [Test]
        public void AddRemoveComponent_WithQuery_PreservesChangeVersions_ManagedComponents()
        {
            m_ManagerDebug.SetGlobalSystemVersion(10);
            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var entity3 = m_Manager.CreateEntity(typeof(EcsTestData2));
            var entity4 = m_Manager.CreateEntity(typeof(EcsTestData2), typeof(EcsTestManagedComponent));
            var entity5 = m_Manager.CreateEntity(typeof(EcsTestManagedComponent));

            m_ManagerDebug.SetGlobalSystemVersion(20);

            m_Manager.SetComponentData(entity2, new EcsTestData2(7));
            m_Manager.SetComponentData(entity3, new EcsTestData2(8));
            m_Manager.SetComponentData(entity4, new EcsTestData2(9));

            Assert.AreEqual(10, GetComponentDataVersion<EcsTestData>(entity1));
            Assert.AreEqual(10, GetComponentDataVersion<EcsTestData>(entity2));
            Assert.AreEqual(20, GetComponentDataVersion<EcsTestData2>(entity2));
            Assert.AreEqual(20, GetComponentDataVersion<EcsTestData2>(entity3));
            Assert.AreEqual(20, GetComponentDataVersion<EcsTestData2>(entity4));
            Assert.AreEqual(10, GetComponentDataVersion<EcsTestManagedComponent>(entity4));
            Assert.AreEqual(10, GetComponentDataVersion<EcsTestManagedComponent>(entity5));

            m_ManagerDebug.SetGlobalSystemVersion(30);

            m_Manager.AddSharedComponentManaged(m_Manager.UniversalQuery, new EcsTestSharedComp(1));

            m_ManagerDebug.SetGlobalSystemVersion(40);

            m_Manager.AddComponent(m_Manager.UniversalQuery, typeof(EcsTestTag));

            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity1));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity2));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity3));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity4));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity5));

            Assert.AreEqual(40, GetComponentDataVersion<EcsTestTag>(entity1));
            Assert.AreEqual(40, GetComponentDataVersion<EcsTestTag>(entity2));
            Assert.AreEqual(40, GetComponentDataVersion<EcsTestTag>(entity3));
            Assert.AreEqual(40, GetComponentDataVersion<EcsTestTag>(entity4));
            Assert.AreEqual(40, GetComponentDataVersion<EcsTestTag>(entity5));

            m_ManagerDebug.SetGlobalSystemVersion(50);

            m_Manager.RemoveComponent(m_Manager.UniversalQuery, typeof(EcsTestSharedComp2));

            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity1));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity2));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity3));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity4));
            Assert.AreEqual(30, GetSharedComponentDataVersion<EcsTestSharedComp>(entity5));

            m_ManagerDebug.SetGlobalSystemVersion(60);

            m_Manager.RemoveComponent(m_Manager.UniversalQuery, typeof(EcsTestSharedComp));

            Assert.AreEqual(10, GetComponentDataVersion<EcsTestData>(entity1));
            Assert.AreEqual(10, GetComponentDataVersion<EcsTestData>(entity2));
            Assert.AreEqual(20, GetComponentDataVersion<EcsTestData2>(entity2));
            Assert.AreEqual(20, GetComponentDataVersion<EcsTestData2>(entity3));
            Assert.AreEqual(20, GetComponentDataVersion<EcsTestData2>(entity4));
            Assert.AreEqual(10, GetComponentDataVersion<EcsTestManagedComponent>(entity4));
            Assert.AreEqual(10, GetComponentDataVersion<EcsTestManagedComponent>(entity5));
        }

#endif
    }
}
