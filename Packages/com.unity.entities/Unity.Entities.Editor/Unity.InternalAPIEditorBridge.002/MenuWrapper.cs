using System;
using UnityEditor;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace Unity.Editor.Bridge
{
    class MenuWrapper
    {
        readonly GenericMenu m_GenericMenu = new GenericMenu();

        public GenericMenu GenericMenu
        {
            get
            {
                Assert.IsTrue(m_GenericMenu.GetItemCount() == 0);
                return m_GenericMenu;
            }
        }

        public void ApplyGenericMenuItemsTo(DropdownMenu menu)
        {
            var menuItems = m_GenericMenu.menuItems;

            for (var i = 0; i < menuItems.Count; i++)
            {
                var menuItem = menuItems[i];

                if (menuItem.separator)
                {
                    if (i < menuItems.Count - 1)
                        menu.AppendSeparator(menuItem.content.text);
                }
                else if (menuItem.userData != null)
                    menu.AppendAction(menuItem.content.text, a => menuItem.func2(a.userData), _ => menuItem.func2 == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal, menuItem.userData);
                else
                    menu.AppendAction(menuItem.content.text, a => menuItem.func(), menuItem.func == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            }

            m_GenericMenu.menuItems.Clear();
        }
    }
}
