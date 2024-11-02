using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    // We need a custom type because `isSelectable` is not available through UXML
#if UNITY_2023_3_OR_NEWER
    [UxmlElement]
#endif
    partial class SelectableLabel : Label
    {
#if !UNITY_2023_3_OR_NEWER
        new class UxmlFactory : UxmlFactory<SelectableLabel, UxmlTraits> { }
        new class UxmlTraits : Label.UxmlTraits { }
#endif

        public SelectableLabel() : this(string.Empty) { }
        public SelectableLabel(string text) : base(text) => ((ITextSelection)this).isSelectable = true;
    }
}
