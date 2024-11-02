using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class FoldoutWithoutActionButton : FoldoutWithHeader
    {
        public Label HeaderName;
        public Label MatchingCount;

        public FoldoutWithoutActionButton()
        {
            Resources.Templates.FoldoutWithoutActionButton.AddStyles(this);
            this.Q(className: "unity-toggle__input").AddToClassList(UssClasses.FoldoutWithoutActionButton.ToggleInput);

            Header = Resources.Templates.FoldoutWithoutActionButton.Clone();
            HeaderName = Header.Q<Label>(className: UssClasses.FoldoutWithoutActionButton.Name);
            MatchingCount = Header.Q<Label>(className: UssClasses.FoldoutWithoutActionButton.Count);
        }
    }
}
