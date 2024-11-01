using System;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

namespace Unity.Entities.Tests
{
    struct SharedData1 : ISharedComponentData
    {
        public int value;

        public SharedData1(int val) { value = val; }
    }

    struct SharedData2 : ISharedComponentData
    {
        public int value;

        public SharedData2(int val) { value = val; }
    }

    struct SharedData3 : ISharedComponentData
    {
        public int value;

        public SharedData3(int val) { value = val; }
    }

    struct SharedData4 : ISharedComponentData
    {
        public int value;

        public SharedData4(int val) { value = val; }
    }

    struct SharedData5 : ISharedComponentData
    {
        public int value;

        public SharedData5(int val) { value = val; }
    }

    struct SharedData6 : ISharedComponentData
    {
        public int value;

        public SharedData6(int val) { value = val; }
    }

    struct SharedData7 : ISharedComponentData
    {
        public int value;

        public SharedData7(int val) { value = val; }
    }

    struct SharedData8 : ISharedComponentData
    {
        public int value;

        public SharedData8(int val) { value = val; }
    }

    struct SharedData9 : ISharedComponentData
    {
        public int value;

        public SharedData9(int val) { value = val; }
    }

    struct SharedData10 : ISharedComponentData
    {
        public int value;

        public SharedData10(int val) { value = val; }
    }

    struct SharedData11 : ISharedComponentData
    {
        public int value;

        public SharedData11(int val) { value = val; }
    }

    struct SharedData12 : ISharedComponentData
    {
        public int value;

        public SharedData12(int val) { value = val; }
    }

    struct SharedData13 : ISharedComponentData
    {
        public int value;

        public SharedData13(int val) { value = val; }
    }

    struct SharedData14 : ISharedComponentData
    {
        public int value;

        public SharedData14(int val) { value = val; }
    }

    struct SharedData15 : ISharedComponentData
    {
        public int value;

        public SharedData15(int val) { value = val; }
    }

    struct SharedData16 : ISharedComponentData
    {
        public int value;

        public SharedData16(int val) { value = val; }
    }

    struct SharedData17 : ISharedComponentData
    {
        public int value;

        public SharedData17(int val) { value = val; }
    }

    unsafe struct SharedDataRefCounter : ISharedComponentData, IRefCounted, IEquatable<SharedDataRefCounter>
    {
        public int Value;
        public int RefCounter => *_refCounter;
        private readonly int* _refCounter;

        public SharedDataRefCounter(int value, int* refCounter)
        {
            Value = value;
            _refCounter = refCounter;
        }

        public void Retain()
        {
            ++*_refCounter;
        }

        public void Release()
        {
            --*_refCounter;
        }

        public bool Equals(SharedDataRefCounter other)
        {
            return Value == other.Value && _refCounter == other._refCounter;
        }

