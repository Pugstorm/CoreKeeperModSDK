using System;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Diagnostics.GUI;
using UnityEditor.AddressableAssets.Diagnostics.GUI.Graph;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.Diagnostics;

namespace UnityEditor.AddressableAssets.Diagnostics
{
    /*
     * ResourceManager specific implementation of an EventViewerWindow
     */
    class ResourceProfilerWindow : EventViewerWindow
    {
        [MenuItem("Window/Asset Management/Addressables/Event Viewer", priority = 2051)]
        internal static void ShowWindow()
        {
            var setting = AddressableAssetSettingsDefaultObject.Settings;
            if (setting == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Attempting to open Addressables Event Viewer window, but no Addressables Settings file exists.  \n\nOpen 'Window/Asset Management/Addressables/Groups' for more info.", "Ok");
                return;
            }

            AddressableAnalytics.ReportUsageEvent(AddressableAnalytics.UsageEventType.OpenEventViewerWindow);
            var window = GetWindow<ResourceProfilerWindow>();
            window.titleContent = new GUIContent("Addressables Event Viewer", "Addressables Event Viewer");
            window.Show();
        }

        protected override bool ShowEventDetailPanel
        {
            get { return false; }
        }

        protected override bool ShowEventPanel
        {
            get { return true; }
        }

        protected static string GetDataStreamName(int stream)
        {
            return ((ResourceManager.DiagnosticEventType)stream).ToString();
        }

        protected override bool OnCanHandleEvent(string graph)
        {
            return graph == "ResourceManager";
        }

        protected override bool OnRecordEvent(DiagnosticEvent evt)
        {
            if (evt.Graph == "ResourceManager")
            {
                switch ((ResourceManager.DiagnosticEventType)evt.Stream)
                {
                    case ResourceManager.DiagnosticEventType.AsyncOperationCreate:
                    case ResourceManager.DiagnosticEventType.AsyncOperationDestroy:
                    case ResourceManager.DiagnosticEventType.AsyncOperationComplete:
                    case ResourceManager.DiagnosticEventType.AsyncOperationFail:
                        return true;
                }
            }

            return base.OnRecordEvent(evt);
        }

        protected override void OnDrawEventDetail(Rect rect, DiagnosticEvent evt)
        {
        }

        protected override void OnGetColumns(List<string> columnNames, List<string> tooltips, List<float> columnSizes)
        {
            if (columnNames == null || columnSizes == null)
                return;
            columnNames.AddRange(new[] {"Event", "Key"});
            tooltips.AddRange(new[] {"A diagnostic event sent by the ResourceManager", "Identifier for the event"});
            columnSizes.AddRange(new float[] {150, 400});
        }

        protected override bool OnDrawColumnCell(Rect cellRect, DiagnosticEvent evt, int column)
        {
            switch (column)
            {
                case 0:
                    EditorGUI.LabelField(cellRect, ((ResourceManager.DiagnosticEventType)evt.Stream).ToString());
                    break;
                case 1:
                    EditorGUI.LabelField(cellRect, evt.DisplayName);
                    break;
            }

            return true;
        }

        protected override void OnInitializeGraphView(EventGraphListView graphView)
        {
            if (graphView == null)
                return;

            Color labelBgColor = GraphColors.LabelGraphLabelBackground;

            Color refCountBgColor = new Color(53 / 255f, 136 / 255f, 167 / 255f, 1);
            Color loadingBgColor = Color.Lerp(refCountBgColor, GraphColors.WindowBackground, 0.5f);
            Color refCountColor = new Color(123 / 255f, 158 / 255f, 6 / 255f, 1);

            //Each DefineGraph call makes an association between a "name" (e.g. EventCount) and all EventDataSets with a matching "graph" member.
            //These graphs are then used to determine how/what the graphView will draw when dealing with samples from its associated EventDataSets

            graphView.DefineGraph("EventCount", 0, new GraphLayerVertValueLine(0, "EventCount", "Event Counts", Color.green),
                new GraphLayerLabel(0, "EventCount", "Number of instantiations on current frame", refCountColor, GraphColors.LabelGraphLabelBackground, v => v.ToString()));

            graphView.DefineGraph("InstantiationCount", 0, new GraphLayerVertValueLine(0, "InstantiationCount", "Instantiation Counts", Color.green),
                new GraphLayerLabel(0, "InstantiationCount", "Number of instantiations on current frame", refCountColor, GraphColors.LabelGraphLabelBackground, v => v.ToString()));

            graphView.DefineGraph("ResourceManager", (int)ResourceManager.DiagnosticEventType.AsyncOperationReferenceCount,
                new GraphLayerBackgroundGraph((int)ResourceManager.DiagnosticEventType.AsyncOperationReferenceCount, refCountBgColor,
                    (int)ResourceManager.DiagnosticEventType.AsyncOperationPercentComplete, loadingBgColor, "LoadPercent", "Loaded"),
                new GraphLayerBarChartMesh((int)ResourceManager.DiagnosticEventType.AsyncOperationReferenceCount, "RefCount", "Reference Count", refCountColor),
                new GraphLayerEventMarker((int)ResourceManager.DiagnosticEventType.AsyncOperationCreate, "", "", Color.grey, Color.grey),
                new GraphLayerEventMarker((int)ResourceManager.DiagnosticEventType.AsyncOperationComplete, "", "", Color.white, Color.white),
                new GraphLayerEventMarker((int)ResourceManager.DiagnosticEventType.AsyncOperationDestroy, "", "", Color.black, Color.black),
                new GraphLayerEventMarker((int)ResourceManager.DiagnosticEventType.AsyncOperationFail, "", "", Color.red, Color.red),
                new GraphLayerLabel((int)ResourceManager.DiagnosticEventType.AsyncOperationReferenceCount, "RefCount", "Reference Count", refCountColor, labelBgColor, v => v.ToString())
            );
        }
    }
}
