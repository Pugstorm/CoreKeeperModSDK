using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class FoldoutWithHeader : Foldout
    {
        VisualElement m_Header;
        string m_Text;

        public FoldoutWithHeader()
        {
            this.Q<Toggle>().Q(className: Toggle.inputUssClassName).style.flexGrow = 0;
        }

        public new string text
        {
            get => m_Text;
            set
            {
                m_Text = value;
                Header = new Label(m_Text);
            }
        }

        public VisualElement Header
        {
            get => m_Header;
            set
            {
                m_Header?.RemoveFromHierarchy();
                m_Header = value;

                this.Q<Toggle>().Add(m_Header);
            }
        }
    }
}
