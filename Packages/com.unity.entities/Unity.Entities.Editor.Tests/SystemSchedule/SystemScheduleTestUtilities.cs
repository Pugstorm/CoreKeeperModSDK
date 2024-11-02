using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Editor.Bridge;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using TreeView = Unity.Editor.Bridge.TreeView;

namespace Unity.Entities.Editor.Tests
{
    static class SystemScheduleTreeViewExtension
    {
        public static bool CheckIfTreeViewContainsGivenSystemType(this SystemTreeView @this, Type systemType, out SystemTreeViewItem item)
        {
            if (@this.m_TreeViewRootItems == null || @this.m_TreeViewRootItems.Count == 0)
            {
                item = null;
                return false;
            }

            var systemName = systemType.Name;
            foreach (var rootItem in @this.m_TreeViewRootItems)
            {
                if (!(rootItem is SystemTreeViewItem systemTreeViewItem))
                {
                    item = null;
                    return false;
                }

                if (CheckIfTreeViewItemContainsSystem(systemTreeViewItem, systemName, out var outItem))
                {
                    item = outItem;
                    return true;
                }
            }

            item = null;
            return false;
        }

        static bool CheckIfTreeViewItemContainsSystem(SystemTreeViewItem item, string systemName, out SystemTreeViewItem outItem)
        {
            if (item.children.Any())
            {
                var itemName = item.GetSystemName();
                itemName = Regex.Replace(itemName, @"[(].*", string.Empty);
                itemName = Regex.Replace(itemName, @"\s+", string.Empty, RegexOptions.IgnoreCase).Trim();

                if (itemName.IndexOf(systemName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    outItem = item;
                    return true;
                }

                foreach (var childItem in item.children)
                {
                    if (CheckIfTreeViewItemContainsSystem(childItem as SystemTreeViewItem, systemName, out outItem))
                        return true;
                }
            }

            outItem = null;
            return false;
        }
    }

    static class SystemScheduleTestUtilities
    {
        public static SystemScheduleWindow CreateSystemsWindow()
        {
            var window = ScriptableObject.CreateInstance<SystemScheduleWindow>();
            window.Show();
            window.Update();
            return window;
        }

        public static void DestroySystemsWindow(SystemScheduleWindow window)
        {
            window.Close();
            Object.DestroyImmediate(window);
        }

        public static void CollectExpandedGroupNodeNames(SystemTreeView treeView, ITreeViewItem item, List<string> resultList)
        {
            if (!item.children.Any())
                return;

            var systemTreeView = treeView.Q<TreeView>();
            var systemTreeViewItem = item as SystemTreeViewItem;
            var itemName = systemTreeViewItem?.GetSystemName();

            if (systemTreeView.IsExpanded(item.id))
                resultList.Add(itemName);

            foreach (var child in item.children)
            {
                CollectExpandedGroupNodeNames(treeView, child, resultList);
            }
        }

        public static void ExpandAllGroupNodes(SystemTreeView treeView, ITreeViewItem item)
        {
            if (!item.children.Any())
                return;

            var systemTreeView = treeView.Q<TreeView>();
            if (!systemTreeView.IsExpanded(item.id))
                systemTreeView.ExpandItem(item.id);

            foreach (var child in item.children)
            {
                ExpandAllGroupNodes(treeView, child);
            }
        }

        public class UpdateSystemGraph : IEditModeTestYieldInstruction
        {
            const int k_WaitFrames = 6000;
            readonly SystemScheduleWindow m_SystemScheduleWindow;
            readonly Type m_GivenSystemType;
            int m_Count;

            public UpdateSystemGraph(Type systemType)
            {
                m_SystemScheduleWindow = EditorWindow.GetWindow<SystemScheduleWindow>();
                m_GivenSystemType = systemType;
            }

            public IEnumerator Perform()
            {
                var systemTreeView = m_SystemScheduleWindow.rootVisualElement.Q<SystemTreeView>();

                for (;;)
                {
                    if (m_GivenSystemType != null &&
                        systemTreeView.CheckIfTreeViewContainsGivenSystemType(m_GivenSystemType, out _)
                        || m_GivenSystemType == null && systemTreeView.m_TreeViewRootItems.Count > 0)
                        break;

                    if (++m_Count > k_WaitFrames)
                    {
                        throw new TimeoutException( m_GivenSystemType == null
                                                    ? $"System tree view is empty within {k_WaitFrames} frames."
                                                    : $"Expected system of type {m_GivenSystemType.Name} is not detected in system tree view within {k_WaitFrames} frames." );
                    }

                    yield return null;
                }
            }

            public bool ExpectDomainReload { get; }
            public bool ExpectedPlaymodeState { get; }
        }
    }
}
