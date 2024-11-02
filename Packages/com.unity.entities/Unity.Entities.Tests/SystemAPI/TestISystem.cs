using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Entities.Tests;
using Unity.Entities.Tests.TestSystemAPI;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Entities.SystemAPI;

[assembly: RegisterGenericSystemType(typeof(TestISystemSystem.TestGenericISystem_MyInterface<MyInterfaceImpl>))]
[assembly: RegisterGenericSystemType(typeof(TestISystemSystem.TestGenericISystem_BufferElement<EcsIntElement>))]

namespace Unity.Entities.Tests.TestSystemAPI
{
    /// <summary>
    /// Make sure this matches <see cref="TestSystemBase"/>.
    /// </summary>
    [TestFixture]
    public class TestISystem : ECSTestsFixture
    {
        [SetUp]
        public void SetUp() => World.CreateSystem<TestISystemSystem>();

        unsafe ref TestISystemSystem GetTestSystemUnsafe()
        {
            var systemHandle = World.GetExistingSystem<TestISystemSystem>();
            if (systemHandle == default)
                throw new InvalidOperationException("This system does not exist any more");
            return ref World.Unmanaged.GetUnsafeSystemRef<TestISystemSystem>(systemHandle);
        }

        unsafe ref SystemState GetSystemStateRef()
        {
            var systemHandle = World.GetExistingSystem<TestISystemSystem>();
            var statePtr = World.Unmanaged.ResolveSystemState(systemHandle);
            if (statePtr == null)
                throw new InvalidOperationException("No system state exists any more for this SystemRef");
            return ref *statePtr;
        }
        #region Query Access
        [Test]
        public void Query([Values] SystemAPIAccess access, [Values(1,2,3,4,5,6,7)] int queryArgumentCount) => GetTestSystemUnsafe().QuerySetup(ref GetSystemStateRef(), queryArgumentCount, access);
        #endregion

