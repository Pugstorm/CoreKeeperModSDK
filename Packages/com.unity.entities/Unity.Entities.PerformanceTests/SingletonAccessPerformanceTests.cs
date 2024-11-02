using System;
using Unity.Entities.Tests;
using Unity.PerformanceTesting;
using Unity.Collections;
using NUnit.Framework;
using Unity.Burst;
using Unity.Jobs;

namespace Unity.Entities.PerformanceTests
{
    public partial class SingletonAccessTestFixture : ECSTestsFixture
    {
        protected partial class TestComponentSystem : SystemBase
        {
            readonly int k_Count = 100000;
            EntityQuery m_Query, m_QueryWithFilter;
            EntityQuery m_EnableableQuery, m_EnableableQueryWithFilter;
            EntityQuery m_BufferQuery, m_BufferQueryWithFilter;
            EntityQuery m_EnableableBufferQuery, m_EnableableBufferQueryWithFilter;

            protected override void OnUpdate()
            {
            }

            protected override void OnCreate()
            {
                base.OnCreate();

                m_Query = EntityManager.CreateEntityQuery(typeof(EcsTestFloatData));
                m_QueryWithFilter = EntityManager.CreateEntityQuery(typeof(EcsTestFloatData), typeof(EcsTestSharedComp));
                m_QueryWithFilter.SetSharedComponentFilterManaged(new EcsTestSharedComp(1));

                m_BufferQuery = EntityManager.CreateEntityQuery(typeof(EcsIntElement));
                m_BufferQueryWithFilter = EntityManager.CreateEntityQuery(typeof(EcsIntElement), typeof(EcsTestSharedComp));
                m_BufferQueryWithFilter.SetSharedComponentFilterManaged(new EcsTestSharedComp(1));
            }

            public void ClearQueries()
            {
                m_Query.Dispose();
                m_QueryWithFilter.Dispose();
                m_BufferQuery.Dispose();
                m_BufferQueryWithFilter.Dispose();
            }

            public void GetSingletonTest(SingletonAccessPerformanceTests.AccessType accessType, float expectedValue)
            {
                float accumulate = 0.0f;
                switch (accessType)
                {
                    case SingletonAccessPerformanceTests.AccessType.ThroughSystem:
                        for (int i = 0; i < k_Count; i++)
                            accumulate += SystemAPI.GetSingleton<EcsTestFloatData>().Value;
                        break;
                    case SingletonAccessPerformanceTests.AccessType.ThroughQuery:
                        for (int i = 0; i < k_Count; i++)
                            accumulate += m_Query.GetSingleton<EcsTestFloatData>().Value;
                        break;
                    case SingletonAccessPerformanceTests.AccessType.ThroughQueryWithFilter:
                        for (int i = 0; i < k_Count; i++)
                            accumulate += m_QueryWithFilter.GetSingleton<EcsTestFloatData>().Value;
                        break;
                }
                Assert.AreEqual(k_Count * expectedValue, accumulate);
            }

            public void GetSingletonRWTest(SingletonAccessPerformanceTests.AccessType accessType, float expectedValue)
            {
                float accumulate = 0.0f;
                switch (accessType)
                {
                    case SingletonAccessPerformanceTests.AccessType.ThroughSystem:
                        for (int i = 0; i < k_Count; i++)
                            accumulate += SystemAPI.GetSingletonRW<EcsTestFloatData>().ValueRW.Value;
                        break;
                    case SingletonAccessPerformanceTests.AccessType.ThroughQuery:
                        for (int i = 0; i < k_Count; i++)
                            accumulate += m_Query.GetSingletonRW<EcsTestFloatData>().ValueRW.Value;
                        break;
                    case SingletonAccessPerformanceTests.AccessType.ThroughQueryWithFilter:
                        for (int i = 0; i < k_Count; i++)
                            accumulate += m_QueryWithFilter.GetSingletonRW<EcsTestFloatData>().ValueRW.Value;
                        break;
                }
                Assert.AreEqual(k_Count * expectedValue, accumulate);
            }

            public void GetSingletonBufferTest(SingletonAccessPerformanceTests.AccessType accessType, int expectedLength)
            {
                int accumulate = 0;
                switch (accessType)
                {
                    case SingletonAccessPerformanceTests.AccessType.ThroughSystem:
                        for (int i = 0; i < k_Count; i++)
                            accumulate += SystemAPI.GetSingletonBuffer<EcsIntElement>().Length;
                        break;
                    case SingletonAccessPerformanceTests.AccessType.ThroughQuery:
                        for (int i = 0; i < k_Count; i++)
                            accumulate += m_BufferQuery.GetSingletonBuffer<EcsIntElement>().Length;
                        break;
                    case SingletonAccessPerformanceTests.AccessType.ThroughQueryWithFilter:
                        for (int i = 0; i < k_Count; i++)
                            accumulate += m_BufferQueryWithFilter.GetSingletonBuffer<EcsIntElement>().Length;
                        break;
                }
                Assert.AreEqual(k_Count * expectedLength, accumulate);
            }

