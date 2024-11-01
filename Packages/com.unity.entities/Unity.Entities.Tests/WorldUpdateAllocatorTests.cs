using System;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Tests
{

#if ENABLE_UNITY_COLLECTIONS_CHECKS

    public class WorldUpdateAllocatorTests : ECSTestsCommonBase
    {
        World m_world;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            m_world = new World("WorldUpdateAllocatorTests", WorldFlags.Simulation);
            var system = m_world.GetOrCreateSystem<WorldUpdateAllocatorResetSystem>();
            var group = m_world.GetOrCreateSystemManaged<InitializationSystemGroup>();
            group.AddSystemToUpdateList(system);
            group.SortSystems();
        }

        [TearDown]
        public override void TearDown()
        {
            m_world.Dispose();
            base.TearDown();
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void UpdateInvalidatesNativeList()
        {
            var container = m_world.UpdateAllocator.AllocateNativeList<byte>(m_world.UpdateAllocator.InitialSizeInBytes / 1000);
            container.Resize(1, NativeArrayOptions.ClearMemory);
            container[0] = 0xFE;
            m_world.Update();
            m_world.Update();
            Assert.Throws<ObjectDisposedException>(() =>
            {
                container[0] = 0xEF;
            });
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void UpdateInvalidatesNativeArray()
        {
            var container = m_world.UpdateAllocator.AllocateNativeArray<byte>(m_world.UpdateAllocator.InitialSizeInBytes / 1000);
            container[0] = 0xFE;
            m_world.Update();
            m_world.Update();
            Assert.Throws<ObjectDisposedException>(() =>
            {
                container[0] = 0xEF;
            });
        }

    [Test]
        public void NativeListCanBeCreatedViaMemberFunction()
        {
            var container = m_world.UpdateAllocator.AllocateNativeList<byte>(m_world.UpdateAllocator.InitialSizeInBytes / 1000);
            container.Resize(1, NativeArrayOptions.ClearMemory);
            container[0] = 0xFE;
        }

        [Test]
        public void NativeListCanBeDisposed()
        {
            var container = m_world.UpdateAllocator.AllocateNativeList<byte>(m_world.UpdateAllocator.InitialSizeInBytes / 1000);
            container.Resize(1, NativeArrayOptions.ClearMemory);
            container[0] = 0xFE;
            container.Dispose();
            m_world.Update();
            m_world.Update();
        }

        [Test]
        public void NativeArrayCanBeDisposed()
        {
            var container = m_world.UpdateAllocator.AllocateNativeArray<byte>(m_world.UpdateAllocator.InitialSizeInBytes / 1000);
            container[0] = 0xFE;
            container.Dispose();
            m_world.Update();
            m_world.Update();
        }

        [Test]
        public void NumberOfBlocksIsTemporarilyStable()
        {
            m_world.UpdateAllocator.AllocateNativeList<byte>(m_world.UpdateAllocator.InitialSizeInBytes * 10);
            var blocksBefore = m_world.UpdateAllocator.BlocksAllocated;
            m_world.Update();
            m_world.Update();
            var blocksAfter = m_world.UpdateAllocator.BlocksAllocated;
            Assert.AreEqual(blocksAfter, blocksBefore);
        }

        [Test]
        public void NumberOfBlocksEventuallyDrops()
        {
            m_world.UpdateAllocator.AllocateNativeList<byte>(m_world.UpdateAllocator.InitialSizeInBytes * 10);
            var blocksBefore = m_world.UpdateAllocator.BlocksAllocated;
            m_world.Update();
            m_world.Update();
            m_world.Update();
            m_world.Update();
            var blocksAfter = m_world.UpdateAllocator.BlocksAllocated;
            Assert.IsTrue(blocksAfter < blocksBefore);
        }

        [Test]
        public void PossibleToAllocateGigabytes()
        {
            const int giga = 1024 * 1024 * 1024;
            var container0 = m_world.UpdateAllocator.AllocateNativeList<byte>(giga);
            var container1 = m_world.UpdateAllocator.AllocateNativeList<byte>(giga);
            var container2 = m_world.UpdateAllocator.AllocateNativeList<byte>(giga);
            container0.Resize(1, NativeArrayOptions.ClearMemory);
            container1.Resize(1, NativeArrayOptions.ClearMemory);
            container2.Resize(1, NativeArrayOptions.ClearMemory);
            container0[0] = 0;
            container1[0] = 1;
            container2[0] = 2;
            Assert.AreEqual((byte)0, container0[0]);
            Assert.AreEqual((byte)1, container1[0]);
            Assert.AreEqual((byte)2, container2[0]);
        }

        [Test]
        public void ExhaustsFirstBlockBeforeAllocatingMore()
        {
            for(var i = 0; i < 50; ++i)
            {
                m_world.UpdateAllocator.AllocateNativeList<byte>(m_world.UpdateAllocator.InitialSizeInBytes / 100);
                Assert.AreEqual(1, m_world.UpdateAllocator.BlocksAllocated);
            }
            m_world.UpdateAllocator.AllocateNativeList<byte>(m_world.UpdateAllocator.InitialSizeInBytes);
            Assert.AreEqual(2, m_world.UpdateAllocator.BlocksAllocated);
        }

        [Test]
        public void PossibleToAllocateAfterAFewUpdates()
        {
            var capacity = m_world.UpdateAllocator.InitialSizeInBytes / 2;
            m_world.UpdateAllocator.AllocateNativeList<byte>(capacity);
            m_world.Update();
            m_world.UpdateAllocator.AllocateNativeList<byte>(capacity);
            m_world.Update();
            m_world.UpdateAllocator.AllocateNativeList<byte>(capacity);
            m_world.Update();
            m_world.UpdateAllocator.AllocateNativeList<byte>(capacity);
            m_world.Update();
        }


        [Test]
        public unsafe void WorldUpdateAllocatorNoOverflow()
        {
            var worldName = "WorldUpdateAllocatorNoOverflowTests0";
            var local_world0 = new World(worldName, WorldFlags.Simulation);
            var allocatorHandle0 = local_world0.UpdateAllocator.Handle.Value;
            local_world0.Dispose();

            // Check wether world update allocator handle value is re-used after world dispose.
            for (int i = 1; i < 5; i++)
            {
                worldName = "WorldUpdateAllocatorNoOverflowTests" + i;
                using (var local_world = new World(worldName, WorldFlags.Simulation))
                {
                    Assert.AreEqual(allocatorHandle0, local_world.UpdateAllocator.Handle.Value);
                }
            }
        }
    }

#endif

}
