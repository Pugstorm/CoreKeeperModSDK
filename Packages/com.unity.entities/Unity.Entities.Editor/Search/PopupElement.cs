using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class PopupElement : VisualElement
    {
        class Content : PopupWindowContent
        {
            readonly PopupElement m_Element;

            public Content(PopupElement element)
            {
                m_Element = element;
            }

            public override void OnGUI(Rect rect)
            {
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                    editorWindow.Close();
            }

            public override Vector2 GetWindowSize() => m_Element.GetSize();

            public override void OnOpen()
            {
                editorWindow.rootVisualElement.Add(m_Element);
            }
        }

        protected virtual Vector2 GetSize() => worldBound.size;

        Content m_Content;

        public void ShowAtPosition(Rect activatorRect)
        {
            m_Content = new Content(this);
            UnityEditor.PopupWindow.Show(activatorRect, m_Content);
        }

        public void Close()
        {
            m_Content.editorWindow.Close();
        }
    }
}
