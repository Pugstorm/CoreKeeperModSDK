using System;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class EntityView : VisualElement
    {
        readonly Label m_EntityName;
        EntityViewData m_Data;

        public EntityView(EntityViewData data)
        {
            Resources.Templates.EntityView.Clone(this);

            m_EntityName = this.Q<Label>(className: UssClasses.EntityView.EntityName);
            if (data.Entity != default)
                Update(data);
            this.Q<VisualElement>(className: UssClasses.EntityView.GoTo)
                .RegisterCallback<MouseDownEvent, EntityView>((_, @this) =>
                {
                    Analytics.SendEditorEvent(Analytics.Window.Inspector, Analytics.EventType.RelationshipGoTo, Analytics.GoToEntityDestination);
                    EntitySelectionProxy.SelectEntity(@this.m_Data.World, @this.m_Data.Entity);
                }, this);
        }

        public void Update(EntityViewData data)
        {
            m_Data = data;
            m_EntityName.text = data.EntityName;
        }
    }
}
