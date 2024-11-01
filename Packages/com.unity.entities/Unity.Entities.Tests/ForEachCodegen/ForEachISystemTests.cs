// TODO: convert tests over to use/compare with foreach and IJobEntity
// DOTS-6252
#if FALSE
using NUnit.Framework;
using Unity.Collections;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests.ForEachCodegen
{
    [TestFixture]
    public unsafe partial class ForEachISystemTests : ECSTestsFixture
    {
        internal static Entity TestEntity;
        internal static Entity DisabledEntity;

        [SetUp]
        public void SetUp()
        {
            World.GetOrCreateSystem<ForEachISystemTestsSystem>();

            var myArch = m_Manager.CreateArchetype(
                ComponentType.ReadWrite<EcsTestData>(),
                ComponentType.ReadWrite<EcsTestData2>(),
                ComponentType.ReadWrite<EcsTestSharedComp>(),
                ComponentType.ReadWrite<EcsTestSharedComp2>(),
                ComponentType.ReadWrite<EcsIntElement>(),
                ComponentType.ReadWrite<EcsTestTag>());

            TestEntity = m_Manager.CreateEntity(myArch);
            m_Manager.SetComponentData(TestEntity, new EcsTestData { value = 3});
            m_Manager.SetComponentData(TestEntity, new EcsTestData2 { value0 = 4});
            var buffer = m_Manager.GetBuffer<EcsIntElement>(TestEntity);
            buffer.Add(new EcsIntElement {Value = 18});
            buffer.Add(new EcsIntElement {Value = 19});
            m_Manager.SetSharedComponentData(TestEntity, new EcsTestSharedComp { value = 5 });
            m_Manager.SetSharedComponentData(TestEntity, new EcsTestSharedComp2 { value0 = 11, value1 = 13 });

            DisabledEntity = m_Manager.CreateEntity(typeof(Disabled), typeof(EcsTestData3));
        }

        SystemRef<ForEachISystemTestsSystem> GetTestSystemRef() => World.GetExistingSystem<ForEachISystemTestsSystem>();

        ref SystemState GetSystemStateRef(SystemRef<ForEachISystemTestsSystem> testSystemRef)
        {
            var statePtr = World.Unmanaged.ResolveSystemState(testSystemRef);
            if (statePtr == null)
                throw new System.InvalidOperationException("No system state exists any more for this SystemRef");
            return ref *statePtr;
        }

        [Test]
        public void SimplestCase([Values] bool useSystemStateForEach) => GetTestSystemRef().Struct.SimplestCase(ref GetSystemStateRef(GetTestSystemRef()), useSystemStateForEach);

        [Test]
        public void WithTagComponent([Values] bool useSystemStateForEach) => GetTestSystemRef().Struct.WithTagComponent(ref GetSystemStateRef(GetTestSystemRef()), useSystemStateForEach);

        [Test]
        public void WithNone([Values] bool useSystemStateForEach) => GetTestSystemRef().Struct.WithNone(ref GetSystemStateRef(GetTestSystemRef()), useSystemStateForEach);

        [Test]
        public void WithAny_DoesntExecute_OnEntityWithoutThatComponent([Values] bool useSystemStateForEach) => GetTestSystemRef().Struct.WithAny_DoesntExecute_OnEntityWithoutThatComponent(ref GetSystemStateRef(GetTestSystemRef()), useSystemStateForEach);

        [Test]
        public void WithAllSharedComponent([Values] bool useSystemStateForEach) => GetTestSystemRef().Struct.WithAllSharedComponentData(ref GetSystemStateRef(GetTestSystemRef()), useSystemStateForEach);

        [Test]
        public void StoresEntityQueryInField([Values] bool useSystemStateForEach) => GetTestSystemRef().Struct.StoresEntityQueryInField(ref GetSystemStateRef(GetTestSystemRef()), useSystemStateForEach);

        [Test]
        public void WithEntityQueryOption_DisabledEntity([Values] bool useSystemStateForEach) => GetTestSystemRef().Struct.WithEntityQueryOption_DisabledEntity(ref GetSystemStateRef(GetTestSystemRef()), useSystemStateForEach);

        [Test]
        public void WithEntityQueryOption_DisabledAndPrefabEntity() => GetTestSystemRef().Struct.WithEntityQueryOption_DisabledAndPrefabEntity(ref GetSystemStateRef(GetTestSystemRef()));

        [Test]
        public void DynamicBufferNoWarnings() => GetTestSystemRef().Struct.DynamicBufferNoWarnings(ref GetSystemStateRef(GetTestSystemRef()));

        [Test]
        public void AddToDynamicBuffer([Values] bool useSystemStateForEach) => GetTestSystemRef().Struct.AddToDynamicBuffer(ref GetSystemStateRef(GetTestSystemRef()), useSystemStateForEach);

        [Test]
        public void ModifyDynamicBuffer([Values] bool useSystemStateForEach) => GetTestSystemRef().Struct.ModifyDynamicBuffer(ref  GetSystemStateRef(GetTestSystemRef()), useSystemStateForEach);

        [Test]
        public void IterateExistingDynamicBufferReadOnly([Values] bool useSystemStateForEach) => GetTestSystemRef().Struct.IterateExistingDynamicBufferReadOnly(ref  GetSystemStateRef(GetTestSystemRef()), useSystemStateForEach);

        [Test]
        public void DisposeNativeArray_DisposesAtEnd([Values] bool useSystemStateForEach) => GetTestSystemRef().Struct.DisposeNativeArray(ref GetSystemStateRef(GetTestSystemRef()), useSystemStateForEach);

        [Test]
        public void ForEach_EntityParameter_NoWarnings() => GetTestSystemRef().Struct.ForEach_EntityParameter_NoWarnings(ref  GetSystemStateRef(GetTestSystemRef()));

    }

    partial struct ForEachISystemTestsSystem : ISystem
    {
        EntityQuery m_StoredQuery;

        public void SimplestCase(ref SystemState systemState, bool useSystemStateForEach)
        {
            if (useSystemStateForEach)
                systemState.Entities.ForEach((ref EcsTestData e1, in EcsTestData2 e2) => { e1.value += e2.value0; }).Schedule();
            else
                systemState.Entities.ForEach((ref EcsTestData e1, in EcsTestData2 e2) => { e1.value += e2.value0; }).Schedule();
            systemState.Dependency.Complete();
            Assert.AreEqual(7, systemState.EntityManager.GetComponentData<EcsTestData>(ForEachISystemTests.TestEntity).value);
        }

        public void WithTagComponent(ref SystemState systemState, bool useSystemStateForEach)
        {
            if (useSystemStateForEach)
                systemState.Entities.ForEach((ref EcsTestData e1, ref EcsTestTag e2) => { e1.value = 5; }).Schedule();
            else
                systemState.Entities.ForEach((ref EcsTestData e1, ref EcsTestTag e2) => { e1.value = 5; }).Schedule();
            systemState.Dependency.Complete();
            Assert.AreEqual(5, systemState.EntityManager.GetComponentData<EcsTestData>(ForEachISystemTests.TestEntity).value);
        }

        public void WithNone(ref SystemState systemState, bool useSystemStateForEach)
        {
            var one = 1;
            if (useSystemStateForEach)
                systemState.Entities
                    .WithNone<EcsTestData2>()
                    .ForEach((ref EcsTestData e1) => { e1.value += one; })
                    .Schedule();
            else
                systemState.Entities
                    .WithNone<EcsTestData2>()
                    .ForEach((ref EcsTestData e1) => { e1.value += one; })
                    .Schedule();
            systemState.Dependency.Complete();
            Assert.AreEqual(3, systemState.EntityManager.GetComponentData<EcsTestData>(ForEachISystemTests.TestEntity).value);
        }

        public void WithAny_DoesntExecute_OnEntityWithoutThatComponent(ref SystemState systemState, bool useSystemStateForEach)
        {
            var one = 1;
            if (useSystemStateForEach)
                systemState.Entities
                    .WithAny<EcsTestData3>()
                    .ForEach((ref EcsTestData e1) => { e1.value += one; })
                    .Schedule();
            else
                systemState.Entities
                    .WithAny<EcsTestData3>()
                    .ForEach((ref EcsTestData e1) => { e1.value += one; })
                    .Schedule();
            systemState.Dependency.Complete();
            Assert.AreEqual(3, systemState.EntityManager.GetComponentData<EcsTestData>(ForEachISystemTests.TestEntity).value);
        }

        public void WithAllSharedComponentData(ref SystemState systemState, bool useSystemStateForEach)
        {
            var one = 1;
            if (useSystemStateForEach)
                systemState.Entities
                    .WithAll<EcsTestSharedComp>()
                    .ForEach((ref EcsTestData e1) => { e1.value += one; })
                    .Schedule();
            else
                systemState.Entities
                    .WithAll<EcsTestSharedComp>()
                    .ForEach((ref EcsTestData e1) => { e1.value += one; })
                    .Schedule();
            systemState.Dependency.Complete();
            Assert.AreEqual(4, systemState.EntityManager.GetComponentData<EcsTestData>(ForEachISystemTests.TestEntity).value);
        }

        public void StoresEntityQueryInField(ref SystemState systemState, bool useSystemStateForEach)
        {
            var count = 0;
            if (useSystemStateForEach)
                systemState.Entities
                    .WithStoreEntityQueryInField(ref m_StoredQuery)
                    .ForEach((ref EcsTestData e1) => { count++; })
                    .Run();
            else
                systemState.Entities
                    .WithStoreEntityQueryInField(ref m_StoredQuery)
                    .ForEach((ref EcsTestData e1) => { count++; })
                    .Run();

            Assert.AreEqual(m_StoredQuery.CalculateEntityCount(), count);
        }

        public void WithEntityQueryOption_DisabledEntity(ref SystemState systemState, bool useSystemStateForEach)
        {
            Entity disabledEntity = default;
            if (useSystemStateForEach)
                systemState.Entities
                    .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                    .ForEach((Entity entity, in EcsTestData3 data3) => { disabledEntity = entity; })
                    .Run();
            else
                systemState.Entities
                    .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                    .ForEach((Entity entity, in EcsTestData3 data3) => { disabledEntity = entity; })
                    .Run();
            systemState.Dependency.Complete();

            Assert.AreEqual(ForEachISystemTests.DisabledEntity, disabledEntity);
        }

        private EntityQuery _IncludeDisabledAndPrefabTests;
        public void WithEntityQueryOption_DisabledAndPrefabEntity(ref SystemState systemState)
        {
            systemState.Entities
                    .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                    .WithStoreEntityQueryInField( ref _IncludeDisabledAndPrefabTests)
                    .ForEach((Entity entity) => { })
                    .Run();

            Assert.AreEqual(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab, _IncludeDisabledAndPrefabTests.GetEntityQueryDesc().Options);
        }

        public void DynamicBufferNoWarnings(ref SystemState systemState)
        {
            systemState.Entities
                .ForEach((DynamicBuffer<EcsIntElement> buf) =>
                {
                    buf.Add(4);
                })
                .Schedule();

            systemState.Dependency.Complete();

            LogAssert.NoUnexpectedReceived();
        }

        public void AddToDynamicBuffer(ref SystemState systemState, bool useSystemStateForEach)
        {
            if (useSystemStateForEach)
                systemState.Entities
                    .ForEach((ref EcsTestData e1, ref DynamicBuffer<EcsIntElement> buf) =>
                    {
                        buf.Add(4);
                    })
                    .Schedule();
            else
                systemState.Entities
                    .ForEach((ref EcsTestData e1, ref DynamicBuffer<EcsIntElement> buf) =>
                    {
                        buf.Add(4);
                    })
                    .Schedule();

            systemState.Dependency.Complete();

            var buffer = systemState.EntityManager.GetBuffer<EcsIntElement>(ForEachISystemTests.TestEntity);
            Assert.AreEqual(3, buffer.Length);
            CollectionAssert.AreEqual(new[] {18, 19, 4}, buffer.Reinterpret<int>().AsNativeArray());
        }

        public void ModifyDynamicBuffer(ref SystemState systemState, bool useSystemStateForEach)
        {
            if (useSystemStateForEach)
                systemState.Entities
                    .ForEach((ref EcsTestData e1, ref DynamicBuffer<EcsIntElement> buf) =>
                    {
                        for (int i = 0; i < buf.Length; ++i) buf[i] = buf[i].Value * 2;
                    })
                    .Schedule();
            else
                systemState.Entities
                    .ForEach((ref EcsTestData e1, ref DynamicBuffer<EcsIntElement> buf) =>
                    {
                        for (int i = 0; i < buf.Length; ++i) buf[i] = buf[i].Value * 2;
                    })
                    .Schedule();

            systemState.Dependency.Complete();

            var buffer = systemState.EntityManager.GetBuffer<EcsIntElement>(ForEachISystemTests.TestEntity);
            CollectionAssert.AreEqual(new[] {18 * 2, 19 * 2}, buffer.Reinterpret<int>().AsNativeArray());
        }

        static int SumOfBufferElements(DynamicBuffer<EcsIntElement> buf)
        {
            var total = 0;
            for (var i = 0; i != buf.Length; i++)
                total += buf[i].Value;
            return total;
        }

        public void IterateExistingDynamicBufferReadOnly(ref SystemState systemState, bool useSystemStateForEach)
        {
            if (useSystemStateForEach)
                systemState.Entities
                    .ForEach((ref EcsTestData e1, in DynamicBuffer<EcsIntElement> buf) =>
                    {
                        e1.value = SumOfBufferElements(buf);
                    })
                    .Schedule();
            else
                systemState.Entities
                    .ForEach((ref EcsTestData e1, in DynamicBuffer<EcsIntElement> buf) =>
                    {
                        e1.value = SumOfBufferElements(buf);
                    })
                    .Schedule();
            systemState.Dependency.Complete();

            Assert.AreEqual(18 + 19, systemState.EntityManager.GetComponentData<EcsTestData>(ForEachISystemTests.TestEntity).value);
        }

        public void ForEach_EntityParameter_NoWarnings(ref SystemState systemState)
        {
            systemState.Entities.ForEach((Entity entity) => {}).Run();
            LogAssert.NoUnexpectedReceived();
        }

        public void DisposeNativeArray(ref SystemState systemState, bool useSystemStateForEach)
        {
            var testArray = new NativeArray<int>(100, Allocator.Temp);

            if (useSystemStateForEach)
                systemState.Entities
                    .WithReadOnly(testArray)
                    .WithDisposeOnCompletion(testArray)
                    .ForEach((Entity entity) =>
                    {
                        var length = testArray.Length;
                    })
                    .Run();
            else
                systemState.Entities
                    .WithReadOnly(testArray)
                    .WithDisposeOnCompletion(testArray)
                    .ForEach((Entity entity) =>
                    {
                        var length = testArray.Length;
                    })
                    .Run();

            Assert.IsFalse(testArray.IsCreated);
        }
    }
}
#endif
