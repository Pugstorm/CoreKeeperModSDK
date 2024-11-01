using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Editor.Tests
{
    unsafe class ComponentDataDifferTests
    {
        World m_World;
        NativeList<Entity> m_NewEntities;
        NativeList<Entity> m_MissingEntities;
        NativeList<byte> m_Storage;
        EntityDiffer m_EntityDiffer;
        ComponentDataDiffer m_ChunkDiffer;

        protected World World => m_World;

        [SetUp]
        public void Setup()
        {
            m_World = new World("TestWorld");
            m_NewEntities = new NativeList<Entity>(m_World.UpdateAllocator.ToAllocator);
            m_MissingEntities = new NativeList<Entity>(m_World.UpdateAllocator.ToAllocator);
            m_Storage = new NativeList<byte>(m_World.UpdateAllocator.ToAllocator);
            m_EntityDiffer = new EntityDiffer(m_World);
            var ecs = m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
            m_ChunkDiffer = new ComponentDataDiffer(ecs, typeof(EcsTestData));
        }

        [TearDown]
        public void TearDown()
        {
            m_NewEntities.Dispose();
            m_Storage.Dispose();
            m_MissingEntities.Dispose();
            m_EntityDiffer.Dispose();
            m_ChunkDiffer.Dispose();
            m_World.Dispose();
        }

        [Test]
        public void ComponentDataDiffer_Simple()
        {
            var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle);
            jobHandle.Complete();

            Assert.That(result.AddedComponentCount, Is.EqualTo(0));
            Assert.That(result.RemovedComponentCount, Is.EqualTo(0));

            result.Dispose();
        }

        [Test]
        public unsafe void ComponentDataDiffer_DetectNewEmptyEntityInArchetype()
        {
            var entityA = CreateEntity(new EcsTestData { value = 12 });

            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();

                Assert.That(result.AddedComponentCount, Is.EqualTo(1));
                Assert.That(result.RemovedComponentCount, Is.EqualTo(0));

                Assert.That(result.GetAddedComponent<EcsTestData>(0), Is.EqualTo((entityA, new EcsTestData { value = 12 })));
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            var entityB = m_World.EntityManager.CreateEntity(typeof(EcsTestData));
            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();

                Assert.That(result.AddedComponentCount, Is.EqualTo(1));
                Assert.That(result.RemovedComponentCount, Is.EqualTo(0));

                Assert.That(result.GetAddedComponent<EcsTestData>(0), Is.EqualTo((entityB, default(EcsTestData))));
            }
        }

        [Test]
        public unsafe void ComponentDataDiffer_DetectMissingComponentWhenEntityDestroyed()
        {
            var entityA = CreateEntity(new EcsTestData { value = 12 });
            var entityB = CreateEntity(new EcsTestData { value = 12 });

            using (m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.DestroyEntity(entityB);

            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();

                Assert.That(result.AddedComponentCount, Is.EqualTo(0));
                Assert.That(result.RemovedComponentCount, Is.EqualTo(1));
            }
        }

        [Test]
        public unsafe void ComponentDataDiffer_DetectNewAndMissing()
        {
            var entityA = CreateEntity(new EcsTestData { value = 12 });

            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();

                Assert.That(result.AddedComponentCount, Is.EqualTo(1));
                Assert.That(result.RemovedComponentCount, Is.EqualTo(0));

                Assert.That(result.GetAddedComponent<EcsTestData>(0), Is.EqualTo((entityA, new EcsTestData { value = 12 })));
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            var entityB = CreateEntity(new EcsTestData { value = 22 });
            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();

                Assert.That(result.AddedComponentCount, Is.EqualTo(1));
                Assert.That(result.RemovedComponentCount, Is.EqualTo(0));

                Assert.That(result.GetAddedComponent<EcsTestData>(0), Is.EqualTo((entityB, new EcsTestData { value = 22 })));
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.DestroyEntity(entityA);
            var entityC = CreateEntity(new EcsTestData { value = 32 });

            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();

                Assert.That(result.AddedComponentCount, Is.EqualTo(2));
                Assert.That(result.RemovedComponentCount, Is.EqualTo(2));

                Assert.That(result.GetAddedComponent<EcsTestData>(0), Is.EqualTo((entityB, new EcsTestData { value = 22 })));
                Assert.That(result.GetAddedComponent<EcsTestData>(1), Is.EqualTo((entityC, new EcsTestData { value = 32 })));
                Assert.That(result.GetRemovedComponent<EcsTestData>(0), Is.EqualTo((entityA, new EcsTestData { value = 12 })));
                Assert.That(result.GetRemovedComponent<EcsTestData>(1), Is.EqualTo((entityB, new EcsTestData { value = 22 })));
            }
        }

        [Test]
        public unsafe void ComponentDataDiffer_DetectChangedAsNewAndRemoved()
        {
            var entityA = CreateEntity(new EcsTestData { value = 12 });
            var entityB = CreateEntity(new EcsTestData { value = 22 });

            using (m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.SetComponentData(entityA, new EcsTestData { value = 32 });

            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
                Assert.That(result.AddedComponentCount, Is.EqualTo(1));
                Assert.That(result.RemovedComponentCount, Is.EqualTo(1));

                Assert.That(result.GetAddedComponent<EcsTestData>(0), Is.EqualTo((entityA, new EcsTestData { value = 32 })));
                Assert.That(result.GetRemovedComponent<EcsTestData>(0), Is.EqualTo((entityA, new EcsTestData { value = 12 })));
            }
        }

        [Test]
        public unsafe void ComponentDataDiffer_ResultShouldntBeInterlaced()
        {
            var entityA = CreateEntity(new EcsTestData { value = 12 });
            var entityB = CreateEntity(new EcsTestData { value = 22 });
            m_World.EntityManager.AddSharedComponentManaged(entityA, new EcsTestSharedComp { value = 1 });
            m_World.EntityManager.AddSharedComponentManaged(entityB, new EcsTestSharedComp { value = 1 });
            var entityC = CreateEntity(new EcsTestData { value = 32 });
            m_World.EntityManager.AddSharedComponentManaged(entityC, new EcsTestSharedComp { value = 2 });

            using (m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.RemoveComponent<EcsTestData>(entityB);
            Assert.That(m_World.EntityManager.HasComponent<EcsTestData>(entityB), Is.False);
            var entityBbis = CreateEntity(new EcsTestData { value = 52 });
            m_World.EntityManager.AddSharedComponentManaged(entityBbis, new EcsTestSharedComp { value = 1 });
            var entityD = CreateEntity(new EcsTestData { value = 42 });
            m_World.EntityManager.AddSharedComponentManaged(entityD, new EcsTestSharedComp { value = 2 });

            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
                Assert.That(result.AddedComponentCount, Is.EqualTo(2));
                Assert.That(result.RemovedComponentCount, Is.EqualTo(1));

                Assert.That(result.GetAddedComponent<EcsTestData>(0), Is.EqualTo((entityBbis, new EcsTestData { value = 52 })));
                Assert.That(result.GetAddedComponent<EcsTestData>(1), Is.EqualTo((entityD, new EcsTestData { value = 42 })));
                Assert.That(result.GetRemovedComponent<EcsTestData>(0), Is.EqualTo((entityB, new EcsTestData { value = 22 })));
            }
        }

        [Test]
        public unsafe void ComponentDataDiffer_ExtractSimpleResults()
        {
            var entities = Enumerable.Range(1, 10).Select(i =>
            {
                var data = new EcsTestData { value = i };
                return (e:CreateEntity(data), data);
            }).ToArray();
            using (m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            var expectedEntitiesWithRemovedComponents = new List<(Entity e, EcsTestData previous, EcsTestData current)>();
            for (var i = 0; i < entities.Length; i++)
            {
                if (i % 2 != 0)
                    continue;

                var current = new EcsTestData { value = i * 100 };
                m_World.EntityManager.SetComponentData(entities[i].e, current);
                expectedEntitiesWithRemovedComponents.Add((entities[i].e, entities[i].data, current));
            }

            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
                Assert.That(result.AddedComponentCount, Is.EqualTo(expectedEntitiesWithRemovedComponents.Count));
                Assert.That(result.RemovedComponentCount, Is.EqualTo(expectedEntitiesWithRemovedComponents.Count));

                var removedComponents = result.GetRemovedComponents<EcsTestData>(World.UpdateAllocator.ToAllocator);
                var addedComponents = result.GetAddedComponents<EcsTestData>(World.UpdateAllocator.ToAllocator);
                using var entitiesWithRemovedComponents = result.GetEntitiesWithRemovedComponents<EcsTestData>(World.UpdateAllocator.ToAllocator);
                try
                {
                    Assert.That(removedComponents.entities.ToArray(), Is.EquivalentTo(expectedEntitiesWithRemovedComponents.Select(x => x.e)));
                    Assert.That(addedComponents.entities.ToArray(), Is.EquivalentTo(expectedEntitiesWithRemovedComponents.Select(x => x.e)));
                    Assert.That(entitiesWithRemovedComponents.ToArray(), Is.EquivalentTo(expectedEntitiesWithRemovedComponents.Select(x => x.e)));
                    Assert.That(removedComponents.componentData.ToArray(), Is.EquivalentTo(expectedEntitiesWithRemovedComponents.Select(x => x.previous)));
                    Assert.That(addedComponents.componentData.ToArray(), Is.EquivalentTo(expectedEntitiesWithRemovedComponents.Select(x => x.current)));
                }
                finally
                {
                    removedComponents.entities.Dispose();
                    removedComponents.componentData.Dispose();
                    addedComponents.entities.Dispose();
                    addedComponents.componentData.Dispose();
                }
            }
        }

        [Test]
        public void ComponentDataDiffer_DetectMissingChunk()
        {
            var entityA = CreateEntity(new EcsTestData { value = 12 });
            var entityInChunk = m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->GetEntityInChunk(entityA);
            Assert.That(entityInChunk.Chunk != ChunkIndex.Null);

            using (m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.DestroyEntity(entityA);
            entityInChunk = m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->GetEntityInChunk(entityA);

            Assert.That(entityInChunk.Chunk == ChunkIndex.Null);

            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
                Assert.That(result.AddedComponentCount, Is.EqualTo(0));
                Assert.That(result.RemovedComponentCount, Is.EqualTo(1));

                Assert.That(result.GetRemovedComponent<EcsTestData>(0), Is.EqualTo((entityA, new EcsTestData { value = 12 })));
            }
        }

        [Test]
        public void ComponentDataDiffer_DetectChangingQuery()
        {
            using (var customQuery = m_World.EntityManager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2)))
            {
                var entityA = CreateEntity(new EcsTestData { value = 12 });
                var entityB = CreateEntity(new EcsTestData { value = 22 });
                m_World.EntityManager.AddComponentData(entityB, new EcsTestData2 { value0 = 32, value1 = 42 });

                using (var result = m_ChunkDiffer.GatherComponentChangesAsync(customQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
                {
                    jobHandle.Complete();
                    Assert.That(result.AddedComponentCount, Is.EqualTo(1));
                    Assert.That(result.RemovedComponentCount, Is.EqualTo(0));

                    Assert.That(result.GetAddedComponent<EcsTestData>(0), Is.EqualTo((entityB, new EcsTestData { value = 22 })));
                }

                using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
                {
                    jobHandle.Complete();
                    Assert.That(result.AddedComponentCount, Is.EqualTo(1));
                    Assert.That(result.RemovedComponentCount, Is.EqualTo(0));

                    Assert.That(result.GetAddedComponent<EcsTestData>(0), Is.EqualTo((entityA, new EcsTestData { value = 12 })));
                }

                {
                    var result = m_ChunkDiffer.GatherComponentChangesAsync(customQuery, World.UpdateAllocator.ToAllocator, out var jobHandle);
                    jobHandle.Complete();
                    Assert.That(result.AddedComponentCount, Is.EqualTo(0));
                    Assert.That(result.RemovedComponentCount, Is.EqualTo(1));

                    Assert.That(result.GetRemovedComponent<EcsTestData>(0), Is.EqualTo((entityA, new EcsTestData { value = 12 })));
                    result.Dispose();
                }
            }
        }

        [Test]
        public unsafe void ComponentDataDiffer_MovedEntityWithinChunkDetectedAsRemovedAndAdded()
        {
            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            var entity1 = CreateEntity(new EcsTestData { value = 1 });
            var entity2 = CreateEntity(new EcsTestData { value = 2 });
            using (m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.DestroyEntity(entity1);
            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();

                Assert.That(result.RemovedComponentCount, Is.EqualTo(2));
                Assert.That(result.AddedComponentCount, Is.EqualTo(1));

                // entity1 is detected as removed because it's been deleted
                Assert.That(result.GetRemovedComponent<EcsTestData>(0), Is.EqualTo((entity1, new EcsTestData { value = 1 })));
                // entity2 is detected as removed because it was removed from position 1
                Assert.That(result.GetRemovedComponent<EcsTestData>(1), Is.EqualTo((entity2, new EcsTestData { value = 2 })));
                // entity2 is detected as re-added because it was added at position 0
                Assert.That(result.GetAddedComponent<EcsTestData>(0), Is.EqualTo((entity2, new EcsTestData { value = 2 })));
            }
        }

        [Test]
        public unsafe void ComponentDataDiffer_MovedEntityBetweenChunksDetectedAsRemovedAndAdded()
        {
            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();

            var archetypeA = m_World.EntityManager.CreateArchetype(typeof(EcsTestData), typeof(CompA));
            var archetypeB = m_World.EntityManager.CreateArchetype(typeof(EcsTestData), typeof(CompB));
            var entityA1 = m_World.EntityManager.CreateEntity(archetypeA);
            var entityA2 = m_World.EntityManager.CreateEntity(archetypeA);
            var entityB1 = m_World.EntityManager.CreateEntity(archetypeB);
            m_World.EntityManager.SetComponentData(entityA1, new EcsTestData { value = 1 });
            m_World.EntityManager.SetComponentData(entityA2, new EcsTestData { value = 2 });
            m_World.EntityManager.SetComponentData(entityB1, new EcsTestData { value = 1 });
            using (m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            // Move entityA2 from archetype A to archetype B to provoke a change of chunk
            m_World.EntityManager.RemoveComponent<CompA>(entityA2);
            m_World.EntityManager.AddComponent<CompB>(entityA2);

            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
                Assert.That(result.RemovedComponentCount, Is.EqualTo(1));
                Assert.That(result.AddedComponentCount, Is.EqualTo(1));

                Assert.That(result.GetRemovedComponent<EcsTestData>(0), Is.EqualTo((entityA2, new EcsTestData { value = 2 })));
                Assert.That(result.GetAddedComponent<EcsTestData>(0), Is.EqualTo((entityA2, new EcsTestData { value = 2 })));
            }
        }

        [Test]
        public unsafe void ComponentDataDiffer_DetectNewEntityWithSameComponentData()
        {
            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            var entityA = CreateEntity(new EcsTestData { value = 1 });
            var entityB = CreateEntity(new EcsTestData { value = 1 });
            using (m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.DestroyEntity(entityB);
            var entityC = CreateEntity(new EcsTestData { value = 1 });
            using (var result = m_ChunkDiffer.GatherComponentChangesAsync(m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var jobHandle))
            {
                jobHandle.Complete();
                Assert.That(result.AddedComponentCount, Is.EqualTo(1));
                Assert.That(result.RemovedComponentCount, Is.EqualTo(1));

                Assert.That(result.GetRemovedComponent<EcsTestData>(0), Is.EqualTo((entityB, new EcsTestData { value = 1 })));
                Assert.That(result.GetAddedComponent<EcsTestData>(0), Is.EqualTo((entityC, new EcsTestData { value = 1 })));
            }
        }

        [Test]
        public void ComponentDataDiffer_CheckIfDifferCanWatchType()
        {
            Assert.That(ComponentDataDiffer.CanWatch(typeof(EcsTestData)), Is.True);
            Assert.That(ComponentDataDiffer.CanWatch(typeof(EcsTestSharedComp)), Is.False);
            Assert.That(ComponentDataDiffer.CanWatch(typeof(Entity)), Is.False);
        }

        struct CompA : IComponentData
        {
#pragma warning disable 649
            public int Value;
#pragma warning restore 649
        }

        struct CompB : IComponentData
        {
#pragma warning disable 649
            public int Value;
#pragma warning restore 649
        }

        Entity CreateEntity<T>(T data) where T : unmanaged, IComponentData
        {
            var e = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddComponentData(e, data);

            return e;
        }
    }
}
