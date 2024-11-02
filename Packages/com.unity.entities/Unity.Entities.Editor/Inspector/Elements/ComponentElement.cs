using Unity.Entities.UI;
using Unity.Properties;
using Unity.Scenes;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    sealed class ComponentElement<TComponent> : ComponentElementBase, IBinding
    {
        readonly PropertyElement m_Content;
        readonly Toggle m_Enabled;

        public ComponentElement(IComponentProperty property, EntityInspectorContext context, ref TComponent value) : base(property, context)
        {
            binding = this;
            m_Content = CreateContent(property, ref value);
            m_Enabled = this.Q<Toggle>(className: UssClasses.Inspector.Component.Enabled);

            if (TypeManager.IsEnableable(TypeIndex))
            {
                m_Enabled.RegisterValueChangedCallback((e) =>
                {
                    Context.EntityManager.SetComponentEnabled(Context.Entity, typeof(TComponent), e.newValue);
                });
            }
        }

        protected override void OnComponentChanged(BindingContextElement element, PropertyPath path)
        {
            var component = element.GetTarget<TComponent>();
            var container = Container;

            if (container.IsReadOnly)
                return;

            PropertyContainer.SetValue(ref container, Path, component);
            EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
        }

        protected override void OnPopulateMenu(DropdownMenu menu)
        {
            var container = Container;
            var value = PropertyContainer.GetValue<EntityContainer, TComponent>(ref container, Path);
            menu.AddCopyValue(value);

            var query = SearchUtils.CreateComponentQuery(value);
            menu.AppendAction("Open in Hierarchy...", (a) =>
            {
                HierarchyWindow.OpenWindow(query);
            }, DropdownMenuAction.AlwaysEnabled);
            menu.AppendAction("Open in Search Window...", (a) =>
            {
                HierarchySearchProvider.OpenProvider(query);
            }, DropdownMenuAction.AlwaysEnabled);
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

            if (TypeManager.IsEnableable(TypeIndex) && Context.EntityManager.HasComponent(Context.Entity, typeof(TComponent)))
            {
                m_Enabled.visible = true;
                m_Enabled.SetValueWithoutNotify(Context.EntityManager.IsComponentEnabled(Context.Entity, typeof(TComponent)));
            }
            else
            {
                m_Enabled.visible = false;
                m_Enabled.SetValueWithoutNotify(true);
            }

            var container = Container;
            if (PropertyContainer.TryGetValue<EntityContainer, TComponent>(ref container, Path, out var component))
                m_Content.SetTarget(component);
        }

        void IBinding.Release()
        {
        }
    }
}
