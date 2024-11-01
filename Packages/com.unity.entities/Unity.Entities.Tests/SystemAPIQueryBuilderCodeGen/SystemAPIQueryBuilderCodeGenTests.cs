using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Tests.SystemAPIQueryBuilderCodeGen
{
    [TestFixture]
    partial class SystemAPIQueryBuilderCodeGenTests : ECSTestsFixture
    {
        MyTestSystem _testSystem;

        [SetUp]
        public void SetUp()
        {
            _testSystem = World.GetOrCreateSystemManaged<MyTestSystem>();
        }

        [Test] public void WithAllTheThings_Test() => _testSystem.WithAllTheThings();
        [Test] public void CreateTwoArchetypeQueries_WithRO_AndRW_Test() => _testSystem.CreateArchetypeQueries_WithRO_AndRW();
        [Test] public void CreateMultipleArchetypeQueries_Test() => _testSystem.CreateMultipleArchetypeQueries();
        [Test] public void ChainedWithEntityQueryMethodAfterBuilding_Test() => _testSystem.ChainedWithEntityQueryMethodAfterBuilding();

        [Test] public void WithAspect() => _testSystem.WithAspect();
        [Test] public void WithAspect2() => _testSystem.WithAspect2();
        [Test] public void WithAspectAliased() => _testSystem.WithAspectAliased();

        partial class MyTestSystem : SystemBase
        {
            private unsafe T[] ToManagedArray<T>(T* values, int length) where T : unmanaged
            {
                var array = new T[length];
                for (int i = 0; i < length; ++i)
                    array[i] = values[i];
                return array;
            }

            unsafe NativeArray<T> ToSortedNativeArray<T>(T[] values) where T : unmanaged, IComparable<T>
            {
                var array = new NativeArray<T>(values, Allocator.Temp);
                NativeSortExtension.Sort((T*)array.GetUnsafePtr(), array.Length);
                return array;
            }

            public unsafe void WithAllTheThings()
            {
                var query = SystemAPI.QueryBuilder()
                    .WithAll<EcsTestData>()
                    .WithAny<EcsTestData2>()
                    .WithNone<EcsTestData3>()
                    .WithDisabled<EcsTestDataEnableable>()
                    .WithAbsent<EcsTestData5>()
                    .WithPresent<EcsTestDataEnableable2>()
                    .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                    .Build();
                var queryData = query._GetImpl()->_QueryData;

                Assert.AreEqual(1, queryData->ArchetypeQueryCount);

                var archetypeQuery = queryData->ArchetypeQueries[0];

                Assert.AreEqual(1, archetypeQuery.AllCount);
                Assert.AreEqual(1, archetypeQuery.NoneCount);
                Assert.AreEqual(1, archetypeQuery.AnyCount);
                Assert.AreEqual(1, archetypeQuery.DisabledCount);
                Assert.AreEqual(1, archetypeQuery.AbsentCount);
                Assert.AreEqual(1, archetypeQuery.PresentCount);

                Assert.AreEqual(ComponentType.ReadOnly<EcsTestData>().TypeIndex, archetypeQuery.All[0]);
                Assert.AreEqual(ComponentType.ReadOnly<EcsTestData2>().TypeIndex, archetypeQuery.Any[0]);
                Assert.AreEqual(ComponentType.ReadOnly<EcsTestData3>().TypeIndex, archetypeQuery.None[0]);
                Assert.AreEqual(ComponentType.ReadOnly<EcsTestDataEnableable>().TypeIndex, archetypeQuery.Disabled[0]);
                Assert.AreEqual(ComponentType.ReadOnly<EcsTestData5>().TypeIndex, archetypeQuery.Absent[0]);
                Assert.AreEqual(ComponentType.ReadOnly<EcsTestDataEnableable2>().TypeIndex, archetypeQuery.Present[0]);

                Assert.AreEqual(EntityQueryOptions.IncludeDisabledEntities, archetypeQuery.Options);
            }

            public unsafe void CreateArchetypeQueries_WithRO_AndRW()
            {
                var query =
                    SystemAPI.QueryBuilder()
                        .WithAll<EcsTestData>().WithNone<EcsTestData2>()
                        .AddAdditionalQuery()
                        .WithAllRW<EcsTestData2>().WithAnyRW<EcsTestData3, EcsTestData4>()
                        .Build();

                var queryData = query._GetImpl()->_QueryData;

                Assert.AreEqual(2, queryData->ArchetypeQueryCount);

                var archetypeQuery1 = queryData->ArchetypeQueries[0];
                Assert.AreEqual(1, archetypeQuery1.AllCount);
                Assert.AreEqual(1, archetypeQuery1.NoneCount);
                Assert.AreEqual(0, archetypeQuery1.AnyCount);
                Assert.AreEqual(0, archetypeQuery1.DisabledCount);
                Assert.AreEqual(0, archetypeQuery1.AbsentCount);
                Assert.AreEqual(0, archetypeQuery1.PresentCount);

                Assert.AreEqual(ComponentType.ReadOnly<EcsTestData>().TypeIndex, archetypeQuery1.All[0]);
                Assert.AreEqual(ComponentType.ReadOnly<EcsTestData2>().TypeIndex, archetypeQuery1.None[0]);

                var archetypeQuery2 = queryData->ArchetypeQueries[1];

                Assert.AreEqual(1, archetypeQuery2.AllCount);
                Assert.AreEqual(0, archetypeQuery2.NoneCount);
                Assert.AreEqual(2, archetypeQuery2.AnyCount);
                Assert.AreEqual(0, archetypeQuery2.DisabledCount);
                Assert.AreEqual(0, archetypeQuery2.AbsentCount);
                Assert.AreEqual(0, archetypeQuery2.PresentCount);

                Assert.AreEqual(ComponentType.ReadWrite<EcsTestData2>().TypeIndex, archetypeQuery2.All[0]);
                Assert.That(ToManagedArray(archetypeQuery2.Any, archetypeQuery2.AnyCount), Is.EquivalentTo(new TypeIndex[] {
                    ComponentType.ReadOnly<EcsTestData3>().TypeIndex,
                    ComponentType.ReadOnly<EcsTestData4>().TypeIndex,
                }));
            }

            public unsafe void CreateMultipleArchetypeQueries()
            {
                var query =
                    SystemAPI.QueryBuilder()
                        .WithAll<EcsTestData>()
                        .AddAdditionalQuery()
                        .WithNone<EcsTestData2>()
                        .AddAdditionalQuery()
                        .WithAny<EcsTestData3>()
                        .AddAdditionalQuery()
                        .WithOptions(EntityQueryOptions.IncludePrefab)
                        .Build();

                Assert.AreEqual(4, query._GetImpl()->_QueryData->ArchetypeQueryCount);
            }

            public void ChainedWithEntityQueryMethodAfterBuilding()
            {
                var entity = EntityManager.CreateEntity(ComponentType.ReadOnly<EcsTestData>(), ComponentType.ReadOnly<EcsTestData2>());

                int numEntitiesMatchingQuery =
                    SystemAPI.QueryBuilder()
                        .WithNone<EcsTestData3>()
                        .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                        .WithAny<EcsTestData2>()
                        .WithAll<EcsTestData>()
                        .Build()
                        .CalculateEntityCount();

                Assert.AreEqual(1,numEntitiesMatchingQuery);

                EntityManager.DestroyEntity(entity);
            }

            public unsafe void WithAspect()
            {
                EntityQuery query;
                query = SystemAPI.QueryBuilder().WithAspect<MyAspect>().Build();
                var queryData = query._GetImpl()->_QueryData;

                Assert.AreEqual(1, queryData->ArchetypeQueryCount);

                var archetypeQuery = queryData->ArchetypeQueries[0];

                Assert.AreEqual(1, archetypeQuery.AllCount);
                Assert.AreEqual(0, archetypeQuery.NoneCount);
                Assert.AreEqual(0, archetypeQuery.AnyCount);
                Assert.AreEqual(0, archetypeQuery.DisabledCount);
                Assert.AreEqual(0, archetypeQuery.AbsentCount);
                Assert.AreEqual(0, archetypeQuery.PresentCount);

                Assert.AreEqual(ComponentType.ReadWrite<EcsTestData>(), archetypeQuery.GetComponentTypeAllAt(0));
            }

            public unsafe void WithAspect2()
            {
                EntityQuery query;
                query = SystemAPI.QueryBuilder().WithAspect<MyAspect>().WithAspect<MyAspectMiscTests>().Build();
                var queryData = query._GetImpl()->_QueryData;

                Assert.AreEqual(1, queryData->ArchetypeQueryCount);
                var archetypeQuery = queryData->ArchetypeQueries[0];

                using NativeArray<ComponentType> receivedAll = archetypeQuery.SortedComponentTypeAll();
                using NativeArray<ComponentType> expectedAll = ToSortedNativeArray(
                    new ComponentType[]
                    {
                        ComponentType.ReadWrite<EcsTestData>(),
                        ComponentType.ReadWrite<EcsTestData2>(),
                        ComponentType.ReadWrite<EcsTestData3>(),
                        ComponentType.ReadOnly<EcsTestData4>()
                    });

                Assert.AreEqual(expectedAll.Length, archetypeQuery.AllCount);
                for (int i = 0; i != expectedAll.Length; i++)
                    Assert.AreEqual(expectedAll[i], receivedAll[i]);

                Assert.AreEqual(0, archetypeQuery.NoneCount);
                Assert.AreEqual(0, archetypeQuery.AnyCount);
                Assert.AreEqual(0, archetypeQuery.DisabledCount);
                Assert.AreEqual(0, archetypeQuery.AbsentCount);
                Assert.AreEqual(0, archetypeQuery.PresentCount);
            }

            public unsafe void WithAspectAliased()
            {
                EntityQuery query;
                query = SystemAPI.QueryBuilder().WithAll<EcsTestData>().WithAspect<MyAspect>().Build();
                var queryData = query._GetImpl()->_QueryData;

                Assert.AreEqual(1, queryData->ArchetypeQueryCount);

                var archetypeQuery = queryData->ArchetypeQueries[0];

                Assert.AreEqual(1, archetypeQuery.AllCount);
                Assert.AreEqual(0, archetypeQuery.NoneCount);
                Assert.AreEqual(0, archetypeQuery.AnyCount);
                Assert.AreEqual(0, archetypeQuery.DisabledCount);
                Assert.AreEqual(0, archetypeQuery.AbsentCount);
                Assert.AreEqual(0, archetypeQuery.PresentCount);

                Assert.AreEqual(ComponentType.ReadWrite<EcsTestData>(), archetypeQuery.GetComponentTypeAllAt(0));
            }

            protected override void OnUpdate()
            {
            }
        }

    }

    static class ArchetypeQueryExt
    {
        public static unsafe NativeArray<ComponentType> SortedComponentTypeAll(this ArchetypeQuery archetypeQuery)
        {
            var array = new NativeArray<ComponentType>(archetypeQuery.AllCount, Allocator.Temp);
            for (int i = 0; i != array.Length; i++)
                array[i] = archetypeQuery.GetComponentTypeAllAt(i);
            NativeSortExtension.Sort((ComponentType*)array.GetUnsafePtr(), array.Length);
            return array;
        }
        public static unsafe ComponentType GetComponentTypeAllAt(this ArchetypeQuery archetypeQuery, int index)
        {
            var componentType = ComponentType.FromTypeIndex(archetypeQuery.All[index]);
            componentType.AccessModeType = (ComponentType.AccessMode)archetypeQuery.AllAccessMode[index];
            return componentType;
        }
    }
}
