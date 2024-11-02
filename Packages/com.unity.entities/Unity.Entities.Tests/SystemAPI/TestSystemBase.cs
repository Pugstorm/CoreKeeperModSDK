using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Entities.Tests;
using Unity.Entities.Tests.TestSystemAPI;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Entities.SystemAPI;

[assembly: RegisterGenericSystemType(typeof(TestSystemBaseSystem.GenericISystem<EcsTestData>))]
namespace Unity.Entities.Tests.TestSystemAPI
{
    /// <summary>
    /// Make sure this matches <see cref="TestISystem"/>.
    /// </summary>
    [TestFixture]
    public class TestSystemBase : ECSTestsFixture
    {
        [SetUp]
        public void SetUp() {
            World.GetOrCreateSystemManaged<TestSystemBaseSystem>();
            World.GetOrCreateSystemManaged<TestSystemBaseSystem.GenericSystem<EcsTestData>>();
            World.GetOrCreateSystem<TestSystemBaseSystem.GenericISystem<EcsTestData>>();
        }

        #region Query Access
        [Test]
        public void Query([Values(1,2,3,4,5,6,7)] int queryArgumentCount) => World.GetExistingSystemManaged<TestSystemBaseSystem>().QuerySetup(queryArgumentCount);
        #endregion

        #region Time Access
        [Test]
        public void Time([Values] MemberUnderneath memberUnderneath) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestTime(memberUnderneath);
        #endregion

        #region Component Access

