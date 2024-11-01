using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class SystemDependencyView : VisualElement
    {
        SystemDependencyViewData m_Data;
        readonly Label m_SystemName;

        public SystemDependencyView(in SystemDependencyViewData data)
        {
            Resources.Templates.SystemDependencyView.Clone(this);
            m_SystemName = this.Q<Label>(className: UssClasses.SystemDependencyView.Name);
            this.Q(className: UssClasses.SystemDependencyView.GotoButtonContainer).RegisterCallback<MouseDownEvent, SystemDependencyView>((evt, @this) =>
            {
                SystemScheduleWindow.HighlightSystem(@this.m_Data.SystemProxy);
                ContentUtilities.ShowSystemInspectorContent(@this.m_Data.SystemProxy);
            }, this);

            Update(data);
        }

        public void Update(in SystemDependencyViewData data)
        {
            if (m_Data.Equals(data))
                return;

            m_Data = data;
            m_SystemName.text = data.SystemName;
        }
    }
}
