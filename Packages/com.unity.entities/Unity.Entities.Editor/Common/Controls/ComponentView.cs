using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class ComponentView : VisualElement
    {
        static readonly string k_ReadOnly = L10n.Tr("Read");
        static readonly string k_ReadWrite = L10n.Tr("Read & Write");
        static readonly string k_Exclude = L10n.Tr("Exclude");

        ComponentViewData m_Data;
        readonly Label m_ComponentName;
        public Label m_AccessMode;
        readonly VisualElement m_ComponentIcon;

        public ComponentView(in ComponentViewData data)
        {
            Resources.Templates.ComponentView.Clone(this);
            m_ComponentIcon = this.Q(className: UssClasses.ComponentView.Icon);
            m_ComponentName = this.Q<Label>(className: UssClasses.ComponentView.Name);
            m_AccessMode = this.Q<Label>(className: UssClasses.ComponentView.AccessMode);
            this.Q(className: UssClasses.ComponentView.GoTo).RegisterCallback<MouseDownEvent, ComponentView>((evt, @this) =>
            {
                evt.StopPropagation();
#if !UNITY_2023_2_OR_NEWER
                evt.PreventDefault();
#endif
                Analytics.SendEditorEvent(Analytics.Window.Inspector, Analytics.EventType.RelationshipGoTo, Analytics.GoToComponentDestination);
                ComponentsWindow.HighlightComponent(@this.m_Data.InComponentType);
                ContentUtilities.ShowComponentInspectorContent(@this.m_Data.InComponentType);
            }, this);

            Update(data);
        }

        public void Update(in ComponentViewData data)
        {
            if (m_Data.Equals(data))
                return;

            UpdateIcon(m_Data.Kind, data.Kind);
            m_Data = data;
            m_ComponentName.text = data.Name;

            m_AccessMode.text = data.AccessMode switch
            {
                ComponentType.AccessMode.ReadOnly => k_ReadOnly,
                ComponentType.AccessMode.ReadWrite => k_ReadWrite,
                ComponentType.AccessMode.Exclude => k_Exclude,
                _ => string.Empty
            };
        }

        static string GetClassForKind(ComponentViewData.ComponentKind kind) => kind switch
        {
            ComponentViewData.ComponentKind.Tag => "tag",
            ComponentViewData.ComponentKind.Buffer => "buffer",
            ComponentViewData.ComponentKind.Shared => "shared",
            ComponentViewData.ComponentKind.Chunk => "chunk",
            ComponentViewData.ComponentKind.Managed => "managed",
            _ => string.Empty
        };

        void UpdateIcon(ComponentViewData.ComponentKind previousKind, ComponentViewData.ComponentKind newKind)
        {
            if (previousKind == newKind)
                return;

            m_ComponentIcon.RemoveFromClassList(GetClassForKind(previousKind));
            m_ComponentIcon.AddToClassList(GetClassForKind(newKind));
        }
    }
}
