using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities.Editor.Tests;
using Unity.Entities.UniversalDelegates;
using Unity.PerformanceTesting;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Entities.Editor.Tests.Search;

namespace Unity.Entities.Editor.PerformanceTests.Search
{
    public abstract class HierarchySearchPerformanceTests : QuickSearchTests
    {
        public static readonly string s_SearchWorld = "SearchPerformanceTest";
        public const int s_EntitiesCount = 100000;
        public const int s_HierarchyCountAddon = 3;

        internal Hierarchy m_Hierarchy;
        internal World m_World;
        
        public void ValidateHierarchyCreation()
        {
            CreateHierarchy();
            var nodes = m_Hierarchy.GetNodes();
            Assert.AreEqual(s_EntitiesCount + s_HierarchyCountAddon, nodes.Count);
        }

        public void Search(EntitySearchTestCase tc)
        {
            var desc = HierarchySearchProvider.CreateHierarchyQueryDescriptor(tc.Query);
            Assert.IsTrue(string.IsNullOrEmpty(desc.parsingErrors), $"Query parsing errors: {desc.parsingErrors}");

            CreateHierarchy();

            var validateFilterCreation = HierarchySearchProvider.CreateHierarchyFilter(m_Hierarchy.HierarchySearch, desc, m_Hierarchy.Allocator);
            Assert.IsTrue(validateFilterCreation.IsValid);
            
            Measure.Method(() =>
            {
                m_Hierarchy.Update(true);
            }).SetUp(() =>
                {
                    var filter = HierarchySearchProvider.CreateHierarchyFilter(m_Hierarchy.HierarchySearch, desc, m_Hierarchy.Allocator);
                    m_Hierarchy.SetFilter(filter);
                })
                .SampleGroup($"RunSearchTest query: {tc.Query}")
                .WarmupCount(5)
                .MeasurementCount(10)
                .Run();
        }

        public void CreateWorld()
        {
            ClearWorld();
            m_World = new World(s_SearchWorld);
            m_World.UpdateAllocatorEnableBlockFree = true;
            PopulateWorld(m_World);
        }

        public void CreateHierarchy()
        {
            ClearHierarchy();
            m_Hierarchy = new Hierarchy(Allocator.Persistent, UnityEditor.DataMode.Runtime);
            m_Hierarchy.Configuration.UpdateMode = Hierarchy.UpdateModeType.Synchronous;
            m_Hierarchy.SetWorld(m_World);
            m_Hierarchy.Update(true);
        }

        public void ClearHierarchy()
        {
            m_Hierarchy?.Dispose();
            m_Hierarchy = null;
        }

        public void ClearWorld()
        {
            m_World?.Dispose();
            m_World = null;
        }

        protected abstract void PopulateWorld(World world);
    }

    public class NoSharedComponents : HierarchySearchPerformanceTests
    {
        static IEnumerable<EntitySearchTestCase> GetEntitySearchTestCase()
        {
            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=SceneSection", 0);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=Transform", s_EntitiesCount);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #SearchTestSharedComponent.intValue=0", 0);
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            CreateWorld();
        }


        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            ClearWorld();
        }

        [TearDown]
        public void TearDown()
        {
            ClearHierarchy();
        }

        [SetUp]
        public void Setup()
        {
            ClearHierarchy();
        }

        [Test]
        public void RunValidateHierarchyCreation()
        {
            ValidateHierarchyCreation();
        }

        [Test, Performance]
        public void RunSearch([ValueSource(nameof(GetEntitySearchTestCase))] EntitySearchTestCase tc)
        {
            Search(tc);
        }

        protected override void PopulateWorld(World world)
        {
            var manager = world.EntityManager;
            var baseArchetype = manager.CreateArchetype(typeof(Transform));
            manager.CreateEntity(baseArchetype, s_EntitiesCount);
        }
    }

    public class AllSharedComponents : HierarchySearchPerformanceTests
    {
        static IEnumerable<EntitySearchTestCase> GetEntitySearchTestCase()
        {
            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=SceneSection", 0);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=Transform", s_EntitiesCount);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #SearchTestSharedComponent.intValue=0", s_EntitiesCount);
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            CreateWorld();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            ClearWorld();
        }

        [TearDown]
        public void TearDown()
        {
            ClearHierarchy();
        }

        [SetUp]
        public void Setup()
        {
            ClearHierarchy();
        }

        [Test]
        public void RunValidateHierarchyCreation()
        {
            ValidateHierarchyCreation();
        }

        [Test, Performance]
        public void RunSearch([ValueSource(nameof(GetEntitySearchTestCase))] EntitySearchTestCase tc)
        {
            Search(tc);
        }

        protected override void PopulateWorld(World world)
        {
            var manager = world.EntityManager;
            var baseArchetype = manager.CreateArchetype(typeof(Transform), typeof(SearchTestSharedComponent));
            manager.CreateEntity(baseArchetype, s_EntitiesCount);
        }
    }

    public class HalfSharedComponents : HierarchySearchPerformanceTests
    {
        static IEnumerable<EntitySearchTestCase> GetEntitySearchTestCase()
        {
            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=SceneSection", 0);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} all=Transform", s_EntitiesCount);
            yield return new EntitySearchTestCase($"w={s_SearchWorld} #SearchTestSharedComponent.intValue=0", s_EntitiesCount / 2);
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            CreateWorld();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            ClearWorld();
        }

        [TearDown]
        public void TearDown()
        {
            ClearHierarchy();
        }

        [SetUp]
        public void Setup()
        {
            ClearHierarchy();
        }

        [Test]
        public void RunValidateHierarchyCreation()
        {
            ValidateHierarchyCreation();
        }

        [Test, Performance]
        public void RunSearch([ValueSource(nameof(GetEntitySearchTestCase))] EntitySearchTestCase tc)
        {
            Search(tc);
        }

        protected override void PopulateWorld(World world)
        {
            var manager = world.EntityManager;
            var baseArchetype = manager.CreateArchetype(typeof(Transform));
            manager.CreateEntity(baseArchetype, s_EntitiesCount / 2);

            var archetypeWithSharedComp = manager.CreateArchetype(typeof(Transform), typeof(SearchTestSharedComponent));
            manager.CreateEntity(archetypeWithSharedComp, s_EntitiesCount / 2);
        }
    }
}
