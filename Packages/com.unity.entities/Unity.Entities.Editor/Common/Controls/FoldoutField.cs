using System.Linq;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
#if UNITY_2023_3_OR_NEWER
    [UxmlElement]
#endif
    partial class FoldoutField : VisualElement
    {
#if !UNITY_2023_3_OR_NEWER
        class FoldoutFieldFactory : UxmlFactory<FoldoutField, FoldoutFieldTraits> { }
        class FoldoutFieldTraits : UxmlTraits { }
#endif
        const string s_UssClassName = "foldout-field";
        const string s_IconOpenClassName = s_UssClassName + "__icon-open";
        const string s_IconClosedClassName = s_UssClassName + "__icon-closed";

        readonly Image m_Icon;
        readonly Label m_Label;
        readonly VisualElement m_Value;
        readonly VisualElement m_Content;
        bool m_Open;

        public bool open
        {
            get => m_Open;
            set
            {
                m_Open = value;
                if (m_Open)
                {
                    m_Icon.RemoveFromClassList(s_IconClosedClassName);
                    m_Icon.AddToClassList(s_IconOpenClassName);
                    m_Content.SetVisibility(true);
                }
                else
                {
                    m_Icon.RemoveFromClassList(s_IconOpenClassName);
                    m_Icon.AddToClassList(s_IconClosedClassName);
                    m_Content.SetVisibility(false);
                }
            }
        }

        public string text
        {
            get => m_Label.text;
            set => m_Label.text = value;
        }

        public VisualElement value
        {
            get => m_Value.Children().FirstOrDefault();
            set
            {
                m_Value.Clear();
                m_Value.Add(value);
            }
        }

        public override VisualElement contentContainer => m_Content;

        public FoldoutField()
        {
            var template = PackageResources.LoadTemplate("Controls/foldout-field");
            var foldout = template.Clone(this);
            var toggle = foldout.Q("toggle");

            toggle.RegisterCallback<ClickEvent>((e) =>
            {
                open = !open;
                e.StopPropagation();
            });

            m_Icon = toggle.Q<Image>("icon");
            m_Label = toggle.Q<Label>("label");
            m_Value = toggle.Q("value");
            m_Content = foldout.Q("content");

            open = m_Open;
        }
    }
}