        #region Time Access
        [Test]
        public void Time([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath) => GetTestSystemUnsafe().TestTime(ref GetSystemStateRef(), access, memberUnderneath);
        #endregion

        #region Component Access

        [Test]
        public void GetComponentLookup([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestGetComponentLookup(ref GetSystemStateRef(), access, memberUnderneath, readAccess);

        [Test]
        public void GetComponent([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestGetComponent(ref GetSystemStateRef(), access, memberUnderneath);

        [Test]
        public void GetComponentRW([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestGetComponentRW(ref GetSystemStateRef(), access, memberUnderneath);

        [Test]
        public void SetComponent([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestSetComponent(ref GetSystemStateRef(), access);

        [Test]
        public void HasComponent([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestHasComponent(ref GetSystemStateRef(), access, memberUnderneath);

        [Test]
        public void GetComponentForSystem([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestGetComponentForSystem(ref GetSystemStateRef(), access, memberUnderneath);

        [Test]
        public void GetComponentRWForSystem([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestGetComponentRWForSystem(ref GetSystemStateRef(), access, memberUnderneath);

        [Test]
        public void SetComponentForSystem([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestSetComponentForSystem(ref GetSystemStateRef(), access);

        [Test]
        public void HasComponentForSystem([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestHasComponentForSystem(ref GetSystemStateRef(), access, memberUnderneath);
        [Test]
        public void IsComponentEnabledForSystem() => GetTestSystemUnsafe().TestIsComponentEnabledForSystem(ref GetSystemStateRef());
        [Test]
        public void SetComponentEnabledForSystem() => GetTestSystemUnsafe().TestSetComponentEnabledForSystem(ref GetSystemStateRef());
        #endregion

        #region Buffer Access

        [Test]
        public void GetBufferLookup([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestGetBufferLookup(ref GetSystemStateRef(), access, memberUnderneath, readAccess);

        [Test]
        public void GetBuffer([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestGetBuffer(ref GetSystemStateRef(), access, memberUnderneath);

        [Test]
        public void HasBuffer([Values] SystemAPIAccess access, [Values] MemberUnderneath memberUnderneath, [Values] ReadAccess readAccess) => GetTestSystemUnsafe().TestHasBuffer(ref GetSystemStateRef(), access, memberUnderneath);

        [Test]
        public void IsBufferEnabled() => GetTestSystemUnsafe().TestIsBufferEnabled(ref GetSystemStateRef());
        [Test]
        public void SetBufferEnabled() => GetTestSystemUnsafe().TestSetBufferEnabled(ref GetSystemStateRef());


        #endregion

        #region StorageInfo Access

        [Test]
        public void GetEntityStorageInfoLookup([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestGetEntityStorageInfoLookup(ref GetSystemStateRef(), access);

        [Test]
        public void Exists([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestExists(ref GetSystemStateRef(), access);

        #endregion

        #region Singleton

        [Test]
        public void GetSingleton([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestGetSingleton(ref GetSystemStateRef(), access);
        [Test]
        public void GetSingletonWithSystemEntity([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestGetSingletonWithSystemEntity(ref GetSystemStateRef(), access);
        [Test]
        public void TryGetSingleton([Values] SystemAPIAccess access, [Values] TypeArgumentExplicit typeArgumentExplicit) => GetTestSystemUnsafe().TestTryGetSingleton(ref GetSystemStateRef(), access, typeArgumentExplicit);
        [Test]
        public void GetSingletonRW([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestGetSingletonRW(ref GetSystemStateRef(), access);
        [Test]
        public void TryGetSingletonRW([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestTryGetSingletonRW(ref GetSystemStateRef(), access);
        [Test]
        public void SetSingleton([Values] SystemAPIAccess access, [Values] TypeArgumentExplicit typeArgumentExplicit) => GetTestSystemUnsafe().TestSetSingleton(ref GetSystemStateRef(), access, typeArgumentExplicit);
        [Test]
        public void GetSingletonEntity([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestGetSingletonEntity(ref GetSystemStateRef(), access);
        [Test]
        public void TryGetSingletonEntity([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestTryGetSingletonEntity(ref GetSystemStateRef(), access);
        [Test]
        public void GetSingletonBuffer([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestGetSingletonBuffer(ref GetSystemStateRef(), access);
        [Test]
        public void GetSingletonBufferWithSystemEntity([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestGetSingletonBufferWithSystemEntity(ref GetSystemStateRef(), access);
        [Test]
        public void TryGetSingletonBuffer([Values] SystemAPIAccess access, [Values] TypeArgumentExplicit typeArgumentExplicit) => GetTestSystemUnsafe().TestTryGetSingletonBuffer(ref GetSystemStateRef(), access, typeArgumentExplicit);
        [Test]
        public void HasSingleton([Values] SystemAPIAccess access, [Values] SingletonVersion singletonVersion) => GetTestSystemUnsafe().TestHasSingleton(ref GetSystemStateRef(), access, singletonVersion);
        #endregion

        #region Aspect

        [Test]
        public void GetAspectRW([Values] SystemAPIAccess access) => GetTestSystemUnsafe().TestGetAspectRW(ref GetSystemStateRef(), access);

        #endregion

        #region Handles

        [Test]
        public void GetEntityTypeHandle() => GetTestSystemUnsafe().TestGetEntityTypeHandle(ref GetSystemStateRef());
        [Test]
        public void GetComponentTypeHandle() => GetTestSystemUnsafe().TestGetComponentTypeHandle(ref GetSystemStateRef());
        [Test]
        public void GetBufferTypeHandle() => GetTestSystemUnsafe().TestGetBufferTypeHandle(ref GetSystemStateRef());
        [Test]
        public void GetSharedComponentTypeHandle() => GetTestSystemUnsafe().TestGetSharedComponentTypeHandle(ref GetSystemStateRef());

        #endregion

        #region NoError
        [Test]
        public void Nesting() => GetTestSystemUnsafe().TestNesting(ref GetSystemStateRef());

        [Test]
        public void StatementInsert() => GetTestSystemUnsafe().TestStatementInsert(ref GetSystemStateRef());
        [Test]
        public void GenericTypeArgument() => GetTestSystemUnsafe().TestGenericTypeArgument(ref GetSystemStateRef());

        [Test]
        public void VariableInOnCreate() => World.CreateSystem<TestISystemSystem.VariableInOnCreateSystem>();

        [Test]
        public void PropertyWithOnlyGetter() => World.CreateSystem<TestISystemSystem.PropertyWithOnlyGetterSystem>().Update(World.Unmanaged);

        [Test]
        public void GenericSystem1() => World.CreateSystem<TestISystemSystem.TestGenericISystem_MyInterface<MyInterfaceImpl>>().Update(World.Unmanaged);

        [Test]
        public void GenericSystem2() => World.CreateSystem<TestISystemSystem.TestGenericISystem_BufferElement<EcsIntElement>>().Update(World.Unmanaged);

        [Test]
        public void ExplicitInterfaceImplementation() => World.CreateSystem<TestISystemSystem.ExplicitInterfaceImplementationSystem>().Update(World.Unmanaged);
        #endregion
    }

    public interface IMyInterface : IBufferElementData
    {
        public int Value { get; set; }
    }

    struct MyInterfaceImpl : IMyInterface
    {
        public int Value { get; set; }
    }
    [BurstCompile]
    partial struct TestISystemSystem : ISystem
    {
        #region Query Access
        [BurstCompile]
        public void QuerySetup(ref SystemState state, int queryArgumentCount, SystemAPIAccess access)
        {
            for (var i = 1; i <= 10; i++)
            {
                var e = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(e, LocalTransform.FromPosition(i, i, i));
                state.EntityManager.AddComponentData(e, new LocalToWorld());

                state.EntityManager.AddComponentData(e, new EcsTestData(i));
                state.EntityManager.AddComponentData(e, new EcsTestData2(i));
                state.EntityManager.AddComponentData(e, new EcsTestData3(i));
                state.EntityManager.AddComponentData(e, new EcsTestData4(i));
                state.EntityManager.AddComponentData(e, new EcsTestData5(i));
                state.EntityManager.AddComponentData(e, new EcsTestDataEnableable(i));
                state.EntityManager.AddComponentData(e, new EcsTestDataEnableable2(i));
            }

            Assert.AreEqual(55*queryArgumentCount, queryArgumentCount switch
            {
                1 => Query1(ref state, access),
                2 => Query2(ref state, access),
                3 => Query3(ref state, access),
                4 => Query4(ref state, access),
                5 => Query5(ref state, access),
                6 => Query6(ref state, access),
                7 => Query7(ref state, access),
                _ => throw new ArgumentOutOfRangeException(nameof(queryArgumentCount), queryArgumentCount, null)
            });
        }

        [BurstCompile]
        int Query1(ref SystemState state, SystemAPIAccess access)
        {
            var sum = 0;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    foreach (var transform in Query<RefRO<LocalTransform>>())
                        sum += (int) transform.ValueRO.Position.x;
                    break;
                case SystemAPIAccess.Using:
                    foreach (var transform in Query<RefRO<LocalTransform>>())
                        sum += (int) transform.ValueRO.Position.x;
                    break;
            }
            return sum;
        }

        [BurstCompile]
        int Query2(ref SystemState state, SystemAPIAccess access)
        {
            var sum = 0;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    foreach (var (transform, data1) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EcsTestData>>())
                    {
                        sum += (int)transform.ValueRO.Position.x;
                        sum += data1.ValueRO.value;
                    }
                    break;
                case SystemAPIAccess.Using:
                    foreach (var (transform, data1) in Query<RefRO<LocalTransform>, RefRW<EcsTestData>>())
                    {
                        sum += (int)transform.ValueRO.Position.x;
                        sum += data1.ValueRO.value;
                    }
                    break;
            }
            return sum;
        }

        [BurstCompile]
        int Query3(ref SystemState state, SystemAPIAccess access)
        {
            var sum = 0;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    foreach (var (transform, data1, data2) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>>())
                    {
                        sum += (int)transform.ValueRO.Position.x;
                        sum += data1.ValueRO.value;
                        sum += data2.ValueRO.value0;
                    }
                    break;
                case SystemAPIAccess.Using:
                    foreach (var (transform, data1, data2) in Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>>())
                    {
                        sum += (int)transform.ValueRO.Position.x;
                        sum += data1.ValueRO.value;
                        sum += data2.ValueRO.value0;
                    }
                    break;
            }
            return sum;
        }

        [BurstCompile]
        int Query4(ref SystemState state, SystemAPIAccess access)
        {
            var sum = 0;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    foreach (var (transform, data1, data2, data3) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>, RefRW<EcsTestData3>>())
                    {
                        sum += (int)transform.ValueRO.Position.x;
                        sum += data1.ValueRO.value;
                        sum += data2.ValueRO.value0;
                        sum += data3.ValueRO.value0;
                    }
                    break;
                case SystemAPIAccess.Using:
                    foreach (var (transform, data1, data2, data3) in Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>, RefRW<EcsTestData3>>())
                    {
                        sum += (int)transform.ValueRO.Position.x;
                        sum += data1.ValueRO.value;
                        sum += data2.ValueRO.value0;
                        sum += data3.ValueRO.value0;
                    }
                    break;
            }
            return sum;
        }

        [BurstCompile]
        int Query5(ref SystemState state, SystemAPIAccess access)
        {
            var sum = 0;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    foreach (var (transform, data1, data2, data3, data4) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>, RefRW<EcsTestData3>, RefRW<EcsTestData4>>())
                    {
                        sum += (int)transform.ValueRO.Position.x;
                        sum += data1.ValueRO.value;
                        sum += data2.ValueRO.value0;
                        sum += data3.ValueRO.value0;
                        sum += data4.ValueRO.value0;
                    }
                    break;
                case SystemAPIAccess.Using:
                    foreach (var (transform, data1, data2, data3, data4) in Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>, RefRW<EcsTestData3>, RefRW<EcsTestData4>>())
                    {
                        sum += (int)transform.ValueRO.Position.x;
                        sum += data1.ValueRO.value;
                        sum += data2.ValueRO.value0;
                        sum += data3.ValueRO.value0;
                        sum += data4.ValueRO.value0;
                    }
                    break;
            }
            return sum;
        }

        [BurstCompile]
        int Query6(ref SystemState state, SystemAPIAccess access)
        {
            var sum = 0;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    foreach (var (transform, data1, data2, data3, data4, data5) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>, RefRW<EcsTestData3>, RefRW<EcsTestData4>, RefRW<EcsTestData5>>())
                    {
                        sum += (int)transform.ValueRO.Position.x;
                        sum += data1.ValueRO.value;
                        sum += data2.ValueRO.value0;
                        sum += data3.ValueRO.value0;
                        sum += data4.ValueRO.value0;
                        sum += data5.ValueRO.value0;
                    }
                    break;
                case SystemAPIAccess.Using:
                    foreach (var (transform, data1, data2, data3, data4, data5) in Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>, RefRW<EcsTestData3>, RefRW<EcsTestData4>, RefRW<EcsTestData5>>())
                    {
                        sum += (int)transform.ValueRO.Position.x;
                        sum += data1.ValueRO.value;
                        sum += data2.ValueRO.value0;
                        sum += data3.ValueRO.value0;
                        sum += data4.ValueRO.value0;
                        sum += data5.ValueRO.value0;
                    }
                    break;
            }
            return sum;
        }

        [BurstCompile]
        int Query7(ref SystemState state, SystemAPIAccess access)
        {
            var sum = 0;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
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
                    break;
                case SystemAPIAccess.Using:
                    foreach (var (transform, data1, data2, data3, data4, data5, data6) in Query<RefRO<LocalTransform>, RefRW<EcsTestData>, RefRW<EcsTestData2>, RefRW<EcsTestData3>, RefRW<EcsTestData4>, RefRW<EcsTestData5>, RefRW<EcsTestDataEnableable>>())
                    {
                        sum += (int)transform.ValueRO.Position.x;
                        sum += data1.ValueRO.value;
                        sum += data2.ValueRO.value0;
                        sum += data3.ValueRO.value0;
                        sum += data4.ValueRO.value0;
                        sum += data5.ValueRO.value0;
                        sum += data6.ValueRO.value;
                    }
                    break;
            }
            return sum;
        }

        #endregion

        #region Time Access

        [BurstCompile]
        public void TestTime(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath)
        {
            var time = new TimeData(42, 0.5f);
            state.World.SetTime(time);

            if (access == SystemAPIAccess.SystemAPI) {
                if (memberUnderneath == MemberUnderneath.WithMemberUnderneath) {
                    Assert.That(SystemAPI.Time.DeltaTime, Is.EqualTo(time.DeltaTime));
                } else if (memberUnderneath == MemberUnderneath.WithoutMemberUnderneath) {
                    Assert.That(SystemAPI.Time, Is.EqualTo(time));
                }
            } else if (access == SystemAPIAccess.Using) {
                if (memberUnderneath == MemberUnderneath.WithMemberUnderneath) {
                    Assert.That(Time.DeltaTime, Is.EqualTo(time.DeltaTime));
                } else if (memberUnderneath == MemberUnderneath.WithoutMemberUnderneath) {
                    Assert.That(Time, Is.EqualTo(time));
                }
            }
        }
        #endregion

        #region Component Access
        [BurstCompile]
        public void TestGetComponentLookup(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath, ReadAccess readAccess) {
            var e = state.EntityManager.CreateEntity();
            var t = LocalTransform.FromPosition(0, 2, 0);
            state.EntityManager.AddComponentData(e, t);

            switch (readAccess) {
                case ReadAccess.ReadOnly:
                    // Get works
                    switch (memberUnderneath) {
                        case MemberUnderneath.WithMemberUnderneath:
                            switch (access) {
                                case SystemAPIAccess.SystemAPI: {
                                    var tGet = SystemAPI.GetComponentLookup<LocalTransform>(true)[e];
                                    Assert.That(tGet, Is.EqualTo(t));
                                } break;
                                case SystemAPIAccess.Using: {
                                    var tGet = GetComponentLookup<LocalTransform>(true)[e];
                                    Assert.That(tGet, Is.EqualTo(t));
                                } break;
                            }
                            break;
                        case MemberUnderneath.WithoutMemberUnderneath:
                            switch (access) {
                                case SystemAPIAccess.SystemAPI: {
                                    var cdfe = SystemAPI.GetComponentLookup<LocalTransform>(true);
                                    var tGet = cdfe[e];
                                    Assert.That(tGet, Is.EqualTo(t));
                                } break;
                                case SystemAPIAccess.Using: {
                                    var cdfe = GetComponentLookup<LocalTransform>(true);
                                    var tGet = cdfe[e];
                                    Assert.That(tGet, Is.EqualTo(t));
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
                                    t.Position += 1;
                                    var cdfe = SystemAPI.GetComponentLookup<LocalTransform>();
                                    cdfe[e] = t;
                                    var tSet = SystemAPI.GetComponentLookup<LocalTransform>(true)[e];
                                    Assert.That(tSet, Is.EqualTo(t));
                                } break;
                                case SystemAPIAccess.Using: {
                                    t.Position += 1;
                                    var cdfe = GetComponentLookup<LocalTransform>();
                                    cdfe[e] = t;
                                    var tSet = GetComponentLookup<LocalTransform>(true)[e];
                                    Assert.That(tSet, Is.EqualTo(t));
                                } break;
                            }
                            break;
                        case MemberUnderneath.WithoutMemberUnderneath:
                            switch (access) {
                                case SystemAPIAccess.SystemAPI: {
                                    t.Position += 1;
                                    var cdfe = SystemAPI.GetComponentLookup<LocalTransform>();
                                    cdfe[e] = t;
                                    Assert.That(cdfe[e], Is.EqualTo(t));
                                } break;
                                case SystemAPIAccess.Using: {
                                    t.Position += 1;
                                    var cdfe = GetComponentLookup<LocalTransform>();
                                    cdfe[e] = t;
                                    Assert.That(cdfe[e], Is.EqualTo(t));
                                } break;
                            }
                            break;
                    }
                } break;
            }
        }

        [BurstCompile]
        public void TestGetComponent(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath) {
            var e = state.EntityManager.CreateEntity();
            var t = LocalTransform.FromPosition(0, 2, 0);
            state.EntityManager.AddComponentData(e, t);

            switch (memberUnderneath) {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.GetComponent<LocalTransform>(e), Is.EqualTo(t));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(GetComponent<LocalTransform>(e), Is.EqualTo(t));
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.GetComponent<LocalTransform>(e), Is.EqualTo(t));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(GetComponent<LocalTransform>(e), Is.EqualTo(t));
                            break;
                    }
                    break;
            }
        }

        [BurstCompile]
        public void TestGetComponentRW(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath) {
            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<LocalTransform>(e);
            var t = LocalTransform.FromPosition(0, 2, 0);
            SystemAPI.GetComponentRW<LocalTransform>(e).ValueRW = t;

            switch (memberUnderneath) {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.GetComponentRW<LocalTransform>(e).ValueRO, Is.EqualTo(t));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(GetComponentRW<LocalTransform>(e).ValueRO, Is.EqualTo(t));
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.GetComponentRW<LocalTransform>(e).ValueRO, Is.EqualTo(t));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(GetComponentRW<LocalTransform>(e).ValueRO, Is.EqualTo(t));
                            break;
                    }
                    break;
            }
        }

        [BurstCompile]
        public void TestSetComponent(ref SystemState state, SystemAPIAccess access) {
            var e = state.EntityManager.CreateEntity();
            var t = LocalTransform.FromPosition(0, 2, 0);
            state.EntityManager.AddComponentData(e, t);


            switch (access) {
                case SystemAPIAccess.SystemAPI:
                    t.Position += 1;
                    SystemAPI.SetComponent(e, t);
                    Assert.That(SystemAPI.GetComponent<LocalTransform>(e), Is.EqualTo(t));
                    break;
                case SystemAPIAccess.Using:
                    t.Position += 1;
                    SetComponent(e, t);
                    Assert.That(GetComponent<LocalTransform>(e), Is.EqualTo(t));
                    break;
            }
        }

        [BurstCompile]
        public void TestHasComponent(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath) {
            var e = state.EntityManager.CreateEntity(typeof(LocalTransform));

            switch (memberUnderneath) {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.HasComponent<LocalTransform>(e).GetHashCode(), Is.EqualTo(1));
                            Assert.That(SystemAPI.HasComponent<EcsTestData>(e).GetHashCode(), Is.EqualTo(0));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(HasComponent<LocalTransform>(e).GetHashCode(), Is.EqualTo(1));
                            Assert.That(HasComponent<EcsTestData>(e).GetHashCode(), Is.EqualTo(0));
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.HasComponent<LocalTransform>(e), Is.EqualTo(true));
                            Assert.That(SystemAPI.HasComponent<EcsTestData>(e), Is.EqualTo(false));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(HasComponent<LocalTransform>(e), Is.EqualTo(true));
                            Assert.That(HasComponent<EcsTestData>(e), Is.EqualTo(false));
                            break;
                    }
                    break;
            }
        }

        [BurstCompile]
        public void TestGetComponentForSystem(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath)
        {
            var t = LocalTransform.FromPosition(0, 2, 0);
            state.EntityManager.AddComponentData(state.SystemHandle, t);

            switch (memberUnderneath)
            {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access)
                    {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.GetComponent<LocalTransform>(state.SystemHandle), Is.EqualTo(t));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(GetComponent<LocalTransform>(state.SystemHandle), Is.EqualTo(t));
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access)
                    {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.GetComponent<LocalTransform>(state.SystemHandle), Is.EqualTo(t));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(GetComponent<LocalTransform>(state.SystemHandle), Is.EqualTo(t));
                            break;
                    }
                    break;
            }
        }

        [BurstCompile]
        public void TestGetComponentRWForSystem(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath)
        {
            var t = LocalTransform.FromPosition(0, 2, 0);
            state.EntityManager.AddComponent<LocalTransform>(state.SystemHandle);
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    SystemAPI.GetComponentRW<LocalTransform>(state.SystemHandle).ValueRW = t;
                    break;
                case SystemAPIAccess.Using:
                    GetComponentRW<LocalTransform>(state.SystemHandle).ValueRW = t;
                    break;
            }

            switch (memberUnderneath)
            {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access)
                    {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.GetComponentRW<LocalTransform>(state.SystemHandle).ValueRW, Is.EqualTo(t));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(GetComponentRW<LocalTransform>(state.SystemHandle).ValueRW, Is.EqualTo(t));
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access)
                    {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.GetComponentRW<LocalTransform>(state.SystemHandle).ValueRW, Is.EqualTo(t));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(GetComponentRW<LocalTransform>(state.SystemHandle).ValueRW, Is.EqualTo(t));
                            break;
                    }
                    break;
            }
        }

        [BurstCompile]
        public void TestSetComponentForSystem(ref SystemState state, SystemAPIAccess access)
        {
            var t = LocalTransform.FromPosition(0, 2, 0);
            state.EntityManager.AddComponentData(state.SystemHandle, t);
            state.EntityManager.AddComponentData(state.SystemHandle, t);

            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    t.Position += 1;
                    SystemAPI.SetComponent(state.SystemHandle, t);
                    Assert.That(SystemAPI.GetComponent<LocalTransform>(state.SystemHandle), Is.EqualTo(t));
                    break;
                case SystemAPIAccess.Using:
                    t.Position += 1;
                    SetComponent(state.SystemHandle, t);
                    Assert.That(GetComponent<LocalTransform>(state.SystemHandle), Is.EqualTo(t));
                    break;
            }
        }

        [BurstCompile]
        public void TestHasComponentForSystem(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath)
        {
            state.EntityManager.AddComponent(state.SystemHandle, ComponentType.ReadWrite<LocalTransform>());

            switch (memberUnderneath)
            {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access)
                    {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.HasComponent<LocalTransform>(state.SystemHandle).GetHashCode(), Is.EqualTo(1));
                            Assert.That(SystemAPI.HasComponent<EcsTestData>(state.SystemHandle).GetHashCode(), Is.EqualTo(0));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(HasComponent<LocalTransform>(state.SystemHandle).GetHashCode(), Is.EqualTo(1));
                            Assert.That(HasComponent<EcsTestData>(state.SystemHandle).GetHashCode(), Is.EqualTo(0));
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access)
                    {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.HasComponent<LocalTransform>(state.SystemHandle), Is.EqualTo(true));
                            Assert.That(SystemAPI.HasComponent<EcsTestData>(state.SystemHandle), Is.EqualTo(false));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(HasComponent<LocalTransform>(state.SystemHandle), Is.EqualTo(true));
                            Assert.That(HasComponent<EcsTestData>(state.SystemHandle), Is.EqualTo(false));
                            break;
                    }
                    break;
            }
        }

        [BurstCompile]
        public void TestIsComponentEnabledForSystem(ref SystemState state)
        {
            state.EntityManager.AddComponent(state.SystemHandle, ComponentType.ReadWrite<EcsTestDataEnableable>());
            Assert.That(IsComponentEnabled<EcsTestDataEnableable>(state.SystemHandle), Is.EqualTo(true));
        }

        [BurstCompile]
        public void TestSetComponentEnabledForSystem(ref SystemState state)
        {
            state.EntityManager.AddComponent(state.SystemHandle, ComponentType.ReadWrite<EcsTestDataEnableable>());
            SetComponentEnabled<EcsTestDataEnableable>(state.SystemHandle, false);
            Assert.That(state.EntityManager.IsComponentEnabled<EcsTestDataEnableable>(state.SystemHandle.m_Entity), Is.EqualTo(false));
            SetComponentEnabled<EcsTestDataEnableable>(state.SystemHandle, true);
            Assert.That(state.EntityManager.IsComponentEnabled<EcsTestDataEnableable>(state.SystemHandle.m_Entity), Is.EqualTo(true));
        }

        #endregion

        #region Buffer Access

        [BurstCompile]
        public void TestGetBufferLookup(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath, ReadAccess readAccess) {
            var e = state.EntityManager.CreateEntity();
            var t = new EcsIntElement { Value = 42 };
            var buffer = state.EntityManager.AddBuffer<EcsIntElement>(e);
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

        [BurstCompile]
        public void TestGetBuffer(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath) {
            var e = state.EntityManager.CreateEntity();
            var buffer = state.EntityManager.AddBuffer<EcsIntElement>(e);
            var t = new EcsIntElement() { Value = 42 };
            buffer.Add(t);

            switch (memberUnderneath) {
                case MemberUnderneath.WithMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.GetBuffer<EcsIntElement>(e)[0].Value, Is.EqualTo(t.Value));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(GetBuffer<EcsIntElement>(e)[0].Value, Is.EqualTo(t.Value));
                            break;
                    }
                    break;
                case MemberUnderneath.WithoutMemberUnderneath:
                    switch (access) {
                        case SystemAPIAccess.SystemAPI:
                            Assert.That(SystemAPI.GetBuffer<EcsIntElement>(e)[0], Is.EqualTo(t));
                            break;
                        case SystemAPIAccess.Using:
                            Assert.That(GetBuffer<EcsIntElement>(e)[0], Is.EqualTo(t));
                            break;
                    }
                    break;
            }
        }

        [BurstCompile]
        public void TestHasBuffer(ref SystemState state, SystemAPIAccess access, MemberUnderneath memberUnderneath) {
            var e = state.EntityManager.CreateEntity(typeof(EcsIntElement));

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

        [BurstCompile]
        public void TestIsBufferEnabled(ref SystemState state)
        {
            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<EcsIntElementEnableable>(e);
            Assert.That(IsBufferEnabled<EcsIntElementEnableable>(e), Is.EqualTo(true));
        }

        [BurstCompile]
        public void TestSetBufferEnabled(ref SystemState state)
        {
            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<EcsIntElementEnableable>(e);
            SetBufferEnabled<EcsIntElementEnableable>(e, false);
            Assert.That(state.EntityManager.IsComponentEnabled<EcsIntElementEnableable>(e), Is.EqualTo(false));
            SetBufferEnabled<EcsIntElementEnableable>(e, true);
            Assert.That(state.EntityManager.IsComponentEnabled<EcsIntElementEnableable>(e), Is.EqualTo(true));
        }

        #endregion

        #region StorageInfo Access

        [BurstCompile]
        public void TestGetEntityStorageInfoLookup(ref SystemState state, SystemAPIAccess access)
        {
            var e = state.EntityManager.CreateEntity();

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

        [BurstCompile]
        public void TestExists(ref SystemState state, SystemAPIAccess access)
        {
            var e = state.EntityManager.CreateEntity();

            switch (access) {
                case SystemAPIAccess.SystemAPI: {
                    Assert.IsTrue(SystemAPI.Exists(e));
                } break;
                case SystemAPIAccess.Using: {
                    Assert.IsTrue(Exists(e));
                } break;
            }
        }

        #endregion

        #region Singleton Access
        [BurstCompile]
        public void TestGetSingleton(ref SystemState state, SystemAPIAccess access)
        {
            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(e, new EcsTestData(5));
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    Assert.AreEqual(SystemAPI.GetSingleton<EcsTestData>().value, 5);
                    break;
                case SystemAPIAccess.Using:
                    Assert.AreEqual(GetSingleton<EcsTestData>().value, 5);
                    break;
            }
        }

        [BurstCompile]
        public void TestGetSingletonWithSystemEntity(ref SystemState state, SystemAPIAccess access)
        {
            state.EntityManager.AddComponentData(state.SystemHandle, new EcsTestData(5));
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    Assert.AreEqual(SystemAPI.GetSingleton<EcsTestData>().value, 5);
                    break;
                case SystemAPIAccess.Using:
                    Assert.AreEqual(GetSingleton<EcsTestData>().value, 5);
                    break;
            }
        }

        [BurstCompile]
        public void TestTryGetSingleton(ref SystemState state, SystemAPIAccess access, TypeArgumentExplicit typeArgumentExplicit)
        {
            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(e, new EcsTestData(5));

            switch (access)
            {
                case SystemAPIAccess.SystemAPI when typeArgumentExplicit == TypeArgumentExplicit.TypeArgumentShown:
                    Assert.True(SystemAPI.TryGetSingleton<EcsTestData>(out var valSystemAPITypeArgumentShown));
                    Assert.AreEqual(valSystemAPITypeArgumentShown.value, 5);
                    break;
                case SystemAPIAccess.SystemAPI:
                    Assert.True(SystemAPI.TryGetSingleton(out EcsTestData valSystemAPITypeArgumentHidden));
                    Assert.AreEqual(valSystemAPITypeArgumentHidden.value, 5);
                    break;
                case SystemAPIAccess.Using when typeArgumentExplicit == TypeArgumentExplicit.TypeArgumentShown:
                    Assert.True(TryGetSingleton<EcsTestData>(out var valUsingTypeArgumentShown));
                    Assert.AreEqual(valUsingTypeArgumentShown.value, 5);
                    break;
                case SystemAPIAccess.Using:
                    Assert.True(TryGetSingleton(out EcsTestData valUsingTypeArgumentHidden));
                    Assert.AreEqual(valUsingTypeArgumentHidden.value, 5);
                    break;
            }
        }

        [BurstCompile]
        public void TestGetSingletonRW(ref SystemState state, SystemAPIAccess access)
        {
            state.EntityManager.CreateEntity(typeof(EcsTestData));
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    SystemAPI.GetSingletonRW<EcsTestData>().ValueRW.value = 5;
                    break;
                case SystemAPIAccess.Using:
                    GetSingletonRW<EcsTestData>().ValueRW.value = 5;
                    break;
            }

            Assert.AreEqual(5, GetSingleton<EcsTestData>().value);
        }

        [BurstCompile]
        public void TestTryGetSingletonRW(ref SystemState state, SystemAPIAccess access)
        {
            state.EntityManager.CreateEntity(typeof(EcsTestData));
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                {
                    if (SystemAPI.TryGetSingletonRW<EcsTestData>(out var @ref))
                        @ref.ValueRW.value = 5;
                } break;
                case SystemAPIAccess.Using:
                {
                    if (TryGetSingletonRW<EcsTestData>(out var @ref))
                        @ref.ValueRW.value = 5;
                } break;
            }

            Assert.AreEqual(5, GetSingleton<EcsTestData>().value);
        }

        [BurstCompile]
        public void TestSetSingleton(ref SystemState state, SystemAPIAccess access, TypeArgumentExplicit typeArgumentExplicit)
        {
            state.EntityManager.CreateEntity(typeof(EcsTestData));
            var data = new EcsTestData(5);
            switch (access)
            {
                case SystemAPIAccess.SystemAPI when typeArgumentExplicit == TypeArgumentExplicit.TypeArgumentShown:
                    SystemAPI.SetSingleton<EcsTestData>(data);
                    break;
                case SystemAPIAccess.SystemAPI:
                    SystemAPI.SetSingleton(data);
                    break;
                case SystemAPIAccess.Using when typeArgumentExplicit == TypeArgumentExplicit.TypeArgumentShown:
                    SetSingleton<EcsTestData>(data);
                    break;
                case SystemAPIAccess.Using:
                    SetSingleton(data);
                    break;
            }

            Assert.AreEqual(5, GetSingleton<EcsTestData>().value);
        }

        [BurstCompile]
        public void TestGetSingletonEntity(ref SystemState state, SystemAPIAccess access)
        {
            var e1 = state.EntityManager.CreateEntity(typeof(EcsTestData));
            var e2 = Entity.Null;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    e2 = SystemAPI.GetSingletonEntity<EcsTestData>();
                    break;
                case SystemAPIAccess.Using:
                    e2 = GetSingletonEntity<EcsTestData>();
                    break;
            }
            Assert.AreEqual(e1, e2);
        }

        [BurstCompile]
        public void TestTryGetSingletonEntity(ref SystemState state, SystemAPIAccess access)
        {
            var e1 = state.EntityManager.CreateEntity(typeof(EcsTestData));
            var e2 = Entity.Null;
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    Assert.True(SystemAPI.TryGetSingletonEntity<EcsTestData>(out e2));
                    break;
                case SystemAPIAccess.Using:
                    Assert.True(TryGetSingletonEntity<EcsTestData>(out e2));
                    break;
            }
            Assert.AreEqual(e1, e2);
        }

        [BurstCompile]
        public void TestGetSingletonBuffer(ref SystemState state, SystemAPIAccess access)
        {
            var e = state.EntityManager.CreateEntity();
            var buffer1 = state.EntityManager.AddBuffer<EcsIntElement>(e);
            buffer1.Add(5);

            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    Assert.AreEqual(buffer1[0],SystemAPI.GetSingletonBuffer<EcsIntElement>()[0]);
                    break;
                case SystemAPIAccess.Using:
                    Assert.AreEqual(buffer1[0],GetSingletonBuffer<EcsIntElement>()[0]);
                    break;
            }
        }

        [BurstCompile]
        public void TestGetSingletonBufferWithSystemEntity(ref SystemState state, SystemAPIAccess access)
        {
            state.EntityManager.AddComponent<EcsIntElement>(state.SystemHandle);
            var buffer1 = state.EntityManager.GetBuffer<EcsIntElement>(state.SystemHandle);
            buffer1.Add(5);

            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    Assert.AreEqual(buffer1[0], SystemAPI.GetSingletonBuffer<EcsIntElement>()[0]);
                    break;
                case SystemAPIAccess.Using:
                    Assert.AreEqual(buffer1[0], GetSingletonBuffer<EcsIntElement>()[0]);
                    break;
            }
        }

        [BurstCompile]
        public void TestTryGetSingletonBuffer(ref SystemState state, SystemAPIAccess access, TypeArgumentExplicit typeArgumentExplicit)
        {
            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddBuffer<EcsIntElement>(e);
            if (SystemAPI.TryGetSingletonBuffer<EcsIntElement>(out var buffer, false))
                buffer.Add(5);

            switch (access)
            {
                case SystemAPIAccess.SystemAPI when typeArgumentExplicit == TypeArgumentExplicit.TypeArgumentShown:
                    Assert.True(SystemAPI.TryGetSingletonBuffer<EcsIntElement>(out var buffer2SystemAPITypeArgumentShown, true));
                    Assert.AreEqual(buffer[0], buffer2SystemAPITypeArgumentShown[0]);
                    break;
                case SystemAPIAccess.SystemAPI:
                    Assert.True(SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<EcsIntElement> buffer2SystemAPITypeArgumentHidden));
                    Assert.AreEqual(buffer[0], buffer2SystemAPITypeArgumentHidden[0]);
                    break;
                case SystemAPIAccess.Using when typeArgumentExplicit == TypeArgumentExplicit.TypeArgumentShown:
                    Assert.True(TryGetSingletonBuffer<EcsIntElement>(out var buffer2UsingTypeArgumentShown, true));
                    Assert.AreEqual(buffer[0], buffer2UsingTypeArgumentShown[0]);
                    break;
                case SystemAPIAccess.Using:
                    Assert.True(TryGetSingletonBuffer(out DynamicBuffer<EcsIntElement> buffer2UsingTypeArgumentHidden, true));
                    Assert.AreEqual(buffer[0], buffer2UsingTypeArgumentHidden[0]);
                    break;
            }
        }

        [BurstCompile]
        public void TestHasSingleton(ref SystemState state, SystemAPIAccess access, SingletonVersion singletonVersion)
        {
            state.EntityManager.CreateEntity(typeof(EcsTestData), typeof(EcsIntElement));

            switch (access)
            {
                case SystemAPIAccess.SystemAPI when singletonVersion == SingletonVersion.ComponentData:
                    Assert.True(SystemAPI.HasSingleton<EcsTestData>());
                    break;
                case SystemAPIAccess.SystemAPI when singletonVersion == SingletonVersion.Buffer:
                    Assert.True(SystemAPI.HasSingleton<EcsIntElement>());
                    break;
                case SystemAPIAccess.Using when singletonVersion == SingletonVersion.ComponentData:
                    Assert.True(HasSingleton<EcsTestData>());
                    break;
                case SystemAPIAccess.Using when singletonVersion == SingletonVersion.Buffer:
                    Assert.True(HasSingleton<EcsIntElement>());
                    break;
            }
        }
        #endregion

        #region Aspect

        [BurstCompile]
        public void TestGetAspectRW(ref SystemState state, SystemAPIAccess access)
        {
            var entity = state.EntityManager.CreateEntity(typeof(EcsTestData));
            switch (access)
            {
                case SystemAPIAccess.SystemAPI:
                    SystemAPI.GetAspect<EcsTestAspect0RW>(entity).EcsTestData.ValueRW.value = 5;
                    break;
                case SystemAPIAccess.Using:
                    GetAspect<EcsTestAspect0RW>(entity).EcsTestData.ValueRW.value = 5;
                    break;
            }

            Assert.AreEqual(5, GetComponent<EcsTestData>(entity).value);
        }

        #endregion

        #region Handles

        [BurstCompile]
        public void TestGetEntityTypeHandle(ref SystemState state){
            var e = state.EntityManager.CreateEntity();
            var handle = SystemAPI.GetEntityTypeHandle();
            Assert.That(state.EntityManager.GetChunk(e).GetNativeArray(handle)[0]==e);
        }

        [BurstCompile]
        public void TestGetComponentTypeHandle(ref SystemState state){
            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(e, new EcsTestData(5));
            var handle = SystemAPI.GetComponentTypeHandle<EcsTestData>();
            Assert.That(state.EntityManager.GetChunk(e).GetNativeArray(ref handle)[0].GetValue()==5);
        }

        [BurstCompile]
        public void TestGetBufferTypeHandle(ref SystemState state){
            var e = state.EntityManager.CreateEntity();
            var buffer =state.EntityManager.AddBuffer<EcsIntElement>(e);
            buffer.Add(new EcsIntElement{Value=5});
            var handle = SystemAPI.GetBufferTypeHandle<EcsIntElement>();
            Assert.That(state.EntityManager.GetChunk(e).GetBufferAccessor(ref handle)[0][0].Value==5);
        }

        [BurstCompile]
        public void TestGetSharedComponentTypeHandle(ref SystemState state){
            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddSharedComponent(e, new SharedData1(5));
            var handle = SystemAPI.GetSharedComponentTypeHandle<SharedData1>();
            Assert.That(state.EntityManager.GetChunk(e).GetSharedComponent(handle).value==5);
        }

        #endregion

        #region NoError

        [BurstCompile]
        void NestingSetup(ref SystemState state)
        {
            // Setup Archetypes
            var playerArchetype = state.EntityManager.CreateArchetype(new FixedList128Bytes<ComponentType> {
                ComponentType.ReadWrite<EcsTestFloatData>(), ComponentType.ReadWrite<LocalTransform>(), ComponentType.ReadWrite<EcsTestTag>()
            }.ToNativeArray(state.World.UpdateAllocator.ToAllocator));
            var coinArchetype = state.EntityManager.CreateArchetype(new FixedList128Bytes<ComponentType> {
                ComponentType.ReadWrite<EcsTestFloatData>(), ComponentType.ReadWrite<LocalTransform>(),
            }.ToNativeArray(state.World.UpdateAllocator.ToAllocator));
            var coinCounterArchetype = state.EntityManager.CreateArchetype(new FixedList128Bytes<ComponentType> {
                ComponentType.ReadWrite<EcsTestData>()
            }.ToNativeArray(state.World.UpdateAllocator.ToAllocator));

            // Setup Players
            var players = state.EntityManager.CreateEntity(playerArchetype, 5, state.World.UpdateAllocator.ToAllocator);
            foreach (var player in players)
                SetComponent(player, new EcsTestFloatData {Value = 0.1f});
            SetComponent(players[0], LocalTransform.FromPosition(0,1,0));
            SetComponent(players[1], LocalTransform.FromPosition(1,1,0));
            SetComponent(players[2], LocalTransform.FromPosition(0,1,1));
            SetComponent(players[3], LocalTransform.FromPosition(1,1,1));
            SetComponent(players[4], LocalTransform.FromPosition(1,0,1));

            // Setup Enemies
            var coins = state.EntityManager.CreateEntity(coinArchetype, 5, state.World.UpdateAllocator.ToAllocator);
            foreach (var coin in coins)
                SetComponent(coin, new EcsTestFloatData {Value = 1f});
            SetComponent(coins[0], LocalTransform.FromPosition(0,1,0));
            SetComponent(coins[1], LocalTransform.FromPosition(1,1,0));
            SetComponent(coins[2], LocalTransform.FromPosition(0,1,1));
            SetComponent(coins[3], LocalTransform.FromPosition(1,1,1));
            SetComponent(coins[4], LocalTransform.FromPosition(1,0,1));

            // Setup Coin Counter
            state.EntityManager.CreateEntity(coinCounterArchetype);
        }

        [BurstCompile]
        public void TestNesting(ref SystemState state)
        {
            NestingSetup(ref state);

            foreach (var (playerTransform, playerRadius) in Query<RefRO<LocalTransform>, RefRO<EcsTestFloatData>>().WithAll<EcsTestTag>())
            foreach (var (coinTransform, coinRadius, coinEntity) in Query<RefRO<LocalTransform>, RefRO<EcsTestFloatData>>().WithEntityAccess().WithNone<EcsTestTag>())
                if (math.distancesq(playerTransform.ValueRO.Position, coinTransform.ValueRO.Position) < coinRadius.ValueRO.Value + playerRadius.ValueRO.Value)
                    GetSingletonRW<EcsTestData>().ValueRW.value++; // Three-layer SystemAPI nesting

            var coinsCollected = GetSingleton<EcsTestData>().value;
            Assert.AreEqual(15, coinsCollected);
        }

        /// <summary>
        /// This will throw in cases where SystemAPI doesn't properly insert .Update and .CompleteDependencyXX statements.
        /// </summary>
        public void TestStatementInsert(ref SystemState state)
        {
            // Asserts that does not throw - Not using Assert.DoesNotThrow since a lambda capture to ref state will fail.
            foreach (var (transform, target) in Query<RefRO<LocalTransform>, RefRO<EcsTestDataEntity>>())
            {
                if (Exists(target.ValueRO.value1))
                {
                    var targetTransform = GetComponent<LocalTransform>(target.ValueRO.value1);
                    var src = transform.ValueRO.Position;
                    var dst = targetTransform.Position;
                    Assert.That(src, Is.Not.EqualTo(dst));
                }
            }
        }

        public void TestGenericTypeArgument(ref SystemState state)
        {
            Assert.False(HasSingleton<EcsTestGenericTag<int>>());
        }

        [BurstCompile]
        public partial struct ExplicitInterfaceImplementationSystem : ISystem
        {
            public void OnCreate(ref SystemState state) => state.RequireForUpdate<EcsTestData>();

            [BurstCompile]
            void ISystem.OnUpdate(ref SystemState state)
            {
                state.EntityManager.AddComponentData(state.SystemHandle, new EcsTestData(5));
                Assert.AreEqual(GetSingleton<EcsTestData>().value, 5);
            }
        }

        [BurstCompile]
        public partial struct VariableInOnCreateSystem : ISystem
        {
            [BurstCompile]
            public void OnCreate(ref SystemState state) {
                var readOnly = true;
                var lookupA = GetComponentLookup<EcsTestData>(readOnly);
            }
        }

        [BurstCompile]
        public partial struct PropertyWithOnlyGetterSystem : ISystem
        {
            int DataValue => GetSingleton<EcsTestData>().value;

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                state.EntityManager.AddComponentData(state.SystemHandle, new EcsTestData(5));
                Assert.AreEqual(DataValue, 5);
            }
        }

        [BurstCompile]
        public partial struct TestGenericISystem_MyInterface<T> : ISystem where T : unmanaged, IMyInterface
        {
            public void OnUpdate(ref SystemState state)
            {
                var e = state.EntityManager.CreateEntity();
                var buffer = state.EntityManager.AddBuffer<T>(e);
                buffer.Add(new T { Value = 1 });
                var handle = SystemAPI.GetBufferTypeHandle<T>();
                Assert.That(state.EntityManager.GetChunk(e).GetBufferAccessor(ref handle)[0][0].Value == 1);
            }
        }

        // We specify T as EcsIntElement in our test
        public partial struct TestGenericISystem_BufferElement<T> : ISystem where T : unmanaged, IBufferElementData
        {
            public void OnUpdate(ref SystemState state)
            {
                var e = state.EntityManager.CreateEntity();

                var buffer = state.EntityManager.AddBuffer<EcsIntElement>(e);
                buffer.Add(new EcsIntElement { Value = 1 });

                var handle = GetBufferTypeHandle<T>();
                var bufferElementData = state.EntityManager.GetChunk(e).GetBufferAccessor(ref handle)[0][0];

                Assert.That(bufferElementData is EcsIntElement { Value: 1 });
            }
        }
        #endregion
    }
}