        [Test]
        public void GetComponentLookup([Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetComponentLookup(memberUnderneath, readAccess);

        [Test]
        public void GetComponent([Values] MemberUnderneath memberUnderneath) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetComponent(memberUnderneath);

        [Test]
        public void GetComponentRW([Values] MemberUnderneath memberUnderneath) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetComponentRW(memberUnderneath);

        [Test]
        public void SetComponent() => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestSetComponent();

        [Test]
        public void HasComponent([Values] MemberUnderneath memberUnderneath) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestHasComponent(memberUnderneath);

        [Test]
        public void GetComponentForSystem([Values] MemberUnderneath memberUnderneath) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetComponentForSystem(memberUnderneath);

        [Test]
        public void GetComponentRWForSystem([Values] MemberUnderneath memberUnderneath) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetComponentRWForSystem(memberUnderneath);

        [Test]
        public void SetComponentForSystem() => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestSetComponentForSystem();

        [Test]
        public void HasComponentForSystem([Values] MemberUnderneath memberUnderneath) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestHasComponentForSystem(memberUnderneath);
        #endregion

        #region Buffer Access

        [Test]
        public void GetBufferDataFromEntity([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) =>
            World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetBufferLookup(access, memberUnderneath, readAccess);

        [Test]
        public void GetBuffer([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) =>
            World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetBuffer(access, memberUnderneath);

        [Test]
        public void HasBuffer([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) =>
            World.GetExistingSystemManaged<TestSystemBaseSystem>().TestHasBuffer(access, memberUnderneath);

        #endregion

        #region StorageInfo Access

        [Test]
        public void GetEntityStorageInfoLookup([Values] SystemAPIAccess access) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetEntityStorageInfoLookup(access);

        [Test]
        public void Exists([Values] SystemAPIAccess access) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestExists(access);

        #endregion

        #region Singleton Access
        [Test]
        public void GetSingleton() => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetSingleton();
        [Test]
        public void GetSingletonWithSystemEntity() => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetSingletonWithSystemEntity();
        [Test]
        public void TryGetSingleton([Values] TypeArgumentExplicit typeArgumentExplicit) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestTryGetSingleton(typeArgumentExplicit);
        [Test]
        public void GetSingletonRW() => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetSingletonRW();
        [Test]
        public void TryGetSingletonRW() => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestTryGetSingletonRW();
        [Test]
        public void SetSingleton([Values] TypeArgumentExplicit typeArgumentExplicit) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestSetSingleton(typeArgumentExplicit);
        [Test]
        public void GetSingletonEntity() => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetSingletonEntity();
        [Test]
        public void TryGetSingletonEntity() => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestTryGetSingletonEntity();
        [Test]
        public void GetSingletonBuffer() => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetSingletonBuffer();
        [Test]
        public void GetSingletonBufferWithSystemEntity() => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetSingletonBufferWithSystemEntity();
        [Test]
        public void TryGetSingletonBuffer([Values] TypeArgumentExplicit typeArgumentExplicit) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestTryGetSingletonBuffer(typeArgumentExplicit);
        [Test]
        public void HasSingleton([Values] SingletonVersion singletonVersion) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestHasSingleton(singletonVersion);
        #endregion

        #region Aspect

        [Test]
        public void GetAspect([Values] SystemAPIAccess access) => World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGetAspect(access);

        #endregion

        #region NoError
        [Test]
        public void Nesting() =>  World.GetExistingSystemManaged<TestSystemBaseSystem>().TestNesting();
        [Test]
        public void StatementInsert() =>  World.GetExistingSystemManaged<TestSystemBaseSystem>().TestStatementInsert();
        [Test]
        public void GenericTypeArgument() =>  World.GetExistingSystemManaged<TestSystemBaseSystem>().TestGenericTypeArgument();
        [Test]
        public void GenericSystem() => World.GetExistingSystemManaged<TestSystemBaseSystem.GenericSystem<EcsTestData>>().TestGenericSystem();
        
        [Test]
        public unsafe void GenericISystem()
        {
            ref var state =
                ref World.Unmanaged.ResolveSystemStateRef(World
                    .GetExistingSystem<TestSystemBaseSystem.GenericISystem<EcsTestData>>());
            ((TestSystemBaseSystem.GenericISystem<EcsTestData>*)state.m_SystemPtr)->TestGenericSystem(ref state);
        }

        [Test]
        public void VariableInOnCreate() => World.CreateSystemManaged<TestSystemBaseSystem.VariableInOnCreateSystem>();
        #endregion
    }

    partial class TestSystemBaseSystem : SystemBase
    {
        protected override void OnCreate() {}
        protected override void OnDestroy() {}
        protected override void OnUpdate() {}

        #region Query Access
        public void QuerySetup(int queryArgumentCount)
        {
            for (var i = 0; i < 10; i++)
            {
                var e = EntityManager.CreateEntity();
                EntityManager.AddComponentData(e, LocalTransform.FromPosition(i));
                EntityManager.AddComponentData(e, new LocalToWorld());
                EntityManager.AddComponentData(e, new EcsTestData(i));
                EntityManager.AddComponentData(e, new EcsTestData2(i));
                EntityManager.AddComponentData(e, new EcsTestData3(i));
                EntityManager.AddComponentData(e, new EcsTestData4(i));
                EntityManager.AddComponentData(e, new EcsTestData5(i));
                EntityManager.AddComponentData(e, new EcsTestDataEnableable(i));
                EntityManager.AddComponentData(e, new EcsTestDataEnableable2(i));
            }

            Assert.AreEqual(45*queryArgumentCount, queryArgumentCount switch
            {
                1 => Query1(),
                2 => Query2(),
                3 => Query3(),
                4 => Query4(),
                5 => Query5(),
                6 => Query6(),
                7 => Query7(),
                _ => throw new ArgumentOutOfRangeException(nameof(queryArgumentCount), queryArgumentCount, null)
            });
        }

        int Query1()
        {
            var sum = 0;
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>())
                sum += (int) transform.ValueRO.Position.x;
            return sum;
        }

        int Query2()
        {
            var sum = 0;
            foreach (var (transform, data1) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EcsTestData>>())
            {
                sum += (int)transform.ValueRO.Position.x;
                sum += data1.ValueRO.value;
            }
            return sum;
        }

        int Query3()
        {
            var sum = 0;
            foreach (var (transform, data1, data2) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>>())
            {
                sum += (int)transform.ValueRO.Position.x;
                sum += data1.ValueRO.value;
                sum += data2.ValueRO.value0;
            }
            return sum;
        }

        int Query4()
        {
            var sum = 0;
            foreach (var (transform, data1, data2, data3) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>, RefRW<EcsTestData3>>())
            {
                sum += (int)transform.ValueRO.Position.x;
                sum += data1.ValueRO.value;
                sum += data2.ValueRO.value0;
                sum += data3.ValueRO.value0;
            }
            return sum;
        }

        int Query5()
        {
            var sum = 0;
            foreach (var (transform, data1, data2, data3, data4) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>, RefRW<EcsTestData3>, RefRW<EcsTestData4>>())
            {
                sum += (int)transform.ValueRO.Position.x;
                sum += data1.ValueRO.value;
                sum += data2.ValueRO.value0;
                sum += data3.ValueRO.value0;
                sum += data4.ValueRO.value0;
            }
            return sum;
        }

        int Query6()
        {
            var sum = 0;
            foreach (var (transform, data1, data2, data3, data4, data5) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>, RefRW<EcsTestData3>, RefRW<EcsTestData4>, RefRW<EcsTestData5>>())
            {
                sum += (int)transform.ValueRO.Position.x;
                sum += data1.ValueRO.value;
                sum += data2.ValueRO.value0;
                sum += data3.ValueRO.value0;
                sum += data4.ValueRO.value0;
                sum += data5.ValueRO.value0;
            }
            return sum;
        }

        int Query7()
        {
            var sum = 0;
            foreach (var (transform, data1, data2, data3, data4, data5, data6) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>, RefRW<EcsTestData3>, RefRW<EcsTestData4>, RefRW<EcsTestData5>, RefRW<EcsTestDataEnableable>>())
            {
                sum += (int)transform.ValueRO.Position.x;
                sum += data1.ValueRO.value;
                sum += data2.ValueRO.value0;
                sum += data3.ValueRO.value0;
                sum += data4.ValueRO.value0;
                sum += data5.ValueRO.value0;
                sum += data6.ValueRO.value;
            }
            return sum;
        }

        #endregion

        #region Time Access

        public void TestTime(MemberUnderneath memberUnderneath)
        {
            var time = new TimeData(42, 0.5f);
            World.SetTime(time);

            if (memberUnderneath == MemberUnderneath.WithMemberUnderneath) {
                Assert.That(SystemAPI.Time.DeltaTime, Is.EqualTo(time.DeltaTime));
            } else if (memberUnderneath == MemberUnderneath.WithoutMemberUnderneath) {
                Assert.That(SystemAPI.Time, Is.EqualTo(time));
            }
        }
        #endregion

        #region Component Access

        public void TestGetComponentLookup(MemberUnderneath memberUnderneath, ReadAccess readAccess) {
            var e = EntityManager.CreateEntity();
            var t = LocalTransform.FromPosition(0, 2, 0);
            EntityManager.AddComponentData(e, t);

            switch (readAccess) {
                case ReadAccess.ReadOnly:
                    // Get works
                    switch (memberUnderneath) {
                        case MemberUnderneath.WithMemberUnderneath: {
                            var tGet = SystemAPI.GetComponentLookup<LocalTransform>(true)[e];
                            Assert.That(tGet, Is.EqualTo(t));
                        } break;
                        case MemberUnderneath.WithoutMemberUnderneath: {
                            var lookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
                            var tGet = lookup[e];
                            Assert.That(tGet, Is.EqualTo(t));
                        } break;
                    } break;

                // Set works
                case ReadAccess.ReadWrite: {
                    switch (memberUnderneath) {
                        case MemberUnderneath.WithMemberUnderneath: {
                            t.Position += 1;
                            var lookup = SystemAPI.GetComponentLookup<LocalTransform>();
                            lookup[e] = t;
                            var tSet = SystemAPI.GetComponentLookup<LocalTransform>(true)[e];
                            Assert.That(tSet, Is.EqualTo(t));
                        } break;
                        case MemberUnderneath.WithoutMemberUnderneath: {
                            t.Position += 1;
                            var lookup = SystemAPI.GetComponentLookup<LocalTransform>();
                            lookup[e] = t;
                            Assert.That(lookup[e], Is.EqualTo(t));
                        } break;
                    }
                } break;
            }
        }

        public void TestGetComponent(MemberUnderneath memberUnderneath) {
            var e = EntityManager.CreateEntity();
            var t = LocalTransform.FromPosition(0, 2, 0);
            EntityManager.AddComponentData(e, t);

            switch (memberUnderneath) {
                case MemberUnderneath.WithMemberUnderneath:
                    Assert.That(SystemAPI.GetComponent<LocalTransform>(e), Is.EqualTo(t));
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    Assert.That(SystemAPI.GetComponent<LocalTransform>(e), Is.EqualTo(t));
                    break;
            }
        }

        public void TestGetComponentRW(MemberUnderneath memberUnderneath)
        {
            var e = EntityManager.CreateEntity();
            EntityManager.AddComponent<LocalTransform>(e);
            var t = LocalTransform.FromPosition(0, 2, 0);
            SystemAPI.GetComponentRW<LocalTransform>(e).ValueRW = t;
            switch (memberUnderneath)
            {
                case MemberUnderneath.WithMemberUnderneath:
                    Assert.That(SystemAPI.GetComponentRW<LocalTransform>(e).ValueRO, Is.EqualTo(t));
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    Assert.That(SystemAPI.GetComponentRW<LocalTransform>(e).ValueRO, Is.EqualTo(t));
                    break;
            }
        }

        public void TestSetComponent() {
            var e = EntityManager.CreateEntity();
            var t = LocalTransform.FromPosition(0, 2, 0);
            EntityManager.AddComponentData(e, t);

            t.Position += 1;
            SystemAPI.SetComponent(e, t);
            Assert.That(SystemAPI.GetComponent<LocalTransform>(e), Is.EqualTo(t));
        }

        public void TestHasComponent(MemberUnderneath memberUnderneath) {
            var e = EntityManager.CreateEntity(typeof(LocalTransform));

            switch (memberUnderneath) {
                case MemberUnderneath.WithMemberUnderneath:
                    Assert.That(SystemAPI.HasComponent<LocalTransform>(e).GetHashCode(), Is.EqualTo(1));
                    Assert.That(SystemAPI.HasComponent<EcsTestData>(e).GetHashCode(), Is.EqualTo(0));
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    Assert.That(SystemAPI.HasComponent<LocalTransform>(e), Is.EqualTo(true));
                    Assert.That(SystemAPI.HasComponent<EcsTestData>(e), Is.EqualTo(false));
                    break;
            }
        }

        public void TestGetComponentForSystem(MemberUnderneath memberUnderneath)
        {
            var t = LocalTransform.FromPosition(0, 2, 0);
            EntityManager.AddComponentData(SystemHandle, t);

            switch (memberUnderneath)
            {
                case MemberUnderneath.WithMemberUnderneath:
                    Assert.That(SystemAPI.GetComponent<LocalTransform>(SystemHandle), Is.EqualTo(t));
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    Assert.That(SystemAPI.GetComponent<LocalTransform>(SystemHandle), Is.EqualTo(t));
                    break;
            }
        }

        public void TestGetComponentRWForSystem(MemberUnderneath memberUnderneath)
        {
            var t = LocalTransform.FromPosition(0, 2, 0);
            EntityManager.AddComponent<LocalTransform>(SystemHandle);
            SystemAPI.GetComponentRW<LocalTransform>(SystemHandle).ValueRW = t;

            switch (memberUnderneath)
            {
                case MemberUnderneath.WithMemberUnderneath:
                    Assert.That(SystemAPI.GetComponentRW<LocalTransform>(SystemHandle).ValueRW, Is.EqualTo(t));
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    Assert.That(SystemAPI.GetComponentRW<LocalTransform>(SystemHandle).ValueRW, Is.EqualTo(t));
                    break;
            }
        }

        public void TestSetComponentForSystem()
        {
            var t = LocalTransform.FromPosition(0, 2, 0);
            EntityManager.AddComponentData(SystemHandle, t);

            t.Position += 1;
            SystemAPI.SetComponent(SystemHandle, t);
            Assert.That(SystemAPI.GetComponent<LocalTransform>(SystemHandle), Is.EqualTo(t));
        }

        public void TestHasComponentForSystem(MemberUnderneath memberUnderneath)
        {
            var t = LocalTransform.FromPosition(0, 2, 0);
            EntityManager.AddComponentData(SystemHandle, t);

            switch (memberUnderneath)
            {
                case MemberUnderneath.WithMemberUnderneath:
                    Assert.That(SystemAPI.HasComponent<LocalTransform>(SystemHandle).GetHashCode(), Is.EqualTo(1));
                    Assert.That(SystemAPI.HasComponent<EcsTestData>(SystemHandle).GetHashCode(), Is.EqualTo(0));
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    Assert.That(SystemAPI.HasComponent<LocalTransform>(SystemHandle), Is.EqualTo(true));
                    Assert.That(SystemAPI.HasComponent<EcsTestData>(SystemHandle), Is.EqualTo(false));
                    break;
            }
        }
        #endregion

        #region Buffer Access
        public void TestGetBufferLookup(SystemAPIAccess access, MemberUnderneath memberUnderneath, ReadAccess readAccess) {
            var e = EntityManager.CreateEntity();
            var t = new EcsIntElement { Value = 42 };
            var buffer = EntityManager.AddBuffer<EcsIntElement>(e);
            buffer.Add(t);

            switch (readAccess) {
                case ReadAccess.ReadOnly:
                    // Get works
                    switch (memberUnderneath) {
                        case MemberUnderneath.WithMemberUnderneath:
                            switch (access) {
                                case SystemAPIAccess.SystemAPI: {
                                    var tGet = SystemAPI.GetBufferLookup<EcsIntElement>(true)[e];
                                    Assert.That(tGet[0], Is.EqualTo(t));
                                } break;
                                case SystemAPIAccess.Using: {
                                    var tGet = GetBufferLookup<EcsIntElement>(true)[e];
                                    Assert.That(tGet[0], Is.EqualTo(t));
                                } break;
                            }
                            break;
                        case MemberUnderneath.WithoutMemberUnderneath:
                            switch (access) {
                                case SystemAPIAccess.SystemAPI: {
                                    var bfe = SystemAPI.GetBufferLookup<EcsIntElement>(true);
                                    var tGet = bfe[e];
                                    Assert.That(tGet[0], Is.EqualTo(t));
                                } break;
                                case SystemAPIAccess.Using: {
                                    var bfe = GetBufferLookup<EcsIntElement>(true);
                                    var tGet = bfe[e];
                                    Assert.That(tGet[0], Is.EqualTo(t));
                                } break;
                            }
                            break;
                    } break;

                // Set works
                case ReadAccess.ReadWrite: {
                    switch (memberUnderneath) {
                        case MemberUnderneath.WithMemberUnderneath:
                            switch (access) {
                                case SystemAPIAccess.SystemAPI: {
                                    t.Value += 1;
                                    var bfe = SystemAPI.GetBufferLookup<EcsIntElement>();
                                    bfe[e].ElementAt(0) = t;
                                    var tSet = SystemAPI.GetBufferLookup<EcsIntElement>(true)[e];
                                    Assert.That(tSet[0], Is.EqualTo(t));
                                } break;
                                case SystemAPIAccess.Using: {
                                    t.Value += 1;
                                    var bfe = GetBufferLookup<EcsIntElement>();
                                    bfe[e].ElementAt(0) = t;
                                    var tSet = GetBufferLookup<EcsIntElement>(true)[e];
                                    Assert.That(tSet[0], Is.EqualTo(t));
                                } break;
                            }
                            break;
                        case MemberUnderneath.WithoutMemberUnderneath:
                            switch (access) {
                                case SystemAPIAccess.SystemAPI: {
                                    t.Value += 1;
                                    var bfe = SystemAPI.GetBufferLookup<EcsIntElement>();
                                    bfe[e].ElementAt(0) = t;
                                    Assert.That(bfe[e][0], Is.EqualTo(t));
                                } break;
                                case SystemAPIAccess.Using: {
                                    t.Value += 1;
                                    var bfe = GetBufferLookup<EcsIntElement>();
                                    bfe[e].ElementAt(0) = t;
                                    Assert.That(bfe[e][0], Is.EqualTo(t));
                                } break;
                            }
                            break;
                    }
                } break;
            }
        }

        public void TestGetBuffer(SystemAPIAccess access, MemberUnderneath memberUnderneath) {
            var e = EntityManager.CreateEntity();
            var buffer = EntityManager.AddBuffer<EcsIntElement>(e);
            var t = new EcsIntElement() { Value = 42 };
            buffer.Add(t);

            switch (memberUnderneath) {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.GetBuffer<EcsIntElement>(e)[0].Value, Is.EqualTo(t.Value));
                            break;
                        case SystemAPIAccess.Using:
                            // Assert.That(GetBuffer<EcsIntElement>(e)[0].Value, Is.EqualTo(t.Value));
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.GetBuffer<EcsIntElement>(e)[0], Is.EqualTo(t));
                            break;
                        case SystemAPIAccess.Using:
                            // Assert.That(GetBuffer<EcsIntElement>(e)[0], Is.EqualTo(t));
                            break;
                    }
                    break;
            }
        }

        public void TestHasBuffer(SystemAPIAccess access, MemberUnderneath memberUnderneath) {
            var e = EntityManager.CreateEntity(typeof(EcsIntElement));

            switch (memberUnderneath) {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.HasBuffer<EcsIntElement>(e).GetHashCode(), Is.EqualTo(1));
                            Assert.That(SystemAPI.HasBuffer<EcsIntElement2>(e).GetHashCode(), Is.EqualTo(0));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(HasBuffer<EcsIntElement>(e).GetHashCode(), Is.EqualTo(1));
                            Assert.That(HasBuffer<EcsIntElement2>(e).GetHashCode(), Is.EqualTo(0));
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.HasBuffer<EcsIntElement>(e), Is.EqualTo(true));
                            Assert.That(SystemAPI.HasBuffer<EcsIntElement2>(e), Is.EqualTo(false));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(HasBuffer<EcsIntElement>(e), Is.EqualTo(true));
                            Assert.That(HasBuffer<EcsIntElement2>(e), Is.EqualTo(false));
                            break;
                    }
                    break;
            }
        }

        #endregion

        #region StorageInfo Access

        public void TestGetEntityStorageInfoLookup(SystemAPIAccess access)
        {
            var e = EntityManager.CreateEntity();

            switch (access) {
                case SystemAPIAccess.SystemAPI: {
                    var storageInfoLookup = SystemAPI.GetEntityStorageInfoLookup();
                    Assert.IsTrue(storageInfoLookup.Exists(e));
                } break;
                case SystemAPIAccess.Using: {
                    var storageInfoLookup = GetEntityStorageInfoLookup();
                    Assert.IsTrue(storageInfoLookup.Exists(e));
                } break;
            }
        }

        public void TestExists(SystemAPIAccess access)
        {
            var e = EntityManager.CreateEntity();

            switch (access) {
                case SystemAPIAccess.SystemAPI: {
                    Assert.IsTrue(SystemAPI.Exists(e));
                } break;
                case SystemAPIAccess.Using: {
                    //Assert.IsTrue(Exists(e));
                } break;
            }
        }

        #endregion

        #region Singleton Access
        public void TestGetSingleton()
        {
            var e = EntityManager.CreateEntity();
            EntityManager.AddComponentData(e, new EcsTestData(5));
            Assert.AreEqual(SystemAPI.GetSingleton<EcsTestData>().value, 5);
        }

        public void TestGetSingletonWithSystemEntity()
        {
            EntityManager.AddComponentData(SystemHandle, new EcsTestData(5));
            Assert.AreEqual(SystemAPI.GetSingleton<EcsTestData>().value, 5);
        }

        public void TestTryGetSingleton(TypeArgumentExplicit typeArgumentExplicit)
        {
            var e = EntityManager.CreateEntity();
            EntityManager.AddComponentData(e, new EcsTestData(5));

            switch (typeArgumentExplicit)
            {
                case TypeArgumentExplicit.TypeArgumentShown:
                    Assert.True(SystemAPI.TryGetSingleton<EcsTestData>(out var valSystemAPITypeArgumentShown));
                    Assert.AreEqual(valSystemAPITypeArgumentShown.value, 5);
                    break;
                case TypeArgumentExplicit.TypeArgumentHidden:
                    Assert.True(SystemAPI.TryGetSingleton(out EcsTestData valSystemAPITypeArgumentHidden));
                    Assert.AreEqual(valSystemAPITypeArgumentHidden.value, 5);
                    break;
            }
        }

        public void TestGetSingletonRW()
        {
            EntityManager.CreateEntity(typeof(EcsTestData));
            SystemAPI.GetSingletonRW<EcsTestData>().ValueRW.value = 5;
            Assert.AreEqual(5, SystemAPI.GetSingleton<EcsTestData>().value);
        }

        public void TestTryGetSingletonRW()
        {
            EntityManager.CreateEntity(typeof(EcsTestData));
            if (SystemAPI.TryGetSingletonRW<EcsTestData>(out var @ref))
                @ref.ValueRW.value = 5;
            Assert.AreEqual(5, SystemAPI.GetSingleton<EcsTestData>().value);
        }

        public void TestSetSingleton(TypeArgumentExplicit typeArgumentExplicit)
        {
            EntityManager.CreateEntity(typeof(EcsTestData));
            var data = new EcsTestData(5);
            switch (typeArgumentExplicit)
            {
                case TypeArgumentExplicit.TypeArgumentShown:
                    SystemAPI.SetSingleton<EcsTestData>(data);
                    break;
                case TypeArgumentExplicit.TypeArgumentHidden:
                    SystemAPI.SetSingleton(data);
                    break;
            }

            Assert.AreEqual(5, SystemAPI.GetSingleton<EcsTestData>().value);
        }

        public void TestGetSingletonEntity()
        {
            var e1 = EntityManager.CreateEntity(typeof(EcsTestData));
            Assert.AreEqual(e1, SystemAPI.GetSingletonEntity<EcsTestData>());
        }

        public void TestTryGetSingletonEntity()
        {
            var e1 = EntityManager.CreateEntity(typeof(EcsTestData));
            Assert.True(SystemAPI.TryGetSingletonEntity<EcsTestData>(out var e2));
            Assert.AreEqual(e1, e2);
        }

        public void TestGetSingletonBuffer()
        {
            var e = EntityManager.CreateEntity();
            var buffer1 = EntityManager.AddBuffer<EcsIntElement>(e);
            buffer1.Add(5);
            Assert.AreEqual(buffer1[0],SystemAPI.GetSingletonBuffer<EcsIntElement>()[0]);
        }

        public void TestGetSingletonBufferWithSystemEntity()
        {
            EntityManager.AddComponent<EcsIntElement>(SystemHandle);
            var buffer1 = EntityManager.GetBuffer<EcsIntElement>(SystemHandle);
            buffer1.Add(5);
            Assert.AreEqual(buffer1[0], SystemAPI.GetSingletonBuffer<EcsIntElement>()[0]);
        }

        public void TestTryGetSingletonBuffer(TypeArgumentExplicit typeArgumentExplicit)
        {
            var e = EntityManager.CreateEntity();
            var buffer1 = EntityManager.AddBuffer<EcsIntElement>(e);
            buffer1.Add(5);

            switch (typeArgumentExplicit)
            {
                case TypeArgumentExplicit.TypeArgumentShown:
                    Assert.True(SystemAPI.TryGetSingletonBuffer<EcsIntElement>(out var buffer2SystemAPITypeArgumentShown));
                    Assert.AreEqual(buffer1[0], buffer2SystemAPITypeArgumentShown[0]);
                    break;
                case TypeArgumentExplicit.TypeArgumentHidden:
                    Assert.True(SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<EcsIntElement> buffer2SystemAPITypeArgumentHidden));
                    Assert.AreEqual(buffer1[0], buffer2SystemAPITypeArgumentHidden[0]);
                    break;
            }
        }

        public void TestHasSingleton(SingletonVersion singletonVersion)
        {
            EntityManager.CreateEntity(typeof(EcsTestData), typeof(EcsIntElement));

            switch (singletonVersion)
            {
                case SingletonVersion.ComponentData:
                    Assert.True(SystemAPI.HasSingleton<EcsTestData>());
                    break;
                case SingletonVersion.Buffer:
                    Assert.True(SystemAPI.HasSingleton<EcsIntElement>());
                    break;
            }
        }

        #endregion

        #region Aspect

        public void TestGetAspect(SystemAPIAccess access)
        {
            var entity = EntityManager.CreateEntity(typeof(EcsTestData));
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    SystemAPI.GetAspect<EcsTestAspect0RW>(entity ).EcsTestData.ValueRW.value = 5;
                    break;
                case SystemAPIAccess.Using:
                    SystemAPI.GetAspect<EcsTestAspect0RW>(entity).EcsTestData.ValueRW.value = 5;
                    break;
            }

            Assert.AreEqual(5, SystemAPI.GetComponent<EcsTestData>(entity).value);
        }

        #endregion

        #region NoError

        void NestingSetup()
        {
            // Setup Archetypes
            var playerArchetype = EntityManager.CreateArchetype(new FixedList128Bytes<ComponentType> {
                ComponentType.ReadWrite<EcsTestFloatData>(), ComponentType.ReadWrite<LocalTransform>(), ComponentType.ReadWrite<EcsTestTag>()
            }.ToNativeArray(World.UpdateAllocator.ToAllocator));
            var coinArchetype = EntityManager.CreateArchetype(new FixedList128Bytes<ComponentType> {
                ComponentType.ReadWrite<EcsTestFloatData>(), ComponentType.ReadWrite<LocalTransform>(),
            }.ToNativeArray(World.UpdateAllocator.ToAllocator));
            var coinCounterArchetype = EntityManager.CreateArchetype(new FixedList128Bytes<ComponentType> {
                ComponentType.ReadWrite<EcsTestData>()
            }.ToNativeArray(World.UpdateAllocator.ToAllocator));

            // Setup Players
            var players = EntityManager.CreateEntity(playerArchetype, 5, World.UpdateAllocator.ToAllocator);
            foreach (var player in players)
                SystemAPI.SetComponent(player, new EcsTestFloatData {Value = 0.1f});
            SystemAPI.SetComponent(players[0], LocalTransform.FromPosition(0,1,0));
            SystemAPI.SetComponent(players[1], LocalTransform.FromPosition(1,1,0));
            SystemAPI.SetComponent(players[2], LocalTransform.FromPosition(0,1,1));
            SystemAPI.SetComponent(players[3], LocalTransform.FromPosition(1,1,1));
            SystemAPI.SetComponent(players[4], LocalTransform.FromPosition(1,0,1));

            // Setup Enemies
            var coins = EntityManager.CreateEntity(coinArchetype, 5, World.UpdateAllocator.ToAllocator);
            foreach (var coin in coins)
                SystemAPI.SetComponent(coin, new EcsTestFloatData {Value = 1f});
            SystemAPI.SetComponent(coins[0], LocalTransform.FromPosition(0,1,0));
            SystemAPI.SetComponent(coins[1], LocalTransform.FromPosition(1,1,0));
            SystemAPI.SetComponent(coins[2], LocalTransform.FromPosition(0,1,1));
            SystemAPI.SetComponent(coins[3], LocalTransform.FromPosition(1,1,1));
            SystemAPI.SetComponent(coins[4], LocalTransform.FromPosition(1,0,1));

            // Setup Coin Counter
            EntityManager.CreateEntity(coinCounterArchetype);
        }

        public void TestNesting()
        {
            NestingSetup();

            foreach (var (playerTranslation, playerRadius) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EcsTestFloatData>>().WithAll<EcsTestTag>())
            foreach (var (coinTranslation, coinRadius, coinEntity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EcsTestFloatData>>().WithEntityAccess().WithNone<EcsTestTag>())
                if (math.distancesq(playerTranslation.ValueRO.Position, coinTranslation.ValueRO.Position) < coinRadius.ValueRO.Value + playerRadius.ValueRO.Value)
                    SystemAPI.GetSingletonRW<EcsTestData>().ValueRW.value++; // Three-layer SystemAPI nesting

            var coinsCollected = SystemAPI.GetSingleton<EcsTestData>().value;
            Assert.AreEqual(15, coinsCollected);
        }

        /// <summary>
        /// This will throw in cases where SystemAPI doesn't properly insert .Update and .CompleteDependencyXX statements.
        /// </summary>
        public void TestStatementInsert()
        {
            // Asserts that does not throw - Not using Assert.DoesNotThrow since a lambda capture to ref state will fail.
            foreach (var (transform, target) in Query<RefRO<LocalTransform>, RefRO<EcsTestDataEntity>>())
            {
                if (SystemAPI.Exists(target.ValueRO.value1))
                {
                    var targetTransform = SystemAPI.GetComponent<LocalTransform>(target.ValueRO.value1);
                    var src = transform.ValueRO.Position;
                    var dst = targetTransform.Position;
                    Assert.That(src, Is.Not.EqualTo(dst));
                }
            }
        }

        public void TestGenericTypeArgument()
        {
            Assert.False(SystemAPI.HasSingleton<EcsTestGenericTag<int>>());
        }

        public partial class GenericSystem<T> : SystemBase where T : unmanaged, IComponentData {
            protected override void OnUpdate() {}

            public void TestGenericSystem() {
                var e = EntityManager.CreateEntity(typeof(EcsTestData));
                Assert.True(SystemAPI.HasComponent<T>(e));
            }
        }
        
        public partial struct GenericISystem<T> : ISystem where T : unmanaged, IComponentData {
            public void OnUpdate() {}

            public void TestGenericSystem(ref SystemState state) {
                var e = state.EntityManager.CreateEntity(typeof(EcsTestData));
                Assert.True(SystemAPI.HasComponent<T>(e));
            }
        }

        public partial class VariableInOnCreateSystem : SystemBase {
            protected override void OnCreate() {
                var readOnly = true;
                var lookup = GetComponentLookup<EcsTestData>(readOnly);
            }

            protected override void OnUpdate() {}
        }

        #endregion
    }
}
