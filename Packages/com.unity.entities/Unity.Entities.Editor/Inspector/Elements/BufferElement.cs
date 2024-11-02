using System.Collections.Generic;
using Unity.Entities.UI;
using Unity.Properties;
using Unity.Scenes;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    sealed class BufferElement<TList, TElement> : ComponentElementBase, IBinding
        where TList : IList<TElement>
    {
        readonly PropertyElement m_Content;
        readonly Toggle m_Enabled;

        int m_Count;

        public BufferElement(IComponentProperty property, EntityInspectorContext context, ref InspectedBuffer<TList, TElement> value) : base(property, context)
        {
            binding = this;
            m_Content = CreateContent(property, ref value);
            m_Enabled = this.Q<Toggle>(className: UssClasses.Inspector.Component.Enabled);
            m_Count = value.Value?.Count ?? 0;

            if (TypeManager.IsEnableable(TypeIndex))
            {
                m_Enabled.RegisterValueChangedCallback((e) =>
                {
                    Context.EntityManager.SetComponentEnabled(Context.Entity, typeof(TElement), e.newValue);
                });
            }
        }

        protected override void OnComponentChanged(BindingContextElement element, PropertyPath path)
        {
            var buffer = element.GetTarget<InspectedBuffer<TList, TElement>>();
            var container = Container;

            if (container.IsReadOnly)
                return;

            PropertyContainer.SetValue(ref container, Path, buffer.Value);
            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
        }

        protected override void SetReadonly(VisualElement root)
        {
            // Buffers will generate a paginated list of elements. In read-only mode, we will selectively disable
            // specific elements to keep navigation enabled. This will only work for the top-level list. Nested paginated
            // lists will be completely disabled.
            root.Q(className: "unity-platforms__list-element__size")?.SetEnabled(false);
            root.Q("platforms-list-content")?.SetEnabled(false);
            root.Q<Button>(className: "unity-platforms__list-element__add-item-button")?.SetEnabled(false);
            root.Q<Toggle>(className: UssClasses.Inspector.Component.Enabled).SetEnabled(false);
        }

        protected override void OnPopulateMenu(DropdownMenu menu)
        {
            var buffer = m_Content.GetTarget<InspectedBuffer<TList, TElement>>();
            menu.AddCopyValue(buffer.Value);
        }

        void IBinding.PreUpdate()
        {
        }

        void IBinding.Update()
        {
            if (!Context.World.IsCreated || !Context.EntityManager.SafeExists(Container.Entity))
            {
                RemoveFromHierarchy();
                return;
            }

            if (TypeManager.IsEnableable(TypeIndex) && Context.EntityManager.HasComponent(Context.Entity, typeof(TElement)))
            {
                m_Enabled.visible = true;
                m_Enabled.SetValueWithoutNotify(Context.EntityManager.IsComponentEnabled(Context.Entity, typeof(TElement)));
            }
            else
            {
                m_Enabled.visible = false;
                m_Enabled.SetValueWithoutNotify(true);
            }

            var target = m_Content.GetTarget<InspectedBuffer<TList, TElement>>();
            if (null != target.Value && target.Value.Count != m_Count)
            {
                m_Count = target.Value.Count;
                m_Content.ForceReload();
                StylingUtility.AlignInspectorLabelWidth(m_Content);
            }
        }

        void IBinding.Release()
        {
        }
    }
}
