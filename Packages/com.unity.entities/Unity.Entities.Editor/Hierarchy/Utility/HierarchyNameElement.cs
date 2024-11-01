using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
#if UNITY_2023_3_OR_NEWER
    [UxmlElement]
#endif
    partial class HierarchyNameElement : VisualElement
    {
#if !UNITY_2023_3_OR_NEWER
        [UsedImplicitly]
        class HierarchyNameElementFactory : UxmlFactory<HierarchyNameElement, HierarchyNameElementTraits> { }

        [UsedImplicitly]
        class HierarchyNameElementTraits : UxmlTraits { }
#endif

        public string Text
        {
            get => Label.text;
            set => Label.text = value;
        }

        bool IsRenaming { get; set; }

        public event Action<HierarchyNameElement, bool> OnRename;

        public Label Label { get; } = new();
        TextField TextField { get; } = new();

        public HierarchyNameElement()
        {
            AddToClassList(UssClasses.Hierarchy.Item.Name);

            focusable = true;
            delegatesFocus = false;

            Add(Label);
            Add(TextField);

            TextField.selectAllOnFocus = true;
            TextField.selectAllOnMouseUp = false;
            TextField.style.display = DisplayStyle.None;

            TextField.RegisterCallback<MouseUpEvent>(OnMouseUpEvent);
            TextField.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
            TextField.RegisterCallback<BlurEvent>(OnBlurEvent);
        }

        public void BeginRename()
        {
            if (IsRenaming)
                return;

            IsRenaming = true;
            delegatesFocus = true;

            Label.style.display = DisplayStyle.None;
            TextField.style.display = DisplayStyle.Flex;

            TextField.value = Text;
            TextField.Q<TextElement>().Focus();
        }

        public void CancelRename()
        {
            if (IsRenaming)
                EndRename(true);
        }

        void EndRename(bool canceled = false)
        {
            IsRenaming = false;
            delegatesFocus = false;
            schedule.Execute(Focus);

            TextField.style.display = DisplayStyle.None;
            Label.style.display = DisplayStyle.Flex;

            if (!canceled) // When the rename is canceled, the label keep its current value.
                Label.text = TextField.value;

            OnRename?.Invoke(this, canceled);
        }

        void OnMouseUpEvent(MouseUpEvent evt)
        {
            if (!IsRenaming)
                return;

            TextField.Q<TextElement>().Focus();
            evt.StopPropagation();
        }

        void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (IsRenaming && evt.keyCode == KeyCode.Escape)
                EndRename(true);
        }

        void OnBlurEvent(BlurEvent evt)
        {
            if (!IsRenaming)
                return;

            EndRename();
        }
    }
}
