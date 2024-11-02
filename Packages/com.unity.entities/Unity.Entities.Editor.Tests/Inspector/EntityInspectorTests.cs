#pragma warning disable 649
using System;
using System.Collections;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Serialization.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Entities.Editor.Tests
{
    using EntityInspectorTypes;

    namespace EntityInspectorTypes
    {
        class InspectorTestWindow : EditorWindow
        {
            public void AddRoot(VisualElement element)
            {
                rootVisualElement.Add(element);
            }
        }

        struct BufferComponent : IBufferElementData
        {
            public float Value;
        }

        struct TagComponent : IComponentData
        {
        }

        struct StructComponent : IComponentData
        {
            public float Value;
        }

        struct SharedStructComponent : ISharedComponentData
        {
            public float Value;
        }

        struct EnableableStructComponent : IComponentData, IEnableableComponent
        {
            public float Value;
        }

        struct EnableableBufferComponent : IBufferElementData, IEnableableComponent
        {
            public float Value;
        }

        struct EnableableTagComponent : IComponentData, IEnableableComponent
        {
        }

        [Flags] enum Int8Enum : sbyte { None = 0, Value = 1 << 4 }
        [Flags] enum UInt8Enum : byte { None = 0, Value = 1 << 4 }
        [Flags] enum Int16Enum : short { None = 0, Value = 1 << 12 }
        [Flags] enum UInt16Enum : ushort { None = 0, Value = 1 << 12 }
        [Flags] enum Int32Enum : int { None = 0, Value = 1 << 28 }
        [Flags] enum UInt32Enum : uint { None = 0, Value = 1 << 28 }
        [Flags] enum Int64Enum : long { None = 0, Value = 1L << 60 }
        [Flags] enum UInt64Enum : ulong { None = 0, Value = 1UL << 60 }

        struct ComponentWithEnumFields : IComponentData
        {
            public Int8Enum Int8Enum;
            public UInt8Enum UInt8Enum;
            public Int16Enum Int16Enum;
            public UInt16Enum UInt16Enum;
            public Int32Enum Int32Enum;
            public UInt32Enum UInt32Enum;
            public Int64Enum Int64Enum;
            public UInt64Enum UInt64Enum;
        }

        struct RuntimeBarTestComponent : IComponentData
        {
            public float FloatValue;
            public float3 Float3Value;
            public Hash128 Hash128Value;
            public FixedString32Bytes FixedString32BytesValue;
        }

#if  !UNITY_DISABLE_MANAGED_COMPONENTS
        class ClassComponent : IComponentData
        {
            public float Value;
        }

        class EnableableClassComponent : IComponentData, IEnableableComponent
        {
            public float Value;
        }
#endif
    }

    [TestFixture]
    class EntityInspectorTests
    {
        World m_World;
        Entity m_Entity;
        EntityEditor m_Editor;
        EntitySelectionProxy m_Proxy;

        [SetUp]
        public void SetUp()
        {
            if (m_World == null || !m_World.IsCreated)
                m_World = new World("Entity Inspector tests");

            m_Entity = m_World.EntityManager.CreateEntity();
            m_Proxy = EntitySelectionProxy.CreateInstance(m_World, m_Entity);
            var editor = UnityEditor.Editor.CreateEditor(m_Proxy);
            Assert.That(editor, Is.TypeOf<EntityEditor>());
            m_Editor = (EntityEditor)editor;
            m_Editor.ImmediateInitialize = true;
            SessionState<TabViewDrawer.State>.GetOrCreate("EntityInspector").TabIndex = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Proxy);
            Object.DestroyImmediate(m_Editor);

            if (null != m_World && m_World.IsCreated)
                m_World.Dispose();
        }

        void EntityInspectorScope(Action<VisualElement> action)
        {
            var root = m_Editor.CreateInspectorGUI();
            Assert.That(root, Is.Not.Null);

            var window = ScriptableObject.CreateInstance<InspectorTestWindow>();
            Assert.That(window, Is.Not.Null);

            try
            {
                window.Show();
                window.AddRoot(root);
                action(root);
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void Entity_WithNoComponents_HasEmptyInspector()
        {
            EntityInspectorScope((root) =>
            {
                var list = root.Query<ComponentElementBase>().ToList();
                Assert.That(list.Count, Is.EqualTo(1)); // +1 for Simulate
            });
        }

        [Test]
        public void Entity_WithComponents_HasMatchingComponentElements()
        {
            m_World.EntityManager.AddComponent<TagComponent>(m_Entity);
            m_World.EntityManager.AddComponent<StructComponent>(m_Entity);
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            m_World.EntityManager.AddComponent<ClassComponent>(m_Entity);
#endif
            m_World.EntityManager.AddSharedComponentManaged(m_Entity, new SharedStructComponent());

            EntityInspectorScope((root) =>
            {
                var query = root.Query<ComponentElementBase>();
                var list = query.ToList();
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                Assert.That(list.Count, Is.EqualTo(5)); // +1 for Simulate
#else
            Assert.That(list.Count, Is.EqualTo(4)); // +1 for Simulate
#endif
                Assert.That(list.OfType<TagElement<Simulate>>().Count(), Is.EqualTo(1));
                Assert.That(list.OfType<TagElement<TagComponent>>().Count(), Is.EqualTo(1));
                Assert.That(list.OfType<ComponentElement<StructComponent>>().Count(), Is.EqualTo(1));
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                Assert.That(list.OfType<ComponentElement<ClassComponent>>().Count(), Is.EqualTo(1));
#endif
                Assert.That(list.OfType<ComponentElement<SharedStructComponent>>().Count(), Is.EqualTo(1));
                Is.EqualTo(1);
            });
        }

        [Test]
        public void Inspector_WhenComponentsAreAddedOrRemoved_UpdatesProperly()
        {
            EntityInspectorScope((root) =>
            {
                var list = new List<ComponentElementBase>();
                root.Query<ComponentElementBase>().ToList(list);
                Assert.That(list.Count, Is.EqualTo(1)); // +1 for Simulate
                Assert.That(list.OfType<TagElement<Simulate>>().Count(), Is.EqualTo(1));
                Assert.That(list.OfType<TagElement<TagComponent>>().Count(), Is.Zero);
                Assert.That(list.OfType<ComponentElement<StructComponent>>().Count(), Is.Zero);
                list.Clear();

                m_World.EntityManager.AddComponent<TagComponent>(m_Entity);
                root.ForceUpdateBindings();
                root.Query<ComponentElementBase>().ToList(list);
                Assert.That(list.Count, Is.EqualTo(2)); // +1 for Simulate
                Assert.That(list.OfType<TagElement<Simulate>>().Count(), Is.EqualTo(1));
                Assert.That(list.OfType<TagElement<TagComponent>>().Count(), Is.EqualTo(1));
                Assert.That(list.OfType<ComponentElement<StructComponent>>().Count(), Is.Zero);
                list.Clear();

                m_World.EntityManager.AddComponent<StructComponent>(m_Entity);
                root.ForceUpdateBindings();
                root.Query<ComponentElementBase>().ToList(list);
                Assert.That(list.Count, Is.EqualTo(3)); // +1 for Simulate
                Assert.That(list.OfType<TagElement<Simulate>>().Count(), Is.EqualTo(1));
                Assert.That(list.OfType<TagElement<TagComponent>>().Count(), Is.EqualTo(1));
                Assert.That(list.OfType<ComponentElement<StructComponent>>().Count(), Is.EqualTo(1));
                list.Clear();

                m_World.EntityManager.RemoveComponent<TagComponent>(m_Entity);
                root.ForceUpdateBindings();
                root.Query<ComponentElementBase>().ToList(list);
                Assert.That(list.Count, Is.EqualTo(2)); // +1 for Simulate
                Assert.That(list.OfType<TagElement<Simulate>>().Count(), Is.EqualTo(1));
                Assert.That(list.OfType<TagElement<TagComponent>>().Count(), Is.Zero);
                Assert.That(list.OfType<ComponentElement<StructComponent>>().Count(), Is.EqualTo(1));
                list.Clear();

                m_World.EntityManager.RemoveComponent<StructComponent>(m_Entity);
                root.ForceUpdateBindings();
                root.Query<ComponentElementBase>().ToList(list);
                Assert.That(list.Count, Is.EqualTo(1)); // +1 for Simulate
                Assert.That(list.OfType<TagElement<Simulate>>().Count(), Is.EqualTo(1));
                Assert.That(list.OfType<TagElement<TagComponent>>().Count(), Is.Zero);
                Assert.That(list.OfType<ComponentElement<StructComponent>>().Count(), Is.Zero);
                list.Clear();
            });
        }

        [Test]
        public void Inspector_WhenComponentsAreEnabledOrDisabled_UpdatesProperly()
        {
            m_World.EntityManager.AddComponent<EnableableStructComponent>(m_Entity);
            m_World.EntityManager.AddComponent<EnableableBufferComponent>(m_Entity);
            m_World.EntityManager.AddComponent<EnableableTagComponent>(m_Entity);
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            m_World.EntityManager.AddComponent<EnableableClassComponent>(m_Entity);
#endif

            EntityInspectorScope((root) =>
            {
                { // struct component
                    var element = root.Q<ComponentElement<EnableableStructComponent>>();
                    var enabled = element.Q<Toggle>(className: UssClasses.Inspector.Component.Enabled);
                    var isEnabled = m_World.EntityManager.IsComponentEnabled<EnableableStructComponent>(m_Entity);
                    Assert.That(enabled.value, Is.EqualTo(isEnabled));

                    isEnabled = !isEnabled;
                    m_World.EntityManager.SetComponentEnabled<EnableableStructComponent>(m_Entity, isEnabled);
                    root.ForceUpdateBindings();
                    Assert.That(enabled.value, Is.EqualTo(isEnabled));

                    isEnabled = !isEnabled;
                    enabled.value = isEnabled;
                    Assert.That(m_World.EntityManager.IsComponentEnabled<EnableableStructComponent>(m_Entity), Is.EqualTo(isEnabled));
                }
                { // buffer component
                    var element = root.Q<BufferElement<DynamicBufferContainer<EnableableBufferComponent>, EnableableBufferComponent>>();
                    var enabled = element.Q<Toggle>(className: UssClasses.Inspector.Component.Enabled);
                    var isEnabled = m_World.EntityManager.IsComponentEnabled<EnableableBufferComponent>(m_Entity);
                    Assert.That(enabled.value, Is.EqualTo(isEnabled));

                    isEnabled = !isEnabled;
                    m_World.EntityManager.SetComponentEnabled<EnableableBufferComponent>(m_Entity, isEnabled);
                    root.ForceUpdateBindings();
                    Assert.That(enabled.value, Is.EqualTo(isEnabled));

                    isEnabled = !isEnabled;
                    enabled.value = isEnabled;
                    Assert.That(m_World.EntityManager.IsComponentEnabled<EnableableBufferComponent>(m_Entity), Is.EqualTo(isEnabled));
                }
                { // tag component
                    var element = root.Q<TagElement<EnableableTagComponent>>();
                    var enabled = element.Q<Toggle>(className: UssClasses.Inspector.Component.Enabled);
                    var isEnabled = m_World.EntityManager.IsComponentEnabled<EnableableTagComponent>(m_Entity);
                    Assert.That(enabled.value, Is.EqualTo(isEnabled));

                    isEnabled = !isEnabled;
                    m_World.EntityManager.SetComponentEnabled<EnableableTagComponent>(m_Entity, isEnabled);
                    root.ForceUpdateBindings();
                    Assert.That(enabled.value, Is.EqualTo(isEnabled));

                    isEnabled = !isEnabled;
                    enabled.value = isEnabled;
                    Assert.That(m_World.EntityManager.IsComponentEnabled<EnableableTagComponent>(m_Entity), Is.EqualTo(isEnabled));
                }
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                { // class component
                    var element = root.Q<ComponentElement<EnableableClassComponent>>();
                    var enabled = element.Q<Toggle>(className: UssClasses.Inspector.Component.Enabled);
                    var isEnabled = m_World.EntityManager.IsComponentEnabled<EnableableClassComponent>(m_Entity);
                    Assert.That(enabled.value, Is.EqualTo(isEnabled));

                    isEnabled = !isEnabled;
                    m_World.EntityManager.SetComponentEnabled<EnableableClassComponent>(m_Entity, isEnabled);
                    root.ForceUpdateBindings();
                    Assert.That(enabled.value, Is.EqualTo(isEnabled));

                    isEnabled = !isEnabled;
                    enabled.value = isEnabled;
                    Assert.That(m_World.EntityManager.IsComponentEnabled<EnableableClassComponent>(m_Entity), Is.EqualTo(isEnabled));
                }
#endif
            });
        }

        [Test]
        public void Inspector_WhenComponentsValueIsChanged_UpdatesProperly()
        {
            m_World.EntityManager.AddComponent<StructComponent>(m_Entity);

            EntityInspectorScope((root) =>
            {
                var element = root.Q<ComponentElement<StructComponent>>();
                var field = element.Q<FloatField>();
                Assert.That(field.value, Is.EqualTo(0.0f));

                m_World.EntityManager.SetComponentData(m_Entity, new StructComponent { Value = 15.0f });
                root.ForceUpdateBindings();
                Assert.That(field.value, Is.EqualTo(15.0f));

                field.value = 100.0f;
                var data = m_World.EntityManager.GetComponentData<StructComponent>(m_Entity);
                root.ForceUpdateBindings();
                if (m_Editor.m_InspectorContext.IsReadOnly)
                {
                    Assert.That(data.Value, Is.EqualTo(15.0f));
                    Assert.That(field.value, Is.EqualTo(15.0f));
                }
                else
                {
                    Assert.That(data.Value, Is.EqualTo(100.0f));
                    Assert.That(field.value, Is.EqualTo(100.0f));
                }
            });
        }

        [Test]
        public void Entity_WithBuffers_CanNavigateThroughItems()
        {
            var buffer = m_World.EntityManager.AddBuffer<BufferComponent>(m_Entity);
            for (var i = 0; i < 10; ++i)
                buffer.Add(new BufferComponent {Value = i});

            EntityInspectorScope((root) =>
            {
                var query = root.Query<ComponentElementBase>();
                var list = query.ToList();
                Assert.That(list.Count, Is.EqualTo(2)); // +1 for Simulate
                Assert.That(root.Q(className: "unity-platforms__list-element__size").enabledInHierarchy, Is.False);
                Assert.That(root.Q("platforms-list-content").enabledInHierarchy, Is.False);
                Assert.That(root.Q(className: "unity-platforms__list-element__add-item-button").enabledInHierarchy, Is.False);

                Assert.That(root.Q(className: "unity-platforms__pagination-element__pagination-size").enabledInHierarchy, Is.True);
                // When inspecting a buffer, on the first page, the previous button is disabled, but the parent will be enabled.
                Assert.That(root.Q(className: "unity-platforms__pagination-element__previous-page-button").enabledInHierarchy, Is.False);
                Assert.That(root.Q(className: "unity-platforms__pagination-element__previous-page-button").parent.enabledInHierarchy, Is.True);
                Assert.That(root.Q(className: "unity-platforms__pagination-element__elements-range").enabledInHierarchy, Is.True);
                Assert.That(root.Q(className: "unity-platforms__pagination-element__next-page-button").enabledInHierarchy, Is.True);
            });
        }

        [Test]
        public void Inspector_RuntimeBar_NotInEditMode()
        {
            m_World.EntityManager.AddComponent<StructComponent>(m_Entity);

            EntityInspectorScope((root) =>
            {
                var element = root.Q<ComponentElement<StructComponent>>();
                var field = element.Q<FloatField>();
                var runtimeBar = field.Q(className: "unity-platforms__runtime-bar");
                Assert.That(runtimeBar, Is.Null);
            });
        }

        [Test]
        public void Component_WithEnumFields()
        {
            m_World.EntityManager.AddComponentData(m_Entity, new ComponentWithEnumFields
            {
                Int8Enum = Int8Enum.Value,
                UInt8Enum = UInt8Enum.Value,
                Int16Enum = Int16Enum.Value,
                UInt16Enum = UInt16Enum.Value,
                Int32Enum = Int32Enum.Value,
                UInt32Enum = UInt32Enum.Value,
                Int64Enum = Int64Enum.Value,
                UInt64Enum = UInt64Enum.Value
            });

            EntityInspectorScope((root) =>
            {
                Assert.That(root.Q<ComponentElement<ComponentWithEnumFields>>(), Is.Not.Null);
            });
        }

        [UnityTest]
        public IEnumerator Inspector_RuntimeBar_OnlyInPlayMode()
        {
            yield return new EnterPlayMode();

            var playModeTestWorld = new World("Entity Inspector Playmode tests");
            var entity = playModeTestWorld.EntityManager.CreateEntity();
            playModeTestWorld.EntityManager.AddComponent<RuntimeBarTestComponent>(entity);
            playModeTestWorld.EntityManager.AddComponent<EntityGuid>(entity);
            var proxy = EntitySelectionProxy.CreateInstance(playModeTestWorld, entity);
            var editor = UnityEditor.Editor.CreateEditor(proxy);
            var entityEditor = (EntityEditor)editor;
            entityEditor.ImmediateInitialize = true;

            var root = entityEditor.CreateInspectorGUI();
            var window = ScriptableObject.CreateInstance<InspectorTestWindow>();
            window.Show();
            window.AddRoot(root);

            try
            {
                var runtimeBarTestElement = root.Q<ComponentElement<RuntimeBarTestComponent>>();

                // float
                var floatField = runtimeBarTestElement.Q<FloatField>();
                var floatFieldRuntimeBar = floatField.Q(className: "unity-platforms__runtime-bar");
                Assert.That(floatFieldRuntimeBar, Is.Not.Null);
                Assert.That(floatFieldRuntimeBar.style.display.value, Is.EqualTo(DisplayStyle.Flex));

                // float3
                var float3Field = runtimeBarTestElement.Q<Vector3Field>();
                var float3FieldRuntimeBar = float3Field.Q(className: "entity-inspector__runtime-bar");
                Assert.That(float3FieldRuntimeBar, Is.Not.Null);
                Assert.That(float3FieldRuntimeBar.style.display.value, Is.EqualTo(DisplayStyle.Flex));

                // Hash128 + FixedString32Bytes
                var textFields = runtimeBarTestElement.Query<TextField>().ToList();
                Assert.That(textFields.Count, Is.EqualTo(2));
                foreach (var tf in textFields)
                {
                    var textFieldsRuntimeBar = tf.Q(className: "entity-inspector__runtime-bar");
                    Assert.That(textFieldsRuntimeBar, Is.Not.Null);
                    Assert.That(textFieldsRuntimeBar.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                }

                // Entity GUID
                var entityGuidElement = root.Q<ComponentElement<EntityGuid>>();
                var entityGuidTextField = entityGuidElement.Q<TextField>();
                var entityGuidRuntimeBar = entityGuidTextField.Q(className: "entity-inspector__runtime-bar");
                Assert.That(entityGuidRuntimeBar, Is.Not.Null);
                Assert.That(entityGuidRuntimeBar.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            }
            finally
            {
                window.Close();
                playModeTestWorld?.Dispose();
            }

            yield return new ExitPlayMode();
        }
    }
}
#pragma warning restore 649
