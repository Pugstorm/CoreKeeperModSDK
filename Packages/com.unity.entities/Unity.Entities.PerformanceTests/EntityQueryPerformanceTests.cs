using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using Unity.Entities.Tests;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Unity.Entities.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    public partial class EntityQueryPerformanceTests : EntityPerformanceTestFixture
    {
        struct TestTag0 : IComponentData{}
        struct TestTag1 : IComponentData{}
        struct TestTag2 : IComponentData{}
        struct TestTag3 : IComponentData{}
        struct TestTag4 : IComponentData{}
        struct TestTag5 : IComponentData{}
        struct TestTag6 : IComponentData{}
        struct TestTag7 : IComponentData{}
        struct TestTag8 : IComponentData{}
        struct TestTag9 : IComponentData{}
        struct TestTag10 : IComponentData{}
        struct TestTag11 : IComponentData{}
        struct TestTag12 : IComponentData{}
        struct TestTag13 : IComponentData{}
        struct TestTag14 : IComponentData{}
        struct TestTag15 : IComponentData{}
        struct TestTag16 : IComponentData{}
        struct TestTag17 : IComponentData{}
        struct TestTag18 : IComponentData{}
        struct TestTag19 : IComponentData{}
        struct TestTag20 : IComponentData{}
        struct TestTag21 : IComponentData{}
        struct TestTag22 : IComponentData{}
        struct TestTag23 : IComponentData{}
        struct TestTag24 : IComponentData{}
        struct TestTag25 : IComponentData{}
        struct TestTag26 : IComponentData{}
        struct TestTag27 : IComponentData{}
        struct TestTag28 : IComponentData{}
        struct TestTag29 : IComponentData{}
        struct TestTag30 : IComponentData{}
        struct TestTag31 : IComponentData{}
        struct TestTag32 : IComponentData{}
        struct TestTag33 : IComponentData{}
        struct TestTag34 : IComponentData{}
        struct TestTag35 : IComponentData{}
        struct TestTag36 : IComponentData{}
        struct TestTag37 : IComponentData{}
        struct TestTag38 : IComponentData{}
        struct TestTag39 : IComponentData{}
        struct TestTag40 : IComponentData{}
        struct TestTag41 : IComponentData{}
        struct TestTag42 : IComponentData{}
        struct TestTag43 : IComponentData{}
        struct TestTag44 : IComponentData{}
        struct TestTag45 : IComponentData{}
        struct TestTag46 : IComponentData{}
        struct TestTag47 : IComponentData{}
        struct TestTag48 : IComponentData{}
        struct TestTag49 : IComponentData{}
        struct TestTag50 : IComponentData{}
        struct TestTag51 : IComponentData{}
        struct TestTag52 : IComponentData{}
        struct TestTag53 : IComponentData{}
        struct TestTag54 : IComponentData{}
        struct TestTag55 : IComponentData{}
        struct TestTag56 : IComponentData{}
        struct TestTag57 : IComponentData{}
        struct TestTag58 : IComponentData{}
        struct TestTag59 : IComponentData{}
        struct TestTag60 : IComponentData{}
        struct TestTag61 : IComponentData{}
        struct TestTag62 : IComponentData{}
        struct TestTag63 : IComponentData{}
        struct TestTag64 : IComponentData{}
        struct TestTag65 : IComponentData{}
        struct TestTag66 : IComponentData{}
        struct TestTag67 : IComponentData{}
        struct TestTag68 : IComponentData{}
        struct TestTag69 : IComponentData{}
        struct TestTag70 : IComponentData{}
        struct TestTag71 : IComponentData{}
        struct TestTag72 : IComponentData{}
        struct TestTag73 : IComponentData{}
        struct TestTag74 : IComponentData{}
        struct TestTag75 : IComponentData{}
        struct TestTag76 : IComponentData{}
        struct TestTag77 : IComponentData{}
        struct TestTag78 : IComponentData{}
        struct TestTag79 : IComponentData{}
        struct TestTag80 : IComponentData{}
        struct TestTag81 : IComponentData{}
        struct TestTag82 : IComponentData{}
        struct TestTag83 : IComponentData{}
        struct TestTag84 : IComponentData{}
        struct TestTag85 : IComponentData{}
        struct TestTag86 : IComponentData{}
        struct TestTag87 : IComponentData{}
        struct TestTag88 : IComponentData{}
        struct TestTag89 : IComponentData{}
        struct TestTag90 : IComponentData{}
        struct TestTag91 : IComponentData{}
        struct TestTag92 : IComponentData{}
        struct TestTag93 : IComponentData{}
        struct TestTag94 : IComponentData{}
        struct TestTag95 : IComponentData{}
        struct TestTag96 : IComponentData{}
        struct TestTag97 : IComponentData{}
        struct TestTag98 : IComponentData{}
        struct TestTag99 : IComponentData{}
        struct TestTag100 : IComponentData{}

        struct LargeComponent0 : IComponentData
        {
            public int data0;
            public int data1;
            public int data2;
            public int data3;
            public int data4;
            public int data5;
            public int data6;
            public int data7;
            public int data8;
            public int data9;
        }

        //duplicate 40 byte data structures of the above for generating archetypes with data, similar to tag components
        struct LargeComponent1 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent2 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent3 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent4 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent5 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent6 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent7 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent8 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent9 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent10 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent11 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent12 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent13 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}
        struct LargeComponent14 : IComponentData{public int data0;public int data1; public int data2; public int data3;
        public int data4; public int data5; public int data6; public int data7; public int data8; public int data9;}

        Type[] LargeComponentTypes =
        {
            typeof(LargeComponent0),
            typeof(LargeComponent1),
            typeof(LargeComponent2),
            typeof(LargeComponent3),
            typeof(LargeComponent4),
            typeof(LargeComponent5),
            typeof(LargeComponent6),
            typeof(LargeComponent7),
            typeof(LargeComponent8),
            typeof(LargeComponent9),
            typeof(LargeComponent10),
            typeof(LargeComponent11),
            typeof(LargeComponent12),
            typeof(LargeComponent13),
            typeof(LargeComponent14)
        };

        Type[] TagTypes =
        {
            typeof(TestTag0),
            typeof(TestTag1),
            typeof(TestTag2),
            typeof(TestTag3),
            typeof(TestTag4),
            typeof(TestTag5),
            typeof(TestTag6),
            typeof(TestTag7),
            typeof(TestTag8),
            typeof(TestTag9),
            typeof(TestTag10),
            typeof(TestTag11),
            typeof(TestTag12),
            typeof(TestTag13),
            typeof(TestTag14),
            typeof(TestTag15),
            typeof(TestTag16),
            typeof(TestTag17),
            typeof(TestTag18),
            typeof(TestTag19),
            typeof(TestTag20),
            typeof(TestTag21),
            typeof(TestTag22),
            typeof(TestTag23),
            typeof(TestTag24),
            typeof(TestTag25),
            typeof(TestTag26),
            typeof(TestTag27),
            typeof(TestTag28),
            typeof(TestTag29),
            typeof(TestTag30),
            typeof(TestTag31),
            typeof(TestTag32),
            typeof(TestTag33),
            typeof(TestTag34),
            typeof(TestTag35),
            typeof(TestTag36),
            typeof(TestTag37),
            typeof(TestTag38),
            typeof(TestTag39),
            typeof(TestTag40),
            typeof(TestTag41),
            typeof(TestTag42),
            typeof(TestTag43),
            typeof(TestTag44),
            typeof(TestTag45),
            typeof(TestTag46),
            typeof(TestTag47),
            typeof(TestTag48),
            typeof(TestTag49),
            typeof(TestTag50),
            typeof(TestTag51),
            typeof(TestTag52),
            typeof(TestTag53),
            typeof(TestTag54),
            typeof(TestTag55),
            typeof(TestTag56),
            typeof(TestTag57),
            typeof(TestTag58),
            typeof(TestTag59),
            typeof(TestTag60),
            typeof(TestTag61),
            typeof(TestTag62),
            typeof(TestTag63),
            typeof(TestTag64),
            typeof(TestTag65),
            typeof(TestTag66),
            typeof(TestTag67),
            typeof(TestTag68),
            typeof(TestTag69),
            typeof(TestTag70),
            typeof(TestTag71),
            typeof(TestTag72),
            typeof(TestTag73),
            typeof(TestTag74),
            typeof(TestTag75),
            typeof(TestTag76),
            typeof(TestTag77),
            typeof(TestTag78),
            typeof(TestTag79),
            typeof(TestTag80),
            typeof(TestTag81),
            typeof(TestTag82),
            typeof(TestTag83),
            typeof(TestTag84),
            typeof(TestTag85),
            typeof(TestTag86),
            typeof(TestTag87),
            typeof(TestTag88),
            typeof(TestTag89),
            typeof(TestTag90),
            typeof(TestTag91),
            typeof(TestTag92),
            typeof(TestTag93),
            typeof(TestTag94),
            typeof(TestTag95),
            typeof(TestTag96),
            typeof(TestTag97),
            typeof(TestTag98),
            typeof(TestTag99),
            typeof(TestTag100),
        };

        //not an actually useful function. Used to do work on largeComponent to prevent "Never assigned to" error
        private void dummyWorkLargeComponentData()
        {
            var entity  = m_Manager.CreateEntity(typeof(LargeComponent0));
            var component = m_Manager.GetComponentData<LargeComponent0>(entity);

            //work done purely so I do not throw a "never assigned to" error on LargeComponent
            component.data0 = entity.Index;
            component.data1 = entity.Index;
            component.data2 = entity.Index;
            component.data3 = entity.Index;
            component.data4 = entity.Index;
            component.data5 = entity.Index;
            component.data5 = entity.Index;
            component.data6 = entity.Index;
            component.data7 = entity.Index;
            component.data8 = entity.Index;
            component.data9 = entity.Index;

            m_Manager.SetComponentData(entity,component);
        }

        NativeArray<EntityArchetype> CreateUniqueLargeArchetypes(int size, EnabledBitsMode enabledBitsMode, params ComponentType[] extraTypes)
        {
            var archetypes = CollectionHelper.CreateNativeArray<EntityArchetype>(size, World.UpdateAllocator.ToAllocator);


            if (size > math.pow(2, LargeComponentTypes.Length))
                throw new ArgumentException("Exceeded supported number of archetypes. Please add more LargeComponent data to LargeComponentsTypes");

            var typeCount = CollectionHelper.Log2Ceil(size);
            for (int i = 0; i < size; i++)
            {
                var typeList = new List<ComponentType>();
                for (int typeIndex = 0; typeIndex < typeCount; typeIndex++)
                {
                    if ((i & (1 << typeIndex)) != 0)
                        typeList.Add(LargeComponentTypes[typeIndex]);
                }

                typeList.Add(typeof(EcsTestData));
                typeList.Add(typeof(EcsTestSharedComp));
                if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                    typeList.Add(typeof(EcsTestDataEnableable));

                foreach(var extra in extraTypes)
                    typeList.Add(extra);

                var types = typeList.ToArray();
                archetypes[i] = m_Manager.CreateArchetype(types);
            }

            return archetypes;
        }

        NativeArray<EntityArchetype> CreateUniqueTagArchetypes(int size, EnabledBitsMode enabledBitsMode,
            params ComponentType[] extraTypes)
        {
            var usualTypes = new List<ComponentType>();
            usualTypes.Add(typeof(EcsTestData));
            usualTypes.Add(typeof(EcsTestSharedComp));
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
            {
                usualTypes.Add(typeof(EcsTestDataEnableable));
            }

            return CreateUniqueTagArchetypes(size, extraTypes.Concat(usualTypes).ToArray());
        }

        NativeArray<EntityArchetype> CreateUniqueTagArchetypes(int size, params ComponentType[] extraTypes)
        {
            var archetypes = CollectionHelper.CreateNativeArray<EntityArchetype>(size, World.UpdateAllocator.ToAllocator);

            var typeCount = CollectionHelper.Log2Ceil(size);
            for (int i = 0; i < size; i++)
            {
                var typeList = new List<ComponentType>();
                for (int typeIndex = 0; typeIndex < typeCount; typeIndex++)
                {
                    if ((i & (1 << typeIndex)) != 0)
                        typeList.Add(TagTypes[typeIndex]);
                }

                foreach (var extra in extraTypes)
                    typeList.Add(extra);

                var types = typeList.ToArray();
                archetypes[i] = m_Manager.CreateArchetype(types);
            }

            return archetypes;
        }

        static ulong GetNextNumberWithEqualBinaryWeight(ulong lastNumber)
        {
            ulong a = lastNumber;
            ulong c = a & (0 - a);
            ulong b = a + c;
            ulong d = (a ^ b) >> 2;
            ulong e = (d / c) | b;
            return e;
        }

        // !!!Warning!!!: Don't use these archetypes for anything except other than CreateEntityQuery/GetEntityQuery
        // performance tests! They contain fake tag types that don't exist in the TypeManager and will explode if
        // anything asks the TypeManager about them.
        NativeArray<NativeArray<ComponentType>> CreateUniqueFakeTagArchetypes(int numArchetypes, params ComponentType[] extraTypes)
        {
            var allocator = World.UpdateAllocator.ToAllocator;
            var archetypes = CollectionHelper.CreateNativeArray<NativeArray<ComponentType>>(numArchetypes, allocator);

            var numTypesInTypeManager = TypeManager.GetTypeCount();
            var fakeTypeIndex = numTypesInTypeManager;

            for (int archetypeIndex = 0; archetypeIndex < numArchetypes; archetypeIndex++, fakeTypeIndex++)
            {
                var fakeType = new ComponentType
                {
                    AccessModeType = ComponentType.AccessMode.ReadWrite,
                    TypeIndex = new TypeIndex { Value = fakeTypeIndex | TypeManager.HasNoEntityReferencesFlag | TypeManager.ZeroSizeInChunkTypeFlag }
                };
                var typeList = CollectionHelper.CreateNativeArray<ComponentType>(1 + extraTypes.Length, allocator);
                int componentIndex = 0;
                typeList[componentIndex++] = fakeType;

                foreach(var extra in extraTypes)
                    typeList[componentIndex++] = extra;

                archetypes[archetypeIndex] = typeList;
            }

            return archetypes;
        }

        NativeArray<NativeArray<ComponentType>> CreateUniqueTagCombinations(int numArchetypes, int componentsPerArchetype, params ComponentType[] extraTypes)
        {
            Assert.IsTrue(componentsPerArchetype < 64, "CreateUniqueTagCombinations cannot handle more than 63 components per archetype");

            if (componentsPerArchetype == 1 && numArchetypes > 64)
            {
                // Not enough bits in ulong or entries in TagTypes? Just fake some component types!
                return CreateUniqueFakeTagArchetypes(numArchetypes, extraTypes);
            }

            var allocator = World.UpdateAllocator.ToAllocator;
            var archetypes = CollectionHelper.CreateNativeArray<NativeArray<ComponentType>>(numArchetypes, allocator);

            // componentMask should always have N bits set.
            ulong componentMask = (1ul << componentsPerArchetype) - 1ul;
            for (int archetypeIndex = 0; archetypeIndex < numArchetypes; archetypeIndex++)
            {
                var typeList = CollectionHelper.CreateNativeArray<ComponentType>(componentsPerArchetype + extraTypes.Length, allocator);

                ulong mask = componentMask;
                int possibleTagIndex = 0;
                int componentIndex = 0;
                // Add all the types matching the mask to the typelist
                while (mask != 0)
                {
                    if (possibleTagIndex >= TagTypes.Length)
                    {
                        throw new ArgumentException($"Insufficient TagTypes to CreateUniqueTagCombinations with {numArchetypes} combinations of {componentsPerArchetype} components");
                    }
                    if ((mask & 1ul) != 0)
                    {
                        typeList[componentIndex++] = TagTypes[possibleTagIndex];
                    }

                    possibleTagIndex++;
                    mask >>= 1;
                }

                foreach(var extra in extraTypes)
                    typeList[componentIndex++] = extra;

                archetypes[archetypeIndex] = typeList;

                componentMask = GetNextNumberWithEqualBinaryWeight(componentMask);
            }

            return archetypes;
        }

        public enum EnabledBitsMode
        {
            NoEnableableComponents,
            NoComponentsDisabled,
            FewComponentsDisabled,
            ManyComponentsDisabled,
        }

        [Test, Performance]
        public void EntityQuery_DestroyEntity([Values] EnabledBitsMode enabledBitsMode)
        {
            int entityCount = 10_000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp),
                (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                    ? typeof(EcsTestData)
                    : typeof(EcsTestDataEnableable));
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>();
            if (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestData>();
            else
                queryBuilder.WithAll<EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            Measure.Method(
                    () =>
                    {
                        m_Manager.DestroyEntity(query);
                    })
                .SetUp(() =>
                {
                    var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
                    for (int i = 0; i < entityCount; i++)
                    {
                        if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                        else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                    }
                })
                .CleanUp(() => { World.UpdateAllocator.Rewind(); })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"DestroyEntity_{entityCount}x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_DestroyEntity_WithLinkedEntityGroup([Values] EnabledBitsMode enabledBitsMode)
        {
            int entityCount = 10_000;
            var rootArchetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(LinkedEntityGroup),
                (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                    ? typeof(EcsTestData)
                    : typeof(EcsTestDataEnableable));
            var childArchetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp),
                (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                    ? typeof(EcsTestData)
                    : typeof(EcsTestDataEnableable));
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>();
            if (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestData>();
            else
                queryBuilder.WithAll<EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            Measure.Method(
                    () =>
                    {
                        m_Manager.DestroyEntity(query);
                    })
                .SetUp(() =>
                {
                    var entities1 = m_Manager.CreateEntity(rootArchetype, entityCount, World.UpdateAllocator.ToAllocator);
                    var entities2 = m_Manager.CreateEntity(childArchetype, entityCount, World.UpdateAllocator.ToAllocator);
                    for (int i = 0; i < entityCount; i++)
                    {
                        if ((enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0)) ||
                            (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0)))
                        {
                            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities1[i], false);
                            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities2[i], false);
                        }
                        else
                        {
                            var buffer = m_Manager.GetBuffer<LinkedEntityGroup>(entities1[i], false);
                            buffer.Add(entities2[i]);
                        }
                    }
                })
                .CleanUp(() => { World.UpdateAllocator.Rewind(); })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"DestroyEntity_{entityCount}x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_AddComponent([Values] EnabledBitsMode enabledBitsMode)
        {
            int entityCount = 10_000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp),
                (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                    ? typeof(EcsTestData)
                    : typeof(EcsTestDataEnableable));
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>();
            if (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestData>();
            else
                queryBuilder.WithAll<EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            var query2 = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>().Build(m_Manager);
            using var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Persistent);
            for (int i = 0; i < entityCount; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
            }
            Measure.Method(
                    () =>
                    {
                        m_Manager.AddComponent<EcsTestData2>(query);
                    })
                .CleanUp(() =>
                {
                    m_Manager.RemoveComponent<EcsTestData2>(query2);
                    World.UpdateAllocator.Rewind();
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"AddComponent_{entityCount}x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_AddComponent_Tag([Values] EnabledBitsMode enabledBitsMode)
        {
            int entityCount = 10_000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp),
                (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                    ? typeof(EcsTestData)
                    : typeof(EcsTestDataEnableable));
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>();
            if (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestData>();
            else
                queryBuilder.WithAll<EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            var query2 = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>().Build(m_Manager);
            using var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Persistent);
            for (int i = 0; i < entityCount; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
            }
            Measure.Method(
                    () =>
                    {
                        m_Manager.AddComponent<EcsTestTag>(query);
                    })
                .CleanUp(() =>
                {
                    m_Manager.RemoveComponent<EcsTestTag>(query2);
                    World.UpdateAllocator.Rewind();
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"AddComponent_Tag_{entityCount}x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_RemoveComponent([Values] EnabledBitsMode enabledBitsMode)
        {
            int entityCount = 10_000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestData2),
                (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                    ? typeof(EcsTestData)
                    : typeof(EcsTestDataEnableable));
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>();
            if (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestData>();
            else
                queryBuilder.WithAll<EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            var query2 = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>().Build(m_Manager);
            using var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Persistent);
            for (int i = 0; i < entityCount; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
            }
            Measure.Method(
                    () =>
                    {
                        m_Manager.RemoveComponent<EcsTestData2>(query);
                    })
                .CleanUp(() =>
                {
                    m_Manager.AddComponent<EcsTestData2>(query2);
                    World.UpdateAllocator.Rewind();
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"RemoveComponent_{entityCount}x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_RemoveComponent_Tag([Values] EnabledBitsMode enabledBitsMode)
        {
            int entityCount = 10_000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp), typeof(EcsTestTag),
                (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                    ? typeof(EcsTestData)
                    : typeof(EcsTestDataEnableable));
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>();
            if (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestData>();
            else
                queryBuilder.WithAll<EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            var query2 = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>().Build(m_Manager);
            using var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Persistent);
            for (int i = 0; i < entityCount; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
            }
            Measure.Method(
                    () =>
                    {
                        m_Manager.RemoveComponent<EcsTestTag>(query);
                    })
                .CleanUp(() =>
                {
                    m_Manager.AddComponent<EcsTestTag>(query2);
                    World.UpdateAllocator.Rewind();
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"RemoveComponent_Tag_{entityCount}x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_AddSharedComponent([Values] EnabledBitsMode enabledBitsMode)
        {
            int entityCount = 10_000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp),
                (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                    ? typeof(EcsTestData)
                    : typeof(EcsTestDataEnableable));
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>();
            if (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestData>();
            else
                queryBuilder.WithAll<EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            var query2 = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>().Build(m_Manager);
            using var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Persistent);
            for (int i = 0; i < entityCount; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
            }
            var sharedComponentValue = new EcsTestSharedComp2(17);
            Measure.Method(
                    () =>
                    {
                        m_Manager.AddSharedComponent<EcsTestSharedComp2>(query, sharedComponentValue);
                    })
                .CleanUp(() =>
                {
                    m_Manager.RemoveComponent<EcsTestSharedComp2>(query2);
                    World.UpdateAllocator.Rewind();
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"AddSharedComponent_{entityCount}x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_SetSharedComponent([Values] EnabledBitsMode enabledBitsMode)
        {
            int entityCount = 10_000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp),
                (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                    ? typeof(EcsTestData)
                    : typeof(EcsTestDataEnableable));
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>();
            if (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestData>();
            else
                queryBuilder.WithAll<EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            using var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Persistent);
            for (int i = 0; i < entityCount; i++)
            {
                if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (i % 100 == 0))
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (i % 2 == 0))
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
            }
            var sharedComponentValue = new EcsTestSharedComp(17);
            Measure.Method(
                    () =>
                    {
                        m_Manager.SetSharedComponent(query, sharedComponentValue);
                    })
                .CleanUp(() =>
                {
                    m_Manager.SetSharedComponent(query, default(EcsTestSharedComp));
                    World.UpdateAllocator.Rewind();
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"SetSharedComponent_{entityCount}x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_Matches([Values] bool enableChunkFilter,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.ManyComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            int entityCount = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp),
                (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                    ? typeof(EcsTestData)
                    : typeof(EcsTestDataEnableable));

            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>();
            if (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestData>();
            else
                queryBuilder.WithAll<EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            if (enableChunkFilter)
                query.SetSharedComponentFilter(new EcsTestSharedComp());

            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            Measure.Method(
                    () =>
                    {
                        int matchCount = 0;
                        for(int i=0; i<entityCount; ++i)
                            matchCount += query.Matches(entities[i]) ? 1 : 0;
                        Assert.AreEqual(entityCount, matchCount);
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"Matches_{entityCount}x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_MatchesIgnoreFilter([Values] bool enableChunkFilter,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.ManyComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            int entityCount = 1000;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp),
                (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                    ? typeof(EcsTestData)
                    : typeof(EcsTestDataEnableable));

            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp).WithAll<EcsTestSharedComp>();
            if (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                queryBuilder.WithAll<EcsTestData>();
            else
                queryBuilder.WithAll<EcsTestDataEnableable>();
            using var query = m_Manager.CreateEntityQuery(queryBuilder);
            if (enableChunkFilter)
                query.SetSharedComponentFilter(new EcsTestSharedComp());

            using var entities = m_Manager.CreateEntity(archetype, entityCount, World.UpdateAllocator.ToAllocator);
            Measure.Method(
                    () =>
                    {
                        int matchCount = 0;
                        for(int i=0; i<entityCount; ++i)
                            matchCount += query.MatchesIgnoreFilter(entities[i]) ? 1 : 0;
                        Assert.AreEqual(entityCount, matchCount);
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup(new SampleGroup($"MatchesIgnoreFilter_{entityCount}x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_IsEmpty_N_Chunks([Values(1, 100)] int archetypeCount, [Values(1, 100)] int chunkCount, [Values] bool resultEmpty,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.ManyComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            // This measures the cost of EntityQueries filtering a large number of archetypes it matches
            using var archetypes = CreateUniqueLargeArchetypes(archetypeCount, enabledBitsMode);
            var lastArchetype = archetypes[archetypes.Length - 1];
            var ent = default(Entity);
            if (!resultEmpty)
            {
                ent = m_Manager.CreateEntity(lastArchetype);
                m_Manager.AddSharedComponentManaged(ent, new EcsTestSharedComp { value = 42 });
            }

            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
            {
                for (int i = 0; i < archetypeCount; i++)
                {
                    using var entities = m_Manager.CreateEntity(archetypes[i],
                        archetypes[i].ChunkCapacity * chunkCount, World.UpdateAllocator.ToAllocator);
                    foreach (var e in entities)
                        m_Manager.SetComponentEnabled<EcsTestDataEnableable>(e, false);
                }
            }

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>(),ComponentType.ReadWrite<EcsTestSharedComp>())
                : m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>(),ComponentType.ReadWrite<EcsTestSharedComp>());
            Measure.Method(
                    () =>
                    {
                        bool result = resultEmpty;
                        for(int i=0; i<100; ++i)
                            result = result && query.IsEmpty;
                        Assert.AreEqual(resultEmpty, result);
                    })
                .WarmupCount(1)
                .MeasurementCount(10)
                .SampleGroup(new SampleGroup("IsEmpty WithoutFilter 100x", SampleUnit.Microsecond))
                .Run();

            query.AddSharedComponentFilterManaged(new EcsTestSharedComp { value = 42 });
            Measure.Method(
                    () =>
                    {
                        bool result = resultEmpty;
                        for(int i=0; i<100; ++i)
                            result = result && query.IsEmpty;
                        Assert.AreEqual(resultEmpty, result);
                    })
                .WarmupCount(1)
                .MeasurementCount(10)
                .SampleGroup(new SampleGroup("IsEmpty WithFilter 100x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_IsEmpty_N_Archetypes([Values(1, 100, 10000)] int archetypeCount, [Values] bool resultEmpty,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.ManyComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            // This measures the cost of EntityQueries filtering a large number of archetypes it matches
            using var archetypes = CreateUniqueTagArchetypes(archetypeCount, enabledBitsMode);
            var lastArchetype = archetypes[archetypes.Length - 1];
            var ent = default(Entity);
            if (!resultEmpty)
            {
                ent = m_Manager.CreateEntity(lastArchetype);
                m_Manager.AddSharedComponentManaged(ent, new EcsTestSharedComp { value = 42 });
            }

            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
            {
                for (int i = 0; i < archetypeCount; i++)
                {
                    var e = m_Manager.CreateEntity(archetypes[i]);
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(e, false);
                }
            }

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>(),ComponentType.ReadWrite<EcsTestSharedComp>())
                : m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>(),ComponentType.ReadWrite<EcsTestSharedComp>());
            Measure.Method(
                    () =>
                    {
                        bool result = resultEmpty;
                        for(int i=0; i<100; ++i)
                            result = result && query.IsEmpty;
                        Assert.AreEqual(resultEmpty, result);
                    })
                .WarmupCount(1)
                .MeasurementCount(10)
                .SampleGroup(new SampleGroup("IsEmpty WithoutFilter 100x", SampleUnit.Microsecond))
                .Run();

            query.AddSharedComponentFilterManaged(new EcsTestSharedComp { value = 42 });
            Measure.Method(
                    () =>
                    {
                        bool result = resultEmpty;
                        for(int i=0; i<100; ++i)
                            result = result && query.IsEmpty;
                        Assert.AreEqual(resultEmpty, result);
                    })
                .WarmupCount(1)
                .MeasurementCount(10)
                .SampleGroup(new SampleGroup("IsEmpty WithFilter 100x", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_IsEmptyIgnoreFilter_N_Archetypes_SparseMatch([Values(1, 100, 10000)] int archetypeCount, [Values] bool resultEmpty)
        {
            // This measures the cost of EntityQueries filtering a large number of archetypes it matches
            var archetypes = CreateUniqueTagArchetypes(archetypeCount, EnabledBitsMode.NoEnableableComponents);
            var lastArchetype = archetypes[archetypes.Length - 1];

            if(!resultEmpty)
                m_Manager.CreateEntity(lastArchetype);

            var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());

            Measure.Method(
                () =>
                {
                    //give some extra work that won't be optimized out.
                    bool lastResult = true;
                    // To get a useful metric
                    for (int i = 0; i < 1000; i++)
                    {
                        var r = query.IsEmptyIgnoreFilter;
                        lastResult = r;
                    }
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup("IsEmptyIgnoreFilter (1000x)")
                .Run();

            archetypes.Dispose();
        }

        [Test, Performance]
        public void EntityQuery_GetSingletonEntity_N_Archetypes_SparseMatch([Values(1, 100, 10000)] int archetypeCount)
        {
            var archetypes = CreateUniqueTagArchetypes(archetypeCount, EnabledBitsMode.NoEnableableComponents);
            var lastArchetype = archetypes[archetypes.Length - 1];
            var singleton = m_Manager.CreateEntity(lastArchetype);

            var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());

            Measure.Method(
                    () =>
                    {
                        // To get a useful metric
                        for (int i = 0; i < 1000; i++)
                        {
                            var ent = query.GetSingletonEntity();
                            Assert.AreEqual(singleton, ent);
                        }
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup("GetSingletonEntity (1000x)")
                .Run();

            archetypes.Dispose();
        }

        [Test, Performance]
        public void EntityQuery_GetSingleton_N_Archetypes_SparseMatch([Values(1, 100, 10000)] int archetypeCount)
        {
            using var archetypes = CreateUniqueTagArchetypes(archetypeCount, EnabledBitsMode.NoEnableableComponents);
            var lastArchetype = archetypes[archetypes.Length - 1];
            var singleton = m_Manager.CreateEntity(lastArchetype);
            m_Manager.SetComponentData(singleton, new EcsTestData {value=42});

            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());

            Measure.Method(
                    () =>
                    {
                        // To get a useful metric
                        int sum = 0;
                        for (int i = 0; i < 1000; i++)
                        {
                            sum += query.GetSingleton<EcsTestData>().value;
                        }
                        Assert.AreEqual(42*1000, sum);
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup("GetSingleton (1000x)")
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_GetSingletonBuffer_N_Archetypes_SparseMatch([Values(1, 100, 10000)] int archetypeCount)
        {
            using var archetypes = CreateUniqueTagArchetypes(archetypeCount,
                EnabledBitsMode.NoEnableableComponents, typeof(EcsIntElement));
            var lastArchetype = archetypes[archetypes.Length - 1];
            var singleton = m_Manager.CreateEntity(lastArchetype);
            var buffer = m_Manager.GetBuffer<EcsIntElement>(singleton);
            buffer.Add(new EcsIntElement { Value = 17 });

            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsIntElement>());

            Measure.Method(
                    () =>
                    {
                        // To get a useful metric
                        int sum = 0;
                        for (int i = 0; i < 1000; i++)
                        {
                            sum += query.GetSingletonBuffer<EcsIntElement>(true).Length;
                        }
                        Assert.AreEqual(1000, sum);
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup("GetSingletonBuffer (1000x)")
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_SetSingleton_N_Archetypes_SparseMatch([Values(1, 100, 10000)] int archetypeCount)
        {
            using var archetypes = CreateUniqueTagArchetypes(archetypeCount, EnabledBitsMode.NoEnableableComponents);
            var lastArchetype = archetypes[archetypes.Length - 1];
            var singleton = m_Manager.CreateEntity(lastArchetype);
            m_Manager.SetComponentData(singleton, new EcsTestData {value=42});

            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>());

            Measure.Method(
                    () =>
                    {
                        // To get a useful metric
                        for (int i = 0; i < 1000; i++)
                        {
                            query.SetSingleton<EcsTestData>(new EcsTestData {value = i});
                            Assert.AreEqual(i, m_Manager.GetComponentData<EcsTestData>(singleton).value);
                        }
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup("SetSingleton (1000x)")
                .Run();
        }

        unsafe ComponentType[] CreateRandomMatchingQueryTypes(NativeArray<EntityArchetype> archetypes)
        {
            var random = new Random(34343);
            var typeSet = new HashSet<ComponentType>();

            for (int i = 0; i < archetypes.Length; i++)
            {
                if (random.NextBool())
                {
                    // Start from 1, since Types[0] is always Entity
                    for (int typeIndex = 1; typeIndex < archetypes[i].Archetype->TypesCount; typeIndex++)
                    {
                        typeSet.Add(archetypes[i].Archetype->Types[typeIndex].ToComponentType());
                    }
                }
            }

            return typeSet.ToArray();
        }

        [Test, Performance]
        public void EntityQuery_IsEmptyIgnoreFilter_N_Archetypes_RandomMatch([Values(1, 100, 10000)] int archetypeCount, [Values] bool resultEmpty)
        {
            // This measures the cost of EntityQueries filtering a large number of archetypes it matches
            using var archetypes = CreateUniqueTagArchetypes(archetypeCount, EnabledBitsMode.NoEnableableComponents);
            var lastArchetype = archetypes[archetypes.Length - 1];

            if(resultEmpty)
                m_Manager.CreateEntity(lastArchetype);

            using var query = m_Manager.CreateEntityQuery(CreateRandomMatchingQueryTypes(archetypes));

            Measure.Method(
                () =>
                {
                    //give some extra work that won't be optimized out.
                    bool lastResult = true;
                    // To get a useful metric
                    for (int i = 0; i < 1000; i++)
                    {
                        var r = query.IsEmptyIgnoreFilter;
                        lastResult = r;
                    }
                })
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup("IsEmptyIgnoreFilter (1000x)")
                .Run();
        }

        // Query Performance Test Helpers ----

        private static List<ComponentType[]> CreateComponentTypesListFromArchetypes(NativeArray<NativeArray<ComponentType>> archetypes)
        {
            var queryTypes = new List<ComponentType[]>();
            foreach (var archetype in archetypes)
            {
                queryTypes.Add(archetype.ToArray());
            }

            return queryTypes;
        }

        private static List<EntityQueryDesc> CreateEntityQueryDescsFromArchetypes(NativeArray<NativeArray<ComponentType>> archetypes)
        {
            var queryTypes = new List<EntityQueryDesc>();
            foreach (var archetype in archetypes)
            {
                queryTypes.Add(new EntityQueryDesc { All = archetype.ToArray() });
            }

            return queryTypes;
        }

        private static unsafe EntityQueryBuilder CreateEntityQueryBuilderFromArchetype(NativeArray<ComponentType> types)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp, (ComponentType*)types.GetUnsafeReadOnlyPtr(), types.Length);
            return builder;
        }

        [BurstCompile(CompileSynchronously = true)]
        partial struct QueryISystem : ISystem
        {
            [BurstDiscard]
            static void VerifyBurstCompiled()
            {
                Assert.Fail("QueryISystem is expected to be BurstCompiled and it isn't.");
            }
            [BurstCompile(CompileSynchronously = true)]
            public static void GetQueries(ref SystemState state, ref NativeArray<NativeArray<ComponentType>> queries)
            {
                VerifyBurstCompiled();
                for (int i = 0; i < queries.Length; i++)
                {
                    var builder = CreateEntityQueryBuilderFromArchetype(queries[i]);
                    var query = state.GetEntityQuery(builder);
                }
            }

            [BurstCompile(CompileSynchronously = true)]
            public static void CreateQueries(ref SystemState state, ref NativeArray<NativeArray<ComponentType>> queries)
            {
                VerifyBurstCompiled();
                for (int i = 0; i < queries.Length; i++)
                {
                    var builder = CreateEntityQueryBuilderFromArchetype(queries[i]);
                    var query = state.GetEntityQuery(builder);
                }
            }
        }

        // Query Creation ------

        [Test, Performance]
        public void EntityQuery_CreateEntityQuery_RealArchetypes([Values(1,8,16)] int componentsPerQuery)
        {
            int archetypeCount = 10000;
            NativeArray<EntityArchetype> archetypes = default;
            var queryTypes = new ComponentType[componentsPerQuery];
            var rng = new Random(1234);
            Measure.Method(
                    () =>
                    {
                        var query = m_Manager.CreateEntityQuery(queryTypes);
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                // Create and destroy the World (and EntityManager) each measurement, so we know it's not caching query data
                .SetUp(() =>
                {
                    Setup();
                    archetypes = CreateUniqueTagArchetypes(archetypeCount);
                    int ti = rng.NextInt(TagTypes.Length);
                    for (int i = 0; i < componentsPerQuery; ++i)
                        queryTypes[i] = TagTypes[(ti+i) % TagTypes.Length];
                })
                .CleanUp(() =>
                {
                    TearDown();
                    archetypes.Dispose();
                })
                .SampleGroup(new SampleGroup("CreateEntityQuery", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_CreateEntityQuery_ComponentTypeArray_UniqueArchetypes(
            [Values(1, 100, 1000)] int queryCount, [Values(1,2,16)] int componentsPerQuery)
        {
            using var archetypes = CreateUniqueTagCombinations(queryCount, componentsPerQuery);
            var queryTypes = CreateComponentTypesListFromArchetypes(archetypes);

            Measure.Method(
                    () =>
                    {
                        for (int i = 0; i < queryCount; i++)
                        {
                            var query = m_Manager.CreateEntityQuery(queryTypes[i]);
                        }
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                // Create and destroy the World (and EntityManager) each measurement, so we know it's not caching query data
                .SetUp(Setup)
                .CleanUp(TearDown)
                .SampleGroup("CreateEntityQuery")
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_CreateEntityQuery_EntityQueryDesc_UniqueArchetypes(
                [Values(1, 100, 1000)] int queryCount, [Values(1,2,16)] int componentsPerQuery)
        {
            using var archetypes = CreateUniqueTagCombinations(queryCount, componentsPerQuery);
            var queryDescs = CreateEntityQueryDescsFromArchetypes(archetypes);

            Measure.Method(
                    () =>
                    {
                        for (int i = 0; i < queryCount; i++)
                        {
                            var query = m_Manager.CreateEntityQuery(queryDescs[i]);
                        }
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                // Create and destroy the World (and EntityManager) each measurement, so we know it's not caching query data
                .SetUp(Setup)
                .CleanUp(TearDown)
                .SampleGroup("CreateEntityQuery")
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_CreateEntityQuery_EntityQueryBuilder_UniqueArchetypes(
                [Values(1, 100, 1000)] int queryCount, [Values(1,2,16)] int componentsPerQuery)
        {
            using var archetypes = CreateUniqueTagCombinations(queryCount, componentsPerQuery);

            Measure.Method(
                    () =>
                    {
                        for (int i = 0; i < queryCount; i++)
                        {
                            var queryBuilder = CreateEntityQueryBuilderFromArchetype(archetypes[i]);
                            var query = m_Manager.CreateEntityQuery(queryBuilder);
                        }
                    })
                .WarmupCount(1)
                .MeasurementCount(100)
                // Create and destroy the World (and EntityManager) each measurement, so we know it's not caching query data
                .SetUp(Setup)
                .CleanUp(TearDown)
                .SampleGroup("CreateEntityQuery")
                .Run();
        }

        [Test, Performance]
        public unsafe void EntityQuery_CreateEntityQuery_EntityQueryBuilder_Burst_UniqueArchetypes(
            [Values(1, 100, 1000)] int queryCount, [Values(1,2,16)] int componentsPerQuery)
        {
            var archetypes = CreateUniqueTagCombinations(queryCount, componentsPerQuery);

            SystemState* state = null;

            Measure.Method(
                    () =>
                    {
                        QueryISystem.CreateQueries(ref *state, ref archetypes);
                    })
                // Warmup count ensures methods are burst-compiled before test. Setup recreates World so it should have
                // no queries cached.
                .WarmupCount(1)
                .MeasurementCount(100)
                // Create and destroy the World (and EntityManager) each measurement, so we know it's not caching query data
                .SetUp(() =>
                {
                    Setup();
                    var handle = m_World.GetOrCreateSystem<QueryISystem>();
                    state = m_World.Unmanaged.ResolveSystemState(handle);
                })
                .CleanUp(() =>
                {
                    state = null;
                    TearDown();
                })
                .SampleGroup("CreateEntityQuery")
                .Run();
        }

        // Query Caching ----

        [Test, Performance]
        public void EntityQuery_GetEntityQuery_ComponentTypeArray_UniqueArchetypes(
            [Values(1, 10, 100)] int queryCount, [Values(1,2,16)] int componentsPerQuery)
        {
            using var archetypes = CreateUniqueTagCombinations(queryCount, componentsPerQuery);
            var queryTypes = CreateComponentTypesListFromArchetypes(archetypes);

            var system = m_World.CreateSystemManaged<EmptySystem>();

            Measure.Method(
                    () =>
                    {
                        for (int i = 0; i < queryCount; i++)
                        {
                            var query = system.GetEntityQuery(queryTypes[i]);
                        }
                    })
                // Warm up to create the queries in the first place.
                // This test should time how long it takes to retrieve the cached query.
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup("GetEntityQuery")
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_GetEntityQuery_EntityQueryDesc_UniqueArchetypes(
            [Values(1, 10, 100)] int queryCount, [Values(1,2,16)] int componentsPerQuery)
        {
            using var archetypes = CreateUniqueTagCombinations(queryCount, componentsPerQuery);
            var queryDescs = CreateEntityQueryDescsFromArchetypes(archetypes);

            var system = m_World.CreateSystemManaged<EmptySystem>();

            Measure.Method(
                    () =>
                    {
                        for (int i = 0; i < queryCount; i++)
                        {
                            var query = system.GetEntityQuery(queryDescs[i]);
                        }
                    })
                // Warm up to create the queries in the first place.
                // This test should time how long it takes to retrieve the cached query.
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup("GetEntityQuery")
                .Run();
        }

        [Test, Performance]
        public void EntityQuery_GetEntityQuery_EntityQueryBuilder_UniqueArchetypes(
                [Values(1, 10, 100)] int queryCount, [Values(1,2,16)] int componentsPerQuery)
        {
            using var archetypes = CreateUniqueTagCombinations(queryCount, componentsPerQuery);

            var system = m_World.CreateSystemManaged<EmptySystem>();

            Measure.Method(
                    () =>
                    {
                        for (int i = 0; i < queryCount; i++)
                        {
                            var queryBuilder = CreateEntityQueryBuilderFromArchetype(archetypes[i]);
                            var query = system.GetEntityQuery(queryBuilder);
                        }
                    })
                // Warm up to create the queries in the first place.
                // This test should time how long it takes to retrieve the cached query.
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup("GetEntityQuery")
                .Run();
        }

        [Test, Performance]
        public unsafe void EntityQuery_GetEntityQuery_EntityQueryBuilder_Burst_UniqueArchetypes(
                [Values(1, 10, 100)] int queryCount, [Values(1,2,16)] int componentsPerQuery)
        {
            var archetypes = CreateUniqueTagCombinations(queryCount, componentsPerQuery);

            SystemState* state = null;

            Measure.Method(
                    () =>
                    {
                        QueryISystem.GetQueries(ref *state, ref archetypes);
                    })
                // Warm up to create the queries in the first place.
                // This test should time how long it takes to retrieve the cached query.
                .WarmupCount(1)
                .MeasurementCount(100)
                .SampleGroup("GetEntityQuery")
                .SetUp(() =>
                {
                    Setup();
                    var handle = m_World.GetOrCreateSystem<QueryISystem>();
                    state = m_World.Unmanaged.ResolveSystemState(handle);
                    // Get the queries one first to create them, so we're only timing cache access
                    QueryISystem.GetQueries(ref *state, ref archetypes);
                })
                .CleanUp(() =>
                {
                    state = null;
                    TearDown();
                })
                .Run();
        }

        //-----------

        [Test, Performance]
        public void CalculateEntityCount_N_Archetypes_M_ChunksPerArchetype([Values(1, 100)] int archetypeCount, [Values(1, 100)] int chunkCount,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.ManyComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            using var archetypes = CreateUniqueTagArchetypes(archetypeCount, enabledBitsMode, typeof(EcsTestData2));
            int expectedEntityCount = 0;
            int expectedFilteredEntityCount = archetypeCount;
            int expectedUnfilteredEntityCount = 0;
            for (int i = 0; i < archetypes.Length; ++i)
            {
                int chunkCapacity = archetypes[i].ChunkCapacity;
                using var entities = new NativeArray<Entity>( chunkCapacity * chunkCount, Allocator.Temp);
                m_Manager.CreateEntity(archetypes[i], entities);
                expectedEntityCount += entities.Length;
                expectedUnfilteredEntityCount += entities.Length;
                if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled)
                {
                    for (int entityIndex=0; entityIndex < entities.Length; entityIndex += 2)
                    {
                        m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[entityIndex], false);
                        expectedEntityCount -= 1;
                    }
                }
                // One enabled entity in each archetype should match the shared component filter
                m_Manager.SetSharedComponentManaged(entities[1], new EcsTestSharedComp(archetypeCount));
            }

            // Create extra empty archetypes to make sure we're not wasting time searching them.
            using var emptyArchetypes = CreateUniqueTagArchetypes(1000, enabledBitsMode, typeof(EcsTestData2), typeof(EcsTestData3));

            using var query = enabledBitsMode == EnabledBitsMode.NoEnableableComponents
                ? m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp))
                : m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            Measure.Method(
                    () =>
                    {
                        int sum = 0;
                        for(int i=0; i<100; ++i)
                            sum += query.CalculateEntityCountWithoutFiltering();
                        Assert.AreEqual(100*expectedUnfilteredEntityCount, sum);
                    })
                .WarmupCount(1)
                .MeasurementCount(10)
                .SampleGroup(new SampleGroup("CalculateEntityCountWithoutFiltering 100x", SampleUnit.Microsecond))
                .Run();

            Measure.Method(
                () =>
                {
                    int sum = 0;
                    for(int i=0; i<100; ++i)
                        sum += query.CalculateEntityCount();
                    Assert.AreEqual(100*expectedEntityCount, sum);
                })
                .WarmupCount(1)
                .MeasurementCount(10)
                .SampleGroup(new SampleGroup("CalculateEntityCount 100x", SampleUnit.Microsecond))
                .Run();

            // Add a shared component filter for a second test
            query.SetSharedComponentFilterManaged(new EcsTestSharedComp {value = archetypeCount});

            Measure.Method(
                () =>
                {
                    int sum = 0;
                    for(int i=0; i<100; ++i)
                        sum += query.CalculateEntityCount();
                    Assert.AreEqual(100*expectedFilteredEntityCount, sum);
                })
                .WarmupCount(1)
                .MeasurementCount(10)
                .SampleGroup(new SampleGroup("CalculateEntityCount 100x with Filtering", SampleUnit.Microsecond))
                .Run();
        }

        [Test, Performance]
        public void CalculateChunkCount_N_Archetypes_M_ChunksPerArchetype([Values(1, 100)] int archetypeCount, [Values(1, 100)] int chunkCount,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.ManyComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            using var archetypes = CreateUniqueTagArchetypes(archetypeCount, enabledBitsMode, typeof(EcsTestData2));
            for (int i = 0; i < archetypes.Length; ++i)
            {
                int chunkCapacity = archetypes[i].ChunkCapacity;
                using var entities = new NativeArray<Entity>( chunkCapacity * chunkCount, Allocator.Temp);
                m_Manager.CreateEntity(archetypes[i], entities);
                if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled)
                {
                    for (int entityIndex=0; entityIndex < entities.Length; entityIndex += 2)
                    {
                        m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[entityIndex], false);
                    }
                }
                // One enabled entity in each archetype should match the shared component filter
                m_Manager.SetSharedComponentManaged(entities[1], new EcsTestSharedComp(archetypeCount));
            }
            int expectedChunkCount = archetypes.Length * (chunkCount+1);
            int expectedFilteredChunkCount = archetypes.Length * 1;

            // Create extra empty archetypes, to make sure we're not wasting time searching them.
            using var emptyArchetypes = CreateUniqueTagArchetypes(1000, enabledBitsMode,
                typeof(EcsTestData2), typeof(EcsTestData3));

            using var query = enabledBitsMode == EnabledBitsMode.NoEnableableComponents
                ? m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestSharedComp))
                : m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable), typeof(EcsTestData2), typeof(EcsTestSharedComp));

            int sum = 0;
            Measure.Method(
                    () =>
                    {
                        for(int i=0; i<100; ++i)
                            sum += query.CalculateChunkCountWithoutFiltering();
                    })
                .WarmupCount(1)
                .CleanUp(
                    () =>
                    {
                        FastAssert.AreEqual(100*expectedChunkCount, sum);
                        sum = 0;
                    })
                .MeasurementCount(10)
                .SampleGroup(new SampleGroup("CalculateChunkCountWithoutFiltering 100x", SampleUnit.Microsecond))
                .Run();

            Measure.Method(
                () =>
                {
                    for(int i=0; i<100; ++i)
                        sum += query.CalculateChunkCount();
                })
                .CleanUp(
                    () =>
                    {
                        FastAssert.AreEqual(100*expectedChunkCount, sum);
                        sum = 0;
                    })
                .WarmupCount(1)
                .MeasurementCount(10)
                .SampleGroup(new SampleGroup("CalculateChunkCount 100x", SampleUnit.Microsecond))
                .Run();

            // Add a shared component filter for a second test
            query.SetSharedComponentFilter(new EcsTestSharedComp {value = archetypeCount});

            Measure.Method(
                () =>
                {
                    int sum = 0;
                    for(int i=0; i<100; ++i)
                        sum += query.CalculateChunkCount();
                    Assert.AreEqual(100*expectedFilteredChunkCount, sum);
                })
                .WarmupCount(1)
                .MeasurementCount(10)
                .SampleGroup(new SampleGroup("CalculateChunkCount 100x with Filtering", SampleUnit.Microsecond))
                .Run();
        }

        [Test,Performance]
        public void ToArchetypeChunkArray_Performance([Values(1, 100, 1000)] int archetypeCount, [Values] bool enableChunkFilter,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.NoComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            int chunksPerArchetype = 10;
            using var archetypes = CreateUniqueTagArchetypes(archetypeCount,
                enabledBitsMode, typeof(EcsTestData2));
            var sharedCompValue = new EcsTestSharedComp(17);
            for (int i = 0; i < archetypes.Length; ++i)
            {
                int chunkCapacity = archetypes[i].ChunkCapacity;
                using var entities = new NativeArray<Entity>( chunkCapacity * chunksPerArchetype, Allocator.Temp);
                m_Manager.CreateEntity(archetypes[i], entities);
                // One enabled entity in each archetype should match the shared component filter
                m_Manager.SetSharedComponentManaged(entities[1], sharedCompValue);
            }

            // Create extra empty archetypes to make sure we're not wasting time searching them.
            using var emptyArchetypes = CreateUniqueTagArchetypes(1000,
                enabledBitsMode, typeof(EcsTestData2), typeof(EcsTestData3));

            using var query = enabledBitsMode == EnabledBitsMode.NoEnableableComponents
                ? m_Manager.CreateEntityQuery(typeof(EcsTestData2), typeof(EcsTestSharedComp))
                : m_Manager.CreateEntityQuery(typeof(EcsTestData2), typeof(EcsTestSharedComp),
                    typeof(EcsTestDataEnableable));
            if (enableChunkFilter)
                query.SetSharedComponentFilterManaged(sharedCompValue);
            using var expectedList = query.ToArchetypeChunkListAsync(Allocator.Persistent, out var gatherJobHandle);
            gatherJobHandle.Complete();
            var expectedChunks = expectedList.AsArray().ToArray();

            var result =  default(NativeArray<ArchetypeChunk>);
            Measure.Method(
                    () =>
                    {
                        result = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
                    })

                .CleanUp( () =>
                {
                    CollectionAssert.AreEqual(expectedChunks, result.ToArray());
                    result.Dispose();
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup("ToArchetypeChunkArray")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        [Test,Performance]
        public void ToArchetypeChunkListAsync_Performance([Values(1, 100, 1000)] int archetypeCount, [Values] bool enableChunkFilter,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.NoComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            int chunksPerArchetype = 10;
            using var archetypes = CreateUniqueTagArchetypes(archetypeCount,
                enabledBitsMode, typeof(EcsTestData2));
            var sharedCompValue = new EcsTestSharedComp(17);
            for (int i = 0; i < archetypes.Length; ++i)
            {
                int chunkCapacity = archetypes[i].ChunkCapacity;
                using var entities = new NativeArray<Entity>( chunkCapacity * chunksPerArchetype, Allocator.Temp);
                m_Manager.CreateEntity(archetypes[i], entities);
                // One enabled entity in each archetype should match the shared component filter
                m_Manager.SetSharedComponentManaged(entities[1], sharedCompValue);
            }

            // Create extra archetypes to make sure we're not wasting time searching them.
            using var emptyArchetypes = CreateUniqueTagArchetypes(1000,
                    enabledBitsMode, typeof(EcsTestData2), typeof(EcsTestData3));

            using var query = enabledBitsMode == EnabledBitsMode.NoEnableableComponents
                ? m_Manager.CreateEntityQuery(typeof(EcsTestData2), typeof(EcsTestSharedComp))
                : m_Manager.CreateEntityQuery(typeof(EcsTestData2), typeof(EcsTestSharedComp),
                    typeof(EcsTestDataEnableable));
            if (enableChunkFilter)
                query.SetSharedComponentFilterManaged(sharedCompValue);
            var expectedChunks = query.ToArchetypeChunkArray(Allocator.Temp).ToArray();

            var result =  default(NativeList<ArchetypeChunk>);
            Measure.Method(
                    () =>
                    {
                        result = query.ToArchetypeChunkListAsync(World.UpdateAllocator.ToAllocator, out var jobHandle);
                        jobHandle.Complete();
                    })

                .CleanUp( () =>
                {
                    CollectionAssert.AreEqual(expectedChunks, result.AsArray().ToArray());
                    result.Dispose();
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup("ToArchetypeChunkListAsync")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        private void CreateArchetypesAndEntities(int entityCount, EnabledBitsMode enabledBitsMode, bool unique, params ComponentType[] extraTypes)
        {
            NativeArray<Entity> entities = CollectionHelper.CreateNativeArray<Entity>(entityCount,
                m_World.UpdateAllocator.ToAllocator);
            if (unique)
            {
                using var archetypes = CreateUniqueTagArchetypes(entityCount, enabledBitsMode, extraTypes);
                for (int entIter = 0; entIter < entityCount; entIter++)
                {
                    entities[entIter] = m_Manager.CreateEntity(archetypes[entIter]);
                    if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (entIter % 2 == 0))
                        m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[entIter], false);
                }
            }
            else
            {
                using var archetypes = CreateUniqueTagArchetypes(1, enabledBitsMode, extraTypes);
                m_Manager.CreateEntity(archetypes[0], entities);
                for (int entIter = 0; entIter < entityCount; entIter++)
                {
                    if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled && (entIter % archetypes[0].ChunkCapacity == 0))
                        m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[entIter], false);
                    else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled && (entIter % 2 == 0))
                        m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[entIter], false);
                }
            }
            entities.Dispose();
        }

        private void ToEntityArray_Performance(int entityCount, EnabledBitsMode enabledBitsMode, bool unique)
        {
            CreateArchetypesAndEntities(entityCount, enabledBitsMode, unique);

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp))
                : m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp),
                    typeof(EcsTestDataEnableable));

            int expectedCount = query.CalculateEntityCount();
            var result =  default(NativeArray<Entity>);
            Measure.Method(
                    () =>
                    {
                        result = query.ToEntityArray(World.UpdateAllocator.ToAllocator);
                    })

                    .CleanUp( () =>
                    {
                        Assert.AreEqual(expectedCount, result.Length);
                        result.Dispose();
                        World.UpdateAllocator.Rewind();
                    })
                .SampleGroup("ToEntityArray")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        private void ToComponentDataArray_Performance_SmallComponent(int entityCount, EnabledBitsMode enabledBitsMode, bool unique)
        {
            CreateArchetypesAndEntities(entityCount, enabledBitsMode, unique);

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(typeof(EcsTestData))
                : m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable), typeof(EcsTestData));

            int expectedCount = query.CalculateEntityCount();
            var result =  default(NativeArray<EcsTestData>);

            Measure.Method(
                    () => {
                        result = query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                    })
                .CleanUp(() =>
                {
                    Assert.AreEqual(expectedCount, result.Length);
                    result.Dispose();
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup("ToComponentDataArray")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        private void ToComponentDataArray_Performance_LargeComponent(int entityCount, EnabledBitsMode enabledBitsMode, bool unique)
        {
            CreateArchetypesAndEntities(entityCount, enabledBitsMode, unique, typeof(LargeComponent0));

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(typeof(LargeComponent0))
                : m_Manager.CreateEntityQuery(typeof(LargeComponent0), typeof(EcsTestDataEnableable));

            int expectedCount = query.CalculateEntityCount();
            var result =  default(NativeArray<LargeComponent0>);

            Measure.Method(
                    () => {
                        result = query.ToComponentDataArray<LargeComponent0>(World.UpdateAllocator.ToAllocator);
                    })
                .CleanUp(() =>
                {
                    Assert.AreEqual(expectedCount, result.Length);
                    result.Dispose();
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup("ToComponentDataArray")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        private void CopyFromComponentDataArray_Performance_SmallComponent(int entityCount, EnabledBitsMode enabledBitsMode, bool unique)
        {
            CreateArchetypesAndEntities(entityCount, enabledBitsMode, unique);

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(typeof(EcsTestData))
                : m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestDataEnableable));

            using var result =  query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);

            Measure.Method(
                    () => {
                        query.CopyFromComponentDataArray(result);
                    })
                .SampleGroup("CopyFromComponentDataArray")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        private void CopyFromComponentDataArray_Performance_LargeComponent(int entityCount, EnabledBitsMode enabledBitsMode, bool unique)
        {
            CreateArchetypesAndEntities(entityCount, enabledBitsMode, unique, typeof(LargeComponent0));

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(typeof(LargeComponent0))
                : m_Manager.CreateEntityQuery(typeof(LargeComponent0), typeof(EcsTestDataEnableable));

            using var result =  query.ToComponentDataArray<LargeComponent0>(World.UpdateAllocator.ToAllocator);

            Measure.Method(
                    () => {
                        query.CopyFromComponentDataArray(result);
                    })
                .SampleGroup("CopyFromComponentDataArray")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        [Test, Performance]
        public void ToEntityArray_Performance_Same([Values(1,1000,100000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            ToEntityArray_Performance(entityCount, enabledBitsMode, false);
        }

        [Test, Performance]
        public void ToEntityArray_Performance_Unique([Values(1,1000,10000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            ToEntityArray_Performance(entityCount, enabledBitsMode, true);
        }

        [Test, Performance]
        public void ToComponentDataArray_Performance_Same_SmallComponent(
            [Values(1,1000,100000)] int entityCount, [Values] EnabledBitsMode enabledBitsMode)
        {
            ToComponentDataArray_Performance_SmallComponent(entityCount, enabledBitsMode, false);
        }

        [Test, Performance]
        public void ToComponentDataArray_Performance_Same_LargeComponent(
            [Values(1,1000,100000)] int entityCount, [Values] EnabledBitsMode enabledBitsMode)
        {
            ToComponentDataArray_Performance_LargeComponent(entityCount, enabledBitsMode, false);
        }

        [Test, Performance]
        public void ToComponentDataArray_Performance_Unique_SmallComponent([Values(1,1000,10000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            ToComponentDataArray_Performance_SmallComponent(entityCount, enabledBitsMode, true);
        }

        [Test, Performance]
        public void ToComponentDataArray_Performance_Unique_LargeComponent([Values(1,1000,10000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            ToComponentDataArray_Performance_LargeComponent(entityCount, enabledBitsMode, true);
        }

        [Test, Performance]
        public void CopyFromComponentDataArray_Performance_Same_SmallComponent([Values(1,1000,100000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            CopyFromComponentDataArray_Performance_SmallComponent(entityCount, enabledBitsMode, false);
        }

        [Test, Performance]
        public void CopyFromComponentDataArray_Performance_Same_LargeComponent([Values(1,10000,100000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            CopyFromComponentDataArray_Performance_LargeComponent(entityCount, enabledBitsMode, false);
        }

        [Test, Performance]
        public void CopyFromComponentDataArray_Performance_Unique_SmallComponent([Values(1,1000,10000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            CopyFromComponentDataArray_Performance_SmallComponent(entityCount, enabledBitsMode, true);
        }

        [Test, Performance]
        public void CopyFromComponentDataArray_Performance_Unique_LargeComponent([Values(1,1000,10000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            CopyFromComponentDataArray_Performance_LargeComponent(entityCount, enabledBitsMode, true);
        }

        private void ToEntityListAsync_Performance(int entityCount, EnabledBitsMode enabledBitsMode, bool unique)
        {
            CreateArchetypesAndEntities(entityCount, enabledBitsMode, unique);

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp))
                : m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp),
                    typeof(EcsTestDataEnableable));

            int expectedCount = query.CalculateEntityCount();
            var result =  default(NativeList<Entity>);
            Measure.Method(
                    () =>
                    {
                        result = query.ToEntityListAsync(World.UpdateAllocator.ToAllocator, out var jobHandle);
                        jobHandle.Complete();
                    })

                    .CleanUp( () =>
                    {
                        Assert.AreEqual(expectedCount, result.Length);
                        result.Dispose();
                        World.UpdateAllocator.Rewind();
                    })
                .SampleGroup("ToEntityListAsync")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        private void ToComponentDataListAsync_Performance_SmallComponent(int entityCount, EnabledBitsMode enabledBitsMode, bool unique)
        {
            CreateArchetypesAndEntities(entityCount, enabledBitsMode, unique);

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(typeof(EcsTestData))
                : m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable), typeof(EcsTestData));

            int expectedCount = query.CalculateEntityCount();
            var result =  default(NativeList<EcsTestData>);

            Measure.Method(
                    () => {
                        result = query.ToComponentDataListAsync<EcsTestData>(World.UpdateAllocator.ToAllocator,
                            out var jobHandle);
                        jobHandle.Complete();
                    })
                .CleanUp(() =>
                {
                    Assert.AreEqual(expectedCount, result.Length);
                    result.Dispose();
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup("ToComponentDataListAsync")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        private void ToComponentDataListAsync_Performance_LargeComponent(int entityCount, EnabledBitsMode enabledBitsMode, bool unique)
        {
            CreateArchetypesAndEntities(entityCount, enabledBitsMode, unique, typeof(LargeComponent0));

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(typeof(LargeComponent0))
                : m_Manager.CreateEntityQuery(typeof(LargeComponent0), typeof(EcsTestDataEnableable));

            int expectedCount = query.CalculateEntityCount();
            var result =  default(NativeList<LargeComponent0>);

            Measure.Method(
                    () => {
                        result = query.ToComponentDataListAsync<LargeComponent0>(World.UpdateAllocator.ToAllocator, out var jobHandle);
                        jobHandle.Complete();
                    })
                .CleanUp(() =>
                {
                    Assert.AreEqual(expectedCount, result.Length);
                    result.Dispose();
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup("ToComponentDataListAsync")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        private void CopyFromComponentDataListAsync_Performance_SmallComponent(int entityCount, EnabledBitsMode enabledBitsMode, bool unique)
        {
            CreateArchetypesAndEntities(entityCount, enabledBitsMode, unique);

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(typeof(EcsTestData))
                : m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestDataEnableable));

            using var result =  query.ToComponentDataListAsync<EcsTestData>(World.UpdateAllocator.ToAllocator, out var gatherJobHandle);
            gatherJobHandle.Complete();

            Measure.Method(
                    () => {
                        query.CopyFromComponentDataListAsync(result, out var jobHandle);
                        jobHandle.Complete();
                    })
                .SampleGroup("CopyFromComponentDataListAsync")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        private void CopyFromComponentDataListAsync_Performance_LargeComponent(int entityCount, EnabledBitsMode enabledBitsMode, bool unique)
        {
            CreateArchetypesAndEntities(entityCount, enabledBitsMode, unique, typeof(LargeComponent0));

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(typeof(LargeComponent0))
                : m_Manager.CreateEntityQuery(typeof(LargeComponent0), typeof(EcsTestDataEnableable));

            using var result =  query.ToComponentDataListAsync<LargeComponent0>(World.UpdateAllocator.ToAllocator, out var gatherJobHandle);
            gatherJobHandle.Complete();

            Measure.Method(
                    () => {
                        query.CopyFromComponentDataListAsync(result, out var jobHandle);
                        jobHandle.Complete();
                    })
                .SampleGroup("CopyFromComponentDataListAsync")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        [Test, Performance]
        public void ToEntityListAsync_Performance_Same([Values(1,1000,100000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            ToEntityListAsync_Performance(entityCount, enabledBitsMode, false);
        }

        [Test, Performance]
        public void ToEntityListAsync_Performance_Unique([Values(1,1000,10000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            ToEntityListAsync_Performance(entityCount, enabledBitsMode, true);
        }

        [Test, Performance]
        public void ToComponentDataListAsync_Performance_Same_SmallComponent(
            [Values(1,1000,100000)] int entityCount, [Values] EnabledBitsMode enabledBitsMode)
        {
            ToComponentDataListAsync_Performance_SmallComponent(entityCount, enabledBitsMode, false);
        }

        [Test, Performance]
        public void ToComponentDataListAsync_Performance_Same_LargeComponent(
            [Values(1,1000,100000)] int entityCount, [Values] EnabledBitsMode enabledBitsMode)
        {
            ToComponentDataListAsync_Performance_LargeComponent(entityCount, enabledBitsMode, false);
        }

        [Test, Performance]
        public void ToComponentDataListAsync_Performance_Unique_SmallComponent([Values(1,1000,10000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            ToComponentDataListAsync_Performance_SmallComponent(entityCount, enabledBitsMode, true);
        }

        [Test, Performance]
        public void ToComponentDataListAsync_Performance_Unique_LargeComponent([Values(1,1000,10000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            ToComponentDataListAsync_Performance_LargeComponent(entityCount, enabledBitsMode, true);
        }

        [Test, Performance]
        public void CopyFromComponentDataListAsync_Performance_Same_SmallComponent([Values(1,1000,100000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            CopyFromComponentDataListAsync_Performance_SmallComponent(entityCount, enabledBitsMode, false);
        }

        [Test, Performance]
        public void CopyFromComponentDataListAsync_Performance_Same_LargeComponent([Values(1,1000,100000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            CopyFromComponentDataListAsync_Performance_LargeComponent(entityCount, enabledBitsMode, false);
        }

        [Test, Performance]
        public void CopyFromComponentDataListAsync_Performance_Unique_SmallComponent([Values(1,1000,10000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            CopyFromComponentDataListAsync_Performance_SmallComponent(entityCount, enabledBitsMode, true);
        }

        [Test, Performance]
        public void CopyFromComponentDataListAsync_Performance_Unique_LargeComponent([Values(1,1000,10000)] int entityCount,
            [Values] EnabledBitsMode enabledBitsMode)
        {
            CopyFromComponentDataListAsync_Performance_LargeComponent(entityCount, enabledBitsMode, true);
        }

        [BurstCompile]
        partial struct AsyncGatherScatterSystem : ISystem
        {
            // Disables all but every 3rd entity
            [BurstCompile]
            partial struct DisableJob : IJobEntity
            {
                [NativeDisableParallelForRestriction] public ComponentLookup<EcsTestDataEnableable> Lookup;
                void Execute(Entity e)
                {
                    int val = Lookup[e].value;
                    if ((val % 3) != 0)
                    {
                        Lookup.SetComponentEnabled(e, false);
                    }
                }
            }

            // Negates every value in the provided array
            [BurstCompile]
            struct ProcessJob : IJob
            {
                public NativeArray<EcsTestDataEnableable> ValuesArray;
                public void Execute()
                {
                    int valueCount = ValuesArray.Length;
                    for (int i = 0; i < valueCount; ++i)
                    {
                        int x = ValuesArray[i].value;
                        ValuesArray[i] = new EcsTestDataEnableable(-x);
                    }
                }
            }

            private EntityQuery _query;
            private ComponentLookup<EcsTestDataEnableable> _lookup;
            private int _expectedValueCount;

            public void OnCreate(ref SystemState state)
            {
                _query = state.GetEntityQuery(typeof(EcsTestDataEnableable));
                _lookup = state.GetComponentLookup<EcsTestDataEnableable>(false);
            }

            public void OnUpdate(ref SystemState state)
            {
                _lookup.Update(ref state);
                // Schedule job that disables entities
                var disableJob = new DisableJob { Lookup = _lookup };
                var disableJobHandle = disableJob.ScheduleByRef(_query, state.Dependency);
                // Extract component values. Must explicitly depend on disableJobHandle, since it was scheduled within the same system.
                var valueList = _query.ToComponentDataListAsync<EcsTestDataEnableable>(
                    state.WorldUpdateAllocator, disableJobHandle,
                    out var gatherJobHandle);
                // Process gathered values
                var processJob = new ProcessJob { ValuesArray = valueList.AsDeferredJobArray() };
                var processJobHandle = processJob.Schedule(gatherJobHandle);
                // Scatter processed values back to entities
                _query.CopyFromComponentDataListAsync(valueList, processJobHandle, out var scatterJobHandle);

                scatterJobHandle.Complete();
                Assert.AreEqual(_query.CalculateEntityCount(), valueList.Length);
                state.Dependency = default;
            }
        }

        [Test, Performance]
        public void AsyncGatherScatter_Integration_Performance()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            int entityCount = 1000;
            using var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Persistent);
            using var query = m_Manager.CreateEntityQuery(typeof(EcsTestDataEnableable));
            var sysHandle = m_World.CreateSystem<AsyncGatherScatterSystem>();
            Measure.Method(() =>
                {
                    sysHandle.Update(m_World.Unmanaged);
                })
                .SetUp(() =>
                {
                    // reset and re-enable all entities
                    for (int i = 0; i < entityCount; ++i)
                    {
                        m_Manager.SetComponentData(entities[i], new EcsTestDataEnableable(i));
                        m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], true);
                    }
                })
                .CleanUp(() =>
                {
                    for (int i = 0; i < entityCount; ++i)
                    {
                        bool expectedEnabled = ((i % 3) == 0);
                        bool actualEnabled = m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entities[i]);
                        Assert.AreEqual(expectedEnabled, actualEnabled, $"Entity {i} mismatch in enabled state");

                        int expectedValue = expectedEnabled ? -i : i;
                        int actualValue = m_Manager.GetComponentData<EcsTestDataEnableable>(entities[i]).value;
                        Assert.AreEqual(expectedValue, actualValue, $"Entity {i} value mismatch");
                    }
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup("Integration")
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        [Test, Performance]
        public void CalculateFilteredChunkIndexArray_Performance([Values] bool unique, [Values] bool enableChunkFilter,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.FewComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            int entityCount = 10000;
            CreateArchetypesAndEntities(entityCount, enabledBitsMode, unique);

            // Create extra archetypes that match the query but don't have any chunks
            using var emptyArchetypes = CreateUniqueTagArchetypes(1000, enabledBitsMode,
                typeof(EcsTestFloatData));

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp))
                : m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp),
                    typeof(EcsTestDataEnableable));
            if (enableChunkFilter)
                query.SetSharedComponentFilter(default(EcsTestSharedComp));

            int expectedChunkCount = query.CalculateChunkCount();
            var resultArray = default(NativeArray<int>);
            Measure.Method(
                    () =>
                    {
                        resultArray = query.CalculateFilteredChunkIndexArray(World.UpdateAllocator.ToAllocator);
                    })

                .CleanUp( () =>
                {
                    Assert.AreEqual(expectedChunkCount, resultArray.Length);
                    resultArray.Dispose();
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup(new SampleGroup("CalculateFilteredChunkIndexArray", SampleUnit.Microsecond))
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();

            Measure.Method(
                    () =>
                    {
                        resultArray = query.CalculateFilteredChunkIndexArrayAsync(World.UpdateAllocator.ToAllocator, default,
                            out var jobHandle);
                        jobHandle.Complete();
                    })

                .CleanUp( () =>
                {
                    Assert.AreEqual(expectedChunkCount, resultArray.Length);
                    resultArray.Dispose();
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup(new SampleGroup("CalculateFilteredChunkIndexArrayAsync", SampleUnit.Microsecond))
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }

        [Test, Performance]
        public void CalculateBaseEntityIndexArray_Performance([Values] bool unique, [Values] bool enableChunkFilter,
            [Values(EnabledBitsMode.NoEnableableComponents, EnabledBitsMode.FewComponentsDisabled)] EnabledBitsMode enabledBitsMode)
        {
            int entityCount = 10000;
            CreateArchetypesAndEntities(entityCount, enabledBitsMode, unique);

            // Create extra archetypes that match the query but don't have any chunks
            using var emptyArchetypes = CreateUniqueTagArchetypes(1000, enabledBitsMode,
                typeof(EcsTestFloatData));

            using var query = (enabledBitsMode == EnabledBitsMode.NoEnableableComponents)
                ? m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp))
                : m_Manager.CreateEntityQuery(typeof(EcsTestData), typeof(EcsTestSharedComp),
                    typeof(EcsTestDataEnableable));
            if (enableChunkFilter)
                query.SetSharedComponentFilter(default(EcsTestSharedComp));

            int expectedChunkCount = query.CalculateChunkCount();
            var resultArray = default(NativeArray<int>);
            Measure.Method(
                    () =>
                    {
                        resultArray = query.CalculateBaseEntityIndexArray(World.UpdateAllocator.ToAllocator);
                    })

                .CleanUp( () =>
                {
                    Assert.AreEqual(expectedChunkCount, resultArray.Length);
                    resultArray.Dispose();
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup(new SampleGroup("CalculateBaseEntityIndexArray", SampleUnit.Microsecond))
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();

            Measure.Method(
                    () =>
                    {
                        resultArray = query.CalculateBaseEntityIndexArrayAsync(World.UpdateAllocator.ToAllocator, default,
                            out var jobHandle);
                        jobHandle.Complete();
                    })

                .CleanUp( () =>
                {
                    Assert.AreEqual(expectedChunkCount, resultArray.Length);
                    resultArray.Dispose();
                    World.UpdateAllocator.Rewind();
                })
                .SampleGroup(new SampleGroup("CalculateBaseEntityIndexArrayAsync", SampleUnit.Microsecond))
                .WarmupCount(1) // make sure we're not timing job compilation on the first run
                .MeasurementCount(100)
                .Run();
        }
    }
}
