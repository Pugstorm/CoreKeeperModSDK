using System;
using Unity.Editor.Bridge;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    static class QueryWindowHelper
    {
        // Todo: retrieve last dock area after domain reload
        static EditorWindowBridge.DockArea s_LastDockArea;

        public static void OpenNewWindow(World world, EntityQuery query, SystemProxy systemProxy, int queryOrder, EntityQueryContentTab tab)
        {
            var windowName = L10n.Tr("Query");

            Analytics.SendEditorEvent(Analytics.Window.Query, Analytics.EventType.WindowOpen);

            var wnd = SelectionUtility.CreateWindow(new EntityQueryContentProvider
            {
                World = world,
                Query = query,
                SystemProxy = systemProxy,
                QueryOrder = queryOrder,
                Tab = tab,
            }, new ContentWindowParameters
            {
                AddScrollView = false,
                ApplyInspectorStyling = false
            });

            wnd.titleContent = EditorGUIUtility.TrTextContent(windowName, EditorIcons.Query);
            wnd.minSize = Constants.MinWindowSize;
            
            if (s_LastDockArea is { IsValid: true })
            {
                s_LastDockArea.AddTab(wnd);
                wnd.Focus();
            }
            else
            {
                wnd.Show();
                s_LastDockArea = wnd.GetDockArea();
            }
        }
    }
}
