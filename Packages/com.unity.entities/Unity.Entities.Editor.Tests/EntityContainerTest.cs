using System;
using NUnit.Framework;
using Unity.Entities.Tests;
using Unity.Properties;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    class EntityContainerTest : ECSTestsFixture
    {
        [Flags]
        internal enum Category
        {
            None = 0,
            StructData = 1,
            ClassData = 2,
            StructChunkData = 4,
            ClassChunkData = 8,
            BufferData = 16,
            ManagedData = 32,
            SharedData = 64
        }

        struct StructComponentData : IComponentData
        {
            public Category Category;
            public float FloatValue;
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        internal class ClassComponentData : IComponentData
        {
            public Category Category;
            public float FloatValue;
        }

        internal class ClassComponentData2 : IComponentData
        {
            public Category Category;
            public float FloatValue;
        }
#endif

        struct StructChunkData : IComponentData
        {
            public int Value;
            public Category Category;
            public float FloatValue;
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        internal class ClassChunkData : IComponentData
        {
            public Category Category;
            public GameObject GameObject;
            public float FloatValue;
        }
#endif

        internal struct SharedComponentData : ISharedComponentData
        {
            public Category Category;
            public float FloatValue;
        }

        struct BufferElement : IBufferElementData
        {
            public Category Category;
            public int Value;
            public float FloatValue;
        }

        class TestComponentCategoryVisitor : PropertyVisitor,
            IVisitPropertyAdapter<StructComponentData>,
            IVisitPropertyAdapter<StructChunkData>,
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            IVisitPropertyAdapter<ClassComponentData>,
            IVisitPropertyAdapter<ClassComponentData2>,
            IVisitPropertyAdapter<ClassChunkData>,
#endif
            IVisitPropertyAdapter<SharedComponentData>,
            IVisitPropertyAdapter<BufferElement>,
            IVisitPropertyAdapter<DynamicBufferContainer<BufferElement>>,
            IVisitPropertyAdapter<Transform>,
            IVisitPropertyAdapter<GameObject>
        {
            public GameObject GameObject { private get; set; }

            public TestComponentCategoryVisitor()
            {
                AddAdapter(this);
            }

            public void Visit<TContainer>(in VisitContext<TContainer, StructComponentData> context, ref TContainer container, ref StructComponentData data)
            {
                Assert.That(data.Category, Is.EqualTo(Category.StructData));
            }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
            public void Visit<TContainer>(in VisitContext<TContainer, ClassComponentData> context, ref TContainer container, ref ClassComponentData data)
            {
                Assert.That(data.Category, Is.EqualTo(Category.ClassData));
            }

            public void Visit<TContainer>(in VisitContext<TContainer, ClassComponentData2> context, ref TContainer container, ref ClassComponentData2 data)
            {
                Assert.That(data.Category, Is.EqualTo(Category.ClassData));
            }

            public void Visit<TContainer>(in VisitContext<TContainer, ClassChunkData> context, ref TContainer container, ref ClassChunkData data)
            {
                Assert.That(data.Category, Is.EqualTo(Category.ClassChunkData));
                Assert.That(data.GameObject, Is.EqualTo(GameObject));
            }
#endif

            public void Visit<TContainer>(in VisitContext<TContainer, SharedComponentData> context, ref TContainer container, ref SharedComponentData data)
            {
                Assert.That(data.Category, Is.EqualTo(Category.SharedData));
            }

            public void Visit<TContainer>(in VisitContext<TContainer, Transform> context, ref TContainer container, ref Transform transform)
            {
                Assert.That(transform.localPosition, Is.EqualTo(Vector3.right));
                Assert.That(transform.localScale, Is.EqualTo(Vector3.up));
                Assert.That(transform.localRotation, Is.EqualTo(Quaternion.Euler(15, 30, 45)));
            }

            public void Visit<TContainer>(in VisitContext<TContainer, StructChunkData> context, ref TContainer container, ref StructChunkData data)
            {
                Assert.That(data.Value, Is.EqualTo(25));
                Assert.That(data.Category, Is.EqualTo(Category.StructChunkData));
            }

            public void Visit<TContainer>(in VisitContext<TContainer, DynamicBufferContainer<BufferElement>> context, ref TContainer container, ref DynamicBufferContainer<BufferElement> data)
            {
                for (var i = 0; i < data.Count; ++i)
                    Assert.That(data[i].Value, Is.EqualTo(i));
            }

            public void Visit<TContainer>(in VisitContext<TContainer, BufferElement> context, ref TContainer container, ref BufferElement data)
            {
                Assert.That(data.Category, Is.EqualTo(Category.BufferData));
            }

            public void Visit<TContainer>(in VisitContext<TContainer, GameObject> context, ref TContainer container, ref GameObject value)
            {
                Assert.That(value, Is.EqualTo(GameObject));
            }
        }

        class TestDataWriteBackVisitor : PropertyVisitor,
            IVisitPropertyAdapter<StructComponentData>,
            IVisitPropertyAdapter<StructChunkData>,
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            IVisitPropertyAdapter<ClassComponentData>,
            IVisitPropertyAdapter<ClassComponentData2>,
            IVisitPropertyAdapter<ClassChunkData>,
#endif
            IVisitPropertyAdapter<SharedComponentData>,
            IVisitPropertyAdapter<BufferElement>,
            IVisitPropertyAdapter<DynamicBufferContainer<BufferElement>>
        {
            public GameObject GameObject { private get; set; }
            public bool Read { get; set; }
            public float Value { get; }
            public Category Category { get; }

            public TestDataWriteBackVisitor(bool read, float value, Category category)
            {
                Read = read;
                Value = value;
                Category = category;
                AddAdapter(this);
            }

            public void Visit<TContainer>(in VisitContext<TContainer, StructComponentData> context, ref TContainer container, ref StructComponentData data)
            {
                if (!Category.HasFlag(Category.StructData))
                    return;

                if (Read)
                    Assert.That(data.FloatValue, Is.EqualTo(Value));
                else
                    data.FloatValue = Value;
            }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
            public void Visit<TContainer>(in VisitContext<TContainer, ClassComponentData> context, ref TContainer container, ref ClassComponentData data)
            {
                if (!Category.HasFlag(Category.ClassData))
                    return;

                if (Read)
                    Assert.That(data.FloatValue, Is.EqualTo(Value));
                else
                    data.FloatValue = Value;
            }

            public void Visit<TContainer>(in VisitContext<TContainer, ClassComponentData2> context, ref TContainer container, ref ClassComponentData2 data)
            {
                if (!Category.HasFlag(Category.ClassData))
                    return;

                if (Read)
                    Assert.That(data.FloatValue, Is.EqualTo(Value));
                else
                    data.FloatValue = Value;
            }

            public void Visit<TContainer>(in VisitContext<TContainer, ClassChunkData> context, ref TContainer container, ref ClassChunkData data)
            {
                if (!Category.HasFlag(Category.ClassChunkData))
                    return;

                if (Read)
                    Assert.That(data.FloatValue, Is.EqualTo(Value));
                else
                    data.FloatValue = Value;
            }
#endif

            public void Visit<TContainer>(in VisitContext<TContainer, SharedComponentData> context, ref TContainer container, ref SharedComponentData data)
            {
                if (!Category.HasFlag(Category.SharedData))
                    return;

                if (Read)
                    Assert.That(data.FloatValue, Is.EqualTo(Value));
                else
                    data.FloatValue = Value;
            }

            public void Visit<TContainer>(in VisitContext<TContainer, StructChunkData> context, ref TContainer container, ref StructChunkData data)
            {
                if (!Category.HasFlag(Category.StructChunkData))
                    return;

                if (Read)
                    Assert.That(data.FloatValue, Is.EqualTo(Value));
                else
                    data.FloatValue = Value;
            }

            public void Visit<TContainer>(in VisitContext<TContainer, DynamicBufferContainer<BufferElement>> context, ref TContainer container, ref DynamicBufferContainer<BufferElement> data)
            {
                if (!Category.HasFlag(Category.BufferData))
                    return;

                if (Read)
                    Assert.That(data.Count, Is.EqualTo(51));
                else
                    data.Add( new BufferElement{ FloatValue = data.Count * Value });

                context.ContinueVisitation(ref container, ref data);
            }

            public void Visit<TContainer>(in VisitContext<TContainer, BufferElement> context, ref TContainer container, ref BufferElement data)
            {
                if (!Category.HasFlag(Category.BufferData))
                    return;
                if (Read)
                    Assert.That(data.FloatValue, Is.EqualTo(Value * (context.Property as IListElementProperty).Index));
                else
                    data.FloatValue = Value * (context.Property as IListElementProperty).Index;
            }
        }

        class TestDynamicBufferContainerVisitor : PropertyVisitor,
            IVisitPropertyAdapter<DynamicBufferContainer<BufferElement>>
        {
            public TestDynamicBufferContainerVisitor()
                => AddAdapter(this);

            internal DynamicBufferContainer<BufferElement> Container;

            public void Visit<TContainer>(in VisitContext<TContainer, DynamicBufferContainer<BufferElement>> context, ref TContainer container, ref DynamicBufferContainer<BufferElement> value)
            {
                Container = value;
            }
        }

        class InvalidEntityVisitor : PropertyVisitor
        {
            protected override void VisitProperty<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container, ref TValue value)
            {
                throw new InvalidOperationException();
            }
        }

        GameObject _gameObject;

        public override void Setup()
        {
            base.Setup();
            _gameObject = new GameObject();
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
            base.TearDown();
        }

        [Test]
        public void EntityContainer_WhenVisited_ReturnsTheCorrectValuesForAllComponentTypes()
        {
            var entity = m_Manager.CreateEntity(typeof(StructComponentData),
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                typeof(ClassComponentData),
#endif
                typeof(SharedComponentData));

            m_Manager.AddChunkComponentData<StructChunkData>(entity);
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            m_Manager.AddChunkComponentData<ClassChunkData>(entity);
#endif

            _gameObject.transform.localPosition = Vector3.right;
            _gameObject.transform.localScale = Vector3.up;
            _gameObject.transform.localRotation = Quaternion.Euler(15, 30, 45);

            m_Manager.AddComponentObject(entity, _gameObject.transform);
            m_Manager.AddComponentObject(entity, _gameObject);
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            m_Manager.AddComponentObject(entity, new ClassComponentData2 { Category = Category.ClassData });
#endif

            var buffer = m_Manager.AddBuffer<BufferElement>(entity);
            for (var i = 0; i < 50; ++i)
                buffer.Add(new BufferElement {Category = Category.BufferData, Value = i});

            m_Manager.SetSharedComponentManaged(entity, new SharedComponentData { Category = Category.SharedData });
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(entity), new StructChunkData { Value = 25, Category = Category.StructChunkData });
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(entity), new ClassChunkData { GameObject = _gameObject, Category = Category.ClassChunkData });
            m_Manager.SetComponentData(entity, new ClassComponentData { Category = Category.ClassData });
#endif
            m_Manager.SetComponentData(entity, new StructComponentData { Category = Category.StructData });
            PropertyContainer.Accept(new TestComponentCategoryVisitor { GameObject = _gameObject}, new EntityContainer(m_Manager, entity, true));
        }

         [Test]
        public void EntityContainer_WhenVisited_CanReadAndWriteData()
        {
            var entity = m_Manager.CreateEntity(typeof(StructComponentData),
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                typeof(ClassComponentData),
#endif
                typeof(SharedComponentData));

            m_Manager.AddChunkComponentData<StructChunkData>(entity);
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            m_Manager.AddChunkComponentData<ClassChunkData>(entity);
#endif

#if !UNITY_DISABLE_MANAGED_COMPONENTS
            m_Manager.AddComponentObject(entity, new ClassComponentData2 { Category = Category.ClassData });
#endif

            var buffer = m_Manager.AddBuffer<BufferElement>(entity);
            for (var i = 0; i < 50; ++i)
                buffer.Add(new BufferElement {Category = Category.BufferData, FloatValue = i});

            m_Manager.SetSharedComponentManaged(entity, new SharedComponentData { Category = Category.SharedData });
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(entity), new StructChunkData { Value = 25, Category = Category.StructChunkData });
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            m_Manager.SetChunkComponentData(m_Manager.GetChunk(entity), new ClassChunkData { GameObject = _gameObject, Category = Category.ClassChunkData });
            m_Manager.SetComponentData(entity, new ClassComponentData { Category = Category.ClassData });