            public void GetSingletonEntityTest(SingletonAccessPerformanceTests.AccessType accessType, int expectedVersion)
            {
                Entity entity;
                int accumulate = 0;
                switch (accessType)
                {
                    case SingletonAccessPerformanceTests.AccessType.ThroughSystem:
                        for (int i = 0; i < k_Count; i++)
                        {
                            entity = SystemAPI.GetSingletonEntity<EcsTestFloatData>();
                            accumulate += entity.Version;
                        }
                        break;
                    case SingletonAccessPerformanceTests.AccessType.ThroughQuery:
                        for (int i = 0; i < k_Count; i++)
                        {
                            entity = m_Query.GetSingletonEntity();
                            accumulate += entity.Version;
                        }
                        break;
                    case SingletonAccessPerformanceTests.AccessType.ThroughQueryWithFilter:
                        for (int i = 0; i < k_Count; i++)
                        {
                            entity = m_QueryWithFilter.GetSingletonEntity();
                            accumulate += entity.Version;
                        }
                        break;
                }
                Assert.AreEqual(k_Count * expectedVersion, accumulate);
            }

            public void HasSingletonTest()
            {
                int accumulate = 0;
                for (int i = 0; i < k_Count; i++)
                    accumulate += SystemAPI.HasSingleton<EcsTestFloatData>() ? 1 : 0;
                Assert.AreEqual(k_Count, accumulate);
            }

            public void SetSingletonTest(SingletonAccessPerformanceTests.AccessType accessType)
            {
                switch (accessType)
                {
                    case SingletonAccessPerformanceTests.AccessType.ThroughSystem:
                        for (int i = 0; i < k_Count; i++)
                            SystemAPI.SetSingleton(new EcsTestFloatData());
                        break;
                    case SingletonAccessPerformanceTests.AccessType.ThroughQuery:
                        for (int i = 0; i < k_Count; i++)
                            m_Query.SetSingleton(new EcsTestFloatData());
                        break;
                    case SingletonAccessPerformanceTests.AccessType.ThroughQueryWithFilter:
                        for (int i = 0; i < k_Count; i++)
                            m_QueryWithFilter.SetSingleton(new EcsTestFloatData());
                        break;
                }
            }

        }
        protected TestComponentSystem TestSystem => World.GetOrCreateSystemManaged<TestComponentSystem>();
    }

    [Category("Performance")]
    public class SingletonAccessPerformanceTests : SingletonAccessTestFixture
    {
        Entity m_Entity;

        public enum AccessType
        {
            ThroughSystem,
            ThroughQuery,
            ThroughQueryWithFilter
        }

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            m_Entity = m_Manager.CreateEntity(typeof(EcsTestFloatData),
                typeof(EcsIntElement), typeof(EcsTestDataEnableable),
                typeof(EcsIntElementEnableable), typeof(EcsTestSharedComp));
            m_Manager.SetComponentData(m_Entity, new EcsTestFloatData { Value = 17 });
            m_Manager.SetComponentData(m_Entity, new EcsTestDataEnableable { value = 23 });
            var buffer = m_Manager.GetBuffer<EcsIntElement>(m_Entity);
            buffer.Add(new EcsIntElement { Value = 37 });
            var buffer2 = m_Manager.GetBuffer<EcsIntElementEnableable>(m_Entity);
            buffer2.Add(new EcsIntElementEnableable { Value = 42 });
            buffer2.Add(new EcsIntElementEnableable { Value = 56 });
            m_Manager.SetSharedComponentManaged(m_Entity, new EcsTestSharedComp(1));
        }

        [TearDown]
        public override void TearDown()
        {
            m_Manager.DestroyEntity(m_Entity);
            TestSystem.ClearQueries();
            base.TearDown();
        }

        [Test, Performance]
        [Category("Performance")]
        public void GetSingleton([Values] AccessType accessType)
        {
            float expectedValue = m_Manager.GetComponentData<EcsTestFloatData>(m_Entity).Value;
            Measure.Method(() =>
            {
                TestSystem.GetSingletonTest(accessType, expectedValue);
            }).WarmupCount(5).MeasurementCount(100).SampleGroup("SingletonAccess").Run();
        }

        [Test, Performance]
        [Category("Performance")]
        public void GetSingletonRW([Values] AccessType accessType)
        {
            float expectedValue = m_Manager.GetComponentData<EcsTestFloatData>(m_Entity).Value;
            Measure.Method(() =>
            {
                TestSystem.GetSingletonRWTest(accessType, expectedValue);
            }).WarmupCount(5).MeasurementCount(100).SampleGroup("SingletonAccess").Run();
        }

        [Test, Performance]
        [Category("Performance")]
        public void GetSingletonBuffer([Values] AccessType accessType)
        {
            int expectedLength = m_Manager.GetBuffer<EcsIntElement>(m_Entity).Length;
            Measure.Method(() =>
            {
                TestSystem.GetSingletonBufferTest(accessType, expectedLength);
            }).WarmupCount(5).MeasurementCount(100).SampleGroup("SingletonBufferAccess").Run();
        }

        [Test, Performance]
        [Category("Performance")]
        public void GetSingletonEntity([Values] AccessType accessType)
        {
            int expectedVersion = m_Entity.Version;
            Measure.Method(() =>
            {
                TestSystem.GetSingletonEntityTest(accessType, expectedVersion);
            }).WarmupCount(5).MeasurementCount(100).SampleGroup("SingletonAccess").Run();
        }

        [Test, Performance]
        [Category("Performance")]
        public void HasSingleton()
        {
            Measure.Method(() =>
            {
                TestSystem.HasSingletonTest();
            }).WarmupCount(5).MeasurementCount(100).SampleGroup("SingletonAccess").Run();
        }

        [Test, Performance]
        [Category("Performance")]
        public void SetSingleton([Values] AccessType accessType)
        {
            Measure.Method(() =>
            {
                TestSystem.SetSingletonTest(accessType);
            }).WarmupCount(5).MeasurementCount(100).SampleGroup("SingletonAccess").Run();
        }
    }
}
