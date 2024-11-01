using Unity.Entities.UI;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class TagElement<TComponent> : ComponentElementBase, IBinding
    {
        readonly Toggle m_Enabled;

        public TagElement(IComponentProperty property, EntityInspectorContext context) : base(property, context)
        {
            binding = this;

            Resources.Templates.Inspector.TagComponentElement.Clone(this);
            this.Q<Label>().text = DisplayName;

            var icon = this.Q(className: UssClasses.Inspector.Component.Icon);
            icon.AddToClassList(UssClasses.Inspector.Component.Icon);
            icon.AddToClassList(UssClasses.Inspector.Icons.Small);
            icon.AddToClassList(UssClasses.Inspector.ComponentTypes.Tag);

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
            // Nothing to do..
        }

        protected override void OnPopulateMenu(DropdownMenu menu)
        {
            // Nothing to do..
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
        }

        void IBinding.Release()
        {
        }
    }
}