#endif
            m_Manager.SetComponentData(entity, new StructComponentData { Category = Category.StructData });

            PropertyContainer.Accept(new TestDataWriteBackVisitor(false, 25, Category.StructChunkData), new EntityContainer(m_Manager, entity, false));
            PropertyContainer.Accept(new TestDataWriteBackVisitor(true, 25, Category.StructChunkData), new EntityContainer(m_Manager, entity, false));
            PropertyContainer.Accept(new TestDataWriteBackVisitor(false, 25, Category.ClassChunkData), new EntityContainer(m_Manager, entity, false));
            PropertyContainer.Accept(new TestDataWriteBackVisitor(true, 25, Category.ClassChunkData), new EntityContainer(m_Manager, entity, false));
            PropertyContainer.Accept(new TestDataWriteBackVisitor(false, 25, Category.StructData), new EntityContainer(m_Manager, entity, false));
            PropertyContainer.Accept(new TestDataWriteBackVisitor(true, 25, Category.StructData), new EntityContainer(m_Manager, entity, false));
            PropertyContainer.Accept(new TestDataWriteBackVisitor(false, 25, Category.ClassData), new EntityContainer(m_Manager, entity, false));
            PropertyContainer.Accept(new TestDataWriteBackVisitor(true, 25, Category.ClassData), new EntityContainer(m_Manager, entity, false));
            PropertyContainer.Accept(new TestDataWriteBackVisitor(false, 25, Category.SharedData), new EntityContainer(m_Manager, entity, false));
            PropertyContainer.Accept(new TestDataWriteBackVisitor(true, 25, Category.SharedData), new EntityContainer(m_Manager, entity, false));
            PropertyContainer.Accept(new TestDataWriteBackVisitor(false, 25, Category.BufferData), new EntityContainer(m_Manager, entity, false));
            PropertyContainer.Accept(new TestDataWriteBackVisitor(true, 25, Category.BufferData), new EntityContainer(m_Manager, entity, false));
        }

        [Test]
        public void EntityContainer_WhenVisitingAnInvalidEntity_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => PropertyContainer.Accept(new InvalidEntityVisitor(), new EntityContainer(m_Manager, Entity.Null, false)));

            var entity = m_Manager.CreateEntity(typeof(StructComponentData), typeof(BufferElement));
            var container = new EntityContainer(m_Manager, entity, false);

            // Validate that we are actually visiting something.
            Assert.Throws<InvalidOperationException>(() => PropertyContainer.Accept(new InvalidEntityVisitor(), container));

            // We can find the correct path and this will not throw.
            Assert.That(PropertyContainer.IsPathValid(ref container, new PropertyPath($"{nameof(EntityContainerTest)}_{nameof(StructComponentData)}")), Is.True);
            Assert.That(PropertyContainer.IsPathValid(ref container, new PropertyPath($"{nameof(EntityContainerTest)}_{nameof(BufferElement)}")), Is.True);

            m_Manager.DestroyEntity(entity);

            Assert.DoesNotThrow(() => PropertyContainer.Accept(new InvalidEntityVisitor(), container));

            // We cannot find the correct path anymore and this will not throw.
            Assert.That(PropertyContainer.IsPathValid(ref container, new PropertyPath($"{nameof(EntityContainerTest)}_{nameof(StructComponentData)}")), Is.False);
            Assert.That(PropertyContainer.IsPathValid(ref container, new PropertyPath($"{nameof(EntityContainerTest)}_{nameof(BufferElement)}")), Is.False);
        }

        [Test]
        public void DynamicBufferContainer_WhenAccessCountOnStaleContainer_DoesNotThrow()
        {
            var entity = m_Manager.CreateEntity(typeof(StructComponentData), typeof(BufferElement));
            var buffer = m_Manager.AddBuffer<BufferElement>(entity);
            for (var i = 0; i < 50; ++i)
                buffer.Add(new BufferElement { Category = Category.BufferData, FloatValue = i });

            var visitor = new TestDynamicBufferContainerVisitor();
            PropertyContainer.Accept(visitor, new EntityContainer(m_Manager, entity, true));

            Assert.That(visitor.Container.Count, Is.EqualTo(50));

            m_Manager.RemoveComponent<BufferElement>(entity);

            Assert.DoesNotThrow(() => { var c = visitor.Container.Count; });
            Assert.That(visitor.Container.Count, Is.Zero);
        }
    }
}
