#pragma warning disable 649
using System.Linq;
using NUnit.Framework;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Serialization.Editor;

namespace Unity.Entities.Editor.Tests
{
    struct AspectTestComponentData : IComponentData
    {
        public int intField;
    }

    readonly partial struct TestAspect : IAspect
    {
        public readonly RefRW<AspectTestComponentData> m_Data;

        [CreateProperty]
        public int IntField
        {
            get => m_Data.ValueRW.intField;
            set
            {
                if (m_Data.ValueRW.intField.Equals(value))
                    return;

                m_Data.ValueRW.intField = value;
            }
        }

        public static Entity CreateEntity(EntityManager manager)
        {
            return manager.CreateEntity(typeof(AspectTestComponentData));
        }

        public void SetValue(int intValue)
        {
            IntField = intValue;
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    partial class AspectSetComponentSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref AspectTestComponentData testComponentData) =>
            {
                testComponentData.intField = 66;
            }).Run();
        }

        protected override void OnCreate()
        {
        }

        protected override void OnDestroy()
        {
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    partial class AspectSetAspectSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((TestAspect testAspect) =>
            {
                testAspect.SetValue(88);
            }).Run();
        }

        protected override void OnCreate()
        {
        }

        protected override void OnDestroy()
        {
        }
    }

    [TestFixture]
    class AspectInspectorTests
    {
        World m_World;
        Entity m_Entity;
        EntityEditor m_Editor;
        EntitySelectionProxy m_Proxy;
        VisualElement m_Root;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_World = new World("Aspects Tab Tests");
            var testSystem = m_World.GetOrCreateSystemManaged<AspectSetComponentSystem>();
            m_World.GetOrCreateSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(testSystem);

            TypeManager.InitializeAspects(ignoreTestAspects:false);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            m_World.Dispose();
            TypeManager.InitializeAspects(ignoreTestAspects:true);
        }

        [SetUp]
        public void SetUp()
        {
            m_Entity = TestAspect.CreateEntity(m_World.EntityManager);
            m_Proxy = EntitySelectionProxy.CreateInstance(m_World, m_Entity);
            var editor = UnityEditor.Editor.CreateEditor(m_Proxy);
            Assert.That(editor, Is.TypeOf<EntityEditor>());
            m_Editor = (EntityEditor)editor;
            m_Editor.ImmediateInitialize = true;
            SessionState<TabViewDrawer.State>.GetOrCreate("EntityInspector").TabIndex = 1;
        }

        [TearDown]
        public void TearDown()
        {
            SessionState<TabViewDrawer.State>.GetOrCreate("EntityInspector").TabIndex = 0;

            Object.DestroyImmediate(m_Proxy);
            Object.DestroyImmediate(m_Editor);
            m_World.EntityManager.DestroyEntity(m_Entity);
        }

        [Test]
        public void AspectsTab_TabBuiltProperly()
        {
            m_Root = m_Editor.CreateInspectorGUI();
            var content = m_Root.Query<TabContent>().ToList();
            Assert.That(content.Count, Is.EqualTo(3));
            Assert.That(content[1].TabName, Is.EqualTo("Aspects"));

            var aspectElements = m_Root.Query<AspectElementBase>().ToList();
            Assert.That(aspectElements.Count, Is.EqualTo(1));

            var searchElement = m_Root.Q<SearchElement>(className: "entity-inspector-aspects-tab__search-field");
            Assert.That(searchElement, Is.Not.Null);

            var noResultLabel = m_Root.Q<Label>(className: "empty-inspector-label");
            Assert.That(noResultLabel, Is.Not.Null);
            Assert.That(noResultLabel.text, Is.EqualTo("No aspects."));
            Assert.That(noResultLabel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void AspectTab_AspectShowCorrectComponentValue_SetByComponentQuery()
        {
            var testSystem = m_World.GetOrCreateSystemManaged<AspectSetComponentSystem>();
            testSystem.Update();

            m_Root = m_Editor.CreateInspectorGUI();
            var aspectElements = m_Root.Query<AspectElementBase>().ToList();
            Assert.That(aspectElements.Count, Is.GreaterThan(0));
            var integerField = aspectElements[0].Q<IntegerField>();
            Assert.That(integerField.value, Is.EqualTo(66));
        }

        [Test]
        public void AspectTab_AspectShowCorrectComponentValue_SetByAspectQuery()
        {
            var testSystem = m_World.GetOrCreateSystemManaged<AspectSetAspectSystem>();
            testSystem.Update();

            m_Root = m_Editor.CreateInspectorGUI();
            var aspectElements = m_Root.Query<AspectElementBase>().ToList();
            Assert.That(aspectElements.Count, Is.GreaterThan(0));
            var integerField = aspectElements[0].Q<IntegerField>();
            Assert.That(integerField.value, Is.EqualTo(88));
        }
    }
}
#pragma warning restore 649
