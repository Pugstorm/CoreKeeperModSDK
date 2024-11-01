using System;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    class ContextualElement : VisualElement
    {
        public struct Item
        {
            public string ContextMenuLabel;
            public VisualElement Element;
        }

        class State
        {
            public int Index;
        }

        State m_State;
        Item[] m_Items;

        public ContextualElement(string sessionKey, params Item[] elements)
        {
            m_State = Unity.Serialization.Editor.SessionState<State>.GetOrCreate(sessionKey);
            m_Items = elements;

            focusable = true;
            pickingMode = PickingMode.Position;

            foreach (var item in m_Items)
            {
                Add(item.Element);
            }

            this.AddManipulator(new ContextualMenuManipulator(PopulateContextMenu));
            UpdateVisibility(m_State.Index);
        }

        void PopulateContextMenu(ContextualMenuPopulateEvent evt)
        {
            var menu = evt.menu;

            for (var i = 0; i < m_Items.Length; i++)
            {
                var local = i;

                menu.AppendAction(m_Items[i].ContextMenuLabel, e =>
                {
                    m_State.Index = local;
                    UpdateVisibility(m_State.Index);
                }, i == m_State.Index ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
        }

        void UpdateVisibility(int index)
        {
            for (var i = 0; i < m_Items.Length; i++)
            {
                m_Items[i].Element.SetVisibility(i == index);
            }
        }
    }
}
