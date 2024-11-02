using NUnit.Framework;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    class UnmanagedSharedComponentDataDifferTests
    {
        World m_World;
        UnmanagedSharedComponentDataDiffer m_Differ;

        protected World World => m_World;

        [SetUp]
        public void Setup()
        {
            m_World = new World("TestWorld");
            m_Differ = new UnmanagedSharedComponentDataDiffer(typeof(EcsTestSharedComp));
        }

        [TearDown]
        public void TearDown()
        {
            m_World.Dispose();
            m_Differ.Dispose();
        }

        [Test]
        public void UnmanagedSharedComponentDataDiffer_Simple()
        {
            using var result = m_Differ.GatherComponentChangesAsync(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator, out var handle);
            handle.Complete();
            Assert.That(result.AddedSharedComponentCount, Is.EqualTo(0));
            Assert.That(result.RemovedSharedComponentCount, Is.EqualTo(0));
        }

        [Test]
        public unsafe void UnmanagedSharedComponentDataDiffer_DetectMissingEntityWhenDestroyed()
        {
            var entityA = m_World.EntityManager.CreateEntity();
            var entityB = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityA, new EcsTestSharedComp { value = 1 });
            m_World.EntityManager.AddSharedComponentManaged(entityB, new EcsTestSharedComp { value = 1 });
            m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator).Dispose();

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.DestroyEntity(entityB);

            using var result = m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator);
            Assert.That(result.AddedSharedComponentCount, Is.EqualTo(0));
            Assert.That(result.RemovedSharedComponentCount, Is.EqualTo(1));
            Assert.That(result.GetRemovedSharedComponent<EcsTestSharedComp>(0), Is.EqualTo((entityB, new EcsTestSharedComp { value = 1 })));
        }

        [Test]
        public unsafe void UnmanagedSharedComponentDataDiffer_DetectReplacedChunk()
        {
            var entityA = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityA, new EcsTestSharedComp { value = 1 });
            using (var result = m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator))
            {
                Assert.That(result.AddedSharedComponentCount, Is.EqualTo(1));
                Assert.That(result.RemovedSharedComponentCount, Is.EqualTo(0));
                Assert.That(result.GetAddedSharedComponent<EcsTestSharedComp>(0), Is.EqualTo((entityA, new EcsTestSharedComp { value = 1 })));
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.SetSharedComponentManaged(entityA, new EcsTestSharedComp { value = 2 });
            using (var result = m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator))
            {
                Assert.That(result.AddedSharedComponentCount, Is.EqualTo(1));
                Assert.That(result.RemovedSharedComponentCount, Is.EqualTo(1));
                Assert.That(result.GetAddedSharedComponent<EcsTestSharedComp>(0), Is.EqualTo((entityA, new EcsTestSharedComp { value = 2 })));
                Assert.That(result.GetRemovedSharedComponent<EcsTestSharedComp>(0), Is.EqualTo((entityA, new EcsTestSharedComp { value = 1 })));
            }
        }

        [Test]
        public void UnmanagedSharedComponentDataDiffer_DetectNewEntity()
        {
            var entityA = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityA, new EcsTestSharedComp { value = 1 });
            var entityB = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityB, new EcsTestSharedComp { value = 2 });

            using var result = m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator);
            Assert.That(result.AddedSharedComponentCount, Is.EqualTo(2));
            Assert.That(result.RemovedSharedComponentCount, Is.EqualTo(0));
            Assert.That(result.GetAddedSharedComponent<EcsTestSharedComp>(0), Is.EqualTo((entityA, new EcsTestSharedComp { value = 1 })));
            Assert.That(result.GetAddedSharedComponent<EcsTestSharedComp>(1), Is.EqualTo((entityB, new EcsTestSharedComp { value = 2 })));
        }

        [Test]
        public void UnmanagedSharedComponentDataDiffer_DetectEntityOnDefaultComponentValue()
        {
            var archetype = m_World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp));
            var entityA = m_World.EntityManager.CreateEntity(archetype);

            using var result = m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator);
            Assert.That(result.AddedSharedComponentCount, Is.EqualTo(1));
            Assert.That(result.RemovedSharedComponentCount, Is.EqualTo(0));
            Assert.That(result.GetAddedSharedComponent<EcsTestSharedComp>(0), Is.EqualTo((entityA, new EcsTestSharedComp { value = 0 })));
        }

        [Test]
        public unsafe void UnmanagedSharedComponentDataDiffer_DetectNewAndMissingEntityInExistingChunk()
        {
            var entityA = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityA, new EcsTestSharedComp { value = 1 });
            var entityB = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityB, new EcsTestSharedComp { value = 1 });

            m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator).Dispose();

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.RemoveComponent<EcsTestSharedComp>(entityB);
            using (var result = m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator))
            {
                Assert.That(result.AddedSharedComponentCount, Is.EqualTo(0));
                Assert.That(result.RemovedSharedComponentCount, Is.EqualTo(1));
                Assert.That(result.GetRemovedSharedComponent<EcsTestSharedComp>(0), Is.EqualTo((entityB, new EcsTestSharedComp { value = 1 })));
            }

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            var entityC = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityC, new EcsTestSharedComp { value = 1 });
            using (var result = m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator))
            {
                Assert.That(result.AddedSharedComponentCount, Is.EqualTo(1));
                Assert.That(result.RemovedSharedComponentCount, Is.EqualTo(0));
                Assert.That(result.GetAddedSharedComponent<EcsTestSharedComp>(0), Is.EqualTo((entityC, new EcsTestSharedComp { value = 1 })));
            }
        }

        [Test]
        public unsafe void UnmanagedSharedComponentDataDiffer_DetectMovedEntitiesAsNewAndRemoved()
        {
            var entityA = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityA, new EcsTestSharedComp { value = 1 });
            var entityB = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityB, new EcsTestSharedComp { value = 1 });

            m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator).Dispose();

            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.RemoveComponent<EcsTestSharedComp>(entityA);
            var entityC = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityC, new EcsTestSharedComp { value = 1 });

            using var result = m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator);
            Assert.That(result.AddedSharedComponentCount, Is.EqualTo(2));
            Assert.That(result.RemovedSharedComponentCount, Is.EqualTo(2));

            Assert.That(result.GetAddedSharedComponent<EcsTestSharedComp>(0), Is.EqualTo((entityB, new EcsTestSharedComp { value = 1 })));
            Assert.That(result.GetAddedSharedComponent<EcsTestSharedComp>(1), Is.EqualTo((entityC, new EcsTestSharedComp { value = 1 })));

            Assert.That(result.GetRemovedSharedComponent<EcsTestSharedComp>(0), Is.EqualTo((entityA, new EcsTestSharedComp { value = 1 })));
            Assert.That(result.GetRemovedSharedComponent<EcsTestSharedComp>(1), Is.EqualTo((entityB, new EcsTestSharedComp { value = 1 })));
        }

        [Test]
        public unsafe void UnmanagedSharedComponentDataDiffer_DetectMissingChunk([Values(10, 100, 129, 500, 1000)] int entityCount)
        {
            var entityA = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityA, new EcsTestSharedComp { value = 1 });

            var entities = new NativeArray<Entity>(entityCount, Allocator.Persistent);
            try
            {
                for (var i = 0; i < entityCount; i++)
                {
                    var e = m_World.EntityManager.CreateEntity();
                    m_World.EntityManager.AddSharedComponentManaged(e, new EcsTestSharedComp { value = 2 });
                    entities[i] = e;
                }

                m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator).Dispose();

                m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
                m_World.EntityManager.DestroyEntity(entities);
                using var result = m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator);
                Assert.That(result.AddedSharedComponentCount, Is.EqualTo(0));

                Assert.That(result.RemovedSharedComponentCount, Is.EqualTo(entityCount));
                var results = new (Entity entity, EcsTestSharedComp comp)[entityCount];
                for (var i = 0; i < entityCount; i++)
                {
                    results[i] = result.GetRemovedSharedComponent<EcsTestSharedComp>(i);
                }

                Assert.That(results.Select(p => p.entity).ToArray(), Is.EquivalentTo(entities.ToArray()));
                Assert.That(results.Select(p => p.comp).ToArray(), Is.EquivalentTo(Enumerable.Repeat(new EcsTestSharedComp { value = 2 }, entityCount).ToArray()));
            }
            finally
            {
                entities.Dispose();
            }
        }

        [Test]
        public unsafe void UnmanagedSharedComponentDataDiffer_DetectEntityMovingFromOneChunkToAnother()
        {
            var entityA = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityA, new EcsTestSharedComp { value = 1 });
            var entityB = m_World.EntityManager.CreateEntity();
            m_World.EntityManager.AddSharedComponentManaged(entityB, new EcsTestSharedComp { value = 2 });

            m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator).Dispose();
            m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->IncrementGlobalSystemVersion();
            m_World.EntityManager.SetSharedComponentManaged(entityB, new EcsTestSharedComp { value = 1 });
            using var result = m_Differ.GatherComponentChanges(m_World.EntityManager, m_World.EntityManager.UniversalQuery, World.UpdateAllocator.ToAllocator);
            Assert.That(result.AddedSharedComponentCount, Is.EqualTo(1));
            Assert.That(result.RemovedSharedComponentCount, Is.EqualTo(1));
            Assert.That(result.GetAddedSharedComponent<EcsTestSharedComp>(0), Is.EqualTo((entityB, new EcsTestSharedComp { value = 1 })));
            Assert.That(result.GetRemovedSharedComponent<EcsTestSharedComp>(0), Is.EqualTo((entityB, new EcsTestSharedComp { value = 2 })));
        }

        [Test]
        public void UnmanagedSharedComponentDataDiffer_CheckIfDifferCanWatchType()
        {
            Assert.That(UnmanagedSharedComponentDataDiffer.CanWatch(typeof(EcsTestData)), Is.False);
            Assert.That(UnmanagedSharedComponentDataDiffer.CanWatch(typeof(EcsTestSharedComp)), Is.True);
            Assert.That(UnmanagedSharedComponentDataDiffer.CanWatch(typeof(Entity)), Is.False);
        }

        struct OtherSharedComponent : ISharedComponentData
        {
#pragma warning disable 649
            public int SomethingElse;
#pragma warning restore 649
        }
    }
}
