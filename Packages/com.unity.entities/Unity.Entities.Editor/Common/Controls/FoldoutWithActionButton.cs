using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class FoldoutWithActionButton : FoldoutWithHeader
    {
        public VisualElement HeaderIcon;
        public Label HeaderName;
        public Label MatchingCount;
        public VisualElement ActionButtonContainer;
        public VisualElement ActionButton;
        bool m_IsButtonHovered;

        public FoldoutWithActionButton()
        {
            Resources.Templates.FoldoutWithActionButton.AddStyles(this);

            var toggleHeader = this.Q<Toggle>(className:"unity-foldout__toggle");
            toggleHeader.AddToClassList(UssClasses.FoldoutWithActionButton.Toggle);
            this.Q(className: "unity-toggle__input").AddToClassList(UssClasses.FoldoutWithActionButton.ToggleInput);

            Header = Resources.Templates.FoldoutWithActionButton.Clone();
            HeaderIcon = Header.Q(className: UssClasses.FoldoutWithActionButton.Icon);
            HeaderName = Header.Q<Label>(className: UssClasses.FoldoutWithActionButton.Name);
            MatchingCount = Header.Q<Label>(className: UssClasses.FoldoutWithActionButton.Count);
            ActionButton = Header.Q(className: UssClasses.FoldoutWithActionButton.Button);
            ActionButtonContainer = Header.Q(className: UssClasses.FoldoutWithActionButton.ButtonContainer);

            toggleHeader.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (m_IsButtonHovered)
                    return;

                toggleHeader.AddToClassList(UssClasses.FoldoutWithActionButton.ToggleHeaderHoverStyle);
            });

            toggleHeader.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (m_IsButtonHovered)
                    return;

                toggleHeader.RemoveFromClassList(UssClasses.FoldoutWithActionButton.ToggleHeaderHoverStyle);
            });

            ActionButtonContainer.RegisterCallback<MouseEnterEvent>(evt =>
            {
                m_IsButtonHovered = true;
                toggleHeader.RemoveFromClassList(UssClasses.FoldoutWithActionButton.ToggleHeaderHoverStyle);
                ActionButtonContainer.AddToClassList(UssClasses.FoldoutWithActionButton.ToggleHeaderHoverStyle);
            });

            ActionButtonContainer.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                m_IsButtonHovered = false;
                ActionButtonContainer.RemoveFromClassList(UssClasses.FoldoutWithActionButton.ToggleHeaderHoverStyle);
            });
        }
    }
}