        public override bool Equals(object obj)
        {
            return obj is SharedDataRefCounter other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Value;
                hashCode = (hashCode * 397) ^ unchecked((int) (long) _refCounter);
                return hashCode;
            }
        }
    }

    struct ManagedSharedData1 : ISharedComponentData, IEquatable<ManagedSharedData1>
    {
        public Tuple<int, int> value;

        public ManagedSharedData1(Tuple<int, int> val)
        {
            value = val;
        }

        public ManagedSharedData1(int val)
        {
            value = new Tuple<int, int>(val, val);
        }

        public bool Equals(ManagedSharedData1 other)
        {
            return Equals(value, other.value);
        }

        public override bool Equals(object obj)
        {
            return obj is ManagedSharedData1 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (value != null ? value.GetHashCode() : 0);
        }
    }
    struct ManagedSharedData2 : ISharedComponentData, IEquatable<ManagedSharedData2>
    {
        public int value;
        private string _forceManaged;

        public ManagedSharedData2(int val)
        {
            value = val;
            _forceManaged = null;
        }

        public bool Equals(ManagedSharedData2 other)
        {
            return value == other.value && _forceManaged == other._forceManaged;
        }

        public override bool Equals(object obj)
        {
            return obj is ManagedSharedData2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (value * 397) ^ (_forceManaged != null ? _forceManaged.GetHashCode() : 0);
            }
        }
    }

    [BurstCompile]
    class SharedComponentDataTests : ECSTestsFixture
    {
        //@TODO: No tests for invalid shared components / destroyed shared component data
        //@TODO: No tests for if we leak shared data when last entity is destroyed...
        //@TODO: No tests for invalid shared component type?

        [Test]
        public void SetSharedComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData), typeof(SharedData2));

            var group1 = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(SharedData1));
            var group2 = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(SharedData2));
            var group12 = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(SharedData2), typeof(SharedData1));

            var group1_filter_0 = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(SharedData1));
            group1_filter_0.SetSharedComponentFilterManaged(new SharedData1(0));
            var group1_filter_20 = m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(SharedData1));
            group1_filter_20.SetSharedComponentFilterManaged(new SharedData1(20));

            Assert.AreEqual(0, group1.CalculateEntityCount());
            Assert.AreEqual(0, group2.CalculateEntityCount());
            Assert.AreEqual(0, group12.CalculateEntityCount());

            Assert.AreEqual(0, group1_filter_0.CalculateEntityCount());
            Assert.AreEqual(0, group1_filter_20.CalculateEntityCount());

            Entity e1 = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponentData(e1, new EcsTestData(117));
            Entity e2 = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponentData(e2, new EcsTestData(243));

            var group1_filter0_data = group1_filter_0.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);

            Assert.AreEqual(2, group1_filter_0.CalculateEntityCount());
            Assert.AreEqual(0, group1_filter_20.CalculateEntityCount());
            Assert.AreEqual(117, group1_filter0_data[0].value);
            Assert.AreEqual(243, group1_filter0_data[1].value);

            m_Manager.SetSharedComponentManaged(e1, new SharedData1(20));

            group1_filter0_data = group1_filter_0.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            var group1_filter20_data = group1_filter_20.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);

            Assert.AreEqual(1, group1_filter_0.CalculateEntityCount());
            Assert.AreEqual(1, group1_filter_20.CalculateEntityCount());
            Assert.AreEqual(117, group1_filter20_data[0].value);
            Assert.AreEqual(243, group1_filter0_data[0].value);

            m_Manager.SetSharedComponentManaged(e2, new SharedData1(20));

            group1_filter20_data = group1_filter_20.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);

            Assert.AreEqual(0, group1_filter_0.CalculateEntityCount());
            Assert.AreEqual(2, group1_filter_20.CalculateEntityCount());
            Assert.AreEqual(117, group1_filter20_data[0].value);
            Assert.AreEqual(243, group1_filter20_data[1].value);

            group1.Dispose();
            group2.Dispose();
            group12.Dispose();
            group1_filter_0.Dispose();
            group1_filter_20.Dispose();
        }

        [Test]
        public void UnmanagedSharedComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            Entity me1 = m_Manager.CreateEntity(archetype);
            Entity me2 = m_Manager.CreateEntity(archetype);
            Entity ue1 = m_Manager.CreateEntity(archetype);
            Entity ue2 = m_Manager.CreateEntity(archetype);
            Entity ue3 = m_Manager.CreateEntity(archetype);

            Assert.IsFalse(m_Manager.HasComponent<SharedData1>(me1));

            Assert.IsFalse(m_Manager.HasComponent<SharedData1>(ue1));

            // Managed path
            m_Manager.AddSharedComponentManaged(me1, new ManagedSharedData1(new Tuple<int, int>(17, 3)));
            m_Manager.AddSharedComponentManaged(me2, new ManagedSharedData1(new Tuple<int, int>(17, 3)));

            Assert.IsTrue(m_Manager.HasComponent<ManagedSharedData1>(me1));
            Assert.IsFalse(m_Manager.HasComponent<SharedData2>(me1));
            Assert.AreEqual(new Tuple<int, int>(17, 3), m_Manager.GetSharedComponentManaged<ManagedSharedData1>(me1).value);

            m_Manager.RemoveComponent<ManagedSharedData1>(me1);
            m_Manager.RemoveComponent<ManagedSharedData1>(me2);

            // Unmanaged path
            m_Manager.AddSharedComponentManaged(ue1, new SharedData1());
            Assert.IsTrue(m_Manager.HasComponent<SharedData1>(ue1));
            Assert.AreEqual(0, m_Manager.GetSharedComponentManaged<SharedData1>(ue1).value);

            m_Manager.AddSharedComponentManaged(ue1, new SharedData1(17));
            m_Manager.AddSharedComponentManaged(ue2, new SharedData1(17));

            Assert.IsTrue(m_Manager.HasComponent<SharedData1>(ue1));
            Assert.IsFalse(m_Manager.HasComponent<SharedData2>(ue1));
            Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<SharedData1>(ue1).value);

            m_Manager.RemoveComponent<SharedData1>(ue1);
            Assert.IsFalse(m_Manager.HasComponent<SharedData1>(ue1));

            m_Manager.RemoveComponent<SharedData1>(ue2);
            Assert.IsFalse(m_Manager.HasComponent<SharedData1>(ue2));
        }

        [Test]
        public void AddUnmanagedSharedComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            Entity ue1 = m_Manager.CreateEntity(archetype);
            Entity ue2 = m_Manager.CreateEntity(archetype);

            // Unmanaged through managed api
            m_Manager.AddSharedComponentManaged(ue1, new SharedData1(17));
            Assert.IsTrue(m_Manager.HasComponent<SharedData1>(ue1));
            Assert.IsFalse(m_Manager.HasComponent<SharedData2>(ue1));
            Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<SharedData1>(ue1).value);
            Assert.AreEqual(17, m_Manager.GetSharedComponent<SharedData1>(ue1).value);

            // Unmanaged API
            m_Manager.AddSharedComponent(ue2, new SharedData1(34));
            Assert.IsTrue(m_Manager.HasComponent<SharedData1>(ue2));
            Assert.IsFalse(m_Manager.HasComponent<SharedData2>(ue2));
            Assert.AreEqual(34, m_Manager.GetSharedComponentManaged<SharedData1>(ue2).value);
            Assert.AreEqual(34, m_Manager.GetSharedComponent<SharedData1>(ue2).value);
        }

        [Test]
        public void GetSharedComponentCount()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            Entity ue1 = m_Manager.CreateEntity(archetype);

            var startCount = m_Manager.GetSharedComponentCount();

            m_Manager.AddSharedComponentManaged(ue1, new SharedData1(17));
            Assert.AreEqual(startCount + 1, m_Manager.GetSharedComponentCount());

            m_Manager.AddSharedComponentManaged(ue1, new SharedData2(18));
            Assert.AreEqual(startCount + 2, m_Manager.GetSharedComponentCount());

            m_Manager.AddSharedComponentManaged(ue1, new ManagedSharedData1(new Tuple<int, int>(2, 3)));
            Assert.AreEqual(startCount + 3, m_Manager.GetSharedComponentCount());

            // ###REVIEW NOTE### Managed Path doesn't clear the SharedDataComponent when they're no longer referenced, should we fix this behavior or keep it?
            // m_Manager.RemoveComponent<SharedData1>(ue1);
            // Assert.AreEqual(startCount + 2, m_Manager.GetSharedComponentCount());
            //
            // m_Manager.RemoveComponent<SharedData2>(ue1);
            // Assert.AreEqual(startCount + 1, m_Manager.GetSharedComponentCount());
            //
            // m_Manager.RemoveComponent<ManagedSharedData1>(ue1);
            // Assert.AreEqual(startCount + 0, m_Manager.GetSharedComponentCount());
        }

        [Test]
        public void SetUnmanagedSharedComponentData()
        {
            var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData));
            Entity e1 = m_Manager.CreateEntity(archetype);
            Entity e2 = m_Manager.CreateEntity(archetype);
            Entity e3 = m_Manager.CreateEntity(archetype);

            Assert.AreEqual(0, m_Manager.GetSharedComponent<SharedData1>(e1).value);
            m_Manager.SetSharedComponent(e1, new SharedData1(17));
            Assert.AreEqual(17, m_Manager.GetSharedComponent<SharedData1>(e1).value);

            Assert.AreEqual(0, m_Manager.GetSharedComponent<SharedData1>(e2).value);
            m_Manager.SetSharedComponentManaged(e2, new SharedData1(18));
            Assert.AreEqual(18, m_Manager.GetSharedComponent<SharedData1>(e2).value);

            Assert.AreEqual(0, m_Manager.GetSharedComponentManaged<SharedData1>(e3).value);
            m_Manager.SetSharedComponentManaged(e3, new SharedData1(19));
            Assert.AreEqual(19, m_Manager.GetSharedComponentManaged<SharedData1>(e3).value);
        }

        [Test]
        public unsafe void SharedComponentDataWithRefCounter()
        {
            var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(SharedDataRefCounter));
            Entity e1 = m_Manager.CreateEntity(archetype);
            Entity e2 = m_Manager.CreateEntity(archetype);
            Entity e3 = m_Manager.CreateEntity(archetype);

            int refCounter = 0;

            m_Manager.SetSharedComponentManaged(e1, new SharedDataRefCounter(10, &refCounter));
            Assert.AreEqual(1, refCounter);

            m_Manager.SetSharedComponentManaged(e2, new SharedDataRefCounter(20, &refCounter));
            Assert.AreEqual(2, refCounter);

            m_Manager.RemoveComponent<SharedDataRefCounter>(e1);
            Assert.AreEqual(1, refCounter);

            m_Manager.RemoveComponent<SharedDataRefCounter>(e2);
            Assert.AreEqual(0, refCounter);
        }

        [Test]
        public void GetAllUniqueSharedComponents_ReturnsCorrectValues()
        {
            var unique = new List<SharedData1>(0);
            m_Manager.GetAllUniqueSharedComponentsManaged(unique);

            Assert.AreEqual(1, unique.Count);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);

            var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData));
            Entity e = m_Manager.CreateEntity(archetype);
            m_Manager.SetSharedComponentManaged(e, new SharedData1(17));

            unique.Clear();
            m_Manager.GetAllUniqueSharedComponentsManaged(unique);

            Assert.AreEqual(2, unique.Count);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);
            Assert.AreEqual(17, unique[1].value);

            m_Manager.SetSharedComponentManaged(e, new SharedData1(34));

            unique.Clear();
            m_Manager.GetAllUniqueSharedComponentsManaged(unique);

            Assert.AreEqual(2, unique.Count);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);
            Assert.AreEqual(34, unique[1].value);

            m_Manager.DestroyEntity(e);

            unique.Clear();
            m_Manager.GetAllUniqueSharedComponentsManaged(unique);

            Assert.AreEqual(1, unique.Count);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);
        }

        [Test]
        public void GetAllUniqueSharedComponents_Unmanaged_ReturnsCorrectValues()
        {
            m_Manager.GetAllUniqueSharedComponents<SharedData1>(out var unique, Allocator.Temp);

            Assert.AreEqual(1, unique.Length);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);

            var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData));
            Entity e = m_Manager.CreateEntity(archetype);
            m_Manager.SetSharedComponent(e, new SharedData1(17));

            unique.Dispose();

            m_Manager.GetAllUniqueSharedComponents<SharedData1>(out unique, Allocator.Temp);

            Assert.AreEqual(2, unique.Length);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);
            Assert.AreEqual(17, unique[1].value);

            m_Manager.SetSharedComponent(e, new SharedData1(34));

            unique.Dispose();
            m_Manager.GetAllUniqueSharedComponents<SharedData1>(out unique, Allocator.Temp);

            Assert.AreEqual(2, unique.Length);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);
            Assert.AreEqual(34, unique[1].value);

            m_Manager.DestroyEntity(e);

            unique.Dispose();
            m_Manager.GetAllUniqueSharedComponents<SharedData1>(out unique, Allocator.Temp);

            Assert.AreEqual(1, unique.Length);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);
        }

        [BurstCompile]
        public struct TestAllocator : Unity.Collections.AllocatorManager.IAllocator
        {
            long AllocatedBytes;
            long FreedBytes;

            public AllocatorManager.AllocatorHandle m_handle;

            public void ResetCounters()
            {
                AllocatedBytes = FreedBytes = 0;
            }
            public AllocatorManager.AllocatorHandle Handle { get { return m_handle; } set { m_handle = value; } }

            public Allocator ToAllocator { get { return m_handle.ToAllocator; } }

            public bool IsCustomAllocator { get { return m_handle.IsCustomAllocator; } }

            public void Initialize()
            {
                ResetCounters();
            }

            public int Try(ref AllocatorManager.Block block)
            {
                if (block.Range.Pointer != IntPtr.Zero)
                {
                    FreedBytes += block.AllocatedBytes;
                }

                var temp = block.Range.Allocator;
                block.Range.Allocator = AllocatorManager.Persistent;
                var error = AllocatorManager.Try(ref block);
                block.Range.Allocator = temp;
                if (error != 0)
                    return error;

                if (block.Range.Pointer != IntPtr.Zero) // if we allocated or reallocated...
                {
                    AllocatedBytes += block.AllocatedBytes;
                }

                return 0;
            }

            [BurstCompile]
            [MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
            public static unsafe int Try(IntPtr state, ref AllocatorManager.Block block)
            {
                return ((TestAllocator*)state)->Try(ref block);
            }

            public AllocatorManager.TryFunction Function => Try;
            public void Dispose()
            {
                m_handle.Dispose();
            }

            public void AssertNoLeaks()
            {
                Assert.AreEqual(AllocatedBytes, FreedBytes);
            }
        }

        [Test]
        public void GetAllUniqueSharedComponents_Unmanaged_DoesNotLeak()
        {
            AllocatorManager.Initialize();
            var allocatorHelper = new AllocatorHelper<TestAllocator>(AllocatorManager.Temp);
            ref var allocator = ref allocatorHelper.Allocator;
            allocator.Initialize();

            m_Manager.GetAllUniqueSharedComponents<SharedData1>(out var unique, allocator.Handle);

            Assert.AreEqual(1, unique.Length);
            Assert.AreEqual(default(SharedData1).value, unique[0].value);

            unique.Dispose();
            allocator.AssertNoLeaks();

            const int kNumSharedComponents = 1000;
            for (int i = 0; i < kNumSharedComponents; i++)
            {
                var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData));
                Entity e = m_Manager.CreateEntity(archetype);
                m_Manager.SetSharedComponent(e, new SharedData1(i));
            }

            m_Manager.GetAllUniqueSharedComponents<SharedData1>(out unique, allocator.Handle);

            Assert.AreEqual(kNumSharedComponents, unique.Length); // ++1 for the default value

            unique.Dispose();
            allocator.AssertNoLeaks();
            allocator.Dispose();
        }

        [Test]
        public unsafe void GetAllUniqueSharedComponents_ReturnsCorrectIndices()
        {
            Entity e = m_Manager.CreateEntity();
            Entity e2 = m_Manager.CreateEntity();

            m_Manager.AddComponentData(e, new EcsTestData(42));
            int refcount1 = 1;
            int refcount2 = 1;
            var sharedDataRefCounter1 = new SharedDataRefCounter(0, &refcount1);
            m_Manager.AddSharedComponentManaged(e, sharedDataRefCounter1);
            var sharedDataRefCounter2 = new SharedDataRefCounter(1, &refcount2);
            m_Manager.AddSharedComponentManaged(e2, sharedDataRefCounter2);
            /*
             * it's important to also remove one of the shared components, because we have had issues where
             * the index is fine until you remove a component and then is wrong afterwards
             */
            m_Manager.RemoveComponent<SharedDataRefCounter>(e);
            var values = new List<SharedDataRefCounter>();
            var indices = new List<int>();
            m_Manager.GetAllUniqueSharedComponentsManaged(values, indices);

            Assert.That(indices[0] == 0);
            var firstrealindex = indices[1];
            Assert.That(EntityComponentStore.IsUnmanagedSharedComponentIndex(firstrealindex));
            Assert.That(firstrealindex == m_Manager.GetSharedComponentIndex<SharedDataRefCounter>(e2));
            m_Manager.RemoveComponent<SharedDataRefCounter>(e2);
        }

        [Test]
        public void GetSharedComponentData()
        {
            var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData));
            Entity e = m_Manager.CreateEntity(archetype);

            Assert.AreEqual(0, m_Manager.GetSharedComponentManaged<SharedData1>(e).value);

            m_Manager.SetSharedComponentManaged(e, new SharedData1(17));

            Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<SharedData1>(e).value);
        }

        [Test]
        public void GetSharedComponentDataAfterArchetypeChange()
        {
            var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData));
            Entity e = m_Manager.CreateEntity(archetype);

            Assert.AreEqual(0, m_Manager.GetSharedComponentManaged<SharedData1>(e).value);

            m_Manager.SetSharedComponentManaged(e, new SharedData1(17));
            m_Manager.AddComponentData(e, new EcsTestData2 {value0 = 1, value1 = 2});

            Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<SharedData1>(e).value);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void NonExistingSharedComponentDataThrows()
        {
            Entity e = m_Manager.CreateEntity(typeof(EcsTestData));

            Assert.Throws<ArgumentException>(() => { m_Manager.GetSharedComponentManaged<SharedData1>(e); });
            Assert.Throws<ArgumentException>(() => { m_Manager.SetSharedComponentManaged(e, new SharedData1()); });
        }

        [Test]
        public void AddSharedComponent()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            Entity e = m_Manager.CreateEntity(archetype);

            Assert.IsFalse(m_Manager.HasComponent<SharedData1>(e));
            Assert.IsFalse(m_Manager.HasComponent<SharedData2>(e));

            m_Manager.AddSharedComponentManaged(e, new SharedData1(17));

            Assert.IsTrue(m_Manager.HasComponent<SharedData1>(e));
            Assert.IsFalse(m_Manager.HasComponent<SharedData2>(e));
            Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<SharedData1>(e).value);

            m_Manager.AddSharedComponentManaged(e, new SharedData2(34));
            Assert.IsTrue(m_Manager.HasComponent<SharedData1>(e));
            Assert.IsTrue(m_Manager.HasComponent<SharedData2>(e));
            Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<SharedData1>(e).value);
            Assert.AreEqual(34, m_Manager.GetSharedComponentManaged<SharedData2>(e).value);
        }

        [Test]
        public void AddSharedComponent_ToEntityArray_Managed_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            int entityCount = 100;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            foreach (var e in entities)
            {
                Assert.IsFalse(m_Manager.HasComponent<ManagedSharedData1>(e));
                Assert.IsFalse(m_Manager.HasComponent<ManagedSharedData2>(e));
            }

            var value1 = new ManagedSharedData1(17);
            m_Manager.AddSharedComponentManaged(entities, value1);
            foreach (var e in entities)
            {
                Assert.IsTrue(m_Manager.HasComponent<ManagedSharedData1>(e));
                Assert.IsFalse(m_Manager.HasComponent<ManagedSharedData2>(e));
                Assert.AreEqual(value1.value, m_Manager.GetSharedComponentManaged<ManagedSharedData1>(e).value);
            }

            var value2 = new ManagedSharedData2(34);
            m_Manager.AddSharedComponentManaged(entities, value2);
            foreach (var e in entities)
            {
                Assert.IsTrue(m_Manager.HasComponent<ManagedSharedData1>(e));
                Assert.IsTrue(m_Manager.HasComponent<ManagedSharedData2>(e));
                Assert.AreEqual(value1.value, m_Manager.GetSharedComponentManaged<ManagedSharedData1>(e).value);
                Assert.AreEqual(value2.value, m_Manager.GetSharedComponentManaged<ManagedSharedData2>(e).value);
            }
        }

        [Test]
        public void AddSharedComponent_ToEntityArray_Unmanaged_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            int entityCount = 100;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            foreach (var e in entities)
            {
                Assert.IsFalse(m_Manager.HasComponent<SharedData1>(e));
                Assert.IsFalse(m_Manager.HasComponent<SharedData2>(e));
            }

            m_Manager.AddSharedComponent(entities, new SharedData1(17));
            foreach (var e in entities)
            {
                Assert.IsTrue(m_Manager.HasComponent<SharedData1>(e));
                Assert.IsFalse(m_Manager.HasComponent<SharedData2>(e));
                Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<SharedData1>(e).value);
            }

            m_Manager.AddSharedComponent(entities, new SharedData2(34));
            foreach (var e in entities)
            {
                Assert.IsTrue(m_Manager.HasComponent<SharedData1>(e));
                Assert.IsTrue(m_Manager.HasComponent<SharedData2>(e));
                Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<SharedData1>(e).value);
                Assert.AreEqual(34, m_Manager.GetSharedComponentManaged<SharedData2>(e).value);
            }
        }

        [Test]
        public void SetSharedComponent_ToEntityArray_Managed_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(ManagedSharedData1), typeof(ManagedSharedData2));
            int entityCount = 100;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);

            var value1 = new ManagedSharedData1(17);
            m_Manager.SetSharedComponentManaged(entities, value1);
            foreach (var e in entities)
            {
                Assert.AreEqual(value1.value, m_Manager.GetSharedComponentManaged<ManagedSharedData1>(e).value);
            }

            var value2 = new ManagedSharedData2(34);
            m_Manager.SetSharedComponentManaged(entities, value2);
            foreach (var e in entities)
            {
                Assert.AreEqual(value1.value, m_Manager.GetSharedComponentManaged<ManagedSharedData1>(e).value);
                Assert.AreEqual(value2.value, m_Manager.GetSharedComponentManaged<ManagedSharedData2>(e).value);
            }
        }

        [Test]
        public void SetSharedComponent_ToEntityArray_Unmanaged_Works()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(SharedData1), typeof(SharedData2));
            int entityCount = 100;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);

            m_Manager.SetSharedComponent(entities, new SharedData1(17));
            foreach (var e in entities)
            {
                Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<SharedData1>(e).value);
            }

            m_Manager.AddSharedComponent(entities, new SharedData2(34));
            foreach (var e in entities)
            {
                Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<SharedData1>(e).value);
                Assert.AreEqual(34, m_Manager.GetSharedComponentManaged<SharedData2>(e).value);
            }
        }

        [Test]
        public void AddSharedComponentCompatibleChunkLayouts()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetypeWithShared = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(SharedData1));
            unsafe
            {
                Assert.IsTrue(ChunkDataUtility.AreLayoutCompatible(archetype.Archetype, archetypeWithShared.Archetype));
            }

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            Entity e = m_Manager.CreateEntity(archetype);

            Assert.IsFalse(m_Manager.HasComponent<SharedData1>(e));

            m_Manager.AddSharedComponentManaged(query, new SharedData1(17));

            Assert.IsTrue(m_Manager.HasComponent<SharedData1>(e));
            Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<SharedData1>(e).value);
        }

        [Test]
        public void AddSharedComponentIncompatibleChunkLayouts()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetypeWithShared = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedCompWithMaxChunkCapacity));
            unsafe
            {
                Assert.IsFalse(ChunkDataUtility.AreLayoutCompatible(archetype.Archetype, archetypeWithShared.Archetype));
            }

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            Entity e = m_Manager.CreateEntity(archetype);

            Assert.IsFalse(m_Manager.HasComponent<EcsTestSharedCompWithMaxChunkCapacity>(e));

            m_Manager.AddSharedComponentManaged(query, new EcsTestSharedCompWithMaxChunkCapacity(17));

            Assert.IsTrue(m_Manager.HasComponent<EcsTestSharedCompWithMaxChunkCapacity>(e));
            Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<EcsTestSharedCompWithMaxChunkCapacity>(e).Value);
        }

        [Test]
        public void AddSharedComponentToMultipleEntitiesIncompatibleChunkLayouts()
        {
            // The goal of this test is to verify that the moved IComponentData keeps the same values from
            // before the addition of the shared component.
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetypeWithShared = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedCompWithMaxChunkCapacity));
            unsafe
            {
                Assert.IsFalse(ChunkDataUtility.AreLayoutCompatible(archetype.Archetype, archetypeWithShared.Archetype));
            }

            var query = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            const int numEntities = 5000;
            using (var entities = new NativeArray<Entity>(numEntities, Allocator.Persistent))
            {
                m_Manager.CreateEntity(archetype, entities);

                for (int i = 0; i < entities.Length; ++i)
                {
                    m_Manager.SetComponentData(entities[i], new EcsTestData(i));
                }

                foreach (var e in entities)
                {
                    FastAssert.IsFalse(m_Manager.HasComponent<EcsTestSharedCompWithMaxChunkCapacity>(e));
                }

                m_Manager.AddSharedComponentManaged(query, new EcsTestSharedCompWithMaxChunkCapacity(17));
                var chunk = m_Manager.GetChunk(entities[0]);
                int maxChunkCapacity = TypeManager.GetTypeInfo<EcsTestSharedCompWithMaxChunkCapacity>().MaximumChunkCapacity;
                int expectedChunkCount = (numEntities + maxChunkCapacity - 1) / maxChunkCapacity;
                Assert.AreEqual(expectedChunkCount, chunk.Archetype.ChunkCount);

                // Ensure that the moved components have the correct values.
                for (int i = 0; i < entities.Length; ++i)
                {
                    FastAssert.IsTrue(m_Manager.HasComponent<EcsTestSharedCompWithMaxChunkCapacity>(entities[i]));
                    FastAssert.AreEqual(17, m_Manager.GetSharedComponentManaged<EcsTestSharedCompWithMaxChunkCapacity>(entities[i]).Value);
                    FastAssert.AreEqual(i, m_Manager.GetComponentData<EcsTestData>(entities[i]).value);
                }
            }
        }

        [Test]
        public void AddSharedComponentViaAddComponentWithIncompatibleChunkLayouts()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var archetypeWithShared = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedCompWithMaxChunkCapacity));
            unsafe
            {
                Assert.IsFalse(ChunkDataUtility.AreLayoutCompatible(archetype.Archetype, archetypeWithShared.Archetype));
            }

            using (var entities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(1, ref World.UpdateAllocator))
            {
                m_Manager.CreateEntity(archetype, entities);
                Assert.IsFalse(m_Manager.HasComponent<EcsTestSharedCompWithMaxChunkCapacity>(entities[0]));

                m_Manager.AddComponent(entities, typeof(EcsTestSharedCompWithMaxChunkCapacity));

                Assert.IsTrue(m_Manager.HasComponent<EcsTestSharedCompWithMaxChunkCapacity>(entities[0]));
                Assert.AreEqual(0, m_Manager.GetSharedComponentManaged<EcsTestSharedCompWithMaxChunkCapacity>(entities[0]).Value);
            }
        }

        [Test]
        public void RemoveSharedComponent()
        {
            Entity e = m_Manager.CreateEntity();

            m_Manager.AddComponentData(e, new EcsTestData(42));
            m_Manager.AddSharedComponentManaged(e, new SharedData1(17));
            m_Manager.AddSharedComponentManaged(e, new SharedData2(34));

            Assert.IsTrue(m_Manager.HasComponent<SharedData1>(e));
            Assert.IsTrue(m_Manager.HasComponent<SharedData2>(e));
            Assert.AreEqual(17, m_Manager.GetSharedComponentManaged<SharedData1>(e).value);
            Assert.AreEqual(34, m_Manager.GetSharedComponentManaged<SharedData2>(e).value);

            m_Manager.RemoveComponent<SharedData1>(e);
            Assert.IsFalse(m_Manager.HasComponent<SharedData1>(e));
            Assert.AreEqual(34, m_Manager.GetSharedComponentManaged<SharedData2>(e).value);

            m_Manager.RemoveComponent<SharedData2>(e);
            Assert.IsFalse(m_Manager.HasComponent<SharedData2>(e));

            Assert.AreEqual(42, m_Manager.GetComponentData<EcsTestData>(e).value);
        }




        [Test]
        public void SCG_DoesNotMatchRemovedSharedComponentInEntityQuery()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData));
            var archetype1 = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData), typeof(SharedData2));

            var group0 = m_Manager.CreateEntityQuery(typeof(SharedData1));
            var group1 = m_Manager.CreateEntityQuery(typeof(SharedData2));

            m_Manager.CreateEntity(archetype0);
            var entity1 = m_Manager.CreateEntity(archetype1);

            Assert.AreEqual(2, group0.CalculateEntityCount());
            Assert.AreEqual(1, group1.CalculateEntityCount());

            m_Manager.RemoveComponent<SharedData2>(entity1);

            Assert.AreEqual(2, group0.CalculateEntityCount());
            Assert.AreEqual(0, group1.CalculateEntityCount());

            group0.Dispose();
            group1.Dispose();
        }

        [Test]
        public void SCG_DoesNotMatchRemovedSharedComponentInChunkQuery()
        {
            var archetype0 = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData));
            var archetype1 = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData), typeof(SharedData2));

            var group0 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<SharedData1>());
            var group1 = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<SharedData2>());

            m_Manager.CreateEntity(archetype0);
            var entity1 = m_Manager.CreateEntity(archetype1);

            var preChunks0 = group0.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            var preChunks1 = group1.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            Assert.AreEqual(2, ArchetypeChunkArray.TotalEntityCountInChunksIgnoreFiltering(preChunks0));
            Assert.AreEqual(1, ArchetypeChunkArray.TotalEntityCountInChunksIgnoreFiltering(preChunks1));

            m_Manager.RemoveComponent<SharedData2>(entity1);

            var postChunks0 = group0.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            var postChunks1 = group1.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            Assert.AreEqual(2, ArchetypeChunkArray.TotalEntityCountInChunksIgnoreFiltering(postChunks0));
            Assert.AreEqual(0, ArchetypeChunkArray.TotalEntityCountInChunksIgnoreFiltering(postChunks1));

            group0.Dispose();
            group1.Dispose();
        }

        [Test]
        public void SCG_SetSharedComponentDataWithQuery()
        {
            var noShared = m_Manager.CreateEntity(typeof(EcsTestData));

            var e0 = m_Manager.CreateEntity(typeof(SharedData1), typeof(EcsTestData));
            var e1 = m_Manager.CreateEntity(typeof(SharedData1));
            var e2 = m_Manager.CreateEntity(typeof(SharedData1), typeof(EcsTestData));

            m_Manager.SetSharedComponentManaged(e0, new SharedData1 {value = 0});
            m_Manager.SetSharedComponentManaged(e1, new SharedData1 {value = 1});
            m_Manager.SetSharedComponentManaged(e2, new SharedData1 {value = 2});

            var c0 = m_Manager.GetChunk(e0);
            var c1 = m_Manager.GetChunk(e1);
            var c2 = m_Manager.GetChunk(e2);
            var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<SharedData1>(), ComponentType.ReadWrite<EcsTestData>());
            m_Manager.SetSharedComponentManaged(query, new SharedData1 {value = 10});

            Assert.AreEqual(10, m_Manager.GetSharedComponentManaged<SharedData1>(e0).value);
            Assert.AreEqual(1, m_Manager.GetSharedComponentManaged<SharedData1>(e1).value);
            Assert.AreEqual(10, m_Manager.GetSharedComponentManaged<SharedData1>(e2).value);
            Assert.IsFalse(m_Manager.HasComponent<SharedData1>(noShared));

            // This is not required but describes current behaviour,
            // Query based shared component does not reorder or merge chunks. (Even though in this case e0 & e2 could be in the same chunk)
            Assert.AreEqual(c0, m_Manager.GetChunk(e0));
            Assert.AreEqual(c1, m_Manager.GetChunk(e1));
            Assert.AreEqual(c2, m_Manager.GetChunk(e2));
            Assert.AreNotEqual(c0, c2);

            query.Dispose();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void TooManySharedComponentsEntity()
        {
            var archetype = m_Manager.CreateArchetype(
                typeof(EcsTestData),
                typeof(SharedData1), typeof(SharedData2), typeof(SharedData3), typeof(SharedData4),
                typeof(SharedData5), typeof(SharedData6), typeof(SharedData7), typeof(SharedData8),
                typeof(SharedData9), typeof(SharedData10), typeof(SharedData11), typeof(SharedData12),
                typeof(SharedData13), typeof(SharedData14), typeof(SharedData15), typeof(SharedData16));

            Entity e = m_Manager.CreateEntity(archetype);
            Assert.Throws<InvalidOperationException>(() => m_Manager.AddComponent<SharedData17>(e));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void TooManySharedComponentsQuery()
        {
            var archetype = m_Manager.CreateArchetype(
                typeof(EcsTestData),
                typeof(SharedData1), typeof(SharedData2), typeof(SharedData3), typeof(SharedData4),
                typeof(SharedData5), typeof(SharedData6), typeof(SharedData7), typeof(SharedData8),
                typeof(SharedData9), typeof(SharedData10), typeof(SharedData11), typeof(SharedData12),
                typeof(SharedData13), typeof(SharedData14), typeof(SharedData15), typeof(SharedData16));

            Entity e = m_Manager.CreateEntity(archetype);
            EntityQuery q = m_Manager.CreateEntityQuery(typeof(EcsTestData));
            Assert.Throws<InvalidOperationException>(() => m_Manager.AddComponent<SharedData17>(q));
            q.Dispose();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity data access safety checks")]
        public void TooManySharedComponentsEntityArray()
        {
            var archetype = m_Manager.CreateArchetype(
                typeof(EcsTestData),
                typeof(SharedData1), typeof(SharedData2), typeof(SharedData3), typeof(SharedData4),
                typeof(SharedData5), typeof(SharedData6), typeof(SharedData7), typeof(SharedData8),
                typeof(SharedData9), typeof(SharedData10), typeof(SharedData11), typeof(SharedData12),
                typeof(SharedData13), typeof(SharedData14), typeof(SharedData15), typeof(SharedData16));

            var entities = new NativeArray<Entity>(1024, Allocator.Persistent);
            m_Manager.CreateEntity(archetype, entities);
            Assert.Throws<InvalidOperationException>(() => m_Manager.AddComponent<SharedData17>(entities));
            entities.Dispose();
        }

        [Test]
        public void GetSharedComponentDataWithTypeIndex()
        {
            var archetype = m_Manager.CreateArchetype(typeof(SharedData1), typeof(EcsTestData));
            Entity e = m_Manager.CreateEntity(archetype);

            var typeIndex = TypeManager.GetTypeIndex<SharedData1>();

            object sharedComponentValue = m_Manager.GetSharedComponentData(e, typeIndex);
            Assert.AreEqual(typeof(SharedData1), sharedComponentValue.GetType());
            Assert.AreEqual(0, ((SharedData1)sharedComponentValue).value);

            m_Manager.SetSharedComponentManaged(e, new SharedData1(17));

            sharedComponentValue = m_Manager.GetSharedComponentData(e, typeIndex);
            Assert.AreEqual(typeof(SharedData1), sharedComponentValue.GetType());
            Assert.AreEqual(17, ((SharedData1)sharedComponentValue).value);
        }

        [Test]
        public void Case1085730()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsStringSharedComponent), typeof(EcsTestData));

            m_Manager.AddSharedComponentManaged(m_Manager.CreateEntity(), new EcsStringSharedComponent { Value = "1" });
            m_Manager.AddSharedComponentManaged(m_Manager.CreateEntity(), new EcsStringSharedComponent { Value = 1.ToString() });

            List<EcsStringSharedComponent> uniques = new List<EcsStringSharedComponent>();
            m_Manager.GetAllUniqueSharedComponentsManaged(uniques);

            Assert.AreEqual(2, uniques.Count);
        }
        [Test]
        public void Case1085730_HashCode()
        {
            var a = new EcsStringSharedComponent { Value = "1" };
            var b = new EcsStringSharedComponent { Value = 1.ToString() };
            int ahash = TypeManager.GetHashCode(ref a);
            int bhash = TypeManager.GetHashCode(ref b);

            Assert.AreEqual(ahash, bhash);
        }

        [Test]
        public void Case1085730_Equals()
        {
            var a = new EcsStringSharedComponent { Value = "1" };
            var b = new EcsStringSharedComponent { Value = 1.ToString() };
            bool iseq = TypeManager.Equals(ref a, ref b);

            Assert.IsTrue(iseq);
        }

        public struct CustomEquality : ISharedComponentData, IEquatable<CustomEquality>
        {
            public int Foo;

            public bool Equals(CustomEquality other)
            {
                return (Foo & 0xff) == (other.Foo & 0xff);
            }

            public override int GetHashCode()
            {
                return Foo & 0xff;
            }
        }

        [Test]
        public void BlittableComponentCustomEquality()
        {
            var archetype = m_Manager.CreateArchetype(typeof(CustomEquality), typeof(EcsTestData));

            m_Manager.AddSharedComponentManaged(m_Manager.CreateEntity(), new CustomEquality { Foo = 0x01 });
            m_Manager.AddSharedComponentManaged(m_Manager.CreateEntity(), new CustomEquality { Foo = 0x2201 });
            m_Manager.AddSharedComponentManaged(m_Manager.CreateEntity(), new CustomEquality { Foo = 0x3201 });

            List<CustomEquality> uniques = new List<CustomEquality>();
            m_Manager.GetAllUniqueSharedComponentsManaged(uniques);

            Assert.AreEqual(2, uniques.Count);
        }

        [Test]
        public unsafe void IRefCounted_IsDisposed_AfterWorldDies()
        {
            var world = new World("IRefCountedTestWorld");
            world.UpdateAllocatorEnableBlockFree = true;
            int RefCount1 = 0;
            var entity = world.EntityManager.CreateEntity();
            var refcountedComp = new EcsTestSharedCompWithRefCount(&RefCount1);
            world.EntityManager.AddSharedComponentManaged(entity, refcountedComp);
            Assert.AreEqual(1, RefCount1);

            world.Dispose();
            Assert.AreEqual(0, RefCount1);
        }



        [Test]
        public unsafe void IRefCounted_IsNotDisposed_AfterMovedAndSrcWorldDestroyed()
        {
            var world = new World("IRefCountedTestWorld");
            world.UpdateAllocatorEnableBlockFree = true;
            var world2 = new World("IRefCountedTestWorld2");
            int RefCount1 = 0;
            var entity = world.EntityManager.CreateEntity();
            var refcountedComp = new EcsTestSharedCompWithRefCount(&RefCount1);
            world.EntityManager.AddSharedComponentManaged(entity, refcountedComp);
            world2.EntityManager.MoveEntitiesFrom(world.EntityManager);
            world.Dispose();
            Assert.AreEqual(1, RefCount1);
            world2.Dispose();
            Assert.AreEqual(0, RefCount1);
        }

        [Test]
        public unsafe void IRefCounted_IsNotDisposed_AfterCopiedAndSrcWorldDestroyed()
        {
            var world = new World("IRefCountedTestWorld");
            world.UpdateAllocatorEnableBlockFree = true;
            var world2 = new World("IRefCountedTestWorld2");
            int RefCount1 = 0;
            var entity = world.EntityManager.CreateEntity();
            var refcountedComp = new EcsTestSharedCompWithRefCount(&RefCount1);
            world.EntityManager.AddSharedComponentManaged(entity, refcountedComp);
            var entities = new NativeArray<Entity>(1, Allocator.Temp);
            entities[0] = entity;
            world2.EntityManager.CopyEntitiesFrom(world.EntityManager, entities);
            world.Dispose();
            Assert.AreEqual(1, RefCount1);
            world2.Dispose();
            Assert.AreEqual(0, RefCount1);
        }

        [Test]
        public unsafe void IRefCounted_IsNotDisposed_AfterCopiedAndSrcCopyRemoved()
        {
            var world = new World("IRefCountedTestWorld");
            world.UpdateAllocatorEnableBlockFree = true;
            var world2 = new World("IRefCountedTestWorld2");

            int RefCount1 = 0;
            var entity = world.EntityManager.CreateEntity();
            var refcountedComp = new EcsTestSharedCompWithRefCount(&RefCount1);
            world.EntityManager.AddSharedComponentManaged(entity, refcountedComp);
            var entities = new NativeArray<Entity>(1, Allocator.Temp);
            entities[0] = entity;
            world2.EntityManager.CopyEntitiesFrom(world.EntityManager, entities);
            world.EntityManager.RemoveComponent(entity, ComponentType.ReadWrite<EcsTestSharedCompWithRefCount>());

            Assert.AreEqual( 1, RefCount1);
            world.Dispose();
            world2.Dispose();
            Assert.AreEqual( 0, RefCount1);
        }

        [Test]
        public unsafe void IRefCounted_IsNotDisposed_AfterAddedToTwoEntities_AndDeletedOnce()
        {
            var world = new World("IRefCountedTestWorld");
            world.UpdateAllocatorEnableBlockFree = true;

            int RefCount1 = 0;
            var entity = world.EntityManager.CreateEntity();
            var entity2 = world.EntityManager.CreateEntity();
            var refcountedComp = new EcsTestSharedCompWithRefCount(&RefCount1);
            world.EntityManager.AddSharedComponentManaged(entity, refcountedComp);
            world.EntityManager.AddSharedComponentManaged(entity2, refcountedComp);
            world.EntityManager.RemoveComponent(entity, ComponentType.ReadWrite<EcsTestSharedCompWithRefCount>());

            Assert.AreEqual(1, RefCount1);
            world.Dispose();
            Assert.AreEqual(0, RefCount1);

            /*
             * incidentally, check that being IRefcounted doesn't force a component to be treated as a managed shared component
             */
            Assert.IsFalse(TypeManager.IsManagedSharedComponent(TypeManager.GetTypeIndex<EcsTestSharedCompWithRefCount>()));
        }

        [Test]
        public unsafe void IRefCounted_IsDisposed_AfterAddedToTwoEntities_AndDeletedBoth()
        {
            var world = new World("IRefCountedTestWorld");
            world.UpdateAllocatorEnableBlockFree = true;

            int RefCount1 = 0;
            var entity = world.EntityManager.CreateEntity();
            var entity2 = world.EntityManager.CreateEntity();
            var refcountedComp = new EcsTestSharedCompWithRefCount(&RefCount1);
            world.EntityManager.AddSharedComponentManaged(entity, refcountedComp);
            world.EntityManager.AddSharedComponentManaged(entity2, refcountedComp);
            world.EntityManager.RemoveComponent(entity, ComponentType.ReadWrite<EcsTestSharedCompWithRefCount>());
            world.EntityManager.RemoveComponent(entity2, ComponentType.ReadWrite<EcsTestSharedCompWithRefCount>());

            Assert.AreEqual(0, RefCount1);
            world.Dispose();
        }
    }
}
